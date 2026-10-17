# fg_fishgenome.py (FishGenome art, 2026-10-17): genome shape keys + a pattern UV on the 25 existing fish meshes. Topology is untouched.
# Source meshes: the committed fish models, read from the authoring blends (fishgame_m3_fish.blend: 24 species FG_Fish_<Name>; fishgame_lookdev.blend:
# Fish_Bluegill_LD). Their vertex/face counts equal the committed Art/Export/Fish_<Name>.fbx (checked below), so the new FBX differ only by:
#   * 4 blendshapes: Fat, Long, BigFin, Stubby (Basis + 4 shape keys; Unity weights 0..100)
#   * a third UV set "Pat" (= Unity uv3 / TEXCOORD2): the species-independent pattern-mask frame, see PAT_* below and the report
# Everything else (UV0 "UVMap" species tile, UV1 "Layer", vertex colour "Col", pivot, axes, scale) is byte-for-byte the committed mesh data.
# Axes: authored in Blender space as the committed fish are (nose at -Y, Z up, metres), exported with the same FBX settings as fg_fish.py's export().
# Deterministic: no random numbers. Run: FG_ROOT=<repo> blender -b --factory-startup --python Art/Blender/fg_fishgenome.py
import bpy, bmesh, os, sys, json, math, hashlib
import numpy as np
from mathutils import Vector, kdtree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fg_genome_common as G

ROOT = G.root()
OUT = os.path.join(ROOT, 'Art', 'Export', 'FishGenome')

# ---- shape key definitions -------------------------------------------------------------------------------------------------------------------
# cross-section scale S(u) = 1 + a * bump(u) + c along the body (u 0 nose .. 1 tail end); ky = length scale; belly sags (back grows 35% less);
# fin plates move with the body surface point they grow from (anchor), keep their size, and stretch along the length by 60% of ky.
# BigFin scales the fin plates only (about their anchor). Eyes, barbels, spikes, barnacles and lures move rigidly with the surface they sit on.
SHAPES = {
    'Fat':    dict(a=0.42, c=0.00, ky=0.96, fin=1.00),
    'Long':   dict(a=0.00, c=-0.14, ky=1.34, fin=1.00),
    'BigFin': dict(a=0.00, c=0.00, ky=1.00, fin=1.55),
    'Stubby': dict(a=0.12, c=0.04, ky=0.76, fin=1.00),
}
SHAPE_ORDER = ['Fat', 'Long', 'BigFin', 'Stubby']

# ---- pattern UV ("Pat") ----------------------------------------------------------------------------------------------------------------------
# One frame for all species so a single mask atlas fits every mesh: the BODY LENGTH (nose to the end of the body island) maps to PAT_LEN of the
# tile, isotropic, centred at v = PAT_VC on the body axis. u = PAT_U0 + (y - nose) / length * PAT_LEN ; v = PAT_VC + (z - axisZ) / length * PAT_LEN.
# Flat species (rays: thickness < 55% of width) use x instead of z, so the top surface is what the pattern lands on. Fins, tail, barbels, spikes
# and barnacles use the same frame (a pattern continues over the tail and fins). Eyes (and the pupils/glints) map to PAT_EYE, a texel that every
# mask keeps at 0: patterns never tint eyes, and a shader can read "uv3 > 0.95" as "this is an eye" for the albino/ghost/glow traits.
PAT_U0, PAT_LEN, PAT_VC = 0.02, 0.68, 0.50
PAT_CLAMP = 0.93
PAT_EYE = (0.975, 0.975)

EYE_VERTS = (146, 86, 34)       # eye white / pupil / glint sphere topologies of fg_fish.py (16x10, 12x8, 8x5 UV spheres)

def log(*a): print('FGENOME', *a, flush=True)

