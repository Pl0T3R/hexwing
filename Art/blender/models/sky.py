"""
The space backdrop of both maps, rendered from Blender world shaders with a panoramic
(equirectangular) camera, so every pixel is a direction:
- Sky_<Map>_Nebula.jpg (4096 x 2048): nebula clouds, a galactic band with dust lanes and glowing
  clusters. SpaceSky.shader adds its procedural stars
  (antialiased, no shimmer) and the sun on top. Sampled as a plain lat-long texture with the shader's
  own mapping (centre = straight ahead +z, right = +x), at mip 0, so there is no seam.
- Planet_<Map>.jpg (2048 x 1024): the planet's surface as a lat-long map (the direction is the surface
  normal): Orbit a blue ocean world with continents, ice caps and clouds, Belt an amber gas giant with
  bands and storms. SpaceSky.shader lights it with the sun and draws the Belt giant's rings.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy  # noqa: E402

from lib import export, geo  # noqa: E402

NAME = "Sky"
TEX = 4096


class W:
    """Node helper for world shaders."""

    def __init__(self, world):
        self.nt = world.node_tree
        self.nt.nodes.clear()
        self.out = self.nt.nodes.new('ShaderNodeOutputWorld')
        tc = self.nt.nodes.new('ShaderNodeTexCoord')
        self.dir = tc.outputs['Generated']

    def n(self, kind, **props):
        node = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(node, k, v)
        return node

    def L(self, a, b):
        self.nt.links.new(a, b)

    def noise(self, scale, detail, rough, distortion=0.0, offset=(0, 0, 0), vector=None):
        vec = vector if vector is not None else self.dir
        if offset != (0, 0, 0):
            add = self.n('ShaderNodeVectorMath', operation='ADD')
            self.L(vec, add.inputs[0])
            add.inputs[1].default_value = offset
            vec = add.outputs[0]
        nz = self.n('ShaderNodeTexNoise')
        self.L(vec, nz.inputs['Vector'])
        nz.inputs['Scale'].default_value = scale
        nz.inputs['Detail'].default_value = detail
        nz.inputs['Roughness'].default_value = rough
        nz.inputs['Distortion'].default_value = distortion
        return nz

    def math(self, op, a, b=None, clamp=False):
        m = self.n('ShaderNodeMath', operation=op, use_clamp=clamp)
        self._set(m.inputs[0], a)
        if b is not None:
            self._set(m.inputs[1], b)
        return m.outputs[0]

    def _set(self, sock, v):
        if isinstance(v, bpy.types.NodeSocket):
            self.L(v, sock)
        else:
            sock.default_value = v

    def remap(self, v, a, b, c=0.0, d=1.0):
        m = self.n('ShaderNodeMapRange', clamp=True)
        self._set(m.inputs['Value'], v)
        m.inputs['From Min'].default_value = a
        m.inputs['From Max'].default_value = b
        m.inputs['To Min'].default_value = c
        m.inputs['To Max'].default_value = d
        return m.outputs[0]

    def ramp(self, v, stops):
        r = self.n('ShaderNodeValToRGB')
        self._set(r.inputs['Factor'], v)
        els = r.color_ramp.elements
        while len(els) < len(stops):
            els.new(0.5)
        for e, (pos, col) in zip(els, stops):
            e.position = pos
            e.color = (*col, 1.0)
        return r.outputs['Color']

    def scale_color(self, color, factor):
        m = self.n('ShaderNodeVectorMath', operation='SCALE')
        self._set(m.inputs[0], color)
        self._set(m.inputs['Scale'], factor)
        return m.outputs[0]

    def add(self, a, b):
        m = self.n('ShaderNodeVectorMath', operation='ADD')
        self._set(m.inputs[0], a)
        self._set(m.inputs[1], b)
        return m.outputs[0]

    def dot(self, vec):
        m = self.n('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        self.L(self.dir, m.inputs[0])
        m.inputs[1].default_value = vec
        return m.outputs['Value']

    def emit(self, color):
        bg = self.n('ShaderNodeBackground')
        self._set(bg.inputs['Color'], color)
        bg.inputs['Strength'].default_value = 1.0
        self.L(bg.outputs[0], self.out.inputs['Surface'])


def nebula(world, palette, band_normal, density, dust):
    w = W(world)
    # big soft clouds, shaped by a second noise and coloured by a third
    big = w.noise(1.1, 8, 0.62, distortion=0.7)
    shape = w.noise(2.6, 6, 0.55, distortion=1.4, offset=(5.1, 2.3, 7.7))
    tint = w.noise(0.8, 2, 0.5, offset=(11.0, 3.0, 1.0))
    clouds = w.math('MULTIPLY', w.remap(big.outputs['Factor'], 0.48 - density * 0.1, 0.78), w.remap(shape.outputs['Factor'], 0.3, 0.8, 0.35, 1.0))
    cloud_col = w.ramp(tint.outputs['Factor'], palette['clouds'])
    color = w.scale_color(cloud_col, w.math('MULTIPLY', clouds, palette['cloud_strength']))
    # a galactic band along a great circle, with dark dust lanes and bright clusters
    d = w.dot(band_normal)
    band = w.math('EXPONENT', w.math('MULTIPLY', w.math('POWER', w.math('DIVIDE', d, 0.22), 2.0), -1.0))
    lanes = w.noise(5.0, 8, 0.6, distortion=2.0, offset=(3.3, 9.1, 4.4))
    lane_mask = w.remap(lanes.outputs['Factor'], 0.5, 0.64, 1.0, 1.0 - dust)
    knots = w.noise(14.0, 4, 0.5, offset=(1.7, 7.7, 2.2))
    knot = w.math('POWER', w.remap(knots.outputs['Factor'], 0.55, 0.9), 3.0)
    band_light = w.math('MULTIPLY', w.math('MULTIPLY', band, lane_mask), w.math('ADD', 0.6, w.math('MULTIPLY', knot, 3.0)))
    color = w.add(color, w.scale_color(palette['band'], w.math('MULTIPLY', band_light, palette['band_strength'])))
    # deep space floor
    color = w.add(color, palette['space'])
    w.emit(color)


def ocean_planet(world):
    w = W(world)
    land_n = w.noise(1.8, 12, 0.58, distortion=0.3)
    land = w.remap(land_n.outputs['Factor'], 0.52, 0.56)
    tone = w.noise(6.0, 8, 0.6, offset=(4, 4, 4))
    land_col = w.ramp(tone.outputs['Factor'], [(0.3, (0.16, 0.14, 0.1)), (0.55, (0.25, 0.24, 0.17)), (0.8, (0.35, 0.3, 0.24))])
    sea_col = w.ramp(land_n.outputs['Factor'], [(0.3, (0.01, 0.04, 0.1)), (0.52, (0.03, 0.12, 0.22))])
    mix = w.n('ShaderNodeMix', data_type='RGBA')
    w._set(mix.inputs[0], land)
    w._set(mix.inputs[6], sea_col)
    w._set(mix.inputs[7], land_col)
    color = mix.outputs[2]
    # ice caps
    sep = w.n('ShaderNodeSeparateXYZ')
    w.L(w.dir, sep.inputs[0])
    cap_edge = w.noise(4.0, 6, 0.6, offset=(2, 7, 5))
    lat = w.math('ADD', w.math('ABSOLUTE', sep.outputs[2]), w.math('MULTIPLY', w.math('SUBTRACT', cap_edge.outputs['Factor'], 0.5), 0.12))
    cap = w.remap(lat, 0.93, 0.95)
    mix2 = w.n('ShaderNodeMix', data_type='RGBA')
    w._set(mix2.inputs[0], cap)
    w._set(mix2.inputs[6], color)
    mix2.inputs[7].default_value = (0.75, 0.8, 0.85, 1.0)
    # swirling clouds
    swirl = w.noise(2.2, 10, 0.6, distortion=0.9, offset=(9, 1, 3))
    wisp = w.noise(7.0, 6, 0.55, distortion=0.4, offset=(1, 6, 2))
    cover = w.math('MULTIPLY', swirl.outputs['Factor'], w.remap(wisp.outputs['Factor'], 0.3, 0.7, 0.75, 1.15))
    cloud = w.remap(cover, 0.5, 0.7, 0.0, 0.85)
    mix3 = w.n('ShaderNodeMix', data_type='RGBA')
    w._set(mix3.inputs[0], cloud)
    w._set(mix3.inputs[6], mix2.outputs[2])
    mix3.inputs[7].default_value = (0.85, 0.88, 0.9, 1.0)
    w.emit(mix3.outputs[2])


def gas_giant(world):
    w = W(world)
    sep = w.n('ShaderNodeSeparateXYZ')
    w.L(w.dir, sep.inputs[0])
    # turbulence stretched along the bands (the direction squashed in latitude before the noise)
    flat = w.n('ShaderNodeVectorMath', operation='MULTIPLY')
    w.L(w.dir, flat.inputs[0])
    flat.inputs[1].default_value = (1.0, 1.0, 5.0)
    warp = w.noise(2.2, 8, 0.62, distortion=1.6, vector=flat.outputs[0])
    lat = w.math('ADD', sep.outputs[2], w.math('MULTIPLY', w.math('SUBTRACT', warp.outputs['Factor'], 0.5), 0.07))
    # band values from a noise along latitude only: uneven widths and tones
    line = w.n('ShaderNodeCombineXYZ')
    w._set(line.inputs[2], w.math('MULTIPLY', lat, 6.0))
    band_n = w.noise(1.0, 5, 0.55, vector=line.outputs[0])
    fine = w.math('SINE', w.math('MULTIPLY', lat, 95.0))
    v = w.math('ADD', w.remap(band_n.outputs['Factor'], 0.3, 0.7), w.math('MULTIPLY', fine, 0.05))
    color = w.ramp(v, [(0.15, (0.2, 0.07, 0.025)), (0.4, (0.48, 0.22, 0.07)), (0.62, (0.76, 0.48, 0.2)), (0.85, (0.9, 0.74, 0.5))])
    # one big oval storm south of the equator, with a darker rim
    sq = w.n('ShaderNodeVectorMath', operation='MULTIPLY')
    w.L(w.dir, sq.inputs[0])
    sq.inputs[1].default_value = (1.0, 1.0, 2.4)
    nrm = w.n('ShaderNodeVectorMath', operation='NORMALIZE')
    w.L(sq.outputs[0], nrm.inputs[0])
    dot = w.n('ShaderNodeVectorMath', operation='DOT_PRODUCT')
    w.L(nrm.outputs[0], dot.inputs[0])
    dot.inputs[1].default_value = (0.62, 0.58, -0.53)
    storm = w.remap(dot.outputs['Value'], 0.975, 0.99)
    rim = w.math('SUBTRACT', w.remap(dot.outputs['Value'], 0.96, 0.975), storm, clamp=True)
    mix = w.n('ShaderNodeMix', data_type='RGBA')
    w._set(mix.inputs[0], storm)
    w._set(mix.inputs[6], color)
    mix.inputs[7].default_value = (0.62, 0.2, 0.07, 1.0)
    mix2 = w.n('ShaderNodeMix', data_type='RGBA')
    w._set(mix2.inputs[0], w.math('MULTIPLY', rim, 0.6))
    w.L(mix.outputs[2], mix2.inputs[6])
    mix2.inputs[7].default_value = (0.9, 0.78, 0.6, 1.0)
    w.emit(mix2.outputs[2])


def render_pano(path, width, height, samples=8):
    scene = bpy.context.scene
    cam = bpy.data.objects.get("Pano")
    if cam is None:
        cam = bpy.data.objects.new("Pano", bpy.data.cameras.new("Pano"))
        scene.collection.objects.link(cam)
    cam.data.type = 'PANO'
    cam.data.panorama_type = 'EQUIRECTANGULAR'
    # the image centre looks along Unity's +z (Blender -Y), up is Unity's +y (Blender +Z), the right edge
    # turns toward Unity's +x: the mapping SpaceSky.shader uses (u = 0.5 + atan2(x, z) / 2pi, v = 0.5 + asin(y) / pi)
    cam.rotation_euler = (math.radians(90.0), 0.0, math.radians(180.0))
    scene.camera = cam
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    geo.limit_threads(scene)
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    # JPEG: Unity compresses to 8-bit ASTC anyway; the dither keeps the dark gradients from banding
    scene.render.image_settings.file_format = 'JPEG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.image_settings.quality = 92
    scene.render.dither_intensity = 1.0
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


MAPS = {
    "Orbit": dict(
        palette=dict(
            clouds=[(0.2, (0.16, 0.05, 0.3)), (0.5, (0.04, 0.16, 0.26)), (0.8, (0.3, 0.06, 0.24))],
            cloud_strength=0.42, band=(0.36, 0.33, 0.42), band_strength=0.16, space=(0.0025, 0.0035, 0.008)),
        band_normal=(0.3, 0.55, 0.78), density=0.3, dust=0.75, planet=ocean_planet),
    "Belt": dict(
        palette=dict(
            clouds=[(0.2, (0.36, 0.07, 0.03)), (0.5, (0.16, 0.08, 0.06)), (0.8, (0.42, 0.18, 0.05))],
            cloud_strength=0.4, band=(0.42, 0.33, 0.26), band_strength=0.12, space=(0.004, 0.003, 0.003)),
        band_normal=(-0.5, 0.7, 0.2), density=0.15, dust=0.85, planet=gas_giant),
}


def main(preview=True):
    geo.reset()
    scene = bpy.context.scene
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    os.makedirs(export.TEXTURES, exist_ok=True)
    made = []
    for name, spec in MAPS.items():
        nebula(world, spec['palette'], spec['band_normal'], spec['density'], spec['dust'])
        path = os.path.join(export.TEXTURES, f"Sky_{name}_Nebula.jpg")
        render_pano(path, TEX, TEX // 2)
        made.append(path)
        spec['planet'](world)
        path = os.path.join(export.TEXTURES, f"Planet_{name}.jpg")
        render_pano(path, 2048, 1024, samples=4)
        made.append(path)
    if preview:
        from PIL import Image
        sheet = Image.new("RGB", (2048, 1024), (0, 0, 0))
        for i, p in enumerate(made):
            im = Image.open(p).convert("RGB")
            if "Nebula" in p:
                # brighten the dark sky for the preview only
                im = im.resize((1024, 512)).point(lambda v: min(255, int(v * 2.2)))
                sheet.paste(im, ((i // 2) * 1024, 0))
            else:
                sheet.paste(im.resize((1024, 512)), ((i // 2) * 1024, 512))
        sheet.save(os.path.join(export.PREVIEWS, "Sky.jpg"), quality=88)
    print("Sky: " + ", ".join(os.path.basename(p) for p in made))
    return (0, 0, 0)


if __name__ == "__main__":
    main()
