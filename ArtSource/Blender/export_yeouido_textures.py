"""Extract selected packed v05 material maps without saving the source blend."""

import os
import sys
import bpy


SUFFIX_BY_SURFACE = {
    "Asphalt": "",
    "Concrete": ".001",
    "Granite": ".002",
    "Plaster": ".003",
    "Bark": ".004",
    "Timber": ".005",
    "Soil": ".006",
}


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
if not args:
    raise RuntimeError("Pass the texture output folder after '--'.")
root = os.path.abspath(args[0])

for surface, suffix in SUFFIX_BY_SURFACE.items():
    folder = os.path.join(root, surface)
    os.makedirs(folder, exist_ok=True)
    for base_name, extension, file_format in (
        ("BaseColor.jpg", ".jpg", "JPEG"),
        ("NormalGL.png", ".png", "PNG"),
        ("Roughness.png", ".png", "PNG"),
    ):
        image = bpy.data.images.get(base_name + suffix)
        if image is None or image.packed_file is None:
            raise RuntimeError(f"Packed v05 image '{base_name + suffix}' is missing.")
        output = os.path.join(folder, os.path.splitext(base_name)[0] + extension)
        previous_path = image.filepath_raw
        previous_format = image.file_format
        try:
            image.filepath_raw = output
            image.file_format = file_format
            image.save()
        finally:
            image.filepath_raw = previous_path
            image.file_format = previous_format
        print(f"OSC_TEXTURE {surface} {base_name + suffix} {output}")
