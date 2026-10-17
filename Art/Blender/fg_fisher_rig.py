# FishGame player character: armature, skinning, procedural animations and FBX export, built from text.
#
# Run (headless), after fg_fisher.py has saved the model:
#   blender -b --python Art/Blender/fg_fisher_rig.py -- <stage> [outdir]
#   stages: rig      open fishgame_fisher.blend, build Fisher_Rig, skin every part, split the arms,
#                    save fishgame_fisher_rigged.blend
#           anims    key the procedural actions into fishgame_fisher_rigged.blend (in place, no root motion)
#           export   Art/Export/Char_Fisher.fbx (meshes + armature + every action as a take) and
#                    Art/Export/Char_Fisher_Anims.fbx (armature + actions only)
#           preview  render contact sheets of every action into outdir (default: the system temp dir);
#                    optional: a comma list of actions, then a comma list of frames for big stills
#           all      rig, anims, export
#
# Axes as in fg_fisher.py: Blender Z up, the character faces -Y (its left is +X), feet on z = 0, metres.
# Joint heights come from fg_fisher.H and the part meshes' own bounds, so the rig follows a reshaped model.
#
# Bone names are Unity Humanoid friendly (Hips .. RightToes, mitten hands: thumb + one index chain).
# Extra bones: Eye_L / Eye_R (each carries its pupil, so Unity can aim the eyes) and Rod (child of
# RightHand at the fist's grip; its +Y runs from the grip along the rod toward the tip).
#
# First person (spec 7.4): the owner's body renders shadows-only except the right arm, so the arms are
# split into ArmL and ArmR objects, each holding that side's forearm, mitten and shirt sleeve.
#
# How the actions are made: a temporary control layer (IK targets for hands and feet, knee and elbow poles)
# is added to the armature, every frame is posed from code (sines, eased key poses, planted feet), then
# the evaluated pose is baked to plain FK keys on the deform bones and the control layer is deleted, so
# the saved file and the FBX hold only the Humanoid bones and their baked keys.

import bpy, bmesh, math, os, sys, tempfile
from mathutils import Vector, Matrix, Quaternion, Euler
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fg_fisher  # noqa: E402  (only its constants are used; its main() is guarded)

H = fg_fisher.H
SRC_PATH = fg_fisher.BLEND_PATH
RIG_PATH = os.path.join(HERE, 'fishgame_fisher_rigged.blend')
EXPORT_DIR = os.path.normpath(os.path.join(HERE, '..', 'Export'))
FPS = 30
RIG = 'Fisher_Rig'
SIDES = (('L', 'Left', 1.0), ('R', 'Right', -1.0))


# ================================================================ small helpers
def ctx_select(*obs, active=None):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or obs[0]


def world_verts(ob):
    mw = ob.matrix_world
    return [mw @ v.co for v in ob.data.vertices]


def centroid(pts):
    s = Vector()
    for p in pts:
        s += p
    return s / max(1, len(pts))


def slice_centre(ob, z, sx):
    """Bounding-box centre of one side (sign sx of x) of the mesh's cross-section at height z. Uses the
    edges crossing the plane: the decimated meshes have long triangles, so a band of vertices is too sparse."""
    vs = world_verts(ob)
    pts = []
    for e in ob.data.edges:
        a, b = vs[e.vertices[0]], vs[e.vertices[1]]
        if (a.z - z) * (b.z - z) < 0:
            p = a.lerp(b, (z - a.z) / (b.z - a.z))
            if p.x * sx > 0.01:
                pts.append(p)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), z))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), z))
    return (lo + hi) / 2


def bbox_centre(ob):
    pts = world_verts(ob)
    return Vector([(min(p[i] for p in pts) + max(p[i] for p in pts)) / 2 for i in range(3)])


def clamp01(x):
    return 0.0 if x < 0 else 1.0 if x > 1 else x


def smooth(x):
    x = clamp01(x)
    return x * x * (3 - 2 * x)


def lerp(a, b, t):
    return a + (b - a) * t


def wave(t, k=1.0, ph=0.0):
    return math.sin(2 * math.pi * (k * t + ph))


def wavec(t, k=1.0, ph=0.0):
    return math.cos(2 * math.pi * (k * t + ph))


def bump(t, a, b):
    """0 outside [a, b], a smooth 0 -> 1 -> 0 hump inside."""
    if t <= a or t >= b:
        return 0.0
    return 0.5 - 0.5 * math.cos(2 * math.pi * (t - a) / (b - a))


def back_out(x, s=1.7):
    """Ease out with an overshoot (friend-slop bounce)."""
    x = clamp01(x) - 1
    return 1 + x * x * ((s + 1) * x + s)


def keys(t, ks, ease=smooth):
    """Interpolate a list of (time, value) keys (floats, Vectors or tuples) with an easing per segment.
    A key may carry its own easing as a third item (used for the segment that ends at that key)."""
    if t <= ks[0][0]:
        return _v(ks[0][1])
    for a, b in zip(ks, ks[1:]):
        if t <= b[0]:
            e = b[2] if len(b) > 2 else ease
            u = e((t - a[0]) / (b[0] - a[0]))
            va, vb = _v(a[1]), _v(b[1])
            return va + (vb - va) * u
    return _v(ks[-1][1])


def _v(x):
    return Vector(x) if isinstance(x, (tuple, list)) else x


def rq(px=0.0, ry=0.0, yz=0.0):
    """A rotation in armature axes, degrees: px + pitches forward (top toward -Y), ry + leans to the
    character's left (top toward +X), yz + turns left. Applied X then Y then Z."""
    return Euler((math.radians(px), math.radians(ry), math.radians(yz)), 'XYZ').to_quaternion()


def frame3(primary, secondary):
    """Orthonormal basis whose first column is primary and second is secondary made orthogonal to it."""
    p = Vector(primary).normalized()
    s = Vector(secondary)
    s = (s - p * s.dot(p)).normalized()
    return Matrix((p, s, p.cross(s))).transposed()


# ================================================================ stage 1: rig
def joints():
    """Every joint position, from H and the part meshes. Left side (x > 0); the right side mirrors it."""
    ob = bpy.data.objects
    sh, hip, knee, ank = H['shoulder'], H['hip'], H['knee'], H['ankle']
    J = {}
    J['hips'] = Vector((0, 0, hip + 0.04))
    J['spine'] = Vector((0, 0, H['waist'] + 0.04))
    J['chest'] = Vector((0, 0, H['chest'] - 0.07))
    J['upperchest'] = Vector((0, 0, H['chest'] + 0.05))
    J['neck'] = Vector((0, 0, sh + 0.01))
    J['head'] = Vector((0, 0, H['head'] - 0.12))
    J['head_top'] = Vector((0, 0, max(p.z for p in world_verts(ob['Head']))))
    for side, eye in (('L', 'Eye_L'), ('R', 'Eye_R')):
        J['eye_' + side] = bbox_centre(ob[eye])

    # Legs: the trouser leg's centre gives x; heights from H.
    x_thigh = slice_centre(ob['Pants'], knee + 0.18, 1).x
    x_shin = slice_centre(ob['Pants'], ank + 0.12, 1).x
    J['upperleg'] = Vector((x_thigh, 0, hip))
    J['lowerleg'] = Vector((lerp(x_thigh, x_shin, 0.5), -0.012, knee))
    J['foot'] = Vector((x_shin, 0, ank))
    boot = [p for p in world_verts(ob['Boots']) if p.x > 0]
    toe_y = min(p.y for p in boot)
    J['toes'] = Vector((x_shin, toe_y + 0.075, 0.035))
    J['toes_tip'] = Vector((x_shin, toe_y + 0.01, 0.035))
    J['heel'] = Vector((x_shin, max(p.y for p in boot if p.z < 0.04), 0.0))

    # Arms: the shoulder joint sits inside the shirt's shoulder ball; the hand from the mitten blob.
    shirt_w = max(p.x for p in world_verts(ob['Shirt']) if abs(p.z - (sh - 0.01)) < 0.01)
    S = Vector((shirt_w - 0.08, 0, sh - 0.01))
    arm = [p for p in world_verts(ob['Arms']) if p.x > 0]
    bottom = min(p.z for p in arm)
    hand_c = centroid([p for p in arm if p.z < bottom + 0.11])
    hand_c.z = bottom + 0.075
    top = max(p.z for p in arm)
    fore_top = centroid([p for p in arm if p.z > top - 0.05])
    wrist = hand_c + (fore_top - hand_c).normalized() * 0.085
    elbow = S.lerp(wrist, 0.48) + Vector((0, 0.008, 0))
    J['shoulder'] = Vector((0.035, -0.005, sh - 0.035))
    J['upperarm'] = S
    J['lowerarm'] = elbow
    J['hand'] = wrist
    J['hand_c'] = hand_c
    down = (hand_c - wrist).normalized()
    J['index'] = hand_c + down * 0.02 + Vector((0.004, 0, 0))
    J['index_mid'] = J['index'] + down * 0.045
    J['index_tip'] = J['index'] + down * 0.085
    J['thumb'] = wrist + Vector((-0.012, -0.03, -0.03))
    J['thumb_mid'] = hand_c + Vector((-0.02, -0.055, 0.03))
    J['thumb_tip'] = hand_c + Vector((-0.024, -0.075, -0.005))
    return J


