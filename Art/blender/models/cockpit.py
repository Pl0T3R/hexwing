"""
The player's cockpit: the inside of the Interceptor's ball (the team's ship, models/interceptor.py),
with its round front window, eight spokes and the small hub ring, in the layout of
Cockpit/CockpitFactory.cs (ship origin at the ball's centre, eye at (0, 0.12, -0.05), radius 1.05 m,
window edge 52 degrees from straight ahead). The code still places the five display glasses,
their texts and the moving stick and throttle; this model gives everything around them:
- the shell with meridian ribs and a shoulder ring, equipment boxes, cables,
- the window frame: the heavy round edge, eight spokes and the hub ring (clear glass inside it),
- the front console with bezels for the three displays, side consoles with switches and bezels,
- the seat with harness and headrest, the floor plate with pedals, the stick and throttle housings,
- light strips (emission tinted at runtime with the ship class's accent colour).
A second small model, CockpitControls, holds the stick and the throttle lever, each around its pivot.
Nothing comes closer than 30 cm to the eye.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402,F401  (bpy first: it provides bmesh and mathutils)
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

from lib import bake, export, geo, surface  # noqa: E402

NAME = "Cockpit"
TEX = 2048
R = 1.05
HALF = 52.0
EYE = Vector((0.0, 0.12, -0.05))

PALETTE = {
    "SHELL": dict(base=(0.15, 0.16, 0.18), metal=0.35, rough=0.55, panel_scale=2.4, wear=0.2, grime=0.3, ao_dist=0.2, panel_dark=0.5),
    "FRAME": dict(base=(0.045, 0.05, 0.055), metal=0.75, rough=0.38, panel=0.3, wear=0.35, ao_dist=0.2),
    "TRIM": dict(base=(0.22, 0.23, 0.25), metal=0.3, rough=0.5, panel_scale=3.2, wear=0.25, ao_dist=0.15),
    "DARK": dict(base=(0.025, 0.027, 0.03), metal=0.5, rough=0.6, panel=0.0, wear=0.15, ao_dist=0.15),
    "SEAT": dict(base=(0.06, 0.062, 0.068), metal=0.0, rough=0.88, panel=0.0, wear=0.05, grime=0.25, bump=0.2, ao_dist=0.15),
    "STRAP": dict(base=(0.42, 0.17, 0.03), metal=0.0, rough=0.8, panel=0.0, wear=0.0, grime=0.2, ao_dist=0.1),
    "METAL": dict(base=(0.32, 0.32, 0.34), metal=0.9, rough=0.3, panel=0.0, wear=0.2, ao_dist=0.15),
    "LIGHT": dict(base=(0.9, 0.95, 1.0), emit=1.0, emit_color=(0.92, 0.96, 1.0), panel=0.0, wear=0.0, grime=0.0),
    # coloured lamps and buttons: saturated, so the Hull shader keeps their colour (only white lights take the class tint)
    "L_RED": dict(base=(1.0, 0.08, 0.05), emit=1.0, emit_color=(1.0, 0.08, 0.05), panel=0.0, wear=0.0, grime=0.0),
    "L_GREEN": dict(base=(0.15, 1.0, 0.25), emit=1.0, emit_color=(0.15, 1.0, 0.25), panel=0.0, wear=0.0, grime=0.0),
    "L_BLUE": dict(base=(0.1, 0.45, 1.0), emit=1.0, emit_color=(0.1, 0.45, 1.0), panel=0.0, wear=0.0, grime=0.0),
    "L_YELLOW": dict(base=(1.0, 0.8, 0.1), emit=1.0, emit_color=(1.0, 0.8, 0.1), panel=0.0, wear=0.0, grime=0.0),
    "SCOPE": dict(base=(0.004, 0.012, 0.012), metal=0.0, rough=0.08, panel=0.0, wear=0.0, grime=0.05, bump=0.0),
}

RADAR = Vector((0.0, -0.02, 0.6))       # the scope's face on the pod above the centre display (the game draws the blips)
GAUGES = Vector((-0.27, -0.366, 0.455))  # the bar graph panel below the left display (the game draws the bars)
# the overhead display: a long strip above the window, 62 degrees up and 0.85 m from the eye, so it only shows
# when the pilot lifts the head (CockpitFactory places its glass and text at the same point)
OVERHEAD = EYE + Vector((0.0, math.sin(math.radians(62.0)), math.cos(math.radians(62.0)))) * 0.85
OVERHEAD_SIZE = (0.44, 0.075)


def sphere(r, theta, phi):
    """A point on the ball: theta from straight ahead (+z), phi around it (0 = right, 90 = up)."""
    t, p = math.radians(theta), math.radians(phi)
    return Vector((math.sin(t) * math.cos(p), math.sin(t) * math.sin(p), math.cos(t))) * r


HUB = 15.0                      # the hub ring's angle from straight ahead
SPOKES = [22.5 + 45.0 * i for i in range(8)]   # none straight up, down or sideways: the horizon stays clear


def window_edge(phi):
    """Angle from straight ahead to the round window edge (the same in every direction)."""
    return HALF


def inward(p):
    return -Vector(p).normalized()


def shell():
    sectors, rows = 36, 14
    bm = bmesh.new()
    grid = []
    for s in range(sectors):
        phi = s * 360.0 / sectors
        edge = window_edge(phi)
        grid.append([bm.verts.new(sphere(R, edge + (179.0 - edge) * k / rows, phi)) for k in range(rows + 1)])
    for s in range(sectors):
        n = (s + 1) % sectors
        for k in range(rows):
            bm.faces.new((grid[s][k], grid[n][k], grid[n][k + 1], grid[s][k + 1]))
    pole = bm.verts.new(sphere(R, 180.0, 0.0))
    for s in range(sectors):
        n = (s + 1) % sectors
        bm.faces.new((grid[s][rows], grid[n][rows], pole))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)   # faces look inward, at the pilot
    return geo.from_bmesh("Shell", bm, "SHELL")


def on_shell(name, theta, phi, size, depth_in, slot, spin=0.0):
    """A box sitting on the inside of the shell, its local z pointing inward."""
    p = sphere(R - depth_in, theta, phi)
    up = Vector((0.0, 1.0, 0.0)) if abs(sphere(1, theta, phi).y) < 0.9 else Vector((0.0, 0.0, 1.0))
    m = geo.look_matrix(p, inward(p), up) @ geo.rot(z=spin)
    return _box_m(name, size, slot, m)


def _box_m(name, size, slot, matrix):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    return geo.from_bmesh(name, bm, slot, matrix)


def arc(theta0, theta1, phi, r, n=16):
    return [sphere(r, theta0 + (theta1 - theta0) * i / n, phi) for i in range(n + 1)]


def bezel(name, center, size, depth, slot, eye=EYE, border=0.022, matrix=None):
    """
    A frame around a display glass (the glass itself is made in code at the same place), its front face just
    behind the glass. It faces the eye, or follows matrix (the display's frame: z away from the pilot).
    """
    c = Vector(center)
    if matrix is None:
        f = (c - eye).normalized()
        m = geo.look_matrix(c + f * (depth * 0.5 + 0.004), f)
    else:
        m = matrix @ geo.at((0, 0, depth * 0.5 + 0.004))
    w, h = size[0] + 2 * border, size[1] + 2 * border
    return _box_m(name, (w, h, depth), slot, m)


SIDE_SIZE = (0.18, 0.13)   # the side display glasses (CockpitFactory: Display Side Left / Right)


def side_console(side):
    """The angled console block beside the seat (size 0.16 x 0.26 x 0.42)."""
    return geo.at((0.6 * side, -0.27, 0.12), geo.rot(z=-25 * side))


def side_face(side):
    """The side console's inner face, the one turned to the pilot: its centre, outward normal, up and nose directions."""
    m = side_console(side)
    r = m.to_3x3()
    return (m @ Vector((-0.08 * side, 0.0, 0.0)), (r @ Vector((-side, 0.0, 0.0))).normalized(),
            (r @ Vector((0.0, 1.0, 0.0))).normalized(), (r @ Vector((0.0, 0.0, 1.0))).normalized())


def side_display(side):
    """
    The side display: flat on the upper front of the console's inner face, its glass 12 mm proud of it (so
    nothing of it can cut into the console). Returns the glass centre and its frame (z into the console).
    CockpitFactory places the glass and its text with the same numbers.
    """
    c, n, up, nose = side_face(side)
    centre = c + up * 0.03 + nose * 0.085 + n * 0.012
    return centre, geo.look_matrix(centre, -n, up)


def build_cockpit():
    geo.reset()
    surface.make_all(PALETTE)
    parts = []
    P = parts.append

    P(shell())

    # ribs along the shell: from the window corners back over the top, sides and floor
    for phi in SPOKES:
        path = arc(window_edge(phi) + 1.0, 168.0, phi, R - 0.03, n=20)
        P(geo.bevel(geo.sweep("Rib", path, geo.rect(0.075, 0.06), "FRAME", up=inward), width=0.01, segments=1, angle=40))
    # shoulder ring behind the pilot and a lower ring
    for theta, size in ((112.0, (0.06, 0.05)), (140.0, (0.05, 0.04))):
        ring = [sphere(R - 0.03, theta, a) for a in range(0, 360, 10)]
        P(geo.bevel(geo.sweep("Ring", ring, geo.rect(*size), "FRAME", up=inward, closed_path=True), width=0.008, segments=1, angle=40))

    # window frame: the heavy round edge, eight spokes to the hub ring (clear glass inside it)
    edge_path = [sphere(R - 0.025, window_edge(a), a) for a in range(0, 360, 3)]
    P(geo.bevel(geo.sweep("WindowEdge", edge_path, geo.rect(0.09, 0.07), "FRAME", up=inward, closed_path=True), width=0.012, segments=2, angle=30))
    inner = [sphere(R - 0.02, HUB, a) for a in range(0, 360, 6)]
    P(geo.bevel(geo.sweep("HubRing", inner, geo.rect(0.026, 0.024), "FRAME", up=inward, closed_path=True), width=0.004, segments=1, angle=30))
    for phi in SPOKES:
        spoke = [sphere(R - 0.02, HUB + (window_edge(phi) - HUB) * t / 10.0, phi) for t in range(11)]
        P(geo.bevel(geo.sweep("Spoke", spoke, geo.rect(0.026, 0.03), "FRAME", up=inward), width=0.004, segments=1, angle=30))
        # light studs just outside the window edge, at the spokes
        P(geo.disc("Stud", sphere(R - 0.06, window_edge(phi) + 4.0, phi), inward(sphere(1, window_edge(phi) + 4.0, phi)), 0.012, "LIGHT", sides=8))

    # front console: a body under the window, faceted toward the pilot, with bezels for the three displays
    body = [(-0.62, 0.50), (-0.40, 0.43), (-0.08, 0.61), (-0.06, 0.79), (-0.62, 0.84)]
    P(geo.bevel(geo.slab("Console", body, 0.96, "TRIM", matrix=geo.rot(z=90)), width=0.015, segments=2))
    P(geo.bevel(geo.slab("ConsoleLip", [(-0.075, 0.6), (-0.05, 0.6), (-0.04, 0.8), (-0.065, 0.8)], 1.0, "DARK", matrix=geo.rot(z=90)), width=0.006, segments=1))
    for name, c, size in (("BezelC", (0.0, -0.2, 0.52), (0.24, 0.15)), ("BezelL", (-0.27, -0.21, 0.47), (0.2, 0.14)),
                          ("BezelR", (0.27, -0.21, 0.47), (0.2, 0.14))):
        P(geo.bevel(bezel(name, c, size, 0.09, "DARK"), width=0.006, segments=1))
    # a light strip under the window along the console top, and small buttons beside the displays
    P(geo.bevel(_box_m("ConsoleStrip", (0.7, 0.012, 0.014), "LIGHT", geo.look_matrix(Vector((0, -0.075, 0.66)), Vector((0, 0.2, 1)))), width=0.003, segments=1))
    for side in (1, -1):
        for k in range(4):
            c = Vector((0.4 * side, -0.27 + k * 0.035, 0.5 + k * 0.02))
            P(_box_m("Button", (0.03, 0.018, 0.012), "LIGHT" if k % 2 == 0 else "METAL", geo.look_matrix(c, c - EYE)))

    # side consoles: angled blocks with a display bezel and rows of switches
    for side in (1, -1):
        P(geo.bevel(_box_m("SideConsole", (0.16, 0.26, 0.42), "TRIM", side_console(side)), width=0.012, segments=2))
        sc, sm = side_display(side)
        P(geo.bevel(bezel("BezelSide", sc, SIDE_SIZE, 0.03, "DARK", matrix=sm), width=0.005, segments=1))
        for row in range(3):
            for k in range(5):
                p = Vector((0.66 * side - 0.03 * side * row, -0.15 + 0.012 * row, -0.03 + 0.05 * k + 0.25 * (row == 2)))
                slot = "LIGHT" if (row + k) % 3 == 0 else "METAL"
                P(_box_m("Switch", (0.018, 0.012, 0.022), slot, geo.at(p, geo.rot(z=-25 * side))))
        # an armrest on top of each side console
        P(geo.bevel(_box_m("Armrest", (0.11, 0.05, 0.34), "SEAT", geo.at((0.52 * side, -0.37, -0.02), geo.rot(z=-12 * side))), width=0.02, segments=2))

    # seat: pan, back with bolsters, headrest, harness straps
    P(geo.bevel(geo.box("SeatPan", (0, -0.6, -0.12), (0.5, 0.1, 0.5), "SEAT"), width=0.03, segments=3))
    back = geo.at((0, -0.15, -0.44), geo.rot(x=-8))
    P(geo.bevel(_box_m("SeatBack", (0.5, 0.8, 0.1), "SEAT", back), width=0.03, segments=3))
    for side in (1, -1):
        P(geo.bevel(_box_m("Bolster", (0.07, 0.62, 0.14), "SEAT", back @ geo.at((0.26 * side, -0.05, 0.03), geo.rot(y=-12 * side))), width=0.025, segments=2))
        P(geo.bevel(_box_m("Strap", (0.05, 0.62, 0.012), "STRAP", back @ geo.at((0.12 * side, 0.0, -0.058), geo.rot(z=8 * side))), width=0.003, segments=1))
    P(geo.bevel(_box_m("SeatFrame", (0.56, 0.9, 0.05), "FRAME", back @ geo.at((0, -0.02, 0.07))), width=0.01, segments=1))
    P(geo.bevel(_box_m("Headrest", (0.3, 0.18, 0.09), "SEAT", geo.at((0, 0.32, -0.5), geo.rot(x=-8))), width=0.03, segments=3))
    P(geo.bevel(geo.box("Buckle", (0, -0.25, -0.385), (0.07, 0.07, 0.02), "METAL"), width=0.006, segments=1))

    # floor: plate with ribs, two pedals, the stick and throttle housings
    P(geo.bevel(geo.box("Floor", (0, -0.78, 0.12), (0.92, 0.04, 0.95), "TRIM"), width=0.01, segments=1))
    for z in (-0.2, 0.05, 0.3, 0.5):
        P(geo.box("FloorRib", (0, -0.755, z), (0.8, 0.012, 0.03), "DARK"))
    for side in (1, -1):
        P(geo.bevel(_box_m("Pedal", (0.11, 0.025, 0.2), "METAL", geo.at((0.16 * side, -0.7, 0.5), geo.rot(x=-35))), width=0.006, segments=1))
    P(geo.bevel(geo.cylinder("StickBoot", (0.24, -0.6, 0.22), (0.24, -0.53, 0.22), 0.07, 0.055, "DARK", sides=12), width=0.008, segments=1))
    P(geo.bevel(geo.box("StickBase", (0.24, -0.63, 0.22), (0.2, 0.06, 0.24), "TRIM"), width=0.01, segments=1))
    P(geo.bevel(geo.box("ThrottleHousing", (-0.3, -0.505, 0.12), (0.1, 0.05, 0.26), "TRIM"), width=0.01, segments=1))
    P(geo.box("ThrottleSlot", (-0.3, -0.478, 0.12), (0.025, 0.01, 0.22), "DARK"))
    P(geo.box("ThrottleStrip", (-0.36, -0.478, 0.12), (0.008, 0.008, 0.2), "LIGHT"))

    # equipment on the shell beside and behind the pilot, cables low along the sides
    for theta, phi, size in ((100, 15, (0.22, 0.14, 0.08)), (120, 25, (0.18, 0.2, 0.06)), (100, 165, (0.22, 0.14, 0.08)),
                             (120, 155, (0.18, 0.2, 0.06)), (130, 90, (0.4, 0.2, 0.08)), (150, 60, (0.2, 0.16, 0.07)),
                             (150, 120, (0.2, 0.16, 0.07)), (95, 210, (0.24, 0.12, 0.07)), (95, 330, (0.24, 0.12, 0.07))):
        P(geo.bevel(on_shell("Box", theta, phi, size, size[2] / 2 + 0.02, "TRIM"), width=0.008, segments=1))
        P(on_shell("BoxLight", theta, phi, (size[0] * 0.6, 0.008, 0.006), size[2] + 0.025, "LIGHT"))
    for phi0, phi1 in ((195, 245), (295, 345)):
        for k, (dr, th) in enumerate(((0.06, 92), (0.08, 98), (0.07, 104))):
            path = [sphere(R - dr, th + 6 * math.sin(i * 0.7 + k), phi0 + (phi1 - phi0) * i / 14) for i in range(15)]
            P(geo.sweep("Cable", path, geo.circle(0.011 + 0.003 * k, 8), "DARK" if k != 1 else "STRAP"))
    # rib light strips (inner side of the ribs, low)
    for i in (2, 4):
        phi = i * 60.0 + 30.0
        path = arc(80.0, 120.0, phi - 30.0, R - 0.07, n=8)
        P(geo.sweep("RibLight", path, geo.rect(0.012, 0.006), "LIGHT", up=inward))

    instruments(P)
    main = geo.join(parts, NAME + "_LOD0")
    return main


def facing(c):
    """A frame at c looking at the pilot: local x right, y up, z away from the eye."""
    c = Vector(c)
    return geo.look_matrix(c, c - EYE)


def frame_path(m, outline, inset=0.0):
    return [tuple(m @ Vector((x * (1 - inset), y * (1 - inset), 0.0))) for x, y in outline]


def frame_outline(hw, hh, tips, chamfer, tip):
    """
    A display frame's outline (x right, y up, clockwise from the top left): flat top and bottom, a point on
    each side named in tips (left, right), chamfered corners on the sides without one.
    """
    left, right = tips
    pts = [(-hw, hh)] if left else [(-hw, hh - chamfer), (-hw + chamfer, hh)]
    pts += [(hw, hh)] if right else [(hw - chamfer, hh), (hw, hh - chamfer)]
    if right:
        pts += [(hw + tip, 0.0)]
    pts += [(hw, -hh)] if right else [(hw, -hh + chamfer), (hw - chamfer, -hh)]
    pts += [(-hw, -hh)] if left else [(-hw + chamfer, -hh), (-hw, -hh + chamfer)]
    if left:
        pts += [(-hw - tip, 0.0)]
    return pts


def hex_frame(P, name, m, w, h, tips, glow="L_BLUE", tip=0.006, chamfer=0.012):
    """
    An angular frame around a display glass of w x h, with a thin lit line along it. It lies on the face of
    the display's bezel (along the middle of its 22 mm border, 12 mm wide), so it never reaches past the
    bezel and frames of neighbouring displays can't overlap. m: the glass's frame (z away from the pilot).
    """
    hw, hh = w / 2 + 0.011, h / 2 + 0.011
    f = geo.xd(m, (0, 0, 1))
    outline = frame_outline(hw, hh, tips, chamfer, tip)
    frame = geo.bevel(geo.sweep(name, frame_path(m, outline), geo.rect(0.012, 0.008), "FRAME", up=f, closed_path=True),
                      width=0.002, segments=1, angle=30)
    line = geo.sweep(name + "Glow", frame_path(m @ geo.at((0, 0, -0.0048)), outline), geo.rect(0.003, 0.0016), glow, up=f, closed_path=True)
    P(frame)
    P(line)
    return [frame, line]


def instruments(P):
    """
    Instruments in the style of the owner's references (a TIE cockpit and a modern space-sim cockpit): angular
    display frames with lit edges, a scope pod with a button row, a bar graph panel, a button grid, lamps,
    toggle switches and corrugated hoses. The game draws the live parts (radar blips, bars) at SOCKET_Radar
    and SOCKET_Gauges.
    """
    # angular frames with lit lines around the five displays: blue on the front three (the centre one with cut
    # corners, the outer two pointed on their outer side only), red on the side consoles (pointed toward the nose)
    check = {}
    for name, c, size, tips in (("FrameC", (0.0, -0.2, 0.52), (0.24, 0.15), (False, False)),
                                ("FrameL", (-0.27, -0.21, 0.47), (0.2, 0.14), (True, False)),
                                ("FrameR", (0.27, -0.21, 0.47), (0.2, 0.14), (False, True))):
        check[name] = hex_frame(P, name, facing(c), *size, tips)
    for side, name in ((-1, "FrameSL"), (1, "FrameSR")):
        sc, sm = side_display(side)
        nose_x = Vector(geo.xd(sm, (1, 0, 0))).dot(side_face(side)[3])
        check[name] = hex_frame(P, name, sm, *SIDE_SIZE, (nose_x < 0, nose_x > 0), glow="L_RED")

    # the scope pod on the console top: a housing, a round scope with range rings, a button row, lamps and knobs
    f = (RADAR - EYE).normalized()
    m = facing(RADAR)
    check["ScopePod"] = [geo.bevel(_box_m("ScopePod", (0.2, 0.12, 0.12), "TRIM", m @ geo.at((0, 0, 0.06))), width=0.01, segments=2)]
    P(check["ScopePod"][0])
    P(geo.tube("ScopeBezel", tuple(RADAR - f * 0.012), tuple(RADAR + f * 0.004), 0.06, 0.05, "FRAME", sides=32))
    P(geo.disc("ScopeGlass", tuple(RADAR + f * 0.002), tuple(-f), 0.051, "SCOPE", sides=32))
    for r in (0.017, 0.034, 0.049):
        ring = [tuple(m @ Vector((math.cos(math.radians(a)) * r, math.sin(math.radians(a)) * r, -0.001))) for a in range(0, 360, 10)]
        P(geo.sweep("ScopeRing", ring, geo.rect(0.0015, 0.001), "L_GREEN", up=tuple(f), closed_path=True))
    for a in (0, 90):
        d = (math.cos(math.radians(a)), math.sin(math.radians(a)))
        P(geo.sweep("ScopeCross", [tuple(m @ Vector((-d[0] * 0.049, -d[1] * 0.049, -0.001))), tuple(m @ Vector((d[0] * 0.049, d[1] * 0.049, -0.001)))],
                    geo.rect(0.0012, 0.001), "L_GREEN", up=tuple(f)))
    P(_box_m("ScopeSelf", (0.006, 0.006, 0.002), "L_GREEN", m @ geo.at((0, 0, -0.002))))
    for k, slot in enumerate(("L_GREEN", "L_RED", "L_BLUE", "DARK", "L_YELLOW", "DARK")):
        P(geo.bevel(_box_m("PodButton", (0.018, 0.014, 0.012), slot, m @ geo.at((-0.05 + k * 0.02, 0.075, -0.005))), width=0.002, segments=1))
    for k in range(2):
        P(geo.disc("PodLamp", tuple(m @ Vector((-0.078, 0.022 - k * 0.04, -0.004))), tuple(-f), 0.006, "L_RED", sides=10))
        P(geo.bevel(geo.cylinder("PodKnob", tuple(m @ Vector((0.078, 0.022 - k * 0.04, 0.0))), tuple(m @ Vector((0.078, 0.022 - k * 0.04, -0.016))),
                                 0.011, 0.009, "METAL", sides=12), width=0.002, segments=1))

    # the bar graph panel below the left display: a recess with four slots (the game fills them), lit labels below
    m = facing(GAUGES)
    f = (GAUGES - EYE).normalized()
    check["GaugePanel"] = [geo.bevel(_box_m("GaugePanel", (0.2, 0.085, 0.02), "DARK", m @ geo.at((0, 0, 0.004))), width=0.004, segments=1)]
    P(check["GaugePanel"][0])
    P(geo.bevel(geo.sweep("GaugeFrame", frame_path(m @ geo.at((0, 0, -0.006)), [(-0.1, 0.0425), (0.1, 0.0425), (0.1, -0.0425), (-0.1, -0.0425)]),
                          geo.rect(0.01, 0.012), "FRAME", up=tuple(f), closed_path=True), width=0.002, segments=1))
    for k, slot in enumerate(("L_BLUE", "L_YELLOW", "L_BLUE", "L_GREEN")):
        x = -0.066 + k * 0.044
        P(_box_m("GaugeSlot", (0.018, 0.06, 0.003), "SCOPE", m @ geo.at((x, 0.004, -0.007))))
        P(_box_m("GaugeLabel", (0.016, 0.004, 0.002), slot, m @ geo.at((x, -0.033, -0.008))))

    # the button grid below the right display: square lamps, two big red buttons, toggle switches
    m = facing((0.27, -0.366, 0.455))
    check["ButtonPanel"] = [geo.bevel(_box_m("ButtonPanel", (0.2, 0.085, 0.02), "DARK", m @ geo.at((0, 0, 0.004))), width=0.004, segments=1)]
    P(check["ButtonPanel"][0])
    for row in range(3):
        for col in range(4):
            slot = ("LIGHT", "METAL", "L_RED", "METAL")[(row + col * 2) % 4]
            P(geo.bevel(_box_m("GridButton", (0.016, 0.014, 0.008), slot, m @ geo.at((-0.075 + col * 0.022, 0.024 - row * 0.022, -0.006))), width=0.002, segments=1))
    for k in range(2):
        P(geo.bevel(geo.cylinder("RedButton", tuple(m @ Vector((0.03 + k * 0.034, 0.012, 0.0))), tuple(m @ Vector((0.03 + k * 0.034, 0.012, -0.012))),
                                 0.012, 0.011, "L_RED", sides=12), width=0.002, segments=1))
    for k in range(3):
        P(geo.bevel(_box_m("ToggleBase", (0.012, 0.012, 0.006), "METAL", m @ geo.at((0.024 + k * 0.025, -0.022, -0.004))), width=0.001, segments=1))
        P(_box_m("Toggle", (0.004, 0.004, 0.02), "METAL", m @ geo.at((0.024 + k * 0.025, -0.017, -0.012)) @ geo.rot(x=30)))

    # on the rear part of each side console's inner face (behind its display): vertical bar graphs (red and
    # white segments) with round lamps under them on the left, lit square buttons and two lamps on the right
    for side in (-1, 1):
        c, n, up, nose = side_face(side)
        m = geo.look_matrix(c - nose * 0.12 + n * 0.002, -n, up)   # z into the face, flush with it
        items = check["Side" + ("L" if side < 0 else "R")] = []
        if side < 0:
            for col in range(3):
                for seg in range(8):
                    slot = "L_RED" if (seg + col) % 3 else "LIGHT"
                    items.append(_box_m("BarSeg", (0.008, 0.006, 0.004), slot, m @ geo.at((-0.03 + col * 0.03, -0.03 + seg * 0.012, 0.0))))
                items.append(geo.disc("BarLamp", tuple(m @ Vector((-0.03 + col * 0.03, -0.05, -0.003))), geo.xd(m, (0, 0, -1)), 0.007,
                                      "L_RED" if col < 2 else "LIGHT", sides=10))
        else:
            for row in range(2):
                for col in range(3):
                    items.append(geo.bevel(_box_m("SideButton", (0.014, 0.014, 0.008), "LIGHT" if (row + col) % 2 else "METAL",
                                                  m @ geo.at((-0.022 + col * 0.022, 0.0 + row * 0.025, -0.002))), width=0.002, segments=1))
            for k in range(2):
                items.append(geo.disc("SideLamp", tuple(m @ Vector((-0.011 + k * 0.022, -0.04, -0.002))), geo.xd(m, (0, 0, -1)), 0.006, "L_RED", sides=10))
        for ob in items:
            P(ob)

    # two corrugated hoses from under the console to the seat sides
    for side in (1, -1):
        a, b, c = Vector((0.2 * side, -0.46, 0.42)), Vector((0.3 * side, -0.62, 0.2)), Vector((0.24 * side, -0.5, -0.12))
        path = [tuple((1 - t) ** 2 * a + 2 * (1 - t) * t * b + t ** 2 * c) for t in (i / 16 for i in range(17))]
        P(geo.sweep("Hose", path, geo.circle(0.018, 10), "DARK"))
        for i in range(1, 16):
            p, q = Vector(path[i]), Vector(path[i + 1])
            mid = (p + q) / 2
            d = (q - p).normalized()
            P(geo.cylinder("HoseRib", tuple(mid - d * 0.006), tuple(mid + d * 0.006), 0.023, 0.023, "DARK", sides=10, cap0=False, cap1=False))

    # the overhead display: a dark housing reaching up into the shell, its bezel, an amber-lit frame
    m = facing(OVERHEAD)
    check["Overhead"] = [geo.bevel(_box_m("OverheadHousing", (0.54, 0.13, 0.12), "TRIM", m @ geo.at((0, 0, 0.068))), width=0.01, segments=2)]
    P(check["Overhead"][0])
    P(geo.bevel(bezel("BezelOverhead", OVERHEAD, OVERHEAD_SIZE, 0.03, "DARK"), width=0.005, segments=1))
    hex_frame(P, "FrameOverhead", m, *OVERHEAD_SIZE, (True, True), glow="L_YELLOW")
    for x in (-0.24, 0.24):
        P(_box_m("OverheadLamp", (0.012, 0.03, 0.004), "L_RED", m @ geo.at((x, 0.0, -0.002))))

    layout_check(check)
    geo.socket("Radar", tuple(RADAR - (RADAR - EYE).normalized() * 0.004))
    geo.socket("Gauges", tuple(GAUGES - (GAUGES - EYE).normalized() * 0.009))


def _unity_points(objects):
    back = geo.TO_BLENDER.inverted()
    return [(back @ (ob.matrix_world @ v.co).to_4d()).to_3d() for ob in objects for v in ob.data.vertices]


def _hull(points):
    pts = sorted(set(points))
    if len(pts) < 3:
        return pts
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def _overlap(a, b):
    """How far two convex outlines overlap (degrees; 0 or less = apart), by separating axes."""
    depth = 1e9
    for poly in (a, b):
        for i in range(len(poly)):
            p, q = poly[i], poly[(i + 1) % len(poly)]
            ax = (-(q[1] - p[1]), q[0] - p[0])
            length = math.hypot(*ax) or 1.0
            ax = (ax[0] / length, ax[1] / length)
            pa = [x * ax[0] + y * ax[1] for x, y in a]
            pb = [x * ax[0] + y * ax[1] for x, y in b]
            depth = min(depth, min(max(pa), max(pb)) - max(min(pa), min(pb)))
    return depth


def layout_check(items):
    """
    Prints 'Cockpit check: ...' for instrument parts that overlap as the pilot sees them (outlines in
    degrees from the eye) and for side console parts that cut into the console block or the armrest.
    """
    problems = 0
    outlines = {}
    for name, objects in items.items():
        pts = []
        for p in _unity_points(objects):
            d = p - EYE
            pts.append((round(math.degrees(math.atan2(d.x, d.z)), 3), round(math.degrees(math.atan2(d.y, math.hypot(d.x, d.z))), 3)))
        outlines[name] = _hull(pts)
    names = sorted(outlines)
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            depth = _overlap(outlines[a], outlines[b])
            if depth > 0.05:
                print(f"Cockpit check: {a} and {b} overlap by {depth:.2f} degrees as seen from the eye")
                problems += 1
    for side, keys in ((-1, ("FrameSL", "SideL")), (1, ("FrameSR", "SideR"))):
        blocks = ((side_console(side).inverted(), Vector((0.08, 0.13, 0.21))),
                  (geo.at((0.52 * side, -0.37, -0.02), geo.rot(z=-12 * side)).inverted(), Vector((0.055, 0.025, 0.17))))
        for key in keys:
            for p in _unity_points(items[key]):
                for inv, half in blocks:
                    q = inv @ p
                    if abs(q.x) < half.x - 0.002 and abs(q.y) < half.y - 0.002 and abs(q.z) < half.z - 0.002:
                        print(f"Cockpit check: {key} cuts into the {'console' if half.x > 0.06 else 'armrest'} at {tuple(round(v, 3) for v in p)}")
                        problems += 1
                        break
                else:
                    continue
                break
    # the overhead display: hidden while looking straight ahead (its lowest point well above the view),
    # clear of the window opening, and its housing just reaching the shell
    over = _unity_points(items["Overhead"])
    low = min(math.degrees(math.atan2((p - EYE).y, math.hypot((p - EYE).x, (p - EYE).z))) for p in over)
    near_window = min(math.degrees(math.acos(max(-1.0, min(1.0, p.normalized().z)))) for p in over)
    print(f"Overhead display: lowest point {low:.1f} degrees above the eye, {near_window:.1f} degrees from straight ahead (window edge {HALF:.0f})")
    if low < 50.0 or near_window < HALF + 4.0:
        print("Cockpit check: the overhead display is too low (in view or over the window)")
        problems += 1
    print(f"Cockpit check: {problems} problem(s)")
    sc = [side_display(s) for s in (-1, 1)]
    for s, (centre, m) in zip((-1, 1), sc):
        fwd, up = geo.xd(m, (0, 0, 1)), geo.xd(m, (0, 1, 0))
        print(f"Side display {s:+d}: centre ({centre.x:.4f}, {centre.y:.4f}, {centre.z:.4f}), "
              f"forward ({fwd[0]:.4f}, {fwd[1]:.4f}, {fwd[2]:.4f}), up ({up[0]:.4f}, {up[1]:.4f}, {up[2]:.4f})")


def build_controls():
    """The stick and the throttle lever, each around its pivot at the base (CockpitInstruments turns them)."""
    parts = []
    P = parts.append
    # stick: boot, shaft, an angled grip with a trigger and a lit hat button
    P(geo.bevel(geo.cylinder("Boot", (0, 0.0, 0), (0, 0.06, 0), 0.05, 0.03, "DARK", sides=12), width=0.006, segments=1))
    P(geo.bevel(geo.cylinder("Shaft", (0, 0.05, 0), (0, 0.2, 0), 0.018, 0.016, "METAL", sides=10), width=0.003, segments=1))
    grip = geo.loft("Grip", [
        (0.0, geo.superellipse(0.048, 0.056, count=12, power=2.4)),
        (0.05, geo.superellipse(0.052, 0.062, count=12, power=2.4), (0, 0.004)),
        (0.1, geo.superellipse(0.046, 0.058, count=12, power=2.4), (0, 0.01)),
    ], "TRIM", matrix=geo.at((0, 0.19, 0), geo.rot(x=-90)))
    P(geo.bevel(grip, width=0.006, segments=2))
    P(geo.bevel(geo.box("Trigger", (0, 0.24, 0.032), (0.016, 0.03, 0.012), "DARK"), width=0.003, segments=1))
    P(geo.cylinder("Hat", (0, 0.29, 0.008), (0, 0.3, 0.012), 0.011, 0.01, "L_RED", sides=12))
    P(geo.cylinder("FrontButton", (0, 0.255, 0.028), (0, 0.255, 0.036), 0.009, 0.008, "L_RED", sides=12))
    P(geo.bevel(geo.box("Thumb", (0.018, 0.275, 0.0), (0.012, 0.012, 0.02), "METAL"), width=0.002, segments=1))
    stick = geo.join(parts, "CockpitControls_Stick")

    parts = []
    P = parts.append
    P(geo.bevel(geo.cylinder("LeverShaft", (0, 0, 0), (0, 0.15, 0), 0.016, 0.014, "METAL", sides=10), width=0.003, segments=1))
    P(geo.bevel(geo.box("Handle", (0, 0.18, 0.0), (0.06, 0.06, 0.09), "TRIM"), width=0.012, segments=2))
    P(geo.bevel(geo.box("HandleCap", (0, 0.212, 0.0), (0.05, 0.012, 0.075), "DARK"), width=0.004, segments=1))
    P(geo.box("HandleLight", (-0.031, 0.18, 0.0), (0.004, 0.012, 0.05), "LIGHT"))
    lever = geo.join(parts, "CockpitControls_Throttle")
    return stick, lever


def controls():
    """The stick and throttle model: one 512 atlas for both, split again after the bake."""
    geo.reset()
    surface.make_all(PALETTE)
    stick, lever = build_controls()
    geo.tag_part(stick, 0)
    geo.tag_part(lever, 1)
    joined = geo.join([stick, lever], "CockpitControls_Bake")
    bake.unwrap(joined, margin=0.006)
    bake.bake(joined, "CockpitControls", 512, export.TEXTURES, ao_distance=0.05, margin=4)
    stick2, lever2 = geo.split_parts(joined, ["CockpitControls_Stick", "CockpitControls_Throttle"])
    export.fbx([stick2, lever2], "CockpitControls")
    return geo.triangles(stick2), geo.triangles(lever2)


def preview_from_fbx():
    """Preview sheet from the exported FBX and textures (no rebuild)."""
    import bpy
    geo.reset()
    bpy.ops.import_scene.fbx(filepath=os.path.join(export.MODELS, NAME + ".fbx"))
    ob = bpy.data.objects[NAME + "_LOD0"]
    paths = {k: os.path.join(export.TEXTURES, f"{NAME}_{k}.png") for k in ("Albedo", "Mask", "Normal")}
    bpy.context.scene.world = bpy.data.worlds.new("World")
    cockpit_preview(ob, paths, geo.triangles(ob))


def main(preview=True):
    main_ob = build_cockpit()
    bake.unwrap(main_ob, margin=0.002)
    paths = bake.bake(main_ob, NAME, TEX, export.TEXTURES, ao_distance=0.3, margin=6)
    export.fbx([main_ob] + [o for o in bpy.data.objects if o.name.startswith("SOCKET_")], NAME)
    tris = geo.triangles(main_ob)
    if preview:
        cockpit_preview(main_ob, paths, tris)
    stick, lever = controls()
    print(f"{NAME}: {tris} triangles; controls {stick} + {lever}")
    return (tris, stick, lever)


def cockpit_preview(ob, paths, tris):
    """Renders the cockpit from the pilot's eye: straight ahead, down-left at the throttle side, right."""
    import bpy
    from PIL import Image, ImageDraw, ImageFont
    scene = bpy.context.scene
    mat = bake.textured_material(NAME + "_Preview", paths)
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    for o in list(scene.objects):
        if o.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(o)
    world = scene.world
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputWorld')
    bg = nt.nodes.new('ShaderNodeBackground')
    bg.inputs['Color'].default_value = (0.012, 0.016, 0.03, 1.0)
    bg.inputs['Strength'].default_value = 1.0
    nt.links.new(bg.outputs[0], out.inputs[0])
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.0
    sun.data.color = (1.0, 0.95, 0.88)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (geo.to_blender((-0.45, -0.55, -0.7)) * -1).to_track_quat('-Z', 'Y').to_euler()
    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'POINT'))
    fill.data.energy = 6.0
    fill.data.color = (0.55, 0.8, 1.0)
    fill.location = geo.to_blender((0, 0.3, 0.3))
    scene.collection.objects.link(fill)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 14
    cam.data.clip_start = 0.02
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.cycles.samples = 64
    scene.render.resolution_x, scene.render.resolution_y = 800, 600
    scene.view_settings.view_transform = 'AgX'
    os.makedirs(export.PREVIEWS, exist_ok=True)
    views = (("pilot view, ahead", (0, -0.15, 1)), ("down-left (throttle, side console)", (-0.8, -0.8, 0.6)),
             ("right and back (seat side)", (1, -0.2, -0.15)))
    tiles = []
    for i, (label, d) in enumerate(views):
        cam.location = geo.to_blender(EYE)
        cam.rotation_euler = (geo.to_blender(EYE + Vector(d)) - geo.to_blender(EYE)).to_track_quat('-Z', 'Y').to_euler()
        path = os.path.join(export.PREVIEWS, f"_{NAME}_{i}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        tiles.append((label, path))
    sheet = Image.new("RGB", (800 * 3, 628), (12, 14, 18))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 15)
    for i, (label, path) in enumerate(tiles):
        sheet.paste(Image.open(path).convert("RGB"), (i * 800, 28))
        draw.text((i * 800 + 8, 6), label, fill=(200, 220, 235), font=font)
        os.remove(path)
    draw.text((2400 - 420, 6), f"Cockpit  {tris} tri  tex {TEX}", fill=(255, 190, 90), font=font)
    sheet.save(os.path.join(export.PREVIEWS, NAME + ".jpg"), quality=88)


if __name__ == "__main__":
    main()
