"""
UV unwrapping and texture baking into the game's three textures per asset:
- <Name>_Albedo.png (sRGB): the colour, with a touch of the baked AO in it,
- <Name>_Mask.png (linear RGBA): R metal, G smoothness, B emission, A ambient occlusion,
- <Name>_Normal.png (linear, OpenGL / Y+ like Unity): the bevel and panel-line detail.
"""
import os

import bpy
import numpy as np

from . import surface


def triangulate(ob):
    """Triangles before UVs and baking, so Blender's and Unity's tangent spaces (MikkTSpace) match."""
    m = ob.modifiers.new("Triangulate", 'TRIANGULATE')
    m.quad_method = 'BEAUTY'
    m.ngon_method = 'BEAUTY'
    m.keep_custom_normals = True
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=m.name)


def unwrap(ob, margin=0.004, angle=60.0):
    """Smart UV project + pack: every face in its own space of one atlas."""
    import math
    triangulate(ob)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=1.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=margin, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')


def _image(name, size):
    """A float, linear bake target: its pixels are exactly the baked values (encoding happens on save)."""
    img = bpy.data.images.get(name)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=True)
    img.colorspace_settings.name = 'Non-Color'
    return img


def _target(ob, img, show):
    for slot in ob.material_slots:
        mat = slot.material
        node = mat.node_tree.nodes['BakeTarget']
        node.image = img
        mat.node_tree.nodes.active = node
        if show is not None:
            surface.show(mat, show)


def _pixels(img):
    buf = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    return buf.reshape(img.size[1], img.size[0], 4)


def bake(ob, name, size, out_dir, samples=12, ao_samples=32, margin=8, ao_in_albedo=0.35):
    """Bakes the four passes of ob and writes the three game textures. Returns their paths."""
    scene = bpy.context.scene
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.light_settings.distance = 1.0
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    b = scene.render.bake
    b.margin = margin
    b.margin_type = 'EXTEND'
    b.use_clear = True
    b.normal_space = 'TANGENT'

    color = _image(name + "_color", size)
    mask = _image(name + "_mask", size)
    normal = _image(name + "_normal", size)
    ao = _image(name + "_ao", size)

    scene.cycles.samples = samples
    _target(ob, color, 'color')
    bpy.ops.object.bake(type='EMIT')
    _target(ob, mask, 'mask')
    bpy.ops.object.bake(type='EMIT')
    _target(ob, normal, 'bsdf')
    bpy.ops.object.bake(type='NORMAL')
    scene.cycles.samples = ao_samples
    _target(ob, ao, None)
    bpy.ops.object.bake(type='AO')

    os.makedirs(out_dir, exist_ok=True)
    from PIL import Image
    c = _pixels(color)
    m = _pixels(mask)
    n = _pixels(normal)
    a = _pixels(ao)[..., 0:1]
    # the colour was baked as linear values: encode it to sRGB for the albedo PNG
    albedo = c[..., :3] * (1.0 - ao_in_albedo + ao_in_albedo * a)
    albedo = _to_srgb(np.clip(albedo, 0.0, 1.0))
    mask_rgba = np.concatenate([m[..., :3], a], axis=-1)
    paths = {}
    for kind, data, mode in (("Albedo", albedo, "RGB"), ("Mask", mask_rgba, "RGBA"), ("Normal", n[..., :3], "RGB")):
        arr = (np.clip(data, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)[::-1]   # Blender rows go bottom-up
        path = os.path.join(out_dir, f"{name}_{kind}.png")
        Image.fromarray(arr, mode).save(path, optimize=True)
        paths[kind] = path
    for img in (color, mask, normal, ao):
        bpy.data.images.remove(img)
    _target(ob, None, 'bsdf')
    return paths


def _to_srgb(x):
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1.0 / 2.4) - 0.055)


def textured_material(name, paths):
    """A preview material that uses the baked textures the way the game's Hull shader does."""
    mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    alb = nt.nodes.new('ShaderNodeTexImage')
    alb.image = bpy.data.images.load(paths["Albedo"])
    msk = nt.nodes.new('ShaderNodeTexImage')
    msk.image = bpy.data.images.load(paths["Mask"])
    msk.image.colorspace_settings.name = 'Non-Color'
    nrm = nt.nodes.new('ShaderNodeTexImage')
    nrm.image = bpy.data.images.load(paths["Normal"])
    nrm.image.colorspace_settings.name = 'Non-Color'
    nmap = nt.nodes.new('ShaderNodeNormalMap')
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    em = nt.nodes.new('ShaderNodeMath')
    em.operation = 'MULTIPLY'
    em.inputs[1].default_value = 6.0
    L = nt.links.new
    L(alb.outputs['Color'], bsdf.inputs['Base Color'])
    L(msk.outputs['Color'], sep.inputs[0])
    L(sep.outputs[0], bsdf.inputs['Metallic'])
    L(sep.outputs[1], inv.inputs[1])
    L(inv.outputs[0], bsdf.inputs['Roughness'])
    L(alb.outputs['Color'], bsdf.inputs['Emission Color'])
    L(sep.outputs[2], em.inputs[0])
    L(em.outputs[0], bsdf.inputs['Emission Strength'])
    L(nrm.outputs['Color'], nmap.inputs['Color'])
    L(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    L(bsdf.outputs[0], out.inputs['Surface'])
    return mat
