# Face direction study: four goofy-adult faces, painted into the head texture (not stuck-on parts),
# toon-shaded (2-band ramp + inverted-hull outline) in EEVEE. Run: blender -b --python face_study.py -- <outdir>
import bpy, bmesh, math, os, sys
from mathutils import Vector
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[sys.argv.index('--') + 1]
SURF = 0.575
TEX = 1024
SS = 4  # supersample for clean edges


def ball(c, r, s=(1, 1, 1)):
    return (Vector(c), r / SURF, Vector(s))


def blob(name, elems, voxel=0.004):
    mb = bpy.data.metaballs.new(name)
    mb.resolution = mb.render_resolution = 0.006
    mb.threshold = 0.6
    for c, r, s in elems:
        e = mb.elements.new()
        e.type = 'ELLIPSOID'
        e.co = c
        e.radius = r
        e.stiffness = 2.0
        e.size_x, e.size_y, e.size_z = s
    tmp = bpy.data.objects.new(name, mb)
    bpy.context.scene.collection.objects.link(tmp)
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    m = ob.modifiers.new('r', 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel
    bpy.ops.object.modifier_apply(modifier='r')
    m = ob.modifiers.new('s', 'CORRECTIVE_SMOOTH'); m.iterations = 4; m.use_only_smooth = True
    bpy.ops.object.modifier_apply(modifier='s')
    ob.select_set(False)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def toon_mat(name, rgb=None, img=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    dif = nt.nodes.new('ShaderNodeBsdfDiffuse')
    s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].color = (0.62, 0.60, 0.72, 1)   # cool shadow band
    ramp.color_ramp.elements[1].position = 0.42
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
    mix.inputs['Factor'].default_value = 1.0
    emit = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(dif.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], mix.inputs['B'])
    if img is not None:
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = img
        nt.links.new(t.outputs['Color'], mix.inputs['A'])
    else:
        mix.inputs['A'].default_value = (*rgb, 1)
    nt.links.new(mix.outputs['Result'], emit.inputs['Color'])
    nt.links.new(emit.outputs[0], out.inputs['Surface'])
    return m


def outline(ob, width=0.006):
    m = ob.modifiers.new('outline', 'SOLIDIFY')
    m.thickness = width
    m.offset = 1.0
    m.use_flip_normals = True
    m.material_offset = len(ob.data.materials)
    ol = bpy.data.materials.get('M_Outline')
    if ol is None:
        ol = bpy.data.materials.new('M_Outline')
        ol.use_nodes = True
        ol.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.08, 0.06, 0.07, 1)
        ol.use_backface_culling = True
        ol.node_tree.nodes.remove(ol.node_tree.nodes['Principled BSDF'])
        e = ol.node_tree.nodes.new('ShaderNodeEmission')
        e.inputs['Color'].default_value = (0.08, 0.06, 0.07, 1)
        ol.node_tree.links.new(e.outputs[0], ol.node_tree.nodes['Material Output'].inputs['Surface'])
    ob.data.materials.append(ol)


def front_uv(ob, x0, x1, z0, z1):
    """Planar front projection of the face box onto the texture; everything else samples the skin border."""
    me = ob.data
    uv = me.uv_layers.new(name='UV')
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co
        u = (co.x - x0) / (x1 - x0)
        v = (co.z - z0) / (z1 - z0)
        if co.y > 0.02:          # back of the head: park in the plain skin corner
            u, v = 0.02, 0.02
        uv.data[loop.index].uv = (u, v)


# ---------------------------------------------------------------- face painting (2D, clean shapes)
class P:
    def __init__(self, skin):
        self.im = Image.new('RGBA', (TEX * SS, TEX * SS), (*[int(c * 255) for c in skin], 255))
        self.d = ImageDraw.Draw(self.im)

    def px(self, u, v):           # u, v in 0..1 (v up)
        return (u * TEX * SS, (1 - v) * TEX * SS)

    def ell(self, cu, cv, w, h, fill, outline=None, width=0):
        a = self.px(cu - w / 2, cv + h / 2); b = self.px(cu + w / 2, cv - h / 2)
        self.d.ellipse([a, b], fill=fill, outline=outline, width=int(width * TEX * SS))

    def line(self, pts, fill, width):
        self.d.line([self.px(*p) for p in pts], fill=fill, width=int(width * TEX * SS), joint='curve')
        r = width * TEX * SS / 2
        for p in (pts[0], pts[-1]):
            x, y = self.px(*p); self.d.ellipse([x - r, y - r, x + r, y + r], fill=fill)

    def poly(self, pts, fill):
        self.d.polygon([self.px(*p) for p in pts], fill=fill)

    def chord(self, cu, cv, w, h, start, end, fill):
        a = self.px(cu - w / 2, cv + h / 2); b = self.px(cu + w / 2, cv - h / 2)
        self.d.chord([a, b], start, end, fill=fill)

    def image(self, name):
        im = self.im.resize((TEX, TEX), Image.LANCZOS)
        path = os.path.join(OUT, name + '.png')
        im.save(path)
        return bpy.data.images.load(path)


