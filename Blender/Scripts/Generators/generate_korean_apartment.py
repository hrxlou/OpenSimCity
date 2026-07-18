"""Generate the first-party Korean 2020s apartment test asset.

This file is intentionally self-contained so the asset can be regenerated from
Blender's background CLI or from the Blender MCP ``execute_blender_code`` tool.
It creates the editable source scene, three presentation LODs, neutral preview
renders, explicit FBX/GLB exports, and a validation report.

The building is a fictional composition. It borrows broad visual cues from
recent Seoul apartment developments without copying a real site, logo, facade
pattern, or text mark.
"""

from __future__ import annotations

import json
import math
import os
import shutil
from collections import defaultdict
from pathlib import Path
from typing import Iterable, Sequence

import bpy
from mathutils import Matrix, Vector


ASSET_ID = "bld-r-high-korean-apartment-2020s-a"
ASSET_NAME = "KoreanApartment2020s"

FLOOR_COUNT = 30
PODIUM_FLOORS = 2
FLOOR_HEIGHT = 3.0
TOWER_FLOORS = FLOOR_COUNT - PODIUM_FLOORS
PODIUM_HEIGHT = PODIUM_FLOORS * FLOOR_HEIGHT
TOWER_HEIGHT = TOWER_FLOORS * FLOOR_HEIGHT
TOWER_TOP = PODIUM_HEIGHT + TOWER_HEIGHT
ROOF_TOP = 96.0

TOWER_WIDTH = 34.0
TOWER_DEPTH = 18.0
PODIUM_WIDTH = 39.0
PODIUM_DEPTH = 26.0

COLLECTION_NAMES = (
    "APT_BODY",
    "FACADE_MODULES",
    "WINDOWS",
    "PODIUM",
    "ROOF",
    "SIGNAGE",
    "PROPS",
    "LOD0",
    "LOD1",
    "LOD2",
    "CAMERAS",
    "LIGHTING",
)


def repo_root() -> Path:
    """Resolve the repository whether Blender runs the file or an exec string."""

    try:
        return Path(__file__).resolve().parents[3]
    except NameError:
        return Path(os.environ.get("OPENSIMCITY_ROOT", Path.cwd())).resolve()


ROOT = repo_root()
SOURCE_DIR = ROOT / "Blender" / "Source" / "OpenSimCity" / ASSET_NAME
EXPORT_DIR = ROOT / "Blender" / "Export" / "OpenSimCity" / ASSET_NAME
UNITY_DIR = ROOT / "Assets" / "OpenSimCity" / "Content" / "Art" / "Buildings" / ASSET_NAME
ARTIFACT_DIR = ROOT / "Artifacts" / "Art" / "korean-apartment-test"
CHECKPOINT_DIR = ARTIFACT_DIR / "checkpoints"


MATERIAL_SPECS = {
    "MAT_WALL_IVORY": ((0.82, 0.82, 0.78, 1.0), 0.0, 0.55),
    "MAT_WALL_LIGHT_GRAY": ((0.59, 0.61, 0.62, 1.0), 0.0, 0.5),
    "MAT_ACCENT_CHARCOAL": ((0.055, 0.075, 0.085, 1.0), 0.25, 0.28),
    "MAT_ACCENT_BRONZE": ((0.25, 0.12, 0.055, 1.0), 0.75, 0.25),
    "MAT_GLASS_BLUEGRAY": ((0.045, 0.12, 0.17, 1.0), 0.2, 0.18),
    "MAT_BASE_STONE": ((0.20, 0.22, 0.22, 1.0), 0.0, 0.75),
    "MAT_PREVIEW_GROUND": ((0.48, 0.50, 0.52, 1.0), 0.0, 0.95),
}


MATERIALS: dict[str, bpy.types.Material] = {}
MESH_CACHE: dict[tuple, bpy.types.Mesh] = {}
COLLECTIONS: dict[str, bpy.types.Collection] = {}
CREATED_OBJECTS: list[bpy.types.Object] = []
WARNINGS: list[str] = []


def ensure_dirs() -> None:
    for directory in (SOURCE_DIR, EXPORT_DIR, UNITY_DIR, ARTIFACT_DIR, CHECKPOINT_DIR):
        directory.mkdir(parents=True, exist_ok=True)


def clear_scene() -> None:
    # Operators skip objects hidden by a previous LOD pass. Remove datablocks
    # directly so repeated MCP/CLI runs cannot accumulate stale geometry.
    for object_ in list(bpy.data.objects):
        bpy.data.objects.remove(object_, do_unlink=True)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for curve in list(bpy.data.curves):
        if curve.users == 0:
            bpy.data.curves.remove(curve)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    MESH_CACHE.clear()
    CREATED_OBJECTS.clear()
    WARNINGS.clear()


