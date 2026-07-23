"""Extract and normalize the pinned P3-08 Downtown City MegaKit representatives.

Run with Blender so the same FBX importer/exporter implementation is used by
every contributor:

blender --background --factory-startup --python \
  Blender/Scripts/Importers/import_downtown_city_representatives.py -- \
  --archive "/path/to/Downtown City MegaKit[Standard].zip" \
  --manifest Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard/Provenance/acquisition-manifest.json \
  --output-root Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import math
import sys
import zipfile
from pathlib import Path, PurePosixPath

import bmesh
import bpy
from io_scene_fbx import export_fbx_bin, fbx_utils
from mathutils import Matrix, Vector

_ORIGINAL_FBX_HEADER_ELEMENTS = export_fbx_bin.fbx_header_elements


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    return parser.parse_args(argv)


def sha256_path(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_if_changed(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == content:
        return
    path.write_bytes(content)


def stable_fbx_uuid(uuids: dict, key: object) -> fbx_utils.UUID:
    """Generate stable FBX object IDs instead of Python's randomized hash IDs."""
    if isinstance(key, int) and 0 <= key < 2**63:
        value = key
    else:
        encoded = repr(key).encode("utf-8")
        value = int.from_bytes(hashlib.sha256(encoded).digest()[:8], "big")
        value &= (1 << 63) - 1
    if value > 1_000_000_000:
        compact = value % 1_000_000_000
        if compact not in uuids:
            value = compact
    while value in uuids:
        value += 1
        if value >= 2**63:
            raise ValueError(f"Unable to generate a stable FBX UUID for {key!r}")
    return fbx_utils.UUID(value)


def stable_fbx_header(root: object, scene_data: object, time: object = None) -> None:
    del time
    _ORIGINAL_FBX_HEADER_ELEMENTS(
        root,
        scene_data,
        datetime.datetime(1970, 1, 1),
    )


def basename_fbx_texture_path(image: object, scene_data: object) -> tuple[str, str]:
    del scene_data
    file_name = Path(bpy.path.abspath(image.filepath)).name or Path(image.name).name
    return file_name, file_name


def reset_deterministic_fbx_ids() -> None:
    # Blender's stock exporter derives FBX IDs from Python hash(), whose seed is
    # randomized by Blender even when PYTHONHASHSEED is set. Patch the private
    # helper used by Blender 5.1.2 and clear its process-global caches before
    # each export so clean invocations produce byte-identical FBX files.
    fbx_utils._key_to_uuid = stable_fbx_uuid
    fbx_utils._keys_to_uuids.clear()
    fbx_utils._uuids_to_keys.clear()
    fbx_utils.ObjectWrapper.cache_clear()
    export_fbx_bin.fbx_header_elements = stable_fbx_header
    export_fbx_bin._gen_vid_path = basename_fbx_texture_path


def safe_relative(path: str) -> Path:
    posix = PurePosixPath(path)
    if posix.is_absolute() or ".." in posix.parts:
        raise ValueError(f"Unsafe manifest path: {path}")
    return Path(*posix.parts)


def extract_pinned_files(
    archive_path: Path, output_root: Path, manifest: dict
) -> None:
    archive_hash = sha256_path(archive_path)
    expected_hash = manifest["archiveSha256"].lower()
    if archive_hash != expected_hash:
        raise ValueError(
            f"Archive SHA-256 mismatch: expected {expected_hash}, got {archive_hash}"
        )
    if archive_path.stat().st_size != manifest["archiveBytes"]:
        raise ValueError("Archive byte length does not match the acquisition manifest.")

    wanted: dict[str, tuple[Path, str]] = {}
    license_row = manifest["license"]
    wanted[license_row["archiveEntry"]] = (
        safe_relative(license_row["repositoryPath"]),
        license_row["sha256"].lower(),
    )
    for asset in manifest["selection"]:
        wanted[asset["sourceEntry"]] = (
            safe_relative(asset["sourcePath"]),
            asset["sourceSha256"].lower(),
        )
    for texture in manifest["requiredTextures"]:
        wanted[texture["archiveEntry"]] = (
            safe_relative(texture["repositoryPath"]),
            texture["sha256"].lower(),
        )

    with zipfile.ZipFile(archive_path, "r") as archive:
        archive_names = set(archive.namelist())
        missing = sorted(set(wanted) - archive_names)
        if missing:
            raise ValueError(f"Pinned archive entries are missing: {missing}")

        for entry_name, (relative_path, expected_entry_hash) in sorted(wanted.items()):
            content = archive.read(entry_name)
            actual_hash = hashlib.sha256(content).hexdigest()
            if actual_hash != expected_entry_hash:
                raise ValueError(
                    f"Entry SHA-256 mismatch for {entry_name}: "
                    f"expected {expected_entry_hash}, got {actual_hash}"
                )
            write_if_changed(output_root / relative_path, content)


