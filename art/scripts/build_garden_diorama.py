"""Garden diorama: a small, chunky, soft, warm island garden. Saves art/blender/HexR_Garden.blend.

Run headless:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_garden_diorama.py

Reference split: layout and props idea from a compact "vegetable garden" diorama (own models, nothing copied);
colour tone from a warm golden-hour river illustration; modelling precision from a chunky bevelled wizard tower.

Metres, Z up. The seated player is at (0, -0.35) facing +Y at a wooden table under a chunky pergola; the
0.9 x 0.5 m operation zone in the middle of the table top stays clear.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler, noise

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "art", "blender", "HexR_Garden.blend")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


# ------------------------------------------------------------------ helpers
def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def hx(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16)/255 for i in (0, 2, 4))
def tolin(c): return tuple(lin(x) for x in c)
def mix(a, b, t): return tuple(a[i]+(b[i]-a[i])*t for i in range(3))
def sm(t): t = max(0.0, min(1.0, t)); return t*t*(3-2*t)
Z = Vector((0, 0, 1))
SUN = Vector((0.45, -0.3, 0.8)).normalized()

_mats = {}
def M(name, h, emit=None, estr=1.0):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = tolin(hx(h)); b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = 0.85
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.15
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
    m.use_backface_culling = False
    _mats["VC"] = m
    return m

COLL = {}
def coll(name):
    if name not in COLL:
        c = bpy.data.collections.new(name); sc.collection.children.link(c); COLL[name] = c
    return COLL[name]


class B:
    """One object, many parts. shade: 'hard' (smooth + sharp edges > 35 deg) or 'organic' (all smooth)."""
    def __init__(self, name, cname="Garden", shade="hard"):
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

    def box(self, size, loc, m, rot=None, bottom=False, bevel=0.03):
        v = bmesh.ops.create_cube(self.bm, size=1)["verts"]
        z = loc[2] + (size[2]/2 if bottom else 0)
        self._place(v, m, scale=size, rot=rot, loc=(loc[0], loc[1], z))
        if bevel > 0:
            es = list({e for vv in v for e in vv.link_edges})
            bmesh.ops.bevel(self.bm, geom=es, offset=min(bevel, min(size)*0.45), segments=3, affect='EDGES')

    def cyl(self, r1, r2, h, loc, m, seg=16, rot=None, bottom=True, cap=True, bevel=0.0):
        v = bmesh.ops.create_cone(self.bm, cap_ends=cap, segments=seg, radius1=r1, radius2=r2, depth=h)["verts"]
        z = loc[2] + (h/2 if bottom else 0)
        self._place(v, m, rot=rot, loc=(loc[0], loc[1], z))
        if bevel > 0:
            es = list({e for vv in v for e in vv.link_edges})
            bmesh.ops.bevel(self.bm, geom=es, offset=min(bevel, min(r1, r2, h/2)*0.9), segments=3, affect='EDGES')

    def blob(self, r, loc, m, sub=3, squash=(1, 1, 1), rot=None, grad=None, jit=0.0, seed=0):
        """Smooth ellipsoid. grad=(dark, light) paints a sun-lit vertex-colour gradient (uses the VC material)."""
        rnd = random.Random(seed)
        v = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=r)["verts"]
        for p in v:
            if jit: p.co *= 1 + rnd.uniform(-jit, jit)
            p.co.x *= squash[0]; p.co.y *= squash[1]; p.co.z *= squash[2]
        self._place(v, VC() if grad else m, rot=rot, loc=loc)
        if grad:
            dark, light = grad
            for vv in v:
                n = vv.co.normalized(); zr = vv.co.z/max(1e-6, r*squash[2])
                t = sm(0.42 + 0.5*n.dot(SUN) + 0.22*zr)
                c = mix(dark, light, t)
                for l in vv.link_loops: l[self.col] = (*tolin(c), 1)

    def strip_leaf(self, base, direction, length, width, bend, dark, light, segs=6, roll=0.0):
        """A big, smooth, curved leaf (like the chunky leaves on the pergola)."""
        d = Vector(direction).normalized()
        side = d.cross(Z); side = side.normalized() if side.length > 1e-4 else Vector((1, 0, 0))
        if roll: side = (side*math.cos(roll) + Z*math.sin(roll)).normalized()
        rows = []
        for i in range(segs+1):
            u = i/segs
            c = Vector(base) + d*length*u + Z*(-bend*length*u*u)
            wv = width*math.sin(math.pi*min(0.999, 0.12+0.88*u))*(1-0.15*u)
            col = mix(dark, light, sm(0.2+0.8*u))
            rows.append(((self.bm.verts.new(c - side*wv), col), (self.bm.verts.new(c), mix(col, light, 0.2)), (self.bm.verts.new(c + side*wv), col)))
        m = VC()
        for a, b in zip(rows[:-1], rows[1:]):
            for k in range(2):
                f = self.bm.faces.new((a[k][0], b[k][0], b[k+1][0], a[k+1][0])); f.material_index = self._mi(m)
                for lp, q in zip(f.loops, (a[k], b[k], b[k+1], a[k+1])): lp[self.col] = (*tolin(q[1]), 1)

    def finish(self, loc=(0, 0, 0)):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        for f in self.bm.faces: f.smooth = True
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


# ------------------------------------------------------------------ palette (river-image tone)
OL_D, OL_M, OL_L = hx("5E8A3C"), hx("86AB4A"), hx("CDD878")      # olive -> sunlit yellow-green
TEAL_D, TEAL_L = hx("3F7F55"), hx("9CCB70")
EARTH_L, EARTH_D = hx("B9824F"), hx("6E4B34")
DIRT, DIRT_D = hx("DDAA70"), hx("CB9558")
SAND, SAND_L = hx("EDD18E"), hx("F6E3B2")
WATER_S, WATER_D = hx("86CAD2"), hx("3F8FAE")
CREAM, CREAM2 = "F7E9C8", "EBD6A8"
WOOD, WOOD2, TRUNK = "D8A66A", "C08A52", "8A5F3E"
CORAL, LAV, PINK, YEL = "E58F7F", "B9A5D6", "F4B6C2", "F8C24A"

CX, CY = 0.0, 1.0                      # island centre
R0 = 4.3
def island_r(a): return R0*(1 + 0.10*math.sin(3*a+1.0) + 0.06*math.sin(5*a+2.2) + 0.04*math.sin(9*a))
POND = (2.2, 2.4, 0.95)               # x, y, radius
def pond_k(x, y): return sm(1.25 - math.hypot(x-POND[0], y-POND[1])/POND[2])
def path_x(y): return 0.35*math.sin((y+1.2)*0.9) if y < -0.9 else 0.0      # dirt path from the table front to the island edge
def on_path(x, y, pad=0.0): return y < -0.9 and abs(x - path_x(y)) < 0.45 + pad
def ground_z(x, y):
    d = math.hypot(x-CX, y-CY)/R0
    z = 0.07*(1 - d*d) - 0.20*pond_k(x, y)
    return z


# ------------------------------------------------------------------ island: grass top, dirt path, pond bank, chunky earth underside
g = B("Island", shade="organic")
NS, NR = 64, 22
sec = [i*math.tau/NS for i in range(NS)]
def top_color(x, y, t):
    n1 = noise.fractal(Vector((x*0.35, y*0.35, 1)), 0.5, 2.0, 3)
    c = mix(OL_M, OL_L, sm(0.5 + n1*1.3))
    c = mix(OL_D, c, sm(1.25 - t*1.0)) if t > 0.8 else c                          # darker rim
    k = 1 - sm((abs(x - path_x(y)) - 0.25)/0.4) if y < -0.9 else 0
    c = mix(c, mix(DIRT, DIRT_D, sm(0.5 + noise.noise(Vector((x*1.3, y*1.3, 4))))), k)
    pk = pond_k(x, y); c = mix(c, mix(SAND, SAND_L, sm(0.5 + n1)), sm(pk*1.8)*0.95)
    return c
rings = []
for k in range(NR+1):
    t = k/NR; ring = []
    for a in sec:
        r = island_r(a)*t
        x, y = CX + r*math.cos(a), CY + r*math.sin(a)
        z = ground_z(x, y) - (0.03*sm((t-0.92)/0.08))
        ring.append((g.bm.verts.new((x, y, z)), top_color(x, y, t)))
    rings.append(ring)
under = [(1.0, -0.02, 0.0), (1.0, -0.22, 0.0), (0.93, -0.7, 0.2), (0.74, -1.3, 0.45), (0.45, -1.9, 0.75), (0.15, -2.35, 1.0)]
for (t, z, tt) in under:
    ring = []
    for a in sec:
        r = island_r(a)*t*(1 + 0.05*math.sin(a*7+z))
        ring.append((g.bm.verts.new((CX + r*math.cos(a), CY + r*math.sin(a), z)), mix(EARTH_L, EARTH_D, sm(tt*1.1 + 0.15*noise.noise(Vector((a*2, z, 0)))))))
    rings.append(ring)
tip = g.bm.verts.new((CX, CY, -2.6))
for r0, r1 in zip(rings[:-1], rings[1:]):
    for i in range(NS):
        j = (i+1) % NS
        f = g.bm.faces.new((r0[i][0], r0[j][0], r1[j][0], r1[i][0])); f.material_index = g._mi(VC())
        for lp, q in zip(f.loops, (r0[i], r0[j], r1[j], r1[i])): lp[g.col] = (*tolin(q[1]), 1)
last = rings[-1]
for i in range(NS):
    j = (i+1) % NS
    f = g.bm.faces.new((last[i][0], last[j][0], tip)); f.material_index = g._mi(VC())
    for lp, c in zip(f.loops, (last[i][1], last[j][1], EARTH_D)): lp[g.col] = (*tolin(c), 1)
# centre fan
f = g.bm.faces.new([rings[0][0][0]]*0 or []) if False else None
g.finish()

# pond water + lily pads
wt = B("Pond", shade="organic")
NW = 40
cen = wt.bm.verts.new((POND[0], POND[1], -0.075))
ringw = []
for i in range(NW):
    a = i*math.tau/NW; r = POND[2]*0.92*(1 + 0.05*math.sin(a*3))
    ringw.append(wt.bm.verts.new((POND[0] + r*math.cos(a), POND[1] + r*math.sin(a), -0.075)))
for i in range(NW):
    j = (i+1) % NW
    f = wt.bm.faces.new((cen, ringw[i], ringw[j])); f.material_index = wt._mi(VC())
    for lp, c in zip(f.loops, (WATER_D, WATER_S, WATER_S)): lp[wt.col] = (*tolin(c), 1)
for k, (dx, dy, rr) in enumerate([(-0.3, 0.2, 0.17), (0.25, -0.25, 0.14), (0.35, 0.35, 0.12)]):
    wt.cyl(rr, rr, 0.012, (POND[0]+dx, POND[1]+dy, -0.07), M("Lily", "7FB84A"), seg=14)
    wt.blob(0.035, (POND[0]+dx+0.03, POND[1]+dy, -0.05), M("LilyF", PINK), sub=2, squash=(1, 1, 0.8))
wt.finish()


# ------------------------------------------------------------------ table + pergola (chunky, cream, rounded)
TX, TY, TW, TD, TH = 0.0, 0.70, 1.20, 0.80, 0.74
t = B("Table")
mt1, mt2 = M("Table_Top", WOOD), M("Table_Leg", WOOD2)
t.box((TW, TD, 0.07), (TX, TY, TH-0.035), mt1, bevel=0.03)
for sx in (-1, 1):
    for sy in (-1, 1):
        t.box((0.1, 0.1, TH-0.07), (TX+sx*(TW/2-0.12), TY+sy*(TD/2-0.12), 0.0), mt2, bottom=True, bevel=0.025)
t.box((TW-0.3, 0.07, 0.08), (TX, TY-TD/2+0.12, TH-0.115), mt2, bevel=0.02)
t.box((TW-0.3, 0.07, 0.08), (TX, TY+TD/2-0.12, TH-0.115), mt2, bevel=0.02)
t.finish()
td = B("TableDeco", shade="organic")
mc = M("Ceramic", CREAM); mteal = M("Ceramic_Teal", "7FB5AE")
vx, vy = TX-0.50, TY+0.28
td.cyl(0.055, 0.045, 0.13, (vx, vy, TH), mteal, seg=18, bevel=0.01)
for i, (dx, dy, h, c) in enumerate([(0, 0, 0.3, PINK), (0.03, -0.02, 0.24, YEL), (-0.03, 0.02, 0.27, LAV), (0.01, 0.04, 0.22, CORAL)]):
    td.cyl(0.005, 0.005, h, (vx+dx, vy+dy, TH+0.12), M("Stem", "6A9A3E"), seg=6)
    td.blob(0.035, (vx+dx, vy+dy, TH+0.12+h), M("F_"+c, c), sub=2, squash=(1, 1, 0.8))
cx, cy = TX+0.50, TY+0.28
td.cyl(0.07, 0.06, 0.016, (cx, cy, TH), mc, seg=20)
td.cyl(0.04, 0.034, 0.07, (cx, cy, TH+0.016), mc, seg=20, bevel=0.008)
td.finish()

pg = B("Pergola")
mp, mb = M("Pergola_Post", CREAM), M("Pergola_Beam", CREAM2)
PX, PY0, PY1, PH = 1.5, -0.65, 1.95, 2.15
for sx in (-1, 1):
    for py in (PY0, PY1):
        pg.box((0.24, 0.24, PH), (sx*PX, py, 0.0), mp, bottom=True, bevel=0.07)
for py in (PY0, PY1):
    pg.box((2*PX+0.6, 0.2, 0.22), (0, py, PH+0.08), mp, bevel=0.07)
for i in range(6):
    x = -PX + 2*PX*i/5
    arch = 0.12*(1-(x/PX)**2)                             # slightly arched roof
    pg.box((0.2, PY1-PY0+0.5, 0.13), (x, (PY0+PY1)/2, PH+0.26+arch), mb, bevel=0.05)
pg.finish()
pl = B("PergolaLeaves", shade="organic")
for (px, py, dr, rl) in [(-PX, PY0, (0.5, 0.2, 0.2), 0.3), (PX, PY1, (-0.5, 0.2, 0.2), -0.3), (PX-0.2, PY0, (-0.6, 0.3, 0.1), 0.4),
                         (-PX+0.1, PY1, (0.6, -0.3, 0.1), -0.4), (0.3, PY0, (0.2, 0.5, 0.2), 0.2)]:
    for k in range(3):
        pl.strip_leaf((px, py, PH+0.26), (dr[0]+k*0.2-0.2, dr[1]+0.15*k, dr[2]), 0.5, 0.14, 0.5, hx("5C9A2E"), hx("C6EA4A"), roll=rl+0.25*k)
pl.finish()


# ------------------------------------------------------------------ egg trees (chunky, sunlit gradient), bushes
tr = B("Trees", shade="organic")
rnd = random.Random(7)
mtr = M("Trunk", TRUNK)
spots = [(-3.0, 3.6, 2.9, 0.75), (-1.4, 4.7, 3.4, 0.82), (0.5, 4.9, 2.8, 0.7), (2.2, 4.3, 3.2, 0.78), (3.7, 2.6, 2.5, 0.68),
         (-3.9, 1.6, 2.6, 0.7), (-3.7, -0.8, 2.1, 0.62)]
for k, (x, y, h, r) in enumerate(spots):
    z0 = ground_z(x, y)
    tr.cyl(0.14, 0.2, 0.7, (x, y, z0-0.05), mtr, seg=12)
    pal = (TEAL_D, TEAL_L) if k % 2 == 0 else (OL_D, OL_L)
    tr.blob(r, (x, y, z0+0.5+h/2), None, sub=4, squash=(0.9, 0.9, h/(2*r)), grad=pal, jit=0.015, seed=k)
for k, (x, y, r) in enumerate([(-2.6, 2.4, 0.5), (-2.9, 0.4, 0.42), (1.2, 3.8, 0.45), (2.9, 0.4, 0.38), (-1.0, 3.6, 0.36)]):
    tr.blob(r, (x, y, ground_z(x, y)+r*0.62), None, sub=3, squash=(1.15, 1.0, 0.8), grad=(OL_D, OL_L), jit=0.02, seed=40+k)
# one blossom tree
tr.cyl(0.1, 0.15, 0.55, (3.3, -0.2, ground_z(3.3, -0.2)-0.04), mtr, seg=10)
tr.blob(0.75, (3.3, -0.2, ground_z(3.3, -0.2)+1.3), None, sub=4, squash=(1, 1, 0.95), grad=(hx("E798B0"), hx("FFE0E6")), jit=0.02, seed=77)
tr.finish()


# ------------------------------------------------------------------ fence (stubby rounded posts)
fe = B("Fence")
mf, mfr = M("Fence_Post", "FFF3DE"), M("Fence_Rail", "F4E1BC")
def fence_arc(a0, a1, rad, n):
    for i in range(n+1):
        a = math.radians(a0 + (a1-a0)*i/n)
        x, y = CX + rad*math.cos(a)*island_r(a)/R0, CY + rad*math.sin(a)*island_r(a)/R0
        fe.box((0.13, 0.1, 0.5), (x, y, ground_z(x, y)-0.02), mf, rot=(0, 0, math.degrees(a)+90), bottom=True, bevel=0.05)
        fe.blob(0.07, (x, y, ground_z(x, y)+0.5), mf, sub=2, squash=(1, 0.8, 0.8))
        if i < n:
            a2 = math.radians(a0 + (a1-a0)*(i+0.5)/n)
            x2, y2 = CX + rad*math.cos(a2)*island_r(a2)/R0, CY + rad*math.sin(a2)*island_r(a2)/R0
            fe.box((0.45, 0.06, 0.07), (x2, y2, ground_z(x2, y2)+0.3), mfr, rot=(0, 0, math.degrees(a2)+90), bevel=0.025)
fence_arc(205, 245, 3.75, 7)       # front left
fence_arc(295, 335, 3.75, 7)       # front right
fence_arc(-10, 20, 3.75, 5)
fence_arc(160, 192, 3.75, 5)
fe.finish()


# ------------------------------------------------------------------ props
pr = B("Props")
green, greend = M("Pump_Green", "3F8A5C"), M("Pump_Dark", "2F6E4A")
pxp, pyp = -2.9, 1.7
pz = ground_z(pxp, pyp)
pr.cyl(0.22, 0.24, 0.04, (pxp, pyp, pz), M("Pump_Base", "DCCFE8"), seg=18, bevel=0.01)
pr.cyl(0.07, 0.09, 0.8, (pxp, pyp, pz+0.04), green, seg=14, bevel=0.015)
pr.cyl(0.09, 0.07, 0.1, (pxp, pyp, pz+0.84), green, seg=14, bevel=0.02)
pr.box((0.5, 0.06, 0.06), (pxp-0.12, pyp, pz+0.95), greend, rot=(0, 18, 0), bevel=0.02)
pr.cyl(0.035, 0.03, 0.26, (pxp+0.18, pyp, pz+0.6), green, seg=10, rot=(0, -70, 0), bevel=0.01)
pr.blob(0.1, (pxp+0.3, pyp, pz+0.02), M("Puddle", "9ED3DC"), sub=2, squash=(1.8, 1.2, 0.12))
# bucket
bk = (-2.5, 1.0); bz = ground_z(*bk)
pr.cyl(0.19, 0.16, 0.26, (bk[0], bk[1], bz), M("Bucket", LAV), seg=18, bevel=0.012)
pr.cyl(0.19, 0.19, 0.02, (bk[0], bk[1], bz+0.26), M("Bucket_Rim", "9884BC"), seg=18, cap=False)
# tool chest
ck = (-1.8, 2.7); cz = ground_z(*ck)
pr.box((0.65, 0.34, 0.3), (ck[0], ck[1], cz), M("Chest", "C96A5A"), bottom=True, bevel=0.04, rot=(0, 0, 8))
pr.box((0.67, 0.36, 0.1), (ck[0], ck[1], cz+0.3), M("Chest_Lid", "B95A4C"), bottom=True, bevel=0.04, rot=(0, 0, 8))
pr.box((0.06, 0.03, 0.1), (ck[0]+0.02, ck[1]-0.19, cz+0.22), M("Latch", "E8D6A8"), bevel=0.01, rot=(0, 0, 8))
# bird house on a pole
bh = (2.6, 1.0); hz = ground_z(*bh)
pr.cyl(0.04, 0.05, 1.5, (bh[0], bh[1], hz), M("Pole", TRUNK), seg=10)
pr.box((0.3, 0.3, 0.3), (bh[0], bh[1], hz+1.5), M("BHouse", CORAL), bottom=True, bevel=0.03)
pr.cyl(0.0, 0.26, 0.2, (bh[0], bh[1], hz+1.8), M("BRoof", "D4604F"), seg=4, rot=(0, 0, 45), bevel=0.01)
pr.cyl(0.05, 0.05, 0.02, (bh[0], bh[1]-0.155, hz+1.66), M("Hole", "5A3A30"), seg=12, rot=(90, 0, 0))
# basket of carrots + tomato stakes + carrot bed
bs = (1.9, -0.5); bz2 = ground_z(*bs)
for i in range(5):
    pr.cyl(0.17-0.005*i, 0.17-0.005*i, 0.04, (bs[0], bs[1], bz2+0.03+i*0.045), M("Basket", "E9A99B"), seg=18, bevel=0.012)
for k in range(5):
    a = k*1.2
    pr.cyl(0.0, 0.045, 0.2, (bs[0]+0.06*math.cos(a), bs[1]+0.06*math.sin(a), bz2+0.15), M("Carrot", "F08A32"), seg=10, rot=(15*math.cos(a), 15*math.sin(a), 0))
pr.cyl(0.015, 0.015, 0.25, (bs[0]-0.17, bs[1], bz2+0.28), M("BasketH", "E9A99B"), seg=8)
for k in range(2):
    tp = (2.3 + 0.35*k, 0.2)
    zz = ground_z(*tp)
    pr.cyl(0.015, 0.015, 0.9, (tp[0], tp[1], zz), M("Stake", WOOD2), seg=8)
    for j in range(4):
        pr.blob(0.045, (tp[0]+0.05*(-1)**j, tp[1]+0.03, zz+0.3+j*0.17), M("Tomato", "E5463F"), sub=2)
for k in range(8):
    cp = (1.2 + 0.2*(k % 4), -0.3 - 0.18*(k//4))
    zz = ground_z(*cp)
    pr.cyl(0.0, 0.035, 0.12, (cp[0], cp[1], zz-0.02), M("Carrot", "F08A32"), seg=8)
pr.finish()
vg = B("VeggieLeaves", shade="organic")
for k in range(8):
    cp = (1.2 + 0.2*(k % 4), -0.3 - 0.18*(k//4)); zz = ground_z(*cp)
    for j in range(4):
        a = j*1.6 + k
        vg.strip_leaf((cp[0], cp[1], zz+0.06), (math.cos(a)*0.4, math.sin(a)*0.4, 1), 0.22, 0.05, 0.4, hx("4E8F2E"), hx("B6E04A"))
for k in range(2):
    tp = (2.3 + 0.35*k, 0.2); zz = ground_z(*tp)
    for j in range(5):
        a = j*1.3
        vg.strip_leaf((tp[0], tp[1], zz+0.25+0.14*j), (math.cos(a)*0.5, math.sin(a)*0.5, 0.4), 0.2, 0.06, 0.3, hx("4E8F2E"), hx("B6E04A"))
vg.finish()

# chubby birds
bd = B("Birds", shade="organic")
mby, mbo, mbk = M("Chick", YEL), M("Beak", "F08A32"), M("Eye", "3A2A24")
def bird(x, y, z, rot=0.0, s=1.0):
    bd.blob(0.11*s, (x, y, z+0.11*s), mby, sub=3, squash=(1, 1, 0.92), rot=(0, 0, rot))
    ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    for sgn in (-1, 1):
        bd.blob(0.016*s, (x + (-sgn*0.045*ca - 0.095*sa)*s, y + (-sgn*0.045*sa + 0.095*ca)*s*-1*-1, z+0.14*s), mbk, sub=1)
    bd.cyl(0.0, 0.022*s, 0.035*s, (x - 0.115*sa*s*-1, y + 0.115*ca*s*-1, z+0.11*s), mbo, seg=8, rot=(90, 0, rot+180))
bird(-0.4, 0.4, PH+0.40, rot=200, s=1.0)
bird(1.6, 2.5, PH+0.08+0.12, rot=160, s=0.85) if False else None
bird(0.4, PY1, PH+0.17, rot=180, s=0.9)
bird(-2.85, 2.2, ground_z(-2.85, 2.2)+0.5, rot=210, s=0.8)
bird(2.65, 1.0, ground_z(2.65, 1.0)+1.95, rot=250, s=0.8)
bd.finish()

# flowers + chunky grass tufts
fl = B("Flowers", shade="organic")
rnd = random.Random(55)
fcols = [PINK, "F7CDD2", "F8E08A", LAV, "F2A48E", "FFF8F0", "9EC3E6"]
fm = {c: M("Flower_"+c, c) for c in fcols}
for (cx_, cy_, rad) in [(-1.2, -1.5, 0.55), (1.3, -1.8, 0.5), (-3.0, -0.1, 0.45), (3.0, 1.6, 0.45), (0.9, 3.5, 0.5), (-0.9, 3.9, 0.4)]:
    for i in range(14):
        a = rnd.uniform(0, math.tau); rr = rad*math.sqrt(rnd.random())
        fx, fy = cx_ + math.cos(a)*rr, cy_ + math.sin(a)*rr
        if on_path(fx, fy, 0.1) or pond_k(fx, fy) > 0.1: continue
        zz = ground_z(fx, fy); fh = rnd.uniform(0.15, 0.32)
        fl.cyl(0.008, 0.008, fh, (fx, fy, zz), M("FStem", "5E9338"), seg=6)
        fl.blob(rnd.uniform(0.045, 0.07), (fx, fy, zz+fh), fm[rnd.choice(fcols)], sub=2, squash=(1, 1, 0.75))
fl.finish()
gt = B("GrassTufts", shade="organic")
rnd = random.Random(60)
for i in range(110):
    a = rnd.uniform(0, math.tau); rr = R0*0.9*math.sqrt(rnd.random())
    x, y = CX + rr*math.cos(a), CY + rr*math.sin(a)
    if on_path(x, y, 0.1) or pond_k(x, y) > 0.05 or (abs(x) < 1.0 and -0.1 < y < 1.5): continue
    zz = ground_z(x, y)
    for j in range(4):
        b = rnd.uniform(0, math.tau)
        gt.strip_leaf((x, y, zz), (math.cos(b)*0.35, math.sin(b)*0.35, 1), rnd.uniform(0.14, 0.26), 0.035, 0.35, hx("4F8A2E"), hx("C4E256"), segs=4)
gt.finish()

# stepping stones (cream, rounded) along the dirt path, and rocks by the pond
st = B("Stones", shade="organic")
for k, y in enumerate([-1.3, -1.8, -2.35, -2.9]):
    x = path_x(y) + (0.12 if k % 2 else -0.12)
    st.cyl(0.26, 0.24, 0.07, (x, y, ground_z(x, y)), M("Stepstone", "EAD9B4"), seg=14, bevel=0.025)
rnd = random.Random(14)
for k in range(9):
    a = rnd.uniform(0, math.tau); rr = POND[2]*rnd.uniform(1.0, 1.15)
    x, y = POND[0] + rr*math.cos(a), POND[1] + rr*math.sin(a)
    r = rnd.uniform(0.09, 0.2)
    st.blob(r, (x, y, ground_z(x, y)+r*0.3), M("Rock", ["9A958C", "AEA698", "8A8680"][k % 3]), sub=3, squash=(1.2, 1, 0.7), jit=0.06, seed=k)
st.finish()


# ------------------------------------------------------------------ chunky clouds
cl = B("Clouds", "Sky", shade="organic")
mcl = M("Cloud", "FFF1DD", emit="FFE9D2", estr=0.35)
rnd = random.Random(70)
for k in range(7):
    cx, cy, cz = rnd.uniform(-14, 14), rnd.uniform(9, 20), rnd.uniform(5.5, 9)
    for j in range(rnd.randint(3, 4)):
        cl.blob(rnd.uniform(0.9, 1.5), (cx + j*1.3 - 2, cy + rnd.uniform(-0.3, 0.3), cz + (0.35 if j == 1 else 0)), mcl, sub=3, squash=(1.2, 1, 0.85))
cl.finish()


# ------------------------------------------------------------------ light, sky, cameras
sun = bpy.data.lights.new("Sun_Warm", 'SUN'); sun.energy = 3.6; sun.color = tolin(hx("FFD7A0")); sun.angle = math.radians(9)
so = bpy.data.objects.new("Sun_Warm", sun); coll("Lighting").objects.link(so)
so.rotation_euler = (-SUN).to_track_quat('-Z', 'Y').to_euler()

wd = bpy.data.worlds.new("Sky"); sc.world = wd; wd.use_nodes = True
nt = wd.node_tree; nt.nodes.clear()
tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); mp_ = nt.nodes.new("ShaderNodeMapRange")
cr = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(tc.outputs["Object"], sep.inputs[0]); nt.links.new(sep.outputs["Z"], mp_.inputs["Value"])
mp_.inputs["From Min"].default_value = -0.1; mp_.inputs["From Max"].default_value = 0.6; mp_.clamp = True
nt.links.new(mp_.outputs["Result"], cr.inputs["Fac"]); nt.links.new(cr.outputs["Color"], bg.inputs["Color"])
nt.links.new(bg.outputs["Background"], out.inputs["Surface"])
cr.color_ramp.elements[0].position = 0.0; cr.color_ramp.elements[0].color = (*tolin(hx("FFD6AE")), 1)
cr.color_ramp.elements[1].position = 1.0; cr.color_ramp.elements[1].color = (*tolin(hx("93AEE6")), 1)
bg.inputs["Strength"].default_value = 1.5

def cam(name, loc, look, lens=24):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.05; cd.clip_end = 200
    co = bpy.data.objects.new(name, cd); coll("Cameras").objects.link(co); co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return co
cam("Cam_Seat", (0.0, -0.35, 1.15), (0.0, 2.6, 1.15), lens=18)
cam("Cam_Overview", (-6.5, -7.0, 4.2), (0.0, 1.3, 0.9), lens=30)
cam("Cam_Front", (0.0, -9.0, 2.2), (0.0, 1.0, 1.1), lens=38)
sc.camera = bpy.data.objects["Cam_Seat"]
eye = bpy.data.objects.new("Player_Eye_Ref", None); eye.empty_display_type = 'ARROWS'; eye.empty_display_size = 0.15
eye.location = (0, -0.35, 1.15); eye.rotation_euler = (math.radians(90), 0, 0); coll("Lighting").objects.link(eye)

r = sc.render
r.engine = 'BLENDER_EEVEE_NEXT'; r.resolution_x = 1280; r.resolution_y = 720
for vt in ("Filmic", "Standard"):
    try: sc.view_settings.view_transform = vt; break
    except TypeError: pass
try: sc.eevee.use_raytracing = True
except Exception: pass

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
tris = 0
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
print("GARDEN saved", OUT, "objects", len([o for o in bpy.data.objects if o.type == 'MESH']), "tris", tris, "materials", len(bpy.data.materials))