# (name, parent, head key, tail key or vector, align-z hint) for the left side and the spine.
def bone_specs(J):
    fwd = Vector((0, -1, 0))
    specs = [
        ('Hips', None, J['hips'], J['spine'], fwd),
        ('Spine', 'Hips', J['spine'], J['chest'], fwd),
        ('Chest', 'Spine', J['chest'], J['upperchest'], fwd),
        ('UpperChest', 'Chest', J['upperchest'], J['neck'], fwd),
        ('Neck', 'UpperChest', J['neck'], J['head'], fwd),
        ('Head', 'Neck', J['head'], J['head_top'], fwd),
    ]
    for s, side, sx in SIDES:
        m = Vector((sx, 1, 1))

        def M(v):
            return v * m
        eye = J['eye_' + s]
        specs += [
            ('Eye_' + s, 'Head', eye, eye + Vector((0, -0.06, 0)), Vector((0, 0, 1))),
            (side + 'Shoulder', 'UpperChest', M(J['shoulder']), M(J['upperarm']), fwd),
            (side + 'UpperArm', side + 'Shoulder', M(J['upperarm']), M(J['lowerarm']), fwd),
            (side + 'LowerArm', side + 'UpperArm', M(J['lowerarm']), M(J['hand']), fwd),
            (side + 'Hand', side + 'LowerArm', M(J['hand']), M(J['index']), fwd),
            (side + 'ThumbProximal', side + 'Hand', M(J['thumb']), M(J['thumb_mid']), Vector((sx, 0, 0))),
            (side + 'ThumbIntermediate', side + 'ThumbProximal', M(J['thumb_mid']), M(J['thumb_tip']), Vector((sx, 0, 0))),
            (side + 'IndexProximal', side + 'Hand', M(J['index']), M(J['index_mid']), fwd),
            (side + 'IndexIntermediate', side + 'IndexProximal', M(J['index_mid']), M(J['index_tip']), fwd),
            (side + 'UpperLeg', 'Hips', M(J['upperleg']), M(J['lowerleg']), fwd),
            (side + 'LowerLeg', side + 'UpperLeg', M(J['lowerleg']), M(J['foot']), fwd),
            (side + 'Foot', side + 'LowerLeg', M(J['foot']), M(J['toes']), Vector((0, 0, 1))),
            (side + 'Toes', side + 'Foot', M(J['toes']), M(J['toes_tip']), Vector((0, 0, 1))),
        ]
    # Rod: at the right fist's grip, pointing out of the thumb side (forward in the rest pose).
    grip = J['hand_c'] * Vector((-1, 1, 1)) + Vector((0.008, 0, 0.005))
    specs.append(('Rod', 'RightHand', grip, grip + Vector((0, -0.2, 0)), Vector((0, 0, 1))))
    return specs


def build_armature(J):
    arm_data = bpy.data.armatures.new(RIG)
    arm_data.display_type = 'STICK'
    rig = bpy.data.objects.new(RIG, arm_data)
    rig.show_in_front = True
    bpy.data.collections['Fisher'].objects.link(rig)
    ctx_select(rig)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    for name, parent, head, tail, zhint in bone_specs(J):
        b = eb.new(name)
        b.head, b.tail = head, tail
        b.align_roll(zhint)
        if parent:
            b.parent = eb[parent]
            b.use_connect = (b.head - eb[parent].tail).length < 1e-4
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return rig


SPINE = ['Hips', 'Spine', 'Chest', 'UpperChest']


def side_bones(side, *parts):
    return [side + p for p in parts]


ARM_PARTS = ('UpperArm', 'LowerArm', 'Hand', 'ThumbProximal', 'ThumbIntermediate',
             'IndexProximal', 'IndexIntermediate')

# Which bones each part may be skinned to (automatic weights only see these).
SKIN_SETS = {
    'Head': ['UpperChest', 'Neck', 'Head'],
    'Shirt': SPINE + ['Neck'] + [s + p for s in ('Left', 'Right') for p in ('Shoulder', 'UpperArm', 'LowerArm')],
    'Arms': [s + p for s in ('Left', 'Right') for p in ARM_PARTS],
    'Pants': ['Hips', 'Spine'] + [s + p for s in ('Left', 'Right') for p in ('UpperLeg', 'LowerLeg')],
    'Boots': [s + p for s in ('Left', 'Right') for p in ('LowerLeg', 'Foot', 'Toes')],
}
RIGID = {'Hat': 'Head', 'HatBand': 'Head', 'Eye_L': 'Head', 'Eye_R': 'Head', 'Mouth': 'Head',
         'Pupil_L': 'Eye_L', 'Pupil_R': 'Eye_R'}
# The vest rides the torso; the shirt's upper-arm share is kept at the armholes so the sleeve cannot
# push through the vest when the arm lifts.
VEST_BONES = SPINE + ['Neck', 'LeftShoulder', 'RightShoulder', 'LeftUpperArm', 'RightUpperArm']


def add_armature_mod(ob, rig):
    ob.parent = rig
    ob.matrix_parent_inverse = rig.matrix_world.inverted()
    if not any(m.type == 'ARMATURE' for m in ob.modifiers):
        m = ob.modifiers.new('Armature', 'ARMATURE')
        m.object = rig


def skin_auto(ob, rig, bones):
    for b in rig.data.bones:
        b.use_deform = b.name in bones
    ctx_select(ob, rig, active=rig)
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    for b in rig.data.bones:
        b.use_deform = True
    fill_unweighted(ob)


def fill_unweighted(ob):
    """Heat weighting can leave a few vertices (or a whole bone's share) empty: copy the nearest weighted vertex."""
    me = ob.data
    weighted = [v for v in me.vertices if any(g.weight > 1e-4 for g in v.groups)]
    empty = [v for v in me.vertices if not any(g.weight > 1e-4 for g in v.groups)]
    if not empty:
        return
    kd = KDTree(len(weighted))
    for v in weighted:
        kd.insert(v.co, v.index)
    kd.balance()
    for v in empty:
        _, idx, _ = kd.find(v.co)
        for g in me.vertices[idx].groups:
            ob.vertex_groups[g.group].add([v.index], g.weight, 'REPLACE')
    print('  %s: filled %d unweighted vertices' % (ob.name, len(empty)))


def boot_shafts(ob):
    """Heat weighting lets the foot bend the boot shaft, which then lets the trouser hem poke through:
    above the ankle the boot rides the shin only, blending to the automatic weights over 7 cm."""
    vgs = ob.vertex_groups
    for v in ob.data.vertices:
        side = 'Left' if v.co.x > 0 else 'Right'
        k = smooth((v.co.z - (H['ankle'] - 0.01)) / 0.07)
        if k <= 0:
            continue
        for g in list(v.groups):
            g.weight *= (1 - k)
        shin = vgs.get(side + 'LowerLeg') or vgs.new(name=side + 'LowerLeg')
        old = next((g.weight for g in v.groups if g.group == shin.index), 0.0)
        shin.add([v.index], old + k, 'REPLACE')


def skin_rigid(ob, rig, bone):
    ob.vertex_groups.clear()
    vg = ob.vertex_groups.new(name=bone)
    vg.add([v.index for v in ob.data.vertices], 1.0, 'REPLACE')
    add_armature_mod(ob, rig)


