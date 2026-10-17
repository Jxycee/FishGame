# FishGame harbour villagers (design 5.3, tone 2.1), built from text so they are reproducible.
#
# Run (headless, Blender 5.2):
#   FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_villagers.py -- model [only_id ...]
#       builds every villager, bakes AO, exports Art/Export/Villagers/<Name>.fbx + villagers_manifest.json
#   FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_villagers.py -- preview <outdir>
#       lineup (front, 3/4) and face close-ups into outdir (Art/Previews/Villagers)
#
# Same family as the fisher (Art/Blender/fg_fisher.py, imported here: it is side-effect free): a true sphere head
# with flat painted face decals (big white eyes, tiny dot pupils, heavy brows, a big nose), chunky metaball body parts
# that are voxel remeshed and decimated, the same landmark ladder (head / shoulder / chest / belly / hip / knee / ankle)
# scaled per person. Adults, not mascots: tone 2.1 (grown-up goofy, Schedule 1, nothing cute). Static A-pose, not rigged;
# the topology is voxel-remeshed and decimated like the fisher's, which is what the fisher rig skins too.
#
# Authoring axes are the fisher's: Blender Z up, the character faces -Y, feet on z = 0, metres, pivot at the base centre.
# The FBX export (-Z forward, Y up, bake_space_transform) turns that into Unity's Y up, facing +Z (the harbour's mapping,
# Unity = (-bx, bz, -by)). Character left is Blender +x (Unity -x).
#
# Encoding (the harbour's, see fg_harbour.py header): UV0 "UVMap" in metres (box projection per face); UV1 "Layer" = (palette
# slot, texture layer) on every corner; vertex colour "Col" = 0.5 x baked AO; smooth normals; no materials exported.
# Villager specifics: the palette slot indexes the per-model colour list in villagers_manifest.json (not the harbour's S table),
# and the texture layer is a NEW character range (CHAR_LAYERS, 20+) that maps to the greyscale detail maps in
# Art/Textures/Fisher (colour comes from the palette, detail * 2 * tint as in the fisher). Nothing in the harbour range is renumbered.
#
# Export objects per villager: <Name>_Body (clothes, limbs, hands, feet), <Name>_Head (head sphere, ears, nose, hair, hat),
# <Name>_Face (eye whites, lids, brows, mouth, wrinkles; decals on the head), <Name>_Pupil_L / _Pupil_R (separate so a rig can
# slide them over the eye whites), and signature props as their own objects: <Name>_Prop_<Thing>. Props named *_Lost_* are the
# villager's missing wish items (hidden until the crew finds them); the rest are carried or worn.
import bpy, bmesh, math, os, sys, json, zlib, random
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fg_fisher as F   # borrowed: material, ball, capsule, mirror_x, lathe, decal, ellipse, stroke, ctx_select, sphere_point

ROOT = os.environ.get('FG_ROOT') or os.path.abspath(os.path.join(HERE, '..', '..'))
EXPORT_DIR = os.path.join(ROOT, 'Art', 'Export', 'Villagers')
MANIFEST = os.path.join(EXPORT_DIR, 'villagers_manifest.json')
SEED = 0xF15ED0C5

# New character texture layers (greyscale detail maps from the fisher, Art/Textures/Fisher/<name>_detail.png).
CHAR_LAYERS = dict(none=0, skin=20, shirt=21, vest=22, hat=23, cargo=24, sock=25, leather=26, webbing=27)
FISHER_REF = dict(fisher_head_r=0.262, fisher_head_z=1.37, fisher_shoulder=1.05, fisher_hip=0.56)


def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode('utf-8')))


def V(*a):
    return Vector(a)


# ================================================================ geometry helpers (own finish: budgets are TRIANGLES)
def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def finish(ob, mat, voxel=0.010, tris=None, smooth_iter=0):
    F.ctx_select(ob)
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
    n = tri_count(ob)
    if tris and n > tris:
        m = ob.modifiers.new('dec', 'DECIMATE')
        m.ratio = tris / n
        bpy.ops.object.modifier_apply(modifier=m.name)
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.clear()
    ob.data.materials.append(mat)


def meta(name, elems, mat, res=0.014, voxel=0.011, tris=None, smooth_iter=2):
    """Metaball elements -> mesh -> voxel remesh -> decimate (the fisher's blob(), budgets in triangles)."""
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
            e.size_x, e.size_y, e.size_z = size
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
    finish(ob, mat, voxel, tris, smooth_iter)
    return ob


def box_ob(name, c, size, mat, bevel=0.006, rot=(0, 0, 0)):
    """A softly bevelled box (clipboard, badge, tab...). c centre, size full extents."""
    bm = bmesh.new()
    mt = Matrix.Translation(c) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((size[0] / 2, size[1] / 2, size[2] / 2, 1))
    bmesh.ops.create_cube(bm, size=2.0, matrix=mt)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    F.ctx_select(ob)
    b = ob.modifiers.new('bev', 'BEVEL')
    b.width = bevel
    b.segments = 2
    bpy.ops.object.modifier_apply(modifier=b.name)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return ob


def prim_ob(name, kind, mat, loc, scale, rot=(0, 0, 0), seg=16, ring=8):
    """sphere / cylinder / cone / torus primitive, transformed into place."""
    bm = bmesh.new()
    mt = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
    if kind == 'sphere':
        bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring, radius=1.0, matrix=mt)
    elif kind == 'cyl':
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=1.0, radius2=1.0, depth=2.0, matrix=mt)
    elif kind == 'cone':
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=1.0, radius2=0.05, depth=2.0, matrix=mt)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return ob


def tube_ob(name, pts, r, mat, seg=6):
    """A thin round tube along a polyline (cords, chains, straps)."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(pts):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        t = (b - a).normalized()
        n = t.cross(Vector((0, 0, 1)))
        if n.length < 1e-4:
            n = t.cross(Vector((1, 0, 0)))
        n.normalize()
        m = t.cross(n)
        rings.append([bm.verts.new(p + (n * math.cos(2 * math.pi * k / seg) + m * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            j = (k + 1) % seg
            bm.faces.new((a[k], a[j], b[j], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return ob


def sphere_cap(name, c, radii, mat, zmin=0.0, keep=None, seg=24, ring=12):
    """Part of an ellipsoid shell (hair, caps): faces whose centre (local to c) has z >= zmin and keep(centre) is true."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
    dead = [f for f in bm.faces if f.calc_center_median().z < zmin or (keep and not keep(f.calc_center_median()))]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    F.ctx_select(ob)
    m = ob.modifiers.new('thick', 'SOLIDIFY')
    m.thickness = 0.012
    m.offset = 1.0
    bpy.ops.object.modifier_apply(modifier=m.name)
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return ob


def lathe(name, profile, mat, c=(0, 0, 0), scale=(1, 1, 1), segs=24):
    ob = F.lathe(name, profile, mat, segs=segs)
    ob.data.transform(Matrix.Translation(c) @ Matrix.Diagonal((*scale, 1)))
    return ob


def unit(v):
    v = Vector(v)
    return v.normalized() if v.length else v


