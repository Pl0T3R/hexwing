"""
"Kolos": the enemy capital ship, 560 m long. The layout matches Combat/CapitalShip.cs, whose
destructible parts (two shield-generator domes, six turrets, the reactor core) are still placed by code
on top of this hull:
- a flat-topped hexagonal hull from the stern block (z -290) to a tapered bow (z +282),
- pedestals under the generator domes at (0, 68, 90) and (0, 68, -60),
- sponsons under the turrets on the flanks (x +-48..54, y 30..34, z 140 / 0 / -120),
- the reactor trench between two walls at x +-20 (z -245..-185), the bridge tower at (0, 72, -150),
- hangar bays, rows of lit windows, running lights, deck structures, and six red engines at the stern.
Dark steel with red trim. Original design. Unity frame: z forward, y up, x right.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401

from lib import geo, pipeline, surface  # noqa: E402

NAME = "Kolos"
TEX = 2048

PALETTE = {
    "HULL": dict(base=(0.085, 0.09, 0.1), metal=0.55, rough=0.45, panel_scale=0.075, mortar=0.022, panel_dark=0.55,
                 tone=0.1, wear=0.3, grime=0.55, ao_dist=12.0, bevel=0.4, bump=0.6),
    "DECK": dict(base=(0.06, 0.063, 0.07), metal=0.5, rough=0.5, panel_scale=0.12, mortar=0.025, tone=0.12, wear=0.25,
                 grime=0.6, ao_dist=8.0, bevel=0.25),
    "TRIM": dict(base=(0.3, 0.035, 0.03), metal=0.4, rough=0.42, panel_scale=0.1, mortar=0.02, wear=0.4, ao_dist=8.0, bevel=0.3),
    "METAL": dict(base=(0.2, 0.2, 0.21), metal=0.9, rough=0.35, panel=0.3, panel_scale=0.2, mortar=0.03, wear=0.3, ao_dist=6.0, bevel=0.2),
    "DARK": dict(base=(0.018, 0.019, 0.022), metal=0.6, rough=0.6, panel=0.0, wear=0.1, ao_dist=6.0, bevel=0.2),
    "WINDOW": dict(base=(0.2, 0.15, 0.08), emit=1.0, emit_color=(1.0, 0.72, 0.42), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "RED": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.1, 0.05), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "ENGINE": dict(base=(0.2, 0.03, 0.01), emit=1.0, emit_color=(1.0, 0.22, 0.08), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}

GENERATORS = (90.0, -60.0)
TURRETS = ((48, 34, 140), (-48, 34, 140), (54, 30, 0), (-54, 30, 0), (48, 34, -120), (-48, 34, -120))


SECTIONS = ((-262.0, 120, 92, 0), (-240.0, 132, 100, 0), (0.0, 114, 100, 0), (160.0, 100, 88, -2),
            (232.0, 64, 54, -6), (282.0, 14, 12, -10))


def hull_x(z, y):
    """The hull's side surface x (right side) at length z and height y: the hexagon's edge there."""
    for (z0, w0, h0, d0), (z1, w1, h1, d1) in zip(SECTIONS, SECTIONS[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0)
            w, h, dy = w0 + (w1 - w0) * t, h0 + (h1 - h0) * t, d0 + (d1 - d0) * t
            return w / 2 - (w / 4) * min(1.0, abs(y - dy) / (h / 2))
    return 0.0


def hexa(w, h):
    """A flat-topped hexagon: pointed at the sides, flat deck and keel."""
    return [(w / 2, 0.0), (w / 4, h / 2), (-w / 4, h / 2), (-w / 2, 0.0), (-w / 4, -h / 2), (w / 4, -h / 2)]


def build():
    geo.reset()
    surface.make_all(PALETTE)
    rng = random.Random(7)
    parts = []
    P = parts.append

    P(geo.bevel(geo.loft("Hull", [(z, hexa(w, h), (0, dy)) for z, w, h, dy in SECTIONS], "HULL"), width=1.2, segments=2, angle=25))
    # armour belt along the flanks (red trim), following the hull's side edge, and a keel ridge
    for side in (1, -1):
        path = [(side * (hull_x(z, -1) + 0.6), -1.0, z) for z in range(-255, 215, 10)]
        P(geo.bevel(geo.sweep("Belt", path, geo.rect(1.6, 7.0), "TRIM"), width=0.3, segments=1))
    P(geo.bevel(geo.box("Keel", (0, -51, -40), (24, 6, 380), "METAL"), width=0.8, segments=1))
    # stern block with six engines
    P(geo.bevel(geo.box("Stern", (0, 0, -276), (130, 90, 30), "HULL"), width=1.0, segments=2))
    for x, y in ((-40, 22), (0, 22), (40, 22), (-40, -18), (0, -18), (40, -18)):
        P(geo.bevel(geo.cylinder("EngineHousing", (x, y, -288), (x, y, -296), 17, 16, "METAL", sides=20), width=0.4, segments=1))
        P(geo.tube("Nozzle", (x, y, -295), (x, y, -305), 16, 12.5, "DARK", sides=20, flare=1.08))
        P(geo.disc("EngineCore", (x, y, -297), (0, 0, -1), 12.6, "ENGINE", sides=20))

    # deck: raised plates along the spine, with gaps for the generator pedestals
    for z0, z1 in ((150, 230), (-30, 60), (-120, -80)):
        P(geo.bevel(geo.box("DeckPlate", (0, 51, (z0 + z1) / 2), (44, 4, z1 - z0), "DECK"), width=0.6, segments=1))
    for z in GENERATORS:
        P(geo.bevel(geo.cylinder("Pedestal", (0, 48, z), (0, 58, z), 24, 20, "METAL", sides=24), width=0.5, segments=1))
        P(geo.bevel(geo.cylinder("PedestalRing", (0, 57, z), (0, 60, z), 19, 18, "TRIM", sides=24), width=0.3, segments=1))
        for k in range(8):
            a = math.radians(k * 45 + 22.5)
            P(geo.disc("PedestalLight", (math.cos(a) * 21.5, 55.5, z + math.sin(a) * 21.5), (math.cos(a), 0.3, math.sin(a)), 0.9, "RED", sides=8))

    # bridge tower and the reactor trench
    P(geo.bevel(geo.box("Tower", (0, 72, -150), (40, 40, 60), "HULL"), width=0.8, segments=2))
    P(geo.bevel(geo.box("Bridge", (0, 95, -160), (60, 12, 30), "HULL"), width=0.6, segments=2))
    P(geo.bevel(geo.box("BridgeTop", (0, 103, -163), (30, 5, 16), "METAL"), width=0.4, segments=1))
    for side in (1, -1):
        P(geo.box("BridgeWindows", (30.2 * side, 96, -160), (0.6, 2.2, 24), "WINDOW"))
        P(geo.box("BridgeWindowsFront", (0, 96, -144.8), (50, 2.2, 0.6), "WINDOW"))
    for k in range(4):
        P(geo.bevel(geo.cylinder("Antenna", (-12 + k * 8, 105, -165), (-12 + k * 8, 118 + 4 * (k % 2), -165), 0.6, 0.3, "METAL", sides=6), width=0.05, segments=1))
    for side in (1, -1):
        P(geo.bevel(geo.box("TrenchWall", (20 * side, 62, -215), (6, 22, 60), "HULL"), width=0.6, segments=1))
        for k in range(6):
            P(geo.box("TrenchLight", (17 * side, 58 + (k % 2) * 6, -240 + k * 10), (0.5, 1.2, 1.2), "RED"))
    P(geo.box("TrenchFloor", (0, 51.5, -215), (34, 3, 60), "DARK"))

    # turret sponsons on the flanks
    for x, y, z in TURRETS:
        side = 1 if x > 0 else -1
        P(geo.bevel(geo.box("Sponson", (x - 4 * side, y - 4, z), (18, 6, 26), "METAL"), width=0.5, segments=1))
        P(geo.bevel(geo.box("SponsonArm", (x - 10 * side, y - 8, z), (14, 8, 14), "HULL"), width=0.5, segments=1))

    # hangar bays with lit interiors, rows of windows, running lights
    for side in (1, -1):
        for z in (-200, 80):
            x = hull_x(z, -14)
            P(geo.bevel(geo.box("HangarFrame", ((x + 0.5) * side, -14, z), (3, 26, 54), "TRIM"), width=0.3, segments=1))
            P(geo.box("HangarMouth", ((x + 1.2) * side, -14, z), (2, 21, 46), "DARK"))
            P(geo.box("HangarGlow", ((x + 2.3) * side, -14, z), (0.3, 16, 40), "WINDOW"))
        for row, y in enumerate((14, 6, -24)):
            z = -240
            while z < 170:
                length = rng.choice((6, 10, 14))
                if not any(abs(z - hz) < 30 for hz in (-200, 80)) or y > 0:
                    if rng.random() < 0.7:
                        x = min(hull_x(z, y), hull_x(z + length, y), hull_x(z - length, y))
                        P(geo.box("Windows", ((x + 0.15) * side, y, z), (0.6, 1.0, length), "WINDOW"))
                z += length + rng.choice((4, 6, 10))
        for z in range(-250, 200, 45):
            P(geo.disc("RunLight", ((hull_x(z, 3.5) + 0.4) * side, 3.5, z), (side, 0, 0), 0.8, "RED", sides=8))
    # deck greebles: vents, boxes, pipes
    for i in range(70):
        z = rng.uniform(-240, 200)
        if any(abs(z - g) < 30 for g in GENERATORS) or -185 > z > -245 or -180 < z < -120:
            continue
        x = rng.uniform(-24, 24)
        w, d, h = rng.uniform(3, 9), rng.uniform(4, 16), rng.uniform(1.5, 5)
        P(geo.bevel(geo.box("Greeble", (x, 51 + h / 2, z), (w, h, d), rng.choice(("METAL", "DECK", "DARK"))), width=0.2, segments=1))
    for side in (1, -1):
        for k in range(3):
            path = [(side * (14 + k * 3), 51.5, z) for z in range(-110, 140, 10)]
            P(geo.sweep("Pipe", path, geo.circle(1.0, 8), "METAL"))
    # bow details: a red nose band and sensor dishes
    P(geo.bevel(geo.loft("BowBand", [(220.0, hexa(70, 60), (0, -5)), (226.0, hexa(66, 57), (0, -5.5))], "TRIM"), width=0.4, segments=1))
    for side in (1, -1):
        P(geo.bevel(geo.cylinder("Dish", (12 * side, 44, 200), (12 * side, 47, 200), 6, 4, "METAL", sides=16), width=0.2, segments=1))

    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (x, y, -305), (x, y, -360), 12, 2, "GLOW", sides=16, cap0=False, cap1=False)
              for x, y in ((-40, 22), (0, 22), (40, 22), (-40, -18), (0, -18), (40, -18))]
    glow = geo.join(plumes, NAME + "_Glow")
    sockets = [geo.socket("Bridge", (0, 95, -160)), geo.socket("Reactor", (0, 64, -215)), geo.socket("Nose", (0, -10, 282))]
    for i, z in enumerate(GENERATORS):
        sockets.append(geo.socket(f"Generator{i}", (0, 68, z)))
    for i, (x, y, z) in enumerate(TURRETS):
        sockets.append(geo.socket(f"Turret{i}", (x, y, z)))
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=300.0, lods=(0.35, 0.1), preview=preview, ao_distance=25.0)


if __name__ == "__main__":
    main()
