"""
The player's team fighter: a replica of the owner's reference ship (Photo 2, the TIE Interceptor),
built from the blueprint at the-blueprints.com (front and side views, 1085 x 903 px; reference only,
not in the repo). Private project: see CLAUDE.md. Unity frame: z forward, y up, x right, origin at
the centre of the cockpit ball.

Measurements (blueprint px -> metres; side view 0.01886 m/px for a 9.6 m length, front view
0.01447 m/px for the same 7.15 m height):
- ball radius 1.45, front window radius 0.92 with 8 spokes to a hub ring of 0.33,
- each wing bends at 4.2 m out, 1.08 m above/below the centre line; the blades run in to 2.32 m out
  at 3.55 m high (front view),
- side outline of a blade: dagger tip at z 6.51 on the bend line, top edge from z -0.09 to -1.55,
  rear corner at z -2.72 on the bend line; the vertical spine between the bends runs z -2.72..1.6,
- wing cannons on the bend lines, muzzles at z 6.8; the pylons meet the spine at 4.15 m.
Variants (classes of the same ship, small differences): sokol (exactly Photo 2), jastrzab (longer,
narrower blades, green trim), zubr (armour plates, heavier pylons, bomb pods, amber trim),
swallow (the wingmen: plain).
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401  (bpy first: it provides bmesh and mathutils)
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

from lib import geo, pipeline, surface  # noqa: E402

TEX = 1024

BALL_R = 1.45
WINDOW_R = 0.92
HUB_R = 0.33
BEND_X, BEND_Y = 4.2, 1.08
TOP_Y = 3.55
Z_TOP_FRONT, Z_TOP_REAR, Z_REAR = -0.09, -1.55, -2.72
Z_SPINE_FRONT = 1.6
PYLON_END = 4.15
MUZZLE_Z = 6.8

VARIANTS = {
    "sokol": dict(top_x=2.32, z_tip=6.51, trim=None, armor=False, bombs=False, pylon=1.0),
    "swallow": dict(top_x=2.32, z_tip=6.51, trim=None, armor=False, bombs=False, pylon=1.0),
    "jastrzab": dict(top_x=2.62, z_tip=7.25, trim=(0.08, 0.38, 0.12), armor=False, bombs=False, pylon=0.9),
    "zubr": dict(top_x=2.32, z_tip=6.3, trim=(0.55, 0.32, 0.04), armor=True, bombs=True, pylon=1.3),
}


def palette(trim):
    p = {
        "HULL": dict(base=(0.33, 0.34, 0.36), metal=0.35, rough=0.45, panel_scale=2.2, wear=0.25, grime=0.35, ao_dist=0.5),
        "FRAME": dict(base=(0.4, 0.41, 0.43), metal=0.4, rough=0.42, panel=0.4, panel_scale=3.0, wear=0.35, ao_dist=0.3),
        "PANEL": dict(base=(0.075, 0.08, 0.09), metal=0.5, rough=0.32, panel=1.0, panel_scale=7.0, panel_dark=0.5, mortar=0.004,
                      wear=0.1, grime=0.2, ao_dist=0.3),
        "METAL": dict(base=(0.2, 0.2, 0.21), metal=0.9, rough=0.32, panel=0.0, wear=0.25),
        "DARK": dict(base=(0.025, 0.026, 0.03), metal=0.6, rough=0.55, panel=0.0, wear=0.15),
        "GLASS": dict(base=(0.006, 0.008, 0.012), metal=0.0, rough=0.05, panel=0.0, wear=0.0, grime=0.1, bump=0.0),
        "ENGINE": dict(base=(0.2, 0.05, 0.02), emit=1.0, emit_color=(1.0, 0.32, 0.16), panel=0.0, wear=0.0, grime=0.0),
    }
    if trim:
        p["TRIM"] = dict(base=trim, metal=0.3, rough=0.45, panel=0.0, wear=0.3)
    return p


def sphere_pt(r, theta, phi):
    """A point on a sphere around the origin: theta from straight ahead (+z), phi around it (0 = right)."""
    t, p = math.radians(theta), math.radians(phi)
    return Vector((math.sin(t) * math.cos(p), math.sin(t) * math.sin(p), math.cos(t))) * r


def ball(name, r, slot, theta_max=180.0, rings=16, segs=32):
    """A sphere (or a front cap up to theta_max) around the origin."""
    bm = bmesh.new()
    grid = []
    for k in range(1, rings + 1):
        theta = theta_max * k / rings
        grid.append([bm.verts.new(sphere_pt(r, theta, 360.0 * s / segs)) for s in range(segs)])
    front = bm.verts.new(sphere_pt(r, 0.0, 0.0))
    for s in range(segs):
        bm.faces.new((front, grid[0][s], grid[0][(s + 1) % segs]))
    for a, b in zip(grid, grid[1:]):
        for s in range(segs):
            n = (s + 1) % segs
            bm.faces.new((a[s], b[s], b[n], a[n]))
    if theta_max >= 179.9:
        back = bm.verts.new(sphere_pt(r, 180.0, 0.0))
        for s in range(segs):
            bm.faces.new((grid[-1][(s + 1) % segs], grid[-1][s], back))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return geo.from_bmesh(name, bm, slot)


def outward(p):
    return Vector(p).normalized()


def on_ball(name, theta, phi, size, slot, lift=0.0):
    """A box lying on the ball's surface (local z = outward)."""
    p = sphere_pt(BALL_R + size[2] / 2 + lift, theta, phi)
    up = (0.0, 1.0, 0.0) if abs(outward(p).y) < 0.9 else (0.0, 0.0, 1.0)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    return geo.from_bmesh(name, bm, slot, geo.look_matrix(p, outward(p), up))


