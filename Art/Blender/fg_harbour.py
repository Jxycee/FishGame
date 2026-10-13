# fg_harbour.py (T17-B phase 1, 2026-10-14): the Harbour Pier visual meshes for FishGame, modelled procedurally in Blender.
# Run it in an EMPTY Blender file (bpy.ops.wm.read_homefile(use_empty=True)); main() rebuilds every harbour object, bakes AO into the
# vertex colours, exports one FBX per group to Art/Export/Harbour and writes harbour_manifest.json. Deterministic: every random draw comes
# from random.Random(seed) on a named stream (SEED ^ crc32(stream)); mathutils.noise is position-only.
#
# Coordinates. Everything is authored in UNITY axes (x east, y up, z out to sea; the coast runs along x at z -45, land is z < -45) and
# mapped to Blender once, when the mesh is made: Blender = (-x, -z, y) (to_blender). The FBX export (-Z forward, Y up, bake_space_transform,
# the fish settings) writes FBX (bx, bz, -by) and Unity negates x on import, so the vertex lands back on the Unity (x, y, z) it was authored
# at. The map is a reflection, so every face's winding is reversed when it is mapped. Each FBX object sits at the origin with an identity
# transform: the phase-2 builder places each one at the origin.
#
# Encoding (T17-B ruling, the same as the fish): UV0 "UVMap" (wood in metres, grain along u = the long axis, posts grain up; triplanar
# objects get world xz); UV1 "Layer" = (palette slot, texture layer) on every corner; vertex colour "Col" = 0.5 x ao (half-scale grey,
# _JitterEncoded 1) from a per-vertex ray-cast AO bake (1 m, terrain as occluder; see bake_ao) remapped above a per-part floor; smooth or
# analytic normals only (rounded boxes carry the exact rounded-box normal as custom normals); no materials exported. Objects whose
# name ends _Tri go on M_Toon_Tri (triplanar), the rest on M_Toon_Base. The FBX carries no uv1.z (mapping): see the manifest note.
import bpy, bmesh, math, json, os, zlib, random
from mathutils import Vector, Matrix, noise

ROOT = r'C:\Dev\FishGame-harbour-art'
EXPORT_DIR = os.path.join(ROOT, 'Art', 'Export', 'Harbour')
SCRIPT_PATH = os.path.join(ROOT, 'Art', 'Blender', 'fg_harbour.py')
MANIFEST_PATH = os.path.join(EXPORT_DIR, 'harbour_manifest.json')

# ---------------------------------------------------------------------------------------------------------------------------------------
# Layout: mirrors HarbourPierLayout / plan T17.2 (docs/superpowers/plans/2026-10-13-m3-plan-maps.md). Hand copy; the phase-2 EditMode
# bounds test keeps the two in step. Unity metres.
# ---------------------------------------------------------------------------------------------------------------------------------------
SEED = 0xF15ED0C5
COAST_Z = -45.0

def beach_y(z):  # BeachY(z) = clamp(0.12 x (CoastZ - z), -0.06, 1.2)
    return min(1.2, max(-0.06, 0.12 * (COAST_Z - z)))

def seabed_y(z):  # SeabedY(z) = -min(9, 0.25 x max(0, z - CoastZ))
    return -min(9.0, 0.25 * max(0.0, z - COAST_Z))

def terrain_y(z):  # TerrainY = z <= CoastZ ? BeachY : SeabedY
    return beach_y(z) if z <= COAST_Z else seabed_y(z)

DECK_TOP, DECK_THICK = 1.5, 0.12
DECK_UNDER = DECK_TOP - DECK_THICK
DECK_X0, DECK_X1, DECK_Z0, DECK_Z1 = -1.5, 1.5, -57.0, -8.0
THEAD_X0, THEAD_X1, THEAD_Z0, THEAD_Z1 = -5.0, 5.0, -8.0, -5.0
RAMP_LOW, RAMP_HIGH, RAMP_W, RAMP_THICK = (0.0, 1.17, -60.5), (0.0, 1.45, -57.0), 3.0, 0.1
POST_R, POST_X, POST_Z0, POST_Z1, POST_STEP = 0.09, 1.4, -56.0, -8.0, 3.0
THEAD_POSTS = [(4.6, -7.6), (-4.6, -7.6), (4.6, -5.4), (-4.6, -5.4)]
# Ladders: LadderVolume boxes (min, max) and the collider-only rail boxes the visual rails stay inside.
LADDER_MID_BOX = ((1.55, -1.4, -25.5), (2.55, 2.5, -24.5))
LADDER_MID_RAILBOX = ((1.55, -1.4, -25.25), (1.61, 2.5, -24.75))     # rail box Ladder_Mid at x 1.58, 0.06 x height x 0.5
LADDER_HEAD_BOX = ((1.5, -1.4, -4.95), (2.5, 2.5, -3.95))
LADDER_HEAD_RAILBOX = ((1.75, -1.4, -4.95), (2.25, 2.5, -4.89))      # rail box Ladder_Head at z -4.92, 0.5 wide in x
LADDER_BOTTOM, LADDER_RAIL_TOP = -1.4, 2.35                          # rails rise 0.85 m above the deck as grab rails (inside the rail boxes)
BREAKWATER = (28.0, 32.0, -56.0, -25.0)                              # x0, x1, z0, z1; collider y -6..1.5, top 1.5 walkable
BREAKWATER_TOP = 1.5
ROCK_COUNT, ROCK_X, ROCK_Z, ROCK_SIZE = 12, (-58.0, 58.0), (-54.0, -47.0), (0.6, 1.6)
STAND_POINTS = [(-48, 0.23, -46.5, 0), (-30, 0.23, -46.5, 0), (-12, 0.23, -46.5, 0), (14, 0.23, -46.5, 0), (48, 0.23, -46.5, 0),
                (0, 1.55, -25, 90), (0, 1.55, -25, 270), (-4.5, 1.55, -6.5, 270), (4.5, 1.55, -6.5, 90), (0, 1.55, -5.5, 0),
                (30, 1.55, -26, 0)]
SPAWNS = [(0.0, 1.55, -55.0 + 1.5 * i) for i in range(4)]
BELL = (1.1, 1.5, -47.5)
HUT_C, HUT_SIZE = (-9.0, 1.2, -63.0), (4.0, 3.0, 3.0)                # ground = BeachY(-63) = 1.2
BOLLARDS = [(4.6, -7.6), (-4.6, -7.6), (4.6, -5.4), (-3.9, -5.4)]   # T-head corners; the west-front one moved in to clear the lamp
LAMPS = [(-1.35, -46.0), (-4.8, -5.3)]
CRATES = [(-1.05, -49.75, 0.55, 4.0, 0.0), (-1.1, -49.15, 0.5, -7.0, 0.0), (-1.05, -49.73, 0.42, 17.0, 0.55)]  # x, z, size, yaw, lift
BARRELS = [(-1.02, -48.55), (-1.12, -48.0)]
# Keep-out for dressing: the deck walking line, the spawn points and the bell.
WALK_X = (-0.6, 0.6)

# Palette slots and texture layers (spec 10.2 order; EnvTextureLayers).
S = dict(Grass=0, Foliage=1, FoliageDark=2, Wood=3, WoodDark=4, Sand=5, Rock=6, Reed=7, Dock=8, Roof=9, Accent=10)
L = dict(none=0, Grass=1, Foliage=2, Wood=3, Bark=4, Sand=5, Rock=6, Cloth=7)
AO_WOOD, AO_ROCK, AO_PROP = 0.45, 0.62, 0.5
MAX_TRIS = 21000

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode('utf-8')))

def to_blender(v):
    return Vector((-v[0], -v[2], v[1]))

M_U2B = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, -1.0), (0.0, 1.0, 0.0)))

def ry(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'Y')

def rx(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'X')

def rz(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'Z')

def clamp(a, lo, hi):
    return lo if a < lo else hi if a > hi else a

# ---------------------------------------------------------------------------------------------------------------------------------------
# Geometry accumulator (Unity axes). One Group = one exported object.
# ---------------------------------------------------------------------------------------------------------------------------------------
class Group:
    def __init__(self, name, tri=False):
        self.name, self.tri = name, tri
        self.V, self.N, self.F, self.UV, self.SL, self.AO = [], [], [], [], [], []
        self.items = []
        self._mark = 0

    def add(self, geo, slot, layer, ao=AO_PROP, fsl=None):
        V, N, F, UV = geo
        base = len(self.V)
        self.V += [Vector(v) for v in V]
        self.N += [Vector(n).normalized() for n in N]
        for i, f in enumerate(F):
            self.F.append(tuple(base + k for k in f))
            self.UV.append(list(UV[i]))
            self.SL.append(fsl[i] if fsl else (slot, layer))
            self.AO.append(ao)

    def item(self, name, pos, size, collider='box', auto=True, **extra):
        # Records a placed item for the manifest. With auto, its bounds come from the geometry added since the previous item: position =
        # world AABB centre and footprint = world AABB size (yaw already included), so a box collider can be built from them directly.
        # Sphere items keep their sphere centre (position) and radius, plus the AABB.
        d = dict(name=name, position=[round(p, 3) for p in pos], footprint=[round(s, 3) for s in size], collider=collider)
        if auto and len(self.V) > self._mark:
            vs = self.V[self._mark:]
            lo = [min(v[i] for v in vs) for i in range(3)]
            hi = [max(v[i] for v in vs) for i in range(3)]
            d['bounds_min'] = [round(x, 3) for x in lo]
            d['bounds_max'] = [round(x, 3) for x in hi]
            if collider != 'sphere':
                d['position'] = [round((lo[i] + hi[i]) / 2, 3) for i in range(3)]
                d['footprint'] = [round(hi[i] - lo[i], 3) for i in range(3)]
        self._mark = len(self.V)
        d.update(extra)
        self.items.append(d)

    def tris(self):
        return sum(len(f) - 2 for f in self.F)

def xform(geo, c, rot=None):
    V, N, F, UV = geo
    rot = rot or Matrix.Identity(3)
    c = Vector(c)
    return ([c + rot @ Vector(v) for v in V], [rot @ Vector(n) for n in N], F, UV)

