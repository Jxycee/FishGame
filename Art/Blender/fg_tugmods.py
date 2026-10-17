# fg_tugmods.py (harbour-tug-upgrades, 2026-10-17): bolt-on upgrade modules for the crew tug "Lucky Break", the moving home (design 3.7).
# Run in an EMPTY Blender file:  FG_ROOT=<repo> blender -b --factory-startup --python Art/Blender/fg_tugmods.py
# It imports the harbour generator (fg_harbour.py, untouched: the import has no side effects) for the tug itself, the geometry helpers, the
# palette slots S / texture layers L, the toon preview and the FBX writer, builds every module, bakes AO against the tug and the other
# modules, exports one FBX per module to Art/Export/TugUpgrades, writes tugupgrades_manifest.json (mounts, footprints, clearance report)
# and renders the previews to Art/Previews/TugUpgrades.
#
# Coordinates: TUG-LOCAL Unity metres (origin at the tug's waterline centre, +z bow, +x starboard, y up; TUG_WORLD places the tug). Every
# FBX object is authored in tug-local space with an identity transform at the tug's origin, so a module is installed by parenting it to the
# tug at (0,0,0). Parts that move carry a `pivot` (tug-local) in the manifest: make an empty at the pivot, parent the mesh under it with
# localPosition = -pivot, animate the empty. The dinghy is the one free body: its own origin is its keel centre (placement in the manifest).
# Encoding is the harbour's (see the fg_harbour.py header): UV0 metres, UV1 = (palette slot, texture layer), vertex colour 0.5 x AO, smooth
# normals, no materials exported. Deterministic: every random draw comes from rng(stream) = Random(SEED ^ crc32(stream)).
import bpy, bmesh, math, json, os, sys, zlib, random, importlib.util
from mathutils import Vector, Matrix

def _load_harbour():
    root = os.environ.get('FG_ROOT') or os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')) if '__file__' in globals() else os.getcwd()
    p = os.path.join(root, 'Art', 'Blender', 'fg_harbour.py')
    sp = importlib.util.spec_from_file_location('fg_harbour', p)
    m = importlib.util.module_from_spec(sp)
    sys.modules['fg_harbour'] = m
    sp.loader.exec_module(m)      # defines everything; main() only runs under __main__ (the module name here is fg_harbour)
    return m, root

H, ROOT = _load_harbour()
S, L, tb, rbox, lathe, tube, blob, xform, mirror_fix = H.S, H.L, H.tb, H.rbox, H.lathe, H.tube, H.blob, H.xform, H.mirror_fix
add_lathe, ry, rx, rz, clamp, frange = H.add_lathe, H.ry, H.rx, H.rz, H.clamp, H.frange
TUG_DECK, TUG_FORE, TUG_HOUSE, TUG_HOUSE_TOP, TUG_WH, TUG_WH_TOP = H.TUG_DECK, H.TUG_FORE, H.TUG_HOUSE, H.TUG_HOUSE_TOP, H.TUG_WH, H.TUG_WH_TOP
tug_w, tug_sheer, tug_keel, tug_outline = H.tug_w, H.tug_sheer, H.tug_keel, H.tug_outline

EXPORT_DIR = os.path.join(ROOT, 'Art', 'Export', 'TugUpgrades')
PREVIEW_DIR = os.path.join(ROOT, 'Art', 'Previews', 'TugUpgrades')
MANIFEST_PATH = os.path.join(EXPORT_DIR, 'tugupgrades_manifest.json')
SEED = 0x7A6B1E55

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(('tugmods.' + stream).encode('utf-8')))

D, FD, HT, WT = TUG_DECK, TUG_FORE, TUG_HOUSE_TOP, TUG_WH_TOP      # main deck 1.25, foredeck 1.55, upper deck 3.6, wheelhouse roof 5.85
HZ0, HZ1, HHW = TUG_HOUSE                                           # deckhouse z -2.6..3.4, half width 1.55
WZ0, WZ1, WHW = TUG_WH                                              # wheelhouse z 0.5..3.2, half width 1.35
WALL = 0.12                                                         # deckhouse wall thickness (inner faces at +-1.43, z -2.48 / 3.28)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Module group: an H.G2 (tug-local, not "local" in the harbour sense so the AO bake sees the tug) plus the manifest facts.
# ---------------------------------------------------------------------------------------------------------------------------------------
class M(H.G2):
    def __init__(self, name, mount, note, rays=20, pivot=None, anim=None, material=None, state=None, requires=None, replaces=None, **extra):
        H.G2.__init__(self, name, 'core', rays=rays, anim=anim, material=material, note=note)
        self.mount, self.pivot, self.state = mount, pivot, state
        self.solids = []            # (label, lo, hi) boxes used by the clearance check (per part, so L-shaped modules do not collide falsely)
        self.mod = dict(requires=requires or [], replaces=replaces or [])
        self.mod.update(extra)

    def sol(self, label, mark=True):
        # a solid for the clearance check from the geometry added since the last mark
        vs = self.V[self._mark:]
        if not vs:
            return
        lo = [min(v[i] for v in vs) for i in range(3)]
        hi = [max(v[i] for v in vs) for i in range(3)]
        self.solids.append((label, lo, hi))
        self._mark = len(self.V)

    def box_item(self, name, c, size, **extra):
        # a box collider the Unity builder places as given (tug-local); also a clearance solid
        d = dict(name=name, collider='box', position=[round(v, 3) for v in c], footprint=[round(v, 3) for v in size],
                 bounds_min=[round(c[i] - size[i] / 2, 3) for i in range(3)], bounds_max=[round(c[i] + size[i] / 2, 3) for i in range(3)])
        d.update(extra)
        self.items.append(d)
        return d

def P(*a):
    return Vector(a)

def seg(g, a, b, r, slot, layer=L['none'], ao=0.5, sides=6, caps=True):
    g.add(tube([Vector(a), Vector(b)], r, sides, caps=caps), slot, layer, ao)

def cube_geo(size):
    # a plain box, 12 triangles, flat normals: for thin plates, planks, tape and anything too small to show a bevel
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    V, N, F, UV = [], [], [], []
    for axis in range(3):
        for sgn in (-1, 1):
            o = [i for i in range(3) if i != axis]
            quad = []
            for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = [0.0, 0.0, 0.0]
                p[axis] = sgn * (sx, sy, sz)[axis]
                p[o[0]] = a * (sx, sy, sz)[o[0]]
                p[o[1]] = b * (sx, sy, sz)[o[1]]
                quad.append(Vector(p))
            n = Vector([sgn if i == axis else 0 for i in range(3)])
            base = len(V)
            V += quad; N += [n] * 4
            f = [base, base + 1, base + 2, base + 3]
            if (V[base + 1] - V[base]).cross(V[base + 2] - V[base]).dot(n) < 0:
                f = list(reversed(f))
            F.append(f)
            UV.append([H.box_uv(V[k], n, size) for k in f])
    return V, N, F, UV

def bx(g, c, size, r, slot, layer=L['none'], ao=0.5, rot=None, tint=1.0):
    geo = cube_geo(size) if (r <= 0 or min(size) < 0.03) else rbox(size, r, 1)
    g.add(xform(geo, c, rot), slot, layer, ao, None, tint)

def cyl(g, c, h, r, slot, layer=L['none'], ao=0.5, sides=10, rot=None, cap1=True, cap0=False, taper=None):
    add_lathe(g, c, [(0.0, r), (h, r if taper is None else taper)], slot, layer, ao, sides=sides, rot=rot, cap0=cap0, cap1=cap1)

def glass(g, c, size, rot=None):
    bx(g, c, size, min(0.01, min(size) * 0.3), S['Rock'], L['none'], 0.5, rot, tint=0.45)      # dark glass: Rock slot, darkened (the tug's windows)

def screw(g, c, n=(0, 0, 1), r=0.014, slot=None):
    nn = Vector(n).normalized()
    rot = Matrix.Rotation(0, 3, 'Y')
    q = Vector((0, 1, 0)).rotation_difference(nn)
    add_lathe(g, c, [(0.0, r), (0.012, r * 0.8)], slot if slot is not None else S['Rock'], L['none'], 0.5, sides=5, rot=q.to_matrix(), cap1=True)

def text(g, body, size, centre, rot, slot, depth=0.004):
    # Raised lettering on a face whose outward normal is rot @ +z (fg_harbour.sign_text's mapping), but ONLY the front faces: the back and the
    # side strips of a 4 mm extrusion are never seen, and cost two thirds of the triangles.
    V, N, F, UV = H.text_geo(body, size, depth)
    keep = [i for i, f in enumerate(F) if N[f[0]].z > 0.5]
    used = sorted(set(k for i in keep for k in F[i]))
    idx = {k: j for j, k in enumerate(used)}
    V2 = [V[k] for k in used]
    N2 = [N[k] for k in used]
    F2 = [[idx[k] for k in F[i]] for i in keep]
    UV2 = [UV[i] for i in keep]
    Mr = Matrix(((-1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)))
    R = rot @ Mr
    g.add(mirror_fix(([Vector(centre) + R @ v for v in V2], [R @ n for n in N2], F2, UV2)), slot, L['none'], 0.6)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Small shared builders
# ---------------------------------------------------------------------------------------------------------------------------------------
def basis(ex, up=(0.0, 1.0, 0.0)):
    # rotation whose local +x is ex (columns ex, ey, ez)
    ex = Vector(ex).normalized()
    ez = ex.cross(Vector(up))
    if ez.length < 1e-6:
        ez = ex.cross(Vector((0.0, 0.0, 1.0)))
    ez.normalize()
    ey = ez.cross(ex).normalized()
    return Matrix(((ex.x, ey.x, ez.x), (ex.y, ey.y, ez.y), (ex.z, ey.z, ez.z)))

def axis_rot(d):
    return Vector((0.0, 1.0, 0.0)).rotation_difference(Vector(d).normalized()).to_matrix()

def bar(g, a, b, w, h, r, slot, layer=L['none'], ao=0.5, tint=1.0):
    # a rounded bar from a to b, w wide and h tall (local y follows +y as far as the direction allows)
    a, b = Vector(a), Vector(b)
    d = b - a
    bx(g, (a + b) / 2, (d.length, h, w), r, slot, layer, ao, basis(d), tint)

def lat(g, base, d, prof, slot, layer=L['none'], ao=0.5, sides=10, cap0=False, cap1=True, ring_slots=None):
    # a lathe whose axis points along d, starting at base
    add_lathe(g, base, prof, slot, layer, ao, sides=sides, rot=axis_rot(d), cap0=cap0, cap1=cap1, ring_slots=ring_slots)

def tape(g, c, size, rot=None):
    # a strip of grey duct tape (the Rock slot, cloth layer): the worn-and-fixed look of the tug
    bx(g, c, size, 0.002, S['Rock'], L['Cloth'], 0.6, rot)

def up_text(normal_yaw_deg, tilt=0.0):
    return ry(normal_yaw_deg) @ rx(tilt)

def rope_loop(g, c, r, rad=0.012, slot=None, n=14, rot=None):
    pts = [Vector((r * math.cos(2 * math.pi * i / n), 0.0, r * math.sin(2 * math.pi * i / n))) for i in range(n)]
    g.add(xform(tube(pts, rad, 4, caps=False, closed=True), c, rot), slot if slot is not None else S['Sand'], L['Cloth'], 0.55)

def disc(g, c, r, h, slot, layer=L['none'], ao=0.5, sides=12, rot=None):
    add_lathe(g, c, [(0.0, r), (h, r)], slot, layer, ao, sides=sides, rot=rot, cap0=True, cap1=True)

def sphere_part(g, c, rad, scale, slot, layer=L['none'], ao=0.55, rot=None):
    r = rng('sph%.2f%.2f%.2f' % tuple(c))
    geo = blob(rad, r, scale, subdiv=2, rough=0.0, flat=-2)
    g.add(xform(geo, c, rot), slot, layer, ao)

WATER_MAT = 'M_Water_Tank (the creek water material without the shore-foam band; builder: translucent, scrolling ripples, no toon lighting)'

def water_slab(name, x0, x1, z0, z1, y, mount, note, depth=0.04, ripple=0.006, nx=9, nz=8, **extra):
    # A calm water surface (a thin closed slab) as its own object: the builder gives it the water material and can animate / hide it.
    g = M(name, mount, note, rays=0, material=WATER_MAT, **extra)
    V, N, F, UV = [], [], [], []
    def hy(x, z):
        return y + ripple * math.sin(7.0 * x + 1.3) * math.cos(6.0 * z + 0.4)
    for j in range(nz + 1):
        for i in range(nx + 1):
            x = x0 + (x1 - x0) * i / nx
            z = z0 + (z1 - z0) * j / nz
            e = 0.01
            gx = (hy(x + e, z) - hy(x - e, z)) / (2 * e)
            gz = (hy(x, z + e) - hy(x, z - e)) / (2 * e)
            V.append(Vector((x, hy(x, z), z))); N.append(Vector((-gx, 1.0, -gz)).normalized())
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            q = [a, a + 1, a + nx + 2, a + nx + 1]
            F.append(q); UV.append([(V[k].x, V[k].z) for k in q])
    # flip so faces point up (the slab's top)
    F = [list(reversed(f)) if (V[f[1]] - V[f[0]]).cross(V[f[2]] - V[f[0]]).y < 0 else f for f in F]
    g.add((V, N, F, UV), S['Foliage'], L['none'], 1.0)
    g.mark()
    # the skirt: a rim down the four sides so the slab is a closed body (and reads as water depth from the side)
    for (ax, az, bx_, bz) in ((x0, z0, x1, z0), (x1, z0, x1, z1), (x1, z1, x0, z1), (x0, z1, x0, z0)):
        d = Vector((bx_ - ax, 0, bz - az))
        n = Vector((d.z, 0, -d.x)).normalized()
        pa, pb = Vector((ax, y, az)), Vector((bx_, y, bz))
        V2 = [pa, pb, pb - Vector((0, depth, 0)), pa - Vector((0, depth, 0))]
        g.add((V2, [n] * 4, [[0, 1, 2, 3]], [[(0, 0), (1, 0), (1, 1), (0, 1)]]), S['Foliage'], L['none'], 1.0)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# AFT DECK: live well, fighting chair + rod gimbal, davit + hook block, tow fairlead
# Aft deck free space (checked in the clearance report): ice chests (-0.95,-4.55) (0.65,-4.55) (1.2,-6.3) (1.8,-5.05), stern fishing spot
# (0,-6.7), gangway gap on the port quarter, the H-bitt at z -3.7, the crew's gear. The chair, the live well and the davit foot take the free
# floor between them.
# ---------------------------------------------------------------------------------------------------------------------------------------
LW_C = (0.92, -5.45)           # live well centre (x, z)
LW_SIZE = (0.74, 0.64, 0.34)   # outer x, z, height above the deck
CH_C = (0.0, -5.4)             # fighting chair pedestal (x, z); the angler faces aft (-z)
DV_C = (1.05, -6.95)           # davit pedestal

def build_livewell():
    cx, cz = LW_C
    W_, Dp, Hh = LW_SIZE
    t = 0.06
    g = M('Tug_LiveWell', dict(position=[cx, D, cz], yaw_deg=0.0, surface='main deck, aft starboard of the chair', footprint_xz=[cx - W_ / 2, cz - Dp / 2, cx + W_ / 2, cz + Dp / 2]),
          'Raised live well: a planked fish box on the aft deck (3 planks, one repainted orange: the mismatched replacement) with its own hinged lid '
          '(Tug_LiveWell_Lid) and its own water surface (Tug_LiveWell_Water). Box top at deck + 0.34; water at deck + 0.26.',
          upgrade='live_well', anim='static (lid and water are separate objects)')
    ph = Hh / 3.0
    rows = [(S['Wood'], S['Wood'], S['Dock']), (S['Dock'], S['Dock'], S['Accent']), (S['Wood'], S['Reed'], S['Wood'])]    # per plank: (long sides, fwd wall, aft wall)
    for k in range(3):
        y = D + ph * (k + 0.5)
        ls, fs, as_ = rows[k]
        bx(g, (cx, y, cz + Dp / 2 - t / 2), (W_, ph - 0.01, t), 0.0, fs, L['Wood'], 0.5)          # forward wall (+z)
        bx(g, (cx, y, cz - Dp / 2 + t / 2), (W_, ph - 0.01, t), 0.0, as_, L['Wood'], 0.5)         # aft wall (-z)
        for sx in (-1, 1):
            bx(g, (cx + sx * (W_ / 2 - t / 2), y, cz), (t, ph - 0.01, Dp - 2 * t - 0.01), 0.0, ls, L['Wood'], 0.5)
    for sx in (-1, 1):                                       # corner posts with bolt heads
        for sz in (-1, 1):
            bx(g, (cx + sx * (W_ / 2 - 0.025), D + Hh / 2, cz + sz * (Dp / 2 - 0.025)), (0.05, Hh + 0.02, 0.05), 0.012, S['WoodDark'], L['Wood'], 0.45)
    # hasp and padlock on the aft wall
    bx(g, (cx, D + Hh - 0.07, cz - Dp / 2 - 0.012), (0.1, 0.12, 0.02), 0.006, S['Rock'], L['none'], 0.5)
    add_lathe(g, (cx, D + Hh - 0.17, cz - Dp / 2 - 0.03), [(0.0, 0.03), (0.07, 0.03)], S['Accent'], L['none'], 0.5, sides=8, rot=rx(90), cap0=True, cap1=True)
    # hose bib: the inlet pipe up the aft wall and over the rim into the water
    ix = cx + 0.15
    g.add(tube([P(ix, D, cz - Dp / 2 - 0.035), P(ix, D + Hh + 0.07, cz - Dp / 2 - 0.035), P(ix, D + Hh + 0.07, cz - Dp / 2 + 0.12), P(ix, D + Hh - 0.07, cz - Dp / 2 + 0.2)],
               0.02, 6), S['Rock'], L['none'], 0.5)
    for yy in (0.1, 0.24):
        bar(g, P(ix - 0.03, D + yy, cz - Dp / 2 - 0.045), P(ix + 0.03, D + yy, cz - Dp / 2 - 0.045), 0.03, 0.025, 0.008, S['Accent'], L['none'], 0.5)
    # the aerator: a small orange pump on the forward wall, an air hose looping into the water
    bx(g, (cx - 0.18, D + 0.2, cz + Dp / 2 + 0.05), (0.14, 0.1, 0.09), 0.02, S['Accent'], L['none'], 0.55)
    bx(g, (cx - 0.18, D + 0.265, cz + Dp / 2 + 0.05), (0.15, 0.03, 0.1), 0.01, S['Rock'], L['none'], 0.5)
    g.add(tube([P(cx - 0.18, D + 0.25, cz + Dp / 2 + 0.01), P(cx - 0.18, D + Hh + 0.06, cz + Dp / 2 - 0.0), P(cx - 0.17, D + Hh + 0.02, cz + Dp / 2 - 0.12), P(cx - 0.12, D + 0.28, cz + Dp / 2 - 0.2)],
               0.01, 4), S['WoodDark'], L['none'], 0.5)
    # hinge plates on the forward top edge (the lid pivots here)
    for sx in (-0.26, 0.26):
        bx(g, (cx + sx, D + Hh - 0.01, cz + Dp / 2 + 0.03), (0.1, 0.03, 0.06), 0.008, S['Rock'], L['none'], 0.5)
    # lettering on the long side facing the chair
    text(g, 'LIVE WELL', 0.075, (cx - W_ / 2 - 0.004, D + 0.2, cz + 0.02), ry(270), S['Sand'])
    text(g, 'NOT A SINK', 0.045, (cx - W_ / 2 - 0.004, D + 0.1, cz + 0.02), ry(270), S['Sand'])
    g.sol('box')
    g.box_item('LiveWell_Box', (cx, D + Hh / 2, cz), (W_, Hh, Dp))
    g.mod['water_surface_y'] = round(D + 0.26, 3)
    g.mod['lid_pivot_hint'] = 'see Tug_LiveWell_Lid.pivot'
    return g

