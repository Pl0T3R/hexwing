"""
Drone: the target drone of the drone range and the training. A hovering quad drone about 6 m across, not
a dart: a faceted gem-shaped core with a dark sensor lens and a red eye at the front, a red glowing band
around its waist, four angular arms in an X carrying short thruster pods (glowing at the back, orange
caps at the front), orange target plates top and bottom, and a sensor antenna. Fits the 4 m hit radius
(GameConfig drones.radius). Original design. Unity frame: z forward, y up, x right.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "Drone"
TEX = 512

PALETTE = {
    "HULL": dict(base=(0.55, 0.56, 0.58), metal=0.35, rough=0.38, panel_scale=1.8, wear=0.35, grime=0.35),
    "ORANGE": dict(base=(0.7, 0.2, 0.02), metal=0.2, rough=0.45, panel=0.0, wear=0.35, grime=0.3),
    "DARK": dict(base=(0.03, 0.033, 0.038), metal=0.6, rough=0.5, panel=0.0, wear=0.15),
    "METAL": dict(base=(0.25, 0.25, 0.27), metal=0.9, rough=0.3, panel=0.3, wear=0.25),
    "GLASS": dict(base=(0.01, 0.012, 0.016), metal=0.0, rough=0.05, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
    "LIGHT": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.1, 0.05), panel=0.0, wear=0.0, grime=0.0),
    "ENGINE": dict(base=(0.2, 0.05, 0.01), emit=1.0, emit_color=(1.0, 0.4, 0.12), panel=0.0, wear=0.0, grime=0.0),
}

ARM_R = 2.5   # thruster pods' distance from the centre


def octa(r):
    return geo.polygon(2 * r, 2 * r, 8, 22.5)


def sharp(ob, width=0.025):
    return geo.bevel(ob, width=width, segments=1, angle=25)


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    # the core: a faceted gem, wider than it is long
    core = [(-1.15, octa(0.55)), (-0.6, octa(1.15)), (0.6, octa(1.15)), (1.15, octa(0.62))]
    P(sharp(geo.loft("Core", core, "HULL")))
    P(sharp(geo.loft("Band", [(-0.12, octa(1.19)), (0.12, octa(1.19))], "LIGHT"), 0.01))
    # the front: a dark lens with a red eye
    P(sharp(geo.loft("Lens", [(1.1, octa(0.5)), (1.3, octa(0.38))], "GLASS"), 0.01))
    P(geo.disc("Eye", (0, 0, 1.31), (0, 0, 1), 0.16, "LIGHT", sides=10))
    # orange target plates on top and bottom, an antenna on top
    for y in (1, -1):
        P(sharp(geo.box("TargetPlate", (0, y * 1.08, 0), (1.0, 0.08, 1.0), "ORANGE", geo.rot(y=45)), 0.01))
    P(geo.cylinder("Antenna", (0.3, 1.1, -0.3), (0.3, 2.0, -0.5), 0.04, 0.02, "METAL", sides=6))
    P(geo.disc("AntennaTip", (0.3, 2.02, -0.5), (0, 1, 0), 0.07, "LIGHT", sides=6))
    # four arms in an X with thruster pods
    glows = []
    for k in range(4):
        a = math.radians(45 + 90 * k)
        cx, cy = math.cos(a), math.sin(a)
        arm = [(cx * 0.9, cy * 0.9, 0.0), (cx * (ARM_R - 0.3), cy * (ARM_R - 0.3), -0.2)]
        P(sharp(geo.sweep("Arm", arm, geo.rect(0.34, 0.34), "METAL", up=(0, 0, 1))))
        px, py = cx * ARM_R, cy * ARM_R
        pod = [(-0.9, octa(0.32)), (-0.6, octa(0.42)), (0.5, octa(0.42)), (0.8, octa(0.3))]
        P(sharp(geo.loft("Pod", pod, "HULL", matrix=geo.at((px, py, -0.2)))))
        P(sharp(geo.loft("PodCap", [(0.5, octa(0.43)), (0.8, octa(0.31))], "ORANGE", matrix=geo.at((px, py, -0.2))), 0.01))
        P(geo.disc("PodGlow", (px, py, -1.11), (0, 0, -1), 0.26, "ENGINE", sides=8))
        glows.append(geo.cylinder("Plume", (px, py, -1.12), (px, py, -2.4), 0.24, 0.04, "GLOW", sides=8, cap0=False, cap1=False))
    hull = geo.join(parts, NAME + "_LOD0")
    glow = geo.join(glows, NAME + "_Glow")
    sockets = [geo.socket("Nose", (0, 0, 1.3)), geo.socket("Engine", (0, 0, -1.2))]
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    return pipeline.finish(NAME, hull, [glow] + sockets, TEX, radius=4.0, preview=preview, ao_distance=0.6)


if __name__ == "__main__":
    main()