INK = (34, 24, 26, 255)
WHITE = (255, 253, 246, 255)


def arc(cu, cv, w, h, a0, a1, n=12):
    return [(cu + math.cos(math.radians(a)) * w / 2, cv + math.sin(math.radians(a)) * h / 2)
            for a in [a0 + (a1 - a0) * i / n for i in range(n + 1)]]


def face_A(p):
    """Deadpan local: big round whites, tiny pupils looking off to the side, flat brows, small flat mouth."""
    for s in (-1, 1):
        p.ell(0.5 + s * 0.115, 0.55, 0.19, 0.20, WHITE, INK, 0.008)
        p.ell(0.5 + s * 0.115 + 0.045, 0.55, 0.035, 0.037, INK)            # side-eye
        p.line([(0.5 + s * 0.04, 0.70), (0.5 + s * 0.20, 0.705)], INK, 0.022)
    p.line([(0.44, 0.275), (0.56, 0.272)], INK, 0.012)


def face_B(p):
    """Sleepy dad: heavy half lids, droopy walrus moustache, thick brows."""
    for s in (-1, 1):
        cu = 0.5 + s * 0.11
        p.ell(cu, 0.55, 0.17, 0.15, WHITE, INK, 0.008)
        p.chord(cu, 0.55, 0.17, 0.15, 180, 360, (*[int(c * 255 * 0.86) for c in (0.93, 0.70, 0.56)], 255))  # lid
        p.line(arc(cu, 0.55, 0.17, 0.0, 180, 360, 2), INK, 0.009)
        p.ell(cu, 0.515, 0.03, 0.03, INK)
        p.line([(cu - s * 0.075, 0.665), (cu + s * 0.07, 0.65)], (60, 40, 30, 255), 0.032)
    # moustache: two drooping lobes
    for s in (-1, 1):
        p.poly([(0.5, 0.33), (0.5 + s * 0.06, 0.345), (0.5 + s * 0.14, 0.30), (0.5 + s * 0.16, 0.235),
                (0.5 + s * 0.11, 0.26), (0.5 + s * 0.05, 0.285), (0.5, 0.29)], (70, 46, 32, 255))
    p.line([(0.46, 0.215), (0.54, 0.22)], INK, 0.010)


def face_C(p):
    """Graphic grin: tall oval eyes with a shine, no nose, a big toothy grin, stubble dots."""
    for s in (-1, 1):
        cu = 0.5 + s * 0.095
        p.ell(cu, 0.56, 0.13, 0.20, WHITE, INK, 0.008)
        p.ell(cu - 0.02, 0.55, 0.05, 0.07, INK)
        p.ell(cu - 0.03, 0.57, 0.015, 0.02, WHITE)
        p.line(arc(cu, 0.69, 0.13, 0.05, 20, 160, 8), INK, 0.016)
    p.chord(0.5, 0.33, 0.34, 0.20, 0, 180, INK)
    p.chord(0.5, 0.33, 0.30, 0.15, 0, 180, (150, 40, 50, 255))
    p.poly([(0.36, 0.33), (0.64, 0.33), (0.62, 0.30), (0.38, 0.30)], WHITE)
    for i in range(18):
        a = math.radians(200 + i * 8)
        p.ell(0.5 + math.cos(a) * 0.22, 0.30 + math.sin(a) * 0.16, 0.008, 0.008, (120, 90, 80, 255))


def face_D(p):
    """Suspicious: one squinting, one wide, a raised brow, crooked smirk."""
    p.ell(0.39, 0.55, 0.15, 0.16, WHITE, INK, 0.008)
    p.ell(0.405, 0.545, 0.03, 0.03, INK)
    p.line(arc(0.39, 0.55, 0.15, 0.16, 15, 165, 10), INK, 0.009)
    p.line(arc(0.39, 0.70, 0.17, 0.07, 10, 170, 10), INK, 0.022)          # raised brow
    p.ell(0.615, 0.53, 0.17, 0.07, WHITE, INK, 0.008)                      # squint
    p.ell(0.63, 0.53, 0.028, 0.028, INK)
    p.line([(0.53, 0.62), (0.70, 0.595)], INK, 0.024)                       # flat brow
    p.line([(0.42, 0.285), (0.52, 0.28), (0.58, 0.30), (0.60, 0.315)], INK, 0.012)


def face_E(p):
    """JRPG hero fisherman (Dark Cloud / old-school Zelda), PEAK-flat features, side-eye pupils."""
    for s in (-1, 1):
        cu = 0.5 + s * 0.105
        p.ell(cu, 0.52, 0.15, 0.20, WHITE, INK, 0.006)
        p.ell(cu + 0.035, 0.515, 0.085, 0.12, (70, 45, 30, 255))               # iris, glancing right
        p.ell(cu + 0.035, 0.51, 0.04, 0.06, INK)
        p.ell(cu + 0.02, 0.545, 0.022, 0.03, WHITE)                            # shine
        p.line(arc(cu, 0.535, 0.16, 0.185, 15, 165, 12), INK, 0.018)          # heavy upper lash line
        p.line([(cu - s * 0.07, 0.665 - 0.0), (cu + s * 0.075, 0.69)], (55, 35, 25, 255), 0.028)  # determined brow
    p.chord(0.5, 0.33, 0.10, 0.06, 0, 180, (120, 40, 45, 255))
    p.line(arc(0.5, 0.345, 0.10, 0.03, 180, 360, 8), INK, 0.008)


