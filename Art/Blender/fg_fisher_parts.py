# FishGame fisher: a library of swappable face and head parts (PEAK-style customisation), built from text.
#
# Run (headless), after fg_fisher.py has saved the base model:
#   blender -b --python Art/Blender/fg_fisher_parts.py -- <stage> [outdir]
#     stages: parts    build every part and save Art/Blender/fishgame_fisher_parts.blend
#             preview  labelled grid renders per category plus a random-characters sheet into outdir
#
# Nothing here is placed against fixed coordinates. Every part is fitted to the actual meshes in
# fishgame_fisher.blend (Head, Eye_L/R, Mouth), measured by ray casts, so the library re-fits when the head
# or hat is reshaped: re-run this script after `fg_fisher.py -- model`.
#
# Eyes (REPO-style, user 2026-10-17): the eyeball and its pupil both ride the Eye_L/Eye_R bone and turn on
# the ball's centre, so side-eye tracking works for every shape (the ball is an ellipsoid, so a pupil turned
# on its own would sink into the white). Shape comes from skin-coloured lids that sit just outside the ball
# and ride the Head bone (they never turn with the eye). Each lid mesh carries a "Blink" shape key; the
# resting lid line is the variant. Pupils are thin caps on the ball's surface with their origin at the eye
# centre, matching the eyeball's origin.
#
# Every part object has custom properties: slot (category), part (variant name, shared by its L/R pieces),
# bone (what it rides in Unity), and default (True on the default of each slot).

import bpy, bmesh, math, os, sys, random, tempfile
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fg_fisher as F

PARTS_PATH = os.path.join(HERE, 'fishgame_fisher_parts.blend')

# Category collections, in grid/preview order.
SLOTS = ['Eyes', 'Pupils', 'Mouths', 'Brows', 'Noses', 'FacialHair', 'Hats', 'Hair']

PCOL = {
    'hair':     (0.20, 0.12, 0.07),
    'teeth':    (0.96, 0.95, 0.90),
    'stubble':  (0.18, 0.12, 0.10),
    'bags':     (0.78, 0.52, 0.52),
    'beanie':   (0.80, 0.20, 0.15),
    'pompom':   (0.95, 0.93, 0.88),
    'trucker':  (0.15, 0.22, 0.45),
    'foam':     (0.95, 0.95, 0.92),
    'souwester': (0.98, 0.76, 0.10),
    'straw':    (0.86, 0.72, 0.42),
    'ribbon':   (0.55, 0.12, 0.10),
}


def mat(name):
    """Base-model materials are reused by name; part-only colours are made here."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    key = name[2:].lower()
    return F.material(name, PCOL[key], rough=0.8 if key != 'teeth' else 0.3)


# ---------------------------------------------------------------- measuring the head
class Eye:
    """An eyeball as an axis-aligned ellipsoid: centre c, semi-axes a. pt(d, k) maps a unit direction in
    the ball's local space onto the ellipsoid scaled by k."""

    def __init__(self, c, a, sx):
        self.c, self.a, self.sx = Vector(c), Vector(a), sx

    def pt(self, d, k=1.0):
        return self.c + Vector((self.a.x * d.x, self.a.y * d.y, self.a.z * d.z)) * k

    def metric(self, p):
        q = p - self.c
        return Vector((q.x / self.a.x, q.y / self.a.y, q.z / self.a.z)).length


def world_bbox(ob):
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector(tuple(min(p[i] for p in pts) for i in range(3)))
    hi = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
    return lo, hi


class Fit:
    """Measurements of the current Head (and eyes, mouth) by ray casting its evaluated mesh."""

    def __init__(self):
        head = bpy.data.objects['Head']
        dg = bpy.context.evaluated_depsgraph_get()
        bm = bmesh.new()
        bm.from_object(head, dg)
        bm.transform(head.matrix_world)
        bm.normal_update()
        self.bm = bm
        self.bvh = BVHTree.FromBMesh(bm)

        self.eyes = {}
        for side, sx in (('L', 1), ('R', -1)):
            lo, hi = world_bbox(bpy.data.objects['Eye_' + side])
            self.eyes[side] = Eye((lo + hi) / 2, (hi - lo) / 2, sx)
        L = self.eyes['L']
        self.eye_z = (L.c.z + self.eyes['R'].c.z) / 2
        self.eye_top = max(e.c.z + e.a.z * 1.12 for e in self.eyes.values())   # incl. the lid shell

        f, b = self.front_y(0, self.eye_z), self.back_y(0, self.eye_z)
        cy, R = (f + b) / 2, (b - f) / 2
        self.top = self.ray((0, cy, 5), (0, 0, -1))[0].z
        self.hc = Vector((0, cy, self.top - R))           # projection centre for shells (hats, hair)

        mo = bpy.data.objects.get('Mouth')
        if mo:
            lo, hi = world_bbox(mo)
            self.mouth_z = (lo.z + hi.z) / 2 - 0.004
        else:
            self.mouth_z = self.eye_z - 0.125
        # Built-in nose: the most forward point on the centre line between the eyes and the mouth.
        best = None
        z = self.eye_z - 0.02
        while z > self.mouth_z + 0.01:
            y = self.front_y(0, z)
            if best is None or y < best[0]:
                best = (y, z)
            z -= 0.002
        self.nose_tip_y, self.nose_z = best
        self.face_y = (self.front_y(0, self.nose_z + 0.045) + self.front_y(0, self.nose_z - 0.045)) / 2
        self.nose_d = self.face_y - self.nose_tip_y        # how far the built-in nose stands proud
        x = 0.0
        while x < 0.08 and self.front_y(x, self.nose_z) < self.nose_tip_y + self.nose_d * 0.85:
            x += 0.001
        self.nose_w = x                                    # its half-width
        # Chin: going down the centre line from the mouth, where the surface turns to face downwards.
        z = self.mouth_z
        while z > self.hc.z - 0.35:
            hit = self.ray((0, -3, z), (0, 1, 0))
            if hit[1].z < -0.6:
                break
            z -= 0.002
        self.chin_z = z

    def ray(self, o, d):
        loc, nrm, idx, dist = self.bvh.ray_cast(Vector(o), Vector(d).normalized())
        if loc is None:
            raise RuntimeError('ray missed the head: %s %s' % (o, d))
        return loc, nrm, dist

    def front_y(self, x, z):
        return self.ray((x, -3, z), (0, 1, 0))[0].y

    def back_y(self, x, z):
        return self.ray((x, 3, z), (0, -1, 0))[0].y

    def face_pt(self, x, z, lift=0.0):
        """Point on the face seen straight from the front, lifted along the surface normal."""
        loc, nrm, _ = self.ray((x, -3, z), (0, 1, 0))
        if nrm.y > 0:
            nrm = -nrm
        return loc + nrm * lift, nrm

    obstacles = ()   # extra BVHs (in world space) that shells must also clear, e.g. every brow part

    def shell_dist(self, d):
        """Distance from the head centre to the surface along direction d (outermost obstacle wins)."""
        d = d.normalized()
        loc, nrm, idx, dist = self.bvh.ray_cast(self.hc, d)
        best = dist if loc is not None else 0.0
        for bvh in self.obstacles:
            # Cast inwards from outside so the obstacle's outer surface is what counts.
            o = self.hc + d * 1.0
            loc, nrm, idx, dist = bvh.ray_cast(o, -d, 1.0)
            if loc is not None:
                best = max(best, 1.0 - dist)
        return best

    def inside(self, p, margin=0.0):
        """True if p is inside the head (or within margin of its surface)."""
        loc, nrm, idx, dist = self.bvh.find_nearest(p)
        if loc is None:
            return False
        return (p - loc).dot(nrm) < margin

    def push_out(self, bm, clear):
        """Move any vertex closer to the head than `clear` out along the ray from the head centre."""
        for v in bm.verts:
            d = v.co - self.hc
            need = self.shell_dist(d) + clear
            if d.length < need:
                v.co = self.hc + d.normalized() * need


