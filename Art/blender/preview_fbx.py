"""
Renders a model's preview sheet again from its exported FBX and textures, without rebuilding it.
Usage: python Art/blender/preview_fbx.py <Name> <radius in metres>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402

from lib import bake, export, geo  # noqa: E402


def main(name, radius):
    geo.reset()
    bpy.ops.import_scene.fbx(filepath=os.path.join(export.MODELS, name + ".fbx"))
    lod0 = [ob for ob in bpy.data.objects if ob.type == 'MESH' and ob.name.endswith("_LOD0")]
    for ob in list(bpy.data.objects):
        if ob.type != 'MESH' or ob not in lod0:
            bpy.data.objects.remove(ob)
    paths = {k: os.path.join(export.TEXTURES, f"{name}_{k}.png") for k in ("Albedo", "Mask", "Normal")}
    mat = bake.textured_material(name + "_Preview", paths)
    tris = sum(geo.triangles(ob) for ob in lod0)
    export.preview(lod0[0], name, mat, radius=radius, extra=lod0[1:], notes=f"{name}  LOD0 {tris} tri")


if __name__ == "__main__":
    main(sys.argv[1], float(sys.argv[2]))