# ================================================================ builder
class Build:
    """One villager under construction. Authority: none (offline art tool)."""

    def __init__(self, spec):
        self.s = spec
        self.id = spec['id']
        self.slots = []        # [{key, rgb, layer}] -> palette slot = index
        self.matcache = {}
        self.groups = {}       # group name -> [objects]
        self.props = []        # [{name, group, lost, note}]
        s = self.s
        self.hr = s['hr']
        self.k = self.hr / FISHER_REF['fisher_head_r']
        # landmark ladder (same ratios as the fisher)
        hz = s['height'] - self.hr - s.get('hunch', 0) * 0.05
        self.hy = -s.get('hunch', 0) * 0.16
        self.hz = hz
        self.sh = hz - 1.15 * self.hr
        self.hip = s['leg'] * s['height']
        self.ch = self.hip + (self.sh - self.hip) * 0.80
        self.be = self.hip + (self.sh - self.hip) * 0.40
        self.knee = self.hip * 0.58
        self.ankle = 0.11 * s['height'] / 1.6
        self.R = 0.215 * s.get('w', 1.0)
        F.HEAD_C = (0.0, self.hy, self.hz)
        F.HEAD_R = self.hr

    # ---- palette / materials
    def mat(self, key, rgb=None, layer=None):
        """Material for a colour key. The first call with rgb defines the palette entry (slot = order of definition)."""
        if key in self.matcache:
            return self.matcache[key]
        if rgb is None:
            rgb, layer = self.s['pal'][key][0], self.s['pal'][key][1]
        slot = len(self.slots)
        self.slots.append(dict(key=key, rgb=[round(c, 3) for c in rgb], layer=layer or 'none'))
        m = F.material('%s_%s' % (self.id, key), rgb, rough=0.8)
        m['fg_slot'] = slot
        m['fg_layer'] = CHAR_LAYERS[layer or 'none']
        self.matcache[key] = m
        return m

    def put(self, group, ob):
        self.groups.setdefault(group, []).append(ob)
        return ob

    def pal(self, key):
        return self.mat(key)

    # ---- tuned shapes
    def torso_profile(self):
        """(z, half-width scale, depth scale, y offset) ladder from hip to shoulder; width/depth multiply R."""
        s = self.s
        p = s.get('prof', (0.95, 1.0, 1.0, 0.92))
        bz = s.get('belly', 0.0)
        hun = s.get('hunch', 0.0)
        zs = [self.hip + 0.05, self.be, self.ch, self.sh - 0.03]
        out = []
        for i, z in enumerate(zs):
            d = [0.80, 0.88 + 0.55 * bz, 0.78, 0.70][i] * s.get('d', 1.0)
            y = [0.0, -0.035 - 0.08 * bz, 0.0, 0.0][i] - hun * 0.13 * ((z - self.hip) / max(0.1, self.sh - self.hip)) ** 1.5
            out.append((z, p[i], d, y))
        return out

    def torso_elems(self, grow=0.0, z_lo=None, z_hi=None, wscale=1.0, dscale=1.0, lower=None):
        prof = self.torso_profile()
        sp = (prof[-1][0] - prof[0][0]) / 3.0
        R = self.R + grow
        el = []
        for z, wx, dy, y in prof:
            if (z_lo is not None and z < z_lo) or (z_hi is not None and z > z_hi):
                continue
            el.append(F.ball((0, y, z), R, (wx * wscale, dy * dscale, max(0.55, sp * 1.15 / self.R))))
        if lower is not None:   # skirt of balls hanging below the hip (long coats, aprons)
            zc = self.hip - 0.02
            while zc > lower - 0.1:
                el.append(F.ball((0, prof[0][3], zc), R, (prof[0][1] * 0.97, prof[0][2] * 0.9, 0.78)))
                zc -= 0.12
        sw = self.s.get('sw', self.s.get('w', 1.0))
        zs = prof[-1][0] + 0.005
        ys = prof[-1][3]
        if z_hi is None or zs <= z_hi:
            el.append(F.ball((0.17 * sw, ys, zs), 0.085 + grow, (1.0, 1.0, 0.9)))
            el.append(F.ball((-0.17 * sw, ys, zs), 0.085 + grow, (1.0, 1.0, 0.9)))
        return el

    def front_y(self, z):
        prof = self.torso_profile()
        for (z0, w0, d0, y0), (z1, w1, d1, y1) in zip(prof, prof[1:]):
            if z0 <= z <= z1:
                t = (z - z0) / (z1 - z0)
                return (y0 + (y1 - y0) * t) - self.R * (d0 + (d1 - d0) * t) * 1.02
        z0, w0, d0, y0 = prof[0] if z < prof[0][0] else prof[-1]
        return y0 - self.R * d0

    def arm_geom(self, sign=1):
        s = self.s
        sw = s.get('sw', s.get('w', 1.0))
        ang = math.radians(s.get('arm', 26))
        S = V(sign * (0.17 * sw + 0.07), self.torso_profile()[-1][3], self.sh - 0.035)
        L = 0.50 * s.get('arm_len', 1.0) * self.s['height'] / 1.7
        d = V(sign * math.sin(ang), -0.04, -math.cos(ang)).normalized()
        return S, d, L

    def body_hit(self, x, z):
        """Front surface y of the body group at (x, z), by ray cast against what is built so far."""
        verts, polys = [], []
        for ob in self.groups.get('body', []):
            base = len(verts)
            verts += [tuple(v.co) for v in ob.data.vertices]
            polys += [tuple(base + i for i in p.vertices) for p in ob.data.polygons]
        bvh = BVHTree.FromPolygons(verts, polys)
        hit = bvh.ray_cast(V(x, -3, z), V(0, 1, 0))
        return hit[0].y if hit[0] is not None else self.front_y(z)

    # ---- body builders
    def build_torso(self, key, group='body', tris=1300, grow=0.0, name='Torso', extra=(), **kw):
        el = F.mirror_x(self.torso_elems(grow, **kw) + list(extra))
        return self.put(group, meta(self.id + '_' + name, el, self.pal(key), tris=tris, smooth_iter=3))

    def build_legs(self, trouser, shoe, sole, long=1.0, tris=900, skin='skin', tw=1.0, flare=0.0, boot_top=0.07, shoe_tris=550):
        s = self.s
        lw = s.get('lw', s.get('w', 1.0))
        x = 0.105 * lw
        hip, kn, an = self.hip, self.knee, self.ankle
        r1, r2 = 0.115 * s.get('tw', 1.0) * tw, 0.092 * s.get('tw', 1.0) * tw
        if long >= 0.95:
            el = [F.capsule((x, 0, hip + 0.03), (x, -0.005, kn), r1), F.capsule((x, -0.005, kn), (x + 0.0, 0.0, an + 0.06), r2)]
            if flare:
                el.append(F.ball((x, -0.01, an + 0.09), r2 + flare, (1, 1, 0.5)))
            self.put('body', meta(self.id + '_Legs', F.mirror_x(el), self.pal(trouser), tris=tris, smooth_iter=3))
        else:
            hem = hip - (hip - kn) * 2.0 * long * 0.9   # shorts hem, fraction of the thigh
            el = [F.capsule((x, 0, hip + 0.03), (x, -0.003, max(hem, kn)), r1)]
            self.put('body', meta(self.id + '_Shorts', F.mirror_x(el), self.pal(trouser), tris=tris * 0.55, smooth_iter=3))
            skin_el = [F.capsule((x, 0, max(hem, kn) + 0.02), (x, 0, an + 0.07), r2 * 0.97)]
            self.put('body', meta(self.id + '_LegSkin', F.mirror_x(skin_el), self.pal(skin), tris=tris * 0.45, smooth_iter=3))
        sk = s.get('shoe_len', 1.0)
        if shoe:
            ft = [F.ball((x, -0.045 * sk, 0.075), 0.098, (1.0, 1.55 * sk, 0.78)), F.capsule((x, 0, 0.08), (x, 0, an + boot_top), 0.095 * (0.5 + 0.5 * tw) if boot_top > 0.2 else 0.095)]
            self.put('body', meta(self.id + '_Shoes', F.mirror_x(ft), self.pal(shoe), tris=shoe_tris, smooth_iter=3, voxel=0.010))
        if sole:
            so = [F.ball((x, -0.045 * sk, 0.022), 0.10, (1.05, 1.6 * sk, 0.25))]
            self.put('body', meta(self.id + '_Soles', F.mirror_x(so), self.pal(sole), tris=220, smooth_iter=2, voxel=0.009))

    def build_arms(self, sleeve, sleeve_key, skin='skin', tris_arm=420, tris_sleeve=420, cuff=None, glove=None):
        """sleeve = fraction of the arm covered (0 none .. 1 to the wrist)."""
        s = self.s
        th = s.get('thick', 1.0)
        elA, elS, elH = [], [], []
        for sign in (1, -1):
            S, d, L = self.arm_geom(sign)
            wr = S + d * L
            ar = 0.060 * th
            if sleeve < 1.0:
                a0 = S + d * (L * max(sleeve, 0.0) - 0.03)
                elA.append(F.capsule(a0, wr, ar))
            if sleeve > 0:
                e = S + d * (L * min(sleeve, 1.0) + (0.01 if sleeve < 1 else 0.03))
                elS.append(F.capsule(S - d * 0.02, e, ar + 0.024 * (1.0 if sleeve < 1 else 0.7)))
            # hand: palm + fingers + thumb, fingers along the arm direction
            hc = wr + d * 0.05
            elH.append(F.ball(hc, 0.07 * th, (0.72, 0.95, 1.0), rot=(0, 0, 0)))
            for off in (-0.032, 0.0, 0.032):
                a = hc + d * 0.035 + V(0, -off, 0)
                elH.append(F.capsule(a, a + d * (0.085 - abs(off) * 0.4), 0.026 * th))
            tb = hc + V(0, -0.05, 0.0)
            elH.append(F.capsule(tb, tb + V(sign * 0.012, -0.025, -0.06), 0.025 * th))
        if elA:
            self.put('body', meta(self.id + '_Arms', elA, self.pal(skin), tris=tris_arm, smooth_iter=3, voxel=0.009))
        if elS:
            self.put('body', meta(self.id + '_Sleeves', elS, self.pal(sleeve_key), tris=tris_sleeve, smooth_iter=3, voxel=0.009))
        hk = glove or skin
        self.put('body', meta(self.id + '_Hands', elH, self.pal(hk), tris=640, smooth_iter=2, voxel=0.006, res=0.010))
        self.hands = {sign: (self.arm_geom(sign)[0] + self.arm_geom(sign)[1] * self.arm_geom(sign)[2]) for sign in (1, -1)}

    def hand_pos(self, sign):
        S, d, L = self.arm_geom(sign)
        return S + d * (L + 0.05), d

    def prop(self, name, objs, lost=False, note=''):
        objs = objs if isinstance(objs, list) else [objs]
        self.groups.setdefault('prop_' + name, []).extend(objs)
        self.props.append(dict(name=name, lost=lost, note=note))

    def prof_at(self, z):
        prof = self.torso_profile()
        z = min(max(z, prof[0][0]), prof[-1][0])
        for (z0, w0, d0, y0), (z1, w1, d1, y1) in zip(prof, prof[1:]):
            if z0 <= z <= z1:
                t = (z - z0) / (z1 - z0)
                return w0 + (w1 - w0) * t, d0 + (d1 - d0) * t, y0 + (y1 - y0) * t
        return prof[-1][1:]

    def band(self, key, z, h, grow=0.02, gap=0.0, group='body', name='Band', wscale=1.0):
        """A thin strip round the torso at height z (reflective bands, belts, cuffs); gap = open front half-width."""
        wx, dy, y = self.prof_at(z)
        a, bb = (self.R * wx + grow) * wscale, self.R * dy + grow
        seg = 28
        bm = bmesh.new()
        rings = []
        for zz in (z - h / 2, z + h / 2):
            rings.append([bm.verts.new((math.sin(2 * math.pi * i / seg) * a, y - math.cos(2 * math.pi * i / seg) * bb, zz)) for i in range(seg)])
        for i in range(seg):
            j = (i + 1) % seg
            mid = 2 * math.pi * (i + 0.5) / seg
            if gap and math.cos(mid) > 0 and abs(math.sin(mid) * a) < gap:
                continue
            bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(self.id + '_' + name)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(self.id + '_' + name, me)
        bpy.context.scene.collection.objects.link(ob)
        pf = me.polygons[0]
        if pf.normal.dot(pf.center - V(0, y, z)) < 0:
            F.ctx_select(ob)
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.flip_normals()
            bpy.ops.object.mode_set(mode='OBJECT')
        for p in me.polygons:
            p.use_smooth = True
        me.materials.append(self.pal(key))
        return self.put(group, ob)

    def hair(self, key, zfront=0.78, back=-0.3, grow=0.012, zmin=-0.05, name='Hair'):
        hr = self.hr
        ob = sphere_cap(self.id + '_' + name, F.HEAD_C, (hr + grow,) * 3, self.pal(key), zmin=zmin * hr,
                        keep=lambda c: c.y > back * hr or c.z > (zfront + 0.07 * math.sin(math.atan2(c.x, -c.y) * 4.0)) * hr)
        return self.put('head', ob)

    def spikes(self, key, n, length, base, zmin=0.1, tag='Spikes', jitter=0.5):
        r = rng(self.id + tag)
        hr = self.hr
        mat = self.pal(key)
        objs = []
        for i in range(n):
            while True:
                d = Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(zmin, 1))).normalized()
                if d.y < 0.35 and d.z > zmin:
                    break
            d = (d + Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(-1, 1))) * jitter * 0.3).normalized()
            ln = length * r.uniform(0.6, 1.2)
            c = Vector(F.HEAD_C) + d * (hr * 0.96 + ln * 0.35)
            objs.append(self._oriented('cone', mat, c, d, base, ln))
        ob = self._join(self.id + '_' + tag, objs)
        return self.put('head', ob)

    def _oriented(self, kind, mat, c, d, radius, length, seg=6, ry=None):
        bm = bmesh.new()
        q = d.to_track_quat('Z', 'Y')
        mt = Matrix.Translation(c) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((radius, ry or radius, length / 2, 1))
        if kind == 'cone':
            bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=1.0, radius2=0.08, depth=2.0, matrix=mt)
        else:
            bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=1.0, radius2=1.0, depth=2.0, matrix=mt)
        me = bpy.data.meshes.new('o')
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new('o', me)
        bpy.context.scene.collection.objects.link(ob)
        for p in me.polygons:
            p.use_smooth = True
        me.materials.append(mat)
        return ob

    def lanyard(self, cord_key='cord', badge_key='badge', badge_size=(0.075, 0.012, 0.105), card_key=None, name='Lanyard', off=0.036):
        objs = []
        zc = self.ch + 0.02
        for sx in (1, -1):
            pts = []
            for x, z in ((0.07, self.sh + 0.01), (0.062, self.sh - 0.07), (0.03, zc + 0.1), (0.0, zc)):
                pts.append(V(sx * x, self.front_y(z) - off, z))
            objs.append(tube_ob(self.id + '_Cord', pts, 0.0065, self.pal(cord_key), seg=5))
        y = self.front_y(zc - 0.07) - off
        objs.append(box_ob(self.id + '_Badge', V(0, y, zc - 0.075), badge_size, self.pal(badge_key), bevel=0.005))
        if card_key:
            objs.append(box_ob(self.id + '_BadgeFace', V(0, y - 0.0075, zc - 0.07), (0.05, 0.004, 0.04), self.pal(card_key), bevel=0.0015))
        self.prop(name, objs, note='worn round the neck')


    def plate(self, key, z0, z1, halfw, thick=0.03, name='Plate', tris=500, group='body', dy=0.0, x=0.0, wtaper=1.0):
        """A rounded plate that follows the torso front (aprons, bibs, panels): a vertical chain of flat ellipsoids."""
        n = max(1, int(round((z1 - z0) / 0.12)))
        seg = (z1 - z0) / n
        el = []
        for i in range(n):
            zc = z0 + seg * (i + 0.5)
            t = i / max(1, n - 1)
            hw = halfw * (1.0 + (wtaper - 1.0) * t)
            el.append(F.ball((x, self.front_y(zc) + dy - thick * 0.3, zc), hw, (1.0, thick / hw, max(0.5, seg * 0.62 / hw))))
        return self.put(group, meta(self.id + '_' + name, F.mirror_x(el) if x else el, self.pal(key), tris=tris, smooth_iter=2, voxel=0.008, res=0.012))

    def ring_on_head(self, u, v, r, lift, tube_r, key, name, seg=14, tube_seg=5):
        k = self.k
        c = F.sphere_point(u * k, v * k, lift * k)
        n = (c - Vector(F.HEAD_C)).normalized()
        a = (Vector((1, 0, 0)) - n * n.x).normalized()
        b = n.cross(a)
        pts = [c + (a * math.cos(2 * math.pi * i / seg) + b * math.sin(2 * math.pi * i / seg)) * r * k for i in range(seg + 1)]
        return tube_ob(self.id + '_' + name, pts, tube_r * k, self.pal(key), seg=tube_seg), c, n, a, b

    def head_ring(self, key, zrel, h, out=0.008, name='HeadBand', segs=24):
        """A band round the head at zrel (x head radius above the centre)."""
        hr = self.hr
        z = zrel * hr
        r = math.sqrt(max(0.0, 1 - zrel * zrel)) * hr + out
        ob = lathe(self.id + '_' + name, [(r, z - h / 2), (r + 0.008, z - h / 2), (r + 0.008, z + h / 2), (r, z + h / 2)], self.pal(key), c=F.HEAD_C, segs=segs)
        return ob

    # ---- shells (cardigans, vests, jackets): solidified torso with a cut front
    def shell(self, part, key, grow, slit=None, hem=None, neck=0.14, thick=0.012, z_lo=None, tris=1700, group='body', sleeves=False, extra=(), lower=None, boxes=()):
        el = F.mirror_x(self.torso_elems(grow, z_lo=z_lo, lower=lower) + list(extra))
        ob = meta(self.id + '_' + part, el, self.pal(key), voxel=0.012, res=0.016, tris=2600, smooth_iter=3)
        F.ctx_select(ob)
        m = ob.modifiers.new('thick', 'SOLIDIFY')
        m.thickness = thick
        m.offset = 1.0
        bpy.ops.object.modifier_apply(modifier=m.name)
        cutters = []
        if slit:   # (z0, z1, half width at z0, half width at z1)
            z0, z1, w0, w1 = slit
            bm = bmesh.new()
            outline = [(-w0, z0), (w0, z0), (w1, z1), (-w1, z1)]
            vs = [bm.verts.new((x, y, z)) for y in (-0.8, -0.02 + self.torso_profile()[1][3]) for x, z in outline]
            bm.faces.new(vs[:4])
            bm.faces.new(vs[4:][::-1])
            for i in range(4):
                j = (i + 1) % 4
                bm.faces.new((vs[i], vs[4 + i], vs[4 + j], vs[j]))
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            cutters.append(bm)
        if neck:
            b = bmesh.new()
            ny = self.torso_profile()[-1][3] + 0.01
            bmesh.ops.create_cone(b, cap_ends=True, segments=32, radius1=neck, radius2=neck, depth=0.26,
                                  matrix=Matrix.Translation((0, ny, self.sh + 0.09)) @ Matrix.Diagonal((1, 0.85, 1, 1)))
            cutters.append(b)
        if hem is not None:
            b = bmesh.new()
            bmesh.ops.create_cube(b, size=1.0, matrix=Matrix.Translation((0, 0, hem - 0.5)) @ Matrix.Diagonal((3, 3, 1, 1)))
            cutters.append(b)
        for lo, hi in boxes:
            b = bmesh.new()
            bmesh.ops.create_cube(b, size=1.0, matrix=Matrix.Translation(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2))
                                  @ Matrix.Diagonal((hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2], 1)))
            cutters.append(b)
        if sleeves is False:
            sw = self.s.get('sw', self.s.get('w', 1.0))
            for sx in (1, -1):
                b = bmesh.new()
                bmesh.ops.create_cone(b, cap_ends=True, segments=24, radius1=0.085, radius2=0.085, depth=0.3,
                                      matrix=Matrix.Translation((sx * (0.17 * sw + 0.16), self.torso_profile()[-1][3], self.sh - 0.05))
                                      @ Euler((0, math.radians(90), 0)).to_matrix().to_4x4() @ Matrix.Diagonal((1.1, 0.9, 1, 1)))
                cutters.append(b)
        for i, cb in enumerate(cutters):
            me = bpy.data.meshes.new('cut%d' % i)
            cb.to_mesh(me)
            cb.free()
            co = bpy.data.objects.new('cut%d' % i, me)
            bpy.context.scene.collection.objects.link(co)
            F.ctx_select(ob)
            bo = ob.modifiers.new('cut', 'BOOLEAN')
            bo.operation = 'DIFFERENCE'
            bo.solver = 'EXACT'
            bo.object = co
            bpy.ops.object.modifier_apply(modifier=bo.name)
            bpy.data.objects.remove(co)
        F.ctx_select(ob)
        n = tri_count(ob)
        if n > tris:
            d = ob.modifiers.new('dec', 'DECIMATE')
            d.ratio = tris / n
            bpy.ops.object.modifier_apply(modifier=d.name)
        keepmat = self.pal(key)
        for p in ob.data.polygons:
            p.use_smooth = True
            p.material_index = 0
        ob.data.materials.clear()
        ob.data.materials.append(keepmat)
        return self.put(group, ob)

    def panel(self, part, key, z0, z1, halfw=None, grow=0.022, thick=0.011, tris=500, front=True, group='body'):
        """A garment panel that hugs the torso between heights z0 and z1: only the front half (aprons, bibs) or all round
        (overall waists, wader bibs), narrowed to +-halfw. Built as a solidified torso shell cut by boxes."""
        yc = self.prof_at((z0 + z1) / 2)[2]
        big = 2.0
        boxes = [((-big, -big, z1), (big, big, z1 + big)), ((-big, -big, z0 - big), (big, big, z0))]
        if front:
            boxes.append(((-big, yc + 0.0, z0 - 0.1), (big, yc + big, z1 + 0.1)))
        if halfw:
            boxes += [((halfw, -big, z0 - 0.1), (halfw + big, big, z1 + 0.1)), ((-halfw - big, -big, z0 - 0.1), (-halfw, big, z1 + 0.1))]
        return self.shell(part, key, grow, neck=0, sleeves=True, thick=thick, tris=tris, group=group, lower=(z0 if z0 < self.hip else None), boxes=boxes)


    # ---- the head
    def build_head(self, skin='skin', ear_scale=1.0, seg=32, ring=16):
        hr, k = self.hr, self.k
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=hr, location=F.HEAD_C)
        head = bpy.context.active_object
        head.name = self.id + '_HeadSphere'
        bpy.ops.object.transform_apply(location=True)
        finish(head, self.pal(skin), voxel=0)
        self.put('head', head)
        ears = F.mirror_x([F.ball((hr * 0.97, 0.01 * k + self.hy * 0, self.hz - 0.02 * k), 0.045 * k * ear_scale, (0.5, 0.85, 1.0))])
        self.put('head', meta(self.id + '_Ears', ears, self.pal(skin), tris=170, voxel=0.006, res=0.01, smooth_iter=2))

    def nose(self, key='nose', r=0.05, sx=1.08, sy=0.9, sz=0.95, v=-0.098, droop=0.0, tris=360):
        k = self.k
        p = F.sphere_point(0, v * k)
        d = (p - Vector(F.HEAD_C)).normalized()
        r *= k
        el = [F.ball(p + d * r * 0.35, r, (sx, sy, sz)),
              F.ball(p + d * r * 0.1 + V(0, 0, 0.035 * k), r * 0.5, (0.8, 0.9, 1.3))]
        if droop:
            el.append(F.ball(p + d * r * 0.5 + V(0, 0, -droop * k), r * 0.55, (1.0, 0.9, 0.9)))
        self.put('head', meta(self.id + '_Nose', el, self.pal(key), voxel=0.0045, res=0.008, tris=tris, smooth_iter=2))

    # ---- face decals
    def face(self, **o):
        """Eyes, pupils, lids, brows, mouth, wrinkles. All lengths in fisher metres, scaled by the head size."""
        k = self.k
        eye_u, eye_v = o.get('eye_u', 0.078) * k, o.get('eye_v', -0.038) * k
        eye_w, eye_h = o.get('eye_w', 0.110) * k, o.get('eye_h', 0.118) * k
        prad = o.get('pupil_r', 0.0145) * k
        gx, gy = o.get('gaze', (0.0, 0.0))
        conv = o.get('converge', 0.004)
        skin_dark = o.get('wrinkle_key', 'wrinkle')
        n_eye = 16
        for side, sx in (('L', 1), ('R', -1)):
            u = sx * eye_u
            ow = self.put('face', self.decal(self.id + '_EyeWhite_' + side, F.ellipse(u, eye_v, eye_w, eye_h, n=n_eye), 'eye', 0.0015, 3))
            pu = u + gx * k - sx * conv * k
            pv = eye_v + (-0.006 + gy) * k
            pu = max(min(pu, u + eye_w * 0.34), u - eye_w * 0.34)
            pob = self.decal(self.id + '_Pupil_' + side, F.ellipse(pu, pv, prad * 2, prad * 2.1, n=12), 'pupil', 0.0030, 2)
            self.put('pupil_' + side, pob)
            if o.get('lid_top', 0) > 0:
                self.put('face', self.lid(u, eye_v, eye_w, eye_h, o['lid_top'], True, o.get('lid_key', 'skin')))
            if o.get('lid_bot', 0) > 0:
                self.put('face', self.lid(u, eye_v, eye_w, eye_h, o['lid_bot'], False, o.get('lid_key', 'skin')))
            if o.get('bags'):
                self.put('face', self.decal(self.id + '_Bag_' + side, F.ellipse(u, eye_v - eye_h * 0.56, eye_w * 0.95, 0.022 * k, n=14),
                                            skin_dark, 0.0011, 2))
            # brow
            b = o.get('brow', {})
            bw, bt = b.get('w', 0.100) * k, b.get('thick', 0.020) * k
            inner, outer, arch, bv = b.get('inner', 0.0), b.get('outer', -0.012), b.get('arch', 0.006), b.get('v', 0.036) * k
            if sx < 0 and 'raise_R' in b:
                bv += b['raise_R'] * k
            if sx > 0 and 'raise_L' in b:
                bv += b['raise_L'] * k
            pts = []
            for i in range(7):
                t = i / 6 * 2 - 1
                tt = (t + 1) / 2
                pts.append((u + sx * t * bw / 2, bv + arch * k * (1 - t * t) + (inner + (outer - inner) * tt) * k))
            self.put('face', self.decal(self.id + '_Brow_' + side, F.stroke(pts, bt), b.get('key', 'brow'), 0.0025, 2))
            # wrinkles (crow's feet)
            if o.get('wrinkles', 0):
                for i in range(o['wrinkles']):
                    a = math.radians(-20 + 22 * i)
                    p0 = (u + sx * (eye_w / 2 + 0.006 * k), eye_v)
                    p1 = (p0[0] + sx * math.cos(a) * 0.028 * k, p0[1] + math.sin(a) * 0.028 * k)
                    self.put('face', self.decal(self.id + '_Crow_%s%d' % (side, i), F.stroke([p0, p1], 0.0042 * k), skin_dark, 0.0012, 1))
        # mouth
        m = o.get('mouth', {})
        mv, mw = m.get('v', -0.166) * k, m.get('w', 0.085) * k
        style = m.get('style', 'smirk')
        mu = m.get('u', 0.0) * k
        if style in ('smirk', 'flat', 'frown', 'scowl', 'pursed'):
            curve, tilt = dict(smirk=(0.010, 0.010), flat=(0.0, 0.0), frown=(-0.020, 0.0), scowl=(-0.016, -0.004), pursed=(0.004, 0.0))[style]
            curve, tilt = m.get('curve', curve), m.get('tilt', tilt)
            if style == 'pursed':
                mw *= 0.5
            pts = [(mu + t * mw / 2, mv + curve * k * t * t + tilt * k * t) for t in [i / 10 * 2 - 1 for i in range(11)]]
            self.put('face', self.decal(self.id + '_Mouth', F.stroke(pts, m.get('thick', 0.011) * k), 'mouth', 0.0015, 2))
        elif style == 'grin':
            self.put('face', self.decal(self.id + '_Mouth', F.ellipse(mu, mv - 0.004 * k, mw * 1.35, 0.052 * k, n=18, squash_top=0.5), 'mouth', 0.0015, 2))
            self.put('face', self.decal(self.id + '_Teeth', [(mu - mw * 0.6 + i / 8 * mw * 1.2, mv + 0.020 * k - 0.004 * k * (1 - abs(i - 4) / 4)) for i in range(9)]
                                        + [(mu + mw * 0.58 - i / 8 * mw * 1.16, mv + 0.0 * k) for i in range(9)], 'teeth', 0.0021, 1))
        elif style == 'none':
            pass
        elif style == 'o':
            self.put('face', self.decal(self.id + '_Mouth', F.ellipse(mu, mv, 0.03 * k, 0.034 * k, n=12), 'mouth', 0.0015, 2))
        # forehead / cheek wrinkles
        for i in range(o.get('brow_lines', 0)):
            v = 0.078 * k + i * 0.016 * k
            pts = [(t * 0.07 * k, v + 0.006 * k * (1 - t * t)) for t in [j / 4 * 2 - 1 for j in range(5)]]
            self.put('face', self.decal(self.id + '_Line%d' % i, F.stroke(pts, 0.004 * k), skin_dark, 0.0012, 1))
        if o.get('cheek_lines'):
            for sx in (1, -1):
                pts = [(sx * (0.045 + 0.012 * t) * k, (-0.105 - 0.045 * t) * k) for t in [j / 4 for j in range(5)]]
                self.put('face', self.decal(self.id + '_Cheek%d' % sx, F.stroke(pts, 0.0045 * k), skin_dark, 0.0012, 1))
        if o.get('stubble'):
            self.put('face', self.decal(self.id + '_Stubble', F.ellipse(0, -0.165 * k, 0.30 * k, 0.165 * k, n=24, squash_top=0.35), 'stubble', 0.0008, 3))

    def decal(self, name, outline, key, lift, rings):
        ob = F.decal(name, outline, self.pal(key), lift=lift, rings=rings)
        return ob

    def lid(self, u, v, w, h, frac, top, key):
        """A skin-coloured lid over the top (or bottom) `frac` of an eye, with a dark lid line."""
        w2, h2 = w * 1.10, h * 1.10
        ycut = v + h / 2 - frac * h if top else v - h / 2 + frac * h
        pts = []
        for i in range(20):
            a = 2 * math.pi * i / 20
            y = v + math.sin(a) * h2 / 2
            if (top and y < ycut) or (not top and y > ycut):
                y = ycut
            pts.append((u + math.cos(a) * w2 / 2, y))
        lid = self.decal(self.id + '_Lid', pts, key, 0.0036, 2)
        line = self.decal(self.id + '_LidLine', F.stroke([(u - w / 2 * 1.02, ycut), (u, ycut), (u + w / 2 * 1.02, ycut)], 0.005 * self.k), 'brow', 0.0040, 1)
        return self._join(self.id + '_LidJoin', [lid, line])

    def _join(self, name, objs):
        if len(objs) == 1:
            return objs[0]
        for o in bpy.context.selected_objects:
            o.select_set(False)
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.join()
        ob = bpy.context.active_object
        ob.name = name
        return ob


