# FishGame player character candidates: JRPG-hero fishermen, built from text so they are reproducible.
#
# Run (headless, Blender 4.5+; EEVEE needs Mesa EGL in a container: libegl1 libegl-mesa0 libgl1-mesa-dri libgbm1):
#   blender -b --python Art/Blender/fg_hero.py -- model   <variant> <form>     save Art/Blender/fishgame_hero_<variant>_<form>.blend
#   blender -b --python Art/Blender/fg_hero.py -- preview <variant> <form> <outdir>
#   variants: drifter (after Dark Cloud), angler (after Ocarina of Time), islander (after Wind Waker)
#   forms:    jrpg (close to the source game's proportions) or toon (pushed toward Wind Waker's toon look)
#
# Direction (user, 2026-10-17): a Dark Cloud / old-school Zelda look, a JRPG-hero type fisherman with
# PEAK-style facial stylisation (flat painted shapes) and REPO-style tracking side-eyes; first close to the
# JRPG source, then slightly more toony, the way Ocarina of Time became Wind Waker.
#
# Rig contract (fg_fisher_rig.py builds the armature from this, so every variant shares one rig):
# - scene['hero_joints'] is a JSON dict of joint positions (Blender coords): hips, spine, chest, neck, head,
#   head_center, head_top, rod_grip, and per side (_l/_r) shoulder, elbow, wrist, hand_tip, hip, knee, ankle,
#   toe, eye (the eye centre on the face surface).
# - each mesh object carries ob['bone']: the Unity Humanoid bone it rides, or 'auto' (automatic weights).
#   Pupil_L / Pupil_R ride Eye_L / Eye_R, whose heads sit at head_center: turning them slides the pupils
#   over the face (the side-eye / look-at).
#
# Authoring axes: Blender Z up, the character faces -Y, feet on z = 0, metres. FBX export (-Z forward, Y up,
# bake_space_transform) gives Unity's Y up, facing +Z. Shapes are metaballs (stiffness 2, threshold 0.6: the
# surface sits at 0.575 x the element radius) so overlapping parts blend smoothly, plus a few hard-surface
# pieces (spikes, lathes). Faces are flat painted decals ray cast onto the head.

import bpy, bmesh, json, math, os, sys, tempfile
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
SURF = 0.575
HEIGHT = 1.62          # head-top height of every form; the game's capsule and camera stay the same


# ---------------------------------------------------------------- variant tables
# heads: body height in head heights (smaller = toonier). eye: eye size scale. The rest picks colours.
VARIANTS = {
    'drifter': dict(heads={'jrpg': 5.8, 'toon': 4.3}, eye={'jrpg': 0.9, 'toon': 1.25},
                    skin=(0.95, 0.74, 0.58), hair=(0.33, 0.22, 0.14), iris=(0.38, 0.25, 0.14)),
    'angler':  dict(heads={'jrpg': 5.4, 'toon': 3.9}, eye={'jrpg': 0.95, 'toon': 1.3},
                    skin=(0.97, 0.78, 0.62), hair=(0.92, 0.72, 0.30), iris=(0.20, 0.38, 0.70)),
    'islander': dict(heads={'jrpg': 4.4, 'toon': 3.1}, eye={'jrpg': 1.1, 'toon': 1.45},
                     skin=(0.93, 0.70, 0.52), hair=(0.55, 0.36, 0.18), iris=(0.15, 0.15, 0.18)),
}
SLOT = (0.22, 0.45, 0.85)      # player slot colour (host blue); Unity tints per slot
SLOTS = [(0.22, 0.45, 0.85), (0.92, 0.92, 0.90), (0.50, 0.85, 0.45), (0.60, 0.12, 0.12)]


# ---------------------------------------------------------------- basics
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def sel(ob):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob


def apply_mod(ob, m):
    sel(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)


def toon(name, rgb, shadow=(0.70, 0.66, 0.80)):
    """Two-band cel material: diffuse -> Shader to RGB -> hard ramp (shadow tint / lit) x base colour.
    The colour lives in a node called 'Base' so lineups and Unity-side tints can swap it."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    dif = nt.nodes.new('ShaderNodeBsdfDiffuse')
    s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    bw = nt.nodes.new('ShaderNodeRGBToBW')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].color = (*shadow, 1)
    ramp.color_ramp.elements[1].position = 0.16
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    base = nt.nodes.new('ShaderNodeRGB')
    base.name = 'Base'
    base.outputs[0].default_value = (*rgb, 1)
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Factor'].default_value = 1.0
    emit = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(dif.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs['Color'], bw.inputs[0])
    nt.links.new(bw.outputs[0], ramp.inputs['Fac'])
    nt.links.new(base.outputs[0], mix.inputs['A'])
    nt.links.new(ramp.outputs['Color'], mix.inputs['B'])
    nt.links.new(mix.outputs['Result'], emit.inputs['Color'])
    nt.links.new(emit.outputs[0], out.inputs['Surface'])
    return m


def flat(name, rgb):
    """Unlit flat colour (face decals: they already are the drawing)."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    base = nt.nodes.new('ShaderNodeRGB')
    base.name = 'Base'
    base.outputs[0].default_value = (*rgb, 1)
    emit = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(base.outputs[0], emit.inputs['Color'])
    nt.links.new(emit.outputs[0], out.inputs['Surface'])
    return m


