"""Hub menu sample: a classical desk with an open book; a soft low-poly diorama grows out of the pages.
Saves art/blender/HexR_Hub_Book.blend. Sample spread = T2 piano (music cottage on a hill).

Run headless:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_hub_book.py

Style refs: HEXR-Meta/Art ref/Menu/ (two images, reference only): a 3D diorama standing on an open book, thick
layered page block, red cover, soft light + AO. Ours is warm and classical (no roads / power lines / cars).

Metres, Z up. The seated player's eye is at (0, -0.42, 1.18) facing +Y; the desk top is at 0.74 (aligned to the
real table in Unity). Collections: Desk, Book, Diorama_T2 (one per spread later), Preview_Text (labels for the
render only; Unity draws them with TMP), Lighting, Cameras.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler, noise

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "art", "blender", "HexR_Hub_Book.blend")
SERIF = "/System/Library/Fonts/Supplemental/Georgia.ttf"     # preview text only

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


# ------------------------------------------------------------------ helpers (same shape as build_garden_diorama.py)
def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def hx(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16)/255 for i in (0, 2, 4))
def tolin(c): return tuple(lin(x) for x in c)
def mix(a, b, t): return tuple(a[i]+(b[i]-a[i])*t for i in range(3))
def sm(t): t = max(0.0, min(1.0, t)); return t*t*(3-2*t)

_mats = {}
def M(name, h, rough=0.85, metal=0.0, emit=None, estr=1.0):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = tolin(hx(h)); b.inputs["Base Color"].default_value = (*c, 1)
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.2
    m.diffuse_color = (*c, 1)
    if emit:
        b.inputs["Emission Color"].default_value = (*tolin(hx(emit)), 1); b.inputs["Emission Strength"].default_value = estr
    _mats[name] = m
    return m

def VC():
    if "VC" in _mats: return _mats["VC"]
    m = bpy.data.materials.new("VC"); m.use_nodes = True
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    a = nt.nodes.new("ShaderNodeAttribute"); a.attribute_name = "Col"
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.1
    _mats["VC"] = m
    return m

COLL = {}
def coll(name, parent=None):
    if name not in COLL:
        c = bpy.data.collections.new(name); (parent or sc.collection).children.link(c); COLL[name] = c
    return COLL[name]


class B:
    """One object, many parts. shade: 'hard' (smooth + sharp edges > 38 deg), 'flat' (facets) or 'organic'."""
    def __init__(self, name, cname, shade="hard"):
        self.name, self.cname, self.shade = name, cname, shade
        self.bm = bmesh.new(); self.mats = []
        self.col = self.bm.loops.layers.color.new("Col")

    def _mi(self, m):
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)

    def _place(self, verts, m, scale=None, rot=None, loc=(0, 0, 0)):
        S = Matrix.Diagonal((*(scale or (1, 1, 1)), 1))
        R = Euler([math.radians(a) for a in (rot or (0, 0, 0))]).to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(loc) @ R @ S, verts=verts)
        mi = self._mi(m)
        for f in {f for v in verts for f in v.link_faces}: f.material_index = mi

    def box(self, size, loc, m, rot=None, bottom=False, bevel=0.0):
        v = bmesh.ops.create_cube(self.bm, size=1)["verts"]
        z = loc[2] + (size[2]/2 if bottom else 0)
        self._place(v, m, scale=size, rot=rot, loc=(loc[0], loc[1], z))
        if bevel > 0:
            es = list({e for vv in v for e in vv.link_edges})
            bmesh.ops.bevel(self.bm, geom=es, offset=min(bevel, min(size)*0.45), segments=2, affect='EDGES')

    def cyl(self, r1, r2, h, loc, m, seg=12, rot=None, bottom=True, bevel=0.0, jit=0.0, seed=0):
        v = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h)["verts"]
        if jit:
            rnd = random.Random(seed)
            for p in v:
                k = 1 + rnd.uniform(-jit, jit); p.co.x *= k; p.co.y *= k
        z = loc[2] + (h/2 if bottom else 0)
        self._place(v, m, rot=rot, loc=(loc[0], loc[1], z))
        if bevel > 0:
            es = list({e for vv in v for e in vv.link_edges})
            bmesh.ops.bevel(self.bm, geom=es, offset=min(bevel, min(r1 or r2, r2 or r1, h/2)*0.9), segments=2, affect='EDGES')

    def ico(self, r, loc, m, sub=1, squash=(1, 1, 1), rot=None, jit=0.0, seed=0):
        rnd = random.Random(seed)
        v = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=r)["verts"]
        for p in v:
            if jit: p.co *= 1 + rnd.uniform(-jit, jit)
            p.co.x *= squash[0]; p.co.y *= squash[1]; p.co.z *= squash[2]
        self._place(v, m, rot=rot, loc=loc)

    def prism(self, prof, y0, y1, m, axis='y'):
        """Extrude a closed XZ profile (list of (x, z)) from y0 to y1 (axis='x': profile is (y, z), extruded along x)."""
        def P(a, b, t): return Vector((t, a, b)) if axis == 'x' else Vector((a, t, b))
        f0 = [self.bm.verts.new(P(a, b, y0)) for a, b in prof]
        f1 = [self.bm.verts.new(P(a, b, y1)) for a, b in prof]
        mi = self._mi(m); n = len(prof)
        for f in [self.bm.faces.new(f0[::-1]), self.bm.faces.new(f1)]: f.material_index = mi
        for i in range(n):
            j = (i+1) % n
            self.bm.faces.new((f0[i], f0[j], f1[j], f1[i])).material_index = mi

    def finish(self, loc=(0, 0, 0)):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        for f in self.bm.faces: f.smooth = self.shade != "flat"
        if self.shade == "hard":
            lim = math.radians(38)
            for e in self.bm.edges:
                if len(e.link_faces) == 2:
                    try:
                        if e.calc_face_angle() > lim: e.smooth = False
                    except ValueError: pass
        me = bpy.data.meshes.new(self.name); self.bm.to_mesh(me); self.bm.free()
        for m in self.mats: me.materials.append(m)
        o = bpy.data.objects.new(self.name, me); o.location = loc
        coll(self.cname).objects.link(o)
        return o


def text(name, body, loc, size, m, rot=(0, 0, 0), align='CENTER', extrude=0.0003):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.size = size; cu.align_x = align; cu.align_y = 'CENTER'
    cu.extrude = extrude
    if os.path.exists(SERIF): cu.font = bpy.data.fonts.load(SERIF, check_existing=True)
    o = bpy.data.objects.new(name, cu); o.location = loc; o.rotation_euler = [math.radians(a) for a in rot]
    cu.materials.append(m); coll("Preview_Text").objects.link(o)
    return o


# ------------------------------------------------------------------ palette (warm, classical; T2 menu colours)
WALNUT, WALNUT_L, WALNUT_D = "7A4E33", "94623F", "5C3A26"
LEATHER_G, GOLD = "4F6650", "C9A24E"
OXBLOOD, OXBLOOD_D = "7A3B32", "5E2C26"
PAGE, PAGE_E, PAGE_D = "FBF3E2", "EFE2C6", "DCC9A6"
SAGE, CORAL, HONEY, CREAM = "8FAE6A", "E58E73", "E8B45C", "F7E9C8"
WAX, WAX_L = "A8473A", "C25A47"

DESK_TOP = 0.74
BOARD = 0.004                     # cover board thickness
PW, PD = 0.27, 0.36               # one page: width (x) and depth (y)
BY = 0.06                         # book centre y
LAYERS = 7                        # visible page layers on the edges
LH = 0.0034                       # layer height
PAGE_TOP = DESK_TOP + BOARD + LAYERS*LH + 0.001


# ================================================================== DESK
d = B("Desk", "Desk")
mw, mwl, mwd = M("Walnut", WALNUT, 0.6), M("WalnutLight", WALNUT_L, 0.6), M("WalnutDark", WALNUT_D, 0.7)
TW, TD, TT = 1.24, 0.70, 0.036
DY = 0.10
d.box((TW, TD, TT), (0, DY, DESK_TOP - TT/2), mw, bevel=0.008)
d.box((TW - 0.02, TD - 0.02, 0.012), (0, DY, DESK_TOP - TT - 0.006), mwd, bevel=0.003)          # moulding step
d.box((1.02, 0.50, 0.0014), (0, DY + 0.02, DESK_TOP + 0.0007), M("Leather", LEATHER_G, 0.75))     # leather inlay
for sx in (-1, 1):                                                                                    # gold tooling line
    d.box((0.0025, 0.49, 0.0004), (sx*0.495, DY + 0.02, DESK_TOP + 0.0016), M("Gold", GOLD, 0.35, 0.8))
for sy in (-1, 1):
    d.box((0.99, 0.0025, 0.0004), (0, DY + 0.02 + sy*0.235, DESK_TOP + 0.0016), M("Gold", GOLD))
AZ = DESK_TOP - TT - 0.012
d.box((TW - 0.10, TD - 0.10, 0.09), (0, DY, AZ - 0.045), mw, bevel=0.004)                         # apron
for x in (-0.36, 0.0, 0.36):                                                                          # drawers + brass knobs
    d.box((0.30 if x else 0.36, 0.006, 0.062), (x, DY - (TD - 0.10)/2 - 0.002, AZ - 0.045), mwl, bevel=0.003)
    d.cyl(0.008, 0.008, 0.012, (x, DY - (TD - 0.10)/2 - 0.006, AZ - 0.045), M("Brass", GOLD, 0.3, 0.9), seg=12, rot=(90, 0, 0), bottom=False)
for sx in (-1, 1):                                                                                    # turned, tapered legs
    for sy in (-1, 1):
        x, y = sx*(TW/2 - 0.08), DY + sy*(TD/2 - 0.08)
        d.cyl(0.032, 0.032, 0.05, (x, y, AZ - 0.14), mwd, seg=10, bevel=0.006)
        d.cyl(0.026, 0.016, AZ - 0.16 - 0.06, (x, y, 0.06), mw, seg=10)
        d.ico(0.024, (x, y, AZ - 0.14 - 0.012), mwd, sub=1, squash=(1, 1, 0.7))
        d.cyl(0.018, 0.022, 0.06, (x, y, 0.0), mwd, seg=10)
d.finish()

# brass banker's lamp (back left) — the warm key light comes from here
lp = B("DeskLamp", "Desk")
brass = M("Brass", GOLD, 0.3, 0.9)
LX, LY = -0.42, 0.30
lp.cyl(0.07, 0.065, 0.018, (LX, LY, DESK_TOP), brass, seg=24, bevel=0.004)
lp.cyl(0.008, 0.008, 0.30, (LX, LY + 0.02, DESK_TOP + 0.018), brass, seg=10)
lp.cyl(0.006, 0.006, 0.16, (LX, LY - 0.03, DESK_TOP + 0.30), brass, seg=10, rot=(90, 0, 0), bottom=False)
shade = M("LampGlass", "2F6B4F", 0.25, emit="5E9C6E", estr=0.6)
lp.prism([(LX + math.cos(a)*0.075, DESK_TOP + 0.285 + math.sin(a)*0.045) for a in [math.pi*i/12 for i in range(13)]],
         LY - 0.12, LY + 0.04, shade, axis='y')
lp.finish()

# a stack of old books + inkwell and quill (back right)
pr = B("DeskProps", "Desk")
for i, (w, dd, h, c, rz) in enumerate([(0.22, 0.16, 0.035, "6F4A6B", 4), (0.20, 0.15, 0.03, "4F6650", -6), (0.18, 0.13, 0.028, "9A5B3B", 10)]):
    z = DESK_TOP + sum(x[2] for x in [(0, 0, 0.035), (0, 0, 0.03), (0, 0, 0.028)][:i])
    pr.box((w, dd, h), (0.44, 0.30, z), M("Book%d" % i, c, 0.8), rot=(0, 0, rz), bottom=True, bevel=0.004)
    pr.box((w - 0.012, dd + 0.003, h - 0.008), (0.443, 0.30, z + 0.004), M("BookPages", PAGE_E), rot=(0, 0, rz), bottom=True)
pr.cyl(0.03, 0.026, 0.045, (0.28, 0.33, DESK_TOP), M("InkGlass", "2E3A4A", 0.15), seg=8, bevel=0.004)
pr.cyl(0.012, 0.012, 0.012, (0.28, 0.33, DESK_TOP + 0.045), brass, seg=10)
qv = bmesh.ops.create_cone(pr.bm, cap_ends=True, segments=6, radius1=0.009, radius2=0.0005, depth=0.14)["verts"]
pr._place(qv, M("Quill", "F3EAD8", 0.9), scale=(1, 0.25, 1), rot=(-25, 30, 0), loc=(0.255, 0.355, DESK_TOP + 0.085))
pr.finish()


# ================================================================== BOOK
bk = B("Book", "Book")
ox, oxd, gold = M("Oxblood", OXBLOOD, 0.7), M("OxbloodDark", OXBLOOD_D, 0.75), M("Gold", GOLD)
pg, pge, pgd = M("Page", PAGE, 0.95), M("PageEdge", PAGE_E, 0.95), M("PageEdgeDark", PAGE_D, 0.95)
# cover boards (open flat) with gold edge tooling and a rounded spine under the gutter
for sx in (-1, 1):
    bk.box((PW + 0.012, PD + 0.014, BOARD), (sx*(PW/2 + 0.004), BY, DESK_TOP + BOARD/2), ox, bevel=0.0015)
    for sy in (-1, 1):
        bk.box((PW - 0.004, 0.003, 0.0004), (sx*(PW/2 + 0.004), BY + sy*(PD/2 + 0.0035), DESK_TOP + BOARD + 0.0002), gold)
bk.cyl(0.008, 0.008, PD + 0.014, (0, BY, DESK_TOP - 0.001), oxd, seg=12, rot=(90, 0, 0), bottom=False)

def page_top(xa, i):
    """height of layer i's top at |x| = xa: pages dip into the gutter and round over the fore-edge."""
    base = DESK_TOP + BOARD + (i + 1)*LH
    dip = (0.010 + 0.0015*i)*(1 - sm(xa/0.05))
    return base - dip

