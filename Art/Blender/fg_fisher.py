# FishGame player character ("the fisher"), built from text so it is reproducible.
#
# Run (headless):  blender -b --python Art/Blender/fg_fisher.py -- <stage> [outdir]
#   stages: model   build the meshes and save Art/Blender/fishgame_fisher.blend
#           preview render a turnaround, a face close-up and a skin-colour lineup into outdir
#
# Authoring axes: Blender Z up, the character faces -Y, feet on z = 0, metres. The FBX export
# (-Z forward, Y up, bake_space_transform) turns that into Unity's Y up, facing +Z.
#
# Look (user 2026-10-17, references checked: PEAK's scouts, RV There Yet's dads, REPO's side-eye):
# - PEAK: a big round head straight on a short body, colourful skin (the skin is the player's colour pick),
#   a flat painted face (cream eye ovals, big navy pupils, blush, a small line mouth), noodle arms with
#   mitten hands, shorts, tall socks and chunky boots.
# - RV There Yet: stubby, a dad belly, dad-fisher clothes (fishing vest with lure pins, bucket hat).
# - REPO: only the behaviour. The pupils are separate flat discs on the head sphere; the rig turns them
#   about the head centre so they slide over the eye whites (side-eye / look-at).
#
# The head is a true sphere (HEAD_C, HEAD_R) so face decals (eyes, pupils, blush, mouth, and every
# swappable face part in fg_fisher_parts.py) map onto it exactly with sphere_point(). Body shapes are
# metaballs (stiffness 2, threshold 0.6: the surface sits at 0.575 x the element radius), so overlapping
# parts blend into one soft surface; each is converted to a mesh, voxel remeshed and decimated.

import bpy, bmesh, math, os, sys, tempfile
from mathutils import Vector, Quaternion, Euler, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(HERE, 'fishgame_fisher.blend')
SURF = 0.575  # metaball surface radius / element radius at stiffness 2, threshold 0.6

# ---------------------------------------------------------------- palette (preview colours)
COL = {
    'skin':   (0.98, 0.52, 0.18),   # PEAK-style bright skin; the player's colour pick in game
    'blush':  (0.90, 0.30, 0.16),
    'shirt':  (0.92, 0.88, 0.74),   # cream short-sleeve shirt
    'vest':   (0.52, 0.50, 0.33),   # khaki-olive fishing vest
    'shorts': (0.70, 0.58, 0.40),   # cargo shorts
    'socks':  (0.95, 0.94, 0.90),
    'stripe': (0.20, 0.45, 0.70),
    'boots':  (0.42, 0.24, 0.13),   # brown leather
    'sole':   (0.20, 0.15, 0.12),
    'hat':    (0.62, 0.58, 0.40),
    'strap':  (0.33, 0.24, 0.16),
    'eye':    (0.99, 0.96, 0.86),   # cream eye white
    'pupil':  (0.04, 0.05, 0.16),   # navy
    'mouth':  (0.16, 0.05, 0.08),
    'lure_r': (0.85, 0.12, 0.10),
    'lure_w': (0.97, 0.97, 0.95),
    'metal':  (0.75, 0.75, 0.72),
}
SKINS = [(0.98, 0.52, 0.18), (0.30, 0.75, 0.30), (0.45, 0.40, 0.90), (0.25, 0.70, 0.80)]  # lineup render

# ---------------------------------------------------------------- key heights (z, metres): stubby, ~1.6 m to the head top
HEAD_C = (0.0, 0.0, 1.37)
HEAD_R = 0.262
H = dict(
    head=HEAD_C[2], head_r=HEAD_R,
    eye_z=HEAD_C[2] - 0.005,
    shoulder=1.05,
    chest=0.95,
    belly=0.76,
    hip=0.56,
    shorts_hem=0.36,
    knee=0.33,
    sock_top=0.30,
    ankle=0.11,
)
# Face layout in metres along the head surface from the face centre (u right, v up), for sphere_point().
FACE = dict(
    eye_u=0.092, eye_v=-0.030, eye_w=0.086, eye_h=0.100,
    pupil_w=0.050, pupil_h=0.060,
    blush_u=0.150, blush_v=-0.088, blush_w=0.075, blush_h=0.042,
    mouth_v=-0.112, mouth_w=0.064, mouth_curve=0.016, mouth_thick=0.010,
)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.unit_settings.system = 'METRIC'
    s.unit_settings.scale_length = 1.0


