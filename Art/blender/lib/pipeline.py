"""The common end of every ship-like asset: UVs, bake, LODs, FBX, preview."""
from . import bake, export, geo


def finish(name, hull, extra, tex, radius, lods=(0.3, 0.08), preview=True, ao_distance=1.0, views=None):
    """
    hull: the joined <name>_LOD0 object; extra: glow parts and sockets exported with it.
    Returns the triangle counts per LOD.
    """
    bake.unwrap(hull)
    paths = bake.bake(hull, name, tex, export.TEXTURES, ao_distance=ao_distance)
    lod_objects = [hull] + [geo.decimate(hull, ratio, f"{name}_LOD{i + 1}") for i, ratio in enumerate(lods)]
    export.fbx(lod_objects + list(extra), name)
    tris = tuple(geo.triangles(o) for o in lod_objects)
    print(f"{name}: " + " / ".join(f"LOD{i} {t}" for i, t in enumerate(tris)) + " triangles")
    if preview:
        mat = bake.textured_material(name + "_Preview", paths)
        export.preview(hull, name, mat, radius=radius, notes=f"{name}  " + " / ".join(str(t) for t in tris) + f" tri  tex {tex}", views=views)
    return tris
