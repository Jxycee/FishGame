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
#
# Pass 2 (2026-10-14, the detail pass) is appended below the phase-1 code: see the PASS 2 banner. main() runs phase 1 exactly as before,
# then pass2_main(). Phase-1 output stays byte-identical (its own AO occluders; export_if_changed keeps unchanged FBX files).
import bpy, bmesh, math, json, os, zlib, random
from mathutils import Vector, Matrix, noise

# Repo root: FG_ROOT, else two levels above this file (when run with --python), else the repo above the working directory (a script exec'd in
# Blender has no __file__), else the main checkout. Never a worktree path: the generator must work wherever the branch is checked out.
def _repo_root():
    if os.environ.get('FG_ROOT'):
        return os.environ['FG_ROOT']
    if '__file__' in globals():
        return os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    d = os.getcwd()
    while True:
        if os.path.exists(os.path.join(d, 'Art', 'Blender', 'fg_harbour.py')):
            return d
        if os.path.dirname(d) == d:
            return r'C:\Dev\FishGame'
        d = os.path.dirname(d)

ROOT = _repo_root()
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
STAND_POINTS_P1 = [(-48, 0.23, -46.5, 0), (-30, 0.23, -46.5, 0), (-12, 0.23, -46.5, 0), (14, 0.23, -46.5, 0), (48, 0.23, -46.5, 0),
                   (0, 1.55, -25, 90), (0, 1.55, -25, 270), (-4.5, 1.55, -6.5, 270), (4.5, 1.55, -6.5, 90), (0, 1.55, -5.5, 0),
                   (30, 1.55, -26, 0)]       # phase 1 (the rock draw reads this list: it must not change)
# Round 2b (2026-10-16, user unlock "tug pulled off the wall"): the breakwater-tip stand moves 1.5 m west and 0.5 m back and faces 330 (out
# over the open sea, north-north-west), so the tug's gangway can land at z -26 beside it (HarbourPierLayout.StandPoints mirrors this).
STAND_POINTS = [(-48.5, 0.75, -41.2, 20)] + STAND_POINTS_P1[1:10] + [(28.5, 1.55, -26.5, 330)]   # round 3: stand 0 on the lagoon dock
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
    if r > 0.0:      # round 3: r 0 = a plain prism (cheap fence pickets)
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
        if any(math.hypot(x - p[0], z - p[2]) < 3 for p in STAND_POINTS_P1):
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
                  (0.55, 0.55, 0.53), (0.62, 0.68, 0.36), (0.58, 0.44, 0.30), (0.70, 0.30, 0.24), (0.95, 0.45, 0.15),
                  (0.95, 0.96, 0.98), (0.24, 0.46, 0.28), (0.13, 0.30, 0.20), (0.30, 0.52, 0.24), (0.36, 0.56, 0.22)]   # round 2: slots 11-15 (preview only)

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
    vs = getattr(g, 'VS', None)        # round 2 (pass-2 groups only): a per-vertex palette slot (the slot blends across a face; the layer never does)
    for pi, poly in enumerate(me.polygons):
        uvs = list(reversed(g.UV[pi]))
        slot, layer = g.SL[pi]
        for k, li in enumerate(poly.loop_indices):
            a0[2 * li], a0[2 * li + 1] = uvs[k]
            sv = vs[me.loops[li].vertex_index] if vs is not None else None
            a1[2 * li], a1[2 * li + 1] = float(slot if sv is None else sv), float(layer)
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
    # Over the vertices that faces use: a part whose faces all collapsed (G2.add drops zero-area faces) leaves loose vertices in the FBX, and
    # Unity's import drops unused vertices, so the manifest bounds must not count them (round 0: Yard_B's pot-stack base).
    used = sorted(set(k for f in g.F for k in f)) if g.F else range(len(g.V))
    lo = [min(g.V[k][i] for k in used) for i in range(3)]
    hi = [max(g.V[k][i] for k in used) for i in range(3)]
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
            export_if_changed(ob, path)    # pass 2: an unchanged model keeps its committed bytes (see fbx_equivalent)
        lo, hi = u_bounds(g)
        t = g.tris(); total += t
        layers_used = sorted(set(l for _, l in g.SL)); slots_used = sorted(set(s for s, _ in g.SL))
        manifest['fbx'].append(dict(name=g.name, file='Art/Export/Harbour/' + g.name + '.fbx', triangles=t, vertices=len(g.V),
                                    bounds_min=lo, bounds_max=hi, triplanar=g.tri, material='M_Toon_Tri' if g.tri else 'M_Toon_Base',
                                    slots=slots_used, layers=layers_used, items=g.items))
        report.append('%s tris=%d verts=%d' % (g.name, t, len(g.V)))
    manifest['total_triangles'] = total
    pass2_main(manifest, groups, export=export, bake=bake)     # pass 2 (detail pass): appends its FBX entries and manifest['pass2']
    with open(MANIFEST_PATH, 'w', encoding='utf-8') as fh:
        json.dump(manifest, fh, indent=1)
    store_text_block()
    report.append('TOTAL %d' % total)
    print('\n'.join(report))
    return objs, groups

# =======================================================================================================================================
# PASS 2 (2026-10-14, branch harbour-art2): the detail pass. Land shape, harbour life and clutter, shoreline and plants, backdrop, and the
# crew's getaway boat (spec R76: the harbour is the river-mouth stop on the crew's run downriver from the mafia).
#
# Everything below is ADDITIVE. The phase-1 builders above are untouched and still produce byte-identical FBX: phase 1 is baked with its own
# occluders only (bake_ao(phase-1 groups) exactly as before) and an FBX is only rewritten when its content changed (export_if_changed: the
# Blender FBX writer stamps the time into the header and derives object UIDs from Python's per-process hash(), so a byte compare of two
# exports of the same mesh always differs; the structural compare ignores exactly those two things).
#
# Groups. Every pass-2 FBX belongs to one of three groups:
#   core   - land shape, buildings, solid props (everything with a collider), landmarks, backdrop. Always drawn.
#   detail - collider-free clutter, plants, gulls, string lights, flags. Unity hides these on the Low preset (GTX 900-class), so Low changes
#            nothing in gameplay: no Detail mesh carries a collider.
#   col    - simplified walkable collision meshes (*_Col): no uv, no AO, never drawn.
# Names: Harbour_Core_<Name>[_Tri].fbx, Harbour_Detail_<Name>[_Tri].fbx, Harbour_<Name>_Col.fbx ("_Tri" stays LAST: the Unity postprocessor
# keys on it). Local models (the crew boat, gulls, buoys, flags, the swinging sign ...) are authored around their own pivot at the origin and
# the manifest lists their placements (Unity position + yaw) and an animHint for the Unity side (sway, bob, spin, gull, swing).
# Seeds: every pass-2 draw uses its own stream "harbour_pier.p2.<job>" (rng2), so phase-1 streams never shift.
# =======================================================================================================================================
P2_STREAM = 'harbour_pier.p2.'

def rng2(job):
    return rng(P2_STREAM + job)

def smoothstep(e0, e1, x):
    if e0 == e1:
        return 0.0 if x < e0 else 1.0
    t = clamp((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

def lerp(a, b, t):
    return a + (b - a) * t

def n2(x, z, f, seed=0.0):
    # Perlin noise in [-1, 1] at a world xz (position only: deterministic).
    return noise.noise(Vector((x * f + 17.31 * seed, z * f - 9.17 * seed, 3.7 + seed)))

def seg_dist(px, pz, ax, az, bx, bz):
    vx, vz = bx - ax, bz - az
    L2 = vx * vx + vz * vz
    t = 0.0 if L2 < 1e-12 else clamp(((px - ax) * vx + (pz - az) * vz) / L2, 0.0, 1.0)
    dx, dz = px - (ax + vx * t), pz - (az + vz * t)
    return math.sqrt(dx * dx + dz * dz), t

def poly_dist(px, pz, pts):
    best = 1e9
    for i in range(len(pts) - 1):
        d, _ = seg_dist(px, pz, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1])
        best = min(best, d)
    return best

def point_in_poly(px, pz, poly):
    inside = False
    n = len(poly)
    for i in range(n):
        ax, az = poly[i]; bx, bz = poly[(i + 1) % n]
        if (az > pz) != (bz > pz):
            x = ax + (pz - az) * (bx - ax) / (bz - az)
            if px < x:
                inside = not inside
    return inside

def poly_signed(px, pz, poly):
    # signed distance to a closed polygon (negative inside)
    d = poly_dist(px, pz, list(poly) + [poly[0]])
    return -d if point_in_poly(px, pz, poly) else d

def rect_dist(px, pz, cx, cz, hx, hz):
    dx = max(abs(px - cx) - hx, 0.0); dz = max(abs(pz - cz) - hz, 0.0)
    return math.hypot(dx, dz)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Pass-2 layout (Unity metres). The play area is inside the walls x -60.5..60.5, z -75.5..60.5. Hard keep-outs: the beach strip z -45..-55
# (BeachY), the coast at z -45, the seabed and water, the pier/T-head/ladders/breakwater walk, the 11 stand points + 3 m, the cast lanes
# (20 m, +-25 deg along each facing), the pier walk line x -0.6..0.6, the spawn line, the bell. Land shaping only behind z -60.
# ---------------------------------------------------------------------------------------------------------------------------------------
WALL_X, WALL_ZS, WALL_ZN = 60.5, -106.5, 60.5     # round 1 (2026-10-15): the south wall moved from -75.5 to -106.5 (two terraces)
PLATEAU_Y = 1.2
# ---- Round 1 (2026-10-15): the town climbs two terraces behind the harbour (map layout pass) ----
# Level 0 = the plateau (y 1.2) and its dunes; dry-stone retaining wall 1 at z -75.6 lifts level 1 (the upper lane, the coast road, a row of
# cottages) 2.6 m; wall 2 at z -95.0 lifts level 2 (the chapel and its bell tower, the overlook) another 3.0 m. Stone stairs cut into the
# walls (the collider is the smooth ramp in Harbour_Dunes_Col, <= 23.2 deg; the visual steps sit on it). Behind the south wall the hills
# rise in soft farm terraces (a patchwork of fields with hedge banks), a forest belt on the ridge, the windmill and the farm.
T1_Z, T1_H = -75.6, 2.6
T2_Z, T2_H = -95.0, 5.6
RISER_HALF = 0.25
RAMP_HW, RAMP_CHEEK = 1.5, 0.45
RAMPS = [('A', 4.4, 1, 6.5), ('B', -47.0, 1, 6.5), ('D', 33.0, 1, 6.5), ('C', -12.0, 2, 7.0), ('E', 26.0, 2, 7.0)]   # name, x, wall, run
HILL_STEP = 4.0
SHAPE_Z = -60.0                      # dunes start behind this line
# The 16 code trees (HarbourPierBuilder.PlaceTrees: Pcg32 stream harbour_pier.trees, seed 0xF15ED0C5; ported to Python once and copied here):
# (x, z, height scale, pine). Pass-2 buildings and solid props keep >= 2.5 m from each trunk and the dunes are flattened around them.
CODE_TREES = [(54.49, -66.68, 1.19, True), (16.7, -61.25, 1.07, False), (-45.04, -66.85, 0.94, False), (48.06, -68.4, 1.15, True),
              (37.66, -67.95, 1.0, False), (-51.52, -60.92, 0.89, False), (-17.56, -67.4, 1.19, False), (40.75, -72.96, 1.2, True),
              (-53.19, -65.65, 1.09, True), (-24.12, -65.5, 1.24, True), (-30.21, -70.56, 1.27, True), (-28.93, -72.3, 1.26, False),
              (-25.81, -72.72, 0.94, False), (-49.07, -63.21, 1.21, False), (-48.58, -60.43, 0.94, True), (-40.35, -62.04, 0.83, True)]
BEACH_ROCKS = [(-24.275, -51.925, 0.657), (-55.688, -49.11, 0.657), (-34.294, -53.902, 0.439), (49.836, -50.716, 0.426),
               (14.537, -49.961, 0.318), (-31.849, -52.952, 0.731), (-25.087, -53.275, 0.694), (-46.307, -52.653, 0.414),
               (15.215, -52.084, 0.759), (56.451, -48.047, 0.703), (34.112, -47.353, 0.337), (39.153, -52.067, 0.532)]

# Buildings (centre x, z, width along x, depth along z, front faces +z = the sea). Ground is the plateau (1.2): the dunes are flattened here.
BAIT_SHOP = (-18.5, -63.0, 5.0, 3.6)
NET_SHED = (-30.5, -62.9, 4.4, 3.0)
SMOKEHOUSE = (-37.5, -66.8, 2.6, 2.6)
DOCK_OFFICE = (10.5, -63.3, 3.4, 2.8)
HUT_RECT = (-9.0, -63.0, 2.2, 1.7)   # phase-1 hut half sizes (for keep-outs)
# Paths (sand ribbons): polylines of (x, z), half width.
PATHS = [
    ([(0.0, -60.4), (-2.6, -61.3), (-6.0, -61.6), (-9.6, -61.75), (-13.0, -61.5), (-18.0, -60.85), (-23.0, -61.0), (-27.5, -61.2),
      (-31.0, -61.0), (-34.2, -62.3), (-36.4, -64.8)], 0.95),                                                       # west: hut, bait shop, net shed, smokehouse
    ([(0.0, -60.4), (3.0, -61.0), (7.0, -61.4), (10.5, -61.5), (13.5, -62.6), (17.0, -63.6), (21.5, -63.2), (25.5, -61.2),
      (28.6, -58.6), (30.0, -56.8)], 0.95),                                                                            # east: dock office, boatyard, breakwater root
    ([(3.0, -61.0), (3.4, -64.5), (3.2, -68.0), (4.0, -71.5), (4.4, -75.5), (4.6, -80.0), (5.2, -84.0)], 0.85),      # south: through the wall gate to the coast road
]
STONE_WALL_Z = T1_Z + RISER_HALF + 0.06   # round 1: the face of retaining wall 1 (round 0's low field wall and its gate are gone)
# The crew's getaway boat (R76): moored along the pier's EAST side near the root, bow toward the land. (The west side at z -42..-34 is taken
# by phase-1 Rowboat_West at z -37.8..-34.6, and nearer the coast the water is too shallow for a 0.45 m draft.) Out of every cast lane.
CREW_BOAT_POS, CREW_BOAT_YAW = (3.18, 0.0, -38.8), 180.0

STAND_R = 3.0
LANE_LEN, LANE_HALF_DEG = 20.0, 25.0

def lane_hit(x, z, margin=0.0):
    # True when (x, z) is inside a stand point's 3 m circle or its cast lane wedge (20 m, +-25 deg about its facing), grown by margin.
    for sx, sy, sz, f in STAND_POINTS + STAND_POINTS_R1:
        dx, dz = x - sx, z - sz
        d = math.hypot(dx, dz)
        if d < STAND_R + margin:
            return True
        if d > LANE_LEN + margin:
            continue
        fx, fz = math.sin(math.radians(f)), math.cos(math.radians(f))
        along = dx * fx + dz * fz
        if along <= 0:
            continue
        side = abs(-dx * fz + dz * fx)
        if side <= along * math.tan(math.radians(LANE_HALF_DEG)) + margin:
            return True
    return False

def pier_hit(x, z, margin=0.0):
    if -1.6 - margin <= x <= 1.6 + margin and DECK_Z0 - 4.0 - margin <= z <= DECK_Z1 + margin:
        return True
    if THEAD_X0 - margin <= x <= THEAD_X1 + margin and THEAD_Z0 - margin <= z <= THEAD_Z1 + margin:
        return True
    bx0, bx1, bz0, bz1 = BREAKWATER
    return bx0 - 2.5 - margin <= x <= bx1 + 2.5 + margin and bz0 - margin <= z <= bz1 + 2.5 + margin

def tree_clear(x, z, r):
    return all(math.hypot(x - tx, z - tz) >= r for tx, tz, _, _ in CODE_TREES)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Pass-2 Group: kind (core/detail/col), per-face tint (a darkening factor multiplied into the AO, for the black car and the mafia boat),
# local models with placements, animHint, AO settings, material hint for the Unity builder.
# ---------------------------------------------------------------------------------------------------------------------------------------
class G2(Group):
    def __init__(self, name, kind, tri=False, local=False, rays=24, ao_dist=1.0, anim=None, material=None, note=None):
        Group.__init__(self, name, tri)
        self.kind, self.local, self.rays, self.ao_dist, self.anim = kind, local, rays, ao_dist, anim
        self.material, self.note = material, note
        self.TINT = []
        self.VS = None
        self.VAO = None        # round 2b: painted per-vertex ao for rays == 0 groups (the crown cards)
        self.placements = []
        self.extra = {}

    def add(self, geo, slot, layer, ao=AO_PROP, fsl=None, tint=1.0, vsl=None):
        # vsl (round 2): an optional palette slot per vertex of geo (None = the face's slot); the slot then blends softly across faces.
        # Pass-2 parts are oriented here: a face whose geometric normal disagrees with its vertex normals is reversed (with its corner uvs),
        # so every pass-2 face is front-facing in Unity whatever order the part builder emitted it in.
        V, N, F, UV = geo
        F2, UV2 = [], []
        # drop repeated corners (a blade or lathe tip that closes to a point) and faces left with fewer than 3 distinct corners, so the
        # FBX carries no zero-area triangles (Unity's import would drop them and the manifest count would not match)
        Fc, UVc, kept = [], [], []
        for fi, (f, uv) in enumerate(zip(F, UV)):
            keep, kuv, seen = [], [], []
            for k, t in zip(f, uv):
                p = Vector(V[k])
                if any((p - q).length < 1e-7 for q in seen):
                    continue
                seen.append(p); keep.append(k); kuv.append(t)
            if len(keep) >= 3:
                Fc.append(keep); UVc.append(kuv); kept.append(fi)
        F, UV = Fc, UVc
        if fsl:
            fsl = [fsl[i] for i in kept]
        for f, uv in zip(F, UV):
            a, b, c = Vector(V[f[0]]), Vector(V[f[1]]), Vector(V[f[2]])
            gn = (b - a).cross(c - a)
            if len(f) > 3 and gn.length < 1e-12:
                gn = (c - a).cross(Vector(V[f[3]]) - a)
            avg = Vector()
            for k in f:
                avg += Vector(N[k])
            if gn.dot(avg) < 0:
                F2.append(tuple(reversed(f))); UV2.append(list(reversed(uv)))
            else:
                F2.append(tuple(f)); UV2.append(list(uv))
        geo = (V, N, F2, UV2)
        n0 = len(self.F)
        v0 = len(self.V)
        Group.add(self, geo, slot, layer, ao, fsl)
        self.TINT += [tint] * (len(self.F) - n0)
        if vsl is not None or self.VS is not None:
            if self.VS is None:
                self.VS = [None] * v0
            self.VS += list(vsl) if vsl is not None else [None] * (len(self.V) - v0)

    def mark(self):
        self._mark = len(self.V)

    def solid(self, name, collider='box', **extra):
        # Item from the geometry added since the last mark(): box = the world AABB; sphere = AABB centre + radius of the bounding sphere.
        vs = self.V[self._mark:]
        lo = [min(v[i] for v in vs) for i in range(3)]
        hi = [max(v[i] for v in vs) for i in range(3)]
        c = [(lo[i] + hi[i]) / 2 for i in range(3)]
        d = dict(name=name, collider=collider, position=[round(x, 3) for x in c], footprint=[round(hi[i] - lo[i], 3) for i in range(3)],
                 bounds_min=[round(x, 3) for x in lo], bounds_max=[round(x, 3) for x in hi])
        if collider == 'sphere':
            d['radius'] = round(max((Vector(v) - Vector(c)).length for v in vs), 3)
        d.update(extra)
        self.items.append(d)
        self._mark = len(self.V)
        return d

    def place(self, name, pos, yaw=0.0, anim=None, **extra):
        d = dict(name=name, position=[round(p, 3) for p in pos], yaw_deg=round(yaw, 2))
        if anim or self.anim:
            d['animHint'] = anim or self.anim
        d.update(extra)
        self.placements.append(d)
        return d

def ry_u(deg):
    # Unity yaw (deg, clockwise seen from above, 0 = +z) as a rotation of Unity-space vectors: x' = x cos + z sin, z' = -x sin + z cos.
    return ry(deg)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Heightfields
# ---------------------------------------------------------------------------------------------------------------------------------------
def hf_geo(xs, zs, yfn, keep=None, eps=0.25):
    # Grid surface in Unity axes, quads (a, a+nx, a+nx+1, a+1) face +y; smooth normals from central differences of yfn; uv0 = world xz.
    # keep(i, j) -> bool selects the quads to emit (unused vertices are dropped).
    nx = len(xs)
    Y = [[yfn(x, z) for x in xs] for z in zs]
    used = {}
    V, N, F, UV = [], [], [], []
    def vid(i, j):
        k = (i, j)
        if k not in used:
            x, z = xs[i], zs[j]
            used[k] = len(V)
            V.append(Vector((x, Y[j][i], z)))
            gx = (yfn(x + eps, z) - yfn(x - eps, z)) / (2 * eps)
            gz = (yfn(x, z + eps) - yfn(x, z - eps)) / (2 * eps)
            N.append(Vector((-gx, 1.0, -gz)).normalized())
        return used[k]
    for j in range(len(zs) - 1):
        for i in range(nx - 1):
            if keep and not keep(i, j):
                continue
            q = [vid(i, j), vid(i, j + 1), vid(i + 1, j + 1), vid(i + 1, j)]
            F.append(q)
            UV.append([(V[k].x, V[k].z) for k in q])
    return V, N, F, UV, Y

def frange(a, b, step):
    n = int(round((b - a) / step))
    return [a + (b - a) * i / n for i in range(n + 1)]

# ---------------------------------------------------------------------------------------------------------------------------------------
# Land shape: one height field for the dunes (inside the walls, behind z -60) and the hills/headlands/river valley outside the walls.
# land_h(x, z) = metres above the plateau (1.2); the surface is drawn at DUNE_BASE + h, so where h is 0 it sits 6 cm UNDER the code plateau
# (hidden, no z-fight) and the dunes rise out of the plateau along a soft contour. Inside the walls the dunes are flattened around the
# buildings, paths, yards and the 16 code trees (so their trunks stand on 1.2 as the code places them).
# ---------------------------------------------------------------------------------------------------------------------------------------
DUNE_BASE = PLATEAU_Y - 0.06
WEST_HEAD = [(-92.0, -80.0), (-92.5, -56.0), (-94.0, -34.0), (-97.0, -10.0), (-100.0, 10.0), (-98.5, 26.0), (-104.0, 36.0),
             (-118.0, 37.0), (-136.0, 24.0), (-152.0, -20.0), (-160.0, -80.0)]
EAST_HEAD = [(63.6, -80.0), (63.6, -54.0), (66.0, -32.0), (70.5, -8.0), (75.5, 15.0), (80.0, 34.0), (87.0, 46.0), (99.0, 50.0),
             (122.0, 43.0), (150.0, 10.0), (165.0, -80.0)]
HEAD_TOP = {'W': 11.5, 'E': 10.5}
# Round 4 (2026-10-18, critics round 3: "the cliff top is a ruler-straight edge, one flat banded wall"; "the creek just stops at the cliff
# top"): the headland tops rise and fall along the coast (head_var, +-3.5 m over ~50 m, shared by the hills and the swept cliff crest so they
# meet), and a brook comes off the West Head's top down a shallow valley to a saddle where it pours 8 m into the sea from a wet alcove
# cut into the face - the west end's landmark, seen from the lagoon, the horn, the pier and the overview.
FALL_EDGE = (-95.5, -22.0)        # the point on the West Head's edge polyline where the brook goes over
FALL_Y = 8.4                      # the lip (Unity y)
BROOK = [(-100.6, -22.6), (-105.5, -28.5), (-111.0, -37.5), (-116.5, -49.0), (-121.0, -61.0)]   # lip -> upstream
BROOK_RISE = 3.2
FALL_INFO = {}                    # filled by cliff_sweep: the fall row's edge point, inward normal and wander

def brook_floor(t):
    return FALL_Y + BROOK_RISE * t ** 1.4

def head_var(key, x, z):
    v = 3.4 * n2(x, z, 0.03, 150 if key == 'W' else 151) + 1.2 * n2(x, z, 0.075, 152)
    if key == 'W':
        dd = math.hypot(x - FALL_EDGE[0], z - FALL_EDGE[1])
        v = lerp(v, FALL_Y - HEAD_TOP['W'], math.exp(-(dd / 6.5) ** 2))     # the saddle the brook found
    return v

def brook_cut(x, z, top_h):
    # the brook's valley in the West Head's top (top_h: metres above the plateau): a flat floor +-4 m, sides out to 11 m
    d, t = poly_nearest(x, z, BROOK)
    w = smoothstep(11.0, 4.0, d)
    if w <= 0.0:
        return top_h, 0.0
    return lerp(top_h, brook_floor(t) - DUNE_BASE - 0.04, w), w
# The river the crew came down (R76): it leaves the hills in the south-west, runs past the west wall and crosses the beach into the bay.
RIVER = [(-79.0, -36.0), (-80.0, -45.0), (-80.6, -52.0), (-81.6, -62.0), (-84.0, -74.0), (-89.0, -86.0), (-97.0, -98.0), (-108.0, -110.0),
         (-122.0, -120.0), (-140.0, -128.0), (-168.0, -134.0)]

def river_half_width(z):
    return lerp(4.2, 7.5, smoothstep(-60.0, -44.0, z))

def road_z_far(x):
    return -88.0 + 2.5 * math.sin(x * 0.021 + 0.6)

# Round 3 (2026-10-17): inside the walls the coast road is the village lane and it no longer runs ruler-straight: an S that swings north
# between the west cottages, dips south through the square under the chapel, swings north again past the east cottages and south by the
# nets stall (critics round 2: "a straight cobble ribbon to a vanishing point"). Catmull-Rom through LANE_PTS, blended into the far road's
# gentle sine past the walls.
LANE_PTS = [(-70.0, -89.9), (-62.0, -89.6), (-54.0, -89.2), (-46.0, -88.4), (-38.0, -87.4), (-30.0, -85.6), (-22.0, -85.4), (-15.0, -87.0),
            (-9.0, -88.4), (-3.0, -88.8), (3.0, -88.2), (9.0, -86.6), (15.0, -85.0), (22.0, -85.6), (28.0, -87.6), (34.0, -89.2), (40.0, -89.8),
            (47.0, -89.0), (54.0, -87.4), (62.0, -85.6), (70.0, -84.2)]

def lane_z(x):
    pts = LANE_PTS
    i = 1
    while i < len(pts) - 3 and x > pts[i + 1][0]:
        i += 1
    (x0, z0), (x1, z1), (x2, z2), (x3, z3) = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
    t = clamp((x - x1) / (x2 - x1), 0.0, 1.0)
    m1 = (z2 - z0) / (x2 - x0) * (x2 - x1)
    m2 = (z3 - z1) / (x3 - x1) * (x2 - x1)
    t2, t3 = t * t, t * t * t
    return (2 * t3 - 3 * t2 + 1) * z1 + (t3 - 2 * t2 + t) * m1 + (-2 * t3 + 3 * t2) * z2 + (t3 - t2) * m2

def road_z(x):
    k = smoothstep(68.0, 60.0, abs(x))
    return lerp(road_z_far(x), lane_z(x), k)

ROAD_X0, ROAD_X1 = -170.0, 62.0
ROAD_EAST = [(62.0, road_z(62.0)), (70.0, -92.5), (80.0, -99.0), (95.0, -103.0), (120.0, -105.0)]   # bends into the hills past the east wall

def road_pts():
    pts = [(x, road_z(x)) for x in frange(ROAD_X0, ROAD_X1, 2.0)]
    return pts + ROAD_EAST[1:]

# keep-outs that flatten the dunes inside the walls: (cx, cz, half x, half z)
YARDS = [(21.2, -66.0, 3.6, 2.4),      # boatyard: half-painted boat on sawhorses
         (5.2, -58.4, 1.6, 1.0),       # fish-cleaning table
         (-24.6, -59.4, 2.6, 1.0),     # net-drying racks
         (-13.6, -59.8, 1.4, 0.8),     # lunch bench
         (14.6, -59.6, 1.6, 1.2),      # dock-office bench and noticeboard
         (-6.0, -69.0, 2.2, 1.6),      # upturned canoe on the dunes
         (-41.5, -63.6, 1.8, 1.8)]     # smokehouse woodpile

def dune_part(x, z):
    # level-0 dunes (inside and just outside the walls), ending at retaining wall 1
    s = smoothstep(-60.5, -66.0, z)
    # dune ridges roughly parallel to the coast, broken up by noise
    ridge = 0.5 + 0.5 * math.sin(z * 0.42 + 2.2 * n2(x, z, 0.04, 10) + 0.8)
    d = 0.35 * ridge + 0.45 * (0.5 + 0.5 * n2(x, z, 0.06, 11)) + 0.25 * n2(x, z, 0.16, 12)
    amp = lerp(1.0, 2.1, smoothstep(-63.0, -70.0, z)) * (1.0 - 0.8 * smoothstep(-70.5, -74.6, z))
    h = s * amp * max(0.0, d - 0.12)
    h += s * 0.55 * smoothstep(55.0, 61.0, abs(x))
    return h * smoothstep(T1_Z + 0.6, T1_Z + 2.0, z)

def riser_s(x, z, zw, wall):
    # 0 in front of the retaining wall at zw, 1 behind it; a near-vertical riser (outside the walls it widens into a grassy bank); the
    # stair corridors of this wall are a straight ramp from zw + RISER_HALF down to the level behind, cut into the upper level
    out = max(0.0, abs(x) - WALL_X)
    hw = RISER_HALF + 0.35 * out
    s = smoothstep(zw + hw, zw - hw, z)
    if out <= 0.0:
        for _, rx_, w, run in RAMPS:
            if w != wall:
                continue
            dx = abs(x - rx_)
            if dx < RAMP_HW + RAMP_CHEEK:
                sr = clamp((zw + RISER_HALF - z) / run, 0.0, 1.0)
                k = smoothstep(RAMP_HW + RAMP_CHEEK, RAMP_HW, dx)
                s = lerp(s, min(s, sr), k)
    return s

def terrace_h(x, z):
    return T1_H * riser_s(x, z, T1_Z, 1) + (T2_H - T1_H) * riser_s(x, z, T2_Z, 2)

def hill_raw(x, z):
    # the hills behind the south wall, metres above level 2 (before terracing)
    r = max(0.0, WALL_ZS - z)
    if r <= 0.0:
        return 0.0
    hills = (0.17 * r + 0.0024 * r * r) * (1.0 + 0.45 * n2(x, 0.0, 0.011, 18))
    hills = min(hills, 34.0) + (5.0 * n2(x, z, 0.017, 13) + 2.2 * n2(x, z, 0.045, 14) + 0.9 * n2(x, z, 0.11, 19)) * smoothstep(5.0, 30.0, r)
    return max(0.0, hills)

def hill_terrace_k(x, z):
    r = WALL_ZS - z
    return smoothstep(1.5, 7.0, r) * (1.0 - smoothstep(58.0, 80.0, r)) * (1.0 - smoothstep(105.0, 150.0, abs(x)))

def hill_quant(h, k):
    # soft farm terraces: flat field bands every HILL_STEP m with a bank (riser) over the last 30% of each band
    if k <= 0.0:
        return h
    q = h / HILL_STEP
    f = q - math.floor(q)
    return lerp(h, (math.floor(q) + smoothstep(0.7, 1.0, f)) * HILL_STEP, k)

def dune_raw(x, z):
    h = dune_part(x, z) + terrace_h(x, z)
    r = max(0.0, WALL_ZS - z)
    if r > 0:
        h += hill_quant(hill_raw(x, z), hill_terrace_k(x, z))
    return h

def inside_walls(x, z, m=0.0):
    return -WALL_X - m <= x <= WALL_X + m and WALL_ZS - m <= z <= WALL_ZN + m

def in_ramp(x, z, m=0.0):
    # inside a stair corridor (x within the half width + m, z along its run)
    for _, rx_, w, run in RAMPS:
        zw = T1_Z if w == 1 else T2_Z
        if abs(x - rx_) < RAMP_HW + m and zw - run - m <= z <= zw + RISER_HALF + m:
            return True
    return False

def flatten_k(x, z):
    k = 1.0
    for cx, cz, w, d in (BAIT_SHOP, NET_SHED, SMOKEHOUSE, DOCK_OFFICE):
        k *= smoothstep(1.2, 5.5, rect_dist(x, z, cx, cz, w / 2, d / 2))
    k *= smoothstep(1.2, 5.5, rect_dist(x, z, HUT_RECT[0], HUT_RECT[1], HUT_RECT[2], HUT_RECT[3]))
    for cx, cz, hx, hz in YARDS:
        k *= smoothstep(0.4, 4.0, rect_dist(x, z, cx, cz, hx, hz))
    for pts, hw in PATHS:
        k *= smoothstep(hw + 0.5, hw + 4.6, poly_dist(x, z, pts))
    for tx, tz, _, _ in CODE_TREES:
        k *= smoothstep(1.3, 4.6, math.hypot(x - tx, z - tz))
    return k

# Round 4 (2026-10-18, critics round 3: "the creek is still a flat turquoise ribbon laid on the lawn, no bed, no banks", four P1 items): the
# creek glen replaces round 3's soft 0.6 m berms. The code plateau (y 1.2) cannot be cut, so the creek keeps its bed at 1.2 and the land
# either side rises ~1.1 m in steep earth banks: a raised west meadow (it also screens the west wall) and an east bank whose broad top
# carries the creek shelter and the fingerpost; below the shelter the east bank narrows to a ridge. The banks die out on the beach toward
# the lagoon and at the culvert, and keep a level ring round the code trees (they stand on 1.2 as the code places them). glen_h is metres
# above the code ground terrain_y(z); land_h (z < -55) and land_y read it, the dunes mesh draws it behind z -59.5 (a 0.33 m column band
# over the creek keeps the faces crisp) and Harbour_Core_Glen_Tri draws it from there to the beach.
GLEN_X0, GLEN_X1 = -66.5, -46.5
GLEN_TREES = None

def creek_side(x, z):
    # signed distance from the creek centreline (positive east of it)
    best, sgn = 1e9, 1.0
    for i in range(len(CREEK) - 1):
        (ax, az), (bx, bz) = CREEK[i], CREEK[i + 1]
        d, t = seg_dist(x, z, ax, az, bx, bz)
        if d < best:
            best = d
            cr = (bx - ax) * (z - az) - (bz - az) * (x - ax)
            sgn = -1.0 if cr > 0 else 1.0
    return best * sgn

def glen_raw(x, z):
    if not (GLEN_X0 < x < GLEN_X1) or z > -49.3 or z < T1_Z + 0.4:
        return 0.0
    zk = smoothstep(-49.3, -54.0, z) * smoothstep(T1_Z + 0.4, T1_Z + 3.4, z)
    if zk <= 0.0:
        return 0.0
    d = creek_side(x, z)
    ad = abs(d)
    inner = CREEK_HW + 0.32 + 0.12 * n2(x, z, 0.5, 121)
    face = smoothstep(inner, inner + 0.75, ad)
    if face <= 0.0:
        return 0.0
    if d < 0:
        fall = smoothstep(11.5, 7.5, ad)                 # the raised west meadow, out past the wall
    else:
        wide = smoothstep(-61.0, -59.0, z)               # a broad top under the shelter, a ridge below it
        top, end = lerp(2.7, 6.4, wide), lerp(4.6, 9.2, wide)
        fall = smoothstep(end, top, ad)
    # the west bank stands tall across the creek (its face looks toward the shelter and the beach); the east bank is a low lip
    H = (1.45 if d < 0 else 0.62) + 0.16 * n2(x, z, 0.13, 120) + 0.08 * n2(x, z, 0.5, 122)
    return H * zk * face * fall

def glen_h(x, z):
    global GLEN_TREES
    h = glen_raw(x, z)
    if h <= 0.0:
        return 0.0
    if GLEN_TREES is None:
        GLEN_TREES = [(tx, tz) for tx, tz, _, _ in CODE_TREES
                      if max(glen_raw(tx + dx, tz + dz) for dx in (-1.5, 0.0, 1.5) for dz in (-1.5, 0.0, 1.5)) > 0.02]
    for tx, tz in GLEN_TREES:
        dd = math.hypot(x - tx, z - tz)
        if dd < 4.2:
            h *= smoothstep(1.1, 4.2, dd)
    return h

def creek_bank(x, z):
    # (round 3's berms: superseded by glen_h)
    return 0.0

# the dunes' x columns: 1 m, with a 0.33 m band over the glen faces (x -60.5 .. -50.5)
GLEN_XS = sorted(set([round(v, 4) for v in frange(-WALL_X, WALL_X, 1.0)] + [round(v, 4) for v in frange(-60.5, -50.5, 1.0 / 3.0)]))

def glen_paint(V, N, F, fsl, vsl):
    # earth banks: where the glen raises the ground, steep faces are bare earth (a darker wet foot), the top lip stays grass
    for k, (v, n) in enumerate(zip(V, N)):
        if not (GLEN_X0 < v.x < GLEN_X1 and -76.0 < v.z < -49.0):
            continue
        gh = glen_h(v.x, v.z)
        if gh > 0.03 and n.y < 0.8:
            # a stony clay bank: grey stone with dark earth seams, darker and wetter at the foot
            low = (v.y - terrain_y(v.z)) < 0.35
            vsl[k] = S['WoodDark'] if (low or n2(v.x, v.z, 0.7, 123) > 0.2) else S['Rock']
        elif gh <= 0.03 and abs(creek_side(v.x, v.z)) < CREEK_HW + 0.5 and n.y < 0.97:
            vsl[k] = S['WoodDark']
    for i, f in enumerate(F):
        cx = sum(V[k].x for k in f) / len(f)
        cz = sum(V[k].z for k in f) / len(f)
        if not (GLEN_X0 < cx < GLEN_X1 and -76.0 < cz < -49.0):
            continue
        ny = sum(N[k].y for k in f) / len(f)
        if ny < 0.8 and glen_h(cx, cz) > 0.03:
            fsl[i] = (S['WoodDark'], L['Sand'])

def glen_cols():
    return sorted(set([x for x in GLEN_XS if x <= GLEN_X1 + 0.01] + [round(v, 4) for v in frange(GLEN_X0, -60.5, 1.0)]))

def glen_surface(x, z):
    # 6 cm under the code ground where the glen is flat (no z-fight), on the glen's height where it rises
    if z <= -55.0:
        return DUNE_BASE + land_h(x, z)
    gh = glen_h(x, z)
    return terrain_y(z) - 0.06 + gh + 0.06 * smoothstep(0.0, 0.15, gh)

def build_glen():
    # Round 4: the glen's banks from z -59.5 (where the dunes mesh stops) down the beach slope to where they die out (z -49.3); same columns
    # as the dunes so the seam at z -59.5 is shared. Visual; the walkable shape is in Harbour_Dunes_Col (build_coast_col).
    g = G2('Harbour_Core_Glen_Tri', 'core', tri=True, rays=14,
           note='Round 4: the creek glen\'s earth banks between z -59.5 and the beach (the dunes mesh draws them behind z -59.5).')
    xs = glen_cols()
    zs = grid_axis(-59.5, -49.0, 0.5)
    yfn = glen_surface
    Y = [[yfn(x, z) - terrain_y(z) for x in xs] for z in zs]
    def keep(i, j):
        return max(Y[j][i], Y[j + 1][i], Y[j][i + 1], Y[j + 1][i + 1]) > 0.05
    V, N, F, UV, _ = hf_geo(xs, zs, yfn, keep, eps=0.1)
    fsl = [(S['Grass'], L['Grass'])] * len(F)
    vsl = []
    for v in V:
        olive = n2(v.x, v.z, 0.11, 98) + 0.4 * n2(v.x, v.z, 0.3, 99)
        vsl.append(S['Reed'] if olive > 0.16 else (S2['FoliageAlt'] if n2(v.x, v.z, 0.08, 100) > 0.22 else S['Grass']))
    fsl = list(fsl)
    glen_paint(V, N, F, fsl, vsl)
    g.add((V, N, F, UV), S['Grass'], L['Grass'], 0.72, fsl, 1.0, vsl)
    # the creek bed: dark wet gravel under the water ribbon, culvert to the beach (the water reads darker and deeper over it)
    bed = [(-57.6, -74.6)] + [p for p in CREEK[1:6]] + [(-56.0, -48.6)]
    ground_ribbon(g, bed, CREEK_HW + 0.38, S['Rock'], L['Sand'], step=0.6, lift=0.022, wobble=0.18, seed=124,
                  yfn=lambda x, z: terrain_y(z), ao=0.7)
    # and under the river past the west wall (critics: "a pale turquoise strip lying on the lawn"): the same dark bed, so it reads as water
    # running over gravel, not paint on grass
    rbed = [(-80.0, -46.5)] + [p for p in RIVER[2:7]]
    ground_ribbon(g, rbed, 3.6, S['Rock'], L['Sand'], step=1.5, lift=0.0, wobble=0.1, seed=125,
                  yfn=lambda x, z: river_ribbon_y(z) - 0.045, ao=0.7, across=(-1.0, -0.6, 0.0, 0.6, 1.0))
    d = G2('Harbour_Detail_Glen', 'detail', rays=10,
           note='Round 4: the creek glen\'s stones (in the bed and at the foot of the banks), ferns and flowers on the bank tops. No colliders.')
    r = rng2('glen')
    for k in range(70):
        i = r.randrange(1, 6)
        (ax, az), (bx, bz) = CREEK[i], CREEK[i + 1]
        t = r.random()
        px, pz = ax + (bx - ax) * t, az + (bz - az) * t
        L_ = math.hypot(bx - ax, bz - az)
        nx, nz = (bz - az) / L_, -(bx - ax) / L_
        foot = k % 3 != 0
        off = (CREEK_HW + r.uniform(0.15, 0.55)) * (1 if r.random() < 0.5 else -1) if foot else r.uniform(-0.7, 0.7) * CREEK_HW
        x, z = px + nx * off, pz + nz * off
        if z > -49.5 or z < T1_Z + 1.2:
            continue
        rad = r.uniform(0.28, 0.55) if foot else r.uniform(0.1, 0.24)
        y = terrain_y(z) + (rad * 0.1 if foot else -rad * 0.25)
        d.add(xform(blob(rad, r, (1.25, 0.55 if foot else 0.4, 1.0), subdiv=1 if rad < 0.3 else 2, rough=0.14, flat=-0.35, yaw=r.uniform(0, 360)), (x, y, z)),
              S['Rock'], L['Rock'], 0.6, None, r.uniform(0.75, 1.0))
    # ferns and wild flowers along the bank tops (a fan of blades; a few orange and white heads)
    for k in range(60):
        x, z = r.uniform(-60.3, -49.5), r.uniform(-73.0, -50.5)
        gh = glen_h(x, z)
        if gh < 0.7 or abs(creek_side(x, z)) < CREEK_HW + 1.3 or not tree_clear(x, z, 1.4):
            continue
        if abs(x - CREEK_SHELTER[0]) < 2.4 and abs(z - CREEK_SHELTER[1]) < 2.6:
            continue
        y = land_y(x, z) - 0.03
        for j in range(r.randint(5, 8)):
            a = r.uniform(0, 2 * math.pi)
            d.add(blade_geo(Vector((x, y, z)), (math.cos(a), 0.0, math.sin(a)), r.uniform(0.35, 0.6), 0.07, 0.35, segs=2),
                  S['FoliageDark'] if j % 2 else S2['FoliageAlt'], L['Grass'], 0.6)
        if r.random() < 0.45:
            for j in range(3):
                fx, fz = x + r.uniform(-0.3, 0.3), z + r.uniform(-0.3, 0.3)
                add_lathe(d, (fx, y + r.uniform(0.25, 0.45), fz), [(0, 0.0), (0.02, 0.05), (0.05, 0.06), (0.06, 0.0)],
                          S['Accent'] if k % 2 else S2['Snow'], L['none'], 0.7, sides=5)
    return [g, d]

def land_h(x, z):
    # metres above the plateau (outside the walls also the headlands and the river valley); never negative
    if inside_walls(x, z):
        # level-0 dunes flattened around the village; the terraces are exact (the ramps and level tops are walkable as drawn)
        h = dune_part(x, z) * flatten_k(x, z) + terrace_h(x, z)
        if z < -55.0 and x < GLEN_X1:
            h = max(h, glen_h(x, z))       # round 4: the creek glen (behind z -55 its base is the plateau)
        return h
    h = dune_raw(x, z)
    # outside: headlands
    for key, poly in (('W', WEST_HEAD), ('E', EAST_HEAD)):
        sd = poly_signed(x, z, poly)
        if sd < 0:
            m = smoothstep(0.0, 7.0, -sd)
            top = HEAD_TOP[key] + head_var(key, x, z) + 1.6 * n2(x, z, 0.05, 15 if key == 'W' else 16)   # round 4: head_var
            if key == 'W':
                top, wb = brook_cut(x, z, top)
                m = max(m, wb * smoothstep(0.0, 3.0, -sd))
            h = max(h, m * top)
    if z > -44.0 and h < 3.0:
        return 0.0
    if z > -60.0 and h < 3.0:
        h *= smoothstep(-56.0, -60.0, z)       # leave the code beach strip outside the walls alone
    # road terrace (outside the walls): blend to the road's own height across it; then the river valley cuts through it (a bridge spans it)
    if z < -78.0:
        dd = road_dist(x, z)
        if dd < 9.0:
            h = lerp(road_h(x), h, smoothstep(3.2, 9.0, dd))
    dr = poly_dist(x, z, RIVER)
    w = river_half_width(z)
    h *= smoothstep(w + 0.6, w + 13.0, dr)
    if z < -55.0 and x > GLEN_X0:
        h = max(h, glen_h(x, z))           # round 4: the glen's west meadow past the wall
    return max(0.0, h)

def road_dist(x, z):
    if x <= ROAD_X1:
        dz = road_z(x) - z
        slope = (road_z(x + 0.25) - road_z(x - 0.25)) / 0.5
        return abs(dz) / math.sqrt(1.0 + slope * slope)
    return poly_dist(x, z, ROAD_EAST)

def road_h(x):
    # the road's height above the plateau: round 1 - the coast road IS the level-1 lane inside the walls; west of the walls it keeps the
    # level (it crosses the river on the bridge); past the east wall it climbs gently into the hills
    if x <= ROAD_X1:
        return T1_H
    return T1_H + (x - ROAD_X1) * 0.12

def land_y(x, z):
    # ground height for placing things (Unity y): the code beach/seabed, raised by the pass-2 land where it is above it
    base = terrain_y(z)
    h = land_h(x, z)
    y = max(base, DUNE_BASE + h) if (h > 0.0 or z < -55.0) else base
    if z > -60.0 and -64.0 < x < 66.0:
        cy = coast_y(x, z)          # round 2: the spit, the bar, the mole, the rock shelf
        if cy is not None and cy > y:
            y = cy
    if -55.0 <= z < -49.0 and GLEN_X0 < x < GLEN_X1:
        y = max(y, terrain_y(z) + glen_h(x, z))     # round 4: the glen banks on the beach slope
    return y

def grid_axis(a, b, step, extra=(), bands=(), bstep=0.5):
    # sorted unique coordinates: a..b every `step`, every `bstep` inside each (lo, hi) band, plus the exact `extra` values inside [a, b]
    n = max(1, int(round((b - a) / step)))
    pts = set(round(a + (b - a) * i / n, 4) for i in range(n + 1))
    for lo, hi in bands:
        lo, hi = max(lo, a), min(hi, b)
        if hi - lo > 1e-6:
            pts.update(round(v, 4) for v in frange(lo, hi, bstep))
    pts.update(round(v, 4) for v in extra if a - 1e-6 <= v <= b + 1e-6)
    out = []
    for v in sorted(pts):
        if not out or v - out[-1] > 0.02:
            out.append(v)
    return out

def seg_axis(segments, extra=()):
    # piecewise axis: [(a, b, step), ...] (contiguous), plus the exact extra values inside the overall range
    pts = []
    for a, b, step in segments:
        n = max(1, int(round((b - a) / step)))
        pts += [a + (b - a) * i / n for i in range(n + 1)]
    lo, hi = segments[0][0], segments[-1][1]
    pts += [v for v in extra if lo <= v <= hi]
    out = []
    for v in sorted(round(p, 4) for p in pts):
        if not out or v - out[-1] > 0.05:
            out.append(v)
    return out

def riser_rows():
    return [zw + d for zw in (T1_Z, T2_Z) for d in (RISER_HALF, RISER_HALF * 0.5, 0.0, -RISER_HALF * 0.5, -RISER_HALF)]

def terrace_axes(x0, x1, z0, z1, step):
    # grid rows/columns for the terrace height field: the riser lines, the ramp ends and the ramp corridor edges are exact grid lines
    xe, ze = [], []
    for _, rx_, w, run in RAMPS:
        for s_ in (-1, 1):
            xe += [rx_ + s_ * RAMP_HW, rx_ + s_ * (RAMP_HW + RAMP_CHEEK * 0.5), rx_ + s_ * (RAMP_HW + RAMP_CHEEK)]
        xe.append(rx_)
        zw = T1_Z if w == 1 else T2_Z
        ze += [zw + RISER_HALF - run, zw + RISER_HALF - run * 0.5]
    for zw in (T1_Z, T2_Z):
        ze += [zw + RISER_HALF, zw + RISER_HALF * 0.5, zw, zw - RISER_HALF * 0.5, zw - RISER_HALF, zw + 1.0, zw - 1.0]
    return grid_axis(x0, x1, step, xe), grid_axis(z0, z1, step, ze)

def build_dunes():
    # Level-0 dunes (1 m grid, as round 0) and, round 1, the two terraces with their stair corridors (2 m grid with exact riser and ramp lines).
    # The quads inside a stair corridor are left out: the stone steps cover it (Harbour_Core_Walls_Tri).
    g = G2('Harbour_Core_Dunes_Tri', 'core', tri=True, rays=16)
    slopes = []
    for part in ('dunes', 'terraces'):
        if part == 'dunes':
            xs = GLEN_XS if GLEN_XS else frange(-WALL_X, WALL_X, 1.0)
            zs = frange(T1_Z + 0.6, -59.5, 1.0)
            keep_h = 0.06
        else:
            xs, zs = terrace_axes(-WALL_X, WALL_X, WALL_ZS, T1_Z + 0.6, 4.0)
            keep_h = 0.06
        H = [[land_h(x, z) for x in xs] for z in zs]
        def keep(i, j, H=H, xs=xs, zs=zs, keep_h=keep_h):
            if max(H[j][i], H[j + 1][i], H[j][i + 1], H[j + 1][i + 1]) <= keep_h:
                return False
            return not in_ramp((xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2, -0.05)
        V, N, F, UV, _ = hf_geo(xs, zs, lambda x, z: DUNE_BASE + land_h(x, z), keep, eps=0.1)
        fsl = []
        for f in F:
            ny = sum(N[k].y for k in f) / len(f)
            fsl.append((S['Rock'], L['Rock']) if ny < 0.5 else (S['Grass'], L['Grass']))
        # round 3 (critics: "a flat saturated lime lawn"): soft olive and blue-green patches in the grass, per vertex (as the code lawn)
        vsl = []
        for v, n in zip(V, N):
            if n.y < 0.5:
                vsl.append(S['Rock'])
                continue
            olive = n2(v.x, v.z, 0.11, 98) + 0.4 * n2(v.x, v.z, 0.3, 99)
            vsl.append(S['Reed'] if olive > 0.16 else (S2['FoliageAlt'] if n2(v.x, v.z, 0.08, 100) > 0.22 else S['Grass']))
        glen_paint(V, N, F, fsl, vsl)        # round 4: earth banks on the creek glen
        g.add((V, N, F, UV), S['Grass'], L['Grass'], 0.72, fsl, 1.0, vsl)
        slopes.append(max_slope(V, F))
    g.extra['max_slope_deg'] = max(slopes)
    return g

def max_slope(V, F):
    worst = 0.0
    for f in F:
        for tri in ((f[0], f[1], f[2]), (f[0], f[2], f[3])):
            a, b, c = V[tri[0]], V[tri[1]], V[tri[2]]
            n = (b - a).cross(c - a)
            if n.length > 0:
                worst = max(worst, math.degrees(math.acos(min(1.0, abs(n.normalized().y)))))
    return round(worst, 1)

def build_dunes_col():
    # Walkable collision: the level-0 dunes on a 2 m grid (as round 0) plus, round 1, the terraces with exact riser lines (a 79 deg face:
    # not walkable, above StepOffset) and the stair corridors as smooth ramps (<= 23.2 deg).
    g = G2('Harbour_Dunes_Col', 'col')
    slopes = []
    for part in ('dunes', 'terraces'):
        if part == 'dunes':
            xs = frange(-WALL_X, WALL_X, 2.0)
            zs = grid_axis(T1_Z + 0.6, -59.5, 2.0, [T1_Z + 0.6])
        else:
            xs, zs = terrace_axes(-WALL_X, WALL_X, WALL_ZS, T1_Z + 0.6, 4.0)
        H = [[land_h(x, z) for x in xs] for z in zs]
        keep = lambda i, j, H=H: max(H[j][i], H[j + 1][i], H[j][i + 1], H[j + 1][i + 1]) > 0.04
        V, N, F, UV, _ = hf_geo(xs, zs, lambda x, z: DUNE_BASE + land_h(x, z), keep, eps=0.1)
        g.add((V, N, F, UV), 0, 0, 1.0)
        slopes.append(max_slope(V, F))
    build_coast_col(g)     # round 2: the spit, the bar, the mole and the rock shelf
    g.extra['max_slope_deg'] = max(slopes)
    g.extra['ramp_slope_deg'] = [round(math.degrees(math.atan2((T1_H if w == 1 else T2_H - T1_H), run)), 1) for _, _, w, run in RAMPS]
    return g

def build_hills():
    # Backdrop land outside the walls: south hills, the two headland tops, the river valley; 3 m grid. Split west / east.
    # Round 1: a finer grid on the near hills (2.5 m; the farm terraces need it), the terrace riser rows continue outside the walls, and
    # the faces are painted by field: each terrace band is split into fields (grass, hay, wheat stubble, a dark crop, ploughed soil) with
    # darker hedge banks on the risers, so the hills read as farmland, not one green mound.
    out = []
    xs = seg_axis([(-200.5, -140.5, 10.0), (-140.5, -80.5, 5.0), (-80.5, 80.5, 3.0), (80.5, 140.5, 5.0), (140.5, 200.5, 10.0)], [-WALL_X, WALL_X])
    zs = seg_axis([(-215.5, -150.5, 5.0), (-150.5, WALL_ZS, 3.0), (WALL_ZS, 60.5, 4.0)], riser_rows() + [T1_Z + 1.0, T1_Z - 1.0, T2_Z + 1.0, T2_Z - 1.0])
    H = [[land_h(x, z) for x in xs] for z in zs]
    def keep_fn(lo_i, hi_i):
        def keep(i, j):
            if not (lo_i <= i < hi_i):
                return False
            x0, x1, z0, z1 = xs[i], xs[i + 1], zs[j], zs[j + 1]
            cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
            if inside_walls(cx, cz):
                return False
            hmax = max(H[j][i], H[j + 1][i], H[j][i + 1], H[j + 1][i + 1])
            beyond_code = abs(cx) > 120.0 or cz < -100.0
            if cz > -44.0:
                return hmax > 0.3
            if abs(cx) > 140.0 and cz > -60.0 and hmax < 0.3:
                return False
            return hmax > 0.06 or beyond_code
        return keep
    mid = xs.index(min(xs, key=lambda x: abs(x - 0.5)))
    # round 3: no hill quads in the first ~5.5 m behind a swept cliff edge - the cliff face and its crest cover that strip, and the hill's 3 m
    # triangles poked out through the face as green spikes wherever a crack or the wander pushed the face in
    swept = [[poly[e] for e in edges] + [poly[(edges[-1] + 1) % len(poly)]] for poly, edges in CLIFF_EDGES.values()]
    base_keep = keep_fn
    def keep_fn(lo_i, hi_i):
        k0 = base_keep(lo_i, hi_i)
        def keep(i, j):
            if not k0(i, j):
                return False
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            for pts, (poly, _) in zip(swept, CLIFF_EDGES.values()):
                if poly_dist(cx, cz, pts) < 5.5 and point_in_poly(cx, cz, poly):
                    return False
            return True
        return keep
    for suffix, lo_i, hi_i in (('A', 0, mid), ('B', mid, len(xs) - 1)):
        g = G2('Harbour_Core_Hills_%s_Tri' % suffix, 'core', tri=True, rays=8, ao_dist=2.0)
        V, N, F, UV, _ = hf_geo(xs, zs, lambda x, z: DUNE_BASE + land_h(x, z), keep_fn(lo_i, hi_i), eps=0.6)
        fsl = []
        for f in F:
            ny = sum(N[k].y for k in f) / len(f)
            cx = sum(V[k].x for k in f) / len(f); cz = sum(V[k].z for k in f) / len(f)
            fsl.append(field_slot(cx, cz, ny))
        # round 2b: the field colour per VERTEX (it blends softly across each face): round 1's per-face fields were hard-edged triangles
        # (critics: "camouflage patches", "stickers"); the painted layer stays per face
        vsl = [field_slot(v.x, v.z, n.y)[0] for v, n in zip(V, N)]
        g.add((V, N, F, UV), S['Grass'], L['Grass'], 0.75, fsl, 1.0, vsl)
        out.append(g)
    return out

FIELD_SLOTS = [(S['Grass'], L['Grass']), (S['Reed'], L['Grass']), (S['Grass'], L['Grass']), (15, L['Grass']),
               (S['Foliage'], L['Foliage']), (S['Reed'], L['Grass']), (15, L['Grass']), (S['Grass'], L['Grass'])]   # round 4: no sand/soil fields

def field_slot(x, z, ny):
    # the hills' paint: rock on steep faces; on the farm terraces a field per band and ~26 m strip, hedge banks on the risers
    if ny < 0.62:
        return (S['Rock'], L['Rock'])
    k = hill_terrace_k(x, z) if z < WALL_ZS else 0.0
    if k < 0.35:
        return (S['Grass'], L['Grass'])
    q = hill_raw(x, z) / HILL_STEP
    f = q - math.floor(q)
    if 0.74 < f < 0.97:
        return (S['Foliage'], L['Grass'])
    band = int(math.floor(q))
    fid = int(math.floor((x + 7.0 * n2(x, z, 0.02, 70)) / 34.0))
    return FIELD_SLOTS[(band * 5 + fid * 3 + (band * fid) % 3) % len(FIELD_SLOTS)]

def river_ribbon_y(z):
    if z >= -55.0:
        return max(beach_y(z), -0.12) + (0.05 if z <= COAST_Z else 0.05 - 0.02 * (z - COAST_Z))
    return PLATEAU_Y + 0.06

def build_river():
    g = G2('Harbour_Core_River', 'core', rays=0, material='M_Water_Creek (water without the shore-foam band, not toon): the river the crew came down, the creek, the tide pools',
           note='Visual only, outside the walls. Draw it with the water material (no palette slot fits water); shadows off.')
    pts = [Vector((p[0], 0.0, p[1])) for p in RIVER]
    pts[0] = pts[0].lerp(pts[1], (-44.4 - RIVER[0][1]) / (RIVER[1][1] - RIVER[0][1]))   # round 4: end at the shoreline, not over the sea
    # resample every ~1.5 m
    dense = []
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        n = max(1, int((b - a).length / 1.5))
        for k in range(n):
            dense.append(a.lerp(b, k / n))
    dense.append(pts[-1])
    V, N, F, UV = [], [], [], []
    across = [-1.0, -0.6, 0.0, 0.6, 1.0]
    for i, p in enumerate(dense):
        t = (dense[min(i + 1, len(dense) - 1)] - dense[max(i - 1, 0)]).normalized()
        side = Vector((t.z, 0.0, -t.x))
        w = river_half_width(p.z)
        for a in across:
            q = p + side * (a * w)
            y = river_ribbon_y(q.z) - 0.07 * abs(a) ** 4
            V.append(Vector((q.x, y, q.z))); N.append(Vector((0, 1, 0)))
    m = len(across)
    for i in range(len(dense) - 1):
        for k in range(m - 1):
            a = i * m + k
            q = [a, a + 1, a + m + 1, a + m]
            # orientation: make the face normal point up
            pa, pb, pc = V[q[0]], V[q[1]], V[q[2]]
            if (pb - pa).cross(pc - pa).y < 0:
                q = list(reversed(q))
            F.append(q); UV.append([(V[j].x, V[j].z) for j in q])
    g.add((V, N, F, UV), S['Sand'], L['none'], 1.0)
    # still water in the tide pools by the east wall (same water material; visual only)
    build_creek_ribbon(g)      # round 1
    for (x, z, rr) in SHELF_POOLS:      # round 2: the pools sit in hollows of the rock shelf (round 1's discs lay on the beach)
        ring = [(x + rr * math.cos(a), z + rr * 0.8 * math.sin(a)) for a in [2 * math.pi * i / 12 for i in range(12)]]
        y = max(coast_y(x + rr * 0.7, z), coast_y(x - rr * 0.7, z)) - 0.06
        V = [Vector((x, y, z))] + [Vector((px, y, pz)) for px, pz in ring]
        F = [[0, 1 + (i + 1) % 12, 1 + i] for i in range(12)]
        g.add((V, [Vector((0, 1, 0))] * len(V), F, [[(V[k].x, V[k].z) for k in f] for f in F]), S['Sand'], L['none'], 1.0)
    return g

TIDE_POOLS = [(55.2, -47.4, 0.7), (57.6, -45.9, 0.55), (63.0, -44.0, 1.1)]

# ---------------------------------------------------------------------------------------------------------------------------------------
# Headland cliffs (rock blobs along the bay-facing edges), paths and sand blowouts, the low stone wall, the coast road with its bridge,
# telephone poles and a farm fence receding into the hills.
# ---------------------------------------------------------------------------------------------------------------------------------------
def dist_to_walls(x, z):
    dx = max(abs(x) - WALL_X, 0.0); dz = max(WALL_ZS - z, z - WALL_ZN, 0.0)
    return math.hypot(dx, dz)

def edge_walk(poly, edges, spacing):
    # points along the given polygon edges every `spacing` m with the inward normal
    out = []
    for e in edges:
        ax, az = poly[e]; bx, bz = poly[(e + 1) % len(poly)]
        L = math.hypot(bx - ax, bz - az)
        tx, tz = (bx - ax) / L, (bz - az) / L
        nx, nz = -tz, tx
        mx, mz = (ax + bx) / 2, (az + bz) / 2
        if not point_in_poly(mx + nx * 2.0, mz + nz * 2.0, poly):
            nx, nz = -nx, -nz
        n = max(1, int(L / spacing))
        for k in range(n):
            t = (k + 0.5) / n
            out.append((ax + (bx - ax) * t, az + (bz - az) * t, nx, nz))
    return out

def cliff_rock(radius, r, scale=(1.0, 1.25, 0.85), subdiv=2, yaw=0.0, top=0.55, strata=3.0):
    # A chunkier cliff stone than blob(): stronger box bias, a flattened top, soft horizontal ledges (strata), flattened base.
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    off = Vector((r.uniform(-50, 50), r.uniform(-50, 50), r.uniform(-50, 50)))
    for v in bm.verts:
        d = v.co.normalized()
        n1 = noise.noise(d * 1.2 + off)
        n2_ = noise.noise(d * 2.9 + off * 1.3)
        box = 1.0 / max(abs(d.x), abs(d.y), abs(d.z))
        bias = 1.0 + (min(box * 0.66, 1.2) - 1.0) * 0.7
        p = d * (1.0 + 0.24 * n1 + 0.1 * n2_) * bias
        p.y += 0.07 * math.sin(p.y * math.pi * strata + off.x)
        if p.y > top:
            p.y = top + (p.y - top) * 0.25
        if p.y < -0.6:
            p.y = -0.6 + (p.y + 0.6) * 0.2
        v.co = p
    mx = max(Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])).length for v in bm.verts)
    k = radius / mx
    rot = ry(yaw)
    for v in bm.verts:
        v.co = rot @ Vector((v.co.x * scale[0] * k, v.co.y * scale[1] * k, v.co.z * scale[2] * k))
    geo = bm_geo(bm, normals='smooth', uv='xz')
    bm.free()
    return geo

def sculpt_mass(radius, r, scale, subdiv, yaw):
    # A big smooth rock mass: low-frequency noise and a gentle box bias give broad planes; two soft strata; a flattened top and base.
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    off = Vector((r.uniform(-50, 50), r.uniform(-50, 50), r.uniform(-50, 50)))
    for v in bm.verts:
        d = v.co.normalized()
        n1 = noise.noise(d * 0.75 + off)
        n2_ = noise.noise(d * 1.7 + off * 1.3)
        box = 1.0 / max(abs(d.x), abs(d.y), abs(d.z))
        bias = 1.0 + (min(box * 0.7, 1.25) - 1.0) * 0.5
        p = d * (1.0 + 0.2 * n1 + 0.06 * n2_) * bias
        p.y += 0.04 * math.sin(p.y * math.pi * 2.2 + off.y)
        if p.y > 0.62:
            p.y = 0.62 + (p.y - 0.62) * 0.3
        if p.y < -0.55:
            p.y = -0.55 + (p.y + 0.55) * 0.2
        v.co = p
    mx = max(Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])).length for v in bm.verts)
    k = radius / mx
    rot = ry(yaw)
    for v in bm.verts:
        v.co = rot @ Vector((v.co.x * scale[0] * k, v.co.y * scale[1] * k, v.co.z * scale[2] * k))
    geo = bm_geo(bm, normals='smooth', uv='xz')
    bm.free()
    return geo

def place_outside(g, geo, c, nx, nz, margin=0.6):
    # Push a rock mass along the headland's inward normal until none of it is inside the play area (|x| < 60.5 + margin).
    V = geo[0]
    c = Vector(c)
    for _ in range(80):
        bad = any(abs(c.x + v.x) < WALL_X + margin and WALL_ZS < c.z + v.z < WALL_ZN for v in V)
        if not bad:
            break
        c += Vector((nx, 0.0, nz)) * 0.5
    # Round 2: strata painted PER VERTEX (the slot blends across each face, so the bands are soft: round 1's per-face bands zigzagged into an
    # orange "decal" on the big triangles): warm ochre / grey / buff layers that wander with noise, a dark wet and mossy foot, grass on the
    # flat tops (grass layer only where a whole face is up-facing and high).
    V2, N2, F2, UV2 = xform(geo, c)
    vsl = []
    for v, n in zip(V2, N2):
        # noise well under the band period, so the strata stay horizontal layers (1.4 m of noise on a 2.3 m period closed into contours)
        band = (v.y + 0.38 * n2(v.x, v.z, 0.035, 41) + 0.12 * n2(v.x, v.z, 0.11, 42)) / 2.3
        f = band - math.floor(band)
        if v.y < 0.55:
            vsl.append(S['FoliageDark'] if v.y < 0.15 else S['Reed'])
        elif n.y > 0.8 and v.y > 2.5:
            vsl.append(S['Grass'])
        elif f < 0.3:
            vsl.append(S['Dock'])
        elif f < 0.45:
            vsl.append(S['Sand'])
        else:
            vsl.append(S['Rock'])
    fsl = []
    for f in F2:
        cy = sum(V2[k].y for k in f) / len(f)
        ny = min(N2[k].y for k in f)
        fsl.append((S['Grass'], L['Grass']) if (ny > 0.8 and cy > 2.5) else (S['Rock'], L['Rock']))
    g.add((V2, N2, F2, UV2), S['Rock'], L['Rock'], AO_ROCK, fsl, 1.0, vsl)

# Round 3 (2026-10-17): the headland cliffs are swept faces now, not inflated boulders (critics round 2: "rounded lumpy masses with a wet-boulder
# look and orange speckle; no ledges, caves, clinging plants or foot rocks"). A stepped profile - a wet foot, three strata that step back
# into the headland with flat ledge treads, a grassy lip - is swept along the headland edge with low-frequency wander, vertical cracks that
# cut every riser, and one sea cave. Strata colours alternate warm ochre / cool grey / buff by layer (per vertex: soft), ledge treads and
# the crest carry grass, the foot is dark and wet; a talus of boulders lies along the waterline.
CLIFF_PROFILE = [   # (inset into the headland m, height m above the sea, kind) - kind: f foot, r riser, t tread, c crest
    (-1.2, -3.0, 'f'), (-0.2, -0.4, 'f'), (0.0, 0.35, 'f'), (0.25, 1.1, 'r'), (0.4, 1.7, 'r'), (1.35, 1.85, 't'), (1.55, 2.2, 'r'),
    (1.75, 3.7, 'r'), (1.85, 4.5, 'r'), (2.9, 4.65, 't'), (3.1, 5.0, 'r'), (3.3, 6.6, 'r'), (3.45, 7.6, 'r'), (4.3, 7.75, 't'),
    (4.5, 8.1, 'r'), (4.75, 9.4, 'r'), (5.0, 1.0, 'c'), (6.4, 1.12, 'c'), (9.0, 0.88, 'c')]   # crest heights: x the headland top

CLIFF_TUFTS = []
CLIFF_EDGES = {'W': (WEST_HEAD, list(range(0, 6))), 'E': (EAST_HEAD, list(range(0, 7)))}   # round 3: the headland edges with a swept face

def cliff_sweep(g, poly, edges, top, r, key):
    # the polyline along the chosen edges, resampled every ~0.9 m, with the inward normal (smoothed at the corners)
    pts = [poly[e] for e in edges] + [poly[(edges[-1] + 1) % len(poly)]]
    dense = []
    for i in range(len(pts) - 1):
        (ax, az), (bx, bz) = pts[i], pts[i + 1]
        n = max(1, int(math.hypot(bx - ax, bz - az) / 0.9))
        for k in range(n):
            dense.append((ax + (bx - ax) * k / n, az + (bz - az) * k / n))
    dense.append(pts[-1])
    normals = []
    for i, (x, z) in enumerate(dense):
        (ax, az), (bx, bz) = dense[max(0, i - 3)], dense[min(len(dense) - 1, i + 3)]
        tx, tz = bx - ax, bz - az
        L_ = math.hypot(tx, tz) or 1.0
        nx, nz = -tz / L_, tx / L_
        if not point_in_poly(x + nx * 3.0, z + nz * 3.0, poly):
            nx, nz = -nx, -nz
        normals.append((nx, nz))
    acc = [0.0]
    for i in range(1, len(dense)):
        acc.append(acc[-1] + math.hypot(dense[i][0] - dense[i - 1][0], dense[i][1] - dense[i - 1][1]))
    total = acc[-1]
    rc = rng2('cliffsweep.' + key)
    cracks = []
    sc = rc.uniform(2.0, 5.0)
    while sc < total:
        # round 4: fewer, shallower cracks (round 3's every 3-7.5 m, 0.8-1.9 m deep, drew a row of dark rectangles along each stratum)
        cracks.append((sc, rc.uniform(0.5, 0.9), rc.uniform(0.35, 0.8)))
        sc += rc.uniform(7.0, 14.0)
    piers = []
    sp = rc.uniform(4.0, 9.0)
    while sp < total:
        piers.append((sp, rc.uniform(2.2, 4.0), rc.uniform(0.9, 1.7)))
        sp += rc.uniform(10.0, 17.0)
    cave_s = total * (0.18 if key == 'E' else 0.3)
    def crack(sv):
        c = 0.0
        for cs, w, d in cracks:
            if abs(sv - cs) < 2.0 * w:
                c = max(c, d * math.exp(-((sv - cs) / w) ** 2))
        return c
    rows = []
    fall_best = 1e9
    for i, ((x, z), (nx, nz), sv) in enumerate(zip(dense, normals, acc)):
        wander = max(-0.9, 1.8 * n2(sv, 0.0, 0.03, 31 if key == 'E' else 32) + 0.6 * n2(sv, 0.0, 0.12, 33))   # never far into the play area
        tl = top + head_var(key, x, z)          # round 4: the crest rises and falls along the coast
        dfall = math.hypot(x - FALL_EDGE[0], z - FALL_EDGE[1]) if key == 'W' else 1e9
        wf = math.exp(-(dfall / 4.4) ** 2)       # round 4: the waterfall's alcove
        if dfall < fall_best:
            fall_best = dfall
            FALL_INFO.update(x=x, z=z, nx=nx, nz=nz, wander=wander, i=i)
        hjit = 1.1 * n2(sv, 0.0, 0.025, 34) + 0.3 * n2(sv, 0.0, 0.1, 38)      # tilted, wandering strata
        ck = crack(sv)
        pier = 0.0
        for ps, pw, pd in piers:
            if abs(sv - ps) < 2.5 * pw:
                pier = max(pier, pd * math.exp(-((sv - ps) / pw) ** 2))
        cave = math.exp(-((sv - cave_s) / 2.6) ** 2)
        row = []
        for j, (inset, h, kind) in enumerate(CLIFF_PROFILE):
            if kind == 'c':
                y = tl * h + 0.6 * n2(x, z, 0.08, 35)
                y = lerp(y, FALL_Y - 0.02, min(1.0, wf * 1.6))
                ins = inset + wander
            else:
                y = h * (tl / 10.5) + hjit * (h / 9.4)
                stratum = 0 if h < 1.9 else (1 if h < 4.7 else (2 if h < 7.8 else 3))
                ins = inset + wander + 0.55 * n2(sv, 0.0, 0.07, 40 + stratum) - pier * smoothstep(6.5, 2.0, h)
                if kind == 'r':
                    ins += ck * (1.0 - 0.6 * (h / 9.4))         # the cracks bite deepest low down
                    if h < 4.0:
                        ins += cave * 3.2 * smoothstep(4.0, 1.5, h)   # the sea cave (lower two strata)
                elif kind == 't':
                    ins += 0.5 * ck
                ins = lerp(ins, 4.9 + wander, wf)      # round 4: the alcove behind the waterfall (a recessed, near-vertical wet wall)
            row.append(Vector((x + nx * ins, y, z + nz * ins)))
        rows.append((row, cave, ck, wf))
    V, N, F, UV = [], [], [], []
    m = len(CLIFF_PROFILE)
    for row, _, _, _ in rows:
        V.extend(row)
    for i in range(len(rows) - 1):
        for j in range(m - 1):
            a = i * m + j
            q = [a, a + 1, a + m + 1, a + m]
            F.append(q)
            UV.append([(V[k].x + V[k].z, V[k].y) for k in q])
    # wind every face to look out of the headland (test one riser quad against the outward normal), then vertex normals from the faces
    votes = 0.0
    for i in range(min(12, len(rows) - 1)):
        for j in (6, 7, 10, 11):          # riser quads (near-vertical): their horizontal normal is reliable
            f = F[i * (m - 1) + j]
            a0, b0, c0 = V[f[0]], V[f[1]], V[f[2]]
            fn = (b0 - a0).cross(c0 - a0)
            votes += fn.x * normals[i][0] + fn.z * normals[i][1]
    if votes > 0.0:
        F = [list(reversed(f)) for f in F]
        UV = [list(reversed(u)) for u in UV]
    acc_n = [Vector() for _ in V]
    for f in F:
        a, b, c = V[f[0]], V[f[1]], V[f[2]]
        nrm = (b - a).cross(c - a)
        for k in f:
            acc_n[k] += nrm
    for k in range(len(V)):
        nn = acc_n[k]
        N.append(nn.normalized() if nn.length > 1e-9 else Vector((0, 1, 0)))
    vsl, tint = [], []
    for i, (row, cave, ck, wf) in enumerate(rows):
        for j, (inset, h, kind) in enumerate(CLIFF_PROFILE):
            v = row[j]
            stratum = 0 if h < 1.9 else (1 if h < 4.7 else (2 if h < 7.8 else 3))
            if kind != 'c' and wf > 0.3:
                vsl.append(S['WoodDark'] if (j + i) % 3 else S['FoliageDark'])    # round 4: the wet, mossy alcove
            elif kind == 'f' or v.y < 0.3:
                vsl.append(S['WoodDark'])     # the wet dark foot (round 3: dark green drew spikes up the first stratum)
            elif kind == 'c':
                vsl.append(S['Grass'])
            # (ledge treads stay rock: green treads bled into the risers as green triangles; the scrub clumps below carry the plants)
            elif cave > 0.4 and v.y < 4.0:
                vsl.append(S['WoodDark'])
            else:
                vsl.append((S['Rock'], S['Dock'], S['Rock'], S['Sand'])[(stratum + (1 if key == 'W' else 0)) % 4])
    fsl = []
    for f in F:
        cy = sum(V[k].y for k in f) / len(f)
        ny = sum(N[k].y for k in f) / len(f)
        fsl.append((S['Grass'], L['Grass']) if ny > 0.75 and cy > top * 0.75 else (S['Rock'], L['Sand']))   # grass layer on the crest only; round 4: sand grain on the faces
    g.add((V, N, F, UV), S['Rock'], L['Rock'], AO_ROCK, fsl, 1.0, vsl)
    # dry grass on the ledge treads (instanced ground cover: build_groundcover reads CLIFF_TUFTS)
    for i in range(1, len(rows) - 1):
        row, cave, ck, wf = rows[i]
        for j in (5, 9, 13):
            if ck > 0.3 or wf > 0.1 or n2(row[j].x, row[j].z, 0.25, 39 + j) < 0.05:
                continue
            p_ = row[j].lerp(row[j - 1], 0.45)
            CLIFF_TUFTS.append((round(p_.x, 3), round(p_.y, 3), round(p_.z, 3)))
    # talus: boulders along the waterline (a few on the ledges), skipping the cave mouth
    for i in range(2, len(rows) - 2, 4):
        row, cave, _, wf = rows[i]
        if cave > 0.25 or rc.random() < 0.35 or wf > 0.15:
            continue
        nx, nz = normals[i]
        base = row[2]
        rad = rc.uniform(0.8, 1.7)
        out_ = rc.uniform(0.4, 1.8)
        g.add(xform(cliff_rock(rad, rc, (1.15, 0.8, 1.0), 2, rc.uniform(0, 360)), (base.x - nx * out_, -0.35 + rad * 0.15 * rc.uniform(-1, 1), base.z - nz * out_)),
              S['Rock'], L['Rock'], AO_ROCK, None, rc.uniform(0.78, 0.95))

def build_cliffs():
    # Round 3: swept, stepped cliff faces (cliff_sweep) with a talus foot, plus a few smaller sculpted outcrops that break the skyline.
    out = []
    CLIFF_TUFTS.clear()
    for key, gname in (('W', 'Harbour_Core_Cliffs_West_Tri'), ('E', 'Harbour_Core_Cliffs_East_Tri')):
        poly, edges = CLIFF_EDGES[key]
        g = G2(gname, 'core', tri=True, rays=30, ao_dist=1.6)    # round 4: 30 rays over 1.6 m (10 over 2.5 m stained the faces in blotches)
        r = rng2('cliffs.' + key)
        top = HEAD_TOP[key]
        cliff_sweep(g, poly, edges, top, r, key)
        for k, (x, z, nx, nz) in enumerate(edge_walk(poly, edges, 21.0)):
            rad2 = r.uniform(4.0, 6.0)
            c2 = (x + nx * r.uniform(8.0, 10.5), top + head_var(key, x, z) - r.uniform(1.0, 2.5), z + nz * r.uniform(8.0, 10.5))
            geo = sculpt_mass(rad2, r, (1.0, 0.5, 0.9), 2, r.uniform(0, 360))
            if key == 'W' and math.hypot(x - FALL_EDGE[0], z - FALL_EDGE[1]) < 15.0:
                continue        # round 4: the brook's saddle stays open
            place_outside(g, geo, c2, nx, nz)
        out.append(g)
    return out

def build_cliffs_r2():
    # (round 2's sculpted masses, kept for reference; not built)
    out = []
    for key, poly, edges, gname in (('W', WEST_HEAD, range(0, 8), 'Harbour_Core_Cliffs_West_Tri'),
                                    ('E', EAST_HEAD, range(0, 8), 'Harbour_Core_Cliffs_East_Tri')):
        g = G2(gname, 'core', tri=True, rays=10, ao_dist=2.5)
        r = rng2('cliffs.' + key)
        top = HEAD_TOP[key]
        # Fewer, larger, smoother sculpted masses with broad planes (RV There Yet canyon walls), overlapping along the edge, plus a
        # second, smaller mass behind every other one so the skyline steps; a few foot rocks in the sea.
        for k, (x, z, nx, nz) in enumerate(edge_walk(poly, edges, 10.5)):
            sub = 3      # round 1: subdiv 3 everywhere (the near masses were 5,120 triangles each)
            rad = r.uniform(9.0, 12.5)
            c = (x + nx * r.uniform(3.0, 5.0), top * 0.42 + r.uniform(-0.8, 0.8), z + nz * r.uniform(3.0, 5.0))
            place_outside(g, sculpt_mass(rad, r, (1.0, (top + 1.2) / rad * 0.62, r.uniform(0.7, 0.9)), sub, r.uniform(0, 360)), c, nx, nz)
            if k % 2 == 0:
                rad2 = r.uniform(5.5, 7.5)
                c2 = (x + nx * r.uniform(8.0, 10.0), top - r.uniform(0.5, 2.0), z + nz * r.uniform(8.0, 10.0))
                place_outside(g, sculpt_mass(rad2, r, (1.0, 0.55, 0.9), 3, r.uniform(0, 360)), c2, nx, nz)
            if z > -43.0 and r.random() < 0.5:
                rad3 = r.uniform(2.0, 3.6)
                # round 2: sunk deeper (the flat base sat ~0.5 m under the surface and the water drew it as a bright cyan disc)
                c3 = (x - nx * r.uniform(2.0, 4.5), r.uniform(-1.3, -0.7), z - nz * r.uniform(2.0, 4.5))
                place_outside(g, sculpt_mass(rad3, r, (1.0, 0.7, 0.9), 2, r.uniform(0, 360)), c3, nx, nz)
        out.append(g)
    return out

# ---- ribbons conforming to the ground ----
def ground_ribbon(g, pts, half_w, slot, layer, step=0.75, lift=0.045, across=(-1.0, -0.55, 0.0, 0.55, 1.0), wobble=0.12, seed=0.0,
                  skip=None, yfn=None, ao=0.8, vsl_across=None, edge_noise=0.0):
    # round 4: vsl_across = a palette slot per across column (the colour blends across the ribbon: soft, worn edges instead of a hard
    # decal line); edge_noise = how much the outer columns wander in and out (m), so the edge is broken, not ruler-parallel
    P = [Vector((p[0], 0.0, p[1])) for p in pts]
    dense = []
    for i in range(len(P) - 1):
        a, b = P[i], P[i + 1]
        n = max(1, int(round((b - a).length / step)))
        for k in range(n):
            dense.append(a.lerp(b, k / n))
    dense.append(P[-1])
    yfn = yfn or land_y
    rows = []
    for i, p in enumerate(dense):
        t = (dense[min(i + 1, len(dense) - 1)] - dense[max(i - 1, 0)]).normalized()
        side = Vector((t.z, 0.0, -t.x))
        w = half_w * (1.0 + wobble * n2(p.x, p.z, 0.35, seed))
        row = []
        for a in across:
            ww = w + (edge_noise * n2(p.x, p.z, 0.9, seed + 7.0 * a) if abs(a) > 0.99 else 0.0)
            q = p + side * (a * ww)
            y = yfn(q.x, q.z) + lift * (1.0 - abs(a) ** 6) - 0.035 * abs(a) ** 6
            row.append(Vector((q.x, y, q.z)))
        rows.append((p, row))
    V, N, F, UV = [], [], [], []
    m = len(across)
    for i, (p, row) in enumerate(rows):
        for q in row:
            V.append(q)
            e = 0.3
            gx = (land_y(q.x + e, q.z) - land_y(q.x - e, q.z)) / (2 * e)
            gz = (land_y(q.x, q.z + e) - land_y(q.x, q.z - e)) / (2 * e)
            N.append(Vector((-gx, 1.0, -gz)).normalized())
    for i in range(len(rows) - 1):
        if skip and skip(rows[i][0]) and skip(rows[i + 1][0]):
            continue
        for k in range(m - 1):
            a = i * m + k
            q = [a, a + 1, a + m + 1, a + m]
            if (V[q[1]] - V[q[0]]).cross(V[q[2]] - V[q[0]]).y < 0:
                q = list(reversed(q))
            F.append(q); UV.append([(V[j].x, V[j].z) for j in q])
    vsl = [vsl_across[k % m] for k in range(len(V))] if vsl_across else None
    g.add((V, N, F, UV), slot, layer, ao, None, 1.0, vsl)

def ground_disc(g, cx, cz, R, slot, layer, rings=4, segs=16, lift=0.05, seed=0.0, ao=0.8, edge_slot=None):
    # round 4: edge_slot = the outer ring's palette slot (the colour fades out into the ground round the disc instead of a hard rim)
    V, N, F, UV = [], [], [], []
    V.append(Vector((cx, land_y(cx, cz) + lift, cz))); N.append(Vector((0, 1, 0)))
    for i in range(1, rings + 1):
        for s in range(segs):
            a = 2 * math.pi * s / segs
            rr = R * i / rings * (1.0 + 0.22 * n2(math.cos(a) * 3, math.sin(a) * 3, 0.9, seed))
            x, z = cx + rr * math.cos(a), cz + rr * math.sin(a)
            f = i / rings
            V.append(Vector((x, land_y(x, z) + lift * (1 - f ** 6) - 0.04 * f ** 6, z))); N.append(Vector((0, 1, 0)))
    for s in range(segs):
        F.append([0, 1 + (s + 1) % segs, 1 + s])
    for i in range(1, rings):
        for s in range(segs):
            a = 1 + (i - 1) * segs + s; b = 1 + (i - 1) * segs + (s + 1) % segs
            F.append([a, b, b + segs, a + segs])
    for k, f in enumerate(F):
        if (V[f[1]] - V[f[0]]).cross(V[f[2]] - V[f[0]]).y < 0:
            F[k] = list(reversed(f))
    for q in V:
        e = 0.3
        gx = (land_y(q.x + e, q.z) - land_y(q.x - e, q.z)) / (2 * e)
        gz = (land_y(q.x, q.z + e) - land_y(q.x, q.z - e)) / (2 * e)
    N = []
    for q in V:
        e = 0.3
        gx = (land_y(q.x + e, q.z) - land_y(q.x - e, q.z)) / (2 * e)
        gz = (land_y(q.x, q.z + e) - land_y(q.x, q.z - e)) / (2 * e)
        N.append(Vector((-gx, 1.0, -gz)).normalized())
    UV = [[(V[j].x, V[j].z) for j in f] for f in F]
    vsl = ([slot] * (1 + (rings - 1) * segs) + [edge_slot] * segs) if edge_slot is not None else None
    g.add((V, N, F, UV), slot, layer, ao, None, 1.0, vsl)

def build_paths():
    g = G2('Harbour_Core_Paths', 'core', rays=12)
    for i, (pts, hw) in enumerate(PATHS):
        # soft edge: a wider, slightly lower worn-grass band (Reed slot, Grass layer) under the sand, so grass -> olive -> sand blends
        # round 4: packed earth, darker and worn in the middle, fading through olive into the lawn at a broken edge
        ground_ribbon(g, pts, hw + 0.85, S['Reed'], L['Grass'], seed=60 + i, lift=0.028, wobble=0.25, step=1.0,
                      vsl_across=[S['Grass'], S['Reed'], S['Reed'], S['Reed'], S['Grass']], edge_noise=0.35)
        ground_ribbon(g, pts, hw, S['Wood'], L['Sand'], seed=20 + i, step=1.0, vsl_across=[S['Reed'], S['Dock'], S['WoodDark'], S['Dock'], S['Reed']],
                      edge_noise=0.22)
    # sand blowouts on the dune faces (inside the walls) and a few on the hills by the road
    r = rng2('sandpatch')
    placed = 0
    for _ in range(400):
        if placed >= 9:
            break
        x, z = r.uniform(-56, 56), r.uniform(-74.5, -62.0)
        if land_h(x, z) < 0.55:
            continue
        ground_disc(g, x, z, r.uniform(1.1, 2.0), S['Sand'], L['Sand'], seed=r.uniform(0, 50), edge_slot=S['Reed'])
        placed += 1
    return g

def stone_face(g, p0, u, length, y0, y1, nrm, seed, course=0.7, slen=(1.6, 2.6), batter=0.06, moss=0.18, inset=0.0, warm=0.12, layer=None):
    # A dry-stone face: courses of cushion stones (each a 3 x 3 vertex patch: recessed joints, a bulging centre, pillow normals). The face
    # runs from p0 along u (horizontal unit) for `length`, from y0 to y1, facing nrm (horizontal unit), leaning back by `batter` per metre.
    # Stone colours vary (grey, warm buff, brown, a mossy bottom course): the painted layer stays the Rock texture.
    r = random.Random(zlib.crc32(seed.encode('utf-8')))
    p0, u, nrm = Vector(p0), Vector(u).normalized(), Vector(nrm).normalized()
    up = Vector((0, 1, 0))
    nc = max(1, int(round((y1 - y0) / course)))
    hs = [r.uniform(0.75, 1.25) for _ in range(nc)]
    ys = [y0]
    for hh in hs:
        ys.append(ys[-1] + (y1 - y0) * hh / sum(hs))
    V, N, F, UV, FS = [], [], [], [], []
    for c in range(nc):
        ya, yb = ys[c], ys[c + 1]
        s = -r.uniform(0.0, slen[0]) if c % 2 else 0.0
        while s < length:
            sl = r.uniform(*slen)
            sa, sb = max(0.0, s), min(length, s + sl)
            s += sl
            if sb - sa < 0.12:
                continue
            bulge = r.uniform(0.045, 0.085)
            base = len(V)
            for yy, ty in ((ya, -1), ((ya + yb) / 2, 0), (yb, 1)):
                for ss, ts in ((sa, -1), ((sa + sb) / 2, 0), (sb, 1)):
                    edge = ty != 0 or ts != 0
                    d = (-0.05 if edge else bulge) + inset
                    if ty != 0 and ts != 0:
                        d -= 0.02
                    V.append(p0 + u * ss + nrm * (d - batter * (yy - y0)) + up * yy)
                    N.append((nrm - u * (0.55 * ts) + up * (0.55 * ty)).normalized())
            # round 2: each stone is its own part with its own value (vertex tint) and the soft mottled layer: round 1 put a warm slot
            # on 2 m stones under the fine triplanar cobble texture, which read as pasted beige rectangles (critics, every village shot)
            SF, SUV = [], []
            for j in range(2):
                for i in range(2):
                    a = base + j * 3 + i
                    SF.append([a, a + 1, a + 4, a + 3])
                    SUV.append([(V[k].x + V[k].z, V[k].y) for k in (a, a + 1, a + 4, a + 3)])
            k = r.random()
            # round 3 (critics round 2: "big grey slabs with the same beige rectangles"): smaller stones; no pale cream - a warm stone is
            # sand darkened to tan, the rest grey in three value bands that drift course by course; a darker, mossier base course
            band = (0.8, 0.9, 1.0)[(c + int(r.random() < 0.35)) % 3]
            if c == 0 and r.random() < moss * 1.6:
                slot = S['Reed']
            elif k < warm:
                slot = S['Rock']      # round 4: no sand-tinted stones at all (critics round 3: "pale rectangular patches remain")
            else:
                slot = S['Rock']      # round 3: no brown (Dock) stones either - they read as single wooden blocks
            tint = band * r.uniform(0.86, 1.06) * (0.72 if c == 0 else 1.0) * (0.86 if k < warm else 1.0) * (0.8 if k > 0.93 else 1.0)
            loc = V[base:base + 9]
            g.add(([loc[q] for q in range(9)], N[base:base + 9], [[q - base for q in f] for f in SF], SUV), slot,
                  L['Sand'] if layer is None else layer, 0.42, None, tint)   # round 4: the fine grain layer - each stone reads as one dressed block
            # (round 3's painted cobble layer drew a cobble pattern inside every block: "the same Voronoi cobble on walls, lane, breakwater")
            del V[base:]; del N[base:]

def ramp_rows():
    # (name, x, wall z, run, y foot (world), y top (world))
    out = []
    for name, rx_, w, run in RAMPS:
        zw = T1_Z if w == 1 else T2_Z
        lo = PLATEAU_Y if w == 1 else DUNE_BASE + T1_H
        hi = DUNE_BASE + (T1_H if w == 1 else T2_H)
        out.append((name, rx_, zw, run, lo, hi))
    return out

def parapet(g, ax, az, bx, bz, ytop, r, slot=S['Rock'], h=0.5, d=0.55, name='Parapet'):
    # a run of coping stones (rounded blocks, a little irregular) from a to b on top of a wall, with one box collider per ~6 m
    L_ = math.hypot(bx - ax, bz - az)
    if L_ < 0.3:
        return
    ux, uz = (bx - ax) / L_, (bz - az) / L_
    yaw = math.degrees(math.atan2(ux, uz))
    n = max(1, int(round(L_ / 2.8)))
    seg = L_ / n
    for k in range(n):
        cx, cz = ax + ux * seg * (k + 0.5), az + uz * seg * (k + 0.5)
        hh = h * r.uniform(0.9, 1.08)
        g.add(xform(rbox((d * r.uniform(0.94, 1.04), hh, seg - 0.04), 0.09, 1), (cx, ytop + hh / 2 - 0.08, cz), ry(yaw + r.uniform(-1.2, 1.2))),
              slot, L['Rock'], 0.6, None, r.uniform(0.8, 1.05) * (0.85 if r.random() < 0.3 else 1.0))   # round 3: stone layer, value jitter, no cream
    nb = max(1, int(round(L_ / 6.0)))
    for k in range(nb):
        t0, t1 = k / nb, (k + 1) / nb
        xs_ = [ax + (bx - ax) * t0, ax + (bx - ax) * t1]
        zs_ = [az + (bz - az) * t0, az + (bz - az) * t1]
        lo = [min(xs_) - (d / 2 if abs(uz) > 0.5 else 0.0), ytop - 0.08, min(zs_) - (d / 2 if abs(ux) > 0.5 else 0.0)]
        hi = [max(xs_) + (d / 2 if abs(uz) > 0.5 else 0.0), ytop + h * 1.08 - 0.08, max(zs_) + (d / 2 if abs(ux) > 0.5 else 0.0)]
        g.items.append(dict(name=name, collider='box', position=[round((lo[i] + hi[i]) / 2, 3) for i in range(3)],
                            footprint=[round(hi[i] - lo[i], 3) for i in range(3)], bounds_min=[round(v, 3) for v in lo],
                            bounds_max=[round(v, 3) for v in hi]))

BUTTRESS_X = []

def build_ivy():
    # Round 3 (critics: "no ivy, coping variation or planters - the walls fill the stair view"): ivy and flowering creeper hanging over the
    # coping of both retaining walls in curtains 0.6-1.9 m long, flattened clumps against the stone (detail: hidden on Low, no colliders),
    # and terracotta pots of flowers on the coping by every stair head.
    g = G2('Harbour_Detail_Ivy', 'detail', rays=6, ao_dist=0.5,
           note='Round 3: ivy curtains over the terrace walls and flower pots on the copings by the stair heads. No colliders.')
    r = rng2('ivy')
    for wall, zw, top in ((1, T1_Z, DUNE_BASE + T1_H + 0.04), (2, T2_Z, DUNE_BASE + T2_H + 0.04)):
        face = zw + RISER_HALF + 0.06
        foot = PLATEAU_Y if wall == 1 else DUNE_BASE + T1_H
        x = -WALL_X + r.uniform(1.0, 3.0)
        while x < WALL_X - 1.0:
            ok = all(abs(x - rx_) > RAMP_HW + 1.0 for _, rx_, w, _ in RAMPS if w == wall) and \
                 all(abs(x - bx) > 1.0 for w, bx in BUTTRESS_X if w == wall) and not (wall == 1 and abs(x - CREEK[0][0]) < 2.2)
            if ok:
                width = r.uniform(0.9, 2.2)
                length = r.uniform(0.6, min(1.9, top - foot - 0.4))
                flowers = r.random() < 0.35
                n = int(width * length * 5.5) + 3
                for k in range(n):
                    u = r.uniform(-width / 2, width / 2)
                    # longer strands in the middle of the curtain
                    reach = length * (1.0 - (2.0 * u / width) ** 2 * 0.6) * r.uniform(0.55, 1.0)
                    yv = top + 0.12 - r.uniform(0.0, 1.0) ** 0.7 * reach
                    rad = r.uniform(0.16, 0.27)
                    c = Vector((x + u, yv, face + 0.08 - 0.04 * (top - yv)))
                    geo = blob(rad, r, (1.1, 0.95, 0.45), subdiv=1, rough=0.35, flat=-0.9, yaw=0.0)
                    slot = (S['FoliageDark'], S['Foliage'], S['FoliageDark'], S2['FoliageAlt'])[k % 4]
                    g.add(xform(geo, c), slot, L['Foliage'], 0.6, None, r.uniform(0.85, 1.05))
                    if flowers and r.random() < 0.3:
                        g.add(xform(blob(0.06, r, (1, 0.8, 1), subdiv=1, rough=0.1), c + Vector((r.uniform(-0.1, 0.1), 0.05, 0.12))),
                              (S2['Blossom'], S2['Snow'], S['Accent'])[k % 3], L['none'], 0.75)
                # a cushion over the coping so the curtain starts from planting, not from the stone edge
                for k in range(int(width / 0.4) + 1):
                    c = Vector((x - width / 2 + 0.4 * k + r.uniform(-0.1, 0.1), top + 0.42, zw - 0.25 + r.uniform(-0.1, 0.1)))
                    g.add(xform(blob(r.uniform(0.25, 0.34), r, (1.0, 0.6, 1.0), subdiv=1, rough=0.3, flat=-0.4, yaw=r.uniform(0, 360)), c),
                          S['FoliageDark'] if k % 2 else S['Foliage'], L['Foliage'], 0.6)
                x += width + r.uniform(2.0, 6.0)
            else:
                x += 0.7
    # flower pots on the coping by every stair head
    for name, rx_, w, run in RAMPS:
        zw = T1_Z if w == 1 else T2_Z
        top = DUNE_BASE + (T1_H if w == 1 else T2_H) + 0.04
        for s_ in (-1, 1):
            px, pz, py = rx_ + s_ * (RAMP_HW + 0.3), zw - 0.35, top + 0.42
            add_lathe(g, (px, py, pz), [(0, 0.15), (0.32, 0.2), (0.36, 0.22), (0.4, 0.0)], S['Roof'], L['none'], 0.55, sides=10, cap0=True)
            g.add(xform(blob(0.26, r, (1.0, 0.8, 1.0), subdiv=2, rough=0.3), (px, py + 0.5, pz)), S['Foliage'], L['Foliage'], 0.6)
            for j in range(4):
                g.add(xform(blob(0.065, r, (1, 0.8, 1), subdiv=1, rough=0.1),
                            (px + 0.18 * math.cos(j * 1.7), py + 0.66, pz + 0.18 * math.sin(j * 1.7))),
                      (S['Accent'], S2['Blossom'], S2['Snow'], S['Roof'])[(j + w) % 4], L['none'], 0.75)
    return g

def build_stone_wall():
    # Round 1 (2026-10-15): the terrace retaining walls (they replace round 0's low field wall), the stone stairs cut into them, the coping
    # parapets on top (a 0.5 m lip that keeps players on the terrace edge; box colliders, Shore). The walkable shape itself is
    # Harbour_Dunes_Col (riser faces ~80 deg, stair ramps <= 23.2 deg); these stones only dress it.
    g1 = G2('Harbour_Core_Walls_Tri', 'core', tri=True, rays=12, ao_dist=0.45)
    g2 = G2('Harbour_Core_Walls2_Tri', 'core', tri=True, rays=12, ao_dist=0.45,
            note='Round 3: retaining wall 2 (the chapel terrace) stone faces and every stair (cheeks, steps, iron handrails); wall 1 and every '
                 'parapet, buttress and lamp are Harbour_Core_Walls_Tri. Visual only: the walkable shape is Harbour_Dunes_Col.')
    g = g1
    r = rng2('stonewall')
    rr = ramp_rows()
    BUTTRESS_X.clear()
    for wall, zw, lo_y, hi_y in ((1, T1_Z, PLATEAU_Y - 0.25, DUNE_BASE + T1_H + 0.04), (2, T2_Z, DUNE_BASE + T1_H - 0.2, DUNE_BASE + T2_H + 0.04)):
        g = g1 if wall == 1 else g2
        ramps = sorted([(x, run) for name, x, z, run, lo, hi in rr if z == zw])
        # main face in pieces between the stair openings, a little past the walls at both ends (it tucks into the outside banks)
        xa = -WALL_X - 3.5
        for x, run in ramps + [(None, None)]:
            xb = (x - RAMP_HW) if x is not None else WALL_X + 3.5
            stone_face(g, (xb, 0.0, zw + RISER_HALF + 0.06), (-1, 0, 0), xb - xa, lo_y, hi_y, (0, 0, 1), 'wall%d.%d' % (wall, int(round(xa * 10))),
                       course=0.46, slen=(0.55, 1.15), warm=0.0 if wall == 1 else 0.3)
            if x is not None:
                xa = x + RAMP_HW
        # parapet along the top edge, open at the stairs, and along both stair cheeks (round 3: every box item stays in Walls_Tri: LandBoxModels)
        gs = g
        g = g1
        pa = -WALL_X
        for x, run in ramps + [(None, None)]:
            pb = (x - RAMP_HW - 0.3) if x is not None else WALL_X
            parapet(g, pa, zw - 0.05, pb, zw - 0.05, hi_y - 0.04, r, name='Parapet%d' % wall)
            if x is not None:
                pa = x + RAMP_HW + 0.3
                for s_ in (-1, 1):
                    parapet(g, x + s_ * (RAMP_HW + 0.28), zw - 0.35, x + s_ * (RAMP_HW + 0.28), zw + RISER_HALF - run, hi_y - 0.04, r,
                            name='StairParapet%d' % wall)
        g = g2      # round 3: every stair (cheeks, steps, rails) is in Walls2_Tri (each FBX < 21k)
        # the stairs: cheek faces and the steps
        for x, run in ramps:
            z0 = zw + RISER_HALF
            for s_ in (-1, 1):
                stone_face(g, (x + s_ * RAMP_HW, 0.0, z0 + 0.06), (0, 0, -1), run + 0.06, lo_y, hi_y, (-s_, 0, 0),
                           'cheek%d.%d.%d' % (wall, int(x), s_), course=0.44, slen=(0.5, 1.0), warm=0.0 if wall == 1 else 0.3)
            foot = PLATEAU_Y if wall == 1 else DUNE_BASE + T1_H
            top = hi_y - 0.04
            n = max(1, int(round((top - foot) / 0.2)))
            rise, tread = (top - foot) / n, run / n
            for k in range(n):
                zc = z0 - tread * (k + 0.5)
                ytop = foot + rise * (k + 1) + 0.02
                yb = foot - 0.35
                g.add(xform(rbox((2 * RAMP_HW - 0.04, ytop - yb, tread + 0.05), 0.045, 1), (x + r.uniform(-0.02, 0.02), (ytop + yb) / 2, zc)),
                      S['Sand'] if k % 3 else S['Rock'], L['Rock'], 0.55, None, 0.82 if k % 3 else 1.0)
            # round 3: an iron handrail up each side of the stair (visual; the stair cheeks' parapets are the colliders)
            for s_ in (-1, 1):
                rx0 = x + s_ * (RAMP_HW - 0.18)
                a = Vector((rx0, foot + 0.95, z0 - 0.15))
                b = Vector((rx0, top + 0.95, z0 - run + 0.3))
                g.add(tube([a, b], 0.03, 6), S['WoodDark'], L['none'], 0.5, None, 0.6)
                for t in (0.06, 0.5, 0.94):
                    q = a.lerp(b, t)
                    g.add(tube([Vector((q.x, q.y - 0.95, q.z)), q], 0.022, 5), S['WoodDark'], L['none'], 0.5, None, 0.6)
    # Round 2: break the long uniform runs - a battered buttress every ~9 m (stone courses of its own, a coping cap, a Shore box collider), a
    # drain spout between some of them, and a lamp at the head of every stair.
    g = g1
    for wall, zw, lo_y, hi_y in ((1, T1_Z, PLATEAU_Y - 0.25, DUNE_BASE + T1_H + 0.04), (2, T2_Z, DUNE_BASE + T1_H - 0.2, DUNE_BASE + T2_H + 0.04)):
        face = zw + RISER_HALF + 0.06
        foot = PLATEAU_Y if wall == 1 else DUNE_BASE + T1_H
        xs = []
        x = -WALL_X + 4.0 + r.uniform(0, 3)
        while x < WALL_X - 3.0:
            if all(abs(x - rx_) > RAMP_HW + 2.6 for _, rx_, w, _ in RAMPS if w == wall) and not (wall == 1 and abs(x - CREEK[0][0]) < 3.5)                     and tree_clear(x, face + 0.4, 1.6):
                xs.append(x)
            x += r.uniform(8.0, 10.5)
        BUTTRESS_X.extend((wall, x) for x in xs)      # round 3: the ivy keeps clear of them
        for k, x in enumerate(xs):
            hgt = hi_y - foot - r.uniform(0.25, 0.6)
            g.mark()
            w_, d_ = 0.95, 0.55
            yb, yt = foot - 0.1, foot + hgt
            stone_face(g, (x + w_ / 2, 0.0, face + d_), (-1, 0, 0), w_, yb, yt, (0, 0, 1), 'butt%d.%d' % (wall, k),
                       course=0.5, slen=(0.95, 1.0), batter=0.1, moss=0.15, warm=0.0 if wall == 1 else 0.3)
            for sx in (-1, 1):
                stone_face(g, (x + sx * w_ / 2, 0.0, face), (0, 0, 1), d_, yb, yt, (sx, 0, 0), 'butts%d.%d.%d' % (wall, k, sx),
                           course=0.5, slen=(0.6, 0.7), batter=0.0, moss=0.15, warm=0.0 if wall == 1 else 0.3)
            g.add(xform(rbox((1.05, 0.16, 0.66), 0.05, 1), (x, foot + hgt + 0.02, face + 0.28), rx(-12)), S['Sand'] if wall == 2 else S['Rock'],
                  L['Cloth'], 0.6)
            it = g.solid('Buttress%d_%d' % (wall, k))
            it['bounds_min'][2] = round(face - 0.3, 3)
            it['footprint'][2] = round(it['bounds_max'][2] - it['bounds_min'][2], 3)
            it['position'][2] = round((it['bounds_max'][2] + it['bounds_min'][2]) / 2, 3)
            if k + 1 < len(xs) and k % 2 == 0:
                dx = (x + xs[k + 1]) / 2
                g.add(xform(lathe([(0.0, 0.07), (0.32, 0.07), (0.34, 0.09), (0.36, 0.0)], 7, cap0=True, cap1=False)[0], (dx, foot + 0.55, face - 0.05), rx(90)),
                      S['WoodDark'], L['none'], 0.4)
    for name, x, zw, run, lo, hi in rr:
        lamp_post(g, x + RAMP_HW + 0.75, zw - 0.62, 'StairLamp_' + name, y=hi)
    g.extra['stairs'] = [dict(name=name, x=x, wall_z=z, run=run, foot_y=round(lo, 3), top_y=round(hi, 3)) for name, x, z, run, lo, hi in rr]
    return [g1, g2]

def build_road():
    # The coast road behind the dunes (outside the walls): a dirt track on its terrace, a wooden bridge over the river, telephone poles and
    # a post-and-wire farm fence receding along it. Backdrop: no colliders.
    g = G2('Harbour_Core_Road', 'core', rays=8, ao_dist=1.5)
    pts = road_pts()
    over_river = lambda p: poly_dist(p.x, p.z, RIVER) < river_half_width(p.z) + 2.0
    # round 1: inside the walls the road is the village's upper lane: a narrower grey cobbled lane; outside, the sand coast road
    inside = lambda p: inside_walls(p.x, p.z, 0.5)
    ground_ribbon(g, pts, 2.3, S['Dock'], L['Sand'], step=2.0, lift=0.07, seed=40, skip=lambda p: over_river(p) or inside(p), wobble=0.05,
                  vsl_across=[S['Reed'], S['Dock'], S['WoodDark'], S['Dock'], S['Reed']])
    # round 3: inside the walls a 2.9 m cobbled lane over a worn dirt band (4.5 m) that frays into the grass (critics: "narrow it, let the
    # cobble fade to dirt at the edges"); the village square's cobbles are a disc in build_upper_village
    ground_ribbon(g, pts, 1.8, S['Reed'], L['Grass'], step=1.0, lift=0.068, seed=42, skip=lambda p: not inside(p), wobble=0.22)
    ground_ribbon(g, pts, 1.45, S['Rock'], L['Rock'], step=1.0, lift=0.082, seed=41, skip=lambda p: not inside(p), wobble=0.1)
    # bridge
    best = min(pts, key=lambda p: poly_dist(p[0], p[1], RIVER))
    bx = best[0]
    bz = road_z(bx)
    deck_y = DUNE_BASE + road_h(bx) + 0.07
    t = Vector((1.0, 0.0, (road_z(bx + 1) - road_z(bx - 1)) / 2)).normalized()
    yaw = math.degrees(math.atan2(t.x, t.z))
    span = 2 * river_half_width(bz) + 7.0
    rot = ry(yaw)
    c = Vector((bx, deck_y - 0.15, bz))
    g.add(xform(rbox((4.8, 0.3, span), 0.06), c, rot), S['Wood'], L['Wood'], 0.5)
    for s in (-1, 1):
        for k in range(5):
            p = c + rot @ Vector((s * 2.3, 0.65, -span / 2 + span * k / 4))
            g.add(xform(rbox((0.14, 1.0, 0.14), 0.03, 1), p, rot), S['WoodDark'], L['Wood'], 0.5)
        g.add(xform(rbox((0.12, 0.12, span), 0.03), c + rot @ Vector((s * 2.3, 1.15, 0)), rot), S['WoodDark'], L['Wood'], 0.5)
        for k in (-1, 1):   # stone abutments
            p = c + rot @ Vector((0, -1.4, k * (span / 2 - 0.8)))
            g.add(xform(rbox((5.2, 3.0, 1.8), 0.3), p, rot), S['Rock'], L['Rock'], 0.6)
    for k in (-1, 1):       # trestle legs into the river
        p = c + rot @ Vector((0, -1.6, k * span * 0.18))
        g.add(xform(rbox((4.2, 3.2, 0.35), 0.08), p, rot), S['WoodDark'], L['Wood'], 0.5)
    g.extra['bridge'] = dict(x=round(bx, 2), z=round(bz, 2), deck_y=round(deck_y, 2))
    # telephone poles on the south side of the road, wires sagging between crossarms
    r = rng2('poles')
    poles = []
    x = ROAD_X0 + 6.0
    while x < ROAD_X1 - 2.0:
        z = road_z(x) - 3.8
        if poly_dist(x, z, RIVER) > river_half_width(z) + 3.0 and not inside_walls(x, z, 1.0):   # round 1: none on the village lane
            poles.append((x, z))
        x += 20.0
    for (x, z) in ROAD_EAST[1:]:
        poles.append((x + 2.5, z - 3.6))
    tops = []
    for (x, z) in poles:
        y0 = land_y(x, z)
        lean = r.uniform(-2.0, 2.0)
        rot = rz(lean)
        add_lathe(g, (x, y0 - 0.3, z), [(0, 0.15), (6.6, 0.11), (6.72, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=6, rot=rot)
        arm_c = Vector((x, y0 + 5.9, z)) + rot @ Vector((0, 0.0, 0))
        g.add(xform(rbox((1.6, 0.12, 0.12), 0.03, 1), arm_c, rot), S['WoodDark'], L['Wood'], 0.5)
        ends = []
        for s in (-0.7, 0.7):
            e = arm_c + rot @ Vector((s, 0.12, 0))
            add_lathe(g, e - Vector((0, 0.06, 0)), [(0, 0.04), (0.12, 0.045), (0.14, 0.0)], S['Sand'], L['none'], 0.6, sides=4)
            ends.append(e + Vector((0, 0.06, 0)))
        tops.append(ends)
    for a, b in zip(tops[:-1], tops[1:]):
        for k in range(2):
            pa, pb = a[k], b[k]
            if (pb - pa).length > 30:
                continue
            g.add(tube(rope_line(pa, pb, 0.6, n=8), 0.025, 3, caps=False), S['WoodDark'], L['none'], 0.5)
    build_hill_track(g)      # round 1
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Backdrop (outside the walls, no colliders): forest on the hills, a farm and fields, the lighthouse on the east headland, islands and sea
# stacks, far boats, the mafia's sleek dark boat on the horizon, a parked black car at the bend of the coast road, river channel markers.
# Placed 60-160 m from the pier (daytime exp2 fog 0.008: ~50% at 100 m, ~8% at 200 m); boats stay inside the water extent (x, z within 120).
# ---------------------------------------------------------------------------------------------------------------------------------------
LIGHTHOUSE = (92.0, 41.0)
ISLANDS = [(-48.0, 108.0, 22.0, 7.5, 0.0), (36.0, 116.0, 16.0, 5.5, 40.0), (-150.0, 72.0, 34.0, 9.0, 10.0), (150.0, 84.0, 38.0, 10.0, -20.0)]
STACKS = [(-31.0, 78.0, 4.6, 2.5), (-24.5, 83.0, 2.6, 2.0), (54.0, 73.0, 5.2, 2.3), (61.0, 79.0, 2.4, 2.4), (10.0, 98.0, 3.4, 2.2)]
FAR_BOATS = [('Trawler', (-72.0, 0.0, 96.0), 62.0), ('Sailboat', (58.0, 0.0, 100.0), -35.0)]
MAFIA_BOAT = ('MafiaBoat', (16.0, 0.0, 112.0), -88.0)
BLACK_CAR = (66.5, -91.4, 28.0)          # x, z, yaw: parked at the east bend of the coast road
CHANNEL_MARKERS = [(-72.5, -36.5, 'G'), (-87.5, -34.5, 'R'), (-71.0, -26.0, 'G'), (-89.0, -24.5, 'R')]

def taper_tube(pts, radii, sides=6):
    # a swept circle along a polyline with a radius per point (parallel-transport frames), capped at the top
    pts = [Vector(p) for p in pts]
    n = len(pts)
    T = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    ref = Vector((1, 0, 0)) if abs(T[0].x) < 0.9 else Vector((0, 0, 1))
    nrm = (ref - T[0] * ref.dot(T[0])).normalized()
    V, N, F, UV = [], [], [], []
    for i in range(n):
        if i > 0:
            nrm = (nrm - T[i] * nrm.dot(T[i])).normalized()
        bb = nrm.cross(T[i]).normalized()
        for s_ in range(sides + 1):
            a = 2 * math.pi * s_ / sides
            d = nrm * math.cos(a) + bb * math.sin(a)
            V.append(pts[i] + d * radii[i]); N.append(d)
    for i in range(n - 1):
        for s_ in range(sides):
            a = i * (sides + 1) + s_
            F.append([a, a + sides + 1, a + sides + 2, a + 1])
            UV.append([(s_ / sides, i), (s_ / sides, i + 1), ((s_ + 1) / sides, i + 1), ((s_ + 1) / sides, i)])
    ci = len(V); V.append(pts[-1] + T[-1] * radii[-1] * 0.5); N.append(T[-1])
    base = (n - 1) * (sides + 1)
    for s_ in range(sides):
        F.append([base + s_, base + s_ + 1, ci]); UV.append([(0, 0), (1, 0), (0.5, 1)])
    return V, N, F, UV

def leaf_clump(rad, r, scale, sub):
    # a soft, lumpy leaf clump (ragged silhouette): an icosphere pushed by two noise octaves, flattened underneath
    return blob(rad, r, scale, subdiv=sub, rough=0.32, flat=-0.45, yaw=r.uniform(0, 360))

# ---- Round 2 (2026-10-16): trees rebuilt from many small clumps with crown-shaped normals ----
# Round 1's trees were a few big faceted clumps (critics: "stacked hex pancakes", "melon hedges"). A crown is now 12-20 small lumpy clumps
# scattered over an ellipsoid (leafy) or a spiral cone (pine), and every clump vertex gets a normal blended toward the CROWN's outward
# direction (the stylised-foliage trick in RV There Yet / PEAK): the toon band then reads one soft volume with a ragged, lumpy silhouette,
# so 80-triangle clumps do not show their facets. Slots run light on top (FoliageAlt / PineLight) to dark underneath (FoliageDark /
# PineDark), on top of the shader's top tint and the baked AO.
S2 = dict(Snow=11, PineLight=12, PineDark=13, Blossom=14, FoliageAlt=15)

def crown_normals(geo, c, k=0.72, squash=(1.0, 1.0, 1.0), up_bias=0.0):
    # blend each vertex normal toward the direction from the crown centre c (in a squashed frame, so an ellipsoid crown shades as one)
    V, N, F, UV = geo
    c = Vector(c)
    N2 = []
    for v, n in zip(V, N):
        d = Vector(((v[0] - c.x) / squash[0], (v[1] - c.y) / squash[1] + up_bias, (v[2] - c.z) / squash[2]))
        if d.length < 1e-6:
            N2.append(Vector(n)); continue
        N2.append((Vector(n) * (1.0 - k) + d.normalized() * k).normalized())
    return V, N2, F, UV

def clump_geo(rad, r, scale, sub=1, rough=0.42):
    return blob(rad, r, scale, subdiv=sub, rough=rough, flat=-0.5, yaw=r.uniform(0, 360))

# ---- Round 2b (2026-10-16): painterly crown cards (user unlock "painterly crown trees") ----
# Critics (round 1, every lens): the trees are "stacked hex pancakes" and "watermelon blobs" - the single biggest cheapness signal up close.
# A crown is now a set of alpha-clipped painted cards (Art/Blender/fg_crowncards.py -> Assets/_Project/Content/Textures/T_CrownCards.png;
# Unity M_Toon_Foliage = FishGame/Toon with _ALPHATEST_ON, both faces drawn) around a few dark inner clumps that keep a solid core where the
# cards thin out. The cards go in their own "<name>_Cards" models (the material is per model). Their normals are the crown's, so the whole
# crown shades as one soft volume under the toon ramp (the PEAK / RV There Yet foliage trick), and their ao is painted per vertex (darker low
# and deep in the crown): the cards never enter the ao bake, so the trees cast no black discs on the ground. Card models are core (Low draws
# them) and carry no colliders. A card is 2-4 triangles where a clump was 80: the crowns cost about a third of round 1's.
CARD_UV = {'leaf': (0.0, 0.5, 0.5, 1.0), 'pine': (0.5, 0.5, 1.0, 1.0), 'frond': (0.0, 0.0, 0.5, 0.5),
           'grass': (0.5, 0.0, 0.75, 0.5), 'flower': (0.75, 0.0, 1.0, 0.5)}
CARD_MATERIAL = 'M_Toon_Foliage'
CARD_SUFFIX = '_Cards'

def cards_group(name, local=False, note=None):
    g = G2(name, 'core', local=local, rays=0, material=CARD_MATERIAL, note=note)
    g.VAO = []
    return g

def card(gc, c, u, v, w, h, cell, slot, nfn, aofn, tint=1.0, cols=1, rows=1, disp=None):
    # One card centred at c, width w along the unit u, height h along the unit v; uv = the atlas cell (inset half a texel). cols x rows
    # splits it into a grid (more normals across a wide card); disp(s, t) -> a world offset per grid vertex (s, t in 0..1) bends it (the
    # pine tiers' drooping umbrellas). nfn(p) -> the vertex normal, aofn(p) -> its painted ao (0..1).
    c, u, v = Vector(c), Vector(u).normalized(), Vector(v).normalized()
    u0, v0, u1, v1 = CARD_UV[cell]
    e = 0.5 / 1024
    u0, v0, u1, v1 = u0 + e, v0 + e, u1 - e, v1 - e
    P, T = [], []
    for j in range(rows + 1):
        t = j / rows
        for i in range(cols + 1):
            s = i / cols
            q = c + u * (w * (s - 0.5)) + v * (h * (t - 0.5))
            if disp is not None:
                q = q + disp(s, t)
            P.append(q)
            T.append((u0 + (u1 - u0) * s, v0 + (v1 - v0) * t))
    F, UV = [], []
    for j in range(rows):
        for i in range(cols):
            k = j * (cols + 1) + i
            q = [k, k + 1, k + cols + 2, k + cols + 1]
            F.append(q)
            UV.append([T[x] for x in q])
    gc.add((P, [nfn(p) for p in P], F, UV), slot, L['none'], 0.0, None, tint)
    gc.VAO += [clamp(aofn(p), 0.0, 1.0) for p in P]

def card_frame(nn, r, roll=0.6):
    # an upright-ish card frame (u, v) for a card facing nn, rolled by up to `roll` radians (v keeps the painted crown the right way up)
    up = Vector((0, 1, 0))
    u = nn.cross(up)
    if u.length < 1e-3:
        u = Vector((1, 0, 0))
    u.normalize()
    v = u.cross(nn).normalized()
    if v.y < 0:
        v, u = -v, -u
    a = r.uniform(-roll, roll)
    return u * math.cos(a) + v * math.sin(a), v * math.cos(a) - u * math.sin(a)

def crown_cards_round(gc, C, rx_, ry_, r, slot, alt, dark, near=True, n=None):
    # An ellipsoid crown: two crossed core cards through it, then 22-28 (near) or 10 (far) leaf-cluster cards on its shell, facing out with
    # a random tilt and roll; light slot on top, dark underneath.
    C = Vector(C)
    up = Vector((0, 1, 0))
    def nfn(p):
        d = Vector(((p.x - C.x) / rx_, (p.y - C.y) / (ry_ * 1.15) + 0.25, (p.z - C.z) / rx_))
        return d.normalized() if d.length > 1e-6 else up
    def aofn(p):
        hgt = (p.y - (C.y - ry_)) / (2.0 * ry_)
        rad = math.sqrt(((p.x - C.x) / rx_) ** 2 + ((p.z - C.z) / rx_) ** 2 + ((p.y - C.y) / ry_) ** 2)
        return 0.4 + 0.36 * smoothstep(0.0, 1.0, hgt) + 0.3 * smoothstep(0.3, 1.0, rad)
    a0 = r.uniform(0, math.pi)
    for k in range(2):
        a = a0 + k * math.pi / 2
        card(gc, C + Vector((0, 0.04 * ry_, 0)), (math.cos(a), 0, math.sin(a)), up, 2.0 * rx_ * 0.9, 2.0 * ry_ * 1.05, 'leaf', dark if k else slot,
             nfn, aofn, cols=2)
    n = n if n is not None else (r.randint(22, 28) if near else 10)
    size = 1.0 * rx_ if near else 1.25 * rx_
    for i in range(n):
        yv = 1.0 - 1.75 * (i + 0.5) / n
        rad_xz = math.sqrt(max(0.0, 1.0 - yv * yv))
        a = i * 2.39996 + r.uniform(-0.4, 0.4)
        d = Vector((math.cos(a) * rad_xz, yv, math.sin(a) * rad_xz))
        p = C + Vector((d.x * rx_, d.y * ry_, d.z * rx_)) * r.uniform(0.66, 0.9)
        nn = (d + Vector((r.uniform(-1, 1), r.uniform(-0.5, 0.8), r.uniform(-1, 1))) * 0.7).normalized()
        u, v = card_frame(nn, r)
        s = size * r.uniform(0.72, 1.08)
        sl = alt if yv > 0.42 else (dark if yv < -0.25 or i % 6 == 0 else slot)
        card(gc, p, u, v, s, s, 'leaf', sl, nfn, aofn)

def crown_cards_pine(gc, trunk_at, base, h, r, slot, light, dark, near=True):
    # A conifer: 3 (near) or 2 (far) crossed full-height silhouette cards along the (bent) trunk, plus drooping frond-burst tiers (5 near,
    # 3 far) that read from above and break the crossed planes from the side. Cone normals: out from the trunk axis, tipped up.
    base = Vector(base)
    ax0, ax1 = trunk_at(0.12), trunk_at(1.03)
    axis = (ax1 - ax0).normalized()
    mid, H = (ax0 + ax1) / 2, (ax1 - ax0).length
    def axis_at(p):
        return trunk_at(clamp((p.y - base.y) / h, 0.0, 1.05))
    def nfn(p):
        a = axis_at(p)
        o = Vector((p.x - a.x, 0.0, p.z - a.z))
        if o.length < 1e-4:
            return Vector((0, 1, 0))
        return (o.normalized() + Vector((0, 0.7, 0))).normalized()
    def aofn(p):
        t = clamp((p.y - base.y) / h, 0.0, 1.0)
        a = axis_at(p)
        o = math.hypot(p.x - a.x, p.z - a.z) / (0.3 * h * (1.0 - t) + 0.06 * h)
        return 0.38 + 0.42 * smoothstep(0.08, 0.95, t) + 0.22 * smoothstep(0.15, 1.0, o)
    def nfn_sil(p):
        # the crossed planes contain the axis, so a radial normal would lie in the card (edge-on to a viewer facing it: all rim): mostly up
        a = axis_at(p)
        o = Vector((p.x - a.x, 0.0, p.z - a.z))
        return (o.normalized() * 0.45 + Vector((0, 1, 0))).normalized() if o.length > 1e-4 else Vector((0, 1, 0))
    m = 3 if near else 2
    a0 = r.uniform(0, math.pi)
    for k in range(m):
        a = a0 + k * math.pi / m
        u = Vector((math.cos(a), 0, math.sin(a)))
        u = (u - axis * u.dot(axis)).normalized()
        card(gc, mid, u, axis, 0.6 * h, H, 'pine', slot if k % 2 == 0 else dark, nfn_sil, aofn, cols=2)
    tiers = 6 if near else 3
    for k in range(tiers):
        t = 0.24 + 0.66 * k / (tiers - 1)
        R = h * (0.29 * (1.0 - t) + 0.07)
        c = trunk_at(t) - Vector((0, 0.03 * h, 0))
        a = r.uniform(0, 2 * math.pi)
        u = Vector((math.cos(a), r.uniform(-0.14, 0.14), math.sin(a))).normalized()
        v = Vector((-math.sin(a), r.uniform(-0.14, 0.14), math.cos(a))).normalized()
        sl = light if t > 0.62 else (dark if t < 0.42 else slot)
        droop = 0.55 * R
        # an umbrella: the centre raised, the rim hanging (a cone skirt from the side, the frond burst from above)
        card(gc, c - Vector((0, 0.25 * droop, 0)), u, v, 2.3 * R, 2.3 * R, 'frond', sl, nfn, aofn, cols=2, rows=2,
             disp=lambda s_, t_, d=droop: Vector((0, d * max(0.0, 1.0 - math.hypot(2 * s_ - 1, 2 * t_ - 1)), 0)))

def tree_pine(g, base, h, slot, sides=7, lean=(0.0, 0.0), key=None, gc=None):
    # A coastal pine: a tapering, slightly bent trunk and a spiral of 13-19 small drooping clumps (near) narrowing to a ragged tip, so the
    # silhouette is a soft cone with lumps and gaps, not stacked tiers. The cone normals make it shade as one volume. sides <= 6: far LOD.
    base = Vector(base)
    r = rng2(key or 'pine2.%d.%d' % (int(round(base.x * 10)), int(round(base.z * 10))))
    far = sides <= 6
    light = S2['PineLight'] if slot != S['Foliage'] else S['Foliage']
    dark = S2['PineDark']
    bend = Vector((-math.sin(math.radians(lean[1])), 0.0, math.sin(math.radians(lean[0]))))
    def trunk_at(t):
        return base + Vector((0, h * t, 0)) + bend * (h * 0.32 * t * t)
    pts = [trunk_at(t) for t in ((-0.03, 0.3, 0.6, 0.97) if gc is None else (-0.03, 0.25, 0.5, 0.74))]   # card pines: the trunk stops inside
    rads = [0.055 * h, 0.04 * h, 0.026 * h, 0.01 * h]
    g.add(taper_tube(pts if not far else pts[::3], rads if not far else rads[::3], 6 if not far else 4), S['WoodDark'], L['Bark'], 0.6)
    if gc is not None:
        # round 2b: a dark inner volume along the trunk (the core where the cards thin out), the crown as cards in gc
        crown_cards_pine(gc, trunk_at, base, h, r, slot, light, dark, near=not far)
        return
    n = 9 if far else r.randint(13, 16)
    a = r.uniform(0, 2 * math.pi)
    for i in range(n):
        t = 0.24 + 0.7 * (i / (n - 1)) ** 0.9
        R = h * (0.26 * (1.0 - t) ** 0.85 + 0.05)
        a += math.radians(137.5) + r.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), 0.0, math.sin(a)))
        if bend.length > 1e-6:
            d = (d + bend.normalized() * 0.4).normalized()
        c = trunk_at(t)
        p = c + d * (R * r.uniform(0.45, 0.62)) - Vector((0, 0.03 * h, 0))
        rad = R * r.uniform(0.78, 0.95) + 0.1
        geo = clump_geo(rad, r, (1.0, 0.7, 1.0), 1 if far else 2, 0.32)
        geo = xform(geo, p, ry(math.degrees(math.atan2(d.x, d.z))) @ rx(18))
        # cone normals: outward from the trunk axis, tipped up (a 0.6 rise per unit out)
        V, N, F, UV = geo
        N2 = []
        for v, nn in zip(V, N):
            ax = trunk_at(clamp((v[1] - base.y) / h, 0.0, 1.0))
            o = Vector((v[0] - ax.x, 0.0, v[2] - ax.z))
            o = o.normalized() if o.length > 1e-5 else d
            cn = (o + Vector((0, 0.6, 0))).normalized()
            N2.append((Vector(nn) * 0.3 + cn * 0.7).normalized())
        geo = (V, N2, F, UV)
        s_ = light if t > 0.62 else (dark if t < 0.4 or i % 4 == 0 else slot)
        g.add(geo, s_, L['Foliage'], 0.72)
    tip = trunk_at(1.0)
    geo = clump_geo(0.07 * h + 0.08, r, (0.8, 1.7, 0.8), 1, 0.3)
    g.add(crown_normals(xform(geo, tip - Vector((0, 0.03 * h, 0))), tip - Vector((0, 0.25 * h, 0)), 0.6), light, L['Foliage'], 0.55)

def tree_round(g, base, h, r, slot, sub=1, trunk=True, key=None, gc=None):
    # A leafy tree: a forked tapering trunk carrying 13-18 small lumpy clumps spread over an ellipsoid crown (gaps between them read as
    # leaf masses), all shaded with the crown's normals. sub >= 2: near (more clumps); sub 1: mid/far (9 clumps).
    base = Vector(base)
    rr = rng2(key or 'leafy2.%d.%d' % (int(round(base.x * 10)), int(round(base.z * 10))))
    near = sub >= 2
    C = base + Vector((0, 0.62 * h, 0))
    rx_, ry_ = 0.42 * h, 0.3 * h
    if trunk:
        g.add(taper_tube([base - Vector((0, 0.05 * h, 0)), base + Vector((0, 0.3 * h, 0)), base + Vector((0.03 * h, 0.55 * h, 0))],
                         [0.08 * h, 0.06 * h, 0.045 * h], 6), S['WoodDark'], L['Bark'], 0.6)
    alt = S2['FoliageAlt'] if slot == S['Foliage'] else S['Foliage']
    if gc is not None:
        # round 2b: 4 (near) or 2 dark inner clumps on short branches, the crown as cards in gc
        for i in range(4 if near else 2):
            a = i * 2.39996 + rr.uniform(-0.3, 0.3)
            p = C + Vector((math.cos(a) * rx_ * 0.4, rr.uniform(-0.15, 0.25) * ry_, math.sin(a) * rx_ * 0.4))
            geo = xform(clump_geo(0.48 * rx_, rr, (1.0, 0.8, 1.0), 2 if near else 1, 0.3), p)
            g.add(crown_normals(geo, C, 0.7, (rx_, ry_ * 1.2, rx_)), S['FoliageDark'], L['Foliage'], 0.5)
            if trunk and near:
                g.add(taper_tube([base + Vector((0.03 * h, 0.5 * h, 0)), p], [0.03 * h, 0.014 * h], 4), S['WoodDark'], L['Bark'], 0.6)
        crown_cards_round(gc, C, rx_, ry_, rr, slot, alt, S['FoliageDark'], near)
        return
    n = rr.randint(13, 16) if near else 9
    for i in range(n):
        # Fibonacci directions over the upper 80% of the ellipsoid, jittered
        yv = 1.0 - 1.7 * (i + 0.5) / n
        rad_xz = math.sqrt(max(0.0, 1.0 - yv * yv))
        a = i * 2.39996 + rr.uniform(-0.35, 0.35)
        d = Vector((math.cos(a) * rad_xz, yv, math.sin(a) * rad_xz))
        p = C + Vector((d.x * rx_, d.y * ry_, d.z * rx_)) * rr.uniform(0.62, 0.8)
        rad = h * rr.uniform(0.17, 0.22)
        geo = xform(clump_geo(rad, rr, (1.0, 0.82, 1.0), 2 if near else 1, 0.3), p)
        geo = crown_normals(geo, C - Vector((0, 0.08 * h, 0)), 0.72, (rx_, ry_ * 1.2, rx_))
        s_ = alt if yv > 0.45 else (S['FoliageDark'] if yv < -0.15 or i % 5 == 0 else slot)
        g.add(geo, s_, L['Foliage'], 0.72)
        if trunk and near and i % 3 == 0 and yv < 0.5:
            g.add(taper_tube([base + Vector((0.03 * h, 0.5 * h, 0)), p - (p - C) * 0.35], [0.03 * h, 0.014 * h], 4), S['WoodDark'], L['Bark'], 0.6)

def hedge_row(g, pts, r, h=0.8, w=0.75, step=0.4, slot=None, flowers=0.0):
    # A clipped hedge: overlapping small clumps along a polyline, the top cut flat at h (a soft dome above it), shaded with normals blended
    # out from the row's centre line. pts: [(x, z)], ground from land_y.
    slot = S['FoliageDark'] if slot is None else slot
    segs = list(zip(pts[:-1], pts[1:]))
    for (ax, az), (bx, bz) in segs:
        L_ = math.hypot(bx - ax, bz - az)
        if L_ < 0.2:
            continue
        ux, uz = (bx - ax) / L_, (bz - az) / L_
        nseg = max(1, int(round(L_ / step)))
        for k in range(nseg + 1):
            t = k / nseg
            x, z = ax + (bx - ax) * t + r.uniform(-0.05, 0.05), az + (bz - az) * t + r.uniform(-0.05, 0.05)
            y0 = land_y(x, z)
            rad = w * r.uniform(0.5, 0.6)
            geo = blob(rad, r, (1.0, h / w * 1.05, 1.0), subdiv=1, rough=0.22, flat=-0.6, yaw=r.uniform(0, 360))
            V, N, F, UV = geo
            top = h * 0.5 - 0.02
            V = [Vector((v[0], top + (v[1] - top) * 0.2 if v[1] > top else v[1], v[2])) for v in V]
            geo = xform((V, N, F, UV), (x, y0 + h * 0.5 - 0.04, z))
            V, N, F, UV = geo
            N2 = []
            for v, nn in zip(V, N):
                o = Vector((v[0] - x, 0.0, v[2] - z))
                side = (ux * o.z - uz * o.x)
                cn = Vector((-uz * side, 0.0, ux * side))
                cn = cn.normalized() if cn.length > 1e-5 else Vector((0, 1, 0))
                upk = smoothstep(y0 + h * 0.55, y0 + h * 0.95, v[1])
                cn = (cn * (1.0 - upk) + Vector((0, 1, 0)) * upk).normalized()
                N2.append((Vector(nn) * 0.2 + cn * 0.8).normalized())
            g.add((V, N2, F, UV), slot, L['Foliage'], 0.7)
            if flowers and r.random() < flowers:
                fc = [S['Accent'], S['Snow'] if 'Snow' in S else S2['Snow'], S['Roof']][r.randint(0, 2)]
                for j in range(3):
                    q = Vector((x + r.uniform(-0.3, 0.3), y0 + h * r.uniform(0.8, 1.0), z + r.uniform(-0.3, 0.3)))
                    g.add(xform(blob(0.08, r, (1, 0.7, 1), subdiv=1, rough=0.1), q), fc, L['none'], 0.75)

def shrub(g, x, z, r, rad=0.45, slot=None, flowers=None):
    # a low shrub (0.5-0.9 m): 4-6 clumps around a centre, crown-shaded, optional flower dots
    slot = S['FoliageDark'] if slot is None else slot
    y0 = land_y(x, z)
    C = Vector((x, y0 + rad * 0.55, z))
    n = r.randint(4, 6)
    for i in range(n):
        a = i * 2.39996 + r.uniform(-0.4, 0.4)
        p = C + Vector((math.cos(a) * rad * 0.55, r.uniform(-0.05, 0.3) * rad, math.sin(a) * rad * 0.55))
        geo = xform(clump_geo(rad * r.uniform(0.5, 0.65), r, (1.0, 0.85, 1.0), 1, 0.3), p)
        g.add(crown_normals(geo, C - Vector((0, rad * 0.3, 0)), 0.7), slot if i % 3 else S['Foliage'], L['Foliage'], 0.58)
    if flowers is not None:
        for j in range(5):
            a = r.uniform(0, 2 * math.pi)
            q = C + Vector((math.cos(a) * rad * 0.7, rad * r.uniform(0.25, 0.6), math.sin(a) * rad * 0.7))
            g.add(xform(blob(0.055, r, (1, 0.7, 1), subdiv=1, rough=0.1), q), flowers, L['none'], 0.7)

def build_forest():
    # Round 1: the forest as dense grove masses (RV There Yet tree walls) instead of scattered single trees, a belt along the ridge behind the
    # farm terraces, groves on the flanks and in the farmland. Far groves only (>= 40 m from the play area).
    # Round 2b: every grove tree is a card tree now (trunk + one dark inner clump in Forest_Tri, the crown in Harbour_Core_ForestCrowns_Cards),
    # so the ridge belt can be continuous (critics: "no forest belt, the canopy blobs are tiny next to the hill"): two staggered rows of
    # 9-13 m conifers with leafy trees mixed in, 2-3x round 1's crown size.
    g = G2('Harbour_Core_Forest_Tri', 'core', tri=True, rays=6, ao_dist=2.5)
    gc = cards_group('Harbour_Core_ForestCrowns_Cards',
                     note='Round 2b: the crown cards of the forest belt, the farmland groves and the headland trees (M_Toon_Foliage). No colliders.')
    r = rng2('forest')
    groves = []
    for row, (z0, step, dz) in enumerate(((-170.0, 13.0, 7.0), (-153.0, 17.0, 5.0))):     # the ridge belt: two staggered rows
        for x in frange(-176.0 + row * step / 2, 176.0, step):
            gx, gz = x + r.uniform(-3, 3), z0 + r.uniform(-dz, dz)
            if road_dist(gx, gz) < 9.0 or poly_dist(gx, gz, RIVER) < river_half_width(gz) + 8.0:
                continue
            if any(math.hypot(gx - fx, gz - fz) < 15.0 for fx, fz, _, _ in FARM) or math.hypot(gx - WINDMILL[0], gz - WINDMILL[1]) < 16.0:
                continue
            groves.append((gx, gz, r.random() < 0.72, 'belt'))
    for _ in range(400):                                            # flanks and the farmland
        if len(groves) >= 20 + sum(1 for q in groves if q[3] == 'belt'):
            break
        x, z = r.uniform(-175, 175), r.uniform(-140, -40)
        if inside_walls(x, z, 22.0) or road_dist(x, z) < 8.0 or poly_dist(x, z, RIVER) < river_half_width(z) + 7.0:
            continue
        if z > -60.0 and abs(x) < 90.0:
            continue
        if math.hypot(x - WINDMILL[0], z - WINDMILL[1]) < 16.0 or any(math.hypot(x - fx, z - fz) < 14.0 for fx, fz, _, _ in FARM):
            continue
        if poly_dist(x, z, HILL_TRACK) < 7.0 or poly_dist(x, z, FARM_TRACK) < 7.0 or math.hypot(x - LIGHTHOUSE[0], z - LIGHTHOUSE[1]) < 16.0:
            continue
        if any(math.hypot(x - gx, z - gz) < 14.0 for gx, gz, _, _ in groves):
            continue
        groves.append((x, z, r.random() < 0.5, 'grove'))
    trees = 0
    for gx, gz, conifer, kind in groves:
        n = r.randint(3, 5) if kind == 'belt' else r.randint(5, 8)
        R = r.uniform(4.5, 7.0)
        for k in range(n):
            a = r.uniform(0, 2 * math.pi); d = R * math.sqrt(r.random())
            tx, tz = gx + d * math.cos(a), gz + d * math.sin(a) * 0.75
            if road_dist(tx, tz) < 5.0 or poly_dist(tx, tz, RIVER) < river_half_width(tz) + 4.0:
                continue
            y0 = land_y(tx, tz) - 0.2
            if conifer or (kind == 'belt' and k % 4 == 0):
                tree_pine(g, (tx, y0, tz), r.uniform(9.0, 13.0), S['FoliageDark'] if r.random() < 0.7 else S['Foliage'], sides=6, gc=gc)
            else:
                tree_round(g, (tx, y0, tz), r.uniform(6.5, 9.5), r, S['Foliage'] if r.random() < 0.55 else S['FoliageDark'], sub=1, gc=gc)
            trees += 1
    g.extra['trees'] = trees
    # Round 2b: hedgerows along the farm terraces' banks (critics: "no hedgerows, the fields float"): card bushes on the riser band of each
    # field terrace, with gaps, off the tracks, the river and the farmyards.
    hr = rng2('hedgerows')
    bushes = 0
    for z in frange(WALL_ZS - 64.0, WALL_ZS - 5.0, 3.2):
        for x in frange(-112.0, 112.0, 3.2):
            if bushes >= 190:
                break
            jx, jz = x + hr.uniform(-0.9, 0.9), z + hr.uniform(-0.9, 0.9)
            roll = hr.random()
            if hill_terrace_k(jx, jz) < 0.45 or inside_walls(jx, jz, 3.0):
                continue
            q = hill_raw(jx, jz) / HILL_STEP
            f = q - math.floor(q)
            if not (0.76 < f < 0.92) or n2(jx, jz, 0.045, 81) < -0.25 or roll < 0.25:
                continue
            if road_dist(jx, jz) < 4.0 or poly_dist(jx, jz, HILL_TRACK) < 3.5 or poly_dist(jx, jz, FARM_TRACK) < 3.5:
                continue
            if poly_dist(jx, jz, RIVER) < river_half_width(jz) + 3.0 or math.hypot(jx - WINDMILL[0], jz - WINDMILL[1]) < 9.0:
                continue
            if any(math.hypot(jx - fx, jz - fz) < 9.0 for fx, fz, _, _ in FARM):
                continue
            sc = hr.uniform(0.85, 1.25)
            C = Vector((jx, land_y(jx, jz) + 0.75 * sc, jz))
            g.add(crown_normals(xform(clump_geo(0.75 * sc, hr, (1.3, 0.8, 1.0), 1, 0.3), C), C - Vector((0, 0.35 * sc, 0)), 0.7),
                  S['FoliageDark'], L['Foliage'], 0.5)
            crown_cards_round(gc, C + Vector((0, 0.1, 0)), 1.25 * sc, 0.85 * sc, hr, S['Foliage'], S2['FoliageAlt'], S['FoliageDark'], near=False, n=6)
            bushes += 1
    g.extra['hedgerow_bushes'] = bushes
    g.extra['groves'] = len(groves)
    out = [g, gc]
    # the headland tops: wind-bent pines and a few round trees (a second mesh, so each stays under the 21k limit)
    g = G2('Harbour_Core_HeadlandTrees_Tri', 'core', tri=True, rays=6, ao_dist=2.0)
    out.append(g)
    for key, poly, top in (('W', WEST_HEAD, HEAD_TOP['W']), ('E', EAST_HEAD, HEAD_TOP['E'])):
        placed = 0
        for _ in range(300):
            if placed >= 7:
                break
            x = r.uniform(min(p[0] for p in poly), max(p[0] for p in poly))
            z = r.uniform(-75.0, 40.0)
            if poly_signed(x, z, poly) > -9.0:
                continue
            if key == 'E' and math.hypot(x - LIGHTHOUSE[0], z - LIGHTHOUSE[1]) < 14.0:
                continue
            y0 = land_y(x, z)
            lean = (-12.0 if key == 'E' else 12.0)    # bent by the sea wind, away from the bay
            if r.random() < 0.7:
                tree_pine(g, (x, y0, z), r.uniform(5.5, 8.5), S['FoliageDark'], lean=(r.uniform(-6, 6), lean), gc=gc)
            else:
                tree_round(g, (x, y0, z), r.uniform(4.0, 6.0), r, S['Foliage'], sub=1, gc=gc)
            placed += 1
    return out

def build_islands():
    g = G2('Harbour_Core_Islands_Tri', 'core', tri=True, rays=6, ao_dist=3.0)
    r = rng2('islands')
    for (x, z, rad, hgt, yaw) in ISLANDS:
        geo = blob(rad, r, (1.0, hgt / rad * 1.6, 0.75), subdiv=3 if abs(x) < 100 else 2, rough=0.18, flat=-0.25, yaw=yaw)
        V, N, F, UV = geo
        fsl = [((S['Grass'], L['Grass']) if sum(N[k].y for k in f) / len(f) > 0.6 else (S['Rock'], L['Rock'])) for f in F]
        g.add(xform(geo, (x, -0.35 * hgt, z)), 0, 0, 0.7, fsl)
        # a few trees on the near islands
        if abs(x) < 100:
            for k in range(4):
                a = r.uniform(0, 2 * math.pi); d = r.uniform(0.0, 0.5) * rad
                tx, tz = x + math.cos(a) * d, z + math.sin(a) * d * 0.75
                # find the island top by casting down on its own geometry later: approximate with an ellipsoid cap
                ty = -0.35 * hgt + hgt * 1.0 * math.sqrt(max(0.0, 1.0 - (d / rad) ** 2)) - 0.6
                tree_pine(g, (tx, ty, tz), r.uniform(4.0, 7.0), S['FoliageDark'], sides=6)
    for (x, z, rad, tall) in STACKS:
        geo = cliff_rock(rad, r, (1.0, tall, 0.8), subdiv=3, yaw=r.uniform(0, 360), top=0.9, strata=5.0)
        V, N, F, UV = geo
        # gull-white tops (guano): the top faces in Sand
        fsl = [((S['Sand'], L['Rock']) if sum(N[k].y for k in f) / len(f) > 0.75 else (S['Rock'], L['Rock'])) for f in F]
        g.add(xform(geo, (x, rad * tall * 0.25, z)), 0, 0, 0.62, fsl)
        for k in range(2):   # tumbled boulders at the foot
            a = r.uniform(0, 2 * math.pi)
            g.add(xform(cliff_rock(rad * r.uniform(0.35, 0.5), r, (1.0, 0.8, 0.9), subdiv=2, yaw=r.uniform(0, 360)),
                        (x + math.cos(a) * rad * 0.95, -0.3, z + math.sin(a) * rad * 0.95)), S['Rock'], L['Rock'], 0.62)
    return g

def build_lighthouse(g):
    x, z = LIGHTHOUSE
    y0 = land_y(x, z) - 0.3
    bands = [S['Sand'], S['Roof'], S['Sand'], S['Roof'], S['Sand']]
    prof = [(0.0, 2.3), (0.6, 2.25), (3.0, 2.05), (5.6, 1.88), (8.2, 1.72), (10.8, 1.58), (12.6, 1.5)]
    add_lathe(g, (x, y0, z), prof, S['Sand'], L['Rock'], 0.6, sides=14, ring_slots=[bands[0], bands[1], bands[2], bands[3], bands[4], bands[4]])
    add_lathe(g, (x, y0 + 12.6, z), [(0.0, 0.0), (0.02, 2.2), (0.28, 2.25), (0.3, 0.0)], S['WoodDark'], L['none'], 0.6, sides=18)
    g.add(xform(tube([Vector((2.1 * math.cos(a), 0, 2.1 * math.sin(a))) for a in [2 * math.pi * i / 18 for i in range(18)]], 0.05, 4,
                     caps=False, closed=True), (x, y0 + 13.5, z)), S['WoodDark'], L['none'], 0.6)
    add_lathe(g, (x, y0 + 12.9, z), [(0.0, 1.2), (1.8, 1.2)], S['Accent'], L['none'], 1.0, sides=12)       # the lamp room (bright glass)
    add_lathe(g, (x, y0 + 14.7, z), [(0.0, 1.45), (0.1, 1.45), (0.9, 0.7), (1.3, 0.25), (1.6, 0.0)], S['Roof'], L['none'], 0.6, sides=14)
    add_lathe(g, (x, y0 + 16.2, z), [(0.0, 0.12), (0.3, 0.12), (0.45, 0.0)], S['WoodDark'], L['none'], 0.6, sides=8)
    # door and windows
    add_box(g, (x - 2.2, y0 + 1.1, z), (0.2, 2.0, 1.0), 0.06, S['Dock'], L['Wood'], 0.5)
    for h in (4.5, 8.0, 11.0):
        add_box(g, (x - 1.92 + 0.0 * h, y0 + h, z + 0.4), (0.18, 0.8, 0.55), 0.05, S['WoodDark'], L['none'], 0.5)
    # keeper's cottage beside it, with a chimney (smoke point)
    cx, cz = x + 2.0, z - 9.0
    cy = land_y(cx, cz) - 0.2
    poly = [(-2.2, 0.0), (2.2, 0.0), (2.2, 2.6), (0.0, 4.2), (-2.2, 2.6)]
    g.add(xform(prism(poly, 6.0, 0.06), (cx, cy, cz), ry(90)), S['Sand'], L['Wood'], 0.55)
    for sz in (-1, 1):
        rot = rx(sz * 36.0)
        g.add(xform(rbox((6.6, 0.18, 2.9), 0.06), (cx, cy + 3.5, cz + sz * 1.2), ry(0) @ rot), S['Roof'], L['Wood'], 0.55)
    add_box(g, (cx + 2.2, cy + 4.4, cz - 0.8), (0.6, 1.6, 0.6), 0.08, S['Rock'], L['Rock'], 0.55)
    g.extra.setdefault('smoke_points', []).append(dict(name='KeeperCottageChimney', position=[round(cx + 2.2, 2), round(cy + 5.3, 2), round(cz - 0.8, 2)]))
    g.extra.setdefault('light_points', []).append(dict(name='LighthouseLamp', position=[round(x, 2), round(y0 + 13.8, 2), round(z, 2)],
                                                       animHint='beacon'))

def far_boat(g, kind, c, heading, r):
    rot = ry(heading)
    if kind == 'Trawler':
        add_part(g, hull(9.0, 3.4, 1.6, transom=0.8, sheer=0.5, hull_slot=S['Accent'], stripe=S['Sand']), c, rot, 0.6)
        g.add(xform(rbox((2.4, 2.0, 2.8), 0.15), Vector(c) + rot @ Vector((0, 1.3, -1.2)), rot), S['Sand'], L['none'], 0.6)
        g.add(xform(rbox((2.6, 0.2, 3.0), 0.08), Vector(c) + rot @ Vector((0, 2.4, -1.2)), rot), S['Roof'], L['none'], 0.6)
        add_lathe(g, Vector(c) + rot @ Vector((0, 0.3, 1.8)), [(0, 0.1), (6.0, 0.07), (6.1, 0.0)], S['WoodDark'], L['none'], 0.6, sides=6)
        g.add(xform(rbox((0.1, 0.1, 5.0), 0.03), Vector(c) + rot @ Vector((0, 4.5, 0.5)), rot @ rx(-25)), S['WoodDark'], L['none'], 0.6)
    else:
        add_part(g, hull(7.0, 2.4, 1.1, transom=0.7, sheer=0.3, hull_slot=S['Sand'], stripe=S['Roof']), c, rot, 0.6)
        add_lathe(g, Vector(c) + rot @ Vector((0, 0.2, 0.6)), [(0, 0.08), (8.5, 0.05), (8.6, 0.0)], S['WoodDark'], L['none'], 0.6, sides=6)
        # mainsail: a thin triangle, both sides
        p0 = Vector((0, 0.9, 0.5)); p1 = Vector((0, 8.3, 0.65)); p2 = Vector((0, 1.1, -2.6))
        V = [Vector(c) + rot @ p for p in (p0, p1, p2)]
        nrm = (V[1] - V[0]).cross(V[2] - V[0]).normalized()
        g.add((V, [nrm] * 3, [[0, 1, 2]], [[(0, 0), (1, 0), (0, 1)]]), S['Sand'], L['Cloth'], 0.7)
        g.add(([v + nrm * -0.01 for v in V], [-nrm] * 3, [[0, 2, 1]], [[(0, 0), (0, 1), (1, 0)]]), S['Sand'], L['Cloth'], 0.7)

def mafia_boat(g, c, heading):
    # long, low, sleek, dark: an ominous silhouette on the horizon (tint darkens it to near-black)
    rot = ry(heading)
    add_part_t(g, hull(8.5, 2.3, 0.9, transom=0.85, sheer=0.12, hull_slot=S['WoodDark'], stripe=S['Rock']), c, rot, 0.6, 0.35)
    g.add(xform(rbox((1.9, 0.7, 2.6), 0.3), Vector(c) + rot @ Vector((0, 0.45, 0.4)), rot @ rx(-6)), S['Rock'], L['none'], 0.6, tint=0.3)
    g.add(xform(rbox((1.7, 0.05, 1.0), 0.02), Vector(c) + rot @ Vector((0, 0.95, 1.4)), rot @ rx(-35)), S['Rock'], L['none'], 0.6, tint=0.45)
    add_lathe(g, Vector(c) + rot @ Vector((0, 0.8, -0.4)), [(0, 0.03), (1.6, 0.02), (1.62, 0.0)], S['Rock'], L['none'], 0.6, sides=5)

def add_part_t(g, geo_fs, c, rot, ao, tint):
    geo, fs = geo_fs
    g.add(xform(geo, c, rot), 0, 0, ao, fs, tint=tint)

def black_car(g, x, z, yaw):
    y0 = land_y(x, z)
    rot = ry(yaw)
    c = Vector((x, y0, z))
    T = 0.28
    g.add(xform(rbox((1.85, 0.75, 4.6), 0.28), c + rot @ Vector((0, 0.72, 0)), rot), S['Rock'], L['Wood'], 0.6, tint=T)
    g.add(xform(rbox((1.65, 0.62, 2.4), 0.24), c + rot @ Vector((0, 1.32, -0.25)), rot), S['Rock'], L['none'], 0.6, tint=T)
    g.add(xform(rbox((1.68, 0.42, 2.2), 0.18), c + rot @ Vector((0, 1.36, -0.25)), rot), S['Rock'], L['none'], 0.6, tint=0.55)   # tinted glass
    for sx in (-1, 1):
        for sz in (-1.45, 1.45):
            add_lathe(g, c + rot @ Vector((sx * 0.86, 0.36, sz)), [(-0.13, 0.0), (-0.12, 0.33), (0.12, 0.33), (0.13, 0.0)], S['Rock'], L['none'], 0.5,
                      sides=12, rot=rot @ rz(90))
    for sx in (-0.6, 0.6):   # headlights
        g.add(xform(rbox((0.32, 0.14, 0.06), 0.03), c + rot @ Vector((sx, 0.85, 2.31)), rot), S['Sand'], L['none'], 0.8)

def build_backdrop():
    g = G2('Harbour_Core_Backdrop', 'core', rays=8, ao_dist=1.5)
    r = rng2('backdrop')
    build_lighthouse(g)
    for kind, c, hd in FAR_BOATS:
        far_boat(g, kind, c, hd, r)
    mafia_boat(g, MAFIA_BOAT[1], MAFIA_BOAT[2])
    black_car(g, *BLACK_CAR)
    for (x, z, col) in CHANNEL_MARKERS:
        y = seabed_y(z) - 0.3
        add_lathe(g, (x, y, z), [(0, 0.16), (abs(y) + 2.3, 0.14), (abs(y) + 2.32, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=8)
        top = S['Foliage'] if col == 'G' else S['Roof']
        tc = Vector((x, 2.3, z))
        if col == 'G':
            add_lathe(g, tc, [(0, 0.0), (0.02, 0.42), (0.6, 0.42), (0.62, 0.0)], top, L['none'], 0.7, sides=10)
        else:
            add_lathe(g, tc, [(0, 0.0), (0.02, 0.45), (0.75, 0.0)], top, L['none'], 0.7, sides=10)
    # a farm on the hills (round 1: higher up, on the farm terraces): farmhouse, barn, a second house; hay bales; the windmill
    for (fx, fz, yaw, kind) in FARM:
        fy = land_y(fx, fz) - 0.25
        rot = ry(yaw)
        if kind == 'house':
            poly = [(-2.6, 0.0), (2.6, 0.0), (2.6, 3.0), (0.0, 5.0), (-2.6, 3.0)]
            g.add(xform(prism(poly, 7.0, 0.08), (fx, fy, fz), rot @ ry(90)), S['Sand'], L['Wood'], 0.55)
            for sz in (-1, 1):
                g.add(xform(rbox((7.6, 0.2, 3.5), 0.06), Vector((fx, fy, fz)) + rot @ Vector((0, 4.15, sz * 1.38)), rot @ rx(sz * 37.0)),
                      S['Roof'], L['Wood'], 0.55)
            add_box(g, Vector((fx, fy, fz)) + rot @ Vector((2.0, 5.0, 0.8)), (0.7, 2.0, 0.7), 0.08, S['Rock'], L['Rock'], 0.55, rot=rot)
            for wx in (-2.0, 0.0, 2.0):
                add_box(g, Vector((fx, fy, fz)) + rot @ Vector((wx, 1.7, 2.62)), (0.8, 0.9, 0.1), 0.04, S['WoodDark'], L['none'], 0.5, rot=rot)
        else:
            poly = [(-3.5, 0.0), (3.5, 0.0), (3.5, 3.6), (0.0, 6.2), (-3.5, 3.6)]
            g.add(xform(prism(poly, 9.0, 0.08), (fx, fy, fz), rot @ ry(90)), S['Roof'], L['Wood'], 0.55)
            for sz in (-1, 1):
                g.add(xform(rbox((9.6, 0.2, 4.6), 0.06), Vector((fx, fy, fz)) + rot @ Vector((0, 5.0, sz * 1.8)), rot @ rx(sz * 36.6)),
                      S['WoodDark'], L['Wood'], 0.55)
            add_box(g, Vector((fx, fy, fz)) + rot @ Vector((0, 1.8, 3.55)), (2.8, 3.4, 0.12), 0.05, S['Sand'], L['Wood'], 0.5, rot=rot)
        g.extra.setdefault('smoke_points', [])
        if kind == 'house':
            p = Vector((fx, fy, fz)) + rot @ Vector((2.0, 6.1, 0.8))
            g.extra['smoke_points'].append(dict(name='FarmChimney', position=[round(p.x, 2), round(p.y, 2), round(p.z, 2)]))
    fr = rng2('fields')
    for k in range(7):
        hx, hz = FARM[1][0] + fr.uniform(-12, 12), FARM[1][1] + fr.uniform(4, 12)
        add_lathe(g, (hx, land_y(hx, hz) + 0.15, hz), [(-0.6, 0.0), (-0.58, 0.55), (0.58, 0.55), (0.6, 0.0)], S['Sand'], L['Grass'], 0.6,
                  sides=10, rot=ry(fr.uniform(0, 180)) @ rz(90))
    windmill_body(g)
    far_ridges(g)       # round 2b
    return g

def far_ridges(g):
    # Round 2b (critics: "no depth layering, no far ridge"): two silhouette ridges far behind the hills - one strip of quads each, the fog
    # turns them hazy blue (PEAK's layered tints). No colliders; ~260 triangles.
    for k, (z0, h0, amp, slot) in enumerate(((-262.0, 50.0, 13.0, S['FoliageDark']), (-318.0, 74.0, 18.0, S['Rock']))):
        V, N, F, UV = [], [], [], []
        xs = frange(-340.0, 340.0, 10.0)
        for i, x in enumerate(xs):
            zz = z0 + 14.0 * n2(x, 0.0, 0.01, 84 + k)
            top = h0 + amp * n2(x, 0.0, 0.009, 80 + k) + 0.35 * amp * n2(x, 0.0, 0.03, 82 + k) - 0.00012 * x * x
            V += [Vector((x, -6.0, zz + 6.0)), Vector((x, top, zz))]
            N += [Vector((0.0, 0.35, 1.0)).normalized(), Vector((0.0, 0.7, 1.0)).normalized()]
            if i:
                a = 2 * i
                F.append([a - 2, a, a + 1, a - 1])
                UV.append([(V[q].x, V[q].y) for q in (a - 2, a, a + 1, a - 1)])
        g.add((V, N, F, UV), slot, L['none'], 0.85)

def field_patch(g, cx, cz, w, d, yaw, slot, step=2.0):
    rot = ry(yaw)
    nx, nz = int(w / step) + 1, int(d / step) + 1
    V, N, F, UV = [], [], [], []
    for j in range(nz + 1):
        for i in range(nx + 1):
            u, v = -w / 2 + w * i / nx, -d / 2 + d * j / nz
            p = Vector((cx, 0, cz)) + rot @ Vector((u, 0, v))
            edge = max(abs(u) / (w / 2), abs(v) / (d / 2))
            p.y = land_y(p.x, p.z) + 0.22 - (0.35 if edge > 0.999 else 0.0)
            V.append(p); N.append(Vector((0, 1, 0)))
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            q = [a, a + nx + 1, a + nx + 2, a + 1]
            if (V[q[1]] - V[q[0]]).cross(V[q[2]] - V[q[0]]).y < 0:
                q = list(reversed(q))
            F.append(q)
    acc = [Vector() for _ in V]
    for f in F:
        n = (V[f[1]] - V[f[0]]).cross(V[f[2]] - V[f[0]])
        for k in f:
            acc[k] += n
    N = [a.normalized() for a in acc]
    UV = [[(V[k].x, V[k].z) for k in f] for f in F]
    g.add((V, N, F, UV), slot, L['Grass'], 0.75)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Buildings on the plateau (Core, one box collider each): the bait shop with its serving hatch, counter, awning and hand-painted sign; the
# open net shed under a corrugated tin roof; the stone-and-timber smokehouse with a chimney (smoke point); the little harbour office with a
# noticeboard (WANTED poster), mailbox, flagpole and weather vane. All fronts face the sea (+z). Ground = the plateau (dunes flattened).
# ---------------------------------------------------------------------------------------------------------------------------------------
def gable_house(g, c, W, D, eave, ridge, wall_slot, roof_slot, trim_slot=None, roof_kind='slab', overhang=0.3, yaw=0.0, wall_layer=None):
    # Walls: a bevelled house-shaped prism, ridge along local x; corner posts and trims; a two-slab roof (or corrugated tin) + ridge roll.
    cx, cy, cz = c
    rot = ry(yaw)
    poly = [(-D / 2, 0.0), (D / 2, 0.0), (D / 2, eave), (0.0, ridge), (-D / 2, eave)]
    g.add(xform(prism(poly, W, 0.04), (cx, cy, cz), rot), wall_slot, wall_layer if wall_layer is not None else L['Wood'], AO_WOOD)
    trim = trim_slot if trim_slot is not None else S['WoodDark']
    P = lambda x, y, z: Vector((cx, cy, cz)) + rot @ Vector((x, y, z))
    for sx in (-1, 1):
        for sz in (-1, 1):
            g.add(xform(rbox((0.14, eave, 0.14), 0.03), P(sx * W / 2, eave / 2, sz * D / 2), rot), trim, L['Wood'], 0.45)
    for sz in (-1, 1):
        g.add(xform(rbox((W + 0.1, 0.16, 0.06), 0.02), P(0, 0.08, sz * (D / 2 + 0.01)), rot), trim, L['Wood'], 0.45)
        g.add(xform(rbox((W + 0.1, 0.12, 0.06), 0.02), P(0, eave - 0.06, sz * (D / 2 + 0.01)), rot), trim, L['Wood'], 0.45)
    run = D / 2 + overhang
    ang = math.degrees(math.atan2(ridge - eave, D / 2))
    slab_len = run / math.cos(math.radians(ang)) + 0.05
    for sz in (-1, 1):
        rr = rot @ rx(ang * sz)
        mid = P(0, ridge - (ridge - eave) * (run / 2) / (D / 2) + 0.07, sz * run / 2)
        if roof_kind == 'tin':
            corrugated(g, mid, rr, W + 0.5, slab_len, roof_slot)
        else:
            g.add(xform(rbox((W + 0.5, 0.11, slab_len), 0.04), mid, rr), roof_slot, L['Wood'], 0.5)
    geo, fs = lathe([(0, 0.0), (0.02, 0.07), (0.06, 0.09), (W + 0.44, 0.09), (W + 0.48, 0.07), (W + 0.5, 0.0)], 10)
    g.add(xform(geo, P(-W / 2 - 0.25, ridge + 0.1, 0), rot @ rz(-90)), roof_slot if roof_kind != 'tin' else S['Rock'], L['Wood'], 0.5)
    return P

def corrugated(g, mid, rot, w, l, slot, waves=None):
    # A corrugated tin sheet: a thin sheet whose top rides a sine across x (ridges run down the slope), both faces, smooth normals.
    waves = waves or int(w / 0.18)
    nu, nv = waves * 3, 1
    V, N, F, UV = [], [], [], []
    for side in (1, -1):
        base = len(V)
        for j in range(nv + 1):
            for i in range(nu + 1):
                u = -w / 2 + w * i / nu
                v = -l / 2 + l * j / nv
                ph = 2 * math.pi * waves * i / nu
                y = 0.025 * math.sin(ph) + (0.0 if side > 0 else -0.012)
                n = Vector((-0.025 * 2 * math.pi * waves / w * math.cos(ph), 1.0, 0.0)).normalized() * side
                V.append(mid + rot @ Vector((u, y, v))); N.append(rot @ n)
        for j in range(nv):
            for i in range(nu):
                a = base + j * (nu + 1) + i
                q = [a, a + nu + 1, a + nu + 2, a + 1]
                F.append(q if side > 0 else list(reversed(q)))
                UV.append([(V[k].x, V[k].z) for k in q])
    g.add((V, N, F, UV), slot, L['Rock'], 0.5)

def window(g, P, rot, x, y, z, w, h, frame=S['WoodDark'], shutters=None, depth=0.05):
    # glass (dark, tinted), a frame, a cross bar, optional shutters; on a wall facing +z in the house frame at (x, y, z)
    g.add(xform(rbox((w, h, 0.03), 0.01), P(x, y, z + 0.005), rot), S['Rock'], L['none'], 0.5, tint=0.55)
    for dx, dy, sx_, sy_ in ((0, h / 2, w + 0.12, 0.08), (0, -h / 2, w + 0.16, 0.1), (-w / 2, 0, 0.08, h), (w / 2, 0, 0.08, h),
                             (0, 0, w, 0.04), (0, 0, 0.04, h)):
        g.add(xform(rbox((sx_, sy_, depth + 0.02), 0.015, 1), P(x + dx, y + dy, z + depth / 2), rot), frame, L['Wood'], 0.5)
    if shutters is not None:
        for s in (-1, 1):
            g.add(xform(rbox((w * 0.5, h + 0.06, 0.04), 0.015), P(x + s * (w * 0.75 + 0.08), y, z + 0.03), rot @ ry(s * 12.0)),
                  shutters, L['Wood'], 0.5)

def door(g, P, rot, x, z, w=0.9, h=1.85, slot=S['Dock'], knob=S['Accent']):
    g.add(xform(rbox((w, h, 0.07), 0.03), P(x, h / 2 + 0.02, z + 0.04), rot), slot, L['Wood'], 0.5)
    for by in (0.4, 1.4):
        g.add(xform(rbox((w - 0.04, 0.1, 0.035), 0.012), P(x, by, z + 0.085), rot), S['WoodDark'], L['Wood'], 0.45)
    g.add(xform(rbox((0.06, 0.06, 0.06), 0.025), P(x + w / 2 - 0.15, 0.95, z + 0.1), rot), knob, L['none'], 0.5)

def sign_text(g, body, size, centre, rot, slot, depth=0.006, both=False):
    # Raised lettering on a face whose outward normal is rot @ +z. Unity is left-handed: a viewer looking at a +z face has +x on their
    # LEFT, so glyph x maps to -x (the phase-1 sign rule); the map is a reflection, so windings are reversed (mirror_fix).
    V, N, F, UV = text_geo(body, size, depth)
    sides = (1, -1) if both else (1,)
    for side in sides:
        Mr = Matrix(((-side, 0, 0), (0, 1, 0), (0, 0, side)))
        R = rot @ Mr
        g.add(mirror_fix(([Vector(centre) + R @ v for v in V], [R @ n for n in N], F, UV)), slot, L['none'], 0.6)

def build_bait_shop(g, r):
    cx, cz, W, D = BAIT_SHOP
    cy = PLATEAU_Y
    rot = ry(0)
    g.mark()
    P = gable_house(g, (cx, cy, cz), W, D, 2.4, 3.55, S['Sand'], S['Roof'], trim_slot=S['Accent'])
    fz = D / 2
    door(g, P, rot, -1.7, fz, slot=S['Accent'], knob=S['Sand'])
    window(g, P, rot, 1.55, 1.55, fz, 0.7, 0.6, frame=S['Accent'])
    # serving hatch with a propped-up flap and a counter in front of it
    g.add(xform(rbox((1.7, 0.95, 0.03), 0.01), P(-0.1, 1.45, fz + 0.005), rot), S['WoodDark'], L['none'], 0.5, tint=0.6)
    g.add(xform(rbox((1.85, 0.06, 0.9), 0.02), P(-0.1, 2.05, fz + 0.45), rot @ rx(-18)), S['Accent'], L['Wood'], 0.5)
    for sx in (-0.85, 0.65):
        g.add(xform(rbox((0.04, 0.9, 0.04), 0.012), P(sx, 1.55, fz + 0.75), rot @ rx(25)), S['WoodDark'], L['Wood'], 0.5)
    g.add(xform(rbox((2.1, 0.08, 0.62), 0.025), P(-0.1, 1.02, fz + 0.31), rot), S['Wood'], L['Wood'], 0.5)
    g.add(xform(rbox((2.0, 0.95, 0.5), 0.03), P(-0.1, 0.5, fz + 0.3), rot), S['WoodDark'], L['Wood'], 0.45)
    # awning over the hatch: striped canvas
    for k in range(7):
        x0 = -1.15 + k * 0.32
        g.add(xform(rbox((0.32, 0.03, 1.1), 0.01), P(x0 + 0.16, 2.62, fz + 0.5), rot @ rx(-20)),
              S['Roof'] if k % 2 == 0 else S['Sand'], L['Cloth'], 0.6)
    # name board over the door and hatch, with the painted fish
    bc = P(-0.4, 3.05, fz + 0.12)
    g.add(xform(rbox((3.2, 0.56, 0.08), 0.03), bc, rot), S['Dock'], L['Wood'], 0.5)
    sign_text(g, 'BAIT & TACKLE', 0.3, bc + Vector((0.25, -0.02, 0.045)), rot, S['Sand'])
    fr = rng2('baitshop.fish')
    g.add(xform(blob(0.17, fr, (1.7, 0.75, 0.25), subdiv=2, rough=0.05, flat=-2), bc + Vector((-1.2, 0.0, 0.05))), S['Accent'], L['none'], 0.6)
    g.add(xform(blob(0.09, fr, (0.6, 1.1, 0.3), subdiv=2, rough=0.03, flat=-2), bc + Vector((-0.92, 0.0, 0.05))), S['Accent'], L['none'], 0.6)
    # side chalkboard (A-frame), worms sign
    ab = P(1.6, 0.0, fz + 1.5)
    for s in (-1, 1):
        g.add(xform(rbox((0.62, 0.9, 0.04), 0.015), ab + Vector((0, 0.45, s * 0.14)), rot @ rx(s * 10)), S['WoodDark'], L['Wood'], 0.5)
    sign_text(g, 'LIVE\nWORMS', 0.13, ab + Vector((0, 0.55, 0.205)), rot @ rx(10), S['Sand'])
    d = g.solid('BaitShop')
    d['bounds_note'] = 'walls, counter and awning (the awning is above head height: a box up to 1.1 m in front of the wall is enough)'
    # wanted poster on the west wall, facing -x
    wanted_poster(g, P(-W / 2 - 0.03, 1.6, 0.5), rot @ ry(-90), r)
    g._mark = len(g.V)
    return P

def wanted_poster(g, c, rot, r):
    # A cheap paper poster: WANTED, four silly crew silhouettes (pillowy bodies, round heads, one with a fishing hat), a big reward.
    c = Vector(c)
    g.add(xform(rbox((0.78, 1.02, 0.012), 0.004), c, rot @ rz(3)), S['Sand'], L['Cloth'], 0.7)
    sign_text(g, 'WANTED', 0.13, c + rot @ Vector((0, 0.38, 0.012)), rot @ rz(3), S['Roof'])
    sign_text(g, 'REWARD $$$', 0.075, c + rot @ Vector((0, -0.4, 0.012)), rot @ rz(3), S['WoodDark'])
    for k in range(4):
        x = -0.27 + k * 0.18
        body = c + rot @ Vector((x, -0.12 + 0.02 * (k % 2), 0.012))
        g.add(xform(blob(0.085, r, (0.95, 1.3, 0.12), subdiv=2, rough=0.03, flat=-2), body), S['WoodDark'], L['none'], 0.6)
        g.add(xform(blob(0.07, r, (1.0, 1.0, 0.12), subdiv=2, rough=0.03, flat=-2), body + rot @ Vector((0, 0.15, 0.0))), S['WoodDark'], L['none'], 0.6)
        if k == 2:   # a bucket hat
            g.add(xform(rbox((0.16, 0.04, 0.012), 0.005, 1), body + rot @ Vector((0, 0.215, 0.0)), rot), S['WoodDark'], L['none'], 0.6)
            g.add(xform(rbox((0.1, 0.05, 0.012), 0.005, 1), body + rot @ Vector((0, 0.245, 0.0)), rot), S['WoodDark'], L['none'], 0.6)
        if k == 0:   # a raised arm (waving at the camera)
            g.add(xform(rbox((0.03, 0.13, 0.012), 0.005, 1), body + rot @ Vector((-0.085, 0.1, 0.0)), rot @ rz(35)), S['WoodDark'], L['none'], 0.6)
        if k == 3:   # holding up a fish
            g.add(xform(blob(0.05, r, (1.8, 0.7, 0.12), subdiv=1, rough=0.02, flat=-2), body + rot @ Vector((0.1, 0.12, 0.0))), S['WoodDark'], L['none'], 0.6)
        # dot eyes (paper showing through)
        for ex in (-0.022, 0.022):
            g.add(xform(blob(0.011, r, (1, 1, 0.3), subdiv=1, rough=0.0, flat=-2), body + rot @ Vector((ex, 0.16, 0.006))), S['Sand'], L['none'], 0.8)

def big_wanted(g, c, rot, r, scale=1.5):
    # round 3 (critics: "the story reads only if you stand in front of one small poster"): a 1.2 m WANTED poster - wanted_poster built at
    # the origin, scaled and moved
    t = G2('tmp_poster', 'core')
    wanted_poster(t, (0.0, 0.0, 0.0), rot, r)
    V = [Vector(c) + v * scale for v in t.V]
    F = [list(f) for f in t.F]
    g.add((V, t.N, F, t.UV), S['Sand'], L['Cloth'], 0.7, list(t.SL))

def build_net_shed(g, r):
    cx, cz, W, D = NET_SHED
    cy = PLATEAU_Y
    rot = ry(0)
    g.mark()
    # an open-fronted shed: back wall, two side walls, posts at the front, corrugated tin roof sloping back (lean-to)
    P = lambda x, y, z: Vector((cx, cy, cz)) + rot @ Vector((x, y, z))
    g.add(xform(rbox((W, 2.6, 0.12), 0.03), P(0, 1.3, -D / 2), rot), S['WoodDark'], L['Wood'], AO_WOOD)
    for s in (-1, 1):
        g.add(xform(prism([(-D / 2, 0.0), (D / 2, 0.0), (D / 2, 2.9), (-D / 2, 2.6)], 0.12, 0.03), P(s * W / 2, 0, 0), rot), S['WoodDark'], L['Wood'], AO_WOOD)
        g.add(xform(rbox((0.16, 2.95, 0.16), 0.03), P(s * (W / 2 - 0.05), 1.47, D / 2 - 0.05), rot), S['Wood'], L['Wood'], 0.45)
    g.add(xform(rbox((W + 0.1, 0.14, 0.16), 0.03), P(0, 2.95, D / 2 - 0.05), rot), S['Wood'], L['Wood'], 0.45)
    ang = math.degrees(math.atan2(0.3, D))
    corrugated(g, P(0, 2.98, 0.1), rot @ rx(-ang), W + 0.6, D + 0.6, S['Rock'])
    # boards nailed on crooked, a hand-painted "NETS" on the side
    for k in range(3):
        g.add(xform(rbox((0.9, 0.14, 0.03), 0.01), P(W / 2 + 0.07, 0.6 + 0.5 * k, -0.4 + 0.3 * k), rot @ ry(90) @ rz(r.uniform(-8, 8))), S['Wood'], L['Wood'], 0.5)
    sign_text(g, 'NETS', 0.32, P(W / 2 + 0.075, 2.1, 0.2), rot @ ry(90), S['Sand'])
    # inside: a workbench along the back wall
    g.add(xform(rbox((2.4, 0.08, 0.7), 0.02), P(-0.6, 0.92, -D / 2 + 0.45), rot), S['Wood'], L['Wood'], 0.5)
    for sx in (-1.7, 0.5):
        g.add(xform(rbox((0.08, 0.9, 0.6), 0.02), P(sx, 0.45, -D / 2 + 0.45), rot), S['WoodDark'], L['Wood'], 0.5)
    it = g.solid('NetShed')
    it['visual_bounds_min'], it['visual_bounds_max'] = list(it['bounds_min']), list(it['bounds_max'])
    it['bounds_max'][2] = round(cz + 0.1, 3)      # collider: the back half (walls + workbench); the open front stays walkable
    it['position'][2] = round((it['bounds_min'][2] + it['bounds_max'][2]) / 2, 3)
    it['footprint'][2] = round(it['bounds_max'][2] - it['bounds_min'][2], 3)
    return P

def build_smokehouse(g, r):
    cx, cz, W, D = SMOKEHOUSE
    cy = PLATEAU_Y
    rot = ry(0)
    g.mark()
    # stone base, timber upper, steep roof, a big stone chimney on the west side
    g.add(xform(rbox((W + 0.1, 1.0, D + 0.1), 0.12), (cx, cy + 0.5, cz), rot), S['Rock'], L['Rock'], 0.55)
    P = gable_house(g, (cx, cy + 0.95, cz), W, D, 1.9, 3.1, S['Wood'], S['Roof'], trim_slot=S['WoodDark'], overhang=0.35)
    door(g, P, rot, 0.4, D / 2, w=0.8, h=1.6, slot=S['WoodDark'], knob=S['Rock'])
    g.add(xform(rbox((0.9, 4.8, 0.9), 0.12), (cx - W / 2 - 0.35, cy + 2.4, cz - 0.3), rot), S['Rock'], L['Rock'], 0.55)
    g.add(xform(rbox((1.05, 0.2, 1.05), 0.06), (cx - W / 2 - 0.35, cy + 4.85, cz - 0.3), rot), S['Rock'], L['Rock'], 0.55)
    g.extra.setdefault('smoke_points', []).append(dict(name='SmokehouseChimney', position=[round(cx - W / 2 - 0.35, 2), round(cy + 5.0, 2), round(cz - 0.3, 2)]))
    # vents (louvres) in the gable
    for k in range(3):
        g.add(xform(rbox((0.5, 0.05, 0.08), 0.015), P(-0.6, 2.25 + 0.12 * k, D / 2 + 0.04), rot @ rx(-25)), S['WoodDark'], L['Wood'], 0.5)
    bc = P(-0.2, 2.5, D / 2 + 0.06) + Vector((0, 0.0, 0.0))
    g.add(xform(rbox((1.9, 0.4, 0.06), 0.02), P(0.3, 1.9, D / 2 + 0.06), rot), S['Dock'], L['Wood'], 0.5)
    sign_text(g, 'SMOKED FISH', 0.17, P(0.3, 1.9, D / 2 + 0.1), rot, S['Sand'])
    g.solid('Smokehouse')

def build_dock_office(g, r):
    cx, cz, W, D = DOCK_OFFICE
    cy = PLATEAU_Y
    rot = ry(0)
    g.mark()
    P = gable_house(g, (cx, cy, cz), W, D, 2.3, 3.4, S['Reed'], S['Dock'], trim_slot=S['Sand'], roof_kind='tin')
    fz = D / 2
    door(g, P, rot, -0.9, fz, slot=S['Accent'], knob=S['Sand'])
    window(g, P, rot, 0.75, 1.5, fz, 0.8, 0.65, frame=S['Sand'], shutters=S['Accent'])
    window(g, P, rot, 0.0, 1.5, -fz - 0.02, 0.7, 0.55, frame=S['Sand'])
    bc = P(0.0, 2.75, fz + 0.06)
    g.add(xform(rbox((2.6, 0.42, 0.07), 0.025), bc, rot), S['Sand'], L['Wood'], 0.55)
    sign_text(g, 'HARBOUR OFFICE', 0.2, bc + Vector((0, -0.02, 0.04)), rot, S['WoodDark'])
    # weather-vane mount on the ridge (the vane itself is an animated model) and a stove pipe
    add_lathe(g, P(0.9, 3.45, 0), [(0, 0.06), (0.45, 0.04), (0.47, 0.0)], S['WoodDark'], L['none'], 0.5, sides=8)
    add_lathe(g, P(-1.1, 3.0, -0.6), [(0, 0.07), (0.75, 0.07), (0.78, 0.11), (0.86, 0.11), (0.9, 0.0)], S['Rock'], L['none'], 0.5, sides=10)
    g.extra.setdefault('smoke_points', []).append(dict(name='OfficeStovePipe', position=[round(cx - 1.1, 2), round(cy + 3.95, 2), round(cz - 0.6, 2)]))
    d = g.solid('HarbourOffice')
    g.extra['weather_vane'] = [round(cx + 0.9, 3), round(cy + 3.92, 3), round(cz, 3)]
    return P

def build_buildings():
    g = G2('Harbour_Core_Buildings', 'core', rays=24)
    r = rng2('buildings')
    build_bait_shop(g, r)
    build_net_shed(g, r)
    build_smokehouse(g, r)
    build_dock_office(g, r)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# The crew's boat (R76, user 2026-10-14): a chunky stylised TUGBOAT, the getaway boat they came downriver in. Silhouette reference only
# (Rust's tug): high bow, rubber fender ring, tyres on the sides, a deckhouse you can walk into (lockers, shelves, a cot), a raised
# wheelhouse on top (windows, door, helm, ladders), a smokestack, a mast with a light, a towing bitt, coiled lines and the crew's gear.
# Fishing spots: bow, both side walkways, the open aft working deck, and the upper deck rails. 4 players fit with room for their chests.
#
# Local space (the M4 boat ride reuses it): origin at the waterline centre, +z forward (bow), +x starboard, y up. Unity metres.
# Length 15.5 m (z -7.5..8.0), beam 5.0 m, draft 1.5 m (keel at y -1.5). Main deck 1.25 (aft and waist), foredeck 1.55 (a 0.3 m step).
# Exports: Harbour_Core_CrewBoat (visual), Harbour_Core_CrewBoat_Col (walkable collision: hull, decks, bulwarks, deckhouse and wheelhouse
# walls/floors/roofs, rails, furniture), Harbour_Core_CrewBoat_IceChest (one chest model, 4 placements CrewBoat_IceChest_1..4, "pickup").
# The harbour copy is moored along the OUTER (east) face of the breakwater, bow to the sea, clear of every cast lane (see TUG_WORLD).
# ---------------------------------------------------------------------------------------------------------------------------------------
TUG_L0, TUG_L1, TUG_B = -7.5, 8.0, 5.0
TUG_DECK, TUG_FORE, TUG_FORE_Z = 1.25, 1.55, 3.9
TUG_HOUSE = (-2.6, 3.4, 1.55)          # deckhouse z0, z1, half width
TUG_HOUSE_TOP = 3.6                    # deckhouse roof top (the upper deck)
TUG_WH = (0.5, 3.2, 1.35)              # wheelhouse z0, z1, half width
TUG_WH_TOP = 5.85
# round 2b (critics: "the tug is jammed against the breakwater"): pulled 4.2 m off the wall (x 36.8 -> 41.0), clear of the rubble toe
# (x ~35 at the waterline) by ~3.5 m of open water, and 3 m north (z -22 -> -19: its stern leaves the lookout's 20 m lane; the gangway, at
# z -26, lands beside the moved breakwater-tip stand); the gangway spans the gap on a trestle. Lane clearances: verification in the manifest.
TUG_WORLD = dict(position=(41.0, 0.0, -19.0), yaw=0.0)
TUG_GANGWAY_Z = -7.0                   # local z of the port bulwark gap (gangway from the breakwater)
TUG_ICE_CHESTS = [(1.2, -6.3), (1.8, -5.05), (-0.95, -4.55), (0.65, -4.55)]
TUG_FISH_SPOTS = [('Bow', (0.0, TUG_FORE, 6.95), 0.0), ('PortRail', (-1.95, TUG_DECK, 0.6), 270.0), ('StarboardRail', (1.95, TUG_DECK, 0.6), 90.0),
                  ('Stern', (0.0, TUG_DECK, -6.7), 180.0), ('UpperDeckPort', (-1.2, TUG_HOUSE_TOP, -1.6), 270.0),
                  ('UpperDeckStarboard', (1.2, TUG_HOUSE_TOP, -1.6), 90.0)]

def tb(size, r):
    # rounded box for the tug: one bevel segment on small parts (rungs, posts, handles), two on the big ones
    return rbox(size, r, 1 if min(size) < 0.15 else 2)

def tug_t(z):
    return clamp((z - TUG_L0) / (TUG_L1 - TUG_L0), 0.0, 1.0)

def tug_w(z):
    # half beam at the sheer
    t = tug_t(z)
    if t < 0.2:
        return TUG_B / 2 * (0.55 + 0.45 * math.sin(0.5 * math.pi * t / 0.2))
    if t < 0.58:
        return TUG_B / 2
    return TUG_B / 2 * max(0.03, math.cos(0.5 * math.pi * (t - 0.58) / 0.42) ** 0.75)

def tug_sheer(z):
    t = tug_t(z)
    return TUG_DECK + 0.05 + 0.75 * max(0.0, (t - 0.55) / 0.45) ** 1.8

def tug_keel(z):
    t = tug_t(z)
    if t < 0.15:
        return -1.5 + 0.7 * (1 - t / 0.15) ** 2
    if t > 0.7:
        return -1.5 + 1.45 * ((t - 0.7) / 0.3) ** 2
    return -1.5

def tug_deck_y(z):
    return TUG_FORE if z >= TUG_FORE_Z else TUG_DECK

def tug_outline(step=0.5, inset=0.0):
    # closed deck-edge outline (x, z) going round: starboard stern -> bow -> port -> stern
    zs = frange(TUG_L0 + 0.02, TUG_L1 - 0.02, step)
    stb = [(tug_w(z) - inset, z) for z in zs]
    prt = [(-(tug_w(z) - inset), z) for z in reversed(zs)]
    return stb + prt

def build_tug_hull(g):
    # Lofted U sections (stern -> bow), subdivided once. Face slots: below the waterline dark red, a cream boot stripe at the waterline,
    # red topsides, a cream sheer stripe. (The top edge is the sheer; the deck and bulwarks are separate parts.)
    bm = bmesh.new()
    ns, nj = 21, 13
    rows = []
    for k in range(ns):
        z = TUG_L0 + (TUG_L1 - TUG_L0) * k / (ns - 1)
        z = min(max(z, TUG_L0 + 0.01), TUG_L1 - 0.01)
        w, s, kb = tug_w(z), tug_sheer(z), tug_keel(z)
        row = []
        for j in range(nj):
            th = -math.pi / 2 + math.pi * j / (nj - 1)
            x = w * math.sin(th)
            y = s - (s - kb) * max(0.0, math.cos(th)) ** 0.42
            row.append(bm.verts.new((x, y, z)))
        rows.append(row)
    for k in range(ns - 1):
        for j in range(nj - 1):
            bm.faces.new((rows[k][j], rows[k][j + 1], rows[k + 1][j + 1], rows[k + 1][j]))
    bm.faces.new(rows[0])                      # stern plate
    bm.normal_update()
    probe = [f for f in bm.faces if len(f.verts) == 4][(ns // 2) * (nj - 1) + 1]
    if probe.calc_center_median().x * probe.normal.x < 0 or probe.normal.y > 0.5:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    V, N, F, MI, FN = eval_cage(bm, [('SUBSURF', dict(levels=1, render_levels=1))])
    bm.free()
    UV, fs = [], []
    for i, f in enumerate(F):
        cy = sum(V[k].y for k in f) / len(f)
        cz = sum(V[k].z for k in f) / len(f)
        UV.append([box_uv(V[k], FN[i], (TUG_B, 3.0, 15.5)) for k in f])
        # round 2b: no cream boot stripe at the waterline (on the bow's big faces it zigzagged: critics read it as dents)
        if cy < -0.05:
            fs.append((S['Roof'], L['Wood']))
        elif cy > tug_sheer(cz) - 0.22:
            fs.append((S['Sand'], L['Wood']))
        else:
            fs.append((S['Roof'], L['Wood']))
    tint = []
    g.add((V, N, F, UV), 0, 0, 0.5, fs)
    # darken below the waterline (anti-fouling) through the tint
    n = len(F)
    for i in range(n):
        cy = sum(V[k].y for k in F[i]) / len(F[i])
        if cy < -0.05:
            g.TINT[len(g.TINT) - n + i] = 0.55

def sweep_profile(path, prof, closed=True, up=Vector((0, 1, 0))):
    # Sweep a 2D profile (n = outward offset, h = up) along a 3D path in the xz plane: outward = path tangent x up.
    P = [Vector(p) for p in path]
    n = len(P)
    V, N, F, UV = [], [], [], []
    m = len(prof)
    pn = []
    for i in range(m):
        a, b = prof[max(i - 1, 0)], prof[min(i + 1, m - 1)]
        dn, dh = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dn, dh) or 1.0
        pn.append((dh / ln, -dn / ln))
    for i in range(n):
        t = (P[(i + 1) % n] - P[i - 1]) if closed else (P[min(i + 1, n - 1)] - P[max(i - 1, 0)])
        t.y = 0.0
        t.normalize()
        out = Vector((-t.z, 0.0, t.x))
        for (on, oh), (nn, nh) in zip(prof, pn):
            V.append(P[i] + out * on + up * oh)
            N.append((out * nn + up * nh).normalized())
    rings = n if closed else n - 1
    for i in range(rings):
        a0, b0 = i * m, ((i + 1) % n) * m
        for k in range(m - 1):
            q = [a0 + k, b0 + k, b0 + k + 1, a0 + k + 1]
            F.append(q); UV.append([(V[j].x + V[j].z, V[j].y) for j in q])
    return V, N, F, UV

def tug_outline_orient(pts):
    # make the outline counter-clockwise seen from above in the maths frame used by sweep_profile (outward = (-t.z, 0, t.x))
    area = 0.0
    for i in range(len(pts)):
        x0, z0 = pts[i]; x1, z1 = pts[(i + 1) % len(pts)]
        area += x0 * z1 - x1 * z0
    return pts if area < 0 else list(reversed(pts))

def build_tug_bulwarks(g):
    # Bulwark walls on the sheer (cream inside, red outside, a wooden cap rail), a gap on the port side for the gangway, the rubber
    # fender ring just under the sheer, a bow "beard" fender and a rope bow pudding, tyres hung along both sides.
    outline = tug_outline_orient(tug_outline(0.45))
    pts = [(x, tug_sheer(z), z) for x, z in outline]
    def bul_h(z):
        return 0.82 + 0.35 * max(0.0, (tug_t(z) - 0.6) / 0.4)
    # split at the gangway gap (port side, x < 0, |z - gap| < 0.55)
    gap = lambda p: p[0] < 0 and abs(p[2] - TUG_GANGWAY_Z) < 0.55
    runs, cur = [], []
    for p in pts:
        if gap(p):
            if cur:
                runs.append(cur); cur = []
        else:
            cur.append(p)
    if cur:
        runs.append(cur)
    if len(runs) > 1 and not gap(pts[0]) and not gap(pts[-1]):
        runs[0] = runs[-1] + runs[0]; runs.pop()
    for run in runs:
        V, N, F, UV, fsl = [], [], [], [], []
        P = [Vector(p) for p in run]
        n = len(P)
        up = Vector((0, 1, 0))
        m = 6
        for i, p in enumerate(P):
            t = P[min(i + 1, n - 1)] - P[max(i - 1, 0)]
            t.y = 0.0
            t.normalize()
            o = Vector((t.z, 0.0, -t.x))
            if o.dot(Vector((p.x, 0.0, p.z - 0.3))) < 0:
                o = -o
            h = bul_h(p.z)
            ring = [(p - up * 0.02, o), (p + up * h, o), (p + o * -0.03 + up * (h + 0.05), (o + up).normalized()),
                    (p - o * 0.14 + up * (h + 0.05), (up - o).normalized()), (p - o * 0.17 + up * h, -o), (p - o * 0.17 - up * 0.08, -o)]
            for q, nn in ring:
                V.append(q); N.append(nn)
        for i in range(n - 1):
            for k in range(m - 1):
                a0, b0 = i * m + k, (i + 1) * m + k
                F.append([a0, b0, b0 + 1, a0 + 1])
                UV.append([(V[j].x + V[j].z, V[j].y) for j in F[-1]])
                fsl.append((S['Roof'], L['Wood']) if k == 0 else ((S['Sand'], L['Wood']) if k == 4 else (S['Wood'], L['Wood'])))
        # end caps at the gangway gap
        for i in (0, n - 1):
            ring = [i * m + k for k in range(m)]
            F.append(ring); UV.append([(V[j].x, V[j].y) for j in ring]); fsl.append((S['Wood'], L['Wood']))
            c = sum((V[j] for j in ring), Vector()) / m
            tdir = (P[1] - P[0]) if i == 0 else (P[-1] - P[-2])
            tdir.y = 0
            tdir.normalize()
            nn = -tdir if i == 0 else tdir
            base = len(V)
            for j in ring:
                V.append(V[j].copy()); N.append(nn)
            F[-1] = [base + k for k in range(m)]
        g.add((V, N, F, UV), 0, 0, 0.5, fsl)
    # fender ring (dark rubber) just below the sheer, all round
    ring = [Vector((x * 1.02 + (0.12 if x > 0 else -0.12), tug_sheer(z) - 0.28, z)) for x, z in tug_outline(0.5)]
    g.add(tube(ring, 0.17, 8, caps=False, closed=True), S['Rock'], L['none'], 0.5, tint=0.32)
    # bow beard: a thick rubber fender down the stem, plus a rope pudding on top
    stem = [Vector((0.0, tug_sheer(TUG_L1 - 0.05) - 0.3 - 0.2 * k, TUG_L1 + 0.12 - 0.05 * k * k)) for k in range(8)]
    g.add(tube(stem, 0.34, 10), S['Rock'], L['none'], 0.5, tint=0.32)
    pud = [Vector((0.9 * math.sin(a), tug_sheer(TUG_L1 - 0.4) - 0.1, TUG_L1 - 0.55 + 0.75 * math.cos(a))) for a in frange(-1.3, 1.3, 0.2)]
    g.add(tube(pud, 0.2, 8), S['Sand'], L['Cloth'], 0.55)
    # stern fender (rubber) around the round stern
    sf = [Vector((x * 1.04, 0.55, z - 0.06)) for x, z in tug_outline(0.4) if z < TUG_L0 + 2.2]
    sf.sort(key=lambda v: math.atan2(v.x, -(v.z - (TUG_L0 + 2.0))))
    g.add(tube(sf, 0.24, 8), S['Rock'], L['none'], 0.5, tint=0.32)
    # tyres on ropes along both sides
    for side in (-1, 1):
        for z in (-3.1, -1.0, 1.2, 3.2):
            if side < 0 and abs(z - TUG_GANGWAY_Z) < 1.0:
                continue
            w = tug_w(z)
            c = Vector((side * (w + 0.2), 0.75, z))
            ring2 = [Vector((0.0, 0.3 * math.sin(a), 0.3 * math.cos(a))) for a in [2 * math.pi * i / 12 for i in range(12)]]
            g.add(xform(tube(ring2, 0.13, 6, caps=False, closed=True), c, rz(side * 10)), S['Rock'], L['none'], 0.5, tint=0.3)
            top = Vector((side * (w - 0.05), tug_sheer(z) + 0.84, z))
            g.add(tube([top, c + Vector((0, 0.3, 0))], 0.018, 4), S['Sand'], L['Cloth'], 0.55)

def deck_surface(g, z0, z1, y, inset, slot=S['Wood'], step=0.5):
    zs = frange(z0, z1, step)
    V, N, F, UV = [], [], [], []
    for z in zs:
        w = max(0.05, tug_w(z) - inset)
        V += [Vector((-w, y, z)), Vector((0.0, y, z)), Vector((w, y, z))]
        N += [Vector((0, 1, 0))] * 3
    for i in range(len(zs) - 1):
        for k in range(2):
            a = i * 3 + k
            q = [a, a + 3, a + 4, a + 1]
            if (V[q[1]] - V[q[0]]).cross(V[q[2]] - V[q[0]]).y < 0:
                q = list(reversed(q))
            F.append(q); UV.append([(V[j].z, V[j].x) for j in q])
    g.add((V, N, F, UV), slot, L['Wood'], 0.5)

def build_tug_house(g, r):
    z0, z1, hw = TUG_HOUSE
    H0, H1 = TUG_DECK, TUG_HOUSE_TOP
    wt = 0.12
    hh = H1 - 0.12 - H0
    yc = H0 + hh / 2
    # deckhouse walls (cream) with an aft door opening at x -0.55 (0.95 wide, 2.0 high)
    for sx in (-1, 1):
        g.add(xform(tb((wt, hh, z1 - z0), 0.04), (sx * (hw - wt / 2), yc, (z0 + z1) / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((2 * hw, hh, wt), 0.04), (0.0, yc, z1 - wt / 2)), S['Sand'], L['Wood'], 0.5)
    dx0, dx1 = -1.02, -0.07
    g.add(xform(tb((dx0 + hw, hh, wt), 0.04), ((-hw + dx0) / 2, yc, z0 + wt / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((hw - dx1, hh, wt), 0.04), ((dx1 + hw) / 2, yc, z0 + wt / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((dx1 - dx0, hh - 2.0, wt), 0.03), ((dx0 + dx1) / 2, H0 + 2.0 + (hh - 2.0) / 2, z0 + wt / 2)), S['Sand'], L['Wood'], 0.5)
    # the door, swung open against the wall (orange), and a trim band
    g.add(xform(tb((0.9, 1.95, 0.06), 0.03), (dx0 - 0.02, H0 + 1.0, z0 - 0.48), ry(90)), S['Accent'], L['Wood'], 0.5)
    for sz in (z0 - 0.02, z1 + 0.02):
        g.add(xform(tb((2 * hw + 0.06, 0.1, 0.04), 0.015), (0, H0 + 0.08, sz)), S['Accent'], L['Wood'], 0.45)
    # roof slab (the upper deck), with a lip
    g.add(xform(tb((2 * hw + 0.3, 0.14, z1 - z0 + 0.3), 0.05), (0, H1 - 0.07, (z0 + z1) / 2)), S['Dock'], L['Wood'], 0.5)
    # portholes on the sides
    for sx in (-1, 1):
        for z in (z0 + 1.2, (z0 + z1) / 2, z1 - 1.2):
            ring = [Vector((0.0, 0.2 * math.sin(a), 0.2 * math.cos(a))) for a in [2 * math.pi * i / 10 for i in range(10)]]
            c = Vector((sx * (hw + 0.01), H0 + 1.55, z))
            g.add(xform(tube(ring, 0.04, 4, caps=False, closed=True), c), S['Accent'], L['none'], 0.5)
            g.add(xform(tb((0.02, 0.36, 0.36), 0.01), c + Vector((sx * 0.0, 0, 0))), S['Rock'], L['none'], 0.5, tint=0.5)
    # interior: lockers on the starboard wall, shelves with tins and boxes, a cot with a blanket and pillow on the port wall, a lamp
    for k in range(3):
        z = z0 + 0.9 + 0.62 * k
        g.add(xform(tb((0.5, 1.9, 0.58), 0.03), (hw - wt - 0.27, H0 + 0.95, z)), S['Reed'], L['none'], 0.5)
        g.add(xform(tb((0.02, 0.2, 0.08), 0.008), (hw - wt - 0.53, H0 + 1.15, z + 0.15)), S['Rock'], L['none'], 0.5)
        for v in range(3):
            g.add(xform(tb((0.02, 0.03, 0.3), 0.008), (hw - wt - 0.53, H0 + 1.7 + 0.06 * v, z - 0.05)), S['WoodDark'], L['none'], 0.5)
    for k, y in enumerate((H0 + 1.25, H0 + 1.75)):
        g.add(xform(tb((0.36, 0.04, 1.6), 0.012), (hw - wt - 0.2, y, z1 - 1.25)), S['Wood'], L['Wood'], 0.5)
        for j in range(4):
            if r.random() < 0.8:
                add_lathe(g, (hw - wt - 0.2, y + 0.02, z1 - 1.9 + 0.38 * j), [(0, 0.07), (0.16, 0.07), (0.17, 0.0)],
                          [S['Accent'], S['Roof'], S['Foliage'], S['Sand']][(j + k) % 4], L['none'], 0.55, sides=8, cap1=True)
    cz0, cz1 = z1 - 2.4, z1 - 0.25
    g.add(xform(tb((0.85, 0.42, cz1 - cz0), 0.05), (-hw + wt + 0.45, H0 + 0.25, (cz0 + cz1) / 2)), S['WoodDark'], L['Wood'], 0.5)
    g.add(xform(tb((0.8, 0.16, cz1 - cz0 - 0.1), 0.07), (-hw + wt + 0.45, H0 + 0.53, (cz0 + cz1) / 2)), S['Roof'], L['Cloth'], 0.55)
    g.add(xform(blob(0.25, r, (1.4, 0.45, 0.9), subdiv=2, rough=0.05, flat=-2), (-hw + wt + 0.45, H0 + 0.68, cz1 - 0.35)), S['Sand'], L['Cloth'], 0.6)
    g.add(xform(blob(0.45, r, (1.0, 0.25, 1.6), subdiv=2, rough=0.12, flat=-2), (-hw + wt + 0.4, H0 + 0.66, (cz0 + cz1) / 2 - 0.3)), S['Reed'], L['Cloth'], 0.6)
    add_lathe(g, (0.0, H1 - 0.5, (z0 + z1) / 2), [(0, 0.0), (0.02, 0.12), (0.22, 0.16), (0.26, 0.05), (0.38, 0.03)], S['Accent'], L['none'], 0.8,
              sides=10, cap0=True)
    g.extra['interior_light'] = [0.0, round(H1 - 0.45, 2), round((z0 + z1) / 2, 2)]
    # ladder: main deck -> upper deck, on the aft wall right of the door
    lx = 0.75
    for sx in (-0.22, 0.22):
        g.add(xform(tb((0.05, H1 + 0.9 - H0, 0.05), 0.018), (lx + sx, (H0 + H1 + 0.9) / 2, z0 - 0.2)), S['WoodDark'], L['none'], 0.4)
    y = H0 + 0.3
    while y < H1 + 0.6:
        g.add(xform(tb((0.44, 0.04, 0.04), 0.012), (lx, y, z0 - 0.2)), S['WoodDark'], L['none'], 0.4)
        y += 0.3
    g.extra.setdefault('ladders', []).append(dict(name='LadderToUpperDeck', bottom=[lx, H0, round(z0 - 0.2, 2)], top=[lx, H1, round(z0 - 0.2, 2)],
                                                  volume_min=[round(lx - 0.35, 2), H0, round(z0 - 0.75, 2)], volume_max=[round(lx + 0.35, 2), round(H1 + 1.0, 2), round(z0, 2)]))
    # upper deck rails (posts + two rails), open at the ladder top and in front of the wheelhouse door
    rail_pts = [(-hw - 0.1, z0 - 0.1), (hw + 0.1, z0 - 0.1), (hw + 0.1, z1 + 0.1), (-hw - 0.1, z1 + 0.1)]
    edges = [(rail_pts[0], rail_pts[1]), (rail_pts[1], rail_pts[2]), (rail_pts[2], rail_pts[3]), (rail_pts[3], rail_pts[0])]
    for (ax, az), (bx, bz) in edges:
        L_ = math.hypot(bx - ax, bz - az)
        n = max(1, int(L_ / 0.9))
        for k in range(n + 1):
            px, pz = ax + (bx - ax) * k / n, az + (bz - az) * k / n
            if abs(px - lx) < 0.4 and abs(pz - (z0 - 0.1)) < 0.05:
                continue
            g.add(xform(tb((0.05, 0.95, 0.05), 0.015), (px, H1 + 0.47, pz)), S['WoodDark'], L['none'], 0.45)
        for hy in (0.5, 0.95):
            if abs(az - (z0 - 0.1)) < 0.05 and abs(bz - (z0 - 0.1)) < 0.05:
                # split around the ladder top
                for (sx0, sx1) in ((ax, lx - 0.35), (lx + 0.35, bx)):
                    g.add(tube([Vector((sx0, H1 + hy, az)), Vector((sx1, H1 + hy, az))], 0.025, 6), S['Wood'], L['Wood'], 0.5)
            else:
                g.add(tube([Vector((ax, H1 + hy, az)), Vector((bx, H1 + hy, bz))], 0.025, 6), S['Wood'], L['Wood'], 0.5)

def build_tug_wheelhouse(g, r):
    z0, z1, hw = TUG_WH
    H0, H1 = TUG_HOUSE_TOP, TUG_WH_TOP
    wt = 0.1
    hh = H1 - 0.12 - H0
    yc = H0 + hh / 2
    # walls: a low wall band, corner posts, big windows above (dark glass), the aft wall with a door
    band = 1.0
    for sx in (-1, 1):
        g.add(xform(tb((wt, band, z1 - z0), 0.03), (sx * (hw - wt / 2), H0 + band / 2, (z0 + z1) / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((2 * hw, band, wt), 0.03), (0, H0 + band / 2, z1 - wt / 2)), S['Sand'], L['Wood'], 0.5)
    for sx in (-1, 1):
        for sz in (z0, z1):
            g.add(xform(tb((0.14, hh, 0.14), 0.03), (sx * (hw - 0.07), yc, sz + (0.07 if sz == z0 else -0.07))), S['Sand'], L['Wood'], 0.5)
    gy = H0 + band + (hh - band) / 2
    gh = hh - band - 0.1
    g.add(xform(tb((2 * hw - 0.2, gh, 0.04), 0.01), (0, gy, z1 - 0.06)), S['Rock'], L['none'], 0.5, tint=0.5)
    for sx in (-1, 1):
        g.add(xform(tb((0.04, gh, z1 - z0 - 0.25), 0.01), (sx * (hw - 0.06), gy, (z0 + z1) / 2)), S['Rock'], L['none'], 0.5, tint=0.5)
    for sx in (-1, 0, 1):   # mullions on the front
        g.add(xform(tb((0.07, gh, 0.07), 0.02), (sx * (hw - 0.1) * 0.5 if sx else 0.0, gy, z1 - 0.04)), S['WoodDark'], L['Wood'], 0.5)
    # aft wall: door opening at x -0.45 (0.85 wide, 1.95 high)
    dx0, dx1 = -0.9, -0.05
    g.add(xform(tb((dx0 + hw, hh, wt), 0.03), ((-hw + dx0) / 2, yc, z0 + wt / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((hw - dx1, hh, wt), 0.03), ((dx1 + hw) / 2, yc, z0 + wt / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((dx1 - dx0, hh - 1.95, wt), 0.03), ((dx0 + dx1) / 2, H0 + 1.95 + (hh - 1.95) / 2, z0 + wt / 2)), S['Sand'], L['Wood'], 0.5)
    g.add(xform(tb((0.36, 0.5, 0.03), 0.01), (0.65, H0 + 1.5, z0 - 0.01)), S['Rock'], L['none'], 0.5, tint=0.5)   # aft window
    # roof with an overhang and a sun visor at the front
    g.add(xform(tb((2 * hw + 0.4, 0.14, z1 - z0 + 0.5), 0.05), (0, H1 - 0.07, (z0 + z1) / 2 + 0.1)), S['Dock'], L['Wood'], 0.5)
    g.add(xform(tb((2 * hw + 0.3, 0.05, 0.45), 0.02), (0, H1 - 0.35, z1 + 0.25), rx(-18)), S['Accent'], L['Wood'], 0.5)
    # inside: console with the helm wheel, the rolled map and the thermos, a compass binnacle
    g.add(xform(tb((2 * hw - 0.3, 1.0, 0.55), 0.04), (0, H0 + 0.5, z1 - 0.4)), S['WoodDark'], L['Wood'], 0.5)
    wc = Vector((0.0, H0 + 1.25, z1 - 0.78))
    ring = [Vector((0.38 * math.cos(a), 0.38 * math.sin(a), 0.0)) for a in [2 * math.pi * i / 16 for i in range(16)]]
    g.add(xform(tube(ring, 0.03, 6, caps=False, closed=True), wc, rx(-20)), S['Wood'], L['Wood'], 0.5)
    for k in range(8):
        a = 2 * math.pi * k / 8
        g.add(xform(tube([Vector((0, 0, 0)), Vector((0.5 * math.cos(a), 0.5 * math.sin(a), 0))], 0.02, 4), wc, rx(-20)), S['Wood'], L['Wood'], 0.5)
    add_lathe(g, wc + Vector((0, 0, 0.05)), [(0, 0.06), (0.25, 0.05), (0.27, 0.0)], S['Rock'], L['none'], 0.5, sides=8, rot=rx(70))
    add_lathe(g, (0.75, H0 + 1.0, z1 - 0.45), [(0, 0.12), (0.15, 0.14), (0.24, 0.1), (0.26, 0.0)], S['Accent'], L['none'], 0.55, sides=10)
    geo, fs = lathe([(-0.32, 0.0), (-0.31, 0.06), (0.31, 0.06), (0.32, 0.0)], 10)
    g.add(xform(geo, (-0.7, H0 + 1.06, z1 - 0.45), rz(90) @ ry(15)), S['Sand'], L['Cloth'], 0.6)     # the rolled map
    add_lathe(g, (-0.95, H0 + 1.0, z1 - 0.32), [(0, 0.05), (0.24, 0.05), (0.26, 0.035), (0.31, 0.035), (0.32, 0.0)], S['Foliage'], L['none'], 0.55,
              sides=10)    # thermos
    # mast with a light, a searchlight, a horn
    add_lathe(g, (0.55, H1, (z0 + z1) / 2), [(0, 0.07), (2.6, 0.045), (2.62, 0.0)], S['WoodDark'], L['none'], 0.5, sides=8)
    g.add(xform(tb((1.0, 0.05, 0.05), 0.015), (0.55, H1 + 2.0, (z0 + z1) / 2)), S['WoodDark'], L['none'], 0.5)
    add_lathe(g, (0.55, H1 + 2.62, (z0 + z1) / 2), [(0, 0.0), (0.02, 0.09), (0.16, 0.09), (0.2, 0.0)], S['Accent'], L['none'], 1.0, sides=10)
    g.extra.setdefault('light_points', []).append(dict(name='MastLight', position=[0.55, round(H1 + 2.7, 2), round((z0 + z1) / 2, 2)], animHint='blink', frame='CrewBoat local'))
    add_lathe(g, (-0.7, H1 + 0.1, z1 - 0.3), [(0, 0.0), (0.02, 0.12), (0.25, 0.15), (0.27, 0.0)], S['Rock'], L['none'], 0.5, sides=10, rot=rx(80))
    add_lathe(g, (0.0, H1 + 0.08, z1 - 0.9), [(0, 0.03), (0.15, 0.04), (0.35, 0.12), (0.36, 0.0)], S['Accent'], L['none'], 0.6, sides=8, rot=rx(85))
    # ladder from the upper deck to the wheelhouse roof (aft wall, right of the door)
    lx = 0.75
    for sx in (-0.18, 0.18):
        g.add(xform(tb((0.04, H1 + 0.6 - H0, 0.04), 0.015), (lx + sx, (H0 + H1 + 0.6) / 2, z0 - 0.15)), S['WoodDark'], L['none'], 0.4)
    y = H0 + 0.3
    while y < H1 + 0.4:
        g.add(xform(tb((0.36, 0.035, 0.035), 0.01), (lx, y, z0 - 0.15)), S['WoodDark'], L['none'], 0.4)
        y += 0.3
    g.extra.setdefault('ladders', []).append(dict(name='LadderToWheelhouseRoof', bottom=[lx, H0, round(z0 - 0.15, 2)], top=[lx, H1, round(z0 - 0.15, 2)],
                                                  volume_min=[round(lx - 0.3, 2), H0, round(z0 - 0.65, 2)], volume_max=[round(lx + 0.3, 2), round(H1 + 1.0, 2), round(z0, 2)]))
    # roof rail
    for (ax, az, bx, bz) in ((-hw, z0 + 0.1, -hw, z1), (hw, z0 + 0.1, hw, z1), (-hw, z1, hw, z1)):
        g.add(tube([Vector((ax, H1 + 0.5, az)), Vector((bx, H1 + 0.5, bz))], 0.022, 6), S['Wood'], L['Wood'], 0.5)
        for t in (0.0, 0.5, 1.0):
            g.add(xform(tb((0.04, 0.5, 0.04), 0.012), (ax + (bx - ax) * t, H1 + 0.25, az + (bz - az) * t)), S['WoodDark'], L['none'], 0.45)

def build_tug_deckgear(g, r):
    # smokestack on the upper deck (raked aft), towing H-bitt, coiled lines, life rings, foredeck bitts and the anchor windlass,
    # the crew's gear: duffels, a cooler, jerry cans, a tarp-covered pile, a spare rolled map; a duct-taped patch on the hull.
    z0, z1, hw = TUG_HOUSE
    fc = Vector((-0.75, TUG_HOUSE_TOP, -1.3))
    rk = rx(-8)
    add_lathe(g, fc, [(0, 0.5), (0.3, 0.48), (1.6, 0.44), (1.62, 0.47), (1.95, 0.47), (1.97, 0.44), (2.3, 0.42), (2.32, 0.0)], S['Accent'], L['Wood'], 0.55,
              sides=14, rot=rk, ring_slots=[S['Accent'], S['Accent'], S['Sand'], S['Sand'], S['WoodDark'], S['WoodDark'], S['WoodDark']])
    g.extra.setdefault('smoke_points', []).append(dict(name='Smokestack', position=[round(fc.x, 2), round(fc.y + 2.35, 2), round(fc.z - 0.32, 2)], frame='CrewBoat local'))
    # towing H-bitt on the aft deck
    bz = -3.7
    for sx in (-0.5, 0.5):
        add_lathe(g, (sx, TUG_DECK, bz), [(0, 0.2), (0.05, 0.17), (0.62, 0.16), (0.66, 0.2), (0.72, 0.2), (0.74, 0.0)], S['WoodDark'], L['none'], 0.5,
                  sides=12, cap0=True)
    g.add(xform(tb((1.3, 0.14, 0.14), 0.04), (0, TUG_DECK + 0.5, bz)), S['WoodDark'], L['none'], 0.5)
    # coiled lines
    for (cx, cz, k) in ((-0.9, -6.75, 0), (1.0, -2.95, 1), (-0.75, 5.9, 2)):
        y = tug_deck_y(cz)
        pts = []
        for i in range(int(2.6 * 14) + 1):
            a = 2 * math.pi * i / 14
            rr = 0.12 + 0.2 * i / (2.6 * 14)
            pts.append(Vector((cx + rr * math.cos(a), y + 0.03 + 0.015 * math.sin(a * 3), cz + rr * math.sin(a))))
        g.add(tube(pts, 0.03, 5), S['Sand'], L['Cloth'], 0.5)
    # life rings on the deckhouse sides
    for sx in (-1, 1):
        lc = Vector((sx * (hw + 0.08), TUG_DECK + 1.5, z1 - 0.6))
        pts = [Vector((0.0, 0.26 * math.sin(a), 0.26 * math.cos(a))) for a in [2 * math.pi * i / 20 for i in range(20)]]
        V, N, F, UV = tube(pts, 0.065, 8, caps=False, closed=True)
        fsl = []
        for f in F:
            ang = math.atan2(sum(V[k].y for k in f), sum(V[k].z for k in f))
            fsl.append(((S['Sand'] if int(((ang + math.pi) / (math.pi / 4))) % 2 == 0 else S['Accent']), L['Cloth']))
        g.add(xform((V, N, F, UV), lc), 0, 0, 0.55, fsl)
    # foredeck: two bollards (bitts) and an anchor windlass, the anchor hanging at the bow
    for sx in (-0.55, 0.55):
        add_lathe(g, (sx, TUG_FORE, 6.45), [(0, 0.16), (0.3, 0.13), (0.34, 0.17), (0.4, 0.17), (0.42, 0.0)], S['WoodDark'], L['none'], 0.5, sides=12, cap0=True)
    geo, fs = lathe([(-0.55, 0.0), (-0.53, 0.18), (-0.35, 0.2), (0.35, 0.2), (0.53, 0.18), (0.55, 0.0)], 12)
    g.add(xform(geo, (0, TUG_FORE + 0.32, 5.3), rz(90)), S['Rock'], L['none'], 0.5)
    g.add(xform(tb((0.9, 0.3, 0.5), 0.06), (0, TUG_FORE + 0.15, 5.3)), S['WoodDark'], L['none'], 0.5)
    # the crew's gear on the aft deck (port side, out of the stern fishing spot): duffels, a cooler, jerry cans, a tarp-covered pile
    gear = [('duffel', (-1.6, -2.9, 20.0, S['Reed'])), ('duffel', (-1.45, -3.35, -12.0, S['Roof'])), ('duffel', (1.75, -2.9, 80.0, S['Dock'])),
            ('cooler', (1.6, -3.45, 5.0, S['Sand'])), ('jerry', (-2.0, -5.2, 0.0, S['Roof'])), ('jerry', (-1.95, -5.62, 4.0, S['Roof'])),
            ('jerry', (-1.6, -5.4, 8.0, S['Foliage'])), ('tarp', (1.45, 1.6, 0.0, S['Reed']))]
    for kind, (x, z, yaw, slot) in gear:
        y = tug_deck_y(z)
        rot = ry(yaw)
        if kind == 'duffel':
            geo, fs = lathe([(-0.4, 0.0), (-0.38, 0.12), (-0.3, 0.2), (0.3, 0.2), (0.38, 0.12), (0.4, 0.0)], 12)
            g.add(xform(geo, (x, y + 0.18, z), rot @ rz(90) @ rx(0)), slot, L['Cloth'], 0.55)
            g.add(xform(tube([Vector((-0.12, 0.33, 0)), Vector((-0.08, 0.45, 0)), Vector((0.08, 0.45, 0)), Vector((0.12, 0.33, 0))], 0.018, 4), (x, y, z), rot),
                  S['WoodDark'], L['none'], 0.5)
        elif kind == 'cooler':
            g.add(xform(tb((0.7, 0.42, 0.45), 0.06), (x, y + 0.21, z), rot), slot, L['none'], 0.6)
            g.add(xform(tb((0.74, 0.08, 0.49), 0.035), (x, y + 0.45, z), rot), S['Accent'], L['none'], 0.6)
        elif kind == 'jerry':
            g.add(xform(tb((0.2, 0.5, 0.36), 0.05), (x, y + 0.25, z), rot), slot, L['none'], 0.55)
            g.add(xform(tb((0.08, 0.06, 0.2), 0.02), (x, y + 0.53, z - 0.03), rot), slot, L['none'], 0.55)
            add_lathe(g, Vector((x, y + 0.5, z + 0.12)), [(0, 0.035), (0.07, 0.03), (0.08, 0.0)], S['Rock'], L['none'], 0.5, sides=6)
        elif kind == 'tarp':
            # starboard walkway is kept clear: the tarp pile sits on the deckhouse roof edge? no: lashed on the foredeck, starboard
            pass
    # a tarp-covered pile lashed on the foredeck (starboard), with two ropes over it
    tc = Vector((1.1, TUG_FORE, 4.75))
    g.add(xform(blob(0.65, r, (1.0, 0.55, 1.25), subdiv=2, rough=0.18, flat=-0.2), tc + Vector((0, 0.3, 0))), S['Reed'], L['Cloth'], 0.55)
    for dz in (-0.35, 0.35):
        g.add(tube([tc + Vector((-0.6, 0.05, dz)), tc + Vector((-0.3, 0.55, dz)), tc + Vector((0.3, 0.55, dz)), tc + Vector((0.6, 0.05, dz))], 0.02, 4),
              S['Sand'], L['Cloth'], 0.5)
    # spare rolled map sticking out of the red duffel
    geo, fs = lathe([(-0.3, 0.0), (-0.29, 0.04), (0.29, 0.04), (0.3, 0.0)], 8)
    g.add(xform(geo, (-1.45, TUG_DECK + 0.45, -3.35), rx(55) @ ry(20)), S['Sand'], L['Cloth'], 0.6)
    # the duct-taped patch on the port bow and rust streaks under the scuppers
    pz = 4.4
    w = tug_w(pz)
    for k, ang in enumerate((25.0, -25.0, 0.0)):
        c = Vector((-w - 0.035, 0.85, pz))
        g.add(xform(tb((0.012, 0.09, 0.62), 0.004), c, rx(ang)), S['Rock'], L['Cloth'], 0.7)
    for z in (-1.8, 0.6, 2.4):
        for sx in (-1, 1):
            w = tug_w(z)
            g.add(xform(tb((0.01, 0.42, 0.07), 0.004), (sx * (w + 0.018), tug_sheer(z) - 0.55, z)), S['WoodDark'], L['none'], 0.6, tint=0.8)
    # the name on the bow (both sides) and across the stern
    for sx in (-1, 1):
        # round 3 (critics: the name read "CKY BREAK" - the bow flare swallowed the ends of the word and the tyres hid the rest): a painted
        # nameboard on each side of the deckhouse, above the portholes, flat and fully readable from the breakwater and the basin
        hz0, hz1, hw = TUG_HOUSE
        z = (hz0 + hz1) / 2 - 0.2
        g.add(xform(tb((0.05, 0.38, 2.55), 0.02), (sx * (hw + 0.03), TUG_DECK + 1.98, z)), S['WoodDark'], L['Wood'], 0.5)
        sign_text(g, 'LUCKY BREAK', 0.25, (sx * (hw + 0.065), TUG_DECK + 1.9, z), ry(sx * 90.0), S['Sand'])
    sign_text(g, 'LUCKY BREAK', 0.3, (0.0, 0.95, TUG_L0 - 0.04), ry(180), S['Sand'])

def build_crew_boat():
    g = G2('Harbour_Core_CrewBoat', 'core', local=True, rays=20, anim='bob (gentle while moored)',
           note='The crew\'s tugboat (R76). Local model: origin at the waterline centre, +z forward. Reused by the M4 boat ride.')
    r = rng2('tug')
    build_tug_hull(g)
    build_tug_bulwarks(g)
    deck_surface(g, TUG_L0 + 0.1, TUG_FORE_Z, TUG_DECK, 0.17)
    deck_surface(g, TUG_FORE_Z, TUG_L1 - 0.35, TUG_FORE, 0.17)
    # the foredeck step face
    w = tug_w(TUG_FORE_Z) - 0.17
    g.add(xform(tb((2 * w, TUG_FORE - TUG_DECK, 0.08), 0.02), (0, (TUG_DECK + TUG_FORE) / 2, TUG_FORE_Z - 0.04)), S['WoodDark'], L['Wood'], 0.5)
    build_tug_house(g, r)
    build_tug_wheelhouse(g, r)
    build_tug_deckgear(g, r)
    pos, yaw = TUG_WORLD['position'], TUG_WORLD['yaw']
    g.place('CrewBoat', pos, yaw)
    g.extra['fishing_spots'] = [dict(name=n, local=[round(v, 2) for v in p], facing_local_deg=f) for n, p, f in TUG_FISH_SPOTS]
    g.extra['size'] = dict(length=TUG_L1 - TUG_L0, beam=TUG_B, draft=1.5, main_deck_y=TUG_DECK, foredeck_y=TUG_FORE, upper_deck_y=TUG_HOUSE_TOP,
                           wheelhouse_roof_y=TUG_WH_TOP)
    return g

def build_ice_chest():
    g = G2('Harbour_Core_CrewBoat_IceChest', 'core', local=True, rays=16, anim='pickup',
           note='One player ice chest (empty placeholder). Origin at the bottom centre, +z forward. 4 placements on the tug aft deck.')
    g.add(tb((0.78, 0.44, 0.5), 0.07), S['Sand'], L['none'], 0.6)
    g.V = [v + Vector((0, 0.22, 0)) for v in g.V]
    g.add(xform(tb((0.82, 0.09, 0.54), 0.04), (0, 0.48, 0)), S['Accent'], L['none'], 0.6)
    for sx in (-1, 1):
        g.add(xform(tb((0.04, 0.06, 0.2), 0.015), (sx * 0.41, 0.36, 0)), S['Rock'], L['none'], 0.5)
    g.add(xform(tb((0.14, 0.04, 0.03), 0.01), (0, 0.42, 0.27)), S['Rock'], L['none'], 0.5)
    for i, (x, z) in enumerate(TUG_ICE_CHESTS):
        g.place('CrewBoat_IceChest_%d' % (i + 1), (x, TUG_DECK, z), 0.0, frame='CrewBoat local', collider='box', size=[0.82, 0.53, 0.54])
    return g

def cbox(g, c, size, rot=None):
    # a closed collision box (8 corners, 6 quads, outward normals)
    rot = rot or Matrix.Identity(3)
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    V = [Vector(c) + rot @ Vector((sx * hx, sy * hy, sz * hz)) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    idx = lambda sx, sy, sz: ((sx > 0) * 4 + (sy > 0) * 2 + (sz > 0))
    F = []
    for axis in range(3):
        for sgn in (-1, 1):
            corners = []
            for a in (-1, 1):
                for b in (-1, 1):
                    s_ = [0, 0, 0]
                    s_[axis] = sgn
                    o = [i for i in range(3) if i != axis]
                    s_[o[0]] = a; s_[o[1]] = b
                    corners.append(idx(*s_))
            q = [corners[0], corners[1], corners[3], corners[2]]
            F.append(q)
    N = []
    for v in V:
        N.append((v - Vector(c)).normalized())
    # orientation is fixed by G2.add from these outward corner normals
    g.add((V, N, F, [[(0, 0)] * 4 for _ in F]), 0, 0, 1.0)

def build_crew_boat_col():
    # Walkable collision for the tug (local space). Hull block capped by the decks, bulwark walls with the gangway gap, the deckhouse
    # (walls with the door gap, interior floor = the deck, roof = the upper deck), the wheelhouse (walls with a door gap, roof), the rails,
    # furniture blocks. Ladders are LadderVolumes (manifest), not geometry. No uv, no AO.
    g = G2('Harbour_Core_CrewBoat_Col', 'col', local=True)
    # hull: coarse loft, closed by the two deck levels and the step
    zs = frange(TUG_L0 + 0.01, TUG_L1 - 0.01, 1.1)
    nj = 9
    V, N, F = [], [], []
    for z in zs:
        w, kb = tug_w(z), tug_keel(z)
        top = tug_deck_y(z)
        for j in range(nj):
            th = -math.pi / 2 + math.pi * j / (nj - 1)
            x = w * math.sin(th)
            y = top - (top - kb) * max(0.0, math.cos(th)) ** 0.42
            V.append(Vector((x, y, z))); N.append(Vector((math.sin(th), -math.cos(th), 0.0)).normalized())
    for i in range(len(zs) - 1):
        for j in range(nj - 1):
            a = i * nj + j
            F.append([a, a + 1, a + nj + 1, a + nj])
    # deck caps: fan between port and starboard tops per station
    for i in range(len(zs) - 1):
        a0, a1 = i * nj, i * nj + nj - 1
        b0, b1 = (i + 1) * nj, (i + 1) * nj + nj - 1
        F.append([a0, b0, b1, a1])
    for j in range(nj):
        N[j] = (N[j] + Vector((0, 0, -1))).normalized()
    # end plates
    F.append([k for k in range(nj)])
    last = (len(zs) - 1) * nj
    F.append([last + k for k in range(nj)])
    # deck-cap normals point up: give the top-edge verts an up bias so G2.add orients the caps upward
    for i in range(len(zs)):
        for j in (0, nj - 1):
            N[i * nj + j] = (N[i * nj + j] + Vector((0, 2.0, 0))).normalized()
    g.add((V, N, F, [[(0, 0)] * len(f) for f in F]), 0, 0, 1.0)
    # bulwarks: one wall box per outline segment, skipping the gangway gap
    out = tug_outline(1.0)
    for i in range(len(out)):
        (ax, az), (bx, bz) = out[i], out[(i + 1) % len(out)]
        mx, mz = (ax + bx) / 2, (az + bz) / 2
        if mx < 0 and abs(mz - TUG_GANGWAY_Z) < 0.6:
            continue
        L_ = math.hypot(bx - ax, bz - az)
        if L_ < 1e-3:
            continue
        yaw = math.degrees(math.atan2(bx - ax, bz - az))
        h = 0.82 + 0.35 * max(0.0, (tug_t(mz) - 0.6) / 0.4)
        base = tug_sheer(mz)
        cbox(g, (mx * 0.985, base + h / 2 - 0.05, mz), (0.18, h + 0.1, L_ + 0.05), ry(yaw))
    # deckhouse walls (door gap x -1.02..-0.07 aft), roof
    z0, z1, hw = TUG_HOUSE
    H0, H1 = TUG_DECK, TUG_HOUSE_TOP
    hh = H1 - H0
    for sx in (-1, 1):
        cbox(g, (sx * (hw - 0.06), H0 + hh / 2, (z0 + z1) / 2), (0.12, hh, z1 - z0))
    cbox(g, (0, H0 + hh / 2, z1 - 0.06), (2 * hw, hh, 0.12))
    cbox(g, ((-hw - 1.02) / 2, H0 + hh / 2, z0 + 0.06), (hw - 1.02, hh, 0.12))
    cbox(g, ((-0.07 + hw) / 2, H0 + hh / 2, z0 + 0.06), (hw + 0.07, hh, 0.12))
    cbox(g, (-0.545, H0 + 2.0 + (hh - 2.0) / 2, z0 + 0.06), (0.95, hh - 2.0, 0.12))
    cbox(g, (0, H1 - 0.07, (z0 + z1) / 2), (2 * hw + 0.3, 0.14, z1 - z0 + 0.3))
    # interior furniture: lockers (starboard), cot (port)
    cbox(g, (hw - 0.12 - 0.27, H0 + 0.95, z0 + 0.9 + 0.62), (0.52, 1.9, 1.86))
    cbox(g, (-hw + 0.12 + 0.45, H0 + 0.3, z1 - 1.325), (0.86, 0.6, 2.15))
    # upper-deck rails (open at the ladder top x 0.4..1.1 on the aft edge)
    for (cx, cz, sx_, sz_) in ((0.0, z1 + 0.1, 2 * hw + 0.2, 0.08), (-hw - 0.1, (z0 + z1) / 2, 0.08, z1 - z0 + 0.2),
                               (hw + 0.1, (z0 + z1) / 2, 0.08, z1 - z0 + 0.2), ((-hw - 0.1 + 0.4) / 2, z0 - 0.1, 0.4 + hw + 0.1, 0.08),
                               ((1.1 + hw + 0.1) / 2, z0 - 0.1, hw + 0.1 - 1.1, 0.08)):
        cbox(g, (cx, H1 + 0.5, cz), (sx_, 1.0, sz_))
    # wheelhouse walls (door gap x -0.9..-0.05 aft), roof, console
    w0, w1, whw = TUG_WH
    W0, W1 = TUG_HOUSE_TOP, TUG_WH_TOP
    wh = W1 - W0
    for sx in (-1, 1):
        cbox(g, (sx * (whw - 0.05), W0 + wh / 2, (w0 + w1) / 2), (0.1, wh, w1 - w0))
    cbox(g, (0, W0 + wh / 2, w1 - 0.05), (2 * whw, wh, 0.1))
    cbox(g, ((-whw - 0.9) / 2, W0 + wh / 2, w0 + 0.05), (whw - 0.9, wh, 0.1))
    cbox(g, ((-0.05 + whw) / 2, W0 + wh / 2, w0 + 0.05), (whw + 0.05, wh, 0.1))
    cbox(g, (-0.475, W0 + 1.95 + (wh - 1.95) / 2, w0 + 0.05), (0.85, wh - 1.95, 0.1))
    cbox(g, (0, W1 - 0.07, (w0 + w1) / 2 + 0.1), (2 * whw + 0.4, 0.14, w1 - w0 + 0.5))
    cbox(g, (0, W0 + 0.5, w1 - 0.4), (2 * whw - 0.3, 1.0, 0.55))
    for (ax, az, bx, bz) in ((-whw, w0 + 0.1, -whw, w1), (whw, w0 + 0.1, whw, w1), (-whw, w1, whw, w1)):
        cbox(g, ((ax + bx) / 2, W1 + 0.25, (az + bz) / 2), (max(0.06, abs(bx - ax)), 0.5, max(0.06, abs(bz - az))))
    # deck furniture: smokestack, H-bitt, foredeck bitts and windlass, tarp pile, gear
    cbox(g, (-0.75, TUG_HOUSE_TOP + 1.15, -1.45), (0.95, 2.3, 0.95))
    cbox(g, (0.0, TUG_DECK + 0.37, -3.7), (1.45, 0.74, 0.42))
    for sx in (-0.55, 0.55):
        cbox(g, (sx, TUG_FORE + 0.21, 6.45), (0.36, 0.42, 0.36))
    cbox(g, (0.0, TUG_FORE + 0.25, 5.3), (1.1, 0.5, 0.5))
    cbox(g, (1.1, TUG_FORE + 0.3, 4.75), (1.3, 0.6, 1.6))
    cbox(g, (-1.65, TUG_DECK + 0.2, -3.1), (0.75, 0.4, 0.95))
    cbox(g, (1.65, TUG_DECK + 0.25, -3.2), (0.8, 0.5, 0.95))
    cbox(g, (-1.85, TUG_DECK + 0.27, -5.4), (0.6, 0.55, 0.8))
    g.place('CrewBoat', TUG_WORLD['position'], TUG_WORLD['yaw'], anim='follows Harbour_Core_CrewBoat')
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Yard props (Core: each solid piece has a box collider in the manifest). Lamp posts along the paths, benches, the fish-cleaning table,
# net-drying rack frames, the half-painted boat on sawhorses, a hand cart, crate and barrel stacks, lobster-pot stacks, an anchor, the
# mailbox and noticeboard (WANTED poster), the office flagpole and the breakwater windsock pole, an upturned canoe, the smokehouse woodpile,
# a picket fence, the open field gate, and the gangway from the breakwater to the tug.
# ---------------------------------------------------------------------------------------------------------------------------------------
LAMPS2 = [(-3.0, -63.3), (-13.4, -63.5), (5.4, -63.0), (-24.9, -62.8), (20.0, -61.6)]
BENCHES = [('LunchBench', (-13.6, -59.8), 0.0), ('OfficeBench', (14.6, -59.6), -8.0)]
CLEAN_TABLE = (6.6, -58.1)
NOTICE = (4.7, -60.0)
RACKS = [(-26.2, -59.4), (-23.0, -59.4)]
SAWHORSE_BOAT = (21.2, -66.0, 78.0)
CART = (7.6, -64.6, 30.0)
CRATE_STACKS = [(-22.5, -63.6), (-22.4, -65.0), (13.6, -64.6), (-27.4, -64.3)]
POT_STACKS = [(-33.8, -60.5)]
ANCHOR = (13.4, -61.5)
MAILBOX = (7.6, -60.4)
FLAGPOLE = (13.0, -61.2)
WINDSOCK_POLE = (33.4, -58.2)
CANOE = (-6.0, -69.0, 20.0)
WOODPILE = (-41.5, -63.8)
GANGWAY_Z = TUG_WORLD['position'][2] + TUG_GANGWAY_Z

def ground(x, z):
    return land_y(x, z)

def lamp_post(g, x, z, name, y=None):
    y = ground(x, z) if y is None else y
    g.mark()
    add_box(g, (x, y + 0.06, z), (0.26, 0.12, 0.26), 0.03, S['WoodDark'], L['Wood'], 0.45)
    add_lathe(g, (x, y + 0.1, z), [(0, 0.08), (0.12, 0.068), (2.6, 0.056), (2.64, 0.07), (2.68, 0.06)], S['WoodDark'], L['Wood'], 0.5, sides=7)
    add_lathe(g, (x, y + 2.75, z), [(0, 0.05), (0.04, 0.12), (0.3, 0.135), (0.34, 0.08)], S['Accent'], L['none'], 0.6, sides=8, cap0=True)
    add_lathe(g, (x, y + 3.06, z), [(0, 0.17), (0.05, 0.16), (0.17, 0.04), (0.2, 0.035), (0.24, 0.05), (0.27, 0.0)], S['WoodDark'], L['none'], 0.5, sides=8, cap0=True)
    add_box(g, (x, y + 2.72, z), (0.16, 0.06, 0.16), 0.02, S['WoodDark'], L['none'], 0.5)
    # a hook arm for the string lights
    add_box(g, (x, y + 2.55, z), (0.7, 0.05, 0.05), 0.015, S['WoodDark'], L['none'], 0.5)
    d = g.solid(name, collider='box')
    d['light'] = [round(x, 2), round(y + 2.9, 2), round(z, 2)]
    return Vector((x, y + 2.52, z))

def bench(g, name, x, z, yaw, r, y=None):
    y = ground(x, z) if y is None else y
    rot = ry(yaw)
    P = lambda a, b, c: Vector((x, y, z)) + rot @ Vector((a, b, c))
    g.mark()
    for k in range(3):
        g.add(xform(rbox((1.8, 0.05, 0.13), 0.02, 1), P(0, 0.46, -0.16 + 0.15 * k), rot), S['Wood'], L['Wood'], 0.5)
    for k in range(2):
        g.add(xform(rbox((1.8, 0.12, 0.04), 0.015, 1), P(0, 0.72 + 0.16 * k, -0.27), rot @ rx(-10)), S['Wood'], L['Wood'], 0.5)
    for sx in (-0.75, 0.75):
        g.add(xform(rbox((0.08, 0.46, 0.4), 0.02, 1), P(sx, 0.23, -0.02), rot), S['WoodDark'], L['Wood'], 0.45)
        g.add(xform(rbox((0.06, 0.56, 0.06), 0.02, 1), P(sx, 0.72, -0.3), rot @ rx(-10)), S['WoodDark'], L['Wood'], 0.45)
    g.solid(name)
    return P

def cleaning_table(g, r):
    x, z = CLEAN_TABLE
    y = ground(x, z)
    g.mark()
    add_box(g, (x, y + 0.88, z), (1.6, 0.08, 0.7), 0.025, S['Wood'], L['Wood'], 0.5)
    for sx in (-0.7, 0.7):
        for sz in (-0.28, 0.28):
            add_box(g, (x + sx, y + 0.43, z + sz), (0.08, 0.86, 0.08), 0.02, S['WoodDark'], L['Wood'], 0.45)
    add_box(g, (x, y + 0.3, z), (1.4, 0.04, 0.6), 0.015, S['Wood'], L['Wood'], 0.5)   # lower shelf
    g.solid('FishCleaningTable')
    # a gull post next to it (a tall stump) - the gull itself is a perch placement
    g.mark()
    add_lathe(g, (x + 1.25, y, z + 0.3), [(-0.1, 0.13), (0.0, 0.12), (1.25, 0.11), (1.28, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=10)
    g.solid('GullPost')
    return Vector((x + 1.25, y + 1.28, z + 0.3))

def noticeboard(g, r):
    x, z = NOTICE
    y = ground(x, z)
    g.mark()
    for sx in (-0.65, 0.65):
        add_lathe(g, (x + sx, y - 0.1, z), [(0, 0.06), (2.1, 0.055), (2.12, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=8)
    add_box(g, (x, y + 1.45, z), (1.45, 0.9, 0.06), 0.02, S['Dock'], L['Wood'], 0.5)
    add_box(g, (x, y + 2.0, z + 0.05), (1.65, 0.07, 0.3), 0.02, S['WoodDark'], L['Wood'], 0.5)   # little roof
    g.solid('Noticeboard')
    wanted_poster(g, (x - 0.32, y + 1.47, z + 0.04), ry(0), r)
    # two other notices: a tide table and a lost-cat flyer, pinned crooked
    g.add(xform(rbox((0.36, 0.48, 0.008), 0.003, 1), (x + 0.3, y + 1.6, z + 0.035), rz(-6)), S['Sand'], L['Cloth'], 0.7)
    g.add(xform(rbox((0.3, 0.26, 0.008), 0.003, 1), (x + 0.42, y + 1.2, z + 0.035), rz(9)), S['Reed'], L['Cloth'], 0.7)
    sign_text(g, 'TIDES', 0.07, (x + 0.3, y + 1.76, z + 0.04), rz(-6), S['WoodDark'])
    g._mark = len(g.V)

def drying_rack(g, x, z, name):
    y = ground(x, z)
    g.mark()
    for sx in (-1.3, 1.3):
        for sz in (-0.35, 0.35):
            g.add(xform(rbox((0.07, 2.2, 0.07), 0.02, 1), (x + sx, y + 1.05, z + sz * 0.6), rx(sz * 22)), S['WoodDark'], L['Wood'], 0.45)
    g.add(xform(rbox((2.9, 0.08, 0.08), 0.02, 1), (x, y + 2.05, z)), S['Wood'], L['Wood'], 0.5)
    g.solid(name)
    return Vector((x, y + 2.05, z))

def sawhorse_boat(g, r):
    x, z, yaw = SAWHORSE_BOAT
    y = ground(x, z)
    rot = ry(yaw)
    g.mark()
    for zl in (-0.9, 0.9):
        c = Vector((x, y, z)) + rot @ Vector((0, 0, zl))
        g.add(xform(rbox((1.3, 0.08, 0.1), 0.02, 1), c + Vector((0, 0.72, 0)), rot), S['Wood'], L['Wood'], 0.5)
        for sx in (-0.5, 0.5):
            for sz in (-0.15, 0.15):
                g.add(xform(rbox((0.06, 0.78, 0.06), 0.015, 1), c + rot @ Vector((sx, 0.36, sz)), rot @ rx(sz * 70)), S['WoodDark'], L['Wood'], 0.45)
    # the boat: one half freshly painted (Accent), the other half still old and dark - the painter stopped for lunch
    geo, fs = hull(3.4, 1.35, 0.5, hull_slot=S['Accent'], stripe=S['Sand'])
    V, N, F, UV = geo
    fs2 = []
    for f, s_ in zip(F, fs):
        cz = sum(V[k].z for k in f) / len(f)
        sl, ly = s_
        if sl == S['Accent'] and cz < 0.1:
            sl = S['WoodDark']
        fs2.append((sl, ly))
    g.add(xform(geo, Vector((x, y + 1.17, z)), rot), 0, 0, 0.5, fs2)
    g.solid('BoatOnSawhorses')
    return Vector((x, y, z)), rot

def hand_cart(g, r):
    x, z, yaw = CART
    y = ground(x, z)
    rot = ry(yaw)
    P = lambda a, b, c: Vector((x, y, z)) + rot @ Vector((a, b, c))
    g.mark()
    g.add(xform(rbox((1.0, 0.08, 1.5), 0.025), P(0, 0.62, 0), rot), S['Wood'], L['Wood'], 0.5)
    for sx in (-1, 1):
        g.add(xform(rbox((0.05, 0.3, 1.5), 0.015, 1), P(sx * 0.5, 0.8, 0), rot), S['Dock'], L['Wood'], 0.5)
        g.add(xform(rbox((0.06, 0.06, 1.3), 0.02, 1), P(sx * 0.42, 0.66, -1.25), rot @ rx(-8)), S['WoodDark'], L['Wood'], 0.5)
        geo, fs = lathe([(-0.06, 0.0), (-0.05, 0.36), (0.05, 0.36), (0.06, 0.0)], 14)
        g.add(xform(geo, P(sx * 0.62, 0.36, 0.25), rot @ rz(90)), S['WoodDark'], L['Wood'], 0.5)
    g.add(xform(rbox((0.06, 0.6, 0.06), 0.02, 1), P(0, 0.3, 0.6), rot), S['WoodDark'], L['Wood'], 0.5)
    for k, (a, b, c, s_) in enumerate(((-0.2, 0.92, -0.3, 0.48), (0.22, 0.88, 0.25, 0.42), (0.0, 1.32, -0.05, 0.38))):
        g.add(xform(rbox((s_, s_, s_), 0.04), P(a, b, c), rot @ ry(r.uniform(-12, 12))), S['Wood'] if k != 1 else S['Dock'], L['Wood'], AO_WOOD)
    g.solid('HandCart')

def crate_stack(g, x, z, r, name):
    y = ground(x, z)
    g.mark()
    h = y
    n = r.randint(2, 3)
    for k in range(n):
        s_ = r.uniform(0.5, 0.62) * (1.0 - 0.08 * k)
        c = (x + r.uniform(-0.06, 0.06), h + s_ / 2, z + r.uniform(-0.06, 0.06))
        rot = ry(r.uniform(-14, 14))
        g.add(xform(rbox((s_, s_, s_), 0.035), c, rot), S['Wood'] if k % 2 == 0 else S['Dock'], L['Wood'], AO_WOOD)
        for dy in (-1, 1):
            g.add(xform(rbox((s_ + 0.01, 0.05, s_ + 0.01), 0.015, 1), Vector(c) + Vector((0, dy * (s_ / 2 - 0.03), 0)), rot), S['WoodDark'], L['Wood'], 0.45)
        h += s_
    # a barrel beside it
    bx, bz = x + 0.62, z + 0.15
    add_lathe(g, (bx, y, bz), [(0.0, 0.215), (0.02, 0.235), (0.2, 0.26), (0.39, 0.272), (0.58, 0.26), (0.76, 0.235), (0.78, 0.215)],
              S['WoodDark'], L['Wood'], 0.45, sides=12, cap0=True, cap1=True)
    for hh, rr in ((0.12, 0.252), (0.66, 0.252)):
        add_lathe(g, (bx, y, bz), [(hh - 0.035, rr - 0.004), (hh - 0.028, rr + 0.008), (hh + 0.028, rr + 0.008), (hh + 0.035, rr - 0.004)],
                  S['Rock'], L['none'], 0.5, sides=12)
    g.solid(name)

def pot_stack(g, x, z, r, name):
    y = ground(x, z)
    m0 = len(g.V)
    for k in range(r.randint(2, 3)):
        lobster_pot(g, name + '_pot', (x + r.uniform(-0.08, 0.08), y + 0.36 * k, z + r.uniform(-0.08, 0.08)), r.uniform(-25, 25), 0.0)
        g.items.pop()   # lobster_pot records its own item (and moves the mark); the stack gets one box
    g._mark = m0
    g.solid(name)

def anchor(g, r):
    x, z = ANCHOR
    y = ground(x, z)
    rot = ry(-20) @ rx(-14)
    c = Vector((x, y, z))
    g.mark()
    g.add(xform(rbox((0.12, 1.5, 0.12), 0.04), c + rot @ Vector((0, 0.95, 0)), rot), S['Rock'], L['none'], 0.5)
    g.add(xform(rbox((0.9, 0.1, 0.1), 0.04), c + rot @ Vector((0, 1.5, 0)), rot), S['Rock'], L['none'], 0.5)
    arc = [Vector((0.55 * math.sin(a), 0.6 - 0.45 * math.cos(a), 0)) for a in frange(-1.35, 1.35, 0.15)]
    g.add(xform(tube(arc, 0.07, 8), c, rot), S['Rock'], L['none'], 0.5)
    for s in (-1, 1):
        g.add(xform(blob(0.13, r, (1.0, 0.5, 0.6), subdiv=1, rough=0.05), c + rot @ Vector((s * 0.6, 0.62, 0)), rot @ rz(s * 40)), S['Rock'], L['none'], 0.5)
    ring = [Vector((0.0, 0.12 * math.cos(a), 0.12 * math.sin(a))) for a in [2 * math.pi * i / 10 for i in range(10)]]
    g.add(xform(tube(ring, 0.03, 5, caps=False, closed=True), c + rot @ Vector((0, 1.82, 0))), S['Rock'], L['none'], 0.5)
    g.solid('Anchor')

def mailbox(g):
    x, z = MAILBOX
    y = ground(x, z)
    g.mark()
    add_lathe(g, (x, y - 0.1, z), [(0, 0.05), (1.15, 0.045), (1.17, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=8)
    g.add(xform(rbox((0.3, 0.3, 0.5), 0.12), (x, y + 1.25, z)), S['Accent'], L['none'], 0.55)
    g.add(xform(rbox((0.03, 0.22, 0.05), 0.01, 1), (x + 0.17, y + 1.38, z - 0.12)), S['Roof'], L['none'], 0.55)    # the little flag is up
    g.add(xform(rbox((0.08, 0.08, 0.04), 0.01, 1), (x + 0.17, y + 1.5, z - 0.12)), S['Roof'], L['none'], 0.55)
    g.solid('Mailbox')

def flagpole(g, x, z, h, name):
    y = ground(x, z)
    g.mark()
    add_lathe(g, (x, y - 0.1, z), [(0, 0.07), (h, 0.04), (h + 0.02, 0.07), (h + 0.1, 0.0)], S['Sand'], L['none'], 0.55, sides=8)
    g.solid(name)
    return Vector((x, y + h - 0.15, z))

def canoe(g, r):
    x, z, yaw = CANOE
    y = ground(x, z)
    g.mark()
    geo, fs = hull(3.8, 0.85, 0.36, transom=0.05, sheer=0.25, hull_slot=S['Foliage'], stripe=S['Sand'])
    V, N, F, UV = geo
    flip = rz(180)
    g.add(xform(((V, N, F, UV)), Vector((x, y + 0.28, z)), ry(yaw) @ flip), 0, 0, 0.5, fs)
    g.solid('UpturnedCanoe')
    # a paddle beside it
    geo, fs = lathe([(-0.7, 0.0), (-0.68, 0.02), (0.3, 0.02), (0.35, 0.025), (0.7, 0.0)], 6)
    g.add(xform(geo, (x + 0.8, y + 0.04, z - 0.3), ry(yaw + 10) @ rx(90)), S['Wood'], L['Wood'], 0.5)
    g.add(xform(rbox((0.15, 0.02, 0.4), 0.008, 1), (x + 0.8 + 0.6 * math.sin(math.radians(yaw + 10)), y + 0.04, z - 0.3 + 0.6 * math.cos(math.radians(yaw + 10)))),
          S['Wood'], L['Wood'], 0.5)

def woodpile(g, r):
    x, z = WOODPILE
    y = ground(x, z)
    g.mark()
    for row in range(3):
        for k in range(5 - row):
            c = Vector((x - 0.6 + 0.26 * k + 0.13 * row, y + 0.13 + 0.22 * row, z))
            geo, fs = lathe([(-0.55, 0.0), (-0.54, 0.12), (0.54, 0.12), (0.55, 0.0)], 8, cap0=True, cap1=True)
            fsl = [(S['Wood'], L['Wood']) if f is None else (S['Wood'], L['Wood']) for f in fs]
            g.add(xform(geo, c, rx(90) @ ry(r.uniform(0, 90))), S['WoodDark'], L['Bark'], 0.45)
    # chopping block with the axe stuck in it
    add_lathe(g, (x + 1.2, y, z + 0.6), [(0, 0.3), (0.5, 0.28), (0.52, 0.0)], S['Wood'], L['Bark'], 0.5, sides=12, cap1=True)
    g.add(xform(rbox((0.04, 0.7, 0.04), 0.015, 1), (x + 1.25, y + 0.85, z + 0.55), rz(-28)), S['Wood'], L['Wood'], 0.5)
    g.add(xform(rbox((0.03, 0.12, 0.2), 0.01, 1), (x + 1.08, y + 0.56, z + 0.55), rz(-28)), S['Rock'], L['none'], 0.5)
    g.solid('Woodpile')

def picket_fence(g, pts, name, cheap=False, slot=None):
    # cheap (round 3, the lane gardens): plain 12-triangle pickets every 0.3 m with a pointed top (the rounded ones cost 44)
    g.mark()
    slot = S['Sand'] if slot is None else slot
    for (ax, az), (bx, bz) in zip(pts[:-1], pts[1:]):
        L_ = math.hypot(bx - ax, bz - az)
        n = max(1, int(L_ / (0.3 if cheap else 0.24)))
        yaw = math.degrees(math.atan2(bx - ax, bz - az))
        for k in range(n):
            t = (k + 0.5) / n
            x, z = ax + (bx - ax) * t, az + (bz - az) * t
            y = ground(x, z)
            if cheap:
                g.add(xform(prism([(-0.045, 0.0), (0.045, 0.0), (0.045, 0.82), (0.0, 0.9), (-0.045, 0.82)], 0.028, 0.0, segs=1),
                            (x, y - 0.05, z), ry(yaw)), slot, L['Wood'], 0.5)
            else:
                g.add(xform(rbox((0.08, 0.95, 0.03), 0.015, 1), (x, y + 0.45, z), ry(yaw + 90)), slot, L['Wood'], 0.5)
        for hh in (0.3, 0.7):
            mx, mz = (ax + bx) / 2, (az + bz) / 2
            g.add(xform(rbox((0.05, 0.07, L_), 0.015, 1), (mx, ground(mx, mz) + hh, mz - 0.03), ry(yaw)), slot, L['Wood'], 0.5)
    g.solid(name)

def gangway(g):
    # from the breakwater's east edge (x 32.0, top 1.5) to the tug's port bulwark gap (main deck 1.25)
    x0 = BREAKWATER[1] - 0.05
    tx, ty, tz = TUG_WORLD['position']
    x1 = tx - tug_w(TUG_GANGWAY_Z) + 0.35
    z = GANGWAY_Z
    a, b = Vector((x0, BREAKWATER_TOP, z)), Vector((x1, TUG_DECK + 0.02, z))
    mid = (a + b) / 2
    L_ = (b - a).length
    pitch = math.degrees(math.atan2(a.y - b.y, b.x - a.x))
    rot = ry(90) @ rx(pitch)
    g.mark()
    for k in range(int(L_ / 0.3)):
        t = (k + 0.5) / int(L_ / 0.3)
        g.add(xform(rbox((0.9, 0.05, 0.26), 0.015, 1), a.lerp(b, t) + Vector((0, 0.025, 0)), ry(90) @ rx(pitch)), S['Wood'], L['Wood'], 0.5)
    for s in (-1, 1):
        g.add(xform(rbox((0.08, 0.1, L_), 0.02, 1), mid + Vector((0, -0.04, s * 0.42)), rot), S['WoodDark'], L['Wood'], 0.45)
        for t in (0.05, 0.5, 0.95):
            p = a.lerp(b, t) + Vector((0, 0.45, s * 0.45))
            g.add(xform(rbox((0.05, 0.9, 0.05), 0.015, 1), p, rot), S['WoodDark'], L['Wood'], 0.45)
        g.add(tube([a + Vector((0, 0.9, s * 0.45)), b + Vector((0, 0.9, s * 0.45))], 0.025, 5), S['Sand'], L['Cloth'], 0.5)
    # round 2b: the longer span rests on a timber trestle in the water at its middle (visual; two piles and a cap)
    m_ = a.lerp(b, 0.5)
    for s_ in (-1, 1):
        g.add(taper_tube([Vector((m_.x, -2.5, z + s_ * 0.4)), Vector((m_.x, m_.y - 0.05, z + s_ * 0.4))], [0.13, 0.11], 7), S['WoodDark'], L['Wood'], 0.45)
    g.add(xform(rbox((0.24, 0.16, 1.2), 0.03, 1), Vector((m_.x, m_.y - 0.1, z))), S['WoodDark'], L['Wood'], 0.45)
    g.solid('Gangway_Visual', collider='none (see Gangway and GangwayRail items)')
    # colliders: the walk plank as a flat box (top 1.5 at the breakwater end; the tug end is a 0.23 m step down, under StepOffset 0.35)
    g.items.append(dict(name='Gangway', collider='box', position=[round((x0 + x1) / 2, 3), round((TUG_DECK + BREAKWATER_TOP) / 2, 3), round(z, 3)],
                        footprint=[round(x1 - x0, 3), round(BREAKWATER_TOP - TUG_DECK, 3), 0.9],
                        bounds_min=[round(x0, 3), TUG_DECK, round(z - 0.45, 3)], bounds_max=[round(x1, 3), BREAKWATER_TOP, round(z + 0.45, 3)],
                        note='walkable; the tug bobs (animHint), so either keep the moored bob tiny or parent the plank to nothing and accept a few cm'))
    for s_ in (-1, 1):
        zc = z + s_ * 0.45
        g.items.append(dict(name='GangwayRail', collider='box', position=[round((x0 + x1) / 2, 3), 1.95, round(zc, 3)],
                            footprint=[round(x1 - x0, 3), 0.9, 0.08], bounds_min=[round(x0, 3), 1.5, round(zc - 0.04, 3)],
                            bounds_max=[round(x1, 3), 2.4, round(zc + 0.04, 3)]))
    return a, b

def build_yard():
    gA = G2('Harbour_Core_Yard_A', 'core', rays=20)
    gB = G2('Harbour_Core_Yard_B', 'core', rays=20)
    r = rng2('yard')
    tops = []
    for i, (x, z) in enumerate(LAMPS2):
        tops.append(lamp_post(gA, x, z, 'LampPost_%d' % i))
    gA.extra['lamp_tops'] = [[round(v, 3) for v in t] for t in tops]
    for name, (x, z), yaw in BENCHES:
        bench(gA, name, x, z, yaw, r)
    perch = cleaning_table(gA, r)
    gA.extra['gull_perch_post'] = [round(v, 3) for v in perch]
    noticeboard(gA, r)
    rack_tops = [drying_rack(gA, x, z, 'NetRack_%d' % i) for i, (x, z) in enumerate(RACKS)]
    gA.extra['rack_tops'] = [[round(v, 3) for v in t] for t in rack_tops]
    mailbox(gA)
    gA.extra['flag_office'] = [round(v, 3) for v in flagpole(gA, FLAGPOLE[0], FLAGPOLE[1], 5.2, 'Flagpole')]
    gA.extra['windsock'] = [round(v, 3) for v in flagpole(gA, WINDSOCK_POLE[0], WINDSOCK_POLE[1], 4.0, 'WindsockPole')]
    anchor(gA, r)
    gangway(gA)
    bpos, brot = sawhorse_boat(gB, r)
    gB.extra['boatyard'] = [round(v, 3) for v in bpos]
    hand_cart(gB, r)
    for i, (x, z) in enumerate(CRATE_STACKS):
        crate_stack(gB, x, z, r, 'CrateStack_%d' % i)
    for i, (x, z) in enumerate(POT_STACKS):
        pot_stack(gB, x, z, r, 'PotStack_%d' % i)
    canoe(gB, r)
    woodpile(gB, r)
    picket_fence(gB, [(8.2, -65.9), (12.8, -65.9), (14.6, -65.2)], 'PicketFence_Office')
    picket_fence(gB, [(-34.6, -69.6), (-38.0, -70.4), (-41.6, -69.8)], 'PicketFence_Smokehouse')
    return [gA, gB]

# ---------------------------------------------------------------------------------------------------------------------------------------
# Animated pieces (Detail, local models with placements and an animHint for the Unity side): the toon gull (perch points), a bobbing
# float buoy, flags, a windsock, the bait shop's swinging fish sign, hanging nets on the drying racks, three bunting strings, the weather
# vane on the harbour office. Each FBX holds one model around its pivot; the manifest lists its placements (Unity position + yaw).
# ---------------------------------------------------------------------------------------------------------------------------------------
def build_gull():
    g = G2('Harbour_Detail_Anim_Gull', 'detail', local=True, rays=12, anim='gull',
           note='Chunky toon gull, ~0.45 m. Origin between the feet. Perch points: head turns, hops, an occasional squawk (sound emitter).')
    r = rng2('gull')
    body = Vector((0.0, 0.2, 0.0))
    g.add(xform(blob(0.17, r, (0.85, 0.8, 1.35), subdiv=2, rough=0.04, flat=-2), body), S['Sand'], L['none'], 0.65)
    head = Vector((0.0, 0.36, 0.15))
    g.add(xform(blob(0.095, r, (0.95, 1.0, 1.05), subdiv=2, rough=0.03, flat=-2), head), S['Sand'], L['none'], 0.65)
    for s in (-1, 1):   # folded wings, grey, tips dark
        g.add(xform(blob(0.15, r, (0.32, 0.6, 1.5), subdiv=2, rough=0.03, flat=-2), body + Vector((s * 0.12, 0.04, -0.05)), rz(s * 10) @ rx(8)), S['Rock'], L['none'], 0.6)
        g.add(xform(blob(0.06, r, (0.4, 0.4, 1.8), subdiv=1, rough=0.02, flat=-2), body + Vector((s * 0.1, 0.03, -0.25)), rx(12)), S['WoodDark'], L['none'], 0.6)
        g.add(xform(blob(0.022, r, (1, 1, 1), subdiv=1, rough=0.0, flat=-2), head + Vector((s * 0.07, 0.025, 0.045))), S['WoodDark'], L['none'], 0.6, tint=0.4)
    add_lathe(g, head + Vector((0, -0.01, 0.08)), [(0, 0.03), (0.06, 0.026), (0.11, 0.012), (0.12, 0.0)], S['Accent'], L['none'], 0.7, sides=8, rot=rx(90))
    for s in (-1, 1):
        add_lathe(g, (s * 0.045, 0.0, 0.0), [(0, 0.014), (0.1, 0.012), (0.11, 0.0)], S['Accent'], L['none'], 0.6, sides=5)
        g.add(xform(rbox((0.06, 0.012, 0.07), 0.005, 1), (s * 0.045, 0.006, 0.02)), S['Accent'], L['none'], 0.6)
    g.add(xform(blob(0.06, r, (0.9, 0.35, 1.4), subdiv=1, rough=0.02, flat=-2), body + Vector((0, 0.02, -0.25)), rx(18)), S['Sand'], L['none'], 0.6)
    return g

def build_float_buoy():
    g = G2('Harbour_Detail_Anim_Buoy', 'detail', local=True, rays=8, anim='bob',
           note='A lobster-pot float with a flag stick. Origin at the waterline. Visual only: no collider, does not change castable cells.')
    prof = [(-0.18, 0.0), (-0.16, 0.12), (-0.05, 0.19), (0.08, 0.19), (0.17, 0.13), (0.22, 0.0)]
    add_lathe(g, (0, 0, 0), prof, S['Accent'], L['none'], 0.6, sides=12, ring_slots=[S['Accent'], S['Sand'], S['Sand'], S['Accent'], S['Accent']])
    add_lathe(g, (0, 0.2, 0), [(0, 0.015), (0.9, 0.012), (0.92, 0.0)], S['WoodDark'], L['none'], 0.6, sides=5)
    g.add(xform(rbox((0.01, 0.16, 0.24), 0.004, 1), (0, 1.02, 0.12)), S['Roof'], L['Cloth'], 0.7)
    return g

def cloth_panel(w, h, nu, nv, wave, phase=0.0, sag=0.0):
    # a rectangular cloth hanging from its hoist edge (local x 0..w along the pole side? no: x 0..w away from the hoist, y down 0..-h),
    # waved in z; two faces (front +z-ish and back) with smooth normals.
    V, N, F, UV = [], [], [], []
    for side in (1, -1):
        base = len(V)
        for j in range(nv + 1):
            for i in range(nu + 1):
                u, v = i / nu, j / nv
                x = u * w
                y = -v * h - sag * math.sin(math.pi * u) * (1 - v)
                z = wave * u * math.sin(2.4 * math.pi * u + phase + 0.6 * v)
                dz = wave * (math.sin(2.4 * math.pi * u + phase + 0.6 * v) + u * 2.4 * math.pi * math.cos(2.4 * math.pi * u + phase + 0.6 * v)) / w
                n = Vector((-dz, 0.0, 1.0)).normalized() * side
                V.append(Vector((x, y, z - 0.004 * (1 - side)))); N.append(n)
        for j in range(nv):
            for i in range(nu):
                a = base + j * (nu + 1) + i
                F.append([a, a + 1, a + nu + 2, a + nu + 1])
                UV.append([(V[k].x, V[k].y) for k in F[-1]])
    return V, N, F, UV

def build_flag():
    g = G2('Harbour_Detail_Anim_Flag', 'detail', local=True, rays=0, anim='sway',
           note='A harbour flag, hoist at the origin, flying along +x. Rotate the placement to the wind; sway/flutter in Unity.')
    V, N, F, UV = cloth_panel(1.3, 0.8, 8, 4, 0.08)
    fsl = []
    for f in F:
        cy = sum(V[k].y for k in f) / 4
        fsl.append((S['Accent'] if cy > -0.27 or cy < -0.53 else S['Sand'], L['Cloth']))
    g.add((V, N, F, UV), 0, 0, 0.8, fsl)
    return g

def build_windsock():
    g = G2('Harbour_Detail_Anim_WindSock', 'detail', local=True, rays=0, anim='sway (points downwind, droops in calm)',
           note='Striped windsock on a swivel ring; origin at the swivel, the sock streams along +x.')
    ring = [Vector((0.0, 0.2 * math.sin(a), 0.2 * math.cos(a))) for a in [2 * math.pi * i / 12 for i in range(12)]]
    g.add(tube(ring, 0.02, 5, caps=False, closed=True), S['Rock'], L['none'], 0.7)
    prof = [(0.0, 0.2), (0.4, 0.19), (0.8, 0.17), (1.2, 0.15), (1.6, 0.12), (1.8, 0.11)]
    # the sock: an open cone along +x, two-sided (outer and inner), drooping a little
    for side in (1, -1):
        geo, fs = lathe(prof if side > 0 else [(h, rr - 0.006) for h, rr in prof], 12)
        V, N, F, UV = geo
        if side < 0:
            N = [-n for n in N]
        fsl = [(S['Accent'] if int(sum(V[k].y for k in f) / len(f) / 0.4) % 2 == 0 else S['Sand'], L['Cloth']) for f in F]
        V2 = [Vector((v.y, -0.04 * (v.y / 1.8) ** 2 + v.x * 0.0, 0.0)) + Vector((0, v.x, v.z)) for v in V]
        N2 = [Vector((n.y, n.x, n.z)) for n in N]
        g.add((V2, N2, F, UV), 0, 0, 0.8, fsl)
    add_lathe(g, (0, -0.25, 0), [(0, 0.03), (0.25, 0.03), (0.26, 0.0)], S['Rock'], L['none'], 0.7, sides=6)
    return g

def build_swing_sign():
    g = G2('Harbour_Detail_Anim_SwingSign', 'detail', local=True, rays=10, anim='swing (about the local x axis)',
           note='The bait shop\'s hanging fish sign. Origin at the hinge on the bracket; it hangs down -y and faces +z/-z.')
    r = rng2('swingsign')
    for s in (-1, 1):
        g.add(tube([Vector((s * 0.32, 0.0, 0.0)), Vector((s * 0.34, -0.22, 0.0))], 0.012, 4), S['Rock'], L['none'], 0.6)
    # a fat cartoon fish, cut from a plank: body, tail, a big eye, painted both sides
    c = Vector((0.0, -0.45, 0.0))
    g.add(xform(blob(0.26, r, (1.6, 0.85, 0.16), subdiv=2, rough=0.04, flat=-2), c), S['Accent'], L['Wood'], 0.6)
    g.add(xform(blob(0.2, r, (0.75, 1.15, 0.16), subdiv=2, rough=0.03, flat=-2), c + Vector((0.42, 0.0, 0.0))), S['Accent'], L['Wood'], 0.6)
    g.add(xform(blob(0.09, r, (1.6, 0.6, 0.18), subdiv=1, rough=0.02, flat=-2), c + Vector((0.3, 0.0, 0.0))), S['Accent'], L['Wood'], 0.6)
    for zs in (-1, 1):
        g.add(xform(blob(0.06, r, (1, 1, 0.3), subdiv=1, rough=0.0, flat=-2), c + Vector((-0.24, 0.05, zs * 0.045))), S['Sand'], L['none'], 0.8)
        g.add(xform(blob(0.028, r, (1, 1, 0.3), subdiv=1, rough=0.0, flat=-2), c + Vector((-0.26, 0.05, zs * 0.06))), S['WoodDark'], L['none'], 0.6, tint=0.4)
        for k in range(3):
            g.add(xform(rbox((0.02, 0.18, 0.01), 0.006, 1), c + Vector((-0.02 + 0.1 * k, 0.0, zs * 0.05))), S['Roof'], L['none'], 0.7)
    sign_text(g, 'BAIT', 0.13, c + Vector((0.04, -0.12, 0.05)), ry(0), S['Sand'])
    sign_text(g, 'BAIT', 0.13, c + Vector((0.04, -0.12, -0.05)), ry(180), S['Sand'])
    return g

def build_hanging_net():
    g = G2('Harbour_Detail_Anim_HangingNet', 'detail', local=True, rays=0, anim='sway',
           note='A fishing net draped over a drying-rack pole (both sides hang). Origin on the pole axis, the pole along x.')
    for side in (1, -1):
        V, N, F, UV = cloth_panel(2.5, 1.1 if side > 0 else 0.8, 14, 6, 0.09, phase=side * 0.7, sag=0.18)
        # hang it from the pole on one side: x along the pole, y down; bunched folds (gathered toward the hanging points) and a bulge
        V2 = []
        for v in V:
            u = v.x / 2.5
            fold = 0.07 * math.sin(u * math.pi * 7.0) * (-v.y)
            V2.append(Vector((v.x - 1.25 + 0.12 * math.sin(u * math.pi * 3.0) * (-v.y), v.y, v.z * 0.6 + fold + side * (0.05 + 0.16 * (-v.y)))))
        g.add((V2, N, F, UV), S['Reed'] if side > 0 else S['Dock'], L['Cloth'], 0.55)
    r = rng2('net.floats')
    for k in range(6):
        x = -1.15 + 0.46 * k
        g.add(xform(blob(0.05, r, (1.0, 0.8, 1.4), subdiv=1, rough=0.02, flat=-2), (x, -1.3, 0.2)), S['Accent'], L['none'], 0.7)
    return g

def build_bunting(name, a, b, sag, seed):
    a, b = Vector(a), Vector(b)
    mid = (a + b) / 2
    g = G2('Harbour_Detail_Anim_' + name, 'detail', local=True, rays=0, anim='sway',
           note='A bunting string, authored around the midpoint of its two anchors (the placement). Sway about the a-b axis.')
    pts = rope_line(a - mid, b - mid, sag, n=16)
    g.add(tube(pts, 0.01, 4, caps=False), S['Sand'], L['Cloth'], 0.7)
    L_ = (b - a).length
    n = max(4, int(L_ / 0.42))
    cols = [S['Accent'], S['Roof'], S['Sand'], S['Reed'], S['Dock']]
    for k in range(n):
        t = (k + 0.5) / n
        p = (a - mid).lerp(b - mid, t) - Vector((0, sag * 4 * t * (1 - t), 0))
        d = (b - a).normalized()
        w = 0.15
        q0, q1 = p - d * w, p + d * w
        tip = p - Vector((0, 0.3, 0))
        nrm = (q1 - q0).cross(tip - q0).normalized()
        slot = cols[(k + seed) % len(cols)]
        g.add(([q0, q1, tip], [nrm] * 3, [[0, 1, 2]], [[(0, 0), (1, 0), (0.5, 1)]]), slot, L['Cloth'], 0.8)
        g.add(([q0 - nrm * 0.004, q1 - nrm * 0.004, tip - nrm * 0.004], [-nrm] * 3, [[0, 2, 1]], [[(0, 0), (0.5, 1), (1, 0)]]), slot, L['Cloth'], 0.8)
    g.place(name, mid, 0.0)
    g.extra['anchors'] = [[round(v, 3) for v in a], [round(v, 3) for v in b]]
    return g

def build_weather_vane():
    g = G2('Harbour_Detail_Anim_WeatherVane', 'detail', local=True, rays=0, anim='spin (turns to the wind)',
           note='A fish weather vane over N/E/S/W arms; origin at the spindle base. Only the upper part (y > 0.25) should spin.')
    r = rng2('vane')
    add_lathe(g, (0, 0, 0), [(0, 0.03), (0.6, 0.02), (0.62, 0.0)], S['Rock'], L['none'], 0.6, sides=6)
    for k in range(4):
        a = k * 90.0
        g.add(xform(rbox((0.012, 0.012, 0.5), 0.004, 1), (0, 0.22, 0), ry(a)), S['Rock'], L['none'], 0.6)
    g.add(xform(blob(0.13, r, (1.9, 0.8, 0.12), subdiv=2, rough=0.03, flat=-2), (0, 0.5, 0.05), ry(90)), S['Accent'], L['none'], 0.6)
    g.add(xform(blob(0.09, r, (0.5, 1.2, 0.12), subdiv=1, rough=0.02, flat=-2), (0, 0.5, -0.27), ry(90)), S['Accent'], L['none'], 0.6)
    return g

def build_anim_models(yard_groups, buildings, jetty=None, upper=None):
    yA = yard_groups[0]
    out = []
    gull = build_gull()
    perches = [('Gull_PierLamp', (-1.35, DECK_TOP + 3.03, -46.0), 200.0), ('Gull_HutRidge', (-9.6, PLATEAU_Y + 2.97, -63.0), 150.0),
               ('Gull_BaitShopRidge', (-17.2, PLATEAU_Y + 3.74, -63.0), 30.0), ('Gull_CleaningPost', tuple(yA.extra['gull_perch_post']), 250.0),
               ('Gull_Noticeboard', (NOTICE[0] + 0.3, land_y(*NOTICE) + 2.08, NOTICE[1] + 0.02), 120.0),
               ('Gull_StoneWall', (-20.0, DUNE_BASE + T1_H + 0.42, T1_Z - 0.05), 300.0),
               ('Gull_MooringBuoy', (8.0, 0.35, -17.0), 80.0),
               ('Gull_TugWheelhouse', (TUG_WORLD['position'][0] + 0.9, TUG_WH_TOP, TUG_WORLD['position'][2] + 2.6), 190.0),
               ('Gull_BoatOnSawhorses', (SAWHORSE_BOAT[0], land_y(SAWHORSE_BOAT[0], SAWHORSE_BOAT[1]) + 1.45, SAWHORSE_BOAT[1]), 60.0)]
    for name, p, yaw in perches:
        gull.place(name, p, yaw)
    gull.extra['perch_points_spare'] = [[round(v, 2) for v in p] for p in ((-4.8, DECK_TOP + 3.03, -46.0),)]
    out.append(gull)
    buoy = build_float_buoy()
    for name, (x, z) in (('Float_A', (-40.5, -24.0)), ('Float_B', (-38.0, -21.5)), ('Float_C', (45.0, -14.0)), ('Float_D', (-36.0, -14.0))):
        buoy.place(name, (x, 0.0, z), 0.0)
    out.append(buoy)
    flag = build_flag()
    flag.place('Flag_HarbourOffice', tuple(yA.extra['flag_office']), 70.0)
    tw = TUG_WORLD['position']
    flag.place('Flag_TugStern', (tw[0] + 0.0, TUG_DECK + 2.3, tw[2] + TUG_L0 + 0.35), 160.0, frame='world (follows the tug: local (0, 2.3, -7.15))')
    if jetty is not None:
        flag.place('Flag_Lookout', tuple(jetty.extra['pennant']), 60.0)
    out.append(flag)
    ws = build_windsock()
    ws.place('WindSock_Breakwater', tuple(yA.extra['windsock']), 60.0)
    out.append(ws)
    sw = build_swing_sign()
    sw.place('SwingSign_BaitShop', (BAIT_SHOP[0] + BAIT_SHOP[2] / 2 + 0.05, PLATEAU_Y + 2.3, BAIT_SHOP[1] + BAIT_SHOP[3] / 2 + 0.55), 90.0)
    out.append(sw)
    net = build_hanging_net()
    for i, t in enumerate(yA.extra['rack_tops']):
        net.place('Net_Rack_%d' % i, tuple(t), 0.0)
    out.append(net)
    lt = yA.extra['lamp_tops']
    bx, bz, bw, bd = BAIT_SHOP
    out.append(build_bunting('Bunting_A', (bx - bw / 2 - 0.05, PLATEAU_Y + 2.35, bz + bd / 2 + 0.05), (lt[3][0] + 0.3, lt[3][1], lt[3][2]), 0.35, 0))
    out.append(build_bunting('Bunting_B', (FLAGPOLE[0], PLATEAU_Y + 3.4, FLAGPOLE[1]), (lt[4][0] - 0.3, lt[4][1], lt[4][2]), 0.4, 2))
    out.append(build_bunting('Bunting_C', (-7.0, PLATEAU_Y + 2.0, -61.5 + 0.05), (lt[0][0] - 0.3, lt[0][1], lt[0][2]), 0.3, 4))
    if upper is not None:
        # round 2b: bunting along the upper lane between its lamps (critics: "a straight, empty avenue")
        ut = upper.extra['lamp_tops']
        out.append(build_bunting('Bunting_D', tuple(ut[0]), tuple(ut[1]), 0.55, 1))
        out.append(build_bunting('Bunting_E', tuple(ut[2]), tuple(ut[3]), 0.55, 3))
    wv = build_weather_vane()
    wv.place('WeatherVane_HarbourOffice', tuple(buildings.extra['weather_vane']), 0.0)
    if upper is not None:
        wv.place('WeatherVane_Chapel', tuple(upper.extra['chapel_vane']), 30.0)
    out.append(wv)
    return out

# ---------------------------------------------------------------------------------------------------------------------------------------
# Detail (collider-free; hidden on Low): string lights, small clutter that tells the little stories, plants, shoreline dressing, reeds,
# tide-pool rocks. Core trees (with colliders) live here too because they share the placement rules.
# ---------------------------------------------------------------------------------------------------------------------------------------
def build_lights(yA):
    g = G2('Harbour_Detail_Lights', 'detail', rays=0,
           note='String lights: wires + bulbs in the Accent slot (make Accent glow at dusk/night in Unity, or add a few point lights at light_points).')
    lt = [Vector(t) for t in yA.extra['lamp_tops']]
    hx, hz = HUT_C[0], HUT_C[2]
    bx, bz, bw, bd = BAIT_SHOP
    ox, oz, ow, od = DOCK_OFFICE
    nx, nz, nw, nd = NET_SHED
    hooks = [(lt[0] + Vector((0.32, 0, 0)), Vector((-1.91, PLATEAU_Y + 1.9, -58.8))),                          # lamp 0 -> pier sign post
             (lt[0] - Vector((0.32, 0, 0)), Vector((hx + 2.0, PLATEAU_Y + 2.0, hz + 1.5))),                     # lamp 0 -> hut east eave
             (Vector((hx - 2.0, PLATEAU_Y + 2.0, hz + 1.5)), lt[1] + Vector((0.32, 0, 0))),                     # hut west eave -> lamp 1
             (lt[1] - Vector((0.32, 0, 0)), Vector((bx + bw / 2, PLATEAU_Y + 2.35, bz + bd / 2))),              # lamp 1 -> bait shop
             (Vector((bx - bw / 2, PLATEAU_Y + 2.35, bz + bd / 2)), lt[3] + Vector((0.32, 0, 0))),              # bait shop -> lamp 3
             (lt[3] - Vector((0.32, 0, 0)), Vector((nx + nw / 2 - 0.05, PLATEAU_Y + 2.9, nz + nd / 2 - 0.05))),  # lamp 3 -> net shed
             (lt[2] - Vector((0.32, 0, 0)), Vector((NOTICE[0] + 0.8, land_y(*NOTICE) + 2.0, NOTICE[1] + 0.05))),  # lamp 2 -> noticeboard roof
             (lt[2] + Vector((0.32, 0, 0)), Vector((ox - ow / 2, PLATEAU_Y + 2.3, oz + od / 2))),               # lamp 2 -> office
             (Vector((ox + ow / 2, PLATEAU_Y + 2.3, oz + od / 2)), lt[4] - Vector((0.32, 0, 0)))]              # office -> lamp 4
    bulbs = 0
    for a, b in hooks:
        L_ = (b - a).length
        sag = 0.12 + 0.03 * L_
        pts = rope_line(a, b, sag, n=max(8, int(L_ * 1.5)))
        g.add(tube(pts, 0.012, 4, caps=False), S['WoodDark'], L['none'], 0.7)
        n = max(2, int(L_ / 0.55))
        for k in range(1, n):
            t = k / n
            p = a.lerp(b, t) - Vector((0, sag * 4 * t * (1 - t), 0))
            add_lathe(g, p - Vector((0, 0.13, 0)), [(0, 0.0), (0.02, 0.05), (0.08, 0.055), (0.11, 0.03), (0.13, 0.018)], S['Accent'], L['none'], 1.0, sides=6, cap0=True)
            bulbs += 1
    g.extra['bulbs'] = bulbs
    return g

def build_clutter(yA, yB, r=None):
    g = G2('Harbour_Detail_Clutter', 'detail', rays=16)
    r = r or rng2('clutter')
    # -- the lunch on the bench (someone just went to check their line): sandwich, thermos, mug, an apple, a gull eyeing it (perch)
    bx, bz = BENCHES[0][1]
    by = land_y(bx, bz) + 0.485
    for k, (dy, slot) in enumerate(((0.0, S['Sand']), (0.025, S['Foliage']), (0.045, S['Sand']))):
        g.add(xform(rbox((0.2, 0.02, 0.18), 0.008, 1), (bx + 0.35, by + dy, bz - 0.02), ry(14)), slot, L['none'], 0.7)
    g.add(xform(rbox((0.22, 0.012, 0.22), 0.004, 1), (bx + 0.36, by - 0.006, bz - 0.03), ry(20)), S['Sand'], L['Cloth'], 0.7)   # paper
    add_lathe(g, (bx - 0.05, by, bz), [(0, 0.05), (0.24, 0.05), (0.26, 0.035), (0.31, 0.035), (0.32, 0.0)], S['Roof'], L['none'], 0.6, sides=10)
    add_lathe(g, (bx + 0.1, by, bz + 0.12), [(0, 0.04), (0.09, 0.045), (0.1, 0.0)], S['Sand'], L['none'], 0.6, sides=10)
    g.add(xform(blob(0.045, r, (1, 0.9, 1), subdiv=2, rough=0.03), (bx - 0.5, by + 0.04, bz + 0.05)), S['Roof'], L['none'], 0.6)
    # -- fish-cleaning table: a bucket of fish, two fish on the board, a knife, scraps on the ground
    cx, cz = CLEAN_TABLE
    cy = land_y(cx, cz)
    add_lathe(g, (cx + 0.45, cy + 0.92, cz + 0.1), [(0, 0.15), (0.3, 0.19), (0.31, 0.2), (0.32, 0.0)], S['Rock'], L['none'], 0.55, sides=12, cap0=True)
    add_lathe(g, (cx - 0.75, cy, cz - 0.55), [(0, 0.15), (0.3, 0.19), (0.31, 0.2), (0.32, 0.0)], S['Accent'], L['none'], 0.55, sides=12, cap0=True)
    for k in range(3):
        g.add(xform(blob(0.11, r, (1.8, 0.5, 0.7), subdiv=2, rough=0.04), (cx - 0.75 + r.uniform(-0.06, 0.06), cy + 0.33 + 0.03 * k, cz - 0.55), ry(r.uniform(0, 180)) @ rz(70)),
              S['Rock'], L['none'], 0.6)
    for k, (dx, yaw) in enumerate(((-0.3, 10.0), (0.05, -25.0))):
        g.add(xform(blob(0.13, r, (1.9, 0.45, 0.65), subdiv=2, rough=0.03), (cx + dx, cy + 0.95, cz - 0.05), ry(yaw)), S['Rock'] if k == 0 else S['Roof'], L['none'], 0.6)
        g.add(xform(blob(0.07, r, (0.5, 0.3, 1.0), subdiv=1, rough=0.02), (cx + dx + 0.24 * math.cos(math.radians(yaw)), cy + 0.95, cz - 0.05 - 0.24 * math.sin(math.radians(yaw)))),
              S['Rock'] if k == 0 else S['Roof'], L['none'], 0.6)
    g.add(xform(rbox((0.24, 0.012, 0.035), 0.004, 1), (cx + 0.3, cy + 0.93, cz + 0.2), ry(35)), S['Rock'], L['none'], 0.6)
    g.add(xform(rbox((0.12, 0.03, 0.035), 0.01, 1), (cx + 0.18, cy + 0.94, cz + 0.28), ry(35)), S['WoodDark'], L['none'], 0.6)
    # -- boatyard: paint tins, a brush across one, a drip trail, a step stool; the paint is the new Accent colour
    sx, sz, syaw = SAWHORSE_BOAT
    sy = land_y(sx, sz)
    for k, (dx, dz) in enumerate(((1.2, 0.9), (1.45, 0.55), (-1.3, -0.4))):
        add_lathe(g, (sx + dx, sy, sz + dz), [(0, 0.1), (0.2, 0.1), (0.21, 0.0)], S['Rock'], L['none'], 0.55, sides=10, cap1=True)
        add_lathe(g, (sx + dx, sy + 0.21, sz + dz), [(0, 0.09), (0.005, 0.0)], S['Accent'], L['none'], 0.7, sides=10, cap0=True)
    g.add(xform(rbox((0.04, 0.03, 0.32), 0.01, 1), (sx + 1.2, sy + 0.24, sz + 0.9), ry(30)), S['Wood'], L['Wood'], 0.6)
    g.add(xform(rbox((0.06, 0.04, 0.07), 0.01, 1), (sx + 1.2 + 0.08, sy + 0.24, sz + 0.9 + 0.14), ry(30)), S['Accent'], L['none'], 0.7)
    for k in range(5):
        g.add(xform(blob(0.05 + 0.01 * k, r, (1, 0.08, 1), subdiv=1, rough=0.1), (sx + 0.4 - 0.25 * k, sy + 0.01, sz + 0.3 + 0.1 * k)), S['Accent'], L['none'], 0.7)
    # -- net shed: oars and a boat hook leaning on the wall, buoys hung on nails, a rope coil, a broom
    nx, nz, nw, nd = NET_SHED
    for k, (dx, ang) in enumerate(((-1.6, 8.0), (-1.35, 5.0), (-1.1, -6.0))):
        geo, fs = lathe([(-1.3, 0.0), (-1.28, 0.025), (0.5, 0.025), (0.55, 0.03), (1.3, 0.0)], 6)
        g.add(xform(geo, (nx + dx, PLATEAU_Y + 1.25, nz + nd / 2 + 0.15), rx(-12) @ rz(ang)), S['Wood'], L['Wood'], 0.5)
        if k < 2:
            g.add(xform(rbox((0.16, 0.5, 0.025), 0.01, 1), (nx + dx + 0.1 * math.sin(math.radians(ang)), PLATEAU_Y + 0.3, nz + nd / 2 + 0.33), rx(-12) @ rz(ang)),
                  S['Wood'], L['Wood'], 0.5)
    for k in range(3):
        c = Vector((nx + nw / 2 + 0.2, PLATEAU_Y + 1.1 + 0.05 * k, nz - 0.9 + 0.55 * k))
        add_lathe(g, c, [(-0.17, 0.0), (-0.15, 0.1), (0.0, 0.15), (0.15, 0.1), (0.17, 0.0)], [S['Accent'], S['Roof'], S['Sand']][k], L['none'], 0.6, sides=10)
    rope_coil(g, 'Coil_NetShed', (nx - 0.9, PLATEAU_Y, nz + 0.2)); g.items.pop()
    rope_coil(g, 'Coil_Tug', (TUG_WORLD['position'][0] - 4.2, BREAKWATER_TOP, GANGWAY_Z - 1.4)); g.items.pop()
    # -- bait shop: a bucket of worms on the counter, a stack of bait tubs, a fishing rod leaning, a crate of empty bottles
    bx, bz, bw, bd = BAIT_SHOP
    fz = bz + bd / 2
    add_lathe(g, (bx + 0.4, PLATEAU_Y + 1.06, fz + 0.3), [(0, 0.11), (0.18, 0.13), (0.19, 0.0)], S['Accent'], L['none'], 0.55, sides=10, cap0=True)
    for k in range(4):
        add_lathe(g, (bx - 0.6 + 0.02 * k, PLATEAU_Y + 1.06 + 0.07 * k, fz + 0.3), [(0, 0.08), (0.07, 0.085), (0.075, 0.0)], S['Sand'], L['none'], 0.6, sides=10, cap0=True)
    g.add(xform(rbox((0.02, 2.6, 0.02), 0.008, 1), (bx + bw / 2 - 0.2, PLATEAU_Y + 1.25, fz + 0.14), rx(-10)), S['WoodDark'], L['none'], 0.6)
    g.add(xform(rbox((0.42, 0.26, 0.32), 0.02, 1), (bx + bw / 2 + 0.4, PLATEAU_Y + 0.13, fz - 0.4), ry(12)), S['Wood'], L['Wood'], 0.5)
    for k in range(4):
        add_lathe(g, (bx + bw / 2 + 0.3 + 0.12 * (k % 2), PLATEAU_Y + 0.1, fz - 0.47 + 0.12 * (k // 2)), [(0, 0.035), (0.18, 0.035), (0.24, 0.014), (0.28, 0.014), (0.285, 0.0)],
                  S['Foliage'], L['none'], 0.6, sides=6)
    # the swinging sign's bracket on the bait shop corner
    g.add(xform(rbox((0.05, 0.05, 1.0), 0.015, 1), (bx + bw / 2 + 0.05, PLATEAU_Y + 2.32, fz + 0.5)), S['Rock'], L['none'], 0.6)
    g.add(xform(rbox((0.04, 0.4, 0.04), 0.012, 1), (bx + bw / 2 + 0.05, PLATEAU_Y + 2.15, fz + 0.18), rx(-40)), S['Rock'], L['none'], 0.6)
    # -- harbour office: a life ring on the wall, a crate of mail, a bicycle-less bike rack? (no): a stack of tide charts in a box
    ox, oz, ow, od = DOCK_OFFICE
    lc = Vector((ox + ow / 2 + 0.07, PLATEAU_Y + 1.55, oz + 0.3))
    pts = [Vector((0.0, 0.24 * math.sin(a), 0.24 * math.cos(a))) for a in [2 * math.pi * i / 20 for i in range(20)]]
    V, N, F, UV = tube(pts, 0.06, 6, caps=False, closed=True)
    fsl = [((S['Sand'] if int(((math.atan2(sum(V[k].y for k in f), sum(V[k].z for k in f)) + math.pi) / (math.pi / 4))) % 2 == 0 else S['Accent']), L['Cloth']) for f in F]
    g.add(xform((V, N, F, UV), lc), 0, 0, 0.55, fsl)
    # -- smokehouse: fish hanging on a rack by the door, a sack of wood chips
    sx_, sz_, sw, sd = SMOKEHOUSE
    g.add(xform(rbox((1.2, 0.04, 0.04), 0.012, 1), (sx_ + 0.3, PLATEAU_Y + 2.2, sz_ + sd / 2 + 0.35)), S['WoodDark'], L['none'], 0.6)
    for k in range(5):
        g.add(xform(blob(0.12, r, (0.45, 1.8, 0.7), subdiv=2, rough=0.03), (sx_ - 0.2 + 0.25 * k, PLATEAU_Y + 1.95, sz_ + sd / 2 + 0.35)), S['Dock'], L['none'], 0.55)
    g.add(xform(blob(0.3, r, (1.0, 1.2, 0.85), subdiv=2, rough=0.12, flat=-0.7), (sx_ + 1.5, PLATEAU_Y + 0.3, sz_ + sd / 2 + 0.2)), S['Sand'], L['Cloth'], 0.55)
    # -- the tug's mooring lines to the breakwater (bow line to the tip, stern line and a spring) and their rings
    tx, ty, tz = TUG_WORLD['position']
    # rings on the breakwater's east edge, all > 3 m from the tip stand point (28.5, -26.5 since round 2b)
    rings = [(BREAKWATER[1] + 0.25, BREAKWATER_TOP - 0.15, -28.0), (BREAKWATER[1] + 0.25, BREAKWATER_TOP - 0.15, tz - 10.0),
             (BREAKWATER[1] + 0.25, BREAKWATER_TOP - 0.15, -31.0)]
    cleats = [(tx - 1.0, TUG_FORE + 0.4, tz + 5.0), (tx - 0.5, TUG_DECK + 0.6, tz - 3.7), (tx - tug_w(-1.0) + 0.2, TUG_DECK + 0.9, tz - 1.0)]
    for a, b in zip(rings, cleats):
        g.add(tube(rope_line(a, b, 0.35, n=12), 0.022, 5), S['Sand'], L['Cloth'], 0.55)
        ring = [Vector((0.1 * math.cos(t), 0.0, 0.1 * math.sin(t))) for t in [2 * math.pi * i / 10 for i in range(10)]]
        g.add(xform(tube(ring, 0.018, 5, caps=False, closed=True), a), S['Rock'], L['none'], 0.55)
    # -- by the pier root: a pair of boots left on the grass, a forgotten tackle box, a kid's bucket and spade
    for k, dx in enumerate((0.0, 0.2)):
        bx2, bz2 = -2.6 + dx, -60.0 + 0.05 * k
        by2 = land_y(bx2, bz2)
        g.add(xform(rbox((0.13, 0.32, 0.16), 0.05), (bx2, by2 + 0.16, bz2), ry(10 + 20 * k)), S['Reed'], L['none'], 0.55)
        g.add(xform(rbox((0.13, 0.1, 0.3), 0.04), (bx2 + 0.02, by2 + 0.05, bz2 + 0.08), ry(10 + 20 * k)), S['Reed'], L['none'], 0.55)
    tbx, tbz = 2.3, -57.6
    tby = land_y(tbx, tbz)
    g.add(xform(rbox((0.5, 0.24, 0.26), 0.04), (tbx, tby + 0.12, tbz), ry(-18)), S['Roof'], L['none'], 0.55)
    g.add(xform(rbox((0.2, 0.05, 0.05), 0.015, 1), (tbx, tby + 0.27, tbz), ry(-18)), S['Rock'], L['none'], 0.55)
    clutter_vignettes(g, r)
    return g

def clutter_vignettes(g, r):
    # -- a washing line behind the hut: two poles, a sagging line, shirts, a towel and a pair of socks
    a, b = Vector((-11.7, 0, -66.1)), Vector((-15.3, 0, -66.5))
    for p in (a, b):
        add_lathe(g, (p.x, land_y(p.x, p.z) - 0.1, p.z), [(0, 0.045), (1.9, 0.04), (1.92, 0.0)], S['Wood'], L['Wood'], 0.5, sides=6)
    pa, pb = Vector((a.x, land_y(a.x, a.z) + 1.75, a.z)), Vector((b.x, land_y(b.x, b.z) + 1.75, b.z))
    g.add(tube(rope_line(pa, pb, 0.18, n=10), 0.01, 3, caps=False), S['Sand'], L['Cloth'], 0.6)
    for k, (t, w, h, slot) in enumerate(((0.18, 0.5, 0.6, S['Roof']), (0.42, 0.55, 0.62, S['Reed']), (0.66, 0.7, 0.45, S['Sand']), (0.84, 0.14, 0.28, S['Accent']), (0.9, 0.14, 0.26, S['Accent']))):
        p = pa.lerp(pb, t) - Vector((0, 0.18 * 4 * t * (1 - t), 0))
        d = (pb - pa).normalized()
        yaw = math.degrees(math.atan2(d.x, d.z)) + 90
        g.add(xform(rbox((w, h, 0.02), 0.01, 1), p - Vector((0, h / 2, 0)), ry(yaw) @ rx(r.uniform(-6, 6))), slot, L['Cloth'], 0.65)
        if k < 2:   # shirt sleeves
            for sgn in (-1, 1):
                g.add(xform(rbox((0.18, 0.22, 0.02), 0.008, 1), p + ry(yaw) @ Vector((sgn * (w / 2 + 0.06), -0.15, 0)), ry(yaw) @ rz(sgn * 25)), slot, L['Cloth'], 0.65)
    # -- the crew's campfire from last night on the dunes: a stone ring, charred logs, a kettle, two log seats
    cx, cz = -4.2, -66.6
    cy = land_y(cx, cz)
    for k in range(9):
        ang = 2 * math.pi * k / 9
        x, z = cx + 0.55 * math.cos(ang), cz + 0.55 * math.sin(ang)
        g.add(xform(blob(0.13, r, (1.1, 0.7, 0.9), subdiv=1, rough=0.2), (x, land_y(x, z) + 0.03, z)), S['Rock'], L['Rock'], 0.6)
    for k in range(3):
        geo, fs = lathe([(-0.32, 0.0), (-0.3, 0.05), (0.3, 0.05), (0.32, 0.0)], 6)
        g.add(xform(geo, (cx, cy + 0.07, cz), ry(60 * k) @ rx(90) @ rz(8)), S['WoodDark'], L['Bark'], 0.45, tint=0.45)
    add_lathe(g, (cx + 0.25, cy + 0.12, cz + 0.1), [(0, 0.09), (0.12, 0.11), (0.18, 0.06), (0.22, 0.02), (0.24, 0.0)], S['Rock'], L['none'], 0.55, sides=8, cap0=True)
    for (dx, dz, yaw) in ((-1.3, 0.3, 80.0), (1.2, -0.6, 20.0)):
        geo, fs = lathe([(-0.5, 0.0), (-0.48, 0.17), (0.48, 0.17), (0.5, 0.0)], 10)
        x, z = cx + dx, cz + dz
        g.add(xform(geo, (x, land_y(x, z) + 0.15, z), ry(yaw) @ rx(90)), S['Wood'], L['Bark'], 0.5)
    g.extra.setdefault('smoke_points', []).append(dict(name='CampfireEmbers', position=[cx, round(cy + 0.15, 2), cz], note='a thin wisp only'))
    # -- a rod rack on the hut's east wall with three rods and a landing net
    for k in range(3):
        g.add(xform(rbox((0.018, 2.3, 0.018), 0.006, 1), (-6.85 + 0.0, PLATEAU_Y + 1.15, -62.0 + 0.18 * k), rz(-6 - 2 * k)), S['WoodDark'], L['none'], 0.6)
    g.add(xform(rbox((0.08, 0.06, 0.7), 0.02, 1), (-6.88, PLATEAU_Y + 1.6, -61.85)), S['Wood'], L['Wood'], 0.5)
    # -- a pile of old net and floats by the breakwater root
    nx_, nz_ = 34.4, -59.3
    g.add(xform(blob(0.6, r, (1.3, 0.45, 1.0), subdiv=2, rough=0.3, flat=-0.3), (nx_, land_y(nx_, nz_) + 0.12, nz_)), S['Sand'], L['Cloth'], 0.55)
    for k in range(4):
        add_lathe(g, (nx_ - 0.5 + 0.35 * k, land_y(nx_, nz_) + 0.22, nz_ + 0.5 - 0.2 * k), [(-0.12, 0.0), (-0.1, 0.08), (0.1, 0.08), (0.12, 0.0)],
                  [S['Accent'], S['Roof']][k % 2], L['none'], 0.6, sides=8, rot=rx(90) @ ry(30 * k))
    # -- round 1: the bait shop's east gable (bait_lane_player_eye) was a blank wall: a shuttered window, a lean-to over crates, a barrel and a
    #    fish rack, floats and a lantern on the wall, a drainpipe; all collider-free detail
    bx_, bz_, bw_, bd_ = BAIT_SHOP
    wx = bx_ + bw_ / 2 + 0.02
    side = ry(90)
    PS = lambda a, b, c: Vector((wx, PLATEAU_Y, bz_)) + side @ Vector((a, b, c))
    window(g, PS, side, 0.55, 1.65, 0.0, 0.7, 0.62, frame=S['Accent'], shutters=S['Foliage'])
    for zz in (-0.15, -1.55):
        add_lathe(g, (wx + 1.25, PLATEAU_Y - 0.05, bz_ + zz), [(0, 0.06), (1.95, 0.05), (1.97, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=6)
    g.add(xform(rbox((1.5, 0.07, 1.9), 0.02, 1), (wx + 0.68, PLATEAU_Y + 2.02, bz_ - 0.85), rz(14)), S['Rock'], L['Rock'], 0.55)
    g.add(xform(rbox((0.1, 0.1, 1.9), 0.02, 1), (wx + 1.25, PLATEAU_Y + 1.87, bz_ - 0.85)), S['WoodDark'], L['Wood'], 0.5)
    crate_stack(g, wx + 0.45, bz_ - 1.35, r, 'BaitLeanToCrates'); g.items.pop()
    for k in range(4):
        g.add(xform(blob(0.1, r, (0.45, 1.9, 0.7), subdiv=2, rough=0.03), (wx + 0.95, PLATEAU_Y + 1.45, bz_ - 0.3 - 0.22 * k)), S['Dock'] if k % 2 else S['Sand'], L['none'], 0.55)
    g.add(xform(rbox((0.04, 0.04, 1.0), 0.012, 1), (wx + 0.95, PLATEAU_Y + 1.72, bz_ - 0.62)), S['WoodDark'], L['none'], 0.5)
    for k, (zz, yy) in enumerate(((0.95, 2.05), (1.25, 1.85), (1.55, 2.1))):
        add_lathe(g, (wx + 0.12, PLATEAU_Y + yy, bz_ + zz), [(-0.13, 0.0), (-0.11, 0.09), (0.11, 0.09), (0.13, 0.0)], [S['Accent'], S['Roof'], S['Sand']][k], L['none'], 0.6, sides=8, rot=rx(90))
        g.add(tube([Vector((wx + 0.04, PLATEAU_Y + yy + 0.35, bz_ + zz)), Vector((wx + 0.12, PLATEAU_Y + yy + 0.13, bz_ + zz))], 0.008, 3), S['Sand'], L['Cloth'], 0.6)
    add_lathe(g, (wx + 0.08, PLATEAU_Y, bz_ + bd_ / 2 - 0.08), [(0, 0.05), (2.3, 0.05), (2.35, 0.08), (2.4, 0.0)], S['Rock'], L['none'], 0.55, sides=6)
    add_lathe(g, (wx + 0.3, PLATEAU_Y + 2.15, bz_ + 0.3), [(0, 0.0), (0.02, 0.09), (0.22, 0.1), (0.3, 0.05), (0.36, 0.0)], S['Accent'], L['none'], 0.7, sides=6)
    # -- a sandcastle with a bucket and spade on the beach (well clear of the stand points)
    sx, sz = -5.6, -50.6
    sy = beach_y(sz)
    add_lathe(g, (sx, sy - 0.02, sz), [(0, 0.34), (0.16, 0.3), (0.18, 0.2), (0.34, 0.17), (0.36, 0.0)], S['Sand'], L['Sand'], 0.6, sides=10)
    for k in range(4):
        ang = 2 * math.pi * k / 4 + 0.4
        add_lathe(g, (sx + 0.32 * math.cos(ang), sy + 0.1, sz + 0.32 * math.sin(ang)), [(0, 0.08), (0.18, 0.06), (0.26, 0.0)], S['Sand'], L['Sand'], 0.6, sides=6)
    g.add(xform(rbox((0.012, 0.18, 0.12), 0.004, 1), (sx, sy + 0.44, sz)), S['Roof'], L['Cloth'], 0.7)   # a little flag
    add_lathe(g, (sx + 0.7, sy, sz + 0.25), [(0, 0.08), (0.2, 0.11), (0.21, 0.0)], S['Accent'], L['none'], 0.6, sides=10, cap0=True)
    g.add(xform(rbox((0.1, 0.02, 0.4), 0.008, 1), (sx + 0.85, sy + 0.04, sz - 0.2), ry(40) @ rx(10)), S['Foliage'], L['none'], 0.6)

def blade_geo(base, direction, length, width, bend, segs=3):
    # one tapered, curved grass blade, two faces
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 1, 0)))
    if side.length < 1e-6:
        side = Vector((1, 0, 0))
    side.normalize()
    V, N, F, UV = [], [], [], []
    pts = []
    for k in range(segs + 1):
        t = k / segs
        p = Vector(base) + Vector((0, 1, 0)) * (length * t) + d * (bend * t * t * length)
        w = width * (1 - t) ** 0.8
        pts.append((p, w))
    nrm = d.cross(side).normalized()
    if nrm.y < 0:
        nrm = -nrm
    for p, w in pts:
        V += [p - side * w, p + side * w]
        N += [-d, -d]
    for k in range(segs):
        a = 2 * k
        F.append([a, a + 1, a + 3, a + 2]); UV.append([(0, k), (1, k), (1, k + 1), (0, k + 1)])
    nb = len(V)
    for i in range(nb):
        V.append(V[i] + d * 0.003); N.append(d.copy())
    for k in range(segs):
        a = nb + 2 * k
        F.append([a, a + 2, a + 3, a + 1]); UV.append([(0, k), (0, k + 1), (1, k + 1), (1, k)])
    return V, N, F, UV

def plant_ok(x, z, obstacles, margin=0.3, allow_beach=False):
    if inside_walls(x, z):
        if not allow_beach and z > -55.0:
            return False
        if pier_hit(x, z, 0.6):
            return False
        for pts, hw in PATHS:
            if poly_dist(x, z, pts) < hw + 0.15:
                return False
    for (lo, hi) in obstacles:
        if lo[0] - margin <= x <= hi[0] + margin and lo[2] - margin <= z <= hi[2] + margin:
            return False
    for tx, tz, _, _ in CODE_TREES:
        if math.hypot(x - tx, z - tz) < 0.6:
            return False
    return True

def obstacle_boxes(groups):
    out = []
    for g in groups:
        for it in g.items:
            if 'bounds_min' in it:
                out.append((it['bounds_min'], it['bounds_max']))
    hx, hz = HUT_C[0], HUT_C[2]
    out.append(([hx - 2.3, 0, hz - 1.8], [hx + 2.3, 5, hz + 1.8]))
    out.append(([-4.0, 0, -59.4], [-1.6, 4, -58.3]))     # pier sign
    return out

def build_plants(obst):
    gA = G2('Harbour_Detail_Plants_A', 'detail', rays=8, ao_dist=0.6)
    gB = G2('Harbour_Detail_Plants_B', 'detail', rays=8, ao_dist=0.6)
    r = rng2('plants')
    def tuft(g, x, z, n, h, slot):
        y = land_y(x, z) - 0.02
        for k in range(n):
            a = r.uniform(0, 2 * math.pi)
            off = Vector((math.cos(a), 0, math.sin(a))) * r.uniform(0.0, 0.12)
            g.add(blade_geo(Vector((x, y, z)) + off, (math.cos(a), 0.0, math.sin(a)), h * r.uniform(0.6, 1.1), 0.05, r.uniform(0.25, 0.6), segs=2),
                  slot, L['Grass'], 0.55)
    # dune grass tufts: on the dunes and along the plateau edge, in drifts
    placed = 0
    for _ in range(4000):
        if placed >= 90:
            break
        x, z = r.uniform(-59.5, 59.5), r.uniform(-75.0, -54.6)
        h = land_h(x, z)
        dens = 0.35 + 0.65 * smoothstep(0.1, 0.8, h) + (0.6 if z > -57.5 else 0.0)
        if r.random() > dens * (0.5 + 0.5 * n2(x, z, 0.12, 50)):
            continue
        if not plant_ok(x, z, obst, 0.35):
            continue
        g = gA if x < 0 else gB
        tuft(g, x, z, r.randint(6, 9), r.uniform(0.4, 0.75), S['Reed'] if r.random() < 0.55 else S['Grass'])
        placed += 1
    # wildflower patches
    flowers = 0
    for _ in range(800):
        if flowers >= 30:
            break
        x, z = r.uniform(-58.0, 58.0), r.uniform(-73.5, -56.0)
        if not plant_ok(x, z, obst, 0.5):
            continue
        g = gA if x < 0 else gB
        col = [S['Accent'], S['Sand'], S['Roof']][flowers % 3]
        for k in range(r.randint(5, 7)):
            fx, fz = x + r.uniform(-0.6, 0.6), z + r.uniform(-0.6, 0.6)
            fy = land_y(fx, fz)
            hgt = r.uniform(0.18, 0.38)
            g.add(blade_geo((fx, fy - 0.02, fz), (r.uniform(-1, 1), 0, r.uniform(-1, 1)), hgt, 0.012, 0.15, segs=2), S['Grass'], L['Grass'], 0.6)
            add_lathe(g, (fx, fy + hgt - 0.02, fz), [(0, 0.0), (0.01, 0.045), (0.03, 0.05), (0.045, 0.0)], col, L['none'], 0.75, sides=5)
        flowers += 1
    # bushes against buildings and along the stone wall
    bushes = 0
    for _ in range(900):
        if bushes >= 22:
            break
        if r.random() < 0.5:
            x, z = r.uniform(-58.0, 58.0), STONE_WALL_Z + r.uniform(0.7, 1.6)
        else:
            x, z = r.uniform(-45.0, 25.0), r.uniform(-70.0, -60.5)
        if not plant_ok(x, z, obst, 0.45):
            continue
        g = gA if x < 0 else gB
        shrub(g, x, z, r, r.uniform(0.35, 0.55), flowers=[S['Accent'], None, S['Roof'], None, S2['Snow']][bushes % 5])   # round 2: low shrubs
        bushes += 1
    edge = edge_dressing(gA, gB, obst, r)
    gA.extra['counts'] = dict(tufts=placed, flower_patches=flowers, bushes=bushes, **edge)
    return [gA, gB]

def edge_dressing(gA, gB, obst, r):
    # Grass tufts along every path edge, both sides of the stone wall and the building bases; small stones and sticks scattered
    # along the paths and in the grass, so no large flat green area is left bare (RV There Yet density).
    def tuft(g, x, z, n, h, slot):
        y = land_y(x, z) - 0.02
        for k in range(n):
            a = r.uniform(0, 2 * math.pi)
            off = Vector((math.cos(a), 0, math.sin(a))) * r.uniform(0.0, 0.1)
            g.add(blade_geo(Vector((x, y, z)) + off, (math.cos(a), 0.0, math.sin(a)), h * r.uniform(0.6, 1.1), 0.045, r.uniform(0.25, 0.6), segs=2),
                  slot, L['Grass'], 0.55)
    nt = ns = nk = 0
    for pts, hw in PATHS:
        for i in range(len(pts) - 1):
            (ax, az), (bx, bz) = pts[i], pts[i + 1]
            L_ = math.hypot(bx - ax, bz - az)
            tx, tz = (bx - ax) / L_, (bz - az) / L_
            n = int(L_ / 0.85)
            for k in range(n):
                t = (k + r.uniform(0.1, 0.9)) / max(n, 1)
                for side in (-1, 1):
                    if r.random() < 0.3:
                        continue
                    off = hw + 0.55 + r.uniform(0.0, 0.7)
                    x, z = ax + (bx - ax) * t - tz * off * side, az + (bz - az) * t + tx * off * side
                    if not inside_walls(x, z) or z > -55.5 or not plant_ok(x, z, obst, 0.25):
                        continue
                    tuft(gA if x < 0 else gB, x, z, r.randint(3, 5), r.uniform(0.3, 0.55), S['Reed'] if r.random() < 0.5 else S['Grass'])
                    nt += 1
                # small stones and sticks along the worn edge
                if r.random() < 0.35:
                    side = 1 if r.random() < 0.5 else -1
                    off = hw + r.uniform(0.1, 0.6)
                    x, z = ax + (bx - ax) * t - tz * off * side, az + (bz - az) * t + tx * off * side
                    if inside_walls(x, z) and z <= -55.5 and plant_ok(x, z, obst, 0.2):
                        g = gA if x < 0 else gB
                        if r.random() < 0.65:
                            g.add(xform(blob(r.uniform(0.06, 0.16), r, (1.2, 0.6, 1.0), subdiv=1, rough=0.25), (x, land_y(x, z) + 0.01, z)), S['Rock'], L['Rock'], 0.6)
                            ns += 1
                        else:
                            a = r.uniform(0, math.pi)
                            ln = r.uniform(0.3, 0.7)
                            p0 = Vector((x - math.cos(a) * ln / 2, land_y(x, z) + 0.025, z - math.sin(a) * ln / 2))
                            p1 = Vector((x + math.cos(a) * ln / 2, land_y(x, z) + 0.025, z + math.sin(a) * ln / 2))
                            g.add(tube([p0, p0.lerp(p1, 0.5) + Vector((0, 0.01, 0)), p1], 0.018, 4), S['WoodDark'], L['Bark'], 0.55)
                            nk += 1
    # round 1: the foot of both retaining walls and the lawn just behind their parapets
    for zw in (T1_Z, T2_Z):
      x = -58.0
      while x < 58.0:
        if not in_ramp(x, zw, 0.6):
            for side in (-1, 1):
                if r.random() < 0.55:
                    z = (zw + RISER_HALF + r.uniform(0.35, 0.75)) if side > 0 else (zw - r.uniform(0.55, 0.95))
                    if plant_ok(x, z, obst, 0.2):
                        tuft(gA if x < 0 else gB, x, z, r.randint(4, 7), r.uniform(0.35, 0.65), S['Reed'] if r.random() < 0.6 else S['Grass'])
                        nt += 1
        x += r.uniform(1.0, 2.2)
    # building bases (front and sides) and the hut
    for (cx, cz, w, d) in (BAIT_SHOP, NET_SHED, SMOKEHOUSE, DOCK_OFFICE, (HUT_C[0], HUT_C[2], 4.0, 3.0)):
        per = [(cx + u * w / 2, cz - d / 2 - 0.3) for u in frange(-1, 1, 0.25)] + [(cx - w / 2 - 0.3, cz + v * d / 2) for v in frange(-1, 1, 0.34)] +               [(cx + w / 2 + 0.3, cz + v * d / 2) for v in frange(-1, 1, 0.34)]
        for (x, z) in per:
            if r.random() < 0.6 and plant_ok(x, z, obst, 0.0):
                tuft(gA if x < 0 else gB, x + r.uniform(-0.1, 0.1), z, r.randint(4, 6), r.uniform(0.3, 0.55), S['Grass'] if r.random() < 0.6 else S['Reed'])
                nt += 1
    # scattered pebbles and sticks in the open grass
    for _ in range(160):
        x, z = r.uniform(-58.0, 58.0), r.uniform(-73.0, -55.8)
        if not plant_ok(x, z, obst, 0.3):
            continue
        g = gA if x < 0 else gB
        g.add(xform(blob(r.uniform(0.05, 0.14), r, (1.2, 0.55, 1.0), subdiv=1, rough=0.25), (x, land_y(x, z) + 0.005, z)), S['Rock'], L['Rock'], 0.6)
        ns += 1
    return dict(edge_tufts=nt, stones=ns, sticks=nk)

TREES2 = [(-4.5, -72.0, 'pine'), (12.5, -71.0, 'pine'), (24.0, -72.5, 'round'), (-12.0, -73.2, 'pine'), (-35.5, -73.0, 'round'),
          (33.0, -69.5, 'pine'), (-57.0, -72.5, 'pine'), (57.5, -60.8, 'pine'), (8.0, -68.6, 'round'), (-44.3, -70.8, 'round'),
          # round 1: shade trees on the terraces - four along the upper lane (between the lane and wall 2), two by the chapel overlook
          # (round 2b: the one at x -3 moved to -8.6, out of the overlook's view of the harbour)
          # round 3: the lane bends and the cottages moved - the square's big shade tree, lane trees clear of the south row
          (-44.0, -92.6, 'round'), (-9.0, -91.6, 'big'), (6.0, -93.8, 'round'), (29.6, -92.6, 'round'), (-7.0, -100.8, 'round'), (16.0, -101.0, 'round'),
          (51.5, -92.8, 'round'), (-45.4, -56.3, 'round')]

def build_trees():
    # A few more trees inside the walls (wind-bent pines and a round one), Core with a trunk box collider each.
    # Round 1: the terrace shade trees (TREES2[10:]) go in their own model, Harbour_Core_TerraceTrees_Tri (each FBX stays under 21k).
    g1 = G2('Harbour_Core_Trees_Tri', 'core', tri=True, rays=10)
    g2 = G2('Harbour_Core_TerraceTrees_Tri', 'core', tri=True, rays=10)
    gc = cards_group('Harbour_Core_TreeCrowns_Cards',
                     note='Round 2b: the crown cards of the village and terrace trees (Trees_Tri / TerraceTrees_Tri keep the trunks, the inner '
                          'clumps and the trunk colliders). M_Toon_Foliage, no colliders.')
    r = rng2('trees2')
    for i, (x, z, kind) in enumerate(TREES2):
        if not tree_clear(x, z, 3.0):
            raise RuntimeError('tree %d too close to a code tree' % i)
        y = land_y(x, z)
        g = g1 if i < 10 else g2
        g.mark()
        if kind == 'pine':
            tree_pine(g, (x, y, z), r.uniform(5.0, 6.5), S['FoliageDark'], sides=10, lean=(r.uniform(-4, 4), 9.0), gc=gc)
        elif kind == 'big':
            tree_round(g, (x, y, z), 6.6, r, S['Foliage'], sub=3, gc=gc)      # round 3: the village square's shade tree
        else:
            tree_round(g, (x, y, z), 4.2, r, S['Foliage'], sub=3, gc=gc)
        it = g.solid('Tree2_%d' % i)
        # the collider is the trunk only (a box 0.6 x 2.5 x 0.6 at the base); the canopy stays walk-under
        it['visual_bounds_min'], it['visual_bounds_max'] = it['bounds_min'], it['bounds_max']
        it['bounds_min'] = [round(x - 0.3, 3), round(y, 3), round(z - 0.3, 3)]
        it['bounds_max'] = [round(x + 0.3, 3), round(y + 2.5, 3), round(z + 0.3, 3)]
        it['position'] = [round(x, 3), round(y + 1.25, 3), round(z, 3)]
        it['footprint'] = [0.6, 2.5, 0.6]
        it['bounds_note'] = 'bounds_min/max = the trunk collider; the canopy (visual_bounds) stays walk-under'
    return [g1, g2, gc]

# ---------------------------------------------------------------------------------------------------------------------------------------
# Round 0 (2026-10-14): the 16 code trees (HarbourPierBuilder.PlaceTrees) get painterly visuals. Four LOCAL models (two pines, two leafy
# trees) authored at the origin for a tree of height scale 1; Unity parents one under each Tree_NN (whose localScale is the Pcg32 height), so
# the trunk and canopy colliders stay exactly where they were. Local models bake AO against themselves only, so no other FBX changes.
# ---------------------------------------------------------------------------------------------------------------------------------------
CODE_TREE_PINE_H, CODE_TREE_ROUND_H = 5.4, 4.3

def build_code_trees():
    variants = [
        G2('Harbour_Core_CodeTree_PineA_Tri', 'core', tri=True, local=True, rays=10, ao_dist=1.2,
           note='Code-tree pine (straight). Origin at the trunk foot, height scale 1: parent under Tree_NN (localScale = the Pcg32 height).'),
        G2('Harbour_Core_CodeTree_PineB_Tri', 'core', tri=True, local=True, rays=10, ao_dist=1.2,
           note='Code-tree pine (wind-bent toward +x). Origin at the trunk foot, height scale 1: parent under Tree_NN.'),
        G2('Harbour_Core_CodeTree_RoundA_Tri', 'core', tri=True, local=True, rays=10, ao_dist=1.2,
           note='Code-tree leafy tree. Origin at the trunk foot, height scale 1: parent under Tree_NN (leafy seasons; winter shows the bare branches).'),
        G2('Harbour_Core_CodeTree_RoundB_Tri', 'core', tri=True, local=True, rays=10, ao_dist=1.2,
           note='Code-tree leafy tree (variant B). Origin at the trunk foot, height scale 1: parent under Tree_NN.'),
    ]
    # round 2b: each variant's crown cards are a local twin model (<variant>_Cards, M_Toon_Foliage) with the same placements
    cards = [cards_group(v.name.replace('_Tri', CARD_SUFFIX), local=True,
                         note='Round 2b: the crown cards of ' + v.name + ' (same origin and placements; parent under the same Tree_NN).')
             for v in variants]
    r = rng2('codetrees')
    tree_pine(variants[0], (0.0, 0.0, 0.0), CODE_TREE_PINE_H, S['FoliageDark'], sides=10, lean=(0.0, 0.0), key='codetree.pineA', gc=cards[0])
    tree_pine(variants[1], (0.0, 0.0, 0.0), CODE_TREE_PINE_H * 0.95, S['Foliage'], sides=10, lean=(3.0, 10.0), key='codetree.pineB', gc=cards[1])
    tree_round(variants[2], (0.0, 0.0, 0.0), CODE_TREE_ROUND_H, r, S['Foliage'], sub=2, key='codetree.roundA', gc=cards[2])
    tree_round(variants[3], (0.0, 0.0, 0.0), CODE_TREE_ROUND_H * 1.05, r, S['FoliageDark'], sub=2, key='codetree.roundB', gc=cards[3])
    pines = rounds = 0
    for i, (x, z, h, pine) in enumerate(CODE_TREES):
        if pine:
            g = variants[pines % 2]; pines += 1
        else:
            g = variants[2 + rounds % 2]; rounds += 1
        yaw = (i * 137.5) % 360.0
        for gg in (g, cards[variants.index(g)]):
            gg.place('Tree_%02d' % i, (x, PLATEAU_Y, z), yaw, frame='Tree_%02d local (the code tree root; its localScale is the height)' % i,
                     height_scale=h, pine=pine)
    return variants + cards

def build_shore(obst):
    g = G2('Harbour_Detail_Shore', 'detail', rays=10)
    t = G2('Harbour_Detail_TidePools_Tri', 'detail', tri=True, rays=10)
    r = rng2('shore')
    shore_items = []
    # seaweed clumps and driftwood along the waterline (z -44.2..-45.8), outside every lane and stand circle (margin 1 m)
    sw = 0
    for _ in range(600):
        if sw >= 26:
            break
        x, z = r.uniform(-59.0, 59.0), r.uniform(-45.8, -44.2)
        if lane_hit(x, z, 1.0) or pier_hit(x, z, 1.0) or abs(x - 30.0) < 4.0:
            continue
        y = max(beach_y(z), -0.02)
        for k in range(r.randint(2, 4)):
            a = r.uniform(0, 2 * math.pi)
            p = Vector((x + r.uniform(-0.4, 0.4), y + 0.01, z + r.uniform(-0.3, 0.3)))
            d = Vector((math.cos(a), 0.0, math.sin(a)))
            pts = [p + d * (0.18 * j) + Vector((0, 0.012 * math.sin(j * 1.7), 0)) + d.cross(Vector((0, 1, 0))) * 0.05 * math.sin(j * 1.3 + k) for j in range(5)]
            g.add(tube(pts, 0.03, 4), S['FoliageDark'] if k % 2 else S['Reed'], L['none'], 0.6)
        shore_items.append(('Seaweed', x, z))
        sw += 1
    dw = 0
    for _ in range(400):
        if dw >= 9:
            break
        x, z = r.uniform(-58.0, 58.0), r.uniform(-47.5, -45.2)
        if lane_hit(x, z, 1.0) or pier_hit(x, z, 1.5) or not plant_ok(x, z, obst, 0.5, allow_beach=True):
            continue
        y = beach_y(z)
        yaw = r.uniform(0, 180)
        Lg = r.uniform(1.2, 2.6)
        prof = [(-Lg / 2, 0.0), (-Lg / 2 + 0.02, 0.07), (-Lg / 4, 0.09), (Lg / 4, 0.08), (Lg / 2 - 0.05, 0.06), (Lg / 2, 0.0)]
        geo, fs = lathe(prof, 7)
        g.add(xform(geo, (x, y + 0.04, z), ry(yaw) @ rx(90) @ rz(r.uniform(-4, 4))), S['Sand'], L['Bark'], 0.5)
        if r.random() < 0.6:   # a stub branch
            g.add(xform(lathe([(0, 0.035), (0.35, 0.02), (0.36, 0.0)], 5)[0], (x, y + 0.08, z), ry(yaw + 40) @ rz(60)), S['Sand'], L['Bark'], 0.5)
        shore_items.append(('Driftwood', x, z))
        dw += 1
    # shells and two crabs on the beach (well clear of the stand points)
    for k in range(40):
        for _ in range(50):
            x, z = r.uniform(-58.0, 58.0), r.uniform(-54.0, -45.3)
            if not lane_hit(x, z, 0.5) and not pier_hit(x, z, 0.8) and plant_ok(x, z, obst, 0.3, allow_beach=True):
                break
        y = beach_y(z)
        if k % 3 == 0:
            add_lathe(g, (x, y - 0.01, z), [(0, 0.06), (0.025, 0.05), (0.04, 0.0)], S['Sand'], L['none'], 0.8, sides=6, rot=ry(r.uniform(0, 360)))
        else:
            g.add(xform(blob(0.045, r, (1.0, 0.35, 0.8), subdiv=1, rough=0.1), (x, y + 0.005, z), ry(r.uniform(0, 360))),
                  [S['Sand'], S['Accent'], S['Roof']][k % 3], L['none'], 0.8)
    crabs = [(-38.5, -48.6, 30.0), (55.6, -46.2, -40.0)]
    for (x, z, yaw) in crabs:
        y = beach_y(z)
        rot = ry(yaw)
        c = Vector((x, y + 0.06, z))
        g.add(xform(blob(0.09, r, (1.3, 0.55, 1.0), subdiv=2, rough=0.04), c, rot), S['Roof'], L['none'], 0.6)
        for s in (-1, 1):
            g.add(xform(blob(0.045, r, (1.2, 0.7, 0.8), subdiv=1, rough=0.02), c + rot @ Vector((s * 0.12, 0.02, 0.12)), rot), S['Roof'], L['none'], 0.6)
            g.add(xform(blob(0.018, r, (1, 1, 1), subdiv=1, rough=0.0), c + rot @ Vector((s * 0.035, 0.07, 0.07)), rot), S['WoodDark'], L['none'], 0.6, tint=0.4)
            for k in range(3):
                g.add(xform(tube([Vector((s * 0.08, 0, -0.04 + 0.05 * k)), Vector((s * 0.16, 0.03, -0.06 + 0.05 * k)), Vector((s * 0.2, -0.05, -0.07 + 0.05 * k))], 0.01, 3),
                            c, rot), S['Roof'], L['none'], 0.6)
    g.extra['crabs'] = [dict(name='Crab_%d' % i, position=[x, round(beach_y(z) + 0.06, 3), z], animHint='scuttle (optional)') for i, (x, z, _) in enumerate(crabs)]
    # tide-pool rocks: a low cluster by the east wall (inside, kept low: < 0.35 m, no collider) and a bigger one outside it
    tp = 0
    for _ in range(300):
        if tp >= 16:
            break
        x, z = r.uniform(51.5, 60.0), r.uniform(-49.5, -43.8)
        if lane_hit(x, z, 0.6) or any(math.hypot(x - bx, z - bz) < br + 0.5 for bx, bz, br in BEACH_ROCKS):
            continue
        rad = r.uniform(0.25, 0.6)
        t.add(xform(blob(rad, r, (1.3, 0.45, 1.0), subdiv=2, rough=0.22, flat=-0.4, yaw=r.uniform(0, 360)), (x, terrain_y(z) + 0.05 * rad, z)), S['Rock'], L['Rock'], AO_ROCK)
        tp += 1
    for _ in range(200):
        if tp >= 34:
            break
        x, z = r.uniform(61.5, 66.0), r.uniform(-50.0, -38.0)
        rad = r.uniform(0.5, 1.3)
        t.add(xform(blob(rad, r, (1.2, 0.6, 1.0), subdiv=2, rough=0.24, flat=-0.4, yaw=r.uniform(0, 360)), (x, terrain_y(z) + 0.1 * rad, z)), S['Rock'], L['Rock'], AO_ROCK)
        tp += 1
    g.extra['shore_items'] = [dict(kind=k, x=round(x, 2), z=round(z, 2)) for k, x, z in shore_items]
    return [g, t]

def build_reeds():
    g = G2('Harbour_Detail_Reeds', 'detail', rays=0,
           note='Reeds and cattails along the river banks (outside the walls) and a patch by the west wall.')
    r = rng2('reeds')
    n = 0
    for _ in range(3000):
        if n >= 60:
            break
        i = r.randrange(len(RIVER) - 1)
        (ax, az), (bx, bz) = RIVER[i], RIVER[i + 1]
        t = r.random()
        px, pz = ax + (bx - ax) * t, az + (bz - az) * t
        L_ = math.hypot(bx - ax, bz - az)
        nx, nz = -(bz - az) / L_, (bx - ax) / L_
        side = 1 if r.random() < 0.5 else -1
        off = river_half_width(pz) + r.uniform(-0.6, 1.4)
        x, z = px + nx * off * side, pz + nz * off * side
        if z > -46.0 or z < -150.0:
            continue
        y = max(land_y(x, z), PLATEAU_Y + 0.0)
        for k in range(r.randint(4, 7)):
            a = r.uniform(0, 2 * math.pi)
            base = Vector((x + 0.15 * math.cos(a), y - 0.05, z + 0.15 * math.sin(a)))
            hgt = r.uniform(0.9, 1.7)
            g.add(blade_geo(base, (math.cos(a), 0, math.sin(a)), hgt, 0.03, 0.15, segs=2), S['Reed'], L['Grass'], 0.6)
            if k == 0 and r.random() < 0.5:
                top = base + Vector((0, hgt * 0.92, 0))
                add_lathe(g, top - Vector((0, 0.18, 0)), [(0, 0.0), (0.02, 0.035), (0.16, 0.035), (0.18, 0.0)], S['WoodDark'], L['none'], 0.6, sides=5)
        n += 1
    build_creek_dressing(g, r)      # round 1
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# ROUND 1 (2026-10-15): map layout and player environment. Zones and levels:
#   level 0   the harbour village (round 0) - pier, bait shop, net shed, smokehouse, harbour office;
#   east      the inner basin: a fishing jetty off the east beach (stand point 11) with moored boats, and the lookout tower by the
#             breakwater root (stand point 12, the highest fishing spot, 4.4 m) next to the tug's berth and its departure board;
#   west      the creek mouth: a stream out of a culvert in retaining wall 1 runs across the plateau and the beach into the bay, with reeds;
#   level 1   the upper lane (the coast road) with a row of cottages, gardens and lamp posts, reached by three stone stairs;
#   level 2   the chapel and its bell tower (the town landmark) and the overlook, reached by two more stairs;
#   beyond    farm terraces, the windmill (sails turn), the farm, a forest belt, a black sedan watching from the hill track.
# Loop: pier -> breakwater -> lookout -> jetty -> stairs D -> upper lane -> stairs E/C -> chapel overlook -> stairs B -> creek -> beach.
# ---------------------------------------------------------------------------------------------------------------------------------------
LEVEL1_Y, LEVEL2_Y = DUNE_BASE + T1_H, DUNE_BASE + T2_H
# cottages on level 1: x, z (centre), width (x), depth (z), eave, ridge, wall slot, wall layer, roof slot, trim slot, yaw
# Round 3: ten cottages in two clusters, 2-4 m apart, every one FACING THE LANE (north row yaw ~180, south row yaw ~0) with a fenced front
# garden on the lane side; the lane's bends put a house across the line of sight at each turn, so the far end is found by walking.
UPPER_HOUSES = [(-54.0, -82.0, 5.4, 4.4, 2.6, 4.1, S['Sand'], L['Sand'], S['Roof'], S['Accent'], 182.0),
                (-40.5, -81.2, 4.6, 4.2, 2.4, 3.8, S['Reed'], L['Wood'], S['Dock'], S['Sand'], 177.0),
                (-17.5, -80.4, 5.0, 4.4, 2.5, 4.0, S['Wood'], L['Wood'], S['Roof'], S['Sand'], 183.0),
                (-32.0, -91.6, 4.6, 4.2, 2.5, 3.9, S['Sand'], L['Sand'], S['Foliage'], S['WoodDark'], -2.0),
                (-24.6, -91.4, 5.0, 4.4, 2.7, 4.3, S['Accent'], L['Wood'], S['Rock'], S['Sand'], 3.0),
                (10.4, -91.8, 4.8, 4.2, 2.5, 3.9, S['Rock'], L['Rock'], S['Roof'], S['Sand'], 2.0),
                (17.6, -91.4, 5.0, 4.4, 2.6, 4.1, S['Sand'], L['Sand'], S['Dock'], S['Foliage'], -3.0),
                (23.8, -80.4, 5.0, 4.4, 2.6, 4.1, S['Reed'], L['Wood'], S['Roof'], S['Accent'], 178.0),
                (39.0, -81.4, 6.0, 4.6, 2.8, 4.4, S['Wood'], L['Wood'], S['Foliage'], S['Sand'], 181.5),
                (46.6, -81.8, 5.2, 4.4, 2.6, 4.1, S['Sand'], L['Sand'], S['Roof'], S['WoodDark'], 176.0)]
VILLAGE_SQUARE = (-3.0, -88.6, 6.0)   # centre x, z, radius: cobbles under the chapel's terrace wall, between stairs A and C
CHAPEL = (4.0, -98.4)                 # bell tower centre (level 2); the nave runs back (-z) from it
SQUARE_TREE = (-9.0, -91.6)           # round 3: the square's shade tree (TREES2 'big'; west of the overlook's view)
HARBOUR_BOARD = (-0.4, -94.0, 0.0)    # round 3: x, z, yaw (backs onto wall 2, faces the square)
LANE_LAMPS = [(-46.6, -85.6), (-35.0, -89.8), (-9.4, -84.6), (3.6, -93.4), (13.4, -87.8), (31.4, -86.0), (52.0, -90.4)]   # round 3: x, z
JETTY = (54.4, 57.0, -51.0, -34.0, 0.95)          # x0, x1, z0, z1, deck top
LOOKOUT = (34.7, 39.5, -51.2, -47.6, 4.4)         # x0, x1, z0, z1, deck top
LOOKOUT_STAIR = (38.3, 39.5, -56.0)               # x0, x1, foot z (plateau 1.2); it climbs north to the deck's south edge
LOOKOUT_ROOF = 6.8
WINDMILL = (-24.0, -138.0)
FARM = [(-58.0, -121.0, 8.0, 'house'), (-45.0, -127.0, -4.0, 'barn'), (46.0, -128.0, -12.0, 'house')]
HILL_TRACK = [(26.0, -106.5), (27.5, -112.0), (18.0, -118.0), (8.0, -121.5), (12.0, -126.0), (26.0, -131.0), (34.0, -137.0)]
# round 2b: the farm track branches west off the hill track's first bend, past the windmill to the barn and the farmhouse (critics: "the farms
# float; nothing ties them to the town")
FARM_TRACK = [(8.0, -121.5), (-2.0, -126.5), (-12.0, -131.0), (-19.5, -134.0), (-30.0, -133.5), (-40.0, -129.5), (-51.0, -124.5), (-55.5, -123.0)]
HILL_SEDAN = (9.5, -122.6, 150.0)
CREEK = [(-57.5, -75.2), (-58.2, -70.0), (-57.0, -65.0), (-58.0, -60.0), (-57.0, -55.0), (-56.4, -50.0), (-55.2, -47.2), (-54.2, -43.8)]   # round 3: into the lagoon
CREEK_HW = 0.85

PATHS += [
    ([(-36.4, -64.8), (-38.5, -69.0), (-42.0, -72.8), (-45.5, -74.6), (-47.0, -75.3)], 0.85),              # village -> stairs B
    ([(25.5, -61.2), (27.5, -65.0), (30.2, -70.0), (32.6, -74.4), (33.0, -75.3)], 0.85),                  # boatyard -> stairs D
    ([(4.4, -81.9), (3.6, -83.4), (1.2, -84.6)], 0.85), ([(-47.0, -81.9), (-47.0, -86.2)], 0.85), ([(33.0, -81.9), (33.0, -86.6)], 0.85),
    ([(-7.4, -93.0), (-10.4, -94.0), (-12.0, -94.9)], 0.8), ([(26.0, -88.8), (26.0, -94.9)], 0.8),       # lane -> stairs C / E (round 3: the bent lane)
    ([(-12.0, -101.6), (-9.6, -97.0), (-2.0, -96.2), (12.0, -96.3), (22.0, -96.8), (26.0, -101.6)], 0.8),  # the overlook walk on level 2
    ([(26.0, -101.8), (26.0, -106.4)], 0.8),                                                               # -> the hill track gap
]
PATHS[2] = ([(0.0, -60.4), (3.0, -61.0), (3.4, -64.5), (3.2, -68.0), (4.0, -71.5), (4.4, -75.3)], 0.85)     # south path -> stairs A
# keep-outs that flatten the dunes: the creek
YARDS += [(-57.5, -66.0, 1.6, 9.5)]

# round-1 fishing spots (HarbourPierLayout.StandPoints 11 and 12): the jetty head and the lookout deck
# (a separate list: phase 1's rock draw reads STAND_POINTS and must not change)
STAND_POINTS_R1 = [(55.7, JETTY[4] + 0.05, -35.0, 352), (36.4, LOOKOUT[4] + 0.05, -48.3, 36)]   # round 2b: the jetty faces 352 (was 340): the moved tug   # facings keep the tug 1.0-1.6 m out of both lanes

def pbox(g, lo, hi, slot, layer, ao=0.5, rot=None, r=0.03):
    c = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2))
    g.add(xform(rbox((hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]), r, 1), c, rot), slot, layer, ao)

def box_item(g, name, lo, hi, **extra):
    d = dict(name=name, collider='box', position=[round((lo[i] + hi[i]) / 2, 3) for i in range(3)],
             footprint=[round(hi[i] - lo[i], 3) for i in range(3)], bounds_min=[round(v, 3) for v in lo], bounds_max=[round(v, 3) for v in hi])
    d.update(extra)
    g.items.append(d)
    return d

# ---- cottages ----
def window2(g, P, rot, x, y, z, w, h, frame, shutters=None):
    # cheaper window: a frame slab, a dark glass pane, a sill; optional shutters
    g.add(xform(rbox((w + 0.16, h + 0.16, 0.05), 0.02, 1), P(x, y, z + 0.02), rot), frame, L['Wood'], 0.5)
    g.add(xform(rbox((w, h, 0.06), 0.015, 1), P(x, y, z + 0.035), rot), S['Rock'], L['none'], 0.5, tint=0.5)
    g.add(xform(rbox((w + 0.28, 0.07, 0.16), 0.02, 1), P(x, y - h / 2 - 0.1, z + 0.08), rot), frame, L['Wood'], 0.5)
    if shutters is not None:
        for s_ in (-1, 1):
            g.add(xform(rbox((w * 0.48, h + 0.08, 0.04), 0.015, 1), P(x + s_ * (w * 0.74 + 0.1), y, z + 0.04), rot), shutters, L['Wood'], 0.5)

def cottage(g, c, W, D, eave, ridge, wall, wall_layer, roof, trim, yaw, r, name, chimney=True):
    rot = ry(yaw)
    cx, cy, cz = c
    P = lambda x, y, z: Vector((cx, cy, cz)) + rot @ Vector((x, y, z))
    g.mark()
    g.add(xform(rbox((W + 0.24, 0.42, D + 0.24), 0.05, 1), P(0, 0.1, 0), rot), S['Rock'], L['Rock'], 0.55)          # stone plinth
    poly = [(-D / 2, 0.0), (D / 2, 0.0), (D / 2, eave), (0.0, ridge), (-D / 2, eave)]
    g.add(xform(prism(poly, W, 0.03, segs=1), P(0, 0.2, 0), rot), wall, wall_layer, AO_WOOD)
    for sx in (-1, 1):
        for sz in (-1, 1):
            g.add(xform(rbox((0.15, eave, 0.15), 0.03, 1), P(sx * W / 2, 0.2 + eave / 2, sz * D / 2), rot), trim, L['Wood'], 0.45)
    over = 0.35
    run = D / 2 + over
    ang = math.degrees(math.atan2(ridge - eave, D / 2))
    slab = run / math.cos(math.radians(ang)) + 0.05
    for sz in (-1, 1):
        mid = P(0, 0.2 + ridge - (ridge - eave) * (run / 2) / (D / 2) + 0.08, sz * run / 2)
        g.add(xform(rbox((W + 0.55, 0.13, slab), 0.05, 1), mid, rot @ rx(ang * sz)), roof, L['Wood'], 0.5)
    g.add(xform(rbox((W + 0.6, 0.13, 0.26), 0.05, 1), P(0, 0.2 + ridge + 0.12, 0), rot), roof, L['Wood'], 0.5)
    fz = D / 2
    dx = -W / 2 + 1.0 if W > 4.7 else 0.0
    g.add(xform(rbox((0.95, 1.95, 0.08), 0.03, 1), P(dx, 0.2 + 0.98, fz + 0.03), rot), S['Dock'] if wall != S['Dock'] else S['Accent'], L['Wood'], 0.5)
    g.add(xform(rbox((1.25, 0.12, 0.2), 0.03, 1), P(dx, 0.2 + 2.02, fz + 0.08), rot), trim, L['Wood'], 0.5)
    sh = trim if r.random() < 0.6 else None
    for wx in ((0.6, 1.75) if W > 4.7 else (-1.4, 1.4)):
        window2(g, P, rot, wx, 0.2 + 1.5, fz, 0.72, 0.8, trim, sh)
    # back: a window and a back door; gable ends: a small window each
    back = rot @ ry(180)
    PB = lambda x, y, z: Vector((cx, cy, cz)) + back @ Vector((x, y, z))
    window2(g, PB, back, 0.9, 0.2 + 1.5, fz, 0.7, 0.7, trim)
    g.add(xform(rbox((0.9, 1.9, 0.08), 0.03, 1), PB(-1.0, 0.2 + 0.95, fz + 0.03), back), S['WoodDark'], L['Wood'], 0.5)
    for s_ in (-1, 1):
        side = rot @ ry(90 * s_)
        PS = lambda x, y, z, side=side: Vector((cx, cy, cz)) + side @ Vector((x, y, z))
        window2(g, PS, side, 0.0, 0.2 + eave + 0.45, W / 2, 0.5, 0.5, trim)
    if chimney:
        hx = W / 2 - 0.7
        g.add(xform(rbox((0.55, 1.9, 0.55), 0.05, 1), P(hx, 0.2 + ridge - 0.1, -0.5), rot), S['Rock'], L['Rock'], 0.55)
        g.add(xform(rbox((0.7, 0.12, 0.7), 0.04, 1), P(hx, 0.2 + ridge + 0.9, -0.5), rot), S['Rock'], L['Rock'], 0.55)
        g.extra.setdefault('smoke_points', [])
        p = P(hx, 0.2 + ridge + 1.05, -0.5)
        if len(g.extra['smoke_points']) < 3:
            g.extra['smoke_points'].append(dict(name='CottageChimney_' + name, position=[round(p.x, 2), round(p.y, 2), round(p.z, 2)]))
    g.solid(name)
    return P

def bell_tower_and_chapel(g, r):
    x, z = CHAPEL
    y = LEVEL2_Y
    tw, th = 2.9, 9.4
    g.mark()
    # nave behind the tower: tall plastered walls, red roof, three tall windows a side, a little apse
    nz, nW, nD = z - 0.9 - 3.4, 5.4, 6.8
    rot = ry(0)
    P = lambda a, b, c: Vector((x, y, nz)) + Vector((a, b, c))
    poly = [(-nW / 2, 0.0), (nW / 2, 0.0), (nW / 2, 3.7), (0.0, 6.1), (-nW / 2, 3.7)]
    g.add(xform(rbox((nW + 0.3, 0.5, nD + 0.3), 0.06, 1), P(0, 0.15, 0)), S['Rock'], L['Rock'], 0.55)
    g.add(xform(prism(poly, nD, 0.04, segs=1), P(0, 0.35, 0), ry(90)), S['Sand'], L['Sand'], 0.5)
    run = nW / 2 + 0.4
    ang = math.degrees(math.atan2(6.1 - 3.7, nW / 2))
    slab = run / math.cos(math.radians(ang)) + 0.05
    for sx in (-1, 1):
        g.add(xform(rbox((slab, 0.15, nD + 0.5), 0.05, 1), P(sx * run / 2, 0.35 + 6.1 - 2.4 * (run / 2) / (nW / 2) + 0.08, 0), rz(-sx * ang)),
              S['Roof'], L['Wood'], 0.5)
    for sx in (-1, 1):
        for k in range(3):
            g.add(xform(rbox((0.08, 1.7, 0.6), 0.03, 1), P(sx * (nW / 2 + 0.02), 0.35 + 2.2, -2.2 + 2.2 * k)), S['Rock'], L['none'], 0.5, tint=0.5)
            g.add(xform(rbox((0.1, 1.9, 0.78), 0.03, 1), P(sx * (nW / 2 + 0.0), 0.35 + 2.2, -2.2 + 2.2 * k)), S['WoodDark'], L['Wood'], 0.5)
    add_lathe(g, P(0, 0.35, -nD / 2), [(0.0, 1.9), (3.0, 1.9), (3.1, 2.05), (3.2, 0.0)], S['Sand'], L['Sand'], 0.5, sides=12)
    # the bell tower: a stone shaft, a belfry with four arches and a bronze bell, a clock face toward the sea, a slate spire, a fish weathervane mount
    T = lambda a, b, c: Vector((x, y, z)) + Vector((a, b, c))
    g.add(xform(rbox((tw + 0.4, 0.6, tw + 0.4), 0.08, 1), T(0, 0.2, 0)), S['Rock'], L['Rock'], 0.55)
    g.add(xform(rbox((tw, th, tw), 0.06, 1), T(0, th / 2 + 0.3, 0)), S['Sand'], L['Rock'], 0.5)
    for hh in (3.2, 6.4):
        g.add(xform(rbox((tw + 0.18, 0.18, tw + 0.18), 0.04, 1), T(0, hh, 0)), S['Rock'], L['Rock'], 0.5)
    g.add(xform(rbox((1.2, 2.3, 0.12), 0.04, 1), T(0, 1.45, tw / 2 + 0.02)), S['Dock'], L['Wood'], 0.5)
    g.add(xform(rbox((1.5, 0.22, 0.2), 0.04, 1), T(0, 2.65, tw / 2 + 0.06)), S['Rock'], L['Rock'], 0.5)
    for k in range(2):
        g.add(xform(rbox((0.36, 0.9, 0.1), 0.03, 1), T(0, 4.6 + 2.0 * k, tw / 2 + 0.01)), S['Rock'], L['none'], 0.5, tint=0.45)
    cf = T(0, th - 0.4, tw / 2 + 0.05)
    add_lathe(g, cf, [(0.0, 0.0), (0.01, 0.78), (0.08, 0.82), (0.12, 0.0)], S['Sand'], L['none'], 0.7, sides=16, rot=rx(90))
    add_lathe(g, cf, [(0.0, 0.84), (0.1, 0.9), (0.14, 0.86)], S['WoodDark'], L['none'], 0.6, sides=16, rot=rx(90))
    g.add(xform(rbox((0.07, 0.6, 0.03), 0.01, 1), cf + Vector((0.0, 0.25, 0.14)), rz(-12)), S['WoodDark'], L['none'], 0.6)
    g.add(xform(rbox((0.07, 0.42, 0.03), 0.01, 1), cf + Vector((0.16, -0.08, 0.14)), rz(-115)), S['WoodDark'], L['none'], 0.6)
    by = 0.3 + th
    g.add(xform(rbox((tw + 0.3, 0.25, tw + 0.3), 0.05, 1), T(0, by + 0.1, 0)), S['Rock'], L['Rock'], 0.55)
    for sx in (-1, 1):
        for sz in (-1, 1):
            g.add(xform(rbox((0.55, 2.4, 0.55), 0.05, 1), T(sx * (tw / 2 - 0.27), by + 1.4, sz * (tw / 2 - 0.27))), S['Sand'], L['Rock'], 0.5)
    g.add(xform(rbox((tw + 0.3, 0.3, tw + 0.3), 0.05, 1), T(0, by + 2.7, 0)), S['Rock'], L['Rock'], 0.55)
    add_lathe(g, T(0, by + 0.95, 0), [(0.0, 0.62), (0.12, 0.6), (0.5, 0.42), (0.85, 0.3), (1.0, 0.18), (1.08, 0.0)], S['Accent'], L['none'], 0.6, sides=12)
    g.add(xform(rbox((0.12, 0.25, 1.9), 0.03, 1), T(0, by + 2.05, 0)), S['WoodDark'], L['Wood'], 0.5)
    add_lathe(g, T(0, by + 2.85, 0), [(0.0, 2.25), (0.25, 2.25), (4.6, 0.0)], S['Rock'], L['Wood'], 0.55, sides=4, rot=ry(45))
    add_lathe(g, T(0, by + 7.4, 0), [(0, 0.05), (1.1, 0.035), (1.12, 0.0)], S['WoodDark'], L['none'], 0.6, sides=6)
    d = g.solid('ChapelTower')
    g.extra['tower_top'] = [round(x, 2), round(y + by + 8.5, 2), round(z, 2)]
    g.extra['chapel_vane'] = [round(x, 3), round(y + by + 8.5, 3), round(z, 3)]
    wanted_poster(g, T(tw / 2 + 0.02, 1.6, 0.5), ry(90), r)
    g._mark = len(g.V)

def overlook(g, r):
    # benches, a coin telescope, a lamp and a little shrine-like noticeboard on the level-2 edge, facing the harbour
    for name, x, yaw in (('OverlookBench_W', -5.5, 0.0), ('OverlookBench_E', 14.0, 0.0)):
        bench(g, name, x, -96.45, yaw, r)
    tx, tz = 9.0, -95.9
    ty = land_y(tx, tz)
    g.mark()
    add_lathe(g, (tx, ty, tz), [(0, 0.14), (0.08, 0.12), (1.0, 0.06), (1.04, 0.09), (1.08, 0.0)], S['WoodDark'], L['none'], 0.5, sides=8)
    g.add(xform(rbox((0.26, 0.3, 0.32), 0.08, 1), (tx, ty + 1.2, tz)), S['Accent'], L['none'], 0.55)
    add_lathe(g, (tx, ty + 1.25, tz), [(0, 0.09), (0.55, 0.07), (0.6, 0.0)], S['Rock'], L['none'], 0.55, sides=8, rot=rx(75))
    g.solid('CoinTelescope')

def village_square(g, r):
    # Round 3: the village square - the hub where the lane, stair A (down to the harbour) and stair C (up to the chapel) meet. Cobbles in an
    # uneven disc with a darker kerb ring, a big shade tree (TREES2 'big') with a ring bench, the well and two market stalls (Harbour_Core_Story),
    # the HARBOUR BOARD (quest and shop notices, the crew's WANTED poster pinned on it) and two lamps with bunting across.
    cx, cz, R = VILLAGE_SQUARE
    ground_disc(g, cx, cz, R + 1.1, S['Dock'], L['Sand'], rings=5, segs=24, lift=0.075, seed=91, edge_slot=S['Reed'])     # round 4: worn earth, no cream rim
    ground_disc(g, cx, cz, R, S['Rock'], L['Rock'], rings=5, segs=24, lift=0.088, seed=92, edge_slot=S['Dock'])
    tx, tz = SQUARE_TREE
    ty = land_y(tx, tz)
    g.mark()
    for k in range(8):
        a0, a1 = 2 * math.pi * k / 8, 2 * math.pi * (k + 1) / 8
        am = (a0 + a1) / 2
        c = Vector((tx + 1.15 * math.cos(am), ty + 0.44, tz + 1.15 * math.sin(am)))
        g.add(xform(rbox((0.9, 0.06, 0.38), 0.02, 1), c, ry(-math.degrees(am) + 90)), S['Wood'], L['Wood'], 0.5)
        c2 = Vector((tx + 1.15 * math.cos(am), ty + 0.21, tz + 1.15 * math.sin(am)))
        g.add(xform(rbox((0.07, 0.42, 0.3), 0.015, 1), c2, ry(-math.degrees(am) + 90)), S['WoodDark'], L['Wood'], 0.45)
    g.solid('SquareRingBench')
    # the HARBOUR BOARD: two posts, a wide board under a little shingle roof, notices and the crew's poster
    bx, bz, byaw = HARBOUR_BOARD
    by = land_y(bx, bz)
    rot = ry(byaw)
    P = lambda a, b_, c: Vector((bx, by, bz)) + rot @ Vector((a, b_, c))
    g.mark()
    for sx in (-1.05, 1.05):
        add_lathe(g, P(sx, -0.1, 0), [(0, 0.08), (2.55, 0.07), (2.58, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=8)
    g.add(xform(rbox((2.3, 1.25, 0.08), 0.02, 1), P(0, 1.55, 0), rot), S['Dock'], L['Wood'], 0.5)
    g.add(xform(rbox((2.5, 0.08, 0.12), 0.02, 1), P(0, 2.22, 0.02), rot), S['WoodDark'], L['Wood'], 0.5)
    for s_ in (-1, 1):
        g.add(xform(rbox((2.7, 0.06, 0.42), 0.02, 1), P(0, 2.5, s_ * 0.17), rot @ rx(s_ * 28)), S['Roof'], L['Wood'], 0.55)
    g.solid('HarbourBoard')
    sign_text(g, 'HARBOUR BOARD', 0.15, P(0, 2.06, 0.06), rot, S2['Snow'])
    for k, (nx, ny, w, h, slot, tilt) in enumerate(((-0.75, 1.62, 0.42, 0.56, S['Sand'], -5), (-0.2, 1.48, 0.36, 0.46, S2['Snow'], 4),
                                                   (0.62, 1.66, 0.5, 0.36, S['Reed'], -3), (0.7, 1.22, 0.32, 0.3, S['Sand'], 8))):
        g.add(xform(rbox((w, h, 0.008), 0.003, 1), P(nx, ny, 0.05), rot @ rz(tilt)), slot, L['Cloth'], 0.7)
    wanted_poster(g, P(0.2, 1.3, 0.05), rot, r)
    g._mark = len(g.V)

def build_upper_village():
    # Round 3: three models (each FBX < 21k): the chapel, the overlook and the west cottages; the east cottages; the lane furniture (lamps,
    # garden fences, the village square). All Village (Structure box colliders).
    g0 = G2('Harbour_Core_Buildings_Upper', 'core', rays=16)
    ge = G2('Harbour_Core_Buildings_UpperEast', 'core', rays=16, note='Round 3: the east cottage row on the upper lane.')
    gl = G2('Harbour_Core_VillageLane', 'core', rays=12,
            note='Round 3: the upper lane furniture - lamps, picket fences round the front gardens, the village square (cobbles, ring bench, '
                 'HARBOUR BOARD). Box items -> Structure colliders.')
    r = rng2('uppervillage')
    for i, (x, z, W, D, eave, ridge, wall, wl, roof, trim, yaw) in enumerate(UPPER_HOUSES):
        cottage(g0 if x < 0 else ge, (x, LEVEL1_Y, z), W, D, eave, ridge, wall, wl, roof, trim, yaw, r, 'Cottage_%d' % i)
    g = g0
    bell_tower_and_chapel(g, r)
    overlook(g, r)
    g = gl
    tops = []
    for i, (x, z) in enumerate(LANE_LAMPS):
        tops.append(lamp_post(g, x, z, 'LaneLamp_%d' % i))
    g0.extra['lamp_tops'] = [[round(v, 3) for v in t] for t in tops]
    # round 3: a picket fence along every front garden's lane edge (straight runs, one box each), the gate gap on the door's path
    for i, (x, z, W, D, eave, ridge, wall, wl, roof, trim, yaw) in enumerate(UPPER_HOUSES):
        rot = ry(yaw)
        fwd = rot @ Vector((0.0, 0.0, 1.0))
        front = Vector((x, 0.0, z)) + fwd * (D / 2)
        edge = road_z(x) + (2.35 if fwd.z < 0 else -2.35)          # the lane's dirt edge on this side
        depth = abs(edge - front.z) - 0.35
        if depth < 0.9:
            continue
        dx = -W / 2 + 1.0 if W > 4.7 else 0.0
        P = lambda a, d: (Vector((x, 0.0, z)) + rot @ Vector((a, 0.0, D / 2 + d)))
        a0, a1 = -W / 2 - 0.3, W / 2 + 0.3
        fslot = (S['Sand'], S2['Snow'], S['WoodDark'])[i % 3]
        for (ua, ub, tag) in ((a0, dx - 0.6, 'W'), (dx + 0.6, a1, 'E')):
            pa, pb = P(ua, depth), P(ub, depth)
            picket_fence(g, [(pa.x, pa.z), (pb.x, pb.z)], 'GardenFence_%d%s' % (i, tag), cheap=True, slot=fslot)
        for ua in (a0, a1):
            pa, pb = P(ua, 0.15), P(ua, depth)
            picket_fence(g, [(pa.x, pa.z), (pb.x, pb.z)], 'GardenFenceSide_%d%s' % (i, 'W' if ua < 0 else 'E'), cheap=True, slot=fslot)
    village_square(g, r)
    g = g0
    # a field wall along the south edge of level 2 (the hills begin behind it; the invisible boundary is just beyond): visual only
    fw = r
    pa = -WALL_X
    for xa, xb in ((-WALL_X, HILL_TRACK[0][0] - 1.6), (HILL_TRACK[0][0] + 1.6, WALL_X)):
        n = max(1, int(round((xb - xa) / 3.2)))
        for k in range(n):
            cx = xa + (xb - xa) * (k + 0.5) / n
            hh = fw.uniform(0.7, 0.85)
            g.add(xform(rbox((0.6, hh, (xb - xa) / n - 0.05), 0.12, 1), (cx, LEVEL2_Y + hh / 2 - 0.1, WALL_ZS + 0.6), ry(90 + fw.uniform(-1.5, 1.5))),
                  S['Rock'] if fw.random() < 0.75 else S['Sand'], L['Rock'], 0.6)
    g.extra['level_y'] = dict(level0=PLATEAU_Y, level1=round(LEVEL1_Y, 3), level2=round(LEVEL2_Y, 3))
    return [g0, ge, gl]

# ---- the fishing jetty and the lookout tower (Structure, box colliders) ----
def build_jetty():
    g = G2('Harbour_Core_Jetty', 'core', rays=16,
           note='Round 1: the east-basin fishing jetty (stand point 11) and the lookout tower (stand point 12) with its stair. Box items '
                'become Structure BoxColliders (deck, posts, rails, stair steps); the stair handrails and the roof are visual only.')
    r = rng2('jetty')
    x0, x1, z0, z1, top = JETTY
    # deck planks across x, stringers, posts to the seabed/beach, bollards, a lamp at the head, a life ring
    z = z0
    k = 0
    while z < z1 - 0.05:
        w = min(0.42, z1 - z)
        g.add(xform(rbox((x1 - x0 + r.uniform(-0.04, 0.06), 0.07, w - 0.035), 0.012, 1), ((x0 + x1) / 2 + r.uniform(-0.03, 0.03), top - 0.035, z + w / 2),
                    ry(r.uniform(-0.6, 0.6))), S['Wood'] if r.random() < 0.55 else S['Dock'], L['Wood'], 0.5)
        z += w
        k += 1
    for sx in (x0 + 0.25, x1 - 0.25):
        pbox(g, (sx - 0.08, top - 0.26, z0), (sx + 0.08, top - 0.07, z1), S['WoodDark'], L['Wood'], 0.45)
    posts = []
    zz = z1 - 0.3
    while zz > z0 + 1.0:
        posts.append(zz)
        zz -= 3.0
    for pz in posts:
        for px in (x0 + 0.12, x1 - 0.12):
            foot = terrain_y(pz) - 0.05
            add_lathe(g, (px, foot, pz), [(0, 0.13), (0.15, 0.11), (top - 0.07 - foot, 0.1), (top + 0.18 - foot, 0.1), (top + 0.22 - foot, 0.0)],
                      S['WoodDark'], L['Wood'], 0.45, sides=7)
            box_item(g, 'JettyPost', (px - 0.11, foot, pz - 0.11), (px + 0.11, top - 0.07, pz + 0.11))
    box_item(g, 'JettyDeck', (x0, top - 0.12, z0), (x1, top, z1))
    for bx in (x0 + 0.35, x1 - 0.35):
        g.mark()
        add_lathe(g, (bx, top, z1 - 0.35), [(0, 0.15), (0.3, 0.13), (0.36, 0.18), (0.42, 0.0)], S['WoodDark'], L['none'], 0.5, sides=10)
        g.solid('JettyBollard')
    lamp_post(g, x1 - 0.3, z1 - 4.6, 'JettyLamp', y=top)
    # life ring on a post by the head
    lc = Vector((x1 - 0.05, top + 0.9, z1 - 2.6))
    add_lathe(g, (x1 - 0.05, top - 0.1, z1 - 2.6), [(0, 0.06), (1.2, 0.05), (1.22, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=6)
    pts = [Vector((0.0, 0.24 * math.sin(a), 0.24 * math.cos(a))) for a in [2 * math.pi * i / 16 for i in range(16)]]
    V, N, F, UV = tube(pts, 0.06, 6, caps=False, closed=True)
    fsl = [((S['Sand'] if int(((math.atan2(sum(V[k].y for k in f), sum(V[k].z for k in f)) + math.pi) / (math.pi / 4))) % 2 == 0 else S['Accent']), L['Cloth']) for f in F]
    g.add(xform((V, N, F, UV), lc + Vector((0.09, 0, 0))), 0, 0, 0.55, fsl)

    # ---- lookout tower ----
    lx0, lx1, lz0, lz1, ltop = LOOKOUT
    sx0, sx1, sfoot = LOOKOUT_STAIR
    # deck planks across z
    xq = lx0
    while xq < lx1 - 0.05:
        w = min(0.34, lx1 - xq)
        g.add(xform(rbox((w - 0.035, 0.08, lz1 - lz0 + r.uniform(-0.03, 0.05)), 0.012, 1), (xq + w / 2, ltop - 0.04, (lz0 + lz1) / 2)),
              S['Wood'] if r.random() < 0.55 else S['Dock'], L['Wood'], 0.5)
        xq += w
    for zq in (lz0 + 0.25, (lz0 + lz1) / 2, lz1 - 0.25):
        pbox(g, (lx0, ltop - 0.3, zq - 0.08), (lx1, ltop - 0.08, zq + 0.08), S['WoodDark'], L['Wood'], 0.45)
    box_item(g, 'LookoutDeck', (lx0, ltop - 0.14, lz0), (lx1, ltop, lz1))
    cols = [(lx0 + 0.2, lz0 + 0.2), (lx0 + 0.2, lz1 - 0.2), (lx1 - 0.2, lz1 - 0.2), (sx0 - 0.15, lz0 + 0.2)]
    for (px, pz) in cols:
        foot = terrain_y(pz) - 0.1
        add_lathe(g, (px, foot, pz), [(0, 0.19), (0.25, 0.16), (LOOKOUT_ROOF - foot, 0.13), (LOOKOUT_ROOF - foot + 0.05, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=8)
        box_item(g, 'LookoutPost', (px - 0.15, foot, pz - 0.15), (px + 0.15, LOOKOUT_ROOF, pz + 0.15))
    # diagonal braces under the deck (visual)
    for (ax_, az_), (bx_, bz_) in ((cols[0], cols[1]), (cols[1], cols[2]), (cols[0], cols[3])):
        a = Vector((ax_, terrain_y(az_) + 0.6, az_)); b = Vector((bx_, ltop - 0.35, bz_))
        g.add(tube([a, b], 0.06, 5), S['Wood'], L['Wood'], 0.45)
        a2 = Vector((bx_, terrain_y(bz_) + 0.6, bz_)); b2 = Vector((ax_, ltop - 0.35, az_))
        g.add(tube([a2, b2], 0.06, 5), S['Wood'], L['Wood'], 0.45)
    # railings (box colliders): north, west, east, the south side west of the stair opening
    rail_h = 1.0
    runs = [((lx0, lz1), (lx1, lz1)), ((lx0, lz0), (lx0, lz1)), ((lx1, lz0), (lx1, lz1)), ((lx0, lz0), (sx0, lz0))]
    for (ax_, az_), (bx_, bz_) in runs:
        L_ = math.hypot(bx_ - ax_, bz_ - az_)
        n = max(1, int(L_ / 0.9))
        for k in range(n + 1):
            t = k / n
            px, pz = ax_ + (bx_ - ax_) * t, az_ + (bz_ - az_) * t
            pbox(g, (px - 0.035, ltop, pz - 0.035), (px + 0.035, ltop + rail_h - 0.05, pz + 0.035), S['Wood'], L['Wood'], 0.5, r=0.012)
        for hh in (0.5, rail_h):
            lo = (min(ax_, bx_) - 0.05, ltop + hh - 0.07, min(az_, bz_) - 0.05)
            hi = (max(ax_, bx_) + 0.05, ltop + hh, max(az_, bz_) + 0.05)
            pbox(g, lo, hi, S['WoodDark'], L['Wood'], 0.45, r=0.02)
        box_item(g, 'LookoutRail', (min(ax_, bx_) - 0.05, ltop, min(az_, bz_) - 0.05), (max(ax_, bx_) + 0.05, ltop + rail_h, max(az_, bz_) + 0.05))
    # the stair: solid treads (box colliders), visual stringers and handrails
    rise_total = ltop - PLATEAU_Y
    n = 11
    rise, tread = rise_total / n, (lz0 - sfoot) / n
    for k in range(n):
        za, zb = sfoot + tread * k, sfoot + tread * (k + 1)
        ytop = PLATEAU_Y + rise * (k + 1)
        g.add(xform(rbox((sx1 - sx0 - 0.06, 0.07, tread + 0.04), 0.012, 1), ((sx0 + sx1) / 2, ytop - 0.035, (za + zb) / 2)), S['Wood'], L['Wood'], 0.5)
        box_item(g, 'LookoutStep', (sx0, min(ytop - 0.6, terrain_y(zb) - 0.05), za), (sx1, ytop, zb + (0.02 if k < n - 1 else 0.0)))
    for sx in (sx0 + 0.04, sx1 - 0.04):
        a = Vector((sx, PLATEAU_Y - 0.1, sfoot)); b = Vector((sx, ltop - 0.05, lz0))
        g.add(xform(rbox((0.07, 0.26, (b - a).length), 0.02, 1), (a + b) / 2, rx(math.degrees(math.atan2(b.y - a.y, b.z - a.z)))), S['WoodDark'], L['Wood'], 0.45)
        g.add(tube([a + Vector((0, 1.0, 0.1)), b + Vector((0, 1.0, 0.0))], 0.035, 5), S['Wood'], L['Wood'], 0.5)
        for t in (0.0, 0.5, 1.0):
            p = a.lerp(b, t)
            pbox(g, (sx - 0.035, p.y, p.z - 0.035), (sx + 0.035, p.y + 1.0, p.z + 0.035), S['WoodDark'], L['Wood'], 0.5, r=0.012)
    # roof (visual): a gable over the deck, a bell, a pennant pole; a telescope on a tripod; a LOOKOUT board
    rw, rd = lx1 - lx0 + 0.7, lz1 - lz0 + 0.7
    cx_, cz_ = (lx0 + lx1) / 2, (lz0 + lz1) / 2
    for s_ in (-1, 1):
        g.add(xform(rbox((rw, 0.12, rd / 2 / math.cos(math.radians(26)) + 0.08), 0.04, 1), (cx_, LOOKOUT_ROOF + 0.5, cz_ + s_ * rd / 4), rx(s_ * 26)),
              S['Roof'], L['Wood'], 0.5)
    g.add(xform(rbox((rw + 0.05, 0.12, 0.24), 0.04, 1), (cx_, LOOKOUT_ROOF + 0.98, cz_)), S['WoodDark'], L['Wood'], 0.5)
    for s_ in (-1, 1):
        pbox(g, (lx0 + 0.1, LOOKOUT_ROOF - 0.15, cz_ + s_ * (rd / 2 - 0.5) - 0.07), (lx1 - 0.1, LOOKOUT_ROOF + 0.05, cz_ + s_ * (rd / 2 - 0.5) + 0.07),
             S['WoodDark'], L['Wood'], 0.45)
    add_lathe(g, (cx_ - 0.8, LOOKOUT_ROOF - 0.95, cz_), [(0.0, 0.3), (0.08, 0.29), (0.35, 0.2), (0.55, 0.13), (0.62, 0.0)], S['Accent'], L['none'], 0.6, sides=10)
    add_lathe(g, (cx_ + 1.4, LOOKOUT_ROOF + 1.0, cz_), [(0, 0.05), (2.2, 0.035), (2.22, 0.0)], S['Sand'], L['none'], 0.55, sides=6)
    g.extra['pennant'] = [round(cx_ + 1.4, 3), round(LOOKOUT_ROOF + 3.0, 3), round(cz_, 3)]
    tx_, tz_ = lx0 + 0.9, lz1 - 0.6
    for a in (0, 120, 240):
        d = Vector((math.sin(math.radians(a)), 0, math.cos(math.radians(a)))) * 0.3
        g.add(tube([Vector((tx_, ltop, tz_)) + d, Vector((tx_, ltop + 1.15, tz_))], 0.02, 4), S['WoodDark'], L['none'], 0.5)
    add_lathe(g, (tx_, ltop + 1.2, tz_), [(0, 0.07), (0.6, 0.055), (0.62, 0.0)], S['Accent'], L['none'], 0.55, sides=8, rot=rx(70) @ rz(-20))
    bc = Vector((lx0 - 0.02, ltop - 0.8, cz_))
    g.add(xform(rbox((0.06, 0.55, 2.2), 0.02, 1), bc), S['Dock'], L['Wood'], 0.5)

    # ---- the tug's departure board on the breakwater root (story: the crew is leaving tonight) ----
    bx_, bz_ = 33.6, -55.2
    by_ = land_y(bx_, bz_)
    g.mark()
    for s_ in (-0.75, 0.75):
        add_lathe(g, (bx_ + s_, by_ - 0.1, bz_), [(0, 0.06), (2.2, 0.055), (2.22, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=6)
    g.add(xform(rbox((1.8, 1.1, 0.07), 0.03, 1), (bx_, by_ + 1.55, bz_)), S['WoodDark'], L['Wood'], 0.5)
    g.add(xform(rbox((1.95, 0.08, 0.25), 0.02, 1), (bx_, by_ + 2.15, bz_ + 0.03)), S['Wood'], L['Wood'], 0.5)
    rot = ry(0)
    sign_text(g, 'LUCKY BREAK', 0.19, (bx_, by_ + 1.85, bz_ + 0.04), rot, S['Accent'])
    sign_text(g, 'DEPARTS AT DUSK', 0.13, (bx_, by_ + 1.55, bz_ + 0.04), rot, S['Sand'])
    g.solid('DepartureBoard')
    return g

# ---- moorings in the basin (detail: visual only) ----
def build_moorings():
    g = G2('Harbour_Detail_Moorings', 'detail', rays=10,
           note='Round 1: boats moored in the east basin by the jetty, mooring lines and a stack of fish crates on the jetty. No colliders.')
    r = rng2('moorings')
    x0, x1, z0, z1, top = JETTY
    rowboat(g, 'Moored_Rowboat', (53.5, 0.28, -41.6), 4.0, S['Foliage'])
    g.items.pop()
    # a small motor fishing boat on the east side: hull, a little wheelhouse, a mast with a lamp, crates on deck
    # (round 4: the small motor fishing boat moored here is gone - the East Ledge took its berth on the jetty's east side)
    # fenders hanging off the jetty, a crate stack and a pot pile on the deck by the head
    for zf in (-37.0, -40.0, -43.0):
        add_lathe(g, (x1 + 0.12, top - 0.75, zf), [(0, 0.0), (0.05, 0.11), (0.45, 0.11), (0.5, 0.0)], S['Accent'], L['none'], 0.55, sides=8)
    for k in range(3):
        g.add(xform(rbox((0.55, 0.32, 0.4), 0.03, 1), (x0 + 0.5, top + 0.16 + 0.32 * k, z1 - 4.0 + 0.04 * k), ry(r.uniform(-12, 12))),
              S['Wood'] if k % 2 == 0 else S['Dock'], L['Wood'], 0.5)
    for k in range(2):
        lobster_pot(g, 'JettyPot_%d' % k, (x1 - 0.5, top + 0.36 * k, z1 - 7.4), r.uniform(-20, 20), 0.0)
        g.items.pop()
    g.items.clear()
    return g

# ---- windmill sails (core, local: they turn) ----
def build_windmill_sails():
    g = G2('Harbour_Core_Anim_WindmillSails', 'core', local=True, rays=8, anim='rotor',
           note='Round 1: the windmill sails, around the hub at the origin; they turn about local +z (animHint rotor). Core: visible on Low.')
    add_lathe(g, (0, 0, -0.2), [(0.0, 0.35), (0.5, 0.3), (0.62, 0.12), (0.7, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=10, rot=rx(90))
    for k in range(4):
        rot = rz(45 + 90 * k)
        g.add(xform(rbox((0.16, 6.2, 0.14), 0.03, 1), Vector((0, 0, 0.25)) + rot @ Vector((0, 3.1, 0)), rot), S['WoodDark'], L['Wood'], 0.5)
        g.add(xform(rbox((1.25, 4.6, 0.04), 0.015, 1), Vector((0, 0, 0.3)) + rot @ Vector((0.7, 3.6, 0)), rot), S['Sand'], L['Cloth'], 0.6)
        for j in range(4):
            g.add(xform(rbox((1.35, 0.05, 0.05), 0.012, 1), Vector((0, 0, 0.34)) + rot @ Vector((0.68, 1.6 + 1.3 * j, 0)), rot), S['WoodDark'], L['Wood'], 0.5)
    g.place('WindmillSails', tuple(windmill_hub()), 0.0)      # one instance at the hub, facing +z (the harbour)
    return g

def windmill_hub():
    # the sails' hub: on the cap's front (+z, the door side, toward the harbour)
    x, z = WINDMILL
    return Vector((x, land_y(x, z) - 0.3 + 9.3, z + 2.15))

def windmill_body(g):
    x, z = WINDMILL
    y0 = land_y(x, z) - 0.3
    add_lathe(g, (x, y0, z), [(0.0, 2.9), (0.6, 2.8), (6.5, 2.1), (8.6, 1.85), (8.7, 0.0)], S['Sand'], L['Rock'], 0.55, sides=10,
              ring_slots=[S['Rock'], S['Sand'], S['Sand'], S['Sand']])
    add_lathe(g, (x, y0 + 8.6, z), [(0.0, 2.15), (0.2, 2.2), (1.2, 1.7), (2.2, 0.9), (2.7, 0.0)], S['Roof'], L['Wood'], 0.55, sides=10)
    add_lathe(g, (x, y0 + 4.2, z), [(0.0, 2.55), (0.12, 2.6), (0.2, 0.0)], S['WoodDark'], L['Wood'], 0.55, sides=10)
    g.add(xform(rbox((1.1, 1.9, 0.2), 0.04, 1), (x, y0 + 1.0, z + 2.75)), S['Dock'], L['Wood'], 0.5)
    for h in (2.8, 5.6):
        g.add(xform(rbox((0.6, 0.75, 0.18), 0.03, 1), (x, y0 + h, z + 2.55 - 0.08 * h)), S['Rock'], L['none'], 0.5, tint=0.5)
    hub = windmill_hub()
    g.extra['windmill_hub'] = [round(hub.x, 3), round(hub.y, 3), round(hub.z, 3)]
    return hub

def build_hill_track(g):
    # the farm track from the level-2 field gap up the hill (switchbacks), a fence along it, the farm gate; the black sedan parked at a bend,
    # watching the harbour (story: the mafia is in town)
    pts = [(x, z) for x, z in HILL_TRACK]
    ground_ribbon(g, pts, 1.5, S['Dock'], L['Sand'], step=1.5, lift=0.07, seed=91, wobble=0.08, vsl_across=[S['Reed'], S['Dock'], S['WoodDark'], S['Dock'], S['Reed']])
    r = rng2('hilltrack')
    for i in range(len(pts) - 1):
        (ax, az), (bx, bz) = pts[i], pts[i + 1]
        L_ = math.hypot(bx - ax, bz - az)
        nx, nz = -(bz - az) / L_, (bx - ax) / L_
        n = int(L_ / 3.5)
        for k in range(n):
            t = (k + 0.5) / n
            px, pz = ax + (bx - ax) * t + nx * 2.4, az + (bz - az) * t + nz * 2.4
            if inside_walls(px, pz, 0.5):
                continue
            add_lathe(g, (px, land_y(px, pz) - 0.15, pz), [(0, 0.06), (1.15, 0.05), (1.18, 0.0)], S['Wood'], L['Wood'], 0.5, sides=5)
    # round 2b: the farm track (no fence; a few posts at the bends)
    ground_ribbon(g, FARM_TRACK, 1.3, S['Dock'], L['Sand'], step=1.5, lift=0.07, seed=93, wobble=0.1, vsl_across=[S['Reed'], S['Dock'], S['WoodDark'], S['Dock'], S['Reed']])
    for (px, pz) in FARM_TRACK[1:-1]:
        for s_ in (-1, 1):
            qx = px + s_ * 2.0
            add_lathe(g, (qx, land_y(qx, pz) - 0.15, pz), [(0, 0.06), (1.15, 0.05), (1.18, 0.0)], S['Wood'], L['Wood'], 0.5, sides=5)
    black_car(g, *HILL_SEDAN)

def build_creek_ribbon(g):
    # the creek out of the culvert in retaining wall 1, across the plateau and the beach into the bay (water material; visual only)
    pts = [Vector((p[0], 0.0, p[1])) for p in CREEK]
    dense = []
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        n = max(1, int((b - a).length / 1.0))
        for k in range(n):
            dense.append(a.lerp(b, k / n))
    dense.append(pts[-1])
    V, N, F, UV = [], [], [], []
    across = [-1.0, -0.5, 0.0, 0.5, 1.0]
    for i, p in enumerate(dense):
        t = (dense[min(i + 1, len(dense) - 1)] - dense[max(i - 1, 0)]).normalized()
        side = Vector((t.z, 0.0, -t.x))
        w = CREEK_HW * (1.0 + 0.25 * n2(p.x, p.z, 0.3, 95)) * (1.0 + 0.8 * smoothstep(-49.0, -44.0, p.z))
        for a in across:
            q = p + side * (a * w)
            base = terrain_y(q.z) if q.z > -55.0 else PLATEAU_Y
            y = base + 0.045 - 0.03 * abs(a) ** 4
            if q.z > COAST_Z:
                y = 0.03
            V.append(Vector((q.x, y, q.z))); N.append(Vector((0, 1, 0)))
    m = len(across)
    for i in range(len(dense) - 1):
        for k in range(m - 1):
            a = i * m + k
            q = [a, a + 1, a + m + 1, a + m]
            pa, pb, pc = V[q[0]], V[q[1]], V[q[2]]
            if (pb - pa).cross(pc - pa).y < 0:
                q = list(reversed(q))
            F.append(q); UV.append([(V[j].x, V[j].z) for j in q])
    g.add((V, N, F, UV), S['Sand'], L['none'], 1.0)

def build_creek_dressing(g, r):
    # culvert arch in the wall, a plank footbridge where the plateau path would cross, stepping stones on the beach, reeds and pebbles
    cx, cz = CREEK[0]
    face_z = T1_Z + RISER_HALF + 0.12
    g.add(xform(rbox((1.6, 1.0, 0.12), 0.05, 1), (cx, PLATEAU_Y + 0.35, face_z + 0.02)), S['Rock'], L['none'], 0.5, tint=0.25)
    for k in range(7):
        a = math.pi * k / 6
        p = Vector((cx + 1.0 * math.cos(a), PLATEAU_Y - 0.1 + 0.95 * math.sin(a), face_z + 0.12))
        g.add(xform(blob(0.28, r, (1.0, 0.8, 0.6), subdiv=1, rough=0.15), p), S['Rock'], L['Rock'], 0.6)
    bz = -58.6      # (round 4: the plank crossing here is gone - the glen's banks stand either side; the stepping stones cross the bed)
    # round 3: stepping stones upstream on the plateau, and an arched plank footbridge over the mouth onto the West Horn
    for k, (x, z) in enumerate(((-58.4, -66.8), (-57.6, -67.5), (-56.9, -68.3), (-56.2, -69.0))):
        g.add(xform(blob(0.32, r, (1.2, 0.45, 1.0), subdiv=1, rough=0.12, flat=-0.3), (x, PLATEAU_Y + 0.04, z)), S['Rock'], L['Rock'], 0.6)
    n = 0
    for _ in range(400):
        if n >= 44:
            break
        i = r.randrange(len(CREEK) - 1)
        (ax, az), (bx2, bz2) = CREEK[i], CREEK[i + 1]
        t = r.random()
        px, pz = ax + (bx2 - ax) * t, az + (bz2 - az) * t
        side = 1 if r.random() < 0.5 else -1
        x, z = px + side * (CREEK_HW + r.uniform(0.1, 0.7)), pz
        if z > -46.5:
            continue
        y = PLATEAU_Y if z < -55.0 else terrain_y(z)
        for k in range(r.randint(4, 7)):
            a = r.uniform(0, 2 * math.pi)
            base = Vector((x + 0.14 * math.cos(a), y - 0.04, z + 0.14 * math.sin(a)))
            hgt = r.uniform(0.7, 1.4)
            g.add(blade_geo(base, (math.cos(a), 0, math.sin(a)), hgt, 0.03, 0.15, segs=2), S['Reed'], L['Grass'], 0.6)
            if k == 0 and r.random() < 0.5:
                top = base + Vector((0, hgt * 0.92, 0))
                add_lathe(g, top - Vector((0, 0.18, 0)), [(0, 0.0), (0.02, 0.035), (0.16, 0.035), (0.18, 0.0)], S['WoodDark'], L['none'], 0.6, sides=5)
        n += 1

# ---- terrace dressing (detail): gardens, laundry, flower boxes, tufts and flowers on the levels, planters on the overlook ----
def build_terrace_dressing(obst):
    g = G2('Harbour_Detail_Terraces', 'detail', rays=8, ao_dist=0.6,
           note='Round 1: level 1 and 2 dressing - vegetable plots, washing lines, flower boxes, potted plants, grass and flowers. No colliders.')
    r = rng2('terracedress')
    def tuft(x, z, n, h, slot):
        y = land_y(x, z) - 0.02
        for k in range(n):
            a = r.uniform(0, 2 * math.pi)
            off = Vector((math.cos(a), 0, math.sin(a))) * r.uniform(0.0, 0.1)
            g.add(blade_geo(Vector((x, y, z)) + off, (math.cos(a), 0.0, math.sin(a)), h * r.uniform(0.6, 1.1), 0.045, r.uniform(0.25, 0.6), segs=2),
                  slot, L['Grass'], 0.55)
    def ok(x, z, m=0.3):
        if in_ramp(x, z, 0.8) or abs(z - T1_Z) < 0.7 or abs(z - T2_Z) < 0.7:
            return False
        for pts, hw in PATHS:
            if poly_dist(x, z, pts) < hw + 0.15:
                return False
        if road_dist(x, z) < 2.6:
            return False
        for (lo, hi) in obst:
            if lo[0] - m <= x <= hi[0] + m and lo[2] - m <= z <= hi[2] + m:
                return False
        return True
    # vegetable plots behind three cottages: soil beds with rows of cabbages and bean poles
    for i in (1, 3, 6, 8):
        x, z, W, D = UPPER_HOUSES[i][:4]
        back = -1.0 if math.cos(math.radians(UPPER_HOUSES[i][10])) > 0 else 1.0     # round 3: behind = away from the lane
        bz = z + back * (D / 2 + 1.6)
        field_patch(g, x, bz, W - 0.6, 1.6, 0.0, S['Dock'], step=1.0)
        for row in range(2):
            for k in range(int((W - 1.2) / 0.55)):
                px, pz = x - W / 2 + 0.9 + 0.55 * k, bz - 0.4 + 0.8 * row
                g.add(xform(blob(0.17, r, (1.0, 0.75, 1.0), subdiv=1, rough=0.25), (px, land_y(px, pz) + 0.28, pz)),
                      S['Foliage'] if (k + row) % 3 else S['FoliageDark'], L['Foliage'], 0.6)
    # washing lines between neighbouring cottages (back yards)
    for i, j in ((3, 4), (5, 6)):        # round 3: the two south-row pairs (their back yards face wall 2)
        xa, za = UPPER_HOUSES[i][0] + UPPER_HOUSES[i][2] / 2 + 0.2, UPPER_HOUSES[i][1] - 1.0
        xb, zb = UPPER_HOUSES[j][0] - UPPER_HOUSES[j][2] / 2 - 0.2, UPPER_HOUSES[j][1] - 1.0
        pa, pb = Vector((xa, LEVEL1_Y + 2.1, za)), Vector((xb, LEVEL1_Y + 2.1, zb))
        g.add(tube(rope_line(pa, pb, 0.2, n=10), 0.01, 3, caps=False), S['Sand'], L['Cloth'], 0.6)
        d = (pb - pa).normalized()
        yaw = math.degrees(math.atan2(d.x, d.z)) + 90
        for k, (t, w, h, slot) in enumerate(((0.2, 0.55, 0.6, S['Accent']), (0.4, 0.5, 0.55, S['Sand']), (0.6, 0.7, 0.45, S['Roof']), (0.8, 0.45, 0.6, S['Foliage']))):
            p = pa.lerp(pb, t) - Vector((0, 0.2 * 4 * t * (1 - t), 0))
            g.add(xform(rbox((w, h, 0.02), 0.01, 1), p - Vector((0, h / 2, 0)), ry(yaw) @ rx(r.uniform(-5, 5))), slot, L['Cloth'], 0.65)
    # flower boxes under the cottages' front windows
    for (x, z, W, D, eave, ridge, wall, wl, roof, trim, yaw) in UPPER_HOUSES:
        rot = ry(yaw)
        for wx in ((0.6, 1.75) if W > 4.7 else (-1.4, 1.4)):
            c = Vector((x, LEVEL1_Y, z)) + rot @ Vector((wx, 0.2 + 1.5 - 0.62, D / 2 + 0.2))
            g.add(xform(rbox((0.85, 0.2, 0.2), 0.03, 1), c, rot), S['Wood'], L['Wood'], 0.55)
            for k in range(4):
                g.add(xform(blob(0.09, r, (1, 0.8, 1), subdiv=1, rough=0.2), c + rot @ Vector((-0.3 + 0.2 * k, 0.15, 0.0))),
                      [S['Accent'], S['Roof'], S['Sand'], S['Foliage']][(k + int(x)) % 4], L['none'], 0.65)
    # back-yard clutter on the lane side of every cottage: a crate stack with a barrel, a woodpile or a pot stack; a bench by three front doors
    for i, (x, z, W, D, eave, ridge, wall, wl, roof, trim, yaw) in enumerate(UPPER_HOUSES):
        back = -1.0 if math.cos(math.radians(yaw)) > 0 else 1.0
        bx, bz = x + (W / 2 - 0.7) * (1 if i % 2 else -1), z + back * (D / 2 + 0.75)
        g.mark()
        if i % 3 == 0:
            crate_stack(g, bx, bz, r, 'YardCrates_%d' % i)
        elif i % 3 == 1:
            pot_stack(g, bx, bz, r, 'YardPots_%d' % i)
        else:
            for k in range(3):
                geo, fs = lathe([(-0.5, 0.0), (-0.49, 0.11), (0.49, 0.11), (0.5, 0.0)], 7, cap0=True, cap1=True)
                g.add(xform(geo, (bx - 0.25 + 0.24 * k, land_y(bx, bz) + 0.12 + 0.2 * (k == 1), bz), rx(90) @ ry(r.uniform(0, 60))), S['WoodDark'], L['Bark'], 0.45)
        if i in (0, 3, 5, 7, 9):
            fp = Vector((x, 0.0, z)) + ry(yaw) @ Vector((1.6, 0.0, D / 2 + 0.75))
            bench(g, 'CottageBench_%d' % i, fp.x, fp.z, yaw, r)
    g.items.clear()
    # potted plants and a planter row on the overlook; tufts and flower clumps over both levels
    for k, x in enumerate((-8.5, -1.0, 11.0, 17.5)):
        z = -95.8
        y = land_y(x, z)
        add_lathe(g, (x, y, z), [(0, 0.2), (0.4, 0.26), (0.45, 0.28), (0.48, 0.0)], S['Dock'], L['none'], 0.55, sides=10)
        g.add(xform(blob(0.32, r, (1.0, 0.9, 1.0), subdiv=2, rough=0.25), (x, y + 0.72, z)), S['Foliage'], L['Foliage'], 0.55)
        for j in range(3):
            g.add(xform(blob(0.07, r, (1, 0.8, 1), subdiv=1, rough=0.1), (x + 0.2 * math.cos(j * 2.1), y + 0.95, z + 0.2 * math.sin(j * 2.1))),
                  [S['Accent'], S['Roof'], S['Sand']][(k + j) % 3], L['none'], 0.65)
    placed = flowers = 0
    for _ in range(3000):
        if placed >= 60:      # round 3: 110 -> 60 (the instanced ground cover carries the grass; ten cottages' dressing needed the room)
            break
        x, z = r.uniform(-59.0, 59.0), r.uniform(-105.6, -76.6)
        if not ok(x, z, 0.35):
            continue
        tuft(x, z, r.randint(4, 7), r.uniform(0.3, 0.6), S['Reed'] if r.random() < 0.45 else S['Grass'])
        placed += 1
        if r.random() < 0.3:
            col = [S['Accent'], S['Sand'], S['Roof']][flowers % 3]
            for k in range(r.randint(4, 6)):
                fx, fz = x + r.uniform(-0.5, 0.5), z + r.uniform(-0.5, 0.5)
                fy = land_y(fx, fz)
                hgt = r.uniform(0.18, 0.36)
                g.add(blade_geo((fx, fy - 0.02, fz), (r.uniform(-1, 1), 0, r.uniform(-1, 1)), hgt, 0.012, 0.15, segs=2), S['Grass'], L['Grass'], 0.6)
                add_lathe(g, (fx, fy + hgt - 0.02, fz), [(0, 0.0), (0.01, 0.045), (0.03, 0.05), (0.045, 0.0)], col, L['none'], 0.75, sides=5)
            flowers += 1
    # Round 2: clipped hedges (0.8 m) along the south field wall and low flowering hedges (0.55 m) behind both wall-top parapets, cut into
    # runs around everything in the way (round 1's 46 one-metre hedge blobs read as melons and one blocked the overlook view).
    def runs(z, x0, x1, m=0.45, min_len=3.0, max_len=12.0):
        out, cur = [], []
        for x in frange(x0, x1, 0.4):
            if ok(x, z, m):
                cur.append(x)
                if cur[-1] - cur[0] >= max_len:
                    out.append(cur); cur = []
            else:
                if cur and cur[-1] - cur[0] >= min_len:
                    out.append(cur)
                cur = []
        if cur and cur[-1] - cur[0] >= min_len:
            out.append(cur)
        return out
    bushes = 0
    gh = G2('Harbour_Detail_Hedges', 'detail', rays=6, ao_dist=0.6,
            note='Round 2: clipped hedges along the south field wall, flowering hedge strips behind the wall-top parapets, low shrubs. No colliders.')
    g0, g = g, gh
    for run in runs(WALL_ZS + 1.7, -58.5, 58.5):
        zz = WALL_ZS + 1.7
        hedge_row(g, [(run[0], zz + r.uniform(-0.15, 0.15)), (run[-1] - 0.6, zz + r.uniform(-0.15, 0.15))], r, h=0.85, w=0.8)
        bushes += 1
    for zw in (T1_Z, T2_Z):
        zz = zw - 1.3
        for k, run in enumerate(runs(zz, -58.5, 58.5, 0.3, 2.0, 7.0)):
            if k % 3 == 2:
                continue        # leave gaps: the parapet shows and the view stays open
            hedge_row(g, [(run[0], zz), (run[-1], zz + r.uniform(-0.1, 0.1))], r, h=0.55, w=0.6, step=0.5,
                      slot=S['Foliage'] if k % 2 else S['FoliageDark'], flowers=0.22)
            bushes += 1
    for _ in range(300):
        if bushes >= 40:
            break
        x, z = r.uniform(-58.0, 58.0), r.uniform(-93.5, -91.0)
        if not ok(x, z, 0.5):
            continue
        shrub(g, x, z, r, r.uniform(0.35, 0.5), flowers=[S['Accent'], S['Roof'], S2['Snow'], None][bushes % 4])
        bushes += 1
    g = g0
    g.extra['counts'] = dict(tufts=placed, flower_patches=flowers, bushes=bushes)
    return [g, gh]

def build_gull_fly():
    g = G2('Harbour_Detail_Anim_GullFly', 'detail', local=True, rays=8, anim='circle',
           note='Round 1: a gliding gull (wings out, ~1 m span). Origin at the body centre; it circles its placement (animHint circle, '
                'radius in the placement) and banks into the turn.')
    r = rng2('gullfly')
    g.add(xform(blob(0.13, r, (0.85, 0.8, 1.9), subdiv=2, rough=0.03, flat=-2), (0, 0, 0)), S['Sand'], L['none'], 0.7)
    g.add(xform(blob(0.08, r, (1, 1, 1.1), subdiv=2, rough=0.02, flat=-2), (0, 0.06, 0.26)), S['Sand'], L['none'], 0.7)
    g.add(xform(rbox((0.03, 0.03, 0.08), 0.01, 1), (0, 0.05, 0.36)), S['Accent'], L['none'], 0.7)
    for s_ in (-1, 1):
        inner = [Vector((s_ * 0.05, 0.02, 0.06)), Vector((s_ * 0.3, 0.06, 0.08)), Vector((s_ * 0.3, 0.05, -0.08)), Vector((s_ * 0.05, 0.0, -0.1))]
        outer = [Vector((s_ * 0.3, 0.06, 0.08)), Vector((s_ * 0.56, 0.0, 0.0)), Vector((s_ * 0.5, 0.0, -0.1)), Vector((s_ * 0.3, 0.05, -0.08))]
        for quad, slot in ((inner, S['Sand']), (outer, S['Rock'])):
            nrm = (quad[1] - quad[0]).cross(quad[2] - quad[0]).normalized()
            if nrm.y < 0:
                nrm = -nrm
            g.add((quad, [nrm] * 4, [[0, 1, 2, 3]], [[(0, 0), (1, 0), (1, 1), (0, 1)]]), slot, L['none'], 0.7)
            g.add(([q - nrm * 0.004 for q in quad], [-nrm] * 4, [[0, 3, 2, 1]], [[(0, 0), (0, 1), (1, 1), (1, 0)]]), slot, L['none'], 0.7)
    g.add(xform(rbox((0.16, 0.02, 0.12), 0.008, 1), (0, 0.0, -0.3)), S['Sand'], L['none'], 0.7)
    for name, c, rad in (('GullFly_Pier', (-2.0, 9.0, -28.0), 7.0), ('GullFly_Basin', (46.0, 11.0, -34.0), 9.0), ('GullFly_Village', (-18.0, 13.0, -66.0), 8.0),
                         ('GullFly_THead', (3.0, 7.5, -4.0), 5.5), ('GullFly_Tug', (38.0, 12.0, -18.0), 6.5), ('GullFly_Chapel', (6.0, 26.0, -99.0), 6.0)):
        g.place(name, c, 0.0, radius=rad)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Pass-2 build order, layout verification, export (only rewritten when the content changed) and the manifest additions.
# ---------------------------------------------------------------------------------------------------------------------------------------
def fbx_equivalent(pa, pb):
    # True when two binary FBX files hold the same content apart from the header time stamp and the object UIDs (Blender derives UIDs from
    # Python's per-process randomised hash(), so two exports of the same mesh never match byte for byte).
    import io_scene_fbx.parse_fbx as pf
    try:
        ra, _ = pf.parse(pa)
        rb, _ = pf.parse(pb)
    except Exception:
        return False
    skip = {b'FBXHeaderExtension', b'CreationTime', b'FileId'}
    def eq(a, b):
        if a.id != b.id or a.props_type != b.props_type or len(a.props) != len(b.props) or len(a.elems) != len(b.elems):
            return False
        if a.id in skip:
            return True
        for t, x, y in zip(a.props_type, a.props, b.props):
            if t == ord('L'):
                continue
            if t in (ord('d'), ord('f'), ord('i'), ord('l'), ord('b'), ord('c')):
                if list(x) != list(y):
                    return False
            elif x != y:
                return False
        return all(eq(c, d) for c, d in zip(a.elems, b.elems))
    return eq(ra, rb)

def export_if_changed(ob, path):
    # Export next to the target; keep the existing file when its content is the same (so an unchanged model stays byte-identical in git).
    tmp = path[:-4] + '.tmp.fbx'
    export_fbx(ob, tmp)
    if os.path.exists(path) and fbx_equivalent(path, tmp):
        os.remove(tmp)
        return 'kept'
    os.replace(tmp, path)
    return 'written'

# =======================================================================================================================================
# ROUND 2 (2026-10-16): the coast is no longer one straight beach. Critics (round 1): "the shoreline is one straight diagonal band", "the east
# basin is the same open water", "no lagoon or rocky shelf". Four new landforms, all Shore land in Harbour_Dunes_Col (so the bake sees them
# as land where they stand above the water) and drawn by Harbour_Core_Coast_Tri:
#   WestSpit  - a curved sand spit off the west wall that hooks round the creek mouth: the creek now runs into a small lagoon behind it.
#   EastBar   - a shingle bar bulging out of the beach at the breakwater root: the pier bay becomes a curved cove.
#   Mole      - a rubble mole from the east headland foot, west along z -10, with a green harbour light on its head: with the breakwater it
#               closes the east basin (the tug lies in its mouth). North of every cast lane (the tug's starboard lane ends z -16 there).
#   Shelf     - a flat rock shelf at the foot of the east wall beside the jetty, with tide pools in it (the old cyan pool discs are gone).
# The east basin also gets a shallow sandy floor (visual only, y -1.6..-2.1) under the water: the water shader reads the depth, so the basin
# draws greener and calmer than the open sea. Gameplay depth (SeabedY, CastProbe) is unchanged.
# =======================================================================================================================================
COAST_BARS = [
    # name, centreline, (half width root, tip), (top y root, tip), kind
    # Round 3 (2026-10-17): the west spit grew into the West Horn - a dune hook off the west wall that curls round a calm creek lagoon - with
    # a rock knuckle at its tip, a shingle bar from the beach closing the lagoon's mouth, and a rock point in the middle of the bay. With the
    # breakwater they make the straight beach a bay (critics round 2: "one straight diagonal beach, the same water everywhere").
    ('WestHorn', [(-61.2, -47.5), (-59.8, -39.0), (-58.6, -31.0), (-56.6, -24.4), (-52.6, -19.4), (-47.0, -16.8), (-42.0, -17.4),
                  (-38.6, -19.6)], (2.0, 1.2), (1.35, 0.55), 'sand'),
    ('HornKnuckle', [(-40.2, -18.6), (-37.2, -20.6), (-36.0, -22.8)], (1.5, 1.1), (1.05, 0.7), 'rock'),
    ('LagoonBar', [(-37.2, -46.2), (-38.0, -42.2), (-39.4, -38.6)], (1.7, 0.9), (0.42, 0.2), 'shingle'),
    ('BayPoint', [(-21.0, -46.2), (-21.4, -43.6), (-22.2, -42.0)], (1.6, 1.0), (0.8, 0.5), 'rock'),
    ('EastBar', [(20.6, -45.4), (23.0, -43.9), (25.6, -43.0), (27.9, -43.2)], (1.1, 1.4), (0.4, 0.3), 'shingle'),   # round 2b: out of the x 14 lane
    ('Mole', [(64.5, -13.8), (60.5, -11.7), (55.6, -10.0), (51.2, -9.3), (48.2, -9.7)], (1.7, 1.5), (1.75, 1.75), 'rubble'),
    # round 4 (critics, third round running: "no rock ledge, tide pool or casting shelf at the east cliff foot"): the shelf grew into the East
    # Ledge - a boulder shelf a metre above the water hugging the cliff foot from the beach to a casting point past the jetty's middle (stand 14)
    ('Shelf', [(60.6, -50.2), (60.2, -46.4), (60.0, -43.0), (59.9, -40.0), (59.8, -37.3)], (1.6, 1.25), (0.72, 1.0), 'rock'),
    # round 3: a grassy knoll between the creek mouth and the net sheds (a tree on top; it hides the shelter until you round it)
    ('Knoll', [(-47.6, -57.2), (-45.4, -56.2), (-43.2, -55.4)], (1.6, 1.4), (2.75, 2.55), 'knoll'),
]
COAST_FALL = {'sand': 2.4, 'shingle': 1.8, 'rubble': 5.5, 'rock': 1.2, 'knoll': 3.4}
SHELF_POOLS = [(60.3, -45.0, 0.55), (60.4, -42.2, 0.45), (60.2, -39.9, 0.34)]   # round 4: on the East Ledge's top, by the wall
BASIN_FLOOR = (40.5, 59.5, -44.0, -18.0)     # x0, x1, z0, z1 of the shallow sandy floor
MOLE_LIGHT = (48.6, -9.7)

def poly_nearest(px, pz, pts):
    # (distance, t along the polyline 0..1)
    best, bt = 1e9, 0.0
    lens = [math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1)]
    tot, acc = sum(lens), 0.0
    for i in range(len(pts) - 1):
        d, t = seg_dist(px, pz, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1])
        if d < best:
            best, bt = d, (acc + t * lens[i]) / tot
        acc += lens[i]
    return best, bt

def coast_bar_y(bar, x, z):
    # the bar's surface at (x, z) or None outside its footprint: a soft dome over the centreline falling to the seabed
    name, pts, hw, top, kind = bar
    fall = COAST_FALL[kind]
    d, t = poly_nearest(x, z, pts)
    w = lerp(hw[0], hw[1], t)
    yt = lerp(top[0], top[1], t) + (0.12 * n2(x, z, 0.35, 61) if kind in ('rock', 'rubble') else 0.04 * n2(x, z, 0.5, 62))
    if name == 'WestHorn':
        # round 3: a dune body, not a tube - fatter in the middle of the hook, a wobbling edge, hummocky crest
        arc = math.sin(math.pi * t)
        w *= (1.0 + 0.5 * arc) * (1.0 + 0.16 * n2(x, z, 0.13, 60))
        yt += arc * (0.45 + 0.4 * n2(x, z, 0.09, 59))
    if d > w + fall:
        return None
    k = smoothstep(w + fall, w * 0.35, d)
    base = terrain_y(z)
    if name == 'Shelf':
        for (px, pz, pr) in SHELF_POOLS:
            q = math.hypot((x - px) / pr, (z - pz) / (pr * 0.8))
            yt -= 0.22 * smoothstep(1.25, 0.6, q)
    return base + (yt - base) * (k ** (0.55 if kind in ('rubble', 'rock') else 0.8))

COAST_BOX = [(min(p[0] for p in b[1]) - 1.8 * max(b[2]) - COAST_FALL[b[4]] - 0.5, max(p[0] for p in b[1]) + 1.8 * max(b[2]) + COAST_FALL[b[4]] + 0.5,
              min(p[1] for p in b[1]) - 1.8 * max(b[2]) - COAST_FALL[b[4]] - 0.5, max(p[1] for p in b[1]) + 1.8 * max(b[2]) + COAST_FALL[b[4]] + 0.5)
             for b in COAST_BARS]

def coast_y(x, z):
    # the highest coast landform at (x, z), or None
    best = None
    for bar, (x0, x1, z0, z1) in zip(COAST_BARS, COAST_BOX):
        if x0 <= x <= x1 and z0 <= z <= z1:
            y = coast_bar_y(bar, x, z)
            if y is not None and (best is None or y > best):
                best = y
    return best

def coast_faces(bar, step):
    # height-field quads of one bar where it rises above y -0.45 (the rest stays under water, invisible); returns (V, N, F, UV, Y)
    x0, x1, z0, z1 = COAST_BOX[COAST_BARS.index(bar)]
    xs, zs = frange(x0, x1, step), frange(z0, z1, step)
    def yfn(x, z):
        y = coast_bar_y(bar, x, z)
        return terrain_y(z) - 0.3 if y is None else y
    rocky = True     # round 3: sand and shingle run down to the seabed too (their -0.45 m cut drew a sawtooth edge through the water)
    def keep(i, j):
        ys = [yfn(xs[i + a], zs[j + b]) for a in (0, 1) for b in (0, 1)]
        if rocky:
            # round 2b: the rubble and the rock run down to the seabed (cut at -0.45 m their edge drew a sawtooth step in the water's depth ramp)
            if max(y - terrain_y(zz) for y, zz in zip(ys, (zs[j], zs[j + 1], zs[j], zs[j + 1]))) < -0.05:
                return False
        elif max(ys) < -0.45:
            return False
        if min(zs[j], zs[j + 1]) < COAST_Z - 0.2:
            # on the beach: only where the bar stands above the code beach (no z-fight)
            return max(y - terrain_y(zz) for y, zz in zip(ys, (zs[j], zs[j + 1], zs[j], zs[j + 1]))) > 0.03
        return True
    return hf_geo(xs, zs, yfn, keep, eps=0.1)

def build_coast():
    g = G2('Harbour_Core_Coast_Tri', 'core', tri=True, rays=12, ao_dist=1.5,
           note='Round 2: the west sand spit and creek lagoon, the shingle bar at the breakwater root, the rubble mole closing the east basin '
                '(a green harbour light on its head), the rock shelf with tide pools by the jetty, and the east basin\'s shallow sandy floor '
                '(visual only: the water reads it as shallow). Walkable shape in Harbour_Dunes_Col.')
    r = rng2('coast')
    for bar in COAST_BARS:
        name, pts, hw, top, kind = bar
        V, N, F, UV, _ = coast_faces(bar, 0.7 if kind in ('sand', 'shingle') else 0.9)
        vsl = []
        for v, n in zip(V, N):
            if kind == 'knoll':
                vsl.append(S['Grass'] if n2(v.x, v.z, 0.3, 97) < 0.3 else S['Reed'])
                continue
            if kind in ('sand', 'shingle'):
                # round 3: no wet band (it drew a zigzag on the 0.7 m grid); the water's contact foam and depth tint make the wet edge
                vsl.append(S['Sand'] if kind == 'sand' or n2(v.x, v.z, 0.4, 63) < 0.25 else S['Rock'])
            else:
                if v.y < (0.25 if name in ('Mole', 'Shelf') else -0.05):
                    vsl.append(S['FoliageDark'])
                elif n.y > 0.85 and v.y > 0.55 and n2(v.x, v.z, 0.25, 64) > 0.2:
                    vsl.append(S['Reed'])
                else:
                    vsl.append(S['Rock'] if n2(v.x, v.z, 0.18, 65) < 0.3 else S['Sand'])
        layer = L['Sand'] if kind == 'sand' else (L['Grass'] if kind == 'knoll' else L['Rock'])
        g.add((V, N, F, UV), S['Sand'], layer, 0.6, None, 1.0, vsl)
        # dressing: boulders on the rubble and the rock, pebbles on the shingle
        if kind == 'rubble':
            for k in range(16):
                t = (k + r.uniform(0.0, 0.8)) / 16.0
                L_ = sum(math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1))
                # walk to t
                acc, px, pz, ux, uz = 0.0, pts[0][0], pts[0][1], 1.0, 0.0
                for i in range(len(pts) - 1):
                    sl = math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1])
                    if acc + sl >= t * L_ or i == len(pts) - 2:
                        u = clamp((t * L_ - acc) / sl, 0.0, 1.0)
                        px, pz = lerp(pts[i][0], pts[i + 1][0], u), lerp(pts[i][1], pts[i + 1][1], u)
                        ux, uz = (pts[i + 1][0] - pts[i][0]) / sl, (pts[i + 1][1] - pts[i][1]) / sl
                        break
                    acc += sl
                side = (1 if k % 2 else -1) * r.uniform(1.4, 2.6)
                bx, bz = px - uz * side, pz + ux * side
                rad = r.uniform(1.0, 1.6)
                by = (coast_y(bx, bz) or 0.0) - rad * 0.35
                g.add(xform(cliff_rock(rad, r, (1.1, 0.75, 0.95), 2, r.uniform(0, 360)), (bx, by, bz)), S['Rock'], L['Rock'], AO_ROCK,
                      None, r.uniform(0.85, 1.05))
        elif kind == 'rock' and name == 'Shelf':
            # round 4: the ledge's big boulders are Harbour_Core_EastLedge (Structure colliders); here only the small foot stones in the water
            for k in range(9):
                bx, bz = 57.9 + r.uniform(-0.7, 0.3), -46.4 + k * 1.15 + r.uniform(-0.3, 0.3)
                rad = r.uniform(0.4, 0.75)
                g.add(xform(cliff_rock(rad, r, (1.1, 0.8, 0.9), 2, r.uniform(0, 360)), (bx, -0.4 + rad * 0.2, bz)), S['Rock'], L['Rock'], AO_ROCK,
                      None, r.uniform(0.8, 0.95))
        elif kind == 'rock':
            # round 3: the horn's knuckle and the bay point - stacked boulders along the centreline, bigger and higher at the seaward end
            rk = rng2('coast.' + name)
            n = 7 if name == 'HornKnuckle' else 6
            for k in range(n):
                t = (k + rk.uniform(0.1, 0.9)) / n
                i = min(len(pts) - 2, int(t * (len(pts) - 1)))
                u = t * (len(pts) - 1) - i
                px, pz = lerp(pts[i][0], pts[i + 1][0], u), lerp(pts[i][1], pts[i + 1][1], u)
                side = (1 if k % 2 else -1) * rk.uniform(0.3, 1.1)
                tx_, tz_ = pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]
                tl = math.hypot(tx_, tz_)
                bx, bz = px - tz_ / tl * side, pz + tx_ / tl * side
                rad = rk.uniform(0.7, 1.15) * lerp(0.85, 1.25, t)
                by = (coast_y(bx, bz) or terrain_y(bz)) - rad * 0.45
                g.add(xform(cliff_rock(rad, rk, (1.15, 0.85, 0.95), 2, rk.uniform(0, 360)), (bx, by, bz)), S['Rock'], L['Rock'], AO_ROCK,
                      None, rk.uniform(0.82, 1.02))
        elif kind == 'shingle':
            for k in range(10):
                d_, t_ = r.uniform(-1.2, 1.2), r.uniform(0.05, 0.95)
                i = min(len(pts) - 2, int(t_ * (len(pts) - 1)))
                u = t_ * (len(pts) - 1) - i
                px, pz = lerp(pts[i][0], pts[i + 1][0], u), lerp(pts[i][1], pts[i + 1][1], u)
                bx, bz = px + d_ * 0.5, pz + d_
                y = coast_y(bx, bz)
                if y is None or y < 0.05:
                    continue
                g.add(xform(blob(r.uniform(0.18, 0.4), r, (1.2, 0.6, 1.0), subdiv=1, rough=0.25), (bx, y + 0.04, bz)), S['Rock'], L['Rock'], AO_ROCK)
    # the mole's harbour light: a short white tower, a green lantern (light item: on at dusk)
    mx, mz = MOLE_LIGHT
    my = (coast_y(mx, mz) or 1.6) - 0.1
    add_lathe(g, (mx, my, mz), [(0, 0.55), (0.25, 0.52), (2.4, 0.38), (2.45, 0.46), (2.55, 0.46), (2.6, 0.0)], S2['Snow'], L['none'], 0.6, sides=12, cap1=True)
    add_lathe(g, (mx, my + 2.5, mz), [(0, 0.2), (0.45, 0.22), (0.5, 0.0)], S['Foliage'], L['none'], 0.7, sides=10, cap1=True)
    add_lathe(g, (mx, my + 3.0, mz), [(0, 0.26), (0.12, 0.05), (0.14, 0.0)], S['WoodDark'], L['none'], 0.6, sides=10, cap0=True, cap1=True)
    for k in range(3):
        add_lathe(g, (mx, my + 0.6 + k * 0.6, mz), [(0, 0.5), (0.18, 0.47), (0.2, 0.0)], S['Foliage'], L['none'], 0.6, sides=12)
    g.items.append(dict(name='MoleLight', collider='none', position=[mx, round(my + 2.7, 3), mz], footprint=[1.1, 3.1, 1.1],
                        bounds_min=[mx - 0.55, round(my, 3), mz - 0.55], bounds_max=[mx + 0.55, round(my + 3.1, 3), mz + 0.55],
                        light=[mx, round(my + 2.72, 3), mz], light_colour='green'))
    # the east basin's shallow sandy floor (under the water only; visual)
    x0, x1, z0, z1 = BASIN_FLOOR
    xs, zs = frange(x0, x1, 2.0), frange(z0, z1, 2.0)
    V, N, F, UV, _ = hf_geo(xs, zs, basin_floor_y, lambda i, j: basin_floor_y(xs[i], zs[j]) < -0.3, eps=0.02)
    g.add((V, N, F, UV), S['Sand'], L['Sand'], 0.9, None, 1.0, [S['Sand'] if n2(v.x, v.z, 0.12, 67) < 0.35 else S['Reed'] for v in V])
    # round 3: the creek lagoon's sandy floor behind the West Horn (visual only; gameplay depth stays SeabedY): pale sand with weed patches
    lx0, lx1 = min(p[0] for p in LAGOON) - 3.0, max(p[0] for p in LAGOON) + 3.0
    lz0, lz1 = min(p[1] for p in LAGOON) - 1.0, max(p[1] for p in LAGOON) + 4.0
    xs, zs = frange(lx0, lx1, 1.5), frange(max(lz0, COAST_Z - 0.5), lz1, 1.5)
    def keep(i, j):
        ys = [lagoon_bed_y(xs[i + a], zs[j + b]) - terrain_y(zs[j + b]) for a in (0, 1) for b in (0, 1)]
        top = max(lagoon_bed_y(xs[i + a], zs[j + b]) for a in (0, 1) for b in (0, 1))
        return max(ys) > 0.04 and top < 0.05 and not all((coast_y(xs[i + a], zs[j + b]) or -99.0) > 0.0 for a in (0, 1) for b in (0, 1))
    V, N, F, UV, _ = hf_geo(xs, zs, lagoon_bed_y, keep, eps=0.1)
    g.add((V, N, F, UV), S['Sand'], L['Sand'], 0.9, None, 1.0,
          [S['Reed'] if n2(v.x, v.z, 0.16, 68) > 0.32 else (S['Dock'] if v.y < -1.2 else S['Sand']) for v in V])
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Round 3 (2026-10-17): the water's bed map. The water shader coloured the body by the screen depth under each pixel, so anything solid near the
# surface - the breakwater's boulders and rubble, the tug's hull, piles, the edge of a visual floor - drew as a glowing turquoise ghost or a
# hard crease (critics round 2: "cyan ghost boulders", "a turquoise bowl under the tug", "a hard V seam in the basin"). The harbour water now
# reads the NATURAL bed from this map instead: the code beach and seabed, the coast landforms and the two visual floors, blended with smooth
# maxima and blurred (no creases), at 1 m (x -96..96, z -48..120, the water grid). The screen depth keeps only the contact foam, so rocks and
# hulls get a white waterline and darken the water a little but never glow. G holds a zone tint: 1 in the creek lagoon (greener and lighter),
# 0.55 in the east basin, 0 in the open bay. Written to harbour_bedmap.json; the Unity builder bakes it into T_HarbourBedMap.
# ---------------------------------------------------------------------------------------------------------------------------------------
LAGOON = [(-55.8, -45.2), (-55.8, -38.0), (-54.8, -30.6), (-52.4, -24.4), (-48.2, -20.8), (-42.8, -20.8), (-39.2, -24.4), (-38.6, -31.0),
          (-38.8, -37.4), (-37.8, -45.2)]
BEDMAP = (-96.0, -48.0, 1.0, 193, 169)      # x0, z0, cell, width, height (texel centres)

def smax(a, b, k):
    h = clamp(0.5 + 0.5 * (a - b) / k, 0.0, 1.0)
    return lerp(b, a, h) + k * h * (1.0 - h)

def basin_floor_y(x, z):
    x0, x1, z0, z1 = BASIN_FLOOR
    e = min(x - x0, x1 - x, z - z0 + 10.0, z1 - z) / 10.0
    # round 2b: a long soft rim that dives just under the seabed (round 2's rim ended 1.4 m above it and the water drew the step as a
    # sawtooth; a short steep rim drew hard polygon edges in the depth ramp)
    return max(terrain_y(z) - 0.25, lerp(terrain_y(z) - 0.25, -1.75 - 0.25 * n2(x, z, 0.08, 66), smoothstep(0.0, 1.0, e)))

def lagoon_bed_y(x, z):
    # a shallow sandy bowl: 0.45 m at the rim, about 1.4 m in the middle, falling outside the polygon until the seabed takes over (smooth max)
    d = poly_signed(x, z, LAGOON)
    if d < 0:
        f = -0.45 - 0.95 * smoothstep(0.0, 7.0, -d) + 0.12 * n2(x, z, 0.21, 69)
    else:
        f = -0.45 - 0.5 * d
    return smax(terrain_y(z), f, 1.2)

def bed_y(x, z):
    y = terrain_y(z)
    if -64.0 < x < -30.0 and z < -14.0:
        y = smax(y, lagoon_bed_y(x, z), 0.6)
    if BASIN_FLOOR[0] - 12.0 < x < BASIN_FLOOR[1] + 12.0 and z < BASIN_FLOOR[3] + 12.0:
        y = smax(y, basin_floor_y(x, z), 1.0)
    c = coast_y(x, z) if -66.0 < x < 68.0 and z < -5.0 else None
    if c is not None:
        y = max(y, c)
    if REEF_BOX[0] < x < REEF_BOX[1] and REEF_BOX[2] < z < REEF_BOX[3]:
        y = smax(y, reef_floor_y(x, z), 0.8)       # round 4: the reef's shallow rocks
    return y

def bed_zone(x, z):
    lag = smoothstep(2.5, -3.0, poly_signed(x, z, LAGOON))
    # round 4: the east basin keeps the open water's blue-teal (it shared the lagoon's green; critics: "the two calm zones blur together");
    # the reef off the pier gets a touch of the green over its shallow rocks
    return max(lag, 0.45 * reef_k(x, z))

def build_bedmap():
    x0, z0, cell, w, h = BEDMAP
    Y = [[max(-12.0, min(3.0, bed_y(x0 + i * cell, z0 + j * cell))) for i in range(w)] for j in range(h)]
    Z = [[bed_zone(x0 + i * cell, z0 + j * cell) for i in range(w)] for j in range(h)]
    def blur(M, passes):
        for _ in range(passes):
            M = [[(M[j][max(i - 1, 0)] + 2 * M[j][i] + M[j][min(i + 1, w - 1)]) / 4.0 for i in range(w)] for j in range(h)]
            M = [[(M[max(j - 1, 0)][i] + 2 * M[j][i] + M[min(j + 1, h - 1)][i]) / 4.0 for i in range(w)] for j in range(h)]
        return M
    return blur(Y, 2), blur(Z, 2)

def write_bedmap(path):
    Y, Z = build_bedmap()
    x0, z0, cell, w, h = BEDMAP
    body = json.dumps(dict(note='Round 3 water bed map (fg_harbour.py build_bedmap): the natural bed height (Unity y, cm) and a zone tint (0..255, 255 = '
                                'the creek lagoon) per 1 m texel centre, rows along +z. Baked by HarbourPierBuilder into T_HarbourBedMap for '
                                'M_Water_Harbour.', x0=x0, z0=z0, cell=cell, width=w, height=h,
                           bed_cm=[int(round(v * 100.0)) for row in Y for v in row],
                           zone=[int(round(clamp(v, 0.0, 1.0) * 255.0)) for row in Z for v in row]), separators=(',', ':'))
    old = open(path, encoding='utf-8').read() if os.path.exists(path) else None
    if old == body:
        return 'kept'
    with open(path, 'w', encoding='utf-8', newline=chr(10)) as fh:
        fh.write(body)
    return 'written'

def build_coast_col(g):
    # Round 2: the coast landforms' walkable surface, appended to Harbour_Dunes_Col (Shore MeshCollider): where it stands above the water the
    # bake makes those cells land; under the water the WaterVolume trigger is hit first, so it changes nothing there.
    for bar in COAST_BARS:
        V, N, F, UV, _ = coast_faces(bar, 1.0)
        g.add((V, N, F, UV), S['Grass'], L['Grass'], 1.0)
    # round 4: the creek glen's banks in front of z -59.5 (behind it the dunes' collision carries them), inside the walls
    xs = [x for x in frange(-60.5, GLEN_X1, 0.5)]
    zs = grid_axis(-59.5, -49.0, 0.5)
    Y = [[glen_surface(x, z) - terrain_y(z) for x in xs] for z in zs]
    keep = lambda i, j: max(Y[j][i], Y[j + 1][i], Y[j][i + 1], Y[j + 1][i + 1]) > 0.05
    V, N, F, UV, _ = hf_geo(xs, zs, glen_surface, keep, eps=0.1)
    g.add((V, N, F, UV), S['Grass'], L['Grass'], 1.0)

# =======================================================================================================================================
# ROUND 2 (2026-10-16): story staging (critics: "nothing on the map says the crew is on the run"; story 4.5-5.5). Spec R76: the harbour is a
# stop on the run downriver from the mafia; the tug is leaving at dusk. Harbour_Core_Story (Village, Structure box colliders):
#   - the tug is mid-loading: a timber derrick on the breakwater swings a netted crate load over the tug's after deck (the load sways),
#     crate stacks and barrels wait by the gangway, an A-frame board repeats LUCKY BREAK / DEPARTS AT DUSK where the crew boards;
#   - a poster post at the pier root: two WANTED posters of the crew and a torn crest flyer (the mafia's mark), GONE FISHING;
#   - a fingerpost at the creek mouth: DOWNRIVER (the exit, west), HARBOUR, CHAPEL;
#   - a second black sedan on the hill track by the south wall; both sedans and the coast-road car have their headlights on at dusk.
# Detail (no colliders, hidden on Low): ducks on the creek lagoon, crabs on the wet sand, a cat on the stair parapet and one on the crates,
# a flapping washing line on the upper lane (poles in Story).
# =======================================================================================================================================
DERRICK = (31.25, -31.2)                 # mast foot on the breakwater top (east edge); the boom reaches over the tug's after deck
DERRICK_TIP = (40.4, 7.0, -24.4)      # round 2b: follows the tug (over its after deck)
CARGO_HOOK = (40.4, 6.85, -24.4)         # the swinging load hangs from here (local model origin = the hook)
LOAD_CRATES = [(31.35, -33.9), (30.95, -35.6)]
A_FRAME = (29.2, -34.2, 35.0)            # x, z, yaw (it faces the tug and the east)
POSTER_POST = (-1.4, -64.6)
FINGERPOST = (-52.2, -54.2)            # round 2b: moved 2 m out of the creek shelter's footprint
HILL_SEDAN2 = (22.4, -110.2, 196.0)
LAUNDRY_POLES = [(-50.2, -78.6), (-44.0, -78.4)]   # round 3: behind the two west cottages (the lane moved)
DUCKS = [(-50.5, -34.5, 30.0), (-49.2, -33.6, 200.0), (-48.4, -35.0, 110.0), (52.0, -27.5, 300.0), (-44.6, -28.4, 250.0), (-45.6, -29.5, 70.0)]   # round 3: on the creek lagoon
STORY_TOPS = {}
CRABS = [(-21.0, -45.6, 40.0), (9.5, -45.4, 200.0), (42.0, -45.7, 120.0), (-38.5, -45.5, 300.0)]

def crate_stack_at(g, x, y, z, r, name, n=3, barrel=True):
    g.mark()
    h = y
    for k in range(n):
        s_ = r.uniform(0.55, 0.66) * (1.0 - 0.08 * k)
        c = (x + r.uniform(-0.06, 0.06), h + s_ / 2, z + r.uniform(-0.06, 0.06))
        rot = ry(r.uniform(-14, 14))
        g.add(xform(rbox((s_, s_, s_), 0.035), c, rot), S['Wood'] if k % 2 == 0 else S['Dock'], L['Wood'], AO_WOOD)
        for dy in (-1, 1):
            g.add(xform(rbox((s_ + 0.01, 0.05, s_ + 0.01), 0.015, 1), Vector(c) + Vector((0, dy * (s_ / 2 - 0.03), 0)), rot), S['WoodDark'], L['Wood'], 0.45)
        # a stencilled fish on the side (a dark blob)
        g.add(xform(blob(0.09, r, (1.8, 0.8, 0.12), subdiv=1, rough=0.02, flat=-2), Vector(c) + rot @ Vector((0, 0, s_ / 2 + 0.006))), S['WoodDark'], L['none'], 0.6)
        h += s_
    if barrel:
        bx, bz = x - 0.2, z + 0.68
        add_lathe(g, (bx, y, bz), [(0.0, 0.215), (0.02, 0.235), (0.2, 0.26), (0.39, 0.272), (0.58, 0.26), (0.76, 0.235), (0.78, 0.215)],
                  S['WoodDark'], L['Wood'], 0.45, sides=12, cap0=True, cap1=True)
        for hh, rr in ((0.12, 0.252), (0.66, 0.252)):
            add_lathe(g, (bx, y, bz), [(hh - 0.035, rr - 0.004), (hh - 0.028, rr + 0.008), (hh + 0.028, rr + 0.008), (hh + 0.035, rr - 0.004)],
                      S['Rock'], L['none'], 0.5, sides=12)
    g.solid(name)
    return h

def derrick(g, r):
    x, z = DERRICK
    y = BREAKWATER_TOP
    g.mark()
    add_box(g, (x, y + 0.15, z), (1.0, 0.3, 1.0), 0.05, S['Rock'], L['Rock'], 0.55)
    add_lathe(g, (x, y + 0.3, z), [(0, 0.16), (6.6, 0.12), (6.7, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=8, cap1=True)
    # back-stays to the breakwater
    for dx, dz in ((-1.4, -1.6), (-1.6, 0.9)):
        g.add(tube([Vector((x, y + 6.4, z)), Vector((x + dx, y + 0.1, z + dz))], 0.025, 4), S['WoodDark'], L['none'], 0.5)
    # the winch drum and its crank
    add_lathe(g, (x - 0.45, y + 0.75, z), [(-0.3, 0.16), (-0.28, 0.2), (0.28, 0.2), (0.3, 0.16)], S['Rock'], L['none'], 0.5, sides=10, rot=rz(90), cap0=True, cap1=True)
    add_box(g, (x - 0.45, y + 0.45, z), (0.7, 0.5, 0.12), 0.03, S['WoodDark'], L['Wood'], 0.5)
    it = g.solid('Derrick', collider='box')
    # the boom from the mast's foot collar to the tip over the tug (visual; high above the walk and the deck)
    foot = Vector((x + 0.15, y + 1.6, z + 0.1))
    tip = Vector(DERRICK_TIP)
    g.add(tube([foot, foot.lerp(tip, 0.5), tip], 0.11, 6), S['Wood'], L['Wood'], 0.5)
    g.add(tube([Vector((x, y + 6.7, z)), tip], 0.018, 4), S['WoodDark'], L['none'], 0.5)                  # topping lift
    g.add(tube([tip, Vector(CARGO_HOOK) + Vector((0, 0.15, 0))], 0.02, 4), S['WoodDark'], L['none'], 0.5)   # whip block
    add_lathe(g, tip - Vector((0, 0.1, 0)), [(-0.12, 0.12), (0.12, 0.12)], S['Rock'], L['none'], 0.5, sides=8, rot=rx(90), cap0=True, cap1=True)
    it['bounds_note'] = 'mast, winch and plinth only (the boom and its lines stay above head height)'

def a_frame_board(g, x, z, yaw, y, gd=None):
    gd = gd or g
    rot = ry(yaw)
    g.mark()
    for s_ in (-1, 1):
        g.add(xform(rbox((0.9, 1.2, 0.05), 0.02, 1), Vector((x, y + 0.58, z)) + rot @ Vector((0, 0, s_ * 0.22)), rot @ rx(s_ * 11)), S['WoodDark'], L['Wood'], 0.5)
    face = Vector((x, y + 0.66, z)) + rot @ Vector((0, 0, 0.27))
    fr = rot @ rx(11)
    g.add(xform(rbox((0.78, 1.0, 0.02), 0.01, 1), face, fr), S['FoliageDark'], L['none'], 0.6, tint=0.55)
    sign_text(gd, 'LUCKY', 0.12, face + fr @ Vector((0, 0.3, 0.012)), fr, S2['Snow'])
    sign_text(gd, 'BREAK', 0.12, face + fr @ Vector((0, 0.14, 0.012)), fr, S2['Snow'])
    sign_text(gd, 'LEAVES', 0.08, face + fr @ Vector((0, -0.07, 0.012)), fr, S['Accent'])
    sign_text(gd, 'TONIGHT', 0.08, face + fr @ Vector((0, -0.19, 0.012)), fr, S['Accent'])
    g.solid('LoadingBoard')

def crest_flyer(g, c, rot, r):
    # the mafia's mark: a black flyer with a gold fish skeleton crest, torn at one corner
    c = Vector(c)
    g.add(xform(rbox((0.5, 0.66, 0.01), 0.004), c, rot @ rz(-4)), S['WoodDark'], L['Cloth'], 0.6, tint=0.45)
    sp = c + rot @ Vector((0, 0.08, 0.008))
    g.add(xform(rbox((0.26, 0.025, 0.01), 0.004, 1), sp, rot), S['Accent'], L['none'], 0.7)
    for k in range(4):
        g.add(xform(rbox((0.02, 0.1 - 0.015 * abs(k - 1.5), 0.01), 0.004, 1), sp + rot @ Vector((-0.08 + k * 0.055, 0, 0.0)), rot), S['Accent'], L['none'], 0.7)
    g.add(xform(blob(0.05, r, (1.0, 1.0, 0.15), subdiv=1, rough=0.02, flat=-2), sp + rot @ Vector((0.15, 0, 0.0))), S['Accent'], L['none'], 0.7)
    sign_text(g, 'YOU OWE US', 0.05, c + rot @ Vector((0, -0.18, 0.01)), rot @ rz(-4), S['Accent'])

def poster_post(g, r, gd=None):
    gd = gd or g
    x, z = POSTER_POST
    y = land_y(x, z)
    g.mark()
    add_lathe(g, (x, y - 0.1, z), [(0, 0.22), (2.5, 0.2), (2.55, 0.0)], S['Wood'], L['Wood'], 0.5, sides=10, cap1=True)
    add_lathe(g, (x, y + 2.5, z), [(0, 0.34), (0.08, 0.36), (0.35, 0.0)], S['Roof'], L['none'], 0.55, sides=10, cap0=True)
    for a, kind in ((0.0, 'wanted'), (70.0, 'flyer'), (-70.0, 'wanted'), (180.0, 'gone')):
        rot = ry(a)
        c = Vector((x, y + 1.45, z)) + rot @ Vector((0, 0, 0.215))
        if kind == 'wanted':
            wanted_poster(gd, c, rot, r)
        elif kind == 'flyer':
            crest_flyer(gd, c, rot, r)
        else:
            gd.add(xform(rbox((0.5, 0.36, 0.01), 0.004), c, rot @ rz(2)), S2['Snow'], L['Cloth'], 0.7)
            sign_text(gd, 'GONE', 0.08, c + rot @ Vector((0, 0.07, 0.008)), rot @ rz(2), S['WoodDark'])
            sign_text(gd, 'FISHING', 0.07, c + rot @ Vector((0, -0.06, 0.008)), rot @ rz(2), S['WoodDark'])
    g.solid('PosterPost')

def fingerpost(g, r, gd=None):
    gd = gd or g
    x, z = FINGERPOST
    y = land_y(x, z)
    g.mark()
    add_lathe(g, (x, y - 0.1, z), [(0, 0.09), (2.4, 0.08), (2.45, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=7, cap1=True)
    for k, (word, yaw, slot) in enumerate((('DOWNRIVER', -90.0, S['Accent']), ('HARBOUR', 75.0, S2['Snow']), ('CHAPEL', 160.0, S2['Snow']))):
        rot = ry(yaw)
        hy = y + 2.05 - k * 0.32
        L_ = 0.16 + 0.085 * len(word)
        board = Vector((x, hy, z)) + rot @ Vector((0, 0, L_ / 2 + 0.05))
        g.add(xform(rbox((0.05, 0.22, L_), 0.02, 1), board, rot), S['Wood'] if k else S['Roof'], L['Wood'], 0.5)
        g.add(xform(rbox((0.05, 0.16, 0.16), 0.02, 1), board + rot @ Vector((0, 0, L_ / 2 + 0.03)), rot @ rx(45)), S['Wood'] if k else S['Roof'], L['Wood'], 0.5)
        for side in (1, -1):
            sign_text(gd, word, 0.11, board + rot @ Vector((side * 0.027, -0.005, 0.0)), rot @ ry(side * 90), slot)
    g.solid('Fingerpost')

def car_lights(g, x, z, yaw, name):
    # two headlight lamps (manifest light items: on at dusk) in front of a parked car
    rot = ry(yaw)
    y0 = land_y(x, z)
    p = Vector((x, y0, z)) + rot @ Vector((0, 0.85, 2.7))
    g.items.append(dict(name=name, collider='none', position=[round(p.x, 3), round(p.y, 3), round(p.z, 3)], footprint=[0.4, 0.4, 0.4],
                        bounds_min=[round(p.x - 0.2, 3), round(p.y - 0.2, 3), round(p.z - 0.2, 3)],
                        bounds_max=[round(p.x + 0.2, 3), round(p.y + 0.2, 3), round(p.z + 0.2, 3)], light=[round(p.x, 3), round(p.y, 3), round(p.z, 3)]))

CREEK_SHELTER = (-53.0, -56.0, 90.0)   # x, z, yaw: round 2b by the creek mouth; round 4: on the glen's broad east bank top, over the creek

def creek_shelter(g, r):
    # Round 2b (critics: "the creek mouth is a vast empty meadow with nothing to walk toward"): an open timber shelter on the sand by the
    # creek mouth - four posts, a shingled gable roof, a picnic table with benches, a rod rack and a lantern (a dusk light). Posts, table
    # and benches are Structure boxes; the roof stays above head height (no collider).
    x, z, yaw = CREEK_SHELTER
    rot = ry(yaw)
    y = min(land_y(x + dx, z + dz) for dx in (-2, 2) for dz in (-1.6, 1.6))
    P = lambda a, b, c: Vector((x, y, z)) + rot @ Vector((a, b, c))
    W, D, H = 3.6, 2.8, 2.35
    for k, (sx, sz) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
        g.mark()
        add_lathe(g, P(sx * W / 2, -0.25, sz * D / 2), [(0, 0.11), (H + 0.25, 0.09), (H + 0.3, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=7, cap1=True)
        g.solid('ShelterPost_%d' % k)
    # roof (no collider): beams, two shingled slabs, a ridge
    for sz in (-1, 1):
        g.add(xform(rbox((W + 0.3, 0.14, 0.14), 0.03, 1), P(0, H, sz * D / 2)), S['WoodDark'], L['Wood'], 0.45)
    for sz in (-1, 1):
        g.add(xform(rbox((W + 0.7, 0.1, D / 2 + 0.55), 0.03, 1), P(0, H + 0.45, sz * (D / 4 + 0.22)), rot @ rx(sz * 28.0)), S['Roof'], L['Wood'], 0.55)
    g.add(xform(rbox((W + 0.8, 0.12, 0.16), 0.03, 1), P(0, H + 0.82, 0)), S['WoodDark'], L['Wood'], 0.45)
    g.mark()
    # the picnic table and its benches
    g.mark()
    add_box(g, P(0, 0.74, 0), (1.8, 0.07, 0.8), 0.02, S['Wood'], L['Wood'], 0.5, rot=rot)
    for sx in (-0.7, 0.7):
        add_box(g, P(sx, 0.36, 0), (0.08, 0.72, 0.7), 0.02, S['WoodDark'], L['Wood'], 0.45, rot=rot)
    g.solid('PicnicTable')
    for sz in (-0.75, 0.75):
        g.mark()
        add_box(g, P(0, 0.44, sz), (1.8, 0.06, 0.3), 0.02, S['Wood'], L['Wood'], 0.5, rot=rot)
        for sx in (-0.7, 0.7):
            add_box(g, P(sx, 0.21, sz), (0.07, 0.42, 0.26), 0.02, S['WoodDark'], L['Wood'], 0.45, rot=rot)
        g.solid('PicnicBench_%d' % int(sz > 0))
    # on the table: a bait tin, a flask, a folded map (orange)
    add_lathe(g, P(-0.4, 0.78, 0.1), [(0, 0.08), (0.08, 0.08), (0.09, 0.0)], S['Rock'], L['none'], 0.55, sides=8, cap1=True)
    add_lathe(g, P(0.35, 0.78, -0.12), [(0, 0.05), (0.22, 0.05), (0.25, 0.03), (0.27, 0.0)], S['Foliage'], L['none'], 0.55, sides=8, cap1=True)
    g.add(xform(rbox((0.32, 0.01, 0.24), 0.004, 1), P(0.05, 0.78, 0.15), rot @ ry(12)), S['Accent'], L['none'], 0.6)
    # a rod rack against the back posts with three rods
    g.mark()
    add_box(g, P(0, 0.5, -D / 2 - 0.15), (1.6, 0.06, 0.1), 0.02, S['WoodDark'], L['Wood'], 0.45, rot=rot)
    for k in range(3):
        g.add(tube([P(-0.5 + 0.5 * k, 0.05, -D / 2 - 0.1), P(-0.45 + 0.5 * k, 2.6, -D / 2 - 0.32)], 0.014, 4), S['WoodDark'], L['none'], 0.5)
        add_lathe(g, P(-0.5 + 0.5 * k + 0.03, 0.5, -D / 2 - 0.12), [(-0.04, 0.05), (0.04, 0.05)], S['Accent'], L['none'], 0.55, sides=6,
                  rot=rot @ rx(90), cap0=True, cap1=True)
    g.solid('RodRack')
    # the lantern under the ridge (a dusk light)
    lp = P(0.9, H + 0.3, 0)
    g.add(tube([lp + Vector((0, 0.5, 0)), lp + Vector((0, 0.18, 0))], 0.01, 3), S['WoodDark'], L['none'], 0.5)
    add_lathe(g, lp, [(-0.16, 0.0), (-0.15, 0.08), (0.1, 0.08), (0.12, 0.05), (0.18, 0.0)], S['Accent'], L['none'], 0.7, sides=6, cap0=True)
    g.items.append(dict(name='ShelterLantern', collider='none', position=[round(lp.x, 3), round(lp.y, 3), round(lp.z, 3)], footprint=[0.3, 0.4, 0.3],
                        bounds_min=[round(lp.x - 0.15, 3), round(lp.y - 0.16, 3), round(lp.z - 0.15, 3)],
                        bounds_max=[round(lp.x + 0.15, 3), round(lp.y + 0.2, 3), round(lp.z + 0.15, 3)], light=[round(lp.x, 3), round(lp.y, 3), round(lp.z, 3)]))
    g.mark()

# Round 2b: a market corner on the upper lane where stairs A arrive (critics: "no square, no stalls, the houses stand 15 m apart"): two
# awning stalls (fish and produce) either side of the stair path and a stone well; Structure boxes.
# round 3: the fish and produce stalls face into the village square from its north edge; the nets stall stays by stair D's head
MARKET_STALLS = [(-7.6, -83.4, 12.0, 'fish'), (-2.4, -82.8, -4.0, 'produce'), (30.2, -82.6, 4.0, 'nets')]
WELL = (1.4, -91.6)

def market_stall(g, x, z, yaw, kind, r):
    rot = ry(yaw)
    y = land_y(x, z)
    P = lambda a, b, c: Vector((x, y, z)) + rot @ Vector((a, b, c))
    W, D = 2.6, 1.3
    g.mark()
    add_box(g, P(0, 0.45, 0), (W, 0.9, D), 0.04, S['Wood'], L['Wood'], 0.5, rot=rot)
    add_box(g, P(0, 0.93, 0.05), (W + 0.1, 0.07, D + 0.1), 0.02, S['WoodDark'], L['Wood'], 0.45, rot=rot)
    g.solid('Stall_' + kind)
    for sx in (-1, 1):
        for sz in (-1, 1):
            add_lathe(g, P(sx * (W / 2 - 0.05), 0.9, sz * (D / 2 + 0.25)), [(0, 0.04), (1.35 if sz < 0 else 1.1, 0.035), (1.4 if sz < 0 else 1.15, 0.0)],
                      S['WoodDark'], L['Wood'], 0.45, sides=6, cap1=True)
    # a striped awning sloping toward the lane (front = -z local: the lane side)
    stripes = 7
    for k in range(stripes):
        sx = -W / 2 - 0.1 + (W + 0.2) * (k + 0.5) / stripes
        g.add(xform(rbox(((W + 0.2) / stripes + 0.005, 0.03, D + 1.0), 0.01, 1), P(sx, 2.13, -0.05), rot @ rx(-12)),
              S['Accent'] if k % 2 == 0 else S2['Snow'], L['Cloth'], 0.6)
    # wares
    if kind == 'fish':
        for k in range(3):
            add_box(g, P(-0.85 + 0.85 * k, 1.05, 0.1), (0.7, 0.2, 0.5), 0.03, S['Wood'], L['Wood'], 0.5, rot=rot)
            for f in range(3):
                g.add(xform(blob(0.09, r, (2.2, 0.7, 0.9), subdiv=1, rough=0.05), P(-1.0 + 0.85 * k + 0.15 * f, 1.2, 0.1 + 0.1 * (f - 1)), rot @ ry(r.uniform(-25, 25))),
                      [S2['Snow'], S['Rock'], S['Accent']][(k + f) % 3], L['none'], 0.65)
        sign_text(g, 'FRESH', 0.16, P(0, 1.55, D / 2 + 0.28), rot @ ry(180), S['WoodDark'])
    elif kind == 'produce':
        for k in range(4):
            add_box(g, P(-0.95 + 0.63 * k, 1.03, 0.0), (0.5, 0.16, 0.6), 0.03, S['WoodDark'], L['Wood'], 0.5, rot=rot)
            col = [S['Roof'], S['Foliage'], S['Accent'], S['Reed']][k]
            for f in range(5):
                g.add(xform(blob(0.07, r, (1, 0.9, 1), subdiv=1, rough=0.15), P(-1.1 + 0.63 * k + 0.07 * f, 1.16, -0.15 + 0.07 * (f % 3))), col, L['none'], 0.65)
    else:
        for k in range(3):
            add_lathe(g, P(-0.8 + 0.8 * k, 0.96, 0.0), [(0, 0.22), (0.18, 0.25), (0.3, 0.0)], [S['Accent'], S['Roof'], S['Sand']][k], L['Cloth'], 0.6, sides=8, cap0=True)
    g.mark()

def market_well(g, r):
    x, z = WELL
    y = land_y(x, z)
    g.mark()
    add_lathe(g, (x, y - 0.05, z), [(0, 0.82), (0.75, 0.8), (0.8, 0.86), (0.86, 0.6), (0.82, 0.0)], S['Rock'], L['Rock'], 0.55, sides=14, cap1=True)
    g.add(xform(blob(0.6, r, (1.0, 0.05, 1.0), subdiv=2, rough=0.0, flat=-2), (x, y + 0.6, z)), S['FoliageDark'], L['none'], 0.4, tint=0.4)
    g.solid('Well')
    for s_ in (-1, 1):
        add_box(g, (x + s_ * 0.85, y + 1.2, z), (0.12, 1.6, 0.12), 0.02, S['WoodDark'], L['Wood'], 0.45)
    add_lathe(g, (x - 0.8, y + 1.75, z), [(0, 0.08), (1.6, 0.08)], S['Wood'], L['Wood'], 0.5, sides=8, rot=rz(-90), cap0=True, cap1=True)
    for s_ in (-1, 1):
        g.add(xform(rbox((2.1, 0.07, 0.95), 0.02, 1), (x, y + 2.25, z + s_ * 0.38), rx(s_ * 32)), S['Roof'], L['Wood'], 0.55)
    add_lathe(g, (x + 0.25, y + 1.2, z), [(0, 0.12), (0.26, 0.14), (0.28, 0.0)], S['Wood'], L['Wood'], 0.5, sides=8, cap0=True)
    g.add(tube([Vector((x + 0.25, y + 1.48, z)), Vector((x + 0.25, y + 1.72, z))], 0.008, 3), S['Sand'], L['Cloth'], 0.5)
    g.mark()

def build_story():
    g = G2('Harbour_Core_Story', 'core', rays=10, ao_dist=0.8,
           note='Round 2: story staging (R76) - the derrick loading the tug, crate stacks and the LUCKY BREAK board by the gangway, the WANTED '
                'poster post at the pier root, the DOWNRIVER fingerpost at the creek mouth, a second black sedan on the hill track, washing-line '
                'poles. Box items -> Structure colliders; light items (collider none) -> lamps at dusk (car headlights).')
    gd = G2('Harbour_Detail_StorySigns', 'detail', rays=0,
            note='Round 2: the lettering and paper of the story props (WANTED posters, the crest flyer, GONE FISHING, the fingerpost words, the '
                 'loading board). No colliders; Low keeps the blank boards (Harbour_Core_Story).')
    r = rng2('story')
    derrick(g, r)
    for k, (x, z) in enumerate(LOAD_CRATES):
        STORY_TOPS[k] = crate_stack_at(g, x, BREAKWATER_TOP, z, r, 'LoadCrates_%d' % k, n=3 if k == 0 else 2)
    # the breakwater: a low sea-wall parapet along its east edge (the walk reads 3.4 m, not a 6 m slab) and two fishing notches on the west
    # side (a bench and two bollards each)
    parapet(g, 31.7, -54.6, 31.7, -37.2, BREAKWATER_TOP, r, h=0.55, d=0.5, name='BreakwaterParapet')
    for k, z in enumerate((-47.5, -40.5)):
        bench(g, 'BreakwaterBench_%d' % k, 28.75, z, -90.0, r, y=BREAKWATER_TOP)
        for dz in (-1.6, 1.6):
            g.mark()
            add_lathe(g, (28.45, BREAKWATER_TOP, z + dz), [(0, 0.17), (0.32, 0.15), (0.38, 0.2), (0.46, 0.2), (0.5, 0.0)], S['WoodDark'], L['none'], 0.5,
                      sides=10, cap1=True)
            g.solid('BreakwaterBollard_%d_%d' % (k, int(dz > 0)))
    ax, az, ayaw = A_FRAME
    a_frame_board(g, ax, az, ayaw, BREAKWATER_TOP, gd)
    poster_post(g, r, gd)
    fingerpost(g, r, gd)
    creek_shelter(g, r)      # round 2b
    for (mx, mz, myaw, kind) in MARKET_STALLS:
        market_stall(g, mx, mz, myaw, kind, r)
    market_well(g, r)
    for k, (x, z) in enumerate(LAUNDRY_POLES):
        g.mark()
        y = land_y(x, z)
        add_lathe(g, (x, y - 0.1, z), [(0, 0.05), (2.3, 0.045), (2.32, 0.0)], S['WoodDark'], L['Wood'], 0.5, sides=6, cap1=True)
        g.add(tube([Vector((x - 0.25, y + 2.15, z)), Vector((x + 0.25, y + 2.15, z))], 0.025, 4), S['WoodDark'], L['none'], 0.5)
        g.solid('LaundryPole_%d' % k)
    # the second sedan (outside the walls: no collider) and the headlights of all three cars
    black_car(g, *HILL_SEDAN2)
    for k, (x, z, yaw) in enumerate((HILL_SEDAN, HILL_SEDAN2, BLACK_CAR)):
        car_lights(g, x, z, yaw, 'Headlights_%d' % k)
    # round 3 (critics: "nothing shows the chase at dusk"): a searchlight on the roof of the sedan at the top of the hill track; at dusk its
    # beam sweeps slowly over the town (the builder adds the beam: manifest pass2.light_points 'Searchlight', yaw toward the harbour)
    # round 3: a net-drying frame at the east jetty's root (the loop's east destination), a net draped over it
    top = drying_rack(g, 50.6, -53.6, 'JettyNetRack')
    for k in range(9):
        t = k / 8.0
        x = top.x - 1.35 + 2.7 * t
        sag = 0.25 * math.sin(math.pi * t)
        g.add(tube([Vector((x, top.y + 0.02, top.z)), Vector((x, top.y - 0.9 - sag, top.z + 0.18)), Vector((x + 0.05, top.y - 1.5 - sag, top.z + 0.3))], 0.02, 3),
              S['Reed'] if k % 2 else S['Sand'], L['Cloth'], 0.6)
    for k in range(5):
        y = top.y - 0.3 * k - 0.1
        g.add(tube([Vector((top.x - 1.35, y, top.z + 0.06 * k)), Vector((top.x + 1.35, y - 0.06, top.z + 0.06 * k))], 0.012, 3, caps=False), S['Sand'], L['Cloth'], 0.6)
    # round 3: big WANTED posters at head height - the bait shop's east wall, the harbour office's west wall, the smokehouse front, stair A's
    # head - so the chase reads from every part of the village
    bx, bz, bw, bd = BAIT_SHOP
    big_wanted(g, (bx + bw / 2 + 0.03, PLATEAU_Y + 1.75, bz - 0.4), ry(90), r)
    ox, oz, ow, od = DOCK_OFFICE
    big_wanted(g, (ox - ow / 2 - 0.03, PLATEAU_Y + 1.75, oz - 0.2), ry(-90), r)
    mx, mz, mw, md = SMOKEHOUSE
    big_wanted(g, (mx + 0.55, PLATEAU_Y + 1.7, mz + md / 2 + 0.03), ry(0), r, scale=1.3)
    sx, sz, _ = HILL_SEDAN2
    sy = land_y(sx, sz) + 1.42
    add_lathe(g, (sx, sy, sz), [(0, 0.06), (0.2, 0.06), (0.22, 0.0)], S['WoodDark'], L['none'], 0.5, sides=6, cap1=True)
    add_lathe(g, (sx, sy + 0.32, sz), [(-0.2, 0.0), (-0.19, 0.2), (0.2, 0.22), (0.22, 0.0)], S['Rock'], L['none'], 0.5, sides=10, rot=rx(80) @ ry(0), cap0=True)
    g.extra.setdefault('light_points', []).append(dict(name='Searchlight', position=[round(sx, 2), round(sy + 0.32, 2), round(sz, 2)],
                                                       yaw_deg=-19.0, animHint='sweep at dusk', frame='world'))
    return [g, gd]

def build_story_anim():
    out = []
    # the netted crate load swinging under the derrick (origin = the hook)
    g = G2('Harbour_Detail_Anim_CargoNet', 'detail', local=True, rays=8, anim='sway',
           note='Round 2: a netted load of fish crates hanging from the derrick hook over the tug (origin = the hook). Sways.')
    r = rng2('cargonet')
    g.add(tube([Vector((0, 0, 0)), Vector((0, -1.3, 0))], 0.02, 4), S['WoodDark'], L['none'], 0.5)
    for k, (dx, dy, dz) in enumerate(((-0.33, -2.05, 0.0), (0.33, -2.05, 0.05), (0.0, -1.5, 0.0))):
        g.add(xform(rbox((0.6, 0.55, 0.6), 0.04), (dx, dy, dz), ry(r.uniform(-12, 12))), S['Wood'] if k % 2 == 0 else S['Dock'], L['Wood'], AO_WOOD)
    for a in range(4):
        d = Vector((math.cos(a * math.pi / 2 + 0.785), 0, math.sin(a * math.pi / 2 + 0.785))) * 0.78
        g.add(tube([Vector((0, -1.3, 0)), Vector((0, -1.25, 0)) + d + Vector((0, -1.0, 0)), d * 0.8 + Vector((0, -2.4, 0))], 0.015, 3), S['Sand'], L['Cloth'], 0.6)
    g.place('CargoNet', CARGO_HOOK, 0.0)
    out.append(g)
    # ducks
    g = G2('Harbour_Detail_Anim_Duck', 'detail', local=True, rays=8, anim='bob',
           note='Round 2: a toon duck (0.35 m) afloat. Origin at the waterline. Bobs.')
    r = rng2('duck')
    g.add(xform(blob(0.16, r, (0.85, 0.6, 1.25), subdiv=2, rough=0.04, flat=-0.5), (0, 0.06, 0)), S['WoodDark'], L['none'], 0.65)
    g.add(xform(blob(0.08, r, (0.9, 1.0, 1.0), subdiv=2, rough=0.03, flat=-2), (0, 0.24, 0.15)), S['FoliageDark'], L['none'], 0.65)
    add_lathe(g, (0, 0.23, 0.22), [(0, 0.035), (0.08, 0.03), (0.09, 0.0)], S['Accent'], L['none'], 0.7, sides=6, rot=rx(90))
    g.add(xform(blob(0.07, r, (1, 0.5, 1.2), subdiv=1, rough=0.02, flat=-2), (0, 0.12, -0.2), rx(-20)), S['WoodDark'], L['none'], 0.6)
    for k, (x, z, yaw) in enumerate(DUCKS):
        g.place('Duck_%d' % k, (x, 0.03 if z > COAST_Z else land_y(x, z) + 0.03, z), yaw)
    out.append(g)
    # crabs (looking around, an occasional hop: the perched-gull motion)
    g = G2('Harbour_Detail_Anim_Crab', 'detail', local=True, rays=8, anim='gull',
           note='Round 2: a red shore crab (0.3 m across) on the wet sand. Origin between its feet. Looks about, hops.')
    r = rng2('crab')
    g.add(xform(blob(0.11, r, (1.3, 0.45, 1.0), subdiv=2, rough=0.04, flat=-0.6), (0, 0.08, 0)), S['Roof'], L['none'], 0.6)
    for s_ in (-1, 1):
        for k in range(3):
            g.add(tube([Vector((s_ * 0.1, 0.07, -0.05 + k * 0.05)), Vector((s_ * 0.2, 0.09, -0.07 + k * 0.06)), Vector((s_ * 0.25, 0.0, -0.08 + k * 0.07))], 0.012, 3), S['Roof'], L['none'], 0.6)
        g.add(xform(blob(0.05, r, (1.3, 0.8, 1.0), subdiv=1, rough=0.03, flat=-2), (s_ * 0.13, 0.12, 0.12)), S['Accent'], L['none'], 0.6)
        g.add(xform(blob(0.02, r, (1, 1, 1), subdiv=1, rough=0.0, flat=-2), (s_ * 0.04, 0.16, 0.08)), S['WoodDark'], L['none'], 0.6, tint=0.4)
    for k, (x, z, yaw) in enumerate(CRABS):
        g.place('Crab_%d' % k, (x, land_y(x, z), z), yaw)
    out.append(g)
    # cats (a sitting cat that looks about)
    g = G2('Harbour_Detail_Anim_Cat', 'detail', local=True, rays=8, anim='gull',
           note='Round 2: a sitting ginger cat (0.4 m). Origin between its feet. Looks about (the perched-gull motion).')
    r = rng2('cat')
    g.add(xform(blob(0.14, r, (0.8, 1.15, 1.0), subdiv=2, rough=0.03, flat=-0.7), (0, 0.15, -0.03)), S['Accent'], L['none'], 0.6)
    g.add(xform(blob(0.095, r, (1.05, 0.95, 0.95), subdiv=2, rough=0.03, flat=-2), (0, 0.36, 0.05)), S['Accent'], L['none'], 0.6)
    for s_ in (-1, 1):
        g.add(([Vector((s_ * 0.03, 0.42, 0.04)), Vector((s_ * 0.085, 0.42, 0.03)), Vector((s_ * 0.065, 0.5, 0.04))], [Vector((0, 0, 1))] * 3,
               [[0, 1, 2]], [[(0, 0), (1, 0), (0.5, 1)]]), S['Accent'], L['none'], 0.6)
        g.add(([Vector((s_ * 0.03, 0.42, 0.04)), Vector((s_ * 0.065, 0.5, 0.04)), Vector((s_ * 0.085, 0.42, 0.03))], [Vector((0, 0, -1))] * 3,
               [[0, 1, 2]], [[(0, 0), (1, 0), (0.5, 1)]]), S['Accent'], L['none'], 0.6)
        g.add(xform(blob(0.016, r, (1, 1.3, 1), subdiv=1, rough=0.0, flat=-2), (s_ * 0.035, 0.38, 0.13)), S['WoodDark'], L['none'], 0.6, tint=0.4)
    g.add(tube([Vector((0, 0.04, -0.14)), Vector((0.12, 0.05, -0.18)), Vector((0.18, 0.12, -0.08))], 0.025, 5), S['Accent'], L['none'], 0.6)
    stair_x = [x for name, x, w, run in RAMPS if name == 'A'][0]
    g.place('Cat_Stairs', (stair_x - RAMP_HW - 0.28, LEVEL1_Y - 0.04 + 0.42, T1_Z - 1.2), 160.0)
    g.place('Cat_Crates', (LOAD_CRATES[0][0], STORY_TOPS.get(0, BREAKWATER_TOP + 1.6), LOAD_CRATES[0][1]), 250.0)
    out.append(g)
    # the washing line between the two poles (sways)
    (ax, az), (bx, bz) = LAUNDRY_POLES
    ya, yb = land_y(ax, az) + 2.15, land_y(bx, bz) + 2.15
    a, b = Vector((ax, ya, az)), Vector((bx, yb, bz))
    mid = (a + b) / 2
    g = G2('Harbour_Detail_Anim_Laundry', 'detail', local=True, rays=0, anim='sway',
           note='Round 2: a washing line of sheets and shirts on the upper lane (origin = the line midpoint). Sways about the line.')
    pts = rope_line(a - mid, b - mid, 0.18, n=12)
    g.add(tube(pts, 0.008, 3, caps=False), S['Sand'], L['Cloth'], 0.7)
    cols = [S2['Snow'], S['Accent'], S2['Snow'], S['Reed'], S['Roof'], S2['Snow']]
    d = (b - a).normalized()
    for k in range(6):
        t = (k + 0.5) / 6
        p = (a - mid).lerp(b - mid, t) - Vector((0, 0.18 * 4 * t * (1 - t), 0))
        w, h = (0.75, 0.9) if k % 2 == 0 else (0.5, 0.55)
        V, N, F, UV = cloth_panel(w, h, 3, 3, 0.04, phase=k)
        nn = d.cross(Vector((0, 1, 0))).normalized()
        V = [p + d * (v.x - w / 2) + Vector((0, v.y, 0)) + nn * v.z for v in V]
        N = [(d * n.x + Vector((0, n.y, 0)) + nn * n.z).normalized() for n in N]
        g.add((V, N, F, UV), cols[k], L['Cloth'], 0.75)
    g.place('Laundry', mid, 0.0)
    out.append(g)
    return out

# =======================================================================================================================================
# ROUND 2b (2026-10-16): ground cover (user unlock "GPU-instanced grass, flowers"). Critics: "the ground the player walks on is a flat, saturated
# green carpet with sparse spikes". Not a mesh: scatter points written to harbour_groundcover.json (Art/Export/Harbour, copied next to the
# manifest); Unity's HarbourGroundCover builds one small crossed-card tuft mesh per kind (T_CrownCards grass / flower cells, M_Toon_Foliage with
# GPU instancing) and the GroundCover component draws them instanced under the Detail root (hidden on Low, no colliders).
# Density: a jittered 0.55 m grid over the lawns inside the walls, thinned by a patchy meadow mask, thicker along wall feet and path verges;
# kinds by low-frequency noise (two grass greens, an olive/straw grass, white, orange and red flower patches); sparse dune grass on the
# beach. Nothing on paths, ramps, the creek, the terrace risers, building or prop footprints, trunks, or steep ground.
# =======================================================================================================================================
GROUNDCOVER = {}
GC_KINDS = ('grass', 'grass_dry', 'flower_white', 'flower_orange', 'flower_red', 'dune')

def build_groundcover(obst):
    r = rng2('groundcover')
    pts = {k: [] for k in GC_KINDS}
    step = 0.55
    for z in frange(-105.6, -46.0, step):
        for x in frange(-59.6, 59.6, step):
            jx, jz = x + r.uniform(-0.24, 0.24), z + r.uniform(-0.24, 0.24)
            roll = r.random(); yaw = r.uniform(0, 2 * math.pi); sc = r.uniform(0.75, 1.3)
            beach = jz > -55.2
            if not plant_ok(jx, jz, obst, 0.2, allow_beach=beach):
                continue
            if road_dist(jx, jz) < 2.3:      # the upper lane (the road inside the walls)
                continue
            if in_ramp(jx, jz, 0.5) or abs(jz - T1_Z) < 0.75 or abs(jz - T2_Z) < 0.75 or poly_dist(jx, jz, CREEK) < CREEK_HW + 0.35:
                continue
            y = land_y(jx, jz)
            if max(abs(land_y(jx + 0.3, jz) - y), abs(land_y(jx, jz + 0.3) - y)) > 0.3:
                continue
            if beach:
                if jz > -48.5 or roll > 0.07 + 0.1 * max(0.0, n2(jx, jz, 0.15, 72)):
                    continue
                pts['dune'].append((jx, y - 0.03, jz, yaw, sc * 1.15))
                continue
            # verges: thicker within ~1.2 m of a path or at the foot of a retaining wall
            verge = 0.0
            for pp, hw in PATHS:
                dd = poly_dist(jx, jz, pp) - hw
                if dd < 1.2:
                    verge = max(verge, 1.0 - dd / 1.2)
            if 0.0 < (jz - (T1_Z + RISER_HALF)) < 1.4 or 0.0 < (jz - (T2_Z + RISER_HALF)) < 1.4:
                verge = max(verge, 0.8)
            meadow = 0.5 + 0.5 * n2(jx, jz, 0.07, 73) + 0.25 * n2(jx, jz, 0.21, 74)
            if roll > clamp(0.22 + 0.55 * meadow + 0.45 * verge, 0.0, 0.95):
                continue
            fl = n2(jx, jz, 0.16, 75)
            if fl > 0.3 and r.random() < 0.7:
                # one colour per ~8 m patch cell (white daisies most often)
                kind = ('flower_white', 'flower_orange', 'flower_white', 'flower_red')[(int(math.floor(jx / 8.0)) * 7 + int(math.floor(jz / 8.0)) * 13) % 4]
            else:
                kind = 'grass_dry' if n2(jx, jz, 0.11, 77) > 0.22 else 'grass'
            pts[kind].append((jx, y - 0.03, jz, yaw, sc))
    # round 3: dry grass on the cliff ledges (cliff_sweep)
    rt = rng2('groundcover.cliffs')
    for (x, y, z) in CLIFF_TUFTS:
        pts['grass_dry'].append((x, y - 0.03, z, rt.uniform(0, 2 * math.pi), rt.uniform(0.9, 1.4)))
    # round 3: marram on the West Horn's dune crest (thick on the crest, thinning down the slopes, none on the wet sand or by the stands)
    rh = rng2('groundcover.horn')
    for z in frange(-50.0, -16.0, 0.6):
        for x in frange(-66.0, -34.0, 0.6):
            jx, jz = x + rh.uniform(-0.25, 0.25), z + rh.uniform(-0.25, 0.25)
            roll, yaw, sc = rh.random(), rh.uniform(0, 2 * math.pi), rh.uniform(0.8, 1.35)
            cy = coast_y(jx, jz)
            if cy is None or cy < 0.45 or (jz < COAST_Z and cy < terrain_y(jz) + 0.15):
                continue
            if any(math.hypot(jx - sx, jz - sz) < 1.6 for sx, _, sz, _ in STAND_POINTS + STAND_POINTS_R1):
                continue
            if roll > clamp(0.12 + 0.6 * smoothstep(0.45, 1.3, cy) + 0.25 * n2(jx, jz, 0.18, 79), 0.0, 0.85):
                continue
            pts['dune'].append((jx, cy - 0.03, jz, yaw, sc * 1.15))
    GROUNDCOVER.clear()
    GROUNDCOVER.update({k: [[round(v, 3) for v in q] for q in pts[k]] for k in GC_KINDS})
    return {k: len(v) for k, v in pts.items()}

def write_groundcover(path):
    body = json.dumps(dict(note='Round 2b ground cover scatter (fg_harbour.py build_groundcover): per kind, [x, y, z, yaw radians, scale] '
                                'per instance, Unity world space. Drawn by GroundCover (Detail; hidden on Low; no colliders).',
                           kinds=[dict(name=k, points=[v for q in GROUNDCOVER[k] for v in q]) for k in GC_KINDS]), separators=(',', ':'))
    old = open(path, encoding='utf-8').read() if os.path.exists(path) else None
    if old == body:
        return 'kept'
    with open(path, 'w', encoding='utf-8', newline=chr(10)) as fh:
        fh.write(body)
    return 'written'

def build_pass2():
    land = [build_dunes(), build_dunes_col(), build_coast()] + build_hills() + [build_river()] + build_cliffs() + \
           [build_paths()] + build_stone_wall() + [build_road()] + build_forest() + [build_islands(), build_backdrop()]
    bld = build_buildings()
    upper_all = build_upper_village()  # round 1 (round 3: three models)
    upper = upper_all[0]
    jetty = build_jetty()              # round 1
    yard = build_yard()
    tug = [build_crew_boat(), build_crew_boat_col(), build_ice_chest()]
    trees = build_trees()
    obst = obstacle_boxes([bld, jetty] + upper_all + yard + trees)
    detail = [build_lights(yard[0]), build_clutter(yard[0], yard[1])] + build_plants(obst) + build_shore(obst) + [build_reeds()] + \
             build_anim_models(yard, bld, jetty, upper) + [build_moorings()] + build_terrace_dressing(obst) + [build_gull_fly()]
    story = build_story() + build_story_anim()          # round 2
    lagoon = build_lagoon() + [build_ivy(), build_breakwater_setts()]   # round 3
    lagoon += build_round4()                                              # round 4
    gc_counts = build_groundcover(obst + obstacle_boxes([story[0], lagoon[0]]))    # round 2b (no mesh: harbour_groundcover.json)
    land[0].extra['groundcover_counts'] = gc_counts
    return land + [bld] + upper_all + [jetty] + yard + tug + trees + detail + build_code_trees() + [build_windmill_sails()] + story + lagoon

def tug_footprint_world(margin=0.35):
    # the tug's hull outline (plus the fender ring / tyres) in world xz
    tx, _, tz = TUG_WORLD['position']
    rot = ry(TUG_WORLD['yaw'])
    pts = []
    for x, z in tug_outline(0.5):
        o = Vector((x, 0.0, z))
        o2 = o + Vector((math.copysign(margin, x), 0.0, 0.0))
        p = rot @ o2
        pts.append((tx + p.x, tz + p.z))
    pts.append((tx, tz + TUG_L1 + 0.5))
    pts.append((tx, tz + TUG_L0 - 0.3))
    return pts

def lane_clearance(x, z):
    # signed clearance (m) from the nearest stand circle / cast-lane wedge (positive outside): the margin at which lane_hit flips
    lo, hi = -12.0, 12.0
    if not lane_hit(x, z, hi):
        return hi
    for _ in range(30):
        mid = (lo + hi) / 2
        if lane_hit(x, z, mid):
            hi = mid
        else:
            lo = mid
    return hi

def verify_layout(groups):
    # Every in-water / shoreline object: its clearance from the stand circles and cast lanes. The tug's hull is sampled densely.
    rep = []
    fp = tug_footprint_world()
    tx, _, tz = TUG_WORLD['position']
    samples = list(fp)
    for x in frange(tx - TUG_B / 2, tx + TUG_B / 2, 0.5):
        for z in frange(tz + TUG_L0, tz + TUG_L1, 0.5):
            if point_in_poly(x, z, fp[:-2]):
                samples.append((x, z))
    c = min(lane_clearance(x, z) for x, z in samples)
    rep.append(dict(name='CrewBoat (tug hull + fenders)', min_clearance_m=round(c, 2), solid=True))
    gx0, gx1 = BREAKWATER[1] - 0.05, tx - tug_w(TUG_GANGWAY_Z) + 0.35
    c = min(lane_clearance(x, GANGWAY_Z + dz) for x in frange(gx0, gx1, 0.25) for dz in (-0.5, 0.0, 0.5))
    rep.append(dict(name='Gangway', min_clearance_m=round(c, 2), solid=True))
    for g in groups:
        for p in g.placements:
            if g.name.endswith('Buoy'):
                x, _, z = p['position']
                rep.append(dict(name=p['name'], min_clearance_m=round(lane_clearance(x, z) - 0.3, 2), solid=False))
    # round 1: the jetty and the lookout tower (solid, Structure) against every lane, and the new lanes against the breakwater walk
    def area(x0, x1, z0, z1):
        return [(x, z) for x in frange(x0, x1, 0.5) for z in frange(z0, z1, 0.5)]
    for name, pts in (('Jetty (deck and posts)', area(JETTY[0], JETTY[1], JETTY[2], JETTY[3])),
                      ('Lookout tower and stair', area(LOOKOUT[0], LOOKOUT[1], LOOKOUT_STAIR[2], LOOKOUT[3]))):
        own = [sp for sp in STAND_POINTS_R1 if (JETTY[0] - 0.1 <= sp[0] <= JETTY[1] + 0.1) == name.startswith('Jetty')]
        rest = [sp for sp in STAND_POINTS_R1 if sp not in own]
        saved = STAND_POINTS_R1[:]
        STAND_POINTS_R1[:] = rest
        c = min(lane_clearance(x, z) for x, z in pts)
        STAND_POINTS_R1[:] = saved
        rep.append(dict(name=name + ' vs the other lanes', min_clearance_m=round(c, 2), solid=True))
    # round 2b: the coast landforms (spit, bar, mole, shelf) wherever they stand above the water, and the mole light
    for bar, (x0, x1, z0, z1) in zip(COAST_BARS, COAST_BOX):
        pts = [(x, z) for x in frange(x0, x1, 0.5) for z in frange(z0, z1, 0.5) if (coast_bar_y(bar, x, z) or -9.0) > -0.3]
        if pts:
            # round 3: a landform is checked against the OTHER stands' lanes (the West Horn carries stand 13: its own circle and lane start on it)
            own = [sp for sp in STAND_POINTS + STAND_POINTS_R1 if (coast_bar_y(bar, sp[0], sp[2]) or -9.0) > 0.0]
            saved = (STAND_POINTS[:], STAND_POINTS_R1[:])
            STAND_POINTS[:] = [sp for sp in STAND_POINTS if sp not in own]
            STAND_POINTS_R1[:] = [sp for sp in STAND_POINTS_R1 if sp not in own]
            c = min(lane_clearance(x, z) for x, z in pts)
            STAND_POINTS[:], STAND_POINTS_R1[:] = saved
            rep.append(dict(name='Coast ' + bar[0] + ' (above the water)' + (' vs the other lanes' if own else ''), min_clearance_m=round(c, 2), solid=True))
    rep.append(dict(name='MoleLight', min_clearance_m=round(lane_clearance(*MOLE_LIGHT) - 0.55, 2), solid=True))
    # round 4: the reef (visual only, but kept past every lane: a bobber never lands in a rock) and the East Ledge's boulders (Structure)
    pts = [(sx + rad * 1.2 * math.cos(a), sz + rad * 1.2 * math.sin(a)) for sx, sz, rad, _ in REEF_STACKS for a in [k * math.pi / 6 for k in range(12)]]
    pts += [(REEF_WRECK[0] + dx, REEF_WRECK[1] + dz) for dx in frange(-4.5, 4.5, 1.5) for dz in frange(-4.5, 4.5, 1.5)]
    rep.append(dict(name='Reef stacks and wreck', min_clearance_m=round(min(lane_clearance(x, z) for x, z in pts), 2), solid=False))
    saved = STAND_POINTS_R1[:]
    STAND_POINTS_R1[:] = [sp for sp in STAND_POINTS_R1 if sp[0] != LEDGE_STAND[0]]
    c = min(lane_clearance(bx + rad * 1.15 * math.cos(a), bz + rad * 1.15 * math.sin(a)) for bx, bz, rad in LEDGE_BOULDERS
            for a in [k * math.pi / 4 for k in range(8)])
    STAND_POINTS_R1[:] = saved
    rep.append(dict(name='East Ledge boulders vs the other lanes', min_clearance_m=round(c, 2), solid=True))
    shore = [g for g in groups if g.name == 'Harbour_Detail_Shore'][0]
    worst = min((lane_clearance(it['x'], it['z']) for it in shore.extra['shore_items']), default=99)
    rep.append(dict(name='Seaweed and driftwood (%d clumps)' % len(shore.extra['shore_items']), min_clearance_m=round(worst, 2), solid=False))
    return rep

def p2_group_entry(g, ob_file):
    lo, hi = u_bounds(g)
    layers_used = sorted(set(l for _, l in g.SL)); slots_used = sorted(set(s for s, _ in g.SL))
    if g.kind == 'col':
        material = 'none (collision only: MeshCollider, no renderer)'
    elif g.material:
        material = g.material
    else:
        material = 'M_Toon_Tri' if g.tri else 'M_Toon_Base'
    d = dict(name=g.name, file='Art/Export/Harbour/' + g.name + '.fbx', group=g.kind, triangles=g.tris(), vertices=len(g.V),
             bounds_min=lo, bounds_max=hi, triplanar=g.tri, material=material, slots=slots_used if g.kind != 'col' else [],
             layers=layers_used if g.kind != 'col' else [], local_space=g.local, items=g.items)
    if g.local:
        d['placements'] = g.placements
    if g.anim:
        d['animHint'] = g.anim
    if g.note:
        d['note'] = g.note
    for k, v in g.extra.items():
        d[k] = v
    return d

def pass2_main(manifest, p1groups, export=True, bake=True):
    for name in P2_COLLECTIONS.values():
        clear_collection(name)
    clear_collection('Preview_Instances')
    groups = build_pass2()
    for g in groups:
        t = g.tris()
        if t > MAX_TRIS:
            raise RuntimeError('%s has %d triangles (limit %d)' % (g.name, t, MAX_TRIS))
        if len(g.V) >= 65535:
            raise RuntimeError('%s has %d vertices (16-bit limit)' % (g.name, len(g.V)))
    objs = [make_object2(g) for g in groups]
    if bake:
        world = [g for g in groups if not g.local and g.kind != 'col' and g.material != CARD_MATERIAL]   # round 2b: cards are no occluders
        ground = ground_occluder(-130.0, 130.0, -120.0, 40.0, 2.0)
        core = [g for g in world if g.kind == 'core']
        raws = bake_ao2(groups, list(p1groups) + world, ground, core_occluders=list(p1groups) + core)
        for ob, g, raw in zip(objs, groups, raws):
            if g.kind != 'col':
                write_ao2(ob, g, raw)
    pm = preview_material()
    wm = bpy.data.materials.get('FG_Preview_Water')
    for ob, g in zip(objs, groups):
        if g.kind != 'col' and not ob.data.materials:
            if g.material == CARD_MATERIAL:
                ob.data.materials.append(toon_card_material())       # round 2b: preview of the alpha-clipped crown cards
            else:
                ob.data.materials.append(wm if (g.material and g.material.startswith('M_Water') and wm) else pm)
    preview_scene2()
    place_preview_instances(groups, objs)
    toon_preview()      # preview-only toon materials and sky for the build-log renders (never exported)
    status = {}
    entries = []
    for ob, g in zip(objs, groups):
        path = os.path.join(EXPORT_DIR, g.name + '.fbx')
        if export:
            # export_fbx strips materials and selects only this object; local models are at the origin already
            status[g.name] = export_if_changed(ob, path)
        entries.append(p2_group_entry(g, path))
    manifest['fbx'] += entries
    if export:
        status['harbour_groundcover.json'] = write_groundcover(os.path.join(EXPORT_DIR, 'harbour_groundcover.json'))
        status['harbour_bedmap.json'] = write_bedmap(os.path.join(EXPORT_DIR, 'harbour_bedmap.json'))   # round 3
    tot = {k: sum(e['triangles'] for e in entries if e['group'] == k) for k in ('core', 'detail', 'col')}
    p1 = manifest['total_triangles']
    inst = 0
    for e in entries:
        if e.get('placements') and e['group'] != 'col':
            inst += e['triangles'] * (len(e['placements']) - 1)
    manifest['pass2'] = dict(
        date='2026-10-14', branch='harbour-art2',
        summary='Detail pass: dunes, paths, stone wall, headlands, river mouth, coast road; bait shop, net shed, smokehouse, harbour office; '
                'yard props and clutter; plants and shoreline; lighthouse, islands, stacks, far boats; the crew tugboat (R76).',
        groups=dict(core='always drawn; every pass-2 collider belongs to a core model', detail='no colliders; hide on the Low preset',
                    col='collision meshes only (MeshCollider, no renderer)'),
        triangles=dict(phase1=p1, pass2_core=tot['core'], pass2_detail=tot['detail'], pass2_col_not_drawn=tot['col'],
                       pass2_extra_from_instances=inst),
        notes=PASS2_NOTES,
        verification=verify_layout(groups),
        smoke_points=[sp for g in groups for sp in g.extra.get('smoke_points', [])],
        light_points=[lp for g in groups for lp in g.extra.get('light_points', [])],
    )
    print('PASS2', tot, 'instances', inst, 'status', status)
    return groups, objs, status

PASS2_NOTES = [
    'Placement: every pass-2 FBX object is at the origin with an identity transform. World models: place at the Unity origin. Local models '
    '(local_space true: the tug, its collision mesh, the ice chest, the Anim_* pieces): instantiate once per placement at position with '
    'yaw_deg about +y. Ice chest placements are in the CrewBoat local frame (parent them to the placed tug).',
    'Materials: "_Tri" -> M_Toon_Tri (the HarbourModelPostprocessor uv1.z fix applies), others M_Toon_Base, Harbour_Core_River -> the water '
    'material (no palette slot fits water; it also holds the tide-pool discs). Col models: MeshCollider only, no renderer.',
    'Low preset: hide every group "detail" model. No detail model carries a collider, so Low changes nothing in gameplay.',
    'Colliders: box items -> BoxCollider from bounds_min/max (Structure layer like the phase-1 props, except the stone wall and gate pillars: '
    'Shore). Walkable land: Harbour_Dunes_Col (MeshCollider, Shore layer, inside the walls only). The tug: Harbour_Core_CrewBoat_Col '
    '(MeshCollider on the placed tug; walkable decks, deckhouse interior, upper deck, wheelhouse). Tug ladders: LadderVolume boxes from '
    'Harbour_Core_CrewBoat.ladders (local frame). Ice chests: a BoxCollider each (size in the placement).',
    'Code trees: the 16 HarbourPierBuilder trees still stand on the plateau (y 1.2): the dunes are flattened within ~1.3 m of each trunk and '
    'ramp up from 4.6 m, so no tree floats or sinks. Pass-2 buildings and props keep >= 2.5 m from each trunk (positions ported from Pcg32). '
    'Their visuals are the four local Harbour_Core_CodeTree_* models (round 0): one placement per Tree_NN, parented under it so its '
    'localScale (the Pcg32 height) scales the tree and the trunk and canopy colliders stay as they were.',
    'Backdrop (hills, cliffs, forest, islands, road, backdrop, river): no colliders; outside the walls; invisible to the bake.',
    'Beach foam: the code beach/water already draws the foam line and wet edge at the coast; no extra foam mesh was added.',
    'The in-water solid objects are the tug (hull box or its MeshCollider) and the gangway; everything else in the water is visual only '
    '(floats, seaweed, gull on a buoy, river and pool water). See verification for their clearance from the stand circles and cast lanes.',
    'No add-on output and no downloaded asset is exported (assets_used stays empty): trees (layered branch clumps), rock masses, dunes and '
    'hills are procedural in fg_harbour.py, so a rebuild from an empty file with --factory-startup needs no add-on and stays deterministic.',
    'The .blend carries preview-only toon materials (FG_Preview_Toon*, palette from ArtPalette.asset Summer) and a built-in sky for the '
    'build-log renders; export_fbx strips every material, so none reaches the FBX.',
]

# ---------------------------------------------------------------------------------------------------------------------------------------
# Pass-2 Blender objects, AO, preview, export and manifest.
# ---------------------------------------------------------------------------------------------------------------------------------------
P2_COLLECTIONS = {'core': 'Harbour_Core', 'detail': 'Harbour_Detail', 'col': 'Harbour_Col'}

def clear_collection(name):
    col = bpy.data.collections.get(name)
    if not col:
        return
    for o in list(col.objects):
        me = o.data if o.type == 'MESH' else None
        bpy.data.objects.remove(o, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)

def make_object2(g):
    ob = make_object(g)
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    collection(P2_COLLECTIONS[g.kind]).objects.link(ob)
    if g.kind == 'col':
        me = ob.data
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        while me.color_attributes:
            me.color_attributes.remove(me.color_attributes[0])
        ob.display_type = 'WIRE'
        ob.hide_render = True
    return ob

def ground_occluder(x0, x1, z0, z1, step):
    # (verts, polys) of the pass-2 ground (land_y) for the AO bake
    xs = frange(x0, x1, step); zs = frange(z0, z1, step)
    verts = [(x, land_y(x, z), z) for z in zs for x in xs]
    nx = len(xs)
    polys = []
    for j in range(len(zs) - 1):
        for i in range(nx - 1):
            a = j * nx + i
            polys.append((a, a + nx, a + nx + 1, a + 1))
    return verts, polys

def bake_ao2(targets, occluders, ground=None, core_occluders=None):
    # The phase-1 bake (bake_ao) with a pass-2 occluder set: every group in `occluders` (phase-1 and pass-2 world groups) plus the pass-2
    # ground; per-target rays and distance. Local models bake against themselves only. Returns one raw-ao list per target.
    from mathutils.bvhtree import BVHTree
    def bvh_of(groups, extra=None):
        verts, polys = [], []
        for g in groups:
            base = len(verts)
            verts += [tuple(v) for v in g.V]
            polys += [tuple(base + k for k in f) for f in g.F]
        if extra:
            base = len(verts)
            verts += extra[0]
            polys += [tuple(base + k for k in p) for p in extra[1]]
        return BVHTree.FromPolygons(verts, polys, all_triangles=False)
    world = bvh_of(occluders, ground)
    # core models bake against phase 1 + core only (Low hides the detail: no dark spots under missing bushes); detail against everything
    core_world = bvh_of(core_occluders, ground) if core_occluders is not None else world
    out = []
    for g in targets:
        if g.rays <= 0 or g.kind == 'col':
            out.append(list(g.VAO) if g.VAO else [1.0] * len(g.V))
            continue
        bvh = bvh_of([g]) if g.local else (core_world if g.kind == 'core' else world)
        dirs = hemisphere_dirs(g.rays)
        dist = g.ao_dist
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

def write_ao2(ob, g, raw):
    # write_ao plus the per-face tint (a darkening factor for the black car and the mafia boat)
    me = ob.data
    ca = me.color_attributes['Col']
    n = len(me.loops)
    out = [0.0] * (4 * n)
    for pi, poly in enumerate(me.polygons):
        lo = g.AO[pi]
        tint = g.TINT[pi]
        for li in poly.loop_indices:
            a = raw[me.loops[li].vertex_index]
            ao = (lo + (1.0 - lo) * max(0.0, min(1.0, a))) * tint
            out[4 * li:4 * li + 4] = (0.5 * ao, 0.5 * ao, 0.5 * ao, 1.0)
    ca.data.foreach_set('color', out)

def show2(groups, bake=False, occluders=()):
    objs = [make_object2(g) for g in groups]
    if bake:
        world = [g for g in groups if not g.local and g.kind != 'col' and g.material != CARD_MATERIAL]   # round 2b: cards are no occluders
        ground = ground_occluder(-130.0, 130.0, -120.0, 40.0, 2.0)
        raws = bake_ao2(groups, list(occluders) + world, ground)
        for ob, g, raw in zip(objs, groups, raws):
            if g.kind != 'col':
                write_ao2(ob, g, raw)
    pm = preview_material()
    wm = bpy.data.materials.get('FG_Preview_Water')
    for ob, g in zip(objs, groups):
        if g.kind != 'col' and not ob.data.materials:
            if g.material == CARD_MATERIAL:
                ob.data.materials.append(toon_card_material())       # round 2b: preview of the alpha-clipped crown cards
            else:
                ob.data.materials.append(wm if (g.material and g.material.startswith('M_Water') and wm) else pm)
    preview_scene2()
    place_preview_instances(groups, objs)
    return objs

def place_preview_instances(groups, objs):
    # Local models: the exported object stays at the origin; the Blender preview shows linked copies at every placement.
    pcol = collection('Preview_Instances')
    for o in list(pcol.objects):
        if any(o.name.startswith('FGI_' + g.name + '_') for g in groups):
            bpy.data.objects.remove(o, do_unlink=True)
    for ob, g in zip(objs, groups):
        if not g.local:
            continue
        ob.hide_set(True) if bpy.context.view_layer.objects.get(ob.name) else None
        ob.hide_render = True
        for i, p in enumerate(g.placements):
            inst = bpy.data.objects.new('FGI_%s_%02d' % (g.name, i), ob.data)
            pcol.objects.link(inst)
            pos = Vector(p['position'])
            if p.get('frame') == 'CrewBoat local':
                pos = Vector(TUG_WORLD['position']) + ry(TUG_WORLD['yaw']) @ pos
            inst.location = to_blender(pos)
            if g.kind == 'col':
                inst.display_type = 'WIRE'
                inst.hide_render = True
            # Unity yaw about +y (clockwise from above) -> Blender rotation about +z
            inst.rotation_euler = (0.0, 0.0, math.radians(-p.get('yaw_deg', 0.0)))
            if 'height_scale' in p:
                inst.scale = (p['height_scale'],) * 3

def terrain_proxy2():
    # preview only: the code beach + seabed over the full code extent (x +-120, z -100..120)
    name = 'FG_Preview_Terrain2'
    old = bpy.data.objects.get(name)
    if old:
        return old
    xs = [x * 3.0 for x in range(-40, 41)]
    zs = sorted(set([z * 1.0 for z in range(-100, -40)] + [z * 3.0 for z in range(-13, 41)]))
    verts = [tuple(to_blender((x, terrain_y(z), z))) for z in zs for x in xs]
    nx = len(xs)
    faces = [(j * nx + i, j * nx + i + nx, j * nx + i + nx + 1, j * nx + i + 1) for j in range(len(zs) - 1) for i in range(nx - 1)]
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); collection('Preview').objects.link(ob)
    if ob.data.polygons[0].normal.z < 0:
        me.flip_normals()
    return ob

def preview_scene2():
    cam = preview_scene()
    old = bpy.data.objects.get('FG_Preview_Terrain')
    if old:
        old.hide_render = True; old.hide_viewport = True
    t2 = terrain_proxy2()
    tm = bpy.data.materials.get('FG_Preview_Ground')
    if tm and not t2.data.materials:
        t2.data.materials.append(tm)
    w = bpy.data.objects.get('FG_Preview_Water')
    if w and not w.get('fg_p2'):
        me = w.data
        for v, co in zip(me.vertices, [(-120, -120, 0), (120, -120, 0), (120, 45, 0), (-120, 45, 0)]):
            v.co = co
        w['fg_p2'] = 1
    cam.data.clip_end = 900
    return cam

# ---------------------------------------------------------------------------------------------------------------------------------------
# Toon preview look (preview only, never exported: export_fbx strips every material). It approximates the Unity FishGame/Toon shader for
# the build-log screenshots:
#   - palette slot from the "Layer" uv x (the Summer palette of ArtPalette.asset) and baked AO from "Col";
#   - a soft 3-step light ramp, warm light and cool shade, a rim light, gentle ambient and a faint painted value breakup;
#   - a blue-green depth gradient for the water;
#   - Blender's built-in sky (no downloaded HDRI).
# ---------------------------------------------------------------------------------------------------------------------------------------
TOON_LIGHT, TOON_SHADE = (1.0, 0.96, 0.88), (0.46, 0.50, 0.62)

def _toon_lighting(nt, base_socket, out_node):
    N = nt.nodes; Lk = nt.links.new
    dif = N.new('ShaderNodeBsdfDiffuse'); dif.inputs['Color'].default_value = (1, 1, 1, 1)
    s2r = N.new('ShaderNodeShaderToRGB'); Lk(dif.outputs[0], s2r.inputs[0])
    bw = N.new('ShaderNodeRGBToBW'); Lk(s2r.outputs['Color'], bw.inputs[0])
    ramp = N.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'EASE'
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.04, (0.0, 0.0, 0.0, 1)
    els[1].position, els[1].color = 0.16, (0.55, 0.55, 0.55, 1)
    e = els.new(0.3); e.color = (1.0, 1.0, 1.0, 1)
    Lk(bw.outputs[0], ramp.inputs['Fac'])
    tint = N.new('ShaderNodeMix'); tint.data_type = 'RGBA'
    tint.inputs['A'].default_value = srgb_to_lin(TOON_SHADE) + (1,)
    tint.inputs['B'].default_value = srgb_to_lin(TOON_LIGHT) + (1,)
    Lk(ramp.outputs['Color'], tint.inputs['Factor'])
    lit = N.new('ShaderNodeMix'); lit.data_type = 'RGBA'; lit.blend_type = 'MULTIPLY'; lit.inputs['Factor'].default_value = 1.0
    Lk(base_socket, lit.inputs['A']); Lk(tint.outputs['Result'], lit.inputs['B'])
    lw = N.new('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = 0.45
    rr = N.new('ShaderNodeMapRange'); rr.inputs['From Min'].default_value = 0.62; rr.inputs['From Max'].default_value = 0.9
    Lk(lw.outputs['Facing'], rr.inputs['Value'])
    rim = N.new('ShaderNodeMath'); rim.operation = 'MULTIPLY'; rim.inputs[1].default_value = 0.22
    Lk(rr.outputs['Result'], rim.inputs[0])
    add = N.new('ShaderNodeMix'); add.data_type = 'RGBA'; add.blend_type = 'ADD'
    add.inputs['B'].default_value = (1.0, 0.97, 0.9, 1)
    Lk(rim.outputs[0], add.inputs['Factor']); Lk(lit.outputs['Result'], add.inputs['A'])
    em = N.new('ShaderNodeEmission'); Lk(add.outputs['Result'], em.inputs['Color'])
    Lk(em.outputs[0], out_node.inputs['Surface'])

def _fresh_material(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL':
            nt.nodes.remove(n)
    return m, nt, next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')

def _value_breakup(nt, colour_socket, scale):
    N = nt.nodes; Lk = nt.links.new
    tex = N.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value = scale; tex.inputs['Detail'].default_value = 3.0
    geo = N.new('ShaderNodeNewGeometry'); Lk(geo.outputs['Position'], tex.inputs['Vector'])
    vr = N.new('ShaderNodeMapRange'); vr.inputs['To Min'].default_value = 0.9; vr.inputs['To Max'].default_value = 1.07
    Lk(tex.outputs['Fac'], vr.inputs['Value'])
    br = N.new('ShaderNodeMix'); br.data_type = 'RGBA'; br.blend_type = 'MULTIPLY'; br.inputs['Factor'].default_value = 1.0
    Lk(colour_socket, br.inputs['A']); Lk(vr.outputs['Result'], br.inputs['B'])
    return br.outputs['Result']

def _palette_ramp(pal):
    # the preview palette as a constant ramp, slot i at i/16 (elements are created in position order: re-positioning existing elements
    # re-sorts them, which scrambled slots 11-15 in round 2's previews)
    els = pal.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    els[0].position = 0.0; els[0].color = srgb_to_lin(PALETTE_SUMMER[0]) + (1.0,)
    for i, c in enumerate(PALETTE_SUMMER[1:], 1):
        e = els.new(i / 16.0); e.color = srgb_to_lin(c) + (1.0,)

def toon_material():
    m = bpy.data.materials.get('FG_Preview_Toon')
    if m:
        return m
    m, nt, out = _fresh_material('FG_Preview_Toon')
    N = nt.nodes; Lk = nt.links.new
    uvn = N.new('ShaderNodeUVMap'); uvn.uv_map = 'Layer'
    sep = N.new('ShaderNodeSeparateXYZ'); Lk(uvn.outputs['UV'], sep.inputs[0])
    mth = N.new('ShaderNodeMath'); mth.operation = 'MULTIPLY_ADD'
    mth.inputs[1].default_value = 1.0 / 16.0; mth.inputs[2].default_value = 0.5 / 16.0
    Lk(sep.outputs['X'], mth.inputs[0])
    pal = N.new('ShaderNodeValToRGB'); pal.color_ramp.interpolation = 'CONSTANT'
    _palette_ramp(pal)
    Lk(mth.outputs[0], pal.inputs['Fac'])
    col = N.new('ShaderNodeVertexColor'); col.layer_name = 'Col'
    two = N.new('ShaderNodeVectorMath'); two.operation = 'SCALE'; two.inputs['Scale'].default_value = 2.0
    Lk(col.outputs['Color'], two.inputs[0])
    ao = N.new('ShaderNodeMix'); ao.data_type = 'RGBA'; ao.blend_type = 'MULTIPLY'; ao.inputs['Factor'].default_value = 1.0
    Lk(pal.outputs['Color'], ao.inputs['A']); Lk(two.outputs['Vector'], ao.inputs['B'])
    _toon_lighting(nt, _value_breakup(nt, ao.outputs['Result'], 0.9), out)
    return m

CROWN_CARDS_PNG = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Textures', 'T_CrownCards.png')

def toon_card_material():
    # Round 2b, preview only (never exported): the toon material with the crown-card atlas multiplied in (x 2.0, M_Toon_Foliage's _CardGain)
    # and alpha-clipped at 0.5, both faces.
    m = bpy.data.materials.get('FG_Preview_ToonCards')
    if m:
        return m
    m, nt, out = _fresh_material('FG_Preview_ToonCards')
    N = nt.nodes; Lk = nt.links.new
    uvn = N.new('ShaderNodeUVMap'); uvn.uv_map = 'Layer'
    sep = N.new('ShaderNodeSeparateXYZ'); Lk(uvn.outputs['UV'], sep.inputs[0])
    mth = N.new('ShaderNodeMath'); mth.operation = 'MULTIPLY_ADD'
    mth.inputs[1].default_value = 1.0 / 16.0; mth.inputs[2].default_value = 0.5 / 16.0
    Lk(sep.outputs['X'], mth.inputs[0])
    pal = N.new('ShaderNodeValToRGB'); pal.color_ramp.interpolation = 'CONSTANT'
    _palette_ramp(pal)
    Lk(mth.outputs[0], pal.inputs['Fac'])
    col = N.new('ShaderNodeVertexColor'); col.layer_name = 'Col'
    two = N.new('ShaderNodeVectorMath'); two.operation = 'SCALE'; two.inputs['Scale'].default_value = 2.0
    Lk(col.outputs['Color'], two.inputs[0])
    ao = N.new('ShaderNodeMix'); ao.data_type = 'RGBA'; ao.blend_type = 'MULTIPLY'; ao.inputs['Factor'].default_value = 1.0
    Lk(pal.outputs['Color'], ao.inputs['A']); Lk(two.outputs['Vector'], ao.inputs['B'])
    img = bpy.data.images.get('T_CrownCards.png')
    if img is None and os.path.exists(CROWN_CARDS_PNG):
        img = bpy.data.images.load(CROWN_CARDS_PNG)
    tex = N.new('ShaderNodeTexImage')
    if img is not None:
        img.colorspace_settings.name = 'Non-Color'
        tex.image = img
    uv0 = N.new('ShaderNodeUVMap'); uv0.uv_map = 'UVMap'
    Lk(uv0.outputs['UV'], tex.inputs['Vector'])
    gain = N.new('ShaderNodeVectorMath'); gain.operation = 'SCALE'; gain.inputs['Scale'].default_value = 2.0
    Lk(tex.outputs['Color'], gain.inputs[0])
    mul = N.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    Lk(ao.outputs['Result'], mul.inputs['A']); Lk(gain.outputs['Vector'], mul.inputs['B'])
    _toon_lighting(nt, mul.outputs['Result'], out)
    for n in N:     # M_Toon_Foliage keeps only a faint rim (crown normals at the silhouette)
        if n.type == 'MATH' and n.operation == 'MULTIPLY' and abs(n.inputs[1].default_value - 0.22) < 1e-4:
            n.inputs[1].default_value = 0.08
    em = next(n for n in N if n.type == 'EMISSION')
    clip = N.new('ShaderNodeMath'); clip.operation = 'GREATER_THAN'; clip.inputs[1].default_value = 0.5
    Lk(tex.outputs['Alpha'], clip.inputs[0])
    tr = N.new('ShaderNodeBsdfTransparent')
    mix = N.new('ShaderNodeMixShader')
    Lk(clip.outputs[0], mix.inputs['Fac']); Lk(tr.outputs[0], mix.inputs[1]); Lk(em.outputs[0], mix.inputs[2])
    Lk(mix.outputs[0], out.inputs['Surface'])
    try:
        m.surface_render_method = 'DITHERED'
    except Exception:
        pass
    m.use_backface_culling = False
    return m

def toon_ground_material():
    m = bpy.data.materials.get('FG_Preview_ToonGround')
    if m:
        return m
    m, nt, out = _fresh_material('FG_Preview_ToonGround')
    N = nt.nodes; Lk = nt.links.new
    geo = N.new('ShaderNodeNewGeometry'); sep = N.new('ShaderNodeSeparateXYZ'); Lk(geo.outputs['Position'], sep.inputs[0])
    mr = N.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 0.55; mr.inputs['From Max'].default_value = 0.75
    Lk(sep.outputs['Z'], mr.inputs['Value'])
    mix = N.new('ShaderNodeMix'); mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = srgb_to_lin(PALETTE_SUMMER[5]) + (1,)
    mix.inputs['B'].default_value = srgb_to_lin(PALETTE_SUMMER[0]) + (1,)
    Lk(mr.outputs['Result'], mix.inputs['Factor'])
    _toon_lighting(nt, _value_breakup(nt, mix.outputs['Result'], 0.35), out)
    return m

def toon_water_material():
    m = bpy.data.materials.get('FG_Preview_ToonWater')
    if m:
        return m
    m = bpy.data.materials.new('FG_Preview_ToonWater'); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; Lk = nt.links.new
    bsdf = next(n for n in N if n.type == 'BSDF_PRINCIPLED')
    geo = N.new('ShaderNodeNewGeometry'); sep = N.new('ShaderNodeSeparateXYZ'); Lk(geo.outputs['Position'], sep.inputs[0])
    # Unity z = -Blender y: shallow at the coast (z -45), deep by ~6 m depth (z -21); the river and pools stay shallow
    mr = N.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 44.0; mr.inputs['From Max'].default_value = 18.0
    Lk(sep.outputs['Y'], mr.inputs['Value'])
    ramp = N.new('ShaderNodeValToRGB')
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, srgb_to_lin((0.30, 0.86, 0.80)) + (1,)
    els[1].position, els[1].color = 1.0, srgb_to_lin((0.03, 0.36, 0.62)) + (1,)
    e = els.new(0.35); e.color = srgb_to_lin((0.10, 0.62, 0.72)) + (1,)
    Lk(mr.outputs['Result'], ramp.inputs['Fac'])
    Lk(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.12
    bsdf.inputs['Alpha'].default_value = 0.9
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return m

def toon_preview():
    # Swap the preview materials for the toon set and light the scene with a built-in sky (preview only).
    tm, gm, wm = toon_material(), toon_ground_material(), toon_water_material()
    old_pal = bpy.data.materials.get('FG_Preview_Palette')
    old_w = bpy.data.materials.get('FG_Preview_Water')
    for ob in bpy.data.objects:
        if ob.type != 'MESH':
            continue
        mats = ob.data.materials
        for i, mt in enumerate(mats):
            if mt is not None and old_pal is not None and mt == old_pal:
                mats[i] = tm
            elif mt is not None and old_w is not None and mt == old_w:
                mats[i] = wm
        if ob.name.startswith('FG_Preview_Terrain') and len(mats):
            mats[0] = gm
    w = bpy.context.scene.world
    nt = w.node_tree
    bg = next(n for n in nt.nodes if n.type == 'BACKGROUND')
    sky = next((n for n in nt.nodes if n.type == 'TEX_SKY'), None)
    if sky is None:
        sky = nt.nodes.new('ShaderNodeTexSky')
        for t in ('PREETHAM', 'HOSEK_WILKIE'):
            try:
                sky.sky_type = t
                break
            except Exception:
                pass
        try:
            sky.sun_direction = (0.35, -0.55, 0.62)
            sky.turbidity = 2.6
        except Exception:
            pass
        nt.links.new(sky.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = 0.6
    sun = bpy.data.objects.get('FG_Preview_Sun')
    if sun:
        sun.data.energy = 3.6
        sun.data.color = srgb_to_lin(TOON_LIGHT)

def render_view2(path, eye_u, target_u, lens=24, res=(1280, 720), levels=48):
    # render_view with the toon preview: Standard view transform (flat toon colours, no filmic roll-off), slight contrast look.
    sc = bpy.context.scene
    cam = preview_scene()
    eye, tgt = to_blender(eye_u), to_blender(target_u)
    cam.location = eye
    cam.rotation_euler = (tgt - eye).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    cam.data.clip_end = 900
    try:
        sc.view_settings.view_transform = 'Standard'
    except Exception:
        pass
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


# =======================================================================================================================================
# ROUND 3 (2026-10-17): the creek lagoon. The West Horn (a dune hook, COAST_BARS) curls round a calm, shallow lagoon where the creek comes
# out; a plank casting dock runs into it from the beach (stand point 0 moves here from the open beach at x -48: a different water to fish,
# greener and calmer, its own buoys), and the horn's tip is a new stand point over the open bay (stand point 13). The horn carries marram
# (ground cover), driftwood, a beached rowboat and a red-and-white channel beacon with a DOWNRIVER board pointing at the river mouth past the
# west wall - where the crew goes next.
# =======================================================================================================================================
LAGOON_DOCK = (-49.6, -47.4, -50.6, -40.4, 0.7)   # x0, x1, z0 (on the beach, flush), z1 (head), deck top
HORN_TIP_STAND = (-45.6, -16.8, 32.0)             # x, z, facing (y from the horn's surface)

def horn_stand_y():
    return round(coast_y(HORN_TIP_STAND[0], HORN_TIP_STAND[1]) + 0.05, 2)

FOOTBRIDGE = (-58.4, -49.6, -53.6, -49.0, 0.45)   # x0, z0 (west end, on the horn), x1, z1 (east end, on the beach), arch rise

def footbridge(g, r):
    x0, z0, x1, z1, rise = FOOTBRIDGE
    L_ = math.hypot(x1 - x0, z1 - z0)
    ux, uz = (x1 - x0) / L_, (z1 - z0) / L_
    yaw = math.degrees(math.atan2(ux, uz))
    def deck_y(t):
        return lerp(land_y(x0, z0), land_y(x1, z1), t) + rise * 4.0 * t * (1.0 - t) + 0.08
    n = int(L_ / 0.34)
    for k in range(n):
        t = (k + 0.5) / n
        px, pz = x0 + ux * L_ * t, z0 + uz * L_ * t
        slope = math.degrees(math.atan2(deck_y(t + 0.02) - deck_y(t - 0.02), 0.04 * L_))
        g.add(xform(rbox((1.5 + r.uniform(-0.05, 0.05), 0.07, 0.31), 0.012, 1), (px, deck_y(t), pz), ry(yaw) @ rx(-slope)),
              S['Wood'] if k % 3 else S['Dock'], L['Wood'], 0.5, None, r.uniform(0.86, 1.02))
        # a walkable box per plank (Structure; the deck climbs 2-4 cm a plank)
        box_item(g, 'FootbridgePlank', (px - 0.2, deck_y(t) - 0.1, pz - 0.75), (px + 0.2, deck_y(t) + 0.035, pz + 0.75))
    for s_ in (-1, 1):
        rail = []
        for k in range(9):
            t = k / 8
            px, pz = x0 + ux * L_ * t + uz * 0.72 * s_, z0 + uz * L_ * t - ux * 0.72 * s_
            rail.append(Vector((px, deck_y(t) + 0.9, pz)))
            if k % 2 == 0:
                g.add(tube([Vector((px, deck_y(t) - 0.35, pz)), Vector((px, deck_y(t) + 0.92, pz))], 0.05, 6), S['WoodDark'], L['Wood'], 0.45)
        g.add(tube(rail, 0.04, 6), S['Wood'], L['Wood'], 0.5)

BREAKWATER_SETTS_LIFT = 0.025

def build_breakwater_setts():
    # Round 3 (critics: "6 m of blank, evenly lit paving - a runway"): a thin paving of smaller setts laid over phase 1's big slabs (2.5 cm
    # proud: the collider top stays 1.5), three to a row with staggered joints, each a grey of its own value, a darker wet band along both
    # edges and moss in a few joints. Pass 2, so the phase-1 FBX stay byte-identical (their AO is baked against each other).
    g = G2('Harbour_Core_BreakwaterSetts_Tri', 'core', tri=True, rays=8, ao_dist=0.4,
           note='Round 3: setts over the breakwater walk (visual; the walkable top is the phase-1 box at y 1.5).')
    r = rng2('breakwater.setts')
    x0, x1, z0, z1 = BREAKWATER
    n = int((z1 - z0) / 0.8)
    pitch = (z1 - z0) / n
    top = BREAKWATER_TOP + BREAKWATER_SETTS_LIFT
    for i in range(n):
        zc = z0 + pitch * (i + 0.5)
        cuts = sorted([r.uniform(0.9, 1.5), r.uniform(2.3, 3.1)])
        if i % 2:
            cuts = sorted([r.uniform(1.4, 2.0), r.uniform(2.8, 3.3)])
        edges = [x0 + 0.03] + [x0 + c for c in cuts] + [x1 - 0.03]
        for k in range(len(edges) - 1):
            sx0, sx1 = edges[k] + 0.035, edges[k + 1] - 0.035
            edge = k == 0 or k == len(edges) - 2
            tint = r.uniform(0.82, 1.04) * (0.84 if edge and r.random() < 0.6 else 1.0)
            slot = S['Reed'] if r.random() < 0.05 else S['Rock']
            g.add(xform(rbox((sx1 - sx0, 0.06, pitch - 0.07), 0.025, 1), ((sx0 + sx1) / 2, top - 0.03, zc), ry(r.uniform(-1.2, 1.2))),
                  slot, L['Rock'], 0.5, None, tint)
    return g

def build_lagoon():
    g = G2('Harbour_Core_Lagoon', 'core', rays=16,
           note='Round 3: the creek-lagoon casting dock (stand point 0; Structure boxes: deck, posts) and the channel beacon on the horn tip.')
    d = G2('Harbour_Detail_Lagoon', 'detail', rays=10,
           note='Round 3: horn dressing - driftwood, a beached rowboat, lagoon floats (green: the lagoon), lobster pots on the dock. No colliders.')
    r = rng2('lagoon')
    x0, x1, z0, z1, top = LAGOON_DOCK
    z = z0
    while z < z1 - 0.05:
        w = min(0.4, z1 - z)
        g.add(xform(rbox((x1 - x0 + r.uniform(-0.05, 0.08), 0.07, w - 0.04), 0.012, 1), ((x0 + x1) / 2 + r.uniform(-0.04, 0.04), top - 0.035, z + w / 2),
                    ry(r.uniform(-1.2, 1.2))), S['Wood'] if r.random() < 0.5 else S['Dock'], L['Wood'], 0.5, tint=r.uniform(0.86, 1.04))
        z += w
    for sx in (x0 + 0.22, x1 - 0.22):
        pbox(g, (sx - 0.08, top - 0.24, z0 + 0.4), (sx + 0.08, top - 0.07, z1), S['WoodDark'], L['Wood'], 0.45)
    zz = z1 - 0.3
    while zz > z0 + 3.0:
        for px in (x0 + 0.1, x1 - 0.1):
            foot = min(terrain_y(zz), lagoon_bed_y(px, zz)) - 0.1
            add_lathe(g, (px, foot, zz), [(0, 0.12), (0.12, 0.1), (top - 0.07 - foot, 0.09), (top + 0.28 - foot, 0.09), (top + 0.32 - foot, 0.0)],
                      S['WoodDark'], L['Wood'], 0.45, sides=7)
            box_item(g, 'LagoonDockPost', (px - 0.1, foot, zz - 0.1), (px + 0.1, top - 0.07, zz + 0.1))
        zz -= 2.6
    box_item(g, 'LagoonDockDeck', (x0, top - 0.12, z0), (x1, top, z1))
    # a rope rail along the west side (posts + sagging rope, visual), a bench-crate and a bait bucket at the head
    for k in range(4):
        pz = z0 + 1.5 + k * 2.6
        add_lathe(g, (x0 + 0.08, top, pz), [(0, 0.06), (0.95, 0.05), (1.0, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=6)
        if k:
            g.add(tube(rope_line(Vector((x0 + 0.08, top + 0.9, pz - 2.6)), Vector((x0 + 0.08, top + 0.9, pz)), 0.18, n=8), 0.02, 4, caps=False),
                  S['Sand'], L['Cloth'], 0.5)
    crate_stack_at(d, x1 - 0.5, top, z0 + 1.6, r, 'LagoonCrate', n=1, barrel=False)
    g.mark()
    add_lathe(g, (x0 + 0.5, top, z1 - 0.6), [(0, 0.17), (0.32, 0.19), (0.34, 0.0)], S['Foliage'], L['none'], 0.5, sides=12)
    g.add(tube([Vector((x0 + 0.5 + 0.17 * math.cos(a), top + 0.36 + 0.08 * math.sin(a), z1 - 0.6)) for a in [math.pi * i / 8 for i in range(9)]], 0.012, 4, caps=False),
          S['Rock'], L['none'], 0.5)
    lamp_post(g, x1 - 0.25, z1 - 0.5, 'LagoonLamp', y=top)
    footbridge(g, r)
    # the channel beacon on the horn's knuckle: a red-and-white banded pole on a stone cairn, a cage top, a DOWNRIVER board (pointing west)
    bx, bz = -37.9, -22.9
    by = (coast_y(bx, bz) or 0.6) - 0.15
    g.mark()
    for k in range(5):
        g.add(xform(cliff_rock(r.uniform(0.45, 0.6), r, (1.1, 0.7, 1.0), 2, r.uniform(0, 360)), (bx + 0.5 * math.cos(k * 1.3), by + 0.15, bz + 0.5 * math.sin(k * 1.3))),
              S['Rock'], L['Rock'], AO_ROCK)
    for k in range(6):
        add_lathe(g, (bx, by + 0.5 + 0.55 * k, bz), [(0, 0.13), (0.55, 0.12), (0.56, 0.0)], S['Roof'] if k % 2 == 0 else S2['Snow'], L['none'], 0.6, sides=10,
                  cap1=(k == 5))
    for k in range(4):
        a = math.pi / 2 * k + 0.4
        g.add(tube([Vector((bx + 0.12 * math.cos(a), by + 3.8, bz + 0.12 * math.sin(a))), Vector((bx + 0.32 * math.cos(a), by + 4.2, bz + 0.32 * math.sin(a))),
                    Vector((bx + 0.12 * math.cos(a), by + 4.6, bz + 0.12 * math.sin(a)))], 0.025, 4), S['WoodDark'], L['none'], 0.5)
    add_lathe(g, (bx, by + 4.6, bz), [(0, 0.3), (0.05, 0.28), (0.12, 0.0)], S['Roof'], L['none'], 0.6, sides=10, cap0=True)
    g.add(xform(rbox((0.12, 0.12, 0.12), 0.02, 1), (bx, by + 4.25, bz)), S['Accent'], L['none'], 0.6)
    rot = ry(-90)
    P = lambda a, b_, c: Vector((bx, by, bz)) + rot @ Vector((a, b_, c))
    g.add(xform(prism([(-0.9, 0.0), (0.6, 0.0), (0.95, 0.24), (0.6, 0.48), (-0.9, 0.48)], 0.07, 0.02, segs=1), P(0.35, 2.0, 0.14), rot @ ry(90)),
          S['Accent'], L['Wood'], 0.5)
    sign_text(g, 'DOWNRIVER', 0.22, P(0.32, 2.24, 0.19), rot, S['WoodDark'])
    g.solid('ChannelBeacon')
    g.items[-1]['light'] = [round(bx, 2), round(by + 4.25, 2), round(bz, 2)]
    g.items[-1]['light_colour'] = 'red'
    # horn dressing: driftwood logs on the outer beach, a beached rowboat on the inner shore, lagoon floats
    for (lx, lz, yaw, ln) in ((-58.6, -33.0, 70.0, 2.8), (-54.8, -24.4, 128.0, 2.2), (-45.4, -19.6, 10.0, 3.1), (-60.8, -40.0, 95.0, 1.9)):
        y = (coast_y(lx, lz) or 0.3)
        prof = [(-ln / 2, 0.0), (-ln / 2 + 0.02, 0.09), (-ln / 4, 0.12), (ln / 4, 0.1), (ln / 2 - 0.05, 0.075), (ln / 2, 0.0)]
        geo, fs = lathe(prof, 7)
        d.add(xform(geo, (lx, y + 0.05, lz), ry(yaw) @ rx(90) @ rz(r.uniform(-4, 4))), S['Sand'], L['Bark'], 0.5)
    bx, bz = -55.4, -26.8
    by = coast_y(bx, bz) or 0.2
    rowboat(d, 'BeachedRowboat', (bx, by + 0.12, bz), 210.0, S['Foliage'], roll=14.0, oars=False)
    for k, (fx, fz) in enumerate(((-53.0, -33.0), (-53.5, -37.5), (-41.0, -36.8))):   # out of the dock stand's lane
        add_lathe(d, (fx, -0.08, fz), [(0, 0.0), (0.02, 0.2), (0.18, 0.24), (0.34, 0.2), (0.4, 0.0)], S['Foliage'], L['none'], 0.6, sides=12)
        add_lathe(d, (fx, 0.3, fz), [(0, 0.03), (0.35, 0.025), (0.37, 0.0)], S['WoodDark'], L['none'], 0.6, sides=5)
    return [g, d]

STAND_POINTS_R1.append((HORN_TIP_STAND[0], horn_stand_y(), HORN_TIP_STAND[1], HORN_TIP_STAND[2]))   # stand point 13

# =======================================================================================================================================
# ROUND 4 (2026-10-18): the creek glen (glen_h, build_glen above, with the land shape), ...
# =======================================================================================================================================
WATERFALL_MATERIAL = 'M_Waterfall'

def build_waterfall():
    # The brook (a ribbon down its valley on the West Head's top) and the fall (a sheet from the lip down the alcove into the sea), one mesh
    # on the Waterfall material: uv0 = (across 0..1, metres along the flow) - the shader scrolls the streaks along v. Plus the plunge: white
    # foam puffs and dark wet rocks at its foot (toon), and a mist point (a smoke point the Unity builder makes a spray of).
    if not FALL_INFO:
        build_cliffs()          # the fall row comes from the West Head's sweep (a live rebuild of this builder alone)
    fi = FALL_INFO
    ex, ez, nx, nz, wd = fi['x'], fi['z'], fi['nx'], fi['nz'], fi['wander']
    tx, tz = nz, -nx                                   # along the edge
    g = G2('Harbour_Core_Waterfall', 'core', rays=0, material=WATERFALL_MATERIAL + ' (FishGame/Waterfall: uv0 = across, metres along the flow)',
           note='Round 4: the West Head brook and its 8 m fall into the sea (visual, outside the walls; no collider, no shadows).')
    V, N, F = [], [], []
    def strip(path, widths, normals_up=True):
        # path: points along the flow (Vector), widths: half widths; across = the edge tangent for the fall, the path's side for the brook
        base = len(V)
        acc = 0.0
        cols = 5
        for i, p in enumerate(path):
            if i:
                acc += (p - path[i - 1]).length
            if normals_up:
                tdir = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)])
                side = Vector((tdir.z, 0.0, -tdir.x)).normalized()
                nrm = Vector((0, 1, 0))
            else:
                side = Vector((tx, 0.0, tz))
                nrm = Vector((-nx, 0.25, -nz)).normalized()
            for c in range(cols):
                a = -1.0 + 2.0 * c / (cols - 1)
                q = p + side * (a * widths[i])
                if not normals_up:
                    q = q + Vector((-nx, 0.0, -nz)) * (0.12 * (1.0 - a * a))      # the sheet bulges out a little in the middle
                V.append(q); N.append(nrm)
        for i in range(len(path) - 1):
            for c in range(cols - 1):
                a = base + i * cols + c
                F.append([a, a + 1, a + cols + 1, a + cols])
        return acc
    # the brook: from the lip back up its valley (flow runs toward the lip)
    lip = Vector((ex + nx * (5.0 + wd), FALL_Y + 0.03, ez + nz * (5.0 + wd)))
    up = [lip] + [Vector((bx, 0.0, bz)) for bx, bz in BROOK[1:]]
    dense = []
    for i in range(len(up) - 1):
        a, b = up[i], up[i + 1]
        n = max(1, int((b - a).length / 1.2))
        for k in range(n):
            dense.append(a.lerp(b, k / n))
    dense.append(up[-1])
    dense = list(reversed(dense))                      # upstream -> lip
    for p in dense[:-1]:
        _, t = poly_nearest(p.x, p.z, BROOK)
        p.y = brook_floor(t) + 0.03
    widths = [lerp(0.6, 1.7, (i / (len(dense) - 1)) ** 2) for i in range(len(dense))]
    b0 = len(V)
    strip(dense, widths, True)
    # the fall: lip curl, then the drop (inset from the edge, y)
    prof = [(5.0, FALL_Y + 0.03), (4.62, FALL_Y - 0.1), (4.42, FALL_Y - 0.5), (4.32, FALL_Y - 1.3), (4.2, FALL_Y - 2.6), (4.08, 4.4),
            (3.98, 2.6), (3.9, 1.1), (3.84, 0.2), (3.8, -0.35)]
    fall = [Vector((ex + nx * (ins + wd), y, ez + nz * (ins + wd))) for ins, y in prof]
    fw = [lerp(1.7, 2.5, k / (len(prof) - 1)) for k in range(len(prof))]
    f0 = len(V)
    strip(fall, fw, False)
    # uv: across 0..1, along = metres of flow (continuous from the brook into the fall)
    UVv = []
    cols = 5
    def along(pts):
        out, acc = [], 0.0
        for i, p in enumerate(pts):
            if i:
                acc += (p - pts[i - 1]).length
            out.append(acc)
        return out
    la, lf = along(dense), along(fall)
    vv = [0.0] * len(V)
    for i, a in enumerate(la):
        for c in range(cols):
            vv[b0 + i * cols + c] = (c / (cols - 1), a)
    for i, a in enumerate(lf):
        for c in range(cols):
            vv[f0 + i * cols + c] = (c / (cols - 1), la[-1] + a)
    UVf = [[vv[k] for k in f] for f in F]
    g.add((V, N, F, UVf), S['Sand'], L['none'], 1.0)
    # the plunge: foam puffs and wet rocks round the foot of the fall
    d = G2('Harbour_Core_FallRocks_Tri', 'core', tri=True, rays=8, ao_dist=1.5,
           note='Round 4: the waterfall plunge - foam puffs (white) and wet rocks at its foot, boulders along the brook. Visual, outside the walls.')
    r = rng2('waterfall')
    foot = Vector((ex + nx * (3.4 + wd), 0.0, ez + nz * (3.4 + wd)))
    for k in range(13):
        a = r.uniform(0, 2 * math.pi)
        rr = r.uniform(0.4, 2.3)
        p = foot + Vector((math.cos(a) * rr, 0.0, math.sin(a) * rr)) + Vector((-nx, 0.0, -nz)) * 0.8
        rad = r.uniform(0.35, 0.8) * (1.2 - rr / 3.5)
        d.add(xform(blob(rad, r, (1.3, 0.38, 1.0), subdiv=2, rough=0.22, flat=-0.6, yaw=r.uniform(0, 360)), (p.x, 0.02 + 0.05 * r.random(), p.z)),
              S2['Snow'], L['none'], 0.9)
    for k in range(9):
        a = r.uniform(0, 2 * math.pi)
        rr = r.uniform(2.0, 3.6)
        p = foot + Vector((math.cos(a) * rr, 0.0, math.sin(a) * rr * 0.7))
        if (p - foot).dot(Vector((nx, 0.0, nz))) > 1.0:
            continue
        rad = r.uniform(0.6, 1.3)
        d.add(xform(cliff_rock(rad, r, (1.2, 0.7, 1.0), 2, r.uniform(0, 360)), (p.x, -0.25 + rad * 0.2, p.z)), S['Rock'], L['Rock'], AO_ROCK, None,
              r.uniform(0.55, 0.75))
    for k in range(0, len(dense) - 3, 3):
        p = dense[k]
        side = 1 if k % 2 else -1
        tdir = dense[k + 1] - p
        sd = Vector((tdir.z, 0.0, -tdir.x)).normalized() * side
        q = p + sd * (widths[k] + r.uniform(0.15, 0.5))
        rad = r.uniform(0.3, 0.6)
        d.add(xform(cliff_rock(rad, r, (1.2, 0.7, 1.0), 1, r.uniform(0, 360)), (q.x, p.y - 0.05, q.z)), S['Rock'], L['Rock'], AO_ROCK, None, r.uniform(0.7, 0.9))
    g.extra.setdefault('smoke_points', []).append(dict(name='MistFall', position=[round(foot.x, 2), 0.4, round(foot.z, 2)]))
    return [g, d]

# ---- the reef off the pier (critics: "the pier head looks at a blank blue plane - nothing to cast toward") ----
# Five rock stacks with a half-sunk fishing boat and a marker buoy, 20-35 m north-west of the T-head: past every cast lane's 20 m and every
# cast's reach (16.4 m), so it is something to fish toward, not a hazard; visual only (no collider: the bake and CastProbe never see it).
# The water reads it through the bed map: a shallow rocky floor rises round the stacks (turquoise over the deep blue) with kelp at the surface.
REEF_C = (-26.0, 11.0)
REEF_STACKS = [(-24.6, 8.6, 2.1, 6.4), (-28.6, 11.2, 1.4, 3.6), (-26.0, 15.0, 1.1, 2.3), (-21.4, 13.0, 0.9, 1.4), (-31.4, 7.0, 0.8, 1.0)]
REEF_WRECK = (-35.6, 9.2, 128.0)           # x, z, heading (clear of the big stack seen from the T-head)
REEF_BOX = (-44.0, 24.0, -6.0, 36.0)
# and the Skerries, a smaller cluster north-east of the T-head (what the T-head looks out at; 30+ m off, past every lane)
REEF_C2 = (13.5, 26.5)
REEF_STACKS += [(13.0, 24.6, 1.35, 3.1), (16.6, 27.6, 0.95, 1.7), (10.4, 28.8, 0.75, 0.9)]

def reef_k(x, z):
    return max(smoothstep(13.0, 6.0, math.hypot((x - REEF_C[0]) / 1.2, z - REEF_C[1])),
               0.8 * smoothstep(8.0, 3.5, math.hypot(x - REEF_C2[0], z - REEF_C2[1])))

def reef_floor_y(x, z):
    y = lerp(-9.0, -2.6 + 0.5 * n2(x, z, 0.25, 160), reef_k(x, z))
    for sx, sz, rad, hgt in REEF_STACKS:
        d = math.hypot(x - sx, z - sz)
        y = max(y, -0.7 - 2.4 * smoothstep(rad * 0.9, rad * 3.2, d))
    return y

def stack_paint(V, N, guano=True):
    vsl = []
    for v, n in zip(V, N):
        band = (v.y + 0.3 * n2(v.x, v.z, 0.2, 161)) / 1.3
        f = band - math.floor(band)
        if v.y < 0.25:
            vsl.append(S['FoliageDark'] if v.y > -0.6 else S['WoodDark'])       # weed line at the water, dark below
        elif guano and n.y > 0.55 and v.y > 0.9:
            vsl.append(S2['Snow'])                                              # the gulls' white tops
        else:
            vsl.append(S['Rock'] if f < 0.6 else (S['Dock'] if f < 0.85 else S['WoodDark']))
    return vsl

def kelp_mat(d, r, kx, kz, n_lo=4, n_hi=7, size=1.0):
    # olive fronds lying on the surface (a fan of flat blades)
    for j in range(r.randint(n_lo, n_hi)):
        b = r.uniform(0, 2 * math.pi)
        base = Vector((kx, 0.02, kz))
        dirn = Vector((math.cos(b), 0.0, math.sin(b)))
        side = Vector((-dirn.z, 0.0, dirn.x))
        ln, w = r.uniform(0.7, 1.5) * size, r.uniform(0.12, 0.2) * size
        pts = [base + dirn * (ln * t) + side * (0.15 * math.sin(t * 5.0 + j)) for t in (0.0, 0.35, 0.7, 1.0)]
        V, N, F, UV = [], [], [], []
        for i, pnt in enumerate(pts):
            ww = w * (1.0 - 0.75 * (i / 3.0))
            V += [pnt - side * ww, pnt + side * ww]
            N += [Vector((0, 1, 0))] * 2
        for i in range(3):
            F.append([2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2])
            UV.append([(0, 0), (1, 0), (1, 1), (0, 1)])
        d.add((V, N, F, UV), S['Reed'] if j % 2 else S['FoliageDark'], L['Foliage'], 0.7)

def build_reef():
    g = G2('Harbour_Core_Reef_Tri', 'core', tri=True, rays=8, ao_dist=1.5,
           note='Round 4: the reef off the pier - rock stacks and a half-sunk fishing boat, beyond every lane and cast; visual only (no collider).')
    d = G2('Harbour_Detail_Reef', 'detail', rays=0,
           note='Round 4: kelp mats at the surface round the reef stacks. No colliders.')
    r = rng2('reef')
    for sx, sz, rad, hgt in REEF_STACKS:
        tall = hgt + 1.6
        geo = cliff_rock(tall / 2.0, r, (rad * 2.0 / tall, 1.0, rad * 1.7 / tall), 3 if hgt > 2.0 else 2, r.uniform(0, 360), top=0.62, strata=4.0)
        V, N, F, UV = xform(geo, (sx, hgt - tall / 2.0, sz))
        g.add((V, N, F, UV), S['Rock'], L['Rock'], AO_ROCK, None, 1.0, stack_paint(V, N))
        for k in range(3):                       # foot boulders breaking the waterline
            a = r.uniform(0, 2 * math.pi)
            br = r.uniform(0.35, 0.7) * rad
            bx, bz = sx + math.cos(a) * rad * 1.15, sz + math.sin(a) * rad * 1.15
            geo = cliff_rock(br, r, (1.2, 0.7, 1.0), 2, r.uniform(0, 360))
            V, N, F, UV = xform(geo, (bx, -0.15 + br * 0.15, bz))
            g.add((V, N, F, UV), S['Rock'], L['Rock'], AO_ROCK, None, r.uniform(0.8, 0.95), stack_paint(V, N, False))
    # the wreck: a small trawler hull rolled on its side, bow up, with a broken mast and its wheelhouse half under
    wx, wz, wh = REEF_WRECK
    rot = ry(wh) @ rx(-17.0) @ rz(31.0)
    c = Vector((wx, 0.1, wz))
    geo, fs = hull(8.5, 2.9, 1.35, transom=0.72, sheer=0.28, hull_slot=S['FoliageDark'], stripe=S['Sand'])
    g.add(xform(geo, c, rot), 0, 0, 0.45, fs, 0.6)
    g.add(xform(rbox((1.9, 1.5, 1.7), 0.08, 2), c + rot @ Vector((0.0, 0.85, -1.4)), rot @ rz(-6)), S['Sand'], L['Wood'], 0.45, None, 0.62)
    g.add(xform(rbox((2.1, 0.12, 1.9), 0.04, 1), c + rot @ Vector((0.0, 1.66, -1.4)), rot @ rz(-6)), S['Roof'], L['Wood'], 0.45, None, 0.55)
    mast = [c + rot @ Vector((0.0, 0.1, 1.0)), c + rot @ Vector((0.05, 2.0, 1.1)), c + rot @ Vector((0.25, 3.1, 1.3))]
    g.add(tube(mast, 0.09, 7), S['WoodDark'], L['Wood'], 0.45, None, 0.7)
    g.add(tube([mast[1] + rot @ Vector((-0.9, 0.0, 0.0)), mast[1] + rot @ Vector((0.9, 0.0, 0.05))], 0.05, 6), S['WoodDark'], L['Wood'], 0.45, None, 0.7)
    for k in range(26):
        a = r.uniform(0, 2 * math.pi)
        rr = r.uniform(4.0, 9.5)
        kx, kz = REEF_C[0] + math.cos(a) * rr * 1.2, REEF_C[1] + math.sin(a) * rr
        if any(math.hypot(kx - sx, kz - sz) < rad * 1.4 for sx, sz, rad, _ in REEF_STACKS):
            continue
        kelp_mat(d, r, kx, kz)
    for k in range(10):
        a = r.uniform(0, 2 * math.pi)
        rr = r.uniform(2.6, 6.0)
        kx, kz = REEF_C2[0] + math.cos(a) * rr, REEF_C2[1] + math.sin(a) * rr
        if any(math.hypot(kx - sx, kz - sz) < rad * 1.4 for sx, sz, rad, _ in REEF_STACKS):
            continue
        kelp_mat(d, r, kx, kz, 3, 5, 0.9)
    return [g, d]

# ---- the East Ledge (critics, third round running): boulders on the ledge's flanks (Structure boxes) and the ledge's stand ----
LEDGE_STAND = (59.4, -37.6, 356.0)          # x, z, facing: north over the east basin, past the jetty head (the east wall's inner face is x 60.0)
LEDGE_BOULDERS = [(57.4, -45.2, 1.05), (57.2, -43.0, 1.2), (57.3, -40.8, 0.95), (57.4, -39.0, 1.05), (62.2, -38.2, 1.3), (62.3, -41.6, 1.15),
                  (62.0, -45.0, 1.2), (63.2, -33.6, 2.2), (64.8, -28.4, 2.7)]   # the last two: sea stacks off the ledge's tip, seen past the jetty
LEDGE_FLOATS = [(64.4, -26.0), (65.0, -21.5)]

def ledge_stand_y():
    return round(coast_y(LEDGE_STAND[0], LEDGE_STAND[1]) + 0.05, 2)

def build_east_ledge():
    g = G2('Harbour_Core_EastLedge_Tri', 'core', tri=True, rays=10, ao_dist=1.5,
           note='Round 4: the East Ledge boulders (Structure boxes), a bollard and a lantern post; the walkable shelf is the coast bar "Shelf".')
    d = G2('Harbour_Detail_EastLedge', 'detail', rays=8, note='Round 4: kelp at the East Ledge foot, a rope coil, a bait bucket. No colliders.')
    r = rng2('eastledge')
    for k, (bx, bz, rad) in enumerate(LEDGE_BOULDERS):
        g.mark()
        top = coast_y(bx, bz) or 0.0
        stack = rad > 2.0
        geo = cliff_rock(rad, r, (0.9, 1.35, 0.85) if stack else (1.15, 0.85, 1.0), 3 if stack else 2, r.uniform(0, 360))
        V, N, F, UV = xform(geo, (bx, (rad * 0.55 if stack else max(top, 0.0) - rad * 0.25), bz))
        g.add((V, N, F, UV), S['Rock'], L['Rock'], AO_ROCK, None, r.uniform(0.85, 1.0), stack_paint(V, N, False))
        g.solid('LedgeBoulder_%d' % k)
    sx, sz, _ = LEDGE_STAND
    sy = coast_y(sx, sz) or 1.0
    g.mark()
    add_lathe(g, (sx + 1.1, sy - 0.1, sz - 1.4), [(0, 0.12), (0.55, 0.1), (0.6, 0.13), (0.68, 0.0)], S['WoodDark'], L['none'], 0.5, sides=10, cap1=True)
    g.solid('LedgeBollard')
    rope_coil(d, 'LedgeRope', (sx + 1.5, sy, sz - 2.2))
    lamp_post(g, sx + 1.2, sz - 3.6, 'LedgeLamp', y=coast_y(sx + 1.2, sz - 3.6))
    add_lathe(d, (sx - 0.6, sy, sz - 1.8), [(0, 0.16), (0.3, 0.19), (0.32, 0.0)], S['Accent'], L['none'], 0.5, sides=12)
    for k in range(18):
        a = r.uniform(-1.2, 1.6)
        rr = r.uniform(2.0, 4.0)
        kx, kz = sx - 1.6 + math.cos(a) * rr, sz - 4.0 + math.sin(a) * rr * 2.2
        if (coast_y(kx, kz) or -9.0) > -0.25 or kz > -33.5:
            continue
        kelp_mat(d, r, kx, kz, 3, 6, 0.8)
    # the ledge's floats (red and white: the bay's are orange, the lagoon's green) and the reef's marker buoy
    f = G2('Harbour_Detail_Anim_LedgeFloat', 'detail', local=True, rays=8, anim='bob',
           note='Round 4: the East Ledge red-and-white floats (origin at the waterline). Bob. Visual only.')
    prof = [(-0.18, 0.0), (-0.16, 0.12), (-0.05, 0.19), (0.08, 0.19), (0.17, 0.13), (0.22, 0.0)]
    add_lathe(f, (0, 0, 0), prof, S['Roof'], L['none'], 0.6, sides=12, ring_slots=[S['Roof'], S2['Snow'], S2['Snow'], S['Roof'], S['Roof']])
    add_lathe(f, (0, 0.2, 0), [(0, 0.015), (0.9, 0.012), (0.92, 0.0)], S['WoodDark'], L['none'], 0.6, sides=5)
    f.add(xform(rbox((0.01, 0.16, 0.24), 0.004, 1), (0, 1.02, 0.12)), S2['Snow'], L['Cloth'], 0.7)
    for k, (fx, fz) in enumerate(LEDGE_FLOATS):
        f.place('LedgeFloat_%d' % k, (fx, 0.0, fz), r.uniform(0, 360))
    m = G2('Harbour_Detail_Anim_ReefMarker', 'detail', local=True, rays=8, anim='bob',
           note='Round 4: the reef marker buoy - a 2.6 m red-and-white spar with a cage and a ball topmark (origin at the waterline). Bobs.')
    add_lathe(m, (0, -0.5, 0), [(0, 0.0), (0.05, 0.5), (0.45, 0.62), (0.75, 0.55), (0.82, 0.0)], S['Roof'], L['none'], 0.6, sides=14, cap1=True)
    for k in range(4):
        add_lathe(m, (0, 0.3 + 0.5 * k, 0), [(0, 0.13), (0.5, 0.11), (0.51, 0.0)], S['Roof'] if k % 2 else S2['Snow'], L['none'], 0.6, sides=10,
                  cap1=(k == 3))
    for k in range(4):
        a = math.pi / 2 * k
        m.add(tube([Vector((0.1 * math.cos(a), 2.3, 0.1 * math.sin(a))), Vector((0.28 * math.cos(a), 2.6, 0.28 * math.sin(a))),
                    Vector((0.1 * math.cos(a), 2.9, 0.1 * math.sin(a)))], 0.02, 4), S['WoodDark'], L['none'], 0.5)
    m.add(xform(blob(0.17, r, (1, 1, 1), subdiv=2, rough=0.0, flat=-2), (0, 3.08, 0)), S['WoodDark'], L['none'], 0.6)
    m.place('ReefMarker', (REEF_C[0] + 4.6, 0.0, REEF_C[1] - 6.6), 0.0)
    return [g, d, f, m]

# ---- life (critics round 3: "no people, no cats or dogs, no dock workers - a model-railway layout"; life 5.5-6.5) ----
# Chunky toon villagers (big heads, round bodies, dot eyes: RV There Yet / GWYF proportions), a dog, hens and a goat. Detail local models
# (no colliders, hidden on Low); AmbientLife moves them: "Villager" idles (weight shift, looks about), "Walker" strolls back and forth along
# its facing for `radius` metres (the dog too), hens and the goat peck and look about (the perched-gull motion).
SKIN = [(S['Sand'], 0.97), (S['Wood'], 1.0), (S['Sand'], 0.88), (S['Dock'], 1.05)]

def villager_body(g, r, coat, trousers, skin, hat, kid=False, extra=None):
    k = 0.68 if kid else 1.0
    ct = extra.get('tint', 1.0) if extra else 1.0      # round 4: the mafia men's black suits (a dark tint over the coat slot)
    sk_slot, sk_tint = skin
    for s_ in (-1, 1):
        g.add(tube([Vector((s_ * 0.1 * k, 0.04, 0.0)), Vector((s_ * 0.1 * k, 0.52 * k, 0.0))], 0.08 * k, 7), trousers, L['Cloth'], 0.6, None, ct)
        g.add(xform(blob(0.1 * k, r, (0.95, 0.6, 1.35), subdiv=2, rough=0.02, flat=-0.6), (s_ * 0.1 * k, 0.045, 0.04 * k)), S['WoodDark'], L['none'], 0.55)
    long_coat = coat[1]
    prof = [(0.46 * k if not long_coat else 0.3 * k, 0.0), (0.48 * k if not long_coat else 0.32 * k, 0.2 * k), (0.62 * k, 0.235 * k), (0.86 * k, 0.245 * k),
            (1.03 * k, 0.21 * k), (1.11 * k, 0.13 * k), (1.14 * k, 0.0)]
    n0 = len(g.F)
    add_lathe(g, (0, 0, 0), prof, coat[0], L['Cloth'], 0.6, sides=12, cap0=True)
    for i in range(n0, len(g.F)):
        g.TINT[i] = ct
    head_y = 1.33 * k + (0.04 if kid else 0.0)
    hr = 0.2 if not kid else 0.17
    g.add(xform(blob(hr, r, (1.0, 1.05, 0.98), subdiv=2, rough=0.02, flat=-2), (0, head_y, 0.0)), sk_slot, L['none'], 0.7, None, sk_tint)
    for s_ in (-1, 1):
        g.add(xform(blob(0.024, r, (1, 1.25, 0.6), subdiv=1, rough=0.0, flat=-2), (s_ * 0.07, head_y + 0.03, hr * 0.93)), S['WoodDark'], L['none'], 0.7, None, 0.35)
        sh = Vector((s_ * 0.24 * k, 1.0 * k, 0.0))
        hand = Vector((s_ * 0.3 * k, 0.6 * k, 0.07 * k)) if not (extra and extra.get('arms_fwd')) else Vector((s_ * 0.2 * k, 0.82 * k, 0.32 * k))
        g.add(tube([sh, sh.lerp(hand, 0.5) + Vector((s_ * 0.03, 0, 0)), hand], 0.062 * k, 6), coat[0], L['Cloth'], 0.6, None, ct)
        g.add(xform(blob(0.058 * k, r, (1, 1, 1), subdiv=1, rough=0.02, flat=-2), hand), sk_slot, L['none'], 0.65, None, sk_tint)
    g.add(xform(blob(0.04, r, (1, 0.9, 1.1), subdiv=1, rough=0.0, flat=-2), (0, head_y - 0.03, hr * 0.98)), sk_slot, L['none'], 0.7, None, sk_tint * 0.92)
    hy = head_y + hr * 0.62
    if hat == 'souwester':      # oilskin rain hat: a dome and a wide brim, longer behind
        add_lathe(g, (0, hy - 0.06, 0), [(0, 0.2), (0.1, 0.19), (0.17, 0.12), (0.2, 0.0)], S['Reed'], L['Cloth'], 0.6, sides=12, cap0=True)
        g.add(xform(blob(0.33, r, (1.0, 0.08, 1.0), subdiv=2, rough=0.0, flat=-2), (0, hy - 0.05, -0.04)), S['Reed'], L['Cloth'], 0.6)
    elif hat == 'cap':          # flat cap with a peak
        g.add(xform(blob(0.21, r, (1.0, 0.42, 1.1), subdiv=2, rough=0.0, flat=-0.2), (0, hy, -0.01)), extra.get('cap', S['Dock']) if extra else S['Dock'], L['Cloth'], 0.6)
        g.add(xform(blob(0.13, r, (1.0, 0.18, 0.8), subdiv=1, rough=0.0, flat=-2), (0, hy - 0.02, 0.17)), extra.get('cap', S['Dock']) if extra else S['Dock'], L['Cloth'], 0.6)
    elif hat == 'beanie':
        add_lathe(g, (0, hy - 0.1, 0), [(0, 0.205), (0.08, 0.205), (0.18, 0.16), (0.25, 0.0)], S['Roof'], L['Cloth'], 0.6, sides=12, cap0=True)
        g.add(xform(blob(0.05, r, (1, 1, 1), subdiv=1, rough=0.1, flat=-2), (0, hy + 0.16, 0)), S2['Snow'], L['Cloth'], 0.6)
    elif hat == 'scarf':        # headscarf: a cap over the crown, knotted under the chin
        g.add(xform(blob(0.215, r, (1.0, 0.75, 1.0), subdiv=2, rough=0.0, flat=-0.1), (0, head_y + 0.06, -0.02)), S['Accent'], L['Cloth'], 0.6)
    elif hat == 'fedora':
        n0 = len(g.F) + 1
        add_lathe(g, (0, hy - 0.07, 0), [(0, 0.19), (0.12, 0.18), (0.17, 0.14), (0.18, 0.0)], S['Rock'], L['Cloth'], 0.6, sides=12, cap0=True)
        g.add(xform(blob(0.3, r, (1.0, 0.07, 1.0), subdiv=2, rough=0.0, flat=-2), (0, hy - 0.06, 0)), S['Rock'], L['Cloth'], 0.6)
        add_lathe(g, (0, hy - 0.05, 0), [(0, 0.193), (0.045, 0.193), (0.046, 0.0)], S['Roof'], L['Cloth'], 0.6, sides=12)
        for i in range(n0 - 1, len(g.F)):
            g.TINT[i] = 0.4
    elif hat == 'straw':
        add_lathe(g, (0, hy - 0.06, 0), [(0, 0.17), (0.12, 0.16), (0.16, 0.0)], S['Sand'], L['Cloth'], 0.6, sides=12, cap0=True)
        g.add(xform(blob(0.36, r, (1.0, 0.06, 1.0), subdiv=2, rough=0.0, flat=-2), (0, hy - 0.05, 0)), S['Sand'], L['Cloth'], 0.6)
        add_lathe(g, (0, hy - 0.04, 0), [(0, 0.172), (0.05, 0.172), (0.051, 0.0)], S['Roof'], L['Cloth'], 0.6, sides=12)
    if extra and extra.get('apron'):
        g.add(xform(rbox((0.3, 0.5, 0.03), 0.02, 1), (0, 0.62, 0.235)), S2['Snow'], L['Cloth'], 0.65)
    if extra and extra.get('rod'):
        g.add(tube([Vector((0.31, 0.62, 0.1)), Vector((0.5, 1.9, 0.9)), Vector((0.62, 2.6, 1.55))], 0.014, 4), S['WoodDark'], L['none'], 0.5)
    if extra and extra.get('crate'):
        add_box(g, (0, 0.86, 0.36), (0.5, 0.32, 0.36), 0.03, S['Wood'], L['Wood'], 0.5)
        for f in range(3):
            g.add(xform(blob(0.07, r, (2.2, 0.7, 0.9), subdiv=1, rough=0.05), (-0.13 + 0.13 * f, 1.04, 0.36)), [S2['Snow'], S['Rock'], S['Accent']][f], L['none'], 0.65)

def build_villagers():
    out = []
    r = rng2('villagers')
    kinds = [
        # model, coat (slot, long), trousers, skin, hat, extra, placements (name, x, z, yaw[, path m])
        ('Fisher', (S['Reed'], True), S['WoodDark'], SKIN[0], 'souwester', dict(rod=True), 'Villager',
         [('Fisher_Beach', -3.4, -49.6, 352.0), ('Fisher_Lagoon', -46.0, -46.4, 25.0)]),
        ('Stallkeeper', (S['Roof'], True), S['Dock'], SKIN[1], 'scarf', dict(apron=True), 'Villager',
         [('Stallkeeper_Fish', -7.35, -82.15, 192.0), ('Stallkeeper_Produce', -2.48, -81.6, 176.0)]),
        ('Dockhand', (S['Rock'], False), S['Dock'], SKIN[2], 'beanie', dict(crate=True, arms_fwd=True), 'Walker',
         [('Dockhand_Breakwater', 29.2, -50.5, 0.0, 7.0), ('Dockhand_Jetty', 55.7, -49.4, 0.0, 6.5)]),
        ('Walker', (S2['PineDark'], False), S['Rock'], SKIN[3], 'cap', dict(cap=S['WoodDark']), 'Walker',
         [('Walker_BaitLane', -21.0, -61.05, 270.0, 6.5), ('Walker_UpperLane', 1.0, -88.4, 270.0, 6.0), ('Walker_Plateau', 8.4, -61.5, 90.0, 5.0)]),
        ('Kid', (S['Accent'], False), S['Foliage'], SKIN[0], 'cap', dict(cap=S['Roof']), 'Villager',
         [('Kid_Stairs', 6.4, -72.6, 200.0), ('Kid_Beach', 4.2, -48.6, 30.0)]),
        ('Gardener', (S['Foliage'], False), S['Dock'], SKIN[2], 'straw', None, 'Villager',
         [('Gardener_Laundry', -47.0, -79.6, 160.0), ('Gardener_Square', 4.4, -90.6, 230.0)]),
        # the mafia (spec R76: the crew is on the run): black-suited men in fedoras - two by the sedans on the hill track, one watching the
        # tug from the head of stair D
        ('Thug', (S['Rock'], True), S['Rock'], SKIN[1], 'fedora', dict(tint=0.22), 'Villager',
         [('Thug_Sedan', 11.6, -121.2, 6.0), ('Thug_Sedan2', 20.2, -108.6, 350.0), ('Thug_StairD', 36.4, -80.6, 20.0)]),
    ]
    for model, coat, trousers, skin, hat, extra, anim, places in kinds:
        g = G2('Harbour_Detail_Anim_' + model, 'detail', local=True, rays=8, anim='idle' if anim == 'Villager' else 'walk',
               note='Round 4: a toon villager (%s; origin between the feet, facing +z). %s. Visual only.' % (model.lower(),
                    'Idles: shifts its weight and looks about' if anim == 'Villager' else 'Strolls back and forth along its facing (radius = the path, m)'))
        villager_body(g, r, coat, trousers, skin, hat, kid=(model == 'Kid'), extra=extra)
        for pl in places:
            name, x, z, yaw = pl[:4]
            y = BREAKWATER_TOP + BREAKWATER_SETTS_LIFT if 'Breakwater' in name else (JETTY[4] if 'Jetty' in name else land_y(x, z))
            p = g.place(name, (x, y, z), yaw)
            if len(pl) > 4:
                p['radius'] = pl[4]
        out.append(g)
    out[-1].extra['smoke_points'] = [dict(name='CigaretteThug', position=[36.55, round(land_y(36.4, -80.6) + 1.3, 2), -80.35])]
    # the dog (a tan mutt with a dark ear) - strolls a few metres by the bait shop; and on the beach by the pier root
    g = G2('Harbour_Detail_Anim_Dog', 'detail', local=True, rays=8, anim='walk',
           note='Round 4: a toon dog (0.55 m at the shoulder; origin between its feet, facing +z). Trots back and forth (radius = the path, m).')
    g.add(xform(blob(0.26, r, (0.75, 0.72, 1.35), subdiv=2, rough=0.03, flat=-2), (0, 0.5, 0)), S['Wood'], L['none'], 0.6)
    g.add(xform(blob(0.16, r, (0.95, 0.9, 1.1), subdiv=2, rough=0.02, flat=-2), (0, 0.72, 0.33)), S['Wood'], L['none'], 0.6)
    g.add(xform(blob(0.08, r, (0.8, 0.7, 1.2), subdiv=1, rough=0.02, flat=-2), (0, 0.67, 0.48)), S['Sand'], L['none'], 0.6)
    g.add(xform(blob(0.03, r, (1, 1, 1), subdiv=1, rough=0.0, flat=-2), (0, 0.7, 0.57)), S['WoodDark'], L['none'], 0.6, None, 0.3)
    for s_ in (-1, 1):
        g.add(xform(blob(0.07, r, (0.5, 1.2, 0.9), subdiv=1, rough=0.02, flat=-2), (s_ * 0.12, 0.8, 0.3), rz(s_ * 25)), S['WoodDark'] if s_ < 0 else S['Wood'], L['none'], 0.6)
        g.add(xform(blob(0.02, r, (1, 1, 1), subdiv=1, rough=0.0, flat=-2), (s_ * 0.06, 0.77, 0.47)), S['WoodDark'], L['none'], 0.6, None, 0.3)
        for fz in (-0.2, 0.22):
            g.add(tube([Vector((s_ * 0.1, 0.42, fz)), Vector((s_ * 0.1, 0.02, fz + 0.02))], 0.045, 6), S['Wood'], L['none'], 0.6)
    g.add(tube([Vector((0, 0.56, -0.3)), Vector((0, 0.72, -0.42)), Vector((0, 0.86, -0.4))], 0.035, 5), S['Wood'], L['none'], 0.6)
    g.add(tube([Vector((-0.1, 0.62, 0.22)), Vector((0.0, 0.6, 0.27)), Vector((0.1, 0.62, 0.22))], 0.025, 5), S['Roof'], L['none'], 0.6)
    for name, x, z, yaw, path in (('Dog_BaitShop', -16.6, -60.4, 270.0, 3.5), ('Dog_Beach', 6.6, -47.8, 70.0, 4.0)):
        g.place(name, (x, land_y(x, z), z), yaw)['radius'] = path
    out.append(g)
    # hens (white with a red comb) between the net shed and the smokehouse, and a goat on the knoll
    g = G2('Harbour_Detail_Anim_Hen', 'detail', local=True, rays=8, anim='gull',
           note='Round 4: a toon hen (0.35 m; origin between its feet). Pecks and looks about (the perched-gull motion).')
    g.add(xform(blob(0.13, r, (0.85, 0.85, 1.2), subdiv=2, rough=0.03, flat=-2), (0, 0.2, 0)), S2['Snow'], L['none'], 0.65)
    g.add(xform(blob(0.07, r, (0.9, 1.0, 1.0), subdiv=2, rough=0.02, flat=-2), (0, 0.33, 0.1)), S2['Snow'], L['none'], 0.65)
    g.add(xform(blob(0.035, r, (0.4, 1.0, 1.2), subdiv=1, rough=0.02, flat=-2), (0, 0.41, 0.1)), S['Roof'], L['none'], 0.65)
    g.add(xform(blob(0.025, r, (0.8, 0.6, 1.4), subdiv=1, rough=0.0, flat=-2), (0, 0.33, 0.17)), S['Accent'], L['none'], 0.65)
    g.add(xform(blob(0.07, r, (0.5, 1.1, 0.9), subdiv=1, rough=0.03, flat=-2), (0, 0.27, -0.14), rx(-30)), S2['Snow'], L['none'], 0.65)
    for s_ in (-1, 1):
        g.add(tube([Vector((s_ * 0.04, 0.12, 0)), Vector((s_ * 0.04, 0.0, 0.02))], 0.012, 4), S['Accent'], L['none'], 0.6)
    for k, (x, z, yaw) in enumerate(((-34.6, -64.0, 30.0), (-35.3, -64.9, 200.0), (-33.9, -65.4, 120.0), (-34.8, -66.0, 300.0), (-33.6, -64.2, 80.0))):
        g.place('Hen_%d' % k, (x, land_y(x, z), z), yaw)
    out.append(g)
    g = G2('Harbour_Detail_Anim_Goat', 'detail', local=True, rays=8, anim='gull',
           note='Round 4: a toon goat (0.8 m; origin between its feet) on the knoll by the creek. Looks about.')
    g.add(xform(blob(0.3, r, (0.75, 0.68, 1.3), subdiv=2, rough=0.04, flat=-2), (0, 0.62, 0)), S2['Snow'], L['none'], 0.6)
    g.add(xform(blob(0.15, r, (0.75, 0.85, 1.3), subdiv=2, rough=0.03, flat=-2), (0, 0.9, 0.38), rx(25)), S2['Snow'], L['none'], 0.6)
    for s_ in (-1, 1):
        g.add(tube([Vector((s_ * 0.05, 1.0, 0.35)), Vector((s_ * 0.08, 1.14, 0.26)), Vector((s_ * 0.1, 1.12, 0.16))], 0.025, 4), S['WoodDark'], L['none'], 0.55)
        g.add(xform(blob(0.025, r, (1, 1, 1), subdiv=1, rough=0.0, flat=-2), (s_ * 0.07, 0.95, 0.47)), S['WoodDark'], L['none'], 0.6, None, 0.3)
        for fz in (-0.24, 0.24):
            g.add(tube([Vector((s_ * 0.13, 0.5, fz)), Vector((s_ * 0.13, 0.02, fz))], 0.045, 6), S2['Snow'], L['none'], 0.6, None, 0.9)
    g.add(tube([Vector((0, 0.82, 0.48)), Vector((0, 0.7, 0.5))], 0.03, 4), S2['Snow'], L['none'], 0.6, None, 0.85)
    g.place('Goat_Knoll', (-46.6, land_y(-46.6, -55.3), -55.3), 140.0)
    out.append(g)
    # the valley (critics: "the red farm and windmill float with no yard; the town does not continue up the valley"): a sheep flock on the
    # pasture between the farm track and the windmill, haystacks by the barn, a hay cart at the track bend
    g = G2('Harbour_Detail_Farm', 'detail', rays=6,
           note='Round 4: the valley farm life - a flock of sheep, haystacks by the barn, a hay cart. Backdrop, no colliders.')
    for k in range(9):
        x, z = -34.0 + r.uniform(-7.0, 7.0), -131.0 + r.uniform(-4.0, 4.0)
        y = land_y(x, z)
        yaw = r.uniform(0, 360)
        rot = ry(yaw)
        P = lambda a, b, c: Vector((x, y, z)) + rot @ Vector((a, b, c))
        g.add(xform(blob(0.42, r, (0.85, 0.7, 1.2), subdiv=2, rough=0.12, flat=-2), P(0, 0.62, 0)), S2['Snow'], L['Cloth'], 0.6)
        g.add(xform(blob(0.16, r, (0.8, 0.9, 1.2), subdiv=1, rough=0.02, flat=-2), P(0, 0.72, 0.5)), S['WoodDark'], L['none'], 0.6)
        for sx, sz in ((-0.15, -0.25), (0.15, -0.25), (-0.15, 0.25), (0.15, 0.25)):
            g.add(tube([P(sx, 0.4, sz), P(sx, 0.0, sz)], 0.04, 4), S['WoodDark'], L['none'], 0.6)
    for k, (dx, dz) in enumerate(((5.0, 3.5), (6.8, 1.2), (4.0, 6.0))):
        x, z = FARM[1][0] + dx, FARM[1][1] + dz
        y = land_y(x, z)
        add_lathe(g, (x, y - 0.1, z), [(0, 1.15), (0.9, 1.2), (1.6, 0.85), (2.0, 0.0)], S['Reed'], L['Grass'], 0.6, sides=12, cap0=True)
    return out + [g]

def build_round4():
    return build_glen() + build_waterfall() + build_reef() + build_east_ledge() + build_villagers()

STAND_POINTS_R1.append((LEDGE_STAND[0], ledge_stand_y(), LEDGE_STAND[1], LEDGE_STAND[2]))   # stand point 14 (round 4): the East Ledge

if __name__ == '__main__':
    main()