def material(name, rgb, rough=0.75):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*rgb, 1.0)
    b.inputs['Roughness'].default_value = rough
    return m


# ---------------------------------------------------------------- metaball helpers
def ball(c, r, s=(1, 1, 1), rot=(0, 0, 0)):
    """An element whose *surface* is an ellipsoid of radius r scaled by s (rot in degrees, XYZ)."""
    return ('ELLIPSOID', Vector(c), r / SURF, Vector(s), Euler([math.radians(a) for a in rot]).to_quaternion())


def capsule(a, b, r):
    """A capsule from point a to point b with surface radius r."""
    a, b = Vector(a), Vector(b)
    d = b - a
    q = Vector((1, 0, 0)).rotation_difference(d.normalized())
    # Blender's capsule: size_x is the half-length of the straight part along local X.
    return ('CAPSULE', (a + b) / 2, r / SURF, Vector((d.length / 2, 1, 1)), q)


def blob(name, elems, mat, res=0.012, voxel=0.010, faces=None, smooth_iter=0):
    mb = bpy.data.metaballs.new('MB_' + name)
    mb.resolution = mb.render_resolution = res
    mb.threshold = 0.6
    for kind, co, rad, size, q in elems:
        e = mb.elements.new()
        e.type = kind
        e.co = co
        e.radius = rad
        e.stiffness = 2.0
        e.rotation = q
        if kind == 'ELLIPSOID':
            e.size_x, e.size_y, e.size_z = size   # ellipsoid size scales the radius
        elif kind == 'CAPSULE':
            e.size_x = size.x
    tmp = bpy.data.objects.new('MB_' + name, mb)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.metaballs.remove(mb)
    me.name = name
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    finish(ob, mat, voxel, faces, smooth_iter)
    return ob


def finish(ob, mat, voxel=0.010, faces=None, smooth_iter=0):
    """Voxel remesh for even topology, optional smoothing, decimate to a face budget, smooth shading."""
    ctx_select(ob)
    if voxel:
        m = ob.modifiers.new('remesh', 'REMESH')
        m.mode = 'VOXEL'
        m.voxel_size = voxel
        m.adaptivity = 0.0
        bpy.ops.object.modifier_apply(modifier=m.name)
    if smooth_iter:
        m = ob.modifiers.new('smooth', 'CORRECTIVE_SMOOTH')
        m.iterations = smooth_iter
        m.smooth_type = 'SIMPLE'
        m.use_only_smooth = True
        bpy.ops.object.modifier_apply(modifier=m.name)
    if faces and len(ob.data.polygons) > faces:
        m = ob.modifiers.new('dec', 'DECIMATE')
        m.ratio = faces / len(ob.data.polygons)
        bpy.ops.object.modifier_apply(modifier=m.name)
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.clear()
    ob.data.materials.append(mat)


def ctx_select(ob):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob


def mirror_x(elems):
    """Return elems plus their mirror across x = 0."""
    out = list(elems)
    for kind, co, rad, size, q in elems:
        if abs(co.x) < 1e-6:
            continue   # on the centre line: mirroring would double its field and inflate the shape
        c = Vector((-co.x, co.y, co.z))
        e = q.to_euler()
        qm = Euler((e.x, -e.y, -e.z)).to_quaternion()
        out.append((kind, c, rad, size, qm))
    return out


