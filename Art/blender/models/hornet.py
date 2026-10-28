"""
Hornet: the enemy fighter. A hexagonal armoured pod with a dark canopy, forward-swept drooping
wings ending in red blades with cannons, twin red engines and a dorsal fin. About 9 m long.
Original design (no film ships). Unity frame: z forward, y up, x right.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from lib import bake, export, geo, surface  # noqa: E402

NAME = "Hornet"
TEX = 1024

PALETTE = {
    "HULL": dict(base=(0.11, 0.115, 0.13), metal=0.5, rough=0.4, wear=0.4, panel_scale=1.1),
    "PLATE": dict(base=(0.075, 0.078, 0.09), metal=0.55, rough=0.36, wear=0.5, panel_scale=1.8, tone=0.04),
    "ACCENT": dict(base=(0.30, 0.022, 0.018), metal=0.35, rough=0.38, wear=0.6, panel_scale=1.6),
    "METAL": dict(base=(0.22, 0.22, 0.23), metal=0.95, rough=0.32, panel=0.4, wear=0.3),
    "DARK": dict(base=(0.025, 0.025, 0.03), metal=0.7, rough=0.55, panel=0.0, wear=0.15),
    "GLASS": dict(base=(0.008, 0.01, 0.014), metal=0.0, rough=0.05, panel=0.0, wear=0.0, grime=0.15, bump=0.0),
    "ENGINE": dict(base=(0.1, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.13, 0.05), panel=0.0, wear=0.0, grime=0.0),
    "LIGHT": dict(base=(0.1, 0.02, 0.01), emit=1.0, emit_color=(1.0, 0.06, 0.03), panel=0.0, wear=0.0, grime=0.0),
}


def build():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append

    # fuselage: a hexagonal pod, wide at the wing roots, tapering to a pointed nose
    hexa = lambda w, h: geo.superellipse(w, h, count=12, power=2.6, flat_bottom=0.25)
    P(geo.bevel(geo.loft("Fuselage", [
        (-4.3, hexa(1.6, 1.35), (0, 0.05)),
        (-3.2, hexa(2.35, 1.95)),
        (-0.4, hexa(2.55, 2.1)),
        (1.6, hexa(2.15, 1.8), (0, 0.02)),
        (3.0, hexa(1.35, 1.1), (0, -0.06)),
        (3.95, hexa(0.42, 0.34), (0, -0.2)),
    ], "HULL"), width=0.04))
    # armoured collar behind the nose (red band)
    P(geo.bevel(geo.loft("NoseBand", [
        (2.15, hexa(1.86, 1.58), (0, -0.01)),
        (2.5, hexa(1.62, 1.36), (0, -0.04)),
    ], "ACCENT"), width=0.02))
    # canopy: a low dark glass blister
    P(geo.bevel(geo.loft("Canopy", [
        (0.0, geo.superellipse(0.9, 0.5, count=12, power=2.2), (0, 0.9)),
        (0.8, geo.superellipse(1.1, 0.75, count=12, power=2.2), (0, 1.0)),
        (1.8, geo.superellipse(0.85, 0.5, count=12, power=2.2), (0, 0.82)),
        (2.4, geo.superellipse(0.3, 0.2, count=12, power=2.2), (0, 0.62)),
    ], "GLASS"), width=0.02))
    # canopy frame ribs
    for z, y in ((0.55, 1.18), (1.35, 1.12)):
        P(geo.bevel(geo.box("Rib", (0, y, z), (1.05, 0.08, 0.1), "METAL"), width=0.015, segments=1))

    # forward-swept wings with anhedral
    wing = [(1.0, -2.3), (1.0, 0.5), (5.4, 1.7), (5.7, 1.2), (5.7, 0.4)]
    for side in (1, -1):
        outline = [(x * side, z) for x, z in wing]
        if side < 0:
            outline.reverse()
        P(geo.bevel(geo.slab("Wing", outline, 0.32, "HULL", taper=0.45,
                             matrix=geo.at((0, -0.35, 0), geo.rot(z=-6 * side))), width=0.03))
        # leading-edge armour strip
        lead = [(1.2 * side, 0.45), (5.35 * side, 1.62), (5.35 * side, 1.4), (1.2 * side, 0.15)]
        if side < 0:
            lead.reverse()
        P(geo.bevel(geo.slab("Lead", lead, 0.36, "ACCENT", taper=0.5,
                             matrix=geo.at((0, -0.35, 0.02), geo.rot(z=-6 * side))), width=0.02))
        # wingtip blade (vertical), red
        blade = [(-1.3, -0.6), (1.5, 0.9), (1.6, 2.7), (0.3, 3.2), (-0.9, 1.4)]
        P(geo.bevel(geo.slab("Blade", blade, 0.2, "ACCENT", taper=0.6,
                             matrix=geo.at((5.75 * side, -1.3, -0.4), geo.rot(z=90))), width=0.025))
        # wingtip cannon with a muzzle
        P(geo.bevel(geo.cylinder("Gun", (5.75 * side, -1.3, 0.2), (5.75 * side, -1.3, 3.2), 0.16, 0.12, "METAL", sides=10), width=0.01, segments=1))
        P(geo.tube("Muzzle", (5.75 * side, -1.3, 3.2), (5.75 * side, -1.3, 3.65), 0.13, 0.07, "DARK", sides=10))
        # mandible: an armoured prong reaching past the nose, red tip
        mand = [(0.95, 0.4), (1.55, 1.0), (1.4, 4.2), (1.05, 4.7), (0.8, 2.2)]
        if side < 0:
            mand = [(-x, z) for x, z in reversed(mand)]
        P(geo.bevel(geo.slab("Mandible", mand, 0.34, "HULL", taper=0.55,
                             matrix=geo.at((0, -0.4, 0), geo.rot(z=-6 * side))), width=0.03))
        P(geo.bevel(geo.box("MandibleTip", (1.22 * side, -0.48, 4.25), (0.32, 0.38, 0.7), "ACCENT", geo.rot(y=-8 * side)), width=0.03))
        # wing root intake
        P(geo.bevel(geo.box("Intake", (1.25 * side, -0.45, 1.2), (0.55, 0.62, 1.9), "DARK", geo.rot(y=6 * side)), width=0.04))
        # engine nacelle and nozzle
        x = 0.72 * side
        P(geo.bevel(geo.cylinder("Nacelle", (x, 0.0, -2.4), (x, 0.0, -4.55), 0.7, 0.64, "METAL", sides=14), width=0.02, segments=1))
        P(geo.tube("Nozzle", (x, 0.0, -4.5), (x, 0.0, -5.1), 0.63, 0.48, "DARK", sides=14, flare=1.08))
        P(geo.disc("EngineCore", (x, 0.0, -4.62), (0, 0, -1), 0.48, "ENGINE", sides=14))
        # running light
        P(geo.disc("Light", (5.75 * side, 1.85, 2.3), (0, 1, 0), 0.08, "LIGHT", sides=8))

    # raised armour plates along the fuselage sides and top
    for side in (1, -1):
        for z0, z1, y in ((-3.0, -1.0, 0.25), (-0.7, 1.3, 0.2)):
            plate = [(0.0, z0), (0.0, z1), (0.55, z1 - 0.25), (0.55, z0 + 0.15)]
            P(geo.bevel(geo.slab("SidePlate", plate, 0.08, "PLATE",
                                 matrix=geo.at((1.22 * side, y, 0), geo.rot(z=90 + 8 * side) if side > 0 else geo.rot(z=-90 - 8 * side))), width=0.015, segments=1))
        # wing top armour and a flap step at the trailing edge
        top = [(1.6, -1.6), (1.6, 0.3), (4.3, 1.15), (4.4, 0.5)]
        if side < 0:
            top = [(-x, z) for x, z in reversed(top)]
        P(geo.bevel(geo.slab("WingPlate", top, 0.08, "PLATE", taper=0.6,
                             matrix=geo.at((0, -0.17, 0.02), geo.rot(z=-6 * side))), width=0.012, segments=1))
        flap = [(1.2, -2.25), (1.2, -1.75), (4.6, 0.0), (4.7, -0.4)]
        if side < 0:
            flap = [(-x, z) for x, z in reversed(flap)]
        P(geo.bevel(geo.slab("Flap", flap, 0.2, "METAL", taper=0.6,
                             matrix=geo.at((0, -0.42, -0.05), geo.rot(z=-6 * side))), width=0.012, segments=1))
        # antenna pair behind the canopy
        P(geo.bevel(geo.cylinder("Antenna", (0.32 * side, 1.05, -0.4), (0.4 * side, 1.75, -0.9), 0.025, 0.012, "METAL", sides=6), width=0.004, segments=1))
    # top hatch and gun turret blister under the nose
    P(geo.bevel(geo.box("Hatch", (0, 1.06, -2.6), (0.9, 0.08, 0.9), "PLATE"), width=0.02, segments=1))
    P(geo.bevel(geo.loft("ChinTurret", [
        (1.6, geo.superellipse(0.6, 0.4, count=10, power=2.2), (0, -0.95)),
        (2.4, geo.superellipse(0.55, 0.36, count=10, power=2.2), (0, -0.95)),
    ], "METAL"), width=0.02))
    P(geo.bevel(geo.cylinder("ChinGun", (0, -0.95, 2.3), (0, -0.95, 3.4), 0.07, 0.05, "DARK", sides=8), width=0.006, segments=1))

    # dorsal fin and spine
    fin = [(0.0, -4.0), (0.0, -1.2), (1.2, -2.3), (1.45, -3.8)]
    P(geo.bevel(geo.slab("Fin", fin, 0.16, "HULL", taper=0.6, matrix=geo.at((0, 0.85, 0), geo.rot(z=90))), width=0.02))
    P(geo.bevel(geo.box("Spine", (0, 1.0, -1.0), (0.35, 0.25, 3.2), "METAL"), width=0.03))
    # small sensor and vent blocks
    for x, z in ((0.55, -2.2), (-0.55, -2.2), (0.6, -0.6), (-0.6, -0.6)):
        P(geo.bevel(geo.box("Vent", (x, 0.95, z), (0.35, 0.12, 0.7), "DARK"), width=0.02, segments=1))
    P(geo.bevel(geo.cylinder("Sensor", (0, -0.62, 2.9), (0, -0.62, 3.7), 0.12, 0.06, "METAL", sides=8), width=0.01, segments=1))

    hull = geo.join(parts, NAME + "_LOD0")

    # additive engine plumes (not baked: drawn with the glow material)
    plumes = [geo.cylinder("Plume", (0.72 * s, 0, -4.7), (0.72 * s, 0, -6.6), 0.46, 0.06, "GLOW", sides=10, cap0=False, cap1=False)
              for s in (1, -1)]
    glow = geo.join(plumes, NAME + "_Glow")
    sockets = [geo.socket("GunR", (5.75, -1.3, 3.7)), geo.socket("GunL", (-5.75, -1.3, 3.7)),
               geo.socket("Engine", (0, 0, -5.1)), geo.socket("Nose", (0, -0.2, 4.7))]
    return hull, glow, sockets


def main(preview=True):
    hull, glow, sockets = build()
    bake.unwrap(hull)
    paths = bake.bake(hull, NAME, TEX, export.TEXTURES)
    lod1 = geo.decimate(hull, 0.3, NAME + "_LOD1")
    lod2 = geo.decimate(hull, 0.08, NAME + "_LOD2")
    export.fbx([hull, lod1, lod2, glow] + sockets, NAME)
    tris = (geo.triangles(hull), geo.triangles(lod1), geo.triangles(lod2))
    print(f"{NAME}: LOD0 {tris[0]} / LOD1 {tris[1]} / LOD2 {tris[2]} triangles")
    if preview:
        mat = bake.textured_material(NAME + "_Preview", paths)
        export.preview(hull, NAME, mat, radius=6.0, notes=f"{NAME}  {tris[0]} / {tris[1]} / {tris[2]} tri  tex {TEX}")
    return tris


if __name__ == "__main__":
    main()
