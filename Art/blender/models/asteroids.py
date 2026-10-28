"""
Asteroids of the Belt map: five rock shapes (unit size, about 1 m across; the game scales them), each
an icosphere pushed out by two noise layers and a few craters, decimated to three LODs. All five share
one texture atlas: dusty brown-grey rock, darker mottling, bright mineral veins, craters in the normal map.
Meshes: Asteroids_<i>_LOD0/1/2 (i = 0..4).
"""
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
from mathutils import Vector, noise  # noqa: E402

from lib import bake, export, geo, surface  # noqa: E402

NAME = "Asteroids"
TEX = 1024
SHAPES = 5

PALETTE = {
    "ROCK": dict(base=(0.16, 0.13, 0.105), metal=0.05, rough=0.85, panel=0.0, wear=0.15, wear_color=(0.3, 0.27, 0.22),
                 grime=0.6, ao_dist=0.3, bevel=0.0, rock=0.9, rock_scale=1.0, mottle=0.6, mottle_color=(0.07, 0.06, 0.05),
                 vein=0.8, vein_color=(0.5, 0.42, 0.28), bump=0.6),
}


def rock(index):
    rng = random.Random(100 + index)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=0.5)
    seed = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    stretch = Vector((rng.uniform(0.7, 1.3), rng.uniform(0.5, 0.95), rng.uniform(0.8, 1.6)))
    craters = [(Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized(), rng.uniform(0.12, 0.3))
               for _ in range(rng.randint(2, 5))]
    for v in bm.verts:
        d = v.co.normalized()
        r = 0.5
        r += 0.22 * noise.noise(d * 1.1 + seed)
        r += 0.07 * noise.noise(d * 3.5 + seed * 2.0)
        r += 0.015 * noise.noise(d * 11.0 + seed * 3.0)
        for c, size in craters:
            a = (d - c).length
            if a < size:
                x = a / size
                r -= 0.07 * size * 4 * (1 - x * x) - 0.03 * size * 4 * max(0.0, 1 - abs(x - 0.95) * 10)
        v.co = Vector((d.x * stretch.x, d.y * stretch.y, d.z * stretch.z)) * r
    ob = geo.from_bmesh(f"{NAME}_{index}_LOD0", bm, "ROCK")
    for p in ob.data.polygons:
        p.use_smooth = True
    # down to a game budget for LOD0, keeping the silhouette
    m = ob.modifiers.new("Decimate", 'DECIMATE')
    m.ratio = 0.24
    geo._apply(ob, m)
    return ob


def main(preview=True):
    geo.reset()
    surface.make_all(PALETTE)
    rocks = [rock(i) for i in range(SHAPES)]
    for i, r in enumerate(rocks):
        geo.tag_part(r, i)
        r.location = geo.to_blender((i * 1.6 - 3.2, 0, 0))
        geo.apply_transform(r)
    joined = geo.join(rocks, NAME + "_Bake")
    bake.unwrap(joined, margin=0.004)
    paths = bake.bake(joined, NAME, TEX, export.TEXTURES, ao_distance=0.4)
    parts = geo.split_parts(joined, [f"{NAME}_{i}_LOD0" for i in range(SHAPES)])
    # each part back to its own centre (they were spread out for the bake)
    for i, ob in enumerate(parts):
        geo.recenter(ob, geo.to_blender((i * 1.6 - 3.2, 0, 0)))
    objects = []
    tris = []
    for i, ob in enumerate(parts):
        lod1 = geo.decimate(ob, 0.3, f"{NAME}_{i}_LOD1")
        lod2 = geo.decimate(ob, 0.08, f"{NAME}_{i}_LOD2")
        objects += [ob, lod1, lod2]
        tris.append((geo.triangles(ob), geo.triangles(lod1), geo.triangles(lod2)))
    export.fbx(objects, NAME)
    print(f"{NAME}: " + "; ".join(f"{i}: {t[0]}/{t[1]}/{t[2]}" for i, t in enumerate(tris)))
    if preview:
        mat = bake.textured_material(NAME + "_Preview", paths)
        for i, ob in enumerate(parts):
            ob.location = geo.to_blender((i * 1.3 - 2.6, 0, 0))
        export.preview(parts[2], NAME, mat, radius=1.6, extra=[p for p in parts if p is not parts[2]],
                       notes=f"{NAME}  {SHAPES} shapes, LOD0 ~{tris[0][0]} tri  tex {TEX}")
    return tris[0]


if __name__ == "__main__":
    main()