def outline(ob, width):
    """Inverted-hull outline (a thin dark rim, like the game's toon shader outline)."""
    m = ob.modifiers.new('outline', 'SOLIDIFY')
    m.thickness = width
    m.offset = 1.0
    m.use_flip_normals = True
    m.use_rim = False
    m.material_offset = len(ob.data.materials)
    ol = bpy.data.materials.get('M_Outline')
    if ol is None:
        ol = flat('M_Outline', (0.10, 0.07, 0.07))
        ol.use_backface_culling = True
    ob.data.materials.append(ol)


# ---------------------------------------------------------------- shape builders
def ball(c, r, s=(1, 1, 1), rot=(0, 0, 0)):
    return ('ELLIPSOID', Vector(c), r / SURF, Vector(s), Euler([math.radians(a) for a in rot]).to_quaternion())


def capsule(a, b, r):
    a, b = Vector(a), Vector(b)
    d = b - a
    q = Vector((1, 0, 0)).rotation_difference(d.normalized())
    return ('CAPSULE', (a + b) / 2, r / SURF, Vector((d.length / 2, 1, 1)), q)


def taper(a, b, r0, r1, n=None):
    """A tapered limb: a chain of balls from radius r0 at a to r1 at b, spaced closely enough to stay smooth."""
    a, b = Vector(a), Vector(b)
    n = max(n or 0, 3, int((b - a).length / (min(r0, r1) * 0.45)) + 1)
    return [ball(a.lerp(b, i / (n - 1)), r0 + (r1 - r0) * i / (n - 1)) for i in range(n)]


def mirror(elems):
    out = list(elems)
    for kind, co, rad, size, q in elems:
        if abs(co.x) < 1e-6:
            continue
        e = q.to_euler()
        out.append((kind, Vector((-co.x, co.y, co.z)), rad, size, Euler((e.x, -e.y, -e.z)).to_quaternion()))
    return out


def blob(name, elems, mat, bone='auto', voxel=0.006, faces=4000, smooth=3, line=0.004):
    mb = bpy.data.metaballs.new('MB_' + name)
    mb.resolution = mb.render_resolution = max(0.004, voxel)
    mb.threshold = 0.6
    for kind, co, rad, size, q in elems:
        e = mb.elements.new()
        e.type = kind
        e.co = co
        e.radius = rad
        e.stiffness = 2.0
        e.rotation = q
        if kind == 'ELLIPSOID':
            e.size_x, e.size_y, e.size_z = size
        else:
            e.size_x = size.x
    tmp = bpy.data.objects.new('MB_' + name, mb)
    bpy.context.scene.collection.objects.link(tmp)
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.metaballs.remove(mb)
    me.name = name
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    m = ob.modifiers.new('r', 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel; apply_mod(ob, m)
    if smooth:
        m = ob.modifiers.new('s', 'CORRECTIVE_SMOOTH'); m.iterations = smooth; m.use_only_smooth = True; apply_mod(ob, m)
    if faces and len(me.polygons) > faces:
        m = ob.modifiers.new('d', 'DECIMATE'); m.ratio = faces / len(ob.data.polygons); apply_mod(ob, m)
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.append(mat)
    ob['bone'] = bone
    if line:
        outline(ob, line)
    return ob


def shell(name, elems, mat, hem_z, bone='auto', front_gap=0.0, neck=None, line=0.004, thick=0.008, v_drop=0.0):
    """A garment shell over the body: a metaball volume made hollow, cut off below hem_z, with an optional
    neck hole (centre, radius) and an open front of width front_gap."""
    ob = blob(name, elems, mat, bone=bone, voxel=0.006, faces=None, smooth=3, line=0)
    m = ob.modifiers.new('t', 'SOLIDIFY'); m.thickness = thick; m.offset = -1.0; apply_mod(ob, m)
    cuts = []
    # Hem cutter: everything below the hem line goes; v_drop lowers the hem toward the centre (a poncho point).
    b = bmesh.new()
    pts = [(-1.0, hem_z + v_drop), (0.0, hem_z), (1.0, hem_z + v_drop), (1.0, -2.0), (-1.0, -2.0)]
    vs = [b.verts.new((x, y, z)) for y in (-1.0, 1.0) for x, z in pts]
    n = len(pts)
    b.faces.new(vs[:n][::-1]); b.faces.new(vs[n:])
    for i in range(n):
        j = (i + 1) % n
        b.faces.new((vs[i], vs[j], vs[n + j], vs[n + i]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces)
    cuts.append(b)
    if neck is not None:
        c, r = neck
        b = bmesh.new()
        bmesh.ops.create_cone(b, cap_ends=True, segments=40, radius1=r, radius2=r * 1.1, depth=0.3,
                              matrix=Matrix.Translation(Vector(c) + Vector((0, 0, 0.12))) @ Matrix.Diagonal((1, 0.85, 1, 1)))
        cuts.append(b)
    if front_gap:
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0, matrix=Matrix.Translation((0, -0.5 - 0.02, hem_z + 0.5)) @ Matrix.Diagonal((front_gap, 1, 1, 1)))
        cuts.append(b)
    for i, cb in enumerate(cuts):
        me = bpy.data.meshes.new('cut'); cb.to_mesh(me); cb.free()
        co = bpy.data.objects.new('cut', me); bpy.context.scene.collection.objects.link(co)
        bo = ob.modifiers.new('b', 'BOOLEAN'); bo.operation = 'DIFFERENCE'; bo.solver = 'EXACT'; bo.object = co
        apply_mod(ob, bo)
        bpy.data.objects.remove(co)
    m = ob.modifiers.new('bev', 'BEVEL'); m.width = 0.003; m.segments = 2; m.limit_method = 'ANGLE'; apply_mod(ob, m)
    for p in ob.data.polygons:
        p.use_smooth = True
    if line:
        outline(ob, line)
    return ob


def spikes(name, specs, mat, bone='Head', line=0.004):
    """Hard-edged hair clumps: each spec is (base, tip, radius); cones bent a little toward their tip."""
    bm = bmesh.new()
    for base, tip, r in specs:
        base, tip = Vector(base), Vector(tip)
        d = tip - base
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        mt = Matrix.Translation(base + d / 2) @ q.to_matrix().to_4x4()
        bmesh.ops.create_cone(bm, cap_ends=False, segments=14, radius1=r, radius2=0.0, depth=d.length, matrix=mt)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.append(mat)
    ob['bone'] = bone
    if line:
        outline(ob, line)
    return ob


def lathe(name, profile, mat, bone, segs=40, line=0.004, cap=True):
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                      for i in range(segs)])
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap:
        c = bm.verts.new((0, 0, profile[-1][1]))
        for i in range(segs):
            bm.faces.new((rings[-1][i], rings[-1][(i + 1) % segs], c))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    ob['bone'] = bone
    if line:
        outline(ob, line)
    return ob