def islands(me):
    """vertex index -> island id (connected faces), and the list of vertex index arrays."""
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    seen = set(); isl = []
    for f in bm.faces:
        if f.index in seen: continue
        stack = [f]; seen.add(f.index); vs = set()
        while stack:
            c = stack.pop()
            for v in c.verts: vs.add(v.index)
            for e in c.edges:
                for g in e.link_faces:
                    if g.index not in seen: seen.add(g.index); stack.append(g)
        isl.append(np.array(sorted(vs)))
    bm.free()
    return isl

def classify(P, isl):
    """-> body island, eyes {white island: [island ids of its pupil/glint + itself]}, fin plates, other attachments. The committed meshes carry no
    per-part material ids, so parts are told apart by geometry: eye whites are the 146-vertex UV spheres of fg_fish.py; a 86/34-vertex sphere
    inside a white's bounding box (+12 mm) is its pupil/glint; a plate is a flat island (PCA: thin third axis, not a tube)."""
    sizes = [len(v) for v in isl]
    body = int(np.argmax(sizes))
    lo = {i: P[v].min(axis=0) for i, v in enumerate(isl)}; hi = {i: P[v].max(axis=0) for i, v in enumerate(isl)}
    whites = [i for i in range(len(isl)) if i != body and sizes[i] == EYE_VERTS[0]]
    eyes = {w: [w] for w in whites}
    used = set(whites)
    for i in range(len(isl)):
        if i == body or i in used or sizes[i] not in EYE_VERTS[1:]: continue
        c = P[isl[i]].mean(axis=0)
        for w in whites:
            if np.all(c > lo[w] - 0.012) and np.all(c < hi[w] + 0.012):
                eyes[w].append(i); used.add(i); break
    plates, extras = [], []
    for i, v in enumerate(isl):
        if i == body or i in used: continue
        s = np.linalg.svd(P[v] - P[v].mean(axis=0), compute_uv=False)
        (plates if (s[1] > 0.3 * s[0] and s[2] < 0.35 * s[1] and len(v) >= 20) else extras).append(i)
    return body, eyes, plates, extras

def deform(P, isl, body, eye, plates, extras, shape):
    """new vertex positions for one shape key (all weights 1.0)."""
    s = SHAPES[shape]
    bv = isl[body]; B = P[bv]
    y0, y1 = B[:, 1].min(), B[:, 1].max()
    zc = 0.5 * (B[:, 2].min() + B[:, 2].max()); h = 0.5 * (B[:, 2].max() - B[:, 2].min())
    def T(p):
        p = np.atleast_2d(p); out = np.empty_like(p)
        u = np.clip((p[:, 1] - y0) / (y1 - y0), 0.0, 1.0)
        S = 1.0 + s['a'] * np.sin(np.pi * u ** 0.85) + s['c']
        t = np.clip((p[:, 2] - (zc - 0.3 * h)) / (0.6 * h), 0.0, 1.0); t = t * t * (3 - 2 * t)
        Sz = 1.0 + (S - 1.0) * (1.0 - 0.35 * t)
        out[:, 0] = p[:, 0] * S; out[:, 1] = y0 + (p[:, 1] - y0) * s['ky']; out[:, 2] = zc + (p[:, 2] - zc) * Sz
        return out
    Q = P.copy()
    Q[bv] = T(B)
    kd = kdtree.KDTree(len(bv))
    for k, i in enumerate(bv): kd.insert(Vector(P[i]), k)
    kd.balance()
    fy = 1.0 + (s['ky'] - 1.0) * 0.6
    plate_ids = set(plates)
    for k_isl in plates + extras:
        ids = isl[k_isl]
        V = P[ids]
        d = [kd.find(Vector(p))[2] for p in V]
        a = V[int(np.argmin(d))]                                    # the island vertex that sits nearest the body surface
        rel = V - a
        if k_isl in plate_ids:
            rel = rel * np.array([1.0, fy, 1.0]) * s['fin']
        Q[ids] = T(a)[0] + rel
    for w, parts in eye.items():
        ids = np.concatenate([isl[i] for i in parts])
        c = P[isl[w]].mean(axis=0)
        Q[ids] = P[ids] + (T(c)[0] - c)
    return Q