def mirror_fix(geo):
    # For a part mapped through a reflection (det < 0): reverse every face (and its corner uvs).
    V, N, F, UV = geo
    return (V, N, [tuple(reversed(f)) for f in F], [list(reversed(u)) for u in UV])

def box_uv(p, fn, size):
    # Wood in metres, grain along u = the part's long axis when it lies in the face plane, else the face's longer in-plane axis.
    d = max(range(3), key=lambda i: abs(fn[i]))
    rem = [i for i in range(3) if i != d]
    lg = max(range(3), key=lambda i: size[i])
    if lg in rem:
        ua = lg
    else:
        ua = max(rem, key=lambda i: size[i])
    va = rem[0] if rem[1] == ua else rem[1]
    return (p[ua], p[va])

def bm_geo(bm, size=None, normals='smooth', uv='box', center=None):
    # Extract (V, N, F, UV) from a bmesh in its local frame. normals: 'smooth' (vertex), 'weighted' (area-weighted faces: flat faces stay
    # flat next to bevel strips), 'flat' (unwelded). uv: 'box' (metres, long axis), 'xz' (planar).
    bm.verts.ensure_lookup_table(); bm.verts.index_update(); bm.normal_update()
    if size is None:
        lo = Vector((min(v.co[i] for v in bm.verts) for i in range(3)))
        hi = Vector((max(v.co[i] for v in bm.verts) for i in range(3)))
        size = hi - lo
    V, N, F, UV = [], [], [], []
    # Deterministic face order: bmesh ops such as triangulate may emit faces (and pick each face's first loop) in a run-dependent order,
    # so faces are sorted by their rounded corner positions and each loop list starts at its smallest corner (winding kept).
    def ck(v):
        return (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
    def loops_of(f):
        lps = list(f.loops)
        k = min(range(len(lps)), key=lambda i: ck(lps[i].vert))
        return lps[k:] + lps[:k]
    faces = sorted(bm.faces, key=lambda f: tuple(sorted(ck(v) for v in f.verts)))
    if normals == 'flat':
        for f in faces:
            idx = []
            for lp in loops_of(f):
                idx.append(len(V)); V.append(lp.vert.co.copy()); N.append(f.normal.copy())
            F.append(idx)
            UV.append([box_uv(lp.vert.co, f.normal, size) if uv == 'box' else (lp.vert.co.x, lp.vert.co.z) for lp in loops_of(f)])
        return V, N, F, UV
    for v in bm.verts:
        V.append(v.co.copy())
        if normals == 'weighted':
            n = Vector()
            for f in v.link_faces:
                n += f.normal * f.calc_area()
            N.append(n.normalized() if n.length > 1e-12 else v.normal.copy())
        else:
            N.append(v.normal.copy())
    for f in faces:
        lps = loops_of(f)
        F.append([lp.vert.index for lp in lps])
        UV.append([box_uv(lp.vert.co, f.normal, size) if uv == 'box' else (lp.vert.co.x, lp.vert.co.z) for lp in lps])
    return V, N, F, UV

# ---- primitives (local frame, then placed with xform) ----

def rbox(size, r, segs=2):
    # Rounded box centred on the origin; exact rounded-box normals (the SmoothMeshBuilder.RoundedBox look).
    sx, sy, sz = size
    r = min(r, 0.45 * min(size))
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=r, offset_type='OFFSET', segments=segs, profile=0.5,
                    affect='EDGES', clamp_overlap=True)
    bm.verts.index_update(); bm.normal_update()
    inner = Vector((sx / 2 - r, sy / 2 - r, sz / 2 - r))
    V, N, F, UV = [], [], [], []
    for v in bm.verts:
        p = v.co
        q = Vector((clamp(p.x, -inner.x, inner.x), clamp(p.y, -inner.y, inner.y), clamp(p.z, -inner.z, inner.z)))
        d = p - q
        V.append(p.copy()); N.append(d.normalized() if d.length > 1e-9 else v.normal.copy())
    for f in bm.faces:
        F.append([v.index for v in f.verts])
        UV.append([box_uv(lp.vert.co, f.normal, size) for lp in f.loops])
    bm.free()
    return V, N, F, UV

def lathe(prof, sides=12, cap0=False, cap1=False, ang0=0.0, ring_slots=None):
    # Surface of revolution about +y. prof: [(h, r)] with ascending h. UV u = h (grain up), v = arc length. Returns geo (+ face slots).
    V, N, F, UV = [], [], [], []
    fs = []
    n = len(prof)
    pn = []
    for i in range(n):
        a, b = prof[max(i - 1, 0)], prof[min(i + 1, n - 1)]
        dh, dr = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dh, dr) or 1.0
        pn.append((dh / ln, -dr / ln))
    for i, (h, r) in enumerate(prof):
        for s in range(sides + 1):
            a = ang0 + 2 * math.pi * s / sides
            ca, sa = math.cos(a), math.sin(a)
            V.append(Vector((r * ca, h, r * sa)))
            N.append(Vector((pn[i][0] * ca, pn[i][1], pn[i][0] * sa)))
    rmax = max(r for _, r in prof)
    for i in range(n - 1):
        for s in range(sides):
            a = i * (sides + 1) + s
            b, d = a + 1, a + sides + 1
            c = d + 1
            F.append([a, d, c, b])
            ua, ub = prof[i][0], prof[i + 1][0]
            va, vb = rmax * 2 * math.pi * s / sides, rmax * 2 * math.pi * (s + 1) / sides
            UV.append([(ua, va), (ub, va), (ub, vb), (ua, vb)])
            fs.append(ring_slots[i] if ring_slots else None)
    for which, cap in ((0, cap0), (n - 1, cap1)):
        if not cap or prof[which][1] < 1e-6:
            continue
        h, r = prof[which]
        ny = -1.0 if which == 0 else 1.0
        ci = len(V)
        V.append(Vector((0, h, 0))); N.append(Vector((0, ny, 0)))
        ring = []
        for s in range(sides):
            a = ang0 + 2 * math.pi * s / sides
            ring.append(len(V)); V.append(Vector((r * math.cos(a), h, r * math.sin(a)))); N.append(Vector((0, ny, 0)))
        for s in range(sides):
            p, q = ring[s], ring[(s + 1) % sides]
            f = [ci, p, q] if which == 0 else [ci, q, p]
            F.append(f); UV.append([(V[k].x, V[k].z) for k in f])
            fs.append(ring_slots[0 if which == 0 else -1] if ring_slots else None)
    return (V, N, F, UV), fs

def tube(pts, radius, sides=6, caps=True, closed=False):
    # Swept circle along a polyline (parallel-transport frames). UV u = arc length (fibres along the rope), v = around.
    pts = [Vector(p) for p in pts]
    n = len(pts)
    T = []
    for i in range(n):
        if closed:
            t = pts[(i + 1) % n] - pts[i - 1]
        else:
            t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        T.append(t.normalized())
    ref = Vector((0, 1, 0)) if abs(T[0].y) < 0.9 else Vector((1, 0, 0))
    nrm = (ref - T[0] * ref.dot(T[0])).normalized()
    frames = []
    for i in range(n):
        if i > 0:
            nrm = (nrm - T[i] * nrm.dot(T[i])).normalized()
        frames.append((nrm.copy(), nrm.cross(T[i]).normalized()))
    V, N, F, UV = [], [], [], []
    acc = [0.0]
    for i in range(1, n):
        acc.append(acc[-1] + (pts[i] - pts[i - 1]).length)
    rings = n + (1 if closed else 0)
    for k in range(rings):
        i = k % n
        nn, bb = frames[i]
        for s in range(sides + 1):
            a = 2 * math.pi * s / sides
            d = nn * math.cos(a) + bb * math.sin(a)
            V.append(pts[i] + d * radius); N.append(d)
    for k in range(rings - 1):
        for s in range(sides):
            a = k * (sides + 1) + s
            b, d = a + 1, a + sides + 1
            c = d + 1
            F.append([a, d, c, b])
            u0 = acc[k] if k < n else acc[-1] + (pts[0] - pts[-1]).length
            u1 = acc[k + 1] if k + 1 < n else acc[-1] + (pts[0] - pts[-1]).length
            UV.append([(u0, radius * a / (sides + 1)), (u1, radius * a / (sides + 1)), (u1, radius * (a + 1) / (sides + 1)), (u0, radius * (a + 1) / (sides + 1))])
    if caps and not closed:
        for which in (0, n - 1):
            t = T[which] * (-1 if which == 0 else 1)
            ci = len(V); V.append(pts[which] + t * radius * 0.35); N.append(t.copy())
            base = which * (sides + 1)
            for s in range(sides):
                p, q = base + s, base + s + 1
                f = [ci, q, p] if which == 0 else [ci, p, q]
                F.append(f); UV.append([(0, 0), (0.02, 0), (0, 0.02)])
    return V, N, F, UV

def blob(radius, r, scale=(1.0, 0.8, 1.0), subdiv=3, rough=0.2, flat=-0.55, yaw=0.0):
    # Lake rock recipe: icosphere with two noise octaves plus a rounded-box bias, flattened base, kept inside the radius-sphere.
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    off = Vector((r.uniform(-50, 50), r.uniform(-50, 50), r.uniform(-50, 50)))
    for v in bm.verts:
        d = v.co.normalized()
        n1 = noise.noise(d * 1.4 + off)
        n2 = noise.noise(d * 3.3 + off * 1.7)
        box = 1.0 / max(abs(d.x), abs(d.y), abs(d.z))
        bias = 1.0 + (min(box * 0.62, 1.12) - 1.0) * 0.45
        p = d * (1.0 + rough * n1 + rough * 0.35 * n2) * bias
        if p.y < flat:
            p.y = flat + (p.y - flat) * 0.2
        v.co = p
    mx = max(Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])).length for v in bm.verts)
    k = radius / mx
    rot = ry(yaw)
    for v in bm.verts:
        v.co = rot @ Vector((v.co.x * scale[0] * k, v.co.y * scale[1] * k, v.co.z * scale[2] * k))
    geo = bm_geo(bm, normals='smooth', uv='xz')
    bm.free()
    return geo

