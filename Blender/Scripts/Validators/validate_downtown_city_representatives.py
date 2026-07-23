"""Validate the committed P3-08 representative vendor slice in Blender."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path, PurePosixPath

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    return parser.parse_args(argv)


def safe_relative(path: str) -> Path:
    posix = PurePosixPath(path)
    if posix.is_absolute() or ".." in posix.parts:
        raise ValueError(f"Unsafe manifest path: {path}")
    return Path(*posix.parts)


def sha256_path(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def exact_case_file(path: Path) -> bool:
    absolute = path.absolute()
    current = Path(absolute.anchor)
    for part in absolute.parts[1:]:
        if not current.is_dir():
            return False
        match = next((child for child in current.iterdir() if child.name == part), None)
        if match is None:
            return False
        current = match
    return current.is_file()


def mesh_objects() -> list[bpy.types.Object]:
    return sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )


def inspect_normalized(path: Path) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(path.resolve()), use_image_search=False)
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender FBX import failed for {path}")
    objects = mesh_objects()
    if not objects:
        raise ValueError(f"Normalized FBX contains no meshes: {path}")

    points = [
        obj.matrix_world @ vertex.co
        for obj in objects
        for vertex in obj.data.vertices
    ]
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
    triangles = 0
    material_slots = 0
    transforms = []
    for obj in objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        material_slots += len(obj.material_slots)
        transforms.append(
            {
                "name": obj.name,
                "rotationDegrees": [
                    round(math.degrees(value), 6) for value in obj.rotation_euler
                ],
                "scale": [round(value, 6) for value in obj.scale],
            }
        )
    dimensions = maximum - minimum
    return {
        "meshCount": len(objects),
        "dimensionsMeters": [round(value, 4) for value in dimensions],
        "minimumMeters": [round(value, 4) for value in minimum],
        "maximumMeters": [round(value, 4) for value in maximum],
        "triangles": triangles,
        "materialSlots": material_slots,
        "objectTransforms": transforms,
    }


def validate_asset(output_root: Path, asset: dict) -> tuple[dict, list[str]]:
    issues: list[str] = []
    source_path = output_root / safe_relative(asset["sourcePath"])
    normalized_path = output_root / safe_relative(asset["normalizedPath"])
    if not exact_case_file(source_path):
        issues.append(f"Missing or case-mismatched source path: {source_path}")
    elif sha256_path(source_path) != asset["sourceSha256"].lower():
        issues.append(f"Source SHA-256 mismatch: {source_path}")

    if not exact_case_file(normalized_path):
        issues.append(f"Missing or case-mismatched normalized path: {normalized_path}")
        return {}, issues

    content = normalized_path.read_bytes()
    windows_absolute_path = re.search(
        rb"[A-Za-z]:\\(?:[A-Za-z0-9_. -]+\\)+[A-Za-z0-9_. -]+",
        content,
    )
    unix_absolute_path = re.search(
        rb"/(?:Users|home)/[A-Za-z0-9_.-]+/",
        content,
    )
    if windows_absolute_path or unix_absolute_path:
        issues.append(f"Normalized FBX retains an absolute author texture path: {normalized_path}")
    for texture_name in asset["textures"]:
        if texture_name.encode("utf-8") not in content:
            issues.append(
                f"Normalized FBX does not reference texture basename {texture_name}: "
                f"{normalized_path}"
            )

    observed = inspect_normalized(normalized_path)
    source_dimensions = asset["expectedDimensionsMeters"]
    expected_dimensions = [
        source_dimensions[0],
        source_dimensions[2],
        source_dimensions[1],
    ]
    for index, expected in enumerate(expected_dimensions):
        actual = observed["dimensionsMeters"][index]
        if abs(actual - expected) > 0.02:
            issues.append(
                f"{asset['assetId']} dimension[{index}] expected {expected}, got {actual}"
            )
    if observed["triangles"] != asset["triangles"]:
        issues.append(
            f"{asset['assetId']} expected {asset['triangles']} triangles, "
            f"got {observed['triangles']}"
        )
    if observed["materialSlots"] != asset["materialSlots"]:
        issues.append(
            f"{asset['assetId']} expected {asset['materialSlots']} material slots, "
            f"got {observed['materialSlots']}"
        )

    minimum = observed["minimumMeters"]
    maximum = observed["maximumMeters"]
    if abs(minimum[1]) > 0.001:
        issues.append(f"{asset['assetId']} pivot is not on the bottom plane.")
    if abs(minimum[0] + maximum[0]) > 0.001:
        issues.append(f"{asset['assetId']} pivot is not centered on X.")
    if abs(minimum[2] + maximum[2]) > 0.001:
        issues.append(f"{asset['assetId']} pivot is not centered on Z.")

    for transform in observed["objectTransforms"]:
        if any(abs(value - 1.0) > 0.0001 for value in transform["scale"]):
            issues.append(
                f"{asset['assetId']} normalized import scale is not (1,1,1): "
                f"{transform['scale']}"
            )

    footprint = [
        max(1, math.ceil(observed["dimensionsMeters"][0] / 8.0)),
        max(1, math.ceil(observed["dimensionsMeters"][2] / 8.0)),
    ]
    if footprint != asset["expectedFootprintCells"]:
        issues.append(
            f"{asset['assetId']} expected footprint {asset['expectedFootprintCells']}, "
            f"got {footprint}"
        )
    observed["footprintCells"] = footprint
    observed["normalizedSha256"] = sha256_path(normalized_path)
    return observed, issues


def main() -> None:
    args = parse_args()
    manifest_path = args.manifest.resolve()
    output_root = args.output_root.resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    issues: list[str] = []
    rows = []

    license_row = manifest["license"]
    license_path = output_root / safe_relative(license_row["repositoryPath"])
    if not exact_case_file(license_path):
        issues.append(f"Missing or case-mismatched license path: {license_path}")
    elif sha256_path(license_path) != license_row["sha256"].lower():
        issues.append(f"License SHA-256 mismatch: {license_path}")

    for texture in manifest["requiredTextures"]:
        texture_path = output_root / safe_relative(texture["repositoryPath"])
        if not exact_case_file(texture_path):
            issues.append(f"Missing or case-mismatched texture path: {texture_path}")
            continue
        if texture_path.stat().st_size != texture["bytes"]:
            issues.append(f"Texture byte length mismatch: {texture_path}")
        if sha256_path(texture_path) != texture["sha256"].lower():
            issues.append(f"Texture SHA-256 mismatch: {texture_path}")

    for asset in manifest["selection"]:
        observed, asset_issues = validate_asset(output_root, asset)
        issues.extend(asset_issues)
        rows.append(
            {
                "assetId": asset["assetId"],
                "prefabKey": asset["prefabKey"],
                "role": asset["role"],
                "sourcePath": asset["sourcePath"],
                "normalizedPath": asset["normalizedPath"],
                "observed": observed,
            }
        )

    report = {
        "schemaVersion": 1,
        "status": "Passed" if not issues else "Failed",
        "blenderVersion": bpy.app.version_string,
        "unitContract": "1 Blender unit = 1 meter = 1 Unity unit",
        "axisContract": "Blender Z-up exported as Unity Y-up (-Z forward, Y up)",
        "pivotContract": "bottom-center",
        "gridCellMeters": 8,
        "assets": rows,
        "issues": issues,
    }
    report_path = output_root / "Provenance" / "validation-report.json"
    report_text = json.dumps(report, indent=2, sort_keys=True) + "\n"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    if not report_path.exists() or report_path.read_text(encoding="utf-8") != report_text:
        report_path.write_text(report_text, encoding="utf-8", newline="\n")

    if issues:
        for issue in issues:
            print(f"P3_08_VALIDATION_ERROR {issue}")
        raise SystemExit(1)
    print(
        "P3_08_VALIDATION_PASSED "
        f"assets={len(rows)} textures={len(manifest['requiredTextures'])} "
        f"report={report_path}"
    )


if __name__ == "__main__":
    main()
