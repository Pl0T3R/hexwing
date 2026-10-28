"""
Mesh building for hard-surface game assets (Blender 5.2, bpy + bmesh).

All coordinates in the model scripts are in Unity's frame: metres, x right, y up, z forward (the nose).
Each primitive is built in that frame and converted to Blender's (x, y, z) = (-x_u, -z_u, y_u), the inverse
of what the FBX export (export.py: forward -Z, up Y) plus Unity's import do, so the model lands in Unity
exactly as written. The conversion mirrors, so faces are flipped back after it.

Every part is its own object while building, tagged with a material slot name; parts may overlap
(normal for game meshes: the baked AO hides the joins). join() merges them at the end.
"""
import math

import bpy  # noqa: F401  (first: it makes bmesh and mathutils importable)
import bmesh
from mathutils import Euler, Matrix, Vector

# Unity (x right, y up, z forward) -> Blender
TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def to_blender(p):
    return (TO_BLENDER @ Vector((p[0], p[1], p[2], 1.0))).to_3d()


def limit_threads(scene=None):
    """
    Renders and bakes use at most HEXWING_CPU_SHARE of the cores (default 0.8: 3 of 4), so the
    machine stays usable while assets build.
    """
    import os
    scene = scene or bpy.context.scene
    share = float(os.environ.get("HEXWING_CPU_SHARE", "0.8"))
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = max(1, int((os.cpu_count() or 1) * share))


