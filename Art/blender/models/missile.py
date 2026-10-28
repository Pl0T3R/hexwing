"""
Missile: the homing missile (player, elites and the pursuit ability). A white body with a dark seeker
nose, an orange band, four tail fins and a hot exhaust. About 2.6 m long. Original design.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Missile"
TEX = 256

PALETTE = {
    "HULL": dict(base=(0.62, 0.63, 0.64), metal=0.2, rough=0.45, panel_scale=4.0, wear=0.3, wear_color=(0.8, 0.8, 0.8)),
    "BAND": dict(base=(0.6, 0.22, 0.03), metal=0.2, rough=0.5, panel=0.0, wear=0.3),
    "DARK": dict(base=(0.03, 0.03, 0.035), metal=0.6, rough=0.3, panel=0.0, wear=0.1),
    "ENGINE": dict(base=(0.1, 0.05, 0.01), emit=1.0, emit_color=(1.0, 0.6, 0.25), panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    circ = geo.superellipse(0.42, 0.42, count=12, power=2.0)
    P(geo.bevel(geo.loft("Body", [(-1.15, circ, (0, 0), 0.9), (-1.0, circ), (0.85, circ)], "HULL"), width=0.01, segments=1))
    P(geo.bevel(geo.loft("Seeker", [(0.85, circ), (1.3, circ, (0, 0), 0.45), (1.45, circ, (0, 0), 0.1)], "DARK"), width=0.008, segments=1))
    P(geo.bevel(geo.loft("Band", [(0.3, circ, (0, 0), 1.03), (0.45, circ, (0, 0), 1.03)], "BAND"), width=0.004, segments=1))
    for i in range(4):
        spin = geo.rot(z=45 + i * 90)
        P(geo.bevel(geo.slab("Fin", [(0.18, -0.6), (0.18, -1.1), (0.45, -1.15), (0.45, -0.95)], 0.04, "HULL", matrix=spin), width=0.005, segments=1))
    P(geo.disc("Exhaust", (0, 0, -1.16), (0, 0, -1), 0.15, "ENGINE", sides=10))
    hull = geo.join(parts, NAME + "_LOD0")
    return hull, [geo.socket("Nose", (0, 0, 1.45))]


def main(preview=True):
    hull, sockets = build()
    return pipeline.finish(NAME, hull, sockets, TEX, radius=1.6, lods=(0.4,), preview=preview)


if __name__ == "__main__":
    main()