def skin_transfer(ob, src, rig, keep):
    """Weights from the nearest point on src (the vest rides the shirt under it), limited to `keep` bones."""
    bvh = BVHTree.FromObject(src, bpy.context.evaluated_depsgraph_get(), deform=False)
    names = {g.index: g.name for g in src.vertex_groups}
    sme = src.data
    ob.vertex_groups.clear()
    groups = {}
    for v in ob.data.vertices:
        loc, _, fi, _ = bvh.find_nearest(v.co)
        poly = sme.polygons[fi]
        acc = {}
        tot = 0.0
        for vi in poly.vertices:
            w = 1.0 / max(1e-5, (sme.vertices[vi].co - loc).length)
            tot += w
            for g in sme.vertices[vi].groups:
                n = names[g.group]
                if n in keep:
                    acc[n] = acc.get(n, 0.0) + g.weight * w
        s = sum(acc.values())
        if s < 1e-6:
            acc, s = {'Chest': 1.0}, 1.0
        for n, w in acc.items():
            if n not in groups:
                groups[n] = ob.vertex_groups.new(name=n)
            groups[n].add([v.index], w / s, 'REPLACE')
    add_armature_mod(ob, rig)


def tidy_weights(ob, limit=4, floor=0.01):
    """At most `limit` influences per vertex (Unity's default skin weights), tiny ones dropped, normalised."""
    vgs = ob.vertex_groups
    for v in ob.data.vertices:
        gs = sorted(((g.weight, g.group) for g in v.groups), reverse=True)
        keep = [(w, gi) for w, gi in gs[:limit] if w >= floor] or gs[:1]
        s = sum(w for w, _ in keep) or 1.0
        kept = {gi for _, gi in keep}
        for w, gi in gs:
            if gi not in kept:
                vgs[gi].remove([v.index])
        for w, gi in keep:
            vgs[gi].add([v.index], w / s, 'REPLACE')
    for vg in list(vgs):
        if not any(vg.index == g.group for v in ob.data.vertices for g in v.groups):
            vgs.remove(vg)


def keep_normals(ob):
    """Freeze the smooth normals as custom normals, so cutting the mesh leaves no shading seam."""
    me = ob.data
    me.normals_split_custom_set([c.vector for c in me.corner_normals])


def split_faces(ob, name, pick):
    """Move the faces for which pick(poly, weights) is true into a new object called name."""
    new = ob.copy()
    new.data = ob.data.copy()
    new.name = new.data.name = name
    for c in ob.users_collection:
        c.objects.link(new)
    for o, keep_picked in ((new, True), (ob, False)):
        me = o.data
        names = {g.index: g.name for g in o.vertex_groups}
        bm = bmesh.new()
        bm.from_mesh(me)
        dl = bm.verts.layers.deform.active
        kill = []
        for f in bm.faces:
            w = {}
            for v in f.verts:
                for gi, gw in v[dl].items():
                    w[names[gi]] = w.get(names[gi], 0.0) + gw / len(f.verts)
            if pick(f, w) != keep_picked:
                kill.append(f)
        bmesh.ops.delete(bm, geom=kill, context='FACES')
        bm.to_mesh(me)
        bm.free()
    return new


def join(target, others):
    ctx_select(target, *others, active=target)
    bpy.ops.object.join()
    return target


def rig_stage():
    bpy.ops.wm.open_mainfile(filepath=SRC_PATH)
    J = joints()
    rig = build_armature(J)
    ob = bpy.data.objects

    for name, bones in SKIN_SETS.items():
        print('skinning', name)
        skin_auto(ob[name], rig, bones)
    boot_shafts(ob['Boots'])
    for name, bone in RIGID.items():
        skin_rigid(ob[name], rig, bone)
    for name in ('Vest', 'VestPockets'):
        skin_transfer(ob[name], ob['Shirt'], rig, VEST_BONES)

    # Arms: split the skin by side, then cut each shirt sleeve (faces the arm bones own) onto its arm.
    keep_normals(ob['Shirt'])
    arms = ob['Arms']
    arm_r = split_faces(arms, 'ArmR', lambda f, w: f.calc_center_median().x < 0)
    arms.name = arms.data.name = 'ArmL'
    for s, side, sx in SIDES:
        sleeve = split_faces(ob['Shirt'], 'Sleeve' + s, lambda f, w, side=side: sum(
            w.get(side + p, 0.0) for p in ('UpperArm', 'LowerArm')) > 0.5)
        join(ob['Arm' + s], [sleeve])
        ob['Arm' + s].data.name = 'Arm' + s

    for o in bpy.data.collections['Fisher'].objects:
        if o.type == 'MESH':
            tidy_weights(o)
            for m in o.modifiers:
                if m.type == 'ARMATURE':
                    m.object = rig
    check_skin(rig)
    bpy.ops.wm.save_as_mainfile(filepath=RIG_PATH)
    print('RIG OK bones=%d' % len(rig.data.bones))


def check_skin(rig):
    bones = {b.name for b in rig.data.bones}
    for o in bpy.data.collections['Fisher'].objects:
        if o.type != 'MESH':
            continue
        bad = [g.name for g in o.vertex_groups if g.name not in bones]
        empty = sum(1 for v in o.data.vertices if not v.groups)
        assert not bad and not empty, (o.name, bad, empty)
        print('  %-12s verts=%5d groups=%s' % (o.name, len(o.data.vertices), sorted(g.name for g in o.vertex_groups)))


# ================================================================ stage 2: animation
# The temporary control layer: (bone, parent, copy-of rest bone or None, offset) - IK targets share the
# rest matrix of the bone they drive; poles sit in front of the knees and behind the elbows.
def add_controls(rig):
    ctx_select(rig)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = rig.data.edit_bones
    made = []

    def ctl(name, parent, like=None, at=None):
        b = eb.new(name)
        if like:
            src = eb[like]
            b.head, b.tail, b.roll = src.head.copy(), src.tail.copy(), src.roll
        else:
            b.head, b.tail = at, at + Vector((0, 0, 0.05))
        b.parent = eb[parent] if parent else None
        b.use_deform = False
        made.append(name)

    for s, side, sx in SIDES:
        ctl('IK_Foot_' + s, None, like=side + 'Foot')
        knee = eb[side + 'LowerLeg'].head
        ctl('Pole_Knee_' + s, 'Hips', at=Vector((knee.x * 1.4, -0.6, knee.z)))
        ctl('IK_Hand_' + s, 'UpperChest', like=side + 'Hand')
        elbow = eb[side + 'LowerArm'].head
        ctl('Pole_Elbow_' + s, 'UpperChest', at=Vector((elbow.x * 1.5, 0.45, elbow.z - 0.05)))
    bpy.ops.object.mode_set(mode='OBJECT')
    for n in made:
        rig.pose.bones[n].rotation_mode = 'QUATERNION'

    for s, side, sx in SIDES:
        for chain, tgt, pole, end in ((side + 'LowerLeg', 'IK_Foot_' + s, 'Pole_Knee_' + s, side + 'Foot'),
                                      (side + 'LowerArm', 'IK_Hand_' + s, 'Pole_Elbow_' + s, side + 'Hand')):
            c = rig.pose.bones[chain].constraints.new('IK')
            c.target, c.subtarget = rig, tgt
            c.pole_target, c.pole_subtarget = rig, pole
            c.chain_count = 2
            c.use_stretch = False
            cr = rig.pose.bones[end].constraints.new('COPY_ROTATION')
            cr.target, cr.subtarget = rig, tgt
            calibrate_pole(rig, c, rig.pose.bones[chain].parent)
    return made


def calibrate_pole(rig, c, chain_top):
    """Pick the pole angle that leaves the chain exactly in its rest pose when the target is at rest."""
    rest = chain_top.bone.matrix_local

    def err(a):
        c.pole_angle = math.radians(a)
        bpy.context.view_layer.update()
        m = chain_top.matrix
        return (m.col[2].xyz - rest.col[2].xyz).length + (m.col[1].xyz - rest.col[1].xyz).length

    best = min(range(-180, 180, 3), key=err)
    fine = min((best + d * 0.25 for d in range(-12, 13)), key=err)
    e = err(fine)
    print('  pole %-14s %7.2f deg  err %.4f' % (chain_top.name, fine, e))
    assert e < 0.02, 'IK chain does not rest cleanly: ' + chain_top.name