def build_livewell_lid():
    cx, cz = LW_C
    W_, Dp, Hh = LW_SIZE
    pv = (cx, D + Hh + 0.025, cz + Dp / 2 + 0.03)       # hinge axis (along x) on the forward top edge
    g = M('Tug_LiveWell_Lid', dict(position=list(pv), yaw_deg=0.0, surface='top of Tug_LiveWell'),
          'Hinged lid of the live well (closed pose). Hinge axis along x at the forward top edge; opens by rotating so the aft edge rises and swings '
          'over past the vertical (max ~100 degrees). Planked, one board replaced in a different wood, rope handle on the aft edge.',
          pivot=dict(position=[round(v, 3) for v in pv], axis='x', open_angle_deg=100.0, note='aft edge goes up and over towards +z'),
          upgrade='live_well', requires=['Tug_LiveWell'], anim='hinge (open/close)')
    lw = W_ + 0.04
    n = 4
    bw = (Dp + 0.04) / n
    z0 = cz + Dp / 2 + 0.03
    for k in range(n):
        zc = z0 - bw * (k + 0.5) + 0.0
        zc = cz + Dp / 2 + 0.02 - bw * (k + 0.5)
        sl = [S['Wood'], S['Dock'], S['Wood'], S['Sand']][k]          # the pale board is the replacement
        bx(g, (cx, D + Hh + 0.0, zc), (lw, 0.04, bw - 0.008), 0.0, sl, L['Wood'], 0.5)
    for sx in (-0.26, 0.26):
        bx(g, (cx + sx, D + Hh + 0.04, cz), (0.06, 0.03, Dp - 0.06), 0.008, S['WoodDark'], L['Wood'], 0.45)
    # rope handle at the aft edge, a tape patch
    rope_loop(g, (cx, D + Hh + 0.045, cz - Dp / 2 - 0.0), 0.07, 0.011, rot=rx(70))
    tape(g, (cx - 0.13, D + Hh + 0.025, cz + 0.12), (0.12, 0.004, 0.05), ry(20))
    tape(g, (cx - 0.07, D + Hh + 0.025, cz + 0.1), (0.12, 0.004, 0.05), ry(-35))
    for sx in (-0.26, 0.26):          # hinge barrels
        add_lathe(g, (cx + sx - 0.05, D + Hh + 0.02, cz + Dp / 2 + 0.035), [(0.0, 0.015), (0.1, 0.015)], S['Rock'], L['none'], 0.5, sides=6, rot=rz(-90), cap0=True, cap1=True)
    g.sol('lid')
    th = math.radians(100.0)
    ez = pv[2] - (Dp + 0.04) * math.cos(th)                       # the aft edge after opening: up and over towards +z
    g.solids.append(('lid_open_envelope', [cx - lw / 2, pv[1], min(pv[2], ez) - 0.03], [cx + lw / 2, pv[1] + (Dp + 0.04) * math.sin(th) + 0.03, max(pv[2], ez) + 0.04]))
    g.mod['open_envelope'] = dict(min=[round(cx - lw / 2, 3), round(pv[1], 3), round(min(pv[2], ez) - 0.03, 3)], max=[round(cx + lw / 2, 3), round(pv[1] + (Dp + 0.04) * math.sin(th) + 0.03, 3), round(max(pv[2], ez) + 0.04, 3)])
    return g

def build_livewell_water():
    cx, cz = LW_C
    W_, Dp, Hh = LW_SIZE
    t = 0.06
    g = water_slab('Tug_LiveWell_Water', cx - W_ / 2 + t + 0.005, cx + W_ / 2 - t - 0.005, cz - Dp / 2 + t + 0.005, cz + Dp / 2 - t - 0.005, D + 0.26,
                   dict(position=[cx, D + 0.26, cz], yaw_deg=0.0, surface='inside Tug_LiveWell'),
                   'Water surface of the live well, its own object: a ripple grid at deck + 0.26 (0.04 skirt). Builder: water material, slow ripple; hide '
                   'when the well is empty; fish models swim just under it.', upgrade='live_well', requires=['Tug_LiveWell'], anim='ripple')
    return g

def build_chair():
    cx, cz = CH_C
    g = M('Tug_FightingChair', dict(position=[cx, D, cz], yaw_deg=180.0, surface='main deck, stern', seat_point=[cx, D + 0.7, cz], footprint_xz=[-0.33, -6.12, 0.33, -5.0]),
          'Fighting chair bolted to the aft deck, the angler faces the stern (yaw 180). Home-built: deck-plank frame, a steel pedestal, a red seat '
          'cushion with a duct-tape patch and an olive back cushion from another seat. The rod gimbal is a separate object (Tug_RodGimbal). '
          'Replaces the stern fishing spot: spot = seat_point, facing 180.',
          upgrade='fighting_chair', anim='static')
    # deck plate and pedestal
    bx(g, (cx, D + 0.02, cz), (0.62, 0.04, 0.62), 0.012, S['Rock'], L['none'], 0.55)
    for sx in (-1, 1):
        for sz in (-1, 1):
            screw(g, (cx + sx * 0.26, D + 0.04, cz + sz * 0.26), (0, 1, 0), 0.02)
    add_lathe(g, (cx, D + 0.04, cz), [(0.0, 0.11), (0.05, 0.09), (0.42, 0.09), (0.46, 0.13), (0.5, 0.13)], S['Rock'], L['none'], 0.5, sides=10, cap0=False, cap1=True)
    # seat: plank base, cushion, back
    bx(g, (cx, D + 0.54, cz), (0.52, 0.05, 0.5), 0.012, S['Dock'], L['Wood'], 0.5)
    bx(g, (cx, D + 0.61, cz - 0.01), (0.5, 0.1, 0.46), 0.04, S['Roof'], L['Cloth'], 0.55)
    tape(g, (cx + 0.1, D + 0.664, cz - 0.07), (0.17, 0.004, 0.07), ry(25))
    tape(g, (cx + 0.14, D + 0.664, cz - 0.07), (0.17, 0.004, 0.07), ry(-35))
    bx(g, (cx, D + 1.0, cz + 0.27), (0.5, 0.06, 0.62), 0.012, S['Dock'], L['Wood'], 0.5, rx(8) @ rx(82))          # back plank (leans back)
    bx(g, (cx, D + 1.0, cz + 0.23), (0.44, 0.52, 0.09), 0.04, S['Reed'], L['Cloth'], 0.55, rx(8))
    # arms: plank + padded end, posts down to the seat plank
    for sx in (-1, 1):
        ax = cx + sx * 0.33
        bx(g, (ax, D + 0.86, cz + 0.02), (0.07, 0.04, 0.46), 0.012, S['Dock'], L['Wood'], 0.5)
        bx(g, (ax, D + 0.86, cz - 0.2), (0.09, 0.07, 0.12), 0.03, S['WoodDark'], L['Cloth'], 0.55)
        seg(g, (ax, D + 0.56, cz + 0.12), (ax, D + 0.84, cz + 0.12), 0.022, S['Rock'], L['none'], 0.5, 6)
    # cup holder on the starboard arm (a dented thermos cup)
    add_lathe(g, (cx + 0.33 + 0.07, D + 0.78, cz + 0.02), [(0.0, 0.05), (0.02, 0.05), (0.09, 0.052), (0.1, 0.0)], S['Accent'], L['none'], 0.55, sides=8, cap0=True)
    bar(g, P(cx + 0.33, D + 0.8, cz + 0.02), P(cx + 0.38, D + 0.8, cz + 0.02), 0.025, 0.03, 0.008, S['Rock'], L['none'], 0.5)
    # harness lugs on the back
    for sx in (-0.18, 0.18):
        rope_loop(g, (cx + sx, D + 0.98, cz + 0.33), 0.035, 0.01, slot=S['Rock'], rot=rx(90), n=8)
    # footrest: a plank plate on two legs, a rubber mat
    bx(g, (cx, D + 0.3, cz - 0.55), (0.6, 0.05, 0.3), 0.012, S['Dock'], L['Wood'], 0.5)
    bx(g, (cx, D + 0.332, cz - 0.55), (0.52, 0.015, 0.22), 0.006, S['Rock'], L['none'], 0.55, tint=0.4)
    for sx in (-0.25, 0.25):
        seg(g, (cx + sx, D, cz - 0.55), (cx + sx, D + 0.28, cz - 0.55), 0.028, S['Rock'], L['none'], 0.5, 6)
        bx(g, (cx + sx, D + 0.02, cz - 0.55), (0.09, 0.04, 0.09), 0.012, S['Rock'], L['none'], 0.5)
    # support strut from the pedestal to the footrest
    seg(g, (cx, D + 0.28, cz - 0.1), (cx, D + 0.28, cz - 0.5), 0.02, S['Rock'], L['none'], 0.5, 6)
    g.sol('chair')
    g.box_item('FightingChair_Body', (cx, D + 0.55, cz - 0.05), (0.66, 1.1, 1.0))
    g.mod['gimbal_socket'] = [round(cx, 3), round(D + 0.72, 3), round(cz - 0.36, 3)]
    return g

def build_gimbal():
    cx, cz = CH_C
    pv = (cx, D + 0.72, cz - 0.36)
    g = M('Tug_RodGimbal', dict(position=list(pv), yaw_deg=180.0, surface='front edge of Tug_FightingChair seat'),
          'Rod gimbal socket for the fighting chair: a bracket plate, a cross pin and a cup whose axis points aft and up (35 deg). The rod butt sits in '
          'the cup. Pivot is the cross pin: pitch about x, and the pin yoke swivels a few degrees about the vertical.',
          pivot=dict(position=[round(v, 3) for v in pv], axis='x (pitch), y (yaw)', limits_deg=dict(pitch=[-20, 50], yaw=[-35, 35])),
          upgrade='fighting_chair', requires=['Tug_FightingChair'], anim='follows the rod (rotates)')
    # bracket plate on the seat front, two cheeks, the pin, the cup
    bx(g, (cx, pv[1] - 0.08, pv[2] + 0.06), (0.2, 0.06, 0.16), 0.015, S['Rock'], L['none'], 0.5)
    for sx in (-1, 1):
        bx(g, (cx + sx * 0.085, pv[1] - 0.02, pv[2]), (0.03, 0.14, 0.1), 0.01, S['Rock'], L['none'], 0.5)
    bar(g, P(cx - 0.12, *pv[1:]), P(cx + 0.12, *pv[1:]), 0.03, 0.03, 0.01, S['WoodDark'], L['none'], 0.5)
    d = (0.0, math.sin(math.radians(35)), -math.cos(math.radians(35)))
    lat(g, P(cx, pv[1], pv[2]), d, [(0.0, 0.05), (0.04, 0.055), (0.22, 0.075), (0.26, 0.085)], S['Rock'], L['none'], 0.5, sides=10, cap0=True, cap1=False)
    lat(g, P(cx, pv[1], pv[2]) + Vector(d) * 0.255, d, [(0.0, 0.088), (0.02, 0.088)], S['Accent'], L['none'], 0.5, sides=10, cap1=False)
    g.sol('gimbal')
    return g

def build_davit():
    px, pz = DV_C
    g = M('Tug_Davit', dict(position=[px, D, pz], yaw_deg=90.0, surface='main deck, starboard stern quarter', footprint_xz=[px - 0.22, pz - 0.22, px + 0.22, pz + 0.22]),
          'Deck crane (a davit): a bolted pedestal at the starboard stern quarter, a boom that reaches out over the quarter (overhangs the water by ~1.6 m; '
          'the boom is at y > 3 so nothing on the deck is under its swing), brace rod, hand winch and a wire that ends at the hook block '
          '(Tug_Davit_Hook, its own object). Pedestal re-painted orange, the boom is the old red one: mismatched.',
          upgrade='davit', anim='static (slew about the pedestal axis if the builder wants; hook block is separate)')
    # flange, bolts, pedestal tube, bearing ring
    disc(g, (px, D, pz), 0.22, 0.04, S['Rock'], L['none'], 0.55, sides=12)
    for k in range(6):
        a = math.pi / 3 * k
        screw(g, (px + 0.17 * math.cos(a), D + 0.04, pz + 0.17 * math.sin(a)), (0, 1, 0), 0.02)
    add_lathe(g, (px, D + 0.04, pz), [(0.0, 0.13), (0.1, 0.1), (1.5, 0.1), (1.54, 0.14), (1.72, 0.14)], S['Accent'], L['Wood'], 0.5, sides=12, cap1=True)
    # boom: tapered tube out to starboard-aft, a bracket at the pivot and a cap at the tip
    piv = P(px, D + 1.72, pz)
    el = math.radians(22.0)
    ln = 2.4
    tip = piv + Vector((math.cos(el), math.sin(el), 0.0)) * ln
    d = tip - piv
    lat(g, piv - d.normalized() * 0.18, d, [(0.0, 0.07), (0.18, 0.07), (ln + 0.18, 0.045)], S['Roof'], L['Wood'], 0.5, sides=8, cap0=True, cap1=True)
    bx(g, (px, D + 1.72, pz), (0.18, 0.2, 0.2), 0.03, S['Rock'], L['none'], 0.5)
    # brace rod from the pedestal (y +1.0) to 60% along the boom
    seg(g, (px + 0.1, D + 1.0, pz), tuple(piv + d * 0.6), 0.02, S['Rock'], L['none'], 0.5, 6)
    # tip sheave bracket and sheave
    sv = tip + Vector((0.0, -0.04, 0.0))
    for sz in (-1, 1):
        bx(g, (sv.x, sv.y - 0.05, sv.z + sz * 0.045), (0.12, 0.2, 0.02), 0.006, S['Rock'], L['none'], 0.5)
    add_lathe(g, (sv.x, sv.y - 0.1, sv.z - 0.03), [(0.0, 0.07), (0.06, 0.07)], S['WoodDark'], L['none'], 0.5, sides=10, rot=rx(90), cap0=True, cap1=True)
    # hand winch on the pedestal (drum along z), crank on the stern side, wire along the boom to the tip
    wy = D + 1.18
    add_lathe(g, (px + 0.0, wy, pz - 0.12), [(0.0, 0.085), (0.0, 0.085), (0.26, 0.085)], S['Rock'], L['none'], 0.5, sides=10, rot=rx(90) @ Matrix.Rotation(0, 3, 'Y'), cap0=True, cap1=True)
    for z in (pz - 0.14, pz + 0.12):
        disc(g, (px, wy, z), 0.12, 0.02, S['Rock'], L['none'], 0.5, sides=10, rot=rx(90))
    bx(g, (px, wy - 0.11, pz - 0.01), (0.16, 0.05, 0.3), 0.012, S['Rock'], L['none'], 0.5)
    bar(g, P(px, wy, pz - 0.2), P(px + 0.0, wy + 0.16, pz - 0.27), 0.03, 0.03, 0.01, S['Rock'], L['none'], 0.5)
    add_lathe(g, (px, wy + 0.16, pz - 0.27), [(0.0, 0.02), (0.1, 0.02)], S['Accent'], L['none'], 0.5, sides=6, rot=rx(90), cap1=True)
    wire = [P(px, wy + 0.08, pz + 0.04), P(px, D + 1.86, pz + 0.05)]
    wire += [piv + d * f + Vector((0.0, 0.085, 0.045)) for f in (0.05, 0.3, 0.6, 0.9)] + [sv + Vector((0.0, 0.05, 0.045))]
    g.add(tube(wire, 0.008, 4), S['Rock'], L['none'], 0.5)
    # SWL sign on the pedestal (a joke with a footnote)
    bx(g, (px - 0.1, D + 0.95, pz + 0.093), (0.24, 0.12, 0.012), 0.004, S['Sand'], L['none'], 0.5, ry(-20))
    text(g, 'SWL 200KG*', 0.032, (px - 0.1, D + 0.96, pz + 0.102), ry(-20), S['WoodDark'])
    text(g, '*ISH', 0.028, (px - 0.1, D + 0.915, pz + 0.102), ry(-20), S['WoodDark'])
    # rust streaks (a darker strip) on the boom
    tape(g, tuple(piv + d * 0.35 + Vector((0, 0.075, 0))), (0.3, 0.004, 0.05), basis(d))
    g.solids += [('pedestal', [px - 0.22, D, pz - 0.22], [px + 0.22, D + 1.76, pz + 0.22]), ('winch', [px - 0.14, wy - 0.14, pz - 0.29], [px + 0.14, wy + 0.2, pz + 0.14])]
    for k in range(4):                                         # the boom in four chunks (a long diagonal AABB would mean nothing)
        a = piv + d * (ln * k / 4.0 - 0.1)
        b = piv + d * (ln * (k + 1) / 4.0)
        g.solids.append(('boom%d' % k, [min(a[i], b[i]) - 0.08 for i in range(3)], [max(a[i], b[i]) + 0.08 for i in range(3)]))
    g.mod['boom_tip'] = [round(v, 3) for v in tip]
    g.mod['sheave'] = [round(v, 3) for v in (sv + Vector((0.0, -0.1, -0.03)))]
    g.mod['boom_pivot'] = [round(v, 3) for v in piv]
    g.mod['rest_hook_attach'] = [round(sv.x, 3), round(sv.y - 0.16, 3), round(sv.z, 3)]
    g.box_item('Davit_Pedestal', (px, D + 0.9, pz), (0.3, 1.8, 0.3))
    return g

def build_hook():
    px, pz = DV_C
    el = math.radians(22.0)
    piv = Vector((px, D + 1.72, pz))
    tip = piv + Vector((math.cos(el), math.sin(el), 0.0)) * 2.4
    top = Vector((tip.x, tip.y - 1.3, tip.z))        # the block hangs 1.3 m under the boom tip in its rest pose
    g = M('Tug_Davit_Hook', dict(position=[round(v, 3) for v in top], yaw_deg=0.0, surface='hangs on the wire from the Tug_Davit boom tip'),
          'Hook block of the davit, its own object. Rest pose: hanging 1.3 m below the boom tip. Pivot = the wire attachment (top of the block): '
          'lower/raise by moving the pivot along y, swing about it. Orange cheek plates, steel hook with a safety latch, a short pennant. The wire above '
          'the pivot is drawn by the builder (LineRenderer from Tug_Davit mod.sheave down to this pivot).',
          pivot=dict(position=[round(v, 3) for v in top], axis='any (hangs)', travel_y=[-1.0, 1.1]),
          upgrade='davit', requires=['Tug_Davit'], anim='lower/raise + swing (pendulum)')
    bx(g, tuple(top - Vector((0, 0.04, 0))), (0.04, 0.08, 0.04), 0.01, S['Rock'], L['none'], 0.5)               # the eye at the top
    for sz in (-1, 1):
        bx(g, tuple(top + Vector((0, -0.16, sz * 0.045))), (0.1, 0.2, 0.025), 0.01, S['Accent'], L['Wood'], 0.5)   # cheek plates
    add_lathe(g, tuple(top + Vector((0, -0.19, -0.04))), [(0.0, 0.055), (0.08, 0.055)], S['Rock'], L['none'], 0.5, sides=10, rot=rx(90), cap0=True, cap1=True)
    # the hook: a shank down from the block, then a J bowl in the x-y plane whose tip comes back up
    hp = [top + Vector((0, -0.27, 0)), top + Vector((0, -0.5, 0))]
    for k in range(0, 13):
        a = math.radians(180 + 20 * k)
        hp.append(top + Vector((0.07 + 0.07 * math.cos(a), -0.5 + 0.07 * math.sin(a), 0.0)))
    g.add(tube(hp, 0.024, 6), S['Rock'], L['none'], 0.5)
    bar(g, top + Vector((0.0, -0.46, 0.0)), top + Vector((0.1, -0.43, 0.0)), 0.014, 0.014, 0.004, S['Accent'], L['none'], 0.5)    # safety latch
    # pennant: a rope tail with a tape flag
    g.add(tube([top + Vector((0, -0.19, 0.04)), top + Vector((0.05, -0.3, 0.12)), top + Vector((0.12, -0.55, 0.14)), top + Vector((0.1, -0.75, 0.1))], 0.012, 4), S['Sand'], L['Cloth'], 0.55)
    tape(g, tuple(top + Vector((0.12, -0.6, 0.14))), (0.05, 0.1, 0.004), ry(30))
    g.sol('hook')
    return g