def lathe(name, profile, mat, segs=48):
    """Revolve a (radius, z) profile around Z. Used for the bucket hat."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    # cap the top
    top = rings[-1]
    c = bm.verts.new((0, 0, profile[-1][1]))
    for i in range(segs):
        bm.faces.new((top[i], top[(i + 1) % segs], c))
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return ob


# ---------------------------------------------------------------- head and face decals
def sphere_point(u, v, lift=0.0):
    """A point on the head sphere, u/v metres right/up of the face centre (which faces -Y), lifted off it."""
    R = HEAD_R
    yaw, pitch = u / R, v / R
    d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    return Vector(HEAD_C) + d * (R + lift)


def decal(name, outline, mat, lift=0.002, rings=4):
    """A flat painted shape on the head: outline is a closed list of (u, v) points (metres on the
    surface). Filled with concentric rings toward its centroid so it bends with the sphere."""
    cu = sum(p[0] for p in outline) / len(outline)
    cv = sum(p[1] for p in outline) / len(outline)
    bm = bmesh.new()
    loops = []
    for k in range(rings, 0, -1):
        t = k / rings
        loops.append([bm.verts.new(sphere_point(cu + (u - cu) * t, cv + (v - cv) * t, lift)) for u, v in outline])
    centre = bm.verts.new(sphere_point(cu, cv, lift))
    n = len(outline)
    for a, b in zip(loops, loops[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    for i in range(n):
        bm.faces.new((loops[-1][i], loops[-1][(i + 1) % n], centre))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    # Normals should face out from the head.
    if (me.polygons[0].normal.dot(me.polygons[0].center - Vector(HEAD_C))) < 0:
        ctx_select(ob)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.flip_normals()
        bpy.ops.object.mode_set(mode='OBJECT')
    return ob


def ellipse(cu, cv, w, h, n=28, squash_top=0.0):
    """Outline of an ellipse (w, h are full sizes); squash_top flattens the upper half a little."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        y = math.sin(a) * h / 2
        if y > 0:
            y *= 1.0 - squash_top
        pts.append((cu + math.cos(a) * w / 2, cv + y))
    return pts


def stroke(points, thick):
    """Outline of a thick round-ended stroke along a polyline of (u, v) points."""
    left, right = [], []
    for i, (u, v) in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        du, dv = b[0] - a[0], b[1] - a[1]
        L = math.hypot(du, dv) or 1.0
        nu, nv = -dv / L * thick / 2, du / L * thick / 2
        left.append((u + nu, v + nv))
        right.append((u - nu, v - nv))
    return left + right[::-1]


def build_face(M):
    F = FACE
    for side, sx in (('L', 1), ('R', -1)):
        u = sx * F['eye_u']
        decal('EyeWhite_' + side, ellipse(u, F['eye_v'], F['eye_w'], F['eye_h']), M['eye'], lift=0.0015)
        decal('Pupil_' + side, ellipse(u, F['eye_v'] - 0.004, F['pupil_w'], F['pupil_h']), M['pupil'], lift=0.0030)
        decal('Blush_' + side, ellipse(sx * F['blush_u'], F['blush_v'], F['blush_w'], F['blush_h']), M['blush'], lift=0.0010)
    pts = [(t * F['mouth_w'] / 2, F['mouth_v'] + F['mouth_curve'] * t * t) for t in [i / 8 * 2 - 1 for i in range(9)]]
    decal('Mouth', stroke(pts, F['mouth_thick']), M['mouth'], lift=0.0015, rings=2)