class Pose:
    """One frame's pose. rot: bone -> rotation in armature axes (see rq), applied in the bone's rest frame,
    so children follow their parents (FK). hips: Hips offset in metres (armature axes). ctl: control bone
    -> (4x4 desired matrix in rest coordinates of its parent, 'parent' | 'world')."""

    def __init__(self, R):
        self.R = R
        self.rot = {}
        self.hips = Vector()
        self.ctl = {}

    def add(self, bone, q):
        self.rot[bone] = q @ self.rot.get(bone, Quaternion())

    def lean(self, px=0.0, ry=0.0, yz=0.0, w=(0.3, 0.35, 0.35)):
        for b, k in zip(('Spine', 'Chest', 'UpperChest'), w):
            self.add(b, rq(px * k, ry * k, yz * k))

    def look(self, px=0.0, ry=0.0, yz=0.0, neck=0.4):
        self.add('Neck', rq(px * neck, ry * neck, yz * neck))
        self.add('Head', rq(px * (1 - neck), ry * (1 - neck), yz * (1 - neck)))

    def eyes(self, px=0.0, yz=0.0):
        for s in 'LR':
            self.add('Eye_' + s, rq(px, 0, yz))

    def shrug(self, s, deg):
        self.add(('Left' if s == 'L' else 'Right') + 'Shoulder', rq(0, -deg if s == 'L' else deg, 0))

    def curl(self, s, fingers=0.0, thumb=0.0):
        side, sx = ('Left', 1) if s == 'L' else ('Right', -1)
        self.add(side + 'IndexProximal', rq(0, fingers * 0.55 * sx, 0))
        self.add(side + 'IndexIntermediate', rq(0, fingers * 0.45 * sx, 0))
        self.add(side + 'ThumbProximal', rq(0, 0, -thumb * 0.5 * sx))
        self.add(side + 'ThumbIntermediate', rq(0, thumb * 0.5 * sx, 0))

    # --- feet: planted on the ground by default; pos is the ankle offset from rest, pitch + = toe down
    def foot(self, s, off=(0, 0, 0), pitch=0.0, yaw=0.0, lift=0.0, toes=None):
        R = self.R
        sx = 1 if s == 'L' else -1
        rest = R.rest['IK_Foot_' + s]
        q = rq(pitch, 0, yaw * sx)
        ank = rest.translation + Vector(off)
        # Keep the heel and the ball of the foot on or above the ground.
        lowest = min((q @ (R.J[k] * Vector((sx, 1, 1)) - R.J['foot'] * Vector((sx, 1, 1)))).z for k in ('heel', 'toes'))
        ank.z = max(ank.z, -lowest + 0.0) + lift
        m = Matrix.Translation(ank) @ q.to_matrix().to_4x4() @ rest.to_3x3().to_4x4()
        self.ctl['IK_Foot_' + s] = (m, 'parent')
        if toes is None:
            toes = -max(0.0, pitch) if lift < 0.01 else 0.0
        self.add(('Left' if s == 'L' else 'Right') + 'Toes', rq(toes, 0, 0))

    def knee(self, s, off=(0, 0, 0)):
        rest = self.R.rest['Pole_Knee_' + s]
        self.ctl['Pole_Knee_' + s] = (Matrix.Translation(Vector(off)) @ rest, 'parent')

    def elbow(self, s, at, space='parent'):
        rest = self.R.rest['Pole_Elbow_' + s]
        m = rest.copy()
        m.translation = Vector(at) * Vector((1 if s == 'L' else -1, 1, 1))
        self.ctl['Pole_Elbow_' + s] = (m, space)

    # --- hands: place the fist's grip point at `grip`, with the fist's thumb/rod axis along `rod` and the
    # hand's long axis (wrist -> knuckles) toward `axis`. Coordinates are in UpperChest rest space
    # ('parent': the hand rides the torso) or armature space ('world': e.g. ladder rungs).
    def hand(self, s, grip, rod, axis, space='parent'):
        R = self.R
        g_rest, rod_rest, axis_rest, hand_rest = R.hand_rest[s]
        rot = frame3(rod, axis) @ frame3(rod_rest, axis_rest).transposed()
        wrist = Vector(grip) - rot @ (g_rest - hand_rest.translation)
        m = Matrix.Translation(wrist) @ (rot @ hand_rest.to_3x3()).to_4x4()
        self.ctl['IK_Hand_' + s] = (m, space)


class Rigger:
    def __init__(self, rig):
        self.rig = rig
        self.J = joints_from_rig(rig)
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
        self.deform = [b.name for b in rig.data.bones if b.use_deform]
        self.hand_rest = {}
        for s, side, sx in SIDES:
            hand = self.rest[side + 'Hand']
            grip = self.J['hand_c'] * Vector((sx, 1, 1)) + Vector((-0.008 * sx, 0, 0.005))
            axis = (self.rest[side + 'IndexProximal'].translation - hand.translation).normalized()
            self.hand_rest[s] = (grip, Vector((0, -1, 0)), axis, hand)

    def fk(self, pose, name):
        """Posed matrix (armature space) of an FK spine bone under `pose`."""
        b = self.rig.data.bones[name]
        q = pose.rot.get(name, Quaternion())
        B = b.matrix_local.to_3x3().to_quaternion()
        local = (B.inverted() @ q @ B).to_matrix().to_4x4()
        if name == 'Hips':
            local = Matrix.Translation(b.matrix_local.to_3x3().inverted() @ pose.hips) @ local
            return b.matrix_local @ local
        return self.fk(pose, b.parent.name) @ b.parent.matrix_local.inverted() @ b.matrix_local @ local

    def apply(self, pose, frame):
        pbs = self.rig.pose.bones
        for name in self.deform:
            pb = pbs[name]
            q = pose.rot.get(name, Quaternion())
            B = pb.bone.matrix_local.to_3x3().to_quaternion()
            pb.rotation_quaternion = B.inverted() @ q @ B
            pb.location = pb.bone.matrix_local.to_3x3().inverted() @ pose.hips if name == 'Hips' else Vector()
        for name in CONTROLS:
            pb = pbs[name]
            m, space = pose.ctl.get(name, (self.rest[name], 'parent'))
            if space == 'world':
                par = pb.bone.parent.name
                m = self.rest[par] @ self.fk(pose, par).inverted() @ m
            pb.matrix_basis = self.rest[name].inverted() @ m

    def bake(self, name, n, fn, loop):
        """Pose every frame from fn(t, frame), evaluate the IK, and key the result as FK on a new action."""
        rig = self.rig
        sc = bpy.context.scene
        poses = {}
        for f in range(n + 1):
            t = f / n
            self.apply(fn(t if not loop else t % 1.0, f), f)
            bpy.context.view_layer.update()
            poses[f] = {b: rig.pose.bones[b].matrix.copy() for b in self.deform}
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data_create()
        rig.animation_data.action = act
        for b in self.deform:
            for c in rig.pose.bones[b].constraints:
                c.mute = True
        prev = {}
        for f in range(n + 1):
            for b in self.deform:
                pb = rig.pose.bones[b]
                bone = pb.bone
                M = poses[f][b]
                if bone.parent:
                    P = poses[f][bone.parent.name]
                    basis = (P @ bone.parent.matrix_local.inverted() @ bone.matrix_local).inverted() @ M
                else:
                    basis = bone.matrix_local.inverted() @ M
                loc, q, _ = basis.decompose()
                if b in prev:
                    q.make_compatible(prev[b])
                prev[b] = q
                pb.rotation_quaternion = q
                pb.keyframe_insert('rotation_quaternion', frame=f, group=b)
                if b == 'Hips':
                    pb.location = loc
                    pb.keyframe_insert('location', frame=f, group=b)
        for b in self.deform:
            for c in rig.pose.bones[b].constraints:
                c.mute = False
        act.use_frame_range = True
        act.frame_start, act.frame_end = 0, n
        act.use_cyclic = loop
        for fc in action_fcurves(act):
            for k in fc.keyframe_points:
                k.interpolation = 'LINEAR'
        rig.animation_data.action = None
        return act


CONTROLS = []


def joints_from_rig(rig):
    """The joint dict, recovered from the armature (the meshes are skinned by now)."""
    b = rig.data.bones
    J = {}
    J['foot'] = b['LeftFoot'].head_local.copy()
    J['toes'] = b['LeftToes'].head_local.copy()
    J['heel'] = Vector((J['foot'].x, J['foot'].y + 0.075, 0.0))
    J['hand_c'] = b['LeftIndexProximal'].head_local.copy() - (b['LeftIndexProximal'].tail_local - b['LeftIndexProximal'].head_local).normalized() * 0.02
    return J