def ensure_collections() -> None:
    scene_collection = bpy.context.scene.collection
    for name in COLLECTION_NAMES:
        collection = bpy.data.collections.new(name)
        scene_collection.children.link(collection)
        COLLECTIONS[name] = collection

    for lod in ("LOD0", "LOD1", "LOD2"):
        marker = bpy.data.objects.new(f"{lod}_MARKER", None)
        marker.empty_display_type = "PLAIN_AXES"
        marker.empty_display_size = 1.0
        marker["asset_id"] = ASSET_ID
        marker["asset_role"] = "lod_marker"
        marker["lod"] = lod
        COLLECTIONS[lod].objects.link(marker)

    root = bpy.data.objects.new(f"{ASSET_NAME}_ROOT", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 2.0
    root.location = (0.0, 0.0, 0.0)
    root["asset_id"] = ASSET_ID
    root["asset_role"] = "asset_root"
    COLLECTIONS["APT_BODY"].objects.link(root)


def make_material(name: str, color: Sequence[float], metallic: float, roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    nodes = material.node_tree.nodes
    principled = nodes.get("Principled BSDF")
    if principled:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness
    return material


def create_materials() -> None:
    MATERIALS.clear()
    for name, (color, metallic, roughness) in MATERIAL_SPECS.items():
        MATERIALS[name] = make_material(name, color, metallic, roughness)


def add_box_vertices(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, int, int, int]],
    materials: list[int],
    center: Sequence[float],
    size: Sequence[float],
    material_index: int,
) -> None:
    cx, cy, cz = center
    sx, sy, sz = (value / 2.0 for value in size)
    base = len(vertices)
    vertices.extend(
        [
            (cx - sx, cy - sy, cz - sz),
            (cx + sx, cy - sy, cz - sz),
            (cx + sx, cy + sy, cz - sz),
            (cx - sx, cy + sy, cz - sz),
            (cx - sx, cy - sy, cz + sz),
            (cx + sx, cy - sy, cz + sz),
            (cx + sx, cy + sy, cz + sz),
            (cx - sx, cy + sy, cz + sz),
        ]
    )
    faces.extend(
        [
            (base + 0, base + 3, base + 2, base + 1),
            (base + 4, base + 5, base + 6, base + 7),
            (base + 0, base + 1, base + 5, base + 4),
            (base + 1, base + 2, base + 6, base + 5),
            (base + 2, base + 3, base + 7, base + 6),
            (base + 3, base + 0, base + 4, base + 7),
        ]
    )
    materials.extend([material_index] * 6)


def mesh_from_boxes(name: str, boxes: Sequence[tuple[Sequence[float], Sequence[float], int]]) -> bpy.types.Mesh:
    key = (
        name,
        tuple((tuple(center), tuple(size), material_index) for center, size, material_index in boxes),
    )
    cached = MESH_CACHE.get(key)
    if cached:
        return cached
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    material_indices: list[int] = []
    for center, size, material_index in boxes:
        add_box_vertices(vertices, faces, material_indices, center, size, material_index)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.clear()
    mesh.update(calc_edges=True)
    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = material_index
    MESH_CACHE[key] = mesh
    return mesh