# ---------------------------------------------------------------- the model
def build():
    reset()
    M = {k: material('M_' + k.capitalize(), v) for k, v in COL.items()}
    M['eye'] = material('M_Eye', COL['eye'], rough=0.6)
    M['boots'] = material('M_Boots', COL['boots'], rough=0.55)

    # Head: a true sphere, so face parts map onto it exactly.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=HEAD_R, location=HEAD_C)
    head = bpy.context.active_object
    head.name = 'Head'
    bpy.ops.object.transform_apply(location=True)
    finish(head, M['skin'], voxel=0, faces=None)
    # Little round ears so the hat has something to sit behind.
    blob('Ears', mirror_x([ball((HEAD_R * 0.97, 0.01, HEAD_C[2] - 0.02), 0.045, (0.5, 0.85, 1.0))]),
         M['skin'], voxel=0.006, faces=800, smooth_iter=2)

    sh, ch, be, hip = H['shoulder'], H['chest'], H['belly'], H['hip']
    # Shirt: a short barrel with an RV dad belly, rounded shoulders and short puffy sleeves.
    blob('Shirt', mirror_x([
        ball((0, 0.0, ch), 0.215, (1.0, 0.78, 0.82)),
        ball((0, -0.035, be), 0.215, (1.0, 0.92, 0.85)),                  # the belly
        ball((0, 0.0, hip + 0.10), 0.20, (1.02, 0.8, 0.6)),
        ball((0.17, 0.0, sh - 0.02), 0.085, (1.0, 1.0, 0.9)),
        capsule((0.20, 0.0, sh - 0.03), (0.29, -0.005, sh - 0.15), 0.083),  # sleeve
    ]), M['shirt'], voxel=0.010, faces=4000, smooth_iter=3)

    # Arms: thick noodles, no visible elbow, big mitten hands with a thumb (PEAK).
    blob('Arms', mirror_x([
        capsule((0.27, -0.005, sh - 0.12), (0.335, -0.02, sh - 0.36), 0.064),
        ball((0.35, -0.025, sh - 0.43), 0.080, (0.9, 0.7, 1.12)),
        ball((0.325, -0.075, sh - 0.40), 0.034, (0.9, 0.9, 1.25), (25, 0, 0)),
    ]), M['skin'], voxel=0.008, faces=2500, smooth_iter=3)

    # Legs: short; cargo shorts over the hips, bare skin, tall socks, chunky boots.
    blob('Shorts', mirror_x([
        ball((0, 0.0, hip + 0.04), 0.19, (1.04, 0.8, 0.5)),
        capsule((0.10, 0.0, hip), (0.11, -0.005, H['shorts_hem'] + 0.03), 0.112),
    ]), M['shorts'], voxel=0.010, faces=3000, smooth_iter=3)
    blob('Legs', mirror_x([
        capsule((0.105, 0.0, H['shorts_hem'] + 0.06), (0.105, 0.0, H['sock_top'] - 0.02), 0.075),
    ]), M['skin'], voxel=0.008, faces=1200, smooth_iter=2)
    blob('Socks', mirror_x([
        capsule((0.105, 0.0, H['sock_top']), (0.105, 0.0, H['ankle'] + 0.04), 0.080),
    ]), M['socks'], voxel=0.008, faces=1500, smooth_iter=2)
    blob('SockStripes', mirror_x([
        capsule((0.105, 0.0, H['sock_top'] - 0.025), (0.105, 0.0, H['sock_top'] - 0.045), 0.0815),
    ]), M['stripe'], voxel=0.006, faces=800, smooth_iter=1)
    blob('Boots', mirror_x([
        ball((0.105, -0.045, 0.075), 0.098, (1.0, 1.55, 0.78)),
        capsule((0.105, 0.0, 0.08), (0.105, 0.0, H['ankle'] + 0.07), 0.095),
    ]), M['boots'], voxel=0.010, faces=2500, smooth_iter=3)
    blob('Soles', mirror_x([
        ball((0.105, -0.045, 0.022), 0.10, (1.05, 1.6, 0.25)),
    ]), M['sole'], voxel=0.008, faces=1000, smooth_iter=2)

    build_vest(M)
    build_hat(M)
    build_face(M)

    col = bpy.data.collections.new('Fisher')
    bpy.context.scene.collection.children.link(col)
    for ob in list(bpy.context.scene.collection.objects):
        bpy.context.scene.collection.objects.unlink(ob)
        col.objects.link(ob)