# ---------------------------------------------------------------- mesh plumbing
def set_props(ob, slot, part, bone='Head', default=False, **extra):
    ob['slot'] = slot
    ob['part'] = part
    ob['bone'] = bone
    ob['default'] = default
    for k, v in extra.items():
        ob[k] = v


def link(ob, slot):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    bpy.data.collections['Parts_' + slot].objects.link(ob)


def orient(bm, ref, outward=True):
    """Make an open sheet's faces point away from (or towards) ref."""
    bm.normal_update()
    s = sum((f.calc_center_median() - ref).dot(f.normal) * f.calc_area() for f in bm.faces)
    if (s < 0) == outward:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))


def eval_mesh(bm, mods):
    """bmesh -> evaluated mesh through a modifier stack (deterministic, so shape keys line up)."""
    me = bpy.data.meshes.new('tmp')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('tmp', me)
    bpy.context.scene.collection.objects.link(ob)
    for kind, kw in mods:
        m = ob.modifiers.new(kind, kind)
        for k, v in kw.items():
            setattr(m, k, v)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return out


def solid(thick, offset=-1.0):
    return ('SOLIDIFY', dict(thickness=thick, offset=offset, use_rim=True, use_even_offset=True))


def subsurf(n=1):
    return ('SUBSURF', dict(levels=n, render_levels=n))


def keyed(name, gen, keys, mods, mats, origin=None):
    """Object from gen(None); each key in keys becomes a shape key from gen(params)."""
    me = eval_mesh(gen(None), mods)
    me.name = name
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for m in mats:
        me.materials.append(m)
    if keys:
        ob.shape_key_add(name='Basis', from_mix=False)
        for kname, p in keys.items():
            km = eval_mesh(gen(p), mods)
            if len(km.vertices) != len(me.vertices):
                raise RuntimeError('%s: shape key %s has a different vertex count' % (name, kname))
            co = [0.0] * (3 * len(km.vertices))
            km.vertices.foreach_get('co', co)
            ob.shape_key_add(name=kname, from_mix=False).data.foreach_set('co', co)
            bpy.data.meshes.remove(km)
    for p in me.polygons:
        p.use_smooth = True
    if origin is not None:
        me.transform(Matrix.Translation(-Vector(origin)), shape_keys=True)
        ob.location = origin
    return ob


def grid_faces(bm, grid, mat_fn=None, cyclic=False):
    rows, cols = len(grid), len(grid[0])
    for j in range(rows - 1):
        for i in range(cols if cyclic else cols - 1):
            i2 = (i + 1) % cols
            f = bm.faces.new((grid[j][i], grid[j][i2], grid[j + 1][i2], grid[j + 1][i]))
            if mat_fn:
                f.material_index = mat_fn(j, i)


def blob_part(name, elems, matl, voxel=0.006, faces=2000, smooth_iter=2, res=0.008):
    return F.blob(name, elems, matl, res=res, voxel=voxel, faces=faces, smooth_iter=smooth_iter)


def join(obs, name):
    F.ctx_select(obs[0])
    for o in obs[1:]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name
    return ob


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- eyes: lids per shape
def eye_dir(az, el):
    """Unit direction in eye space: az about Z from the front (-Y), positive towards +X; el up from level."""
    return Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))


# Lid lines in degrees of elevation on the ball, as functions of o = how far towards the OUTER corner
# (-1 inner corner .. +1 outer corner). 'close' is where the lids meet when blinking.
EYE_SHAPES = [
    ('Round',      dict(up=lambda o: 62, lo=lambda o: -64, close=lambda o: -8)),
    ('Sleepy',     dict(up=lambda o: 6 - 8 * o, lo=lambda o: -58, close=lambda o: -12 - 4 * o)),
    ('Angry',      dict(up=lambda o: 26 + 22 * o, lo=lambda o: -52, close=lambda o: -4 + 10 * o)),
    ('Happy',      dict(up=lambda o: 60, lo=lambda o: -4 - 30 * o * o, close=lambda o: 10 - 30 * o * o)),
    ('Wide',       dict(up=lambda o: 80, lo=lambda o: -80, close=lambda o: -6, pupil='Tiny')),
    ('Tired',      dict(up=lambda o: 22 - 10 * o, lo=lambda o: -36, close=lambda o: -14, bags=True)),
    ('Suspicious', dict(up=lambda o: 13 - 3 * o, lo=lambda o: -17 + 3 * o, close=lambda o: -4)),
    ('Goofy',      dict(up=lambda o: 58, lo=lambda o: -62, close=lambda o: -8,
                        small=dict(up=lambda o: 20 + 6 * o, lo=lambda o: -40, close=lambda o: -12),
                        scale=dict(L=1.28, R=0.78))),
]


def lid_bm(eye, sp, blink):
    """Upper and lower lid shells around an eyeball. blink=None: resting lid line; else closed."""
    bm = bmesh.new()
    cols, rows = 24, 7
    azs = [math.radians(-118 + 236 * i / cols) for i in range(cols + 1)]

    def lid(start, edge, k0, bags=False):
        grid = []
        for j in range(rows + 1):
            t = j / rows
            row = []
            for az in azs:
                o = az * eye.sx / (math.pi / 2)
                el = start + (edge(o) - start) * t
                k = k0 + 0.018 * smoothstep(0.7, 1.0, t)               # lid lip rolls out a little
                if bags:
                    k += 0.10 * math.exp(-((t - 0.55) / 0.22) ** 2) * max(0.0, math.cos(az)) ** 2
                row.append(bm.verts.new(eye.pt(eye_dir(az, math.radians(el)), k)))
            grid.append(row)
        mf = (lambda j, i: 1 if 0.25 <= j / rows <= 0.75 else 0) if bags else None
        grid_faces(bm, grid, mf)

    up, lo = sp['up'], sp['lo']
    if blink:
        c = sp['close']
        up = (lambda o, c=c: c(o) - 2)
        lo = (lambda o, c=c: c(o) + 3)
    lid(90, up, 1.13)
    lid(-90, lo, 1.10, bags=sp.get('bags', False))
    orient(bm, eye.c)
    return bm


def goofy_ball(fit, side, s):
    """A resized eyeball for the goofy shape: same x/z, re-sunk 45% into the face like the default."""
    e = fit.eyes[side]
    a = e.a * s
    x = e.c.x + e.sx * (s - 1) * e.a.x * 0.6      # big eye nudges outwards, small one inwards
    z = e.c.z + (s - 1) * e.a.z * 0.35
    # Sink it as deep (relative to its size) as the default ball sits in the face.
    ratio = (e.c.y - fit.front_y(e.c.x, e.c.z)) / e.a.y
    return Eye(Vector((x, fit.front_y(x, z) + ratio * a.y, z)), a, e.sx)