# ================================================================ finishing: UVs, colours, AO, export
def box_uv(co, n):
    ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    if ay >= ax and ay >= az:
        return (co.x, co.z)
    if ax >= az:
        return (co.y, co.z)
    return (co.x, co.y)


def hemisphere_dirs(n):
    out = []
    ga = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        r = math.sqrt((i + 0.5) / n)
        a = i * ga
        out.append(Vector((r * math.cos(a), math.sqrt(1 - r * r), r * math.sin(a))))
    return out


def prepare(ob):
    """Join slots into UV0/UV1 from the object's materials, drop everything else, return the polygon material slot data."""
    me = ob.data
    for a in list(me.uv_layers):
        me.uv_layers.remove(a)
    uv0 = me.uv_layers.new(name='UVMap')
    uv1 = me.uv_layers.new(name='Layer')
    slotinfo = []
    for m in me.materials:
        if m is None:
            print('NONE MATERIAL on', ob.name)
            slotinfo.append((0, 0))
            continue
        slotinfo.append((m.get('fg_slot', 0), m.get('fg_layer', 0)))
    for p in me.polygons:
        slot, layer = slotinfo[p.material_index] if p.material_index < len(slotinfo) else (0, 0)
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv0.data[li].uv = box_uv(co, p.normal)
            uv1.data[li].uv = (float(slot), float(layer))
    for a in list(me.attributes):
        if a.name in ('sharp_face', 'sharp_edge'):
            continue
    return slotinfo