def build_fairlead():
    x, z = 0.72, -7.4
    sy = tug_sheer(-7.4)
    capy = sy + 0.82 + 0.05          # transom bulwark cap top (bul_h(-7.4) = 0.82)
    g = M('Tug_TowFairlead', dict(position=[x, capy, z], yaw_deg=180.0, surface='transom bulwark cap, starboard of the stern spot', footprint_xz=[x - 0.19, z - 0.15, x + 0.19, z + 0.12]),
          'Roller fairlead bolted over the transom bulwark cap: the tug end of the dinghy tow line. Tow point (tug side) in mod.tow_point.',
          upgrade='dinghy', anim='static', requires=[])
    bx(g, (x, capy + 0.02, z), (0.38, 0.04, 0.26), 0.012, S['Rock'], L['none'], 0.5)
    for sx in (-1, 1):
        bx(g, (x + sx * 0.15, capy + 0.13, z), (0.045, 0.2, 0.2), 0.012, S['Rock'], L['none'], 0.5)
    add_lathe(g, (x - 0.16, capy + 0.17, z - 0.0), [(0.0, 0.05), (0.32, 0.05)], S['WoodDark'], L['none'], 0.5, sides=10, rot=rz(-90), cap0=True, cap1=True)
    for sx in (-1, 1):
        for sz in (-1, 1):
            screw(g, (x + sx * 0.15, capy + 0.04, z + sz * 0.09), (0, 1, 0), 0.016)
    # a tow ring on a shackle hanging aft
    rope_loop(g, (x, capy + 0.1, z - 0.17), 0.07, 0.015, slot=S['Rock'], rot=rx(0), n=10)
    g.mod['tow_point'] = [round(x, 3), round(capy + 0.1, 3), round(z - 0.24, 3)]
    g.sol('fairlead')
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# A local frame helper: build a part facing +z around its own origin, then place it anywhere in tug-local space with a yaw.
# ---------------------------------------------------------------------------------------------------------------------------------------
class Frame:
    def __init__(self, g, origin, yaw=0.0):
        self.g, self.o, self.R = g, Vector(origin), ry(yaw)

    def p(self, x, y, z):
        return self.o + self.R @ Vector((x, y, z))

    def bx(self, c, size, r, slot, layer=L['none'], ao=0.5, rot=None, tint=1.0):
        geo = cube_geo(size) if (r <= 0 or min(size) < 0.03) else rbox(size, r, 1)
        self.g.add(xform(geo, self.p(*c), self.R @ (rot if rot is not None else Matrix.Identity(3))), slot, layer, ao, None, tint)

    def lat(self, c, prof, slot, layer=L['none'], ao=0.5, sides=10, rot=None, cap0=False, cap1=True, ring_slots=None):
        add_lathe(self.g, self.p(*c), prof, slot, layer, ao, sides=sides, rot=self.R @ (rot if rot is not None else Matrix.Identity(3)), cap0=cap0, cap1=cap1, ring_slots=ring_slots)

    def seg(self, a, b, r, slot, layer=L['none'], ao=0.5, sides=6, caps=True):
        self.g.add(tube([self.p(*a), self.p(*b)], r, sides, caps=caps), slot, layer, ao)

    def tube(self, pts, r, slot, layer=L['none'], ao=0.5, sides=4, caps=True):
        self.g.add(tube([self.p(*q) for q in pts], r, sides, caps=caps), slot, layer, ao)

    def text(self, body, size, c, slot, rot=None):
        text(self.g, body, size, self.p(*c), self.R @ (rot if rot is not None else Matrix.Identity(3)), slot)

    def tape(self, c, size, rot=None):
        tape(self.g, self.p(*c), size, self.R @ (rot if rot is not None else Matrix.Identity(3)))

# ---------------------------------------------------------------------------------------------------------------------------------------
# SONAR: dome under the hull, the duct-taped cable, the screen in the wheelhouse
# ---------------------------------------------------------------------------------------------------------------------------------------
SONAR_Z = 1.2

def scaled(geo, sx, sy, sz):
    V, N, F, UV = geo
    return ([Vector((v[0] * sx, v[1] * sy, v[2] * sz)) for v in V], [Vector((n[0] / sx, n[1] / sy, n[2] / sz)).normalized() for n in N], F, UV)

def build_sonar_dome():
    kb = tug_keel(SONAR_Z)
    g = M('Tug_SonarDome', dict(position=[0.0, kb, SONAR_Z], yaw_deg=0.0, surface='hull bottom, on the keel line (underwater)', footprint_xz=[-0.36, SONAR_Z - 0.55, 0.36, SONAR_Z + 0.55]),
          'Transducer dome bolted under the hull on the keel line (it hangs 0.34 m below the keel, so the draft grows to 1.84 m). Dark rubber fairing on a '
          'steel mounting plate with 8 bolts, a zinc anode, and a duct-taped cable pigtail going up into the hull (the cable run is Tug_SonarCable). '
          'Underwater: only visible in the clear-water view.', upgrade='sonar', anim='static', draft_added=0.34)
    # steel mounting plate flush under the hull, a rubber gasket, the pod
    plate = rbox((0.72, 0.05, 1.1), 0.02, 1)
    g.add(xform(plate, (0, kb - 0.015, SONAR_Z)), S['Rock'], L['none'], 0.5)
    for sx in (-1, 1):
        for k in range(4):
            screw(g, (sx * 0.3, kb - 0.04, SONAR_Z - 0.45 + 0.3 * k), (0, -1, 0), 0.022)
    pod, _fs = lathe([(0.0, 0.0), (0.03, 0.17), (0.1, 0.27), (0.19, 0.31), (0.28, 0.32), (0.3, 0.32)], 14, cap0=False, cap1=True)
    pod = scaled(pod, 1.0, 1.0, 1.55)
    g.add(xform(pod, (0.0, kb - 0.04 - 0.3, SONAR_Z)), S['Rock'], L['none'], 0.5, None, 0.45)
    face = lathe([(0.0, 0.0), (0.02, 0.15), (0.04, 0.17)], 12, cap1=True)[0]
    g.add(xform(scaled(face, 1.0, 1.0, 1.3), (0.0, kb - 0.04 - 0.3 - 0.0, SONAR_Z)), S['Sand'], L['none'], 0.5)
    # the zinc anode (a pale lump) on the port side of the plate, the cable gland and the taped pigtail
    bx(g, (-0.22, kb - 0.075, SONAR_Z + 0.35), (0.12, 0.06, 0.2), 0.02, S['Sand'], L['none'], 0.55)
    add_lathe(g, (0.0, kb - 0.04, SONAR_Z + 0.5), [(0.0, 0.05), (0.06, 0.04), (0.14, 0.035)], S['Roof'], L['none'], 0.5, sides=8, cap0=True, rot=rx(90))   # cable gland
    for dz in (0.58, 0.66, 0.74):
        tape(g, (0.0, kb - 0.04, SONAR_Z + dz), (0.06, 0.05, 0.06))
    g.sol('dome')
    g.mod['mount_world_note'] = 'on the hull, centre line, z %.2f; underwater, hide with the water surface culled' % SONAR_Z
    return g

def hull_pt(z, th, off=0.025):
    # a point on the hull skin at section z, angle th (0 = keel, pi/2 = sheer), pushed out by off (build_tug_hull's section)
    w, sh, kb = tug_w(z), tug_sheer(z), tug_keel(z)
    x = w * math.sin(th) + off * math.sin(th)
    y = sh - (sh - kb) * max(0.0, math.cos(th)) ** 0.42 - off * math.cos(th)
    return Vector((x, y, z))

CABLE_Z = 1.85

def sonar_cable_path():
    kb = tug_keel(SONAR_Z)
    pts = [Vector((0.0, kb - 0.04, SONAR_Z + 0.5)), Vector((0.1, kb - 0.05, SONAR_Z + 0.6))]
    n = 9
    for k in range(1, n + 1):                                    # along the hull skin: keel -> up the starboard side to just over the waterline
        th = 1.05 * k / n * 1.0
        z = SONAR_Z + (CABLE_Z - SONAR_Z) * min(1.0, k / (n * 0.6))
        pts.append(hull_pt(z, th, 0.03))
    zc = CABLE_Z
    w, sh = tug_w(zc), tug_sheer(zc)
    bh = 0.82 + 0.35 * max(0.0, (H.tug_t(zc) - 0.6) / 0.4)
    capy = sh + bh + 0.05
    # up the topsides, over the fender ring, up the bulwark's red face, over the cap, down its inside, across the walkway, up the deckhouse
    # wall (between the nameboard and the porthole), over the roof lip, under the upper-deck rail, up the wheelhouse wall, through a drilled hole
    pts += [Vector((w + 0.02, 0.45, zc)), Vector((w + 0.1, 0.8, zc)), Vector((w + 0.2, 1.1, zc)), Vector((w + 0.3, 1.27, zc)), Vector((w + 0.2, 1.42, zc)),
            Vector((w + 0.065, 1.55, zc)), Vector((w + 0.065, capy - 0.1, zc)), Vector((w + 0.04, capy + 0.03, zc)), Vector((w - 0.07, capy + 0.05, zc)),
            Vector((w - 0.2, capy - 0.03, zc)), Vector((w - 0.215, capy - 0.2, zc)), Vector((w - 0.215, D + 0.1, zc)), Vector((w - 0.3, D + 0.03, zc))]
    pts += [Vector((x, D + 0.03, zc)) for x in (2.0, 1.75)] + [Vector((1.585, D + 0.1, zc)), Vector((1.585, 2.2, zc)), Vector((1.585, 3.2, zc)),
            Vector((1.62, 3.4, zc)), Vector((1.745, 3.5, zc)), Vector((1.745, 3.64, zc)), Vector((1.6, 3.64, zc)), Vector((1.45, 3.64, zc)),
            Vector((1.375, 3.7, zc)), Vector((1.375, 4.3, zc)), Vector((1.3, 4.4, zc)), Vector((1.2, 4.4, zc)), Vector((1.2, 4.5, zc + 0.15)),
            Vector((1.1, 4.62, 2.2))]
    return pts

def build_sonar_cable():
    g = M('Tug_SonarCable', dict(position=[0, 0, 0], yaw_deg=0.0, surface='hull, bulwark, deckhouse and wheelhouse walls (starboard side)'),
          'The sonar cable, run by someone in a hurry: from the dome up the starboard topsides over the fender ring, over the bulwark cap, across the '
          'walkway (a trip hazard), up the deckhouse wall between the nameboard and the porthole, over the roof lip, under the upper-deck rail, up the '
          'wheelhouse wall and through a drilled hole to the screen. Strips of grey duct tape every ~0.7 m, a blob of sealant on the hole.',
          upgrade='sonar', requires=['Tug_SonarDome'], anim='static')
    pts = sonar_cable_path()
    # smooth: Catmull-Rom subdivision once, so the cable bends round the bulwark without kinks
    sm = []
    for i in range(len(pts) - 1):
        p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        for t in (0.0, 0.5):
            sm.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    sm.append(pts[-1])
    g.add(tube(sm, 0.02, 5), S['WoodDark'], L['none'], 0.5, None, 0.55)
    # duct tape strips (silver) every ~0.7 m of path length, oriented along the cable
    acc, nxt = 0.0, 0.5
    for i in range(len(sm) - 1):
        d = sm[i + 1] - sm[i]
        acc += d.length
        if acc >= nxt and d.length > 1e-4:
            nxt += 0.7
            c = sm[i + 1]
            bx(g, tuple(c), (0.07, 0.058, 0.058), 0.012, S['Rock'], L['Cloth'], 0.6, basis(d))
    # the sealant on the drilled hole (a lump at the wheelhouse wall) and a loop of slack on the walkway
    sphere_part(g, (1.37, 4.4, CABLE_Z), 0.05, (0.5, 1.0, 1.0), S['Sand'], L['none'])
    g.sol('cable')
    g.mod['route_note'] = 'passes over the starboard fender ring and the bulwark cap by design (allowed contacts in the clearance report)'
    return g

def build_sonar_screen():
    fr_c = Vector((1.0, 4.95, 2.27))
    yaw = 249.0
    g = M('Tug_SonarScreen', dict(position=[round(v, 3) for v in fr_c], yaw_deg=yaw, surface='wheelhouse starboard sill (swing arm)', footprint_xz=[0.78, 2.07, 1.32, 2.5]),
          'Sonar display on a swing arm clamped to the starboard sill of the wheelhouse, facing the helm (yaw 249). A cream bezel on a grey housing '
          '(two different monitors): dark green screen with depth lines, the sea floor and an orange fish arch. Screen quad in mod.screen_quad for a '
          'builder-driven readout. Cable arrives from Tug_SonarCable.', upgrade='sonar', requires=['Tug_SonarDome', 'Tug_SonarCable'], anim='screen can flicker')
    # clamp and arm (world coords), then the monitor in a local frame
    bx(g, (1.3, 4.64, 2.2), (0.09, 0.07, 0.09), 0.02, S['Rock'], L['none'], 0.5)
    seg(g, (1.3, 4.64, 2.2), (1.3, 4.86, 2.2), 0.02, S['Rock'], L['none'], 0.5, 6)
    bar(g, P(1.3, 4.86, 2.2), P(1.12, 4.86, 2.26), 0.035, 0.035, 0.012, S['Rock'], L['none'], 0.5)
    add_lathe(g, (1.3, 4.7, 2.2), [(0.0, 0.032), (0.03, 0.032)], S['Accent'], L['none'], 0.5, sides=8, cap0=True, cap1=True)
    f = Frame(g, fr_c, yaw)
    f.bx((0, 0, -0.1), (0.36, 0.28, 0.2), 0.05, S['Rock'], L['none'], 0.5)                       # housing (grey)
    f.bx((0, 0, 0.0), (0.44, 0.34, 0.05), 0.03, S['Sand'], L['none'], 0.55)                       # bezel (cream)
    f.bx((0, 0, 0.026), (0.36, 0.26, 0.012), 0.01, S['FoliageDark'], L['none'], 0.5, tint=0.7)      # the glass
    # sonar picture: depth lines, sea floor, fish arch (flat bits just in front of the glass)
    for k in range(4):
        f.bx((0, 0.08 - 0.055 * k, 0.034), (0.34, 0.005, 0.004), 0.001, S['Foliage'], L['none'], 1.0)
    for k in range(7):
        h = 0.03 + 0.012 * math.sin(k * 1.7)
        f.bx((-0.15 + 0.05 * k, -0.11 + h / 2, 0.034), (0.05, h, 0.004), 0.001, S['Sand'], L['none'], 1.0)
    for k, (dx, dy) in enumerate(((-0.06, 0.0), (-0.03, 0.025), (0.0, 0.03), (0.03, 0.025), (0.06, 0.0))):
        f.bx((dx + 0.02, dy - 0.01, 0.036), (0.03, 0.012, 0.004), 0.001, S['Accent'], L['none'], 1.0)
    f.bx((0.1, 0.06, 0.036), (0.012, 0.012, 0.004), 0.001, S['Accent'], L['none'], 1.0)
    # knobs, the visor, a label-maker strip, tape holding the bezel on
    for k in range(3):
        f.lat((-0.14 + 0.07 * k, -0.152, 0.03), [(0.0, 0.017), (0.015, 0.014)], S['Roof'] if k == 1 else S['WoodDark'], L['none'], 0.5, sides=8, rot=rx(90))
    f.bx((0, 0.185, 0.03), (0.4, 0.02, 0.1), 0.01, S['Rock'], L['none'], 0.5, rx(18))
    f.bx((0.09, -0.12, 0.03), (0.15, 0.03, 0.006), 0.002, S['Sand'], L['none'], 0.6)
    f.text('SONAR', 0.022, (0.09, -0.12, 0.034), S['WoodDark'])
    f.tape((-0.2, 0.15, 0.03), (0.06, 0.02, 0.005), rz(45))
    f.tape((0.2, -0.14, 0.03), (0.06, 0.02, 0.005), rz(-45))
    g.sol('screen')
    g.mod['screen_quad'] = dict(center=[round(v, 3) for v in f.p(0, 0, 0.03)], size=[0.36, 0.26], yaw_deg=yaw)
    g.mod['cable_end'] = [1.1, 4.62, 2.2]
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# WHEELHOUSE DASH PAIR: Tug_Dash_Stock / Tug_Dash_Upgraded, the same slot on the console top (swap one for the other)
# Console top y 4.6, z 2.52..3.07, front windows behind it (z 3.1). Compass binnacle x 0.75, rolled map x -1.02..-0.38, thermos (-0.95, 2.88):
# the dash sits at x -0.35..0.55, z 2.8..3.07. Local frame faces the helm (yaw 180, local +z = tug -z).
# ---------------------------------------------------------------------------------------------------------------------------------------
DASH_O = (0.1, 4.6, 3.07)

def gauge(f, c, nrot, r=0.05, cracked=False, needle=0.0):
    # a round gauge lying on a slope: nrot turns +y onto the slope's normal; c is the point on the slope
    n = nrot @ Vector((0.0, 1.0, 0.0))
    up = nrot @ Vector((0.0, 0.0, -1.0))
    c = Vector(c)
    f.lat(tuple(c + n * 0.001), [(0.0, r + 0.012), (0.012, r + 0.012)], S['Accent'], L['none'], 0.5, sides=12, rot=nrot, cap0=True)               # the rim
    f.lat(tuple(c + n * 0.006), [(0.0, r), (0.012, r)], S['Sand'], L['none'], 0.55, sides=12, rot=nrot, cap0=True)                                 # the face
    a = math.radians(needle)
    d = up * math.cos(a) + Vector((1.0, 0.0, 0.0)) * math.sin(a)
    base = c + n * 0.021
    f.seg(tuple(base), tuple(base + d * r * 0.8), 0.004, S['WoodDark'], L['none'], 0.5, 4)
    f.lat(tuple(base), [(0.0, 0.009), (0.006, 0.009)], S['Rock'], L['none'], 0.5, sides=6, rot=nrot, cap0=True)
    if cracked:
        for ang in (35, -35):
            f.tape(tuple(base + n * 0.003), (0.011, 0.002, r * 1.8), nrot @ ry(ang))

def build_dash_stock():
    g = M('Tug_Dash_Stock', dict(position=list(DASH_O), yaw_deg=180.0, surface='wheelhouse console top, front edge', footprint_xz=[-0.35, 2.8, 0.55, 3.07]),
          'STOCK wheelhouse dash: a sloped wooden binnacle with three gauges (one cracked, taped), four toggle switches, a key and a handheld VHF in a '
          'cradle, a label-maker strip that reads "FUEL?". Swap with Tug_Dash_Upgraded when the dash upgrade is bought (never both).',
          upgrade='dash', replaces=[], anim='static')
    f = Frame(g, DASH_O, 180.0)
    poly = [(0.0, 0.0), (0.27, 0.0), (0.27, 0.06), (0.05, 0.2), (0.0, 0.2)]
    g.add(xform(H.prism(poly, 0.9, 0.012), f.p(0.0, 0.0, 0.0), f.R), S['WoodDark'], L['Wood'], 0.5)
    k = 0.14 / 0.22                                                   # slope: y = 0.06 + k (0.27 - z)
    nr = rx(math.degrees(math.atan2(k, 1.0)))                         # +y onto the slope normal
    on = lambda x, z, off=0.0: (x, 0.06 + k * (0.27 - z) + off * math.cos(math.atan2(k, 1.0)), z + off * math.sin(math.atan2(k, 1.0)))
    for x, cr, nd in ((-0.28, False, -50.0), (-0.13, True, 15.0), (0.02, False, 70.0)):
        gauge(f, on(x, 0.15), nr, 0.05, cr, nd)
    for i in range(4):                                                # four toggle switches and a key on the lower slope
        f.lat(on(0.12 + 0.05 * i, 0.235), [(0.0, 0.012), (0.04, 0.007)], S['Rock'], L['none'], 0.5, sides=6, rot=nr, cap0=True)
    f.lat(on(-0.28, 0.235), [(0.0, 0.02), (0.015, 0.02)], S['Rock'], L['none'], 0.5, sides=8, rot=nr, cap0=True)
    f.bx(on(-0.28, 0.235, 0.022), (0.012, 0.006, 0.045), 0.002, S['Accent'], L['none'], 0.5, nr)
    # the VHF in its cradle on the flat top (right end), antenna, an orange button
    f.bx((0.34, 0.22, 0.03), (0.1, 0.05, 0.06), 0.012, S['WoodDark'], L['none'], 0.5)
    f.bx((0.34, 0.29, 0.03), (0.07, 0.17, 0.04), 0.015, S['Sand'], L['none'], 0.55)
    f.lat((0.34, 0.37, 0.03), [(0.0, 0.006), (0.18, 0.003)], S['Rock'], L['none'], 0.5, sides=5, cap1=True)
    f.bx((0.34, 0.31, 0.052), (0.045, 0.05, 0.006), 0.002, S['FoliageDark'], L['none'], 0.5, tint=0.7)
    f.lat((0.36, 0.265, 0.052), [(0.0, 0.01), (0.008, 0.01)], S['Accent'], L['none'], 0.5, sides=6, rot=rx(90), cap0=True)
    # label-maker strip along the lower edge of the slope
    f.bx(on(-0.05, 0.258, 0.002), (0.34, 0.004, 0.025), 0.001, S['Sand'], L['none'], 0.6, nr)
    f.text('FUEL?', 0.02, on(-0.05, 0.258, 0.005), S['WoodDark'], rx(-(90 - math.degrees(math.atan2(k, 1.0)))))
    g.sol('dash')
    return g