def action_fcurves(act):
    try:
        return act.fcurves
    except AttributeError:
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags:
                    out.extend(cb.fcurves)
        return out


# ---------------------------------------------------------------- the actions
# Each action is fn(t, frame) -> Pose with t in [0, 1] over the clip. Loops are periodic in t, so the
# last frame equals the first.

def base(R, t=0.0, breathe=1.0):
    """Relaxed stand: knees soft, arms hanging a little forward, fists loose, a slow breath."""
    p = Pose(R)
    b = wave(t, 1) * breathe
    p.hips = Vector((0, 0, -0.012))
    p.add('Chest', rq(-1.0 * b))
    p.add('UpperChest', rq(-1.5 * b))
    p.look(1.5 * b)
    for s, sx in (('L', 1), ('R', -1)):
        p.foot(s, (0.015 * sx, 0, 0), yaw=8)
        p.knee(s, (0.08 * sx, 0, 0))
        p.shrug(s, 1.5 * b)
        relaxed_arm(p, s)
    return p


def relaxed_arm(p, s, fwd=0.0, out=0.0, up=0.0, curl=35):
    sx = 1 if s == 'L' else -1
    grip = Vector(((0.33 + out) * sx, -0.06 - fwd, 0.82 + up))
    p.hand(s, grip, (0.25 * sx, -1, 0.25), (-0.15 * sx, -0.35 - fwd, -1))
    p.elbow(s, (0.55, 0.45, 1.0))
    p.curl(s, curl, 10)


# --- locomotion -----------------------------------------------------------------------------------------
def gait_speed(stride, frames, stance):
    """Ground speed (m/s) at which a gait clip's planted foot does not slide: play the clip at
    (controller speed / this) x."""
    return stride / (stance * frames / FPS)


def gait(R, t, stride, stance, lift, bob, lean, swing, hip_drop, run=False, crouch=0.0, arm_bend=0.0):
    """A procedural in-place gait: a planted foot slides back `stride` metres over the stance share of
    the loop (see gait_speed). t = 0 is the left heel strike."""
    p = base(R, t, 0.0)
    L = stride
    for s, ph, sx in (('L', 0.0, 1), ('R', 0.5, -1)):
        u = (t + ph) % 1.0
        if u < stance:
            v = u / stance
            y = -L / 2 + L * v
            z = 0.0
            pitch = -14 * (1 - smooth(v / 0.3)) + 28 * smooth((v - 0.65) / 0.35) * (0.6 if run else 1.0)
            if run:
                pitch = -6 * (1 - smooth(v / 0.3)) + 30 * smooth((v - 0.5) / 0.5)
        else:
            v = (u - stance) / (1 - stance)
            y = L / 2 - L * smooth(v)
            z = lift * math.sin(math.pi * v) ** 0.8 * (1 + 0.5 * (1 - v))
            if run:  # heel kicks up behind before the knee drives forward
                y += 0.16 * math.sin(math.pi * v) * (1 - v) * 2
                z = lift * math.sin(math.pi * min(1, v * 1.15)) ** 0.5
            pitch = lerp(30 if run else 22, -12, smooth(v * 1.2))
        p.foot(s, (0.02 * sx + crouch * 0.05 * sx, y, 0), pitch=pitch, lift=z, yaw=6)
        p.knee(s, (0.10 * sx * (1 + crouch), 0, 0))
    # Body: bob (low at contact for a walk, low mid-stance for a run), sway over the planted foot.
    if run:
        z = -bob * wavec(t, 2, -stance / 2)
    else:
        z = -bob * wavec(t, 2)
    sway = 0.022 * (0.4 if run else 1) * wavec(t, 1, -stance / 2)
    p.hips = Vector((sway, 0.02 * crouch, -hip_drop + z - crouch * 0.26))
    p.add('Hips', rq(lean * 0.3 + crouch * 10, -4 * wavec(t, 1, -stance / 2), -7 * wavec(t, 1)))
    p.lean(lean + crouch * 18 + 1.5 * wavec(t, 2, 0.1), 3 * wavec(t, 1, -stance / 2), 11 * wavec(t, 1))
    # Head: steadies the gaze against the lean, with a bouncy lag on the bob.
    p.look(-lean * 0.7 - crouch * 22 + 3 * wavec(t, 2, 0.15), -2 * wavec(t, 1, 0.1), -3 * wavec(t, 1))
    # Arms counter-swing the legs; forward swing bends the elbow and lifts the fist.
    for s, sx, ph in (('L', 1, 0.0), ('R', -1, 0.5)):
        a = -wavec(t, 1, ph + 0.04)     # + forward
        fwd = swing * a
        up = arm_bend * 0.45 * (0.5 + 0.5 * a)
        p.hand(s, Vector(((0.30 - 0.06 * arm_bend - 0.03 * max(0, a)) * sx, -0.05 - fwd - 0.12 * arm_bend,
                          0.83 + up + 0.04 * max(0, a))),
               (0.25 * sx, -1, 0.3 + 0.3 * arm_bend), (-0.1 * sx, -0.3 - 0.8 * arm_bend - a * 0.4, -1 + arm_bend * 0.6))
        p.elbow(s, (0.5, 0.6, 1.0 - arm_bend * 0.2))
        p.curl(s, 45 + 25 * arm_bend, 15)
        p.shrug(s, 2 * max(0, a) + 3 * arm_bend)
    return p


# Gait clips: (stride m, frames, stance share). Their natural speeds are printed by the anims stage.
GAITS = {
    'Walk': (0.45, 20, 0.6),
    'Run': (0.58, 14, 0.34),
    'Sprint': (0.68, 12, 0.3),
    'Crouch_Walk': (0.40, 24, 0.62),
}


def act_walk(R):
    L, n, st = GAITS['Walk']
    return lambda t, f: gait(R, t, L, st, lift=0.09, bob=0.022, lean=4, swing=0.13, hip_drop=0.03)


def act_run(R):
    L, n, st = GAITS['Run']
    return lambda t, f: gait(R, t, L, st, lift=0.2, bob=0.04, lean=10, swing=0.15, hip_drop=0.05,
                             run=True, arm_bend=0.55)


def act_sprint(R):
    L, n, st = GAITS['Sprint']
    return lambda t, f: gait(R, t, L, st, lift=0.25, bob=0.05, lean=18, swing=0.2, hip_drop=0.07,
                             run=True, arm_bend=0.75)


def act_crouch_walk(R):
    L, n, st = GAITS['Crouch_Walk']
    return lambda t, f: gait(R, t, L, st, lift=0.08, bob=0.018, lean=4, swing=0.08, hip_drop=0.0,
                             crouch=1.0, arm_bend=0.35)


def act_idle(R):
    def fn(t, f):
        p = base(R, t * 2)                          # two breaths per loop
        shift = wave(t, 1)                          # weight over the left foot, then the right
        p.hips += Vector((0.022 * shift, 0, -0.008 * abs(shift) - 0.006 * wave(t, 2)))
        p.add('Hips', rq(0, -2.5 * shift, 1.5 * wave(t, 1, 0.2)))
        p.lean(0, 3 * shift, -2 * wave(t, 1, 0.25))
        # A glance: eyes dart left, then right, then home; the head follows a little later.
        g = bump(t, 0.36, 0.56) - bump(t, 0.60, 0.80)
        p.eyes(-3 * bump(t, 0.36, 0.56), 28 * g)
        hg = bump(t, 0.40, 0.62) - bump(t, 0.64, 0.86)
        p.look(1 * wave(t, 2), -2.5 * shift, 10 * hg)
        for s, sx in (('L', 1), ('R', -1)):
            relaxed_arm(p, s, fwd=0.01 * wave(t, 1, 0.3 if s == 'L' else 0.8), up=0.006 * wave(t, 2))
        return p
    return fn


def act_crouch_idle(R):
    def fn(t, f):
        p = base(R, t * 2)
        p.hips = Vector((0.01 * wave(t, 1), 0.02, -0.27 - 0.008 * wave(t, 2)))
        p.add('Hips', rq(12))
        p.lean(20 + 1.5 * wave(t, 2), 2 * wave(t, 1))
        p.look(-24 + 1.5 * wave(t, 2, 0.1), 0, 6 * (bump(t, 0.2, 0.45) - bump(t, 0.55, 0.8)))
        p.eyes(0, 25 * (bump(t, 0.15, 0.42) - bump(t, 0.5, 0.78)))
        for s, sx in (('L', 1), ('R', -1)):
            p.foot(s, (0.06 * sx, 0, 0), yaw=12)
            p.knee(s, (0.25 * sx, 0, 0))
            p.hand(s, Vector((0.27 * sx, -0.16, 0.86 + 0.006 * wave(t, 2))), (0.3 * sx, -1, 0.3), (-0.2 * sx, -0.8, -1))
            p.elbow(s, (0.6, 0.5, 1.0))
            p.curl(s, 55, 15)
        return p
    return fn


