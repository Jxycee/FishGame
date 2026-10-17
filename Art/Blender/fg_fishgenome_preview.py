# fg_fishgenome_preview.py (FishGenome art, 2026-10-17): Eevee previews of the genome art. The material below is a FAKE of the shader contract in
# docs/superpowers/handoff/art/fish-genome.md (hue shift, saturation, pattern mask + colour + strength, trait flags): it exists only so the shapes,
# masks and morphs can be judged before the Unity shader exists. It reads the exported FBX (shape keys, UV "Pat") and the PNGs, nothing else.
#   1_shapes_x_patterns.png  one species, 5 shapes (rows) x 8 pattern masks (columns)
#   2_traits.png             one species, 7 trait columns (none, albino, golden, melanistic, glowing, ghost, two-tone) x 3 shape/pattern rows
#   3_morph1.png / 4_morph2.png  all 25 species with their first / second rare morph skin
# Run: FG_ROOT=<repo> blender -b --factory-startup --python Art/Blender/fg_fishgenome_preview.py
import bpy, os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fg_genome_common as G
from mathutils import Vector

ROOT = G.root()
FBX = os.path.join(ROOT, 'Art', 'Export', 'FishGenome')
MASKS = os.path.join(ROOT, 'Art', 'Textures', 'FishGenome')
PREV = os.path.join(ROOT, 'Art', 'Previews', 'FishGenome')
SCRATCH = os.environ.get('FG_SCRATCH', os.path.join(ROOT, '..'))
NAMES = ['Stripes', 'Bars', 'Spots', 'Rosettes', 'Marbled', 'Mottled', 'Saddles', 'TwoTone']
RES = (1920, 1080)

_species_cache = {}
def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True); _species_cache.clear()

def image(path, noncolor=False):
    im = bpy.data.images.load(path, check_existing=True)
    im.colorspace_settings.name = 'Non-Color' if noncolor else 'sRGB'
    return im

def skin_png(name):
    """the baseline tile cut from the sheet, saved once to the scratch folder"""
    p = os.path.join(SCRATCH, 'base_%s.png' % name)
    if not os.path.exists(p): G.write_png(p, G.skin_tile(G.LAYER[name]))
    return p

