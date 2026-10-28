"""
Sokół: the player's balanced fighter (shown in the hangar). Grey-blue hull with cyan stripes, a long
canopy, small canards, swept wings with two cannons each, one main engine and two small ones.
About 11 m long. Original design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Sokol"
TEX = 1024
ACCENT = (0.25, 0.8, 1.0)

PALETTE = {
    "HULL": dict(base=(0.3, 0.33, 0.37), metal=0.45, rough=0.4, panel_scale=1.1, wear=0.35, wear_color=(0.55, 0.57, 0.6)),
    "STRIPE": dict(base=(0.04, 0.3, 0.42), metal=0.35, rough=0.38, panel_scale=1.4, wear=0.4),
    "METAL": dict(base=(0.24, 0.24, 0.26), metal=0.9, rough=0.3, panel=0.3, wear=0.25),
    "DARK": dict(base=(0.03, 0.033, 0.038), metal=0.6, rough=0.5, panel=0.0, wear=0.15),
    "GLASS": dict(base=(0.012, 0.03, 0.045), metal=0.0, rough=0.04, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
    "ENGINE": dict(base=(0.02, 0.06, 0.08), emit=1.0, emit_color=ACCENT, panel=0.0, wear=0.0, grime=0.0),
    "LIGHT": dict(base=(0.02, 0.06, 0.08), emit=1.0, emit_color=ACCENT, panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    body = lambda w, h: geo.superellipse(w, h, count=14, power=2.6, flat_bottom=0.25)
    P(geo.bevel(geo.loft("Fuselage", [
        (-4.9, body(1.7, 1.4)), (-3.6, body(2.2, 1.7)), (0.0, body(2.0, 1.6)),
        (3.2, body(1.4, 1.15), (0, -0.05)), (5.4, body(0.6, 0.48), (0, -0.16)), (6.3, body(0.14, 0.12), (0, -0.22)),
    ], "HULL"), width=0.035))
    P(geo.bevel(geo.loft("Spine", [(-4.5, body(0.9, 0.5), (0, 0.75)), (0.2, body(0.7, 0.4), (0, 0.8))], "METAL"), width=0.02))
    P(geo.bevel(geo.loft("Canopy", [
        (0.4, geo.superellipse(0.7, 0.5, count=12, power=2.0), (0, 0.7)),
        (1.6, geo.superellipse(0.95, 0.8, count=12, power=2.0), (0, 0.82)),
        (3.0, geo.superellipse(0.75, 0.55, count=12, power=2.0), (0, 0.66)),
        (3.8, geo.superellipse(0.25, 0.16, count=12, power=2.0), (0, 0.5)),
    ], "GLASS"), width=0.015))
    P(geo.bevel(geo.loft("NoseStripe", [(4.0, body(1.12, 0.92), (0, -0.09)), (4.45, body(0.96, 0.78), (0, -0.12))], "STRIPE"), width=0.012))
    for side in (1, -1):
        sgn = lambda pts: pts if side > 0 else [(-x, z) for x, z in reversed(pts)]
        wing = sgn([(0.9, 0.4), (0.9, -2.8), (5.0, -3.6), (5.4, -3.0), (5.2, -2.2)])
        P(geo.bevel(geo.slab("Wing", wing, 0.28, "HULL", taper=0.5, matrix=geo.at((0, -0.25, -0.6), geo.rot(z=-3 * side))), width=0.025))
        stripe = sgn([(2.4, -1.25), (2.4, -2.95), (2.9, -3.05), (2.9, -1.4)])
        P(geo.bevel(geo.slab("WingStripe", stripe, 0.32, "STRIPE", taper=0.7, matrix=geo.at((0, -0.25, -0.6), geo.rot(z=-3 * side))), width=0.01, segments=1))
        canard = sgn([(0.7, 3.3), (0.7, 2.4), (1.9, 2.3), (2.0, 2.7)])
        P(geo.bevel(geo.slab("Canard", canard, 0.14, "HULL", taper=0.6, matrix=geo.at((0, -0.1, 0))), width=0.015))
        for k, x in enumerate((3.2, 5.25)):
            gx = x * side
            P(geo.bevel(geo.cylinder("Cannon", (gx, -0.25 - 0.12 * k, -2.0), (gx, -0.25 - 0.12 * k, 1.6 - k * 0.6), 0.11, 0.08, "METAL", sides=8), width=0.006, segments=1))
        x = 1.05 * side
        P(geo.bevel(geo.cylinder("SmallNacelle", (x, -0.3, -3.0), (x, -0.3, -4.9), 0.42, 0.38, "METAL", sides=12), width=0.012, segments=1))
        P(geo.tube("SmallNozzle", (x, -0.3, -4.85), (x, -0.3, -5.2), 0.38, 0.27, "DARK", sides=12))
        P(geo.disc("SmallCore", (x, -0.3, -4.95), (0, 0, -1), 0.28, "ENGINE", sides=12))
        P(geo.disc("WingLight", (5.3 * side, -0.1, -3.1), (side, 0, 0), 0.07, "LIGHT", sides=8))
        fin = [(0.0, -4.6), (0.0, -2.9), (1.4, -3.8), (1.5, -4.7)]
        P(geo.bevel(geo.slab("Fin", fin, 0.12, "HULL", taper=0.6, matrix=geo.at((0.45 * side, 0.7, 0), geo.rot(z=90 - 12 * side))), width=0.015))
    P(geo.bevel(geo.cylinder("Nacelle", (0, 0.1, -4.6), (0, 0.1, -5.3), 0.7, 0.66, "METAL", sides=16), width=0.015, segments=1))
    P(geo.tube("Nozzle", (0, 0.1, -5.25), (0, 0.1, -5.75), 0.66, 0.5, "DARK", sides=16, flare=1.05))
    P(geo.disc("Core", (0, 0.1, -5.4), (0, 0, -1), 0.5, "ENGINE", sides=16))
    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (0, 0.1, -5.55), (0, 0.1, -7.3), 0.48, 0.06, "GLOW", sides=10, cap0=False, cap1=False)]
    plumes += [geo.cylinder("Plume", (1.05 * s, -0.3, -5.1), (1.05 * s, -0.3, -6.2), 0.26, 0.04, "GLOW", sides=8, cap0=False, cap1=False) for s in (1, -1)]
    glow = geo.join(plumes, NAME + "_Glow")
    return hull, glow, [geo.socket("Nose", (0, -0.22, 6.3)), geo.socket("Engine", (0, 0.1, -5.75))]


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=6.5, preview=preview)


if __name__ == "__main__":
    main()
