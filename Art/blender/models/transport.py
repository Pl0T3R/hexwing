"""
Transport: the convoy hauler of the Escort mode, about 70 m long. A sharp arrowhead freighter: a flat
hexagonal hull tapering to a pointed prow, hard chines along both sides, an angular bridge tower at the
stern with a lit window slit, an armoured cargo pod under the hull with ribbed bays, armour plates and
vents on the deck, a sensor mast, running lights and three engines at the stern. Light grey with dark
panels and orange convoy markings. Fits the BoxCollider in Combat/Transport.cs (16 x 16 x 70 m).
Original design. Unity frame: z forward, y up, x right.
"""
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Transport"
TEX = 1024

PALETTE = {
    "HULL": dict(base=(0.42, 0.44, 0.47), metal=0.4, rough=0.45, panel_scale=0.4, mortar=0.01, wear=0.35, grime=0.45, ao_dist=3.0, bevel=0.06),
    "PANEL": dict(base=(0.15, 0.16, 0.18), metal=0.5, rough=0.45, panel_scale=0.6, mortar=0.012, wear=0.35, grime=0.5, ao_dist=2.0, bevel=0.05),
    "MARK": dict(base=(0.6, 0.24, 0.03), metal=0.2, rough=0.5, panel=0.0, wear=0.4, grime=0.3, ao_dist=2.0, bevel=0.03),
    "METAL": dict(base=(0.24, 0.24, 0.25), metal=0.9, rough=0.32, panel=0.3, panel_scale=0.6, wear=0.3, ao_dist=2.0, bevel=0.04),
    "DARK": dict(base=(0.025, 0.027, 0.03), metal=0.6, rough=0.55, panel=0.0, wear=0.1, ao_dist=2.0, bevel=0.03),
    "WINDOW": dict(base=(0.05, 0.1, 0.12), emit=1.0, emit_color=(0.55, 0.9, 1.0), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "ENGINE": dict(base=(0.02, 0.06, 0.08), emit=1.0, emit_color=(0.3, 0.85, 1.0), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "RED": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.15, 0.08), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}

# hull sections: (z, width, height, centre y); flat hexagons, pointed at the sides
SECTIONS = ((-34.0, 11.0, 7.5, 0.4), (-31.0, 13.6, 9.6, 0.3), (-8.0, 14.0, 9.9, 0.2), (14.0, 10.0, 7.5, 0.0),
            (28.0, 4.0, 3.6, -0.2), (34.5, 0.6, 0.75, -0.3))


def hexa(w, h):
    return geo.polygon(w, h, 6, 0.0)


def sharp(ob, width=0.12):
    return geo.bevel(ob, width=width, segments=1, angle=25)


def build():
    geo.reset()
    surface.make_all(PALETTE)
    rng = random.Random(5)
    parts = []
    P = parts.append

    P(sharp(geo.loft("Hull", [(z, hexa(w, h), (0, y)) for z, w, h, y in SECTIONS], "HULL"), 0.2))
    # hard chines: a dark rail along each side's pointed edge
    for x in (-1, 1):
        chine = [(x * (w / 2 + 0.1), y, z) for z, w, h, y in SECTIONS[1:-1]]
        P(geo.sweep("Chine", chine, geo.rect(0.5, 0.6), "PANEL"))
        P(geo.sweep("ChineMark", [(x * (w / 2 + 0.25), y + 0.1, z) for z, w, h, y in SECTIONS[1:4]], geo.rect(0.1, 0.25), "MARK"))
    # deck: dark panel strip, armour plates, vents, the orange convoy chevrons near the prow
    P(sharp(geo.box("DeckStrip", (0, 4.5, -8.0), (4.0, 0.3, 40.0), "PANEL")))
    for k in range(6):
        z = -26.0 + k * 7.0
        P(sharp(geo.box("DeckPlate", (rng.choice((-1, 1)) * 3.6, 4.4, z), (2.6, 0.4, 5.2), "PANEL"), 0.08))
        P(geo.box("Vent", (0, 4.7, z + 2.0), (2.2, 0.1, 1.0), "DARK"))
    for k in range(3):
        z = 16.0 + k * 3.0
        for x in (-1, 1):
            P(geo.box("Chevron", (x * 1.3, 3.2 - k * 0.35, z), (2.4, 0.1, 0.6), "MARK", geo.rot(y=35 * x)))
    # bridge tower at the stern: angular, a lit window slit, an antenna mast
    tower = [(-30.0, hexa(7.0, 3.0)), (-22.0, hexa(7.5, 3.2)), (-18.0, hexa(4.5, 2.2))]
    P(sharp(geo.loft("Tower", tower, "HULL", matrix=geo.at((0, 5.8, 0)))))
    P(geo.box("BridgeWindows", (0, 6.3, -18.6), (4.0, 0.5, 0.3), "WINDOW", geo.rot(x=-35)))
    for x in (-1, 1):
        P(geo.box("BridgeSide", (x * 3.62, 6.1, -24.0), (0.1, 0.4, 4.5), "WINDOW"))
    P(geo.cylinder("Mast", (0, 7.0, -27.0), (0, 8.4, -27.5), 0.2, 0.1, "METAL", sides=6))
    P(geo.disc("MastLight", (0, 8.45, -27.5), (0, 1, 0), 0.3, "RED", sides=8))
    # armoured cargo pod under the hull with ribbed bays
    boxy = lambda w, h: geo.superellipse(w, h, count=16, power=6.0)
    pod = [(-24.0, boxy(8.0, 3.0)), (-21.0, boxy(10.0, 3.8)), (9.0, boxy(9.6, 3.6)), (13.0, boxy(5.0, 2.0))]
    P(sharp(geo.loft("CargoPod", pod, "PANEL", matrix=geo.at((0, -5.4, 0)))))
    for k in range(7):
        z = -17.0 + k * 4.2
        P(sharp(geo.loft("PodRib", [(z - 0.3, boxy(10.4, 4.2)), (z + 0.3, boxy(10.4, 4.2))], "HULL", matrix=geo.at((0, -5.4, 0))), 0.05))
    # stern: three engines in a row, red running lights on the chines
    for x in (-4.0, 0.0, 4.0):
        P(sharp(geo.box("EngineHousing", (x, 0.3, -34.6), (3.6, 4.4, 1.6), "METAL"), 0.08))
        P(geo.box("EngineGlow", (x, 0.3, -35.45), (2.8, 3.6, 0.1), "ENGINE"))
    for x in (-1, 1):
        P(geo.disc("RunLight", (x * 7.0, 0.3, -30.0), (x, 0, 0), 0.4, "RED", sides=8))
        P(geo.disc("NavLight", (x * 2.1, 0.0, 28.0), (x, 0, 0.3), 0.3, "WINDOW", sides=8))

    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (x, 0.3, -35.5), (x, 0.3, -42.0), 1.3, 0.2, "GLOW", sides=10, cap0=False, cap1=False) for x in (-4.0, 0.0, 4.0)]
    glow = geo.join(plumes, NAME + "_Glow")
    sockets = [geo.socket("Nose", (0, -0.3, 34.5)), geo.socket("Engine", (0, 0.3, -35.5))]
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=38.0, preview=preview, ao_distance=4.0)


if __name__ == "__main__":
    main()