def build_vest(M):
    """Fishing vest (RV dad, standing in for PEAK's badge sash): a shell over the shirt, open down the
    front, four flapped pockets and a few lure pins."""
    sh, ch, be = H['shoulder'], H['chest'], H['belly']
    ob = blob('Vest', mirror_x([
        ball((0, 0.0, ch + 0.005), 0.232, (1.0, 0.8, 0.84)),
        ball((0, -0.04, be + 0.02), 0.232, (1.0, 0.92, 0.8)),
        ball((0.16, 0.0, sh - 0.025), 0.075, (1.0, 1.0, 0.9)),
    ]), M['vest'], voxel=0.010, faces=None, smooth_iter=3)
    ctx_select(ob)
    m = ob.modifiers.new('thick', 'SOLIDIFY')
    m.thickness = 0.012
    m.offset = 1.0
    bpy.ops.object.modifier_apply(modifier=m.name)
    cutters = []
    bm = bmesh.new()
    z0, z1 = be - 0.25, sh + 0.12
    outline = [(-0.03, z0), (0.03, z0), (0.13, z1), (-0.13, z1)]
    vs = [bm.verts.new((x, y, z)) for y in (-0.6, -0.04) for x, z in outline]
    bm.faces.new(vs[:4]); bm.faces.new(vs[4:][::-1])
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((vs[i], vs[4 + i], vs[4 + j], vs[j]))
    cutters.append(bm)

    def cyl(loc, r, depth, rot, scale=(1, 1, 1)):
        b = bmesh.new()
        mt = Matrix.Translation(loc) @ Euler([math.radians(v) for v in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
        bmesh.ops.create_cone(b, cap_ends=True, segments=48, radius1=r, radius2=r, depth=depth, matrix=mt)
        return b
    cutters.append(cyl(Vector((0, 0.01, sh + 0.09)), 0.14, 0.2, (0, 0, 0), (1, 0.8, 1)))
    for sx in (1, -1):
        cutters.append(cyl(Vector((sx * 0.29, 0, sh - 0.075)), 0.11, 0.2, (0, 90, 0), (1.2, 0.85, 1)))
    hem = bmesh.new()
    bmesh.ops.create_cube(hem, size=1.0, matrix=Matrix.Translation((0, 0, be - 0.11 - 0.5)))
    cutters.append(hem)
    for i, cb in enumerate(cutters):
        me = bpy.data.meshes.new('VestCut%d' % i)
        cb.to_mesh(me)
        cb.free()
        co = bpy.data.objects.new('VestCut%d' % i, me)
        bpy.context.scene.collection.objects.link(co)
        ctx_select(ob)
        bo = ob.modifiers.new('cut', 'BOOLEAN')
        bo.operation = 'DIFFERENCE'
        bo.solver = 'EXACT'
        bo.object = co
        bpy.ops.object.modifier_apply(modifier=bo.name)
        bpy.data.objects.remove(co)
    ctx_select(ob)
    bev = ob.modifiers.new('soft', 'BEVEL')
    bev.width = 0.004
    bev.segments = 2
    bev.limit_method = 'ANGLE'
    bpy.ops.object.modifier_apply(modifier=bev.name)
    dec = ob.modifiers.new('dec', 'DECIMATE')
    dec.ratio = min(1.0, 4000 / max(1, len(ob.data.polygons)))
    bpy.ops.object.modifier_apply(modifier=dec.name)

    # Pockets: soft boxes with flaps, stuck to the vest surface.
    bm = bmesh.new()
    pins = []
    for x, z, w, h in ((0.105, ch + 0.0, 0.085, 0.075), (0.115, be + 0.035, 0.095, 0.08)):
        for sx in (1, -1):
            hit, loc, nrm, _ = ob.ray_cast(Vector((sx * x, -1.0, z)), Vector((0, 1, 0)))
            if not hit:
                continue
            for dz, hh, dy, d in ((0.0, h, 0.012, 0.028), (h * 0.36, h * 0.32, 0.020, 0.012)):
                mt = (Matrix.Translation(loc + Vector((0, -dy, dz))) @ nrm.to_track_quat('-Y', 'Z').to_matrix().to_4x4()
                      @ Matrix.Diagonal((w / 2 + 0.004 * (dz > 0), d / 2, hh / 2, 1)))
                bmesh.ops.create_cube(bm, size=2.0, matrix=mt)
            if z > ch - 0.01:
                pins.append((sx, loc, nrm))
    me = bpy.data.meshes.new('VestPockets')
    bm.to_mesh(me)
    bm.free()
    pk = bpy.data.objects.new('VestPockets', me)
    bpy.context.scene.collection.objects.link(pk)
    ctx_select(pk)
    bev = pk.modifiers.new('bev', 'BEVEL')
    bev.width = 0.009
    bev.segments = 3
    bpy.ops.object.modifier_apply(modifier=bev.name)
    pk.data.materials.append(M['vest'])
    for o in (ob, pk):
        for p in o.data.polygons:
            p.use_smooth = True

    # Lure pins above the chest pockets (the fisher's answer to PEAK's badge sash): a red-white bobber
    # on the left, a little silver spoon on the right.
    for sx, loc, nrm in pins:
        base = loc + Vector((0, -0.03, 0.075))
        if sx > 0:
            for name, col, dz in (('Lure_BobberTop', 'lure_r', 0.008), ('Lure_BobberBottom', 'lure_w', -0.008)):
                bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.02, location=base)
                o = bpy.context.active_object
                o.name = name
                bm2 = bmesh.new(); bm2.from_mesh(o.data)
                bmesh.ops.delete(bm2, geom=[f for f in bm2.faces if (f.calc_center_median().z > 0) != (dz > 0)], context='FACES')
                bm2.to_mesh(o.data); bm2.free()
                o.data.materials.append(M[col])
                for p in o.data.polygons:
                    p.use_smooth = True
        else:
            bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.024, location=base)
            o = bpy.context.active_object
            o.name = 'Lure_Spoon'
            o.scale = (0.55, 0.25, 1.0)
            o.rotation_euler = (0, math.radians(-20), 0)
            o.data.materials.append(M['metal'])
            for p in o.data.polygons:
                p.use_smooth = True
            ctx_select(o)
            bpy.ops.object.transform_apply(scale=True, rotation=True)


