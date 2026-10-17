# Painted-toon textures for the fisher (spec R74), built from text so they re-run on a rebuilt model.
#
# Stages (the first three need only python3 + numpy + Pillow; the last two run inside Blender):
#   python3 -I Art/Blender/fg_fisher_tex.py fetch <srcdir>     download the CC0 Poly Haven base maps (1k)
#   python3 -I Art/Blender/fg_fisher_tex.py tex   <srcdir>     stylise them into Art/Textures/Fisher/*.png (512, tileable)
#   blender -b --python Art/Blender/fg_fisher_tex.py -- apply  UV project, assign materials, bake AO -> `Col`, save .blend
#   blender -b --python Art/Blender/fg_fisher_tex.py -- preview <dir>   turnaround, face close-up, vest slot colours
# Order after a mesh rebuild: fg_fisher.py model, then `apply` (it is idempotent on a fresh model).
#
# Conventions
#  * Painted look: photo maps are reduced to a high-pass detail, median-cleaned, softly posterised, then overlaid with
#    tileable brush strokes + warm/cool blotches. Everything is FFT-filtered, so every PNG tiles perfectly.
#  * Like PEAK / RV There Yet: every texture is a GREYSCALE painted detail map (mean 0.5) and ALL colour lives in the
#    material. Import `<tex>_detail.png` as Non-Color (sRGB off); colour = tint * detail * 2. In Blender the tint is
#    the RGB node `SlotTint` on each `M_Fisher_*` material (the vest's is the player's slot colour; skin's is the
#    player's skin colour). Several materials share a texture (stripe/sock, sole/leather).
#  * UVs are metric: one UV unit = one tile = TILE metres, seamless by construction (u wraps an integer number of
#    tiles round every cylinder, seam on the back), so the weave has constant size on every part. Not an atlas:
#    textures must use Repeat wrapping.
#  * Normal maps are OpenGL (+Y up). AO lives in the vertex colour layer `Col`.

import math, os, subprocess, sys, json

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(REPO, 'Art', 'Textures', 'Fisher')
BLEND_PATH = os.path.join(HERE, 'fishgame_fisher.blend')
SIZE = 512
TILE = 0.16          # metres of cloth per texture tile (the weave size on screen)
SLOT_COLOURS = {'blue': (0.20, 0.42, 0.85), 'white': (0.92, 0.92, 0.92),
                'green': (0.55, 0.85, 0.45), 'darkred': (0.45, 0.07, 0.07)}

