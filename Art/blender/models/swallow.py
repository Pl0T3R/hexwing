"""
Swallow: the player's wingman. A slim light-grey fighter with a blue-tinted bubble canopy, swept-back
wings with gun pods at the tips, two canted tail fins and twin cyan engines. Blue squadron stripes.
About 11 m long. Original design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Swallow"
TEX = 1024

PALETTE = {
    "HULL": dict(base=(0.5, 0.52, 0.55), metal=0.35, rough=0.42, wear=0.35, panel_scale=1.1, panel_dark=0.55, wear_color=(0.7, 0.72, 0.75)),
    "STRIPE": dict(base=(0.035, 0.11, 0.3), metal=0.3, rough=0.4, wear=0.4, panel_scale=1.5, wear_color=(0.5, 0.52, 0.55)),
    "METAL": dict(base=(0.26, 0.27, 0.29), metal=0.9, rough=0.32, panel=0.3, wear=0.25),
    "DARK": dict(base=(0.03, 0.033, 0.038), metal=0.6, rough=0.5, panel=0.0, wear=0.15),
    "GLASS": dict(base=(0.015, 0.035, 0.05), metal=0.0, rough=0.04, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
    "ENGINE": dict(base=(0.02, 0.06, 0.08), emit=1.0, emit_color=(0.3, 0.85, 1.0), panel=0.0, wear=0.0, grime=0.0),
    "LIGHT": dict(base=(0.02, 0.06, 0.08), emit=1.0, emit_color=(0.45, 0.9, 1.0), panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    body = lambda w, h: geo.superellipse(w, h, count=14, power=2.3, flat_bottom=0.2)

    P(geo.bevel(geo.loft("Fuselage", [
        (-4.7, body(1.05, 0.95)),
        (-3.5, body(1.55, 1.3)),
        (0.0, body(1.65, 1.4)),
        (3.0, body(1.3, 1.12), (0, -0.04)),
        (5.0, body(0.62, 0.52), (0, -0.14)),
        (6.2, body(0.16, 0.13), (0, -0.22)),
    ], "HULL"), width=0.035))
    P(geo.bevel(geo.loft("NoseStripe", [(3.6, body(1.1, 0.94), (0, -0.07)), (4.1, body(0.92, 0.78), (0, -0.1))], "STRIPE"), width=0.015))
    P(geo.bevel(geo.loft("Canopy", [
        (1.0, geo.superellipse(0.75, 0.5, count=12, power=2.0), (0, 0.62)),
        (2.0, geo.superellipse(0.9, 0.78, count=12, power=2.0), (0, 0.72)),
        (3.1, geo.superellipse(0.7, 0.5, count=12, power=2.0), (0, 0.6)),
        (3.7, geo.superellipse(0.25, 0.16, count=12, power=2.0), (0, 0.45)),
    ], "GLASS"), width=0.015))
    P(geo.bevel(geo.box("CanopyFrame", (0, 1.06, 2.15), (0.86, 0.06, 0.08), "METAL"), width=0.01, segments=1))

    for side in (1, -1):
        sgn = lambda pts: pts if side > 0 else [(-x, z) for x, z in reversed(pts)]
        wing = sgn([(0.7, 0.9), (0.7, -2.5), (5.0, -3.5), (5.3, -2.9), (5.1, -2.0)])
        P(geo.bevel(geo.slab("Wing", wing, 0.26, "HULL", taper=0.5, matrix=geo.at((0, -0.15, -0.6), geo.rot(z=4 * side))), width=0.025))
        stripe = sgn([(3.3, -1.55), (3.3, -2.9), (3.8, -3.05), (3.8, -1.8)])
        P(geo.bevel(geo.slab("WingStripe", stripe, 0.3, "STRIPE", taper=0.7, matrix=geo.at((0, -0.15, -0.6), geo.rot(z=4 * side))), width=0.01, segments=1))
        # wingtip gun pod
        x = 5.2 * side
        P(geo.bevel(geo.loft("GunPod", [
            (-3.6, geo.superellipse(0.3, 0.3, count=10, power=2.2)),
            (-1.0, geo.superellipse(0.36, 0.36, count=10, power=2.2)),
            (0.4, geo.superellipse(0.2, 0.2, count=10, power=2.2)),
        ], "METAL", matrix=geo.at((x, 0.2, 0))), width=0.01))
        P(geo.bevel(geo.cylinder("Barrel", (x, 0.2, 0.3), (x, 0.2, 2.0), 0.06, 0.05, "DARK", sides=8), width=0.004, segments=1))
        P(geo.disc("TipLight", (x, 0.39, -2.0), (0, 1, 0), 0.07, "LIGHT", sides=8))
        # canted tail fin
        fin = [(0.0, -4.4), (0.0, -2.6), (1.7, -3.7), (1.85, -4.6)]
        P(geo.bevel(geo.slab("Fin", fin, 0.14, "HULL", taper=0.6, matrix=geo.at((0.55 * side, 0.6, 0), geo.rot(z=90 - 18 * side))), width=0.02))
        # engine
        ex = 0.45 * side
        P(geo.bevel(geo.cylinder("Nacelle", (ex, -0.05, -2.8), (ex, -0.05, -4.75), 0.5, 0.46, "METAL", sides=14), width=0.015, segments=1))
        P(geo.tube("Nozzle", (ex, -0.05, -4.7), (ex, -0.05, -5.15), 0.46, 0.34, "DARK", sides=14, flare=1.05))
        P(geo.disc("Core", (ex, -0.05, -4.8), (0, 0, -1), 0.35, "ENGINE", sides=14))
        # intake under the wing root
        P(geo.bevel(geo.box("Intake", (0.78 * side, -0.38, 0.3), (0.42, 0.5, 1.6), "DARK", geo.rot(y=5 * side)), width=0.03))
    P(geo.bevel(geo.box("Spine", (0, 0.72, -1.8), (0.3, 0.16, 2.6), "METAL"), width=0.02, segments=1))
    P(geo.bevel(geo.cylinder("Antenna", (0, 0.78, -2.8), (0, 1.35, -3.3), 0.02, 0.01, "METAL", sides=6), width=0.003, segments=1))

    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (0.45 * s, -0.05, -4.9), (0.45 * s, -0.05, -6.4), 0.33, 0.05, "GLOW", sides=10, cap0=False, cap1=False)
              for s in (1, -1)]
    glow = geo.join(plumes, NAME + "_Glow")
    sockets = [geo.socket("GunR", (5.2, 0.2, 2.0)), geo.socket("GunL", (-5.2, 0.2, 2.0)),
               geo.socket("Engine", (0, 0, -5.2)), geo.socket("Nose", (0, -0.22, 6.2))]
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=6.5, preview=preview)


if __name__ == "__main__":
    main()
