"""Generate the high-quality Korean premium apartment master asset.

The file is intentionally phase-oriented.  It can be executed from Blender or
loaded through Blender MCP.  No Unity assets, exports, or LODs are created.
"""

from __future__ import annotations

import math
from pathlib import Path
from typing import Iterable, Sequence

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Blender" / "output"
CHECKPOINTS = OUTPUT / "checkpoints"
REVIEW = OUTPUT / "review"
FINAL_BLEND = OUTPUT / "korean_premium_apartment_master.blend"
HDRI_DIR = ROOT / "Blender" / "assets" / "polyhaven" / "hdri"
HDRI_DAY = HDRI_DIR / "cambridge_2k.hdr"
HDRI_DUSK = HDRI_DIR / "suburban_parking_area_2k.hdr"

COLLECTIONS = (
    "APT_MASTER", "APT_BODY", "APT_FACADE", "APT_WINDOWS", "APT_PODIUM",
    "APT_ROOF", "APT_SIGNAGE", "APT_OPTIONAL_CONTEXT", "CAMERAS", "LIGHTING",
)

MATERIALS: dict[str, bpy.types.Material] = {}
WINDOW_DEPTH_RECORDS: list[dict] = []


def ensure_output_dirs() -> None:
    for path in (OUTPUT, CHECKPOINTS, REVIEW):
        path.mkdir(parents=True, exist_ok=True)


def setup_scene() -> None:
    ensure_output_dirs()
    WINDOW_DEPTH_RECORDS.clear()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    # Remove unlinked object datablocks too; otherwise repeated MCP builds can
    # leave name reservations and turn required module names into `.001` etc.
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)
    root = bpy.context.scene.collection
    default = bpy.data.collections.get("Collection")
    if default:
        default.name = "APT_MASTER"
    else:
        default = bpy.data.collections.new("APT_MASTER")
        root.children.link(default)
    for name in COLLECTIONS[1:]:
        col = bpy.data.collections.new(name)
        root.children.link(col)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.resolution_percentage = 100
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.color_depth = "8"
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        scene.view_settings.look = "Medium High Contrast"
    scene.world.color = (0.055, 0.07, 0.095)
    scene["asset_name"] = "Korean Premium Apartment Master"
    scene["design_language"] = "Fictional Seoul 2020s high-end stylized realism"
    scene["floors_above_ground"] = 30
    scene["front_direction"] = "-Y"


def collection(name: str) -> bpy.types.Collection:
    return bpy.data.collections[name]


def _principled(mat: bpy.types.Material) -> bpy.types.Node:
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return bsdf