def material(tag, skin, mask=None, patcol=(0.08, 0.06, 0.05), strength=0.85, hue=0.0, sat=1.0, trait=None):
    m = bpy.data.materials.new(tag); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    N = nt.nodes; L = nt.links
    def node(t, **kw):
        n = N.new(t)
        for k, v in kw.items(): setattr(n, k, v)
        return n
    uv0 = node('ShaderNodeUVMap'); uv0.uv_map = 'UVMap'
    uvp = node('ShaderNodeUVMap'); uvp.uv_map = 'Pat'
    tex = node('ShaderNodeTexImage'); tex.image = image(skin); tex.interpolation = 'Linear'; L.new(uv0.outputs[0], tex.inputs['Vector'])
    hs = node('ShaderNodeHueSaturation'); hs.inputs['Hue'].default_value = 0.5 + hue / 360.0; hs.inputs['Saturation'].default_value = sat
    L.new(tex.outputs['Color'], hs.inputs['Color'])
    col = hs.outputs['Color']
    sep = node('ShaderNodeSeparateXYZ'); L.new(uvp.outputs[0], sep.inputs[0])
    iseye = node('ShaderNodeMath', operation='GREATER_THAN'); L.new(sep.outputs['X'], iseye.inputs[0]); iseye.inputs[1].default_value = 0.95
    mk = None
    if mask is not None:
        mt = node('ShaderNodeTexImage'); mt.image = image(os.path.join(MASKS, 'PatternMask_%d_%s.png' % (mask, NAMES[mask])), True)
        mt.interpolation = 'Linear'; mt.extension = 'EXTEND'; L.new(uvp.outputs[0], mt.inputs['Vector'])
        mk = node('ShaderNodeMath', operation='MULTIPLY'); L.new(mt.outputs['Color'], mk.inputs[0]); mk.inputs[1].default_value = strength
        mix = node('ShaderNodeMix'); mix.data_type = 'RGBA'; L.new(mk.outputs[0], mix.inputs['Factor'])
        L.new(col, mix.inputs[6]); mix.inputs[7].default_value = (*patcol, 1.0); col = mix.outputs[2]
    bw = node('ShaderNodeRGBToBW'); L.new(col, bw.inputs[0])
    def mixc(a, b, fac):
        n = node('ShaderNodeMix'); n.data_type = 'RGBA'
        if isinstance(fac, float): n.inputs['Factor'].default_value = fac
        else: L.new(fac, n.inputs['Factor'])
        if isinstance(a, tuple): n.inputs[6].default_value = (*a, 1.0)
        else: L.new(a, n.inputs[6])
        if isinstance(b, tuple): n.inputs[7].default_value = (*b, 1.0)
        else: L.new(b, n.inputs[7])
        return n.outputs[2]
    def scale(c, k, add=(0, 0, 0)):
        n = node('ShaderNodeMix'); n.data_type = 'RGBA'; n.blend_type = 'MULTIPLY'; n.inputs['Factor'].default_value = 1.0
        L.new(c, n.inputs[6]); n.inputs[7].default_value = (k, k, k, 1.0)
        return n.outputs[2]
    rough, metal, emit, emit_k, alpha = 0.55, 0.0, None, 0.0, False
    if trait == 'albino':
        pale = node('ShaderNodeMath', operation='MULTIPLY_ADD'); L.new(bw.outputs[0], pale.inputs[0]); pale.inputs[1].default_value = 0.45; pale.inputs[2].default_value = 0.62
        tint = node('ShaderNodeCombineColor'); L.new(pale.outputs[0], tint.inputs[0])
        g = node('ShaderNodeMath', operation='MULTIPLY'); L.new(pale.outputs[0], g.inputs[0]); g.inputs[1].default_value = 0.88; L.new(g.outputs[0], tint.inputs[1])
        b = node('ShaderNodeMath', operation='MULTIPLY'); L.new(pale.outputs[0], b.inputs[0]); b.inputs[1].default_value = 0.86; L.new(b.outputs[0], tint.inputs[2])
        col = mixc(col, tint.outputs[0], 0.92)
        white = node('ShaderNodeMath', operation='GREATER_THAN'); L.new(bw.outputs[0], white.inputs[0]); white.inputs[1].default_value = 0.35
        red = node('ShaderNodeMath', operation='MULTIPLY'); L.new(iseye.outputs[0], red.inputs[0]); L.new(white.outputs[0], red.inputs[1])
        col = mixc(col, (0.85, 0.05, 0.10), red.outputs[0])
    elif trait == 'golden':
        gold = node('ShaderNodeMath', operation='MULTIPLY_ADD'); L.new(bw.outputs[0], gold.inputs[0]); gold.inputs[1].default_value = 1.3; gold.inputs[2].default_value = 0.30
        gc = node('ShaderNodeCombineColor'); L.new(gold.outputs[0], gc.inputs[0])
        g2 = node('ShaderNodeMath', operation='MULTIPLY'); L.new(gold.outputs[0], g2.inputs[0]); g2.inputs[1].default_value = 0.70; L.new(g2.outputs[0], gc.inputs[1])
        b2 = node('ShaderNodeMath', operation='MULTIPLY'); L.new(gold.outputs[0], b2.inputs[0]); b2.inputs[1].default_value = 0.16; L.new(b2.outputs[0], gc.inputs[2])
        not_eye = node('ShaderNodeMath', operation='SUBTRACT'); not_eye.inputs[0].default_value = 0.88; L.new(iseye.outputs[0], not_eye.inputs[1])
        col = mixc(col, gc.outputs[0], not_eye.outputs[0]); metal, rough = 0.65, 0.28
    elif trait == 'melanistic':
        col = mixc(scale(col, 0.14), (0.01, 0.012, 0.025), 0.45); rough = 0.35
    elif trait == 'glowing':
        col = scale(col, 0.55)
        glow = node('ShaderNodeMath', operation='MAXIMUM')
        if mk is not None: L.new(mk.outputs[0], glow.inputs[0])
        else: glow.inputs[0].default_value = 0.0
        L.new(iseye.outputs[0], glow.inputs[1]); emit = glow.outputs[0]; emit_k = 5.0
    elif trait == 'ghost':
        col = mixc(col, (0.72, 0.90, 1.0), 0.60); alpha = True; rough = 0.2
    bsdf = node('ShaderNodeBsdfPrincipled'); L.new(col, bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    ao = node('ShaderNodeAttribute'); ao.attribute_name = 'Col'; ao.attribute_type = 'GEOMETRY'
    L.new(ao.outputs['Color'], bsdf.inputs['Base Color']) if False else None
    shade = node('ShaderNodeMix'); shade.data_type = 'RGBA'; shade.blend_type = 'MULTIPLY'; shade.inputs['Factor'].default_value = 1.0
    L.new(col, shade.inputs[6]); L.new(ao.outputs['Color'], shade.inputs[7]); L.new(shade.outputs[2], bsdf.inputs['Base Color'])
    if emit is not None:
        e = node('ShaderNodeMix'); e.data_type = 'RGBA'; L.new(emit, e.inputs['Factor']); e.inputs[6].default_value = (0, 0, 0, 1); e.inputs[7].default_value = (0.10, 1.0, 0.75, 1.0)
        L.new(e.outputs[2], bsdf.inputs['Emission Color']); bsdf.inputs['Emission Strength'].default_value = emit_k
    out = node('ShaderNodeOutputMaterial'); surf = bsdf.outputs[0]
    if alpha:
        fr = node('ShaderNodeFresnel'); fr.inputs['IOR'].default_value = 1.6
        fm = node('ShaderNodeMath', operation='MULTIPLY_ADD'); L.new(fr.outputs[0], fm.inputs[0]); fm.inputs[1].default_value = 0.6; fm.inputs[2].default_value = 0.30
        tr = node('ShaderNodeBsdfTransparent'); ms = node('ShaderNodeMixShader'); L.new(fm.outputs[0], ms.inputs[0]); L.new(tr.outputs[0], ms.inputs[1]); L.new(bsdf.outputs[0], ms.inputs[2])
        surf = ms.outputs[0]; m.surface_render_method = 'BLENDED'
    L.new(surf, out.inputs['Surface'])
    return m

def species_object(name):
    if name not in _species_cache:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(FBX, 'Fish_%s.fbx' % name))
        ob = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]
        for o in [o for o in bpy.data.objects if o not in before and o is not ob]: bpy.data.objects.remove(o, do_unlink=True)
        ob.hide_render = True; ob.hide_viewport = True
        _species_cache[name] = ob
    return _species_cache[name]