def build_dash_upgraded():
    O = (0.1, 4.6, 3.07)
    g = M('Tug_Dash_Upgraded', dict(position=list(O), yaw_deg=180.0, surface='wheelhouse console top, front edge', footprint_xz=[-0.35, 2.72, 0.55, 3.07]),
          'UPGRADED wheelhouse dash (replaces Tug_Dash_Stock): a taller console cap with a chartplotter (a cracked-screen tablet in a case), a red-LED '
          'depth read-out, a big autopilot knob, three rocker switches, a USB fan, and a right-hand panel that is a different orange one from another boat '
          '(odd screws, its own sticker). The Tug_SonarScreen is a separate object on the starboard sill.', upgrade='dash', replaces=['Tug_Dash_Stock'], anim='static (screens can flicker)')
    f = Frame(g, O, 180.0)
    poly = [(0.0, 0.0), (0.35, 0.0), (0.35, 0.05), (0.18, 0.12), (0.05, 0.4), (0.0, 0.4)]
    g.add(xform(H.prism(poly, 0.9, 0.014), f.p(0.0, 0.0, 0.0), f.R), S['Rock'], L['none'], 0.5)
    # face normals: upper (y 0.42, z 0.906) -> rx(65); lower (y 0.93, z 0.377) -> rx(22)
    TU, TL = 65.0, 22.1
    cu, su = math.cos(math.radians(TU)), math.sin(math.radians(TU))
    cl, sl = math.cos(math.radians(TL)), math.sin(math.radians(TL))
    yu = lambda z: 0.4 - (0.28 / 0.13) * (z - 0.05)
    yl = lambda z: 0.12 - (0.07 / 0.17) * (z - 0.18)
    onu = lambda x, z, off=0.0: (x, yu(z) + off * cu, z + off * su)
    onl = lambda x, z, off=0.0: (x, yl(z) + off * cl, z + off * sl)
    rU, rL = rx(TU), rx(TL)
    # depth read-out (red LED bars) on the left, labelled
    f.bx(onu(-0.365, 0.11, 0.008), (0.17, 0.016, 0.1), 0.006, S['WoodDark'], L['none'], 0.5, rU)
    for i in range(3):
        f.bx(onu(-0.41 + 0.045 * i, 0.11, 0.018), (0.03, 0.004, 0.05), 0.001, S['Roof'], L['none'], 1.0, rU)
    f.text('DEPTH', 0.016, onu(-0.365, 0.172, 0.004), S['Sand'], rx(-(90 - TU)))
    # the chartplotter: a tablet in a bumper case, glass with a coast and a route, cracked
    f.bx(onu(-0.12, 0.115, 0.008), (0.3, 0.016, 0.2), 0.012, S['Roof'], L['none'], 0.55, rU)
    f.bx(onu(-0.12, 0.115, 0.017), (0.25, 0.006, 0.15), 0.004, S['FoliageDark'], L['none'], 0.5, rU, tint=0.7)
    for i in range(5):
        f.bx(onu(-0.22 + 0.048 * i, 0.115 + 0.022 * math.sin(i * 0.9), 0.021), (0.045, 0.003, 0.012), 0.001, S['Sand'], L['none'], 1.0, rU @ ry(10 * i - 20))
    f.bx(onu(-0.08, 0.1, 0.022), (0.012, 0.003, 0.012), 0.001, S['Accent'], L['none'], 1.0, rU)
    f.tape(onu(-0.2, 0.115, 0.022), (0.03, 0.003, 0.1), rU @ ry(30))
    # autopilot knob (big, black) with a pointer; three rocker switches on the lower face
    f.lat(onu(0.1, 0.115), [(0.0, 0.05), (0.03, 0.045), (0.035, 0.04)], S['WoodDark'], L['none'], 0.5, sides=12, rot=rU, cap0=True)
    f.bx(onu(0.1, 0.1, 0.037), (0.01, 0.006, 0.045), 0.002, S['Sand'], L['none'], 0.5, rU)
    for i, sl_ in enumerate((S['Roof'], S['Foliage'], S['Sand'])):
        f.bx(onl(-0.15 + 0.06 * i, 0.27, 0.012), (0.04, 0.02, 0.045), 0.008, sl_, L['none'], 0.5, rL)
    # the replacement panel on the right: another orange, odd screws, a sticker, two toggles
    f.bx(onu(0.3, 0.115, 0.006), (0.24, 0.012, 0.2), 0.004, S['Accent'], L['none'], 0.5, rU)
    for x in (0.2, 0.4):
        for z in (0.07, 0.16):
            f.lat(onu(x, z, 0.012), [(0.0, 0.011), (0.006, 0.009)], S['Sand'] if x > 0.3 else S['Rock'], L['none'], 0.5, sides=6, rot=rU, cap0=True)
    f.bx(onu(0.3, 0.095, 0.013), (0.13, 0.004, 0.05), 0.001, S['Sand'], L['none'], 0.6, rU)
    f.text('NOT SINKING', 0.017, onu(0.3, 0.095, 0.016), S['WoodDark'], rx(-(90 - TU)))
    for i in range(2):
        f.lat(onu(0.26 + 0.08 * i, 0.145, 0.012), [(0.0, 0.012), (0.045, 0.007)], S['Rock'], L['none'], 0.5, sides=6, rot=rU, cap0=True)
    # a USB fan on a gooseneck on the flat top, left end (it really does move air)
    f.seg((-0.4, 0.4, 0.022), (-0.385, 0.49, 0.05), 0.012, S['Rock'], L['none'], 0.5, 5)
    f.lat((-0.385, 0.49, 0.05), [(0.0, 0.058), (0.018, 0.058)], S['Foliage'], L['none'], 0.5, sides=10, rot=rx(80), cap0=True)
    for i in range(3):
        a = i * 2 * math.pi / 3
        f.bx((-0.385 + 0.03 * math.cos(a), 0.49 + 0.03 * math.sin(a), 0.082), (0.055, 0.02, 0.004), 0.002, S['Sand'], L['none'], 0.5, rz(math.degrees(a)))
    g.sol('dash')
    g.mod['screen_quad'] = dict(center=[round(v, 3) for v in f.p(*onu(-0.12, 0.115, 0.021))], size=[0.25, 0.15], yaw_deg=180.0, note='chartplotter, tilted 65 deg back')
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# WHEELHOUSE ROOF: searchlight (+ its own head)
# ---------------------------------------------------------------------------------------------------------------------------------------
SL_C = (-0.55, 1.25)

def build_searchlight():
    x, z = SL_C
    g = M('Tug_Searchlight', dict(position=[x, WT, z], yaw_deg=0.0, surface='wheelhouse roof, port of the mast', footprint_xz=[x - 0.26, z - 0.26, x + 0.26, z + 0.26]),
          'Searchlight base on the wheelhouse roof: a bolted plate, a pedestal and the yoke. The lamp is a separate object (Tug_Searchlight_Head) that '
          'pans and tilts. The stock tug already has a small lamp at (-0.7, 5.95, 2.9); this is the big one.', upgrade='searchlight', anim='static')
    disc(g, (x, WT, z), 0.2, 0.035, S['Rock'], L['none'], 0.55, sides=10)
    for k in range(4):
        a = math.pi / 2 * k + math.pi / 4
        screw(g, (x + 0.15 * math.cos(a), WT + 0.035, z + 0.15 * math.sin(a)), (0, 1, 0), 0.02)
    add_lathe(g, (x, WT + 0.035, z), [(0.0, 0.07), (0.05, 0.055), (0.38, 0.055), (0.41, 0.075), (0.46, 0.075)], S['Rock'], L['none'], 0.5, sides=10, cap1=True)
    # yoke: a U the head sits in
    for sx in (-1, 1):
        bx(g, (x + sx * 0.235, WT + 0.64, z), (0.035, 0.34, 0.1), 0.012, S['Rock'], L['none'], 0.5)
    bx(g, (x, WT + 0.49, z), (0.5, 0.05, 0.1), 0.015, S['Rock'], L['none'], 0.5)
    for sx in (-1, 1):
        add_lathe(g, (x + sx * 0.25, WT + 0.64, z), [(0.0, 0.03), (0.02, 0.03)], S['Accent'], L['none'], 0.5, sides=8, rot=rz(-90 * sx), cap0=True, cap1=True)
    # a power cable and a gland box
    bx(g, (x - 0.12, WT + 0.06, z - 0.12), (0.12, 0.1, 0.09), 0.02, S['WoodDark'], L['none'], 0.5)
    g.add(tube([P(x - 0.12, WT + 0.06, z - 0.18), P(x - 0.25, WT + 0.03, z - 0.3), P(x - 0.5, WT + 0.02, z - 0.28)], 0.015, 5), S['WoodDark'], L['none'], 0.5)
    tape(g, (x - 0.3, WT + 0.03, z - 0.3), (0.05, 0.04, 0.05))
    g.sol('base')
    g.mod['light_point'] = dict(position=[x, round(WT + 0.64, 3), round(z + 0.42, 3)], direction=[0.0, -0.08, 1.0], range_m=60.0, spot_angle_deg=22.0, colour='warm white',
                                 frame='tug local; the head pivot is Tug_Searchlight_Head.pivot')
    g.box_item('Searchlight_Base', (x, WT + 0.25, z), (0.4, 0.5, 0.4))
    return g

def build_searchlight_head():
    x, z = SL_C
    pv = (x, WT + 0.64, z)
    g = M('Tug_Searchlight_Head', dict(position=list(pv), yaw_deg=0.0, surface='yoke of Tug_Searchlight'),
          'The searchlight lamp: a drum with a bezel, a lens, a pistol grip with a taped trigger and a rusty hood. Pivot = the yoke pin: tilt about x, '
          'pan about the vertical through the base. Light point in mod.light_point (the lens centre, forward +z).',
          pivot=dict(position=[round(v, 3) for v in pv], axis='x (tilt, -30..60), y (pan, +-160)'), upgrade='searchlight', requires=['Tug_Searchlight'], anim='pan/tilt')
    f = Frame(g, pv, 0.0)
    f.lat((0, 0, -0.2), [(0.0, 0.0), (0.0, 0.17), (0.02, 0.18), (0.38, 0.18), (0.4, 0.2), (0.43, 0.2)], S['Roof'], L['none'], 0.5, sides=14, rot=rx(90), cap0=True, cap1=True)
    f.lat((0, 0, 0.23), [(0.0, 0.17), (0.02, 0.165)], S['Sand'], L['none'], 1.0, sides=14, rot=rx(90), cap0=True, cap1=True)         # the lens (bright)
    f.lat((0, 0, 0.2), [(0.0, 0.21), (0.04, 0.205)], S['Rock'], L['none'], 0.5, sides=14, rot=rx(90), cap0=True)                        # bezel ring
    f.bx((0, -0.22, -0.08), (0.05, 0.12, 0.07), 0.015, S['WoodDark'], L['none'], 0.5, rx(-15))                                   # the grip
    f.bx((0, -0.17, -0.03), (0.02, 0.05, 0.02), 0.005, S['Rock'], L['none'], 0.5)
    f.tape((0, -0.22, -0.045), (0.056, 0.03, 0.04))
    f.bx((0, 0.22, 0.0), (0.3, 0.02, 0.38), 0.008, S['Rock'], L['none'], 0.5, rx(-8))                                           # sun hood
    f.lat((0.0, 0.0, -0.215), [(0.0, 0.06), (0.04, 0.05)], S['Rock'], L['none'], 0.5, sides=8, rot=rx(-90), cap0=True)           # rear cap
    f.tape((0.1, 0.12, 0.12), (0.06, 0.04, 0.02), rx(15))
    g.sol('head')
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# OUTRIGGERS: Tug_Outriggers_Folded / Tug_Outriggers_Deployed (swap; both carry the two bracket bases)
# Bases clamp to the outside of the upper-deck rail posts at z 1.43 (x +-1.82, y 4.05); the wheelhouse roof overhang is at x +-1.55.
# ---------------------------------------------------------------------------------------------------------------------------------------
OR_Z, OR_X, OR_Y = 1.433, 1.82, 4.05

def outrigger_base(g, sx):
    px = sx * OR_X
    bx(g, (sx * 1.755, OR_Y, OR_Z), (0.07, 0.26, 0.2), 0.02, S['Rock'], L['none'], 0.5)                  # clamp plate against the rail post
    bx(g, (px - sx * 0.02, OR_Y - 0.1, OR_Z), (0.1, 0.06, 0.14), 0.02, S['Rock'], L['none'], 0.5)         # foot
    add_lathe(g, (px, OR_Y - 0.1, OR_Z), [(0.0, 0.07), (0.1, 0.065), (0.2, 0.06)], S['Rock'], L['none'], 0.5, sides=10, cap0=True, cap1=True)      # the socket
    for dz in (-0.07, 0.07):
        screw(g, (sx * 1.795, OR_Y + 0.08, OR_Z + dz), (sx, 0, 0), 0.016)
        screw(g, (sx * 1.795, OR_Y - 0.08, OR_Z + dz), (sx, 0, 0), 0.016)
    rope_loop(g, (sx * 1.66, OR_Y + 0.16, OR_Z), 0.05, 0.01, rot=rz(90), n=8)                          # a lashing round the post

def outrigger_pole(g, sx, d, length, slot):
    base = Vector((sx * OR_X, OR_Y + 0.1, OR_Z))
    d = Vector(d).normalized()
    lat(g, base, d, [(0.0, 0.052), (length * 0.4, 0.036), (length, 0.013)], slot, L['none'], 0.5, sides=8, cap0=True, cap1=True)
    for f in (0.34, 0.62, 0.86):                                                       # ferrule bands
        r0 = 0.052 + (0.013 - 0.052) * f * 1.0
        r = 0.036 * (1 - f) + 0.013 * f + 0.006 if f > 0.4 else 0.04
        lat(g, base + d * (length * f), d, [(0.0, r + 0.004), (0.05, r + 0.004)], S['Accent'], L['none'], 0.5, sides=8)
    return base + d * length

def build_outriggers(deployed):
    nm = 'Tug_Outriggers_Deployed' if deployed else 'Tug_Outriggers_Folded'
    g = M(nm, dict(position=[0.0, OR_Y, OR_Z], yaw_deg=0.0, surface='outside of the upper-deck rail posts, both sides, z %.2f' % OR_Z, footprint_xz=[-OR_X - 0.12, OR_Z - 0.12, OR_X + 0.12, OR_Z + 0.12]),
          ('Outriggers DEPLOYED: both poles swung out 32 deg above horizontal and 25 deg aft, a line from each tip back to a rail cleat with a release clip and '
           'a flag. The port pole is grey, the starboard one is cream (a replacement from another boat). The swept volume overhangs the water; '
           'tips at about x +-5.8. Swap with Tug_Outriggers_Folded.' if deployed else
           'Outriggers FOLDED: both poles stand against the wheelhouse side, leaning 12 deg aft, lashed to the rail posts (tips at y 8.5, still below the '
           'mast top 8.47 +- 0.1). Swap with Tug_Outriggers_Deployed.'),
          upgrade='outriggers', state='deployed' if deployed else 'folded', anim='swap folded/deployed (or lerp the pole pivots at the bases)',
          replaces=['Tug_Outriggers_Folded'] if deployed else ['Tug_Outriggers_Deployed'])
    for sx in (-1, 1):
        outrigger_base(g, sx)
        g.sol('base_' + ('P' if sx < 0 else 'S'))
    pivots = []
    for sx in (-1, 1):
        slot = S['Rock'] if sx < 0 else S['Sand']
        if deployed:
            el, az = math.radians(32.0), math.radians(25.0)
            d = (sx * math.cos(el) * math.cos(az), math.sin(el), -math.cos(el) * math.sin(az))
            ln = 5.0
        else:
            d = (0.0, math.cos(math.radians(12)), -math.sin(math.radians(12)))
            ln = 4.6
        tip = outrigger_pole(g, sx, d, ln, slot)
        # split the pole into 5 clearance solids (a long diagonal's AABB would be meaningless)
        base = Vector((sx * OR_X, OR_Y + 0.1, OR_Z))
        for k in range(5):
            a = base + Vector(d).normalized() * ln * k / 5
            b = base + Vector(d).normalized() * ln * (k + 1) / 5
            g.solids.append(('pole%d_%s' % (k, 'P' if sx < 0 else 'S'), [min(a[i], b[i]) - 0.05 for i in range(3)], [max(a[i], b[i]) + 0.05 for i in range(3)]))
        pivots.append(dict(name='pole_' + ('port' if sx < 0 else 'starboard'), position=[round(sx * OR_X, 3), round(OR_Y + 0.1, 3), OR_Z],
                           tip=[round(v, 3) for v in tip], length=ln))
        if deployed:
            # tip eyelet, the line back to the rail cleat (sag), a pulley block and a release clip with a flag
            cl = Vector((sx * 1.6, 3.9, -2.45))
            tipv = Vector(tip)
            mid = (tipv + cl) / 2 + Vector((0.0, -0.35, 0.0))
            line = [tipv + Vector((0, -0.02, 0)), tipv * 0.75 + cl * 0.25 + Vector((0, -0.2, 0)), mid, tipv * 0.25 + cl * 0.75 + Vector((0, -0.2, 0)), cl]
            g.add(tube(line, 0.009, 4), S['Sand'], L['Cloth'], 0.55)
            lat(g, tipv, Vector(d), [(0.0, 0.022), (0.03, 0.018)], S['Rock'], L['none'], 0.5, sides=6)
            clip = tipv * 0.45 + cl * 0.55 + Vector((0, -0.3, 0))
            bx(g, tuple(clip), (0.04, 0.09, 0.025), 0.01, S['Rock'], L['none'], 0.5)
            bx(g, tuple(clip + Vector((sx * 0.0, -0.12, 0.0))), (0.03, 0.14, 0.01), 0.002, S['Roof'] if sx < 0 else S['Accent'], L['Cloth'], 0.6, rx(8))       # flag
            bx(g, tuple(cl + Vector((0, 0.0, 0))), (0.06, 0.04, 0.1), 0.012, S['Rock'], L['none'], 0.5)                                                    # cleat
        else:
            # the tip is tied back to the base so it does not whip: one taut line along the pole, and a tape flag
            g.add(tube([Vector(tip) + Vector((sx * 0.06, 0, 0)), Vector((sx * 1.9, OR_Y + 1.2, OR_Z - 0.08))], 0.008, 4), S['Sand'], L['Cloth'], 0.55)
            bx(g, (tip[0], tip[1] - 0.1, tip[2]), (0.02, 0.12, 0.03), 0.004, S['Roof'], L['Cloth'], 0.6)
    g.mod['pole_pivots'] = pivots
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# BUNKS (inside the deckhouse, port wall, aft half)
# Deckhouse interior: x +-1.43, z -2.48..3.28, floor 1.25, ceiling 3.46. Stock interior: lockers x 0.91..1.41 z -1.99..-0.17 (starboard), the cot x -1.4..-0.55
# z 1.0..3.15 (port), shelves x 1.05..1.41 z 1.35..2.95 (starboard), a lamp at (0, 3.1, 0.4). The bunks take the free port wall z -1.9..-0.1.
# ---------------------------------------------------------------------------------------------------------------------------------------
BK_X0, BK_X1, BK_Z0, BK_Z1 = -1.41, -0.69, -1.9, -0.1