def procedural_material(name: str, color: Sequence[float], roughness: float,
                        metallic: float = 0.0, noise_scale: float = 8.0,
                        bump_strength: float = 0.08) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    bsdf = _principled(mat)
    bsdf.inputs["Base Color"].default_value = (*color[:3], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    tex = nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = noise_scale
    tex.inputs["Detail"].default_value = 3.0
    tex.inputs["Roughness"].default_value = 0.62
    ramp = nodes.new("ShaderNodeValToRGB")
    c0 = tuple(max(0.0, c * 0.92) for c in color[:3])
    c1 = tuple(min(1.0, c * 1.055) for c in color[:3])
    ramp.color_ramp.elements[0].color = (*c0, 1.0)
    ramp.color_ramp.elements[1].color = (*c1, 1.0)
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = bump_strength
    bump.inputs["Distance"].default_value = 0.06
    links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(tex.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def glass_material(name: str, color: Sequence[float], lit: bool = False) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    bsdf = _principled(mat)
    bsdf.inputs["Base Color"].default_value = (*color[:3], 1.0)
    bsdf.inputs["Metallic"].default_value = 0.07
    bsdf.inputs["Roughness"].default_value = 0.14 if not lit else 0.24
    bsdf.inputs["IOR"].default_value = 1.46
    if "Transmission Weight" in bsdf.inputs:
        bsdf.inputs["Transmission Weight"].default_value = 0.34 if not lit else 0.22
    if lit:
        bsdf.inputs["Emission Color"].default_value = (0.68, 0.32, 0.10, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 2.5
    return mat


def emission_material(name: str, color: Sequence[float], strength: float) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (*color[:3], 1.0)
    emission.inputs["Strength"].default_value = strength
    mat.node_tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    return mat


def create_materials() -> None:
    MATERIALS.clear()
    specs = {
        "MAT_Wall_WarmIvory": ((0.69, 0.66, 0.57), 0.64, 0.0, 18.0, 0.045),
        "MAT_Wall_LightGray": ((0.52, 0.54, 0.54), 0.62, 0.0, 14.0, 0.04),
        "MAT_Panel_MidCharcoal": ((0.135, 0.155, 0.165), 0.48, 0.04, 20.0, 0.025),
        "MAT_Panel_Charcoal": ((0.055, 0.065, 0.073), 0.38, 0.12, 24.0, 0.035),
        "MAT_Metal_DarkBronze": ((0.16, 0.085, 0.040), 0.31, 0.72, 38.0, 0.025),
        "MAT_Stone_WarmGray": ((0.29, 0.27, 0.235), 0.72, 0.0, 6.0, 0.12),
        "MAT_Metal_AnodizedDark": ((0.025, 0.032, 0.038), 0.27, 0.78, 42.0, 0.02),
        "MAT_Concrete_Soffit": ((0.39, 0.39, 0.37), 0.79, 0.0, 20.0, 0.055),
        "MAT_Paving": ((0.16, 0.17, 0.18), 0.86, 0.0, 12.0, 0.08),
        "MAT_Green": ((0.055, 0.16, 0.075), 0.86, 0.0, 7.0, 0.11),
    }
    for name, (color, rough, metal, scale, bump) in specs.items():
        MATERIALS[name] = procedural_material(name, color, rough, metal, scale, bump)
    MATERIALS["MAT_Glass_BlueGray"] = glass_material("MAT_Glass_BlueGray", (0.070, 0.155, 0.215))
    MATERIALS["MAT_Glass_BlueGray_Alt"] = glass_material("MAT_Glass_BlueGray_Alt", (0.090, 0.125, 0.155))
    MATERIALS["MAT_Glass_WarmInterior"] = glass_material(
        "MAT_Glass_WarmInterior", (0.32, 0.12, 0.035), True)
    MATERIALS["MAT_Interior_Dark"] = procedural_material(
        "MAT_Interior_Dark", (0.025, 0.038, 0.052), 0.88, 0.0, 4.0, 0.008)
    MATERIALS["MAT_IndirectLight"] = emission_material("MAT_IndirectLight", (1.0, 0.38, 0.10), 5.0)


def _box_geometry(center: Sequence[float], size: Sequence[float], verts: list, faces: list) -> None:
    cx, cy, cz = center
    sx, sy, sz = (v * 0.5 for v in size)
    base = len(verts)
    verts.extend([
        (cx-sx, cy-sy, cz-sz), (cx+sx, cy-sy, cz-sz),
        (cx+sx, cy+sy, cz-sz), (cx-sx, cy+sy, cz-sz),
        (cx-sx, cy-sy, cz+sz), (cx+sx, cy-sy, cz+sz),
        (cx+sx, cy+sy, cz+sz), (cx-sx, cy+sy, cz+sz),
    ])
    faces.extend([
        (base+0, base+1, base+2, base+3), (base+4, base+7, base+6, base+5),
        (base+0, base+4, base+5, base+1), (base+1, base+5, base+6, base+2),
        (base+2, base+6, base+7, base+3), (base+4, base+0, base+3, base+7),
    ])


def box_batch(name: str, boxes: Iterable[tuple[Sequence[float], Sequence[float]]],
              material: str, collection_name: str, bevel: float = 0.02) -> bpy.types.Object:
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for center, size in boxes:
        _box_geometry(center, size, verts, faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(MATERIALS[material])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection(collection_name).objects.link(obj)
    if bevel > 0:
        mod = obj.modifiers.new("Architectural_Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    return obj


def add_box(name: str, center: Sequence[float], size: Sequence[float], material: str,
            collection_name: str, bevel: float = 0.02) -> bpy.types.Object:
    return box_batch(name, [(center, size)], material, collection_name, bevel)


def create_primary_massing() -> None:
    body_boxes = [
        ((-13.75, 0.00, 49.8), (18.5, 19.6, 84.0)),
        ((0.00, 1.20, 49.8), (9.0, 18.0, 84.0)),
        ((13.75, 0.20, 49.8), (18.5, 19.6, 84.0)),
        ((-21.9, -0.10, 51.3), (2.2, 19.8, 81.0)),
        ((21.9, 0.20, 51.3), (2.2, 19.8, 81.0)),
    ]
    obj = box_batch("Body_PrimaryMassing", body_boxes, "MAT_Wall_WarmIvory", "APT_BODY", 0.035)
    obj["phase"] = "A/B massing"
    accents = [
        ((-4.15, -10.15, 51.0), (1.0, 0.42, 80.0)),
        ((4.15, -9.70, 51.0), (1.0, 0.42, 80.0)),
        ((-22.75, 0.0, 53.0), (0.42, 19.2, 76.0)),
        ((22.75, 0.3, 53.0), (0.42, 19.2, 76.0)),
    ]
    box_batch("Body_CharcoalVerticalReveals", accents, "MAT_Panel_Charcoal", "APT_BODY", 0.025)


def create_podium() -> None:
    add_box("Podium_Main", (0, 0.25, 3.9), (52.0, 24.0, 7.8),
            "MAT_Stone_WarmGray", "APT_PODIUM", 0.04)
    panel_boxes = []
    for x in (-22.5, -18.0, -13.5, 13.5, 18.0, 22.5):
        panel_boxes.append(((x, -11.86, 4.0), (3.65, 0.24, 6.7)))
    box_batch("Podium_FrontStonePanels", panel_boxes, "MAT_Wall_LightGray", "APT_PODIUM", 0.018)


def _window_frame_boxes(x: float, y: float, z: float, width: float, height: float,
                        front: bool = True, frame: float = 0.055):
    """Return a thin sash that stays fully inside the masonry opening.

    Keeping the perimeter bars inside the opening avoids intersections with the
    reveal faces.  Those intersections previously produced broken dark lines at
    the heads and jambs in close Cycles renders.
    """
    depth = 0.12
    if front:
        return [
            ((x-width/2+frame/2, y, z), (frame, depth, height)),
            ((x+width/2-frame/2, y, z), (frame, depth, height)),
            ((x, y, z-height/2+frame/2), (width, depth, frame)),
            ((x, y, z+height/2-frame/2), (width, depth, frame)),
        ]
    return [
        ((x, y-width/2+frame/2, z), (depth, frame, height)),
        ((x, y+width/2-frame/2, z), (depth, frame, height)),
        ((x, y, z-height/2+frame/2), (depth, width, frame)),
        ((x, y, z+height/2-frame/2), (depth, width, frame)),
    ]


def _window_reveal_boxes(x: float, outer_y: float, inner_y: float, z: float,
                         width: float, height: float, jamb: float = 0.10):
    """Four deep jambs that make a window read as an opening in the facade."""
    y = (outer_y + inner_y) * 0.5
    depth = abs(outer_y - inner_y)
    return [
        ((x-width/2-jamb/2, y, z), (jamb, depth, height+2*jamb)),
        ((x+width/2+jamb/2, y, z), (jamb, depth, height+2*jamb)),
        ((x, y, z-height/2-jamb/2), (width, depth, jamb)),
        ((x, y, z+height/2+jamb/2), (width, depth, jamb)),
    ]


def _side_window_reveal_boxes(x_outer: float, x_inner: float, y: float, z: float,
                              width: float, height: float, jamb: float = 0.09):
    """Four jambs for a side opening, with depth measured on local X."""
    x = (x_outer + x_inner) * 0.5
    depth = abs(x_outer - x_inner)
    return [
        ((x, y-width/2-jamb/2, z), (depth, jamb, height+2*jamb)),
        ((x, y+width/2+jamb/2, z), (depth, jamb, height+2*jamb)),
        ((x, y, z-height/2-jamb/2), (depth, width, jamb)),
        ((x, y, z+height/2+jamb/2), (depth, width, jamb)),
    ]


def _inset_point(surface: Sequence[float], outward_normal: Sequence[float], depth: float) -> Vector:
    """Move from a facade plane toward the building interior along its local normal."""
    return Vector(surface) - Vector(outward_normal).normalized() * depth


def _record_window_depth(facade: str, kind: str, surface: Sequence[float],
                         outward_normal: Sequence[float], frame: Sequence[float],
                         glass: Sequence[float], interior: Sequence[float],
                         glass_range: tuple[float, float]) -> None:
    WINDOW_DEPTH_RECORDS.append({
        "facade": facade, "kind": kind,
        "surface": tuple(surface), "normal": tuple(Vector(outward_normal).normalized()),
        "frame": tuple(frame), "glass": tuple(glass), "interior": tuple(interior),
        "glass_range": glass_range,
    })


def _facade_skin_row_y(surface_y: float, normal_y: float, x_min: float, x_max: float,
                       z: float, openings: Sequence[tuple[float, float, float]],
                       floor_height: float = 3.0, thickness: float = 0.38):
    """A continuous Y-facing wall row with only the window rectangles omitted."""
    boxes = []
    center_y = surface_y - normal_y * thickness * 0.5
    ordered = sorted(openings, key=lambda item: item[0])
    max_h = max(height for _, _, height in ordered)
    cell_bottom, cell_top = z-floor_height/2, z+floor_height/2
    open_bottom, open_top = z-max_h/2, z+max_h/2
    lower_h, upper_h = open_bottom-cell_bottom, cell_top-open_top
    if lower_h > 0:
        boxes.append((((x_min+x_max)/2, center_y, cell_bottom+lower_h/2),
                      (x_max-x_min, thickness, lower_h)))
    if upper_h > 0:
        boxes.append((((x_min+x_max)/2, center_y, open_top+upper_h/2),
                      (x_max-x_min, thickness, upper_h)))
    cursor = x_min
    for x, width, height in ordered:
        left, right = x-width/2, x+width/2
        if left > cursor:
            boxes.append((((cursor+left)/2, center_y, z), (left-cursor, thickness, max_h)))
        fill_h = (max_h-height)/2
        if fill_h > 1e-4:
            boxes.append(((x, center_y, open_bottom+fill_h/2), (width, thickness, fill_h)))
            boxes.append(((x, center_y, z+height/2+fill_h/2), (width, thickness, fill_h)))
        cursor = max(cursor, right)
    if cursor < x_max:
        boxes.append((((cursor+x_max)/2, center_y, z), (x_max-cursor, thickness, max_h)))
    return boxes


def _facade_skin_row_x(surface_x: float, normal_x: float, y_min: float, y_max: float,
                       z: float, openings: Sequence[tuple[float, float, float]],
                       floor_height: float = 3.0, thickness: float = 0.38):
    """A continuous X-facing wall row with only the window rectangles omitted."""
    boxes = []
    center_x = surface_x - normal_x * thickness * 0.5
    ordered = sorted(openings, key=lambda item: item[0])
    max_h = max(height for _, _, height in ordered)
    cell_bottom, cell_top = z-floor_height/2, z+floor_height/2
    open_bottom, open_top = z-max_h/2, z+max_h/2
    lower_h, upper_h = open_bottom-cell_bottom, cell_top-open_top
    if lower_h > 0:
        boxes.append(((center_x, (y_min+y_max)/2, cell_bottom+lower_h/2),
                      (thickness, y_max-y_min, lower_h)))
    if upper_h > 0:
        boxes.append(((center_x, (y_min+y_max)/2, open_top+upper_h/2),
                      (thickness, y_max-y_min, upper_h)))
    cursor = y_min
    for y, width, height in ordered:
        low, high = y-width/2, y+width/2
        if low > cursor:
            boxes.append(((center_x, (cursor+low)/2, z), (thickness, low-cursor, max_h)))
        fill_h = (max_h-height)/2
        if fill_h > 1e-4:
            boxes.append(((center_x, y, open_bottom+fill_h/2), (thickness, width, fill_h)))
            boxes.append(((center_x, y, z+height/2+fill_h/2), (thickness, width, fill_h)))
        cursor = max(cursor, high)
    if cursor < y_max:
        boxes.append(((center_x, (cursor+y_max)/2, z), (thickness, y_max-cursor, max_h)))
    return boxes


def _skin_surface_y(name: str, rectangles, surface_y: float, normal_y: float,
                    openings: Sequence[tuple[float, float, float, float]],
                    thickness: float, material: str) -> bpy.types.Object:
    """Build a seam-free outer facade surface plus the four jamb faces of each opening."""
    verts, faces = [], []

    def quad(points):
        base = len(verts)
        verts.extend(points)
        faces.append((base, base+1, base+2, base+3))

    for (cx, _cy, cz), (sx, _sy, sz) in rectangles:
        points = ((cx-sx/2, surface_y, cz-sz/2), (cx+sx/2, surface_y, cz-sz/2),
                  (cx+sx/2, surface_y, cz+sz/2), (cx-sx/2, surface_y, cz+sz/2))
        quad(points if normal_y < 0 else tuple(reversed(points)))
    inner_y = surface_y - normal_y*thickness
    for x, z, width, height in openings:
        left, right = x-width/2, x+width/2
        bottom, top = z-height/2, z+height/2
        quad(((left, surface_y, bottom), (left, inner_y, bottom),
              (left, inner_y, top), (left, surface_y, top)))
        quad(((right, inner_y, bottom), (right, surface_y, bottom),
              (right, surface_y, top), (right, inner_y, top)))
        quad(((left, surface_y, bottom), (right, surface_y, bottom),
              (right, inner_y, bottom), (left, inner_y, bottom)))
        quad(((left, inner_y, top), (right, inner_y, top),
              (right, surface_y, top), (left, surface_y, top)))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(MATERIALS[material])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection("APT_FACADE").objects.link(obj)
    return obj


def _skin_surface_x(name: str, rectangles, surface_x: float, normal_x: float,
                    openings: Sequence[tuple[float, float, float, float]],
                    thickness: float, material: str) -> bpy.types.Object:
    """Side-wall counterpart of _skin_surface_y without internal box seams."""
    verts, faces = [], []

    def quad(points):
        base = len(verts)
        verts.extend(points)
        faces.append((base, base+1, base+2, base+3))

    for (_cx, cy, cz), (_sx, sy, sz) in rectangles:
        points = ((surface_x, cy-sy/2, cz-sz/2), (surface_x, cy+sy/2, cz-sz/2),
                  (surface_x, cy+sy/2, cz+sz/2), (surface_x, cy-sy/2, cz+sz/2))
        quad(points if normal_x > 0 else tuple(reversed(points)))
    inner_x = surface_x - normal_x*thickness
    for y, z, width, height in openings:
        low, high = y-width/2, y+width/2
        bottom, top = z-height/2, z+height/2
        quad(((surface_x, low, bottom), (inner_x, low, bottom),
              (inner_x, low, top), (surface_x, low, top)))
        quad(((inner_x, high, bottom), (surface_x, high, bottom),
              (surface_x, high, top), (inner_x, high, top)))
        quad(((surface_x, low, bottom), (surface_x, high, bottom),
              (inner_x, high, bottom), (inner_x, low, bottom)))
        quad(((inner_x, low, top), (inner_x, high, top),
              (surface_x, high, top), (surface_x, low, top)))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(MATERIALS[material])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection("APT_FACADE").objects.link(obj)
    return obj


def create_window_modules() -> None:
    module_specs = {
        "WIN_Living_Large": (3.5, 2.15), "WIN_Bedroom_Medium": (2.0, 1.55),
        "WIN_Bedroom_Small": (1.45, 1.20), "WIN_Balcony_Recessed": (3.10, 2.20),
        "WIN_Service_Narrow": (1.20, 0.80), "WIN_Core_Stair": (1.20, 1.60),
    }
    for index, (name, dims) in enumerate(module_specs.items()):
        existing = bpy.data.objects.get(name)
        if existing:
            bpy.data.objects.remove(existing, do_unlink=True)
        obj = add_box(name, (-8 + index*3.0, 0, -8), (dims[0], 0.08, dims[1]),
                      "MAT_Glass_BlueGray", "APT_WINDOWS", 0.008)
        obj.hide_render = True
        obj.hide_viewport = True
        obj["module_width"] = dims[0]
        obj["module_height"] = dims[1]
        obj["reusable_window_module"] = True


def _front_windows() -> None:
    """Residential front rhythm with thin sash and locally inset glazing."""
    glass_boxes, alt_boxes, lit_glass_boxes, lit_boxes, interior_boxes = [], [], [], [], []
    frame_boxes, reveal_boxes, recess_boxes, louver_boxes = [], [], [], []
    surface = Vector((0.0, -10.88, 0.0))
    normal = Vector((0.0, -1.0, 0.0))
    wing_centers = (-13.75, 13.75)
    # Living is 1.75x the bedroom width; the balcony and service bay are deeper.
    bays = ((-5.70, 3.50, 2.15, "living"), (-1.80, 2.00, 1.55, "bedroom"),
            (0.45, 1.45, 1.20, "small"), (3.25, 3.10, 2.20, "balcony"),
            (6.25, 1.20, 0.80, "service"))
    for floor in range(28):
        z = 9.30 + floor * 3.0
        upper = floor >= 24
        for wi, wing in enumerate(wing_centers):
            for bi, (local_x, base_width, height, kind) in enumerate(bays):
                width = base_width
                # Only the right wing receives the more generous top-floor glazing.
                if upper and wi == 1 and kind in {"living", "balcony"}:
                    width *= 1.10
                elif upper and wi == 0 and kind == "living":
                    width *= 1.04
                x = wing + local_x
                depth = (0.58 if wi == 1 and upper else 0.50) if kind == "balcony" else (
                    0.54 if wi == 1 else 0.46) if kind == "service" else 0.20
                interior_depth = depth + 0.07 if kind in {"balcony", "service"} else 0.26
                frame_pt = _inset_point((x, surface.y, z), normal, 0.14)
                glass_pt = _inset_point((x, surface.y, z), normal, depth)
                interior_pt = _inset_point((x, surface.y, z), normal, interior_depth)
                selector = (floor * 17 + bi * 7 + wi * 11) % 23
                is_lit = selector in (0, 1, 3)
                glass_target = lit_glass_boxes if is_lit else (alt_boxes if selector == 2 else glass_boxes)
                glass_target.append(((glass_pt.x, glass_pt.y, z), (width, 0.055, height)))
                (lit_boxes if is_lit else interior_boxes).append(
                    ((interior_pt.x, interior_pt.y, z), (max(0.40, width-0.16), 0.03, height-0.14)))
                frame_boxes.extend(_window_frame_boxes(x, frame_pt.y, z, width, height, True, 0.032))
                reveal_boxes.extend(_window_reveal_boxes(x, surface.y, frame_pt.y, z, width, height, 0.10))
                if kind in {"living", "balcony"}:
                    for mullion_x in (x-width/6, x+width/6):
                        frame_boxes.append(((mullion_x, frame_pt.y, z), (0.018, 0.055, height)))
                elif kind in {"bedroom", "small"}:
                    frame_boxes.append(((x, frame_pt.y, z), (0.018, 0.055, height)))
                else:
                    louver_boxes.append(((x, frame_pt.y-0.005, z), (width, 0.050, 0.025)))
                if kind in {"balcony", "service"}:
                    recess_boxes.append(((x, interior_pt.y+0.025, z),
                                         (width+0.20, 0.03, height+0.18)))
                _record_window_depth("front", kind, (x, surface.y, z), normal,
                                     frame_pt, glass_pt, interior_pt,
                                     (0.40, 0.70) if kind in {"balcony", "service"} else (0.15, 0.25))
        # Central core retains a narrower and calmer stair-window rhythm.
        core_surface = Vector((0.0, -8.55, z))
        for x in (-1.55, 1.55):
            frame_pt = _inset_point((x, core_surface.y, z), normal, 0.12)
            glass_pt = _inset_point((x, core_surface.y, z), normal, 0.20)
            interior_pt = _inset_point((x, core_surface.y, z), normal, 0.24)
            glass_boxes.append(((x, glass_pt.y, z), (1.18, 0.055, 1.62)))
            interior_boxes.append(((x, interior_pt.y, z), (1.05, 0.03, 1.48)))
            frame_boxes.extend(_window_frame_boxes(x, frame_pt.y, z, 1.18, 1.62, True, 0.03))
            frame_boxes.append(((x, frame_pt.y, z), (0.018, 0.05, 1.62)))
            reveal_boxes.extend(_window_reveal_boxes(x, core_surface.y, frame_pt.y, z, 1.18, 1.62, 0.09))
            _record_window_depth("front_core", "stair", (x, core_surface.y, z), normal,
                                 frame_pt, glass_pt, interior_pt, (0.15, 0.25))

    box_batch("Windows_Front_Glass", glass_boxes, "MAT_Glass_BlueGray", "APT_WINDOWS", 0.006)
    box_batch("Windows_Front_Alt", alt_boxes, "MAT_Glass_BlueGray_Alt", "APT_WINDOWS", 0.006)
    box_batch("Windows_Front_LitGlass", lit_glass_boxes, "MAT_Glass_WarmInterior", "APT_WINDOWS", 0.004)
    box_batch("Windows_Front_WarmInterior", lit_boxes, "MAT_Glass_WarmInterior", "APT_WINDOWS", 0.003)
    box_batch("Windows_Front_Interior", interior_boxes, "MAT_Interior_Dark", "APT_WINDOWS", 0.0)
    box_batch("Windows_Front_Frames", frame_boxes, "MAT_Metal_AnodizedDark", "APT_WINDOWS", 0.007)
    box_batch("Facade_Front_DeepRecesses", recess_boxes, "MAT_Panel_Charcoal", "APT_FACADE", 0.012)
    box_batch("Facade_Front_ServiceLouvers", louver_boxes, "MAT_Metal_AnodizedDark", "APT_FACADE", 0.006)


def build_front_facade() -> None:
    _front_windows()
    # One coplanar masonry skin per facade: the windows are literal gaps in it.
    skins = {"L": [], "R": [], "C": []}
    skin_openings = {"L": [], "R": [], "C": []}
    bays = ((-5.70, 3.50, 2.15, "living"), (-1.80, 2.00, 1.55, "bedroom"),
            (0.45, 1.45, 1.20, "small"), (3.25, 3.10, 2.20, "balcony"),
            (6.25, 1.20, 0.80, "service"))
    for floor in range(28):
        z = 9.30 + floor*3.0
        upper = floor >= 24
        for wi, wing in enumerate((-13.75, 13.75)):
            key = "L" if wi == 0 else "R"
            openings = []
            for local_x, base_width, height, kind in bays:
                width = base_width
                if upper and wi == 1 and kind in {"living", "balcony"}:
                    width *= 1.10
                elif upper and wi == 0 and kind == "living":
                    width *= 1.04
                openings.append((wing+local_x, width, height))
                skin_openings[key].append((wing+local_x, z, width, height))
            skins[key].extend(_facade_skin_row_y(-10.88, -1.0, wing-9.25, wing+9.25, z, openings))
        core_openings = [(-1.55, 1.18, 1.62), (1.55, 1.18, 1.62)]
        skins["C"].extend(_facade_skin_row_y(-8.55, -1.0, -4.50, 4.50, z, core_openings, thickness=0.30))
        skin_openings["C"].extend((x, z, width, height) for x, width, height in core_openings)
    _skin_surface_y("Facade_Front_Skin_LeftGray", skins["L"], -10.88, -1.0,
                    skin_openings["L"], 0.38, "MAT_Wall_LightGray")
    _skin_surface_y("Facade_Front_Skin_RightIvory", skins["R"], -10.88, -1.0,
                    skin_openings["R"], 0.38, "MAT_Wall_WarmIvory")
    _skin_surface_y("Facade_Front_Skin_CoreCharcoal", skins["C"], -8.55, -1.0,
                    skin_openings["C"], 0.30, "MAT_Panel_MidCharcoal")

    # Only full-building boundary accents remain; no window-by-window exoskeleton.
    verticals = [((x, -11.00, 50.0), (0.34, 0.24, 83.0)) for x in (-22.82, 22.82)]
    box_batch("Facade_Frame_Charcoal_Front", verticals, "MAT_Panel_Charcoal", "APT_FACADE", 0.014)
    core_edges = [((-4.34, -8.69, 50.0), (0.30, 0.28, 83.0)),
                  ((4.34, -8.69, 50.0), (0.30, 0.28, 83.0))]
    box_batch("Facade_Core_RecessEdges", core_edges, "MAT_Panel_Charcoal", "APT_FACADE", 0.014)
    # Do not lay thin decorative strips across the top-floor wall.  At oblique
    # angles they read as broken or floating lines rather than architecture.


def _rear_windows() -> None:
    """Regular functional rear module: bed, kitchen, service balcony and louver."""
    glass, alt, interiors, frames, reveals, recesses, louvers = [], [], [], [], [], [], []
    surface = Vector((0.0, 10.88, 0.0))
    normal = Vector((0.0, 1.0, 0.0))
    bays = ((-6.10, 1.90, 1.55, "bedroom"), (-3.55, 1.25, 0.95, "kitchen"),
            (-1.15, 2.35, 1.90, "service_balcony"), (2.00, 1.75, 1.45, "bedroom"),
            (4.65, 1.30, 0.78, "louver"), (6.55, 1.05, 0.82, "utility"))
    for floor in range(28):
        z = 9.30 + floor * 3.0
        for wi, wing in enumerate((-13.75, 13.75)):
            for bi, (local_x, width, height, kind) in enumerate(bays):
                x = wing + local_x
                depth = 0.52 if kind == "service_balcony" else 0.20
                frame_pt = _inset_point((x, surface.y, z), normal, 0.14)
                glass_pt = _inset_point((x, surface.y, z), normal, depth)
                interior_pt = _inset_point((x, surface.y, z), normal,
                                           0.59 if kind == "service_balcony" else 0.26)
                if kind != "louver":
                    (alt if (floor + bi + wi) % 13 == 0 else glass).append(
                        ((x, glass_pt.y, z), (width, 0.055, height)))
                    interiors.append(((x, interior_pt.y, z), (max(0.35, width-0.14), 0.03, height-0.12)))
                    frames.extend(_window_frame_boxes(x, frame_pt.y, z, width, height, True, 0.032))
                    if kind in {"bedroom", "service_balcony"}:
                        frames.append(((x, frame_pt.y, z), (0.018, 0.05, height)))
                    if kind == "service_balcony":
                        recesses.append(((x, interior_pt.y-0.02, z), (width+0.18, 0.03, height+0.16)))
                    _record_window_depth("rear", kind, (x, surface.y, z), normal,
                                         frame_pt, glass_pt, interior_pt,
                                         (0.40, 0.70) if kind == "service_balcony" else (0.15, 0.25))
                else:
                    for offset in (-0.20, 0.0, 0.20):
                        louvers.append(((x, frame_pt.y, z+offset), (width, 0.05, 0.025)))
                reveals.extend(_window_reveal_boxes(x, surface.y, frame_pt.y, z, width, height, 0.09))

        core_surface = Vector((0.0, 11.00, z))
        for x in (-1.45, 1.45):
            frame_pt = _inset_point((x, core_surface.y, z), normal, 0.14)
            glass_pt = _inset_point((x, core_surface.y, z), normal, 0.20)
            interior_pt = _inset_point((x, core_surface.y, z), normal, 0.25)
            glass.append(((x, glass_pt.y, z), (1.05, 0.055, 1.62)))
            interiors.append(((x, interior_pt.y, z), (0.90, 0.03, 1.47)))
            frames.extend(_window_frame_boxes(x, frame_pt.y, z, 1.05, 1.62, True, 0.03))
            frames.append(((x, frame_pt.y, z), (0.018, 0.05, 1.62)))
            reveals.extend(_window_reveal_boxes(x, core_surface.y, frame_pt.y, z, 1.05, 1.62, 0.09))
            _record_window_depth("rear_core", "stair", (x, core_surface.y, z), normal,
                                 frame_pt, glass_pt, interior_pt, (0.15, 0.25))

    box_batch("Windows_Rear_Glass", glass, "MAT_Glass_BlueGray", "APT_WINDOWS", 0.006)
    box_batch("Windows_Rear_Alt", alt, "MAT_Glass_BlueGray_Alt", "APT_WINDOWS", 0.006)
    box_batch("Windows_Rear_Interior", interiors, "MAT_Interior_Dark", "APT_WINDOWS", 0.0)
    box_batch("Windows_Rear_Frames", frames, "MAT_Metal_AnodizedDark", "APT_WINDOWS", 0.007)
    box_batch("Facade_Rear_ServiceRecesses", recesses, "MAT_Panel_Charcoal", "APT_FACADE", 0.012)
    box_batch("Facade_Rear_ServiceLouvers", louvers, "MAT_Metal_AnodizedDark", "APT_FACADE", 0.006)


def build_rear_facade() -> None:
    _rear_windows()
    regions: dict[str, dict] = {}

    def region(key: str, material: str):
        return regions.setdefault(key, {"rects": [], "openings": [], "material": material})

    bays = ((-6.10, 1.90, 1.55), (-3.55, 1.25, 0.95), (-1.15, 2.35, 1.90),
            (2.00, 1.75, 1.45), (4.65, 1.30, 0.78), (6.55, 1.05, 0.82))
    # Each wing repeats the same apartment hierarchy.  Color changes align to
    # whole service zones, never to arbitrary individual windows.
    zones = (
        ("bed_a", -9.25, -4.50, (0,)),
        ("service", -4.50, 0.50, (1, 2)),
        ("bed_b", 0.50, 3.50, (3,)),
        ("louver", 3.50, 5.75, (4,)),
        ("utility", 5.75, 9.25, (5,)),
    )
    for floor in range(28):
        z = 9.30 + floor*3.0
        for wi, wing in enumerate((-13.75, 13.75)):
            wing_key = "L" if wi == 0 else "R"
            base_mat = "MAT_Wall_LightGray" if wi == 0 else "MAT_Wall_WarmIvory"
            service_mat = "MAT_Wall_WarmIvory" if wi == 0 else "MAT_Wall_LightGray"
            for zone_name, local_min, local_max, indices in zones:
                material = ("MAT_Panel_MidCharcoal" if zone_name == "louver"
                            else service_mat if zone_name == "service" else base_mat)
                target = region(f"{wing_key}_{zone_name}", material)
                openings = [(wing+bays[i][0], bays[i][1], bays[i][2]) for i in indices]
                target["rects"].extend(_facade_skin_row_y(
                    10.88, 1.0, wing+local_min, wing+local_max, z, openings))
                target["openings"].extend((x, z, width, height)
                                           for x, width, height in openings)
        core_openings = [(-1.45, 1.05, 1.62), (1.45, 1.05, 1.62)]
        core = region("C_core", "MAT_Panel_MidCharcoal")
        core["rects"].extend(_facade_skin_row_y(
            11.00, 1.0, -4.50, 4.50, z, core_openings, thickness=0.30))
        core["openings"].extend((x, z, width, height)
                                for x, width, height in core_openings)
    for key, data in regions.items():
        is_core = key == "C_core"
        _skin_surface_y(f"Facade_Rear_Module_{key}", data["rects"],
                        11.00 if is_core else 10.88, 1.0, data["openings"],
                        0.30 if is_core else 0.38, data["material"])
    verticals = [((x, 11.00, 50.0), (0.34, 0.24, 83.0)) for x in (-22.82, 22.82)]
    box_batch("Facade_Frame_Charcoal_Rear", verticals,
              "MAT_Panel_MidCharcoal", "APT_FACADE", 0.014)


def build_side_facades() -> None:
    for side in (-1, 1):
        surface_x = side * 23.42
        normal = Vector((float(side), 0.0, 0.0))
        glass, interiors, frames, reveals = [], [], [], []
        light_rects, charcoal_rects, service_rects = [], [], []
        charcoal_openings, service_openings = [], []
        for floor in range(28):
            z = 9.3 + floor*3.0
            # Exactly two vertically aligned bands: stair and bathroom/service.
            for y, width, height, kind in ((-2.80, 1.30, 1.62, "stair"),
                                           (5.20, 0.90, 0.65, "service")):
                frame_pt = _inset_point((surface_x, y, z), normal, 0.12)
                glass_pt = _inset_point((surface_x, y, z), normal, 0.20)
                interior_pt = _inset_point((surface_x, y, z), normal, 0.28)
                glass.append(((glass_pt.x, y, z), (0.055, width, height)))
                interiors.append(((interior_pt.x, y, z), (0.03, width-0.12, height-0.12)))
                frames.extend(_window_frame_boxes(frame_pt.x, y, z, width, height, False, 0.03))
                if kind == "stair":
                    frames.append(((frame_pt.x, y, z), (0.05, 0.018, height)))
                reveals.extend(_side_window_reveal_boxes(surface_x, frame_pt.x, y, z, width, height, 0.09))
                _record_window_depth("right" if side > 0 else "left", kind,
                                     (surface_x, y, z), normal, frame_pt, glass_pt,
                                     interior_pt, (0.15, 0.25))
            # 70% bright wall, a 3.2m stair band and a 2.8m warm service band.
            for y_min, y_max in ((-9.95, -4.40), (-1.20, 3.80), (6.60, 10.15)):
                center_x = surface_x - float(side)*0.19
                light_rects.append(((center_x, (y_min+y_max)/2, z),
                                    (0.38, y_max-y_min, 3.0)))
            charcoal_rects.extend(_facade_skin_row_x(
                surface_x, float(side), -4.40, -1.20, z, [(-2.80, 1.30, 1.62)]))
            charcoal_openings.append((-2.80, z, 1.30, 1.62))
            service_rects.extend(_facade_skin_row_x(
                surface_x, float(side), 3.80, 6.60, z, [(5.20, 0.90, 0.65)]))
            service_openings.append((5.20, z, 0.90, 0.65))
        suffix = "R" if side > 0 else "L"
        box_batch(f"Side_{suffix}_AlignedWindows", glass, "MAT_Glass_BlueGray_Alt", "APT_WINDOWS", 0.006)
        box_batch(f"Side_{suffix}_InteriorBlackout", interiors, "MAT_Interior_Dark", "APT_WINDOWS", 0.0)
        box_batch(f"Side_{suffix}_WindowFrames", frames, "MAT_Metal_AnodizedDark", "APT_WINDOWS", 0.007)
        _skin_surface_x(f"Side_{suffix}_BrightWall", light_rects, surface_x, float(side),
                        [], 0.38, "MAT_Wall_LightGray")
        _skin_surface_x(f"Side_{suffix}_StairBand", charcoal_rects, surface_x, float(side),
                        charcoal_openings, 0.38, "MAT_Panel_MidCharcoal")
        _skin_surface_x(f"Side_{suffix}_ServiceBand", service_rects, surface_x, float(side),
                        service_openings, 0.38, "MAT_Wall_WarmIvory")


def create_entrance() -> None:
    # Two-storey lobby glazing layered over the stone podium.
    add_box("Entrance_LobbyGlass", (0, -12.06, 3.75), (13.6, 0.18, 6.45),
            "MAT_Glass_BlueGray", "APT_PODIUM", 0.015)
    add_box("Entrance_LobbyInterior", (0, -11.77, 3.75), (13.0, 0.08, 6.0),
            "MAT_Interior_Dark", "APT_PODIUM", 0.0)
    mullions = []
    for x in (-6.7, -4.45, -2.2, 0, 2.2, 4.45, 6.7):
        mullions.append(((x, -12.19, 3.75), (0.11, 0.15, 6.5)))
    mullions.append(((0, -12.19, 5.05), (13.5, 0.15, 0.11)))
    box_batch("Entrance_LobbyMullions", mullions, "MAT_Metal_AnodizedDark", "APT_PODIUM", 0.008)
    add_box("Entrance_Canopy", (0, -14.1, 6.72), (11.2, 4.1, 0.48),
            "MAT_Wall_WarmIvory", "APT_PODIUM", 0.10)
    add_box("Entrance_CanopySoffit", (0, -14.1, 6.48), (10.75, 3.75, 0.12),
            "MAT_Concrete_Soffit", "APT_PODIUM", 0.035)
    add_box("Entrance_CanopyBronzeLiner", (0, -15.92, 6.48), (10.2, 0.12, 0.18),
            "MAT_Metal_DarkBronze", "APT_PODIUM", 0.012)
    columns = [((-5.15, -14.25, 3.35), (0.50, 0.72, 6.7)), ((5.15, -14.25, 3.35), (0.50, 0.72, 6.7))]
    box_batch("Entrance_CanopyColumns", columns, "MAT_Stone_WarmGray", "APT_PODIUM", 0.08)
    light_strips = [((0, -13.2, 6.38), (9.5, 0.055, 0.055)), ((0, -15.2, 6.38), (9.5, 0.055, 0.055))]
    box_batch("Entrance_IndirectLightGrooves", light_strips, "MAT_IndirectLight", "APT_PODIUM", 0.01)
    door_frames = [((-1.1, -12.32, 2.25), (0.10, 0.10, 3.0)), ((1.1, -12.32, 2.25), (0.10, 0.10, 3.0)),
                   ((0, -12.32, 3.72), (2.3, 0.10, 0.10))]
    box_batch("Entrance_DoorFrames", door_frames, "MAT_Metal_DarkBronze", "APT_PODIUM", 0.008)


def create_roof_crown() -> None:
    # A continuous parapet makes the roof read as constructed architecture,
    # not three boxes with decorative rods floating above them.
    parapets = [
        ((0.0, -10.25, 92.45), (45.5, 0.55, 1.35)),
        ((0.0, 10.15, 92.35), (45.5, 0.55, 1.15)),
        ((-22.45, 0.0, 92.40), (0.55, 20.0, 1.25)),
        ((22.45, 0.0, 92.40), (0.55, 20.0, 1.25)),
    ]
    box_batch("Roof_ContinuousParapet", parapets, "MAT_Wall_LightGray", "APT_ROOF", 0.035)

    # Mechanical heights are reduced by roughly 17%; only the central core stays charcoal.
    box_batch("Roof_MechanicalCore",
              [((3.0, 1.4, 95.70), (8.5, 9.0, 6.8))],
              "MAT_Panel_MidCharcoal", "APT_ROOF", 0.035)
    light_cores = [((-10.0, 0.8, 94.90), (12.5, 11.0, 5.2)),
                   ((13.0, 0.5, 94.70), (8.2, 10.5, 4.8))]
    box_batch("Roof_MechanicalCore_Light", light_cores,
              "MAT_Wall_LightGray", "APT_ROOF", 0.035)
    light_caps = [((-10.0, 0.8, 97.66), (13.1, 11.6, 0.32)),
                  ((3.0, 1.4, 99.26), (9.1, 9.6, 0.34)),
                  ((13.0, 0.5, 97.26), (8.8, 11.1, 0.30))]
    box_batch("Roof_MechanicalCaps", light_caps, "MAT_Wall_LightGray", "APT_ROOF", 0.025)

    # No detached crown portals, rear screens or side fins.  Every visible roof
    # element now sits on a mechanical volume or is the continuous parapet.
    louvers = []
    for x in [i * 0.64 - 3.2 for i in range(11)]:
        louvers.append(((x+3.0, -3.22, 96.2), (0.14, 0.38, 6.2)))
    box_batch("Roof_VerticalLouvers", louvers, "MAT_Metal_AnodizedDark", "APT_ROOF", 0.008)


def create_signage() -> None:
    curve = bpy.data.curves.new("Sign_UnitNumber_101_Curve", "FONT")
    curve.body = "101"
    curve.align_x = "CENTER"
    curve.size = 2.4
    curve.extrude = 0.035
    curve.bevel_depth = 0.018
    text = bpy.data.objects.new("Sign_UnitNumber_101", curve)
    collection("APT_SIGNAGE").objects.link(text)
    text.data.materials.append(MATERIALS["MAT_Metal_AnodizedDark"])
    text.location = (23.28, -4.0, 22.0)
    text.rotation_euler = (math.radians(90), 0, math.radians(90))
    text["fictional_replaceable_sign"] = True


def create_optional_context() -> None:
    add_box("Context_Ground", (0, 0, -0.18), (92, 75, 0.35), "MAT_Paving", "APT_OPTIONAL_CONTEXT", 0.02)
    add_box("Context_FrontWalk", (0, -20.0, 0.06), (62, 9.0, 0.14), "MAT_Wall_LightGray", "APT_OPTIONAL_CONTEXT", 0.025)
    # Restrained trees placed away from key facade bays.
    trunks, crowns = [], []
    for x, y, h in ((-34,-18,5.5),(34,-17,6.0),(-33,15,6.5),(35,14,5.8)):
        trunks.append(((x,y,h*0.32),(0.45,0.45,h*0.64)))
        crowns.append(((x,y,h*0.82),(3.4,3.4,h*0.72)))
    box_batch("Context_TreeTrunks", trunks, "MAT_Stone_WarmGray", "APT_OPTIONAL_CONTEXT", 0.18)
    box_batch("Context_TreeCrowns", crowns, "MAT_Green", "APT_OPTIONAL_CONTEXT", 0.75)
    people = []
    for x in (-4.0, 3.2, 7.5):
        people.extend([((x,-18.0,0.82),(0.28,0.24,1.45)), ((x,-18.0,1.62),(0.34,0.30,0.34))])
    box_batch("Context_PeopleSilhouettes", people, "MAT_Panel_Charcoal", "APT_OPTIONAL_CONTEXT", 0.10)


def _look_at(obj: bpy.types.Object, target: Sequence[float]) -> None:
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def _camera(name: str, location: Sequence[float], target: Sequence[float], lens: float) -> bpy.types.Object:
    data = bpy.data.cameras.new(name + "_Data")
    data.lens = lens
    data.sensor_width = 36.0
    data.dof.use_dof = False
    obj = bpy.data.objects.new(name, data)
    collection("CAMERAS").objects.link(obj)
    obj.location = location
    _look_at(obj, target)
    return obj


def setup_cameras() -> None:
    # Cameras stay level with their targets so the architectural verticals do
    # not converge; distance provides generous podium/crown breathing room.
    _camera("CAM_Front", (0, -225, 50), (0, 0, 50), 62)
    _camera("CAM_Back", (0, 225, 50), (0, 0, 50), 62)
    _camera("CAM_Side", (-225, 7, 50), (0, 3, 50), 62)
    _camera("CAM_Side_L", (-225, 3, 50), (0, 3, 50), 62)
    _camera("CAM_Side_R", (225, 3, 50), (0, 3, 50), 62)
    _camera("CAM_Perspective", (135, -170, 50), (0, 0, 50), 62)
    _camera("CAM_RearPerspective", (-135, 170, 50), (0, 0, 50), 62)
    _camera("CAM_Entrance", (28, -43, 4.3), (0, -11.5, 4.3), 48)
    _camera("CAM_Rooftop", (66, -74, 96), (0, 0, 96), 58)
    _camera("CAM_FrontWindowClose", (13.75, -62, 46), (13.75, -10.5, 46), 74)
    _camera("CAM_RearWindowClose", (-13.75, 62, 46), (-13.75, 10.5, 46), 74)
    _camera("CAM_SideWindowClose", (-62, 4.0, 46), (-23.1, 4.0, 46), 72)


def setup_review_lighting() -> None:
    world = bpy.context.scene.world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputWorld")
    bg = nodes.new("ShaderNodeBackground")
    bg.inputs["Color"].default_value = (0.34, 0.43, 0.56, 1.0)
    bg.inputs["Strength"].default_value = 0.55
    world.node_tree.links.new(bg.outputs["Background"], out.inputs["Surface"])
    sun_data = bpy.data.lights.new("Sun_Day_Data", "SUN")
    sun_data.energy = 2.0
    sun_data.angle = math.radians(5.0)
    sun = bpy.data.objects.new("Sun_Day", sun_data)
    collection("LIGHTING").objects.link(sun)
    sun.rotation_euler = (math.radians(32), math.radians(-18), math.radians(-34))
    area_data = bpy.data.lights.new("Area_FacadeFill_Data", "AREA")
    area_data.energy = 1500
    area_data.shape = "DISK"
    area_data.size = 28
    area = bpy.data.objects.new("Area_FacadeFill", area_data)
    collection("LIGHTING").objects.link(area)
    area.location = (-38, -52, 58)
    _look_at(area, (0, 0, 42))


def configure_polyhaven_hdri(mode: str = "day") -> bool:
    """Use the documented 2K HDRIs through project-relative paths."""
    path = HDRI_DUSK if mode == "dusk" else HDRI_DAY
    if not path.exists():
        return False
    world = bpy.context.scene.world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    links = world.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputWorld")
    out.name = "OSC_World_Output"
    bg = nodes.new("ShaderNodeBackground")
    bg.name = "OSC_HDRI_Background"
    camera_bg = nodes.new("ShaderNodeBackground")
    camera_bg.name = "OSC_Camera_Background"
    camera_bg.inputs["Color"].default_value = ((0.12, 0.17, 0.24, 1.0) if mode == "dusk"
                                                 else (0.32, 0.43, 0.56, 1.0))
    camera_bg.inputs["Strength"].default_value = 0.32 if mode == "dusk" else 0.7
    env = nodes.new("ShaderNodeTexEnvironment")
    env.name = "OSC_PolyHaven_Environment"
    light_path = nodes.new("ShaderNodeLightPath")
    light_path.name = "OSC_LightPath"
    mix = nodes.new("ShaderNodeMixShader")
    mix.name = "OSC_CameraBackground_Mix"
    existing_image = bpy.data.images.get(path.name)
    if existing_image:
        bpy.data.images.remove(existing_image)
    image = bpy.data.images.load(str(path), check_existing=False)
    # The final .blend lives in Blender/output, so this remains stable when
    # the repository moves.  Packing also keeps checkpoints self-contained.
    if not image.packed_file:
        image.pack()
    image.filepath = "//../assets/polyhaven/hdri/" + path.name
    env.image = image
    links.new(env.outputs["Color"], bg.inputs["Color"])
    links.new(light_path.outputs["Is Camera Ray"], mix.inputs[0])
    links.new(bg.outputs["Background"], mix.inputs[1])
    links.new(camera_bg.outputs["Background"], mix.inputs[2])
    links.new(mix.outputs["Shader"], out.inputs["Surface"])
    world["polyhaven_asset_id"] = "suburban_parking_area" if mode == "dusk" else "cambridge"
    world["polyhaven_resolution"] = "2k"
    world["polyhaven_local_path"] = "//../assets/polyhaven/hdri/" + path.name
    return True


def configure_world(mode: str) -> None:
    world = bpy.context.scene.world
    world.use_nodes = True
    has_hdri = configure_polyhaven_hdri(mode)
    bg = world.node_tree.nodes.get("OSC_HDRI_Background")
    if not has_hdri or not bg:
        setup_review_lighting()
        bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    sun = bpy.data.objects.get("Sun_Day")
    fill = bpy.data.objects.get("Area_FacadeFill")
    warm_mat = bpy.data.materials.get("MAT_Glass_WarmInterior")
    warm_node = (next((n for n in warm_mat.node_tree.nodes
                       if n.type in {"EMISSION", "BSDF_PRINCIPLED"}), None)
                 if warm_mat and warm_mat.use_nodes else None)
    if mode == "dusk":
        if not has_hdri:
            bg.inputs["Color"].default_value = (0.035, 0.055, 0.105, 1.0)
        bg.inputs["Strength"].default_value = 0.22 if has_hdri else 0.25
        if sun:
            sun.data.energy = 0.45
            sun.data.color = (1.0, 0.34, 0.16)
        if fill:
            fill.data.energy = 850
            fill.data.color = (0.30, 0.42, 1.0)
        if warm_node:
            target = "Strength" if warm_node.type == "EMISSION" else "Emission Strength"
            warm_node.inputs[target].default_value = 3.5
    else:
        if not has_hdri:
            bg.inputs["Color"].default_value = (0.34, 0.43, 0.56, 1.0)
        bg.inputs["Strength"].default_value = 0.42 if has_hdri else 0.55
        if sun:
            sun.data.energy = 2.0
            sun.data.color = (1.0, 0.82, 0.68)
        if fill:
            fill.data.energy = 1500
            fill.data.color = (1.0, 0.90, 0.78)
        if warm_node:
            target = "Strength" if warm_node.type == "EMISSION" else "Emission Strength"
            warm_node.inputs[target].default_value = 0.75


def configure_cycles_gpu() -> str:
    """Enable the first available RTX backend, preferring OptiX over CUDA."""
    prefs = bpy.context.preferences.addons["cycles"].preferences
    backend_used = ""
    for backend in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = backend
            prefs.get_devices()
            if any(device.type == backend for device in prefs.devices):
                backend_used = backend
                break
        except Exception:
            continue
    if not backend_used:
        raise RuntimeError("No OptiX or CUDA Cycles device is available")
    for device in prefs.devices:
        device.use = device.type == backend_used
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "GPU"
    scene["cycles_compute_backend"] = backend_used
    return backend_used


def save_checkpoint(filename: str) -> None:
    path = CHECKPOINTS / filename
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


def _render(camera_name: str, filename: str, mode: str = "day", final: bool = False) -> None:
    scene = bpy.context.scene
    scene.camera = bpy.data.objects[camera_name]
    configure_world(mode)
    scene.render.resolution_x = 1920 if final else 960
    scene.render.resolution_y = 1920 if final else 960
    scene.render.resolution_percentage = 100
    if final:
        configure_cycles_gpu()
        scene.cycles.samples = 64
        scene.cycles.use_denoising = True
        scene.cycles.max_bounces = 5
        scene.cycles.diffuse_bounces = 2
        scene.cycles.glossy_bounces = 2
        scene.cycles.transmission_bounces = 2
        scene.render.image_settings.color_depth = "8"
    else:
        try:
            scene.render.engine = "BLENDER_EEVEE_NEXT"
        except TypeError:
            scene.render.engine = "BLENDER_EEVEE"
    scene.render.filepath = str(REVIEW / filename)
    bpy.ops.render.render(write_still=True)


def render_massing_review() -> None:
    _render("CAM_Front", "01_massing_front.png")
    _render("CAM_Perspective", "01_massing_perspective.png")


def render_facade_review() -> None:
    for camera, filename in (("CAM_Front", "02_facade_front.png"), ("CAM_Back", "02_facade_back.png"),
                             ("CAM_Side", "02_facade_side.png"), ("CAM_Perspective", "02_facade_perspective.png")):
        _render(camera, filename)


def render_final_cycles() -> None:
    scene = bpy.context.scene
    configure_cycles_gpu()
    jobs = [
        ("CAM_Front", "final_front_day.png", "day"),
        ("CAM_Back", "final_back_day.png", "day"),
        ("CAM_Side", "final_side_day.png", "day"),
        ("CAM_Perspective", "final_perspective_day.png", "day"),
        ("CAM_Entrance", "final_entrance_day.png", "day"),
        ("CAM_Rooftop", "final_rooftop_day.png", "day"),
        ("CAM_Perspective", "final_perspective_dusk.png", "dusk"),
        ("CAM_Entrance", "final_entrance_dusk.png", "dusk"),
    ]
    for camera, filename, mode in jobs:
        _render(camera, filename, mode, True)


def render_integrated_review_views() -> None:
    """Ten requested audit views for the all-facade integrated revision."""
    jobs = [
        ("CAM_Front", "integrated_front_near_ortho.png"),
        ("CAM_Perspective", "integrated_front_3q.png"),
        ("CAM_Back", "integrated_rear_front.png"),
        ("CAM_RearPerspective", "integrated_rear_3q.png"),
        ("CAM_Side_L", "integrated_left_ortho.png"),
        ("CAM_Side_R", "integrated_right_ortho.png"),
        ("CAM_FrontWindowClose", "integrated_front_windows_close.png"),
        ("CAM_RearWindowClose", "integrated_rear_windows_close.png"),
        ("CAM_SideWindowClose", "integrated_side_windows_close.png"),
        ("CAM_Rooftop", "integrated_rooftop_3q_close.png"),
    ]
    configure_cycles_gpu()
    for camera, filename in jobs:
        _render(camera, filename, "day", True)


def validate_window_depths() -> dict:
    failures = []
    facade_summary: dict[str, dict] = {}
    for record in WINDOW_DEPTH_RECORDS:
        surface = Vector(record["surface"])
        normal = Vector(record["normal"])
        frame_depth = (surface - Vector(record["frame"])).dot(normal)
        glass_depth = (surface - Vector(record["glass"])).dot(normal)
        interior_depth = (surface - Vector(record["interior"])).dot(normal)
        low, high = record["glass_range"]
        checks = {
            "frame_inside_opening": 0.0 < frame_depth < glass_depth,
            "glass_inside_wall": low - 1e-5 <= glass_depth <= high + 1e-5,
            "interior_behind_glass": interior_depth > glass_depth,
            "not_floating_outside": frame_depth > 0.0 and glass_depth > 0.0,
        }
        if not all(checks.values()):
            failures.append({"facade": record["facade"], "kind": record["kind"],
                             "depths": (round(frame_depth, 3), round(glass_depth, 3),
                                        round(interior_depth, 3)), "checks": checks})
        summary = facade_summary.setdefault(record["facade"], {
            "samples": 0, "glass_min_m": 99.0, "glass_max_m": 0.0,
            "normal": tuple(round(v, 2) for v in normal),
        })
        summary["samples"] += 1
        summary["glass_min_m"] = min(summary["glass_min_m"], glass_depth)
        summary["glass_max_m"] = max(summary["glass_max_m"], glass_depth)
    for summary in facade_summary.values():
        summary["glass_min_m"] = round(summary["glass_min_m"], 3)
        summary["glass_max_m"] = round(summary["glass_max_m"], 3)
    result = {"valid": not failures, "sample_count": len(WINDOW_DEPTH_RECORDS),
              "facades": facade_summary, "failures": failures[:20]}
    bpy.context.scene["window_depth_validation"] = str(result)
    return result


def validate_scene() -> dict:
    required_objects = (
        "Body_PrimaryMassing", "Podium_Main", "Entrance_LobbyGlass", "Entrance_Canopy",
        "Roof_MechanicalCore", "Roof_ContinuousParapet", "Sign_UnitNumber_101",
        "WIN_Living_Large", "WIN_Bedroom_Medium", "WIN_Bedroom_Small",
        "WIN_Balcony_Recessed", "WIN_Service_Narrow", "WIN_Core_Stair",
    )
    missing = [name for name in required_objects if name not in bpy.data.objects]
    required_materials = (
        "MAT_Wall_WarmIvory", "MAT_Wall_LightGray", "MAT_Panel_Charcoal",
        "MAT_Metal_DarkBronze", "MAT_Glass_BlueGray", "MAT_Stone_WarmGray",
        "MAT_Metal_AnodizedDark", "MAT_Concrete_Soffit",
    )
    missing_materials = [name for name in required_materials if name not in bpy.data.materials]
    dims_ok = True
    all_mesh = [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.hide_render]
    if all_mesh:
        corners = [o.matrix_world @ Vector(c) for o in all_mesh for c in o.bound_box]
        min_v = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
        max_v = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
        building_dims = (max_v.x-min_v.x, max_v.y-min_v.y, max_v.z-min_v.z)
        dims_ok = 90.0 <= max_v.z <= 105.0
    else:
        building_dims = (0, 0, 0)
        dims_ok = False
    depth_result = validate_window_depths()
    result = {
        "missing_objects": missing, "missing_materials": missing_materials,
        "visible_mesh_count": len(all_mesh), "bounds_with_context_m": tuple(round(v, 2) for v in building_dims),
        "height_contract_ok": dims_ok, "window_depth_validation": depth_result,
        "valid": not missing and not missing_materials and dims_ok and depth_result["valid"],
    }
    bpy.context.scene["validation"] = str(result)
    return result


def build_architecture(render_reviews: bool = True) -> dict:
    setup_scene()
    create_materials()
    create_primary_massing()
    create_podium()
    setup_cameras()
    setup_review_lighting()
    save_checkpoint("01_massing.blend")
    if render_reviews:
        render_massing_review()
    create_window_modules()
    build_front_facade()
    build_rear_facade()
    build_side_facades()
    save_checkpoint("02_facade.blend")
    if render_reviews:
        render_facade_review()
    create_entrance()
    create_roof_crown()
    create_signage()
    create_optional_context()
    save_checkpoint("03_architecture_complete.blend")
    result = validate_scene()
    configure_polyhaven_hdri("day")
    save_checkpoint("04_polyhaven_lookdev.blend")
    save_checkpoint("05_materials_final.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(FINAL_BLEND))
    return result


def main() -> None:
    result = build_architecture(render_reviews=True)
    print("OPENSIMCITY_APARTMENT_VALIDATION", result)


if __name__ == "__main__":
    main()