def place(name, pos, mat, keys=(), length=None):
    src = species_object(name)
    ob = src.copy(); ob.data = src.data.copy(); bpy.context.scene.collection.objects.link(ob)
    ob.hide_render = False; ob.hide_viewport = False
    ob.location = pos
    if length:
        d = ob.dimensions                                                # local axes of the imported FBX: x width, y height, z length
        ob.scale = (min(length / d[2], 0.62 * length / d[1]),) * 3          # fit every species into the same cell box
    for k in keys: ob.data.shape_keys.key_blocks[k].value = 1.0
    ob.data.materials.clear(); ob.data.materials.append(mat)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

def label(text, pos, size=0.028):
    c = bpy.data.curves.new('lbl', 'FONT'); c.body = text; c.size = size; c.align_x = 'CENTER'
    ob = bpy.data.objects.new('lbl', c); bpy.context.scene.collection.objects.link(ob)
    ob.location = pos; ob.rotation_euler = (math.radians(90), 0, math.radians(90))
    m = bpy.data.materials.new('lblm'); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Color'].default_value = (0.92, 0.9, 0.82, 1); o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], o.inputs[0])
    c.materials.append(m)

def stage(center, width, out):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = RES; sc.render.resolution_percentage = 100
    sc.eevee.taa_render_samples = 24
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.20, 0.23, 0.26, 1); bg.inputs['Strength'].default_value = 1.3
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.scene.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = width; cam.location = (5, center[0], center[1]); cam.rotation_euler = (math.radians(90), 0, math.radians(90))
    sc.camera = cam
    for nm, en, rot, ang in (('key', 4.0, (math.radians(55), math.radians(15), math.radians(60)), 20), ('fill', 1.2, (math.radians(70), 0, math.radians(150)), 40)):
        l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); l.data.energy = en; l.data.angle = math.radians(ang); l.rotation_euler = rot
        bpy.context.scene.collection.objects.link(l)
    # a backdrop plane behind the fish (x = -0.6), neutral and slightly lighter than the world
    bpy.ops.mesh.primitive_plane_add(size=20, location=(-0.6, center[0], center[1]), rotation=(0, math.radians(90), 0))
    pl = bpy.context.active_object; pm = bpy.data.materials.new('back'); pm.use_nodes = True
    pm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.30, 0.34, 0.36, 1); pm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.9
    pl.data.materials.append(pm)
    sc.render.filepath = os.path.join(PREV, out); sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    print('FGPREVIEW', out)