# --- jumps ----------------------------------------------------------------------------------------------
def air_pose(R, t, k=1.0):
    """In the air: knees tucked, toes pointed, arms up and flapping (k blends it in)."""
    p = base(R, 0, 0)
    for s, sx, ph in (('L', 1, 0.0), ('R', -1, 0.5)):
        pedal = wave(t, 1, ph)
        p.foot(s, (0.03 * sx, 0.03 + 0.05 * pedal * k, 0.0), pitch=28 * k, lift=(0.20 + 0.05 * pedal) * k)
        p.knee(s, (0.14 * sx, 0, 0.1 * k))
        flap = wave(t, 2, ph * 0.5)
        p.hand(s, Vector(((0.36 + 0.18 * k) * sx, -0.06 - 0.08 * k, 0.82 + (0.45 + 0.06 * flap) * k)),
               (0.3 * sx, -1, 0.4), (0.6 * sx * k, -0.3, -1 + 2 * k))
        p.elbow(s, (0.6, 0.3, 1.0 - 0.4 * k))
        p.shrug(s, 12 * k + 3 * flap * k)
        p.curl(s, 20, 10)
    p.hips = Vector((0, 0, 0.0))
    p.lean(-6 * k + 2 * wave(t, 2), 0, 0)
    p.look(-4 * k, 3 * wave(t, 1) * k, 0)
    p.eyes(-6 * k, 0)
    return p


def act_jump_air(R):
    return lambda t, f: air_pose(R, t)


def act_jump_start(R):
    def fn(t, f):
        crouch = keys(t, [(0, 0.0), (0.55, 1.0), (1.0, -0.3, lambda x: x ** 2)])
        launch = smooth((t - 0.6) / 0.4)
        p = base(R, 0, 0)
        p.hips = Vector((0, 0.02 * crouch, -0.13 * crouch + 0.03 * launch))
        p.add('Hips', rq(14 * crouch))
        p.lean(18 * crouch - 6 * launch)
        p.look(-16 * crouch + 2 * launch)
        for s, sx in (('L', 1), ('R', -1)):
            p.foot(s, (0.02 * sx, 0, 0), pitch=34 * launch, yaw=8)
            p.knee(s, (0.15 * sx, 0, 0))
            back = max(0.0, crouch)
            p.hand(s, Vector(((0.34 + 0.06 * launch) * sx, -0.06 + 0.16 * back - 0.12 * launch,
                              0.84 + 0.05 * back + 0.45 * launch)),
                   (0.3 * sx, -1, 0.3), (0.3 * sx * launch, -0.3 + 0.6 * back, -1 + 1.6 * launch))
            p.elbow(s, (0.6, 0.45, 1.0))
            p.shrug(s, 10 * launch)
            p.curl(s, 45, 15)
        return p
    return fn


def act_jump_land(R):
    def fn(t, f):
        air = 1 - smooth(t / 0.18)
        squash = keys(t, [(0.0, 0.0), (0.12, 0.2), (0.3, 1.0), (0.62, -0.15), (0.82, 0.05), (1.0, 0.0)])
        p = air_pose(R, 0.25, air)
        p.hips = Vector((0, 0.02 * squash, -0.17 * squash))
        p.add('Hips', rq(14 * squash))
        p.lean(18 * squash - 6 * air)
        p.look(-14 * squash - 4 * air)
        for s, sx in (('L', 1), ('R', -1)):
            if air < 1:
                p.foot(s, (0.03 * sx, 0.03 * air, 0), pitch=28 * air, lift=0.2 * air ** 2, yaw=8)
                p.knee(s, (0.16 * sx, 0, 0))
            fl = max(0.0, squash)
            p.hand(s, Vector(((0.36 + 0.18 * fl + 0.14 * air) * sx, -0.08 - 0.05 * fl, 0.82 + 0.30 * fl + 0.55 * air)),
                   (0.3 * sx, -1, 0.4), (0.6 * sx * (fl + air), -0.3, -1 + 1.5 * (fl + air)))
            p.elbow(s, (0.6, 0.3, 1.0 - 0.4 * fl))
            p.shrug(s, 10 * fl + 12 * air)
        p.eyes(4 * squash, 0)
        return p
    return fn


# --- ladder ---------------------------------------------------------------------------------------------
def act_climb(R):
    """Hand over hand up a ladder in front of the character. In place: the rungs slide down past the body
    0.6 m per loop (40 frames, 0.45 m/s); the game scales playback to the real climb speed."""
    D, C = 0.6, 0.62   # climb per loop, share of the loop a limb holds on

    def limb(u, top, bottom):
        if u < C:
            v = u / C
            return lerp(top, bottom, v), 0.0
        v = (u - C) / (1 - C)
        return lerp(bottom, top, smooth(v)), math.sin(math.pi * v)

    def fn(t, f):
        p = base(R, 0, 0)
        p.hips = Vector((0.012 * wave(t, 2), -0.08, -0.03 - 0.015 * wave(t, 2, 0.2)))
        p.add('Hips', rq(6, 0, 4 * wave(t, 1)))
        p.lean(4 + 2 * wave(t, 2), -3 * wave(t, 1, 0.1), -6 * wave(t, 1))
        p.look(-16 + 3 * wave(t, 2, 0.3), 2 * wave(t, 1), 3 * wave(t, 1))
        p.eyes(-10, 0)
        for s, sx, ph in (('L', 1, 0.0), ('R', -1, 0.5)):
            z, off = limb((t + ph) % 1.0, 1.62, 1.62 - D * C)
            p.hand(s, Vector((0.17 * sx, -0.43 + 0.08 * off, z)), (-sx, 0, 0), (0, -0.6, 1), space='world')
            p.elbow(s, (0.7, 0.2, 1.0))
            p.curl(s, 75 * (1 - off), 30)
            p.shrug(s, 6 + 4 * off)
            fz, foff = limb((t + ph + 0.5) % 1.0, 0.11 + D * C, 0.11)
            p.foot(s, (0.02 * sx, -0.16 + 0.07 * foff, fz - 0.11), pitch=-6 + 18 * foff, lift=0.04 * foff)
            p.knee(s, (0.12 * sx, 0, 0.1))
        return p
    return fn


# --- fishing --------------------------------------------------------------------------------------------
READY_GRIP = Vector((-0.20, -0.32, 1.02))
READY_ROD = Vector((0, -0.62, 0.78))


def rod_axis(rod):
    """Hand long axis for a fist on a rod: along the forearm, down and forward of the rod."""
    return Vector((0.15, -0.6, -1.0))


def fishing_stance(R, t=0.0):
    p = base(R, t, 0.6)
    p.foot('L', (0.03, -0.10, 0), yaw=10)
    p.foot('R', (-0.03, 0.08, 0), yaw=18)
    p.knee('L', (0.10, 0, 0))
    p.knee('R', (-0.12, 0, 0))
    p.hips = Vector((0, 0, -0.03))
    return p


def left_rod_hand(p, grip, rod, along=0.32):
    """Left fist on the rod's fore-grip, `along` metres up the rod from the right fist."""
    g = Vector(grip) + Vector(rod).normalized() * along
    p.hand('L', g, Vector(rod), Vector((-0.6, -0.4, -1.0)))
    p.elbow('L', (0.6, 0.6, 1.0))
    p.curl('L', 75, 30)


