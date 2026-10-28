"""
Drone: the target drone of the drone range and the training. A sharp dart about 7 m long: an octagonal
body with a long pointed nose and a red sensor eye, four swept fins in an X with orange and white target
stripes, a red glowing band behind the nose, and a glowing engine nozzle at the back. Fits the 4 m hit
radius (GameConfig drones.radius). Original design. Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Drone"
TEX = 512

PALETTE = {
    "HULL": dict(base=(0.55, 0.56, 0.58), metal=0.35, rough=0.38, panel_scale=1.8, wear=0.35, grime=0.35),
    "ORANGE": dict(base=(0.7, 0.2, 0.02), metal=0.2, rough=0.45, panel=0.0, wear=0.35, grime=0.3),
    "WHITE": dict(base=(0.75, 0.76, 0.77), metal=0.1, rough=0.45, panel=0.0, wear=0.3, grime=0.3),
    "DARK": dict(base=(0.03, 0.033, 0.038), metal=0.6, rough=0.5, panel=0.0, wear=0.15),
    "METAL": dict(base=(0.25, 0.25, 0.27), metal=0.9, rough=0.3, panel=0.3, wear=0.25),
    "LIGHT": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.1, 0.05), panel=0.0, wear=0.0, grime=0.0),
    "ENGINE": dict(base=(0.2, 0.05, 0.01), emit=1.0, emit_color=(1.0, 0.4, 0.12), panel=0.0, wear=0.0, grime=0.0),
}


def octa(r):
    return geo.polygon(2 * r, 2 * r, 8, 22.5)


def sharp(ob, width=0.025):
    return geo.bevel(ob, width=width, segments=1, angle=25)


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    # body: blunt octagonal tail, full middle, long pointed nose
    body = [(-3.2, octa(0.55)), (-2.6, octa(0.8)), (0.6, octa(0.85)), (2.2, octa(0.5)), (3.8, octa(0.04))]
    P(sharp(geo.loft("Body", body, "HULL")))
    P(sharp(geo.loft("NoseCone", [(2.6, octa(0.42)), (3.82, octa(0.035))], "ORANGE", matrix=geo.at((0, 0, 0)))))
    P(geo.disc("Eye", (0, 0.0, 3.0), (0, 0.15, 1), 0.2, "LIGHT", sides=10))
    P(sharp(geo.loft("Band", [(1.1, octa(0.88)), (1.4, octa(0.82))], "LIGHT"), 0.01))
    P(sharp(geo.loft("TailBand", [(-2.4, octa(0.84)), (-1.9, octa(0.86))], "ORANGE"), 0.01))
    # four swept fins in an X, each with an orange root stripe and a white tip
    fin = [(0.6, -2.9), (0.6, 0.2), (3.2, -2.2), (3.4, -2.9)]
    for k in range(4):
        spin = geo.rot(z=45 + 90 * k)
        P(sharp(geo.slab("Fin", fin, 0.12, "HULL", taper=0.5, matrix=spin)))
        P(geo.slab("FinStripe", [(1.6, -2.85), (1.6, -0.5), (2.1, -1.0), (2.1, -2.85)], 0.14, "ORANGE", taper=0.6, matrix=spin))
        P(geo.slab("FinTip", [(2.8, -2.85), (2.8, -1.85), (3.3, -2.2), (3.4, -2.85)], 0.13, "WHITE", taper=0.6, matrix=spin))
    # engine nozzle with a glowing core
    P(geo.tube("Nozzle", (0, 0, -3.5), (0, 0, -3.15), 0.5, 0.34, "METAL", sides=12))
    P(geo.disc("EngineCore", (0, 0, -3.3), (0, 0, -1), 0.36, "ENGINE", sides=12))
    hull = geo.join(parts, NAME + "_LOD0")
    glow = geo.join([geo.cylinder("Plume", (0, 0, -3.5), (0, 0, -5.5), 0.34, 0.05, "GLOW", sides=10, cap0=False, cap1=False)], NAME + "_Glow")
    sockets = [geo.socket("Nose", (0, 0, 3.8)), geo.socket("Engine", (0, 0, -3.5))]
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=4.5, preview=preview, ao_distance=0.8)


if __name__ == "__main__":
    main()