def make_mesh_object(
    name: str,
    boxes: Sequence[tuple[Sequence[float], Sequence[float], int]],
    material_names: Sequence[str],
    collection_name: str,
    lod: str | None,
    loc: Sequence[float] = (0.0, 0.0, 0.0),
    role: str = "asset",
    bevel: float = 0.0,
) -> bpy.types.Object:
    mesh = mesh_from_boxes(name + "_MESH", boxes)
    for material_name in material_names:
        if mesh.materials.get(material_name) is None:
            mesh.materials.append(MATERIALS[material_name])
    object_ = bpy.data.objects.new(name, mesh)
    object_.location = loc
    object_["asset_id"] = ASSET_ID
    object_["asset_role"] = role
    if lod:
        object_["lod"] = lod
    COLLECTIONS[collection_name].objects.link(object_)
    CREATED_OBJECTS.append(object_)
    if bevel > 0.0:
        bpy.context.view_layer.objects.active = object_
        object_.select_set(True)
        modifier = object_.modifiers.new("SmallEdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        object_.select_set(False)
    return object_


def add_box(
    name: str,
    center: Sequence[float],
    size: Sequence[float],
    material_name: str,
    collection_name: str,
    lod: str | None,
    role: str = "asset",
    bevel: float = 0.0,
) -> bpy.types.Object:
    return make_mesh_object(
        name,
        [(center, size, 0)],
        [material_name],
        collection_name,
        lod,
        role=role,
        bevel=bevel,
    )


def add_instanced_mesh(
    name: str,
    mesh: bpy.types.Mesh,
    collection_name: str,
    lod: str,
    loc: Sequence[float],
    role: str = "facade_module",
) -> bpy.types.Object:
    object_ = bpy.data.objects.new(name, mesh)
    object_.location = loc
    object_["asset_id"] = ASSET_ID
    object_["asset_role"] = role
    object_["lod"] = lod
    COLLECTIONS[collection_name].objects.link(object_)
    CREATED_OBJECTS.append(object_)
    return object_


def create_window_mesh(name: str, width: float, height: float, depth: float, frame: float) -> bpy.types.Mesh:
    boxes = [
        ((0.0, 0.0, 0.0), (width - frame * 2.0, depth * 0.7, height - frame * 2.0), 0),
        ((0.0, -depth * 0.43, (height - frame) / 2.0), (width, depth * 0.45, frame), 1),
        ((0.0, -depth * 0.43, -(height - frame) / 2.0), (width, depth * 0.45, frame), 1),
        ((-(width - frame) / 2.0, -depth * 0.43, 0.0), (frame, depth * 0.45, height), 1),
        (((width - frame) / 2.0, -depth * 0.43, 0.0), (frame, depth * 0.45, height), 1),
    ]
    mesh = mesh_from_boxes(name, boxes)
    if mesh.materials.get("MAT_GLASS_BLUEGRAY") is None:
        mesh.materials.append(MATERIALS["MAT_GLASS_BLUEGRAY"])
    if mesh.materials.get("MAT_ACCENT_CHARCOAL") is None:
        mesh.materials.append(MATERIALS["MAT_ACCENT_CHARCOAL"])
    return mesh


def create_small_window_mesh(name: str, width: float, height: float) -> bpy.types.Mesh:
    boxes = [
        ((0.0, 0.0, 0.0), (width - 0.16, 0.12, height - 0.16), 0),
        ((0.0, -0.08, height / 2.0 - 0.08), (width, 0.10, 0.16), 1),
        ((0.0, -0.08, -height / 2.0 + 0.08), (width, 0.10, 0.16), 1),
        ((-width / 2.0 + 0.08, -0.08, 0.0), (0.16, 0.10, height), 1),
        ((width / 2.0 - 0.08, -0.08, 0.0), (0.16, 0.10, height), 1),
    ]
    mesh = mesh_from_boxes(name, boxes)
    if mesh.materials.get("MAT_GLASS_BLUEGRAY") is None:
        mesh.materials.append(MATERIALS["MAT_GLASS_BLUEGRAY"])
    if mesh.materials.get("MAT_ACCENT_CHARCOAL") is None:
        mesh.materials.append(MATERIALS["MAT_ACCENT_CHARCOAL"])
    return mesh


def build_massing() -> None:
    """Build the structural silhouette, podium, and roof for all LODs."""

    tower_bottom = PODIUM_HEIGHT
    tower_center_z = tower_bottom + TOWER_HEIGHT / 2.0

    for lod in ("LOD0", "LOD1", "LOD2"):
        # The three interlocking tower volumes keep the mass from reading as a
        # single box while retaining a repeatable, buildable footprint.
        add_box(
            f"{lod}_Tower_Wing_Left",
            (-10.25, 0.0, tower_center_z),
            (13.5, 17.5, TOWER_HEIGHT),
            "MAT_WALL_IVORY",
            "APT_BODY",
            lod,
        )
        add_box(
            f"{lod}_Tower_Core",
            (0.0, 0.35, tower_center_z),
            (7.0, 16.0, TOWER_HEIGHT),
            "MAT_WALL_LIGHT_GRAY",
            "APT_BODY",
            lod,
        )
        add_box(
            f"{lod}_Tower_Wing_Right",
            (10.25, 0.0, tower_center_z),
            (13.5, 17.5, TOWER_HEIGHT),
            "MAT_WALL_IVORY",
            "APT_BODY",
            lod,
        )

        # A shallow upper setback breaks the roofline and gives the tower a
        # visible crown without using a signature architectural gimmick.
        add_box(
            f"{lod}_Crown_Center",
            (0.0, 0.35, TOWER_TOP - 1.0),
            (8.2, 16.2, 2.0),
            "MAT_WALL_LIGHT_GRAY",
            "APT_BODY",
            lod,
        )

        add_box(
            f"{lod}_Podium_Main",
            (0.0, 0.0, PODIUM_HEIGHT / 2.0),
            (PODIUM_WIDTH, PODIUM_DEPTH, PODIUM_HEIGHT),
            "MAT_BASE_STONE",
            "PODIUM",
            lod,
            bevel=0.18 if lod == "LOD0" else 0.0,
        )
        add_box(
            f"{lod}_Podium_Upper_Band",
            (0.0, -13.35, PODIUM_HEIGHT - 0.45),
            (PODIUM_WIDTH - 1.0, 0.5, 0.9),
            "MAT_WALL_LIGHT_GRAY",
            "PODIUM",
            lod,
        )

        # A broad side panel keeps the usually quieter apartment sidewall
        # legible at isometric city-view distances.
        for side in (-1.0, 1.0):
            add_box(
                f"{lod}_Sidewall_Panel_{'L' if side < 0 else 'R'}",
                (side * 17.30, 0.15, 47.0),
                (0.30, 12.2, 66.0),
                "MAT_WALL_LIGHT_GRAY",
                "FACADE_MODULES",
                lod,
            )


def build_facade() -> None:
    front_window_mesh = create_window_mesh("FacadeWindowFront_MESH", 3.35, 1.95, 0.24, 0.14)
    back_window_mesh = create_small_window_mesh("FacadeWindowBack_MESH", 2.35, 1.40)

    front_x = (-14.4, -10.8, -7.25, -3.55, 3.55, 7.25, 10.8, 14.4)
    back_x = (-13.4, -9.0, -4.5, 0.0, 4.5, 9.0, 13.4)

    # Full facade modules exist only in LOD0. The large bands/patches below
    # are the simplified LOD1 treatment; LOD2 keeps only color blocking.
    for floor in range(TOWER_FLOORS):
        z = PODIUM_HEIGHT + floor * FLOOR_HEIGHT + FLOOR_HEIGHT * 0.56
        for index, x in enumerate(front_x):
            add_instanced_mesh(
                f"FrontWindow_{floor + PODIUM_FLOORS + 1:02d}_{index:02d}",
                front_window_mesh,
                "WINDOWS",
                "LOD0",
                (x, -9.08, z),
            )
        for index, x in enumerate(back_x):
            add_instanced_mesh(
                f"BackWindow_{floor + PODIUM_FLOORS + 1:02d}_{index:02d}",
                back_window_mesh,
                "WINDOWS",
                "LOD0",
                (x, 9.08, z + 0.02),
            )

    for side in (-1.0, 1.0):
        for index, z in enumerate((18.0, 37.0, 56.0, 75.0)):
            add_box(
                f"SideWindow_{'L' if side < 0 else 'R'}_{index:02d}",
                (side * 17.34, -2.1, z),
                (0.16, 1.75, 5.6),
                "MAT_GLASS_BLUEGRAY",
                "WINDOWS",
                "LOD0",
            )

    # Strong but limited vertical lines echo the metal-frame vocabulary in the
    # reference family and remain readable from an isometric camera.
    for x, material in ((-16.0, "MAT_ACCENT_CHARCOAL"), (-5.1, "MAT_ACCENT_BRONZE"), (5.1, "MAT_ACCENT_CHARCOAL"), (16.0, "MAT_ACCENT_BRONZE")):
        for lod in ("LOD0", "LOD1", "LOD2"):
            add_box(
                f"{lod}_Vertical_Frame_{x:+.1f}",
                (x, -9.38, 47.0),
                (0.58 if lod == "LOD0" else 0.72, 0.28, 84.0),
                material,
                "FACADE_MODULES",
                lod,
            )

    # Horizontal breaks at the two-floor rhythm stop the repeated facade from
    # becoming a perfectly uniform barcode.
    for band_index, z in enumerate((12.0, 24.0, 36.0, 48.0, 60.0, 72.0, 84.0)):
        for lod in ("LOD0", "LOD1"):
            add_box(
                f"{lod}_Facade_Band_{band_index:02d}",
                (0.0, -9.30, z),
                (33.4, 0.24, 0.18 if lod == "LOD0" else 0.28),
                "MAT_WALL_LIGHT_GRAY",
                "FACADE_MODULES",
                lod,
            )

    # LOD1: one large dark window patch per facade bay / four-floor band.
    for group in range(7):
        z = 12.0 + group * 12.0
        for x in (-12.5, -7.0, -1.8, 1.8, 7.0, 12.5):
            add_box(
                f"LOD1_Front_WindowPatch_{group:02d}_{x:+.1f}",
                (x, -9.22, z),
                (4.0, 0.16, 8.0),
                "MAT_GLASS_BLUEGRAY",
                "WINDOWS",
                "LOD1",
            )
        for x in (-11.5, -5.8, 0.0, 5.8, 11.5):
            add_box(
                f"LOD1_Back_WindowPatch_{group:02d}_{x:+.1f}",
                (x, 9.20, z),
                (4.5, 0.16, 6.4),
                "MAT_GLASS_BLUEGRAY",
                "WINDOWS",
                "LOD1",
            )

    # LOD2: two broad facade color fields, no individual window geometry.
    for side, y in (("Front", -9.20), ("Back", 9.20)):
        for index, x in enumerate((-11.5, -3.8, 3.8, 11.5)):
            add_box(
                f"LOD2_{side}_ColorField_{index:02d}",
                (x, y, 47.0),
                (6.8, 0.16, 73.0),
                "MAT_GLASS_BLUEGRAY",
                "WINDOWS",
                "LOD2",
            )


def build_podium_and_roof() -> None:
    # Lobby glazing and entrance canopy remain a recognizable close-range cue.
    add_box(
        "Entrance_Lobby_Glass",
        (0.0, -13.20, 3.15),
        (9.0, 0.24, 4.9),
        "MAT_GLASS_BLUEGRAY",
        "PODIUM",
        "LOD0",
    )
    add_box(
        "Entrance_Canopy",
        (0.0, -14.65, 6.10),
        (12.5, 3.7, 0.42),
        "MAT_ACCENT_BRONZE",
        "PODIUM",
        "LOD0",
        bevel=0.12,
    )
    for x in (-5.0, 5.0):
        add_box(
            f"Entrance_Canopy_Pillar_{x:+.0f}",
            (x, -14.15, 3.0),
            (0.36, 0.36, 5.9),
            "MAT_ACCENT_CHARCOAL",
            "PODIUM",
            "LOD0",
        )
    add_box(
        "Entrance_Door_Dark",
        (0.0, -13.38, 2.0),
        (4.2, 0.10, 3.4),
        "MAT_ACCENT_CHARCOAL",
        "PODIUM",
        "LOD0",
    )

    # Simplified entrance for LOD1/LOD2.
    add_box(
        "LOD1_Entrance_Block",
        (0.0, -13.45, 3.0),
        (12.0, 0.22, 4.0),
        "MAT_GLASS_BLUEGRAY",
        "PODIUM",
        "LOD1",
    )
    add_box(
        "LOD1_Entrance_Canopy_Silhouette",
        (0.0, -14.65, 6.10),
        (12.5, 3.7, 0.42),
        "MAT_ACCENT_CHARCOAL",
        "PODIUM",
        "LOD1",
    )
    add_box(
        "LOD2_Entrance_Block",
        (0.0, -13.4, 3.0),
        (13.0, 0.20, 3.3),
        "MAT_ACCENT_CHARCOAL",
        "PODIUM",
        "LOD2",
    )
    add_box(
        "LOD2_Entrance_Canopy_Silhouette",
        (0.0, -14.65, 6.10),
        (12.5, 3.7, 0.42),
        "MAT_ACCENT_CHARCOAL",
        "PODIUM",
        "LOD2",
    )

    # Mechanical room + crown frame. All LODs retain the same external bounds.
    add_box(
        "Roof_Mechanical_Room",
        (0.0, 0.2, 92.0),
        (9.0, 7.0, 4.0),
        "MAT_BASE_STONE",
        "ROOF",
        "LOD0",
    )
    add_box(
        "LOD1_Roof_Mechanical_Room",
        (0.0, 0.2, 92.0),
        (9.0, 7.0, 4.0),
        "MAT_BASE_STONE",
        "ROOF",
        "LOD1",
    )
    add_box(
        "LOD2_Roof_Mechanical_Room",
        (0.0, 0.2, 92.0),
        (9.0, 7.0, 4.0),
        "MAT_BASE_STONE",
        "ROOF",
        "LOD2",
    )

    for lod in ("LOD0", "LOD1", "LOD2"):
        # Keep the external silhouette identical across LODs; the simplified
        # stages gain their budget from removing facade modules, not by
        # changing the crown's bounding box.
        frame_width = 0.55
        frame_material = "MAT_ACCENT_BRONZE" if lod == "LOD0" else "MAT_ACCENT_CHARCOAL"
        for x in (-7.3, 7.3):
            add_box(
                f"{lod}_Roof_Frame_Pillar_{x:+.1f}",
                (x, -2.9, 94.0),
                (frame_width, frame_width, 4.0),
                frame_material,
                "ROOF",
                lod,
            )
        add_box(
            f"{lod}_Roof_Frame_Top",
            (0.0, -2.9, 95.6),
            (15.2, frame_width, frame_width),
            frame_material,
            "ROOF",
            lod,
        )
        add_box(
            f"{lod}_Roof_Parapet_Front",
            (0.0, -8.35, 91.2),
            (30.0, 0.35, 2.2),
            "MAT_WALL_LIGHT_GRAY",
            "ROOF",
            lod,
        )

    # A restrained planted edge reads well in the preview, while remaining
    # replaceable by pooled city props in runtime.
    for x in (-14.0, 14.0):
        add_box(
            f"Planter_{x:+.0f}",
            (x, -13.7, 0.55),
            (4.0, 1.1, 1.1),
            "MAT_WALL_LIGHT_GRAY",
            "PROPS",
            None,
            role="preview_prop",
            bevel=0.08,
        )


def create_signage() -> None:
    text_data = bpy.data.curves.new("Sign_101_Curve", type="FONT")
    text_data.body = "101"
    text_data.align_x = "CENTER"
    text_data.align_y = "CENTER"
    text_data.size = 3.0
    text_data.extrude = 0.04
    text_data.bevel_depth = 0.015
    text_data.materials.append(MATERIALS["MAT_ACCENT_BRONZE"])
    sign = bpy.data.objects.new("Sign_BuildingNumber_101", text_data)
    sign.location = (17.37, -1.4, 52.0)
    sign.rotation_mode = "QUATERNION"
    # Local text X -> world Y, local text Y -> world Z, local normal -> +X.
    basis = Matrix(((0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0)))
    sign.rotation_quaternion = basis.to_quaternion()
    sign["asset_id"] = ASSET_ID
    sign["asset_role"] = "signage"
    sign["lod"] = "LOD0"
    COLLECTIONS["SIGNAGE"].objects.link(sign)
    CREATED_OBJECTS.append(sign)
    bpy.context.view_layer.objects.active = sign
    sign.select_set(True)
    bpy.ops.object.convert(target="MESH")
    sign.select_set(False)


def create_preview_scene() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        # Blender 5.1 exposes the same realtime engine as BLENDER_EEVEE.
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(ARTIFACT_DIR / "preview_perspective.png")
    scene.render.image_settings.color_depth = "8"

    world = scene.world or bpy.data.worlds.new("KoreanApartmentPreviewWorld")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background:
        background.inputs["Color"].default_value = (0.35, 0.39, 0.44, 1.0)
        background.inputs["Strength"].default_value = 0.8
    scene.view_settings.exposure = 0.7

    add_box(
        "Preview_Ground",
        (0.0, 0.0, -0.05),
        (300.0, 300.0, 0.1),
        "MAT_PREVIEW_GROUND",
        "PROPS",
        None,
        role="preview_ground",
    )

    camera_specs = {
        "CAM_Perspective": ((155.0, -205.0, 118.0), (0.0, 0.0, 46.0), 52.0),
        "CAM_Front": ((0.0, -265.0, 47.0), (0.0, 0.0, 47.0), 55.0),
        "CAM_Back": ((0.0, 265.0, 47.0), (0.0, 0.0, 47.0), 55.0),
        "CAM_Side": ((265.0, 0.0, 47.0), (0.0, 0.0, 47.0), 55.0),
        "CAM_TopPerspective": ((160.0, -210.0, 175.0), (0.0, 0.0, 43.0), 58.0),
    }
    for name, (location, target, lens) in camera_specs.items():
        camera_data = bpy.data.cameras.new(name + "_DATA")
        camera_data.lens = lens
        camera_data.sensor_width = 36.0
        camera = bpy.data.objects.new(name, camera_data)
        camera.location = location
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera["asset_role"] = "preview_camera"
        COLLECTIONS["CAMERAS"].objects.link(camera)

    # One large soft source plus a cool rim source gives enough material
    # separation without turning the preview into a marketing render.
    for name, location, energy, color, size in (
        ("Key_Area", (110.0, -120.0, 170.0), 2400.0, (1.0, 0.88, 0.74), 70.0),
        ("Fill_Area", (-120.0, 70.0, 100.0), 1200.0, (0.55, 0.72, 1.0), 90.0),
    ):
        light_data = bpy.data.lights.new(name + "_DATA", type="AREA")
        light_data.energy = energy
        light_data.color = color
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        light.location = location
        light.rotation_euler = (Vector((0.0, 0.0, 42.0)) - light.location).to_track_quat("-Z", "Y").to_euler()
        light["asset_role"] = "preview_light"
        COLLECTIONS["LIGHTING"].objects.link(light)

    sun_data = bpy.data.lights.new("Sun_DATA", type="SUN")
    sun_data.energy = 2.2
    sun_data.angle = math.radians(12.0)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(28.0), math.radians(-26.0), math.radians(-30.0))
    sun["asset_role"] = "preview_light"
    COLLECTIONS["LIGHTING"].objects.link(sun)