def act_cast(R):
    """Overhead cast: settle, wind up over the right shoulder, load, whip, release (frame 26), follow
    through, settle back to the reel-ready pose."""
    n = 45
    K = lambda f: f / n  # noqa: E731
    grip = [(K(0), READY_GRIP), (K(5), READY_GRIP + Vector((0, 0.02, -0.03))),
            (K(15), (-0.36, 0.02, 1.42)), (K(20), (-0.37, 0.07, 1.46)),
            (K(24), (-0.30, -0.22, 1.40), lambda x: x ** 1.5), (K(26), (-0.24, -0.40, 1.24), lambda x: x),
            (K(31), (-0.20, -0.45, 1.06), lambda x: back_out(x, 1.0)), (K(45), READY_GRIP)]
    rod = [(K(0), READY_ROD), (K(5), READY_ROD + Vector((0, -0.1, -0.08))),
           (K(15), (0.04, 0.55, 0.83)), (K(20), (0.05, 0.78, 0.62)),
           (K(24), (0.0, -0.30, 0.95), lambda x: x ** 1.5), (K(26), (0.0, -0.86, 0.50), lambda x: x),
           (K(31), (0.0, -0.95, 0.28), lambda x: back_out(x, 1.0)), (K(45), READY_ROD)]
    twist = [(K(0), 0.0), (K(5), 3.0), (K(15), -20.0), (K(20), -24.0), (K(26), 12.0, lambda x: x ** 1.5),
             (K(31), 16.0), (K(45), 0.0)]
    lean = [(K(0), 0.0), (K(5), 4.0), (K(15), -8.0), (K(20), -11.0), (K(26), 12.0, lambda x: x ** 1.5),
            (K(31), 16.0, back_out), (K(45), 2.0)]
    shift = [(K(0), 0.0), (K(5), 0.01), (K(18), 0.05), (K(26), -0.06), (K(33), -0.07), (K(45), 0.0)]

    def fn(t, f):
        p = fishing_stance(R)
        tw, ln, sh = keys(t, twist), keys(t, lean), keys(t, shift)
        p.hips = Vector((-0.4 * sh, sh, -0.03 - 0.03 * bump(t, K(20), K(36))))
        p.add('Hips', rq(ln * 0.2, 0, tw * 0.35))
        p.lean(ln, -0.15 * ln, tw * 0.65)
        p.look(-ln * 0.6 - 4 * bump(t, K(22), K(40)), 0, -tw * 0.7)
        p.eyes(-6 * bump(t, K(20), K(40)), 0)
        g, r = keys(t, grip), keys(t, rod)
        p.hand('R', g, r, rod_axis(r))
        p.elbow('R', (0.6, 0.35 + 0.15 * smooth((t - K(10)) / 0.2), 1.0))
        p.curl('R', 80, 35)
        p.shrug('R', 12 * bump(t, K(5), K(30)))
        # Left arm: points ahead for balance during the wind-up, swings back on the whip.
        bal = bump(t, K(5), K(26)) * 1.0
        whip = bump(t, K(20), K(45))
        p.hand('L', Vector((0.30 - 0.04 * bal, -0.06 - 0.26 * bal + 0.10 * whip, 0.84 + 0.30 * bal - 0.02 * whip)),
               (0.3, -1, 0.3), (-0.2, -0.3 - 1.0 * bal, -1 + 0.8 * bal))
        p.elbow('L', (0.55, 0.45, 1.0))
        p.curl('L', 40, 15)
        return p
    return fn


def act_reel(R):
    """Rod up at the ready, the left hand cranking the reel (one turn per loop), a little bob to the beat."""
    def fn(t, f):
        p = fishing_stance(R, t)
        p.hips += Vector((0, 0, -0.008 * (0.5 - 0.5 * wavec(t, 2))))
        p.lean(3 + 1.5 * wave(t, 2), 1 * wave(t, 1), -4)
        p.look(-3 + 1.5 * wave(t, 2, 0.15), 1.5 * wave(t, 1), 4)
        p.eyes(4, 0)
        grip = READY_GRIP + Vector((0.01 * wave(t, 1), 0, 0.012 * wave(t, 2)))
        rod = READY_ROD + Vector((0.02 * wave(t, 1, 0.2), 0, 0.03 * wave(t, 2, 0.1)))
        p.hand('R', grip, rod, rod_axis(rod))
        p.elbow('R', (0.6, 0.45, 1.0))
        p.curl('R', 80, 35)
        # The reel hangs under the rod just ahead of the right fist; its handle turns on the left side.
        rn = rod.normalized()
        under = (Vector((0, 0, -1)) - rn * rn.z).normalized()
        centre = grip + rn * 0.10 + under * 0.07 + Vector((0.075, 0, 0))
        a = -2 * math.pi * t
        knob = centre + 0.055 * (math.cos(a) * rn + math.sin(a) * under)
        p.hand('L', knob, Vector((-1, 0.0, 0)), Vector((0.0, -0.5 + 0.3 * math.cos(a), -1)))
        p.elbow('L', (0.6, 0.5, 0.95))
        p.curl('L', 70, 30)
        return p
    return fn


def act_fight(R):
    """Leaning back against a fish: wide braced stance, both fists on the rod held high, two hard heaves
    per loop and a constant strain shiver."""
    def fn(t, f):
        heave = 0.5 - 0.5 * wavec(t, 2)                         # 0 give .. 1 heave back
        heave = heave ** 0.6
        shiver = (wave(t, 13) * 0.5 + wave(t, 17, 0.3) * 0.35 + wave(t, 23, 0.6) * 0.25)
        p = base(R, 0, 0)
        p.foot('L', (0.07, -0.20, 0), yaw=10)
        p.foot('R', (-0.07, 0.17, 0), yaw=22)
        p.knee('L', (0.18, 0, 0))
        p.knee('R', (-0.18, 0, 0))
        p.hips = Vector((0.0, 0.05 + 0.04 * heave, -0.10 - 0.02 * heave + 0.003 * shiver))
        p.add('Hips', rq(-4 - 4 * heave, 0, -8))
        p.lean(-12 - 12 * heave + 1.2 * shiver, -2 + 1.0 * shiver, -20)
        p.look(14 + 8 * heave - 1.5 * shiver, 3 * wave(t, 1), 18)
        p.eyes(8, 0)
        grip = Vector((-0.16, -0.32 + 0.04 * heave, 1.00 + 0.06 * heave + 0.008 * shiver))
        rod = Vector((-0.30 + 0.02 * shiver, -0.65 + 0.25 * heave, 0.70))
        p.hand('R', grip, rod, rod_axis(rod))
        p.elbow('R', (0.6, 0.3, 1.0))
        p.curl('R', 85, 40)
        left_rod_hand(p, grip, rod, 0.13)
        return p
    return fn


def act_hold_fish(R):
    """Both fists overhead holding up a catch, bouncing on the knees, head tipped back to admire it."""
    def fn(t, f):
        b = 0.5 - 0.5 * wavec(t, 2)                       # 0 up .. 1 down, two bounces
        lag = 0.5 - 0.5 * wavec(t, 2, -0.08)
        p = base(R, 0, 0)
        for s, sx in (('L', 1), ('R', -1)):
            p.foot(s, (0.04 * sx, 0, 0), yaw=12, pitch=12 * (1 - b) * (1 if s == 'L' else 0.5))
            p.knee(s, (0.18 * sx, 0, 0))
            p.hand(s, Vector((0.21 * sx, -0.42 + 0.02 * wave(t, 1), 1.40 - 0.08 * lag + 0.02 * wave(t, 1, 0.25 * sx))),
                   (-sx * 0.2, -0.3, 1), (0, -1, 0.1))
            p.elbow(s, (0.7, 0.2, 0.9))
            p.shrug(s, 22 - 4 * lag)
            p.curl(s, 70, 30)
        p.hips = Vector((0.02 * wave(t, 1), 0, -0.02 - 0.08 * b))
        p.add('Hips', rq(4 * b, -4 * wave(t, 1), 3 * wave(t, 1, 0.25)))
        p.lean(-8 - 3 * lag, 3 * wave(t, 1, 0.1), -3 * wave(t, 1, 0.3))
        p.look(-16 + 6 * lag, 6 * wave(t, 1, 0.15), 4 * wave(t, 1, 0.3))
        p.eyes(-12, 6 * wave(t, 1))
        return p
    return fn


def act_wave(R):
    """A big friendly wave with the right hand, the body swaying along with it."""
    def fn(t, f):
        w = wave(t, 2)                                    # two waves per loop
        p = base(R, t)
        p.hips += Vector((0.015 * wave(t, 1), 0, -0.006 * abs(w)))
        p.add('Hips', rq(0, 2 * wave(t, 1), 0))
        p.lean(-2, 4 + 2 * w, 4)
        p.look(-3, 7 + 3 * wave(t, 2, -0.1), 6)
        p.eyes(-3, 4)
        p.hand('R', Vector((-0.52 - 0.07 * w, -0.14, 1.46 - 0.03 * w * w)), (1, -0.1, 0.0),
               (-0.2 + 0.35 * w, -0.25, 1))
        p.elbow('R', (0.8, 0.1, 1.0))
        p.shrug('R', 18)
        p.curl('R', 0, 0)
        relaxed_arm(p, 'L', fwd=0.01 * w, curl=40)
        return p
    return fn