def build_eyes(fit):
    for idx, (shape, sp) in enumerate(EYE_SHAPES):
        part = 'Eyes_' + shape
        for side in ('L', 'R'):
            eye, spec = fit.eyes[side], sp
            extra = {}
            if 'scale' in sp:
                s = sp['scale'][side]
                eye = goofy_ball(fit, side, s)
                if s < 1:
                    spec = sp['small']
                ball = sphere_mesh(part + '_Ball_' + side, eye, mat('M_Eye'))
                set_props(ball, 'Eyes', part, bone='Eye_' + side, order=idx, eye_center=list(eye.c), eye_scale=s)
                link(ball, 'Eyes')
                extra = dict(eye_center=list(eye.c), eye_scale=s)
            ob = keyed(part + '_Lid_' + side, lambda b, eye=eye, spec=spec: lid_bm(eye, spec, b),
                       {'Blink': True}, [solid(0.004), subsurf(1)], [mat('M_Skin'), mat('M_Bags')],
                       origin=eye.c)
            set_props(ob, 'Eyes', part, default=(shape == 'Round'), order=idx,
                      pupil=sp.get('pupil', 'Dot'), **extra)
            link(ob, 'Eyes')


def sphere_mesh(name, eye, m):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=1.0)
    for v in bm.verts:
        v.co = eye.pt(v.co.normalized())
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.transform(Matrix.Translation(-eye.c))
    ob = bpy.data.objects.new(name, me)
    ob.location = eye.c
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = True
    return ob


# ---------------------------------------------------------------- pupils
PUPILS = [
    ('Dot',     dict(rad=24)),
    ('Big',     dict(rad=34)),
    ('Tiny',    dict(rad=12)),
    ('Star',    dict(rad=32, star=True)),
    ('Sparkle', dict(rad=26, sparkle=True)),
]


def pupil_bm(eye, sp):
    bm = bmesh.new()
    segs, rings = 60, 6
    fwd, X, Z = Vector((0, -1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))

    def disk(centre_dir, rad, k, star, mi):
        # centre_dir: unit vector; build a cap around it
        ax1 = centre_dir.cross(Z).normalized()
        ax2 = ax1.cross(centre_dir).normalized()
        cv = bm.verts.new(eye.pt(centre_dir, k))
        prev = None
        rings_v = []
        for r in range(1, rings + 1):
            ring = []
            for s in range(segs):
                psi = 2 * math.pi * s / segs
                rr = rad
                if star:
                    f = abs(((psi - math.pi / 2) * 5 / (2 * math.pi)) % 1.0 - 0.5) * 2   # 1 at the points
                    rr = rad * (0.48 + 0.52 * f)
                rho = math.radians(rr) * r / rings
                d = centre_dir * math.cos(rho) + (ax1 * math.cos(psi) + ax2 * math.sin(psi)) * math.sin(rho)
                ring.append(bm.verts.new(eye.pt(d, k)))
            rings_v.append(ring)
        for s in range(segs):
            bm.faces.new((cv, rings_v[0][s], rings_v[0][(s + 1) % segs])).material_index = mi
        grid_faces(bm, rings_v, lambda j, i: mi, cyclic=True)

    disk(fwd, sp['rad'], 1.022, sp.get('star', False), 0)
    if sp.get('sparkle'):
        # Catch-light: same side on both eyes, as if lit from one place.
        d = (fwd * math.cos(math.radians(11)) +
             Vector((-0.6, 0, 0.8)).normalized() * math.sin(math.radians(11))).normalized()
        disk(d, 6, 1.032, False, 1)
    orient(bm, eye.c)
    return bm


def build_pupils(fit):
    for idx, (name, sp) in enumerate(PUPILS):
        for side in ('L', 'R'):
            eye = fit.eyes[side]
            ob = keyed('Pupil_%s_%s' % (name, side), lambda b, eye=eye, sp=sp: pupil_bm(eye, sp), None,
                       [solid(0.0015)], [mat('M_Pupil'), mat('M_Eye')], origin=eye.c)
            set_props(ob, 'Pupils', 'Pupil_' + name, bone='Eye_' + side, default=(name == 'Dot'), order=idx)
            link(ob, 'Pupils')


# ---------------------------------------------------------------- mouths
def sq(u):
    return max(0.0, 1 - u * u)


# up/lo: lip lines in metres relative to the mouth centre, as functions of u (-1 left .. +1 right).
# opn: how far the lower lip drops (and the upper rises) at full "Open".
MOUTHS = [
    ('Smile', dict(w=0.050, up=lambda u: 0.016 * u * u - 0.003,
                   lo=lambda u: 0.016 * u * u - 0.005 - 0.012 * sq(u))),
    ('Flat', dict(w=0.040, up=lambda u: 0.0025, lo=lambda u: -0.0025)),
    ('Grin', dict(w=0.060, up=lambda u: 0.018 * u * u - 0.001,
                  lo=lambda u: 0.018 * u * u - 0.004 - 0.026 * sq(u) ** 0.8, teeth=0.010)),
    ('O', dict(w=0.022, up=lambda u: 0.016 * sq(u) ** 0.5, lo=lambda u: -0.016 * sq(u) ** 0.5,
               opn_w=1.3, opn_up=0.005)),
    ('Smirk', dict(w=0.045, xoff=0.008, up=lambda u: 0.020 * ((u + 1) / 2) ** 2.5 - 0.002,
                   lo=lambda u: 0.020 * ((u + 1) / 2) ** 2.5 - 0.006 - 0.004 * sq(u))),
    ('Frown', dict(w=0.042, up=lambda u: -0.014 * u * u + 0.004,
                   lo=lambda u: -0.014 * u * u - 0.001 - 0.004 * sq(u))),
    ('BuckTeeth', dict(w=0.046, up=lambda u: 0.010 * u * u + 0.002,
                       lo=lambda u: 0.010 * u * u - 0.003 - 0.008 * sq(u), buck=0.022)),
]


def mouth_bm(fit, sp, opened):
    bm = bmesh.new()
    cols, rows = 32, 4
    w = sp['w'] * (sp.get('opn_w', 1.0) if opened else 1.0)
    xo = sp.get('xoff', 0.0)
    drop = 0.024 if opened else 0.0
    rise = sp.get('opn_up', 0.003) if opened else 0.0
    z0 = fit.mouth_z

    def lines(u):
        up = sp['up'](u) + rise * sq(u) ** 0.5
        lo = sp['lo'](u) - drop * sq(u) ** 0.7
        return up, min(lo, up - 0.0015)

    us = [-1 + 2 * i / cols for i in range(cols + 1)]
    grid = []
    for r in range(rows + 1):
        row = []
        for u in us:
            up, lo = lines(u)
            p, _ = fit.face_pt(xo + u * w, z0 + up + (lo - up) * r / rows, 0.0025)
            row.append(bm.verts.new(p))
        grid.append(row)
    grid_faces(bm, grid)

    if sp.get('teeth'):
        tg = []
        tu = [u * 0.82 for u in us]
        for r in range(3):
            row = []
            for u in tu:
                up, lo = lines(u)
                bot = max(up - sp['teeth'], lo + 0.0015)
                p, _ = fit.face_pt(xo + u * w, z0 + up - 0.0008 + (bot - up) * r / 2, 0.0033)
                row.append(bm.verts.new(p))
            tg.append(row)
        grid_faces(bm, tg, lambda j, i: 1)
    if sp.get('buck'):
        for a, b in ((-0.30, -0.02), (0.02, 0.30)):
            tg = []
            for r in range(4):
                row = []
                for i in range(5):
                    u = a + (b - a) * i / 4
                    up, _ = lines(u)
                    zz = z0 + up - 0.001 - sp['buck'] * r / 3
                    # round the bottom corners
                    if r == 3:
                        zz += 0.004 * (abs((i - 2) / 2.0)) ** 2
                    p, _ = fit.face_pt(xo + u * w, zz, 0.0045)
                    row.append(bm.verts.new(p))
                tg.append(row)
            grid_faces(bm, tg, lambda j, i: 1)
    orient(bm, fit.hc)
    return bm