def mesh_objects() -> list[bpy.types.Object]:
    return sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ vertex.co
        for obj in objects
        for vertex in obj.data.vertices
    ]
    if not points:
        raise ValueError("Imported FBX contains no mesh bounds.")
    minimum = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    return minimum, maximum


def metrics(objects: list[bpy.types.Object]) -> dict:
    minimum, maximum = bounds(objects)
    triangles = 0
    material_slots = 0
    materials: list[str] = []
    for obj in objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        material_slots += len(obj.material_slots)
        for slot in obj.material_slots:
            if slot.material is not None and slot.material.name not in materials:
                materials.append(slot.material.name)
    dimensions = maximum - minimum
    return {
        "meshCount": len(objects),
        "dimensionsMeters": [round(value, 4) for value in dimensions],
        "minimumMeters": [round(value, 4) for value in minimum],
        "maximumMeters": [round(value, 4) for value in maximum],
        "triangles": triangles,
        "materialSlots": material_slots,
        "materials": materials,
        "objectTransforms": [
            {
                "name": obj.name,
                "location": [round(value, 6) for value in obj.location],
                "rotationDegrees": [
                    round(math.degrees(value), 6) for value in obj.rotation_euler
                ],
                "scale": [round(value, 6) for value in obj.scale],
            }
            for obj in objects
        ],
    }


def relink_textures(
    output_root: Path, asset: dict
) -> tuple[list[str], list[str]]:
    expected_names = set(asset["textures"])
    relinked: list[str] = []
    original_paths: list[str] = []
    for image in sorted(bpy.data.images, key=lambda value: value.name):
        if image.source != "FILE":
            continue
        original_paths.append(image.filepath)
        basename = Path(image.filepath.replace("\\", "/")).name
        if basename not in expected_names:
            continue
        target = output_root / "Textures" / basename
        if not target.is_file():
            raise ValueError(f"Texture relink target is missing: {target}")
        image.filepath = str(target.resolve())
        image.filepath_raw = str(target.resolve())
        image.reload()
        relinked.append(basename)

    missing = sorted(expected_names - set(relinked))
    if missing:
        raise ValueError(
            f"{asset['sourceEntry']} did not relink expected textures: {missing}"
        )
    return original_paths, sorted(set(relinked))


def normalize_geometry(objects: list[bpy.types.Object]) -> None:
    for obj in objects:
        obj.data.transform(obj.matrix_world)
        obj.data.update()
        obj.matrix_world = Matrix.Identity(4)

    minimum, maximum = bounds(objects)
    offset = Vector(
        (
            -((minimum.x + maximum.x) * 0.5),
            -((minimum.y + maximum.y) * 0.5),
            -minimum.z,
        )
    )
    translation = Matrix.Translation(offset)
    for obj in objects:
        obj.data.transform(translation)
        obj.data.update()
        obj.location = Vector((0.0, 0.0, 0.0))
        obj.rotation_euler = Vector((0.0, 0.0, 0.0))
        obj.scale = Vector((1.0, 1.0, 1.0))


def triangulate_geometry(objects: list[bpy.types.Object]) -> None:
    for obj in objects:
        edit_mesh = bmesh.new()
        edit_mesh.from_mesh(obj.data)
        bmesh.ops.triangulate(edit_mesh, faces=list(edit_mesh.faces))
        edit_mesh.to_mesh(obj.data)
        edit_mesh.free()
        obj.data.update()


def convert_geometry_to_unity_axes(objects: list[bpy.types.Object]) -> None:
    conversion = Matrix.Rotation(math.radians(-90.0), 4, "X")
    for obj in objects:
        obj.data.transform(conversion)
        obj.data.update()


def assert_expected_metrics(
    asset: dict, observed: dict, unity_axes: bool = False
) -> None:
    source_dimensions = asset["expectedDimensionsMeters"]
    expected_dimensions = (
        [source_dimensions[0], source_dimensions[2], source_dimensions[1]]
        if unity_axes
        else source_dimensions
    )
    for index, expected in enumerate(expected_dimensions):
        actual = observed["dimensionsMeters"][index]
        if abs(actual - expected) > 0.02:
            raise ValueError(
                f"{asset['assetId']} dimension[{index}] expected {expected}, got {actual}"
            )
    if observed["triangles"] != asset["triangles"]:
        raise ValueError(
            f"{asset['assetId']} expected {asset['triangles']} triangles, "
            f"got {observed['triangles']}"
        )
    if observed["materialSlots"] != asset["materialSlots"]:
        raise ValueError(
            f"{asset['assetId']} expected {asset['materialSlots']} material slots, "
            f"got {observed['materialSlots']}"
        )


