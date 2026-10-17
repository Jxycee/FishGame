# fg_props.py (2026-10-17, branch harbour-props): small hand-held and catchable props for FishGame (design 3.2, 3.3, 3.4, 5.3), modelled
# procedurally in Blender. Run it headless:  FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_props.py
# main() builds every prop (plain, plus a "fished-up" variant where the thing comes out of the water), bakes AO into the vertex colours,
# exports one FBX per model to Art/Export/Props, writes props_manifest.json and renders the flat-lay previews to Art/Previews/Props.
#
# It imports Group, the geometry primitives, make_object, export_if_changed and the palette slots S / layers L from fg_harbour.py (the
# import has no side effects: main() is guarded). Encoding is the harbour's: authored in UNITY axes (x right, y up, z forward), mapped with
# to_blender; UV0 "UVMap" in metres; UV1 "Layer" = (palette slot, texture layer); vertex colour "Col" = 0.5 x baked AO; analytic or smooth
# normals; no materials exported; deterministic (random.Random(SEED ^ crc32("props.<name>"))).
#
# Conventions for every prop:
#   * The front of a prop faces +z (Unity forward); a viewer standing in front of it looks toward -z.
#   * Pivot at the base centre (the point that rests on the ground), or at the grip for held items (the manifest says which).
#   * Held items (technique gear) point along +z from the grip; vertical tools (shovel, auger) point down -y. See each manifest entry.
#   * "Fished-up" variants (<Name>_Fished) are the same mesh plus weed strands, barnacle clusters and a darker wet tint in the vertex
#     colour (0.5 x ao x WET_TINT). They share the plain model's pivot and bounds to within the weed.
#
# Palette. S (fg_harbour) slots 0-10 are reused. Slots 11-15 are the harbour's preview-only extras and are NOT used here. NEW slots 16-31
# (the props palette extension, P below) must be appended to the Unity palette texture by the PC step; the preview colours are in PAL.
import bpy, bmesh, math, json, os, sys, zlib, random
from mathutils import Vector, Matrix, noise

def _root():
    if os.environ.get('FG_ROOT'):
        return os.environ['FG_ROOT']
    if '__file__' in globals():
        return os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    return os.getcwd()

ROOT = _root()
sys.path.insert(0, os.path.join(ROOT, 'Art', 'Blender'))
import fg_harbour as H                                    # side-effect free on import (main() only under __main__)
from fg_harbour import (Group, xform, mirror_fix, bm_geo, rbox, lathe, tube, prism, text_geo, taper_tube, ry, rx, rz, clamp, to_blender,
                        S, L, export_if_changed, srgb_to_lin, M_U2B)

EXPORT_DIR = os.path.join(ROOT, 'Art', 'Export', 'Props')
PREVIEW_DIR = os.path.join(ROOT, 'Art', 'Previews', 'Props')
MANIFEST_PATH = os.path.join(EXPORT_DIR, 'props_manifest.json')
SCRIPT_REL = 'Art/Blender/fg_props.py'

# Props palette extension (slots 16-31); each (slot, preview sRGB colour).
P = dict(Brass=16, Bronze=17, Steel=18, Iron=19, GlassGreen=20, GlassBrown=21, GlassBlue=22, GlassClear=23, Gold=24, Paper=25,
         ShellPink=26, ShellCream=27, Ice=28, Khaki=29, White=30, Navy=31)
PAL = list(H.PALETTE_SUMMER[:11]) + [(0.95, 0.96, 0.98)] + [(0.5, 0.5, 0.5)] * 4 + [
    (0.78, 0.60, 0.22), (0.58, 0.38, 0.20), (0.52, 0.56, 0.60), (0.24, 0.25, 0.28),
    (0.28, 0.58, 0.40), (0.55, 0.33, 0.14), (0.33, 0.55, 0.78), (0.80, 0.89, 0.90),
    (0.93, 0.74, 0.20), (0.92, 0.86, 0.68), (0.88, 0.60, 0.56), (0.92, 0.85, 0.70),
    (0.72, 0.88, 0.95), (0.66, 0.58, 0.40), (0.95, 0.96, 0.98), (0.16, 0.24, 0.38)]
assert len(PAL) == 32
L0, LW, LC, LR, LD = L['none'], L['Wood'], L['Cloth'], L['Rock'], L['Dirt']
WET_TINT = 0.6             # fished-up variants: vertex colour multiplier (the "darker wet tint")
MAX_TRIS = 3000

def rng(name):
    return H.rng('props.' + name)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Geometry helpers (Unity axes, local frame; every function returns a geo tuple (V, N, F, UV) unless it adds to a Group)
# ---------------------------------------------------------------------------------------------------------------------------------------
def up_to(d):
    return Vector((0, 1, 0)).rotation_difference(Vector(d).normalized()).to_matrix()

def scl(geo, s):
    V, N, F, UV = geo
    return ([Vector((v.x * s[0], v.y * s[1], v.z * s[2])) for v in V],
            [Vector((n.x / s[0], n.y / s[1], n.z / s[2])).normalized() for n in N], F, UV)

def flip(geo):
    V, N, F, UV = geo
    return (V, [-n for n in N], [list(reversed(f)) for f in F], [list(reversed(u)) for u in UV])

def ico(rad, sub=2, scale=(1.0, 1.0, 1.0), amt=0.0, seed=0.0):
    # Ellipsoid: icosphere, optional position-only noise on the radius (deterministic).
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    off = Vector((seed, seed * 0.7 + 3.1, seed * 1.3 + 7.7))
    for v in bm.verts:
        d = v.co.normalized()
        k = 1.0 + amt * noise.noise(d * 2.1 + off)
        v.co = Vector((d.x * rad * scale[0] * k, d.y * rad * scale[1] * k, d.z * rad * scale[2] * k))
    geo = bm_geo(bm, normals='smooth', uv='xz')
    bm.free()
    return geo

def torus(R, r, segs=12, sides=5):
    # Ring in the local xy plane (axis z).
    pts = [Vector((R * math.cos(a), R * math.sin(a), 0.0)) for a in [2 * math.pi * i / segs for i in range(segs)]]
    return tube(pts, r, sides, caps=False, closed=True)

def torus_xz(R, r, segs=12, sides=5):
    # Ring lying flat (axis y).
    pts = [Vector((R * math.cos(a), 0.0, R * math.sin(a))) for a in [2 * math.pi * i / segs for i in range(segs)]]
    return tube(pts, r, sides, caps=False, closed=True)

