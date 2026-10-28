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
}


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


def bezel(name, center, size, depth, slot, eye=EYE, border=0.022):
    """A frame around a display glass (the glass itself is made in code at the same place)."""
    c = Vector(center)
    f = (c - eye).normalized()
    m = geo.look_matrix(c + f * (depth * 0.5 + 0.004), f)
    w, h = size[0] + 2 * border, size[1] + 2 * border
    return _box_m(name, (w, h, depth), slot, m)


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
        c = Vector((0.6 * side, -0.27, 0.12))
        m = geo.at(c, geo.rot(z=-25 * side))
        P(geo.bevel(_box_m("SideConsole", (0.16, 0.26, 0.42), "TRIM", m), width=0.012, segments=2))
        P(geo.bevel(bezel("BezelSide", (0.56 * side, -0.18, 0.12), (0.18, 0.13), 0.06, "DARK"), width=0.005, segments=1))
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

    main = geo.join(parts, NAME + "_LOD0")
    return main


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
    P(geo.cylinder("Hat", (0, 0.29, 0.008), (0, 0.3, 0.01), 0.008, 0.007, "LIGHT", sides=8))
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
    export.fbx([main_ob], NAME)
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