SKIN = (0.93, 0.70, 0.56)
FACES = [
    ('A_deadpan', face_A, 'round', 0.030),
    ('B_sleepy_dad', face_B, 'pear', 0.045),
    ('C_graphic_grin', face_C, 'bean', 0.0),
    ('D_suspicious', face_D, 'egg', 0.040),
    ('E_jrpg_hero', face_E, 'hero', 0.020),
]


def hair():
    """Spiky hero fringe and a hair cap, dark brown."""
    el = [ball((0, 0.02, 0.07), 0.205, (1.03, 1.0, 0.85))]
    for i, (x, z, r) in enumerate([(-0.11, 0.10, 0.06), (-0.04, 0.13, 0.065), (0.04, 0.12, 0.06), (0.11, 0.09, 0.055)]):
        el.append(ball((x, -0.15, z), r, (0.8, 0.7, 1.4)))
        el.append(ball((x * 1.2, -0.175, z - 0.07), r * 0.45, (0.8, 0.7, 1.3)))
    el += [ball((0.185, -0.03, -0.03), 0.05, (0.6, 0.9, 1.5)), ball((-0.185, -0.03, -0.03), 0.05, (0.6, 0.9, 1.5))]
    return el


def head(shape, nose):
    """Head with the nose sculpted out of it (same metaball family, so no seam)."""
    if shape == 'round':
        el = [ball((0, 0, 0), 0.20, (1.0, 0.96, 1.0))]
    elif shape == 'pear':
        el = [ball((0, 0, 0.04), 0.17), ball((0, -0.01, -0.07), 0.19, (1.12, 0.95, 0.8))]
    elif shape == 'bean':
        el = [ball((0, 0, 0.02), 0.18, (0.92, 0.95, 1.18))]
    elif shape == 'hero':
        el = [ball((0, 0, 0.0), 0.198, (1.0, 0.97, 0.98)), ball((0, -0.03, -0.10), 0.10, (1.05, 0.85, 0.75))]
    else:
        el = [ball((0, 0, 0.0), 0.19, (0.94, 0.95, 1.1)), ball((0, -0.03, -0.11), 0.09, (1.0, 0.8, 0.7))]
    if nose:
        el.append(ball((0, -0.2, -0.04), nose, (0.95, 1.1, 1.0)))
        el.append(ball((0, -0.18, 0.0), nose * 0.5, (0.8, 1.0, 1.4)))
    return el


def build_and_render():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 560, 640
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.70, 0.78, 0.86, 1)
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(-68))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.62
    sc.collection.objects.link(cam); sc.camera = cam
    paths = []
    for name, fn, shape, nose in FACES:
        for o in list(sc.collection.objects):
            if o.type == 'MESH':
                bpy.data.objects.remove(o)
        p = P(SKIN); fn(p)
        img = p.image('tex_' + name)
        h = blob('Head', head(shape, nose))
        front_uv(h, -0.21, 0.21, -0.23, 0.19)
        h.data.materials.append(toon_mat('M_' + name, img=img))
        outline(h)
        ears = blob('Ears', [ball((0.19, 0.01, -0.01), 0.04, (0.45, 0.85, 1.2)), ball((-0.19, 0.01, -0.01), 0.04, (0.45, 0.85, 1.2))])
        ears.data.materials.append(toon_mat('M_ears', rgb=SKIN)); outline(ears, 0.004)
        neck = blob('Body', [ball((0, 0.01, -0.27), 0.08, (1, 1, 1.2)), ball((0, 0, -0.42), 0.22, (1.3, 0.8, 0.6))])
        neck.data.materials.append(toon_mat('M_body', rgb=(0.22, 0.45, 0.85))); outline(neck)
        extra = []
        if name.startswith('E_'):
            hr = blob('Hair', hair())
            hr.data.materials.append(toon_mat('M_hair', rgb=(0.28, 0.17, 0.10))); outline(hr)
            extra.append(hr)
        for ang, tag in ((0, 'front'), (-30, 'q')):
            for o in (h, ears, neck, *extra):
                o.rotation_euler = (0, 0, math.radians(ang))
            cam.location = (0, -3, -0.05); cam.rotation_euler = (math.radians(90), 0, 0)
            sc.render.filepath = os.path.join(OUT, '%s_%s.png' % (name, tag))
            bpy.ops.render.render(write_still=True)
            paths.append(sc.render.filepath)
    print('STUDY OK')


build_and_render()
