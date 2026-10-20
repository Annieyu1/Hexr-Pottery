"""T11 拧瓶盖: three containers (Bottle_Tight, Bottle_Loose, Jar). Saves art/blender/HexR_T11_Bottles.blend.

Run headless:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_t11_bottles.py

v2 (2026-10-18): more lifelike but still soft/stylised. Mineral-water bottle with petal base, grip rings and
shoulder ribs; sport bottle with ribbed waist; jar with gingham cloth cover and twine bow. Illustrated English
labels are rendered here from simple shapes + text (no third-party art). <= 3.5k triangles per group.

Hard requirements (Unity code depends on them):
  * each group is an empty named like the group; origin = CENTRE OF THE BASE BOTTOM, Z up (= Unity +Y)
  * parts: <Group>_Base, _Body, _Liquid, _Bubbles, _Neck (bottles), _Cap
  * <Group>_Cap is its own object, origin ON THE AXIS at the cap centre; it shows its rotation
    (white bar on bottle caps; the twine bow on the jar cloth)
  * <Group>_Liquid is the whole inner cavity; custom property fill_height_m = default level (Unity shader cuts it)
  * nothing at +9 cm X / -4 cm Y from the group origin (the cap lands there)
"""
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "art", "blender", "HexR_T11_Bottles.blend")
TEX = os.path.join(REPO, "art", "textures", "t11")
os.makedirs(TEX, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


# ------------------------------------------------------------------ colour / material helpers
def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def srgb(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16)/255 for i in (0, 2, 4))
def hx(h): return tuple(lin(c) for c in srgb(h))

_mats = {}
def M(name, h, alpha=1.0, rough=0.55):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = hx(h); b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.3
    b.inputs["Alpha"].default_value = alpha
    m.diffuse_color = (*c, alpha)
    if alpha < 1.0:
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
        m.use_backface_culling = True
    _mats[name] = m
    return m

def M_img(name, path, rough=0.6):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(path, check_existing=True)
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.25
    return m


# ------------------------------------------------------------------ textures made here (no third-party art)
def label_texture(fname, bg, blocks):
    """Render a 1024 x 256 label strip with Workbench (flat colours). u = angle around the bottle;
    the FRONT (facing the patient, -Y) is at u = 0.75. blocks: list of ('text'|'circle'|'tri'|'rect', ...)."""
    scn = bpy.data.scenes.new("LabelScene")
    W, H = 4.0, 1.0
    def flat(name, h):
        m = bpy.data.materials.new(name); m.diffuse_color = (*hx(h), 1); return m
    def add(o, m, z):
        o.location.z = z; o.data.materials.append(m); scn.collection.objects.link(o)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
    for v in bm.verts: v.co.x *= W*1.02; v.co.y *= H*1.02
    me = bpy.data.meshes.new("bg"); bm.to_mesh(me); bm.free()
    add(bpy.data.objects.new("bg", me), flat("bg", bg), 0)
    k = 0
    for b in blocks:
        k += 1; z = 0.001*k
        kind = b[0]
        if kind == "text":
            _, s, x, y, size, col, bold = b
            cu = bpy.data.curves.new(f"t{k}", 'FONT'); cu.body = s; cu.size = size; cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
            if bold: cu.offset_x = 0
            o = bpy.data.objects.new(f"t{k}", cu); o.location = (x*W - W/2, y*H - H/2, 0)
            add(o, flat(f"m{k}", col), z)
        else:
            bm = bmesh.new()
            if kind == "circle":
                _, x, y, r, col = b
                bmesh.ops.create_circle(bm, cap_ends=True, segments=48, radius=r)
                for v in bm.verts: v.co.x += x*W - W/2; v.co.y += y*H - H/2
            elif kind == "tri":
                _, pts, col = b
                vs = [bm.verts.new((px*W - W/2, py*H - H/2, 0)) for (px, py) in pts]; bm.faces.new(vs)
            elif kind == "rect":
                _, x0, y0, x1, y1, col = b
                vs = [bm.verts.new((px*W - W/2, py*H - H/2, 0)) for (px, py) in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]; bm.faces.new(vs)
            me = bpy.data.meshes.new(f"s{k}"); bm.to_mesh(me); bm.free()
            add(bpy.data.objects.new(f"s{k}", me), flat(f"m{k}", col), z)
    cd = bpy.data.cameras.new("lc"); cd.type = 'ORTHO'; cd.ortho_scale = W
    co = bpy.data.objects.new("lc", cd); co.location = (0, 0, 5); scn.collection.objects.link(co); scn.camera = co
    r = scn.render; r.engine = 'BLENDER_WORKBENCH'; r.resolution_x = 1024; r.resolution_y = 256; r.film_transparent = False
    scn.display.shading.light = 'FLAT'; scn.display.shading.color_type = 'MATERIAL'
    scn.view_settings.view_transform = 'Standard'
    path = os.path.join(TEX, fname); r.filepath = path; r.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True, scene=scn.name)
    bpy.data.scenes.remove(scn)
    return path

