"""Export selected user-authored Seoul asset collections as normalized FBX files.

Run against a source .blend in background mode. The source file is never saved;
objects are duplicated into a temporary collection, joined to one renderer and
grounded at Z=0 before export.
"""

import os
import sys
import bpy
from mathutils import Vector


ASSET_NAMES = (
    "KR_BLDG_Mixed_01",
    "KR_BLDG_Mixed_02",
    "KR_BLDG_Mixed_03",
    "KR_BLDG_Mixed_04",
    "KR_BLDG_Mixed_05",
    "KR_BLDG_Mixed_06",
    "KR_BLDG_Office_02",
    "KR_BLDG_Office_05",
    "KR_BLDG_Office_08",
    "KR_APT_Point_18F",
    "KR_APT_Point_25F",
    "KR_APT_Point_32F",
    "KR_APT_Slab_12F",
    "KR_APT_Slab_18F",
    "KR_Car_Black",
    "KR_Car_Blue",
    "KR_Car_Silver",
    "KR_Car_Taxi",
    "KR_Car_White",
    "KR_Bus_Blue",
    "KR_PROP_Bollard_900_v04",
    "KR_PROP_BusShelter_6m",
    "KR_PROP_Lamp_Double_10m",
    "KR_TRAFFIC_Signal_Mast",
    "KR_TRAFFIC_Signal_Mast_Red",
    "KR_RD_4Lane_Straight40m",
    "KR_RD_6Lane_Straight40m",
    "KR_RD_8Lane_Straight40m",
    "KR_RD_10Lane_Straight40m",
    "KR_RD_8Lane_Intersection64m",
    "KR_VEG_StreetTree_Anchored_1",
    "KR_VEG_StreetTree_Anchored_2",
    "KR_VEG_StreetTree_Anchored_3",
    "KR_RD_Crosswalk_4m",
    "KR_PROP_StreetLight_6m",
    "KR_VEG_StreetTree_Supported_6m",
    "V05_Office_00",
    "V05_Office_03",
    "V05_Office_06",
    "V05_Office_09",
    "V05_mixed_00",
    "V05_mixed_03",
    "V05_mixed_06",
    "V05_mixed_09",
    "V05_villa_00",
    "V05_villa_05",
    "V05_villa_10",
    "V05_villa_15",
    "V05_villa_20",
    "V05_APT_L_Wing24",
    "V05_APT_Slab18",
    "V05_APT_Slab25",
    "V05_APT_SteppedTower",
    "V05_Underpass_DesignModule_NOT_SURVEYED",
)

OBJECT_ASSET_NAMES = (
    "KR_Bridge_-480",
)


def output_folder():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if not args:
        raise RuntimeError("Pass the FBX output folder after '--'.")
    folder = os.path.abspath(args[0])
    os.makedirs(folder, exist_ok=True)
    return folder


def source_objects(value):
    if isinstance(value, bpy.types.Collection):
        return value.all_objects
    return (value,)


def duplicate_join_ground(source, export_name):
    temp = bpy.data.collections.new("OSC_EXPORT_TEMP")
    bpy.context.scene.collection.children.link(temp)
    duplicates = []
    for original in source_objects(source):
        if original.type != "MESH" or original.hide_render:
            continue
        value = original.copy()
        value.data = original.data.copy()
        value.animation_data_clear()
        temp.objects.link(value)
        value.matrix_world = original.matrix_world.copy()
        duplicates.append(value)
    if not duplicates:
        raise RuntimeError(f"Asset '{export_name}' has no exportable meshes.")

    bpy.ops.object.select_all(action="DESELECT")
    for value in duplicates:
        value.select_set(True)
    bpy.context.view_layer.objects.active = duplicates[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = export_name

    corners = [joined.matrix_world @ Vector(corner) for corner in joined.bound_box]
    min_x = min(value.x for value in corners)
    max_x = max(value.x for value in corners)
    min_y = min(value.y for value in corners)
    max_y = max(value.y for value in corners)
    min_z = min(value.z for value in corners)
    joined.location += Vector((-(min_x + max_x) * 0.5, -(min_y + max_y) * 0.5, -min_z))
    return temp, joined


def export_asset(source, export_name, folder):
    temp, joined = duplicate_join_ground(source, export_name)
    try:
        bpy.ops.object.select_all(action="DESELECT")
        joined.select_set(True)
        bpy.context.view_layer.objects.active = joined
        path = os.path.join(folder, export_name + ".fbx")
        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=True,
            object_types={"MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            bake_anim=False,
            path_mode="AUTO",
        )
        print(f"OSC_EXPORTED {export_name} {tuple(round(v, 3) for v in joined.dimensions)} {path}")
    finally:
        for value in list(temp.objects):
            bpy.data.objects.remove(value, do_unlink=True)
        bpy.data.collections.remove(temp)


folder = output_folder()
args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
if "--objects-only" not in args:
    names = (name for name in ASSET_NAMES if name.startswith("V05_")) \
        if "--v05-only" in args else ASSET_NAMES
    for name in names:
        collection = bpy.data.collections.get(name)
        if collection is not None:
            export_asset(collection, name, folder)
for name in OBJECT_ASSET_NAMES:
    value = bpy.data.objects.get(name)
    if value is not None:
        export_asset(value, "KR_Bridge_Seoul", folder)
