"""
ColossusTurret: a gun turret of the capital ship (six on its flanks, destructible, placed by code). A sharp,
angular gun house: a flat hexagonal body narrowing to a wedge prow, a raised armoured spine, two blade
fins, square barrel shrouds with long barrels and box muzzle brakes, red trim along the chines and a red
sensor chevron, on an octagonal base. About 16 m across; the barrels point along +z (the code turns
the turret toward its side). Original design.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import geo, pipeline, surface  # noqa: E402

NAME = "ColossusTurret"
TEX = 512

PALETTE = {
    "HULL": dict(base=(0.09, 0.095, 0.105), metal=0.55, rough=0.42, panel_scale=0.7, mortar=0.012, wear=0.4, grime=0.5, ao_dist=3.0, bevel=0.06),
    "PLATE": dict(base=(0.13, 0.135, 0.145), metal=0.6, rough=0.4, panel_scale=0.9, mortar=0.012, wear=0.45, grime=0.45, ao_dist=2.0, bevel=0.05),
    "TRIM": dict(base=(0.32, 0.035, 0.03), metal=0.4, rough=0.42, panel=0.0, wear=0.4, ao_dist=2.0, bevel=0.03),
    "METAL": dict(base=(0.22, 0.22, 0.23), metal=0.9, rough=0.35, panel=0.2, wear=0.3, ao_dist=2.0, bevel=0.03),
    "DARK": dict(base=(0.02, 0.02, 0.024), metal=0.6, rough=0.6, panel=0.0, wear=0.1, ao_dist=2.0, bevel=0.02),
    "LIGHT": dict(base=(0.2, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.12, 0.06), panel=0.0, wear=0.0, grime=0.0, bevel=0.0),
}


def hexa(w, h):
    """A flat hexagon: pointed at the sides (the chines), flat top and bottom."""
    return geo.polygon(w, h, 6, 0.0)


def sharp(ob, width=0.06):
    """Crisp edges: one narrow bevel segment, hard normals."""
    return geo.bevel(ob, width=width, segments=1, angle=25)


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append
    # octagonal base and a red ring
    P(sharp(geo.cylinder("Base", (0, -3.0, 0), (0, -1.0, 0), 8.2, 7.4, "METAL", sides=8), 0.1))
    P(sharp(geo.cylinder("BaseTrim", (0, -1.1, 0), (0, -0.8, 0), 7.5, 7.4, "TRIM", sides=8), 0.04))
    # the gun house: flat hexagon sections, wide at the back, a wedge prow in front
    house = [(-6.5, hexa(10.0, 3.6)), (-4.5, hexa(13.0, 4.8)), (1.5, hexa(13.0, 5.0)), (4.5, hexa(8.0, 3.4)), (6.8, hexa(2.4, 1.2))]
    P(sharp(geo.loft("House", house, "HULL", matrix=geo.at((0, 1.2, 0))), 0.1))
    # raised armour spine on top with a red sensor chevron at its front
    spine = [(-5.5, hexa(4.0, 1.6)), (2.5, hexa(4.5, 1.8)), (4.5, hexa(1.2, 0.6))]
    P(sharp(geo.loft("Spine", spine, "PLATE", matrix=geo.at((0, 3.6, 0)))))
    for x in (-1, 1):
        P(geo.box("Chevron", (x * 0.85, 4.05, 3.75), (1.6, 0.25, 0.25), "LIGHT", geo.rot(y=-30 * x)))
    # two swept blade fins at the back
    fin = [(0.0, -6.0), (0.0, -2.5), (2.6, -6.4), (2.2, -6.8)]
    for x in (-1, 1):
        P(sharp(geo.slab("Fin", [(x * a, b) for a, b in fin], 0.3, "PLATE", taper=0.5, matrix=geo.at((x * 2.2, 3.4, 0), geo.rot(z=90 - 20 * x)))))
    # red trim along both chines and armour plates on the cheeks
    for x in (-1, 1):
        P(geo.sweep("ChineTrim", [(x * 6.55, 1.2, -4.4), (x * 6.55, 1.2, 1.4), (x * 4.05, 1.2, 4.4)], geo.rect(0.15, 0.35), "TRIM"))
        P(sharp(geo.box("Cheek", (x * 5.0, 2.6, -1.5), (2.4, 0.4, 5.0), "PLATE", geo.rot(z=-20 * x))))
    # square barrel shrouds, long barrels, box muzzle brakes
    for x in (-1.7, 1.7):
        shroud = [(3.0, geo.polygon(2.2, 2.2, 4, 45.0)), (7.5, geo.polygon(1.5, 1.5, 4, 45.0))]
        P(sharp(geo.loft("Shroud", shroud, "PLATE", matrix=geo.at((x, 1.4, 0)))))
        barrel = [(7.4, geo.polygon(0.9, 0.9, 4, 45.0)), (15.0, geo.polygon(0.7, 0.7, 4, 45.0))]
        P(sharp(geo.loft("Barrel", barrel, "METAL", matrix=geo.at((x, 1.4, 0))), 0.03))
        P(sharp(geo.box("MuzzleBrake", (x, 1.4, 15.5), (1.2, 1.2, 1.2), "DARK"), 0.04))
        P(geo.box("BrakeSlot", (x, 1.4, 15.5), (1.3, 0.25, 0.5), "DARK"))
    hull = geo.join(parts, NAME + "_LOD0")
    return hull, [geo.socket("Muzzle", (0, 1.4, 16.1))]


def main(preview=True):
    hull, sockets = build()
    return pipeline.finish(NAME, hull, sockets, TEX, radius=12.0, preview=preview, ao_distance=4.0)


if __name__ == "__main__":
    main()