# ---------------------------------------------------------------- material table
# src: Poly Haven id; base: colour in the same linear-ish values fg_fisher.COL uses (converted to sRGB for the PNG);
# amp: weave/detail strength; brush: stroke strength; warm: warm/cool blotch hue shift; nrm: normal strength.
POLYHAVEN = {
    'rough_linen':      ('Rough Linen',      'Rico Cilliers (processing), colormass (photography)'),
    'stretch_poplin':   ('Stretch Poplin',   'Rico Cilliers (processing), colormass (photography)'),
    'denim_fabric_03':  ('Denim Fabric 03',  'Rico Cilliers (processing), colormass (photography)'),
    'hessian_230':      ('Hessian 230',      'Rico Cilliers (processing), colormass (photography)'),
    'brown_leather':    ('Brown Leather',    'Rob Tuytel'),
    'cotton_jersey':    ('Cotton Jersey',    'Rico Cilliers (processing), colormass (photography)'),
}
# TEXTURES: greyscale painted details. src = Poly Haven base; amp = weave strength; brush = stroke strength; nrm = normal
# strength (0 = no normal map); doodle = drawn line details (stitch panel, patch pocket, stitched rows, seam + scratches).
TEXTURES = {
    'skin':    dict(src='cotton_jersey',   amp=0.02, brush=0.06, nrm=0.0, doodle=None),
    'shirt':   dict(src='stretch_poplin',  amp=0.07, brush=0.07, nrm=0.6, doodle=None),
    'vest':    dict(src='rough_linen',     amp=0.16, brush=0.10, nrm=0.9, doodle='stitch'),
    'hat':     dict(src='rough_linen',     amp=0.14, brush=0.10, nrm=0.9, doodle='rows'),
    'cargo':   dict(src='denim_fabric_03', amp=0.16, brush=0.09, nrm=1.1, doodle='patch'),
    'sock':    dict(src='cotton_jersey',   amp=0.10, brush=0.05, nrm=0.7, doodle=None),
    'leather': dict(src='brown_leather',   amp=0.14, brush=0.10, nrm=1.0, doodle='seam'),
    'webbing': dict(src='hessian_230',     amp=0.16, brush=0.07, nrm=1.2, doodle=None),
}
# MATERIALS: texture + default tint (linear-ish, as fg_fisher.COL) + roughness. Unity sets the real colours.
MATERIALS = {
    'skin':    dict(tex='skin',    tint=(0.93, 0.70, 0.55), rough=0.7),
    'shirt':   dict(tex='shirt',   tint=(0.86, 0.82, 0.70), rough=0.8),
    'vest':    dict(tex='vest',    tint=(0.20, 0.42, 0.85), rough=0.85),
    'hat':     dict(tex='hat',     tint=(0.55, 0.50, 0.33), rough=0.85),
    'cargo':   dict(tex='cargo',   tint=(0.42, 0.36, 0.26), rough=0.85),
    'sock':    dict(tex='sock',    tint=(0.92, 0.91, 0.88), rough=0.9),
    'stripe':  dict(tex='sock',    tint=(0.80, 0.18, 0.15), rough=0.9),
    'leather': dict(tex='leather', tint=(0.30, 0.17, 0.09), rough=0.6),
    'sole':    dict(tex='leather', tint=(0.10, 0.08, 0.07), rough=0.7),
    'webbing': dict(tex='webbing', tint=(0.30, 0.22, 0.15), rough=0.9),
}


# ================================================================ texture generation (numpy + Pillow)
def _np():
    import numpy as np
    return np


def gauss(a, sx, sy=None, angle=0.0):
    """Periodic (wrap-around) gaussian blur by FFT; sx/sy in pixels, rotated by angle radians (stroke direction = sx)."""
    np = _np()
    sy = sx if sy is None else sy
    h, w = a.shape[:2]
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    c, s = math.cos(angle), math.sin(angle)
    u = fx * c + fy * s
    v = -fx * s + fy * c
    g = np.exp(-2 * math.pi ** 2 * ((sx * u) ** 2 + (sy * v) ** 2))
    if a.ndim == 2:
        return np.fft.ifft2(np.fft.fft2(a) * g).real.astype(np.float32)
    return np.stack([gauss(a[..., i], sx, sy, angle) for i in range(a.shape[2])], -1)


def median3(a):
    np = _np()
    st = np.stack([np.roll(np.roll(a, i, 0), j, 1) for i in (-1, 0, 1) for j in (-1, 0, 1)])
    return np.median(st, axis=0)


def soft_quant(x, steps):
    """Posterise with smooth risers, so flat painted patches with soft edges rather than banding."""
    np = _np()
    y = x * steps
    f = y - np.floor(y)
    f = f * f * (3 - 2 * f)
    return (np.floor(y) + f) / steps


def unit(a):
    return (a - a.mean()) / (a.std() + 1e-6)


def brush_layer(rng, angle, length, width):
    """Tileable noise stretched along `angle`: reads as dry-brush strokes after tanh."""
    np = _np()
    n = rng.standard_normal((SIZE, SIZE)).astype(np.float32)
    return unit(gauss(n, length, width, angle))