def layer_profile(i, off):
    xs = [0.002, 0.008, 0.016, 0.026, 0.038, 0.05, 0.12, 0.2, PW - 0.012 - off]
    top = [(x, page_top(x, i)) for x in xs]
    xe = PW - off
    z0 = DESK_TOP + BOARD + i*LH + 0.0003
    zt = top[-1][1]
    fore =[(xe - 0.004, zt - 0.0004), (xe, (zt + z0)/2), (xe - 0.004, z0 + 0.0004)]
    bottom = [(x, page_top(x, i) - LH*0.88) for x in reversed(xs)]
    return top + fore + bottom

rnd = random.Random(5)
for sx in (-1, 1):
    for i in range(LAYERS):
        off = 0.002 + rnd.uniform(0, 0.003) + (LAYERS - 1 - i)*0.0002
        prof = layer_profile(i, off)
        if sx < 0: prof = [(-x, z) for x, z in reversed(prof)]
        dy = rnd.uniform(-0.0015, 0.0015)
        m = pg if i == LAYERS - 1 else (pge if i % 2 else pgd)
        bk.prism(prof, BY - PD/2 + 0.002 + dy, BY + PD/2 - 0.002 + dy, m)
# headband at the head of the gutter
bk.cyl(0.004, 0.004, 0.02, (0, BY + PD/2 - 0.001, DESK_TOP + 0.012), M("Headband", CORAL, 0.8), seg=8, rot=(0, 90, 0), bottom=False)
bk.finish()