def build_hat(M):
    """Bucket hat: a soft crown with a sloped brim, worn tipped back so the face stays clear."""
    hz = HEAD_C[2]
    base = hz + 0.168
    prof = [
        (0.300, base - 0.058), (0.305, base - 0.050), (0.292, base - 0.040),
        (0.245, base - 0.018), (0.212, base), (0.207, base + 0.02),
        (0.198, base + 0.065), (0.183, base + 0.105), (0.13, base + 0.128),
        (0.06, base + 0.135),
    ]
    under = [(0.292, base - 0.065), (0.25, base - 0.035), (0.209, base - 0.010)]
    ob = lathe('Hat', list(reversed(under)) + prof, M['hat'])
    m = ob.modifiers.new('sub', 'SUBSURF')
    m.levels = 1
    ctx_select(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)
    band = lathe('HatBand', [(0.209, base + 0.0), (0.208, base + 0.032)], M['strap'])
    tip = (Matrix.Translation((0, 0, hz)) @ Matrix.Rotation(math.radians(12), 4, 'X')
           @ Matrix.Translation((0, 0, -hz)))
    for o in (ob, band):
        o.data.transform(tip)



# ---------------------------------------------------------------- preview render
def preview(outdir):
    bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 640
    sc.render.resolution_y = 900
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'Standard'   # punchy toon colours, like the game's toon shader

    world = bpy.data.worlds.new('W')
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.55, 0.68, 0.80, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.8

    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.2
    sun.data.angle = math.radians(12)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sc.collection.objects.link(sun)

    bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0))
    floor = bpy.context.active_object
    floor.data.materials.append(material('M_Floor', (0.62, 0.58, 0.50), 0.9))

    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 1.95
    sc.collection.objects.link(cam)
    sc.camera = cam

    pivot = bpy.data.objects.new('Pivot', None)
    sc.collection.objects.link(pivot)
    for ob in bpy.data.collections['Fisher'].objects:
        if ob.parent is None:
            ob.parent = pivot

    paths = []
    views = [('front', 0), ('three_quarter', -35), ('side', -90), ('back', 180)]
    for name, ang in views:
        pivot.rotation_euler = (0, 0, math.radians(ang))
        cam.location = (0, -6, 0.88)
        cam.rotation_euler = (math.radians(90), 0, 0)
        sc.render.filepath = os.path.join(outdir, 'fisher_%s.png' % name)
        bpy.ops.render.render(write_still=True)
        paths.append(sc.render.filepath)

    # Face close-up.
    pivot.rotation_euler = (0, 0, math.radians(-20))
    cam.data.ortho_scale = 0.62
    cam.location = (0, -6, 1.36)
    sc.render.resolution_x = sc.render.resolution_y = 700
    sc.render.filepath = os.path.join(outdir, 'fisher_face.png')
    bpy.ops.render.render(write_still=True)

    # Skin-colour lineup: the same fisher in four PEAK-style skins, three-quarter view.
    pivot.rotation_euler = (0, 0, math.radians(-25))
    cam.data.ortho_scale = 1.95
    cam.location = (0, -6, 0.88)
    sc.render.resolution_x, sc.render.resolution_y = 520, 900
    skin = bpy.data.materials['M_Skin'].node_tree.nodes['Principled BSDF'].inputs['Base Color']
    blush = bpy.data.materials['M_Blush'].node_tree.nodes['Principled BSDF'].inputs['Base Color']
    for i, rgb in enumerate(SKINS):
        skin.default_value = (*rgb, 1)
        blush.default_value = (rgb[0] * 0.85 + 0.08, rgb[1] * 0.6, rgb[2] * 0.6, 1)
        sc.render.filepath = os.path.join(outdir, 'fisher_skin%d.png' % i)
        bpy.ops.render.render(write_still=True)
    print('PREVIEW', outdir)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    stage = argv[0] if argv else 'model'
    outdir = argv[1] if len(argv) > 1 else tempfile.gettempdir()
    if stage == 'model':
        build()
        bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons)
                   for o in bpy.data.objects if o.type == 'MESH')
        print('MODEL OK tris=%d' % tris)
    elif stage == 'preview':
        os.makedirs(outdir, exist_ok=True)
        preview(outdir)


if __name__ == '__main__':
    main()
