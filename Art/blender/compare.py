"""
Silhouette check against a blueprint: builds a model's geometry (no bake), projects its triangles
orthographically in one view and lays the silhouette (red, half transparent) over the blueprint crop,
scaled so the longest axis of both match. Differences in proportion and outline show at a glance.

Usage: python Art/blender/compare.py <module> <variant|-> <blueprint.png> <x0,y0,x1,y1> <view> <out.png> [flip]
  view: top (z right, x down), side (z right, y up), front (x right, y up)
  flip: h to mirror the silhouette left-right (e.g. a side view whose nose points left)
"""
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "models"))
import bpy  # noqa: E402,F401

from PIL import Image, ImageDraw  # noqa: E402


def unity(v):
    """Blender world -> Unity frame (inverse of geo.TO_BLENDER)."""
    return (-v.x, v.z, -v.y)


def project(p, view):
    x, y, z = p
    if view == "top":
        return z, x        # nose right, starboard down
    if view == "side":
        return z, -y       # nose right, up is up (image y grows down)
    return x, -y           # front: starboard right


def main(module, variant, blueprint, box, view, out, flip=""):
    mod = importlib.import_module(module)
    built = mod.build(variant) if variant != "-" else mod.build()
    hull = built[0] if isinstance(built, tuple) else built
    hull.data.calc_loop_triangles()
    mw = hull.matrix_world
    verts = [project(unity(mw @ v.co), view) for v in hull.data.vertices]
    xs, ys = [p[0] for p in verts], [p[1] for p in verts]
    x0, y0, x1, y1 = min(xs), min(ys), max(xs), max(ys)

    bp = Image.open(blueprint).convert("RGB")
    bx0, by0, bx1, by1 = box
    crop = bp.crop((bx0, by0, bx1, by1))
    bw, bh = crop.size
    scale = bw / (x1 - x0)   # the horizontal extents match; any height difference is a real mismatch
    sil = Image.new("L", crop.size, 0)
    d = ImageDraw.Draw(sil)
    for tri in hull.data.loop_triangles:
        pts = []
        for vi in tri.vertices:
            px, py = verts[vi]
            sx = (px - x0) * scale
            if flip == "h":
                sx = (x1 - x0) * scale - sx
            pts.append((sx, (py - y0) * scale))
        d.polygon(pts, fill=255)
    red = Image.new("RGB", crop.size, (230, 30, 30))
    overlay = Image.composite(Image.blend(crop, red, 0.55), crop, sil)
    big = overlay.resize((overlay.width * 2, overlay.height * 2)) if overlay.width < 500 else overlay
    big.save(out)
    print(f"model extent {x1 - x0:.2f} x {y1 - y0:.2f} m, blueprint {bw} x {bh} px, scale {scale:.1f} px/m -> {out}")


if __name__ == "__main__":
    a = sys.argv[1:]
    main(a[0], a[1], a[2], tuple(int(v) for v in a[3].split(",")), a[4], a[5], a[6] if len(a) > 6 else "")
