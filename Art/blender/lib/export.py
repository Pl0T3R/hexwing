"""FBX export for Unity, and the preview renders (contact sheets) of each asset."""
import math
import os

import bpy
from mathutils import Vector

from . import geo

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MODELS = os.path.join(REPO, "Assets", "HexWing", "Resources", "Models")
TEXTURES = os.path.join(REPO, "Assets", "HexWing", "Resources", "Textures")
PREVIEWS = os.path.join(REPO, "Art", "previews")


def fbx(objects, name):
    """Writes Models/<name>.fbx with the given meshes and empties (Unity frame: see geo.py)."""
    os.makedirs(MODELS, exist_ok=True)
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    for o in objects:
        o.select_set(True)
    path = os.path.join(MODELS, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=True, use_custom_props=False,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    return path


def _look(cam, target):
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def preview(ob, name, material, radius, extra=(), size=(640, 480), samples=48, notes="", views=None):
    """
    Renders ob with its baked textures from three sides against space and writes
    Art/previews/<name>.jpg (one sheet), so the look can be checked without Unity.
    Positions are in the Unity frame (z forward).
    """
    from PIL import Image, ImageDraw
    scene = bpy.context.scene
    os.makedirs(PREVIEWS, exist_ok=True)
    keep = {ob.name} | {e.name for e in extra}
    for o in list(scene.objects):
        if o.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(o)
        elif o.name not in keep:
            o.hide_render = True
    for o in [ob] + [e for e in extra if e.type == 'MESH']:
        o.data.materials.clear()
        o.data.materials.append(material)
    # frame the model: never closer than its farthest point from the origin (the cameras look at it)
    reach = max(((o.matrix_world @ Vector(c)).length for o in [ob] + [e for e in extra if e.type == 'MESH']
                 for c in o.bound_box), default=0.0)
    radius = max(radius, 1.1 * reach)

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    # a dim blue-grey studio sky: dark enough for space, bright enough to read every silhouette
    nt = world.node_tree
    nt.nodes.clear()
    wout = nt.nodes.new('ShaderNodeOutputWorld')
    bg = nt.nodes.new('ShaderNodeBackground')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    up = nt.nodes.new('ShaderNodeMapRange')
    up.inputs['From Min'].default_value = -0.4
    up.inputs['From Max'].default_value = 1.0
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    nt.links.new(sep.outputs[2], up.inputs['Value'])   # Blender z = up
    nt.links.new(up.outputs['Result'], ramp.inputs['Factor'])
    ramp.color_ramp.elements[0].color = (0.006, 0.008, 0.014, 1.0)
    ramp.color_ramp.elements[1].color = (0.05, 0.06, 0.085, 1.0)
    nt.links.new(ramp.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = 1.0
    nt.links.new(bg.outputs['Background'], wout.inputs['Surface'])

    def light(kind, pos, energy, color, size_=0.0):
        data = bpy.data.lights.new(kind + "L", kind)
        data.energy = energy
        data.color = color
        if kind == 'SUN':
            data.angle = math.radians(1.5)
        o = bpy.data.objects.new(kind + "L", data)
        scene.collection.objects.link(o)
        o.location = geo.to_blender(pos)
        _look(o, (0, 0, 0))
        return o

    light('SUN', (radius * 3, radius * 4, radius * 2), 4.0, (1.0, 0.95, 0.88))
    light('SUN', (-radius * 3, -radius * 1, -radius * 3), 1.2, (0.35, 0.5, 0.9))   # cool fill from the nebula side
    light('SUN', (-radius * 2, radius * 3, radius * 3), 1.0, (0.85, 0.9, 1.0))     # rim

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 50
    cam_data.clip_start = radius * 0.01
    cam_data.clip_end = radius * 20.0
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    geo.limit_threads(scene)
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'

    # views: (label, camera position as multiples of the radius, Unity frame)
    views = [(label, (radius * m[0], radius * m[1], radius * m[2])) for label, m in (views or (
        ("3/4 front", (2.2, 1.1, 2.6)), ("side", (3.6, 0.3, 0.0)), ("3/4 rear, above", (-2.1, 2.4, -2.5))))]
    tiles = []
    for label, pos in views:
        cam.location = geo.to_blender(pos)
        _look(cam, (0, 0, 0))
        path = os.path.join(PREVIEWS, f"_{name}_{len(tiles)}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        tiles.append((label, path))

    w, h = size
    sheet = Image.new("RGB", (w * len(tiles), h + 28), (12, 14, 18))
    draw = ImageDraw.Draw(sheet)
    try:
        from PIL import ImageFont
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 15)
    except OSError:
        font = None
    for i, (label, path) in enumerate(tiles):
        sheet.paste(Image.open(path).convert("RGB"), (i * w, 28))
        draw.text((i * w + 8, 6), label, fill=(200, 220, 235), font=font)
        os.remove(path)
    draw.text((w * len(tiles) - 460, 6), notes, fill=(255, 190, 90), font=font)
    out = os.path.join(PREVIEWS, name + ".jpg")
    sheet.save(out, quality=88)
    return out