# thumb-index tabs on the right fore-edge: sage = Learn, coral = Check (chapter jump, poke target >= 6 cm with collider)
tb = B("ChapterTabs", "Book")
for name, c, y in (("Learn", SAGE, BY + 0.07), ("Check", CORAL, BY - 0.02)):
    prof = [(0.0, 0.0), (0.016, 0.0), (0.022, 0.006), (0.022, 0.054), (0.016, 0.06), (0.0, 0.06)]
    v0 = [tb.bm.verts.new((PW - 0.004 + a, y - 0.03 + b, DESK_TOP + BOARD + 2*LH)) for a, b in prof]
    v1 = [tb.bm.verts.new((PW - 0.004 + a, y - 0.03 + b, DESK_TOP + BOARD + 5.6*LH)) for a, b in prof]
    mi = tb._mi(M("Tab" + name, c, 0.7))
    tb.bm.faces.new(v0[::-1]).material_index = mi; tb.bm.faces.new(v1).material_index = mi
    for k in range(len(prof)):
        j = (k+1) % len(prof); tb.bm.faces.new((v0[k], v0[j], v1[j], v1[k])).material_index = mi
    text("Tab_" + name, name, (PW + 0.010, y, DESK_TOP + BOARD + 5.6*LH + 0.0003), 0.0085, M("TabInk", "FFF6E6", 0.8), rot=(0, 0, 90))
tb.finish()

# satin ribbon from the head of the gutter, over the head edge, onto the desk (decorative)
rb = B("Ribbon", "Book", shade="organic")
pts = [(0.004, BY + 0.10, PAGE_TOP - 0.006), (0.006, BY + PD/2, PAGE_TOP - 0.004), (0.012, BY + PD/2 + 0.012, DESK_TOP + 0.012),
       (0.02, BY + PD/2 + 0.02, DESK_TOP + 0.0012), (0.045, BY + PD/2 + 0.07, DESK_TOP + 0.0012), (0.06, BY + PD/2 + 0.11, DESK_TOP + 0.0012)]