def wing_blade(side, sign, v, parts):
    """
    One dagger blade: a flat panel in the plane through the bend line and the blade's top edge (both
    along z), dark solar cells inside a raised light frame, two braces, two notches in the leading edge.
    side: +1 right / -1 left; sign: +1 upper / -1 lower blade.
    """
    P = parts.append
    bend = Vector((BEND_X * side, BEND_Y * sign, 0.0))
    top = Vector((v["top_x"] * side, TOP_Y * sign, 0.0))
    d = top - bend
    length = d.length
    dh = d / length
    normal = Vector((-dh.y, dh.x, 0.0))
    z_tip = v["z_tip"]
    # outline (u along the blade from the bend, z along the ship), leading edge with two notches
    a, b = Vector((0.0, z_tip)), Vector((length, Z_TOP_FRONT))
    e = (b - a).normalized()
    inward = Vector((-e.y, e.x)) if Vector((-e.y, e.x)).dot(Vector((length * 0.5, -1.0)) - a) > 0 else Vector((e.y, -e.x))
    edge = (b - a).length
    outline = [(a.x, a.y)]
    for t in (0.34, 0.56):
        for k, (dt, depth) in enumerate(((-0.025, 0.0), (-0.025, 0.22), (0.025, 0.22), (0.025, 0.0))):
            q = a + e * edge * (t + dt) + inward * depth
            outline.append((q.x, q.y))
    outline += [(b.x, b.y), (length, Z_TOP_REAR), (0.0, Z_REAR)]

    def world(u, z):
        p = bend + dh * u
        return (p.x, p.y, z)

    angle = math.degrees(math.atan2(d.y, d.x))
    matrix = geo.at((bend.x, bend.y, 0.0), geo.rot(z=angle))
    P(geo.slab("Solar", outline, 0.07, "PANEL", matrix=matrix))
    frame_path = [world(u, z) for u, z in outline]
    P(geo.bevel(geo.sweep("BladeFrame", frame_path, geo.rect(0.16, 0.17), "FRAME", up=normal, closed_path=True), width=0.02, segments=1, angle=30))
    # braces across the cells (the "wing brace" lines of the blueprint)
    for zb in (0.38, -1.15):
        u_end = length if zb <= Z_TOP_FRONT else length * (z_tip - zb) / (z_tip - Z_TOP_FRONT)
        P(geo.sweep("Brace", [world(0.05, zb), world(u_end - 0.05, zb)], geo.rect(0.1, 0.13), "FRAME", up=normal))
    # a ridge along the bend line, where the blade meets the spine and the cannon
    P(geo.sweep("BendRidge", [world(0.0, z_tip - 0.2), world(0.0, Z_REAR + 0.05)], geo.rect(0.2, 0.2), "FRAME", up=normal))


def spine(side, v, parts):
    """The vertical strip between the upper and lower bends at the wing's outer edge."""
    P = parts.append
    # bowed outward: from the bends (4.2 m out) to 4.5 m at the middle (blueprint front view)
    x_bend, x_mid = (BEND_X + 0.06) * side, (BEND_X + 0.3) * side
    for sign in (1, -1):
        a, b = Vector((x_bend, BEND_Y * sign, 0.0)), Vector((x_mid, 0.0, 0.0))
        d = b - a
        length = d.length
        z_front = Z_SPINE_FRONT if sign > 0 else Z_SPINE_FRONT - 0.35
        outline = [(0.0, Z_REAR), (length, Z_REAR), (length, Z_SPINE_FRONT - 0.17), (0.0, z_front)]
        angle = math.degrees(math.atan2(d.y, d.x))
        P(geo.slab("Spine", outline, 0.1, "PANEL", matrix=geo.at(a, geo.rot(z=angle))))
        dh = d / length
        normal = Vector((-dh.y, dh.x, 0.0))
        frame = [tuple(a + dh * u + Vector((0.0, 0.0, z))) for u, z in outline]
        P(geo.bevel(geo.sweep("SpineFrame", frame, geo.rect(0.14, 0.2), "FRAME", up=normal, closed_path=True), width=0.02, segments=1, angle=30))
    if v["trim"]:
        P(geo.box("SpineTrim", (x_mid + 0.1 * side, 0.0, -0.55), (0.03, 0.5, 2.6), "TRIM"))