def pat_uv(P, isl, body, eye):
    B = P[isl[body]]
    y0, y1 = B[:, 1].min(), B[:, 1].max(); L = y1 - y0
    xr = B[:, 0].max() - B[:, 0].min(); zr = B[:, 2].max() - B[:, 2].min()
    flat = zr < 0.55 * xr
    axis = 0 if flat else 2
    ac = 0.5 * (B[:, axis].max() + B[:, axis].min())
    uv = np.empty((len(P), 2))
    uv[:, 0] = PAT_U0 + (P[:, 1] - y0) / L * PAT_LEN
    uv[:, 1] = PAT_VC + (P[:, axis] - ac) / L * PAT_LEN
    uv = np.clip(uv, 0.005, PAT_CLAMP)
    for parts in eye.values():
        for i in parts: uv[isl[i]] = PAT_EYE
    return uv, flat, L, float(zr / L)

def export_fbx(ob, path):
    me = ob.data; keep = list(me.materials); me.materials.clear()
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False)
    for m in keep: me.materials.append(m)

def export_if_changed(ob, path, fingerprint, previous):
    # The FBX bytes are not reproducible (the exporter stamps a creation time and time-derived ids), so "changed" means the mesh content changed:
    # a hash of the rounded vertices, shape-key deltas and UVs is kept in fishgenome_meshes.json; an unchanged mesh keeps its FBX byte-identical in git.
    if os.path.exists(path) and previous.get(os.path.basename(path)) == fingerprint: return 'kept'
    export_fbx(ob, path); return 'written'

def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)