def build_bunks():
    cx, cz = (BK_X0 + BK_X1) / 2, (BK_Z0 + BK_Z1) / 2
    ln, wd = BK_Z1 - BK_Z0, BK_X1 - BK_X0
    g = M('Tug_Bunks', dict(position=[cx, D, cz], yaw_deg=0.0, surface='deckhouse floor, port wall, aft half', footprint_xz=[BK_X0, BK_Z0, BK_X1, BK_Z1]),
          'Two stacked bunks against the deckhouse port wall (1.8 x 0.72 m each). Olive mattress below, cream above (two different mattresses), a half-drawn '
          'curtain, a clip lamp, a sock on a post and footboard plates that are swapped (the LOWER bunk says UPPER and the other way round). Sleep points in '
          'mod.sleep_points. Leaves the cot (z >= 1.0), the lockers and the aft door clear.', upgrade='bunks', anim='static')
    levels = (1.52, 2.32)                    # frame top y
    for li, fy in enumerate(levels):
        for sx in (BK_X0 + 0.025, BK_X1 - 0.025):
            bx(g, (sx, fy - 0.05, cz), (0.05, 0.12, ln), 0.0, S['Dock'], L['Wood'], 0.5)                       # side rails (frame)
        for zz in (BK_Z0 + 0.02, BK_Z1 - 0.02):
            bx(g, (cx, fy - 0.03, zz), (wd, 0.16, 0.04), 0.0, S['Wood'], L['Wood'], 0.5)                       # head / foot boards
        for k in range(5):
            bx(g, (cx, fy - 0.1, BK_Z0 + 0.25 + 0.34 * k), (wd - 0.1, 0.02, 0.1), 0.0, S['WoodDark'], L['Wood'], 0.45)    # slats
        slot = S['Reed'] if li == 0 else S['Sand']
        bx(g, (cx, fy + 0.07, cz), (wd - 0.06, 0.11, ln - 0.1), 0.045, slot, L['Cloth'], 0.55)               # the mattress
        # blanket / sleeping bag
        if li == 0:
            bx(g, (cx, fy + 0.13, BK_Z1 - 0.62), (wd - 0.08, 0.04, 0.8), 0.018, S['Roof'], L['Cloth'], 0.55)
            sphere_part(g, (cx, fy + 0.2, BK_Z0 + 0.3), 0.18, (0.7, 0.28, 0.5), S['Sand'], L['Cloth'])        # pillow
        else:
            lat(g, P(cx - 0.18, fy + 0.19, BK_Z1 - 0.28), (0.0, 0.0, -1.0), [(0.0, 0.12), (0.01, 0.16), (0.62, 0.16), (0.63, 0.1)], S['Accent'], L['Cloth'], 0.55, sides=10, cap0=True, cap1=True)
            sphere_part(g, (cx, fy + 0.2, BK_Z0 + 0.3), 0.18, (0.7, 0.28, 0.5), S['Reed'], L['Cloth'])
    # corner posts and the guard rail on the aisle side of the upper bunk
    for sx in (BK_X0 + 0.03, BK_X1 - 0.03):
        for zz in (BK_Z0 + 0.03, BK_Z1 - 0.03):
            seg(g, (sx, D, zz), (sx, 2.95, zz), 0.03, S['Dock'], L['Wood'], 0.5, 6)
    bar(g, P(BK_X1 + 0.005, 2.6, BK_Z0 + 0.05), P(BK_X1 + 0.005, 2.6, BK_Z1 - 0.5), 0.03, 0.05, 0.0, S['Wood'], L['Wood'], 0.5)
    # ladder at the foot end of the aisle side: two uprights and three rungs
    for zz in (BK_Z1 - 0.36, BK_Z1 - 0.08):
        seg(g, (BK_X1 + 0.05, D + 0.05, zz), (BK_X1 + 0.05, 2.5, zz), 0.022, S['WoodDark'], L['Wood'], 0.45, 6)
    for yy in (1.55, 1.85, 2.15):
        seg(g, (BK_X1 + 0.05, yy, BK_Z1 - 0.37), (BK_X1 + 0.05, yy, BK_Z1 - 0.07), 0.018, S['WoodDark'], L['Wood'], 0.45, 6)
    # half-drawn curtain on the lower bunk: a rod under the upper frame, gathered folds
    seg(g, (BK_X1 + 0.02, 2.2, BK_Z0 + 0.1), (BK_X1 + 0.02, 2.2, BK_Z1 - 0.5), 0.012, S['Rock'], L['none'], 0.5, 5)
    for k in range(7):
        zz = BK_Z0 + 0.2 + 0.09 * k
        bx(g, (BK_X1 + 0.03 + 0.012 * (k % 2), 1.78, zz), (0.025, 0.86 - 0.03 * (k % 3), 0.085), 0.008, S['Sand'] if k % 3 else S['Reed'], L['Cloth'], 0.55, ry(10 * (k % 2) - 5))
    # footboard plates, swapped on purpose
    for fy, label in ((levels[0], 'UPPER'), (levels[1], 'LOWER')):
        bx(g, (cx, fy - 0.025, BK_Z1 + 0.005), (0.4, 0.09, 0.012), 0.003, S['Sand'], L['none'], 0.6)
        text(g, label, 0.055, (cx, fy - 0.025, BK_Z1 + 0.014), ry(0), S['WoodDark'])
    # clip lamp on the upper bunk headboard and a sock on a post
    bx(g, (cx, 2.62, BK_Z0 + 0.06), (0.05, 0.03, 0.04), 0.01, S['Rock'], L['none'], 0.5)
    g.add(tube([P(cx, 2.63, BK_Z0 + 0.06), P(cx + 0.05, 2.8, BK_Z0 + 0.12), P(cx + 0.12, 2.86, BK_Z0 + 0.25)], 0.01, 4), S['Rock'], L['none'], 0.5)
    lat(g, P(cx + 0.12, 2.86, BK_Z0 + 0.25), (0.3, -1.0, 0.5), [(0.0, 0.02), (0.01, 0.05), (0.08, 0.075)], S['Accent'], L['none'], 0.6, sides=10, cap0=True)
    sock = [P(BK_X1 - 0.03, 2.78, BK_Z1 - 0.03), P(BK_X1 + 0.02, 2.66, BK_Z1 - 0.03), P(BK_X1 + 0.03, 2.52, BK_Z1 - 0.03), P(BK_X1 + 0.07, 2.42, BK_Z1 - 0.03)]
    g.add(tube(sock, 0.035, 5), S['Sand'], L['Cloth'], 0.55)
    g.sol('bunks')
    g.box_item('Bunk_Lower', (cx, 1.25 + 0.2, cz), (wd, 0.4, ln), walkable=False)
    g.box_item('Bunk_Upper', (cx, 2.32 + 0.0, cz), (wd, 0.28, ln), walkable=False)
    g.mod['sleep_points'] = [dict(name='LowerBunk', position=[round(cx, 3), 1.75, round(cz, 3)], head_toward='-z', facing_deg=0.0, trigger_radius=0.9),
                             dict(name='UpperBunk', position=[round(cx, 3), 2.55, round(cz, 3)], head_toward='-z', facing_deg=0.0, trigger_radius=0.9)]
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# GALLEY (forward wall of the deckhouse interior) + the kettle as its own object
# Counter x -0.45..0.95, z 2.73..3.28 (the wall's inner face), top y 2.15. Cot at x <= -0.55 (z <= 3.15), shelves x >= 1.05 (z <= 2.95).
# ---------------------------------------------------------------------------------------------------------------------------------------
GL_X0, GL_X1, GL_Z0, GL_Z1, GL_TOP = -0.45, 0.95, 2.73, 3.28, 2.15
STOVE_X = -0.14
KETTLE_P = (STOVE_X + 0.13, GL_TOP + 0.14, 3.0)         # kettle base centre (on the right burner, on the grate)

def build_galley():
    cx, cz = (GL_X0 + GL_X1) / 2, (GL_Z0 + GL_Z1) / 2
    wd, dp = GL_X1 - GL_X0, GL_Z1 - GL_Z0
    g = M('Tug_Galley', dict(position=[cx, D, cz], yaw_deg=180.0, surface='deckhouse interior, forward wall', footprint_xz=[GL_X0, GL_Z0, GL_X1, GL_Z1 + 0.4]),
          'Galley: a counter unit (three doors, the right one a different olive green from the others), a 2-burner stove under an extractor hood with a vent '
          'cowl through the front wall, a sink with a thawing fish in it, mugs, a spice shelf, a chalkboard menu and a fire extinguisher. The kettle is a '
          'separate object (Tug_Galley_Kettle). Interactable points in mod.interaction_points. The vent pipe and cowl poke through the front wall of '
          'the deckhouse (z 3.28..3.4) by design.', upgrade='galley', anim='static')
    # carcass
    bx(g, (cx, D + 0.4, cz), (wd, 0.78, dp), 0.03, S['Dock'], L['Wood'], 0.5)
    bx(g, (cx, D + 0.04, cz - 0.02), (wd - 0.06, 0.08, dp - 0.1), 0.01, S['WoodDark'], L['none'], 0.45)
    bx(g, (cx, GL_TOP - 0.02, cz), (wd + 0.04, 0.04, dp + 0.04), 0.012, S['Wood'], L['Wood'], 0.5)             # counter top
    dw = wd / 3.0
    for k in range(3):
        sl = S['Accent'] if k == 2 else S['Reed']
        x = GL_X0 + dw * (k + 0.5)
        bx(g, (x, D + 0.42, GL_Z0 - 0.012), (dw - 0.04, 0.68, 0.025), 0.01, sl, L['Wood'], 0.5)
        bx(g, (x + (0.12 if k != 1 else -0.12), D + 0.58, GL_Z0 - 0.035), (0.02, 0.1, 0.02), 0.006, S['Rock'], L['none'], 0.5)
    # stove (2 burners, grate, knobs, hose), hood and vent
    sx0 = STOVE_X
    bx(g, (sx0, GL_TOP + 0.055, cz), (0.52, 0.11, 0.44), 0.025, S['Rock'], L['none'], 0.55, tint=0.6)
    for b in (-0.13, 0.13):
        add_lathe(g, (sx0 + b, GL_TOP + 0.11, cz), [(0.0, 0.085), (0.012, 0.085)], S['WoodDark'], L['none'], 0.5, sides=12, cap1=True)
        add_lathe(g, (sx0 + b, GL_TOP + 0.11, cz), [(0.0, 0.04), (0.02, 0.04)], S['Accent'], L['none'], 0.6, sides=10, cap1=True)
        bx(g, (sx0 + b, GL_TOP + 0.125, cz), (0.2, 0.02, 0.012), 0.004, S['Rock'], L['none'], 0.5)
        bx(g, (sx0 + b, GL_TOP + 0.125, cz), (0.012, 0.02, 0.2), 0.004, S['Rock'], L['none'], 0.5)
    for b in (-0.13, 0.13):
        lat(g, P(sx0 + b, GL_TOP + 0.06, GL_Z0 + 0.03 - 0.0), (0.0, 0.0, -1.0), [(0.0, 0.022), (0.025, 0.018)], S['Accent'] if b < 0 else S['Roof'], L['none'], 0.5, sides=8, cap0=True)
    g.add(tube([P(sx0, GL_TOP + 0.08, GL_Z1 - 0.02), P(sx0 - 0.1, GL_TOP + 0.02, GL_Z1 + 0.02), P(sx0 - 0.1, GL_TOP - 0.3, GL_Z1 - 0.02)], 0.014, 4), S['WoodDark'], L['none'], 0.5)
    hood = [(GL_Z1, 2.95), (2.86, 2.95), (3.14, 3.4), (GL_Z1, 3.4)]
    g.add(xform(H.prism(hood, 0.56, 0.012), (sx0, 0, 0)), S['Rock'], L['none'], 0.5, None, 0.9)
    bx(g, (sx0, 2.955, 3.05), (0.52, 0.02, 0.3), 0.006, S['Sand'], L['none'], 1.0)                      # the hood's lamp strip
    lat(g, P(sx0, 3.2, GL_Z1 - 0.04), (0.0, 0.0, 1.0), [(0.0, 0.06), (0.36, 0.06)], S['Rock'], L['none'], 0.5, sides=8, cap1=True)      # vent pipe through the wall
    lat(g, P(sx0, 3.2, GL_Z1 + 0.32), (0.0, 0.0, 1.0), [(0.0, 0.065), (0.04, 0.12), (0.1, 0.12), (0.12, 0.0)], S['Accent'], L['none'], 0.5, sides=10, cap0=True)     # cowl
    # sink: rim, basin, swan-neck tap, a fish thawing in it
    sk = 0.55
    bx(g, (sk, GL_TOP + 0.005, cz), (0.42, 0.014, 0.36), 0.006, S['Rock'], L['none'], 0.8)
    bx(g, (sk, GL_TOP + 0.014, cz), (0.34, 0.012, 0.28), 0.004, S['Rock'], L['none'], 0.5, tint=0.5)
    g.add(tube([P(sk, GL_TOP, GL_Z1 - 0.05), P(sk, GL_TOP + 0.26, GL_Z1 - 0.05), P(sk, GL_TOP + 0.31, GL_Z1 - 0.13), P(sk, GL_TOP + 0.26, GL_Z1 - 0.21)], 0.014, 5), S['Rock'], L['none'], 0.8)
    for dx in (-0.07, 0.07):
        lat(g, P(sk + dx, GL_TOP, GL_Z1 - 0.05), (0, 1, 0), [(0.0, 0.016), (0.07, 0.012)], S['Roof'] if dx > 0 else S['Foliage'], L['none'], 0.6, sides=6, cap1=True)
    sphere_part(g, (sk - 0.02, GL_TOP + 0.04, cz), 0.1, (1.6, 0.35, 0.5), S['Rock'], L['none'], 0.5, ry(25))        # the fish
    sphere_part(g, (sk - 0.11, GL_TOP + 0.045, cz + 0.02), 0.04, (0.7, 0.9, 0.3), S['Rock'], L['none'], 0.5, ry(25))
    # mugs, a spice shelf with jars, the chalkboard, the extinguisher
    for k, sl in enumerate((S['Accent'], S['Sand'], S['Reed'])):
        x = 0.14 + 0.065 * k
        add_lathe(g, (x, GL_TOP, 3.12 - 0.02 * k), [(0.0, 0.032), (0.002, 0.034), (0.085 - 0.01 * k, 0.034)], sl, L['none'], 0.55, sides=8, cap0=True)
    bx(g, (0.5, 2.7, GL_Z1 - 0.065), (0.62, 0.025, 0.13), 0.008, S['Wood'], L['Wood'], 0.5)
    for k, sl in enumerate((S['Roof'], S['Foliage'], S['Sand'], S['Accent'], S['Reed'])):
        add_lathe(g, (0.26 + 0.1 * k, 2.713, GL_Z1 - 0.065), [(0.0, 0.03), (0.075, 0.03), (0.09, 0.02), (0.1, 0.02)], sl, L['none'], 0.55, sides=8, cap0=True, cap1=True)
    for sx in (0.24, 0.76):
        bar(g, P(sx, 2.69, GL_Z1 - 0.01), P(sx, 2.69, GL_Z1 - 0.12), 0.012, 0.03, 0.004, S['Rock'], L['none'], 0.5)
    bx(g, (0.52, 3.12, GL_Z1 - 0.015), (0.62, 0.42, 0.03), 0.01, S['Dock'], L['Wood'], 0.5)
    bx(g, (0.52, 3.12, GL_Z1 - 0.034), (0.55, 0.35, 0.012), 0.004, S['FoliageDark'], L['none'], 0.5, tint=0.55)
    text(g, 'SOUP: FISH', 0.06, (0.52, 3.2, GL_Z1 - 0.043), ry(180), S['Sand'])
    text(g, 'MAIN: FISH', 0.06, (0.52, 3.08, GL_Z1 - 0.043), ry(180), S['Sand'])
    add_lathe(g, (0.86, 2.4, GL_Z1 - 0.07), [(0.0, 0.055), (0.03, 0.058), (0.36, 0.058), (0.4, 0.04), (0.44, 0.02)], S['Roof'], L['none'], 0.55, sides=10, cap0=True, cap1=True)   # extinguisher
    bx(g, (0.86, 2.62, GL_Z1 - 0.012), (0.06, 0.04, 0.02), 0.006, S['Rock'], L['none'], 0.5)
    bx(g, (0.86, 2.5, GL_Z1 - 0.012), (0.1, 0.03, 0.02), 0.006, S['Rock'], L['none'], 0.5)
    g.sol('galley')
    g.box_item('Galley_Counter', (cx, D + 0.45, cz), (wd + 0.04, 0.9, dp + 0.04))
    g.mod['interaction_points'] = [dict(name='Stove', position=[round(sx0, 3), GL_TOP + 0.12, round(cz, 3)], stand_at=[round(sx0, 3), D, 2.4], facing_deg=0.0, action='cook catch (buff)'),
                                   dict(name='Sink', position=[sk, GL_TOP + 0.02, round(cz, 3)], stand_at=[sk, D, 2.4], facing_deg=0.0, action='clean fish'),
                                   dict(name='Chalkboard', position=[0.52, 3.12, GL_Z1 - 0.04], stand_at=[0.52, D, 2.4], facing_deg=0.0, action='read the menu')]
    g.mod['steam_point_stove'] = [round(sx0 + 0.13, 3), round(GL_TOP + 0.55, 3), round(cz, 3)]
    return g

def build_kettle():
    px, py, pz = KETTLE_P
    g = M('Tug_Galley_Kettle', dict(position=[px, py, pz], yaw_deg=180.0, surface='right burner of the Tug_Galley stove', footprint_xz=[px - 0.2, pz - 0.15, px + 0.2, pz + 0.15]),
          'The kettle: a dented enamel body with a taped handle, spout aimed at the room (-z). Own object so it can shake and steam (steam_point). '
          'Pivot at the base centre, on the grate (y = counter top + 0.14).', pivot=dict(position=[px, py, pz], axis='base centre'), upgrade='galley', requires=['Tug_Galley'], anim='shake/steam')
    add_lathe(g, (px, py, pz), [(0.0, 0.1), (0.02, 0.116), (0.12, 0.125), (0.17, 0.095), (0.2, 0.05)], S['Foliage'], L['none'], 0.55, sides=12, cap0=True, cap1=True)
    add_lathe(g, (px, py + 0.2, pz), [(0.0, 0.05), (0.02, 0.045), (0.03, 0.02)], S['Foliage'], L['none'], 0.55, sides=10, cap1=True)
    add_lathe(g, (px, py + 0.225, pz), [(0.0, 0.018), (0.02, 0.016)], S['Rock'], L['none'], 0.5, sides=8, cap0=True, cap1=True)
    g.add(tube([P(px, py + 0.07, pz - 0.1), P(px, py + 0.12, pz - 0.17), P(px, py + 0.2, pz - 0.22)], 0.022, 6), S['Foliage'], L['none'], 0.55)        # spout (toward -z)
    hp = [P(px, py + 0.19, pz + 0.085), P(px, py + 0.27, pz + 0.12), P(px, py + 0.3, pz + 0.03), P(px, py + 0.27, pz - 0.06), P(px, py + 0.2, pz - 0.07)]
    g.add(tube(hp, 0.014, 5), S['WoodDark'], L['none'], 0.5)
    tape(g, (px, py + 0.3, pz + 0.03), (0.05, 0.04, 0.05))
    g.sol('kettle')
    g.mod['steam_point'] = [round(px, 3), round(py + 0.2, 3), round(pz - 0.22, 3)]
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# AQUARIUM-ON-BOARD (starboard wall of the deckhouse interior) + its water
# Starboard wall inner face x 1.43; lockers x 0.91..1.41 z -1.99..-0.17; shelves x 1.05..1.41 z 1.35..2.95 (y 2.5 and 3.0): the tank takes z 0.0..1.1.
# The front glass pane is left out (so the decor reads); mod.front_glass gives its quad for a transparent pane.
# ---------------------------------------------------------------------------------------------------------------------------------------
AQ_X0, AQ_X1, AQ_Z0, AQ_Z1 = 1.0, 1.42, 0.0, 1.1
AQ_CAB_TOP, AQ_TANK_TOP = 1.9, 2.4
AQ_WATER_Y = 2.33