def grid_xy(c, r, cw, rh, cols, rows):
    return (c - (cols - 1) / 2.0) * cw, -(r - (rows - 1) / 2.0) * rh

def shapes_x_patterns():
    clear(); sp = 'Carp'
    cols, rows, cw, rh = 8, 5, 0.47, 0.25
    shapes = [('Base', ()), ('Fat', ('Fat',)), ('Long', ('Long',)), ('BigFin', ('BigFin',)), ('Stubby', ('Stubby',))]
    for r, (sn, keys) in enumerate(shapes):
        label(sn, (-0.5, -(cols / 2.0) * cw - 0.02, grid_xy(0, r, cw, rh, cols, rows)[1]), 0.035)
        for c in range(cols):
            x, z = grid_xy(c, r, cw, rh, cols, rows)
            mat = material('m%d_%d' % (r, c), skin_png(sp), mask=c, patcol=(0.10, 0.07, 0.05), strength=0.9)
            place(sp, (0, x, z), mat, keys, length=0.34)
            if r == 0: label(NAMES[c], (-0.5, x, rows / 2.0 * rh + 0.01), 0.032)
    stage((0, 0), cols * cw + 0.2, '1_shapes_x_patterns.png')

def traits():
    clear(); sp = 'Perch'
    cols, rows, cw, rh = 7, 3, 0.46, 0.36
    tr = [('none', None), ('albino', 'albino'), ('golden', 'golden'), ('melanistic', 'melanistic'), ('glowing', 'glowing'), ('ghost', 'ghost'), ('two-tone', None)]
    rowdef = [('Base + spots', (), 2), ('Fat + BigFin + rosettes', ('Fat', 'BigFin'), 3), ('Long + stripes', ('Long',), 0)]
    for r, (rn, keys, mask) in enumerate(rowdef):
        for c, (tn, t) in enumerate(tr):
            x, z = grid_xy(c, r, cw, rh, cols, rows)
            if tn == 'two-tone': mat = material('t%d%d' % (r, c), skin_png(sp), mask=7, patcol=(0.12, 0.10, 0.22), strength=0.85)
            elif tn == 'none': mat = material('t%d%d' % (r, c), skin_png(sp), mask=mask, patcol=(0.10, 0.08, 0.06), strength=0.85)
            else: mat = material('t%d%d' % (r, c), skin_png(sp), mask=mask, patcol=(0.10, 0.08, 0.06), strength=0.85, trait=t)
            place(sp, (0, x, z), mat, keys, length=0.34)
            if r == 0: label(tn, (-0.5, x, rows / 2.0 * rh + 0.0), 0.04)
        label(rn, (-0.5, -(cols / 2.0) * cw - 0.02, grid_xy(0, r, cw, rh, cols, rows)[1] - 0.17), 0.028)
    stage((0, 0), cols * cw + 0.3, '2_traits.png')

def morphs(N, out):
    clear()
    cols, rows, cw, rh = 5, 5, 0.50, 0.30
    for i, n in enumerate(G.SPECIES):
        r, c = divmod(i, cols); x, z = grid_xy(c, r, cw, rh, cols, rows)
        mat = material('s_' + n, os.path.join(FBX, 'Skins', '%s_Morph%d.png' % (n, N)))
        place(n, (0, x, z), mat, (), length=0.40)
        label(n, (-0.5, x, z - 0.14), 0.022)
    stage((0, 0), cols * cw + 0.55, out)

if __name__ == '__main__':
    os.makedirs(PREV, exist_ok=True)
    which = os.environ.get('FG_PREVIEW', 'all')
    if which in ('all', '1'): shapes_x_patterns()
    if which in ('all', '2'): traits()
    if which in ('all', '3'): morphs(1, '3_morph1.png')
    if which in ('all', '4'): morphs(2, '4_morph2.png')