def export_normalized(
    source_path: Path, normalized_path: Path, output_root: Path, asset: dict
) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.scene.unit_settings.length_unit = "METERS"
    result = bpy.ops.import_scene.fbx(
        filepath=str(source_path.resolve()),
        use_image_search=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender FBX import failed for {source_path}")

    objects = mesh_objects()
    raw_metrics = metrics(objects)
    assert_expected_metrics(asset, raw_metrics)
    original_texture_paths, relinked = relink_textures(output_root, asset)
    normalize_geometry(objects)
    triangulate_geometry(objects)
    convert_geometry_to_unity_axes(objects)
    normalized_metrics = metrics(objects)
    assert_expected_metrics(asset, normalized_metrics, unity_axes=True)

    minimum = normalized_metrics["minimumMeters"]
    maximum = normalized_metrics["maximumMeters"]
    if abs(minimum[1]) > 0.001:
        raise ValueError(f"{asset['assetId']} bottom pivot was not normalized to Y=0.")
    if abs(minimum[0] + maximum[0]) > 0.001:
        raise ValueError(f"{asset['assetId']} X bounds are not centered on the pivot.")
    if abs(minimum[2] + maximum[2]) > 0.001:
        raise ValueError(f"{asset['assetId']} Z bounds are not centered on the pivot.")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    normalized_path.parent.mkdir(parents=True, exist_ok=True)
    reset_deterministic_fbx_ids()
    result = bpy.ops.export_scene.fbx(
        filepath=str(normalized_path.resolve()),
        check_existing=False,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        # Unity recalculates MikkTSpace tangents on import. Omitting Blender's
        # generated tangent layer avoids nondeterministic ordering in complex,
        # multi-material exports while preserving the runtime shading contract.
        use_tspace=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
        global_scale=1.0,
        apply_unit_scale=True,
        # Store Blender's meter-to-FBX conversion in the FBX unit scale instead
        # of object transforms. Unity then imports at global/root scale 1.
        apply_scale_options="FBX_SCALE_UNITS",
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender FBX export failed for {normalized_path}")

    return {
        "assetId": asset["assetId"],
        "prefabKey": asset["prefabKey"],
        "sourcePath": asset["sourcePath"],
        "sourceSha256": sha256_path(source_path),
        "normalizedPath": asset["normalizedPath"],
        "normalizedSha256": sha256_path(normalized_path),
        "originalTexturePaths": original_texture_paths,
        "relinkedTextures": relinked,
        "raw": raw_metrics,
        "normalized": normalized_metrics,
    }


def main() -> None:
    args = parse_args()
    archive_path = args.archive.resolve()
    manifest_path = args.manifest.resolve()
    output_root = args.output_root.resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    extract_pinned_files(archive_path, output_root, manifest)
    rows = []
    for asset in manifest["selection"]:
        source_path = output_root / safe_relative(asset["sourcePath"])
        normalized_path = output_root / safe_relative(asset["normalizedPath"])
        rows.append(export_normalized(source_path, normalized_path, output_root, asset))
        print(
            "P3_08_IMPORTED "
            f"{asset['assetId']} triangles={asset['triangles']} "
            f"materialSlots={asset['materialSlots']} "
            f"normalized={asset['normalizedPath']}"
        )

    report = {
        "schemaVersion": 1,
        "assetPack": manifest["assetPack"],
        "archiveSha256": manifest["archiveSha256"],
        "blenderVersion": bpy.app.version_string,
        "unitContract": "1 Blender unit = 1 meter = 1 Unity unit",
        "axisContract": "Blender Z-up exported as Unity Y-up (-Z forward, Y up)",
        "pivotContract": "bottom-center",
        "gridCellMeters": 8,
        "assets": rows,
    }
    report_path = output_root / "Provenance" / "normalization-report.json"
    report_text = json.dumps(report, indent=2, sort_keys=True) + "\n"
    write_if_changed(report_path, report_text.encode("utf-8"))
    print(f"P3_08_NORMALIZATION_REPORT {report_path}")


if __name__ == "__main__":
    main()