def build_aquarium():
    cx, cz = (AQ_X0 + AQ_X1) / 2, (AQ_Z0 + AQ_Z1) / 2
    g = M('Tug_AquariumTank', dict(position=[cx, D, cz], yaw_deg=270.0, surface='deckhouse interior, starboard wall, between the lockers and the shelves', footprint_xz=[AQ_X0, AQ_Z0, AQ_X1, AQ_Z1 + 0.1]),
          'Aquarium-on-board: a 1.1 x 0.42 x 0.5 m glass tank on a plank cabinet (one orange door, one olive: mismatched), hood with a light strip, a hang-on '
          'filter, gravel, rocks, plants, a plastic castle and a plastic diver (tacky, as per design). Front glass left out, quad in mod.front_glass. Water is '
          'Tug_AquariumTank_Water. Bubbles: spawn at mod.bubble_point.', upgrade='aquarium', anim='static (water and bubbles animate)')
    wd, dp = AQ_X1 - AQ_X0, AQ_Z1 - AQ_Z0
    # cabinet with two doors on the room-facing side (-x)
    bx(g, (cx, D + (AQ_CAB_TOP - D) / 2, cz), (wd - 0.02, AQ_CAB_TOP - D - 0.03, dp - 0.04), 0.02, S['Dock'], L['Wood'], 0.5)
    for k, sl in enumerate((S['Reed'], S['Accent'])):
        z = AQ_Z0 + dp * (0.25 + 0.5 * k)
        bx(g, (AQ_X0 - 0.003, D + 0.33, z), (0.025, 0.55, dp / 2 - 0.05), 0.01, sl, L['Wood'], 0.5)
        add_lathe(g, (AQ_X0 - 0.016, D + 0.33, z + (0.1 if k == 0 else -0.1)), [(0.0, 0.016), (0.02, 0.012)], S['Rock'], L['none'], 0.5, sides=6, rot=rz(90), cap0=True, cap1=True)
    bx(g, (cx, AQ_CAB_TOP - 0.01, cz), (wd + 0.02, 0.03, dp + 0.02), 0.01, S['Rock'], L['none'], 0.5)       # tank base rim
    # glass: back and the two ends (dark glass), corner strips instead of a front pane
    gy = (AQ_CAB_TOP + AQ_TANK_TOP) / 2
    glass(g, (AQ_X1 - 0.008, gy, cz), (0.012, AQ_TANK_TOP - AQ_CAB_TOP, dp))
    for zz in (AQ_Z0 + 0.006, AQ_Z1 - 0.006):
        glass(g, (cx, gy, zz), (wd, AQ_TANK_TOP - AQ_CAB_TOP, 0.012))
    for zz in (AQ_Z0, AQ_Z1):
        bx(g, (AQ_X0 + 0.006, gy, zz), (0.02, AQ_TANK_TOP - AQ_CAB_TOP, 0.02), 0.004, S['WoodDark'], L['none'], 0.5)
    # hood: a dark lid with a light strip, a feed flap, an air pump, a hang-on filter on the room-facing end
    bx(g, (cx, AQ_TANK_TOP + 0.03, cz), (wd + 0.02, 0.06, dp + 0.02), 0.015, S['Rock'], L['none'], 0.5, tint=0.6)
    bx(g, (cx - 0.02, AQ_TANK_TOP - 0.005, cz), (wd - 0.1, 0.012, dp - 0.12), 0.004, S['Sand'], L['none'], 1.0)
    bx(g, (AQ_X1 - 0.08, AQ_TANK_TOP + 0.085, AQ_Z0 + 0.15), (0.12, 0.05, 0.12), 0.015, S['WoodDark'], L['none'], 0.5)
    bx(g, (cx, AQ_TANK_TOP + 0.065, AQ_Z1 - 0.15), (0.16, 0.012, 0.16), 0.004, S['Accent'], L['none'], 0.5)
    bx(g, (cx, gy - 0.05, AQ_Z1 + 0.04), (0.1, 0.2, 0.09), 0.015, S['Sand'], L['none'], 0.55)             # filter box
    bx(g, (cx, gy - 0.0, AQ_Z1 + 0.04), (0.05, 0.2, 0.15), 0.01, S['Rock'], L['none'], 0.5)
    g.add(tube([P(AQ_X1 - 0.08, AQ_TANK_TOP + 0.06, AQ_Z0 + 0.15), P(AQ_X1 - 0.08, AQ_TANK_TOP + 0.02, AQ_Z0 + 0.22), P(AQ_X1 - 0.1, 1.98, AQ_Z0 + 0.3)], 0.008, 4), S['WoodDark'], L['none'], 0.5)
    # name plate under the tank
    bx(g, (AQ_X0 - 0.018, AQ_CAB_TOP + 0.05, cz), (0.01, 0.07, 0.3), 0.003, S['Wood'], L['none'], 0.6)
    text(g, 'KEITH', 0.045, (AQ_X0 - 0.024, AQ_CAB_TOP + 0.05, cz), ry(270), S['WoodDark'])
    # inside: gravel, rocks, plants, a plastic castle, a plastic diver, an air stone
    gb = AQ_CAB_TOP + 0.045
    bx(g, (cx, gb - 0.02, cz), (wd - 0.04, 0.05, dp - 0.04), 0.02, S['Sand'], L['Dirt'] if 'Dirt' in L else L['none'], 0.6)
    rr = rng('aquarium')
    for k, (zz, rs) in enumerate(((0.8, 0.09), (0.55, 0.06), (0.95, 0.06))):
        sphere_part(g, (cx + 0.08 - 0.05 * k, gb + rs * 0.5, AQ_Z0 + zz), rs, (1.2, 0.8, 1.0), S['Rock'], L['Rock'], 0.55, ry(rr.uniform(0, 90)))
    for k, (zz, xx, slot) in enumerate(((0.62, 0.15, S['Foliage']), (0.9, 0.12, S['FoliageDark']), (0.4, 0.14, S['Foliage']))):
        for b in range(4):
            a = b * 1.6 + k
            tip = P(cx + xx - 0.11 + 0.04 * math.cos(a), gb + 0.17 + 0.05 * b, AQ_Z0 + zz + 0.04 * math.sin(a))
            g.add(tube([P(cx + xx - 0.11, gb, AQ_Z0 + zz), (P(cx + xx - 0.11, gb, AQ_Z0 + zz) + tip) / 2 + Vector((0.01, 0, 0)), tip], 0.007, 4), slot, L['none'], 0.5)
    cz0 = AQ_Z0 + 0.2                                                                        # the castle
    bx(g, (cx - 0.02, gb + 0.05, cz0), (0.16, 0.1, 0.16), 0.015, S['Accent'], L['none'], 0.55)
    for dz in (-0.07, 0.07):
        for dx in (-0.07, 0.07):
            add_lathe(g, (cx - 0.02 + dx, gb + 0.1, cz0 + dz), [(0.0, 0.025), (0.06, 0.025), (0.1, 0.0)], S['Sand'], L['none'], 0.55, sides=8, cap0=True)
    bx(g, (cx - 0.02 - 0.081, gb + 0.04, cz0), (0.01, 0.07, 0.06), 0.004, S['WoodDark'], L['none'], 0.5)             # door hole
    dvx, dvz = cx + 0.1, AQ_Z0 + 0.7                                                         # the diver (tiny, red)
    add_lathe(g, (dvx, gb, dvz), [(0.0, 0.02), (0.06, 0.02)], S['Roof'], L['none'], 0.55, sides=6, cap1=True)
    sphere_part(g, (dvx, gb + 0.085, dvz), 0.03, (1.0, 1.0, 1.0), S['Sand'], L['none'])
    bx(g, (dvx + 0.025, gb + 0.05, dvz), (0.02, 0.06, 0.04), 0.006, S['Rock'], L['none'], 0.5)
    add_lathe(g, (cx + 0.14, gb, AQ_Z1 - 0.2), [(0.0, 0.015), (0.02, 0.015)], S['Sand'], L['none'], 0.6, sides=6, cap1=True)                  # air stone
    g.sol('tank')
    g.box_item('Aquarium_Cabinet', (cx, D + (AQ_TANK_TOP + 0.09 - D) / 2, cz), (wd, AQ_TANK_TOP + 0.09 - D, dp + 0.12))
    g.mod['front_glass'] = dict(center=[AQ_X0, round(gy, 3), round(cz, 3)], size=[0.0, round(AQ_TANK_TOP - AQ_CAB_TOP, 3), round(dp, 3)], facing_deg=270.0, note='quad in the y-z plane at x = 1.0 facing -x')
    g.mod['bubble_point'] = [round(cx + 0.14, 3), round(gb + 0.03, 3), round(AQ_Z1 - 0.2, 3)]
    g.mod['water_surface_y'] = AQ_WATER_Y
    g.mod['fish_swim_volume'] = dict(min=[AQ_X0 + 0.05, gb + 0.1, AQ_Z0 + 0.05], max=[AQ_X1 - 0.05, AQ_WATER_Y - 0.06, AQ_Z1 - 0.05])
    return g

def build_aquarium_water():
    return water_slab('Tug_AquariumTank_Water', AQ_X0 + 0.015, AQ_X1 - 0.02, AQ_Z0 + 0.015, AQ_Z1 - 0.015, AQ_WATER_Y,
                      dict(position=[(AQ_X0 + AQ_X1) / 2, AQ_WATER_Y, (AQ_Z0 + AQ_Z1) / 2], yaw_deg=270.0, surface='inside Tug_AquariumTank'),
                      'Water of the aquarium, its own object: a closed ripple-topped body from the gravel to deck + 1.08 (y 2.33), open front face. Builder: '
                      'translucent water material; fish models swim inside.', depth=AQ_WATER_Y - (AQ_CAB_TOP + 0.07), ripple=0.003, nx=6, nz=10,
                      upgrade='aquarium', requires=['Tug_AquariumTank'], anim='ripple')

# ---------------------------------------------------------------------------------------------------------------------------------------
# BAIT FREEZER (foredeck, port side)
# ---------------------------------------------------------------------------------------------------------------------------------------
BF_C = (-1.20, 4.45)
BF_SIZE = (0.62, 0.82, 0.9)      # x, y, z

def build_bait_freezer():
    cx, cz = BF_C
    sx, sy, sz = BF_SIZE
    y0 = FD
    g = M('Tug_BaitFreezer', dict(position=[cx, y0, cz], yaw_deg=90.0, surface='foredeck, port side, forward of the step', footprint_xz=[cx - sx / 2, cz - sz / 2, cx + sx / 2, cz + sz / 2]),
          'Chest freezer for bait on the port foredeck: cream body, a lid that is a different red, a taped hasp and padlock, a compressor bulge with vent '
          'slots on the bulwark side, and a power cord that runs over the step to an outlet on the deckhouse front wall. Lettered BAIT / NOT FOOD on the lid '
          'and the walkway face. Lid is part of the body (no animation).', upgrade='bait_freezer', anim='static')
    bx(g, (cx, y0 + 0.37, cz), (sx, sy - 0.1, sz), 0.07, S['Sand'], L['none'], 0.55)
    bx(g, (cx, y0 + sy - 0.045, cz), (sx + 0.03, 0.09, sz + 0.03), 0.04, S['Roof'], L['none'], 0.55)      # lid
    bx(g, (cx, y0 + 0.04, cz), (sx - 0.08, 0.08, sz - 0.08), 0.02, S['Rock'], L['none'], 0.5, tint=0.5)     # plinth
    # compressor bulge and vent slots on the bulwark side (-x)
    bx(g, (cx - sx / 2 - 0.05, y0 + 0.2, cz), (0.1, 0.34, 0.7), 0.04, S['Rock'], L['none'], 0.55)
    for k in range(5):
        bx(g, (cx - sx / 2 - 0.102, y0 + 0.12 + 0.045 * k, cz), (0.01, 0.014, 0.55), 0.002, S['WoodDark'], L['none'], 0.5)
    # handle, hasp, padlock, tape, rust streaks on the walkway face (+x)
    bar(g, P(cx + sx / 2 + 0.05, y0 + sy - 0.12, cz - 0.28), P(cx + sx / 2 + 0.05, y0 + sy - 0.12, cz + 0.28), 0.03, 0.03, 0.01, S['Rock'], L['none'], 0.5)
    for dz in (-0.28, 0.28):
        bar(g, P(cx + sx / 2 + 0.01, y0 + sy - 0.12, cz + dz), P(cx + sx / 2 + 0.05, y0 + sy - 0.12, cz + dz), 0.025, 0.025, 0.008, S['Rock'], L['none'], 0.5)
    bx(g, (cx + sx / 2 + 0.02, y0 + sy - 0.2, cz), (0.02, 0.1, 0.09), 0.006, S['Rock'], L['none'], 0.5)
    add_lathe(g, (cx + sx / 2 + 0.035, y0 + sy - 0.26, cz), [(0.0, 0.03), (0.06, 0.03)], S['Accent'], L['none'], 0.5, sides=8, rot=rz(-90), cap0=True, cap1=True)
    tape(g, (cx + sx / 2 + 0.005, y0 + 0.5, cz + 0.2), (0.012, 0.3, 0.07), rx(15))
    tape(g, (cx + sx / 2 + 0.005, y0 + 0.5, cz + 0.2), (0.012, 0.3, 0.07), rx(-15))
    bx(g, (cx + sx / 2 + 0.003, y0 + 0.22, cz - 0.2), (0.008, 0.28, 0.05), 0.002, S['WoodDark'], L['none'], 0.5, tint=0.8)
    # lettering
    text(g, 'BAIT', 0.13, (cx, y0 + sy + 0.001, cz), rx(-90) @ ry(90), S['Sand'])
    bx(g, (cx + sx / 2 + 0.004, y0 + 0.62, cz), (0.008, 0.07, 0.4), 0.002, S['Sand'], L['none'], 0.6)
    text(g, 'NOT FOOD', 0.045, (cx + sx / 2 + 0.01, y0 + 0.62, cz), ry(90), S['WoodDark'])
    g.sol('freezer')
    g.box_item('BaitFreezer', (cx, y0 + sy / 2, cz), (sx + 0.1, sy, sz + 0.03))
    # the cord: over the step to an outlet box on the deckhouse front wall (z 3.4)
    ox = cx + 0.1
    g.add(tube([P(ox, y0 + 0.35, cz - sz / 2 + 0.03), P(ox, y0 + 0.03, cz - sz / 2 - 0.02), P(ox, y0 + 0.02, 3.97), P(ox, D + 0.03, 3.89), P(ox, D + 0.03, 3.5), P(ox, D + 0.3, 3.43), P(ox, D + 0.5, 3.43)], 0.013, 4), S['WoodDark'], L['none'], 0.5)
    bx(g, (ox, D + 0.55, 3.42), (0.09, 0.12, 0.04), 0.012, S['Rock'], L['none'], 0.5)
    g.sol('cord')
    g.mod['interaction_point'] = dict(name='BaitFreezer', position=[round(cx + sx / 2 + 0.1, 3), y0 + 0.6, round(cz, 3)], stand_at=[round(cx + 0.7, 3), y0, round(cz, 3)], action='take bait / stock bait')
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# DINGHY (towed behind the tug) - the one free body: its own origin is the keel centre (base centre), +z bow, y up. Not in tug-local space.
# ---------------------------------------------------------------------------------------------------------------------------------------
DG_L0, DG_L1 = -1.6, 1.62
DG_PLACEMENT = dict(position=[0.3, -0.17, -15.02], yaw_deg=0.0)      # tug-local: keel 0.17 under the waterline, bow eye 5.9 m behind the fairlead

def dg_beam(z):
    if z <= -0.2:
        return 0.62 + 0.1 * ((z - DG_L0) / (-0.2 - DG_L0)) ** 0.8
    s = (z + 0.2) / (DG_L1 + 0.2)
    return max(0.03, 0.72 * math.sqrt(max(0.0, 1.0 - s ** 2.2)))

def dg_sheer(z):
    return 0.5 + 0.3 * max(0.0, (z - 0.3) / (DG_L1 - 0.3)) ** 1.5

def dg_keel(z):
    return 0.32 * max(0.0, (z - 0.6) / (DG_L1 - 0.6)) ** 1.6

DG_SEC = [(-1.0, 1.0), (-0.97, 0.7), (-0.8, 0.25), (-0.45, 0.0), (0.45, 0.0), (0.8, 0.25), (0.97, 0.7), (1.0, 1.0)]

def build_dinghy():
    pl = DG_PLACEMENT
    g = M('Tug_Dinghy', dict(placement_tug_local=pl, surface='floats on the water, towed behind the tug (own origin = keel centre, +z bow)'),
          'Towable dinghy "BREAK II": a green flat-bottomed skiff, 3.2 x 1.45 m, cream inside, with an outboard (red cowl, one orange panel), two thwarts, '
          'two oars that do not match, a cut-down jug for a bailer, a bow eye with a bridle and a tow rope loop. Own origin at the keel centre. Floats with the '
          'keel 0.17 under the waterline. Tow point (bow eye) at mod.tow_point in dinghy-local space; the tug end is Tug_TowFairlead mod.tow_point.',
          upgrade='dinghy', anim='floats (physics body), towed on a rope from the fairlead', requires=['Tug_TowFairlead'], rays=24)
    g.is_free_body = True
    stations = [DG_L0, -1.45, -1.05, -0.5, 0.05, 0.6, 1.05, 1.4, DG_L1 - 0.01]
    bm = bmesh.new()
    rows = []
    for z in stations:
        b, h, k = dg_beam(z), dg_sheer(z), dg_keel(z)
        rows.append([bm.verts.new((fx * b, k + (h - k) * fy, z)) for fx, fy in DG_SEC])
    for i in range(len(rows) - 1):
        for j in range(len(DG_SEC) - 1):
            bm.faces.new((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]))
    bm.faces.new(rows[0])                                   # the transom plate
    bm.normal_update()
    probe = next(f for f in bm.faces if len(f.verts) == 4 and abs(f.calc_center_median().x) > 0.3 and f.calc_center_median().z > -0.5 and f.calc_center_median().z < 0.2)
    if probe.calc_center_median().x * probe.normal.x < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    V, N, F, MI, FN = H.eval_cage(bm, [('SUBSURF', dict(levels=1, render_levels=1)), ('SOLIDIFY', dict(thickness=0.035, offset=-1.0))])
    bm.free()
    UV, fsl = [], []
    for i, f in enumerate(F):
        c = sum((V[k] for k in f), Vector()) / len(f)
        outv = c - Vector((0.0, 0.3, c.z))
        UV.append([H.box_uv(V[k], FN[i], (1.45, 0.8, 3.2)) for k in f])
        if FN[i].y > 0.75 and c.y > 0.3:
            fsl.append((S['Wood'], L['Wood']))                             # the rim
        elif FN[i].dot(outv) > 0:
            fsl.append((S['Sand'], L['Wood']) if c.y > dg_sheer(c.z) - 0.14 else (S['Foliage'], L['Wood']))
        else:
            fsl.append((S['Sand'], L['Wood']))
    g.add((V, N, F, UV), 0, 0, 0.5, fsl)
    # gunwale strake all round the rim, the transom top board
    rim = [P(-dg_beam(z), dg_sheer(z) + 0.012, z) for z in stations[:-1]] + [P(0.0, dg_sheer(DG_L1 - 0.01) + 0.02, DG_L1 - 0.03)] + [P(dg_beam(z), dg_sheer(z) + 0.012, z) for z in reversed(stations[:-1])]
    g.add(tube(rim, 0.022, 5, caps=True), S['Dock'], L['Wood'], 0.5)
    bx(g, (0.0, dg_sheer(DG_L0) + 0.02, DG_L0 + 0.02), (1.3, 0.07, 0.06), 0.0, S['Dock'], L['Wood'], 0.5)
    # thwarts, floorboards, bow knee
    for zt, sl in ((0.5, S['Wood']), (-0.55, S['Dock'])):
        w = dg_beam(zt) * 1.85
        bx(g, (0.0, 0.34, zt), (w, 0.04, 0.2), 0.0, sl, L['Wood'], 0.5)
        for sx in (-1, 1):
            bx(g, (sx * (w / 2 - 0.05), 0.25, zt), (0.04, 0.17, 0.12), 0.0, S['WoodDark'], L['Wood'], 0.45)
    for k in range(3):
        bx(g, (-0.18 + 0.18 * k, 0.045, -0.45), (0.16, 0.025, 1.7), 0.0, S['Wood'], L['Wood'], 0.5)
    # the outboard on the transom
    ty = dg_sheer(DG_L0)
    bx(g, (0.0, ty - 0.02, DG_L0 - 0.04), (0.2, 0.14, 0.1), 0.02, S['Rock'], L['none'], 0.5)
    bx(g, (0.0, ty + 0.26, DG_L0 - 0.22), (0.3, 0.34, 0.34), 0.07, S['Roof'], L['none'], 0.55)
    bx(g, (0.105, ty + 0.27, DG_L0 - 0.22 - 0.0), (0.095, 0.2, 0.3), 0.02, S['Accent'], L['none'], 0.55, ry(0))        # the replacement cowl panel
    bx(g, (0.0, ty - 0.2, DG_L0 - 0.26), (0.1, 0.78, 0.12), 0.03, S['Rock'], L['none'], 0.5, tint=0.7)
    lat(g, P(0.0, ty - 0.55, DG_L0 - 0.3), (0, 0, -1), [(0.0, 0.0), (0.03, 0.06), (0.2, 0.07), (0.3, 0.04)], S['Rock'], L['none'], 0.5, sides=8, cap1=True, ring_slots=None)
    for k in range(3):
        a = k * 2 * math.pi / 3
        bx(g, (0.07 * math.cos(a), ty - 0.55 + 0.07 * math.sin(a), DG_L0 - 0.62), (0.02, 0.12, 0.08), 0.004, S['Sand'], L['none'], 0.5, rz(math.degrees(a) + 90) @ ry(20))
    bar(g, P(0.0, ty + 0.4, DG_L0 - 0.1), P(0.28, ty + 0.5, DG_L0 + 0.55), 0.028, 0.028, 0.008, S['Rock'], L['none'], 0.5)       # the tiller
    add_lathe(g, (0.28, ty + 0.5, DG_L0 + 0.5), [(0.0, 0.022), (0.12, 0.022)], S['Accent'], L['none'], 0.5, sides=6, rot=rx(80), cap1=True)
    # fuel can and hose
    bx(g, (-0.3, 0.19, -1.15), (0.22, 0.28, 0.32), 0.05, S['Roof'], L['none'], 0.55)
    g.add(tube([P(-0.3, 0.33, -1.15), P(-0.3, 0.45, -1.35), P(-0.12, 0.5, DG_L0 + 0.0), P(0.0, ty + 0.15, DG_L0 - 0.1)], 0.012, 4), S['WoodDark'], L['none'], 0.5)
    # oars: a wooden one and a plastic paddle, resting across the thwarts
    for (a, b, sl, bl) in ((P(-0.36, 0.45, -0.7), P(0.32, 0.46, 1.15), S['Wood'], 0.42), (P(0.4, 0.4, -0.75), P(-0.25, 0.43, 0.95), S['Sand'], 0.46)):
        d = (b - a).normalized()
        seg(g, tuple(a), tuple(b), 0.018, sl, L['Wood'], 0.5, 5)
        bx(g, tuple(b + d * (bl / 2)), (0.16, 0.012, bl), 0.003, sl if sl == S['Wood'] else S['Accent'], L['Wood'], 0.5, basis(d, (0, 1, 0)) @ ry(90))
        sphere_part(g, tuple(a - d * 0.02), 0.03, (1.0, 1.0, 1.0), sl, L['Wood'])
    # a cut-down jug for a bailer
    add_lathe(g, (0.34, 0.05, -0.1), [(0.0, 0.07), (0.01, 0.085), (0.15, 0.09), (0.17, 0.0)], S['Accent'], L['none'], 0.55, sides=10, cap0=True)
    # bow eye, bridle, tow rope loop
    ey = P(0.0, dg_keel(DG_L1) + (dg_sheer(DG_L1) - dg_keel(DG_L1)) * 0.78, DG_L1 + 0.02)
    rope_loop(g, tuple(ey), 0.045, 0.014, slot=S['Rock'], rot=rz(0) @ rx(90) @ ry(90) if False else rx(90), n=10)
    g.add(tube([ey + P(0, 0.0, -0.04), ey + P(0, 0.0, -0.18)], 0.022, 6), S['Rock'], L['none'], 0.5)
    for sx in (-1, 1):
        cl = P(sx * dg_beam(1.3) * 0.98, dg_sheer(1.3) + 0.05, 1.3)
        bx(g, tuple(cl), (0.05, 0.04, 0.08), 0.01, S['Rock'], L['none'], 0.5)
        g.add(tube([cl, (cl + ey) / 2 + P(0, -0.04, 0.05), ey], 0.01, 4), S['Sand'], L['Cloth'], 0.55)
    # the name, both sides of the bow
    for sx in (-1, 1):
        yaw = 73.0 * sx
        text(g, 'BREAK II', 0.075, (sx * 0.575, 0.47, 0.88), ry(yaw), S['Sand'])
    g.sol('dinghy')
    g.mod['tow_point'] = [round(v, 3) for v in ey + P(0, 0, 0.05)]
    g.mod['draft_m'] = 0.17
    g.mod['dimensions_m'] = dict(length=round(DG_L1 - DG_L0, 2), beam=1.45, height=0.8)
    g.mod['seats'] = [dict(name='Thwart_Fore', position=[0.0, 0.38, 0.5]), dict(name='Thwart_Aft', position=[0.0, 0.38, -0.55])]
    g.box_item('Dinghy_Hull', (0.0, 0.3, 0.0), (1.45, 0.6, 3.2))
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# THE BUILD LIST, the stock tug's obstacles, and the clearance check
# ---------------------------------------------------------------------------------------------------------------------------------------
BUILDERS = [build_livewell, build_livewell_lid, build_livewell_water, build_chair, build_gimbal, build_davit, build_hook, build_fairlead,
            build_sonar_dome, build_sonar_cable, build_sonar_screen, build_dash_stock, build_dash_upgraded, build_searchlight, build_searchlight_head,
            lambda: build_outriggers(False), lambda: build_outriggers(True), build_bunks, build_galley, build_kettle, build_aquarium, build_aquarium_water,
            build_bait_freezer, build_dinghy]