def reset():
    """An empty scene in metres, Cycles on the CPU, thread-limited."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    limit_threads(scene)
    return scene


def _object(name, bm, slot, matrix=None):
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
    bmesh.ops.transform(bm, matrix=TO_BLENDER, verts=bm.verts)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob["slot"] = slot
    return ob


def rot(x=0.0, y=0.0, z=0.0):
    """A rotation in the Unity frame (degrees about x, y, z, applied in that order)."""
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_matrix().to_4x4()


def at(position, rotation=None):
    m = Matrix.Translation(Vector(position))
    return m @ rotation if rotation is not None else m


def superellipse(width, height, count=16, power=4.0, flat_bottom=0.0, top=1.0):
    """
    A closed cross-section in the x/y plane: power 2 = ellipse, higher = boxier. flat_bottom 0..1
    squashes the lower half (a flatter belly); top scales the upper half.
    """
    pts = []
    for i in range(count):
        a = 2.0 * math.pi * i / count
        c, s = math.cos(a), math.sin(a)
        x = math.copysign(abs(c) ** (2.0 / power), c) * width * 0.5
        y = math.copysign(abs(s) ** (2.0 / power), s) * height * 0.5
        y *= top if y > 0 else (1.0 - flat_bottom)
        pts.append((x, y))
    return pts


def polygon(width, height, sides, rotate_deg=0.0):
    """A regular polygon cross-section (hexagonal pods and the like)."""
    pts = []
    for i in range(sides):
        a = math.radians(rotate_deg) + 2.0 * math.pi * i / sides
        pts.append((math.cos(a) * width * 0.5, math.sin(a) * height * 0.5))
    return pts


def loft(name, sections, slot, cap_start=True, cap_end=True, matrix=None):
    """
    A hull through cross-sections along z: sections = [(z, [(x, y), ...]) or (z, profile, (dx, dy), scale)].
    All profiles need the same point count. Caps are flat n-gons (beveled later).
    """
    bm = bmesh.new()
    rings = []
    for sec in sections:
        z, prof = sec[0], sec[1]
        dx, dy = sec[2] if len(sec) > 2 else (0.0, 0.0)
        k = sec[3] if len(sec) > 3 else 1.0
        rings.append([bm.verts.new((x * k + dx, y * k + dy, z)) for x, y in prof])
    n = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], b[i], b[j], a[j]))
    if cap_start:
        bm.faces.new(rings[0])
    if cap_end:
        bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _object(name, bm, slot, matrix)


def slab(name, outline, thickness, slot, taper=1.0, matrix=None):
    """
    A flat part (wing, fin, blade, panel) from a 2D outline in the x/z plane, extruded in y around 0.
    taper < 1 thins it toward the end farthest from x = 0, like a real wing.
    """
    bm = bmesh.new()
    xs = [abs(p[0]) for p in outline]
    x0, span = min(xs), (max(xs) - min(xs)) or 1.0
    top, bot = [], []
    for x, z in outline:
        h = thickness * 0.5 * (1.0 - (1.0 - taper) * (abs(x) - x0) / span)
        top.append(bm.verts.new((x, h, z)))
        bot.append(bm.verts.new((x, -h, z)))
    n = len(outline)
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _object(name, bm, slot, matrix)


def box(name, center, size, slot, rotation=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    return _object(name, bm, slot, at(center, rotation))


def cylinder(name, p0, p1, r0, r1, slot, sides=16, cap0=True, cap1=True):
    """A (truncated) cone from p0 to p1 with radii r0 and r1."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    bm = bmesh.new()
    rings = []
    for z, r in ((0.0, r0), (axis.length, r1)):
        rings.append([bm.verts.new((math.cos(2 * math.pi * i / sides) * r, math.sin(2 * math.pi * i / sides) * r, z))
                      for i in range(sides)])
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
    if cap0:
        bm.faces.new(list(reversed(rings[0])))
    if cap1:
        bm.faces.new(rings[1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    q = axis.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    return _object(name, bm, slot, Matrix.Translation(p0) @ q)


def tube(name, p0, p1, r_out, r_in, slot, sides=16, flare=1.0):
    """An open thick ring along p0 -> p1 (engine nozzle): outer wall, back lip, inner wall, front lip.
    flare > 1 widens the p1 end."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    L = axis.length
    bm = bmesh.new()
    spec = ((0.0, r_out), (L, r_out * flare), (L, r_in * flare), (0.0, r_in))
    rings = [[bm.verts.new((math.cos(2 * math.pi * i / sides) * r, math.sin(2 * math.pi * i / sides) * r, z))
              for i in range(sides)] for z, r in spec]
    for a, b in zip(rings, rings[1:] + rings[:1]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    q = axis.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    return _object(name, bm, slot, Matrix.Translation(p0) @ q)


def disc(name, center, normal, radius, slot, sides=16):
    """A flat round face facing along normal (engine glow, light lens)."""
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, radius=radius, segments=sides)
    q = Vector(normal).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    for f in bm.faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    return _object(name, bm, slot, Matrix.Translation(Vector(center)) @ q)


def mirror_x(ob):
    """Adds the mirrored copy (across the ship's centre plane) of a part into the same object."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    copy = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
    verts = [g for g in copy['geom'] if isinstance(g, bmesh.types.BMVert)]
    faces = [g for g in copy['geom'] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.scale(bm, vec=(-1.0, 1.0, 1.0), verts=verts)   # Unity x is Blender -x: same plane
    bmesh.ops.reverse_faces(bm, faces=faces)
    bm.to_mesh(me)
    bm.free()
    return ob


def bevel(ob, width=0.03, segments=2, angle=35.0, harden=True):
    """Chamfers the hard edges (so they catch light) and keeps flat faces flat (weighted normals)."""
    for poly in ob.data.polygons:
        poly.use_smooth = True
    m = ob.modifiers.new("Bevel", 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle)
    m.harden_normals = harden
    m.miter_outer = 'MITER_ARC'
    m.profile = 0.7
    _apply(ob, m)
    w = ob.modifiers.new("WeightedNormal", 'WEIGHTED_NORMAL')
    w.keep_sharp = True
    w.mode = 'FACE_AREA'
    _apply(ob, w)
    return ob


def _apply(ob, mod):
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def join(parts, name):
    """Merges parts into one object, one material (named after the slot) per slot."""
    for p in parts:
        mat = bpy.data.materials.get(p["slot"]) or bpy.data.materials.new(p["slot"])
        p.data.materials.clear()
        p.data.materials.append(mat)
    target = parts[0]
    with bpy.context.temp_override(active_object=target, object=target, selected_objects=parts, selected_editable_objects=parts):
        bpy.ops.object.join()
    target.name = name
    target.data.name = name
    return target


def socket(name, position, parent=None):
    """An empty the game reads by name (gun muzzles, screens, engine, attachment points)."""
    e = bpy.data.objects.new("SOCKET_" + name, None)
    e.empty_display_size = 0.1
    e.location = to_blender(position)
    bpy.context.scene.collection.objects.link(e)
    if parent is not None:
        e.parent = parent
    return e


def triangles(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate(ob, ratio, name):
    """A lower LOD copy (collapse decimation keeps UVs and the overall shape)."""
    copy = ob.copy()
    copy.data = ob.data.copy()
    copy.name = name
    copy.data.name = name
    bpy.context.scene.collection.objects.link(copy)
    m = copy.modifiers.new("Decimate", 'DECIMATE')
    m.ratio = ratio
    m.use_collapse_triangulate = True
    _apply(copy, m)
    return copy


def from_bmesh(name, bm, slot, matrix=None):
    """An object from a bmesh built in the Unity frame (the orientation of its faces is kept)."""
    return _object(name, bm, slot, matrix)


def sweep(name, path, profile, slot, up=None, closed_path=False, caps=True):
    """
    Sweeps a closed 2D profile [(a, b), ...] along a 3D path (Unity frame). At each point the profile's
    b axis points along up (a vector, or a function of the point), its a axis across the path; up
    defaults to world y. Beams along the cockpit shell, frame rails, cables and pipes.
    """
    pts = [Vector(p) for p in path]
    n = len(pts)
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(pts):
        if closed_path:
            t = (pts[(i + 1) % n] - pts[i - 1]).normalized()
        else:
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        u = Vector(up(p) if callable(up) else (up or (0.0, 1.0, 0.0)))
        b = (u - t * u.dot(t))
        if b.length < 1e-5:
            b = Vector((1.0, 0.0, 0.0)) - t * t.x
        b.normalize()
        a = b.cross(t).normalized()
        rings.append([bm.verts.new(p + a * pa + b * pb) for pa, pb in profile])
    m = len(profile)
    pairs = list(zip(rings, rings[1:] + ([rings[0]] if closed_path else [])))
    for r0, r1 in pairs:
        for j in range(m):
            k = (j + 1) % m
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))
    if caps and not closed_path:
        bm.faces.new(rings[0])
        bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _object(name, bm, slot)


def rect(w, h):
    """A rectangular profile for sweep() (w across, h along up)."""
    return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]


def circle(r, sides=8):
    return [(math.cos(2 * math.pi * i / sides) * r, math.sin(2 * math.pi * i / sides) * r) for i in range(sides)]


def look_matrix(center, forward, up=(0.0, 1.0, 0.0)):
    """Like Unity's LookRotation at a point: local z = forward, y close to up, x = right."""
    f = Vector(forward).normalized()
    r = Vector(up).cross(f).normalized()
    u = f.cross(r)
    m = Matrix((
        (r.x, u.x, f.x, center[0]),
        (r.y, u.y, f.y, center[1]),
        (r.z, u.z, f.z, center[2]),
        (0.0, 0.0, 0.0, 1.0)))
    return m


def tag_part(ob, value):
    """Marks every face of ob with a part number (kept through join), for split_parts after a bake."""
    me = ob.data
    attr = me.attributes.get("part") or me.attributes.new("part", 'INT', 'FACE')
    attr.data.foreach_set('value', [value] * len(me.polygons))


def split_parts(ob, names):
    """Splits a joined, baked object back into one object per part number (names[i] for part i)."""
    out = []
    for value, name in enumerate(names):
        copy = ob.copy()
        copy.data = ob.data.copy()
        copy.name = name
        copy.data.name = name
        bpy.context.scene.collection.objects.link(copy)
        me = copy.data
        part = me.attributes["part"].data
        keep = [part[i].value == value for i in range(len(me.polygons))]
        for v in me.vertices:
            v.select = False
        for e in me.edges:
            e.select = False
        for poly, k in zip(me.polygons, keep):
            poly.select = not k
            if not k:
                for vi in poly.vertices:
                    me.vertices[vi].select = True
        # edit-mode operators act on the view layer's active object: make it this copy alone
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        bpy.context.view_layer.objects.active = copy
        copy.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_mode(type='FACE')
        bpy.ops.mesh.delete(type='FACE')
        bpy.ops.object.mode_set(mode='OBJECT')
        out.append(copy)
    bpy.data.objects.remove(ob)
    bpy.context.view_layer.update()
    return out


def xf(matrix, point):
    """A point moved by a Unity-frame matrix (to place small parts on rotated ones)."""
    return tuple((matrix @ Vector((point[0], point[1], point[2], 1.0)))[:3])


def xd(matrix, direction):
    """A direction turned by a Unity-frame matrix."""
    return tuple((matrix.to_3x3() @ Vector(direction))[:])


def apply_transform(ob):
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def recenter(ob, offset):
    """Moves the mesh data by -offset (Blender frame), so a part spread out for baking sits at the origin again."""
    me = ob.data
    off = Vector(offset)
    for v in me.vertices:
        v.co -= off
    me.update()
