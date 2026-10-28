"""
Żubr: the player's bomber (shown in the hangar). A wide armoured body with a bomb bay, short thick
wings carrying two engine pods, a chin turret and a raised cockpit. Olive-brown with amber stripes.
About 14 m long. Original design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Zubr"
TEX = 1024
ACCENT = (1.0, 0.62, 0.18)

PALETTE = {
    "HULL": dict(base=(0.2, 0.19, 0.15), metal=0.4, rough=0.48, panel_scale=0.9, wear=0.45, grime=0.55, wear_color=(0.45, 0.43, 0.38)),
    "PLATE": dict(base=(0.14, 0.135, 0.11), metal=0.45, rough=0.45, panel_scale=1.4, wear=0.5, tone=0.05),
    "STRIPE": dict(base=(0.45, 0.25, 0.04), metal=0.3, rough=0.45, panel_scale=1.2, wear=0.5),
    "METAL": dict(base=(0.24, 0.24, 0.25), metal=0.9, rough=0.32, panel=0.3, wear=0.3),
    "DARK": dict(base=(0.025, 0.025, 0.028), metal=0.6, rough=0.55, panel=0.0, wear=0.15),
    "GLASS": dict(base=(0.04, 0.025, 0.01), metal=0.0, rough=0.05, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
    "ENGINE": dict(base=(0.1, 0.05, 0.01), emit=1.0, emit_color=ACCENT, panel=0.0, wear=0.0, grime=0.0),
    "LIGHT": dict(base=(0.1, 0.05, 0.01), emit=1.0, emit_color=ACCENT, panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    box = lambda w, h: geo.superellipse(w, h, count=12, power=4.0, flat_bottom=0.1)
    P(geo.bevel(geo.loft("Body", [
        (-6.0, box(2.6, 1.9)), (-4.8, box(3.6, 2.4)), (2.0, box(3.4, 2.4)),
        (4.8, box(2.4, 1.8), (0, -0.1)), (6.6, box(1.0, 0.8), (0, -0.3)), (7.4, box(0.4, 0.3), (0, -0.35)),
    ], "HULL"), width=0.05))
    for z in (-3.2, -0.6, 1.8):
        P(geo.bevel(geo.box("Plate", (0, 1.22, z), (2.6, 0.12, 2.0), "PLATE"), width=0.03, segments=1))
    P(geo.box("BombBay", (0, -1.22, -1.0), (1.8, 0.08, 4.4), "DARK"))
    P(geo.bevel(geo.loft("Cockpit", [
        (2.2, geo.superellipse(1.4, 0.7, count=12, power=2.2), (0, 1.25)),
        (3.4, geo.superellipse(1.3, 0.75, count=12, power=2.2), (0, 1.2)),
        (4.3, geo.superellipse(0.9, 0.4, count=12, power=2.2), (0, 0.95)),
    ], "GLASS"), width=0.02))
    P(geo.bevel(geo.loft("Stripe", [(5.1, box(2.24, 1.7), (0, -0.14)), (5.5, box(2.0, 1.52), (0, -0.17))], "STRIPE"), width=0.012))
    P(geo.bevel(geo.loft("ChinTurret", [(4.6, geo.superellipse(0.9, 0.6, count=10, power=2.2), (0, -1.1)), (5.6, geo.superellipse(0.8, 0.55, count=10, power=2.2), (0, -1.1))], "METAL"), width=0.02))
    for x in (-0.18, 0.18):
        P(geo.bevel(geo.cylinder("ChinGun", (x, -1.1, 5.5), (x, -1.1, 6.9), 0.07, 0.06, "DARK", sides=8), width=0.005, segments=1))
    for side in (1, -1):
        sgn = lambda pts: pts if side > 0 else [(-x, z) for x, z in reversed(pts)]
        wing = sgn([(1.6, 1.2), (1.6, -2.4), (5.6, -2.0), (5.6, 0.2)])
        P(geo.bevel(geo.slab("Wing", wing, 0.45, "HULL", taper=0.7, matrix=geo.at((0, -0.2, 0))), width=0.03))
        stripe = sgn([(4.3, 0.4), (4.3, -2.2), (4.8, -2.15), (4.8, 0.32)])
        P(geo.bevel(geo.slab("WingStripe", stripe, 0.5, "STRIPE", taper=0.8, matrix=geo.at((0, -0.2, 0))), width=0.01, segments=1))
        x = 3.6 * side
        P(geo.bevel(geo.loft("Pod", [(-3.6, geo.superellipse(1.1, 1.1, count=14, power=2.4)), (1.6, geo.superellipse(1.2, 1.2, count=14, power=2.4)),
                                     (2.6, geo.superellipse(0.7, 0.7, count=14, power=2.4))], "PLATE", matrix=geo.at((x, -0.2, 0))), width=0.025))
        P(geo.tube("Nozzle", (x, -0.2, -3.55), (x, -0.2, -4.2), 0.55, 0.42, "DARK", sides=14, flare=1.06))
        P(geo.disc("Core", (x, -0.2, -3.7), (0, 0, -1), 0.42, "ENGINE", sides=14))
        P(geo.disc("WingLight", (5.65 * side, -0.2, -1.0), (side, 0, 0), 0.08, "LIGHT", sides=8))
        fin = [(0.0, -6.0), (0.0, -4.4), (1.3, -5.2), (1.4, -6.0)]
        P(geo.bevel(geo.slab("Fin", fin, 0.18, "HULL", taper=0.7, matrix=geo.at((1.2 * side, 1.0, 0), geo.rot(z=90 - 8 * side))), width=0.02))
    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (3.6 * s, -0.2, -4.1), (3.6 * s, -0.2, -5.8), 0.4, 0.06, "GLOW", sides=10, cap0=False, cap1=False) for s in (1, -1)]
    glow = geo.join(plumes, NAME + "_Glow")
    return hull, glow, [geo.socket("Nose", (0, -0.35, 7.4)), geo.socket("Engine", (0, 0, -6.0))]


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=8.0, preview=preview)


if __name__ == "__main__":
    main()