def doodle_mask(kind, rng):
    """Hand-drawn line details (0..1) on a tileable canvas: stitches, patch pockets, seams, scratches."""
    np = _np()
    from PIL import Image, ImageDraw
    im = Image.new('L', (SIZE, SIZE), 0)
    d = ImageDraw.Draw(im)

    def dashed(x0, y0, x1, y1, dash=11, gap=8, w=3):
        n = max(abs(x1 - x0), abs(y1 - y0))
        steps = int(n // (dash + gap)) + 1
        for i in range(steps):
            a = i * (dash + gap) / n
            b = min(1.0, (i * (dash + gap) + dash) / n)
            d.line([x0 + (x1 - x0) * a, y0 + (y1 - y0) * a, x0 + (x1 - x0) * b, y0 + (y1 - y0) * b], fill=255, width=w)

    def box(x0, y0, x1, y1):
        dashed(x0, y0, x1, y0); dashed(x1, y0, x1, y1); dashed(x1, y1, x0, y1); dashed(x0, y1, x0, y0)

    if kind == 'stitch':
        box(26, 26, SIZE - 27, SIZE - 27)
    elif kind == 'patch':
        box(250, 270, 440, 440)                       # patch pocket
        dashed(250, 318, 440, 318)                    # flap line
        d.rectangle([252, 272, 438, 316], outline=255, width=2)
    elif kind == 'rows':
        for y in (150, 175, 330, 355):
            dashed(18, y, SIZE - 19, y, dash=14, gap=6)
    elif kind == 'seam':
        dashed(18, 256, SIZE - 19, 256, dash=16, gap=9, w=3)
        for _ in range(26):                           # scratches and scuffs
            x, y = int(rng.integers(40, 470)), int(rng.integers(40, 470))
            d.line([x, y, x + int(rng.integers(-24, 24)), y + int(rng.integers(-24, 24))], fill=140, width=1)
    k = gauss(np.asarray(im, np.float32) / 255, 0.7)
    return np.clip(k / max(k.max(), 1e-6) * 1.4, 0, 1)


def normal_from_height(h, strength):
    np = _np()
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    drow = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    n = np.stack([-dx * strength, drow * strength, np.ones_like(h)], -1)   # row grows downward, GL +Y is up
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def save(path, a):
    from PIL import Image
    np = _np()
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path, optimize=True)


def stylise_detail(srcdir, src):
    """Photo diffuse -> unit-std painted detail (weave kept, noise and lighting removed)."""
    np = _np()
    from PIL import Image
    path = os.path.join(srcdir, src, src + '_diff_1k.png')
    im = Image.open(path).convert('RGB').resize((SIZE, SIZE), Image.LANCZOS)
    lum = (np.asarray(im, np.float32) / 255) @ np.array([0.299, 0.587, 0.114], np.float32)
    hp = unit(lum - gauss(lum, 8))
    hp = np.clip(hp, -3, 3)
    hp = gauss(median3(hp), 0.7)                       # clean pixel noise, then soften
    hp = unit(0.45 * hp + 0.55 * soft_quant(hp, 2.2))  # flatten into painted tones
    return np.clip(hp, -2.5, 2.5)


def make_all(srcdir):
    np = _np()
    os.makedirs(OUT, exist_ok=True)
    for i, (name, m) in enumerate(TEXTURES.items()):
        rng = np.random.default_rng(1000 + i)
        hp = stylise_detail(srcdir, m['src'])
        # Hand-painted overlay: short dry-brush strokes at two angles + a big soft blotch of tonal drift.
        strokes = np.tanh(1.3 * (0.6 * brush_layer(rng, 0.6, 9, 1.6) + 0.4 * brush_layer(rng, -0.9, 14, 2.2)))
        blotch = brush_layer(rng, 0.3, 26, 20)
        f = 1 + m['amp'] * hp * 0.8 + 1.3 * m['brush'] * strokes + 0.8 * m['brush'] * blotch
        f = f + 0.07 * np.clip(hp - 1.2, 0, 2)                  # worn, scuffed high spots
        f = f * (1 + 0.035 * unit(gauss(rng.standard_normal((SIZE, SIZE)).astype(np.float32), 0.55)))   # print-like grain
        ink = doodle_mask(m['doodle'], rng) if m['doodle'] else np.zeros((SIZE, SIZE), np.float32)
        f = f * (1 - 0.5 * ink)                                # stitches / patch edges read as darker thread lines
        f = f / f.mean()
        save(os.path.join(OUT, name + '_detail.png'), np.stack([np.clip(0.5 * f, 0, 1)] * 3, -1))
        if m['nrm'] > 0:
            height = 0.5 * hp + 0.25 * strokes - 1.2 * ink
            nm = normal_from_height(gauss(height.astype(np.float32), 0.6), m['nrm'] * 1.4)
            save(os.path.join(OUT, name + '_n.png'), nm)
    print('TEX OK ->', OUT)


