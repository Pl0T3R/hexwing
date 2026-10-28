"""
Trident: the enemy elite. A heavy near-black pod with an amber visor, three swept blade wings around it
(one up, two down at the sides) ending in bronze gun rails, one big amber engine and three small ones.
About 12 m long, 12 m across. Original design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Trident"
TEX = 1024

PALETTE = {
    "HULL": dict(base=(0.04, 0.042, 0.048), metal=0.6, rough=0.34, wear=0.35, panel_scale=1.0, panel_dark=0.45),
    "PLATE": dict(base=(0.06, 0.062, 0.07), metal=0.65, rough=0.3, wear=0.45, panel_scale=1.6, tone=0.05),
    "BRONZE": dict(base=(0.33, 0.19, 0.07), metal=0.9, rough=0.32, wear=0.35, panel=0.4, wear_color=(0.55, 0.42, 0.25)),
    "METAL": dict(base=(0.2, 0.2, 0.21), metal=0.95, rough=0.3, panel=0.3, wear=0.3),
    "DARK": dict(base=(0.02, 0.02, 0.024), metal=0.7, rough=0.55, panel=0.0, wear=0.1),
    "GLASS": dict(base=(0.02, 0.012, 0.004), metal=0.0, rough=0.05, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
    "ENGINE": dict(base=(0.1, 0.05, 0.01), emit=1.0, emit_color=(1.0, 0.55, 0.14), panel=0.0, wear=0.0, grime=0.0),
    "LIGHT": dict(base=(0.1, 0.05, 0.01), emit=1.0, emit_color=(1.0, 0.62, 0.2), panel=0.0, wear=0.0, grime=0.0),
}

BLADES = (90.0, 210.0, 330.0)


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    hexa = lambda w, h: geo.superellipse(w, h, count=12, power=2.3, flat_bottom=0.1)

    P(geo.bevel(geo.loft("Pod", [
        (-5.3, hexa(1.9, 1.75)),
        (-4.1, hexa(2.9, 2.6)),
        (0.0, hexa(3.1, 2.8)),
        (3.4, hexa(2.4, 2.15), (0, -0.05)),
        (5.4, hexa(1.2, 1.05), (0, -0.15)),
        (6.6, hexa(0.35, 0.32), (0, -0.25)),
    ], "HULL"), width=0.05))
    # armoured collar, a visor band with an amber slit, a chin sensor
    P(geo.bevel(geo.loft("Collar", [(1.4, hexa(3.22, 2.92)), (2.3, hexa(3.0, 2.72), (0, -0.02))], "PLATE"), width=0.03))
    P(geo.bevel(geo.loft("Visor", [
        (3.5, geo.superellipse(1.7, 0.6, count=12, power=2.0), (0, 0.72)),
        (4.4, geo.superellipse(1.4, 0.45, count=12, power=2.0), (0, 0.55)),
    ], "GLASS"), width=0.015))
    P(geo.box("VisorSlit", (0, 0.86, 3.9), (1.1, 0.04, 0.08), "LIGHT", geo.rot(x=-20)))
    P(geo.bevel(geo.cylinder("Chin", (0, -0.95, 4.2), (0, -0.85, 5.4), 0.2, 0.1, "METAL", sides=10), width=0.01, segments=1))

    for a in BLADES:
        spin = geo.rot(z=a)
        # swept blade wing, thick at the root, an armour plate on it
        blade = [(1.2, -3.4), (1.2, 1.8), (3.6, 0.6), (6.1, -0.4), (6.4, -2.0), (5.8, -3.9)]
        P(geo.bevel(geo.slab("Blade", blade, 0.42, "HULL", taper=0.4, matrix=spin), width=0.035))
        plate = [(1.6, -2.6), (1.6, 0.9), (4.6, -0.3), (4.7, -2.4)]
        P(geo.bevel(geo.slab("BladePlate", plate, 0.5, "PLATE", taper=0.5, matrix=spin), width=0.02, segments=1))
        # bronze gun rail at the tip with a muzzle and an amber light
        tip = spin @ geo.at((6.2, 0, 0))
        P(geo.bevel(geo.loft("Rail", [
            (-3.6, geo.superellipse(0.5, 0.5, count=10, power=2.5)),
            (0.6, geo.superellipse(0.56, 0.56, count=10, power=2.5)),
            (1.6, geo.superellipse(0.3, 0.3, count=10, power=2.5)),
        ], "BRONZE", matrix=tip), width=0.02))
        P(geo.tube("Muzzle", geo.xf(tip, (0, 0, 1.55)), geo.xf(tip, (0, 0, 2.1)), 0.14, 0.08, "DARK", sides=10))
        P(geo.disc("TipLight", geo.xf(tip, (0, 0, -3.62)), geo.xd(spin, (0, 0, -1)), 0.16, "LIGHT", sides=10))
        # small engine at the blade root
        root = spin @ geo.at((1.75, 0, 0))
        P(geo.bevel(geo.cylinder("SmallNacelle", geo.xf(root, (0, 0, -2.6)), geo.xf(root, (0, 0, -4.6)), 0.4, 0.36, "METAL", sides=12), width=0.015, segments=1))
        P(geo.tube("SmallNozzle", geo.xf(root, (0, 0, -4.55)), geo.xf(root, (0, 0, -4.95)), 0.36, 0.26, "DARK", sides=12))
        P(geo.disc("SmallCore", geo.xf(root, (0, 0, -4.65)), (0, 0, -1), 0.27, "ENGINE", sides=12))

    # the main engine
    P(geo.bevel(geo.cylinder("Nacelle", (0, 0, -4.8), (0, 0, -5.7), 1.15, 1.1, "METAL", sides=18), width=0.02, segments=1))
    P(geo.tube("Nozzle", (0, 0, -5.6), (0, 0, -6.3), 1.08, 0.85, "DARK", sides=18, flare=1.06))
    P(geo.disc("Core", (0, 0, -5.75), (0, 0, -1), 0.86, "ENGINE", sides=18))
    # spine vents
    for z in (-3.0, -1.6, -0.2):
        P(geo.bevel(geo.box("Vent", (0, 1.38, z), (0.7, 0.14, 0.9), "DARK"), width=0.02, segments=1))

    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (0, 0, -5.9), (0, 0, -8.6), 0.82, 0.1, "GLOW", sides=12, cap0=False, cap1=False)]
    for a in BLADES:
        root = geo.rot(z=a) @ geo.at((1.75, 0, 0))
        plumes.append(geo.cylinder("Plume", geo.xf(root, (0, 0, -4.9)), geo.xf(root, (0, 0, -6.2)), 0.26, 0.04, "GLOW", sides=8, cap0=False, cap1=False))
    glow = geo.join(plumes, NAME + "_Glow")
    sockets = [geo.socket("Engine", (0, 0, -6.3)), geo.socket("Nose", (0, -0.25, 6.6))]
    for i, a in enumerate(BLADES):
        sockets.append(geo.socket(f"Gun{i}", geo.xf(geo.rot(z=a) @ geo.at((6.2, 0, 0)), (0, 0, 2.1))))
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=7.5, preview=preview)


if __name__ == "__main__":
    main()