# ---------------------------------------------------------------- face decals (PEAK-flat, ray cast onto the head)
class Face:
    def __init__(self, head, center, r):
        self.head, self.c, self.r = head, Vector(center), r

    def point(self, u, v, lift=0.0):
        yaw, pitch = u / self.r, v / self.r
        d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
        hit, loc, nrm, _ = self.head.ray_cast(self.c + d * 1.0, -d)
        return (loc if hit else self.c + d * self.r) + d * lift

    def decal(self, name, outline_uv, mat, lift, bone='Head', rings=4):
        cu = sum(p[0] for p in outline_uv) / len(outline_uv)
        cv = sum(p[1] for p in outline_uv) / len(outline_uv)
        bm = bmesh.new()
        loops = []
        for k in range(rings, 0, -1):
            t = k / rings
            loops.append([bm.verts.new(self.point(cu + (u - cu) * t, cv + (v - cv) * t, lift)) for u, v in outline_uv])
        centre = bm.verts.new(self.point(cu, cv, lift))
        n = len(outline_uv)
        for a, b in zip(loops, loops[1:]):
            for i in range(n):
                j = (i + 1) % n
                bm.faces.new((a[i], a[j], b[j], b[i]))
        for i in range(n):
            bm.faces.new((loops[-1][i], loops[-1][(i + 1) % n], centre))
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        if me.polygons[0].normal.dot(me.polygons[0].center - self.c) < 0:
            me.flip_normals()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        me.materials.append(mat)
        ob['bone'] = bone
        return ob


