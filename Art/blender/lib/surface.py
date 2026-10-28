"""
Procedural hard-surface materials that are baked into texture atlases (bake.py).

Each material slot (a name on the parts, see geo.py) gets a look from a palette entry:
- base colour, metalness and roughness,
- panels: a triplanar brick pattern in object space gives panel lines (dark, recessed in the
  normal map) and a slight tone change from panel to panel,
- edge wear: convex edges (inside ambient occlusion) show brighter bare metal,
- grime: cavities (ambient occlusion) and large noise patches darken and roughen,
- emission: glowing parts (engines, lights, screens) keep their colour and get a mask value.

Every material has three outputs the baker switches between: the colour, the mask
(R metal, G smoothness, B emission) and the full Principled BSDF (for the normal bake and previews).
"""
import bpy

DEFAULT = dict(
    base=(0.2, 0.2, 0.22), metal=0.6, rough=0.45, emit=0.0, emit_color=None,
    panel=1.0, panel_scale=0.8, panel_dark=0.6, tone=0.08,
    wear=0.3, wear_color=(0.32, 0.33, 0.35), grime=0.45, bevel=0.012, bump=0.5,
)


def _rgba(c):
    return (c[0], c[1], c[2], 1.0)


class _G:
    """Tiny node-graph helper."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.x = 0

    def n(self, kind, **props):
        node = self.nt.nodes.new(kind)
        node.location = (self.x, 0)
        self.x += 180
        for k, v in props.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    def math(self, op, a, b=None, clamp=False):
        m = self.n('ShaderNodeMath', operation=op, use_clamp=clamp)
        self._in(m.inputs[0], a)
        if b is not None:
            self._in(m.inputs[1], b)
        return m.outputs[0]

    def mix(self, fac, a, b):
        m = self.n('ShaderNodeMix', data_type='RGBA')
        self._in(m.inputs[0], fac)
        self._in(m.inputs[6], a)
        self._in(m.inputs[7], b)
        return m.outputs[2]

    def _in(self, socket, value):
        if isinstance(value, bpy.types.NodeSocket):
            self.link(value, socket)
        elif isinstance(value, (tuple, list)) and len(value) == 3:
            socket.default_value = _rgba(value)
        else:
            socket.default_value = value


def build(mat, spec):
    """Builds the node graph of one slot material from its palette entry (dict over DEFAULT)."""
    p = dict(DEFAULT)
    p.update(spec)
    g = _G(mat)
    out = g.n('ShaderNodeOutputMaterial', target='CYCLES')
    coord = g.n('ShaderNodeTexCoord').outputs['Object']
    geo = g.n('ShaderNodeNewGeometry')
    sep = g.n('ShaderNodeSeparateXYZ')
    g.link(coord, sep.inputs[0])

    # --- panels: three brick patterns, one per axis plane, blended by the normal (triplanar) ---
    nsep = g.n('ShaderNodeSeparateXYZ')
    g.link(geo.outputs['Normal'], nsep.inputs[0])
    weights = []
    for i in range(3):
        a = g.math('ABSOLUTE', nsep.outputs[i])
        weights.append(g.math('POWER', a, 4.0))
    wsum = g.math('ADD', g.math('ADD', weights[0], weights[1]), weights[2])
    split_noise = g.n('ShaderNodeTexNoise')
    g.link(coord, split_noise.inputs['Vector'])
    split_noise.inputs['Scale'].default_value = 0.9
    split_noise.inputs['Detail'].default_value = 0.0
    split = g.n('ShaderNodeMapRange')
    g.link(split_noise.outputs['Factor'], split.inputs['Value'])
    split.inputs['From Min'].default_value = 0.5
    split.inputs['From Max'].default_value = 0.52
    line, tone = None, None
    planes = ((1, 2), (0, 2), (0, 1))   # the plane seen along X, Y, Z
    for i, (u, v) in enumerate(planes):
        comb = g.n('ShaderNodeCombineXYZ')
        g.link(sep.outputs[u], comb.inputs[0])
        g.link(sep.outputs[v], comb.inputs[1])
        w = g.math('DIVIDE', weights[i], wsum)
        layer_line = None
        for k, (scale, width, height) in enumerate(((p['panel_scale'], 1.3, 0.55), (p['panel_scale'] * 2.3, 0.8, 0.6))):
            brick = g.n('ShaderNodeTexBrick', offset=0.37 + 0.2 * k, offset_frequency=2, squash=0.8, squash_frequency=3)
            g.link(comb.outputs[0], brick.inputs['Vector'])
            brick.inputs['Scale'].default_value = scale
            brick.inputs['Mortar Size'].default_value = 0.006 if k == 0 else 0.008
            brick.inputs['Mortar Smooth'].default_value = 0.3
            brick.inputs['Brick Width'].default_value = width
            brick.inputs['Row Height'].default_value = height
            brick.inputs['Bias'].default_value = 0.0
            brick.inputs['Color1'].default_value = (1.0 - p['tone'], 1.0 - p['tone'], 1.0 - p['tone'], 1.0)
            brick.inputs['Color2'].default_value = (1.0, 1.0, 1.0, 1.0)
            brick.inputs['Mortar'].default_value = (1.0, 1.0, 1.0, 1.0)
            fac = brick.outputs['Factor']
            if k == 0:
                layer_line = fac
                t_sep = g.n('ShaderNodeSeparateColor')
                g.link(brick.outputs['Color'], t_sep.inputs[0])
                t_i = g.math('MULTIPLY', t_sep.outputs[0], w)
                tone = t_i if tone is None else g.math('ADD', tone, t_i)
            else:
                layer_line = g.math('MAXIMUM', layer_line, g.math('MULTIPLY', fac, split.outputs['Result']))
        l_i = g.math('MULTIPLY', layer_line, w)
        line = l_i if line is None else g.math('ADD', line, l_i)
    line = g.math('MULTIPLY', line, p['panel'], clamp=True)

    # --- cavities and edges from ambient occlusion (inside AO finds convex edges) ---
    ao = g.n('ShaderNodeAmbientOcclusion', samples=16, only_local=True)
    ao.inputs['Distance'].default_value = 0.6
    edge_ao = g.n('ShaderNodeAmbientOcclusion', samples=16, inside=True, only_local=True)
    edge_ao.inputs['Distance'].default_value = 0.05
    edge = g.math('SUBTRACT', 1.0, edge_ao.outputs['AO'], clamp=True)
    edge = g.math('MULTIPLY', g.math('POWER', edge, 2.0), 1.6, clamp=True)
    noise = g.n('ShaderNodeTexNoise')
    g.link(coord, noise.inputs['Vector'])
    noise.inputs['Scale'].default_value = 0.6
    noise.inputs['Detail'].default_value = 6.0
    dirt = g.n('ShaderNodeMapRange')
    g.link(noise.outputs['Factor'], dirt.inputs['Value'])
    dirt.inputs['From Min'].default_value = 0.45
    dirt.inputs['From Max'].default_value = 0.75
    cavity = g.math('SUBTRACT', 1.0, ao.outputs['AO'], clamp=True)
    grime = g.math('MULTIPLY', g.math('MAXIMUM', g.math('MULTIPLY', cavity, 1.6), g.math('MULTIPLY', dirt.outputs['Result'], 0.5)), p['grime'], clamp=True)
    wear_mask = g.math('MULTIPLY', g.math('MULTIPLY', edge, p['wear']), g.math('SUBTRACT', 1.0, g.math('MULTIPLY', grime, 0.7)), clamp=True)

    # --- colour ---
    mul = g.n('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY')
    mul.inputs[0].default_value = 1.0
    mul.inputs[6].default_value = _rgba(p['base'])
    tone_rgb = g.n('ShaderNodeCombineColor')
    for k in range(3):
        g.link(tone, tone_rgb.inputs[k])
    g.link(tone_rgb.outputs[0], mul.inputs[7])
    color = mul.outputs[2]
    color = g.mix(line, color, tuple(c * p['panel_dark'] for c in p['base']))
    color = g.mix(grime, color, tuple(c * 0.45 for c in p['base']))
    color = g.mix(wear_mask, color, p['wear_color'])
    if p['emit'] > 0 and p['emit_color'] is not None:
        color = g.mix(1.0, color, p['emit_color'])

    metal = g.math('ADD', p['metal'], g.math('MULTIPLY', wear_mask, 1.0 - p['metal']), clamp=True)
    rough = g.math('ADD', p['rough'], g.math('SUBTRACT', g.math('MULTIPLY', grime, 0.25), g.math('MULTIPLY', wear_mask, 0.15)), clamp=True)
    rough = g.math('ADD', rough, g.math('MULTIPLY', line, 0.2), clamp=True)
    smooth = g.math('SUBTRACT', 1.0, rough, clamp=True)

    # --- normal: rounded edges (bevel) plus recessed panel lines (bump) ---
    bev = g.n('ShaderNodeBevel', samples=8)
    bev.inputs['Radius'].default_value = p['bevel']
    bump = g.n('ShaderNodeBump')
    bump.inputs['Strength'].default_value = p['bump']
    bump.inputs['Distance'].default_value = 0.003
    g.link(g.math('SUBTRACT', 1.0, line), bump.inputs['Height'])
    g.link(bev.outputs['Normal'], bump.inputs['Normal'])

    bsdf = g.n('ShaderNodeBsdfPrincipled')
    g.link(color, bsdf.inputs['Base Color'])
    g.link(metal, bsdf.inputs['Metallic'])
    g.link(rough, bsdf.inputs['Roughness'])
    g.link(bump.outputs['Normal'], bsdf.inputs['Normal'])
    if p['emit'] > 0:
        g.link(color, bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = 4.0 * p['emit']

    emit_color = g.n('ShaderNodeEmission')
    g.link(color, emit_color.inputs['Color'])
    mask = g.n('ShaderNodeCombineColor')
    g.link(metal, mask.inputs['Red'])
    g.link(smooth, mask.inputs['Green'])
    mask.inputs['Blue'].default_value = p['emit']
    emit_mask = g.n('ShaderNodeEmission')
    g.link(mask.outputs[0], emit_mask.inputs['Color'])

    img = g.n('ShaderNodeTexImage')
    img.name = 'BakeTarget'
    g.nt.nodes.active = img

    g.link(bsdf.outputs[0], out.inputs['Surface'])
    _remember(mat, bsdf, emit_color, emit_mask, out)
    return mat


_OUTPUTS = {}


def _remember(mat, bsdf, color, mask, out):
    _OUTPUTS[mat.name] = (bsdf, color, mask, out)


def show(mat, which):
    """Points the material output at 'bsdf', 'color' or 'mask'."""
    bsdf, color, mask, out = _OUTPUTS[mat.name]
    src = {'bsdf': bsdf, 'color': color, 'mask': mask}[which]
    mat.node_tree.links.new(src.outputs[0], out.inputs['Surface'])


def make_all(palette):
    """One material per palette slot name."""
    mats = {}
    for name, spec in palette.items():
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mats[name] = build(mat, spec)
    return mats
