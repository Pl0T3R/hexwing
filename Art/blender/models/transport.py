"""
Transport: the convoy hauler of the Escort mode, about 75 m long. A hexagonal spine carries eight cargo
containers in two rows (each a different faded colour, with stencil stripes), a crew cab with a lit
window strip at the front, radiator fins, and two cyan engines at the back. Fits the BoxCollider in
Combat/Transport.cs (16 x 16 x 70 m). Original design. Unity frame: z forward, y up, x right.
"""
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Transport"
TEX = 1024

PALETTE = {
    "HULL": dict(base=(0.42, 0.44, 0.47), metal=0.4, rough=0.5, panel_scale=0.35, mortar=0.01, wear=0.4, grime=0.55, ao_dist=3.0, bevel=0.06),
    "CARGO_A": dict(base=(0.32, 0.17, 0.05), metal=0.2, rough=0.65, panel_scale=0.5, mortar=0.012, wear=0.5, grime=0.6, ao_dist=2.0, bevel=0.05),
    "CARGO_B": dict(base=(0.08, 0.16, 0.22), metal=0.2, rough=0.65, panel_scale=0.5, mortar=0.012, wear=0.5, grime=0.6, ao_dist=2.0, bevel=0.05),
    "CARGO_C": dict(base=(0.26, 0.26, 0.24), metal=0.2, rough=0.65, panel_scale=0.5, mortar=0.012, wear=0.5, grime=0.6, ao_dist=2.0, bevel=0.05),
    "STRIPE": dict(base=(0.5, 0.38, 0.05), metal=0.2, rough=0.6, panel=0.0, wear=0.6, grime=0.5, ao_dist=2.0, bevel=0.03),
    "METAL": dict(base=(0.22, 0.22, 0.23), metal=0.9, rough=0.35, panel=0.3, panel_scale=0.6, wear=0.3, ao_dist=2.0, bevel=0.05),
    "DARK": dict(base=(0.025, 0.027, 0.03), metal=0.6, rough=0.55, panel=0.0, wear=0.1, ao_dist=2.0, bevel=0.03),
    "WINDOW": dict(base=(0.05, 0.1, 0.12), emit=1.0, emit_color=(0.55, 0.9, 1.0), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
    "ENGINE": dict(base=(0.02, 0.06, 0.08), emit=1.0, emit_color=(0.3, 0.85, 1.0), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    rng = random.Random(3)
    parts = []
    P = parts.append
    spine = geo.polygon(8.5, 8.5, 6, rotate_deg=30)
    P(geo.bevel(geo.loft("Spine", [(-33.0, spine), (29.0, spine)], "HULL"), width=0.15, segments=1))
    for i in range(4):
        z = -24.0 + i * 13.0
        for y in (5.6, -5.6):
            slot = rng.choice(("CARGO_A", "CARGO_B", "CARGO_C"))
            P(geo.bevel(geo.box("Container", (0, y, z), (12.0, 7.0, 11.4), slot), width=0.12, segments=1))
            P(geo.box("Stripe", (0, y, z + 4.8), (12.1, 7.1, 0.7), "STRIPE"))
            for x in (-6.05, 6.05):
                for k in range(3):
                    P(geo.box("Rib", (x, y, z - 4 + k * 4), (0.25, 6.6, 0.3), "METAL"))
        # clamp frame between the container pairs
        P(geo.bevel(geo.box("Clamp", (0, 0, z), (13.2, 1.2, 1.4), "METAL"), width=0.1, segments=1))
    # crew cab
    P(geo.bevel(geo.loft("Cab", [
        (29.0, geo.superellipse(9.5, 8.5, count=12, power=3.0)),
        (34.0, geo.superellipse(9.0, 8.0, count=12, power=3.0), (0, 0.5)),
        (37.5, geo.superellipse(6.0, 5.0, count=12, power=2.6), (0, 1.0)),
    ], "HULL"), width=0.12, segments=1))
    P(geo.box("CabWindow", (0, 3.4, 36.0), (5.4, 1.1, 1.6), "WINDOW", geo.rot(x=-30)))
    P(geo.bevel(geo.cylinder("Antenna", (2, 4.4, 32), (2.5, 8, 31), 0.12, 0.06, "METAL", sides=6), width=0.02, segments=1))
    # radiator fins above and below the spine at the back
    for y in (1, -1):
        P(geo.bevel(geo.slab("Radiator", [(0.0, -33), (0.0, -27), (6.0, -28), (6.0, -32)], 0.3, "METAL",
                             matrix=geo.at((0, 4.2 * y, 0), geo.rot(z=90 * y))), width=0.05, segments=1))
    # engines
    for x in (-6.0, 6.0):
        P(geo.bevel(geo.box("Pylon", (x * 0.6, 0, -34.5), (5.0, 2.0, 4.0), "HULL"), width=0.1, segments=1))
        P(geo.bevel(geo.cylinder("EngineBody", (x, 0, -31.0), (x, 0, -38.5), 2.4, 2.2, "METAL", sides=16), width=0.08, segments=1))
        P(geo.tube("Nozzle", (x, 0, -38.4), (x, 0, -40.0), 2.2, 1.65, "DARK", sides=16, flare=1.06))
        P(geo.disc("Core", (x, 0, -38.7), (0, 0, -1), 1.68, "ENGINE", sides=16))
    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (x, 0, -40.2), (x, 0, -46.0), 1.6, 0.2, "GLOW", sides=12, cap0=False, cap1=False) for x in (-6.0, 6.0)]
    glow = geo.join(plumes, NAME + "_Glow")
    sockets = [geo.socket("Engine", (0, 0, -40)), geo.socket("Nose", (0, 1, 37.5))]
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=40.0, preview=preview, ao_distance=4.0)


if __name__ == "__main__":
    main()