def prism(poly, depth, r, segs=2):
    # A polygon in the (z, y) plane extruded along x (centred), bevelled; area-weighted normals keep the flat faces flat.
    bm = bmesh.new()
    a = [bm.verts.new((-depth / 2, y, z)) for z, y in poly]
    b = [bm.verts.new((depth / 2, y, z)) for z, y in poly]
    m = len(poly)
    for i in range(m):
        j = (i + 1) % m
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=r, offset_type='OFFSET', segments=segs, profile=0.5,
                    affect='EDGES', clamp_overlap=True)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method='BEAUTY', ngon_method='BEAUTY')
    geo = bm_geo(bm, normals='weighted')
    bm.free()
    return geo

def eval_cage(bm, mods, smooth=True, mat_count=1):
    # Runs a bmesh cage through modifiers (solidify, subsurf) and returns local (V, N, F, mat_index per face, face normals).
    me = bpy.data.meshes.new('fg_tmp')
    bm.to_mesh(me)
    for p in me.polygons:
        p.use_smooth = smooth
    ob = bpy.data.objects.new('fg_tmp', me)
    bpy.context.scene.collection.objects.link(ob)
    for i in range(mat_count):
        m = bpy.data.materials.get('fg_tmp_%d' % i) or bpy.data.materials.new('fg_tmp_%d' % i)
        me.materials.append(m)
    for kind, kw in mods:
        md = ob.modifiers.new(kind, kind)
        for k, v in kw.items():
            setattr(md, k, v)
    dg = bpy.context.evaluated_depsgraph_get()
    oe = ob.evaluated_get(dg)
    m2 = oe.to_mesh()
    V = [v.co.copy() for v in m2.vertices]
    N = [v.normal.copy() for v in m2.vertices]
    F = [list(p.vertices) for p in m2.polygons]
    MI = [p.material_index for p in m2.polygons]
    FN = [p.normal.copy() for p in m2.polygons]
    oe.to_mesh_clear()
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    return V, N, F, MI, FN