def build_species(src_ob, name, manifest, previous):
    if src_ob.modifiers: raise SystemExit(name + ': source has modifiers')
    me = src_ob.data.copy(); me.name = 'Fish_' + name
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(n, 3)
    isl = islands(me)
    body, eye, plates, extras = classify(P, isl)
    ob = bpy.data.objects.new('Fish_' + name, me); bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.shape_key_add(name='Basis', from_mix=False)
    stats = {}
    for key in SHAPE_ORDER:
        Q = deform(P, isl, body, eye, plates, extras, key)
        kb = ob.shape_key_add(name=key, from_mix=False); kb.value = 0.0; kb.slider_min = 0.0; kb.slider_max = 1.0
        kb.data.foreach_set('co', Q.ravel())
        d = np.linalg.norm(Q - P, axis=1)
        stats[key] = dict(maxDelta=round(float(d.max()), 4), meanDelta=round(float(d.mean()), 4))
    uv, flat, L, aspect = pat_uv(P, isl, body, eye)
    layer = me.uv_layers.new(name='Pat')
    loop_v = np.empty(len(me.loops), dtype=np.int32); me.loops.foreach_get('vertex_index', loop_v)
    layer.data.foreach_set('uv', uv[loop_v].ravel())
    me.uv_layers.active = me.uv_layers['UVMap']
    # checks (reported in the manifest): eye whites keep their depth in the body surface (signed distance of the sphere centre, + outside), fin plates
    # stay attached (their nearest vertex to the body surface moves by < a few mm). A uniform Giant x1.6 scales every one of these numbers by 1.6.
    from mathutils.bvhtree import BVHTree
    bset = set(isl[body].tolist())
    bpolys = [tuple(p.vertices) for p in me.polygons if p.vertices[0] in bset]
    def surf(Pv):
        return BVHTree.FromPolygons([tuple(v) for v in Pv], bpolys)
    def sdist(tree, c):
        loc, nor, idx, d = tree.find_nearest(Vector(c))
        return d if (Vector(c) - loc).dot(nor) >= 0 else -d
    def plate_gap(tree, Pv):
        return min(tree.find_nearest(Vector(p))[3] for i in plates for p in Pv[isl[i]][::5])
    t0 = surf(P)
    e0 = [sdist(t0, P[isl[w]].mean(axis=0)) for w in eye]; g0 = plate_gap(t0, P) if plates else 0.0
    chk = {}
    for key in SHAPE_ORDER:
        co = np.empty(n * 3); me.shape_keys.key_blocks[key].data.foreach_get('co', co); Q = co.reshape(n, 3)
        t1 = surf(Q)
        e1 = [sdist(t1, Q[isl[w]].mean(axis=0)) for w in eye]
        chk[key] = dict(eyeDepthChange_mm=round(1000 * max(abs(a - b) for a, b in zip(e1, e0)), 2),
                        eyeSideFlip=bool(any((a >= 0) != (b >= 0) for a, b in zip(e1, e0))),
                        plateGapChange_mm=round(1000 * ((plate_gap(t1, Q) - g0) if plates else 0.0), 2))
    path = os.path.join(OUT, 'Fish_%s.fbx' % name)
    h = hashlib.sha1()
    for arr in [P] + [np.array([v.co[:] for v in me.shape_keys.key_blocks[k].data]) for k in SHAPE_ORDER] + [uv]:
        h.update(np.rint(np.asarray(arr) * 1e6).astype(np.int64).tobytes())
    fp = h.hexdigest()[:16]
    res = export_if_changed(ob, path, fp, previous)
    t = tris(me)
    log('%-16s verts=%d tris=%d islands=%d body=%d eyes=%d plates=%d extras=%d flat=%s lenPat=%.3f %s' % (name, n, t, len(isl), len(isl[body]),
        sum(len(v) for v in eye.values()), len(plates), len(extras), flat, L, res))
    manifest[name] = dict(name=name, file='Fish_%s.fbx' % name, tris=t, vertices=n, material='M_Toon_Fish', skinLayer=G.LAYER[name],
                          shapeKeys=SHAPE_ORDER, uvSets=['UVMap', 'Layer', 'Pat'], patFlatProjection=bool(flat), bodyLengthM=round(float(L), 4),
                          bodyHeightOverLength=round(aspect, 3), parts=dict(islands=len(isl), eyes=len(eye), eyeIslands=sum(len(v) for v in eye.values()), finPlates=len(plates), extras=len(extras)),
                          shapeStats=stats, checks=chk, exportResult=res, meshHash=fp,
                          notes='uv3 (Pat) holds the pattern-mask frame; eyes map to (0.975, 0.975), which every mask keeps at 0')
    return ob

def run():
    os.makedirs(OUT, exist_ok=True)
    manifest = {}
    previous = {}
    mp = os.path.join(OUT, 'fishgenome_meshes.json')
    if os.path.exists(mp): previous = {e['file']: e['meshHash'] for e in json.load(open(mp)) if 'meshHash' in e}
    jobs = [('Art/Blender/fishgame_m3_fish.blend', lambda o: o.name[8:] if o.name.startswith('FG_Fish_') else None),
            ('Art/Blender/fishgame_lookdev.blend', lambda o: 'Bluegill' if o.name == 'Fish_Bluegill_LD' else None)]
    for blend, namer in jobs:
        bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, blend))
        for o in sorted(bpy.data.objects, key=lambda o: o.name):
            if o.type != 'MESH': continue
            nm = namer(o)
            if nm is None or nm not in G.LAYER: continue
            build_species(o, nm, manifest, previous)
    missing = [n for n in G.SPECIES if n not in manifest]
    if missing: raise SystemExit('missing species: %s' % missing)
    with open(os.path.join(OUT, 'fishgenome_meshes.json'), 'w') as f:
        json.dump([manifest[n] for n in G.SPECIES], f, indent=1, sort_keys=True)
    log('done', len(manifest), 'species')

if __name__ == '__main__': run()
