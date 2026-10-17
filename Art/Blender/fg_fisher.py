# FishGame player character ("the fisher"), built from text so it is reproducible.
#
# Run (headless):  blender -b --python Art/Blender/fg_fisher.py -- <stage> [outdir]
#   stages: model   build the meshes and save Art/Blender/fishgame_fisher.blend
#           preview render a turnaround PNG into outdir (default: the system temp dir)
#
# Authoring axes: Blender Z up, the character faces -Y, feet on z = 0, metres. The FBX export
# (-Z forward, Y up, bake_space_transform) turns that into Unity's Y up, facing +Z.
#
# Look (spec R74, user 2026-10-17): chunky stylised human (RV There Yet / PEAK proportions), fishing vest
# (tinted by the player's slot colour) over a shirt, bucket hat, REPO-style big eyes whose pupils sit on
# their own bones so Unity can aim them (side-eye tracking), and PEAK-style swappable face parts.
#
# Shapes are metaballs (stiffness 2, threshold 0.6: the surface sits at 0.575 x the element radius), so
# overlapping parts blend into one soft, pillowy surface; each part is converted to a mesh, voxel
# remeshed and decimated to a game budget.

import bpy, bmesh, math, os, sys, tempfile
from mathutils import Vector, Quaternion, Euler, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(HERE, 'fishgame_fisher.blend')
SURF = 0.575  # metaball surface radius / element radius at stiffness 2, threshold 0.6

# ---------------------------------------------------------------- palette (preview colours, linear-ish sRGB)
COL = {
    'skin':   (0.93, 0.70, 0.55),
    'shirt':  (0.86, 0.82, 0.70),   # cream work shirt
    'vest':   (0.20, 0.42, 0.85),   # slot 0 (host) blue; tinted per slot in Unity
    'pants':  (0.42, 0.36, 0.26),   # khaki
    'boots':  (0.20, 0.27, 0.20),   # rubber green
    'hat':    (0.55, 0.50, 0.33),   # olive canvas
    'eye':    (0.97, 0.97, 0.95),
    'pupil':  (0.03, 0.03, 0.04),
    'mouth':  (0.25, 0.08, 0.08),
    'strap':  (0.30, 0.22, 0.15),
}