def gingham_texture(fname, h1, h2, n=8, size=256):
    a, b, w = np.array(srgb(h1)), np.array(srgb(h2)), np.array(srgb("FFFDF8"))
    x = np.arange(size); band = ((x*n)//size) % 2
    vx, vy = band[None, :], band[:, None]
    img = np.where((vx & vy)[..., None] == 1, a, np.where((vx ^ vy)[..., None] == 1, b, w))
    rgba = np.concatenate([img, np.ones((size, size, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new(fname, size, size); im.colorspace_settings.name = 'sRGB'
    im.pixels.foreach_set(rgba.ravel())
    path = os.path.join(TEX, fname); im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    return path


# ------------------------------------------------------------------ revolve a profile (with optional angular modulation + UVs)
def interp_r(side, z):
    pts = [(p[0], p[1]) for p in side]
    for (r0, z0), (r1, z1) in zip(pts[:-1], pts[1:]):
        if z0 <= z <= z1 and z1 > z0:
            return r0 + (r1-r0)*(z-z0)/(z1-z0)
    return pts[-1][0]

def lathe(name, profile, segs, mats, seg_mat=None, uv=None):
    """profile: [(r, z) or (r, z, mod)] bottom->top, r == 0 on the axis at both ends.
    mod(theta) -> (radius factor, z offset). seg_mat[i]: material of strip i. uv: ('cyl', z0, z1) or ('top', R)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    rings = []
    for p in profile:
        r, z = p[0], p[1]; mod = p[2] if len(p) > 2 else None
        if r <= 1e-9:
            rings.append([bm.verts.new((0, 0, z))]); continue
        ring = []
        for i in range(segs):
            th = math.tau*i/segs
            f, dz = mod(th) if mod else (1.0, 0.0)
            ring.append(bm.verts.new((r*f*math.cos(th), r*f*math.sin(th), z+dz)))
        rings.append(ring)
    for k, (a, b) in enumerate(zip(rings[:-1], rings[1:])):
        mi = seg_mat[k] if seg_mat else 0
        for i in range(segs):
            j = (i+1) % segs
            if len(a) == 1: vs, us = (a[0], b[j], b[i]), (0.5, (i+1)/segs, i/segs)
            elif len(b) == 1: vs, us = (a[i], a[j], b[0]), (i/segs, (i+1)/segs, 0.5)
            else: vs, us = (a[i], a[j], b[j], b[i]), (i/segs, (i+1)/segs, (i+1)/segs, i/segs)
            f = bm.faces.new(vs); f.material_index = mi
            for lp, u in zip(f.loops, us):
                co = lp.vert.co
                if uv and uv[0] == 'cyl':
                    lp[uvl].uv = (u, max(0.0, min(1.0, (co.z - uv[1])/(uv[2]-uv[1]))))
                elif uv and uv[0] == 'top':
                    lp[uvl].uv = (0.5 + co.x/(2*uv[1]), 0.5 + co.y/(2*uv[1]))
                else:
                    lp[uvl].uv = (u, 0.5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def finish(name, bm, mats, hard_deg=38):
    for f in bm.faces: f.smooth = True
    lim = math.radians(hard_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            try:
                if e.calc_face_angle() > lim: e.smooth = False
            except ValueError: pass
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    return me

def add_box(bm, size, loc, mi):
    v = bmesh.ops.create_cube(bm, size=1)["verts"]
    for p in v: p.co.x = p.co.x*size[0] + loc[0]; p.co.y = p.co.y*size[1] + loc[1]; p.co.z = p.co.z*size[2] + loc[2]
    for f in {f for vv in v for f in vv.link_faces}: f.material_index = mi

def add_torus(bm, R, r, loc, mi, seg=24, ring=6, rot_x=0.0):
    v = bmesh.ops.create_cone(bm, cap_ends=False, segments=3, radius1=0, radius2=0, depth=0)["verts"] if False else None
    verts = []
    for i in range(seg):
        a = math.tau*i/seg; row = []
        for j in range(ring):
            b = math.tau*j/ring
            p = Vector(((R + r*math.cos(b))*math.cos(a), (R + r*math.cos(b))*math.sin(a), r*math.sin(b)))
            if rot_x: p.rotate(__import__("mathutils").Euler((rot_x, 0, 0)))
            row.append(bm.verts.new(p + Vector(loc)))
        verts.append(row)
    for i in range(seg):
        for j in range(ring):
            f = bm.faces.new((verts[i][j], verts[(i+1) % seg][j], verts[(i+1) % seg][(j+1) % ring], verts[i][(j+1) % ring]))
            f.material_index = mi

def merge(dst, src):
    """append bmesh src into dst (material indices kept)"""
    me = bpy.data.meshes.new("tmp"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)


# ------------------------------------------------------------------ angular modulations
def petal(n=5, amp=0.10, dz=0.0035):
    return lambda th: (1 - amp*0.5*(1 - math.cos(n*th)), -dz*0.5*(1 + math.cos(n*th)))
def ribs(n, amp):
    return lambda th: (1 + amp*math.tanh(2.2*math.cos(n*th)), 0.0)


# ------------------------------------------------------------------ one container
def make_group(gname, x, S):
    col = bpy.data.collections.new(gname); sc.collection.children.link(col)
    root = bpy.data.objects.new(gname, None); root.empty_display_type = 'PLAIN_AXES'; root.empty_display_size = 0.05
    root.location = (x, 0, 0); col.objects.link(root)
    def obj(part, me, loc=(0, 0, 0)):
        o = bpy.data.objects.new(f"{gname}_{part}", me); o.parent = root; o.location = loc
        col.objects.link(o); return o

    # base: wooden cup holder, 3 cm wider than the body, 2 cm high, with a shallow lip ring
    R = S["body_r"] + 0.015
    base = [(0, 0.0), (R-0.005, 0.0), (R, 0.005), (R, 0.0155), (R-0.004, 0.0195), (R-0.007, 0.02), (0, 0.02)]
    obj("Base", finish(f"{gname}_Base", lathe("b", base, 32, None), [M("T11_Wood", "DDB27C")]))

    # body: translucent single layer + illustrated label band (UV: u = angle, v = label height)
    side = list(S["body"])
    l0, l1 = S["label"]
    seg_mat = []
    for i in range(len(side)-1):
        zm = (side[i][1] + side[i+1][1])/2
        seg_mat.append(1 if l0 - 1e-6 <= zm <= l1 + 1e-6 and side[i][0] > 0 and side[i+1][0] > 0 else 0)
    glass = M(f"T11_Glass_{gname}", S["glass"], alpha=0.30, rough=0.12)
    label = M_img(f"T11_Label_{gname}", S["label_tex"])
    obj("Body", finish(f"{gname}_Body", lathe("body", side, S["segs"], None, seg_mat=seg_mat, uv=('cyl', l0, l1)), [glass, label]))

    # liquid: whole inner cavity (shader cuts the level)
    inset = 0.0035; z_lo = 0.026
    inner = [(max(0.004, interp_r(side, z_lo) - inset), z_lo)]
    for p in side:
        r, z = p[0], p[1]
        if r <= 1e-9 or z <= z_lo: continue
        inner.append((max(0.004, r - inset), min(z, S["cavity_top"])))
    inner += S.get("neck_inner", [])
    cav = [(0, z_lo)] + inner + [(0, inner[-1][1])]
    lo = obj("Liquid", finish(f"{gname}_Liquid", lathe("liq", cav, 16, None), [liquid_preview_mat(gname, S["liquid_c"], S["fill"])]))
    lo["fill_height_m"] = S["fill"]

    # a few small bubbles, below the default level
    rnd = random.Random(hash(gname) & 0xffff)
    bm = bmesh.new()
    for k in range(S.get("bubbles", 6)):
        z = rnd.uniform(0.035, S["fill"] - 0.012); rr = interp_r(side, z) - inset - 0.004
        a = rnd.uniform(0, math.tau); d = rnd.uniform(0.3, 0.9)*rr
        v = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=rnd.uniform(0.0012, 0.0024))["verts"]
        for p in v: p.co += Vector((d*math.cos(a), d*math.sin(a), z))
    obj("Bubbles", finish(f"{gname}_Bubbles", bm, [M("T11_Bubble", "F4FBFF", rough=0.2)]))

    # neck (bottles): thread ridges + support ring
    if "neck" in S:
        obj("Neck", finish(f"{gname}_Neck", lathe("neck", S["neck"], 24, None), [M("T11_NeckPlastic", "EDE6D8")]))

    # cap (own object, origin on the axis at the cap centre)
    cz = S["cap_z"]
    if S.get("cloth"):
        obj("Cap", make_jar_cap(gname, S), loc=(0, 0, cz))
    else:
        obj("Cap", make_bottle_cap(gname, S), loc=(0, 0, cz))
    return root

def make_bottle_cap(gname, S):
    cr, h = S["cap_r"], S["cap_h"]/2
    knurl = ribs(S["ridges"], S["amp"])
    prof = [(0, h-0.0008), (cr*0.55, h-0.0008), (cr*0.62, h), (cr-0.0025, h), (cr-0.0008, h-0.0014),
            (cr, h-0.0035, knurl), (cr, -h+0.003, knurl), (cr-0.0003, -h+0.0012), (cr-0.0015, -h), (0, -h)]
    bm = lathe("cap", prof, 48, None)
    for f in bm.faces: f.material_index = 0
    add_box(bm, (cr*1.45, 0.0042, 0.0010), (cr*0.08, 0, h+0.0002), 1)            # white bar on top
    add_box(bm, (0.0015, 0.0042, S["cap_h"]*0.55), (cr*(1+S["amp"]) - 0.0001, 0, 0), 1)   # side stripe
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(f"{gname}_Cap", bm, [M(f"T11_Cap_{gname}", S["cap_c"]), M("T11_Marker", "FFFFFF")])

def make_jar_cap(gname, S):
    cr, h = S["cap_r"], S["cap_h"]/2
    knurl = ribs(S["ridges"], S["amp"])
    lid = [(0, h), (cr-0.003, h), (cr-0.001, h-0.0015), (cr, h-0.004, knurl), (cr, -h+0.002, knurl), (cr-0.0015, -h), (0, -h)]
    bm = lathe("lid", lid, 48, None)
    for f in bm.faces: f.material_index = 0
    # gingham cloth draped over the lid, wavy hem
    hem = lambda th: (1 + 0.025*math.cos(7*th), -0.004*(0.5 + 0.5*math.cos(7*th)))
    cloth = [(0, h+0.0035), (cr*0.6, h+0.0034), (cr+0.0015, h+0.0018), (cr+0.0035, h-0.004), (cr+0.006, -h-0.004, hem), (cr+0.0068, -h-0.011, hem), (0.0, -h-0.011)]
    cb = lathe("cloth", cloth[:-1] + [(cr+0.0055, -h-0.0112, hem), (0, -h-0.0112)], 40, None, uv=('top', cr+0.01))
    for f in cb.faces: f.material_index = 1
    merge(bm, cb)
    # twine: ring around the cloth + a small bow on +X (the rotation marker)
    add_torus(bm, cr+0.0042, 0.0016, (0, 0, -h+0.0015), 2, seg=40, ring=6)
    for sgn in (-1, 1):
        add_torus(bm, 0.006, 0.0014, (cr+0.007, sgn*0.0055, -h+0.0015), 2, seg=12, ring=5, rot_x=math.pi/2)
    add_box(bm, (0.0016, 0.0016, 0.012), (cr+0.006, 0.002, -h-0.004), 2)
    add_box(bm, (0.0016, 0.0016, 0.012), (cr+0.006, -0.002, -h-0.005), 2)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    gpath = gingham_texture("gingham_jar.png", "E9867C", "F4C1B8")
    return finish(f"{gname}_Cap", bm, [M(f"T11_Cap_{gname}", S["cap_c"]), M_img("T11_Gingham", gpath, rough=0.85), M("T11_Twine", "C9A06A", rough=0.9)])


def liquid_preview_mat(gname, h, fill):
    """Blender-only preview of the Unity liquid shader (cut above `fill`, back faces = surface). Not exported."""
    m = bpy.data.materials.new(f"T11_Liquid_{gname}"); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    body = nt.nodes.new("ShaderNodeBsdfPrincipled"); top = nt.nodes.new("ShaderNodeBsdfPrincipled")
    c = hx(h); ct = tuple(min(1.0, x*1.35 + 0.06) for x in c)
    body.inputs["Base Color"].default_value = (*c, 1); body.inputs["Roughness"].default_value = 0.25
    top.inputs["Base Color"].default_value = (*ct, 1); top.inputs["Roughness"].default_value = 0.2
    geo = nt.nodes.new("ShaderNodeNewGeometry"); tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    gt = nt.nodes.new("ShaderNodeMath"); gt.operation = 'GREATER_THAN'; gt.inputs[1].default_value = fill; gt.label = "fill height (m)"
    mix_face = nt.nodes.new("ShaderNodeMixShader"); mix_cut = nt.nodes.new("ShaderNodeMixShader")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    nt.links.new(tc.outputs["Object"], sep.inputs[0]); nt.links.new(sep.outputs["Z"], gt.inputs[0])
    nt.links.new(geo.outputs["Backfacing"], mix_face.inputs[0]); nt.links.new(body.outputs[0], mix_face.inputs[1]); nt.links.new(top.outputs[0], mix_face.inputs[2])
    nt.links.new(gt.outputs[0], mix_cut.inputs[0]); nt.links.new(mix_face.outputs[0], mix_cut.inputs[1]); nt.links.new(tr.outputs[0], mix_cut.inputs[2])
    nt.links.new(mix_cut.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = False
    try: m.surface_render_method = 'DITHERED'
    except Exception: pass
    m.diffuse_color = (*c, 1)
    return m


# ------------------------------------------------------------------ labels (English; front of the label at u = 0.75)
lab_tight = label_texture("label_tight.png", "F4FAFC", [
    ("rect", 0.0, 0.0, 1.0, 0.16, "8FC6E2"), ("rect", 0.0, 0.84, 1.0, 1.0, "8FC6E2"),
    ("tri", [(0.60, 0.22), (0.68, 0.62), (0.76, 0.22)], "A9CFA0"), ("tri", [(0.70, 0.22), (0.80, 0.70), (0.90, 0.22)], "7FB58C"),
    ("tri", [(0.785, 0.62), (0.80, 0.70), (0.815, 0.62)], "FFFFFF"),
    ("text", "MEADOW SPRING", 0.75, 0.80, 0.12, "3F7FA6", True),
    ("text", "natural mineral water", 0.75, 0.30, 0.09, "4F7F95", False),
    ("circle", 0.25, 0.55, 0.13, "8FC6E2"), ("text", "500 mL", 0.25, 0.25, 0.08, "4F7F95", False),
    ("text", "Still · Low sodium", 0.25, 0.55, 0.055, "FFFFFF", False)])
lab_loose = label_texture("label_loose.png", "FFF3DC", [
    ("rect", 0.0, 0.0, 1.0, 0.18, "F7A86A"), ("rect", 0.0, 0.82, 1.0, 1.0, "F7A86A"),
    ("circle", 0.62, 0.52, 0.17, "F6C94E"), ("circle", 0.62, 0.52, 0.13, "FBE7A0"),
    ("tri", [(0.62, 0.52), (0.60, 0.66), (0.64, 0.66)], "F6C94E"), ("tri", [(0.62, 0.52), (0.585, 0.40), (0.655, 0.40)], "F6C94E"),
    ("text", "ZEST", 0.80, 0.60, 0.26, "E0704A", True),
    ("text", "citrus sport drink", 0.80, 0.30, 0.085, "B5683F", False),
    ("text", "500 mL", 0.25, 0.50, 0.10, "B5683F", False)])
lab_jar = label_texture("label_jar.png", "FFF7EC", [
    ("rect", 0.0, 0.0, 1.0, 0.12, "E58E86"), ("rect", 0.0, 0.88, 1.0, 1.0, "E58E86"),
    ("circle", 0.62, 0.48, 0.14, "E25A62"), ("tri", [(0.58, 0.62), (0.62, 0.70), (0.66, 0.62)], "7BB86A"),
    ("circle", 0.60, 0.50, 0.012, "FFE7A0"), ("circle", 0.64, 0.44, 0.012, "FFE7A0"), ("circle", 0.62, 0.55, 0.012, "FFE7A0"),
    ("text", "Strawberry", 0.80, 0.62, 0.15, "B8414B", True),
    ("text", "homemade jam", 0.80, 0.33, 0.085, "8E5A4E", False),
    ("text", "with love · 250 g", 0.25, 0.50, 0.075, "8E5A4E", False)])


# ------------------------------------------------------------------ specs (metres, group-local, base bottom at z = 0)
NECK = [(0, 0.22), (0.0140, 0.22), (0.0140, 0.2232), (0.0168, 0.2235), (0.0168, 0.2258), (0.0140, 0.2262),
        (0.0140, 0.2290), (0.0152, 0.2300), (0.0140, 0.2310), (0.0152, 0.2335), (0.0140, 0.2345), (0.0140, 0.2400), (0, 0.2400)]
NECK_IN = [(0.0105, 0.222), (0.0105, 0.236)]

TIGHT = dict(   # mineral-water bottle: petal base, grip rings, shoulder ribs
    body_r=0.0325, segs=26,
    body=[(0, 0.024, petal()), (0.020, 0.0205, petal()), (0.0285, 0.0225, petal()), (0.0318, 0.027, petal(amp=0.05, dz=0.001)),
          (0.0325, 0.034), (0.0325, 0.046), (0.0312, 0.049), (0.0325, 0.052), (0.0325, 0.062), (0.0312, 0.065), (0.0325, 0.068),
          (0.0325, 0.080), (0.0325, 0.130),
          (0.0325, 0.142), (0.0312, 0.145), (0.0325, 0.148),
          (0.0322, 0.160, ribs(10, 0.03)), (0.0300, 0.178, ribs(10, 0.05)), (0.0255, 0.196, ribs(10, 0.05)), (0.0190, 0.210), (0.0150, 0.220), (0, 0.220)],
    label=(0.080, 0.130), cavity_top=0.217, neck=NECK, neck_inner=NECK_IN,
    glass="BFE6F1", label_tex=lab_tight, liquid_c="86CBEA", fill=0.125,
    cap_z=0.24, cap_h=0.02, cap_r=0.017, ridges=28, amp=0.03, cap_c="EE8D7C")
LOOSE = dict(   # sport bottle: concave base, ribbed waist grip
    body_r=0.0315, segs=30,
    body=[(0, 0.0245), (0.018, 0.0205), (0.027, 0.0205), (0.0305, 0.024), (0.0315, 0.032), (0.0310, 0.062), (0.0300, 0.072),
          (0.0300, 0.080), (0.0300, 0.122),
          (0.0285, 0.128, ribs(14, 0.05)), (0.0270, 0.140, ribs(14, 0.07)), (0.0285, 0.152, ribs(14, 0.05)), (0.0305, 0.160),
          (0.0305, 0.170), (0.0240, 0.198), (0.0150, 0.219), (0.0145, 0.220), (0, 0.220)],
    label=(0.080, 0.122), cavity_top=0.217, neck=NECK, neck_inner=NECK_IN,
    glass="C9EBD3", label_tex=lab_loose, liquid_c="F6BC76", fill=0.125,
    cap_z=0.24, cap_h=0.02, cap_r=0.017, ridges=22, amp=0.045, cap_c="86C894")
JAR = dict(     # jam jar with gingham cloth cover
    body_r=0.045, segs=36,
    body=[(0, 0.022), (0.036, 0.020), (0.0430, 0.024), (0.0450, 0.032), (0.0450, 0.045), (0.0450, 0.085), (0.0450, 0.112),
          (0.0428, 0.120), (0.0395, 0.124), (0.0395, 0.1265), (0.0405, 0.1275), (0.0395, 0.1290), (0.0390, 0.130), (0, 0.130)],
    label=(0.045, 0.085), cavity_top=0.126,
    glass="E4EEF2", label_tex=lab_jar, liquid_c="E36F7D", fill=0.100, bubbles=0,
    cap_z=0.14, cap_h=0.02, cap_r=0.047, ridges=24, amp=0.02, cap_c="F5C75C", cloth=True)

roots = [make_group("Bottle_Tight", -0.15, TIGHT), make_group("Bottle_Loose", 0.0, LOOSE), make_group("Jar", 0.15, JAR)]


# ------------------------------------------------------------------ preview-only: table, light, cameras (collection "Preview_Only")
pv = bpy.data.collections.new("Preview_Only"); sc.collection.children.link(pv)
tb_me = bpy.data.meshes.new("PreviewTable"); bmx = bmesh.new()
bmesh.ops.create_cube(bmx, size=1)
for v in bmx.verts: v.co.x *= 0.9; v.co.y *= 0.5; v.co.z = v.co.z*0.04 - 0.02
bmx.to_mesh(tb_me); bmx.free(); tb_me.materials.append(M("Preview_Table", "E9CE9F"))
tb = bpy.data.objects.new("PreviewTable", tb_me); tb.location = (0, 0.1, 0); pv.objects.link(tb)
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 3.2; sun.color = (1.0, 0.92, 0.8); sun.angle = math.radians(8)
so = bpy.data.objects.new("Sun", sun); pv.objects.link(so)
so.rotation_euler = Vector((0.5, 0.7, -0.6)).to_track_quat('-Z', 'Y').to_euler()
wd = bpy.data.worlds.new("W"); sc.world = wd; wd.use_nodes = True
bg = next(n for n in wd.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.55, 0.42, 0.34, 1); bg.inputs["Strength"].default_value = 0.9
def cam(name, loc, look, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.01; cd.clip_end = 20
    co = bpy.data.objects.new(name, cd); pv.objects.link(co); co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
cam("Cam_Front", (0.0, -0.95, 0.20), (0.0, 0.0, 0.125), 50)
cam("Cam_Top", (0.0, -0.20, 0.75), (0.0, 0.0, 0.15), 50)
cam("Cam_Seat", (0.0, -0.55, 0.42), (0.0, 0.0, 0.12), 40)
cam("Cam_Close", (-0.06, -0.42, 0.22), (-0.06, 0.0, 0.15), 50)
sc.camera = bpy.data.objects["Cam_Front"]
sc.render.engine = 'BLENDER_EEVEE_NEXT'; sc.render.resolution_x = 1280; sc.render.resolution_y = 600
try: sc.view_settings.view_transform = 'Standard'
except TypeError: pass

for r in roots:
    t = 0
    for o in r.children:
        o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
    print(f"T11 {r.name}: {t} tris")
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("T11 saved", OUT)