def set_lod_visibility(lod: str) -> None:
    for object_ in bpy.context.scene.objects:
        object_lod = object_.get("lod")
        if object_lod in ("LOD0", "LOD1", "LOD2"):
            object_.hide_render = object_lod != lod
            object_.hide_viewport = object_lod != lod
        elif object_.get("asset_role") == "lod_marker":
            object_.hide_render = True
            object_.hide_viewport = True


def save_checkpoint(name: str) -> None:
    path = CHECKPOINT_DIR / f"{name}.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


def asset_objects(lod: str | None = None) -> list[bpy.types.Object]:
    objects = []
    for object_ in CREATED_OBJECTS:
        if object_.get("asset_role") in ("preview_ground", "preview_prop", "lod_marker"):
            continue
        if lod and object_.get("lod") != lod:
            continue
        if not lod and object_.get("lod") not in ("LOD0", "LOD1", "LOD2"):
            continue
        if object_.type in {"MESH", "FONT"}:
            objects.append(object_)
    return objects


def select_objects(objects: Iterable[bpy.types.Object]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    selected = list(objects)
    for object_ in selected:
        object_.hide_viewport = False
        object_.select_set(True)
    if selected:
        bpy.context.view_layer.objects.active = selected[0]


def render_previews() -> list[str]:
    set_lod_visibility("LOD0")
    scene = bpy.context.scene
    paths = []
    for camera_name, filename in (
        ("CAM_Perspective", "preview_perspective.png"),
        ("CAM_Front", "preview_front.png"),
        ("CAM_Back", "preview_back.png"),
        ("CAM_Side", "preview_side.png"),
        ("CAM_TopPerspective", "preview_top_perspective.png"),
    ):
        scene.camera = bpy.data.objects.get(camera_name)
        path = ARTIFACT_DIR / filename
        scene.render.filepath = str(path)
        try:
            bpy.ops.render.render(write_still=True)
            paths.append(str(path))
        except Exception as exc:  # pragma: no cover - Blender runtime only
            WARNINGS.append(f"Render failed for {camera_name}: {exc}")
    return paths


def export_fbx(path: Path, lod: str) -> bool:
    select_objects(asset_objects(lod))
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(path),
            use_selection=True,
            object_types={"MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            bake_space_transform=False,
            add_leaf_bones=False,
            use_mesh_modifiers=True,
            path_mode="AUTO",
            embed_textures=False,
        )
        return path.exists()
    except Exception as exc:  # pragma: no cover - Blender runtime only
        WARNINGS.append(f"FBX export failed for {path.name}: {exc}")
        return False


def export_glb(path: Path, lod: str) -> bool:
    select_objects(asset_objects(lod))
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(path),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
        )
        return path.exists()
    except Exception as exc:  # pragma: no cover - Blender runtime only
        WARNINGS.append(f"GLB export failed for {path.name}: {exc}")
        return False