def cannon(side, sign, parts, sockets, index):
    P = parts.append
    x, y = (BEND_X + 0.16) * side, BEND_Y * sign
    P(geo.bevel(geo.cylinder("CannonHousing", (x, y, 4.1), (x, y, 5.25), 0.12, 0.1, "METAL", sides=10), width=0.012, segments=1))
    P(geo.cylinder("CannonBarrel", (x, y, 5.2), (x, y, MUZZLE_Z - 0.18), 0.055, 0.05, "DARK", sides=8))
    P(geo.tube("Muzzle", (x, y, MUZZLE_Z - 0.2), (x, y, MUZZLE_Z), 0.075, 0.04, "METAL", sides=8))
    sockets.append(geo.socket(f"Gun{index}", (x, y, MUZZLE_Z + 0.05)))


def pylon(side, v, parts):
    P = parts.append
    k = v["pylon"]
    prof = lambda depth, h: geo.superellipse(depth, h, count=12, power=4.0)
    # lofted along local z (= world x after the turn), profile (depth along world z, height)
    sections = [(BALL_R - 0.35, prof(1.25 * k, 0.62 * k)), (2.4, prof(1.0 * k, 0.5 * k)), (PYLON_END, prof(0.85 * k, 0.44 * k))]
    turn = geo.rot(y=90.0 * side)
    P(geo.bevel(geo.loft("Pylon", sections, "HULL", matrix=turn), width=0.03, segments=1, angle=30))
    # the trapezoid brace frame near the spine and the targeting sensor pod
    for sgn in (1, -1):
        P(geo.sweep("PylonBrace", [(2.95 * side, 0.24 * sgn, 0.0), (PYLON_END * side, (BEND_Y - 0.1) * sgn, 0.0)], geo.rect(0.11, 0.14), "FRAME", up=(0.0, 0.0, 1.0)))
    P(geo.bevel(geo.box("SensorPod", (3.35 * side, 0.0, 0.85), (0.32, 0.3, 1.5), "METAL"), width=0.03, segments=1))
    P(geo.disc("SensorLens", (3.35 * side, 0.0, 1.61), (0.0, 0.0, 1.0), 0.1, "GLASS", sides=10))
    if v["armor"]:
        P(geo.bevel(geo.box("PylonArmor", (2.5 * side, 0.0, 0.0), (1.6, 0.66 * k + 0.08, 1.15 * k + 0.08), "HULL"), width=0.05, segments=1))
        P(geo.box("PylonArmorTrim", (2.5 * side, 0.36 * k + 0.05, 0.0), (1.4, 0.02, 0.9), "TRIM"))


