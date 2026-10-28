"""
Jastrząb: the player's interceptor (shown in the hangar). Very slim, a long needle nose, cranked delta
wings, twin outward-canted fins, two big engines close together. Dark green-grey with bright green
stripes. About 11 m long. Original design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Jastrzab"
TEX = 1024
ACCENT = (0.35, 1.0, 0.45)

PALETTE = {
    "HULL": dict(base=(0.16, 0.19, 0.17), metal=0.5, rough=0.36, panel_scale=1.2, wear=0.35, wear_color=(0.45, 0.48, 0.46)),
    "STRIPE": dict(base=(0.08, 0.32, 0.1), metal=0.35, rough=0.38, panel_scale=1.4, wear=0.4),
    "METAL": dict(base=(0.24, 0.24, 0.25), metal=0.9, rough=0.3, panel=0.3, wear=0.25),
    "DARK": dict(base=(0.025, 0.03, 0.028), metal=0.6, rough=0.5, panel=0.0, wear=0.15),
    "GLASS": dict(base=(0.015, 0.03, 0.02), metal=0.0, rough=0.04, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
    "ENGINE": dict(base=(0.03, 0.08, 0.03), emit=1.0, emit_color=ACCENT, panel=0.0, wear=0.0, grime=0.0),
    "LIGHT": dict(base=(0.03, 0.08, 0.03), emit=1.0, emit_color=ACCENT, panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    body = lambda w, h: geo.superellipse(w, h, count=12, power=2.2, flat_bottom=0.3)
    P(geo.bevel(geo.loft("Fuselage", [
        (-4.6, body(1.9, 1.1)), (-3.2, body(2.0, 1.25)), (0.0, body(1.3, 1.1)),
        (3.0, body(0.8, 0.7), (0, -0.05)), (5.6, body(0.3, 0.26), (0, -0.12)), (6.8, body(0.06, 0.06), (0, -0.15)),
    ], "HULL"), width=0.03))
    P(geo.bevel(geo.loft("Canopy", [
        (-0.2, geo.superellipse(0.6, 0.45, count=12, power=2.0), (0, 0.55)),
        (0.9, geo.superellipse(0.75, 0.7, count=12, power=2.0), (0, 0.66)),
        (2.2, geo.superellipse(0.5, 0.4, count=12, power=2.0), (0, 0.46)),
        (2.9, geo.superellipse(0.16, 0.12, count=12, power=2.0), (0, 0.34)),
    ], "GLASS"), width=0.012))
    for side in (1, -1):
        sgn = lambda pts: pts if side > 0 else [(-x, z) for x, z in reversed(pts)]
        wing = sgn([(0.6, 1.2), (0.6, -3.6), (4.6, -4.0), (4.7, -3.4), (2.0, -0.6)])
        P(geo.bevel(geo.slab("Wing", wing, 0.22, "HULL", taper=0.45, matrix=geo.at((0, -0.2, 0))), width=0.02))
        stripe = sgn([(1.6, -0.9), (1.9, -0.6), (4.5, -3.3), (4.3, -3.55)])
        P(geo.bevel(geo.slab("WingStripe", stripe, 0.26, "STRIPE", taper=0.6, matrix=geo.at((0, -0.2, 0))), width=0.008, segments=1))
        fin = [(0.0, -4.5), (0.0, -2.6), (1.6, -3.9), (1.75, -4.8)]
        P(geo.bevel(geo.slab("Fin", fin, 0.12, "HULL", taper=0.6, matrix=geo.at((0.75 * side, 0.45, 0), geo.rot(z=90 - 25 * side))), width=0.015))
        x = 0.55 * side
        P(geo.bevel(geo.cylinder("Nacelle", (x, -0.05, -3.0), (x, -0.05, -4.9), 0.55, 0.52, "METAL", sides=14), width=0.012, segments=1))
        P(geo.tube("Nozzle", (x, -0.05, -4.85), (x, -0.05, -5.35), 0.52, 0.4, "DARK", sides=14, flare=1.05))
        P(geo.disc("Core", (x, -0.05, -5.0), (0, 0, -1), 0.4, "ENGINE", sides=14))
        P(geo.bevel(geo.cylinder("Cannon", (0.9 * side, -0.35, -0.5), (0.9 * side, -0.35, 2.8), 0.08, 0.06, "METAL", sides=8), width=0.005, segments=1))
        P(geo.disc("WingLight", (4.7 * side, -0.1, -3.7), (side, 0, 0), 0.06, "LIGHT", sides=8))
    P(geo.bevel(geo.loft("NoseStripe", [(3.6, body(0.7, 0.62), (0, -0.07)), (4.0, body(0.6, 0.53), (0, -0.08))], "STRIPE"), width=0.01))
    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (0.55 * s, -0.05, -5.2), (0.55 * s, -0.05, -7.0), 0.38, 0.05, "GLOW", sides=10, cap0=False, cap1=False) for s in (1, -1)]
    glow = geo.join(plumes, NAME + "_Glow")
    return hull, glow, [geo.socket("Nose", (0, -0.15, 6.8)), geo.socket("Engine", (0, 0, -5.35))]


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=6.5, preview=preview)


if __name__ == "__main__":
    main()