def bounds_for(objects: Iterable[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = []
    for object_ in objects:
        if object_.type not in {"MESH", "FONT"}:
            continue
        points.extend(object_.matrix_world @ Vector(corner) for corner in object_.bound_box)
    if not points:
        return Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0))
    return (
        Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points))),
        Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points))),
    )


def triangle_count(objects: Iterable[bpy.types.Object]) -> int:
    count = 0
    for object_ in objects:
        if object_.type != "MESH":
            continue
        count += sum(max(0, len(polygon.vertices) - 2) for polygon in object_.data.polygons)
    return count


def validate() -> dict:
    report: dict = {
        "asset_id": ASSET_ID,
        "asset_name": ASSET_NAME,
        "blender_version": bpy.app.version_string,
        "dimensions_m": {},
        "object_count": len(asset_objects()),
        "material_count": len([name for name in MATERIALS if name != "MAT_PREVIEW_GROUND"]),
        "materials": [name for name in MATERIALS if name != "MAT_PREVIEW_GROUND"],
        "lods": {},
        "checks": {},
        "warnings": WARNINGS,
    }

    all_min, all_max = bounds_for(asset_objects())
    dimensions = all_max - all_min
    report["dimensions_m"] = {axis: round(value, 3) for axis, value in zip(("x", "y", "z"), dimensions)}

    for lod in ("LOD0", "LOD1", "LOD2"):
        lod_objects = asset_objects(lod)
        min_corner, max_corner = bounds_for(lod_objects)
        report["lods"][lod] = {
            "object_count": len(lod_objects),
            "triangles": triangle_count(lod_objects),
            "min": [round(value, 3) for value in min_corner],
            "max": [round(value, 3) for value in max_corner],
            "dimensions": [round(value, 3) for value in (max_corner - min_corner)],
        }

    scale_ok = all(
        all(abs(value - 1.0) < 1e-5 for value in object_.scale)
        for object_ in asset_objects()
        if object_.type in {"MESH", "FONT"}
    )
    ground_ok = all_min.z >= -1e-4
    structural_objects = [
        object_
        for object_ in asset_objects()
        if object_.get("asset_role") != "signage"
    ]
    structural_min, structural_max = bounds_for(structural_objects)
    root = bpy.data.objects.get(f"{ASSET_NAME}_ROOT")
    origin_ok = bool(root) and all(abs(value) < 1e-5 for value in root.location) and all(abs(value - 1.0) < 1e-5 for value in root.scale)
    no_missing_materials = all(len(object_.data.materials) > 0 for object_ in asset_objects() if hasattr(object_.data, "materials"))
    no_flipped_normals = all(
        polygon.normal.length > 0.5
        for object_ in asset_objects()
        if object_.type == "MESH"
        for polygon in object_.data.polygons
    )
    lod_dimensions = [tuple(report["lods"][lod]["dimensions"]) for lod in ("LOD0", "LOD1", "LOD2")]
    lod_same_size = all(dimension == lod_dimensions[0] for dimension in lod_dimensions[1:])
    report["checks"] = {
        "applied_scale": scale_ok,
        "ground_at_or_above_z0": ground_ok,
        "origin_is_ground_center": origin_ok,
        "non_zero_normals": no_flipped_normals,
        "materials_present": no_missing_materials,
        "lods_same_dimensions": lod_same_size,
        "lod0_under_30000_triangles": report["lods"]["LOD0"]["triangles"] <= 30000,
        "lod1_under_8000_triangles": report["lods"]["LOD1"]["triangles"] <= 8000,
        "lod2_under_1500_triangles": report["lods"]["LOD2"]["triangles"] <= 1500,
    }
    return report