mr = M("Ribbon", HONEY, 0.45)
prev = None
for k, p in enumerate(pts):
    p = Vector(p); nxt = Vector(pts[min(k+1, len(pts)-1)]) - Vector(pts[max(k-1, 0)])
    side = nxt.cross(Vector((0, 0, 1))).normalized()*0.006
    pair = (rb.bm.verts.new(p - side), rb.bm.verts.new(p + side))
    if prev: rb.bm.faces.new((prev[0], prev[1], pair[1], pair[0])).material_index = rb._mi(mr)
    prev = pair
tip = Vector(pts[-1]) + Vector((0.004, 0.006, 0))                                                     # swallowtail notch
rb.bm.faces.new((prev[0], prev[1], rb.bm.verts.new(tip))).material_index = rb._mi(mr)
rb.finish()

# title label on the front-left margin (cream card, gold rule)
lb = B("TitleLabel", "Book")
LBX, LBY = -0.125, BY - PD/2 + 0.038
lb.box((0.13, 0.05, 0.0012), (LBX, LBY, PAGE_TOP + 0.0006), M("LabelCard", CREAM, 0.9), bevel=0.0005)
lb.box((0.122, 0.042, 0.0003), (LBX, LBY, PAGE_TOP + 0.0013), M("LabelRule", GOLD, 0.4, 0.6))
lb.box((0.118, 0.038, 0.0003), (LBX, LBY, PAGE_TOP + 0.0016), M("LabelCard", CREAM))
lb.finish()
ink = M("Ink", "5A3A28", 0.8)
text("Label_Title", "Piano time", (LBX, LBY + 0.004, PAGE_TOP + 0.0018), 0.017, ink)
text("Label_Sub", "Learn  ·  Feel the keys", (LBX, LBY - 0.012, PAGE_TOP + 0.0018), 0.0075, M("InkSoft", "8A6A50", 0.8))

# wax seal = Begin button (front-right margin), with two ribbon tails under it
ws = B("WaxSeal", "Book")
SX, SY = 0.15, BY - PD/2 + 0.04
for k, a in enumerate((-28, 12)):
    r = math.radians(a - 90)
    ws.box((0.014, 0.05, 0.0008), (SX + 0.022*math.cos(r), SY + 0.022*math.sin(r), PAGE_TOP + 0.0004), M("SealRibbon", CORAL, 0.6), rot=(0, 0, a))
ws.cyl(0.029, 0.027, 0.006, (SX, SY, PAGE_TOP + 0.001), M("Wax", WAX, 0.35), seg=20, jit=0.07, seed=3, bevel=0.0025)
ws.cyl(0.020, 0.020, 0.0014, (SX, SY, PAGE_TOP + 0.0068), M("WaxLight", WAX_L, 0.4), seg=24)
ws.cyl(0.016, 0.016, 0.0016, (SX, SY, PAGE_TOP + 0.0068), M("Wax", WAX), seg=24)
ws.finish()
text("Seal_Begin", "Begin", (SX, SY, PAGE_TOP + 0.0086), 0.0095, M("SealInk", "F6D8C4", 0.6), extrude=0.0006)


# ================================================================== DIORAMA T2: music cottage on a hill
DC = "Diorama_T2"
Z0 = PAGE_TOP
GR_D, GR_M, GR_L = hx("6E8F48"), hx("8FAE5A"), hx("C4D27E")
EARTH, EARTH_D = hx("A8784F"), hx("7A5638")
DIRT, SAND = hx("DDBE8E"), hx("E8D3A2")

FX, FY = 0.0, BY + 0.035               # footprint centre
HA, HB = 0.235, 0.135                  # half sizes
def foot_r(a):
    c, s = math.cos(a), math.sin(a)
    p = 4.0
    r = 1/((abs(c)/HA)**p + (abs(s)/HB)**p)**(1/p)
    return r*(1 + 0.025*math.sin(3*a + 1) + 0.015*math.sin(7*a))

HILL = (0.075, BY + 0.085)
def hill_z(x, y):
    dd = math.hypot((x - HILL[0])/1.15, y - HILL[1])
    return 0.042*sm((0.125 - dd)/0.075)