def text_geo(body, size, extrude):
    cu = bpy.data.curves.new('fg_txt', 'FONT')
    cu.body = body; cu.size = size; cu.extrude = extrude; cu.resolution_u = 2
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    ob = bpy.data.objects.new('fg_txt', cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    oe = ob.evaluated_get(dg)
    m2 = oe.to_mesh()
    bm = bmesh.new(); bm.from_mesh(m2)
    oe.to_mesh_clear()
    bpy.data.objects.remove(ob, do_unlink=True); bpy.data.curves.remove(cu)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    geo = bm_geo(bm, normals='flat', uv='xz')
    bm.free()
    return geo

def merge_geo(parts):
    V, N, F, UV, FS = [], [], [], [], []
    for geo, fs in parts:
        base = len(V)
        V += geo[0]; N += geo[1]
        F += [[base + k for k in f] for f in geo[2]]; UV += geo[3]
        FS += fs if fs else [None] * len(geo[2])
    return (V, N, F, UV), FS

def add_lathe(g, c, prof, slot, layer, ao, sides=12, rot=None, cap0=False, cap1=False, ring_slots=None):
    geo, fs = lathe(prof, sides, cap0, cap1, ring_slots=ring_slots)
    fsl = [(x if x is not None else slot, layer) for x in fs] if ring_slots else None
    g.add(xform(geo, c, rot), slot, layer, ao, fsl)

def add_box(g, c, size, r, slot, layer, ao, rot=None, segs=2):
    g.add(xform(rbox(size, r, segs), c, rot), slot, layer, ao)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Pier: deck (planks + stringers), T-head and ramp, posts, ladders.
# ---------------------------------------------------------------------------------------------------------------------------------------
PLANK_W, PLANK_GAP, PLANK_YAW = 0.26, 0.04, 0.8

def plank_draw(r):
    slot = S['Wood'] if r.random() < 0.5 else S['Dock']
    yaw = r.uniform(-PLANK_YAW, PLANK_YAW)
    return slot, yaw

def build_pier_deck():
    g = Group('Harbour_PierDeck')
    r = rng('harbour_pier.planks')
    length = DECK_Z1 - DECK_Z0
    count = int(length / (PLANK_W + PLANK_GAP))
    pitch = length / count
    cy = DECK_TOP - DECK_THICK / 2
    for i in range(count):
        z = DECK_Z0 + pitch * (i + 0.5)
        slot, yaw = plank_draw(r)
        # the yaw is about the vertical axis, so every top stays exactly at DECK_TOP
        g.add(xform(rbox((DECK_X1 - DECK_X0, DECK_THICK, PLANK_W), 0.02), (0, cy, z), ry(yaw)), slot, L['Wood'], AO_WOOD)
    # Two WoodDark stringers under the plank ends (they rest on the posts and give the deck edge a dark band against the sea).
    for side in (-1, 1):
        add_box(g, (1.38 * side, DECK_UNDER - 0.06, (DECK_Z0 + DECK_Z1) / 2), (0.12, 0.12, length - 0.1), 0.025, S['WoodDark'], L['Wood'], 0.4)
    return g

def build_pier_head():
    g = Group('Harbour_PierHead')
    r = rng('harbour_pier.planks.head')
    width = THEAD_X1 - THEAD_X0
    count = int(width / (PLANK_W + PLANK_GAP))
    pitch = width / count
    cy = DECK_TOP - DECK_THICK / 2
    for i in range(count):
        x = THEAD_X0 + pitch * (i + 0.5)
        slot, yaw = plank_draw(r)
        g.add(xform(rbox((PLANK_W, DECK_THICK, THEAD_Z1 - THEAD_Z0), 0.02), (x, cy, (THEAD_Z0 + THEAD_Z1) / 2), ry(yaw)), slot, L['Wood'], AO_WOOD)
    for z in (-7.6, -5.4):  # bearers on the corner posts
        add_box(g, (0, DECK_UNDER - 0.07, z), (width - 0.1, 0.14, 0.14), 0.025, S['WoodDark'], L['Wood'], 0.4)
    # Ramp: planks across it, centred on the collider mid-plane so their top is the collider top (the lake RampDeck construction).
    low, high = Vector(RAMP_LOW), Vector(RAMP_HIGH)
    along = high - low
    ln = along.length
    pitch_angle = math.degrees(math.atan2(along.y, along.z))
    rot = rx(-pitch_angle)
    centre = (low + high) / 2
    n = int(ln / (PLANK_W + PLANK_GAP))
    rp = ln / n
    for i in range(n):
        local = -ln / 2 + rp * (i + 0.5)
        slot, yaw = plank_draw(r)
        c = centre + rot @ Vector((0, 0, local))
        g.add(xform(rbox((RAMP_W, RAMP_THICK, rp - PLANK_GAP), 0.02), c, rot @ ry(yaw)), slot, L['Wood'], AO_WOOD)
    for side in (-1, 1):
        c = centre + rot @ Vector((1.2 * side, -RAMP_THICK / 2 - 0.05, 0))
        g.add(xform(rbox((0.1, 0.1, ln - 0.1), 0.02), c, rot), S['WoodDark'], L['Wood'], 0.4)
    return g

def post_positions():
    out = []
    n = int(round((POST_Z1 - POST_Z0) / POST_STEP)) + 1
    for i in range(n):
        z = POST_Z0 + POST_STEP * i
        out += [(-POST_X, z), (POST_X, z)]
    return out + THEAD_POSTS

def build_posts():
    g = Group('Harbour_PierPosts')
    for x, z in post_positions():
        bed = terrain_y(z)
        hgt = DECK_UNDER - bed
        R = POST_R
        prof = [(-0.02, R * 1.18), (0.12, R * 1.06), (0.4, R)]
        prof = [p for p in prof if p[0] < hgt - 0.05] + [(hgt - 0.015, R), (hgt, R * 0.82)]
        if hgt < 0.45:
            prof = [(-0.02, R * 1.1), (hgt - 0.015, R), (hgt, R * 0.82)]
        add_lathe(g, (x, bed, z), prof, S['WoodDark'], L['Wood'], 0.42, sides=12, cap1=True)
        g.item('Post', (x, bed + hgt / 2, z), (0.18, hgt, 0.18))
    return g

def ladder(g, x0, x1, z0, z1, along_z):
    # Rails along the rail box's long horizontal axis ends, rungs every 0.3 m between them, WoodDark.
    rail = 0.05
    if along_z:   # rails at x centre, z ends inset 0.05 from the box
        xc = (x0 + x1) / 2
        ends = [(xc, z0 + 0.05), (xc, z1 - 0.05)]
        span = (z1 - z0) - 0.1 - rail + 0.01   # rungs end 5 mm inside the rails (not buried in them: the AO bake stays clean)
    else:
        zc = (z0 + z1) / 2
        ends = [(x0 + 0.05, zc), (x1 - 0.05, zc)]
        span = (x1 - x0) - 0.1 - rail + 0.01
    hgt = LADDER_RAIL_TOP - LADDER_BOTTOM
    for ex, ez in ends:
        add_box(g, (ex, (LADDER_RAIL_TOP + LADDER_BOTTOM) / 2, ez), (rail, hgt, rail), 0.02, S['WoodDark'], L['Wood'], 0.35)
    y = LADDER_BOTTOM + 0.3
    while y <= DECK_TOP - 0.08:
        cx, cz = (ends[0][0] + ends[1][0]) / 2, (ends[0][1] + ends[1][1]) / 2
        size = (0.04, 0.035, span) if along_z else (span, 0.035, 0.04)
        add_box(g, (cx, y, cz), size, 0.014, S['WoodDark'], L['Wood'], 0.35)
        y += 0.3

def build_ladders():
    g = Group('Harbour_Ladders')
    (ax0, _, az0), (ax1, _, az1) = LADDER_MID_RAILBOX
    ladder(g, ax0, ax1, az0, az1, True)
    (bx0, _, bz0), (bx1, _, bz1) = LADDER_HEAD_RAILBOX
    ladder(g, bx0, bx1, bz0, bz1, False)
    g.item('LadderMid', (2.05, 0.55, -25.0), (1.0, 3.9, 1.0), collider='LadderVolume (layout)', auto=False, rail_top=LADDER_RAIL_TOP)
    g.item('LadderHead', (2.0, 0.55, -4.45), (1.0, 3.9, 1.0), collider='LadderVolume (layout)', auto=False, rail_top=LADDER_RAIL_TOP)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Breakwater: boulder rows along both sides + a stone path on top (top exactly 1.5), and a submerged rubble base. Rocks on the beach.
# ---------------------------------------------------------------------------------------------------------------------------------------
def build_breakwater():
    g = Group('Harbour_Breakwater_Tri', tri=True)
    base = Group('Harbour_BreakwaterBase_Tri', tri=True)
    r = rng('harbour_pier.stones')
    x0, x1, z0, z1 = BREAKWATER
    # Upper rows: centres outside the walkable box (x 27.4 / 32.6) so the rock inside x 28..32 stays below about 1.53.
    z = z0 + 0.8
    rows = []
    while z <= z1 - 0.6:
        rows.append(z); z += 1.6
    for side, xc in ((-1, x0 - 0.6), (1, x1 + 0.6)):
        for zc in rows:
            rad = r.uniform(0.8, 1.3); yaw = r.uniform(0, 360)
            jx, jz = r.uniform(-0.2, 0.2), r.uniform(-0.2, 0.2)
            top = r.uniform(1.45, 1.65)
            c = (xc + jx, top - 0.8 * rad, zc + jz)
            g.add(xform(blob(rad, r, (1.0, 0.8, 1.0), yaw=yaw), c), S['Rock'], L['Rock'], AO_ROCK)
            g.item('Boulder', c, (2 * rad, 1.6 * rad, 2 * rad), collider='sphere', radius=round(rad, 3))
    for xc in (28.7, 30.0, 31.3):  # tip end row, inside z > -25 except the curve, tops 1.45
        rad = r.uniform(0.8, 1.0); yaw = r.uniform(0, 360)
        c = (xc + r.uniform(-0.1, 0.1), 1.45 - 0.8 * rad, z1 + 0.75)
        g.add(xform(blob(rad, r, (1.0, 0.8, 1.0), yaw=yaw), c), S['Rock'], L['Rock'], AO_ROCK)
        g.item('Boulder', c, (2 * rad, 1.6 * rad, 2 * rad), collider='sphere', radius=round(rad, 3))
    # Path: a base course, then two lines of rounded slabs, tops exactly at BREAKWATER_TOP. Rock slot, Sand layer (triplanar).
    add_box(g, ((x0 + x1) / 2, BREAKWATER_TOP - 0.08 - 0.3, (z0 + z1) / 2), (x1 - x0 - 0.1, 0.6, z1 - z0 - 0.1), 0.08, S['Rock'], L['Sand'], AO_ROCK, segs=1)
    n = int((z1 - z0) / 1.2)
    pitch = (z1 - z0) / n
    for i in range(n):
        zc = z0 + pitch * (i + 0.5)
        # staggered joints: the split between the two slabs of a row moves from row to row
        w1 = (1.35, 2.45, 1.9)[i % 3] + r.uniform(-0.15, 0.15)
        for sx0, sx1 in ((x0 + 0.04, x0 + 0.04 + w1), (x0 + 0.04 + w1 + 0.06, x1 - 0.04)):
            yaw = r.uniform(-1.0, 1.0)
            add_box(g, ((sx0 + sx1) / 2, BREAKWATER_TOP - 0.08, zc), (sx1 - sx0, 0.16, pitch - 0.07), 0.05, S['Rock'], L['Sand'], AO_ROCK, rot=ry(yaw))
    # Submerged rubble base (tiers at y -1.6 and -3.6) so the wall does not hang in the water; only where the seabed is below it.
    rb = rng('harbour_pier.stones.base')
    for tier_y, xs in ((-1.6, (x0 - 1.1, x1 + 1.1)), (-3.6, (x0 - 1.6, x1 + 1.6))):
        for xc in xs:
            zc = z0 + 0.8
            while zc <= z1 + 0.3:
                rad = rb.uniform(1.0, 1.4); yaw = rb.uniform(0, 360)
                c = (xc + rb.uniform(-0.25, 0.25), tier_y + rb.uniform(-0.2, 0.2), zc + rb.uniform(-0.2, 0.2))
                if c[1] > seabed_y(c[2]) + 0.1 * rad and c[2] > COAST_Z - 1.0:
                    base.add(xform(blob(rad, rb, (1.0, 0.85, 1.0), yaw=yaw), c), S['Rock'], L['Rock'], AO_ROCK)
                    base.item('RubbleBoulder', c, (2 * rad, 1.7 * rad, 2 * rad), collider='none (underwater; optional sphere)', radius=round(rad, 3))
                zc += 1.7
        for xc in (28.6, 30.0, 31.4):
            rad = rb.uniform(1.0, 1.3)
            c = (xc, tier_y, z1 + 1.3)
            if c[1] > seabed_y(c[2]) + 0.1 * rad:
                base.add(xform(blob(rad, rb, (1.0, 0.85, 1.0), yaw=rb.uniform(0, 360)), c), S['Rock'], L['Rock'], AO_ROCK)
                base.item('RubbleBoulder', c, (2 * rad, 1.7 * rad, 2 * rad), collider='none (underwater; optional sphere)', radius=round(rad, 3))
    return g, base

def build_rocks():
    g = Group('Harbour_Rocks_Tri', tri=True)
    r = rng('harbour_pier.rocks')
    placed = 0
    attempts = 0
    while placed < ROCK_COUNT and attempts < 500:
        attempts += 1
        x = r.uniform(*ROCK_X); z = r.uniform(*ROCK_Z); size = r.uniform(*ROCK_SIZE); yaw = r.uniform(0, 360)
        if abs(x) < 4 or abs(x - 30) < 4:
            continue
        if any(math.hypot(x - p[0], z - p[2]) < 3 for p in STAND_POINTS):
            continue
        R = size / 2
        c = (x, beach_y(z) + 0.38 * R, z)
        g.add(xform(blob(R, r, (1.0, 0.78, 1.0), yaw=yaw), c), S['Rock'], L['Rock'], AO_ROCK)
        g.item('Rock_%02d' % placed, c, (size, 0.78 * size, size), collider='sphere', radius=round(R, 3))
        placed += 1
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Props (layout T17.2): hut, bollards, crates, barrels, lamp posts.
# ---------------------------------------------------------------------------------------------------------------------------------------
def build_hut(g):
    hx, hy, hz = HUT_C
    W, D = 3.9, 2.9
    eave, ridge = 2.0, 2.78
    # Walls: one bevelled house-shaped prism (ridge along x), Wood.
    poly = [(-D / 2, 0.0), (D / 2, 0.0), (D / 2, eave), (0.0, ridge), (-D / 2, eave)]
    g.add(xform(prism(poly, W, 0.04), (hx, hy, hz)), S['Wood'], L['Wood'], AO_WOOD)
    # Corner posts and a base trim, WoodDark.
    for sx in (-1, 1):
        for sz in (-1, 1):
            add_box(g, (hx + sx * (W / 2), hy + eave / 2, hz + sz * (D / 2)), (0.14, eave, 0.14), 0.03, S['WoodDark'], L['Wood'], 0.4)
    for sz in (-1, 1):
        add_box(g, (hx, hy + 0.08, hz + sz * (D / 2 + 0.01)), (W + 0.1, 0.16, 0.06), 0.02, S['WoodDark'], L['Wood'], 0.4)
        add_box(g, (hx, hy + eave - 0.06, hz + sz * (D / 2 + 0.01)), (W + 0.1, 0.12, 0.06), 0.02, S['WoodDark'], L['Wood'], 0.4)
    # Roof: two rounded planes, overhanging 0.25 m, Roof slot (wood layer = shingle boards), plus a ridge roll.
    run = D / 2 + 0.25
    ang = math.degrees(math.atan2(ridge - eave, D / 2))
    slab_len = run / math.cos(math.radians(ang)) + 0.05
    for sz in (-1, 1):
        rot = rx(ang * sz)
        mid_z = hz + sz * run / 2
        mid_y = hy + ridge - (ridge - eave) * (run / 2) / (D / 2) + 0.07
        g.add(xform(rbox((W + 0.5, 0.11, slab_len), 0.04), (hx, mid_y, mid_z), rot), S['Roof'], L['Wood'], 0.5)
    add_lathe(g, (hx - W / 2 - 0.25, hy + ridge + 0.1, hz), [(0, 0.0), (0.02, 0.07), (0.06, 0.09), (W + 0.44, 0.09), (W + 0.48, 0.07), (W + 0.5, 0.0)],
              S['Roof'], L['Wood'], 0.5, sides=10, rot=rz(-90))
    # Door (Dock) with two battens, on the +z wall facing the pier; a window with an Accent frame; a stove pipe.
    fz = hz + D / 2
    add_box(g, (hx - 0.7, hy + 0.95, fz + 0.04), (0.9, 1.8, 0.07), 0.03, S['Dock'], L['Wood'], 0.5)
    for by in (0.45, 1.45):
        add_box(g, (hx - 0.7, hy + by, fz + 0.085), (0.86, 0.1, 0.035), 0.012, S['WoodDark'], L['Wood'], 0.45)
    add_box(g, (hx - 0.4, hy + 0.95, fz + 0.1), (0.06, 0.06, 0.06), 0.025, S['Accent'], L['none'], 0.5)   # door knob
    wx, wy = hx + 0.9, hy + 1.35
    add_box(g, (wx, wy, fz + 0.02), (0.62, 0.52, 0.04), 0.015, S['WoodDark'], L['none'], 0.35)
    for dx, dy, sx_, sy_ in ((0, 0.3, 0.78, 0.08), (0, -0.3, 0.86, 0.1), (-0.35, 0, 0.08, 0.6), (0.35, 0, 0.08, 0.6), (0, 0, 0.04, 0.5)):
        add_box(g, (wx + dx, wy + dy, fz + 0.05), (sx_, sy_, 0.06), 0.02, S['Accent'], L['Wood'], 0.5)
    add_lathe(g, (hx - 1.0, hy + ridge - 0.45, hz - 0.75), [(0, 0.075), (0.62, 0.075), (0.63, 0.12), (0.69, 0.13), (0.77, 0.06), (0.8, 0.0)],
              S['Rock'], L['none'], 0.5, sides=10)
    g.item('Hut', (hx, hy + 1.5, hz), HUT_SIZE)

def build_props():
    g = Group('Harbour_Props')
    build_hut(g)
    bollard = [(0.0, 0.15), (0.04, 0.15), (0.07, 0.12), (0.26, 0.105), (0.31, 0.15), (0.37, 0.165), (0.41, 0.14), (0.44, 0.07), (0.45, 0.0)]
    for i, (x, z) in enumerate(BOLLARDS):
        add_lathe(g, (x, DECK_TOP, z), bollard, S['Accent'], L['none'], 0.5, sides=14, cap0=True)
        g.item('Bollard_%d' % i, (x, DECK_TOP + 0.225, z), (0.34, 0.45, 0.34))
    for i, (x, z, s, yaw, lift) in enumerate(CRATES):
        c = (x, DECK_TOP + lift + s / 2, z)
        rot = ry(yaw)
        g.add(xform(rbox((s, s, s), 0.04), c, rot), S['Wood'], L['Wood'], AO_WOOD)
        for dx, dz in ((-1, -1), (-1, 1), (1, -1), (1, 1)):
            g.add(xform(rbox((0.07, s + 0.01, 0.07), 0.02), Vector(c) + rot @ Vector((dx * (s / 2 - 0.025), 0, dz * (s / 2 - 0.025))), rot),
                  S['WoodDark'], L['Wood'], 0.45)
        for dy in (-1, 1):
            g.add(xform(rbox((s + 0.01, 0.06, s + 0.01), 0.02), Vector(c) + Vector((0, dy * (s / 2 - 0.03), 0)), rot), S['Dock'], L['Wood'], 0.45)
        g.item('Crate_%d' % i, c, (s, s, s), yaw=yaw)
    barrel = [(0.0, 0.215), (0.02, 0.235), (0.2, 0.26), (0.39, 0.272), (0.58, 0.26), (0.76, 0.235), (0.78, 0.215)]
    for i, (x, z) in enumerate(BARRELS):
        add_lathe(g, (x, DECK_TOP, z), barrel, S['WoodDark'], L['Wood'], 0.45, sides=16, cap0=True, cap1=True)
        for h, rr in ((0.12, 0.252), (0.66, 0.252)):
            add_lathe(g, (x, DECK_TOP, z), [(h - 0.035, rr - 0.004), (h - 0.028, rr + 0.008), (h + 0.028, rr + 0.008), (h + 0.035, rr - 0.004)],
                      S['Rock'], L['none'], 0.5, sides=16)
        g.item('Barrel_%d' % i, (x, DECK_TOP + 0.39, z), (0.55, 0.78, 0.55))
    for i, (x, z) in enumerate(LAMPS):
        add_box(g, (x, DECK_TOP + 0.06, z), (0.24, 0.12, 0.24), 0.03, S['WoodDark'], L['Wood'], 0.45)
        add_lathe(g, (x, DECK_TOP + 0.1, z), [(0, 0.075), (0.1, 0.065), (2.3, 0.055), (2.34, 0.07), (2.38, 0.06)], S['WoodDark'], L['Wood'], 0.5, sides=10)
        add_lathe(g, (x, DECK_TOP + 2.45, z), [(0, 0.05), (0.04, 0.12), (0.3, 0.135), (0.34, 0.08)], S['Accent'], L['none'], 0.6, sides=12, cap0=True)
        add_lathe(g, (x, DECK_TOP + 2.76, z), [(0, 0.17), (0.05, 0.16), (0.17, 0.04), (0.2, 0.035), (0.24, 0.05), (0.27, 0.0)], S['WoodDark'], L['none'], 0.5, sides=12, cap0=True)
        add_box(g, (x, DECK_TOP + 2.42, z), (0.16, 0.06, 0.16), 0.02, S['WoodDark'], L['none'], 0.5)
        g.item('Lamp_%d' % i, (x, DECK_TOP + 1.5, z), (0.24, 3.03, 0.24))
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Boats (dressing, in the water): two rowboats moored beside the pier and a fishing skiff inside the breakwater.
# ---------------------------------------------------------------------------------------------------------------------------------------
def hull(Lh, W, D, transom=0.62, sheer=0.16, hull_slot=S['Roof'], stripe=S['Sand']):
    # Lofted U sections (stern -z .. bow +z), solidified (inner shell + rim) and subdivided once. Returns local geo + face slots.
    bm = bmesh.new()
    ns, nj = 9, 9
    rows = []
    for k in range(ns):
        t = k / (ns - 1)
        z = -Lh / 2 + Lh * t
        if t < 0.45:
            w = W / 2 * (transom + (1 - transom) * math.sin(0.5 * math.pi * t / 0.45))
        else:
            w = W / 2 * max(0.04, math.cos(0.5 * math.pi * (t - 0.45) / 0.55) ** 0.75)
        d = D * (0.92 - 0.3 * max(0.0, t - 0.6))
        rim = 0.05 * (1 - t) ** 2 + sheer * t ** 2.5
        row = []
        for j in range(nj):
            th = -math.pi / 2 + math.pi * j / (nj - 1)
            x = w * math.sin(th)
            y = rim - d * max(0.0, math.cos(th)) ** 0.6
            row.append(bm.verts.new((x, y, z)))
        rows.append(row)
    for k in range(ns - 1):
        for j in range(nj - 1):
            bm.faces.new((rows[k][j], rows[k][j + 1], rows[k + 1][j + 1], rows[k + 1][j]))
    bm.faces.new(rows[0])
    bm.faces.new(list(reversed(rows[-1])))
    bm.normal_update()
    probe = [f for f in bm.faces if len(f.verts) == 4][len(bm.faces) // 2 - 4]
    if probe.normal.y > 0.3 or (probe.calc_center_median().x * probe.normal.x < -1e-6):
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    V, N, F, MI, FN = eval_cage(bm, [('SOLIDIFY', dict(thickness=0.045, offset=-1.0, use_rim=True, use_even_offset=True,
                                                       material_offset=1, material_offset_rim=2)),
                                     ('SUBSURF', dict(levels=1, render_levels=1))], mat_count=3)
    bm.free()
    size = (W, D, Lh)
    UV, fs = [], []
    for i, f in enumerate(F):
        UV.append([box_uv(V[k], FN[i], size) for k in f])
        cy = sum(V[k].y for k in f) / len(f)
        tz = clamp((sum(V[k].z for k in f) / len(f) + Lh / 2) / Lh, 0.0, 1.0)
        rim_here = 0.05 * (1 - tz) ** 2 + sheer * tz ** 2.5
        if MI[i] == 0:
            fs.append((stripe if cy > rim_here - 0.09 else hull_slot, L['Wood']))
        elif MI[i] == 1:
            fs.append((S['Wood'], L['Wood']))
        else:
            fs.append((S['WoodDark'], L['Wood']))
    return (V, N, F, UV), fs

def add_part(g, geo_fs, c, rot, ao):
    geo, fs = geo_fs
    g.add(xform(geo, c, rot), 0, 0, ao, fs)

def rowboat(g, name, c, heading, hull_slot, roll=2.0, oars=True):
    rot = ry(heading) @ rz(roll)
    Lh, W, D = 3.1, 1.25, 0.48
    add_part(g, hull(Lh, W, D, hull_slot=hull_slot), c, rot, 0.5)
    for zl, wl in ((-0.85, 0.95), (0.25, 1.12), (0.95, 0.8)):
        g.add(xform(rbox((wl, 0.05, 0.24), 0.02), Vector(c) + rot @ Vector((0, -0.12, zl)), rot), S['Wood'], L['Wood'], 0.5)
    if oars:
        for sx in (-1, 1):
            orot = rot @ ry(sx * 4) @ rx(90)
            oc = Vector(c) + rot @ Vector((sx * 0.3, -0.03, 0.05))
            geo, fs = lathe([(-1.0, 0.0), (-0.98, 0.025), (0.55, 0.025), (0.6, 0.03), (0.98, 0.022), (1.0, 0.0)], 8)
            g.add(xform(geo, oc, orot), S['Wood'], L['Wood'], 0.5)
            g.add(xform(rbox((0.13, 0.42, 0.025), 0.01), oc + orot @ Vector((0, 0.82, 0)), orot), S['Wood'], L['Wood'], 0.5)
    g.item(name, c, (W, 0.7, Lh), collider='box (optional)', heading_deg=heading)

def skiff(g, name, c, heading):
    rot = ry(heading) @ rz(-1.5)
    Lh, W, D = 4.2, 1.6, 0.6
    add_part(g, hull(Lh, W, D, transom=0.78, sheer=0.22, hull_slot=S['Accent'], stripe=S['Sand']), c, rot, 0.5)
    for zl, wl in ((-1.2, 1.28), (0.1, 1.5)):
        g.add(xform(rbox((wl, 0.06, 0.3), 0.025), Vector(c) + rot @ Vector((0, -0.16, zl)), rot), S['Wood'], L['Wood'], 0.5)
    # bow deck and an outboard motor on the transom
    g.add(xform(rbox((0.9, 0.05, 0.7), 0.02), Vector(c) + rot @ Vector((0, 0.08, 1.45)), rot), S['Dock'], L['Wood'], 0.5)
    mc = Vector(c) + rot @ Vector((0, 0.18, -Lh / 2 - 0.16))
    g.add(xform(rbox((0.32, 0.36, 0.42), 0.1), mc, rot), S['Rock'], L['none'], 0.5)
    g.add(xform(rbox((0.34, 0.08, 0.44), 0.03), mc + rot @ Vector((0, 0.2, 0)), rot), S['Accent'], L['none'], 0.5)
    geo, fs = lathe([(-0.95, 0.0), (-0.92, 0.05), (-0.3, 0.04), (0.0, 0.045)], 10)
    g.add(xform(geo, mc + rot @ Vector((0, -0.18, -0.05)), rot), S['Rock'], L['none'], 0.5)
    g.item(name, c, (W, 0.9, Lh + 0.5), collider='box (optional)', heading_deg=heading)

BOATS = [('Rowboat_West', (-3.35, 0.3, -36.2), 2.0, S['Roof']),
         ('Rowboat_East', (3.45, 0.3, -14.3), -3.0, S['Accent'])]
SKIFF = ('Skiff_Breakwater', (25.4, 0.4, -38.5), 4.0)

def build_boats():
    g = Group('Harbour_Boats')
    for name, c, hd, slot in BOATS:
        rowboat(g, name, c, hd, slot)
    skiff(g, SKIFF[0], SKIFF[1], SKIFF[2])
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Dressing: buoys, net on the hut, lobster pots, rope coils, mooring lines, life ring, the hand-painted sign at the pier root.
# ---------------------------------------------------------------------------------------------------------------------------------------
BUOYS = [('MooringBuoy_A', (-7.0, 0.0, -16.0), 'ball'), ('MooringBuoy_B', (8.0, 0.0, -17.0), 'ball'),
         ('MarkerBuoy_A', (21.0, 0.0, -14.0), 'marker'), ('MarkerBuoy_B', (-22.0, 0.0, -20.0), 'marker')]
POTS = [('LobsterPot_A', (-6.3, 1.2, -62.6), 8.0, 0.0), ('LobsterPot_B', (-6.32, 1.2, -62.58), -11.0, 0.36),
        ('LobsterPot_C', (2.85, 1.2, -59.6), 24.0, 0.0)]
COILS = [('RopeCoil_East', (3.85, DECK_TOP, -7.55)), ('RopeCoil_West', (-3.85, DECK_TOP, -7.55))]
SIGN = ('PierSign', (-2.9, 1.2, -58.8))

def rope_line(a, b, sag, n=14):
    a, b = Vector(a), Vector(b)
    return [a.lerp(b, i / n) - Vector((0, sag * 4 * (i / n) * (1 - i / n), 0)) for i in range(n + 1)]

def lobster_pot(g, name, c, yaw, lift):
    rot = ry(yaw)
    c = Vector(c) + Vector((0, lift, 0))
    g.add(xform(rbox((0.48, 0.05, 0.72), 0.015), c + Vector((0, 0.025, 0)), rot), S['Wood'], L['Wood'], 0.45)
    R = 0.22
    for zl in (-0.32, 0.0, 0.32):
        pts = [Vector((R * math.cos(a), 0.05 + R * math.sin(a), zl)) for a in [math.pi * i / 10 for i in range(11)]]
        g.add(xform(tube(pts, 0.016, 6), c, rot), S['WoodDark'], L['Wood'], 0.45)
    geo, fs = lathe([(-0.33, 0.0), (-0.325, R - 0.015), (0.325, R - 0.015), (0.33, 0.0)], 12, ang0=0.0)
    # keep the upper half of the cylinder (the netting); the lathe axis is turned to lie along z
    V, N, F, UV = geo
    keep = [i for i, f in enumerate(F) if sum(V[k].z for k in f) / len(f) < 0.005]   # rx(90) maps local -z to +y (up)
    geo = (V, N, [F[i] for i in keep], [UV[i] for i in keep])
    g.add(xform(geo, c + Vector((0, 0.05, 0)), rot @ rx(90) @ ry(0)), S['Sand'], L['Cloth'], 0.55)
    g.item(name, (c.x, c.y + 0.15, c.z), (0.5, 0.3, 0.74), yaw=yaw)

def rope_coil(g, name, c):
    c = Vector(c)
    pts = []
    turns = 3.2
    for i in range(int(turns * 18) + 1):
        a = 2 * math.pi * i / 18
        rr = 0.1 + 0.16 * i / (turns * 18)
        pts.append(Vector((rr * math.cos(a), 0.024, rr * math.sin(a))))
    g.add(xform(tube(pts, 0.024, 6), c), S['Sand'], L['Cloth'], 0.5)
    pts2 = []
    for i in range(int(2.0 * 18) + 1):
        a = 2 * math.pi * i / 18 + 0.7
        rr = 0.2 - 0.07 * i / 36
        pts2.append(Vector((rr * math.cos(a), 0.068, rr * math.sin(a))))
    g.add(xform(tube(pts2, 0.024, 6), c), S['Sand'], L['Cloth'], 0.5)
    g.item(name, (c.x, c.y + 0.05, c.z), (0.6, 0.1, 0.6), collider='none (low, walkable)')

def buoy(g, name, c, kind):
    c = Vector(c)
    if kind == 'ball':
        prof = [(-0.2, 0.0), (-0.17, 0.18), (-0.06, 0.29), (0.08, 0.3), (0.22, 0.24), (0.32, 0.12), (0.35, 0.0)]
        add_lathe(g, c, prof, S['Accent'], L['none'], 0.6, sides=14)
        g.add(xform(tube([Vector((0.07 * math.cos(a), 0.4 + 0.07 * math.sin(a), 0)) for a in [2 * math.pi * i / 10 for i in range(10)]], 0.015, 6, caps=False, closed=True), c),
              S['Rock'], L['none'], 0.6)
        g.item(name, c, (0.6, 0.6, 0.6), collider='none (floating)')
    else:
        prof = [(-0.35, 0.0), (-0.33, 0.2), (-0.2, 0.3), (0.3, 0.3), (0.32, 0.31), (0.48, 0.31), (0.5, 0.3), (0.62, 0.26), (1.05, 0.04), (1.08, 0.0)]
        add_lathe(g, c, prof, S['Roof'], L['none'], 0.55, sides=14, ring_slots=[S['Roof'], S['Roof'], S['Roof'], S['Sand'], S['Sand'], S['Sand'], S['Roof'], S['Roof'], S['Roof']])
        g.item(name, c, (0.62, 1.4, 0.62), collider='none (floating)')

def build_sign(g):
    name, c = SIGN
    c = Vector(c)
    for dx in (-0.99, 0.99):   # posts just outside the board ends, so they never cover the lettering
        add_lathe(g, c + Vector((dx, 0, 0)), [(-0.1, 0.06), (1.9, 0.055), (1.93, 0.045), (1.95, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=10)
    bc = c + Vector((0, 1.55, 0))
    add_box(g, bc, (1.85, 0.58, 0.07), 0.03, S['Dock'], L['Wood'], 0.5)
    add_box(g, bc + Vector((0, 0.33, 0)), (2.2, 0.08, 0.1), 0.025, S['WoodDark'], L['Wood'], 0.45)
    # Raised hand-painted lettering on BOTH faces (players see it from the hut and beach, and walking back from the pier).
    # Land side (-z): glyph x -> +x, glyph y -> +y, glyph front (+z) -> -z. Sea side (+z): glyph x -> -x, front stays +z. Both maps are
    # reflections, so the windings are reversed (mirror_fix); each side reads left to right for a viewer facing it.
    V, N, F, UV = text_geo('HARBOUR PIER', 0.24, 0.006)
    fr = rng('harbour_pier.sign')
    for side in (-1, 1):
        Mr = Matrix(((-side, 0, 0), (0, 1, 0), (0, 0, side)))
        geo = mirror_fix(([bc + Vector((0, 0.03, 0.04 * side)) + Mr @ v for v in V], [Mr @ n for n in N], F, UV))
        g.add(geo, S['Accent'], L['none'], 0.6)
        # a little painted fish under the words (body + tail), facing the reader's left
        g.add(xform(blob(0.12, fr, (1.6, 0.7, 0.25), subdiv=2, rough=0.05, flat=-2), bc + Vector((0.0, -0.17, 0.045 * side))), S['Roof'], L['none'], 0.6)
        g.add(xform(blob(0.07, fr, (0.6, 1.0, 0.3), subdiv=2, rough=0.03, flat=-2), bc + Vector((-0.165 * side, -0.17, 0.043 * side))), S['Roof'], L['none'], 0.6)
    g.item(name, (c.x, c.y + 1.0, c.z), (2.2, 2.0, 0.15))

def build_net(g):
    # Net draped on the hut's east wall (x -7), hung from two nails under the eave with sagging folds; Sand + Cloth, both sides.
    hx, hy, hz = HUT_C
    wall_x = hx + 3.9 / 2 + 0.07
    top = hy + 1.9
    nu, nv = 14, 10
    Wn, Hn = 1.5, 1.25
    bm = bmesh.new()
    grid = []
    for j in range(nv + 1):
        v = j / nv
        row = []
        for i in range(nu + 1):
            u = i / nu
            y = top - v * Hn * (1.0 - 0.18 * math.sin(math.pi * u)) - 0.1 * math.sin(math.pi * u)
            x = wall_x + 0.02 + 0.05 * v + 0.035 * math.sin(3 * math.pi * u + 2 * v) * v
            z = hz - 0.85 + (u - 0.5) * Wn * (1 - 0.15 * v)
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    bm.normal_update()
    if sum(f.normal.x for f in bm.faces) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    V, N, F, UV = bm_geo(bm, normals='smooth')
    bm.free()
    UV = [[(V[k].z * 2.5, V[k].y * 2.5) for k in f] for f in F]
    g.add((V, N, F, UV), S['Sand'], L['Cloth'], 0.55)
    back = ([v - Vector((0.008, 0, 0)) for v in V], [-n for n in N], [list(reversed(f)) for f in F], [list(reversed(u)) for u in UV])
    g.add(back, S['Sand'], L['Cloth'], 0.55)
    fr = rng('harbour_pier.net')
    for i in range(5):
        u = (i + 0.5) / 5
        y = top - 0.1 * math.sin(math.pi * u) + 0.02
        z = hz - 0.85 + (u - 0.5) * Wn
        g.add(xform(blob(0.055, fr, (1.0, 0.8, 1.4), subdiv=2, rough=0.02, flat=-2), (wall_x + 0.06, y, z)), S['Accent'], L['none'], 0.6)
    g.item('HutNet', (wall_x + 0.06, top - 0.6, hz - 0.85), (0.2, 1.3, 1.6), collider='none (on the hut wall)')

def build_dressing():
    g = Group('Harbour_Dressing')
    for name, c, kind in BUOYS:
        buoy(g, name, c, kind)
    for name, c, yaw, lift in POTS:
        lobster_pot(g, name, c, yaw, lift)
    for name, c in COILS:
        rope_coil(g, name, c)
    build_net(g)
    build_sign(g)
    # Life ring on the hut front wall (Accent with four Sand bands).
    hx, hy, hz = HUT_C
    lc = Vector((hx - 1.55, hy + 1.45, hz + 1.45 + 0.07))
    pts = [Vector((0.24 * math.cos(a), 0.24 * math.sin(a), 0)) for a in [2 * math.pi * i / 24 for i in range(24)]]
    V, N, F, UV = tube(pts, 0.06, 8, caps=False, closed=True)
    fsl = []
    for f in F:
        ang = math.atan2(sum(V[k].y for k in f), sum(V[k].x for k in f))
        fsl.append(((S['Sand'] if int(((ang + math.pi) / (math.pi / 4))) % 2 == 0 else S['Accent']), L['Cloth']))
    g.add(xform((V, N, F, UV), lc), 0, 0, 0.55, fsl)
    g.item('LifeRing', lc, (0.6, 0.6, 0.12), collider='none (on the hut wall)')
    # Mooring lines: rowboats to the deck edge, the skiff to a ring on the breakwater; rope from each coil to its bollard.
    lines = [((-1.52, DECK_TOP - 0.05, -35.0), Vector(BOATS[0][1]) + Vector((0.05, 0.12, 1.45)), 0.35),
             ((1.52, DECK_TOP - 0.05, -13.0), Vector(BOATS[1][1]) + Vector((-0.07, 0.12, 1.45)), 0.35),
             ((27.2, 1.15, -36.4), Vector(SKIFF[1]) + Vector((0.12, 0.2, 2.05)), 0.4)]
    for a, b, sag in lines:
        g.add(tube(rope_line(a, b, sag), 0.018, 6), S['Sand'], L['Cloth'], 0.55)
    for (name, c), (bx, bz) in zip(COILS, (BOLLARDS[0], BOLLARDS[1])):
        cc = Vector(c)
        a = cc + Vector((0.26 * (1 if bx > 0 else -1), 0.024, 0))
        b = Vector((bx, DECK_TOP + 0.2, bz))
        g.add(tube([a, a.lerp(b, 0.5) + Vector((0, 0.03, 0)), b], 0.02, 6), S['Sand'], L['Cloth'], 0.55)
    g.add(xform(tube([Vector((0.09 * math.cos(a), 0, 0.09 * math.sin(a))) for a in [2 * math.pi * i / 10 for i in range(10)]], 0.018, 6, caps=False, closed=True),
                (27.25, 1.17, -36.4)), S['Rock'], L['none'], 0.55)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Blender objects, AO bake, preview materials, export, manifest.
# ---------------------------------------------------------------------------------------------------------------------------------------
PALETTE_SUMMER = [(0.42, 0.66, 0.30), (0.30, 0.52, 0.24), (0.22, 0.42, 0.19), (0.66, 0.48, 0.30), (0.40, 0.27, 0.17), (0.88, 0.80, 0.58),
                  (0.55, 0.55, 0.53), (0.62, 0.68, 0.36), (0.58, 0.44, 0.30), (0.70, 0.30, 0.24), (0.95, 0.45, 0.15)]

def srgb_to_lin(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

def collection(name):
    col = bpy.data.collections.get(name)
    if not col:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col

def make_object(g):
    old = bpy.data.objects.get(g.name)
    if old:
        me_old = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        if me_old and me_old.users == 0:
            bpy.data.meshes.remove(me_old)
    verts = [tuple(M_U2B @ v) for v in g.V]
    faces = [tuple(reversed(f)) for f in g.F]
    me = bpy.data.meshes.new(g.name)
    me.from_pydata(verts, [], faces)
    me.update()
    uv0 = me.uv_layers.new(name='UVMap')
    uv1 = me.uv_layers.new(name='Layer')
    n_loops = len(me.loops)
    a0 = [0.0] * (2 * n_loops); a1 = [0.0] * (2 * n_loops); ln = [0.0] * (3 * n_loops)
    for pi, poly in enumerate(me.polygons):
        uvs = list(reversed(g.UV[pi]))
        slot, layer = g.SL[pi]
        for k, li in enumerate(poly.loop_indices):
            a0[2 * li], a0[2 * li + 1] = uvs[k]
            a1[2 * li], a1[2 * li + 1] = float(slot), float(layer)
    for li, lp in enumerate(me.loops):
        n = (M_U2B @ g.N[lp.vertex_index]).normalized()
        ln[3 * li:3 * li + 3] = n
    uv0.data.foreach_set('uv', a0)
    uv1.data.foreach_set('uv', a1)
    me.uv_layers.active = uv0
    uv0.active_render = True
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.normals_split_custom_set([tuple(ln[3 * i:3 * i + 3]) for i in range(n_loops)])
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    me.color_attributes.active_color = ca
    me.color_attributes.render_color_index = me.color_attributes.find('Col')
    ca.data.foreach_set('color', [0.5, 0.5, 0.5, 1.0] * n_loops)
    ob = bpy.data.objects.new(g.name, me)
    collection('Harbour').objects.link(ob)
    return ob

def terrain_proxy():
    # Beach + seabed height field (not exported): the AO occluder and the preview ground. Matches BeachY / SeabedY.
    name = 'FG_Preview_Terrain'
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    xs = [x * 2.0 for x in range(-40, 41)]
    zs = [z * 1.0 for z in range(-90, 41)]
    verts = [tuple(to_blender((x, terrain_y(z), z))) for z in zs for x in xs]
    nx = len(xs)
    faces = []
    for j in range(len(zs) - 1):
        for i in range(nx - 1):
            a = j * nx + i
            faces.append((a, a + nx, a + nx + 1, a + 1))
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me)
    collection('Preview').objects.link(ob)
    # make sure the faces point up
    if ob.data.polygons[0].normal.z < 0:
        me.flip_normals()
    return ob

def hemisphere_dirs(n):
    # Fixed cosine-weighted hemisphere directions about +y (Fibonacci spiral): the bake is deterministic.
    out = []
    for i in range(n):
        u = (i + 0.5) / n
        r = math.sqrt(u)
        phi = i * 2.399963229728653
        out.append(Vector((r * math.cos(phi), math.sqrt(max(0.0, 1.0 - u)), r * math.sin(phi))))
    return out

def bake_ao(groups, rays=40, dist=1.0):
    # Ambient occlusion baked per vertex in Blender with BVH ray casts (mathutils.bvhtree) over every harbour mesh plus the analytic
    # terrain (BeachY / SeabedY; the water is not an occluder). Rays that hit a BACK face pass through it: a vertex buried inside another
    # part (a post top inside a stringer, a rung end inside a rail) is not blacked out by the part it sits in, only by real cover.
    # Occlusion of a hit = 1 - 0.5 x (hit distance / dist). Returns one list of raw ao (0..1) per group, per vertex.
    from mathutils.bvhtree import BVHTree
    verts, polys = [], []
    for g in groups:
        base = len(verts)
        verts += [tuple(v) for v in g.V]
        polys += [tuple(base + k for k in f) for f in g.F]
    xs = [x * 2.0 for x in range(-40, 41)]
    zs = [z * 1.0 for z in range(-90, 41)]
    nx = len(xs)
    base = len(verts)
    verts += [(x, terrain_y(z), z) for z in zs for x in xs]
    for j in range(len(zs) - 1):
        for i in range(nx - 1):
            a = base + j * nx + i
            polys.append((a, a + nx, a + nx + 1, a + 1))   # normal +y
    bvh = BVHTree.FromPolygons(verts, polys, all_triangles=False)
    dirs = hemisphere_dirs(rays)
    out = []
    for g in groups:
        ao = []
        for p, n in zip(g.V, g.N):
            t = n.orthogonal().normalized()
            b = n.cross(t)
            org = p + n * 0.004
            occ = 0.0
            for d0 in dirs:
                d = t * d0.x + n * d0.y + b * d0.z
                o, left = org, dist
                for _ in range(6):
                    loc, nrm, idx, dd = bvh.ray_cast(o, d, left)
                    if loc is None:
                        break
                    if nrm.dot(d) > 0.0:
                        o = loc + d * 0.002
                        left -= dd + 0.002
                        if left <= 0.0:
                            break
                        continue
                    occ += 1.0 - 0.5 * ((dist - left + dd) / dist)
                    break
            ao.append(max(0.0, 1.0 - occ / len(dirs)))
        out.append(ao)
    return out

def write_ao(ob, g, raw):
    # Vertex colour = 0.5 x ao (half-scale grey, BF-7), ao = floor + (1 - floor) x raw, the floor per part (wood 0.45, rock 0.62, ...).
    me = ob.data
    ca = me.color_attributes['Col']
    n = len(me.loops)
    out = [0.0] * (4 * n)
    for pi, poly in enumerate(me.polygons):
        lo = g.AO[pi]
        for li in poly.loop_indices:
            a = raw[me.loops[li].vertex_index]
            ao = lo + (1.0 - lo) * max(0.0, min(1.0, a))
            out[4 * li:4 * li + 4] = (0.5 * ao, 0.5 * ao, 0.5 * ao, 1.0)
    ca.data.foreach_set('color', out)

def preview_material():
    m = bpy.data.materials.get('FG_Preview_Palette')
    if m:
        return m
    m = bpy.data.materials.new('FG_Preview_Palette')
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = 0.85
    uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = 'Layer'
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    mth = nt.nodes.new('ShaderNodeMath'); mth.operation = 'MULTIPLY_ADD'
    mth.inputs[1].default_value = 1.0 / 16.0; mth.inputs[2].default_value = 0.5 / 16.0
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    while len(els) < len(PALETTE_SUMMER):
        els.new(0.5)
    for i, c in enumerate(PALETTE_SUMMER):
        els[i].position = i / 16.0
        els[i].color = srgb_to_lin(c) + (1.0,)
    col = nt.nodes.new('ShaderNodeVertexColor'); col.layer_name = 'Col'
    mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    two = nt.nodes.new('ShaderNodeVectorMath'); two.operation = 'SCALE'; two.inputs['Scale'].default_value = 2.0
    nt.links.new(uvn.outputs['UV'], sep.inputs[0])
    nt.links.new(sep.outputs['X'], mth.inputs[0])
    nt.links.new(mth.outputs[0], ramp.inputs['Fac'])
    nt.links.new(col.outputs['Color'], two.inputs[0])
    nt.links.new(ramp.outputs['Color'], mul.inputs['A'])
    nt.links.new(two.outputs['Vector'], mul.inputs['B'])
    nt.links.new(mul.outputs['Result'], bsdf.inputs['Base Color'])
    return m

def preview_scene():
    sc = bpy.context.scene
    terr = bpy.data.objects.get('FG_Preview_Terrain') or terrain_proxy()
    tm = bpy.data.materials.get('FG_Preview_Ground')
    if not tm:
        tm = bpy.data.materials.new('FG_Preview_Ground'); tm.use_nodes = True
        nt = tm.node_tree
        bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); bsdf.inputs['Roughness'].default_value = 0.95
        geo = nt.nodes.new('ShaderNodeNewGeometry'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
        mr = nt.nodes.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 0.55; mr.inputs['From Max'].default_value = 0.75
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
        mix.inputs['A'].default_value = srgb_to_lin(PALETTE_SUMMER[5]) + (1,)
        mix.inputs['B'].default_value = srgb_to_lin(PALETTE_SUMMER[0]) + (1,)
        nt.links.new(geo.outputs['Position'], sep.inputs[0]); nt.links.new(sep.outputs['Z'], mr.inputs['Value'])
        nt.links.new(mr.outputs['Result'], mix.inputs['Factor']); nt.links.new(mix.outputs['Result'], bsdf.inputs['Base Color'])
    if not terr.data.materials:
        terr.data.materials.append(tm)
    water = bpy.data.objects.get('FG_Preview_Water')
    if not water:
        me = bpy.data.meshes.new('FG_Preview_Water')
        me.from_pydata([(-90, -60, 0), (90, -60, 0), (90, 130, 0), (-90, 130, 0)], [], [(0, 1, 2, 3)])
        water = bpy.data.objects.new('FG_Preview_Water', me)
        collection('Preview').objects.link(water)
        wm = bpy.data.materials.new('FG_Preview_Water'); wm.use_nodes = True
        bsdf = next(n for n in wm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        bsdf.inputs['Base Color'].default_value = srgb_to_lin((0.22, 0.58, 0.62)) + (1,)
        bsdf.inputs['Roughness'].default_value = 0.15
        bsdf.inputs['Alpha'].default_value = 0.72
        try:
            wm.surface_render_method = 'BLENDED'
        except Exception:
            pass
        me.materials.append(wm)
    sun = bpy.data.objects.get('FG_Preview_Sun')
    if not sun:
        ld = bpy.data.lights.new('FG_Preview_Sun', 'SUN'); ld.energy = 3.2; ld.angle = math.radians(8)
        sun = bpy.data.objects.new('FG_Preview_Sun', ld); collection('Preview').objects.link(sun)
        sun.rotation_euler = (math.radians(42), math.radians(8), math.radians(205))
    if not sc.world:
        sc.world = bpy.data.worlds.new('World')
    sc.world.use_nodes = True
    bg = next(n for n in sc.world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = srgb_to_lin((0.62, 0.78, 0.92)) + (1,)
    bg.inputs['Strength'].default_value = 0.9
    cam = bpy.data.objects.get('FG_Preview_Cam')
    if not cam:
        cd = bpy.data.cameras.new('FG_Preview_Cam'); cd.clip_end = 600
        cam = bpy.data.objects.new('FG_Preview_Cam', cd); collection('Preview').objects.link(cam)
    sc.camera = cam
    try:
        sc.view_settings.view_transform = 'AgX'
    except Exception:
        pass
    return cam

def render_view(path, eye_u, target_u, lens=30, res=(1280, 720), levels=48):
    sc = bpy.context.scene
    cam = preview_scene()
    eye, tgt = to_blender(eye_u), to_blender(target_u)
    cam.location = eye
    cam.rotation_euler = (tgt - eye).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    try:
        sc.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGB'
    sc.render.image_settings.color_depth = '8'
    sc.render.image_settings.compression = 100
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    if levels:
        shrink_png(path, levels)

def shrink_png(path, levels=48):
    # Posterise to `levels` steps per channel so the PNG compresses like a JPG (preview images only).
    import numpy as np
    img = bpy.data.images.load(path, check_existing=False)
    px = np.array(img.pixels[:], dtype=np.float32)
    px = np.round(px * (levels - 1)) / (levels - 1)
    img.pixels.foreach_set(px)
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)

BUILDERS = [('PierDeck', build_pier_deck), ('PierHead', build_pier_head), ('PierPosts', build_posts), ('Ladders', build_ladders),
            ('Breakwater', build_breakwater), ('Rocks', build_rocks), ('Props', build_props), ('Boats', build_boats),
            ('Dressing', build_dressing)]

def build_groups(only=None):
    out = []
    for key, fn in BUILDERS:
        if only and key not in only:
            continue
        res = fn()
        out += list(res) if isinstance(res, tuple) else [res]
    return out

def u_bounds(g):
    lo = [min(v[i] for v in g.V) for i in range(3)]
    hi = [max(v[i] for v in g.V) for i in range(3)]
    return [round(x, 4) for x in lo], [round(x, 4) for x in hi]

def export_fbx(ob, path):
    me = ob.data
    keep = list(me.materials)
    me.materials.clear()
    bpy.context.view_layer.update()   # a fresh file's view layer lists new objects only after an update
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False)
    for m in keep:
        me.materials.append(m)

def store_text_block():
    if os.path.exists(SCRIPT_PATH):
        t = bpy.data.texts.get('fg_harbour.py') or bpy.data.texts.new('fg_harbour.py')
        with open(SCRIPT_PATH, 'r', encoding='utf-8') as fh:
            t.from_string(fh.read())

def show(groups, bake=False):
    # Build Blender objects for preview (progress shots); no export.
    objs = [make_object(g) for g in groups]
    if bake:
        raws = bake_ao(groups)
        for ob, g, raw in zip(objs, groups, raws):
            write_ao(ob, g, raw)
    pm = preview_material()
    for ob in objs:
        if not ob.data.materials:
            ob.data.materials.append(pm)
    preview_scene()
    return objs

def main(export=True, bake=True):
    for o in list(collection('Harbour').objects):
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
    groups = build_groups()
    report = []
    for g in groups:
        t = g.tris()
        if t > MAX_TRIS:
            raise RuntimeError('%s has %d triangles (limit %d)' % (g.name, t, MAX_TRIS))
    objs = show(groups, bake=bake)
    os.makedirs(EXPORT_DIR, exist_ok=True)
    manifest = dict(
        generator='Art/Blender/fg_harbour.py (text block in Art/Blender/fishgame_harbour.blend)',
        layout='mirrors HarbourPierLayout / plan T17.2 (2026-10-13-m3-plan-maps.md)',
        axes='Unity metres. Authored in Unity axes; Blender = (-x, -z, y); FBX export -Z forward, Y up, bake_space_transform (the fish settings). '
             'Each FBX object is at the origin with an identity transform: place each at the Unity origin.',
        encoding=dict(uv0='UVMap: wood in metres, grain along u (long axis; posts and lathes grain up); _Tri objects: world xz',
                      uv1='Layer: (palette slot, texture layer) on every corner; no uv1.z in the FBX (the mapping comes from the material: '
                          '_Tri -> M_Toon_Tri, else M_Toon_Base)',
                      colour='Col: 0.5 x ao (half-scale grey, _JitterEncoded 1); ao = per-vertex BVH ray-cast bake in Blender (40 rays, 1 m, terrain occluder, back faces ignored) above a per-part floor (wood 0.45, rock 0.62)',
                      normals='smooth / analytic rounded-box custom normals', materials='none exported'),
        slots=S, layers=L, assets_used=[],
        items_note='Per FBX, items lists every placed piece: position and footprint are the world AABB (Unity axes, yaw included) of its geometry; '
                   'sphere items (rocks, boulders) give the sphere centre and radius (the mesh is inside it) plus bounds_min/max. Place rock and '
                   'boulder colliders from these numbers; never re-draw the rocks in C# (Python random.Random is not Pcg32).',
        notes=['uv1.z (mapping) is not in the FBX: RenderValidators.ToonMeshMappingMatchesMaterial expects uv1.z == 1 on M_Toon_Tri, so the '
               'builder writes uv1.z = 1 into the _Tri mesh copies or the validator exempts them.',
               'Layout deviations: west-front T-head bollard at (-3.9, -5.4) to clear the lamp at (-4.8, -5.3); deck stringers at x +-1.38 '
               '(under the plank ends, resting on the posts); ladder grab rails rise to y 2.35 (inside the rail boxes); post foot flares reach '
               '2 cm below TerrainY; the hut roof, ridge roll and stove pipe exceed the 4 x 3 x 3 m box (see the Hut item bounds); lamps top out at y 4.53.',
               'Breakwater boulders overhang the x 28..32 collider box by up to ~1.9 m at the waterline and the rubble base reaches x ~25 / ~35 '
               '(top y ~ -0.48): add per-boulder sphere colliders from the items or widen the box at water level, or swimmers pass into rock.'],
        fbx=[])
    total = 0
    for ob, g in zip(objs, groups):
        path = os.path.join(EXPORT_DIR, g.name + '.fbx')
        if export:
            export_fbx(ob, path)
        lo, hi = u_bounds(g)
        t = g.tris(); total += t
        layers_used = sorted(set(l for _, l in g.SL)); slots_used = sorted(set(s for s, _ in g.SL))
        manifest['fbx'].append(dict(name=g.name, file='Art/Export/Harbour/' + g.name + '.fbx', triangles=t, vertices=len(g.V),
                                    bounds_min=lo, bounds_max=hi, triplanar=g.tri, material='M_Toon_Tri' if g.tri else 'M_Toon_Base',
                                    slots=slots_used, layers=layers_used, items=g.items))
        report.append('%s tris=%d verts=%d' % (g.name, t, len(g.V)))
    manifest['total_triangles'] = total
    with open(MANIFEST_PATH, 'w', encoding='utf-8') as fh:
        json.dump(manifest, fh, indent=1)
    store_text_block()
    report.append('TOTAL %d' % total)
    print('\n'.join(report))
    return objs, groups

if __name__ == '__main__':
    main()