def build_mouths(fit):
    for idx, (name, sp) in enumerate(MOUTHS):
        ob = keyed('Mouth_' + name, lambda o, sp=sp: mouth_bm(fit, sp, o), {'Open': True},
                   [solid(0.003)], [mat('M_Mouth'), mat('M_Teeth')])
        set_props(ob, 'Mouths', 'Mouth_' + name, default=(name == 'Smile'), order=idx)
        link(ob, 'Mouths')


# ---------------------------------------------------------------- brows
def brow_pts(fit, side, n, f):
    """Points along a brow over one eye: f(t) -> (dz, r) for t 0 (inner) .. 1 (outer)."""
    e = fit.eyes[side]
    out = []
    for i in range(n):
        t = i / (n - 1)
        x = e.c.x + e.sx * (-0.70 + 1.55 * t) * e.a.x
        dz, r = f(t)
        z = fit.eye_top + 0.004 + r + dz
        out.append((x, z, r))
    return out


def brow_elems(fit, pts):
    el = []
    for x, z, r in pts:
        p, n = fit.face_pt(x, z, r * 0.30)
        for e in fit.eyes.values():        # never sink into the eyeball or its lids
            if e.metric(p) < 1.18 + r / e.a.z:
                q = p - e.c
                p = e.c + q * ((1.18 + r / e.a.z) / e.metric(p))
        el.append(F.ball(p, r, (1.0, 0.8, 0.9)))
    return el


BROWS = [
    ('Soft',    lambda t: (0.010 * math.sin(math.pi * t), 0.016 * (1 - 0.35 * t))),
    ('Bushy',   lambda t: (0.006 * math.sin(math.pi * t), 0.023 * (1 - 0.15 * t))),
    ('Angry',   lambda t: (0.030 * t - 0.014, 0.018 * (1 - 0.25 * t))),
    ('Worried', lambda t: (0.024 * (1 - t) - 0.006 + 0.004 * math.sin(math.pi * t), 0.015 * (1 - 0.3 * t))),
    ('Thin',    lambda t: (0.013 * math.sin(math.pi * t), 0.0095)),
    ('Unibrow', None),
]


def build_brows(fit):
    m = mat('M_Hair')
    for idx, (name, f) in enumerate(BROWS):
        part = 'Brow_' + name
        if f is None:
            # One bushy brow across both eyes, sagging a little in the middle.
            pts = []
            L, R = fit.eyes['L'], fit.eyes['R']
            x0, x1 = R.c.x - 0.85 * R.a.x, L.c.x + 0.85 * L.a.x
            for i in range(25):
                t = i / 24
                x = x0 + (x1 - x0) * t
                pts.append((x, fit.eye_top + 0.010 + 0.014 - 0.006 * math.cos(math.pi * (2 * t - 1)) ** 2
                            + 0.004 * math.sin(math.pi * t), 0.020))
            ob = blob_part(part, brow_elems(fit, pts), m, voxel=0.004, faces=1500)
            set_props(ob, 'Brows', part, order=idx)
            link(ob, 'Brows')
            continue
        for side in ('L', 'R'):
            ob = blob_part('%s_%s' % (part, side), brow_elems(fit, brow_pts(fit, side, 17, f)), m,
                           voxel=0.004, faces=900)
            set_props(ob, 'Brows', part, default=(name == 'Soft'), order=idx)
            link(ob, 'Brows')


# ---------------------------------------------------------------- noses
def build_noses(fit):
    """Noses sit on the built-in head nose and always enclose it (checked)."""
    z, ty, d, w = fit.nose_z, fit.nose_tip_y, fit.nose_d, fit.nose_w
    m = mat('M_Skin')
    noses = [
        ('Button',   [F.ball((0, ty + d * 0.85, z), w * 1.15, (1.0, d * 1.05 / w, 0.95))]),
        ('BigRound', [F.ball((0, ty + d * 0.45, z - 0.004), d * 1.85, (1.05, 0.95, 0.95)),
                      F.ball((0.022, ty + d * 0.9, z - 0.014), d * 0.85),
                      F.ball((-0.022, ty + d * 0.9, z - 0.014), d * 0.85)]),
        ('Pointy',   [F.ball((0, ty + d * 0.9, z + 0.004), w * 1.12, (1.0, 0.8, 1.15)),
                      F.capsule((0, ty + d * 0.3, z + 0.002), (0, ty - 0.030, z - 0.004), 0.017),
                      F.capsule((0, ty - 0.030, z - 0.004), (0, ty - 0.056, z - 0.006), 0.009)]),
        ('Potato',   [F.ball((0, ty + d * 0.45, z - 0.012), d * 1.75, (1.05, 0.9, 1.05)),
                      F.ball((0, ty + d * 0.6, z + 0.016), d * 0.9),
                      F.ball((0.026, ty + d * 0.95, z - 0.018), d * 0.95),
                      F.ball((-0.026, ty + d * 0.95, z - 0.018), d * 0.95),
                      F.ball((0.010, ty + d * 0.3, z + 0.002), d * 0.7)]),
        ('Hook',     [F.ball((0, ty + d * 0.9, z + 0.010), w * 1.1, (0.95, 0.8, 1.3)),
                      F.capsule((0, ty + d * 0.4, z + 0.030), (0, ty - 0.030, z + 0.004), 0.016),
                      F.capsule((0, ty - 0.030, z + 0.004), (0, ty - 0.034, z - 0.026), 0.017),
                      F.ball((0, ty + d * 0.6, z - 0.006), d * 1.1)]),
    ]
    # Built-in nose sample: head vertices standing proud of the face near the centre line.
    probe = [v.co.copy() for v in fit.bm.verts
             if v.co.y < fit.face_y - 0.006 and abs(v.co.x) < 0.06 and abs(v.co.z - z) < 0.06]
    for idx, (name, el) in enumerate(noses):
        ob = blob_part('Nose_' + name, el, m, voxel=0.003, faces=1500)
        set_props(ob, 'Noses', 'Nose_' + name, default=(name == 'Button'), order=idx)
        link(ob, 'Noses')
        bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
        bad = 0
        for p in probe:
            loc, nrm, _, _ = bvh.find_nearest(p)
            if (p - loc).dot(nrm) > 0.0005:
                bad += 1
        print('CHECK nose %-9s built-in nose points poking out: %d/%d' % (name, bad, len(probe)))


# ---------------------------------------------------------------- facial hair
def moustache_elems(fit, w, f, n=15):
    """f(u) -> (dz, r). The line sits between the nose and the top of the mouth."""
    zm = (fit.nose_z - fit.nose_d * 0.9 + fit.mouth_z + 0.008) / 2
    el = []
    for i in range(n):
        u = -1 + 2 * i / (n - 1)
        dz, r = f(u)
        p, _ = fit.face_pt(u * w, zm + dz, r * 0.35)
        el.append(F.ball(p, r, (1.0, 0.75, 0.85)))
    return el, zm