def surface(fn, nu, nv, wrap_u=False, thick=0.0, uvs=2.5):
    # Parametric patch fn(u, v) -> point, u,v in 0..1. thick > 0 solidifies it into a thin closed shell.
    bm = bmesh.new()
    cols = nu if wrap_u else nu + 1
    rows = [[bm.verts.new(fn(i / nu, j / nv)) for i in range(cols)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            i2 = (i + 1) % cols if wrap_u else i + 1
            bm.faces.new((rows[j][i], rows[j][i2], rows[j + 1][i2], rows[j + 1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if thick > 0.0:
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=thick)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    V, N, F, UV = bm_geo(bm, normals='smooth', uv='xz')
    bm.free()
    UV = [[(V[k].x * uvs, (V[k].z + V[k].y) * uvs) for k in f] for f in F]
    return V, N, F, UV

def cube(size):
    # Plain 12-triangle box with flat normals (hidden or tiny parts: straps, brackets, pins).
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    V, N, F, UV = [], [], [], []
    for axis in range(3):
        for sgn in (-1, 1):
            n = Vector((0, 0, 0)); n[axis] = sgn
            a, b = [i for i in range(3) if i != axis]
            quad = []
            for (ua, ub) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = Vector((0, 0, 0)); p[axis] = sgn * (sx, sy, sz)[axis]; p[a] = ua * (sx, sy, sz)[a]; p[b] = ub * (sx, sy, sz)[b]
                quad.append(p)
            base = len(V)
            V += quad; N += [n] * 4
            f = [base, base + 1, base + 2, base + 3]
            if (quad[1] - quad[0]).cross(quad[2] - quad[1]).dot(n) < 0:
                f = f[::-1]
            F.append(f)
            UV.append([(V[k][a], V[k][b]) for k in f])
    return V, N, F, UV

def cbox(g, c, size, slot, layer=0, ao=0.5, rot=None):
    g.add(xform(cube(size), c, rot), slot, layer, ao)

def text_lo(body, size, extrude):
    # text_geo with the curve resolution at 1 (fewer triangles; the lettering is chunky anyway).
    cu = bpy.data.curves.new('fg_txt', 'FONT')
    cu.body = body; cu.size = size; cu.extrude = extrude; cu.resolution_u = 1
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    ob = bpy.data.objects.new('fg_txt', cu)
    bpy.context.scene.collection.objects.link(ob)
    oe = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    m2 = oe.to_mesh()
    bm = bmesh.new(); bm.from_mesh(m2)
    oe.to_mesh_clear()
    bpy.data.objects.remove(ob, do_unlink=True); bpy.data.curves.remove(cu)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    geo = bm_geo(bm, normals='flat', uv='xz')
    bm.free()
    return geo

def glyph_map(R, U):
    # text_geo glyph frame (x right, y up, z toward the reader) -> world: x -> R, y -> U, z -> -(R x U) (Unity is left-handed, so the
    # numeric map is a reflection and the windings are reversed with mirror_fix). Returns the 3x3 matrix.
    R, U = Vector(R), Vector(U)
    n = -(R.cross(U))
    return Matrix(((R.x, U.x, n.x), (R.y, U.y, n.y), (R.z, U.z, n.z)))

def text_world(body, size, extrude, c, R, U, fit=None):
    geo = text_lo(body, size, extrude)
    V = geo[0]
    if fit:
        w = max(v.x for v in V) - min(v.x for v in V)
        k = fit / w
        geo = scl(geo, (k, k, 1.0))
        V = geo[0]
    cx = (max(v.x for v in V) + min(v.x for v in V)) / 2
    cy = (max(v.y for v in V) + min(v.y for v in V)) / 2
    M = glyph_map(R, U)
    c = Vector(c)
    out = ([c + M @ (v - Vector((cx, cy, 0))) for v in geo[0]], [M @ n for n in geo[1]], geo[2], geo[3])
    return mirror_fix(out)

def size_of(g):
    used = sorted(set(k for f in g.F for k in f))
    lo = [min(g.V[k][i] for k in used) for i in range(3)]
    hi = [max(g.V[k][i] for k in used) for i in range(3)]
    return lo, hi

# ---- Group shorthands ----
def box(g, c, size, r, slot, layer=L0, ao=0.5, rot=None, segs=1):
    H.add_box(g, c, size, r, slot, layer, ao, rot, segs)

def lat(g, c, prof, slot, layer=L0, ao=0.5, sides=12, rot=None, cap0=True, cap1=True, ring_slots=None):
    H.add_lathe(g, c, prof, slot, layer, ao, sides=sides, rot=rot, cap0=cap0, cap1=cap1, ring_slots=ring_slots)

def rod(g, a, b, r0, slot, layer=L0, ao=0.5, r1=None, sides=8, cap0=True, cap1=True):
    a, b = Vector(a), Vector(b)
    d = b - a
    geo, _ = lathe([(0.0, r0), (d.length, r0 if r1 is None else r1)], sides, cap0, cap1)
    g.add(xform(geo, a, up_to(d)), slot, layer, ao)

def rodp(g, a, b, prof, slot, layer=L0, ao=0.5, sides=8, ring_slots=None, cap0=True, cap1=True):
    # prof: [(t, r)] with t the fraction along a -> b.
    a, b = Vector(a), Vector(b)
    d = b - a
    lat(g, a, [(t * d.length, r) for t, r in prof], slot, layer, ao, sides=sides, rot=up_to(d), cap0=cap0, cap1=cap1, ring_slots=ring_slots)

def ball(g, c, rad, slot, layer=L0, ao=0.5, scale=(1, 1, 1), sub=2, rot=None, amt=0.0, seed=0.0):
    g.add(xform(ico(rad, sub, scale, amt, seed), c, rot), slot, layer, ao)

def ring(g, c, R, r, slot, layer=L0, ao=0.5, rot=None, segs=12, sides=5, flat=False):
    g.add(xform(torus_xz(R, r, segs, sides) if flat else torus(R, r, segs, sides), c, rot), slot, layer, ao)

def path(g, pts, r, slot, layer=L0, ao=0.5, sides=5, caps=True):
    g.add(tube(pts, r, sides, caps=caps), slot, layer, ao)

def arc(c, R, a0, a1, n, plane='xy'):
    out = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        ca, sa = R * math.cos(a), R * math.sin(a)
        out.append(Vector(c) + (Vector((ca, sa, 0)) if plane == 'xy' else Vector((0, sa, ca)) if plane == 'yz' else Vector((ca, 0, sa))))
    return out

def text_on(g, body, size, extrude, c, R, U, slot, layer=L0, ao=0.6, fit=None):
    g.add(text_world(body, size, extrude, c, R, U, fit), slot, layer, ao)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Fished-up dressing: weed strands, barnacle clusters, wet tint. Draws come from the model's own stream, so a plain model never changes.
# ---------------------------------------------------------------------------------------------------------------------------------------
def encrust(g, r, weed, barn, grow=1.0):
    lo, hi = size_of(g)
    size = max(hi[i] - lo[i] for i in range(3))
    h = hi[1] - lo[1]
    verts = list(zip(g.V, g.N))
    up = [(v, n) for v, n in verts if v.y > lo[1] + 0.3 * h and n.y > -0.4] or verts
    side = [(v, n) for v, n in verts if abs(n.y) < 0.8] or verts
    rad = clamp(size * 0.016, 0.003, 0.013) * grow
    for _ in range(weed * 2):
        v, n = r.choice(up)
        ln = clamp(size * r.uniform(0.25, 0.5), 0.05, 0.55) * grow
        d = Vector((r.uniform(-0.6, 0.6), -1.0, r.uniform(-0.6, 0.6))).normalized()
        pts = [v + n * rad]
        for _k in range(2):
            pts.append(pts[-1] + d * (ln / 2) + Vector((r.uniform(-1, 1), 0.0, r.uniform(-1, 1))) * ln * 0.12)
        radii = [rad, rad * 0.75, rad * 0.4]
        slot = r.choice([S['FoliageDark'], S['Reed'], S['Foliage']])
        g.add(taper_tube(pts, radii, 3), slot, L['Foliage'], 0.6)
    for _ in range(barn * 2):
        v, n = r.choice(side)
        t1 = n.orthogonal().normalized()
        t2 = n.cross(t1)
        for _k in range(2):
            br = clamp(size * r.uniform(0.012, 0.022), 0.004, 0.02) * grow
            p = v + (t1 * r.uniform(-1, 1) + t2 * r.uniform(-1, 1)) * br * 2.2 - n * br * 0.2
            geo, _ = lathe([(0.0, br), (br * 1.15, br * 0.5)], 5, False, True)
            g.add(xform(geo, p, up_to(n)), r.choice([P['ShellCream'], S['Sand'], P['ShellCream']]), L0, 0.6)
    g.tint = WET_TINT

# ---------------------------------------------------------------------------------------------------------------------------------------
# Registry: every model is declared with @model; fished=dict(weed=, barn=, grow=) gives it a <Name>_Fished variant.
# ---------------------------------------------------------------------------------------------------------------------------------------
REG = []

def model(name, group, pivot, desc, fished=None, who=None, held=None, ground=True, lay=None):
    # lay: a Unity-space rotation used ONLY by the flat-lay previews to lay a vertical tool on its side.
    def deco(fn):
        REG.append(dict(name=name, group=group, pivot=pivot, desc=desc, fished=fished, who=who, held=held, ground=ground, fn=fn, lay=lay))
        return fn
    return deco

def build(spec, wet=False):
    name = 'Prop_' + spec['name'] + ('_Fished' if wet else '')
    g = Group(name)
    g.tint = 1.0
    g.ground = spec['ground']
    r = rng(spec['name'])
    spec['fn'](g, r)
    if wet:
        encrust(g, rng(spec['name'] + '.fished'), **spec['fished'])
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# AO bake (the harbour's per-vertex BVH ray-cast bake; self occlusion plus a ground plane at y = 0 for props that rest on the ground)
# ---------------------------------------------------------------------------------------------------------------------------------------
def bake_one(g, rays=40):
    from mathutils.bvhtree import BVHTree
    lo, hi = size_of(g)
    size = max(hi[i] - lo[i] for i in range(3))
    dist = clamp(size * 0.35, 0.04, 0.45)
    verts = [tuple(v) for v in g.V]
    polys = [tuple(f) for f in g.F]
    if g.ground:
        b = len(verts)
        verts += [(-4, 0, -4), (-4, 0, 4), (4, 0, 4), (4, 0, -4)]
        polys.append((b, b + 1, b + 2, b + 3))
        # the plane's normal must be +y: (-4,-4)->(-4,4)->(4,4) is counter-clockwise seen from -y in a right-handed frame; fix by checking
        n = (Vector(verts[b + 1]) - Vector(verts[b])).cross(Vector(verts[b + 2]) - Vector(verts[b + 1]))
        if n.y < 0:
            polys[-1] = (b + 3, b + 2, b + 1, b)
    bvh = BVHTree.FromPolygons(verts, polys, all_triangles=False)
    dirs = H.hemisphere_dirs(rays)
    ao = []
    for p, n in zip(g.V, g.N):
        t = n.orthogonal().normalized()
        b2 = n.cross(t)
        org = p + n * 0.002
        occ = 0.0
        for d0 in dirs:
            d = t * d0.x + n * d0.y + b2 * d0.z
            o, left = org, dist
            for _ in range(6):
                loc, nrm, idx, dd = bvh.ray_cast(o, d, left)
                if loc is None:
                    break
                if nrm.dot(d) > 0.0:         # back face: pass through (a vertex buried in another part is not blacked out)
                    o = loc + d * 0.001
                    left -= dd + 0.001
                    if left <= 0.0:
                        break
                    continue
                occ += 1.0 - 0.5 * ((dist - left + dd) / dist)
                break
        ao.append(max(0.0, 1.0 - occ / len(dirs)))
    return ao

def write_ao(ob, g, raw):
    me = ob.data
    ca = me.color_attributes['Col']
    n = len(me.loops)
    out = [0.0] * (4 * n)
    for pi, poly in enumerate(me.polygons):
        lo = g.AO[pi]
        for li in poly.loop_indices:
            a = raw[me.loops[li].vertex_index]
            ao = (lo + (1.0 - lo) * clamp(a, 0.0, 1.0)) * g.tint
            out[4 * li:4 * li + 4] = (0.5 * ao, 0.5 * ao, 0.5 * ao, 1.0)
    ca.data.foreach_set('color', out)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Preview material / scene
# ---------------------------------------------------------------------------------------------------------------------------------------
def preview_material():
    m = bpy.data.materials.get('FG_Props_Palette')
    if m:
        return m
    m = bpy.data.materials.new('FG_Props_Palette')
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = 0.8
    uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = 'Layer'
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    mth = nt.nodes.new('ShaderNodeMath'); mth.operation = 'MULTIPLY_ADD'
    mth.inputs[1].default_value = 1.0 / 32.0; mth.inputs[2].default_value = 0.5 / 32.0
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    while len(els) < len(PAL):
        els.new(0.5)
    for i, c in enumerate(PAL):
        els[i].position = i / 32.0
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

def flat_material(name, rgb, rough=0.9):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = srgb_to_lin(rgb) + (1.0,)
    bsdf.inputs['Roughness'].default_value = rough
    return m

def setup_scene():
    sc = bpy.context.scene
    for nm in ('FG_Props_Ground', 'FG_Props_Sun', 'FG_Props_Fill', 'FG_Props_Cam'):
        o = bpy.data.objects.get(nm)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)
    me = bpy.data.meshes.new('FG_Props_Ground')
    me.from_pydata([(-60, -60, 0), (60, -60, 0), (60, 60, 0), (-60, 60, 0)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new('FG_Props_Ground', me)
    H.collection('Preview').objects.link(ground)
    me.materials.append(flat_material('FG_Props_GroundMat', (0.60, 0.57, 0.50), 0.95))
    ld = bpy.data.lights.new('FG_Props_Sun', 'SUN'); ld.energy = 3.4; ld.angle = math.radians(10)
    sun = bpy.data.objects.new('FG_Props_Sun', ld); H.collection('Preview').objects.link(sun)
    sun.rotation_euler = (math.radians(48), math.radians(6), math.radians(160))
    cd = bpy.data.cameras.new('FG_Props_Cam'); cd.type = 'ORTHO'; cd.clip_end = 200
    cam = bpy.data.objects.new('FG_Props_Cam', cd); H.collection('Preview').objects.link(cam)
    sc.camera = cam
    if not sc.world:
        sc.world = bpy.data.worlds.new('World')
    sc.world.use_nodes = True
    bg = next(n for n in sc.world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = srgb_to_lin((0.74, 0.80, 0.84)) + (1,)
    bg.inputs['Strength'].default_value = 1.0
    try:
        sc.view_settings.view_transform = 'AgX'
    except Exception:
        pass
    try:
        sc.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    try:
        sc.eevee.taa_render_samples = 24
    except Exception:
        pass
    return cam

def label(text, pos_u, size):
    cu = bpy.data.curves.new('lbl_' + text, 'FONT')
    cu.body = text; cu.size = size; cu.align_x = 'CENTER'; cu.align_y = 'TOP'
    ob = bpy.data.objects.new('lbl_' + text, cu)
    H.collection('Preview').objects.link(ob)
    ob.location = to_blender(pos_u) + Vector((0, 0, 0.004))
    ob.rotation_euler = (0, 0, 0)               # reads left to right for the camera that looks along Unity -z (the props face +z)
    cu.materials.append(flat_material('FG_Props_Label', (0.12, 0.12, 0.14), 0.9))
    return ob

def render_layout(path, entries, norm, title, gap=0.25, lab=0.11, elev=52.0, res=(1280, 720)):
    """entries: [(label, plain_ob, plain_group, fished_ob or None, spec)]. Items are scaled to `norm` m on their longest side (preview only;
    the real sizes are in the manifest) and laid out on a ground plane, plain on the left of each pair, fished-up on the right.
    The camera looks along Unity -z, i.e. at the front (+z) of every prop."""
    sc = bpy.context.scene
    for o in [o for o in bpy.data.objects if o.name.startswith('lbl_')]:
        bpy.data.objects.remove(o, do_unlink=True)
    cam = setup_scene()
    Mb = M_U2B.to_3x3()
    cells = []
    for lbl, po, pg, fo, spec in entries:
        lay = spec['lay'] or Matrix.Identity(3)
        used = sorted(set(k for f in pg.F for k in f))
        rv = [lay @ pg.V[k] for k in used]
        lo = [min(v[i] for v in rv) for i in range(3)]
        hi = [max(v[i] for v in rv) for i in range(3)]
        ext = [hi[i] - lo[i] for i in range(3)]
        k = min(norm / max(ext), 3.0)
        pair = 2 if fo is not None else 1
        w = ext[0] * k
        lift = not pg.ground
        cells.append(dict(lbl=lbl, po=po, fo=fo, k=k, w=w, d=ext[2] * k, ctr=((hi[0] + lo[0]) / 2, (hi[2] + lo[2]) / 2), pair=pair, lay=lay,
                          y0=(-lo[1] * k) if lift else 0.0, cw=max(pair * w + (pair - 1) * 0.12, len(lbl) * lab * 0.62) + gap))
    aspect = res[0] / res[1]
    se, ce = math.sin(math.radians(elev)), math.cos(math.radians(elev))
    best = None
    for row_w in [x * 0.5 for x in range(8, 70)]:
        rows, cur, cw = [], [], 0.0
        for c in cells:
            if cur and cw + c['cw'] > row_w:
                rows.append(cur); cur, cw = [], 0.0
            cur.append(c); cw += c['cw']
        if cur:
            rows.append(cur)
        maxw = max(sum(c['cw'] for c in row) for row in rows)
        depth = sum(max(c['d'] for c in row) + lab * 2.4 + 0.25 for row in rows)
        need = max(maxw + 0.8, ((depth + 0.6) * se + 1.4 * ce) * aspect)
        if best is None or need < best[0]:
            best = (need, rows, maxw, depth)
    need, rows, maxw, total_d = best
    z = 0.0
    for row in rows:
        width = sum(c['cw'] for c in row)
        depth = max(c['d'] for c in row)
        x = -width / 2
        for c in row:
            pw = c['pair'] * c['w'] + (c['pair'] - 1) * 0.12
            x0 = x + (c['cw'] - gap - pw) / 2 + c['w'] / 2
            xs = [x0] + ([x0 + c['w'] + 0.12] if c['pair'] == 2 else [])
            for ob, xc in zip((c['po'], c['fo']), xs):
                if ob is None:
                    continue
                cx, cz = c['ctr']
                loc_u = Vector((-xc - cx * c['k'], c['y0'], -(z + depth / 2) - cz * c['k']))
                Rb = (Mb @ c['lay'] @ Mb.transposed()) * c['k']
                ob.matrix_world = Matrix.Translation(Mb @ loc_u) @ Rb.to_4x4()
            label(c['lbl'], (-(xs[0] + xs[-1]) / 2, 0.0, -(z - 0.08)), lab)
            x += c['cw']
        z += depth + lab * 2.4 + 0.25
    cam.data.ortho_scale = need
    ctr_u = Vector((0.0, 0.2, -(total_d / 2 - 0.3)))
    eye_u = ctr_u + Vector((0.0, se, ce)) * 30.0
    eye, tgt = to_blender(eye_u), to_blender(ctr_u)
    cam.location = eye
    cam.rotation_euler = (tgt - eye).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGB'
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    H.shrink_png(path, 48)

# =======================================================================================================================================
# 1. Villager belongings (design 5.3). Sizes are real-world; the Unity builder may scale a small one up for readability (see manifest).
# =======================================================================================================================================
@model('ReadingGlasses', 'villager', 'base: on the ground between the temple tips', "Doug Pellow's reading glasses (navy frames, standing open).",
       fished=dict(weed=2, barn=1), who='Doug Pellow (under the T-head)')
def m_glasses(g, r):
    fr = P['Navy']
    for sx in (-1, 1):
        c = Vector((sx * 0.037, 0.034, 0.0))
        ring(g, c, 0.027, 0.0042, fr, L0, 0.5, segs=10, sides=3)
        lat(g, c, [(-0.0012, 0.0255), (0.0012, 0.0255)], P['GlassClear'], L0, 0.6, sides=10, rot=rx(90))
        hinge = Vector((sx * 0.0655, 0.036, -0.002))
        path(g, [hinge, Vector((sx * 0.0665, 0.034, -0.11)), Vector((sx * 0.0665, 0.012, -0.142)), Vector((sx * 0.066, 0.004, -0.148))],
             0.0034, fr, L0, 0.5, sides=3)
    path(g, arc((0, 0.0275, 0), 0.0105, 20, 160, 4), 0.0034, fr, L0, 0.5, sides=3)
    for sx in (-1, 1):
        cbox(g, (sx * 0.0155, 0.0225, 0.0035), (0.004, 0.012, 0.004), P['Brass'], L0, 0.5)

@model('Teapot', 'villager', 'base centre', "Lorraine Bass's grandmother's teapot: cream porcelain with a red band and gold trim.",
       fished=dict(weed=2, barn=2), who='Lorraine Bass (pier)')
def m_teapot(g, r):
    cream, red, gold = P['ShellCream'], S['Roof'], P['Gold']
    lat(g, (0, 0, 0), [(0.0, 0.058), (0.008, 0.08), (0.04, 0.1), (0.085, 0.108), (0.12, 0.098), (0.136, 0.074), (0.14, 0.064)], cream, L0, 0.5,
        sides=14, cap0=True, cap1=False, ring_slots=[cream, cream, red, red, cream, gold])
    lat(g, (0, 0, 0), [(0.14, 0.06), (0.152, 0.054), (0.168, 0.03), (0.175, 0.012)], cream, L0, 0.5, sides=14, cap0=False, cap1=True,
        ring_slots=[gold, cream, cream])
    ball(g, (0, 0.188, 0), 0.015, gold, L0, 0.5, sub=2)
    g.add(taper_tube([Vector(p) for p in [(0.085, 0.05, 0), (0.125, 0.065, 0), (0.158, 0.1, 0), (0.185, 0.145, 0)]], [0.02, 0.016, 0.012, 0.0085], 7),
          cream, L0, 0.5)
    path(g, arc((-0.1, 0.085, 0), 0.062, 105, -105, 8)[::-1], 0.0105, cream, L0, 0.5, sides=6)

@model('ChapelBell', 'villager', 'base: centre of the mouth, mouth down on the ground', "The chapel bell: ~0.8 m, heavy, bronze, on a carrying beam.",
       fished=dict(weed=4, barn=4, grow=1.4), who='Reverend Gil (reef wreck)', ground=True)
def m_bell(g, r):
    br, bt = P['Bronze'], P['Brass']
    outer = [(0.0, 0.385), (0.035, 0.37), (0.12, 0.295), (0.23, 0.24), (0.34, 0.205), (0.46, 0.185), (0.56, 0.17), (0.65, 0.14), (0.72, 0.1), (0.755, 0.055), (0.765, 0.0)]
    lat(g, (0, 0, 0), outer, br, L0, 0.5, sides=14, cap0=False, cap1=False)
    for h, rr in ((0.19, 0.255), (0.235, 0.24), (0.6, 0.16)):
        ring(g, (0, h, 0), rr, 0.009, bt, L0, 0.5, segs=14, sides=3, flat=True)
    inner = [(0.012, 0.345), (0.2, 0.25), (0.4, 0.18), (0.6, 0.13), (0.75, 0.0)]
    geo, _ = lathe(inner, 14, False, False)
    g.add(flip(geo), br, L0, 0.45)
    ring(g, (0, 0.02, 0), 0.375, 0.026, bt, L0, 0.5, segs=14, sides=4, flat=True)          # the sound bow
    rod(g, (0, 0.66, 0), (0, 0.1, 0), 0.011, P['Iron'], L0, 0.45, sides=6)
    ball(g, (0, 0.09, 0), 0.05, P['Iron'], L0, 0.5, sub=1)
    ring(g, (0, 0.795, 0), 0.06, 0.017, br, L0, 0.5, rot=ry(90), segs=8, sides=4)         # the crown loop
    box(g, (0, 0.865, 0), (0.95, 0.085, 0.085), 0.02, S['WoodDark'], LW, 0.5)             # carrying beam, two straps
    for sx in (-0.045, 0.045):
        cbox(g, (sx, 0.865, 0), (0.016, 0.1, 0.105), P['Iron'], L0, 0.5)
    for sx in (-0.43, 0.43):
        cbox(g, (sx, 0.865, 0), (0.05, 0.09, 0.09), S['Wood'], LW, 0.5)

@model('TacklebySign', 'villager', 'base centre of the two feet; faces +z', "The bait shop sign: TACKLEBY'S / BAIT, TACKLE, OPINIONS on a tilted board.",
       fished=dict(weed=4, barn=5, grow=1.2), who='Marge Tackleby (lagoon)')
def m_sign(g, r):
    ctr = Vector((0, 0.42, 0))
    rot = rz(2.2)
    def put(geo, slot, layer, ao):
        g.add(xform(geo, ctr, rot), slot, layer, ao)
    put(cube((1.5, 0.62, 0.05)), S['Dock'], LW, 0.5)
    for y in (-0.33, 0.33):
        put(xform(cube((1.58, 0.05, 0.075)), (0, y, 0)), S['WoodDark'], LW, 0.5)
    for x in (-0.77, 0.77):
        put(xform(cube((0.05, 0.69, 0.075)), (x, 0, 0)), S['WoodDark'], LW, 0.5)
    for x in (-0.5, 0.5):
        put(xform(torus(0.022, 0.006, 6, 3), (x, 0.37, 0.0)), P['Iron'], L0, 0.5)
    put(xform(cube((1.0, 0.012, 0.014)), (0, -0.035, 0.03)), S['Roof'], L0, 0.6)       # the dash between the lines
    t1 = text_world("TACKLEBY'S", 0.2, 0.008, (0, 0.12, 0.03), (-1, 0, 0), (0, 1, 0), fit=1.22)
    t2 = text_world("BAIT, TACKLE, OPINIONS", 0.2, 0.006, (0, -0.15, 0.03), (-1, 0, 0), (0, 1, 0), fit=1.3)
    put(t1, P['Navy'], L0, 0.6)
    put(t2, P['Navy'], L0, 0.6)
    for x in (-0.55, 0.55):
        cbox(g, (x, 0.04, 0.02), (0.1, 0.08, 0.2), S['WoodDark'], LW, 0.5)

@model('LostNetBundle', 'villager', 'base centre of the heap', "The Nettle twins' lost net: a tangled heap with floats, lead weights and a trailing rope.",
       fished=dict(weed=6, barn=3, grow=1.1), who='Gary and Gary Nettle (reef)')
def m_netbundle(g, r):
    ball(g, (0, 0.1, 0), 0.3, S['Sand'], LC, 0.55, scale=(1.0, 0.42, 0.78), sub=3, amt=0.14, seed=1.7)
    for i in range(6):
        a = r.uniform(0, 2 * math.pi)
        c = (0.17 * math.cos(a) * r.uniform(0.3, 1.0), r.uniform(0.07, 0.17), 0.13 * math.sin(a) * r.uniform(0.3, 1.0))
        R = r.uniform(0.12, 0.2)
        ring(g, c, R, 0.013, S['Sand'], LC, 0.55, rot=ry(r.uniform(0, 180)) @ rx(r.uniform(55, 100)), segs=8, sides=3)
    for i in range(4):
        a = i * 1.6 + 0.4
        ball(g, (0.26 * math.cos(a), 0.12 + 0.03 * (i % 2), 0.2 * math.sin(a)), 0.042, S['Accent'], L0, 0.55, sub=1)
    for i in range(3):
        a = i * 2.1 + 1.1
        lat(g, (0.2 * math.cos(a), 0.05, 0.16 * math.sin(a)), [(0.0, 0.02), (0.045, 0.02)], P['Iron'], L0, 0.5, sides=6, rot=rx(r.uniform(60, 90)))
    pts = [Vector((0.2, 0.05, 0.1))]
    for k in range(1, 9):
        pts.append(pts[-1] + Vector((0.07, 0.0, 0.05 * math.sin(k * 1.1))) )
        pts[-1].y = 0.02
    path(g, pts, 0.014, S['Sand'], LC, 0.55, sides=5)

@model('BrassPropeller', 'villager', 'base: the lowest blade tips rest on y = 0, hub on the axis; axis along z', "Ines Varga's sunken brass propeller: three blades, bore and key.",
       fished=dict(weed=5, barn=6, grow=1.0), who='Ines Varga (wreck, low tide)')
def m_prop(g, r):
    br = P['Brass']
    hub_y = 0.17
    lat(g, (0, hub_y, 0), [(-0.075, 0.05), (-0.06, 0.07), (0.03, 0.066), (0.07, 0.05), (0.1, 0.02), (0.11, 0.0)], br, L0, 0.5, sides=14, rot=rx(90),
        cap0=True, cap1=False)
    outline = [(-0.03, 0.05), (-0.075, 0.13), (-0.092, 0.21), (-0.05, 0.285), (0.016, 0.30), (0.056, 0.22), (0.06, 0.12), (0.036, 0.05)]
    for i in range(3):
        blade = prism(outline, 0.016, 0.004, 1)
        M = rz(i * 120.0) @ ry(24.0) @ ry(90.0)
        g.add(xform(blade, (0, hub_y, 0.01), M), br, L0, 0.5)
    lat(g, (0, hub_y, 0), [(-0.082, 0.028), (-0.075, 0.032)], P['Iron'], L0, 0.5, sides=10, rot=rx(90))
    box(g, (0, hub_y, -0.078), (0.014, 0.02, 0.012), 0.003, P['Iron'], L0, 0.5)

@model('CrankHandle', 'villager', 'grip: centre of the wooden handle; the axle points +z', "Ambrose Lock's lock crank: iron arm, square axle end, worn wooden grip.",
       fished=dict(weed=2, barn=2), who='Ambrose Lock (creek mouth)', ground=False)
def m_crank(g, r):
    fe = P['Iron']
    lat(g, (0, 0, 0.07), [(0.0, 0.011), (0.012, 0.019), (0.07, 0.023), (0.13, 0.02), (0.14, 0.0)], S['Wood'], LW, 0.5, sides=10, rot=rx(-90), cap0=True, cap1=True)
    box(g, (0, -0.15, 0.08), (0.036, 0.34, 0.02), 0.007, fe, L0, 0.5)
    lat(g, (0, -0.31, 0.09), [(0.0, 0.034), (0.03, 0.034)], fe, L0, 0.5, sides=12, rot=rx(90))
    rod(g, (0, -0.31, 0.09), (0, -0.31, 0.3), 0.013, fe, L0, 0.5, sides=8)
    box(g, (0, -0.31, 0.3), (0.04, 0.04, 0.05), 0.005, fe, L0, 0.5)
    box(g, (0, 0.0, 0.085), (0.04, 0.035, 0.022), 0.006, fe, L0, 0.5)

@model('SmokingHat', 'villager', 'base centre of the brim, hat standing', "Wendell Krill's lucky smoking hat: battered canvas hat, fish pin and a corncob pipe in the band.",
       fished=dict(weed=3, barn=1), who='Wendell Krill (gull nest, sea arch)')
def m_hat(g, r):
    kh = P['Khaki']
    crown, _ = lathe([(0.03, 0.098), (0.05, 0.1), (0.1, 0.097), (0.125, 0.085), (0.14, 0.055), (0.146, 0.0)], 14, False, True)
    V = crown[0]
    for v in V:
        if v.y > 0.1:
            v.y -= 0.016 * clamp(v.x / 0.09 + 0.3, 0.0, 1.2)
    g.add(crown, kh, LC, 0.5)
    brim, _ = lathe([(0.0, 0.215), (0.01, 0.2), (0.024, 0.14), (0.04, 0.097)], 16, False, False)
    g.add(brim, kh, LC, 0.5)
    g.add(xform(flip(brim), (0, -0.007, 0)), kh, LC, 0.45)
    lat(g, (0, 0.033, 0), [(0.0, 0.1), (0.03, 0.103)], P['Navy'], LC, 0.5, sides=14, cap0=False, cap1=False)
    ball(g, (0, 0.05, 0.1), 0.025, S['Accent'], L0, 0.55, scale=(1.0, 0.55, 0.25), sub=2)
    g.add(xform(prism([(0.0, 0.0), (-0.02, 0.014), (-0.02, -0.014)], 0.004, 0.0, 1), (0, 0.05, 0.1), ry(90) @ rz(0)), S['Accent'], L0, 0.55)
    rod(g, (0.065, 0.045, 0.085), (0.13, 0.09, 0.19), 0.006, S['WoodDark'], LW, 0.5, sides=6)
    lat(g, (0.13, 0.09, 0.19), [(0.0, 0.012), (0.03, 0.018), (0.04, 0.018)], S['Dock'], LW, 0.5, sides=7, rot=up_to((0.3, 0.7, 0.2)), cap0=True, cap1=True)

@model('FieldNotebook', 'villager', 'base centre, lying flat; spine toward -x', "Dr. Penny Hollis's field notebook: navy cover, elastic band, pencil and a fish sticker.",
       fished=dict(weed=2, barn=2), who='Dr. Penny Hollis (lagoon)')
def m_notebook(g, r):
    box(g, (0, 0.013, 0), (0.17, 0.026, 0.23), 0.007, P['Navy'], LC, 0.5, segs=1)
    cbox(g, (0.004, 0.013, 0), (0.158, 0.018, 0.216), P['Paper'], L0, 0.5)
    cbox(g, (0.05, 0.0275, 0), (0.012, 0.008, 0.236), S['Roof'], LC, 0.5)
    for sx, sz in ((-1, -1), (-1, 1), (1, -1), (1, 1)):
        cbox(g, (sx * 0.074, 0.0125, sz * 0.104), (0.026, 0.028, 0.026), P['Brass'], L0, 0.5)
    rod(g, (-0.04, 0.034, 0.1), (0.03, 0.034, -0.11), 0.0045, S['Accent'], L0, 0.5, sides=6)
    rod(g, (0.03, 0.034, -0.11), (0.0345, 0.034, -0.12), 0.0045, S['WoodDark'], LW, 0.5, r1=0.0012, sides=6)
    ball(g, (-0.03, 0.0285, -0.04), 0.03, S['Accent'], L0, 0.55, scale=(1.0, 0.1, 0.55), sub=2)
    text_on(g, 'FIELD NOTES', 0.03, 0.002, (-0.005, 0.0285, 0.065), (-1, 0, 0), (0, 0, -1), P['Gold'], L0, 0.6, fit=0.1)

@model('BingoBottle', 'villager', 'base centre, standing', "Reverend Gil's bingo-ball message bottle: a B-7 ball for a stopper and a paper tag on the neck.",
       fished=dict(weed=2, barn=2), who='Reverend Gil (bottles around the harbour)')
def m_bingo(g, r):
    gl = P['GlassClear']
    lat(g, (0, 0, 0), [(0.0, 0.03), (0.004, 0.04), (0.012, 0.0445), (0.12, 0.0445), (0.135, 0.04), (0.16, 0.025), (0.19, 0.0175), (0.222, 0.0175),
                       (0.228, 0.0215), (0.236, 0.0215)], gl, L0, 0.55, sides=10, cap0=True, cap1=False)
    ball(g, (0, 0.256, 0), 0.028, P['White'], L0, 0.55, sub=2)
    text_on(g, 'B7', 0.03, 0.0025, (0, 0.256, 0.0282), (-1, 0, 0), (0, 1, 0), S['Roof'], L0, 0.6, fit=0.022)
    ring(g, (0, 0.19, 0), 0.0195, 0.0022, S['Sand'], LC, 0.55, segs=10, sides=4, flat=True)
    cbox(g, (0.0, 0.158, 0.033), (0.05, 0.065, 0.004), P['Paper'], L0, 0.55)
    rod(g, (0.0, 0.19, 0.02), (0.0, 0.188, 0.031), 0.0018, S['Sand'], LC, 0.55, sides=4)

# =======================================================================================================================================
# 2. Collectibles: message bottles, shells, sea glass, treasure chest, dig mound and hole.
# =======================================================================================================================================
def scroll(g, a, b, rr, slot=None):
    slot = P['Paper'] if slot is None else slot
    rod(g, a, b, rr, slot, L0, 0.55, sides=7)

@model('Bottle_A', 'collectible', 'base centre, standing', "Message bottle A: green wine bottle, the scroll sticks out of the neck.",
       fished=dict(weed=2, barn=2), who='bottles around the harbour')
def m_bottle_a(g, r):
    gl = P['GlassGreen']
    lat(g, (0, 0, 0), [(0.0, 0.03), (0.005, 0.04), (0.014, 0.0455), (0.13, 0.0455), (0.15, 0.04), (0.18, 0.024), (0.205, 0.0165), (0.24, 0.0165),
                       (0.246, 0.0205), (0.254, 0.0205)], gl, L0, 0.55, sides=12, cap0=True, cap1=False)
    rod(g, (0.004, 0.21, 0.0), (0.012, 0.33, 0.01), 0.011, P['Paper'], L0, 0.55, sides=7, r1=0.012)
    ring(g, (0.0085, 0.26, 0.005), 0.0125, 0.003, S['Roof'], LC, 0.55, rot=up_to((0.07, 1, 0.07)) @ rx(90), segs=8, sides=4)
    ball(g, (0.012, 0.332, 0.01), 0.012, P['Paper'], L0, 0.55, scale=(1, 0.5, 1), sub=1)

@model('Bottle_B', 'collectible', 'base centre, standing', "Message bottle B: squat brown flask, corked and sealed with red wax, paper tag on a string.",
       fished=dict(weed=2, barn=2), who='bottles around the harbour')
def m_bottle_b(g, r):
    gl = P['GlassBrown']
    lat(g, (0, 0, 0), [(0.0, 0.05), (0.006, 0.07), (0.03, 0.082), (0.075, 0.082), (0.105, 0.065), (0.125, 0.032), (0.14, 0.023), (0.175, 0.023),
                       (0.18, 0.027), (0.188, 0.027)], gl, L0, 0.55, sides=12, cap0=True, cap1=False)
    lat(g, (0, 0.18, 0), [(0.0, 0.021), (0.02, 0.024), (0.026, 0.0)], S['Sand'], LW, 0.55, sides=10, cap0=True, cap1=True)
    ball(g, (0, 0.21, 0), 0.021, S['Roof'], L0, 0.55, scale=(1.0, 0.7, 1.0), sub=2)
    ring(g, (0, 0.148, 0), 0.0245, 0.003, S['Sand'], LC, 0.55, segs=10, sides=4, flat=True)
    path(g, [Vector((0.024, 0.148, 0.0)), Vector((0.04, 0.12, 0.03)), Vector((0.05, 0.1, 0.06))], 0.002, S['Sand'], LC, 0.55, sides=4)
    box(g, (0.05, 0.09, 0.07), (0.05, 0.06, 0.004), 0.0015, P['Paper'], L0, 0.55, rot=ry(25) @ rx(-10))

@model('Bottle_C', 'collectible', 'base centre, lying on its side along z (neck toward +z)', "Message bottle C: long blue flask lying down, cork with string, a paper strip in the neck.",
       fished=dict(weed=2, barn=2), who='bottles around the harbour')
def m_bottle_c(g, r):
    gl = P['GlassBlue']
    y0 = 0.05
    lat(g, (0, y0, -0.17), [(0.0, 0.036), (0.004, 0.046), (0.012, 0.05), (0.2, 0.05), (0.23, 0.04), (0.28, 0.022), (0.33, 0.016), (0.355, 0.016),
                            (0.362, 0.02), (0.37, 0.02)], gl, L0, 0.55, sides=12, cap0=True, cap1=False, rot=rx(90))
    lat(g, (0, y0, 0.19), [(0.0, 0.0175), (0.022, 0.021), (0.03, 0.0)], S['Sand'], LW, 0.55, sides=10, cap0=True, cap1=True, rot=rx(90))
    ring(g, (0, y0, 0.12), 0.019, 0.003, S['Roof'], LC, 0.55, rot=ry(0), segs=10, sides=4)
    box(g, (0.0, y0 + 0.02, 0.0), (0.08, 0.004, 0.07), 0.0015, P['Paper'], L0, 0.55, rot=rz(8))
    box(g, (0, 0.01, -0.12), (0.06, 0.02, 0.02), 0.004, P['GlassBlue'], L0, 0.55)    # a flat chock under the belly so it never rolls

# ---- shells ----
@model('Shell_Scallop', 'collectible', 'base centre, lying on its curved back', "Scallop: fan of 11 ribs with two ears, cream with a pink flush.",
       fished=dict(weed=2, barn=3), who='beach')
def m_scallop(g, r):
    def fn(u, v):
        th = math.radians(-62 + 124 * u)
        rho = 0.02 + 0.095 * v
        rib = 0.5 + 0.5 * math.cos(2 * math.pi * 6 * u)
        h = 0.05 * (rho / 0.115) ** 0.7 * (1.0 - 0.5 * (th / 1.08) ** 2) + 0.008 * rib * (rho / 0.115)
        return Vector((rho * math.sin(th), h + 0.006, rho * math.cos(th) - 0.035))
    g.add(surface(fn, 26, 4, thick=0.006), P['ShellCream'], LR, 0.55)
    for sx in (-1, 1):
        cbox(g, (sx * 0.02, 0.014, -0.0425), (0.032, 0.012, 0.014), P['ShellPink'], LR, 0.55)

@model('Shell_Whelk', 'collectible', 'base centre, lying on its side', "Whelk: spiral shell with a flared mouth and a short canal.",
       fished=dict(weed=2, barn=3), who='beach')
def m_whelk(g, r):
    pts, rad = [], []
    n = 26
    for i in range(n + 1):
        t = i / n
        a = t * 4.6 * math.pi
        R = 0.012 + 0.05 * t
        y = 0.12 * (1.0 - t) * (1.0 if t < 0.0 else 1.0)
        pts.append(Vector((R * math.cos(a), 0.012 + 0.11 * (1 - t) ** 1.0 * 0.6 + 0.0, R * math.sin(a))))
        rad.append(0.004 + 0.034 * t ** 1.1)
    pts = [Vector((p.x, 0.02 + 0.1 * (1 - i / n) - 0.0, p.z)) for i, p in enumerate(pts)]
    g.add(taper_tube(pts[::-1][::-1], rad, 8), P['ShellCream'], LR, 0.55)
    pts2 = [pts[-1], pts[-1] + Vector((0.03, -0.045, 0.02)), pts[-1] + Vector((0.05, -0.09, 0.03))]
    g.add(taper_tube(pts2, [0.034, 0.02, 0.008], 8), S['Sand'], LR, 0.55)

@model('Shell_Cowrie', 'collectible', 'base centre, aperture down', "Cowrie: glossy humped shell with a toothed slit, speckled.",
       fished=dict(weed=1, barn=2), who='beach')
def m_cowrie(g, r):
    ball(g, (0, 0.03, 0), 0.05, P['ShellCream'], L0, 0.55, scale=(0.64, 0.68, 0.96), sub=2)
    ball(g, (0, 0.012, 0), 0.043, P['ShellPink'], L0, 0.55, scale=(0.18, 0.1, 0.96), sub=2)
    for i in range(6):
        ball(g, (0.02 * math.sin(i * 2.3), 0.066 - 0.012 * abs(i - 2.5) / 2.5 * 1.0 - 0.0, 0.026 * (i - 2.5) * 0.7), 0.0085, S['Dock'], L0, 0.55, scale=(1, 0.5, 1), sub=1)

@model('Shell_Clam', 'collectible', 'base centre, hinge toward -z', "Clam: two ridged valves, the top one ajar over a pink inside.",
       fished=dict(weed=2, barn=3), who='beach')
def m_clam(g, r):
    prof = [(0.0, 0.07)]
    for i in range(1, 9):
        t = i / 9
        prof.append((0.04 * t ** 0.8 + (0.0035 if i % 2 else 0.0), 0.07 * math.sqrt(max(0.0, 1.0 - t * t)) + 0.002))
    prof.append((0.043, 0.0))
    prof = [(h, rr) for h, rr in prof]
    bot, _ = lathe(prof, 14, False, True)
    g.add(xform(scl(bot, (1.0, 1.0, 0.82)), (0, 0.004, 0)), P['ShellCream'], LR, 0.55)
    lat(g, (0, 0.012, 0), [(0.0, 0.066), (0.002, 0.0)], P['ShellPink'], LR, 0.55, sides=14, rot=rx(0), cap0=False, cap1=False)
    top = scl(lathe(prof, 14, False, True)[0], (1.0, 1.0, 0.82))
    g.add(xform(top, (0, 0.05, -0.052), rx(-142)), P['ShellCream'], LR, 0.55)

@model('Shell_SandDollar', 'collectible', 'base centre, lying flat', "Sand dollar: flat domed disc with a five-petal star.",
       fished=dict(weed=1, barn=2), who='beach')
def m_sanddollar(g, r):
    lat(g, (0, 0, 0), [(0.0, 0.058), (0.008, 0.062), (0.016, 0.054), (0.026, 0.03), (0.03, 0.0)], P['ShellCream'], LR, 0.55, sides=14, cap0=True, cap1=False)
    for i in range(5):
        a = math.radians(72 * i + 18)
        c = Vector((0.026 * math.sin(a), 0.026, 0.026 * math.cos(a)))
        ball(g, c, 0.02, S['Dock'], L0, 0.55, scale=(0.22, 0.06, 1.0), sub=1, rot=ry(math.degrees(a)))
    ball(g, (0, 0.031, 0), 0.006, S['Dock'], L0, 0.55, scale=(1, 0.5, 1), sub=1)

@model('Shell_Razor', 'collectible', 'base centre, lying along x', "Razor clam: long, slightly curved shell with growth bands.",
       fished=dict(weed=1, barn=2), who='beach')
def m_razor(g, r):
    pts = [Vector((x, 0.012 + 0.006 * (1 - (x / 0.075) ** 2) * 0.0, 0.008 * math.sin(x * 20 + 1))) for x in [(-0.07 + 0.14 * i / 8) for i in range(9)]]
    pts = [Vector((p.x, 0.012, p.z * 0.0 + 0.012 * (1 - (p.x / 0.075) ** 2))) for p in pts]
    geo = tube(pts, 0.0125, 8, caps=True)
    geo = scl(geo, (1.0, 0.62, 1.0))
    g.add(geo, P['ShellCream'], LR, 0.55)
    for i in range(4):
        x = -0.045 + 0.03 * i
        ring(g, (x, 0.012 * 0.62, 0.012 * (1 - (x / 0.075) ** 2)), 0.0128, 0.0014, S['Dock'], L0, 0.55, rot=ry(90), segs=8, sides=3)

@model('Shell_MoonSnail', 'collectible', 'base centre', "Moon snail: round coiled shell, pink mouth and a pale navel.",
       fished=dict(weed=2, barn=3), who='beach')
def m_moonsnail(g, r):
    ball(g, (0, 0.045, 0), 0.052, P['ShellCream'], L0, 0.55, scale=(1.0, 0.85, 1.0), sub=2)
    ball(g, (0.012, 0.087, -0.01), 0.036, P['ShellCream'], L0, 0.55, scale=(1.0, 0.8, 1.0), sub=2)
    ball(g, (0.02, 0.118, -0.014), 0.022, P['ShellCream'], L0, 0.55, sub=2)
    ball(g, (0.0, 0.032, 0.04), 0.03, P['ShellPink'], L0, 0.55, scale=(0.9, 1.0, 0.45), sub=2)
    path(g, arc((0, 0.06, 0), 0.047, -40, 120, 8, 'xz'), 0.0035, S['Dock'], L0, 0.55, sides=4)

@model('Shell_Turret', 'collectible', 'base centre, resting on its side', "Turret shell: tall ridged spire.",
       fished=dict(weed=1, barn=3), who='beach')
def m_turret(g, r):
    prof = []
    n = 9
    for i in range(n * 2 + 1):
        t = i / (n * 2)
        rr = 0.048 * (1 - t) + 0.004
        prof.append((0.15 * t, rr * (1.0 if i % 2 == 0 else 0.8)))
    geo, _ = lathe(prof, 9, True, True)
    g.add(xform(geo, (-0.08, 0.04, 0.0), rz(-84)), P['ShellCream'], LR, 0.55)
    ball(g, (-0.085, 0.036, 0.0), 0.035, P['ShellPink'], L0, 0.55, scale=(0.5, 0.8, 1.0), sub=1)

# ---- sea glass ----
@model('SeaGlass_Green', 'collectible', 'base centre', "Sea glass, green: frosted rounded pebble.", fished=dict(weed=1, barn=0), who='beach')
def m_sg_green(g, r):
    ball(g, (0, 0.012, 0), 0.026, P['GlassGreen'], L0, 0.55, scale=(1.0, 0.45, 0.82), sub=2, amt=0.1, seed=3.3)

@model('SeaGlass_Brown', 'collectible', 'base centre', "Sea glass, brown: a flat shard worn smooth.", fished=dict(weed=1, barn=0), who='beach')
def m_sg_brown(g, r):
    ball(g, (0, 0.01, 0), 0.034, P['GlassBrown'], L0, 0.55, scale=(1.0, 0.28, 0.6), sub=2, amt=0.12, seed=4.4)

@model('SeaGlass_Blue', 'collectible', 'base centre', "Sea glass, blue: a lumpy chunk.", fished=dict(weed=1, barn=0), who='beach')
def m_sg_blue(g, r):
    ball(g, (0, 0.015, 0), 0.025, P['GlassBlue'], L0, 0.55, scale=(1.0, 0.7, 0.9), sub=2, amt=0.2, seed=5.5)
    ball(g, (0.018, 0.01, 0.01), 0.014, P['GlassBlue'], L0, 0.55, scale=(1.0, 0.7, 0.9), sub=1, amt=0.15, seed=6.6)

@model('SeaGlass_Clear', 'collectible', 'base centre', "Sea glass, white: a worn bottle-neck ring.", fished=dict(weed=1, barn=0), who='beach')
def m_sg_clear(g, r):
    g.add(xform(torus_xz(0.022, 0.0105, 12, 6), (0, 0.0105, 0)), P['GlassClear'], L0, 0.55)
    ball(g, (0.028, 0.008, 0.005), 0.01, P['GlassClear'], L0, 0.55, scale=(1.0, 0.6, 1.0), sub=1)

# ---- treasure chest ----
def chest_body(g, open_=False):
    W, D, Hh = 0.56, 0.34, 0.24
    wd, br = S['Wood'], P['Brass']
    cbox(g, (0, 0.02, 0), (W, 0.04, D), S['WoodDark'], LW, 0.5)
    for sx in (-1, 1):
        box(g, (sx * (W / 2 - 0.0175), Hh / 2, 0), (0.035, Hh, D), 0.01, wd, LW, 0.5)
    for sz in (-1, 1):
        box(g, (0, Hh / 2, sz * (D / 2 - 0.0175)), (W - 0.07, Hh, 0.035), 0.01, wd, LW, 0.5)
    for sx in (-0.19, 0.19):
        cbox(g, (sx, Hh / 2, 0), (0.05, Hh + 0.01, D + 0.014), P['Iron'], L0, 0.5)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cbox(g, (sx * (W / 2 - 0.0125), 0.03, sz * (D / 2 - 0.0125)), (0.045, 0.06, 0.045), P['Iron'], L0, 0.5)

def chest_lid(g, rot, pivot):
    W, D = 0.56, 0.34
    pr = 0.17
    arcp = [(pr * math.cos(math.radians(a)) * 1.0, 0.0 + pr * math.sin(math.radians(a)) * 0.74) for a in range(0, 181, 20)]
    poly = [(z, y) for z, y in arcp]
    lid = prism(poly, W, 0.008, 1)
    def put(geo, slot, layer, ao):
        g.add(xform(geo, pivot, rot), slot, layer, ao)
    put(xform(lid, (0, 0.0, pr)), S['Wood'], LW, 0.5)
    for sx in (-0.19, 0.19):
        poly2 = [(z * 1.05, y * 1.07) for z, y in poly]
        put(xform(prism(poly2, 0.05, 0.006, 1), (sx, -0.002, pr)), P['Iron'], L0, 0.5)
    put(xform(rbox((0.07, 0.07, 0.025), 0.008, 1), (0, 0.065, 2 * pr + 0.012)), P['Brass'], L0, 0.5)

@model('Chest_Closed', 'collectible', 'base centre, lid closed; faces +z', "Treasure chest, closed: planked body, iron straps, brass lock.",
       fished=dict(weed=4, barn=5, grow=1.2), who='dig and wrecks')
def m_chest_closed(g, r):
    chest_body(g)
    chest_lid(g, Matrix.Identity(3), Vector((0, 0.24, -0.17)))

@model('Chest_Open', 'collectible', 'base centre, lid thrown back; faces +z', "Treasure chest, open: lid up, coins and a ruby inside.",
       fished=dict(weed=4, barn=5, grow=1.2), who='dig and wrecks')
def m_chest_open(g, r):
    chest_body(g)
    chest_lid(g, rx(-112), Vector((0, 0.24, 0.17)) * Vector((1, 1, -1)))
    ball(g, (0, 0.2, 0), 0.19, P['Gold'], L0, 0.5, scale=(1.35, 0.32, 0.8), sub=2, amt=0.08, seed=8.1)
    for i in range(7):
        a = i * 2.4
        lat(g, (0.18 * math.cos(a) * (0.4 + 0.1 * (i % 3)), 0.235 + 0.01 * (i % 3), 0.11 * math.sin(a)), [(0.0, 0.026), (0.006, 0.026)], P['Gold'], L0, 0.5,
            sides=6, rot=rx(r.uniform(-25, 25)) @ rz(r.uniform(-25, 25)))
    ball(g, (0.08, 0.255, 0.04), 0.03, S['Roof'], L0, 0.55, sub=1)
    path(g, arc((-0.12, 0.225, 0.0), 0.06, 0, 300, 12, 'xz'), 0.007, P['White'], L0, 0.55, sides=4)

# ---- dig site ----
def lumpy(geo, amt, seed):
    V = geo[0]
    for v in V:
        k = 1.0 + amt * noise.noise(Vector((v.x * 9 + seed, v.y * 9, v.z * 9)))
        v.x *= k; v.z *= k
    return geo

@model('DigMound', 'collectible', 'base centre on the ground', "A fresh dig mound: loose earth heap with clods, a pebble and a spade mark.", ground=True,
       who='dig spots (low tide beach)')
def m_mound(g, r):
    ball(g, (0, 0.04, 0), 0.26, S['WoodDark'], LD, 0.55, scale=(1.0, 0.3, 0.85), sub=3, amt=0.1, seed=9.9)
    for i in range(7):
        a = i * 0.9 + 0.3
        ball(g, (0.2 * math.cos(a), 0.035, 0.17 * math.sin(a)), r.uniform(0.04, 0.07), S['WoodDark'], LD, 0.55, scale=(1.0, 0.65, 1.0), sub=1,
             amt=0.15, seed=a)
    ball(g, (0.06, 0.1, 0.05), 0.04, S['Rock'], LR, 0.55, scale=(1.0, 0.7, 0.9), sub=1, amt=0.15, seed=1.0)
    box(g, (-0.05, 0.1, 0.0), (0.18, 0.012, 0.03), 0.004, S['Dock'], LD, 0.55, rot=rz(12) @ ry(35))     # spade-edge mark

@model('DugHole', 'collectible', 'rim centre at ground level (y = 0); the hole is 0.16 m deep below it', "A dug hole: lipped crater with a dark floor and spoil lumps.", ground=False,
       who='dig spots (low tide beach)')
def m_hole(g, r):
    bowl, _ = lathe([(-0.16, 0.0), (-0.155, 0.08), (-0.1, 0.13), (-0.03, 0.17), (0.02, 0.2), (0.045, 0.225)], 14, False, False)
    g.add(lumpy(bowl, 0.06, 2.0), S['WoodDark'], LD, 0.55, [(P['Iron'] if i < 14 * 2 else S['WoodDark'], LD) for i in range(len(bowl[2]))])
    out = lumpy(lathe([(0.0, 0.42), (0.02, 0.34), (0.04, 0.27), (0.045, 0.225)], 14, False, False)[0], 0.09, 3.0)
    g.add(out, S['WoodDark'], LD, 0.55)
    for i in range(5):
        a = i * 1.3 + 0.2
        ball(g, (0.3 * math.cos(a), 0.03, 0.3 * math.sin(a)), r.uniform(0.03, 0.05), S['WoodDark'], LD, 0.55, scale=(1.0, 0.6, 1.0), sub=1, amt=0.12, seed=a)

# =======================================================================================================================================
# 3. Technique gear (held items). Pivot at the grip. Rods and tools point along +z from the grip (the manifest says "held_axis");
#    the shovel, auger and lantern pole point as noted. Real-world sizes.
# =======================================================================================================================================
def curve_pts(p0, p1, bend, n):
    # Straight line p0 -> p1 plus a parabolic lift of `bend` m (y) at the tip end.
    p0, p1 = Vector(p0), Vector(p1)
    return [p0.lerp(p1, i / n) + Vector((0, bend * (i / n) ** 2, 0)) for i in range(n + 1)]

def guide(g, p, slot, r=0.0035, R=0.011, segs=6, sides=3):
    ring(g, p, R, r, slot, L0, 0.5, segs=segs, sides=sides)

@model('JigLure', 'gear', 'grip: the line eye at the top; hangs along -y', "Jig lure: painted lead head, feather tail, upturned hook.", ground=False,
       held='hangs down from the eye on the line (-y)', lay=rx(90))
def m_jig(g, r):
    ring(g, (0, 0.0, 0), 0.011, 0.0028, P['Steel'], L0, 0.5, segs=8, sides=4)
    ball(g, (0, -0.04, 0), 0.026, S['Roof'], L0, 0.5, scale=(0.82, 1.35, 0.82), sub=2)
    for sx in (-1, 1):
        ball(g, (sx * 0.015, -0.034, 0.014), 0.0075, P['White'], L0, 0.5, sub=1)
        ball(g, (sx * 0.016, -0.034, 0.0205), 0.0035, P['Navy'], L0, 0.5, sub=1)
    path(g, arc((0, -0.105, 0), 0.032, 200, 330, 7)[::-1], 0.0032, P['Steel'], L0, 0.5, sides=4)
    for i in range(7):
        a = i * 2 * math.pi / 7 + 0.3
        d = Vector((0.4 * math.cos(a), -1.0, 0.4 * math.sin(a))).normalized()
        p0 = Vector((0.012 * math.cos(a), -0.075, 0.012 * math.sin(a)))
        g.add(taper_tube([p0, p0 + d * 0.04, p0 + d * 0.085], [0.0085, 0.006, 0.0025], 4), S['Accent'] if i % 2 == 0 else P['White'], L0, 0.5)

@model('TrollingLure', 'gear', 'grip: the line eye on the nose; trails along -z', "Trolling lure: bullet head with eyes and a skirt of 12 strands.", ground=False,
       held='nose at the pivot, trails behind along -z')
def m_troll(g, r):
    back = up_to((0, 0, -1))
    ring(g, (0, 0, 0), 0.012, 0.003, P['Steel'], L0, 0.5, segs=8, sides=4)
    lat(g, (0, 0, -0.006), [(0.0, 0.0), (0.012, 0.02), (0.045, 0.03), (0.08, 0.025), (0.095, 0.016)], S['Roof'], L0, 0.5, sides=12, rot=back, cap0=False,
        cap1=True, ring_slots=[S['Roof'], S['Roof'], P['White'], S['Roof']])
    for sx in (-1, 1):
        ball(g, (sx * 0.026, 0.01, -0.03), 0.012, P['White'], L0, 0.5, sub=1)
        ball(g, (sx * 0.0335, 0.01, -0.03), 0.006, P['Navy'], L0, 0.5, sub=1)
    for i in range(12):
        a = i * 2 * math.pi / 12
        c, s = math.cos(a), math.sin(a)
        p0 = Vector((0.017 * c, 0.017 * s, -0.095))
        g.add(taper_tube([p0, p0 + Vector((0.012 * c, 0.012 * s, -0.07)), p0 + Vector((0.006 * c, 0.006 * s, -0.17))], [0.0075, 0.0055, 0.0018], 3),
              S['Accent'] if i % 2 == 0 else S['Roof'], L0, 0.5)
    path(g, [Vector((0, 0, -0.1)), Vector((0, 0, -0.2)), Vector((0, 0.012, -0.26))], 0.0022, P['Steel'], L0, 0.5, sides=4)
    path(g, arc((0, -0.022, -0.26), 0.034, 90, 300, 7, 'yz'), 0.0028, P['Steel'], L0, 0.5, sides=4)

@model('CastNet_Folded', 'gear', 'grip: the hand loop at the cord end; the bundle lies along +z', "Cast net, folded: corded bundle with a lead-weighted hem, ready to throw.",
       fished=dict(weed=5, barn=2, grow=1.2), held='bundle hangs from the hand loop along +z', ground=False)
def m_net_folded(g, r):
    ring(g, (0, 0, 0), 0.04, 0.007, S['Sand'], LC, 0.55, segs=10, sides=4)
    path(g, [Vector((0, 0, 0.04)), Vector((0, -0.02, 0.12)), Vector((0, -0.04, 0.2))], 0.009, S['Sand'], LC, 0.55, sides=5)
    ball(g, (0, -0.1, 0.34), 0.16, S['Sand'], LC, 0.55, scale=(0.62, 0.55, 1.55), sub=2, amt=0.1, seed=11.0)
    for i in range(5):
        ring(g, (0, -0.1, 0.24 + 0.075 * i), 0.1 - 0.012 * abs(i - 2), 0.008, S['Dock'], LC, 0.55, rot=Matrix.Identity(3), segs=8, sides=3)
    ring(g, (0, -0.1, 0.55), 0.105, 0.019, P['Iron'], L0, 0.5, segs=10, sides=4)
    for i in range(6):
        a = i * math.pi / 3
        ball(g, (0.105 * math.cos(a), -0.1 + 0.105 * math.sin(a), 0.55), 0.024, P['Iron'], L0, 0.5, sub=1)
    ball(g, (0, -0.1, 0.12), 0.03, S['Accent'], L0, 0.55, sub=1)

@model('CastNet_Spread', 'gear', 'grip: the hand loop at the apex, net hangs below; apex at y = 0', "Cast net, spread: a 2.6 m circle with scalloped hem, radial cords and a lead ring.",
       fished=dict(weed=6, barn=2, grow=1.5), held='apex at the pivot; net opens downward (-y), 1.3 m radius', ground=False)
def m_net_spread(g, r):
    R = 1.3
    def fn(u, v):
        a = 2 * math.pi * u
        rr = R * v
        y = -0.5 * v ** 1.6 - 0.05 * (v ** 3) * math.sin(8 * a)
        return Vector((rr * math.cos(a), y, rr * math.sin(a)))
    g.add(surface(fn, 16, 5, wrap_u=True, thick=0.006, uvs=1.2), S['Sand'], LC, 0.55)
    ring(g, (0, -0.5, 0), R, 0.02, P['Iron'], L0, 0.5, segs=20, sides=4, flat=True)
    for i in range(10):
        a = i * 2 * math.pi / 10 + 0.15
        ball(g, (R * math.cos(a), -0.5, R * math.sin(a)), 0.045, P['Iron'], L0, 0.5, scale=(1, 0.8, 1), sub=1)
    ring(g, (0, 0.0, 0), 0.05, 0.008, S['Sand'], LC, 0.55, segs=10, sides=4)
    for i in range(8):
        a = i * math.pi / 4
        path(g, [Vector((0.04 * math.cos(a), 0.0, 0.04 * math.sin(a))), Vector((0.5 * R * math.cos(a), -0.5 * 0.5 ** 1.6 + 0.01, 0.5 * R * math.sin(a))),
                 Vector((R * math.cos(a), -0.5, R * math.sin(a)))], 0.007, S['Dock'], LC, 0.55, sides=3)

@model('CrabPot', 'gear', 'base centre of the pot; rope and buoy lie beside it on +x', "Crab pot with buoy and rope: the harbour LobsterPot (same build) plus a funnel mouth, hatch, bait cage, rope and a buoy.",
       fished=dict(weed=6, barn=4, grow=1.3), held='stands on the ground; carried by the top hoop', ground=True)
def m_crabpot(g, r):
    H.lobster_pot(g, 'CrabPot', (0.0, 0.0, 0.0), 0.0, 0.0)           # copied by reference: identical to fg_harbour's LobsterPot
    cone, _ = lathe([(0.0, 0.17), (0.055, 0.075)], 10, False, False)
    g.add(xform(cone, (0, 0.05, 0.33), rx(-90) @ Matrix.Identity(3)), S['Sand'], LC, 0.55)
    ring(g, (0, 0.05, 0.335), 0.185, 0.011, S['WoodDark'], LW, 0.45, segs=10, sides=4)
    box(g, (0, 0.268, 0), (0.26, 0.022, 0.3), 0.007, S['Wood'], LW, 0.5)                               # the hatch
    box(g, (0.0, 0.282, 0.0), (0.03, 0.03, 0.14), 0.008, P['Iron'], L0, 0.5)
    ring(g, (0, 0.3, 0), 0.04, 0.007, P['Iron'], L0, 0.5, rot=ry(90), segs=8, sides=4)
    lat(g, (0.0, 0.1, -0.1), [(-0.05, 0.045), (0.05, 0.045)], P['Steel'], L0, 0.5, sides=7, rot=Matrix.Identity(3))
    prof = [(-0.13, 0.0), (-0.12, 0.06), (-0.07, 0.11), (0.0, 0.13), (0.07, 0.11), (0.12, 0.06), (0.13, 0.0)]
    lat(g, (0.78, 0.13, 0.16), prof, S['Accent'], L0, 0.55, sides=12, ring_slots=[S['Accent'], S['Accent'], P['White'], P['White'], S['Accent'], S['Accent']])
    ring(g, (0.78, 0.275, 0.16), 0.03, 0.007, P['Iron'], L0, 0.5, segs=8, sides=4)
    pts = [Vector((0, 0.3, 0)), Vector((0.06, 0.32, 0.08)), Vector((0.15, 0.2, 0.18)), Vector((0.25, 0.02, 0.28)), Vector((0.38, 0.018, 0.22)),
           Vector((0.46, 0.02, 0.32)), Vector((0.58, 0.018, 0.2)), Vector((0.7, 0.03, 0.16)), Vector((0.78, 0.275, 0.16))]
    path(g, pts, 0.012, S['Sand'], LC, 0.55, sides=5)

@model('LanternRig', 'gear', 'grip: hand grip on the pole; the pole points along +z, tilted up 6 degrees', "Lantern-fishing rig: 1.5 m pole, hanging cage lantern with a glowing core, a weighted drop line with a hook.",
       fished=dict(weed=3, barn=2, grow=1.0), held='pole along +z; lantern hangs 0.3 m below the tip', ground=False)
def m_lantern(g, r):
    d = Vector((0, 0.105, 1.0)).normalized()
    tip = d * 1.5
    rodp(g, (0, 0, -0.1), tip, [(0.0, 0.021), (0.4, 0.019), (1.0, 0.009)], S['Wood'], LW, 0.5, sides=7)
    rodp(g, (0, 0, -0.06), d * 0.28, [(0.0, 0.0245), (1.0, 0.0245)], S['WoodDark'], LC, 0.5, sides=8)
    path(g, [tip, tip + Vector((0, 0.05, 0.05)), tip + Vector((0, 0.07, 0.0)), tip + Vector((0, 0.03, -0.04))], 0.006, P['Iron'], L0, 0.5, sides=4)
    lc = tip + Vector((0, -0.42, 0))
    rod(g, tip + Vector((0, 0.0, 0)), lc + Vector((0, 0.11, 0)), 0.0035, P['Iron'], L0, 0.5, sides=4)
    lat(g, lc + Vector((0, 0.1, 0)), [(0.0, 0.07), (0.035, 0.05), (0.06, 0.012)], P['Brass'], L0, 0.5, sides=10, cap0=False, cap1=True)
    ring(g, lc + Vector((0, 0.165, 0)), 0.014, 0.004, P['Brass'], L0, 0.5, segs=8, sides=4)
    lat(g, lc + Vector((0, -0.12, 0)), [(0.0, 0.07), (0.02, 0.075), (0.03, 0.0)], P['Brass'], L0, 0.5, sides=10, cap0=True, cap1=True)
    for i in range(6):
        a = i * math.pi / 3
        rod(g, lc + Vector((0.065 * math.cos(a), -0.1, 0.065 * math.sin(a))), lc + Vector((0.055 * math.cos(a), 0.1, 0.055 * math.sin(a))), 0.004,
            P['Brass'], L0, 0.5, sides=4)
    ball(g, lc + Vector((0, -0.01, 0)), 0.048, S['Accent'], L0, 0.4, sub=2)
    rod(g, lc + Vector((0, -0.14, 0)), lc + Vector((0, -0.42, 0)), 0.0025, S['Sand'], LC, 0.5, sides=4)
    ball(g, lc + Vector((0, -0.45, 0)), 0.018, P['Iron'], L0, 0.5, sub=1)
    path(g, arc(lc + Vector((0, -0.49, 0)), 0.022, 100, 330, 7)[::-1], 0.0028, P['Steel'], L0, 0.5, sides=4)

@model('FightingRod', 'gear', 'grip: the foregrip centre; rod along +z, butt at z = -0.45, tip at z = +1.35', "Heavy fighting rod: 1.8 m stout blank, rubber grips, gimbal butt, big conventional reel and seven guides.", ground=False,
       held='along +z, tip bent up 0.2 m')
def m_fightrod(g, r):
    pts = curve_pts((0, 0, -0.1), (0, 0.0, 1.35), 0.22, 12)
    radii = [0.0235 - 0.0165 * (i / 12) for i in range(13)]
    g.add(taper_tube(pts, radii, 7), P['Iron'], L0, 0.5)
    rodp(g, (0, 0, -0.34), (0, 0, 0.2), [(0.0, 0.032), (0.5, 0.036), (1.0, 0.033)], S['Roof'], LC, 0.5, sides=9)
    rodp(g, (0, 0, -0.45), (0, 0, -0.34), [(0.0, 0.02), (1.0, 0.032)], P['Iron'], L0, 0.5, sides=9)
    ball(g, (0, 0, -0.47), 0.024, P['Steel'], L0, 0.5, sub=2)
    rod(g, (-0.05, 0, -0.455), (0.05, 0, -0.455), 0.01, P['Steel'], L0, 0.5, sides=6)         # gimbal pin
    cbox(g, (0, 0.066, 0.01), (0.07, 0.04, 0.22), P['Steel'], L0, 0.5)             # reel seat / foot
    box(g, (0, 0.13, 0.01), (0.1, 0.085, 0.11), 0.014, P['Iron'], L0, 0.5, segs=1)               # frame
    lat(g, (-0.055, 0.135, 0.01), [(0.0, 0.058), (0.006, 0.062), (0.11, 0.062), (0.116, 0.058)], P['Brass'], L0, 0.5, sides=10, rot=rz(-90))
    lat(g, (-0.05, 0.135, 0.01), [(0.0, 0.035), (0.1, 0.035)], P['Steel'], L0, 0.5, sides=8, rot=rz(-90))
    rod(g, (0.058, 0.135, 0.01), (0.09, 0.2, 0.01), 0.008, P['Steel'], L0, 0.5, sides=5)
    ball(g, (0.095, 0.21, 0.01), 0.017, S['Accent'], L0, 0.5, sub=1)
    ring(g, (0.052, 0.098, 0.05), 0.026, 0.006, P['Brass'], L0, 0.5, rot=ry(90), segs=10, sides=4)    # star drag
    gz = [0.25, 0.5, 0.78, 1.0, 1.18, 1.3]
    for z in gz:
        t = (z + 0.1) / 1.45
        y = 0.22 * t * t + radii[min(12, int(t * 12))] + 0.012
        guide(g, (0, y, z), P['Steel'], 0.004, 0.016 - 0.007 * t, 6, 3)
    guide(g, (0, 0.22 + 0.007, 1.35), P['Steel'], 0.004, 0.008, 6, 3)
    path(g, [Vector((0, 0.18, 0.0)), Vector((0, 0.1, 0.12)), Vector((0, 0.1, 0.26))], 0.0022, S['Sand'], LC, 0.5, sides=3, caps=False)

@model('IceAuger', 'gear', 'grip: centre of the T-handle crossbar; the auger points down -y', "Ice auger: T-handle with wooden grips, orange shaft collar, steel helical flight and two cutting blades.", ground=False,
       held='vertical: handle at the pivot, blades 1.0 m below (-y)', lay=rx(90))
def m_auger(g, r):
    rod(g, (-0.18, 0, 0), (0.18, 0, 0), 0.014, P['Steel'], L0, 0.5, sides=7)
    for sx in (-1, 1):
        rodp(g, (sx * 0.09, 0, 0), (sx * 0.2, 0, 0), [(0.0, 0.025), (0.5, 0.022), (1.0, 0.026)], S['Wood'], LW, 0.5, sides=8)
    rod(g, (0, 0.0, 0), (0, -1.0, 0), 0.0135, P['Steel'], L0, 0.5, sides=7)
    rodp(g, (0, -0.02, 0), (0, -0.3, 0), [(0.0, 0.026), (1.0, 0.02)], S['Accent'], L0, 0.5, sides=9)
    helix = [Vector((0.052 * math.cos(a), -0.4 - 0.58 * a / (6.0 * math.pi), 0.052 * math.sin(a))) for a in [6.0 * math.pi * i / 30 for i in range(31)]]
    path(g, helix, 0.011, P['Steel'], L0, 0.5, sides=4)
    for sx in (-1, 1):
        blade = prism([(0.0, 0.0), (0.0, 0.07), (0.075, 0.0)], 0.01, 0.0, 1)
        g.add(xform(blade, (0, -1.0, 0), ry(90 if sx > 0 else -90)), P['Iron'], L0, 0.5)
    rod(g, (0, -1.0, 0), (0, -1.05, 0), 0.012, P['Iron'], L0, 0.5, r1=0.001, sides=5)

@model('IceHoleRim', 'gear', 'rim centre at ground level (y = 0); the water is 0.05 m below', "Ice-hole rim: chunky lipped ice ring around a dark hole with loose chips.", ground=False,
       held='placed on the ice surface')
def m_icerim(g, r):
    bowl = lumpy(lathe([(-0.08, 0.0), (-0.078, 0.2), (-0.03, 0.235), (0.03, 0.25), (0.08, 0.275)], 16, False, False)[0], 0.05, 4.0)
    g.add(bowl, P['Ice'], L0, 0.55)
    out = lumpy(lathe([(0.0, 0.62), (0.035, 0.5), (0.075, 0.37), (0.082, 0.275)], 16, False, False)[0], 0.1, 5.0)
    g.add(out, P['Ice'], L0, 0.55)
    for i in range(4):
        a = i * 1.7 + 0.5
        ball(g, (0.42 * math.cos(a), 0.05, 0.42 * math.sin(a)), r.uniform(0.045, 0.08), P['Ice'], L0, 0.55, scale=(1.0, 0.7, 1.0), sub=1, amt=0.2, seed=a)
    lat(g, (0, -0.05, 0), [(0.0, 0.226), (0.002, 0.226)], P['Navy'], L0, 0.4, sides=16)

@model('FlyRod', 'gear', 'grip: centre of the cork grip; rod along +z, reel under the butt', "Fly rod and reel: 2.7 m olive blank, cork grip, brass reel seat, fly reel and nine snake guides.", ground=False,
       held='along +z, tip bent up 0.25 m')
def m_flyrod(g, r):
    pts = curve_pts((0, 0.0, -0.05), (0, 0.0, 2.7), 0.3, 16)
    radii = [0.0105 - 0.0085 * (i / 16) ** 0.8 for i in range(17)]
    g.add(taper_tube(pts, radii, 5), S['FoliageDark'], L0, 0.5)
    rodp(g, (0, 0, -0.17), (0, 0, 0.1), [(0.0, 0.017), (0.25, 0.0215), (0.75, 0.0215), (1.0, 0.016)], S['Dock'], LC, 0.5, sides=9)
    rodp(g, (0, 0, -0.27), (0, 0, -0.17), [(0.0, 0.012), (1.0, 0.017)], P['Brass'], L0, 0.5, sides=8)
    box(g, (0, -0.016, -0.245), (0.026, 0.02, 0.08), 0.005, P['Brass'], L0, 0.5)
    lat(g, (-0.018, -0.07, -0.245), [(0.0, 0.046), (0.004, 0.05), (0.03, 0.05), (0.034, 0.046)], P['Steel'], L0, 0.5, sides=14, rot=rz(-90))
    lat(g, (-0.01, -0.07, -0.245), [(0.0, 0.032), (0.02, 0.032)], P['Iron'], L0, 0.5, sides=12, rot=rz(-90))
    rod(g, (0.02, -0.07, -0.245), (0.046, -0.07, -0.22), 0.005, P['Steel'], L0, 0.5, sides=4)
    ball(g, (0.05, -0.07, -0.215), 0.01, S['Wood'], LW, 0.5, sub=1)
    for i, z in enumerate([0.55, 0.85, 1.15, 1.45, 1.75, 2.0, 2.25, 2.45, 2.65]):
        t = (z + 0.05) / 2.75
        guide(g, (0, 0.3 * t * t + radii[min(16, int(t * 16))] + 0.005, z), P['Steel'], 0.0025, 0.0085 - 0.004 * t, 6, 3)
    path(g, [Vector((0, -0.045, -0.245)), Vector((0, -0.01, 0.15)), Vector((0, 0.01, 0.55))], 0.0014, P['Paper'], L0, 0.5, sides=3, caps=False)

@model('MetalDetector', 'gear', 'grip: centre of the hand grip; shaft runs forward-down along +z, coil at the end', "Metal detector: grip with arm cuff, control box with dial, telescoping shaft with cam locks and a search coil.", ground=False,
       held='grip at the pivot, search coil 0.95 m forward and 0.55 m down')
def m_detector(g, r):
    rod(g, (0, 0, -0.07), (0, 0, 0.07), 0.021, S['Roof'], LC, 0.5, sides=8)
    path(g, arc((0, -0.01, 0.07), 0.07, 90, 270, 8, 'yz'), 0.011, P['Iron'], L0, 0.5, sides=5)
    rod(g, (0, -0.012, -0.17), (0, -0.01, -0.07), 0.015, P['Iron'], L0, 0.5, sides=6)
    path(g, arc((0, -0.04, -0.35), 0.12, 20, 160, 8, 'xy'), 0.011, S['Roof'], LC, 0.5, sides=5)         # the arm cuff band
    box(g, (0, 0.035, -0.31), (0.09, 0.012, 0.24), 0.005, P['Iron'], L0, 0.5)
    sd = Vector((0, -0.58, 0.95)).normalized()
    top = Vector((0, 0.04, 0.12))
    bot = top + sd * 1.1
    rodp(g, top, bot, [(0.0, 0.0155), (0.5, 0.0155), (0.5, 0.0135), (1.0, 0.0135)], P['Steel'], L0, 0.5, sides=7)
    for t in (0.36, 0.7):
        rodp(g, top + sd * (t * 1.1), top + sd * (t * 1.1 + 0.05), [(0.0, 0.021), (1.0, 0.021)], S['Accent'], L0, 0.5, sides=7)
    box(g, (0, 0.105, 0.2), (0.08, 0.085, 0.17), 0.015, P['Iron'], L0, 0.5, segs=1)
    lat(g, (0.0, 0.15, 0.17), [(0.0, 0.026), (0.012, 0.026)], S['Accent'], L0, 0.5, sides=12)
    box(g, (0, 0.14, 0.292), (0.05, 0.035, 0.012), 0.004, P['GlassGreen'], L0, 0.6, rot=rx(-15))
    for i in range(3):
        ball(g, (-0.02 + 0.02 * i, 0.075, 0.292), 0.008, S['Roof'] if i == 1 else P['Steel'], L0, 0.5, sub=1)
    cc = bot + sd * 0.02
    rn = up_to(Vector((0, 0.82, 0.57)))                      # coil plate roughly parallel to the ground
    lat(g, cc, [(-0.016, 0.12), (-0.004, 0.14), (0.014, 0.14), (0.024, 0.12), (0.026, 0.0)], P['Iron'], L0, 0.5, sides=14, rot=rn, cap0=True, cap1=False,
        ring_slots=[P['Iron'], P['Iron'], P['Iron'], P['Iron']])
    lat(g, cc, [(0.026, 0.12), (0.03, 0.115), (0.03, 0.0)], S['Accent'], L0, 0.5, sides=14, rot=rn, cap0=False, cap1=False)
    ball(g, cc + Vector((0, 0.018, 0.02)), 0.016, P['Brass'], L0, 0.5, scale=(1, 0.5, 1), sub=1)
    cab = [top + sd * (0.04 + 0.95 * i / 14) + Vector((0.022 * math.cos(i * 1.5), 0.022 * math.sin(i * 1.5), 0)) for i in range(15)]
    path(g, cab, 0.004, P['Iron'], L0, 0.5, sides=3, caps=False)

@model('Shovel', 'gear', 'grip: the D-handle centre; the shovel points down -y', "Shovel: ash shaft with a D-handle and a steel blade with a rolled step.", ground=False,
       held='vertical: D-handle at the pivot, blade 1.0 m below (-y)', lay=rx(90))
def m_shovel(g, r):
    path(g, arc((0, 0.0, 0.0), 0.075, -10, 190, 10, 'xy'), 0.015, S['Wood'], LW, 0.5, sides=6)
    rodp(g, (0, 0.0, 0), (0, -0.78, 0), [(0.0, 0.0175), (1.0, 0.015)], S['Wood'], LW, 0.5, sides=7)
    outline = [(-0.1, -0.78), (-0.11, -0.86), (-0.1, -0.95), (-0.045, -1.04), (0.0, -1.06), (0.045, -1.04), (0.1, -0.95), (0.11, -0.86), (0.1, -0.78)]
    g.add(xform(prism(outline, 0.012, 0.004, 1), (0, 0, 0), ry(90)), P['Steel'], L0, 0.5)
    lat(g, (0, -0.7, 0), [(0.0, 0.0145), (0.07, 0.02), (0.1, 0.03)], P['Steel'], L0, 0.5, sides=8, cap0=False, cap1=False)
    box(g, (0, -0.79, 0), (0.22, 0.03, 0.03), 0.01, P['Iron'], L0, 0.5)
    box(g, (0, -0.8, -0.02), (0.2, 0.02, 0.04), 0.01, P['Steel'], L0, 0.5)

@model('HandCart', 'gear', 'base centre between the wheels, wheels on y = 0; faces +z, handles toward -z', "Hand cart for solo hauling: two spoked wheels, slatted bed with a rope strap, two long handles and a prop foot.", ground=True,
       held='pushed/pulled by the handles; bed 1.0 x 0.62 m')
def m_cart(g, r):
    wd, wdk = S['Wood'], S['WoodDark']
    for sx in (-0.34, 0.34):
        cbox(g, (sx, 0.41, 0.02), (0.06, 0.07, 1.12), wdk, LW, 0.5)
    for i, z in enumerate([-0.42, -0.21, 0.0, 0.21, 0.42]):
        box(g, (0, 0.46, z), (0.64, 0.032, 0.19), 0.01, wd if i % 2 == 0 else S['Dock'], LW, 0.5)
    for sx in (-0.32, 0.32):
        cbox(g, (sx, 0.52, 0.0), (0.03, 0.1, 0.96), wd, LW, 0.5)
    cbox(g, (0, 0.52, 0.48), (0.64, 0.1, 0.03), wd, LW, 0.5)
    cbox(g, (0, 0.52, -0.48), (0.64, 0.07, 0.03), wd, LW, 0.5)
    rod(g, (-0.46, 0.24, 0.1), (0.46, 0.24, 0.1), 0.02, P['Iron'], L0, 0.5, sides=7)
    for sx in (-1, 1):
        x = sx * 0.43
        ring(g, (x, 0.24, 0.1), 0.215, 0.032, P['Iron'], L0, 0.5, rot=ry(90), segs=14, sides=4)
        lat(g, (x + sx * 0.0, 0.24, 0.1), [(-0.03, 0.045), (0.03, 0.045)], P['Steel'], L0, 0.5, sides=10, rot=rz(-90))
        for k in range(5):
            a = k * 2 * math.pi / 5
            rod(g, (x, 0.24 + 0.04 * math.cos(a), 0.1 + 0.04 * math.sin(a)), (x, 0.24 + 0.2 * math.cos(a), 0.1 + 0.2 * math.sin(a)), 0.008, P['Steel'], L0, 0.5, sides=3,
                cap0=False, cap1=False)
        cbox(g, (sx * 0.34, 0.285, 0.1), (0.03, 0.09, 0.13), wdk, LW, 0.5)
    for sx in (-0.3, 0.3):
        rodp(g, (sx, 0.43, -0.42), (sx, 0.78, -1.15), [(0.0, 0.021), (1.0, 0.021)], wd, LW, 0.5, sides=7)
        rodp(g, (sx, 0.76, -1.07), (sx, 0.78, -1.28), [(0.0, 0.027), (1.0, 0.027)], S['Roof'], LC, 0.5, sides=8)
    rod(g, (-0.3, 0.62, -0.78), (0.3, 0.62, -0.78), 0.016, wd, LW, 0.5, sides=6)
    for sx in (-0.3, 0.3):
        rod(g, (sx, 0.43, -0.4), (sx * 1.0, 0.03, -0.62), 0.017, wdk, LW, 0.5, sides=6)
    for z in (-0.22, 0.2):
        path(g, [Vector((-0.34, 0.49, z)), Vector((0, 0.5, z + 0.01)), Vector((0.34, 0.49, z))], 0.011, S['Accent'], LC, 0.55, sides=4)
    cbox(g, (0, 0.425, 0.56), (0.2, 0.13, 0.03), S['Roof'], LW, 0.5)                                   # painted bumper plate


# =======================================================================================================================================
# Build, bake, export, manifest, previews
# =======================================================================================================================================
SLOT_NAMES = {v: k for k, v in S.items()}
SLOT_NAMES.update({v: k for k, v in P.items()})
LAYER_NAMES = {v: k for k, v in L.items()}

def preview_sets():
    # (file name, title, filter on the registry entry, normalised size in metres, label size)
    small_gear = ('JigLure', 'TrollingLure', 'CastNet_Folded', 'CastNet_Spread', 'CrabPot', 'LanternRig', 'IceHoleRim')
    return [('props_villager.png', 'villager', lambda s: s['group'] == 'villager', 0.9, 0.12),
            ('props_collectibles.png', 'collectibles', lambda s: s['group'] == 'collectible', 0.62, 0.075),
            ('props_gear_small.png', 'gear', lambda s: s['group'] == 'gear' and s['name'] in small_gear, 1.1, 0.1),
            ('props_gear_long.png', 'gear', lambda s: s['group'] == 'gear' and s['name'] not in small_gear, 1.9, 0.12)]

def main(export=True, bake=True, previews=True, only=None):
    bpy.ops.wm.read_homefile(use_empty=True)
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and (o.name.startswith('Prop_')):
            bpy.data.objects.remove(o, do_unlink=True)
    os.makedirs(EXPORT_DIR, exist_ok=True)
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    mat = preview_material()
    built = []                                            # (spec, wet, group, object)
    for spec in REG:
        if only and spec['name'] not in only:
            continue
        for wet in ((False, True) if spec['fished'] else (False,)):
            g = build(spec, wet)
            t = g.tris()
            if t > MAX_TRIS:
                raise RuntimeError('%s has %d triangles (limit %d)' % (g.name, t, MAX_TRIS))
            ob = H.make_object(g)
            if bake:
                write_ao(ob, g, bake_one(g))
            ob.data.materials.append(mat)
            built.append((spec, wet, g, ob))
    manifest = dict(
        generator=SCRIPT_REL,
        axes='Unity metres, authored in Unity axes (x right, y up, z forward); Blender = (-x, -z, y); the FBX export is the harbour\'s (-Z forward, Y up, '
             'bake_space_transform). Every FBX object sits at the origin with an identity transform: the Unity builder parents it to the item/prefab '
             'root. The front of a prop faces +z. Pivot = base centre or the grip (see each entry).',
        encoding=dict(uv0='UVMap in metres', uv1='Layer = (palette slot, texture layer) per corner; no uv1.z in the FBX; all props use M_Toon_Base',
                      colour='Col = 0.5 x ao x tint: a per-vertex BVH ray-cast AO bake (40 rays, distance 0.35 x size clamped 0.04-0.45 m, self occlusion plus a ground '
                             'plane at y = 0 for ground props); fished-up variants multiply by %.2f (the wet tint)' % WET_TINT,
                      normals='smooth / analytic', materials='none exported'),
        palette=dict(reused_harbour_slots=S, new_slots_16_to_31=P, preview_colours_srgb={SLOT_NAMES[i]: PAL[i] for i in range(32) if i in SLOT_NAMES and i >= 16},
                     note='Slots 16-31 are NEW: the Unity palette texture (and the 16 -> 32 wide slot lookup in the shader) must be extended on the PC. '
                          'Slots 11-15 (harbour preview-only) are not used.'),
        layers=L, wet_tint=WET_TINT, fbx=[])
    total = 0
    for spec, wet, g, ob in built:
        path = os.path.join(EXPORT_DIR, g.name + '.fbx')
        status = export_if_changed(ob, path) if export else 'not exported'
        lo, hi = H.u_bounds(g)
        t = g.tris(); total += t
        manifest['fbx'].append(dict(
            name=g.name, file='Art/Export/Props/' + g.name + '.fbx', triangles=t, vertices=len(g.V), material='M_Toon_Base', group=spec['group'],
            variant='fished-up' if wet else 'plain', base_model='Prop_' + spec['name'], pivot=spec['pivot'], description=spec['desc'],
            owner_or_source=spec['who'], held_axis=spec['held'], bounds_min=lo, bounds_max=hi,
            size_m=[round(hi[i] - lo[i], 3) for i in range(3)], rests_on_ground=bool(g.ground),
            slots=[SLOT_NAMES.get(s, s) for s in sorted(set(s for s, _ in g.SL))], layers=[LAYER_NAMES.get(l, l) for l in sorted(set(l for _, l in g.SL))],
            wet_tint=WET_TINT if wet else 1.0))
        print('%-34s tris=%5d verts=%5d %s' % (g.name, t, len(g.V), status))
    manifest['total_triangles'] = total
    manifest['models'] = len(built)
    with open(MANIFEST_PATH, 'w', encoding='utf-8') as fh:
        json.dump(manifest, fh, indent=1)
    print('TOTAL tris', total, 'models', len(built))
    if previews:
        by = {}
        for spec, wet, g, ob in built:
            by.setdefault(spec['name'], {})[wet] = (g, ob)
        for fname, title, flt, norm, lab in preview_sets():
            entries = []
            for spec in REG:
                if only and spec['name'] not in only:
                    continue
                if not flt(spec):
                    continue
                d = by[spec['name']]
                pg, po = d[False]
                fg, fo = d.get(True, (None, None))
                entries.append((spec['name'].replace('_', ' '), po, pg, fo, spec))
            if not entries:
                continue
            for _s, _w, _g, o in built:
                o.hide_render = True
            for e in entries:
                e[1].hide_render = False
                if e[3] is not None:
                    e[3].hide_render = False
            render_layout(os.path.join(PREVIEW_DIR, fname), entries, norm, title, lab=lab)
            print('preview', fname)
    return built

if __name__ == '__main__':
    only = os.environ.get('FG_ONLY')
    main(export=not os.environ.get('FG_NOEXPORT'), previews=not os.environ.get('FG_NOPREVIEW'), only=only.split(',') if only else None)