def bake_ao(objs, rays=28, dist=0.35):
    """Per-vertex AO over every object of one villager plus a ground plane (the harbour's recipe, character scale)."""
    verts, polys = [], []
    for ob in objs:
        base = len(verts)
        verts += [tuple(v.co) for v in ob.data.vertices]
        polys += [tuple(base + i for i in p.vertices) for p in ob.data.polygons]
    base = len(verts)
    verts += [(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)]
    polys.append((base, base + 3, base + 2, base + 1))
    bvh = BVHTree.FromPolygons(verts, polys)
    dirs = hemisphere_dirs(rays)
    out = []
    for ob in objs:
        me = ob.data
        nrm = [Vector((0, 0, 0)) for _ in me.vertices]
        for p in me.polygons:
            for i in p.vertices:
                nrm[i] += p.normal
        ao = []
        for v, n in zip(me.vertices, nrm):
            n = n.normalized() if n.length else Vector((0, 0, 1))
            t = n.orthogonal().normalized()
            b = n.cross(t)
            org = v.co + n * 0.004
            occ = 0.0
            for d0 in dirs:
                d = t * d0.x + n * d0.y + b * d0.z
                o, left = org, dist
                for _ in range(4):
                    loc, hn, idx, dd = bvh.ray_cast(o, d, left)
                    if loc is None:
                        break
                    if hn.dot(d) > 0.0:       # back face: pass through (buried parts)
                        o = loc + d * 0.002
                        left -= dd + 0.002
                        if left <= 0:
                            break
                        continue
                    occ += 1.0 - 0.5 * ((dist - left + dd) / dist)
                    break
            ao.append(max(0.0, 1.0 - occ / len(dirs)))
        out.append(ao)
    return out


def write_col(ob, ao, floor=0.5):
    me = ob.data
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    for li, lp in enumerate(me.loops):
        g = 0.5 * (floor + (1.0 - floor) * ao[lp.vertex_index])
        ca.data[li].color = (g, g, g, 1.0)


def fbx_equivalent(pa, pb):
    """True when two binary FBX files hold the same content apart from the header time stamp, the object UIDs (from the per-process
    random hash) and two things Blender does not produce in a stable order from one run to the next: the polygon/edge/colour index
    arrays, and float noise in the de-duplicated UV / colour / normal arrays (checked by their means and rounded value sets). Vertices must match to 1e-5."""
    import io_scene_fbx.parse_fbx as pf
    try:
        ra, _ = pf.parse(pa)
        rb, _ = pf.parse(pb)
    except Exception:
        return False
    skip = {b'FBXHeaderExtension', b'CreationTime', b'FileId'}
    index_arrays = {b'PolygonVertexIndex', b'Edges', b'ColorIndex', b'UVIndex', b'NormalsIndex', b'TangentsIndex'}
    loose = {b'UV', b'Colors', b'Normals'}   # layer arrays are de-duplicated by exact float equality, so their order and length wobble

    def eq(a, b):
        if a.id != b.id or a.props_type != b.props_type or len(a.props) != len(b.props) or len(a.elems) != len(b.elems):
            return False
        if a.id in skip or a.id in index_arrays:
            return True
        for t, x, y in zip(a.props_type, a.props, b.props):
            if t == ord('L'):
                continue
            if t in (ord('d'), ord('f')):
                if a.id in loose:
                    if a.id == b'UV':
                        if set(round(u, 1) for u in x) != set(round(u, 1) for u in y):
                            return False
                    elif abs(sum(x) / max(1, len(x)) - sum(y) / max(1, len(y))) > 2e-3:
                        return False
                elif len(x) != len(y) or any(abs(u - v) > 1e-5 for u, v in zip(x, y)):
                    return False
            elif t in (ord('i'), ord('l'), ord('b'), ord('c')):
                if list(x) != list(y):
                    return False
            elif x != y:
                return False
        return all(eq(c, d) for c, d in zip(a.elems, b.elems))
    return eq(ra, rb)


def export_if_changed(objs, path):
    tmp = path[:-4] + '.tmp.fbx'
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=tmp, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False)
    if os.path.exists(path) and fbx_equivalent(path, tmp):
        os.remove(tmp)
        return 'kept'
    os.replace(tmp, path)
    return 'written'


# ================================================================ the villagers

def ground_feet(b):
    """Feet on z = 0 exactly (the metaball soles overshoot by a couple of centimetres): shift every object of this villager."""
    body = [o for o in b.groups.get('body', []) if o.name.startswith(b.id + '_Soles') or o.name.startswith(b.id + '_Shoes')]
    low = min(v.co.z for o in body for v in o.data.vertices)
    for obs in b.groups.values():
        for o in obs:
            o.data.transform(Matrix.Translation((0, 0, -low)))
    b.hz -= low


def finalize(b):
    """Join each group into one object per export name, then UVs, colours, AO. Returns [(export name, object, group)]."""
    ground_feet(b)
    out = []
    order = ['body', 'head', 'face', 'pupil_L', 'pupil_R']
    names = dict(body='Body', head='Head', face='Face', pupil_L='Pupil_L', pupil_R='Pupil_R')
    for g in order + [g for g in b.groups if g not in order]:
        objs = b.groups.get(g)
        if not objs:
            continue
        for o in objs:
            if not o.data.materials or None in list(o.data.materials):
                print('NOMAT', o.name, list(o.data.materials))
            for p in o.data.polygons:
                p.use_smooth = True
        nm = names.get(g) or ('Prop_' + g[5:] if g.startswith('prop_') else g)
        ob = b._join(b.id + '_' + nm, objs) if len(objs) > 1 else objs[0]
        if len(objs) == 1:
            ob.name = b.id + '_' + nm
        out.append((g, ob))
    for g, ob in out:
        F.ctx_select(ob)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return out


def model(only=None):
    os.makedirs(EXPORT_DIR, exist_ok=True)
    manifest = dict(note='Villager FBX files (static A-pose, not rigged). See docs/superpowers/handoff/art/harbour-villagers.md.',
                    encoding=dict(uv0='UVMap, metres, box projection per face', uv1='Layer = (palette slot, texture layer); slot indexes models[].palette, layer = CHAR_LAYERS',
                                  col='Col = 0.5 x (0.5 + 0.5 x AO)', char_layers=CHAR_LAYERS,
                                  layer_textures={str(v): 'Art/Textures/Fisher/%s_detail.png' % k for k, v in CHAR_LAYERS.items() if v},
                                  axes='Unity metres, feet on y=0, facing +z, pivot at the base centre'),
                    models=[])
    for spec in SPECS:
        if only and spec['id'] not in only:
            continue
        F.reset()
        b = Build(spec)
        build_villager(b)
        parts = finalize(b)
        objs = [ob for _, ob in parts]
        aos = bake_ao(objs)
        for ob, ao in zip(objs, aos):
            prepare(ob)
            write_col(ob, ao)
            ob.data.materials.clear()
        for ob in objs:
            for p in ob.data.polygons:
                p.use_smooth = True
        path = os.path.join(EXPORT_DIR, spec['file'] + '.fbx')
        res = export_if_changed(objs, path)
        tris = {ob.name: tri_count(ob) for ob in objs}
        total = sum(tris.values())
        xs = [v.co for ob in objs for v in ob.data.vertices]
        ent = dict(name=spec['name'], id=spec['id'], file='Art/Export/Villagers/%s.fbx' % spec['file'], tris=total, tris_by_object=tris,
                   material='M_Toon_Base', height_m=round(max(v.z for v in xs), 3), palette=b.slots, role=spec['role'], notes=spec['notes'],
                   props=[dict(object='%s_Prop_%s' % (b.id, p['name']), lost=p['lost'], note=p['note']) for p in b.props],
                   building=spec.get('building'), placement=dict(note='stand at the building; Unity builder places, yaw faces the player/door'))
        manifest['models'].append(ent)
        print('VILLAGER %-18s tris=%5d (%s) %s' % (spec['name'], total, ', '.join('%s %d' % (k.split('_', 1)[-1], v) for k, v in tris.items()), res))
    old = {}
    if only and os.path.exists(MANIFEST):
        with open(MANIFEST) as fh:
            old = {m['id']: m for m in json.load(fh)['models']}
    if only:
        for m in manifest['models']:
            old[m['id']] = m
        manifest['models'] = [old[s['id']] for s in SPECS if s['id'] in old]
    with open(MANIFEST, 'w') as fh:
        json.dump(manifest, fh, indent=1)
    print('MODEL OK', len(manifest['models']))


# ================================================================ previews (Eevee, 1280x720)
def preview_scene():
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    try:
        sc.eevee.taa_render_samples = 24
    except Exception:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('W')
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.50, 0.62, 0.76, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.55
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 2.4
    sun.data.angle = math.radians(14)
    sun.rotation_euler = (math.radians(50), math.radians(8), math.radians(-30))
    sc.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, -0.001))
    fl = bpy.context.active_object
    fl.data.materials.append(F.material('M_PrevFloor', (0.58, 0.54, 0.46), 0.95))
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'
    cam.data.clip_end = 200
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def aim(cam, target, pitch, scale, dist=30):
    th = math.radians(90 - pitch)
    fwd = Vector((0, math.sin(th), -math.cos(th)))
    fwd = Vector((0, math.cos(math.radians(pitch)), -math.sin(math.radians(pitch))))
    cam.rotation_euler = (th, 0, 0)
    cam.location = Vector(target) - fwd * dist
    cam.data.ortho_scale = scale