def ellipse(cu, cv, w, h, n=28, top=1.0, bottom=1.0, tilt=0.0):
    """Ellipse outline; top/bottom scale the upper/lower halves (almond and lidded eye shapes), tilt in v per u."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        x = math.cos(a) * w / 2
        y = math.sin(a) * h / 2
        y *= top if y > 0 else bottom
        pts.append((cu + x, cv + y + tilt * x))
    return pts


def stroke(points, thick, taper_ends=True):
    left, right = [], []
    for i, (u, v) in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        du, dv = b[0] - a[0], b[1] - a[1]
        L = math.hypot(du, dv) or 1.0
        t = 0.35 + 0.65 * math.sin(math.pi * i / (len(points) - 1)) if taper_ends else 1.0
        nu, nv = -dv / L * thick / 2 * t, du / L * thick / 2 * t
        left.append((u + nu, v + nv))
        right.append((u - nu, v - nv))
    return left + right[::-1]


def join(name, obs, bone):
    sel(obs[0])
    for o in obs[1:]:
        o.select_set(True)
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = name
    ob['bone'] = bone
    return ob


# ---------------------------------------------------------------- the shared body
def joints_for(heads):
    """Joint table for a body `heads` head-heights tall (JRPG at ~5-6, Wind Waker at ~3)."""
    hh = HEIGHT / heads                      # head height (chin to crown)
    B = HEIGHT - hh                          # chin height
    hr = hh * 0.52                           # head radius (a touch wider than half its height: toon skull)
    hc = Vector((0, 0.005, B + hh * 0.47))
    sw = max(0.12, min(0.19, B * 0.135))     # half shoulder width
    J = dict(
        head_center=hc, head_top=Vector((0, 0, HEIGHT)), head=Vector((0, 0.0, B + hh * 0.08)),
        neck=Vector((0, 0.01, B - B * 0.02)), chest=Vector((0, 0.0, B * 0.82)), spine=Vector((0, 0.0, B * 0.70)),
        hips=Vector((0, 0.0, B * 0.55)),
    )
    for s, sx in (('l', 1), ('r', -1)):
        J['shoulder_' + s] = Vector((sx * sw, 0.0, B * 0.92))
        J['elbow_' + s] = Vector((sx * (sw + B * 0.07), 0.02, B * 0.71))
        J['wrist_' + s] = Vector((sx * (sw + B * 0.115), -0.01, B * 0.52))
        J['hand_tip_' + s] = Vector((sx * (sw + B * 0.13), -0.02, B * 0.44))
        J['hip_' + s] = Vector((sx * B * 0.075, 0.0, B * 0.50))
        J['knee_' + s] = Vector((sx * B * 0.08, -0.01, B * 0.27))
        J['ankle_' + s] = Vector((sx * B * 0.08, 0.0, B * 0.055))
        J['toe_' + s] = Vector((sx * B * 0.08, -B * 0.13, B * 0.02))
    J['rod_grip'] = J['wrist_r'] + Vector((0, -0.02, -B * 0.04))
    return J, hh, hr, B


def body(J, B, M, cfg, sleeves='long', legs='trousers'):
    """Skin, base clothes and limbs on the shared joint table. Returns the head object."""
    hc = J['head_center']
    hr = cfg['hr']
    head = blob('Head', [
        ball(hc, hr, (1.0, 0.97, 0.98)),
        ball(hc + Vector((0, -hr * 0.18, -hr * 0.45)), hr * 0.62, (1.0, 0.9, 0.85)),     # cheeks and soft chin
    ], M['skin'], bone='Head', voxel=max(0.004, hr / 45), faces=6000, smooth=3, line=cfg['line'])
    blob('Ears', mirror([ball(hc + Vector((hr * 0.95, 0.01, -hr * 0.12)), hr * 0.2, (0.45, 0.85, 1.25), (0, -15, 0))]),
         M['skin'], bone='Head', voxel=0.004, faces=900, smooth=2, line=cfg['line'] * 0.7)
    blob('Neck', [capsule(J['neck'] + Vector((0, 0, -B * 0.03)), J['head'], B * 0.045)], M['skin'], bone='Neck',
         voxel=0.004, faces=800, smooth=1, line=0)

    # Torso (shirt): chest, waist, hips; slim JRPG teen, a little rounder in the toon forms.
    tw = B * 0.115
    blob('Shirt', mirror([
        ball(J['chest'], tw * 1.15, (1.0, 0.72, 1.05)),
        ball(J['spine'], tw, (1.0, 0.75, 1.0)),
        ball(J['shoulder_l'] + Vector((-0.02, 0, -0.01)), B * 0.05),
        ball(J['hips'] + Vector((0, 0, B * 0.04)), tw * 1.02, (1.0, 0.78, 0.7)),
    ] + (taper(J['shoulder_l'], J['elbow_l'], B * 0.048, B * 0.042) + taper(J['elbow_l'], J['wrist_l'], B * 0.042, B * 0.04)
          if sleeves == 'long' else taper(J['shoulder_l'], J['shoulder_l'].lerp(J['elbow_l'], 0.55), B * 0.052, B * 0.048, 3))),
         M['shirt'], voxel=0.005, faces=5000, line=cfg['line'])
    if sleeves != 'long':
        blob('Arms', mirror(taper(J['shoulder_l'].lerp(J['elbow_l'], 0.4), J['elbow_l'], B * 0.038, B * 0.034, 3)
                            + taper(J['elbow_l'], J['wrist_l'], B * 0.034, B * 0.03, 4)),
             M['skin'], voxel=0.004, faces=2000, line=cfg['line'])
    # Hands: chunky JRPG gloves or bare mitten-ish hands.
    hand = []
    w, t = J['wrist_l'], J['hand_tip_l']
    hm = w.lerp(t, 0.45)
    hand.append(ball(hm, B * 0.048, (0.75, 0.95, 1.1)))
    if 'glove' in M:
        hand.append(ball(w.lerp(J['elbow_l'], 0.08), B * 0.052, (1.0, 1.0, 0.75)))   # glove cuff over the sleeve
    for ox in (-0.012, 0.0, 0.012):
        hand += taper(hm + Vector((ox, -0.01, -B * 0.02)), t + Vector((ox * 1.5, -0.012, -B * 0.01)), B * 0.016, B * 0.013, 3)
    hand += taper(hm + Vector((-0.01, -0.02, B * 0.01)), hm + Vector((-0.028, -0.045, -B * 0.015)), B * 0.015, B * 0.012, 3)
    blob('Hands', mirror(hand), M['glove'] if 'glove' in M else M['skin'], voxel=0.004, faces=2500, line=cfg['line'])

    # Legs.
    leg = (taper(J['hip_l'] + Vector((0, 0, B * 0.03)), J['knee_l'], B * 0.07, B * 0.052, 5)
           + taper(J['knee_l'], J['ankle_l'] + Vector((0, 0, B * 0.06)), B * 0.052, B * 0.042, 5))
    if legs == 'shorts':
        blob('Shorts', mirror([ball(J['hips'], B * 0.12, (1.05, 0.8, 0.6))] + taper(J['hip_l'], J['hip_l'].lerp(J['knee_l'], 0.55), B * 0.08, B * 0.075, 3)),
             M['pants'], voxel=0.005, faces=3000, line=cfg['line'])
        blob('Legs', mirror(taper(J['hip_l'].lerp(J['knee_l'], 0.4), J['knee_l'], B * 0.055, B * 0.05, 3)
                            + taper(J['knee_l'], J['ankle_l'] + Vector((0, 0, B * 0.05)), B * 0.05, B * 0.04, 4)),
             M['skin'], voxel=0.004, faces=2000, line=cfg['line'])
    else:
        blob('Pants', mirror([ball(J['hips'], B * 0.12, (1.05, 0.8, 0.6))] + leg), M['pants'], voxel=0.005, faces=4000, line=cfg['line'])
    return head


def boots(J, B, M, cfg, cuff=None, tall=0.20):
    el = []
    a, t = J['ankle_l'], J['toe_l']
    el.append(ball(a.lerp(t, 0.45) + Vector((0, 0, -B * 0.01)), B * 0.06, (0.95, 1.75, 0.75)))
    el += taper(a, a + Vector((0, 0, B * tall)), B * 0.058, B * 0.064)
    blob('Boots', mirror(el), M['boots'], voxel=0.005, faces=3000, line=cfg['line'])
    if cuff is not None:
        blob('BootCuffs', mirror([ball(a + Vector((0, 0, B * tall)), B * 0.068, (1.0, 1.0, 0.55))]), cuff,
             voxel=0.004, faces=1500, line=cfg['line'])


def eyes(F, J, cfg, M, shape='round'):
    """Painted eyes: whites + upper lash line stay on the head; iris + pupil + shine are one object per eye
    that rides its eye bone (side-eye)."""
    s = cfg['eye']
    hr = cfg['hr']
    eu, ev = hr * 0.36, -hr * 0.05
    w, h = hr * 0.30 * s, hr * 0.36 * s
    for side, sx in (('L', 1), ('R', -1)):
        u = sx * eu
        top, bot, tilt = {'round': (1.0, 1.0, 0.0), 'almond': (0.85, 0.75, 0.10 * sx), 'big': (1.05, 0.95, 0.0)}[shape]
        F.decal('EyeWhite_' + side, ellipse(u, ev, w, h, 32, top, bot, tilt), M['eye'], 0.0012)
        lash = [(u + w / 2 * math.cos(a), ev + h / 2 * top * math.sin(a) + tilt * w / 2 * math.cos(a))
                for a in [math.radians(15 + 150 * i / 10) for i in range(11)]]
        F.decal('Lash_' + side, stroke(lash, hr * 0.045 * s), M['ink'], 0.0024, rings=2)
        iw, ih = w * 0.62, h * 0.66
        pu = u - sx * w * 0.06            # resting pupils glance slightly inward: alert, not vacant
        parts = [F.decal('Iris_' + side, ellipse(pu, ev - h * 0.04, iw, ih, 28), M['iris'], 0.0018, bone='Eye_' + side),
                 F.decal('PupilDot_' + side, ellipse(pu, ev - h * 0.04, iw * 0.5, ih * 0.52, 20), M['ink'], 0.0021, bone='Eye_' + side),
                 F.decal('Shine_' + side, ellipse(pu - iw * 0.2, ev + h * 0.12, iw * 0.3, ih * 0.28, 16), M['eye'], 0.0024, bone='Eye_' + side)]
        join('Pupil_' + side, parts, 'Eye_' + side)
        J['eye_' + side.lower()] = F.point(u, ev)
    return eu, ev, w, h


def brows_mouth(F, cfg, M, eu, ev, w, h, mood='hero'):
    hr = cfg['hr']
    for side, sx in (('L', 1), ('R', -1)):
        u = sx * eu
        if mood == 'hero':        # determined: inner end low
            pts = [(u - sx * w * 0.55, ev + h * 0.72), (u, ev + h * 0.86), (u + sx * w * 0.6, ev + h * 0.84)]
        else:                     # easy-going: a soft arch
            pts = [(u - sx * w * 0.5, ev + h * 0.78), (u, ev + h * 0.9), (u + sx * w * 0.55, ev + h * 0.8)]
        smooth = [pts[0]] + [((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) for a, b in zip(pts, pts[1:])] + [pts[-1]]
        F.decal('Brow_' + side, stroke(smooth, hr * 0.07), M['hair_flat'], 0.0024, rings=2)
    mv = ev - hr * 0.62
    mw = hr * 0.26
    pts = [(t * mw / 2, mv + hr * 0.035 * t * t + hr * 0.012 * t) for t in [i / 8 * 2 - 1 for i in range(9)]]
    F.decal('Mouth', stroke(pts, hr * 0.04), M['ink'], 0.0015, rings=2)
    # Nose: a tiny drawn tick (PEAK-flat), not a sculpted lump.
    F.decal('Nose', stroke([(hr * 0.03, ev - hr * 0.30), (0.0, ev - hr * 0.36), (-hr * 0.02, ev - hr * 0.35)], hr * 0.03),
            M['nose'], 0.0014, rings=2)


# ---------------------------------------------------------------- the three heroes
def mats(v, form):
    V = VARIANTS[v]
    M = dict(
        skin=toon('M_Skin', V['skin']), hair=toon('M_Hair', V['hair']), hair_flat=flat('M_HairFlat', [c * 0.8 for c in V['hair']]),
        eye=flat('M_EyeWhite', (1.0, 0.99, 0.95)), ink=flat('M_Ink', (0.08, 0.05, 0.05)), iris=flat('M_Iris', V['iris']),
        nose=flat('M_NoseLine', [c * 0.78 for c in V['skin']]), slot=toon('M_Slot', SLOT),
    )
    return M


def build(variant, form):
    reset()
    V = VARIANTS[variant]
    heads = V['heads'][form]
    J, hh, hr, B = joints_for(heads)
    cfg = dict(hr=hr, eye=V['eye'][form], line=0.0035 if form == 'jrpg' else 0.005)
    M = mats(variant, form)
    globals()['BUILD_' + variant](J, B, M, cfg, form)
    sc = bpy.context.scene
    sc['hero_joints'] = json.dumps({k: list(v) for k, v in J.items()})
    sc['hero_variant'] = variant
    sc['hero_form'] = form
    col = bpy.data.collections.new('Hero')
    sc.collection.children.link(col)
    for ob in list(sc.collection.objects):
        sc.collection.objects.unlink(ob)
        col.objects.link(ob)


def BUILD_drifter(J, B, M, cfg, form):
    """After Dark Cloud: bandana, short poncho in the slot colour, tackle belt, big gloves, fur-cuffed boots."""
    M.update(shirt=toon('M_Shirt', (0.93, 0.90, 0.80)), pants=toon('M_Pants', (0.22, 0.20, 0.24)),
             glove=toon('M_Glove', (0.45, 0.28, 0.16)), boots=toon('M_Boots', (0.50, 0.32, 0.18)),
             fur=toon('M_Fur', (0.95, 0.90, 0.80)), band=toon('M_Bandana', (0.30, 0.50, 0.30)),
             belt=toon('M_Belt', (0.35, 0.22, 0.12)), brass=toon('M_Brass', (0.85, 0.68, 0.28)))
    head = body(J, B, M, cfg, sleeves='long', legs='trousers')
    hc, hr = J['head_center'], cfg['hr']
    F = Face(head, hc, hr)
    eu, ev, w, h = eyes(F, J, cfg, M, 'almond' if form == 'jrpg' else 'big')
    brows_mouth(F, cfg, M, eu, ev, w, h, 'hero')
    # Bandana: a cap over the top-back of the head, a fold over the brow, two knot tails at the back.
    blob('Bandana', [ball(hc + Vector((0, 0.03, hr * 0.38)), hr * 1.0, (1.06, 1.05, 0.78)),
                     ball(hc + Vector((0, -hr * 0.55, hr * 0.42)), hr * 0.55, (1.6, 0.6, 0.45))] +
         [ball(hc + Vector((0, hr * 0.98, hr * 0.12)), hr * 0.2)] +
         taper(hc + Vector((0, hr * 1.02, hr * 0.1)), hc + Vector((hr * 0.25, hr * 1.35, -hr * 0.55)), hr * 0.16, hr * 0.1, 5) +
         taper(hc + Vector((0, hr * 1.02, hr * 0.1)), hc + Vector((-hr * 0.2, hr * 1.25, -hr * 0.7)), hr * 0.15, hr * 0.09, 5),
         M['band'], bone='Head', voxel=max(0.004, hr / 50), faces=3000, line=cfg['line'])
    # Fringe spikes poking out under the bandana.
    sp = []
    for i, x in enumerate((-0.6, -0.25, 0.1, 0.45, 0.75)):
        base = hc + Vector((x * hr * 0.72, -hr * 0.80, hr * 0.48))
        sp.append((base, base + Vector((x * hr * 0.1 - hr * 0.08, -hr * 0.2, -hr * (0.5 + 0.12 * (i % 2)))), hr * 0.2))
    for sx in (1, -1):
        base = hc + Vector((sx * hr * 0.97, -hr * 0.15, hr * 0.2))
        sp.append((base, base + Vector((sx * hr * 0.06, -hr * 0.08, -hr * 0.62)), hr * 0.13))
    spikes('Hair', sp, M['hair'], line=cfg['line'])
    # Poncho: a short slot-coloured shoulder cape over the chest, cut at mid-chest, a neck opening.
    sh = J['shoulder_l'].z
    shell('Poncho', mirror([ball(J['chest'] + Vector((0, 0, B * 0.03)), B * 0.15, (1.12, 0.82, 0.9)),
                            ball(J['spine'], B * 0.135, (1.15, 0.85, 1.0)),
                            ball(J['shoulder_l'] + Vector((0.0, 0, -B * 0.01)), B * 0.075, (1.2, 1.0, 0.9))]),
          M['slot'], hem_z=J['spine'].z - B * 0.06, bone='Chest', neck=(J['neck'], B * 0.07), line=cfg['line'], v_drop=B * 0.55)
    # Belt with tackle pouches and a bobber clipped on.
    blob('Belt', [ball(J['hips'] + Vector((0, 0, B * 0.08)), B * 0.128, (1.06, 0.82, 0.16))], M['belt'], voxel=0.004, faces=1500, line=cfg['line'])
    blob('Pouches', mirror([ball(J['hips'] + Vector((B * 0.11, -B * 0.07, B * 0.05)), B * 0.04, (0.9, 0.6, 1.1))]), M['belt'],
         voxel=0.004, faces=1200, line=cfg['line'])
    blob('Bobber', [ball(J['hips'] + Vector((-B * 0.05, -B * 0.115, B * 0.05)), B * 0.025)], toon('M_Red', (0.85, 0.15, 0.12)),
         bone='Hips', voxel=0.003, faces=600, line=cfg['line'])
    boots(J, B, M, cfg, cuff=M['fur'], tall=0.17)


def BUILD_angler(J, B, M, cfg, form):
    """After Ocarina of Time: a long floppy knit fisher's cap and belted tunic in the slot colour, rod strap
    across the chest, gauntlets, tall boots."""
    M.update(shirt=toon('M_Undershirt', (0.95, 0.93, 0.86)), pants=toon('M_Tights', (0.93, 0.90, 0.82)),
             glove=toon('M_Gauntlet', (0.55, 0.33, 0.18)), boots=toon('M_Boots', (0.48, 0.30, 0.16)),
             belt=toon('M_Belt', (0.36, 0.22, 0.12)), brass=toon('M_Brass', (0.85, 0.68, 0.28)))
    head = body(J, B, M, cfg, sleeves='long', legs='trousers')
    hc, hr = J['head_center'], cfg['hr']
    F = Face(head, hc, hr)
    eu, ev, w, h = eyes(F, J, cfg, M, 'almond' if form == 'jrpg' else 'big')
    brows_mouth(F, cfg, M, eu, ev, w, h, 'hero')
    # Hair: a heavy side-parted fringe and side locks under the cap.
    sp = []
    for i, x in enumerate((-0.78, -0.48, -0.18, 0.12, 0.42, 0.7)):
        base = hc + Vector((x * hr * 0.78, -hr * (0.86 - 0.12 * abs(x)), hr * 0.42))
        sp.append((base, base + Vector((-hr * 0.2 + x * hr * 0.08, -hr * 0.2, -hr * (0.55 + 0.12 * (i % 2)))), hr * 0.27))
    for sx in (1, -1):
        for k in (0, 1):
            base = hc + Vector((sx * hr * (0.98 - 0.05 * k), -hr * (0.3 - 0.25 * k), hr * 0.32))
            sp.append((base, base + Vector((sx * hr * 0.1, -hr * 0.12, -hr * (0.85 - 0.15 * k))), hr * 0.2))
    spikes('Hair', sp, M['hair'], line=cfg['line'])
    # Cap: snug over the crown, then a long floppy tail drooping behind.
    tail = [hc + Vector((0, hr * (0.3 + 0.35 * i), hr * (0.75 - 0.08 * i - 0.06 * i * i))) for i in range(6)]
    el = [ball(hc + Vector((0, 0.01, hr * 0.32)), hr * 1.03, (1.03, 1.05, 0.82))]
    for i, p in enumerate(tail):
        el.append(ball(p, hr * (0.72 - 0.07 * i)))
    blob('Cap', el, M['slot'], bone='Head', voxel=max(0.004, hr / 50), faces=3500, line=cfg['line'])
    blob('CapBand', [ball(hc + Vector((0, 0.0, hr * 0.38)), hr * 1.05, (1.04, 1.05, 0.14))], toon('M_CapBand', [c * 0.7 for c in SLOT]),
         bone='Head', voxel=0.004, faces=1500, line=cfg['line'])
    # Tunic: slot colour, belted, flaring to mid-thigh.
    sh = J['shoulder_l'].z
    tun = mirror([ball(J['chest'], B * 0.135, (1.0, 0.75, 1.05)), ball(J['spine'], B * 0.125, (1.0, 0.78, 1.0)),
                  ball(J['shoulder_l'] + Vector((-0.015, 0, -0.005)), B * 0.058),
                  ball(J['hips'] + Vector((0, 0, -B * 0.02)), B * 0.15, (1.05, 0.85, 0.9)),
                  ball(J['hips'] + Vector((0, 0, -B * 0.13)), B * 0.165, (1.12, 0.95, 0.9))])          # skirt flare
    shell('Tunic', tun, M['slot'], hem_z=J['knee_l'].z + B * 0.11, bone='auto', neck=(J['neck'], B * 0.062),
          line=cfg['line'], thick=0.007, v_drop=-B * 0.04)
    blob('Belt', [ball(J['hips'] + Vector((0, 0, B * 0.07)), B * 0.142, (1.05, 0.82, 0.13))], M['belt'], voxel=0.004, faces=1500, line=cfg['line'])
    hit, loc, n, _ = bpy.data.objects['Belt'].ray_cast(J['hips'] + Vector((0, -1, B * 0.07)), Vector((0, 1, 0)))
    blob('Buckle', [ball(loc + Vector((0, -0.004, 0)), B * 0.028, (1.3, 0.4, 1.0))], M['brass'], bone='Hips', voxel=0.003, faces=500, line=cfg['line'])
    # Rod strap across the chest (left shoulder to right hip).
    a, b = J['shoulder_l'] + Vector((-0.035, -B * 0.095, 0.0)), J['hips'] + Vector((-B * 0.12, -B * 0.125, B * 0.09))
    blob('Strap', [capsule(a, b, B * 0.02)] + [capsule(a + Vector((0, B * 0.185, 0)), b + Vector((0, B * 0.25, 0)), B * 0.02)],
         M['belt'], bone='Chest', voxel=0.004, faces=1500, line=cfg['line'])
    boots(J, B, M, cfg, cuff=toon('M_BootCuff', (0.40, 0.25, 0.13)), tall=0.27)


def BUILD_islander(J, B, M, cfg, form):
    """After Wind Waker's island kids: a big head, a striped sailor shirt (slot-coloured stripes), rolled shorts,
    sandals-ish boots and a wide straw hat."""
    M.update(shirt=toon('M_Shirt', (0.97, 0.96, 0.92)), pants=toon('M_Shorts', (0.92, 0.55, 0.22)),
             boots=toon('M_Boots', (0.45, 0.30, 0.18)), straw=toon('M_Straw', (0.95, 0.83, 0.50)))
    head = body(J, B, M, cfg, sleeves='short', legs='shorts')
    hc, hr = J['head_center'], cfg['hr']
    F = Face(head, hc, hr)
    eu, ev, w, h = eyes(F, J, cfg, M, 'round' if form == 'jrpg' else 'big')
    brows_mouth(F, cfg, M, eu, ev, w, h, 'easy')
    # Stripes on the shirt: thin slot-coloured bands around the torso.
    sh = J['shoulder_l'].z
    for i in range(3):
        z = J['chest'].z + B * 0.03 - i * B * 0.11
        blob('Stripe%d' % i, [ball((0, 0, z), B * 0.134, (1.07, 0.84, 0.2))], M['slot'], bone='auto', voxel=0.004, faces=1500, line=0)
    blob('Collar', [ball(J['neck'] + Vector((0, 0.005, -B * 0.03)), B * 0.075, (1.25, 1.05, 0.3))], M['slot'], bone='Chest',
         voxel=0.004, faces=1200, line=cfg['line'])
    # Hair: a big tousled mop of soft clumps.
    sp = []
    for i in range(9):
        a = math.radians(-100 + i * 25)
        base = hc + Vector((math.sin(a) * hr * 0.92, -math.cos(a) * hr * 0.92, hr * 0.42))
        tip = base + Vector((math.sin(a) * hr * 0.25, -math.cos(a) * hr * 0.3, -hr * (0.55 + 0.1 * (i % 3))))
        sp.append((base, tip, hr * 0.24))
    spikes('Hair', sp, M['hair'], line=cfg['line'])
    blob('HairCap', [ball(hc + Vector((0, 0.02, hr * 0.28)), hr * 1.04, (1.04, 1.06, 0.85))], M['hair'], bone='Head',
         voxel=max(0.004, hr / 50), faces=3000, line=cfg['line'])
    # Straw hat, tipped back.
    top = hc.z + hr * 0.62
    prof = [(hr * 1.75, top - hr * 0.1), (hr * 1.8, top - hr * 0.06), (hr * 1.6, top - hr * 0.02), (hr * 0.82, top + hr * 0.02),
            (hr * 0.78, top + hr * 0.30), (hr * 0.62, top + hr * 0.42), (hr * 0.2, top + hr * 0.45)]
    hat = lathe('Hat', prof, M['straw'], 'Head', line=cfg['line'])
    band = lathe('HatBand', [(hr * 0.825, top + hr * 0.04), (hr * 0.80, top + hr * 0.14)], M['slot'], 'Head', line=0, cap=False)
    for o in (hat, band):
        o.data.transform(Matrix.Translation((0, 0, top)) @ Matrix.Rotation(math.radians(-14), 4, 'X') @ Matrix.Translation((0, 0, -top)))
        o.data.transform(Matrix.Translation((0, hr * 0.15, 0)))
    boots(J, B, M, cfg, cuff=None, tall=0.08)


# ---------------------------------------------------------------- preview (EEVEE toon)
def stage():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('W')
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes['Background']
    # The camera sees a soft sky colour; lighting only gets a little ambient, so the cel shadow band shows.
    lp = nt.nodes.new('ShaderNodeLightPath')
    sky = nt.nodes.new('ShaderNodeBackground'); sky.inputs['Color'].default_value = (0.70, 0.80, 0.90, 1)
    amb = nt.nodes.new('ShaderNodeBackground'); amb.inputs['Color'].default_value = (0.5, 0.55, 0.65, 1); amb.inputs['Strength'].default_value = 0.25
    mixs = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(lp.outputs['Is Camera Ray'], mixs.inputs['Fac'])
    nt.links.new(amb.outputs[0], mixs.inputs[1])
    nt.links.new(sky.outputs[0], mixs.inputs[2])
    nt.links.new(mixs.outputs[0], nt.nodes['World Output'].inputs['Surface'])
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(0.5)        # crisp cel shadows, no soft-shadow speckle
    sun.rotation_euler = (math.radians(50), 0, math.radians(-55))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def preview(variant, form, outdir):
    bpy.ops.wm.open_mainfile(filepath=blend_path(variant, form))
    sc, cam = stage()
    piv = bpy.data.objects.new('Pivot', None)
    sc.collection.objects.link(piv)
    for ob in bpy.data.collections['Hero'].objects:
        ob.parent = piv
    J = {k: Vector(v) for k, v in json.loads(sc['hero_joints']).items()}
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.render.resolution_x, sc.render.resolution_y = 480, 760
    cam.data.ortho_scale = 1.95
    cam.location = (0, -6, 0.86)
    tag = '%s_%s' % (variant, form)
    for name, ang in (('front', 0), ('three_quarter', -35), ('side', -90), ('back', 180)):
        piv.rotation_euler = (0, 0, math.radians(ang))
        sc.render.filepath = os.path.join(outdir, '%s_%s.png' % (tag, name))
        bpy.ops.render.render(write_still=True)
    # Face close-up, three-quarter.
    piv.rotation_euler = (0, 0, math.radians(-20))
    hc = J['head_center']
    cam.data.ortho_scale = max(0.42, (HEIGHT - hc.z) * 3.2)
    cam.location = (0, -6, hc.z + 0.02)
    sc.render.resolution_x = sc.render.resolution_y = 640
    sc.render.filepath = os.path.join(outdir, '%s_face.png' % tag)
    bpy.ops.render.render(write_still=True)
    # Side-eye check: slide the pupils by turning them about the head centre (what the Eye bones will do).
    for side in ('L', 'R'):
        p = bpy.data.objects['Pupil_' + side]
        p.data.transform(Matrix.Translation(hc) @ Matrix.Rotation(math.radians(14), 4, 'Z') @ Matrix.Translation(-hc))
    sc.render.filepath = os.path.join(outdir, '%s_sideeye.png' % tag)
    bpy.ops.render.render(write_still=True)
    print('PREVIEW', tag)


def blend_path(variant, form):
    return os.path.join(HERE, 'fishgame_hero_%s_%s.blend' % (variant, form))


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    stage_, variant, form = argv[0], argv[1], argv[2]
    if stage_ == 'model':
        build(variant, form)
        bpy.ops.wm.save_as_mainfile(filepath=blend_path(variant, form))
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
        print('MODEL OK %s %s tris=%d' % (variant, form, tris))
    elif stage_ == 'preview':
        outdir = argv[3] if len(argv) > 3 else tempfile.gettempdir()
        os.makedirs(outdir, exist_ok=True)
        preview(variant, form, outdir)


if __name__ == '__main__':
    main()
