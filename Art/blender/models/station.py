"""
Station "Dawn": a large orbital station at the edge of the Orbit map, about 700 m across. A central
hub tower with a command crown, a wide habitat ring on four spokes, two long docking arms with berths,
two wings of solar panels, radiators, antenna masts and a lot of lit windows and beacons. Scenery
(with a collision mesh), not a target. Original design. Unity frame: z forward, y up, x right.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401

from lib import geo, pipeline, surface  # noqa: E402

NAME = "Station"
TEX = 2048

PALETTE = {
    "HULL": dict(base=(0.36, 0.37, 0.39), metal=0.45, rough=0.5, panel_scale=0.12, mortar=0.02, wear=0.3, grime=0.55, ao_dist=10.0, bevel=0.3),
    "DARK": dict(base=(0.05, 0.052, 0.06), metal=0.6, rough=0.5, panel_scale=0.15, mortar=0.02, wear=0.2, ao_dist=8.0, bevel=0.2),
    "TRIM": dict(base=(0.05, 0.12, 0.26), metal=0.4, rough=0.45, panel_scale=0.2, mortar=0.02, wear=0.4, ao_dist=8.0, bevel=0.2),
    "SOLAR": dict(base=(0.01, 0.025, 0.06), metal=0.7, rough=0.18, panel=1.0, panel_scale=0.5, mortar=0.04, panel_dark=0.3,
                  tone=0.15, wear=0.05, grime=0.2, ao_dist=4.0, bevel=0.0, bump=0.2),
    "METAL": dict(base=(0.25, 0.25, 0.26), metal=0.9, rough=0.35, panel=0.2, panel_scale=0.3, wear=0.3, ao_dist=6.0, bevel=0.15),
    "WINDOW": dict(base=(0.2, 0.18, 0.12), emit=1.0, emit_color=(1.0, 0.86, 0.62), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "BEACON": dict(base=(0.05, 0.1, 0.15), emit=1.0, emit_color=(0.4, 0.85, 1.0), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "RED": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.12, 0.06), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    rng = random.Random(11)
    parts = []
    P = parts.append
    oct8 = lambda r: geo.polygon(2 * r, 2 * r, 8, rotate_deg=22.5)

    # hub tower along y (built along z, then stood up)
    up = geo.rot(x=-90)
    P(geo.bevel(geo.loft("Hub", [(-160, oct8(22)), (-120, oct8(30)), (100, oct8(30)), (140, oct8(22)), (170, oct8(12))], "HULL", matrix=up), width=0.8, segments=1))
    for z in range(-110, 100, 25):
        P(geo.bevel(geo.loft("HubBand", [(z, oct8(31)), (z + 6, oct8(31))], "TRIM", matrix=up), width=0.3, segments=1))
        for k in range(8):
            a = math.radians(k * 45)
            if rng.random() < 0.8:
                P(geo.box("HubWindows", (math.cos(a) * 30.6, z + 14, math.sin(a) * 30.6), (2.0, 1.4, 14.0), "WINDOW", geo.rot(y=-math.degrees(a) + 90)))
    # command crown on top and a dish
    P(geo.bevel(geo.cylinder("Crown", (0, 170, 0), (0, 190, 0), 40, 34, "HULL", sides=24), width=0.6, segments=1))
    P(geo.box("CrownWindows", (0, 182, 0), (79, 2.2, 79), "WINDOW"))
    P(geo.bevel(geo.cylinder("CrownTop", (0, 190, 0), (0, 196, 0), 30, 24, "DARK", sides=24), width=0.4, segments=1))
    P(geo.bevel(geo.cylinder("DishMast", (0, 196, 0), (0, 230, 0), 2.0, 1.6, "METAL", sides=10), width=0.1, segments=1))
    P(geo.bevel(geo.cylinder("Dish", (0, 228, 0), (0, 236, 6), 22, 8, "METAL", sides=24), width=0.2, segments=1))
    # bottom spire with beacons
    P(geo.bevel(geo.cylinder("Spire", (0, -160, 0), (0, -230, 0), 10, 2, "DARK", sides=12), width=0.2, segments=1))
    for y in (-180, -205, -228):
        P(geo.disc("Beacon", (0, y, 4.0), (0, 0, 1), 1.2, "RED", sides=10))

    # habitat ring on four spokes
    ring_r = 260.0
    ring = [(math.cos(math.radians(a)) * ring_r, 0.0, math.sin(math.radians(a)) * ring_r) for a in range(0, 360, 5)]
    P(geo.bevel(geo.sweep("Ring", ring, geo.rect(34, 26), "HULL", up=(0, 1, 0), closed_path=True), width=0.6, segments=1))
    P(geo.sweep("RingWindowsOut", [(x * 1.067, 4, z * 1.067) for x, _, z in ring], geo.rect(0.8, 2.2), "WINDOW", up=(0, 1, 0), closed_path=True))
    P(geo.sweep("RingWindowsIn", [(x * 0.932, -3, z * 0.932) for x, _, z in ring], geo.rect(0.8, 1.6), "WINDOW", up=(0, 1, 0), closed_path=True))
    P(geo.bevel(geo.sweep("RingTrim", [(x, 13.5, z) for x, _, z in ring], geo.rect(36, 2), "TRIM", up=(0, 1, 0), closed_path=True), width=0.2, segments=1))
    for k in range(4):
        a = math.radians(45 + k * 90)
        d = (math.cos(a), 0, math.sin(a))
        P(geo.bevel(geo.cylinder("Spoke", (d[0] * 28, 0, d[2] * 28), (d[0] * 245, 0, d[2] * 245), 7, 7, "METAL", sides=10), width=0.3, segments=1))
        P(geo.disc("SpokeLight", (d[0] * 150, 7.2, d[2] * 150), (0, 1, 0), 1.0, "BEACON", sides=8))

    # docking arms with berths along +x / -x at the hub's middle
    for side in (1, -1):
        P(geo.bevel(geo.box("Arm", (side * 140, 60, 0), (220, 16, 18), "DARK"), width=0.5, segments=1))
        for k in range(4):
            x = side * (70 + k * 48)
            P(geo.bevel(geo.box("Berth", (x, 60, 16), (22, 10, 14), "HULL"), width=0.3, segments=1))
            P(geo.box("BerthLight", (x, 60, 23.2), (16, 4, 0.5), "BEACON"))
            P(geo.box("BerthLight2", (x, 60, -9.4), (14, 1.0, 0.5), "WINDOW"))
        P(geo.disc("ArmTip", (side * 251, 60, 0), (side, 0, 0), 2.0, "RED", sides=10))

    # solar wings along z
    for side in (1, -1):
        P(geo.bevel(geo.cylinder("SolarBoom", (0, -60, side * 30), (0, -60, side * 520), 3, 3, "METAL", sides=8), width=0.15, segments=1))
        for k in range(5):
            z = side * (90 + k * 90)
            P(geo.box("Solar", (0, -60, z), (190, 1.2, 80), "SOLAR"))
            P(geo.box("SolarFrame", (0, -60, z + 40.5 * side), (192, 2, 2), "METAL"))
    # radiators and masts
    for k in range(6):
        a = math.radians(k * 60 + 30)
        P(geo.bevel(geo.box("Radiator", (math.cos(a) * 46, -100, math.sin(a) * 46), (3, 70, 34), "METAL", geo.rot(y=-math.degrees(a))), width=0.2, segments=1))
    for i in range(20):
        a = rng.uniform(0, 2 * math.pi)
        y = rng.uniform(-100, 90)
        P(geo.bevel(geo.box("Pod", (math.cos(a) * 32, y, math.sin(a) * 32), (rng.uniform(6, 14), rng.uniform(4, 10), rng.uniform(6, 14)), rng.choice(("DARK", "METAL", "HULL")),
                            geo.rot(y=-math.degrees(a))), width=0.2, segments=1))

    hull = geo.join(parts, NAME + "_LOD0")
    sockets = [geo.socket("Top", (0, 236, 0)), geo.socket("DockR", (251, 60, 0)), geo.socket("DockL", (-251, 60, 0))]
    return hull, sockets


def main(preview=True):
    hull, sockets = build()
    return pipeline.finish(NAME, hull, sockets, TEX, radius=420.0, lods=(0.35, 0.1), preview=preview, ao_distance=30.0)


if __name__ == "__main__":
    main()