def preview(outdir, only=None):
    os.makedirs(outdir, exist_ok=True)
    F.reset()
    sc, cam = preview_scene()
    figs = []
    cols = 6
    pitch = 18
    for i, spec in enumerate(SPECS):
        if only and spec['id'] not in only:
            continue
        b = Build(spec)
        build_villager(b)
        ground_feet(b)
        row, col = divmod(i, cols)
        n_in_row = cols if row == 0 else len(SPECS) - cols
        pivot = bpy.data.objects.new('P_' + spec['id'], None)
        sc.collection.objects.link(pivot)
        pivot.location = ((col - (n_in_row - 1) / 2) * 1.55, row * 2.4, 0)
        for obs in b.groups.values():
            for o in obs:
                o.parent = pivot
        figs.append((spec, b, pivot))
    rows = max(1, len(figs) and ((len(figs) - 1) // cols + 1))

    def shoot(name, yaw):
        for spec, b, pivot in figs:
            pivot.rotation_euler = (0, 0, math.radians(yaw))
        if len(figs) > 1:
            aim(cam, (0, (rows - 1) * 1.2, 0.95 + (rows - 1) * 0.35), pitch, 9.3)
        else:
            aim(cam, (figs[0][2].location.x, 0, 1.0), 0, 2.9)
        sc.render.filepath = os.path.join(outdir, name)
        bpy.ops.render.render(write_still=True)
    if os.environ.get('FG_QA'):   # large body shots of 4 villagers each, for checking garments (single row, not committed)
        for fi, (spec, b, pivot) in enumerate(figs):
            pivot.location = (fi * 1.6, 0, 0)
        for gi in range(0, len(figs), 4):
            for spec, b, pivot in figs:
                pivot.rotation_euler = (0, 0, math.radians(-35))
            xs = [figs[k][2].location.x for k in range(gi, min(gi + 4, len(figs)))]
            aim(cam, (sum(xs) / len(xs), 0, 0.95), 0, 6.6)
            sc.render.filepath = os.path.join(outdir, 'qa_%d.png' % (gi // 4))
            bpy.ops.render.render(write_still=True)
            for spec, b, pivot in figs:
                pivot.rotation_euler = (0, 0, math.radians(15))
            sc.render.filepath = os.path.join(outdir, 'qa_front_%d.png' % (gi // 4))
            bpy.ops.render.render(write_still=True)
        return
    shoot('villagers_front.png', 0)
    shoot('villagers_three_quarter.png', -35)
    if os.environ.get('FG_LINEUP_ONLY'):
        return
    for spec, b, pivot in figs:
        pivot.rotation_euler = (0, 0, math.radians(-15))
    sc.render.resolution_x = sc.render.resolution_y = 400
    for spec, b, pivot in figs:
        for s2, b2, p2 in figs:
            for o in p2.children:
                o.hide_render = (p2 is not pivot)
        aim(cam, (pivot.location.x, pivot.location.y, b.hz + 0.02), 0, 0.78 * b.hr / 0.25)
        sc.render.filepath = os.path.join(outdir, 'face_%s.png' % spec['id'])
        bpy.ops.render.render(write_still=True)
    print('PREVIEW', outdir)



# ================================================================ the villagers: specs + per-person builders
def tone(rgb, f):
    return tuple(min(1.0, max(0.0, c * f)) for c in rgb)


def pal_common(skin, hair, extra=None, nose=None):
    P = dict(
        skin=(skin, 'skin'),
        nose=(nose or (min(1.0, skin[0] * 1.04), skin[1] * 0.86, skin[2] * 0.9), 'skin'),
        wrinkle=(tone(skin, 0.74), 'none'),
        stubble=(tone(skin, 0.90), 'none'),
        eye=((1.0, 1.0, 0.97), 'none'),
        pupil=((0.03, 0.03, 0.04), 'none'),
        mouth=((0.25, 0.08, 0.08), 'none'),
        teeth=((0.96, 0.95, 0.90), 'none'),
        brow=(hair, 'none'),
        hair=(hair, 'none'),
    )
    P.update(extra or {})
    return P


def glasses_prop(b, loc, name, key, lens=None, rot_z=20):
    """Reading glasses lying on the ground (two lens rings, a bridge, two arms). Returns the objects."""
    objs = []
    for sx in (1, -1):
        pts = [V(sx * 0.038 + loc.x, 0, 0.006 + math.sin(2 * math.pi * i / 14) * 0.026 + 0.0) + V(math.cos(2 * math.pi * i / 14) * 0.026, loc.y, loc.z) for i in range(15)]
        pts = [V(loc.x + sx * 0.04 + math.cos(2 * math.pi * i / 14) * 0.026, loc.y + math.sin(2 * math.pi * i / 14) * 0.026, loc.z + 0.006) for i in range(15)]
        objs.append(tube_ob(b.id + '_GlassRing', pts, 0.0035, b.pal(key), seg=5))
        objs.append(tube_ob(b.id + '_GlassArm', [V(loc.x + sx * 0.066, loc.y + 0.004, loc.z + 0.006), V(loc.x + sx * 0.074, loc.y + 0.07, loc.z + 0.006), V(loc.x + sx * 0.07, loc.y + 0.1, loc.z + 0.002)],
                            0.0032, b.pal(key), seg=5))
    objs.append(tube_ob(b.id + '_GlassBridge', [V(loc.x - 0.014, loc.y + 0.008, loc.z + 0.006), V(loc.x, loc.y + 0.014, loc.z + 0.008), V(loc.x + 0.014, loc.y + 0.008, loc.z + 0.006)],
                        0.0033, b.pal(key), seg=5))
    for o in objs:
        o.data.transform(Matrix.Translation(loc) @ Matrix.Scale(1.35, 4) @ Matrix.Translation(-loc))
    return objs


# ---- Doug Pellow
def doug(b):
    s = b.s
    b.build_head(ear_scale=1.15)
    b.nose(**s['nose'])
    b.face(**s['face'])
    b.hair('hair', zfront=0.74, back=-0.2, grow=0.008)
    b.build_torso('shirt', tris=950)
    b.shell('Vest', 'hivis', 0.024, slit=(b.hip + 0.02, b.sh + 0.12, 0.012, 0.085), hem=b.hip + 0.06, neck=0.125, tris=1250)
    b.band('reflect', b.ch + 0.01, 0.045, grow=0.029, gap=0.03, name='BandA')
    b.band('reflect', b.be - 0.01, 0.045, grow=0.029, gap=0.03, name='BandB')
    b.build_arms(0.38, 'shirt')
    b.build_legs('navy', 'blackshoe', 'sole', long=1.0, tris=800)
    b.lanyard('lan_cord', 'badge', card_key='badge_face')
    hp, d = b.hand_pos(1)
    objs = [box_ob('Clip_Board', V(hp.x - 0.095, -0.15, hp.z + 0.05), (0.235, 0.014, 0.31), b.pal('board'), bevel=0.006),
            box_ob('Clip_Paper', V(hp.x - 0.095, -0.159, hp.z + 0.035), (0.20, 0.004, 0.25), b.pal('paper'), bevel=0.0015),
            box_ob('Clip_Clip', V(hp.x - 0.095, -0.16, hp.z + 0.205), (0.07, 0.01, 0.03), b.pal('metal'), bevel=0.004)]
    b.prop('Clipboard', objs, note='held in the left hand; a harbour log with a single entry')
    b.prop('ReadingGlasses', glasses_prop(b, V(0.55, -0.1, -0.02), 'g', 'frame'), lost=True,
           note='His lost reading glasses (wish item, fished up under the T-head). Shown lying beside him; hide until found, then show on his forehead/hand. Origin = ground under the lenses.')


# ---- Marge Tackleby
def marge(b):
    s = b.s
    b.build_head()
    b.nose(**s['nose'])
    b.face(**s['face'])
    b.hair('hair', zfront=0.66, back=-0.45, grow=0.009)
    b.put('head', b.head_ring('headband', 0.58, 0.055, out=0.016, name='Sweatband'))
    b.build_torso('tank', tris=1000)
    fy = b.front_y
    # bait-stained canvas apron: bib + skirt, a waist tie, neck strap, and stains in two colours
    b.panel('ApronBib', 'apron', b.ch - 0.05, b.sh - 0.02, halfw=0.14, grow=0.03, tris=300)
    b.panel('ApronSkirt', 'apron', b.hip - 0.1, b.ch - 0.02, halfw=0.215, grow=0.03, tris=420)
    b.band('apron_tie', b.hip + 0.075, 0.04, grow=0.034, name='ApronTie')
    for sx in (1, -1):
        b.put('body', meta(b.id + '_ApronStrap', [F.capsule((sx * 0.11, fy(b.sh - 0.03) - 0.03, b.sh - 0.03), (sx * 0.075, -0.01, b.sh + 0.045), 0.011)], b.pal('apron_tie'),
                           tris=70, voxel=0.005, res=0.008, smooth_iter=1))
    b.put('body', meta(b.id + '_StainsA', [F.ball((0.05, fy(b.be) - 0.042, b.be - 0.02), 0.04, (1.2, 0.3, 0.9)), F.ball((-0.08, fy(b.hip + 0.04) - 0.042, b.hip + 0.04), 0.05, (1.0, 0.3, 1.1)),
                                           F.ball((0.11, fy(b.hip + 0.01) - 0.042, b.hip), 0.03, (1, 0.3, 1))],
                       b.pal('stain_bait'), tris=150, voxel=0.006, res=0.01, smooth_iter=1))
    b.put('body', meta(b.id + '_StainsB', [F.ball((-0.03, fy(b.ch) - 0.045, b.ch - 0.02), 0.03, (1.2, 0.3, 1.0)), F.ball((0.09, fy(b.ch + 0.04) - 0.045, b.ch + 0.04), 0.022, (1, 0.3, 1))],
                       b.pal('stain_fish'), tris=90, voxel=0.006, res=0.01, smooth_iter=1))
    b.build_arms(0.0, 'tank', tris_arm=520)
    # wrist guards
    el = []
    for sign in (1, -1):
        S, d, L = b.arm_geom(sign)
        wr = S + d * L
        el.append(F.capsule(wr - d * 0.085, wr - d * 0.005, 0.060 * s['thick'] + 0.012))
    b.put('body', meta(b.id + '_WristGuards', el, b.pal('pad'), tris=200, voxel=0.007, res=0.011, smooth_iter=2))
    b.build_legs('derby_shorts', 'sneaker', 'sole_white', long=0.45, tris=800, skin='skin')
    # knee pads over the bare knees
    lx = 0.105 * s['lw']
    pads, caps = [], []
    for sx in (1, -1):
        pads.append(F.ball((sx * lx, -0.065, b.knee), 0.105, (1.0, 0.62, 1.0)))
        caps.append(F.ball((sx * lx, -0.118, b.knee), 0.062, (1.0, 0.4, 1.0)))
    b.put('body', meta(b.id + '_KneePads', pads, b.pal('pad'), tris=260, voxel=0.007, res=0.011, smooth_iter=2))
    b.put('body', meta(b.id + '_KneeCaps', caps, b.pal('padcap'), tris=160, voxel=0.007, res=0.011, smooth_iter=2))


# ---- Gary and Gary Nettle
def gary(b):
    s = b.s
    b.build_head(ear_scale=1.2)
    b.nose(**s['nose'])
    b.face(**s['face'])
    if s['variant'] == 'neat':
        # one neat comb-over: long strands combed across a bald crown (great-circle arcs lying on the head), side tufts over the ears
        hr = b.hr
        objs = []
        for i in range(8):
            phi = math.radians(-34 + i * 9.5)
            up = V(0, -math.sin(phi), math.cos(phi))
            pts = [Vector(F.HEAD_C) + (V(1, 0, 0) * math.sin(math.radians(t)) + up * math.cos(math.radians(t))) * (hr * 1.012) for t in range(-84, 85, 14)]
            objs.append(tube_ob(b.id + '_Strand', pts, 0.0085, b.pal('hair'), seg=4))
        for sx in (1, -1):
            objs.append(prim_ob(b.id + '_Tuft', 'sphere', b.pal('hair'), V(sx * hr * 0.96, 0.03, b.hz + 0.02), (0.03, 0.05, 0.065), seg=10, ring=6))
        b.put('head', b._join(b.id + '_ComboHair', objs))
    else:
        b.hair('hair', zfront=0.62, back=-0.5, grow=0.006)
        b.spikes('hair', 24, 0.13, 0.03, zmin=0.1, tag='WildHair')
    jk = 'jumper'
    b.build_torso(jk, tris=1000, extra=[])
    b.band('trim', b.hip + 0.035, 0.07, grow=0.016, name='Hem')
    b.band('trim', b.ch + 0.03, 0.05, grow=0.022, name='ChestBand')
    b.band('trim', b.ch - 0.04, 0.026, grow=0.020, name='ChestBandB')
    b.build_arms(1.0, jk, tris_sleeve=480)
    el = []
    for sign in (1, -1):
        S, d, L = b.arm_geom(sign)
        wr = S + d * L
        el.append(F.capsule(wr - d * 0.07, wr, 0.060 * s['thick'] + 0.03))
    b.put('body', meta(b.id + '_Cuffs', el, b.pal('trim'), tris=190, voxel=0.007, res=0.011, smooth_iter=2))
    b.build_legs('slacks', 'brown_shoe', 'sole', long=1.0, tris=800)
    b.band('trim', b.sh + 0.005, 0.05, grow=0.012, name='Collar')


# ---- Wendell Krill
def wendell(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head(ear_scale=1.3)
    b.nose(**s['nose'])
    b.face(**s['face'])
    # drooping moustache
    mu = []
    for sx in (1, -1):
        p0 = F.sphere_point(sx * 0.01 * k, -0.128 * k, 0.034 * k)
        p1 = F.sphere_point(sx * 0.07 * k, -0.15 * k, 0.03 * k) + V(0, 0, -0.035)
        p2 = F.sphere_point(sx * 0.098 * k, -0.19 * k, 0.012 * k) + V(0, 0, -0.04)
        mu += [F.capsule(p0, p1, 0.026 * k), F.capsule(p1, p2, 0.019 * k)]
    b.put('head', meta(b.id + '_Moustache', mu, b.pal('moustache'), tris=300, voxel=0.005, res=0.008, smooth_iter=2))
    # flat cap
    c = Vector(F.HEAD_C)
    prof = [(1.0 * hr, 0.42 * hr), (1.10 * hr, 0.62 * hr), (1.16 * hr, 0.80 * hr), (0.96 * hr, 0.93 * hr), (0.5 * hr, 0.98 * hr)]
    b.put('head', lathe(b.id + '_Cap', prof, b.pal('cap'), c=c, scale=(1.0, 1.08, 1.0), segs=20))
    b.put('head', box_ob(b.id + '_Peak', c + V(0, -1.02 * hr, 0.58 * hr), (0.78 * hr, 0.55 * hr, 0.026), b.pal('cap'), bevel=0.01, rot=(-14, 0, 0)))
    b.build_torso('shirt', tris=900)
    # long, smoke-darkened apron with soot smears and ties
    b.panel('Apron', 'apron', b.knee + 0.12, b.sh - 0.03, halfw=0.2, grow=0.03, tris=620)
    b.band('apron_tie', b.hip + 0.06, 0.04, grow=0.036, name='ApronTie')
    b.put('body', meta(b.id + '_Soot', [F.ball((0.06, b.front_y(b.be) - 0.046, b.be + 0.0), 0.05, (1.0, 0.3, 1.5)), F.ball((-0.07, b.front_y(b.hip) - 0.05, b.hip - 0.12), 0.06, (1.0, 0.3, 1.2))],
                       b.pal('soot'), tris=130, voxel=0.006, res=0.01, smooth_iter=1))
    for sx in (1, -1):
        b.put('body', meta(b.id + '_ApronStrap', [F.capsule((sx * 0.10, b.front_y(b.sh - 0.03) - 0.03, b.sh - 0.03), (sx * 0.065, -0.01 - 0.0, b.sh + 0.045), 0.011)], b.pal('apron_tie'),
                           tris=70, voxel=0.005, res=0.008, smooth_iter=1))
    b.build_arms(0.62, 'shirt', tris_arm=360)
    b.build_legs('baggy', 'boot', 'sole', long=1.0, tris=760, tw=1.05)
    # the missing lucky smoking hat: a tasselled fez-style smoking cap
    loc = V(-0.55, -0.1, -0.02)
    cap = lathe(b.id + '_LuckyHat', [(0.095, 0.0), (0.098, 0.012), (0.075, 0.1), (0.06, 0.125), (0.0, 0.13)], b.pal('lucky'), c=loc, segs=14)
    band = lathe(b.id + '_LuckyBand', [(0.0955, 0.0), (0.0985, 0.0), (0.0985, 0.025), (0.0955, 0.025)], b.pal('lucky_gold'), c=loc, segs=14)
    tas = tube_ob(b.id + '_Tassel', [loc + V(0, 0, 0.13), loc + V(0.03, 0, 0.14), loc + V(0.07, 0, 0.12), loc + V(0.075, 0, 0.085)], 0.008, b.pal('lucky_gold'), seg=5)
    b.prop('LuckyHat', [cap, band, tas], lost=True, note='His lucky smoking hat (wish item, gull nest on the sea arch). Shown on the ground beside him; hide until returned, then show it on his head instead of the flat cap.')


# ---- Ines Varga
def ines(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head()
    b.nose(**s['nose'])
    b.face(**s['face'])
    b.hair('hair', zfront=0.8, back=-0.2, grow=0.006)
    b.put('head', prim_ob(b.id + '_Bun', 'sphere', b.pal('hair'), V(0, b.hy + 0.9 * hr, b.hz + 0.55 * hr), (0.085, 0.085, 0.075), seg=14, ring=8))
    b.build_torso('tee', tris=1000)
    # overalls: denim bib + straps + patch pocket
    b.panel('Bib', 'denim', b.ch - 0.06, b.sh - 0.07, halfw=0.14, grow=0.026, tris=300)
    b.panel('Waist', 'denim', b.hip - 0.02, b.ch + 0.0, grow=0.016, tris=600, front=False)
    b.put('body', meta(b.id + '_Pocket', [F.ball((0, b.front_y(b.ch - 0.0) - 0.05, b.ch - 0.0), 0.05, (1.0, 0.25, 0.85))], b.pal('denim_dark'), tris=100, voxel=0.005, res=0.008, smooth_iter=1))
    for sx in (1, -1):
        b.put('body', meta(b.id + '_Strap', [F.capsule((sx * 0.1, b.front_y(b.sh - 0.08) - 0.03, b.sh - 0.08), (sx * 0.15, 0.0, b.sh + 0.02), 0.018)], b.pal('denim'), tris=90, voxel=0.005,
                           res=0.008, smooth_iter=1))
    b.build_arms(0.28, 'tee', tris_arm=620, tris_sleeve=300)
    b.build_legs('denim', 'work_boot', 'sole', long=1.0, tris=820, tw=1.0, shoe_tris=700)
    # welding goggles pushed up on the forehead
    objs = []
    for sx in (1, -1):
        c = F.sphere_point(sx * 0.052 * k, 0.108 * k, 0.012 * k)
        n = (c - Vector(F.HEAD_C)).normalized()
        objs.append(b._oriented('cyl', b.pal('rubber'), c, n, 0.037 * k, 0.034, seg=14))
        objs.append(b._oriented('cyl', b.pal('weldglass'), c + n * 0.014, n, 0.027 * k, 0.012, seg=14))
    objs.append(b.head_ring('rubber', 0.43, 0.034, out=0.008, name='GoggleStrap'))
    b.prop('WeldingGoggles', objs, note='worn on the forehead, strap round the head')


# ---- Dr. Penny Hollis
def penny(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head()
    b.nose(**s['nose'])
    b.face(**s['face'])
    # messy hair: a cap, a high bun of curls and a pencil stuck through it
    b.hair('hair', zfront=0.7, back=-0.4, grow=0.008)
    r = rng(b.id + 'curls')
    curls = []
    for i in range(14):
        a = r.uniform(0, 2 * math.pi)
        z = r.uniform(0.55, 1.1)
        rad = hr * (1.0 - 0.3 * (z - 0.55))
        curls.append(prim_ob(b.id + '_Curl', 'sphere', b.pal('hair'), V(math.cos(a) * rad * 0.8, b.hy + 0.1 * hr + math.sin(a) * rad * 0.8 + 0.06, b.hz + z * hr), (0.05, 0.05, 0.045), seg=8, ring=5))
    curls.append(prim_ob(b.id + '_Bun', 'sphere', b.pal('hair'), V(0, b.hy + 0.55 * hr, b.hz + 1.05 * hr), (0.11, 0.11, 0.1), seg=12, ring=7))
    curls.append(b._oriented('cyl', b.pal('pencil'), V(0.0, b.hy + 0.5 * hr, b.hz + 1.15 * hr), V(0.7, 0.2, 0.55).normalized(), 0.008, 0.3, seg=5))
    b.put('head', b._join(b.id + '_Curls', curls))
    # round spectacles
    objs = []
    for sx in (1, -1):
        ring, c, n, a, bb = b.ring_on_head(sx * 0.078, -0.038, 0.054, 0.014, 0.0045, 'frame', 'SpecRing')
        objs.append(ring)
        ear = V(sx * hr * 0.96, 0.0, b.hz)
        objs.append(tube_ob(b.id + '_SpecArm', [c + a * sx * 0.054 * k, c + a * sx * 0.054 * k + n * -0.03, ear], 0.0035, b.pal('frame'), seg=4))
    objs.append(tube_ob(b.id + '_SpecBridge', [F.sphere_point(0.026 * k, -0.03 * k, 0.014 * k), F.sphere_point(0, -0.022 * k, 0.018 * k), F.sphere_point(-0.026 * k, -0.03 * k, 0.014 * k)], 0.0035, b.pal('frame'), seg=4))
    b.prop('Spectacles', objs, note='round wire spectacles, worn')
    b.build_torso('blouse', tris=950)
    # waders: olive rubber trousers with a chest bib
    b.panel('WaderBib', 'waders', b.hip - 0.05, b.ch + 0.04, grow=0.026, tris=520, front=False)
    for sx in (1, -1):
        b.put('body', meta(b.id + '_WaderBrace', [F.capsule((sx * 0.11, b.front_y(b.ch + 0.03) - 0.03, b.ch + 0.03), (sx * 0.12, 0.0, b.sh + 0.02), 0.014)], b.pal('waders'), tris=90,
                           voxel=0.005, res=0.008, smooth_iter=1))
    # long mustard cardigan with elbow patches
    b.shell('Cardigan', 'cardigan', 0.03, slit=(b.hip - 0.05, b.sh + 0.12, 0.02, 0.09), hem=b.hip - 0.2, neck=0.12, sleeves=True, tris=1350, lower=b.hip - 0.2)
    b.build_arms(1.0, 'cardigan', tris_sleeve=560)
    el = []
    for sign in (1, -1):
        S, d, L = b.arm_geom(sign)
        el.append(F.ball(S + d * L * 0.5 + V(0, 0.04, 0), 0.04, (1.0, 0.4, 1.2)))
    b.put('body', meta(b.id + '_ElbowPatches', el, b.pal('patch'), tris=110, voxel=0.006, res=0.01, smooth_iter=1))
    b.build_legs('waders', 'wader_boot', 'sole', long=1.0, tris=760, tw=1.2, flare=0.02, shoe_tris=500)
    b.lanyard('lan_cord', 'badge', card_key='crest')


# ---- Lorraine Bass
def lorraine(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head()
    b.nose(**s['nose'])
    b.face(**s['face'])
    # the beehive: a tall lathe with scalloped swirls
    prof = []
    steps = 14
    for i in range(steps + 1):
        t = i / steps
        base = math.sin(t * math.pi * 0.52 + 0.35) if t > 0 else 0.6
        rr = hr * (0.92 + 0.42 * math.sin(min(1.0, t * 1.2) * math.pi * 0.55) - 0.95 * t * t) * (1 + 0.045 * math.sin(i * 2.2))
        prof.append((max(0.0, rr), hr * (0.35 + 1.75 * t)))
    prof[-1] = (0.02, prof[-1][1])
    c = V(0, b.hy + 0.05, b.hz)
    b.put('head', lathe(b.id + '_Beehive', prof, b.pal('hair'), c=c, scale=(1.0, 1.05, 1.0), segs=20))
    b.hair('hair', zfront=0.62, back=-0.5, grow=0.008)
    b.build_torso('blouse', tris=850)
    b.build_arms(0.52, 'blouse', tris_arm=330, tris_sleeve=300)
    # waist apron with a pocket and a stripe
    b.panel('Apron', 'apron', b.hip - 0.22, b.hip + 0.12, halfw=0.19, grow=0.026, tris=420)
    b.band('apron_stripe', b.hip + 0.12, 0.035, grow=0.034, name='ApronBand')
    b.build_legs('slacks', 'flat_shoe', 'sole', long=1.0, tris=700, tw=0.85)
    # coffee pot, hanging from the right hand: glass jug, coffee, black lid and handle
    hp, d = b.hand_pos(-1)
    c = hp + V(0.125, -0.03, -0.07)
    jug = prim_ob(b.id + '_Jug', 'cyl', b.pal('glass'), c, (0.085, 0.085, 0.105), seg=16)
    coffee = prim_ob(b.id + '_Coffee', 'cyl', b.pal('coffee'), c - V(0, 0, 0.025), (0.078, 0.078, 0.07), seg=16)
    lid = prim_ob(b.id + '_Lid', 'cyl', b.pal('blk'), c + V(0, 0, 0.11), (0.09, 0.09, 0.014), seg=16)
    han = tube_ob(b.id + '_Handle', [c + V(-0.085, 0, 0.09), c + V(-0.14, 0, 0.095), c + V(-0.15, 0, 0.0), c + V(-0.09, 0, -0.08)], 0.013, b.pal('blk'), seg=6)
    b.prop('CoffeePot', [jug, coffee, lid, han], note='held in the right hand by the handle')


# ---- Reverend Gil
def gil(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head()
    b.nose(**s['nose'])
    b.face(**s['face'])
    b.hair('hair', zfront=0.8, back=-0.3, grow=0.007)
    b.build_torso('clerical', tris=1050)
    fy = b.front_y
    b.band('collar', b.sh + 0.012, 0.045, grow=0.02, name='DogCollar')
    b.put('body', box_ob(b.id + '_CollarTab', V(0, fy(b.sh - 0.01) - 0.036, b.sh - 0.004), (0.045, 0.016, 0.06), b.pal('collar'), bevel=0.006))
    # purple stole hanging down the front from both shoulders
    for sx in (1, -1):
        pts = [V(sx * 0.075, b.body_hit(sx * 0.075, b.sh) - 0.015, b.sh + 0.005)] + [V(sx * (0.085 + 0.01 * i / 4), b.body_hit(sx * 0.085, z) - 0.016, z) for i, z in enumerate([b.ch, b.be, b.hip + 0.1, b.hip - 0.12])]
        b.put('body', tube_ob(b.id + '_Stole', pts, 0.02, b.pal('stole'), seg=6))
    b.put('body', box_ob(b.id + '_Pocket', V(-0.115, fy(b.ch) - 0.014, b.ch - 0.0), (0.085, 0.02, 0.075), b.pal('clerical2'), bevel=0.008))
    b.build_arms(1.0, 'clerical', tris_sleeve=520)
    b.build_legs('black_trousers', 'dress_shoe', 'sole', long=1.0, tris=760)
    pc = V(-0.115, fy(b.ch) - 0.018, b.ch + 0.02)
    dd = V(-0.12, -0.12, 1.0).normalized()
    body = b._oriented('cyl', b.pal('dauber'), pc + dd * 0.03, dd, 0.0155, 0.11, seg=10)
    cap = b._oriented('cyl', b.pal('dauber_dark'), pc + dd * 0.095, dd, 0.012, 0.03, seg=10)
    tip = prim_ob(b.id + '_DauberTip', 'sphere', b.pal('dauber_ink'), pc - dd * 0.03, (0.021, 0.021, 0.02), seg=10, ring=6)
    b.prop('BingoDauber', [body, cap, tip], note='tucked in the breast pocket, tip up')
    # sunglasses
    objs = []
    for sx in (1, -1):
        c = F.sphere_point(sx * 0.078 * k, -0.038 * k, 0.012 * k)
        n = (c - Vector(F.HEAD_C)).normalized()
        objs.append(b._oriented('cyl', b.pal('shade'), c, n, 0.058 * k, 0.014, seg=14, ry=0.052 * k))
        objs.append(tube_ob(b.id + '_ShadeArm', [c + V(sx * 0.057 * k, 0.0, 0.0), V(sx * hr * 0.97, -0.05, b.hz + 0.0), V(sx * hr * 0.97, 0.0, b.hz)], 0.0045, b.pal('shade'), seg=4))
    objs.append(tube_ob(b.id + '_ShadeBridge', [F.sphere_point(0.02 * k, -0.03 * k, 0.016 * k), F.sphere_point(0, -0.022 * k, 0.02 * k), F.sphere_point(-0.02 * k, -0.03 * k, 0.016 * k)], 0.0045, b.pal('shade'), seg=4))
    b.prop('Sunglasses', objs, note='worn indoors; the eyes underneath are modelled so they can come off')


# ---- Mr. Fenwick
def fenwick(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head()
    b.nose(**s['nose'])
    b.face(**s['face'])
    # the beard: cheeks, chin and a long fall onto the chest
    el = []
    for sx in (1, -1):
        el.append(F.ball(F.sphere_point(sx * 0.12 * k, -0.115 * k, 0.03 * k), 0.058 * k, (1.0, 1.0, 1.25)))
        el.append(F.ball(F.sphere_point(sx * 0.07 * k, -0.17 * k, 0.045 * k), 0.07 * k, (1.0, 1.0, 1.0)))
    ctr = Vector(F.HEAD_C)
    el.append(F.ball(ctr + V(0, -0.82 * hr, -0.85 * hr), 0.1 * k, (1.15, 0.9, 1.2)))
    el.append(F.ball(ctr + V(0, -0.78 * hr, -1.22 * hr), 0.085 * k, (1.0, 0.85, 1.6)))
    el.append(F.ball(ctr + V(0, -0.62 * hr, -1.65 * hr), 0.06 * k, (0.9, 0.8, 1.8)))
    b.put('head', meta(b.id + '_Beard', el, b.pal('beard'), tris=700, voxel=0.007, res=0.011, smooth_iter=3))
    # sou'wester: tall crown, wide brim, longer at the back
    prof = [(1.02 * hr, 0.35 * hr), (1.1 * hr, 0.55 * hr), (1.06 * hr, 0.9 * hr), (0.9 * hr, 1.05 * hr), (0.5 * hr, 1.12 * hr)]
    b.put('head', lathe(b.id + '_Souwester', prof, b.pal('oil_yellow'), c=Vector(F.HEAD_C), scale=(1.0, 1.04, 1.0), segs=20))
    brim = lathe(b.id + '_Brim', [(1.0 * hr, 0.32 * hr), (1.55 * hr, 0.25 * hr), (1.78 * hr, 0.18 * hr), (1.78 * hr, 0.15 * hr), (1.5 * hr, 0.22 * hr), (1.0 * hr, 0.3 * hr)],
                 b.pal('oil_yellow'), c=Vector(F.HEAD_C) + V(0, 0.18 * hr, 0), scale=(1.0, 1.12, 1.0), segs=20)
    b.put('head', brim)
    foil = lathe(b.id + '_Foil', [(1.62 * hr, 0.205 * hr), (1.82 * hr, 0.16 * hr), (1.82 * hr, 0.13 * hr), (1.62 * hr, 0.17 * hr)],
                 b.pal('foil'), c=Vector(F.HEAD_C) + V(0, 0.18 * hr, 0), scale=(1.0, 1.12, 1.0), segs=13)
    b.prop('TinfoilBrim', [foil], note='a tinfoil lining wrapped round the sou\'wester brim; it is part of his hat, left as an object so it can be swapped for a plain brim')
    # layers: sack jumper, long green oilskin, short yellow oilskin
    hipx = [F.ball((0, 0, b.hip - 0.08), b.R, (1.15, 0.9, 1.3)), F.ball((0, 0, b.hip - 0.2), b.R, (1.2, 0.95, 1.1))]
    b.build_torso('sack', tris=700)
    b.shell('LongCoat', 'oil_green', 0.05, slit=(b.knee + 0.12, b.sh + 0.1, 0.02, 0.07), hem=b.knee + 0.1, neck=0.13, sleeves=True, tris=1350, lower=b.knee + 0.1)
    b.shell('Jacket', 'oil_yellow', 0.085, slit=(b.hip + 0.0, b.sh + 0.1, 0.02, 0.09), hem=b.hip + 0.0, neck=0.14, sleeves=True, tris=1000)
    b.build_arms(1.0, 'oil_yellow', tris_sleeve=560)
    b.put('body', box_ob(b.id + '_Toggle', V(0.0, b.body_hit(0, b.be) - 0.014, b.be), (0.07, 0.02, 0.025), b.pal('rope'), bevel=0.008))
    b.build_legs('oil_green', 'welly', 'sole', long=1.0, tris=620, boot_top=0.2, shoe_tris=520, tw=1.1)


# ---- Ambrose Lock
def ambrose(b):
    s = b.s
    hr, k = b.hr, b.k
    b.build_head(ear_scale=1.7)
    b.nose(**s['nose'])
    b.face(**s['face'])
    # wispy white tufts over the ears
    r = rng(b.id + 'tufts')
    objs = []
    for sx in (1, -1):
        for i in range(5):
            objs.append(b._oriented('cone', b.pal('white_hair'), V(sx * hr * 0.95, b.hy + 0.012 * i, b.hz + 0.05 * k + 0.012 * i),
                                    V(sx * r.uniform(0.7, 1.2), r.uniform(-0.2, 0.5), r.uniform(0.0, 0.7)).normalized(), 0.016, 0.12 + 0.02 * i, seg=5))
    b.put('head', b._join(b.id + '_Tufts', objs))
    b.build_torso('waistcoat', tris=700)
    b.shell('Cardigan', 'cardigan', 0.032, slit=(b.hip - 0.12, b.sh + 0.12, 0.03, 0.1), hem=b.hip - 0.14, neck=0.12, sleeves=True, tris=1250, lower=b.hip - 0.14)
    fy = b.front_y
    for i, z in enumerate((b.be - 0.08, b.be + 0.04, b.ch - 0.02)):
        b.put('body', b._oriented('cyl', b.pal('button'), V(0.028, b.body_hit(0.028, z) - 0.004, z), V(0, -1, 0), 0.015, 0.014, seg=10))
    b.build_arms(1.0, 'cardigan', tris_sleeve=480)
    b.build_legs('highwaist', 'slipper', None, long=1.0, tris=700, shoe_tris=450, tw=1.1)
    # pocket watch on a chain
    wp = V(-0.085, b.body_hit(-0.085, b.be + 0.02) - 0.02, b.be + 0.02)
    chain = tube_ob(b.id + '_Chain', [V(0.03, b.body_hit(0.03, b.ch - 0.04) - 0.02, b.ch - 0.04), V(0.0, b.body_hit(0.0, b.be + 0.12) - 0.026, b.be + 0.1),
                                       V(-0.05, b.body_hit(-0.05, b.be + 0.06) - 0.024, b.be + 0.06), wp + V(0, 0, 0.03)], 0.0042, b.pal('gold'), seg=4)
    watch = b._oriented('cyl', b.pal('gold'), wp, V(0, -1, 0), 0.034, 0.014, seg=16)
    face_ = b._oriented('cyl', b.pal('watch_face'), wp + V(0, -0.007, 0), V(0, -1, 0), 0.027, 0.006, seg=16)
    ring = prim_ob(b.id + '_Bow', 'sphere', b.pal('gold'), wp + V(0, 0, 0.036), (0.01, 0.007, 0.01), seg=8, ring=5)
    b.prop('PocketWatch', [chain, watch, face_, ring], note='hangs on its chain from the waistcoat; keeps the wrong time')


BUILDERS = dict(doug=doug, marge=marge, gary=gary, wendell=wendell, ines=ines, penny=penny, lorraine=lorraine, gil=gil, fenwick=fenwick, ambrose=ambrose)


def build_villager(b):
    BUILDERS[b.s['builder']](b)


# Display colours are the same sRGB-ish values the fisher uses (fg_fisher.COL); Unity sets the real tint from the palette.
SK_PALE, SK_TAN, SK_BROWN, SK_RUDDY, SK_OLIVE, SK_DEEP = (0.94, 0.76, 0.62), (0.86, 0.62, 0.46), (0.62, 0.42, 0.30), (0.93, 0.66, 0.54), (0.80, 0.62, 0.45), (0.45, 0.29, 0.20)

SPECS = [
    dict(id='Doug_Pellow', name='Doug Pellow', file='Doug_Pellow', builder='doug', role='Harbourmaster', building='dock office (fixed)',
         notes='Tall and stiff, hi-vis vest with reflective bands, lanyard, clipboard. Squints: his reading glasses are the separate lost prop Prop_ReadingGlasses.',
         height=1.98, hr=0.23, leg=0.50, w=0.8, sw=0.8, d=0.72, belly=-0.1, arm=10, arm_len=1.1, thick=0.82, tw=0.82, lw=0.85, prof=(0.9, 0.88, 0.95, 0.9),
         nose=dict(r=0.044, sy=1.0, sz=1.18, v=-0.10),
         face=dict(eye_w=0.100, eye_h=0.074, lid_top=0.26, lid_bot=0.28, gaze=(0.004, 0), pupil_r=0.0125, brow=dict(inner=-0.016, outer=0.002, arch=0.0, thick=0.024, w=0.108, v=0.032),
                   mouth=dict(style='flat', w=0.068, curve=-0.002), brow_lines=2, wrinkles=2),
         pal=pal_common(SK_PALE, (0.55, 0.55, 0.57), dict(
             shirt=((0.90, 0.92, 0.95), 'shirt'), hivis=((0.98, 0.62, 0.05), 'vest'), reflect=((0.80, 0.82, 0.78), 'none'), navy=((0.12, 0.17, 0.30), 'cargo'),
             blackshoe=((0.10, 0.10, 0.11), 'leather'), sole=((0.06, 0.06, 0.06), 'leather'), lan_cord=((0.10, 0.20, 0.55), 'webbing'), badge=((0.92, 0.92, 0.90), 'none'),
             badge_face=((0.25, 0.45, 0.80), 'none'), board=((0.55, 0.38, 0.20), 'none'), paper=((0.97, 0.97, 0.94), 'none'), metal=((0.75, 0.75, 0.72), 'none'),
             frame=((0.10, 0.10, 0.12), 'none')))),
    dict(id='Marge_Tackleby', name='Marge Tackleby', file='Marge_Tackleby', builder='marge', role='Bait shop owner, retired roller-derby champion', building='bait shop (fixed)',
         notes='Stocky, huge arms, bait-stained apron with stains in two colours, knee pads over bare knees, wrist guards, sweatband.',
         height=1.62, hr=0.25, leg=0.43, w=1.38, sw=1.4, d=1.12, belly=0.3, arm=34, arm_len=0.95, thick=1.38, tw=1.1, lw=1.25, prof=(1.0, 1.05, 1.0, 0.96),
         nose=dict(r=0.05, sx=1.15, sz=0.9),
         face=dict(eye_w=0.108, eye_h=0.112, gaze=(0.012, 0.0), brow=dict(inner=-0.004, outer=-0.01, thick=0.026, raise_L=0.012), mouth=dict(style='grin', w=0.09), bags=False),
         pal=pal_common(SK_RUDDY, (0.70, 0.70, 0.72), dict(
             tank=((0.78, 0.10, 0.12), 'shirt'), apron=((0.72, 0.64, 0.44), 'webbing'), apron_tie=((0.45, 0.33, 0.20), 'webbing'), stain_bait=((0.38, 0.26, 0.14), 'none'),
             stain_fish=((0.62, 0.72, 0.72), 'none'), pad=((0.13, 0.13, 0.15), 'leather'), padcap=((0.95, 0.35, 0.60), 'leather'), derby_shorts=((0.10, 0.12, 0.35), 'cargo'),
             sneaker=((0.92, 0.92, 0.90), 'leather'), sole_white=((0.85, 0.85, 0.82), 'leather'), headband=((0.95, 0.35, 0.60), 'sock')))),
    dict(id='Gary_Nettle_Neat', name='Gary Nettle (neat comb-over)', file='Gary_Nettle_Neat', builder='gary', variant='neat', role='Net-mender (twin)', building='net shed (fixed); cottage (placeable)',
         notes='Twin A: neat comb-over, red jumper with blue trim. Same body as Gary_Nettle_Wild; the jumper colours are swapped (palette slots are the same keys: jumper/trim).',
         height=1.72, hr=0.245, leg=0.5, w=1.05, sw=1.0, d=1.0, belly=0.35, arm=28, thick=1.0, tw=1.0,
         nose=dict(r=0.054, sx=1.1), face=dict(eye_w=0.108, gaze=(0.012, 0.0), brow=dict(inner=-0.012, outer=-0.004, thick=0.024, raise_R=0.012), mouth=dict(style='smirk'), bags=True),
         pal=pal_common(SK_RUDDY, (0.36, 0.30, 0.27), dict(
             jumper=((0.78, 0.12, 0.12), 'sock'), trim=((0.18, 0.32, 0.72), 'sock'), slacks=((0.40, 0.33, 0.24), 'cargo'), brown_shoe=((0.34, 0.20, 0.11), 'leather'),
             sole=((0.12, 0.09, 0.07), 'leather')))),
    dict(id='Gary_Nettle_Wild', name='Gary Nettle (wild hair)', file='Gary_Nettle_Wild', builder='gary', variant='wild', role='Net-mender (twin)', building='net shed (fixed); cottage (placeable)',
         notes='Twin B: wild ginger hair, blue jumper with red trim. Same body as Gary_Nettle_Neat; the jumper colours are swapped.',
         height=1.72, hr=0.245, leg=0.5, w=1.05, sw=1.0, d=1.0, belly=0.35, arm=28, thick=1.0, tw=1.0,
         nose=dict(r=0.054, sx=1.1), face=dict(eye_w=0.108, gaze=(-0.012, 0.0), brow=dict(inner=-0.012, outer=-0.004, thick=0.024, raise_L=0.012), mouth=dict(style='smirk', tilt=-0.010), bags=True),
         pal=pal_common(SK_RUDDY, (0.78, 0.38, 0.12), dict(
             jumper=((0.18, 0.32, 0.72), 'sock'), trim=((0.78, 0.12, 0.12), 'sock'), slacks=((0.40, 0.33, 0.24), 'cargo'), brown_shoe=((0.34, 0.20, 0.11), 'leather'),
             sole=((0.12, 0.09, 0.07), 'leather')))),
    dict(id='Wendell_Krill', name='Wendell Krill', file='Wendell_Krill', builder='wendell', role='Smokehouse', building='smokehouse (fixed)',
         notes='Old and stooped, flat cap, droopy moustache, long smoke-darkened apron with soot. The lucky smoking hat is the separate lost prop Prop_LuckyHat.',
         height=1.60, hr=0.25, leg=0.46, w=0.85, sw=0.8, d=0.85, belly=0.05, hunch=0.8, arm=20, thick=0.85, tw=0.9,
         nose=dict(r=0.06, droop=0.02, sz=1.1),
         face=dict(eye_w=0.100, eye_h=0.106, lid_top=0.3, gaze=(0.006, -0.004), wrinkles=3, brow_lines=3, cheek_lines=True, bags=True,
                   brow=dict(inner=0.004, outer=-0.016, thick=0.026, key='moustache'), mouth=dict(style='frown', w=0.06)),
         pal=pal_common(SK_TAN, (0.62, 0.62, 0.60), dict(
             moustache=((0.75, 0.75, 0.72), 'none'), cap=((0.34, 0.34, 0.30), 'hat'), shirt=((0.45, 0.50, 0.42), 'shirt'), apron=((0.33, 0.24, 0.17), 'webbing'),
             apron_tie=((0.20, 0.16, 0.12), 'webbing'), soot=((0.10, 0.08, 0.07), 'none'), baggy=((0.30, 0.30, 0.28), 'cargo'), boot=((0.20, 0.14, 0.10), 'leather'),
             sole=((0.08, 0.07, 0.06), 'leather'), lucky=((0.55, 0.10, 0.12), 'hat'), lucky_gold=((0.90, 0.72, 0.20), 'none')))),
    dict(id='Ines_Varga', name='Ines Varga', file='Ines_Varga', builder='ines', role='Boatwright', building='boatyard (fixed)',
         notes='Broad, thick arms, denim overalls with a patch pocket, welding goggles pushed up on the forehead (separate prop), a permanent scowl.',
         height=1.72, hr=0.245, leg=0.5, w=1.32, sw=1.5, d=1.12, belly=0.1, arm=32, thick=1.35, tw=1.12, lw=1.18, prof=(1.0, 1.02, 1.04, 1.0),
         nose=dict(r=0.054, sx=1.12),
         face=dict(eye_w=0.105, eye_h=0.092, lid_top=0.2, gaze=(0.0, 0.0), brow=dict(inner=-0.024, outer=0.012, arch=0.0, thick=0.028), mouth=dict(style='scowl', w=0.085)),
         pal=pal_common(SK_OLIVE, (0.10, 0.08, 0.08), dict(
             tee=((0.82, 0.82, 0.78), 'shirt'), denim=((0.20, 0.34, 0.58), 'cargo'), denim_dark=((0.13, 0.22, 0.40), 'cargo'), work_boot=((0.42, 0.30, 0.16), 'leather'),
             sole=((0.08, 0.07, 0.06), 'leather'), rubber=((0.12, 0.12, 0.12), 'none'), weldglass=((0.08, 0.22, 0.12), 'none')))),
    dict(id='Penny_Hollis', name='Dr. Penny Hollis', file='Penny_Hollis', builder='penny', role='Marine biologist "between universities"', building='aquarium (placeable)',
         notes='Mustard cardigan with elbow patches, olive waders, a lanyard for a university she no longer works at, a messy bun with a pencil, round spectacles (separate prop).',
         height=1.68, hr=0.25, leg=0.5, w=0.95, sw=0.95, d=0.95, belly=0.15, arm=28, thick=0.9, tw=0.95,
         nose=dict(r=0.046),
         face=dict(eye_w=0.116, eye_h=0.124, gaze=(0.0, 0.012), pupil_r=0.0135, bags=True, brow=dict(inner=0.014, outer=-0.006, arch=0.01, thick=0.02), mouth=dict(style='smirk', curve=0.004, tilt=0.004, w=0.07)),
         pal=pal_common(SK_PALE, (0.55, 0.26, 0.12), dict(
             blouse=((0.92, 0.88, 0.80), 'shirt'), cardigan=((0.86, 0.62, 0.12), 'sock'), patch=((0.40, 0.25, 0.12), 'leather'), waders=((0.34, 0.42, 0.20), 'leather'),
             wader_boot=((0.26, 0.32, 0.15), 'leather'), sole=((0.08, 0.07, 0.06), 'leather'), frame=((0.20, 0.14, 0.10), 'none'), pencil=((0.95, 0.80, 0.15), 'none'),
             lan_cord=((0.45, 0.07, 0.10), 'webbing'), badge=((0.92, 0.92, 0.88), 'none'), crest=((0.45, 0.07, 0.10), 'none')))),
    dict(id='Lorraine_Bass', name='Lorraine Bass', file='Lorraine_Bass', builder='lorraine', role='Cafe owner', building='cafe (placeable)',
         notes='Thin and dry, tall beehive, half-lidded deadpan, cafe apron, a coffee pot in the right hand (separate prop).',
         height=1.78, hr=0.235, leg=0.54, w=0.72, sw=0.76, d=0.62, belly=-0.1, arm=22, arm_len=1.05, thick=0.72, tw=0.76, lw=0.8, prof=(0.9, 0.85, 0.9, 0.95),
         nose=dict(r=0.042, sz=1.1),
         face=dict(eye_w=0.108, eye_h=0.110, lid_top=0.48, gaze=(0.013, 0.0), brow=dict(inner=-0.002, outer=-0.006, thick=0.017, raise_R=0.016, arch=0.01), mouth=dict(style='flat', w=0.075, tilt=-0.004), bags=True),
         pal=pal_common(SK_PALE, (0.60, 0.34, 0.18), dict(
             blouse=((0.12, 0.52, 0.55), 'shirt'), apron=((0.93, 0.90, 0.80), 'webbing'), apron_stripe=((0.78, 0.18, 0.18), 'webbing'), slacks=((0.20, 0.20, 0.24), 'cargo'),
             flat_shoe=((0.12, 0.12, 0.14), 'leather'), sole=((0.08, 0.07, 0.06), 'leather'), glass=((0.70, 0.82, 0.84), 'none'), coffee=((0.16, 0.08, 0.04), 'none'), blk=((0.07, 0.07, 0.08), 'none')))),
    dict(id='Reverend_Gil', name='Reverend Gil', file='Reverend_Gil', builder='gil', role='Chapel', building='chapel (fixed)',
         notes='Round and beaming, clerical black with a dog collar and a purple stole, sunglasses (separate prop), a bingo dauber in the breast pocket (separate prop).',
         height=1.76, hr=0.25, leg=0.5, w=1.12, sw=1.05, d=1.1, belly=0.45, arm=28, thick=1.0, tw=1.0,
         nose=dict(r=0.05),
         face=dict(eye_w=0.108, eye_h=0.118, gaze=(0, 0.004), brow=dict(inner=0.002, outer=-0.004, arch=0.012, thick=0.019), mouth=dict(style='grin', w=0.1)),
         pal=pal_common(SK_BROWN, (0.78, 0.78, 0.80), dict(
             clerical=((0.10, 0.10, 0.12), 'shirt'), clerical2=((0.15, 0.15, 0.18), 'shirt'), collar=((0.96, 0.96, 0.94), 'none'), stole=((0.50, 0.18, 0.62), 'sock'),
             black_trousers=((0.09, 0.09, 0.10), 'cargo'), dress_shoe=((0.07, 0.07, 0.08), 'leather'), sole=((0.05, 0.05, 0.05), 'leather'), dauber=((0.95, 0.20, 0.55), 'none'),
             dauber_dark=((0.15, 0.15, 0.18), 'none'), dauber_ink=((0.60, 0.05, 0.35), 'none'), shade=((0.04, 0.04, 0.05), 'none')))),
    dict(id='Mr_Fenwick', name='Mr. Fenwick', file='Mr_Fenwick', builder='fenwick', role='Gull Bar hermit', building='stilt shack on Gull Bar',
         notes='Bearded, hunched, three layers of oilskin, a sou\'wester with a tinfoil-lined brim (the foil is a separate prop), paranoid up-and-away stare.',
         height=1.70, hr=0.26, leg=0.46, w=1.25, sw=1.3, d=1.3, belly=0.3, hunch=0.55, arm=30, thick=1.15, tw=1.1, lw=1.1,
         nose=dict(r=0.056, sx=1.15),
         face=dict(eye_w=0.124, eye_h=0.136, gaze=(0.018, 0.019), pupil_r=0.0095, converge=0.0, bags=True, brow=dict(inner=0.016, outer=0.004, arch=0.01, thick=0.024, key='beard'), mouth=dict(style='none')),
         pal=pal_common(SK_RUDDY, (0.74, 0.74, 0.72), dict(
             beard=((0.74, 0.74, 0.72), 'none'), oil_yellow=((0.95, 0.76, 0.10), 'vest'), oil_green=((0.22, 0.34, 0.18), 'vest'), sack=((0.50, 0.40, 0.26), 'webbing'),
             foil=((0.82, 0.84, 0.86), 'none'), rope=((0.60, 0.48, 0.30), 'webbing'), welly=((0.07, 0.07, 0.08), 'leather'), sole=((0.05, 0.05, 0.05), 'leather')),
             nose=(0.90, 0.50, 0.42))),
    dict(id='Ambrose_Lock', name='Ambrose Lock', file='Ambrose_Lock', builder='ambrose', role='Lock-keeper', building='river lock (fixed)',
         notes='Very old and frail, cardigan over a waistcoat, high-waisted trousers, slippers, a pocket watch on a chain (separate prop), big ears, white tufts, eleven years of waiting in the eyelids.',
         height=1.50, hr=0.27, leg=0.46, w=0.72, sw=0.72, d=0.72, belly=0.1, hunch=0.7, arm=18, thick=0.72, tw=0.85, lw=0.88,
         nose=dict(r=0.056, droop=0.02, sz=1.1),
         face=dict(eye_w=0.104, eye_h=0.112, lid_top=0.36, gaze=(0.0, -0.006), wrinkles=3, brow_lines=3, cheek_lines=True, bags=True,
                   brow=dict(inner=-0.002, outer=-0.014, arch=0.012, thick=0.03, key='white_hair'), mouth=dict(style='frown', w=0.055, curve=-0.012)),
         pal=pal_common(SK_PALE, (0.92, 0.92, 0.92), dict(
             waistcoat=((0.42, 0.28, 0.26), 'vest'), cardigan=((0.40, 0.52, 0.42), 'sock'), button=((0.30, 0.20, 0.12), 'none'), highwaist=((0.40, 0.40, 0.42), 'cargo'),
             slipper=((0.36, 0.17, 0.14), 'sock'), gold=((0.92, 0.75, 0.25), 'none'), watch_face=((0.95, 0.94, 0.88), 'none'), white_hair=((0.92, 0.92, 0.92), 'none')))),
]



def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    stage = argv[0] if argv else 'model'
    if stage == 'model':
        model(set(argv[1:]) or None)
    elif stage == 'preview':
        preview(argv[1] if len(argv) > 1 else os.path.join(ROOT, 'Art', 'Previews', 'Villagers'), set(argv[2:]) or None)


if __name__ == '__main__':
    main()