# name, frames (loops: frame n repeats frame 0), loop, builder
ACTIONS = [
    ('Idle', 120, True, act_idle),
    ('Walk', GAITS['Walk'][1], True, act_walk),
    ('Run', GAITS['Run'][1], True, act_run),
    ('Sprint', GAITS['Sprint'][1], True, act_sprint),
    ('Jump_Start', 8, False, act_jump_start),
    ('Jump_Air', 20, True, act_jump_air),
    ('Jump_Land', 16, False, act_jump_land),
    ('Crouch_Idle', 60, True, act_crouch_idle),
    ('Crouch_Walk', GAITS['Crouch_Walk'][1], True, act_crouch_walk),
    ('Climb', 40, True, act_climb),
    ('Cast', 45, False, act_cast),
    ('Reel_Loop', 20, True, act_reel),
    ('Fight_Strain', 40, True, act_fight),
    ('Hold_Fish_Up', 48, True, act_hold_fish),
    ('Wave', 40, True, act_wave),
]


def anims_stage(only=None):
    bpy.ops.wm.open_mainfile(filepath=RIG_PATH)
    rig = bpy.data.objects[RIG]
    if rig.animation_data:
        rig.animation_data.action = None
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix()
    sc = bpy.context.scene
    sc.render.fps = FPS
    CONTROLS[:] = add_controls(rig)
    R = Rigger(rig)
    for name, n, loop, make in ACTIONS:
        if only and name not in only:
            continue
        act = R.bake(name, n, make(R), loop)
        extra = ' natural speed %.2f m/s' % gait_speed(*GAITS[name]) if name in GAITS else ''
        print('  action %-13s frames 0-%d %s%s' % (name, n, 'loop' if loop else '', extra))
    remove_controls(rig)
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix()
    rig.animation_data.action = bpy.data.actions['Idle'] if 'Idle' in bpy.data.actions else None
    sc.frame_start, sc.frame_end = 0, 120
    bpy.ops.wm.save_as_mainfile(filepath=RIG_PATH)
    print('ANIMS OK actions=%d' % len(bpy.data.actions))


def remove_controls(rig):
    for pb in rig.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    ctx_select(rig)
    bpy.ops.object.mode_set(mode='EDIT')
    for n in CONTROLS:
        rig.data.edit_bones.remove(rig.data.edit_bones[n])
    bpy.ops.object.mode_set(mode='OBJECT')


# ================================================================ stage 3: export
def export_stage():
    bpy.ops.wm.open_mainfile(filepath=RIG_PATH)
    rig = bpy.data.objects[RIG]
    os.makedirs(EXPORT_DIR, exist_ok=True)
    meshes = [o for o in bpy.data.collections['Fisher'].objects if o.type == 'MESH']
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix()
    common = dict(use_selection=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                  bake_space_transform=True, add_leaf_bones=False, primary_bone_axis='Y',
                  secondary_bone_axis='X', armature_nodetype='NULL', use_armature_deform_only=False,
                  bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                  bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                  bake_anim_step=1.0, bake_anim_simplify_factor=0.0, use_custom_props=False)
    ctx_select(rig, *meshes, active=rig)
    path = os.path.join(EXPORT_DIR, 'Char_Fisher.fbx')
    bpy.ops.export_scene.fbx(filepath=path, object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=False,
                             mesh_smooth_type='FACE', **common)
    print('EXPORT', path)
    ctx_select(rig)
    path = os.path.join(EXPORT_DIR, 'Char_Fisher_Anims.fbx')
    bpy.ops.export_scene.fbx(filepath=path, object_types={'ARMATURE'}, **common)
    print('EXPORT', path)


# ================================================================ stage 4: preview
def preview_stage(outdir, only=None, stills=None):
    import numpy as np
    bpy.ops.wm.open_mainfile(filepath=RIG_PATH)
    sc = bpy.context.scene
    rig = bpy.data.objects[RIG]
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 10
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 3
    W, Hh = 220, 300
    sc.render.resolution_x, sc.render.resolution_y = W, Hh
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new('W')
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.55, 0.68, 0.80, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.9
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sc.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=8)
    floor = bpy.context.active_object
    floor.data.materials.append(fg_fisher.material('M_Floor', (0.62, 0.58, 0.50), 0.9))
    # A stand-in rod on the Rod bone (1.7 m along its +Y) so grip and casting arcs are readable.
    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=1.7, vertices=8)
    rod = bpy.context.active_object
    rod.data.transform(Matrix.Rotation(math.radians(-90), 4, 'X') @ Matrix.Translation((0, 0, 0.85 - 0.25)))
    rod.data.materials.append(fg_fisher.material('M_RodPrev', (0.15, 0.1, 0.05), 0.5))
    rod.parent, rod.parent_type, rod.parent_bone = rig, 'BONE', 'Rod'
    rod.matrix_parent_inverse = Matrix()
    rod.location = (0, -rig.data.bones['Rod'].length, 0)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'
    sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(outdir, exist_ok=True)

    views = {'3q': (-40, 2.3, 1.0), 'side': (-90, 2.3, 1.0), 'back': (150, 2.3, 1.0)}

    def aim(view):
        ang, scale, z = views[view]
        a = math.radians(ang)
        cam.data.ortho_scale = scale
        cam.location = (6 * math.sin(a), -6 * math.cos(a), z)
        cam.rotation_euler = (math.radians(90), 0, a)

    def render(path):
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
        bpy.data.images.remove(img)
        return px

    def sheet(name, rows):
        hgt = sum(r[0].shape[0] for r in rows)
        wid = max(sum(p.shape[1] for p in r) for r in rows)
        out = np.ones((hgt, wid, 4), dtype=np.float32)
        y = hgt
        for r in rows:
            y -= r[0].shape[0]
            x = 0
            for p in r:
                out[y:y + p.shape[0], x:x + p.shape[1]] = p
                x += p.shape[1]
        img = bpy.data.images.new(name, wid, hgt, alpha=True)
        img.pixels[:] = out.ravel()
        img.filepath_raw = os.path.join(outdir, name + '.png')
        img.file_format = 'PNG'
        img.save()
        print('SHEET', img.filepath_raw)
        bpy.data.images.remove(img)

    tmp = os.path.join(outdir, '_frames')
    os.makedirs(tmp, exist_ok=True)
    rig.animation_data_create()
    for name, n, loop, _ in ACTIONS:
        if only and name not in only:
            continue
        act = bpy.data.actions[name]
        rig.animation_data.action = act
        if stills:   # big single frames, front three-quarter and side, for close inspection
            sc.render.resolution_x, sc.render.resolution_y = 2 * W, 2 * Hh
            for f in stills:
                row = []
                for view in ('3q', 'side', 'back'):
                    aim(view)
                    sc.frame_set(f)
                    row.append(render(os.path.join(tmp, 'still_%s_%s_%03d.png' % (name, view, f))))
                sheet('still_%s_%03d' % (name, f), [row])
            continue
        cols = min(8, n + 1)
        frames = [round(i * n / (cols - (0 if loop else 1))) for i in range(cols)]
        rows = []
        for view in ('3q', 'side'):
            aim(view)
            row = []
            for f in frames:
                sc.frame_set(f)
                row.append(render(os.path.join(tmp, '%s_%s_%03d.png' % (name, view, f))))
            rows.append(row)
        sheet('anim_' + name, rows)
    print('PREVIEW', outdir)


# ================================================================ entry
def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    stage = argv[0] if argv else 'all'
    if stage in ('rig', 'all'):
        rig_stage()
    if stage in ('anims', 'all'):
        anims_stage(argv[1].split(',') if stage == 'anims' and len(argv) > 1 else None)
    if stage in ('export', 'all'):
        export_stage()
    if stage == 'preview':
        outdir = argv[1] if len(argv) > 1 else tempfile.gettempdir()
        preview_stage(outdir, argv[2].split(',') if len(argv) > 2 else None,
                      [int(f) for f in argv[3].split(',')] if len(argv) > 3 else None)


if __name__ == '__main__':
    main()