def write_report(report: dict, output_files: Sequence[Path], preview_files: Sequence[str]) -> None:
    report["output_files"] = [str(path) for path in output_files]
    report["preview_files"] = list(preview_files)
    report["export_success"] = all(path.exists() for path in output_files)
    report["render_success"] = all(Path(path).exists() for path in preview_files)
    report["warnings"] = WARNINGS
    report_path = ARTIFACT_DIR / "asset_report.txt"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    # Keep a small provenance/report copy next to the export so an approved
    # binary can be audited without opening Blender.
    export_report = EXPORT_DIR / "asset_report.txt"
    export_report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def copy_runtime_exports(output_files: Sequence[Path]) -> list[Path]:
    copied = []
    for source in output_files:
        if source.suffix.lower() not in {".fbx", ".glb"}:
            continue
        target = UNITY_DIR / source.name
        shutil.copy2(source, target)
        copied.append(target)
    return copied


def main() -> dict:
    ensure_dirs()
    clear_scene()
    ensure_collections()
    create_materials()
    build_massing()
    save_checkpoint("apartment_01_massing")
    build_facade()
    save_checkpoint("apartment_02_facade")
    build_podium_and_roof()
    create_signage()
    save_checkpoint("apartment_03_podium_roof")
    create_preview_scene()
    save_checkpoint("apartment_04_lod")

    preview_files = render_previews()
    report = validate()

    # Main runtime files use LOD0. Additional FBX files keep the simplified
    # geometry available for Unity's reproducible LODGroup wrapper setup.
    output_files = [
        EXPORT_DIR / "korean_apartment_2020s.fbx",
        EXPORT_DIR / "korean_apartment_2020s.glb",
        EXPORT_DIR / "korean_apartment_2020s_LOD1.fbx",
        EXPORT_DIR / "korean_apartment_2020s_LOD2.fbx",
    ]
    export_fbx(output_files[0], "LOD0")
    export_glb(output_files[1], "LOD0")
    export_fbx(output_files[2], "LOD1")
    export_fbx(output_files[3], "LOD2")
    output_files = [path for path in output_files if path.exists()]

    runtime_files = copy_runtime_exports(output_files)
    report["runtime_asset_files"] = [str(path) for path in runtime_files]
    write_report(report, output_files, preview_files)

    source_blend = SOURCE_DIR / "korean_apartment_2020s.blend"
    set_lod_visibility("LOD0")
    bpy.ops.wm.save_as_mainfile(filepath=str(source_blend))

    # Also copy the report to the ignored working artifact folder after the
    # final source save, so a local artist can inspect the full run.
    report["source_blend"] = str(source_blend)
    report_path = ARTIFACT_DIR / "asset_report.txt"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    result = main()
    print(json.dumps(result, ensure_ascii=False, indent=2))
