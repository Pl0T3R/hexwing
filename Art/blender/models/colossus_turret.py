"""
ColossusTurret: a gun turret of the capital ship (six on its flanks, destructible, placed by code). A low
armoured dome on a ring with two long barrels, a sensor slit and red trim. About 16 m across; the
barrels point along +z (the code turns the turret toward its side). Original design.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "ColossusTurret"
TEX = 512

PALETTE = {
    "HULL": dict(base=(0.09, 0.095, 0.105), metal=0.55, rough=0.45, panel_scale=0.6, mortar=0.012, wear=0.35, grime=0.55, ao_dist=3.0, bevel=0.08),
    "TRIM": dict(base=(0.3, 0.035, 0.03), metal=0.4, rough=0.42, panel=0.0, wear=0.4, ao_dist=2.0, bevel=0.05),
    "METAL": dict(base=(0.22, 0.22, 0.23), metal=0.9, rough=0.35, panel=0.2, wear=0.3, ao_dist=2.0, bevel=0.05),
    "DARK": dict(base=(0.02, 0.02, 0.024), metal=0.6, rough=0.6, panel=0.0, wear=0.1, ao_dist=2.0, bevel=0.03),
    "LIGHT": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.12, 0.06), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    P(geo.bevel(geo.cylinder("Ring", (0, -3.0, 0), (0, -1.2, 0), 8.0, 7.6, "METAL", sides=24), width=0.15, segments=1))
    P(geo.bevel(geo.cylinder("RingTrim", (0, -1.3, 0), (0, -0.9, 0), 7.7, 7.5, "TRIM", sides=24), width=0.06, segments=1))
    dome = [(-6.5, geo.superellipse(12, 6, count=16, power=2.4)), (-2.0, geo.superellipse(13, 7.5, count=16, power=2.4)),
            (3.0, geo.superellipse(12, 6.5, count=16, power=2.4)), (5.5, geo.superellipse(8, 3.5, count=16, power=2.4))]
    P(geo.bevel(geo.loft("Dome", dome, "HULL", matrix=geo.at((0, 1.2, 0))), width=0.2))
    P(geo.box("Slit", (0, 3.4, 4.6), (5.0, 0.4, 0.4), "LIGHT", geo.rot(x=-35)))
    for x in (-1.6, 1.6):
        P(geo.bevel(geo.cylinder("Barrel", (x, 1.4, 3.0), (x, 1.4, 15.0), 0.75, 0.6, "METAL", sides=12), width=0.05, segments=1))
        P(geo.tube("Muzzle", (x, 1.4, 14.8), (x, 1.4, 16.0), 0.75, 0.45, "DARK", sides=12))
        P(geo.bevel(geo.box("Shroud", (x, 1.4, 5.0), (1.9, 1.9, 4.0), "HULL"), width=0.1, segments=1))
    hull = geo.join(parts, NAME + "_LOD0")
    return hull, [geo.socket("Muzzle", (0, 1.4, 16.0))]


def main(preview=True):
    hull, sockets = build()
    return pipeline.finish(NAME, hull, sockets, TEX, radius=12.0, preview=preview, ao_distance=4.0)


if __name__ == "__main__":
    main()