def fetch(srcdir):
    """Download diffuse + GL normal 1k PNGs from the Poly Haven API (CC0), each into its own directory."""
    for pid in POLYHAVEN:
        d = os.path.join(srcdir, pid)
        os.makedirs(d, exist_ok=True)
        files = json.loads(subprocess.check_output(['curl', '-sS', 'https://api.polyhaven.com/files/' + pid]))
        for key, tag in (('Diffuse', 'diff'), ('nor_gl', 'nor_gl')):
            url = files[key]['1k']['png']['url']
            if not url.startswith('https://dl.polyhaven.org/'):
                raise SystemExit('unexpected host: ' + url)
            subprocess.check_call(['curl', '-sSL', '-o', os.path.join(d, '%s_%s_1k.png' % (pid, tag)), url])
        print('fetched', pid)


# ================================================================ Blender stages
PART_MATERIAL = {
    'Head': 'skin', 'Ears': 'skin', 'Arms': 'skin', 'Legs': 'skin', 'Shirt': 'shirt', 'Pants': 'cargo', 'Shorts': 'cargo',
    'Socks': 'sock', 'SockStripes': 'stripe', 'Boots': 'leather', 'Soles': 'sole', 'Vest': 'vest', 'VestPockets': 'vest',
    'Hat': 'hat', 'HatBand': 'webbing',
}


def tex_path(name, kind):
    return os.path.join(OUT, '%s_%s.png' % (name, kind))