def build_all():
    out = []
    for fn in BUILDERS:
        g = fn()
        if g.name == 'Tug_SonarCable':
            fix_cable_solids(g)
        if g.name == 'Tug_Dinghy':
            g.local = True                      # its AO bakes against itself only (it floats free of the tug)
        if g.tris() > 4000 or len(g.V) >= 65000:
            raise RuntimeError('%s too heavy: %d tris, %d verts' % (g.name, g.tris(), len(g.V)))
        out.append(g)
    return out

def fix_cable_solids(g):
    # the cable is one long mesh: replace its single AABB by one box per ~0.45 m of path so the clearance check means something
    pts = sonar_cable_path()
    g.solids = []
    acc = 0.0
    start = pts[0]
    seg_pts = [pts[0]]
    for p in pts[1:]:
        seg_pts.append(p)
        acc += (p - seg_pts[-2]).length
        if acc > 0.45:
            lo = [min(q[i] for q in seg_pts) - 0.03 for i in range(3)]
            hi = [max(q[i] for q in seg_pts) + 0.03 for i in range(3)]
            g.solids.append(('cable%02d' % len(g.solids), lo, hi))
            seg_pts, acc = [p], 0.0
    if len(seg_pts) > 1:
        g.solids.append(('cable%02d' % len(g.solids), [min(q[i] for q in seg_pts) - 0.03 for i in range(3)], [max(q[i] for q in seg_pts) + 0.03 for i in range(3)]))

def stock_obstacles():
    # (name, kind, data): kind 'box' (lo, hi) hard, 'soft' (lo, hi) clutter that is baked into the tug mesh (cannot be hidden in Unity), 'cyl' (centre xz, r, y0, y1)
    z0, z1, hw = TUG_HOUSE
    w0, w1, whw = TUG_WH
    H0, H1 = D, HT
    W0, W1 = HT, WT
    ob = []
    B = lambda n, lo, hi: ob.append((n, 'box', (lo, hi)))
    Sf = lambda n, lo, hi: ob.append((n, 'soft', (lo, hi)))
    B('deckhouse stbd wall', (hw - 0.12, H0, z0), (hw, H1, z1)); B('deckhouse port wall', (-hw, H0, z0), (-hw + 0.12, H1, z1))
    B('deckhouse front wall', (-hw, H0, z1 - 0.12), (hw, H1, z1)); B('deckhouse aft wall W', (-hw, H0, z0), (-1.02, H1, z0 + 0.12))
    B('deckhouse aft wall E', (-0.07, H0, z0), (hw, H1, z0 + 0.12)); B('deckhouse door lintel', (-1.02, H0 + 2.0, z0), (-0.07, H1, z0 + 0.12))
    B('deckhouse roof slab', (-hw - 0.15, H1 - 0.14, z0 - 0.15), (hw + 0.15, H1, z1 + 0.15))
    B('aft door (open leaf)', (-1.07, H0, -3.53), (-1.01, H0 + 2.2, -2.63))
    B('aft door opening (keep clear)', (-1.02, H0, z0 - 0.5), (-0.07, H0 + 2.0, z0 + 0.5))
    B('wheelhouse stbd wall', (whw - 0.1, W0, w0), (whw, W1, w1)); B('wheelhouse port wall', (-whw, W0, w0), (-whw + 0.1, W1, w1))
    B('wheelhouse front wall', (-whw, W0, w1 - 0.1), (whw, W1, w1)); B('wheelhouse aft wall W', (-whw, W0, w0), (-0.9, W1, w0 + 0.1))
    B('wheelhouse aft wall E', (-0.05, W0, w0), (whw, W1, w0 + 0.1)); B('wheelhouse door lintel', (-0.9, W0 + 1.95, w0), (-0.05, W1, w0 + 0.1))
    B('wheelhouse roof slab', (-whw - 0.2, W1 - 0.14, w0 - 0.15), (whw + 0.2, W1, w1 + 0.35))
    B('wheelhouse console', (-1.2, W0, 2.525), (1.2, 4.6, 3.075))
    B('helm wheel', (-0.5, 4.4, 2.25), (0.5, 5.3, 2.62)); B('compass binnacle', (0.6, 4.6, 2.6), (0.9, 4.88, 2.9))
    B('rolled map', (-1.03, 4.6, 2.64), (-0.37, 4.73, 2.86)); B('thermos', (-1.02, 4.6, 2.81), (-0.88, 4.95, 2.95))
    B('mast', (0.47, WT, 1.78), (0.63, 8.5, 1.92)); B('wheelhouse roof horn', (-0.13, WT, 2.2), (0.13, WT + 0.25, 2.7))
    B('wheelhouse roof lamp (stock)', (-0.88, WT, 2.85), (-0.52, WT + 0.3, 3.2))
    B('wheelhouse roof rail port', (-whw - 0.03, WT, w0 + 0.1), (-whw + 0.03, WT + 0.52, w1)); B('wheelhouse roof rail stbd', (whw - 0.03, WT, w0 + 0.1), (whw + 0.03, WT + 0.52, w1))
    B('wheelhouse roof rail front', (-whw, WT, w1 - 0.03), (whw, WT + 0.52, w1 + 0.03))
    B('wheelhouse roof ladder gap', (0.45, WT, w0 - 0.5), (1.05, WT + 1.0, w0 + 0.3))
    B('ladder volume to upper deck', (0.4, H0, z0 - 0.75), (1.1, H1 + 1.0, z0)); B('ladder volume to wheelhouse roof', (0.45, W0, w0 - 0.65), (1.05, W1 + 1.0, w0))
    B('upper-deck rail stbd', (hw + 0.07, H1, z0 - 0.1), (hw + 0.13, H1 + 1.0, z1 + 0.1)); B('upper-deck rail port', (-hw - 0.13, H1, z0 - 0.1), (-hw - 0.07, H1 + 1.0, z1 + 0.1))
    B('upper-deck rail front', (-hw - 0.1, H1, z1 + 0.07), (hw + 0.1, H1 + 1.0, z1 + 0.13))
    B('upper-deck rail aft W', (-hw - 0.1, H1, z0 - 0.13), (0.4, H1 + 1.0, z0 - 0.07)); B('upper-deck rail aft E', (1.1, H1, z0 - 0.13), (hw + 0.1, H1 + 1.0, z0 - 0.07))
    B('smokestack', (-1.25, H1, -1.95), (-0.25, H1 + 2.4, -0.8))
    B('H-bitt', (-0.7, D, -3.91), (0.7, D + 0.74, -3.49))
    for i, (x, z) in enumerate(H.TUG_ICE_CHESTS):
        B('ice chest %d' % (i + 1), (x - 0.41, D, z - 0.27), (x + 0.41, D + 0.55, z + 0.27))
    B('gangway landing (keep clear)', (-2.4, D, -7.55), (-0.8, D + 2.2, -6.4))
    for nm, (cx, cz) in (('stern', (0.0, -6.7)), ):
        ob.append(('fishing spot %s' % nm, 'cyl', ((cx, cz), 0.5, D, D + 1.9)))
    for nm, p, f in H.TUG_FISH_SPOTS:
        if nm == 'Stern':
            continue
        ob.append(('fishing spot %s' % nm, 'cyl', ((p[0], p[2]), 0.5, p[1], p[1] + 1.9)))
    # interior furniture of the stock deckhouse
    B('lockers (starboard)', (0.91, D, -1.99), (1.41, D + 1.9, -0.17)); B('cot (port)', (-1.41, D, 1.0), (-0.55, D + 0.95, 3.15))
    B('shelves low (starboard)', (1.05, 2.5, 1.35), (1.41, 2.72, 2.95)); B('shelves high (starboard)', (1.05, 3.0, 1.35), (1.41, 3.22, 2.95))
    B('deckhouse interior lamp', (-0.38, 3.08, 0.02), (0.38, 3.48, 0.78))
    # soft: clutter baked into the tug mesh
    Sf('duffel + gear (port)', (-2.03, D, -3.58), (-1.27, D + 0.4, -2.62)); Sf('duffel (starboard)', (1.25, D, -3.68), (2.05, D + 0.5, -2.72))
    Sf('cooler', (1.25, D, -3.68), (1.95, D + 0.5, -3.22)); Sf('jerry cans', (-2.15, D, -5.8), (-1.55, D + 0.55, -5.0))
    Sf('rope coil aft', (-1.22, D, -7.07), (-0.58, D + 0.1, -6.43)); Sf('rope coil mid', (0.68, D, -3.27), (1.32, D + 0.1, -2.63))
    Sf('tarp pile', (0.45, FD, 3.95), (1.75, FD + 0.6, 5.55)); Sf('windlass', (-0.55, FD, 5.05), (0.55, FD + 0.5, 5.55))
    Sf('foredeck bitt port', (-0.73, FD, 6.27), (-0.37, FD + 0.42, 6.63)); Sf('foredeck bitt stbd', (0.37, FD, 6.27), (0.73, FD + 0.42, 6.63))
    Sf('rope coil fore', (-1.07, FD, 5.58), (-0.43, FD + 0.1, 6.22))
    for sx in (-1, 1):
        lo, hi = ((hw, 2.43, 2.47), (hw + 0.18, 3.07, 3.13)) if sx > 0 else ((-hw - 0.18, 2.43, 2.47), (-hw, 3.07, 3.13))
        Sf('life ring %s' % ('stbd' if sx > 0 else 'port'), lo, hi)
        lo, hi = ((hw, 3.03, -0.88), (hw + 0.1, 3.43, 1.68)) if sx > 0 else ((-hw - 0.1, 3.03, -0.88), (-hw, 3.43, 1.68))
        Sf('nameboard %s' % ('stbd' if sx > 0 else 'port'), lo, hi)
        for z in (z0 + 1.2, (z0 + z1) / 2, z1 - 1.2):
            lo, hi = ((hw, 2.55, z - 0.24), (hw + 0.1, 3.05, z + 0.24)) if sx > 0 else ((-hw - 0.1, 2.55, z - 0.24), (-hw, 3.05, z + 0.24))
            Sf('porthole %s z%.1f' % ('stbd' if sx > 0 else 'port', z), lo, hi)
    return ob

def box_gap(lo1, hi1, lo2, hi2):
    d = [max(lo1[i] - hi2[i], lo2[i] - hi1[i], 0.0) for i in range(3)]
    inter = all(lo1[i] < hi2[i] and lo2[i] < hi1[i] for i in range(3))
    return math.sqrt(sum(x * x for x in d)), inter

def cyl_gap(lo, hi, cyl):
    (cx, cz), r, y0, y1 = cyl
    dx = max(lo[0] - cx, 0.0, cx - hi[0]); dz = max(lo[2] - cz, 0.0, cz - hi[2])
    dxz = math.hypot(dx, dz) - r
    dy = max(lo[1] - y1, y0 - hi[1], 0.0)
    if dxz < 0 and dy == 0.0:
        return 0.0, True
    return math.hypot(max(dxz, 0.0), dy), False