STREAM = [(-0.2, BY + 0.152), (-0.17, BY + 0.12), (-0.105, BY + 0.08), (-0.078, BY + 0.03), (-0.095, BY - 0.02), (-0.125, BY - 0.06), (-0.115, BY - 0.094)]
MAINPATH = [(0.035, BY - 0.11), (0.04, BY - 0.06), (0.07, BY - 0.02), (0.088, BY + 0.02), (0.096, BY + 0.045)]
SIDEPATH = [(0.045, BY - 0.04), (-0.02, BY + 0.0), (-0.055, BY + 0.03)]
SIDEPATH2 = [(-0.115, BY + 0.035), (-0.15, BY + 0.05), (-0.17, BY + 0.06)]
def seg_d(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    vx, vy = bx - ax, by - ay; L = vx*vx + vy*vy
    t = max(0, min(1, ((px - ax)*vx + (py - ay)*vy)/L)) if L else 0
    return math.hypot(px - ax - t*vx, py - ay - t*vy)
def line_d(p, pts): return min(seg_d(p, pts[i], pts[i+1]) for i in range(len(pts) - 1))

SLAB = 0.009
def ground(x, y):
    sd = line_d((x, y), STREAM)
    return Z0 + SLAB + hill_z(x, y) + 0.0015*noise.noise(Vector((x*40, y*40, 0))) - 0.007*sm((0.016 - sd)/0.008)

def top_col(x, y, t):
    n = noise.noise(Vector((x*9, y*9, 2)))
    c = mix(GR_M, GR_L, 0.25 + 0.12*n + 0.35*hill_z(x, y)/0.042)
    pd = min(line_d((x, y), MAINPATH), line_d((x, y), SIDEPATH), line_d((x, y), SIDEPATH2))
    c = mix(c, DIRT, sm((0.0085 - pd)/0.005))
    sd = line_d((x, y), STREAM)
    c = mix(c, SAND, sm((0.019 - sd)/0.004)*0.9)
    return mix(c, GR_D, sm((t - 0.9)/0.1)*0.6)

g = B("T2_Terrain", DC, shade="flat")
NS, NR = 120, 26
sec = [i*math.tau/NS for i in range(NS)]
rings = []
for k in range(NR + 1):
    t = (k/NR)**0.85; ring = []
    for a in sec:
        r = foot_r(a)*t
        x, y = FX + r*math.cos(a), FY + r*math.sin(a)
        z = ground(x, y) - 0.003*sm((t - 0.93)/0.07)
        ring.append((g.bm.verts.new((x, y, z)), top_col(x, y, t)))
    rings.append(ring)
for (t, z, cc) in ((1.0, Z0 + SLAB*0.45, EARTH), (0.985, Z0 + 0.0004, EARTH_D)):             # chunky earth edge
    ring = []
    for a in sec:
        r = foot_r(a)*t
        ring.append((g.bm.verts.new((FX + r*math.cos(a), FY + r*math.sin(a), z)), cc))
    rings.append(ring)
mvc = VC()
def face(vs):
    f = g.bm.faces.new([v for v, _ in vs]); f.material_index = g._mi(mvc)
    for lp, (_, c) in zip(f.loops, vs): lp[g.col] = (*tolin(c), 1)
centre = (g.bm.verts.new((FX, FY, ground(FX, FY))), top_col(FX, FY, 0))
for i in range(NS):
    j = (i + 1) % NS
    face([centre, rings[1][i], rings[1][j]])
for k in range(1, len(rings) - 1):
    for i in range(NS):
        j = (i + 1) % NS
        face([rings[k][i], rings[k+1][i], rings[k+1][j], rings[k][j]])
face(list(reversed(rings[-1])))
g.finish()

# stream water, with a small arched bridge
wt = B("T2_Stream", DC, shade="organic")
mwater = M("Water", "7FB6C8", 0.15, emit="9FD0DC", estr=0.15)
prev = None
for k, p in enumerate(STREAM):
    a = Vector((*STREAM[max(k-1, 0)], 0)); b = Vector((*STREAM[min(k+1, len(STREAM)-1)], 0))
    side = (b - a).cross(Vector((0, 0, 1))).normalized()*0.011
    c = Vector((*p, Z0 + SLAB - 0.004))
    pair = (wt.bm.verts.new(c - side), wt.bm.verts.new(c + side))
    if prev: wt.bm.faces.new((prev[0], prev[1], pair[1], pair[0])).material_index = wt._mi(mwater)
    prev = pair
wt.finish()

br = B("T2_Bridge", DC)
mbr, mbrw = M("BridgeRed", "B85042", 0.6), M("BridgeWood", "C89A68", 0.8)
BX0, BX1, BYc = -0.062, -0.112, BY + 0.033
for k in range(8):
    u0, u1 = k/8, (k+1)/8
    x0, x1 = BX0 + (BX1 - BX0)*u0, BX0 + (BX1 - BX0)*u1
    z0, z1 = ground(x0, BYc) + 0.004 + 0.009*math.sin(math.pi*u0), ground(x1, BYc) + 0.004 + 0.009*math.sin(math.pi*u1)
    zm = (z0 + z1)/2; ang = math.degrees(math.atan2(z1 - z0, abs(x1 - x0)))
    br.box((abs(x1 - x0) + 0.0008, 0.016, 0.0022), ((x0 + x1)/2, BYc, zm), mbrw, rot=(0, ang, 0))
    for sy in (-1, 1):
        br.box((abs(x1 - x0) + 0.0006, 0.0014, 0.0014), ((x0 + x1)/2, BYc + sy*0.0075, zm + 0.0075), mbr, rot=(0, ang, 0))
        if k % 2 == 0:
            br.box((0.0012, 0.0012, 0.0075), (x0, BYc + sy*0.0075, z0 + 0.0037), mbr)
br.finish()

# stepping stones on the main path
st = B("T2_Stones", DC, shade="flat")
mst = M("Stone", "E3D4B4", 0.9)
for k in range(9):
    u = k/8
    i = min(int(u*(len(MAINPATH) - 1)), len(MAINPATH) - 2); f = u*(len(MAINPATH) - 1) - i
    x = MAINPATH[i][0] + (MAINPATH[i+1][0] - MAINPATH[i][0])*f + (0.003 if k % 2 else -0.003)
    y = MAINPATH[i][1] + (MAINPATH[i+1][1] - MAINPATH[i][1])*f
    st.cyl(0.0055, 0.005, 0.002, (x, y, ground(x, y) - 0.0005), mst, seg=7, jit=0.12, seed=k)
st.finish()

# ---- the music cottage (faces the player), big French window with a tiny upright piano inside
CXc, CYc = HILL[0], HILL[1]
CZ = ground(CXc, CYc) - 0.001
W, D, H = 0.094, 0.066, 0.05
ck = B("T2_Cottage", DC)
plaster, brick, timber = M("Plaster", "F3E3C8", 0.9), M("Brick", "B9654E", 0.85), M("Timber", "6B4630", 0.8)
roofm, trim = M("RoofTile", "C46A48", 0.75), M("Trim", "FFF8EA", 0.7)
inner = M("Interior", "E9C08A", 0.9, emit="FFC46B", estr=0.25)
ck.box((W + 0.006, D + 0.006, 0.008), (CXc, CYc, CZ), brick, bottom=True, bevel=0.0015)            # brick plinth
fy = CYc - D/2                                                                                          # front wall y
WX0, WX1 = CXc - 0.036, CXc + 0.008                                                                     # window opening
WZ0, WZ1 = CZ + 0.014, CZ + 0.042
RD = 0.036                                                                                              # music room depth behind the window
ck.box((W, D - RD, H), (CXc, CYc + RD/2, CZ + 0.008), plaster, bottom=True)                             # body behind the room
for sx in (-1, 1):                                                                                      # room side walls
    ck.box((0.004, RD, H), (CXc + sx*(W/2 - 0.002), fy + RD/2, CZ + 0.008), plaster, bottom=True)
ck.box((W, RD, 0.002), (CXc, fy + RD/2, CZ + 0.008), M("Floorboards", "A8754C", 0.8), bottom=True)      # floor
ck.box((W, RD, 0.003), (CXc, fy + RD/2, CZ + 0.008 + H - 0.003), plaster, bottom=True)                 # ceiling
ck.box((WX1 - WX0 + 0.01, 0.001, H - 0.006), ((WX0 + WX1)/2, fy + RD - 0.0005, CZ + 0.0105), inner, bottom=True)  # lit back wall
for k, c in enumerate(("FFF3DA", CORAL, HONEY, "FFF3DA", CORAL)):                                       # window box with flowers
    ck.ico(0.0026, (WX0 + 0.005 + k*(WX1 - WX0 - 0.01)/4, fy - 0.006, WZ0 - 0.0005), M("Flower_" + c, c, 0.8), sub=1)
ck.box((WX1 - WX0 + 0.004, 0.006, 0.005), ((WX0 + WX1)/2, fy - 0.006, WZ0 - 0.0085), M("Door", "4F6650", 0.7), bottom=True, bevel=0.0008)
ck.box((WX0 - (CXc - W/2), 0.006, H), ((CXc - W/2 + WX0)/2, fy + 0.003, CZ + 0.008), plaster, bottom=True)
ck.box(((CXc + W/2) - WX1, 0.006, H), ((WX1 + CXc + W/2)/2, fy + 0.003, CZ + 0.008), plaster, bottom=True)
ck.box((WX1 - WX0, 0.006, WZ0 - CZ - 0.008), ((WX0 + WX1)/2, fy + 0.003, CZ + 0.008), plaster, bottom=True)
ck.box((WX1 - WX0, 0.006, CZ + 0.008 + H - WZ1), ((WX0 + WX1)/2, fy + 0.003, WZ1), plaster, bottom=True)
# window frame + mullions + sill
ck.box((WX1 - WX0 + 0.004, 0.003, 0.0025), ((WX0 + WX1)/2, fy - 0.0005, WZ0 - 0.0012), trim)
ck.box((WX1 - WX0 + 0.004, 0.003, 0.0025), ((WX0 + WX1)/2, fy - 0.0005, WZ1 + 0.0012), trim)
for x in (WX0, (WX0 + WX1)/2, WX1):
    ck.box((0.0022, 0.003, WZ1 - WZ0), (x, fy - 0.0005, WZ0), trim, bottom=True)
ck.box((WX1 - WX0, 0.003, 0.0015), ((WX0 + WX1)/2, fy - 0.0005, (WZ0 + WZ1)/2 + 0.005), trim)
ck.box((WX1 - WX0 + 0.008, 0.008, 0.0025), ((WX0 + WX1)/2, fy - 0.003, WZ0 - 0.0025), trim, bevel=0.0006)
# pediment over the window
pv = [ck.bm.verts.new(((WX0 + WX1)/2 + a, fy - 0.001, WZ1 + 0.003 + b)) for a, b in ((-0.026, 0), (0.026, 0), (0, 0.007))]
ck.bm.faces.new(pv).material_index = ck._mi(trim)
# door with a round window, step
DXc = CXc + 0.028
ck.box((0.018, 0.004, 0.032), (DXc, fy - 0.0005, CZ + 0.008), M("Door", "4F6650", 0.7), bottom=True, bevel=0.0008)
ck.cyl(0.004, 0.004, 0.002, (DXc, fy - 0.0025, CZ + 0.031), inner, seg=12, rot=(90, 0, 0), bottom=False)
ck.cyl(0.0012, 0.0012, 0.003, (DXc + 0.0055, fy - 0.003, CZ + 0.022), M("Brass", GOLD), seg=6, rot=(90, 0, 0), bottom=False)
ck.box((0.026, 0.01, 0.004), (DXc, fy - 0.006, CZ + 0.004), M("Stone", "E3D4B4"), bottom=True, bevel=0.001)
# timber corner posts
for sx in (-1, 1):
    for sy in (-1, 1):
        ck.box((0.004, 0.004, H), (CXc + sx*W/2, CYc + sy*D/2, CZ + 0.008), timber, bottom=True)
# gable roof (ridge along x), deep overhang, tile courses
RZ0 = CZ + 0.008 + H
OH = 0.010
rh, half = 0.038, D/2 + OH
for sy in (-1, 1):
    ang = math.degrees(math.atan2(rh, half))*(-sy)
    L = math.hypot(rh, half)
    ck.box((W + 2*OH, L, 0.004), (CXc, CYc + sy*half/2, RZ0 + rh/2 - 0.001), roofm, rot=(ang, 0, 0), bevel=0.0012)
    for k in range(1, 4):
        u = k/4
        ck.box((W + 2*OH + 0.001, 0.0018, 0.0016), (CXc, CYc + sy*half*(1 - u), RZ0 + rh*u + 0.0012), M("RoofTileDark", "A9553A", 0.8), rot=(ang, 0, 0))
for sx in (-1, 1):                                                                                       # gable ends
    gv = [ck.bm.verts.new((CXc + sx*W/2, CYc + a, RZ0 + b)) for a, b in ((-D/2, 0), (D/2, 0), (0, rh*(D/2)/half))]
    ck.bm.faces.new(gv).material_index = ck._mi(plaster)
ck.box((W + 2*OH + 0.004, 0.005, 0.005), (CXc, CYc, RZ0 + rh + 0.0005), M("RoofTileDark", "A9553A"), bevel=0.0012)
# round gable window (left) and chimney (right)
ck.cyl(0.0055, 0.0055, 0.002, (CXc - W/2 - 0.0008, CYc, RZ0 + 0.011), inner, seg=12, rot=(0, 90, 0), bottom=False)
CHX = CXc + 0.03
ck.box((0.014, 0.014, 0.05), (CHX, CYc + 0.012, RZ0 + 0.012), brick, bottom=True, bevel=0.001)
ck.box((0.018, 0.018, 0.004), (CHX, CYc + 0.012, RZ0 + 0.062), M("Plaster", "F3E3C8"), bottom=True, bevel=0.001)
ck.finish()

# tiny upright piano, seen through the window
pn = B("T2_TinyPiano", DC)
PX, PY = (WX0 + WX1)/2 - 0.002, fy + RD - 0.008
pn.box((0.032, 0.012, 0.022), (PX, PY + 0.004, CZ + 0.008), M("PianoWood", "5A3324", 0.4), bottom=True, bevel=0.001)
pn.box((0.031, 0.008, 0.0025), (PX, PY - 0.004, CZ + 0.017), M("Keys", "FFFDF6", 0.5), bottom=True)
pn.box((0.031, 0.009, 0.0015), (PX, PY - 0.002, CZ + 0.0205), M("PianoWood", "5A3324"), bottom=True)
pn.box((0.012, 0.006, 0.006), (PX, PY - 0.012, CZ + 0.008), M("Stool", "B85042", 0.7), bottom=True, bevel=0.001)
pn.finish()

# music notes drifting up from the chimney (animated in Unity: slow rise + fade, 6 s loop)
nt_ = B("T2_Notes", DC, shade="organic")
mnote = M("Note", "FFF1D0", 0.6, emit="FFE3A8", estr=0.5)
for k, (dx, dz, rz) in enumerate(((0.004, 0.022, -10), (-0.008, 0.042, 12), (0.012, 0.064, -6))):
    x, y, z = CHX + dx, CYc + 0.012, RZ0 + 0.07 + dz
    nt_.ico(0.0042, (x, y, z), mnote, sub=2, squash=(1.25, 0.6, 0.9), rot=(0, rz - 20, 0))
    nt_.box((0.0011, 0.0011, 0.014), (x + 0.0045, y, z + 0.0005), mnote, bottom=True)
    nt_.box((0.0055, 0.0011, 0.0022), (x + 0.0068, y, z + 0.0135), mnote, rot=(0, 25, 0))
nt_.finish()

# trees: cypress rows (back) and round, faceted trees; hedges along the front
tr = B("T2_Trees", DC, shade="flat")
cyp, cyp2 = M("Cypress", "4E6E3A", 0.9), M("Cypress2", "5E7E40", 0.9)
leaf = [M("Leaf1", "7FA35A", 0.9), M("Leaf2", "6E9448", 0.9), M("Leaf3", "93B562", 0.9)]
trunk = M("Trunk", "7A5638", 0.9)
rnd = random.Random(11)
for k, (x, y) in enumerate([(-0.205, BY + 0.115), (-0.18, BY + 0.145), (-0.15, BY + 0.155), (-0.125, BY + 0.16),
                             (0.155, BY + 0.155), (0.18, BY + 0.145), (0.205, BY + 0.125), (0.0, BY + 0.155), (-0.03, BY + 0.15)]):
    h = rnd.uniform(0.055, 0.085); z = ground(x, y)
    tr.cyl(0.0015, 0.0015, 0.006, (x, y, z), trunk, seg=5)
    tr.cyl(rnd.uniform(0.010, 0.013), 0.0008, h, (x, y, z + 0.004), cyp if k % 2 else cyp2, seg=7)
for k, (x, y, r) in enumerate([(-0.19, BY + 0.03, 0.022), (-0.165, BY - 0.045, 0.019), (0.145, BY - 0.07, 0.018),
                                (-0.045, BY + 0.11, 0.02), (0.205, BY + 0.06, 0.017), (-0.04, BY - 0.07, 0.015)]):
    z = ground(x, y); th = r*1.5
    tr.cyl(0.0025, 0.0018, th, (x, y, z), trunk, seg=6)
    tr.ico(r, (x, y, z + th + r*0.7), leaf[k % 3], sub=1, jit=0.08, seed=k)
    tr.ico(r*0.6, (x + r*0.6, y - r*0.2, z + th + r*0.35), leaf[(k + 1) % 3], sub=1, jit=0.1, seed=k + 20)
hedge = M("Hedge", "5F8443", 0.9)
for x0, x1, y in ((-0.225, -0.14, BY - 0.085), (0.105, 0.225, BY - 0.088)):
    n = int((x1 - x0)/0.011)
    for i in range(n + 1):
        x = x0 + (x1 - x0)*i/n; yy = y + 0.004*math.sin(i*1.7)
        tr.ico(0.0075, (x, yy, ground(x, yy) + 0.006), hedge, sub=1, squash=(1, 1, 0.85), jit=0.12, seed=i + 40)
tr.finish()

# lavender rows to the right of the cottage, flowers by the door
fl = B("T2_Garden", DC, shade="flat")
lav, lavg = M("Lavender", "9C8CC4", 0.9), M("LavenderLeaf", "7C9A6A", 0.9)
for r_ in range(4):
    x = 0.16 + r_*0.017
    for i in range(7):
        y = BY - 0.04 + i*0.016
        z = ground(x, y)
        fl.ico(0.006, (x, y, z + 0.004), lavg, sub=1, squash=(1, 1.3, 0.7), seed=r_*10 + i)
        fl.ico(0.0045, (x, y, z + 0.009), lav, sub=1, squash=(0.9, 1.2, 0.8), jit=0.1, seed=r_*10 + i + 100)
rnd = random.Random(21)
fcol = [M("FlowerCoral", CORAL, 0.8), M("FlowerCream", "FFF3DA", 0.8), M("FlowerHoney", HONEY, 0.8)]
for k in range(26):
    x = rnd.uniform(CXc - 0.06, CXc + 0.06); y = rnd.uniform(fy - 0.03, fy - 0.006)
    if line_d((x, y), MAINPATH) < 0.011: continue
    fl.ico(0.0022, (x, y, ground(x, y) + 0.002), fcol[k % 3], sub=1)
# a bench and a lamp post across the bridge
bx, by_ = -0.175, BY + 0.066
bz = ground(bx, by_)
fl.box((0.026, 0.008, 0.0016), (bx, by_, bz + 0.007), M("BridgeWood", "C89A68"), rot=(0, 0, 15))
fl.box((0.026, 0.0016, 0.007), (bx - 0.001, by_ + 0.004, bz + 0.0115), M("BridgeWood", "C89A68"), rot=(0, 0, 15))
for sx in (-1, 1):
    fl.box((0.0016, 0.007, 0.007), (bx + sx*0.011, by_ + sx*0.003, bz), timber, rot=(0, 0, 15), bottom=True)
lx, ly = -0.15, BY + 0.075
fl.cyl(0.0012, 0.0012, 0.032, (lx, ly, ground(lx, ly)), M("Iron", "3A3A3F", 0.6), seg=6)
fl.box((0.006, 0.006, 0.007), (lx, ly, ground(lx, ly) + 0.032), M("Lantern", "FFE1A0", 0.5, emit="FFC46B", estr=2.0), bottom=True)
fl.finish()


# ================================================================== light, background, cameras
key = bpy.data.lights.new("Key_Warm", 'SUN'); key.energy = 3.2; key.color = tolin(hx("FFE2B8")); key.angle = math.radians(8)
ko = bpy.data.objects.new("Key_Warm", key); coll("Lighting").objects.link(ko)
ko.rotation_euler = (-Vector((-0.45, -0.55, 0.75)).normalized()).to_track_quat('-Z', 'Y').to_euler()
lamp = bpy.data.lights.new("DeskLamp_Light", 'POINT'); lamp.energy = 6; lamp.color = tolin(hx("FFC46B")); lamp.shadow_soft_size = 0.05
lo = bpy.data.objects.new("DeskLamp_Light", lamp); lo.location = (LX, LY - 0.04, DESK_TOP + 0.25); coll("Lighting").objects.link(lo)

wd = bpy.data.worlds.new("WarmGradient"); sc.world = wd; wd.use_nodes = True
nt = wd.node_tree; nt.nodes.clear()
tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); mp_ = nt.nodes.new("ShaderNodeMapRange")
cr = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(tc.outputs["Generated"], sep.inputs[0]); nt.links.new(sep.outputs["Z"], mp_.inputs["Value"])
mp_.inputs["From Min"].default_value = 0.35; mp_.inputs["From Max"].default_value = 0.8; mp_.clamp = True
nt.links.new(mp_.outputs["Result"], cr.inputs["Fac"]); nt.links.new(cr.outputs["Color"], bg.inputs["Color"])
nt.links.new(bg.outputs["Background"], out.inputs["Surface"])
cr.color_ramp.elements[0].color = (*tolin(hx("E9B98A")), 1)
cr.color_ramp.elements[1].color = (*tolin(hx("FBEEDC")), 1)
bg.inputs["Strength"].default_value = 1.0