def build(variant="sokol"):
    v = VARIANTS[variant]
    geo.reset()
    surface.make_all(palette(v["trim"]))
    parts = []
    P = parts.append
    sockets = []

    # the cockpit ball, the round window with eight spokes to a hub, a ring of panel ribs around it
    P(ball("Ball", BALL_R, "HULL", segs=32, rings=18))
    window_theta = math.degrees(math.asin(WINDOW_R / BALL_R))
    hub_theta = math.degrees(math.asin(HUB_R / BALL_R))
    P(ball("Window", BALL_R + 0.006, "GLASS", theta_max=window_theta, rings=6, segs=32))
    out = lambda p: outward(p)
    ring = lambda theta, r: [sphere_pt(r, theta, a) for a in range(0, 360, 10)]
    P(geo.bevel(geo.sweep("WindowRing", ring(window_theta, BALL_R + 0.05), geo.rect(0.14, 0.12), "FRAME", up=out, closed_path=True), width=0.015, segments=1, angle=30))
    P(geo.sweep("HubRing", ring(hub_theta, BALL_R + 0.04), geo.rect(0.08, 0.1), "FRAME", up=out, closed_path=True))
    for i in range(8):
        phi = 22.5 + 45.0 * i
        spoke = [sphere_pt(BALL_R + 0.035, hub_theta + (window_theta - hub_theta) * t / 6.0, phi) for t in range(7)]
        P(geo.sweep("Spoke", spoke, geo.rect(0.07, 0.08), "FRAME", up=out))
        rib = [sphere_pt(BALL_R + 0.02, window_theta + (100.0 - window_theta) * t / 8.0, phi) for t in range(9)]
        P(geo.sweep("BallRib", rib, geo.rect(0.06, 0.05), "FRAME", up=out))
    # rear ring where the ball meets the engine section, the hatch on top, two chin blasters
    P(geo.sweep("RearRing", ring(115.0, BALL_R + 0.03), geo.rect(0.1, 0.07), "FRAME", up=out, closed_path=True))
    P(geo.bevel(geo.cylinder("Hatch", (0.0, BALL_R - 0.1, -0.15), (0.0, BALL_R + 0.1, -0.15), 0.46, 0.44, "HULL", sides=16), width=0.02, segments=1))
    P(geo.bevel(geo.cylinder("HatchTop", (0.0, BALL_R + 0.08, -0.15), (0.0, BALL_R + 0.17, -0.15), 0.3, 0.26, "METAL", sides=12), width=0.015, segments=1))
    for sx in (1, -1):
        P(geo.cylinder("ChinGun", (0.32 * sx, -1.18, 0.75), (0.32 * sx, -1.18, 1.45), 0.06, 0.05, "DARK", sides=8))
    # the engine section behind the ball: an octagonal block with twin ion vents
    octo = lambda r: geo.polygon(r * 2, r * 2, 8, 22.5)
    P(geo.bevel(geo.loft("EngineBlock", [(-0.95, octo(1.05)), (-1.7, octo(0.98)), (-1.95, octo(0.8))], "HULL"), width=0.03, segments=1, angle=30))
    for sx in (1, -1):
        P(geo.box("Vent", (0.3 * sx, 0.0, -1.97), (0.36, 0.55, 0.06), "DARK"))
        P(geo.box("VentGlow", (0.3 * sx, 0.0, -1.99), (0.28, 0.46, 0.03), "ENGINE"))
    sockets.append(geo.socket("Engine", (0.0, 0.0, -2.0)))
    sockets.append(geo.socket("Nose", (0.0, 0.0, BALL_R)))

    if v["armor"]:
        for theta in (72.0, 108.0):
            for phi in (40.0, 140.0, 220.0, 320.0):
                P(geo.bevel(on_ball("BallArmor", theta, phi, (0.75, 0.55, 0.07), "HULL"), width=0.02, segments=1))
        P(geo.box("BallTrim", (0.0, BALL_R + 0.02, 0.45), (0.5, 0.03, 0.5), "TRIM"))
    if v["bombs"]:
        for sx in (1, -1):
            x = 0.55 * sx
            P(geo.bevel(geo.cylinder("BombPod", (x, -1.32, -0.7), (x, -1.32, 0.9), 0.24, 0.24, "METAL", sides=12), width=0.02, segments=1))
            P(geo.cylinder("BombNose", (x, -1.32, 0.9), (x, -1.32, 1.25), 0.24, 0.05, "DARK", sides=12))
            P(geo.box("BombRack", (x * 0.8, -1.08, 0.1), (0.12, 0.3, 1.0), "DARK"))
    if v["trim"] and not v["armor"]:
        P(geo.sweep("TrimRing", ring(window_theta + 3.0, BALL_R + 0.025), geo.rect(0.05, 0.03), "TRIM", up=out, closed_path=True))

    gun = 0
    for side in (1, -1):
        pylon(side, v, parts)
        spine(side, v, parts)
        for sign in (1, -1):
            wing_blade(side, sign, v, parts)
            cannon(side, sign, parts, sockets, gun)
            gun += 1

    hull = geo.join(parts, "Interceptor_LOD0")
    plumes = [geo.cylinder("Plume", (0.3 * sx, 0.0, -2.0), (0.3 * sx, 0.0, -3.3), 0.2, 0.03, "GLOW", sides=10, cap0=False, cap1=False)
              for sx in (1, -1)]
    glow = geo.join(plumes, "Interceptor_Glow")
    return hull, glow, sockets


def make(name, variant, preview=True):
    hull, glow, sockets = build(variant)
    hull.name, glow.name = f"{name}_LOD0", f"{name}_Glow"
    hull.data.name = hull.name
    views = (("as Photo 2", (1.7, 1.15, 2.4)), ("side", (3.6, 0.25, 0.0)), ("front", (0.0, 0.15, 3.6)))
    return pipeline.finish(name, hull, [glow] + sockets, TEX, radius=5.2, lods=(0.32, 0.1), preview=preview, ao_distance=0.8, views=views)
