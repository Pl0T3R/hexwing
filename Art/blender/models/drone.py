"""
Drone: the target drone of the drone range. A light-grey hexagonal core pointed at both ends with three
swept fins, a red glowing sensor band around the middle and red fin tips. About 5 m long. Original
design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Drone"
TEX = 512

PALETTE = {
    "HULL": dict(base=(0.5, 0.52, 0.55), metal=0.35, rough=0.4, panel_scale=1.8, wear=0.4, wear_color=(0.7, 0.72, 0.75)),
    "DARK": dict(base=(0.03, 0.033, 0.038), metal=0.6, rough=0.5, panel=0.0, wear=0.15),
    "METAL": dict(base=(0.25, 0.25, 0.27), metal=0.9, rough=0.3, panel=0.3, wear=0.25),
    "LIGHT": dict(base=(0.1, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.25, 0.12), panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    hexa = geo.polygon(3.2, 3.2, 6, rotate_deg=30)
    P(geo.bevel(geo.loft("Core", [(-2.2, hexa, (0, 0), 0.25), (-1.2, hexa), (-0.25, hexa), (1.2, hexa), (2.6, hexa, (0, 0), 0.08)], "HULL"), width=0.03))
    P(geo.bevel(geo.loft("Band", [(-0.25, hexa, (0, 0), 1.04), (0.25, hexa, (0, 0), 1.04)], "LIGHT"), width=0.01, segments=1))
    P(geo.bevel(geo.loft("Eye", [(2.0, geo.polygon(0.7, 0.7, 6, 30)), (2.35, geo.polygon(0.4, 0.4, 6, 30))], "LIGHT"), width=0.01, segments=1))
    for i in range(3):
        spin = geo.rot(z=90 + i * 120)
        fin = [(1.4, 0.2), (1.4, -1.6), (3.6, -2.2), (3.8, -1.6)]
        P(geo.bevel(geo.slab("Fin", fin, 0.18, "HULL", taper=0.5, matrix=spin), width=0.015))
        P(geo.bevel(geo.slab("FinTip", [(3.4, -1.55), (3.4, -2.1), (3.8, -2.2), (3.8, -1.6)], 0.2, "LIGHT", matrix=spin), width=0.008, segments=1))
        P(geo.bevel(geo.box("Thruster", geo.xf(spin, (1.2, 0, -1.9)), (0.4, 0.4, 0.6), "METAL", spin), width=0.02, segments=1))
    hull = geo.join(parts, NAME + "_LOD0")
    return hull, [geo.socket("Nose", (0, 0, 2.6))]


def main(preview=True):
    hull, sockets = build()
    return pipeline.finish(NAME, hull, sockets, TEX, radius=3.5, preview=preview)


if __name__ == "__main__":
    main()