fb = B("Floor", "Lighting", shade="organic")                       # warm floor disc that fades into the gradient
fb.cyl(4.0, 4.0, 0.01, (0, 0, -0.01), M("Floor", "D9B08A", 0.95), seg=48)
fb.finish()

def cam(name, loc, look, lens=24):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.02; cd.clip_end = 50
    co = bpy.data.objects.new(name, cd); coll("Cameras").objects.link(co); co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return co
cam("Cam_Seat", (0.0, -0.42, 1.18), (0.0, BY + 0.03, PAGE_TOP), lens=26)
cam("Cam_Hero", (-0.62, -0.50, 1.12), (0.01, BY + 0.03, PAGE_TOP + 0.02), lens=45)
cam("Cam_Close", (0.30, -0.22, 0.93), (CXc - 0.01, CYc - 0.01, PAGE_TOP + 0.03), lens=50)
sc.camera = bpy.data.objects["Cam_Seat"]
eye = bpy.data.objects.new("Player_Eye_Ref", None); eye.empty_display_type = 'ARROWS'; eye.empty_display_size = 0.1
eye.location = (0, -0.42, 1.18); eye.rotation_euler = (math.radians(90), 0, 0); coll("Lighting").objects.link(eye)

r = sc.render
for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
    try: r.engine = eng; break
    except TypeError: pass
r.resolution_x = 1600; r.resolution_y = 900
for vt in ("AgX", "Filmic", "Standard"):
    try: sc.view_settings.view_transform = vt; break
    except TypeError: pass
try: sc.view_settings.look = "AgX - Base Contrast"
except TypeError: pass
for attr, val in (("use_raytracing", True), ("taa_render_samples", 64), ("use_shadows", True)):
    try: setattr(sc.eevee, attr, val)
    except Exception: pass

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
tris = {}
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.calc_loop_triangles(); tris[o.name] = len(o.data.loop_triangles)
dio = sum(v for k, v in tris.items() if k.startswith("T2_"))
print("HUB saved", OUT, "| tris total", sum(tris.values()), "| diorama", dio, "| book", tris.get("Book"), "| desk", tris.get("Desk"))
