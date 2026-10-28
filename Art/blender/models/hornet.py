"""
Hornet: the enemy team's fighter, a replica of the owner's reference ship (Photo 1, the T-65 X-wing),
built from the blueprint at the-blueprints.com (top, side, front and rear views, 1085 x 1305 px;
reference only, not in the repo). Private project: see CLAUDE.md. Unity frame: z forward, y up,
x right, origin at blueprint top-view x = 400 (the middle of the ship's length).

Measurements (0.02042 m/px for a 12.5 m length):
- nose tip z 6.21, nose cone from z 4.39 (0.84 m wide); fuselage 1.51 m wide at z 0, 1.80 m at the
  wing roots (z -2.04), rear body to z -5.31,
- canopy from z -1.6 (1.12 m high) to z 0.95, droid dome at z -1.85,
- S-foils open at 17.5 degrees, leading edge straight at z -2.61, trailing edge z -5.25 (root) to -4.29
  (tip), cannons 5.45 m out and 1.83 m up/down,
- engines 1.65 m out and 0.86 m up/down, z -6.29 (exhaust) to -2.04 (intake),
- wingtip cannons: housing z -4.7..-2.04, barrel to z 1.0, flash suppressor, laser tip at z 2.04.
build("elite") is the Trident: the same ship darker, gold markings, armour on the engines.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401

from lib import geo, pipeline, surface  # noqa: E402

NAME = "Hornet"
TEX = 1024

FOIL = 17.5
PIVOT_Y = 0.12
WING_ROOT, WING_TIP = 0.8, 5.7          # along the foil plane from the ship's axis
LEAD_Z, TRAIL_ROOT_Z, TRAIL_TIP_Z = -2.61, -5.25, -4.29
ENGINE_X, ENGINE_Y = 1.65, 0.86

# fuselage sections: (z, width, top, bottom)
FUSELAGE = ((-5.31, 1.78, 0.72, -0.78), (-2.04, 1.8, 0.8, -0.8), (0.0, 1.51, 0.62, -0.55), (2.04, 1.14, 0.38, -0.48),
            (4.39, 0.84, 0.28, -0.45), (5.4, 0.62, 0.15, -0.36), (6.0, 0.3, 0.02, -0.25), (6.21, 0.06, -0.08, -0.12))


def palette(elite):
    hull = (0.11, 0.11, 0.12) if elite else (0.56, 0.55, 0.52)
    grey = (0.06, 0.06, 0.065) if elite else (0.36, 0.36, 0.37)
    mark = (0.55, 0.4, 0.08) if elite else (0.6, 0.035, 0.025)
    return {
        "HULL": dict(base=hull, metal=0.25, rough=0.5, panel_scale=2.0, panel_dark=0.55, wear=0.45, grime=0.5, ao_dist=0.5),
        "GREY": dict(base=grey, metal=0.35, rough=0.48, panel_scale=2.6, wear=0.4, grime=0.5, ao_dist=0.4),
        "MARK": dict(base=mark, metal=0.2, rough=0.5, panel=0.2, panel_scale=2.0, wear=0.2, grime=0.2),
        "YELLOW": dict(base=(0.6, 0.45, 0.06) if not elite else (0.02, 0.02, 0.02), metal=0.2, rough=0.5, panel=0.0, wear=0.5),
        "METAL": dict(base=(0.32, 0.32, 0.34), metal=0.95, rough=0.28, panel=0.0, wear=0.3),
        "DARK": dict(base=(0.03, 0.03, 0.035), metal=0.6, rough=0.55, panel=0.0, wear=0.15),
        "GLASS": dict(base=(0.02, 0.03, 0.035), metal=0.0, rough=0.06, panel=0.0, wear=0.0, grime=0.2, bump=0.0),
        "DROID": dict(base=(0.08, 0.14, 0.45) if not elite else (0.35, 0.05, 0.04), metal=0.6, rough=0.35, panel=0.0, wear=0.3),
        "ENGINE": dict(base=(0.2, 0.04, 0.02), emit=1.0, emit_color=(1.0, 0.3, 0.12), panel=0.0, wear=0.0, grime=0.0),
    }


def section(w, top, bottom):
    h = max(top - bottom, 0.02)
    return geo.superellipse(w, h, count=16, power=3.0, top=0.8), (0.0, (top + bottom) / 2)


def foil_point(side, sign, u, z):
    """A point on a foil's mid plane: u along the foil from the ship's axis, z along the ship."""
    a = math.radians(FOIL)
    return (side * u * math.cos(a), sign * (PIVOT_Y + u * math.sin(a)), z)


def foil(side, sign, elite, parts, sockets, index):
    P = parts.append
    angle = FOIL * sign * side
    matrix = geo.at((0.0, sign * PIVOT_Y, 0.0), geo.rot(z=angle))
    outline = [(side * WING_ROOT, LEAD_Z), (side * WING_TIP, LEAD_Z), (side * WING_TIP, TRAIL_TIP_Z), (side * WING_ROOT, TRAIL_ROOT_Z)]
    P(geo.bevel(geo.slab("Foil", outline, 0.17, "HULL", taper=0.55, matrix=matrix), width=0.02, segments=1, angle=30))
    # grey inner panel and the coloured marking (red on the upper foils, a yellow band on one lower foil)
    inner = [(side * 1.5, LEAD_Z - 0.25), (side * 2.6, LEAD_Z - 0.25), (side * 2.6, -4.6), (side * 1.5, -4.75)]
    P(geo.slab("FoilPanel", inner, 0.19, "GREY", taper=0.8, matrix=matrix))
    if sign > 0:
        P(geo.slab("FoilMark", [(side * 3.5, -2.9), (side * 4.3, -2.9), (side * 4.3, -3.75), (side * 3.5, -3.75)], 0.185, "MARK", matrix=matrix))
    elif side < 0:
        P(geo.slab("FoilYellow", [(side * 3.1, -3.0), (side * 3.7, -3.0), (side * 3.7, -3.6), (side * 3.1, -3.6)], 0.185, "YELLOW", matrix=matrix))
    # the cannon on the tip: housing with a rear knob, long barrel, flash suppressor with fins, laser tip
    x, y, _ = foil_point(side, sign, WING_TIP + 0.05, 0.0)
    P(geo.bevel(geo.cylinder("CannonHousing", (x, y, -4.7), (x, y, -2.04), 0.17, 0.15, "HULL", sides=12), width=0.02, segments=1))
    P(geo.bevel(geo.cylinder("CannonKnob", (x, y, -4.98), (x, y, -4.68), 0.12, 0.13, "GREY", sides=10), width=0.012, segments=1))
    for zr in (-4.1, -2.6):
        P(geo.cylinder("CannonRing", (x, y, zr), (x, y, zr + 0.08), 0.185, 0.185, "GREY", sides=12))
    P(geo.cylinder("Barrel", (x, y, -2.1), (x, y, 1.05), 0.065, 0.06, "METAL", sides=8))
    P(geo.cylinder("Suppressor", (x, y, 1.0), (x, y, 1.35), 0.09, 0.08, "METAL", sides=8))
    for k in range(3):
        a = math.radians(90 + 120 * k)
        fin = (x + math.cos(a) * 0.11, y + math.sin(a) * 0.11, 1.18)
        P(geo.box("SuppressorFin", fin, (0.03 if abs(math.cos(a)) < 0.5 else 0.16, 0.16 if abs(math.cos(a)) < 0.5 else 0.03, 0.32), "METAL"))
    P(geo.cylinder("LaserTip", (x, y, 1.35), (x, y, 2.04), 0.03, 0.022, "METAL", sides=6))
    sockets.append(geo.socket(f"Gun{index}", (x, y, 2.08)))
    # the engine on the foil root: intake with vanes, ribbed body and exhaust, glowing core
    ex, ey = ENGINE_X * side, ENGINE_Y * sign
    P(geo.tube("Intake", (ex, ey, -2.3), (ex, ey, -1.98), 0.56, 0.42, "GREY", sides=18))
    P(geo.disc("IntakeFan", (ex, ey, -2.2), (0.0, 0.0, 1.0), 0.43, "DARK", sides=18))
    for k in range(3):
        a = math.radians(90 + 120 * k)
        P(geo.box("IntakeVane", (ex + math.cos(a) * 0.2, ey + math.sin(a) * 0.2, -2.15),
                  (0.06 if abs(math.cos(a)) < 0.5 else 0.42, 0.42 if abs(math.cos(a)) < 0.5 else 0.06, 0.06), "GREY"))
    P(geo.bevel(geo.cylinder("EngineBody", (ex, ey, -5.4), (ex, ey, -2.28), 0.45, 0.47, "HULL", sides=18), width=0.02, segments=1))
    for zr in (-4.6, -3.7, -2.9):
        P(geo.cylinder("EngineRib", (ex, ey, zr), (ex, ey, zr + 0.1), 0.49, 0.49, "GREY", sides=18))
    P(geo.cylinder("Exhaust", (ex, ey, -6.25), (ex, ey, -5.38), 0.5, 0.5, "DARK", sides=18))
    for k in range(5):
        zr = -6.2 + k * 0.17
        P(geo.cylinder("ExhaustRib", (ex, ey, zr), (ex, ey, zr + 0.07), 0.54, 0.54, "METAL", sides=18))
    P(geo.tube("Nozzle", (ex, ey, -6.35), (ex, ey, -6.2), 0.5, 0.36, "METAL", sides=18))
    P(geo.disc("EngineCore", (ex, ey, -6.24), (0.0, 0.0, -1.0), 0.37, "ENGINE", sides=18))
    if elite:
        P(geo.bevel(geo.box("EngineArmor", (ex + side * 0.42, ey, -3.9), (0.12, 0.6, 2.6), "GREY"), width=0.02, segments=1))


def build(variant="standard"):
    elite = variant == "elite"
    geo.reset()
    surface.make_all(palette(elite))
    parts = []
    P = parts.append
    sockets = []

    # fuselage: a long wedge from the rear body to the nose cone (the cone is the lighter part)
    secs = []
    for z, w, top, bottom in FUSELAGE:
        prof, offset = section(w, top, bottom)
        secs.append((z, prof, offset))
    body = [s for s in secs if s[0] <= 4.39]
    cone = [s for s in secs if s[0] >= 4.39]
    P(geo.bevel(geo.loft("Fuselage", body, "HULL"), width=0.03, segments=1, angle=30))
    P(geo.bevel(geo.loft("NoseCone", cone, "GREY"), width=0.02, segments=1, angle=30))
    P(geo.box("SensorWindow", (0.0, 0.2, 4.95), (0.18, 0.02, 0.45), "DARK"))
    # the red stripe along both sides of the nose, at mid height
    for side in (1, -1):
        path = []
        for z, w, top, bottom in FUSELAGE[1:5]:
            path.append((side * (w / 2 + 0.006), (top + bottom) / 2 + 0.06, z))
        path[0] = (path[0][0], path[0][1], -0.9)
        P(geo.sweep("NoseStripe", path, geo.rect(0.02, 0.32), "MARK"))
        # proton torpedo launcher slot low on the side
        P(geo.box("TorpedoSlot", (side * 0.62, -0.28, 1.5), (0.04, 0.12, 1.8), "DARK"))
    # canopy with frame bars, then the droid behind it
    canopy = [(-1.6, 0.92, 1.12), (-0.7, 1.0, 1.08), (0.2, 0.84, 0.88), (0.95, 0.6, 0.66)]
    csecs = [(z, geo.superellipse(w, (top - 0.55) * 2, count=12, power=2.6), (0.0, 0.55)) for z, w, top in canopy]
    P(geo.bevel(geo.loft("Canopy", csecs, "GLASS"), width=0.015, segments=1, angle=30))
    for z, w, top in canopy[:3]:
        arch = [(w / 2 * math.cos(math.radians(a)), 0.55 + (top - 0.55) * math.sin(math.radians(a)) + 0.012, z) for a in range(0, 181, 15)]
        P(geo.sweep("CanopyBar", arch, geo.rect(0.05, 0.05), "GREY", up=(0.0, 0.0, 1.0)))
    P(geo.sweep("CanopySpine", [(0.0, top + 0.015, z) for z, w, top in canopy], geo.rect(0.05, 0.04), "GREY"))
    P(geo.bevel(geo.cylinder("DroidBody", (0.0, 0.7, -1.95), (0.0, 0.98, -1.95), 0.27, 0.27, "METAL", sides=16), width=0.01, segments=1))
    P(geo.cylinder("DroidDome", (0.0, 0.98, -1.95), (0.0, 1.22, -1.95), 0.27, 0.08, "METAL", sides=16))
    P(geo.box("DroidPanel", (0.0, 1.06, -1.7), (0.18, 0.08, 0.04), "DROID"))
    P(geo.disc("DroidEye", (0.0, 1.12, -1.71), (0.0, 0.3, 1.0), 0.05, "DARK", sides=8))
    # rear body: S-foil actuators top and bottom, panels, the rear plate
    for sign in (1, -1):
        P(geo.bevel(geo.box("Actuator", (0.0, sign * 0.82, -3.9), (0.7, 0.18, 2.4), "GREY"), width=0.03, segments=1))
        for k in range(4):
            P(geo.box("RearGreeble", ((-0.45 + 0.3 * k), sign * 0.75, -5.0 + 0.25 * (k % 2)), (0.16, 0.08, 0.35), "METAL"))
    for side in (1, -1):
        P(geo.box("RearSidePanel", (side * 0.9, 0.0, -3.7), (0.04, 0.9, 2.6), "GREY"))
    P(geo.box("RearPlate", (0.0, 0.0, -5.33), (1.5, 1.3, 0.05), "GREY"))

    index = 0
    for side in (1, -1):
        for sign in (1, -1):
            foil(side, sign, elite, parts, sockets, index)
            index += 1

    hull = geo.join(parts, NAME + "_LOD0")
    plumes = [geo.cylinder("Plume", (ENGINE_X * sx, ENGINE_Y * sy, -6.35), (ENGINE_X * sx, ENGINE_Y * sy, -8.2), 0.4, 0.05, "GLOW",
                           sides=12, cap0=False, cap1=False) for sx in (1, -1) for sy in (1, -1)]
    glow = geo.join(plumes, NAME + "_Glow")
    sockets += [geo.socket("Nose", (0.0, -0.1, 6.21)), geo.socket("Engine", (0.0, 0.0, -6.35))]
    return hull, glow, sockets


VIEWS = (("as Photo 1", (1.9, 1.0, 1.4)), ("side", (3.6, 0.2, 0.0)), ("front", (0.0, 0.2, 3.6)))


def make(name, variant, preview=True):
    hull, glow, sockets = build(variant)
    hull.name, glow.name = f"{name}_LOD0", f"{name}_Glow"
    return pipeline.finish(name, hull, [glow] + sockets, TEX, radius=7.0, lods=(0.32, 0.1), preview=preview, ao_distance=1.0, views=VIEWS)


def main(preview=True):
    return make(NAME, "standard", preview)


if __name__ == "__main__":
    main()