# ---------------------------------------------------------------- key heights (z, metres)
H = dict(
    head=1.50,      # head centre
    head_r=0.205,   # head radius (big, chunky)
    eye_z=1.53,
    shoulder=1.31,
    chest=1.15,
    waist=0.94,
    hip=0.86,
    knee=0.47,
    ankle=0.11,
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


# ---------------------------------------------------------------- the model
def build():
    reset()
    M = {k: material('M_' + k.capitalize(), v) for k, v in COL.items()}
    M['eye'] = material('M_Eye', COL['eye'], rough=0.25)
    M['pupil'] = material('M_Pupil', COL['pupil'], rough=0.2)

    hz, hr = H['head'], H['head_r']

    # Head: a big soft ball, slightly wider than deep, a fuller jaw/cheek mass, small ears, a button nose.
    blob('Head', mirror_x([
        ball((0, 0, hz), hr, (1.0, 0.95, 1.0)),
        ball((0, -0.035, hz - 0.075), hr * 0.72, (1.08, 0.9, 0.75)),     # cheeks / jaw
        ball((0, 0.0, hz - 0.18), 0.075, (1.0, 1.0, 1.4)),               # neck
        ball((0.198, 0.01, hz - 0.01), 0.04, (0.55, 0.9, 1.1)),          # ear (mirrored)
    ]) + [ball((0, -0.205, hz - 0.035), 0.032, (1.0, 0.85, 0.9))],  # nose
        M['skin'], voxel=0.008, faces=6000, smooth_iter=4)

    # Torso (shirt): a pillowy barrel, a touch of belly, shoulders rounded into the sleeves.
    sh = H['shoulder']
    torso = mirror_x([
        ball((0, 0.0, H['chest']), 0.215, (1.0, 0.72, 1.2)),
        ball((0, -0.03, H['waist'] + 0.04), 0.20, (1.0, 0.8, 0.9)),       # belly
        ball((0, 0.0, H['hip'] + 0.02), 0.19, (1.05, 0.75, 0.7)),         # hips
        ball((0.19, 0.0, sh - 0.01), 0.085, (1.0, 1.0, 0.95)),           # shoulder (mirrored)
        capsule((0.20, 0.0, sh - 0.02), (0.30, 0.0, sh - 0.20), 0.072),  # upper-arm sleeve (A-pose)
    ])
    blob('Shirt', torso, M['shirt'], voxel=0.010, faces=5000, smooth_iter=3)

    # Arms: skin from just above the elbow down; mitten hands with a thumb.
    arm = mirror_x([
        capsule((0.30, 0.0, sh - 0.21), (0.375, -0.01, sh - 0.44), 0.058),   # forearm
        ball((0.395, -0.015, sh - 0.53), 0.065, (0.85, 0.65, 1.15)),        # mitten hand
        ball((0.37, -0.06, sh - 0.49), 0.03, (0.9, 0.9, 1.3), (20, 0, 0)), # thumb
    ])
    blob('Arms', arm, M['skin'], voxel=0.008, faces=3000, smooth_iter=3)

    # Legs (trousers) and boots.
    hip, knee, ank = H['hip'], H['knee'], H['ankle']
    blob('Pants', mirror_x([
        capsule((0.10, 0.0, hip), (0.105, -0.005, knee), 0.092),
        capsule((0.105, -0.005, knee), (0.105, 0.0, ank + 0.06), 0.082),
        ball((0.06, 0.0, hip + 0.02), 0.12, (1.0, 0.85, 0.7)),
    ]), M['pants'], voxel=0.010, faces=3500, smooth_iter=3)
    blob('Boots', mirror_x([
        ball((0.105, -0.03, 0.075), 0.085, (0.95, 1.55, 0.85)),
        capsule((0.105, 0.0, 0.08), (0.105, 0.0, ank + 0.10), 0.088),
    ]), M['boots'], voxel=0.010, faces=2500, smooth_iter=3)

    build_vest(M)
    build_hat(M)
    build_eyes(M)
    build_mouth(M)

    # Tidy names into a collection.
    col = bpy.data.collections.new('Fisher')
    bpy.context.scene.collection.children.link(col)
    for ob in list(bpy.context.scene.collection.objects):
        bpy.context.scene.collection.objects.unlink(ob)
        col.objects.link(ob)


def build_vest(M):
    """The fishing vest: a shell over the shirt, open down the front, with four chest pockets."""
    sh = H['shoulder']
    shell = mirror_x([
        ball((0, 0.0, H['chest'] + 0.01), 0.232, (1.0, 0.74, 1.12)),
        ball((0, -0.03, H['waist'] + 0.06), 0.218, (1.0, 0.82, 0.85)),
        ball((0.17, 0.0, sh - 0.015), 0.07, (1.0, 1.0, 0.9)),
    ])
    ob = blob('Vest', shell, M['vest'], voxel=0.018, faces=None, smooth_iter=3)
    # Cut: open front strip, armholes, neck and the hem below the waist.
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        front_gap = c.y < -0.05 and abs(c.x) < 0.045 + 0.10 * max(0.0, (c.z - 1.12)) / 0.2
        neck = c.z > sh + 0.02 and abs(c.x) < 0.15
        armhole = abs(c.x) > 0.205 and c.z > sh - 0.17
        hem = c.z < H['waist'] - 0.06
        if front_gap or neck or armhole or hem:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    # The cut follows voxel faces, so it zigzags: relax the open edges along themselves a few times.
    edge = [v for v in bm.verts if v.is_boundary]
    for _ in range(12):
        bmesh.ops.smooth_vert(bm, verts=edge, factor=0.6, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(ob.data)
    bm.free()
    # Give the vest thickness so the open edges read as cloth.
    ctx_select(ob)
    sub = ob.modifiers.new('sub', 'SUBSURF')
    sub.levels = 2
    bpy.ops.object.modifier_apply(modifier=sub.name)
    m = ob.modifiers.new('thick', 'SOLIDIFY')
    m.thickness = 0.012
    m.offset = 1.0
    ctx_select(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)
    dec = ob.modifiers.new('dec', 'DECIMATE')
    dec.ratio = min(1.0, 5000 / max(1, len(ob.data.polygons)))
    bpy.ops.object.modifier_apply(modifier=dec.name)

    # Pockets: soft rounded pads on the chest, two each side, with a flap.
    pk = []
    for x, z, w, h in ((0.115, 1.17, 0.075, 0.07), (0.12, 1.03, 0.085, 0.075)):
        pk.append(ball((x, -0.175, z), w, (1.0, 0.32, h / w)))
    blob('VestPockets', mirror_x(pk), M['vest'], voxel=0.006, faces=1200, smooth_iter=2)
    for o in (ob, bpy.data.objects['VestPockets']):
        for p in o.data.polygons:
            p.use_smooth = True


def build_hat(M):
    """Bucket hat: a soft crown with a sloped brim, sitting low on the head."""
    hz, hr = H['head'], H['head_r']
    base = hz + 0.07
    prof = [
        (0.30, base - 0.075), (0.305, base - 0.068), (0.29, base - 0.055),   # brim lip
        (0.235, base - 0.025), (0.205, base), (0.198, base + 0.02),           # brim to crown
        (0.19, base + 0.08), (0.175, base + 0.125), (0.13, base + 0.15),      # crown
        (0.06, base + 0.157),
    ]
    # Inside of the brim so it is not paper thin: walk back along a slightly higher underside.
    under = [(0.29, base - 0.082), (0.24, base - 0.045), (0.20, base - 0.015)]
    ob = lathe('Hat', list(reversed(under)) + prof, M['hat'])
    m = ob.modifiers.new('sub', 'SUBSURF')
    m.levels = 1
    ctx_select(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)
    band = lathe('HatBand', [(0.2, base + 0.0), (0.199, base + 0.035)], M['strap'])
    # Tip both back a little about the head centre so the brim clears the eyes.
    tip = (Matrix.Translation((0, 0, hz)) @ Matrix.Rotation(math.radians(8), 4, 'X')
           @ Matrix.Translation((0, 0, -hz)))
    for o in (ob, band):
        o.data.transform(tip)


def build_eyes(M):
    """REPO-style eyes: two big white balls half sunk into the face, each with a black pupil cap.
    Pupils are separate objects so they can ride an eye bone; rotating the bone slides the pupil over
    the ball (side-eye). Eye shape variants come from scaling the ball and the lid (see EYE_SHAPES)."""
    er = 0.062
    head = bpy.data.objects['Head']
    for side, sx in (('L', 1), ('R', -1)):
        # Find the face surface in front of the eye and sink the ball 45% into it.
        hit, loc, nrm, _ = head.ray_cast(Vector((sx * 0.078, -1.0, H['eye_z'])), Vector((0, 1, 0)))
        c = Vector((sx * 0.078, loc.y + er * 0.45, H['eye_z']))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=er, location=c)
        e = bpy.context.active_object
        e.name = 'Eye_' + side
        e.scale = (0.92, 0.85, 1.12)
        e.data.materials.append(M['eye'])
        # Pupil: a shallow cap on the front of the ball, slightly proud of its surface.
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=er * 0.42,
                                             location=c + Vector((0, -er * 0.80, 0)))
        p = bpy.context.active_object
        p.name = 'Pupil_' + side
        p.scale = (0.9, 0.35, 1.1)
        p.data.materials.append(M['pupil'])
        for o in (e, p):
            ctx_select(o)
            bpy.ops.object.transform_apply(scale=True)
            for poly in o.data.polygons:
                poly.use_smooth = True


def build_mouth(M):
    """Default mouth: a small soft smile under the nose (one of the swappable face parts)."""
    hz = H['head']
    pts = []
    for i in range(7):
        t = (i / 6.0) * 2 - 1
        pts.append(ball((t * 0.045, -0.188 + 0.012 * t * t, hz - 0.095 + 0.016 * t * t), 0.012, (1.0, 0.6, 0.8)))
    blob('Mouth', pts, M['mouth'], voxel=0.004, faces=600, smooth_iter=1)


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
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'

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
    cam.data.ortho_scale = 2.05
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
        cam.location = (0, -6, 0.98)
        cam.rotation_euler = (math.radians(90), 0, 0)
        sc.render.filepath = os.path.join(outdir, 'fisher_%s.png' % name)
        bpy.ops.render.render(write_still=True)
        paths.append(sc.render.filepath)

    # Face close-up.
    pivot.rotation_euler = (0, 0, math.radians(-20))
    cam.data.ortho_scale = 0.62
    cam.location = (0, -6, 1.52)
    sc.render.resolution_x = sc.render.resolution_y = 700
    sc.render.filepath = os.path.join(outdir, 'fisher_face.png')
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
