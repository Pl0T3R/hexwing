"""
Wrecks: four torn sections of a destroyed cruiser for the wreck fields of the Orbit map (60 to 160 m).
Each is a piece of hexagonal hull with ragged, scorched ends, its dark interior open to see, bulkheads,
ribs and beams sticking out of the tear, and a few dead window rows. One shared atlas.
Meshes: Wrecks_<i>_LOD0/1/2 (i = 0..3), each centred on its own origin. Scenery with collision.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

from lib import bake, export, geo, surface  # noqa: E402

NAME = "Wrecks"
TEX = 2048
PIECES = ((160.0, 70.0, 60.0), (110.0, 60.0, 52.0), (80.0, 48.0, 40.0), (60.0, 40.0, 34.0))   # length, width, height

PALETTE = {
    "HULL": dict(base=(0.11, 0.115, 0.125), metal=0.55, rough=0.5, panel_scale=0.12, mortar=0.022, wear=0.45, grime=0.7,
                 ao_dist=8.0, bevel=0.3, wear_color=(0.3, 0.28, 0.26)),
    "BURNT": dict(base=(0.02, 0.018, 0.017), metal=0.3, rough=0.8, panel=0.3, panel_scale=0.15, mortar=0.03, wear=0.2,
                  grime=0.9, ao_dist=6.0, bevel=0.2, rock=0.4, rock_scale=0.15, mottle=0.5, mottle_color=(0.12, 0.05, 0.02)),
    "TRIM": dict(base=(0.22, 0.04, 0.03), metal=0.4, rough=0.55, panel_scale=0.12, mortar=0.022, wear=0.6, grime=0.7, ao_dist=6.0, bevel=0.2),
    "METAL": dict(base=(0.2, 0.19, 0.19), metal=0.85, rough=0.45, panel=0.2, panel_scale=0.3, wear=0.4, grime=0.6, ao_dist=5.0, bevel=0.15),
    "EMBER": dict(base=(0.2, 0.05, 0.01), emit=0.6, emit_color=(1.0, 0.35, 0.08), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}


def hexa(w, h):
    return [(w / 2, 0.0), (w / 4, h / 2), (-w / 4, h / 2), (-w / 2, 0.0), (-w / 4, -h / 2), (w / 4, -h / 2)]


def ragged(prof, steps):
    """A profile with more points (so the tear can be jagged): each hexagon edge split into steps."""
    out = []
    for i in range(len(prof)):
        a, b = Vector(prof[i]), Vector(prof[(i + 1) % len(prof)])
        for k in range(steps):
            p = a.lerp(b, k / steps)
            out.append((p.x, p.y))
    return out


def piece(index, length, width, height, rng):
    parts = []
    P = parts.append
    prof = ragged(hexa(width, height), 4)
    n = len(prof)
    half = length / 2
    # the outer shell: two torn ends (each ring point pushed along z and inward at random)
    def torn_ring(z, sign, depth):
        ring = []
        for x, y in prof:
            dz = rng.uniform(0.0, depth) * sign
            k = rng.uniform(0.82, 1.0)
            ring.append((x * k, y * k, z + dz))
        return ring
    rings = [torn_ring(-half, 1, length * 0.18), [(x, y, -half * 0.4) for x, y in prof], [(x, y, half * 0.4) for x, y in prof],
             torn_ring(half, -1, length * 0.18)]
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    for a, b in zip(vr, vr[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], b[i], b[j], a[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # make the outside face outward: the shell is open, so check one face against the centre
    f = bm.faces[0]
    f.normal_update()
    if f.normal.dot(f.calc_center_median()) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    P(geo.from_bmesh("Shell", bm, "HULL"))
    # the inside: the same surface a little smaller, facing in, scorched
    bm = bmesh.new()
    vr = [[bm.verts.new((x * 0.97, y * 0.97, z)) for x, y, z in r] for r in rings]
    for a, b in zip(vr, vr[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], b[i], b[j], a[j]))
    bm.normal_update()
    f = bm.faces[0]
    if f.normal.dot(f.calc_center_median()) > 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    P(geo.from_bmesh("Inside", bm, "BURNT"))
    # bulkheads and decks inside
    for z in (-half * 0.3, half * 0.25):
        P(geo.bevel(geo.loft("Bulkhead", [(z, hexa(width * 0.95, height * 0.95)), (z + 1.5, hexa(width * 0.95, height * 0.95))], "BURNT"), width=0.2, segments=1))
    P(geo.box("Deck", (0, -height * 0.12, 0), (width * 0.7, 1.0, length * 0.9), "METAL"))
    # ribs and beams sticking out of the tears, a few bent
    for sign in (1, -1):
        for k in range(5):
            x, y = rng.uniform(-width * 0.35, width * 0.35), rng.uniform(-height * 0.35, height * 0.35)
            z0 = sign * half * rng.uniform(0.2, 0.5)
            z1 = sign * (half + rng.uniform(4, length * 0.15))
            bend = (rng.uniform(-6, 6), rng.uniform(-6, 6))
            path = [(x, y, z0), (x + bend[0] * 0.3, y + bend[1] * 0.3, (z0 + z1) / 2), (x + bend[0], y + bend[1], z1)]
            P(geo.sweep("Beam", path, geo.rect(rng.uniform(1.2, 2.4), rng.uniform(1.2, 2.4)), "METAL"))
            if rng.random() < 0.5:
                P(geo.disc("Ember", (x + bend[0], y + bend[1], z1), (0, 0, sign), rng.uniform(0.6, 1.4), "EMBER", sides=8))
    # armour belt and dead window rows on the outside
    for side in (1, -1):
        P(geo.bevel(geo.box("Belt", (side * (width / 2 + 0.6), -1.5, 0), (1.6, 6, length * 0.6), "TRIM"), width=0.3, segments=1, angle=30))
        for row, y in enumerate((6.0, -9.0)):
            P(geo.box("DeadWindows", (side * (width / 2 - width / 4 * abs(y) / (height / 2) + 0.2), y, 0), (0.4, 1.0, length * 0.5), "BURNT"))
    return geo.join(parts, f"{NAME}_{index}_LOD0")


def main(preview=True):
    geo.reset()
    surface.make_all(PALETTE)
    rng = random.Random(23)
    obs = []
    offsets = []
    x = 0.0
    for i, (length, width, height) in enumerate(PIECES):
        ob = piece(i, length, width, height, rng)
        geo.tag_part(ob, i)
        off = (x, 0.0, 0.0)
        x += width + 40.0
        ob.location = geo.to_blender(off)
        geo.apply_transform(ob)
        obs.append(ob)
        offsets.append(off)
    joined = geo.join(obs, NAME + "_Bake")
    bake.unwrap(joined, margin=0.003)
    paths = bake.bake(joined, NAME, TEX, export.TEXTURES, ao_distance=20.0)
    parts = geo.split_parts(joined, [f"{NAME}_{i}_LOD0" for i in range(len(PIECES))])
    objects, tris = [], []
    for ob, off in zip(parts, offsets):
        geo.recenter(ob, geo.to_blender(off))
        lod1 = geo.decimate(ob, 0.35, ob.name.replace("LOD0", "LOD1"))
        lod2 = geo.decimate(ob, 0.12, ob.name.replace("LOD0", "LOD2"))
        objects += [ob, lod1, lod2]
        tris.append((geo.triangles(ob), geo.triangles(lod1), geo.triangles(lod2)))
    export.fbx(objects, NAME)
    print(f"{NAME}: " + "; ".join(f"{i}: {t[0]}/{t[1]}/{t[2]}" for i, t in enumerate(tris)))
    if preview:
        mat = bake.textured_material(NAME + "_Preview", paths)
        x = 0.0
        for ob, (length, width, height) in zip(parts, PIECES):
            ob.location = geo.to_blender((x, 0, 0))
            x += width + 30
        export.preview(parts[1], NAME, mat, radius=200.0, extra=[p for p in parts if p is not parts[1]],
                       notes=f"{NAME}  {len(PIECES)} pieces, LOD0 {tris[0][0]} tri  tex {TEX}")
    return tris[0]


if __name__ == "__main__":
    main()
