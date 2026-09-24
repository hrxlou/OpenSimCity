"""Extract v08 details into game meshes; never save or mutate the source file.

blender -b <v08 Photoreal Integrated v2.blend> --python <this file> -- <model folder>
Evaluates bevels and reduces foliage on copies before exporting metre-scale FBX.
"""
import bpy
import json
import os
import sys
from mathutils import Vector

TARGETS = {
    "KR_VEG_StreetTree_Anchored_1": "V08_StreetTreeA",
    "KR_VEG_StreetTree_Anchored_2": "V08_StreetTreeB",
    "KR_PROP_Bench_1800": "V08_Bench",
    "KR_PROP_WasteBin_800": "V08_WasteBin",
}

folder = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
os.makedirs(folder, exist_ok=True)
source_path = bpy.data.filepath
# Evaluate only the selected source prototypes, not the 25,000-object city scene.
bpy.ops.wm.read_factory_settings(use_empty=True)
with bpy.data.libraries.load(source_path, link=False) as (available, loaded):
    loaded.collections = [name for name in TARGETS if name in available.collections]
for collection in loaded.collections:
    bpy.context.scene.collection.children.link(collection)
report = {"source": os.path.basename(source_path), "assets": []}
depsgraph = bpy.context.evaluated_depsgraph_get()
for collection_name, output_name in TARGETS.items():
    source = bpy.data.collections.get(collection_name)
    if source is None:
        raise RuntimeError("Missing source collection: " + collection_name)
    temporary = bpy.data.collections.new("OSC_GAME_EXPORT")
    bpy.context.scene.collection.children.link(temporary)
    copies = []
    original_vertices = 0
    for original in source.all_objects:
        if original.type != "MESH" or original.hide_render:
            continue
        mesh = bpy.data.meshes.new_from_object(original.evaluated_get(depsgraph))
        value = bpy.data.objects.new(original.name + "_Game", mesh)
        temporary.objects.link(value)
        value.matrix_world = original.matrix_world.copy()
        original_vertices += len(mesh.vertices)
        bpy.ops.object.select_all(action="DESELECT")
        value.select_set(True)
        bpy.context.view_layer.objects.active = value
        if "leav" in original.name.lower() and len(mesh.vertices) > 12000:
            modifier = value.modifiers.new("Game foliage reduction", "DECIMATE")
            modifier.ratio = 0.16
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        copies.append(value)
    if not copies:
        raise RuntimeError("No geometry: " + collection_name)
    bpy.ops.object.select_all(action="DESELECT")
    for value in copies:
        value.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = output_name
    corners = [joined.matrix_world @ Vector(c) for c in joined.bound_box]
    joined.location -= Vector(((min(c.x for c in corners) + max(c.x for c in corners)) / 2,
                               (min(c.y for c in corners) + max(c.y for c in corners)) / 2,
                               min(c.z for c in corners)))
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, output_name + ".fbx"),
                             use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP")
    report["assets"].append({"name": output_name, "collection": collection_name,
                             "evaluated_source_vertices": original_vertices,
                             "export_vertices": len(joined.data.vertices),
                             "dimensions_m": list(joined.dimensions)})
    bpy.data.objects.remove(joined, do_unlink=True)
    bpy.data.collections.remove(temporary)
with open(os.path.join(folder, "v08-export-report.json"), "w") as handle:
    json.dump(report, handle, indent=2)
print("OSC_GAME_EXPORT_COMPLETE", report)