def build_material(bpy, key):
    m = MATERIALS[key]
    t = TEXTURES[m['tex']]
    mat = bpy.data.materials.get('M_Fisher_' + key) or bpy.data.materials.new('M_Fisher_' + key)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = m['rough']
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    def image(path, noncolor):
        n = nt.nodes.new('ShaderNodeTexImage')
        n.image = bpy.data.images.load(path, check_existing=True)
        n.image.filepath = bpy.path.relpath(path)
        n.image.colorspace_settings.name = 'Non-Color' if noncolor else 'sRGB'
        n.extension = 'REPEAT'
        return n

    albedo = image(tex_path(m['tex'], 'detail'), True)
    color_out = albedo.outputs['Color']
    two = nt.nodes.new('ShaderNodeVectorMath')
    two.operation = 'MULTIPLY'
    two.inputs[1].default_value = (2, 2, 2)
    nt.links.new(color_out, two.inputs[0])
    tint = nt.nodes.new('ShaderNodeRGB')
    tint.name = tint.label = 'SlotTint'
    tint.outputs[0].default_value = (*m['tint'], 1)
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs[0].default_value = 1
    nt.links.new(tint.outputs[0], mul.inputs[6])
    nt.links.new(two.outputs[0], mul.inputs[7])
    color_out = mul.outputs[2]
    # AO from the vertex colour layer, softened to 70 %.
    col = nt.nodes.new('ShaderNodeVertexColor')
    col.layer_name = 'Col'
    soft = nt.nodes.new('ShaderNodeMix')
    soft.data_type = 'RGBA'
    soft.inputs[0].default_value = 0.7
    soft.inputs[6].default_value = (1, 1, 1, 1)
    nt.links.new(col.outputs['Color'], soft.inputs[7])
    ao = nt.nodes.new('ShaderNodeMix')
    ao.data_type = 'RGBA'
    ao.blend_type = 'MULTIPLY'
    ao.inputs[0].default_value = 1
    nt.links.new(color_out, ao.inputs[6])
    nt.links.new(soft.outputs[2], ao.inputs[7])
    nt.links.new(ao.outputs[2], bsdf.inputs['Base Color'])
    if t['nrm'] > 0:
        nimg = image(tex_path(m['tex'], 'n'), True)
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nm.inputs['Strength'].default_value = 0.8
        nt.links.new(nimg.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


# ---- UV projection: metric cylinders (seam on the back) with planar caps
class Proj:
    def __init__(self, p0, p1, ref_r):
        from mathutils import Vector
        self.p0 = Vector(p0)
        self.a = (Vector(p1) - Vector(p0)).normalized()
        front = Vector((0, -1, 0))
        self.e1 = (front - self.a * front.dot(self.a)).normalized()   # towards the front, so the seam is at the back
        self.e2 = self.a.cross(self.e1)
        self.n = max(1, round(2 * math.pi * ref_r / TILE))

    def planar(self, p):
        d = p - self.p0
        return d.dot(self.e2) / TILE, d.dot(self.e1) / TILE

    def theta(self, p):
        d = p - self.p0
        return math.atan2(d.dot(self.e2), d.dot(self.e1))

    def v(self, p):
        return (p - self.p0).dot(self.a) / TILE


def uv_project(ob, pick, cap_dot=0.72):
    """pick(centre, normal) -> Proj. Planar on faces whose normal runs along the axis, cylinder otherwise."""
    me = ob.data
    uvl = me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap')
    for poly in me.polygons:
        pr = pick(poly.center, poly.normal)
        pts = [me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        if abs(poly.normal.dot(pr.a)) > cap_dot:
            for li, p in zip(poly.loop_indices, pts):
                uvl.data[li].uv = pr.planar(p)
            continue
        th = [pr.theta(p) for p in pts]
        if max(th) - min(th) > math.pi:                  # face straddles the seam: keep it contiguous
            th = [t + 2 * math.pi if t < 0 else t for t in th]
        for li, p, t in zip(poly.loop_indices, pts, th):
            uvl.data[li].uv = (t / (2 * math.pi) * pr.n, pr.v(p))


def bbox(ob):
    import mathutils
    vs = [v.co for v in ob.data.vertices]
    lo = mathutils.Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = mathutils.Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def mean_x(ob, side):
    xs = [abs(v.co.x) for v in ob.data.vertices if (v.co.x >= 0) == (side > 0) and abs(v.co.x) > 0.02]
    return sum(xs) / len(xs) if xs else 0.1


def make_pick(ob):
    """UV projection layout for a part, derived from its bounds so it survives mesh reshaping."""
    lo, hi = bbox(ob)
    cy = (lo.y + hi.y) / 2
    z0, z1 = lo.z, hi.z
    name = ob.name

    def vert(x, y, r):
        return Proj((x, y, z0), (x, y, z1), r)

    if name in ('Head', 'Ears', 'Hat', 'HatBand', 'Vest', 'VestPockets'):
        r = {'Head': 0.2, 'Ears': 0.2, 'Hat': 0.24, 'HatBand': 0.2, 'Vest': 0.22, 'VestPockets': 0.2}[name]
        p = vert(0, cy, r)
        return lambda c, n: p
    if name == 'Shirt':
        torso = vert(0, cy, 0.2)
        sleeve = {s: vert(s * hi.x * 0.8, cy, 0.07) for s in (1, -1)}
        return lambda c, n: sleeve[1 if c.x > 0 else -1] if abs(c.x) > hi.x * 0.62 else torso
    if name in ('Arms', 'Legs', 'Pants', 'Shorts', 'Socks', 'SockStripes', 'Boots', 'Soles'):
        r = {'Arms': 0.06, 'Legs': 0.07, 'Pants': 0.09, 'Shorts': 0.11, 'Socks': 0.075, 'SockStripes': 0.08, 'Boots': 0.085,
             'Soles': 0.09}[name]
        side = {s: vert(s * mean_x(ob, s), cy, r) for s in (1, -1)}
        return lambda c, n: side[1 if c.x >= 0 else -1]
    p = vert(0, cy, 0.1)
    return lambda c, n: p


def bake_ao(bpy, objs):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 96
    w = bpy.data.worlds.new('AO')
    w.light_settings.distance = 0.35
    sc.world = w
    for ob in objs:
        me = ob.data
        for a in list(me.color_attributes):
            if a.name == 'Col':
                me.color_attributes.remove(a)
        attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
        me.color_attributes.active_color = attr
        me.color_attributes.render_color_index = list(me.color_attributes).index(attr)
        for o in bpy.context.selected_objects:
            o.select_set(False)
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
    sc.world = None
    bpy.data.worlds.remove(w)


def apply():
    import bpy
    bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    meshes = [o for o in bpy.data.collections['Fisher'].objects if o.type == 'MESH']
    mats = {k: build_material(bpy, k) for k in MATERIALS}
    for ob in meshes:
        key = PART_MATERIAL.get(ob.name)
        if not key:
            continue
        uv_project(ob, make_pick(ob))
        ob.data.materials.clear()
        ob.data.materials.append(mats[key])
    bake_ao(bpy, meshes)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print('APPLY OK', len(meshes), 'parts')


def preview(outdir):
    sys.path.insert(0, HERE)
    import bpy, fg_fisher
    try:
        fg_fisher.preview(outdir)        # opens the .blend, lights it, renders the turnaround + face close-up
    except KeyError:
        pass                             # its skin lineup edits 'M_Skin' (now M_Fisher_skin, tinted): redone below
    sc = bpy.context.scene
    pivot, cam = bpy.data.objects['Pivot'], bpy.data.objects['Cam']
    tints = [bpy.data.materials['M_Fisher_vest'].node_tree.nodes['SlotTint']]
    skin = bpy.data.materials['M_Fisher_skin'].node_tree.nodes['SlotTint'].outputs[0]
    blush = bpy.data.materials['M_Blush'].node_tree.nodes['Principled BSDF'].inputs['Base Color']
    pivot.rotation_euler = (0, 0, math.radians(-25))
    cam.data.ortho_scale = 1.95
    cam.location = (0, -6, 0.88)
    sc.render.resolution_x, sc.render.resolution_y = 520, 900
    sc.cycles.samples = 32
    for i, rgb in enumerate(fg_fisher.SKINS):
        skin.default_value = (*rgb, 1)
        blush.default_value = (rgb[0] * 0.85 + 0.08, rgb[1] * 0.6, rgb[2] * 0.6, 1)
        sc.render.filepath = os.path.join(outdir, 'fisher_skin%d.png' % i)
        bpy.ops.render.render(write_still=True)
    skin.default_value = (*MATERIALS['skin']['tint'], 1)
    pivot.rotation_euler = (0, 0, math.radians(-15))
    cam.data.ortho_scale = 1.0
    cam.location = (0, -6, 1.2)
    sc.render.resolution_x, sc.render.resolution_y = 420, 420
    sc.cycles.samples = 32
    for name, rgb in SLOT_COLOURS.items():
        tints[0].outputs[0].default_value = (*rgb, 1)
        sc.render.filepath = os.path.join(outdir, 'fisher_vest_%s.png' % name)
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    stage = argv[0] if argv else 'tex'
    arg = argv[1] if len(argv) > 1 else None
    if stage == 'fetch':
        fetch(arg)
    elif stage == 'tex':
        make_all(arg)
    elif stage == 'apply':
        apply()
    elif stage == 'preview':
        os.makedirs(arg, exist_ok=True)
        preview(arg)


if __name__ == '__main__':
    main()