def seg_dist(px, pz, ax, az, bx_, bz):
    dx, dz = bx_ - ax, bz - az
    t = clamp(((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz + 1e-12), 0.0, 1.0)
    return math.hypot(px - (ax + t * dx), pz - (az + t * dz))

def inside_poly(px, pz, poly):
    c = False
    for i in range(len(poly)):
        (x0, z0), (x1, z1) = poly[i], poly[(i + 1) % len(poly)]
        if (z0 > pz) != (z1 > pz) and px < (x1 - x0) * (pz - z0) / (z1 - z0) + x0:
            c = not c
    return c

def offset_poly(pts, d):
    # shrink a closed polygon by d along its vertex normals (the outline runs starboard stern -> bow -> port -> stern)
    n = len(pts)
    area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
    sgn = 1.0 if area > 0 else -1.0
    out = []
    for i in range(n):
        ax, az = pts[i - 1]; bx_, bz = pts[(i + 1) % n]
        tx, tz = bx_ - ax, bz - az
        ln = math.hypot(tx, tz) or 1.0
        nx, nz = tz / ln * sgn, -tx / ln * sgn           # the outward normal
        out.append((pts[i][0] - nx * d, pts[i][1] - nz * d))
    return out

BULWARK_POLY = None

def bulwark_gap(lo, hi):
    # signed distance (m) from the footprint to the bulwark's inner face: negative = through the bulwark. The gangway gap is an opening.
    global BULWARK_POLY
    if BULWARK_POLY is None:
        BULWARK_POLY = offset_poly(tug_outline(0.25), 0.17)
    poly = BULWARK_POLY
    pts = [(lo[0], lo[2]), (hi[0], lo[2]), (hi[0], hi[2]), (lo[0], hi[2]), ((lo[0] + hi[0]) / 2, lo[2]), ((lo[0] + hi[0]) / 2, hi[2]), (lo[0], (lo[2] + hi[2]) / 2), (hi[0], (lo[2] + hi[2]) / 2)]
    best = 9.0
    for px, pz in pts:
        d = min(seg_dist(px, pz, *poly[i], *poly[(i + 1) % len(poly)]) for i in range(len(poly)))
        best = min(best, d if inside_poly(px, pz, poly) else -d)
    return best

# (module, label prefix or '*', obstacle) contacts that are intended
ALLOWED = [('Tug_SonarCable', '*', 'upper-deck rail stbd'), ('Tug_SonarCable', '*', 'deckhouse roof slab'), ('Tug_SonarCable', '*', 'deckhouse stbd wall'),
           ('Tug_SonarCable', '*', 'wheelhouse stbd wall'), ('Tug_SonarCable', '*', 'wheelhouse console'), ('Tug_SonarCable', '*', 'bulwark'),
           ('Tug_SonarCable', '*', 'nameboard stbd'), ('Tug_SonarCable', '*', 'porthole stbd z2.2'), ('Tug_SonarCable', '*', 'fishing spot StarboardRail'),
           ('Tug_TowFairlead', '*', 'bulwark'), ('Tug_SonarScreen', '*', 'wheelhouse stbd wall'), ('Tug_Galley', '*', 'deckhouse front wall'),
           ('Tug_Outriggers_Folded', 'base_', 'upper-deck rail stbd'), ('Tug_Outriggers_Folded', 'base_', 'upper-deck rail port'),
           ('Tug_Outriggers_Deployed', 'base_', 'upper-deck rail stbd'), ('Tug_Outriggers_Deployed', 'base_', 'upper-deck rail port'),
           ('Tug_Davit_Hook', '*', 'bulwark'),
           ('Tug_BaitFreezer', '*', 'deckhouse front wall'), ('Tug_AquariumTank', '*', 'deckhouse stbd wall'), ('Tug_Bunks', '*', 'deckhouse port wall'),
           ('Tug_Dash_Stock', '*', 'wheelhouse console'), ('Tug_Dash_Upgraded', '*', 'wheelhouse console'), ('Tug_Searchlight', '*', 'wheelhouse roof slab'),
           ('Tug_Galley', '*', 'deckhouse roof slab')]

def allowed(mod, label, obst):
    return any(m == mod and (l == '*' or label.startswith(l)) and o == obst for m, l, o in ALLOWED)

def inside_deckhouse(lo, hi):
    return lo[0] > -1.44 and hi[0] < 1.44 and lo[2] > -2.49 and hi[2] < 3.29 and hi[1] < 3.46

def clearance_report(mods):
    obs = stock_obstacles()
    report, problems = {}, []
    for g in mods:
        if getattr(g, 'is_free_body', False):
            report[g.name] = dict(status='free body (not on the tug)', nearest=[])
            continue
        rows = []
        for (label, lo, hi) in g.solids:
            for (name, kind, data) in obs:
                if kind == 'cyl':
                    if inside_deckhouse(lo, hi):
                        continue                                   # the walls separate it from the (outdoor) fishing spot
                    gp, inter = cyl_gap(lo, hi, data)
                else:
                    gp, inter = box_gap(lo, hi, data[0], data[1])
                if gp > 1.0 and not inter:
                    continue
                ok = allowed(g.name, label, name)
                rows.append(dict(part=label, obstacle=name, kind=kind, gap_m=0.0 if inter else round(gp, 3), overlap=bool(inter), allowed=bool(ok)))
                if inter and not ok:
                    problems.append('%s/%s overlaps %s (%s)' % (g.name, label, name, kind))
            # the bulwark applies to solids standing on the main deck or foredeck (y inside the bulwark's height band)
            if hi[1] > D + 0.02 and lo[1] < 2.3 and lo[1] > -0.5:
                bg = bulwark_gap(lo, hi)
                ok = allowed(g.name, label, 'bulwark')
                rows.append(dict(part=label, obstacle='bulwark (inner face)', kind='bulwark', gap_m=round(bg, 3), overlap=bg < 0, allowed=bool(ok)))
                if bg < 0 and not ok:
                    problems.append('%s/%s is through the bulwark (%.3f m)' % (g.name, label, bg))
        rows.sort(key=lambda r: (not r['overlap'], r['gap_m']))
        nearest, seen = [], set()
        for r in rows:
            if r['obstacle'] in seen:
                continue
            seen.add(r['obstacle']); nearest.append(r)
            if len(nearest) >= 6:
                break
        free = [r['gap_m'] for r in rows if not r['overlap'] and not r['allowed'] and r['gap_m'] >= 0]
        report[g.name] = dict(status='OVERLAP' if any(r['overlap'] and not r['allowed'] for r in rows) else 'ok',
                              min_clearance_m=round(min(free), 3) if free else None, nearest=nearest,
                              intended_contacts=sorted(set(r['obstacle'] for r in rows if r['allowed'] and (r['overlap'] or r['gap_m'] <= 0.02))))
    # every module against every other (all installed at once); parent/child and swap pairs are skipped
    for i, a in enumerate(mods):
        for b in mods[i + 1:]:
            if getattr(a, 'is_free_body', False) or getattr(b, 'is_free_body', False):
                continue
            if b.name in a.mod['requires'] or a.name in b.mod['requires'] or b.name in a.mod['replaces'] or a.name in b.mod['replaces']:
                continue
            for (la, loa, hia) in a.solids:
                for (lb, lob, hib) in b.solids:
                    if box_gap(loa, hia, lob, hib)[1]:
                        problems.append('%s/%s overlaps %s/%s' % (a.name, la, b.name, lb))
    return report, problems

# ---------------------------------------------------------------------------------------------------------------------------------------
# Blender objects, AO bake, export and manifest
# ---------------------------------------------------------------------------------------------------------------------------------------
INSTALL_SETS = dict(
    stock=['Tug_Dash_Stock'],
    upgraded=['Tug_LiveWell', 'Tug_LiveWell_Lid', 'Tug_LiveWell_Water', 'Tug_FightingChair', 'Tug_RodGimbal', 'Tug_Davit', 'Tug_Davit_Hook', 'Tug_TowFairlead',
              'Tug_Dinghy', 'Tug_SonarDome', 'Tug_SonarCable', 'Tug_SonarScreen', 'Tug_Dash_Upgraded', 'Tug_Searchlight', 'Tug_Searchlight_Head',
              'Tug_Outriggers_Deployed', 'Tug_Bunks', 'Tug_Galley', 'Tug_Galley_Kettle', 'Tug_AquariumTank', 'Tug_AquariumTank_Water', 'Tug_BaitFreezer'])

UPGRADES = [
    dict(id='live_well', design='live well', models=['Tug_LiveWell', 'Tug_LiveWell_Lid', 'Tug_LiveWell_Water']),
    dict(id='sonar', design='sonar', models=['Tug_SonarDome', 'Tug_SonarCable', 'Tug_SonarScreen']),
    dict(id='davit', design='crane and davit', models=['Tug_Davit', 'Tug_Davit_Hook']),
    dict(id='fighting_chair', design='fighting chair', models=['Tug_FightingChair', 'Tug_RodGimbal']),
    dict(id='bunks', design='bunks (sleep anywhere)', models=['Tug_Bunks']),
    dict(id='galley', design='galley (cook catches for buffs)', models=['Tug_Galley', 'Tug_Galley_Kettle']),
    dict(id='aquarium', design='aquarium tank', models=['Tug_AquariumTank', 'Tug_AquariumTank_Water']),
    dict(id='bait_freezer', design='bait freezer', models=['Tug_BaitFreezer']),
    dict(id='outriggers', design='outriggers', models=['Tug_Outriggers_Folded', 'Tug_Outriggers_Deployed']),
    dict(id='searchlight', design='searchlight', models=['Tug_Searchlight', 'Tug_Searchlight_Head']),
    dict(id='dinghy', design='a towed dinghy for shallows', models=['Tug_Dinghy', 'Tug_TowFairlead']),
    dict(id='dash', design='wheelhouse dash: stock / upgraded pair', models=['Tug_Dash_Stock', 'Tug_Dash_Upgraded']),
]

def objects_for(mods, tug, bake=True):
    for o in list(H.collection('Harbour').objects):
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
    tug_ob = H.make_object2(tug)
    obs = [H.make_object2(g) for g in mods]
    if bake:
        raws = H.bake_ao2([tug], [tug])
        H.write_ao2(tug_ob, tug, raws[0])
        occ = [tug] + [g for g in mods if g.rays > 0 and not getattr(g, 'is_free_body', False)]
        raws = H.bake_ao2(mods, occ)
        for ob, g, raw in zip(obs, mods, raws):
            H.write_ao2(ob, g, raw)
    return tug_ob, obs

def rnd(v, n=3):
    # round every float in a nested structure (the builders compute positions in floating point; the manifest should read like numbers a person typed)
    if isinstance(v, float):
        return round(v, n)
    if isinstance(v, (list, tuple)):
        return [rnd(x, n) for x in v]
    if isinstance(v, dict):
        return {k: rnd(x, n) for k, x in v.items()}
    return v

def manifest_entry(g, clear):
    lo, hi = H.u_bounds(g)
    used = sorted(set(l for _, l in g.SL)); slots = sorted(set(s for s, _ in g.SL))
    d = dict(name=g.name, file='Art/Export/TugUpgrades/%s.fbx' % g.name, upgrade=g.mod.get('upgrade'), state=g.state, triangles=g.tris(), vertices=len(g.V),
             material=g.material or 'M_Toon_Base', bounds_min=lo, bounds_max=hi, footprint=[round(hi[i] - lo[i], 3) for i in range(3)],
             mount=rnd(g.mount), pivot=rnd(g.pivot), requires=g.mod['requires'], replaces=g.mod['replaces'], animHint=g.anim, slots=slots, layers=used, note=g.note,
             colliders=g.items, clearance=clear.get(g.name))
    for k, v in g.mod.items():
        if k not in ('requires', 'replaces', 'upgrade'):
            d[k] = rnd(v)
    return d

def build_manifest(mods, clear, problems, status):
    tug_tris = None
    m = dict(
        generator='Art/Blender/fg_tugmods.py (imports Art/Blender/fg_harbour.py, which stays untouched)',
        date='2026-10-17', branch='harbour-tug-upgrades', based_on='origin/harbour-art3',
        axes='TUG-LOCAL Unity metres: origin at the tug waterline centre, +z bow, +x starboard, y up (TUG_WORLD places the tug). Every FBX object is authored in '
             'tug-local space with an identity transform: install a module by parenting its mesh object to the tug at (0,0,0). Parts with a "pivot" (lids, hook block, '
             'gimbal, searchlight head, kettle): make an empty at pivot.position under the tug, parent the mesh under it with localPosition = -pivot.position, animate the empty. '
             'Tug_Dinghy is the one free body: its own origin is its keel centre, placement in mod/mount.placement_tug_local.',
        tug_reference=dict(file='Art/Export/Harbour/Harbour_Core_CrewBoat.fbx', constants=dict(
            main_deck_y=D, foredeck_y=FD, upper_deck_y=HT, wheelhouse_roof_y=WT, deckhouse=dict(z0=HZ0, z1=HZ1, half_width=HHW), wheelhouse=dict(z0=WZ0, z1=WZ1, half_width=WHW),
            gangway_gap_z=H.TUG_GANGWAY_Z, ice_chests=[list(c) for c in H.TUG_ICE_CHESTS],
            fishing_spots=[dict(name=n, local=list(p), facing_deg=f) for n, p, f in H.TUG_FISH_SPOTS], world=dict(position=list(H.TUG_WORLD['position']), yaw=H.TUG_WORLD['yaw']))),
        encoding='UV0 UVMap metres; UV1 Layer = (palette slot, texture layer); vertex colour Col = 0.5 x AO (baked here against the tug and the other modules); smooth / analytic '
                 'normals; no materials exported; M_Toon_Base unless a model names another material (the two water objects).',
        install_sets=INSTALL_SETS, upgrades=UPGRADES,
        clearance_method='Per part, an axis-aligned box (long diagonals such as the boom, the cable and the outrigger poles are split into chunks) against: the deckhouse and '
                         'wheelhouse walls, roofs, door openings and ladder volumes, the upper-deck rails, the roof rail, the mast, the console, the 4 ice chests, the 6 '
                         'fishing spots (0.5 m standing circle, 1.9 m tall), the gangway landing (x -2.4..-0.8, z -7.55..-6.4), the bulwark inner face (offset outline, 0.17 m '
                         'in), the interior furniture of the stock deckhouse and the stock deck clutter that is baked into the tug mesh (soft). gap_m is the box-to-box '
                         'distance; overlap true means the volumes intersect. Intended contacts (a cable over the cap, a base clamped to a rail post) are marked allowed. '
                         'Every module is also tested against every other module (parent/child and swap pairs excepted).',
        clearance_problems=problems, models=[manifest_entry(g, clear) for g in mods],
        total_triangles=sum(g.tris() for g in mods), export_status=status,
        not_built=['smoker (design 3.7 lists one; the task did not ask for it)', 'the tug-end tow line mesh (builder draws a rope between Tug_TowFairlead mod.tow_point and Tug_Dinghy mod.tow_point)'],
    )
    return m

def main(export=True, bake=True):
    mods = build_all()
    clear, problems = clearance_report(mods)
    if problems:
        print('CLEARANCE PROBLEMS:'); print('\n'.join(problems))
    tug = H.build_crew_boat()
    tug_ob, obs = objects_for(mods, tug, bake=bake)
    status = {}
    if export:
        os.makedirs(EXPORT_DIR, exist_ok=True)
        for ob, g in zip(obs, mods):
            status[g.name] = H.export_if_changed(ob, os.path.join(EXPORT_DIR, g.name + '.fbx'))
    man = build_manifest(mods, clear, problems, status)
    if export:
        with open(MANIFEST_PATH, 'w', encoding='utf-8') as fh:
            json.dump(man, fh, indent=1)
    print('\n'.join('%-26s tris=%5d verts=%5d %s' % (g.name, g.tris(), len(g.V), status.get(g.name, '')) for g in mods))
    print('TOTAL', man['total_triangles'], 'problems', len(problems))
    return mods, tug, tug_ob, obs

# ---------------------------------------------------------------------------------------------------------------------------------------
# Previews (Eevee, the harbour's toon preview). Preview-only lights, water and the tow rope never reach an FBX.
# ---------------------------------------------------------------------------------------------------------------------------------------
def tank_water_material():
    m = bpy.data.materials.get('FG_Preview_TankWater')
    if m:
        return m
    m = bpy.data.materials.new('FG_Preview_TankWater'); m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = H.srgb_to_lin((0.3, 0.64, 0.66)) + (1,)
    bsdf.inputs['Roughness'].default_value = 0.08
    bsdf.inputs['Alpha'].default_value = 0.45
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return m

def add_light(name, pos_u, energy, radius=0.25):
    ld = bpy.data.lights.new(name, 'POINT')
    ld.energy = energy; ld.shadow_soft_size = radius; ld.color = (1.0, 0.93, 0.8)
    ob = bpy.data.objects.new(name, ld)
    H.collection('Preview').objects.link(ob)
    ob.location = H.to_blender(Vector(pos_u))
    return ob

def tow_rope_object(mods):
    fl = next(g for g in mods if g.name == 'Tug_TowFairlead').mod['tow_point']
    dg = next(g for g in mods if g.name == 'Tug_Dinghy')
    tp = Vector(dg.mod['tow_point']) + Vector(DG_PLACEMENT['position'])
    a, b = Vector(fl), tp
    pts = []
    for k in range(13):
        t = k / 12.0
        p = a * (1 - t) + b * t
        p.y -= 0.9 * math.sin(math.pi * t) * 0.6
        pts.append(p)
    g = M('FG_Preview_TowRope', dict(), 'preview only', rays=0)
    g.add(tube(pts, 0.022, 5), S['Sand'], L['Cloth'], 1.0)
    dv = next(g2 for g2 in mods if g2.name == 'Tug_Davit')
    hk = next(g2 for g2 in mods if g2.name == 'Tug_Davit_Hook')
    g.add(tube([Vector(dv.mod['sheave']), Vector(hk.pivot['position'])], 0.008, 4), S['Rock'], L['none'], 1.0)      # the davit wire (the builder draws this one)
    return g

def setup_preview(mods, tug_ob, obs):
    pm = H.preview_material()
    wm = tank_water_material()
    for ob, g in zip([tug_ob] + obs, [None] + mods):
        if not ob.data.materials:
            ob.data.materials.append(wm if (g is not None and g.material) else pm)
    rope = tow_rope_object(mods)
    rob = H.make_object2(rope)
    rob.data.materials.append(pm)
    H.preview_scene2()
    H.toon_preview()
    dg = next(ob for ob, g in zip(obs, mods) if g.name == 'Tug_Dinghy')
    dg.location = H.to_blender(Vector(DG_PLACEMENT['position']))
    dg.rotation_euler = (0.0, 0.0, math.radians(-DG_PLACEMENT['yaw_deg']))
    return rob

def cut_region(ob, lo_u, hi_u):
    # preview only: remove the faces of the tug mesh whose centre lies in a Unity-axes box (e.g. the helm wheel, to see the dash behind it)
    a, b = H.to_blender(Vector(lo_u)), H.to_blender(Vector(hi_u))
    lo = Vector([min(a[i], b[i]) for i in range(3)]); hi = Vector([max(a[i], b[i]) for i in range(3)])
    bm = bmesh.new(); bm.from_mesh(ob.data)
    kill = [f for f in bm.faces if all(lo[i] <= f.calc_center_median()[i] <= hi[i] for i in range(3))]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(ob.data); bm.free()

def pose_lid(obs, mods, deg):
    # preview only: swing the live-well lid open about its hinge
    lid = next(ob for ob, g in zip(obs, mods) if g.name == 'Tug_LiveWell_Lid')
    pv = H.to_blender(Vector(next(g for g in mods if g.name == 'Tug_LiveWell_Lid').pivot['position']))
    R = Matrix.Rotation(math.radians(deg), 3, 'X')
    lid.rotation_euler = (math.radians(deg), 0.0, 0.0)
    lid.location = pv - R @ pv

def set_visible(mods, obs, rope, names):
    for ob, g in zip(obs, mods):
        ob.hide_render = g.name not in names
        ob.hide_viewport = ob.hide_render
    rope.hide_render = 'Tug_Dinghy' not in names

def render_shot(path, eye_u, target_u, lens=26, res=(1280, 720), samples=24):
    sc = bpy.context.scene
    cam = H.preview_scene()
    eye, tgt = H.to_blender(Vector(eye_u)), H.to_blender(Vector(target_u))
    cam.location = eye
    cam.rotation_euler = (tgt - eye).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    cam.data.clip_start = 0.05
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
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    H.shrink_png(path, 48)

SHOTS = {
    'stock':     dict(eye=(8.0, 7.2, -29.0), tgt=(0.4, 2.7, -7.5), lens=31, vis='stock', res=(960, 540)),
    'upgraded':  dict(eye=(8.0, 7.2, -29.0), tgt=(0.4, 2.7, -7.5), lens=31, vis='upgraded', res=(960, 540)),
    'aft':       dict(eye=(-3.3, 3.7, -9.9), tgt=(0.8, 1.9, -5.8), lens=34, vis='upgraded', hide=('Tug_Dinghy',), res=(1280, 720), lid_open_deg=100.0),
    'galley':    dict(eye=(-0.1, 2.62, -2.35), tgt=(0.5, 2.25, 2.6), lens=23, vis='upgraded', res=(1280, 720), lights=[((0.0, 3.25, 1.0), 700.0), ((0.2, 3.0, 2.2), 250.0)]),
    'bunks':     dict(eye=(0.3, 2.7, 1.2), tgt=(-1.1, 2.0, -1.0), lens=24, vis='upgraded', res=(1280, 720), lights=[((0.0, 3.25, 0.4), 700.0)]),
    'dash_stock':    dict(eye=(0.15, 5.5, 1.5), tgt=(0.4, 4.8, 2.95), lens=30, vis='stock', res=(960, 540), lights=[((0.0, 5.65, 1.9), 500.0)], cut_wheel=True),
    'dash_upgraded': dict(eye=(0.15, 5.5, 1.5), tgt=(0.4, 4.8, 2.95), lens=30, vis='upgraded', hide=('Tug_Dinghy',), res=(960, 540), lights=[((0.0, 5.65, 1.9), 500.0)], cut_wheel=True),
    'hull':      dict(eye=(7.5, -0.4, -4.5), tgt=(0.0, -1.2, 1.2), lens=24, vis='upgraded', res=(1280, 720), water_alpha=0.12),
}

def preview_main(shot, outdir):
    mods = build_all()
    tug = H.build_crew_boat()
    tug_ob, obs = objects_for(mods, tug, bake=True)
    rope = setup_preview(mods, tug_ob, obs)
    s = SHOTS[shot]
    names = set(INSTALL_SETS[s['vis']]) - set(s.get('hide', ()))
    set_visible(mods, obs, rope, names)
    if s.get('cut_wheel'):
        cut_region(tug_ob, (-0.55, 4.3, 2.2), (0.55, 5.4, 2.62))      # the wheel ring and spokes
        cut_region(tug_ob, (-0.52, 4.65, 2.2), (0.52, 5.4, 2.85))     # the hub
    if 'lid_open_deg' in s:
        pose_lid(obs, mods, s['lid_open_deg'])
    for i, (pos, en) in enumerate(s.get('lights', [])):
        add_light('FG_PV_Light%d' % i, pos, en)
    if 'water_alpha' in s:
        w = bpy.data.materials.get('FG_Preview_ToonWater')
        w.node_tree.nodes['Principled BSDF'].inputs['Alpha'].default_value = s['water_alpha']
    os.makedirs(outdir, exist_ok=True)
    render_shot(os.path.join(outdir, 'shot_%s.png' % shot), s['eye'], s['tgt'], s['lens'], s['res'])

def hstack_png(left, right, out):
    import numpy as np
    def load(path):
        img = bpy.data.images.load(path, check_existing=False)
        w, h = img.size
        px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
        bpy.data.images.remove(img)
        return px
    a, b = load(left), load(right)
    h = min(a.shape[0], b.shape[0])
    gap = np.full((h, 6, 4), 1.0, dtype=np.float32)          # a thin white divider
    px = np.concatenate([a[:h], gap, b[:h]], axis=1)
    img = bpy.data.images.new('fg_compose', px.shape[1], px.shape[0], alpha=False)
    img.pixels.foreach_set(px.reshape(-1))
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)

def compose(shots_dir, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    hstack_png(os.path.join(shots_dir, 'shot_stock.png'), os.path.join(shots_dir, 'shot_upgraded.png'), os.path.join(out_dir, 'tug_stock_vs_upgraded.png'))
    hstack_png(os.path.join(shots_dir, 'shot_dash_stock.png'), os.path.join(shots_dir, 'shot_dash_upgraded.png'), os.path.join(out_dir, 'wheelhouse_dash_stock_vs_upgraded.png'))
    import shutil
    for a, b in (('shot_aft.png', 'aft_deck_closeup.png'), ('shot_galley.png', 'galley.png'), ('shot_hull.png', 'hull_sonar_underwater.png')):
        if os.path.exists(os.path.join(shots_dir, a)):
            shutil.copyfile(os.path.join(shots_dir, a), os.path.join(out_dir, b))

if __name__ == '__main__':
    if os.environ.get('FG_TUG_COMPOSE'):
        compose(os.environ['FG_TUG_COMPOSE'], PREVIEW_DIR)
        sys.exit(0)
    shot = os.environ.get('FG_TUG_SHOT')
    if shot:
        preview_main(shot, os.environ.get('FG_TUG_OUT', PREVIEW_DIR))
    else:
        main()