def build_facial_hair(fit):
    m = mat('M_Hair')
    idx = 0

    el, _ = moustache_elems(fit, 0.066, lambda u: (-0.026 * u ** 4 - 0.004 * u * u, 0.022 * (1 - 0.30 * u * u)), n=19)
    ob = blob_part('FacialHair_Walrus', el, m, voxel=0.004, faces=1500)
    set_props(ob, 'FacialHair', 'FacialHair_Walrus', order=idx)
    link(ob, 'FacialHair')
    idx += 1

    # Handlebar: thin bar with ends that curl up and back in.
    # Sits just above the lip, below the nose's wings.
    hw = max(0.050, fit.nose_w * 1.25)                 # curls clear the nose's wings
    el, zm = moustache_elems(fit, hw, lambda u: (-0.018 - 0.004 * u * u, 0.014 * (1 - 0.5 * abs(u))), n=19)
    for sx in (1, -1):
        for i in range(1, 13):
            a = math.pi * 1.4 * i / 12
            x = sx * (hw + 0.013 * math.sin(a))
            zz = zm - 0.022 + 0.013 * (1 - math.cos(a))
            p, _ = fit.face_pt(x, zz, 0.005)
            el.append(F.ball(p, 0.0085 * (1 - 0.3 * i / 12)))
    ob = blob_part('FacialHair_Handlebar', el, m, voxel=0.003, faces=1500)
    set_props(ob, 'FacialHair', 'FacialHair_Handlebar', order=idx)
    link(ob, 'FacialHair')
    idx += 1

    # Full beard: chunky clumps over the jaw and up the cheeks to sideburns, a window left for the mouth.
    rnd = random.Random(7)
    el = []
    win_w, win_lo = 0.058, fit.mouth_z - 0.036
    for i in range(28):
        az = math.radians(-112 + 224 * i / 27)
        for j in range(12):
            elv = math.radians(-8 - 62 * j / 11)
            d = Vector((math.sin(az) * math.cos(elv), -math.cos(az) * math.cos(elv), math.sin(elv)))
            loc, nrm, idx_, dist = fit.bvh.ray_cast(fit.hc, d)
            if loc is None:
                continue
            side = abs(math.degrees(az))
            top = fit.mouth_z - 0.004 + 0.075 * smoothstep(62, 100, side)     # sideburns rise at the sides
            if loc.z > top or loc.z < fit.chin_z - 0.035:
                continue
            if abs(loc.x) < win_w and loc.z > win_lo and loc.y < fit.hc.y:
                continue
            r = 0.021 * rnd.uniform(0.85, 1.15)
            el.append(F.ball(loc + d * 0.003, r))
    ob = blob_part('FacialHair_Beard', el, m, voxel=0.006, faces=3500, smooth_iter=2, res=0.010)
    set_props(ob, 'FacialHair', 'FacialHair_Beard', order=idx)
    link(ob, 'FacialHair')
    idx += 1

    # Stubble: a thin shell copied from the head's lower face, to be drawn as a decal-like layer.
    bm = fit.bm.copy()
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        d = c - fit.hc
        az = abs(math.degrees(math.atan2(d.x, -d.y)))
        top = fit.mouth_z + 0.022 + 0.06 * smoothstep(60, 100, az) - 0.012 * smoothstep(0.03, 0.06, abs(c.x)) * (1 - smoothstep(60, 100, az))
        nose = abs(c.x) < 0.03 and c.z > fit.mouth_z + 0.015
        mouth = abs(c.x) < 0.05 and abs(c.z - fit.mouth_z) < 0.010
        if az > 112 or c.z > top or c.z < fit.chin_z - 0.04 or nose or mouth:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    edge = [v for v in bm.verts if v.is_boundary]      # the cut follows voxel faces: relax its zigzag
    for _ in range(10):
        bmesh.ops.smooth_vert(bm, verts=edge, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.0012
    me = bpy.data.meshes.new('FacialHair_Stubble')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('FacialHair_Stubble', me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(stubble_material())
    for p in me.polygons:
        p.use_smooth = True
    set_props(ob, 'FacialHair', 'FacialHair_Stubble', order=idx, decal=True)
    link(ob, 'FacialHair')
    idx += 1

    ob = bpy.data.objects.new('FacialHair_None', None)
    bpy.context.scene.collection.objects.link(ob)
    set_props(ob, 'FacialHair', 'FacialHair_None', default=True, order=idx)
    link(ob, 'FacialHair')


def stubble_material():
    """Skin speckled with dark dots (preview only; Unity gets a decal texture)."""
    m = bpy.data.materials.get('M_Stubble')
    if m:
        return m
    m = F.material('M_Stubble', PCOL['stubble'], rough=0.9)
    nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    noise = nt.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 260.0
    noise.inputs['Detail'].default_value = 0.0
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.44
    ramp.color_ramp.elements[0].color = (*PCOL['stubble'], 1)
    ramp.color_ramp.elements[1].position = 0.50
    ramp.color_ramp.elements[1].color = (0.82, 0.58, 0.46, 1)    # shaved-skin shadow
    nt.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    return m


# ---------------------------------------------------------------- hair (for when the hat is off)
def fib_dirs(n):
    g = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(1 - z * z)
        yield Vector((r * math.cos(g * i), r * math.sin(g * i), z))


def hairline(fit, d, front, back, power):
    az = abs(math.atan2(d.x, -d.y))                   # 0 at the front, pi at the back
    return front + (back - front) * ((1 - math.cos(az)) / 2) ** power


def scalp_elems(fit, r, off, line, n=900, seed=1, jitter=0.15):
    rnd = random.Random(seed)
    el = []
    for d in fib_dirs(n):
        if d.z < -0.6:
            continue
        loc, nrm, _, _ = fit.bvh.ray_cast(fit.hc, d)
        if loc is None or loc.z < line(d) + r * 0.45:
            continue
        el.append(F.ball(loc + d * off, r * rnd.uniform(1 - jitter, 1 + jitter)))
    return el


def build_hair(fit):
    m = mat('M_Hair')
    front = fit.eye_top + 0.066
    natural = lambda d: hairline(fit, d, front, fit.hc.z - 0.11, 1.4)
    styles = []
    styles.append(('Buzz', scalp_elems(fit, 0.018, -0.008, natural, n=1100, jitter=0.05)))
    bowl = lambda d: hairline(fit, d, fit.eye_top + 0.040, fit.eye_top - 0.035, 1.0)
    styles.append(('Bowl', scalp_elems(fit, 0.034, 0.008, bowl, n=800, jitter=0.05)))
    quiff = scalp_elems(fit, 0.026, 0.0, natural, n=900)
    # Pompadour: a wide roll swept up and forward over the forehead.
    for el_deg, out, r, sxz in ((40, 0.022, 0.040, 1.9), (52, 0.034, 0.046, 1.8), (64, 0.030, 0.042, 1.6),
                                (76, 0.016, 0.036, 1.5)):
        d = Vector((0.0, -math.cos(math.radians(el_deg)), math.sin(math.radians(el_deg))))
        loc = fit.bvh.ray_cast(fit.hc, d)[0]
        quiff.append(F.ball(loc + d * out, r, (sxz, 1.0, 0.75), (-35, 0, 0)))
    styles.append(('Quiff', quiff))
    messy = scalp_elems(fit, 0.026, 0.0, natural, n=900, seed=3)
    rnd = random.Random(11)
    # Chunky tufts: short fat cones (a ball tapering into a smaller one) sticking out of the crown.
    for d in fib_dirs(46):
        if d.z < 0.25 or d.y < -0.7:
            continue
        loc = fit.bvh.ray_cast(fit.hc, d)[0]
        a = (d + Vector((rnd.uniform(-.35, .35), rnd.uniform(-.35, .35), 0.25))).normalized()
        L = rnd.uniform(0.035, 0.055)
        messy.append(F.ball(loc + a * (0.018 + L * 0.35), 0.024))
        messy.append(F.ball(loc + a * (0.018 + L), 0.013))
    styles.append(('Messy', messy))
    for idx, (name, el) in enumerate(styles):
        ob = blob_part('Hair_' + name, el, m, voxel=0.007, faces=3500, smooth_iter=3, res=0.010)
        set_props(ob, 'Hair', 'Hair_' + name, default=(name == 'Buzz'), order=idx)
        link(ob, 'Hair')


# ---------------------------------------------------------------- hats
class HatFrame:
    """A band plane resting on the head: front edge just above the eyes, back edge lower, so hats sit
    tipped back. Band radius r(theta) is measured by ray casts within that plane."""

    def __init__(self, fit, zf, zb, segs=72):
        pf = Vector((0, fit.front_y(0, zf), zf))
        pb = Vector((0, fit.back_y(0, zb), zb))
        d = pb - pf
        self.n = Vector((0, -d.z, d.y)).normalized()
        self.e1 = Vector((1, 0, 0))
        self.f = Vector((0, -self.n.z, self.n.y))     # in-plane, pointing forward
        self.p0 = (pf + pb) / 2
        self.segs = segs
        self.r = []
        for i in range(segs):
            th = 2 * math.pi * i / segs
            loc, nrm, _, dist = fit.bvh.ray_cast(self.p0, self.u(th))
            self.r.append(dist if loc is not None else 0.2)

    def u(self, th):
        return self.f * math.cos(th) + self.e1 * math.sin(th)

    def r_at(self, th):
        x = (th % (2 * math.pi)) / (2 * math.pi) * self.segs
        i = int(x)
        t = x - i
        return self.r[i % self.segs] * (1 - t) + self.r[(i + 1) % self.segs] * t

    def pt(self, th, rad, h):
        return self.p0 + self.u(th) * rad + self.n * h


def hat_bm(fr, rows_fn, apex_h, mat_fn=lambda th, k: 0):
    """Lathe-like hat around the band plane: rows_fn(th, r) -> [(radius, height)], brim lip first."""
    bm = bmesh.new()
    grid = []
    ths = [2 * math.pi * i / fr.segs for i in range(fr.segs)]
    for i, th in enumerate(ths):
        grid.append([bm.verts.new(fr.pt(th, rad, h)) for rad, h in rows_fn(th, fr.r[i])])
    apex = bm.verts.new(fr.p0 + fr.n * apex_h)
    rows = len(grid[0])
    for i in range(fr.segs):
        i2 = (i + 1) % fr.segs
        for k in range(rows - 1):
            f = bm.faces.new((grid[i][k], grid[i2][k], grid[i2][k + 1], grid[i][k + 1]))
            f.material_index = mat_fn(ths[i], k)
        bm.faces.new((grid[i][-1], grid[i2][-1], apex)).material_index = mat_fn(ths[i], rows - 1)
    return bm


def finish_hat(name, bm, fit, mats, thick, clear, extra_obs=()):
    orient(bm, fit.hc)
    fit.push_out(bm, clear + thick)
    ob = keyed(name, lambda _: bm.copy(), None, [solid(thick), subsurf(1)], mats)
    bm.free()
    # Final guarantee: nothing of the hat inside the head after smoothing.
    me = ob.data
    for v in me.vertices:
        d = v.co - fit.hc
        need = fit.shell_dist(d) + 0.002
        if d.length < need:
            v.co = fit.hc + d.normalized() * need
    if extra_obs:
        ob = join([ob] + list(extra_obs), name)
    set_props(ob, 'Hats', name, default=(name == 'Hat_Bucket'))
    link(ob, 'Hats')
    return ob


def build_hats(fit):
    # Hats must clear any brow a player can pick, not just the head.
    dg = bpy.context.evaluated_depsgraph_get()
    fit.obstacles = [BVHTree.FromObject(o, dg) for o in bpy.data.collections['Parts_Brows'].objects]
    zf, zb = fit.eye_top + 0.014, fit.hc.z - 0.005
    fr = HatFrame(fit, zf, zb)
    order = 0

    # Bucket (default): the base model's hat and band, copied as-is so the default matches the model.
    hat, band = bpy.data.objects.get('Hat'), bpy.data.objects.get('HatBand')
    if hat:
        obs = []
        for o in (hat, band):
            if o:
                c = o.copy()
                c.data = o.data.copy()
                bpy.context.scene.collection.objects.link(c)
                obs.append(c)
        ob = join(obs, 'Hat_Bucket')
        for v in ob.data.vertices:          # only moves what would poke through the head or a brow
            d = v.co - fit.hc
            need = fit.shell_dist(d) + 0.002
            if d.length < need:
                v.co = fit.hc + d.normalized() * need
        set_props(ob, 'Hats', 'Hat_Bucket', default=True, order=order, source='fg_fisher.build_hat')
        link(ob, 'Hats')
    order += 1

    # Beanie: hugs the head with a rolled cuff, slouchy top and a pom-pom.
    cl = 0.012

    def beanie(th, r):
        R = r + cl
        return [(R + 0.010, -0.006), (R + 0.014, 0.010), (R + 0.014, 0.032), (R + 0.003, 0.040),
                (R * 0.97, 0.075), (R * 0.86, 0.115), (R * 0.66, 0.155), (R * 0.40, 0.182), (R * 0.15, 0.195)]
    bm = hat_bm(fr, beanie, 0.198, lambda th, k: 0)
    top = fr.p0 + fr.n * 0.198
    pom = blob_part('pompom', [F.ball(top + fr.n * 0.03 + Vector(o) * 0.018, 0.026) for o in
                               ((0, 0, 0), (1, 0, 0.3), (-1, 0, 0.3), (0, 1, 0.3), (0, -1, 0.3), (0, 0, 1))],
                    mat('M_Pompom'), voxel=0.005, faces=800)
    ob = finish_hat('Hat_Beanie', bm, fit, [mat('M_Beanie')], 0.008, 0.006, [pom])
    ob['order'] = order
    order += 1

    # Trucker cap: foam front panel, mesh back, curved bill and a top button.
    cl = 0.010

    def trucker(th, r):
        R = r + cl
        ff = 1 + 0.16 * max(0.0, math.cos(th)) ** 2
        return [(R, -0.004), (R + 0.004, 0.030 * ff), (R * 0.98, 0.080 * ff), (R * 0.86, 0.122 * ff),
                (R * 0.58, 0.152), (R * 0.22, 0.165)]
    bm = hat_bm(fr, trucker, 0.167, lambda th, k: 1 if math.cos(th) > 0.42 and k < 4 else 0)
    vis = []
    nt, nr = 24, 7
    for j in range(nr):
        t = j / (nr - 1)
        row = []
        for i in range(nt + 1):
            th = math.radians(-74 + 148 * i / nt)
            L = 0.115 * max(0.0, 1 - (math.degrees(th) / 80) ** 2) ** 0.5
            rad = fr.r_at(th) + cl - 0.004 + t * L
            h = -0.002 - t * L * 0.22 - 0.016 * (math.degrees(th) / 74) ** 2 * t
            row.append(bm.verts.new(fr.pt(th, rad, h)))
        vis.append(row)
    grid_faces(bm, vis, lambda j, i: 0)
    btn = blob_part('button', [F.ball(fr.p0 + fr.n * 0.170, 0.012, (1, 1, 0.5))], mat('M_Trucker'),
                    voxel=0.003, faces=200)
    ob = finish_hat('Hat_Trucker', bm, fit, [mat('M_Trucker'), mat('M_Foam')], 0.006, 0.004, [btn])
    ob['order'] = order
    order += 1

    # Sou'wester: rounded oilskin crown, brim short at the front and long, drooping at the back.
    cl = 0.014

    def souwester(th, r):
        R = r + cl
        Lb = 0.050 + 0.080 * ((1 - math.cos(th)) / 2) ** 1.2
        return [(R + Lb, -Lb * 0.50), (R + Lb * 0.55, -Lb * 0.22), (R + 0.004, -0.002), (R + 0.004, 0.030),
                (R * 0.95, 0.080), (R * 0.78, 0.125), (R * 0.46, 0.160), (R * 0.16, 0.172)]
    bm = hat_bm(fr, souwester, 0.174)
    ob = finish_hat('Hat_Souwester', bm, fit, [mat('M_Souwester')], 0.007, 0.004)
    ob['order'] = order
    order += 1

    # Straw hat: tall flat-topped crown, wide gently drooping brim, red ribbon band.
    cl = 0.010

    def straw(th, r):
        R = r + cl
        Lw = 0.15
        droop = 0.010 + 0.020 * (1 - math.cos(th)) / 2
        return [(R + Lw, -droop - 0.004 * math.sin(3 * th) ** 2), (R + Lw * 0.6, -droop * 0.45),
                (R + 0.022, -0.002), (R + 0.004, 0.0), (R + 0.004, 0.035), (R + 0.0, 0.10),
                (R * 0.95, 0.140), (R * 0.65, 0.152), (R * 0.28, 0.156)]
    bm = hat_bm(fr, straw, 0.157, lambda th, k: 1 if k == 4 else 0)
    ob = finish_hat('Hat_Straw', bm, fit, [mat('M_Straw'), mat('M_Ribbon')], 0.006, 0.004)
    ob['order'] = order


# ---------------------------------------------------------------- checks
def check_parts(fit):
    """Report clipping (part vertices inside the head) and floating (gap to the head) per part."""
    print('CHECK %-26s %7s %9s %9s' % ('part', 'verts', 'inside', 'min_gap'))
    for slot in SLOTS:
        for ob in bpy.data.collections['Parts_' + slot].objects:
            if ob.type != 'MESH':
                continue
            inside, gap = 0, 9.0
            for v in ob.data.vertices:
                p = ob.matrix_basis @ v.co
                loc, nrm, _, dist = fit.bvh.find_nearest(p)
                if (p - loc).dot(nrm) < 0:
                    inside += 1
                else:
                    gap = min(gap, dist)
            print('CHECK %-26s %7d %9d %9.4f' % (ob.name, len(ob.data.vertices), inside, gap))


# ---------------------------------------------------------------- build
def build_parts():
    bpy.ops.wm.open_mainfile(filepath=F.BLEND_PATH)
    for slot in SLOTS:
        bpy.context.scene.collection.children.link(bpy.data.collections.new('Parts_' + slot))
    fit = Fit()
    print('FIT nose_w=%.3f' % fit.nose_w)
    print('FIT hc=%s top=%.3f eye_z=%.3f eye_top=%.3f nose_z=%.3f nose_d=%.3f mouth_z=%.3f chin_z=%.3f' % (
        tuple(round(c, 3) for c in fit.hc), fit.top, fit.eye_z, fit.eye_top, fit.nose_z, fit.nose_d,
        fit.mouth_z, fit.chin_z))
    build_eyes(fit)
    build_pupils(fit)
    build_mouths(fit)
    build_brows(fit)
    build_noses(fit)
    build_facial_hair(fit)
    build_hair(fit)
    build_hats(fit)
    fit.obstacles = ()
    check_parts(fit)
    fit.bm.free()

    # Keep only the parts: drop the base character and anything it alone used.
    base = bpy.data.collections.get('Fisher')
    if base:
        for ob in list(base.objects):
            bpy.data.objects.remove(ob)
        bpy.data.collections.remove(base)
    for ob in list(bpy.context.scene.collection.objects):
        bpy.data.objects.remove(ob)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=PARTS_PATH)
    n = {s: len(bpy.data.collections['Parts_' + s].objects) for s in SLOTS}
    print('PARTS OK', n)


# ---------------------------------------------------------------- preview
SLOT_COLOURS = [(0.20, 0.42, 0.85), (0.92, 0.92, 0.90), (0.45, 0.85, 0.45), (0.50, 0.08, 0.08)]
HEAD_BASE = ['Head', 'Eye_L', 'Eye_R']
BODY_BASE = ['Shirt', 'Arms', 'Pants', 'Boots', 'Vest', 'VestPockets']


def load_parts():
    with bpy.data.libraries.load(PARTS_PATH, link=False) as (src, dst):
        dst.collections = ['Parts_' + s for s in SLOTS]
    groups = {}
    for c in dst.collections:
        slot = c.name[len('Parts_'):]
        groups[slot] = {}
        for ob in sorted(c.objects, key=lambda o: (o.get('order', 0), o.name)):
            groups[slot].setdefault(ob['part'], []).append(ob)
    return groups


def place(ob, off, holder, rot_z=0.0, scale=None, centre=None, key=None, data_copy=False, pivot=None):
    c = ob.copy()
    if data_copy or key:
        c.data = ob.data.copy()
    if key and c.data.shape_keys:
        for kb in c.data.shape_keys.key_blocks:
            kb.value = 1.0 if kb.name == key else 0.0
    m = ob.matrix_basis.copy()     # appended library objects have no evaluated matrix_world
    if centre is not None:
        m = Matrix.Translation(centre) @ Matrix.Diagonal((scale, scale, scale, 1))
    if rot_z:
        pv = Vector(pivot) if pivot is not None else m.translation
        m = Matrix.Translation(pv) @ Matrix.Rotation(rot_z, 4, 'Z') @ Matrix.Translation(-pv) @ m
    c.matrix_world = Matrix.Translation(off) @ m
    holder.objects.link(c)
    return c


def label(text, loc, holder, size=0.045):
    cu = bpy.data.curves.new('lbl', 'FONT')
    cu.body = text
    cu.size = size
    cu.align_x = 'CENTER'
    ob = bpy.data.objects.new('lbl', cu)
    ob.location = loc
    ob.rotation_euler = (math.radians(90), 0, 0)
    ob.data.materials.append(F.material('M_Label', (0.05, 0.05, 0.06), 0.9))
    holder.objects.link(ob)


def stage_setup():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 20
    sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    w = bpy.data.worlds.new('W')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.62, 0.72, 0.80, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.9
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(15)
    sun.rotation_euler = (math.radians(55), math.radians(8), math.radians(-30))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def render_sheet(sc, cam, holder, name, outdir, x0, x1, z0, z1, px_per_m=900):
    for ob in bpy.context.scene.objects:
        if ob.type in ('MESH', 'FONT', 'EMPTY'):
            ob.hide_render = holder not in ob.users_collection
    w, h = x1 - x0, z1 - z0
    cam.data.ortho_scale = max(w, h)
    cam.location = ((x0 + x1) / 2, -6, (z0 + z1) / 2)
    sc.render.resolution_x = int(w * px_per_m)
    sc.render.resolution_y = int(h * px_per_m)
    sc.render.filepath = os.path.join(outdir, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('RENDER', sc.render.filepath)


def preview(outdir, only=None):
    bpy.ops.wm.open_mainfile(filepath=F.BLEND_PATH)
    groups = load_parts()
    sc, cam = stage_setup()
    fit = Fit()
    head_base = [bpy.data.objects[n] for n in HEAD_BASE]
    D = {s: next((p for p, obs in g.items() if obs[0].get('default')), None) for s, g in groups.items()}

    def face(off, holder, sel, look=0.0, blink=None, talk=None):
        """One head at offset off with parts sel[slot] (None = slot empty)."""
        eyes = groups['Eyes'][sel['Eyes']]
        balls = {o.name[-1]: o for o in eyes if '_Ball_' in o.name}
        for o in head_base:
            if o.name.startswith('Eye_'):
                if o.name[-1] not in balls:
                    # The ball turns with its pupil, on its measured centre (the base eye's origin is not there).
                    place(o, off, holder, rot_z=look, pivot=fit.eyes[o.name[-1]].c)
                continue
            place(o, off, holder)
        for o in eyes:
            place(o, off, holder, key=blink, rot_z=look if '_Ball_' in o.name else 0.0)
        for o in groups['Pupils'][sel['Pupils']]:
            side = o.name[-1]
            if side in balls:
                c = Vector(balls[side]['eye_center'])
                place(o, off, holder, rot_z=look, scale=balls[side]['eye_scale'], centre=c, pivot=c)
            else:
                place(o, off, holder, rot_z=look, pivot=fit.eyes[side].c)
        for slot in ('Mouths', 'Brows', 'Noses', 'FacialHair', 'Hats', 'Hair'):
            p = sel.get(slot)
            if p:
                for o in groups[slot][p]:
                    if o.type == 'MESH':
                        place(o, off, holder, key=talk if slot == 'Mouths' else None)

    def grid(slot, rows, name, dx=0.52, hat=False):
        holder = bpy.data.collections.new('Sheet_' + name)
        sc.collection.children.link(holder)
        parts = list(groups[slot].keys())
        hz = fit.hc.z
        for r, (rlabel, kw) in enumerate(rows):
            for i, p in enumerate(parts):
                sel = dict(D, Hats=None, Hair=None, FacialHair=None)
                sel[slot] = p
                if slot == 'Eyes':
                    sel['Pupils'] = 'Pupil_' + groups['Eyes'][p][0].get('pupil', 'Dot')
                off = Vector((i * dx, 0, -r * 0.62))
                face(off, holder, sel, **kw)
                if r == len(rows) - 1:
                    label(p.split('_', 1)[1], off + Vector((0, -0.4, hz - 0.31)), holder)
            label(rlabel, Vector((-0.42, -0.4, hz - r * 0.62)), holder, size=0.04)
        top = hat if hat else 0.22
        render_sheet(sc, cam, holder, 'parts_' + name, outdir, -0.62, (len(parts) - 1) * dx + 0.30,
                     hz - (len(rows) - 1) * 0.62 - 0.36, hz + top, px_per_m=520)

    sheets = {
        'eyes': lambda: grid('Eyes', [('rest', {}), ('look', dict(look=math.radians(-28))),
                                      ('blink', dict(blink='Blink'))], 'eyes'),
        'pupils': lambda: grid('Pupils', [('rest', {}), ('look', dict(look=math.radians(30)))], 'pupils'),
        'mouths': lambda: grid('Mouths', [('rest', {}), ('open', dict(talk='Open'))], 'mouths'),
        'brows': lambda: grid('Brows', [('', {})], 'brows'),
        'noses': lambda: grid('Noses', [('', {})], 'noses'),
        'facialhair': lambda: grid('FacialHair', [('rest', {}), ('open', dict(talk='Open'))], 'facialhair'),
        'hats': lambda: grid('Hats', [('front', {})], 'hats', dx=0.72, hat=0.40),
        'hair': lambda: grid('Hair', [('', {})], 'hair', hat=0.32),
        'random': lambda: random_sheet(),
        'side': lambda: side_sheet(),
    }

    def random_sheet():
        holder = bpy.data.collections.new('Sheet_random')
        sc.collection.children.link(holder)
        rnd = random.Random(2026)
        body = [bpy.data.objects[n] for n in BODY_BASE]
        for i in range(8):
            off = Vector(((i % 4) * 0.95, 0, -(i // 4) * 2.15))
            sel = {s: rnd.choice(list(g.keys())) for s, g in groups.items()}
            if rnd.random() < 0.6:
                sel['Hair'] = None
            else:
                sel['Hats'] = None
            face(off, holder, sel, look=math.radians(rnd.uniform(-25, 25)))
            vm = F.material('M_VestSlot%d' % (i % 4), SLOT_COLOURS[i % 4])
            for o in body:
                c = place(o, off, holder, data_copy=o.name.startswith('Vest'))
                if o.name.startswith('Vest'):
                    c.data.materials.clear()
                    c.data.materials.append(vm)
            tag = ' '.join(sel[s].split('_', 1)[1] for s in ('Eyes', 'Mouths', 'Hats', 'Hair') if sel.get(s))
            label(tag, off + Vector((0, -0.5, -0.06)), holder, size=0.05)
        render_sheet(sc, cam, holder, 'parts_random', outdir, -0.5, 3 * 0.95 + 0.5, -2.15 - 0.15, 2.0,
                     px_per_m=420)

    def side_sheet():
        # Three-quarter and side views of the defaults and the bulkier parts, to catch floating/clipping.
        holder = bpy.data.collections.new('Sheet_side')
        sc.collection.children.link(holder)
        combos = [dict(D, Hair=None), dict(D, Hats='Hat_Beanie', Hair=None),
                  dict(D, Hats='Hat_Trucker', Hair=None), dict(D, Hats='Hat_Souwester', Hair=None),
                  dict(D, Hats='Hat_Straw', Hair=None), dict(D, Hats=None, Hair='Hair_Quiff', FacialHair='FacialHair_Beard'),
                  dict(D, Hats=None, Hair='Hair_Bowl', Noses='Nose_Pointy', FacialHair='FacialHair_Walrus')]
        piv = []
        for i, sel in enumerate(combos):
            for r, ang in enumerate((-50, -90)):
                sub = bpy.data.collections.new('tmp%d%d' % (i, r))
                sc.collection.children.link(sub)
                face(Vector((0, 0, 0)), sub, sel)
                e = bpy.data.objects.new('piv', None)
                holder.objects.link(e)
                e.location = (i * 0.72, 0, -r * 0.7)
                for o in sub.objects:
                    o.parent = e
                    o.matrix_parent_inverse = Matrix.Translation(Vector((0, 0, 0)))
                    holder.objects.link(o)
                    sub.objects.unlink(o)
                e.rotation_euler = (0, 0, math.radians(ang))
        hz = fit.hc.z
        render_sheet(sc, cam, holder, 'parts_side', outdir, -0.36, 6 * 0.72 + 0.36, hz - 0.7 - 0.35, hz + 0.3,
                     px_per_m=480)

    for k, fn in sheets.items():
        if only and k not in only:
            continue
        fn()
    print('PREVIEW', outdir)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    stage = argv[0] if argv else 'parts'
    if stage == 'parts':
        build_parts()
    elif stage == 'preview':
        outdir = argv[1] if len(argv) > 1 else tempfile.gettempdir()
        os.makedirs(outdir, exist_ok=True)
        preview(outdir, argv[2].split(',') if len(argv) > 2 else None)


if __name__ == '__main__':
    main()
