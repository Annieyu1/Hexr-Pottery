"""Hub music room as a paper-craft pop-up: a clean rebuild of the Tripo "ornate stage set" (kept as reference).
Layered cut-out cards, accordion-pleated curtains and pilasters, scroll + shell crests, mullioned windows and a
lace-punched floor edge; flat paper colours, no textures. Builds into the OPEN Blender scene (collection
MusicRoom_Paper, rebuilt on every run) and saves art/blender/HexR_Hub_MusicRoom_Paper.blend.

Run inside Blender (Text editor or MCP):  exec(open(".../art/scripts/build_hub_musicroom_paper.py").read())

Metres, Z up, viewer at -Y. Same footprint as the reference (0.98 x 0.36 m floor). Fold pivots for PopUpHinge:
  Hinge_Back   (0, 0.13, floor top), back wall folds about X
  Hinge_SideR  (0.47, 0.15, floor top), right side panel folds about Z
Cards are 2D curves (holes = inner splines), converted to meshes at the end.
"""
import bpy, bmesh, math, os

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    "/Users/xuzihan/Documents/Lab/Vibration/HexR-Developer-Tutorial-Meta-OVR/art/scripts"
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "art", "blender", "HexR_Hub_MusicRoom_Paper.blend")
COL = "MusicRoom_Paper"
T, BEV = 0.003, 0.0006          # card thickness, soft paper edge
FLOOR_T = 0.012
WALL_Y = 0.13


# ------------------------------------------------------------------ colours (same helpers as build_hub_book.py)
def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def hx(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16)/255 for i in (0, 2, 4))

_mats = {}
def M(name, h, rough=0.9, emit=0.0):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = tuple(lin(x) for x in hx(h))
    b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.15
    if emit:
        b.inputs["Emission Color"].default_value = (*c, 1); b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*c, 1)
    _mats[name] = m
    return m

WALL = M("Paper_Wall", "F4E6CF")
TRIM = M("Paper_Trim", "E9CFA4")
GOLD = M("Paper_Gold", "D6AE68")
CREAM = M("Paper_Cream", "FFF8EA")
SKY = M("Paper_Sky", "CFE4F0", emit=0.35)
ROSE = M("Paper_Curtain", "E58A84")
ROSE_D = M("Paper_CurtainDark", "C9665F")
FLOOR = M("Paper_Floor", "EFC2C1")
FLOOR_D = M("Paper_FloorEdge", "DDA3A3")
RUG = M("Paper_Rug", "F2C94C")
TERRA = M("Paper_Terracotta", "D9774F")
LEAF = M("Paper_Leaf", "5E9A5A")
LEAF_L = M("Paper_LeafLight", "8DB07A")
PINK = M("Paper_Pink", "F4C6C6")
CORAL = M("Paper_Coral", "EE8B6E")
FLAME = M("Paper_Flame", "F2A623", emit=1.5)


# ------------------------------------------------------------------ scene setup
# PART = "all" rebuilds room + decor; PART = "decor" rebuilds only MusicRoom_Decor and leaves the room, its hand
# edits and anything else (e.g. the user's Piano collection) untouched. Only objects tagged by this script are deleted.
PART = globals().get("PART", "all")
TAG = "paper_build"
DECOR_COL = "MusicRoom_Decor"
sc = bpy.context.scene
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')


def fresh_collection(name):
    """Get (or make) a collection and delete only this script's objects in it; others stay where they are."""
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name); sc.collection.children.link(c)
    doomed = [o for o in c.all_objects if o.get(TAG)]
    keep = {o: o.matrix_world.copy() for o in bpy.data.objects if o.parent in doomed and not o.get(TAG)}
    for o in doomed: bpy.data.objects.remove(o, do_unlink=True)
    for o, mw in keep.items(): o.matrix_world = mw
    return c


if PART == "all":
    col = fresh_collection(COL)
    # the Tripo model stays as a hidden reference
    ref = bpy.data.objects.get("ornate stage set 3d model")
    if ref:
        rc = bpy.data.collections.get("Reference") or bpy.data.collections.new("Reference")
        if rc.name not in sc.collection.children: sc.collection.children.link(rc)
        for c in list(ref.users_collection):
            if c != rc: c.objects.unlink(ref)
        if ref.name not in rc.objects: rc.objects.link(ref)
        ref.hide_set(True); ref.hide_render = True

curves, made = [], []


def empty(name, parent, loc, rot=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); e.empty_display_size = 0.03
    col.objects.link(e); e.parent = parent; e.location = loc; e.rotation_euler = rot
    e[TAG] = True; made.append(e)
    return e


def card(name, parent, outer, holes=(), front=0.0, thick=T, mat=WALL, flat=False, z=0.0):
    """2D cut-out card. Upright cards live in the parent's XZ plane (front = towards -Y); flat ones lie in XY.
    Only structural cards get the soft bevel; small gold / cream ornaments stay crisp (and cheap)."""
    bev = BEV if thick >= T and mat not in (GOLD, CREAM) else 0.0
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '2D'; cu.fill_mode = 'BOTH'
    cu.extrude = max(1e-4, thick/2 - bev); cu.bevel_depth = bev; cu.bevel_resolution = 0
    for poly in (outer, *holes):
        sp = cu.splines.new('POLY'); sp.points.add(len(poly) - 1)
        for p, (u, v) in zip(sp.points, poly): p.co = (u, v, 0, 1)
        sp.use_cyclic_u = True
    cu.materials.append(mat)
    ob = bpy.data.objects.new(name, cu); col.objects.link(ob); ob.parent = parent
    ob[TAG] = True; made.append(ob)
    if flat: ob.location = (0, 0, z)
    else: ob.rotation_euler = (math.pi/2, 0, 0); ob.location = (0, -front, 0)
    curves.append(ob)
    return ob


def mesh_obj(name, parent, bm, mat):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); col.objects.link(ob); ob.parent = parent
    ob[TAG] = True; made.append(ob)
    return ob


# ------------------------------------------------------------------ 2D shapes (u across, v up)
def arc(cx, hw, vs, r, n, a0=0.0, a1=math.pi):
    return [(cx + hw*math.cos(a), vs + r*math.sin(a)) for a in (a0 + (a1-a0)*i/n for i in range(n + 1))]


def arch(cx, w, v0, vs, rise, n=18, grow=0.0):
    hw = w/2 + grow
    return [(cx - hw, v0), (cx + hw, v0)] + arc(cx, hw, vs, rise + grow, n)


def band(cx, w, vs, rise, b, n=18):
    return arc(cx, w/2, vs, rise, n) + arc(cx, w/2 - b, vs, rise - b, n)[::-1]


def rect(u0, v0, u1, v1): return [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]


def circle(cu, cv, r, n=12): return [(cu + r*math.cos(2*math.pi*i/n), cv + r*math.sin(2*math.pi*i/n)) for i in range(n)]


def rrect(cx, cy, w, h, r, n=4):
    pts = []
    for (sx, sy, a0) in ((1, -1, -math.pi/2), (1, 1, 0), (-1, 1, math.pi/2), (-1, -1, math.pi)):
        ccx, ccy = cx + sx*(w/2 - r), cy + sy*(h/2 - r)
        pts += [(ccx + r*math.cos(a0 + math.pi/2*i/n), ccy + r*math.sin(a0 + math.pi/2*i/n)) for i in range(n + 1)]
    return pts


def panes(cx, w, v0, vs, rise, rows, m=0.008, n=8):
    """Glass openings in a mullion card: two columns x rows below the springing line, two quarter-arches above."""
    hw, holes = w/2, []
    for side in (-1, 1):
        ui, uo = cx + side*m/2, cx + side*(hw - m)
        for k in range(rows):
            a = v0 + (vs - v0)*k/rows + (m if k == 0 else m/2)
            b = v0 + (vs - v0)*(k + 1)/rows - m/2
            holes.append(rect(min(ui, uo), a, max(ui, uo), b))
        top = [(cx + side*(hw - m)*math.cos(a), vs + m/2 + (rise - m)*math.sin(a))
               for a in (math.pi/2*i/n for i in range(n + 1))]
        top = [p for p in top if (p[0] - ui)*side > 0.002]
        holes.append([(ui, vs + m/2)] + top + [(ui, top[-1][1])])
    return holes


def volute(cu, cv, R, flip=1, rot=0.0, turns=1.25, width=0.28, n=20):
    out, inn = [], []
    for i in range(n + 1):
        t = i/n; a = rot + flip*2*math.pi*turns*t; r = R*(1 - 0.72*t); wv = R*width*(1 - 0.55*t)
        out.append((cu + r*math.cos(a), cv + r*math.sin(a)))
        inn.append((cu + (r - wv)*math.cos(a), cv + (r - wv)*math.sin(a)))
    return out + inn[::-1]


def fan(cu, cv, R, bumps, n=36):
    return [(cu + r*math.cos(a), cv + r*math.sin(a))
            for a, r in ((math.pi*i/n, R*(0.84 + 0.16*abs(math.sin(bumps*math.pi*i/n)))) for i in range(n + 1))]


def finial(cu, v0, s=1.0, flip=False):
    half = [(0.011, 0), (0.011, 0.007), (0.006, 0.009), (0.006, 0.013)]
    half += [(0.011*math.cos(a), 0.024 + 0.011*math.sin(a)) for a in (math.radians(d) for d in range(-60, 61, 20))]
    half += [(0.003, 0.038), (0.008, 0.046), (0.009, 0.052), (0.006, 0.061), (0.0, 0.073)]
    pts = half + [(-x, y) for x, y in reversed(half[:-1])]
    k = -1 if flip else 1
    return [(cu + x*s, v0 + k*y*s) for x, y in pts]


def ellipse(cx, cy, rx, ry, ang=0.0, n=28):
    ca, sa = math.cos(ang), math.sin(ang)
    return [(cx + rx*math.cos(t)*ca - ry*math.sin(t)*sa, cy + rx*math.cos(t)*sa + ry*math.sin(t)*ca)
            for t in (2*math.pi*i/n for i in range(n))]


def flower(R, petals=5, n=30, rot=0.0):
    return [(r*math.cos(a), r*math.sin(a))
            for a, r in ((rot + 2*math.pi*i/n, R*(0.62 + 0.38*abs(math.cos(petals*math.pi*i/n)))) for i in range(n))]


def teardrop(cu, v0, w, h, n=10):
    """Flame: round bottom, pointed top."""
    right = [(cu + w/2*2.6*math.sqrt(i/n)*(1 - i/n), v0 + h*i/n) for i in range(n + 1)]
    return right + [(2*cu - u, v) for u, v in reversed(right[1:-1])]


def pelmet(cx, w, top, h, swags=3, n=10):
    pts = [(cx - w/2, top), (cx + w/2, top)]
    for k in range(swags):
        for i in range(n + 1):
            if k and not i: continue
            t = i/n
            pts.append((cx + w/2 - (k + t)*w/swags, top - h*0.4 - h*0.6*math.sin(math.pi*t)))
    return pts


# ------------------------------------------------------------------ folded (origami) parts
def pleat_column(name, parent, u, v0, v1, w, d, mat=TRIM):
    """Upright strip folded into an 'M' (two ridges) standing off the wall: a paper pilaster."""
    prof = [(-w/2, 0), (-w/4, -d), (0, -d*0.45), (w/4, -d), (w/2, 0)]
    bm = bmesh.new()
    lo = [bm.verts.new((u + x, y, v0)) for x, y in prof]; hi = [bm.verts.new((u + x, y, v1)) for x, y in prof]
    for i in range(len(prof) - 1): bm.faces.new((lo[i], lo[i+1], hi[i+1], hi[i]))
    bm.faces.new(hi); bm.faces.new(lo[::-1])
    return mesh_obj(name, parent, bm, mat)


def pleat_rail(name, parent, u0, u1, v, h, d, mat=TRIM):
    """Horizontal folded moulding: a strip creased into a 'W' running along the wall."""
    prof = [(0, -h/2), (-d, -h/4), (-d*0.45, 0), (-d, h/4), (0, h/2)]
    bm = bmesh.new()
    a = [bm.verts.new((u0, y, v + z)) for y, z in prof]; b = [bm.verts.new((u1, y, v + z)) for y, z in prof]
    for i in range(len(prof) - 1): bm.faces.new((a[i], a[i+1], b[i+1], b[i]))
    bm.faces.new(a); bm.faces.new(b[::-1])
    return mesh_obj(name, parent, bm, mat)


def lathe(name, parent, loc, prof, sides, mat):
    """Faceted turned shape (pot, vase, candle): prof = [(radius, z)] bottom to top, flat-shaded facets."""
    bm = bmesh.new()
    rings = [[bm.verts.new((loc[0] + r*math.cos(2*math.pi*k/sides + math.pi/sides),
                            loc[1] + r*math.sin(2*math.pi*k/sides + math.pi/sides), loc[2] + z)) for k in range(sides)]
             for r, z in prof]
    for a, b in zip(rings, rings[1:]):
        for k in range(sides): bm.faces.new((a[k], a[(k+1) % sides], b[(k+1) % sides], b[k]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    return mesh_obj(name, parent, bm, mat)


def leaf(name, parent, base, length, width, yaw, pitch, mat, fold=0.006, n=6):
    """Leaf creased along its midrib (a shallow V), rising from base at pitch degrees, turned by yaw."""
    bm = bmesh.new(); mid, left, right = [], [], []
    for i in range(n + 1):
        t = i/n; w = width*math.sin(math.pi*min(1.0, t*1.15))*(1 - 0.15*t)
        mid.append(bm.verts.new((0, t*length, 0)))
        if 0 < i < n:
            left.append(bm.verts.new((-w, t*length, -fold*math.sin(math.pi*t))))
            right.append(bm.verts.new((w, t*length, -fold*math.sin(math.pi*t))))
    for side in (left, right):
        bm.faces.new((mid[0], mid[1], side[0]))
        for i in range(len(side) - 1): bm.faces.new((mid[i+1], mid[i+2], side[i+1], side[i]))
        bm.faces.new((mid[-2], mid[-1], side[-1]))
    ob = mesh_obj(name, parent, bm, mat)
    ob.location = base; ob.rotation_euler = (math.radians(pitch), 0, math.radians(yaw))   # tilt up, then turn
    sol = ob.modifiers.new("Paper", 'SOLIDIFY'); sol.thickness = 0.0012; sol.offset = 0
    return ob


def curtain(name, parent, u_out, inward, width, v_top, v_tie, v_bot, front, pleats=6, amp=0.007):
    """Accordion-pleated curtain hanging from the rod, gathered towards its outer edge at the tie-back."""
    def f(v):        # width fraction along the drop
        if v >= v_tie: t = (v - v_tie)/(v_top - v_tie); return 0.36 + 0.64*(0.5 - 0.5*math.cos(math.pi*t))
        t = (v_tie - v)/(v_tie - v_bot); return 0.36 + 0.46*math.sin(math.pi/2*t)
    vs = [v_top - (v_top - v_tie)*i/6 for i in range(6)] + [v_tie - (v_tie - v_bot)*i/6 for i in range(7)]
    cols = 2*pleats + 1
    bm = bmesh.new(); grid = []
    for v in vs:
        fr = f(v); a = amp*(0.7 + 0.9*(1 - fr))
        grid.append([bm.verts.new((u_out + inward*width*fr*j/(cols - 1), -front - a*(j % 2), v)) for j in range(cols)])
    for r in range(len(vs) - 1):
        for j in range(cols - 1): bm.faces.new((grid[r][j], grid[r][j+1], grid[r+1][j+1], grid[r+1][j]))
    ob = mesh_obj(name, parent, bm, ROSE)
    sol = ob.modifiers.new("Paper", 'SOLIDIFY'); sol.thickness = 0.0015; sol.offset = 0
    # tie-back band across the gathered waist
    fw = width*f(v_tie)
    u0, u1 = sorted((u_out - inward*0.004, u_out + inward*(fw + 0.004)))
    card(name + "_Tie", parent, rect(u0, v_tie - 0.007, u1, v_tie + 0.007), front=front + amp*2.4, mat=GOLD)
    card(name + "_Tassel", parent, finial((u0 + u1)/2 + inward*fw*0.3, v_tie - 0.006, s=0.55, flip=True),
         front=front + amp*2.6, mat=GOLD)
    return ob


# ------------------------------------------------------------------ one arched bay of wall
def bay(name, parent, cx, W, vs, rise, win_w, win_v0, win_vs, win_rise, rows, crest_R, crest=True):
    g = empty(name, parent, (0, 0, 0))
    ring = 0.024
    ring_v0 = max(0.0, win_v0 - 0.02)
    card(name + "_Wall", g, arch(cx, W, 0.0, vs, rise), holes=[arch(cx, win_w, win_v0, win_vs, win_rise)], mat=WALL)
    card(name + "_Rim", g, band(cx, W - 0.004, vs, rise - 0.002, 0.012), front=0.0035, mat=TRIM)
    card(name + "_Frame", g, arch(cx, win_w, ring_v0, win_vs, win_rise, grow=ring),
         holes=[arch(cx, win_w, win_v0, win_vs, win_rise, grow=-0.0005)], front=0.0035, mat=TRIM)
    card(name + "_FrameGold", g, arch(cx, win_w, win_v0, win_vs, win_rise, grow=0.008),
         holes=[arch(cx, win_w, win_v0, win_vs, win_rise, grow=-0.0005)], front=0.0065, mat=GOLD, thick=0.002)
    kv = win_vs + win_rise + ring
    card(name + "_Key", g, [(cx - 0.011, kv - 0.03), (cx + 0.011, kv - 0.03), (cx + 0.017, kv + 0.008), (cx - 0.017, kv + 0.008)],
         front=0.008, mat=GOLD)
    card(name + "_Mullions", g, arch(cx, win_w, win_v0, win_vs, win_rise, grow=0.002),
         holes=panes(cx, win_w, win_v0, win_vs, win_rise, rows), front=-0.0035, thick=0.002, mat=CREAM)
    card(name + "_Sky", g, arch(cx, win_w, win_v0, win_vs, win_rise, grow=0.01), front=-0.007, thick=0.0015, mat=SKY)
    if ring_v0 > 0.0:                                                     # raised window: sill + full skirting
        card(name + "_Sill", g, rect(cx - win_w/2 - ring - 0.008, ring_v0 - 0.008, cx + win_w/2 + ring + 0.008, ring_v0 + 0.004),
             front=0.0075, mat=GOLD)
        card(name + "_Skirt", g, rect(cx - W/2 + 0.002, 0.0, cx + W/2 - 0.002, 0.026), front=0.0035, mat=TRIM)
    else:
        for s in (-1, 1):
            a, b = sorted((cx + s*(W/2 - 0.002), cx + s*(win_w/2 + ring)))
            card(name + "_Skirt%+d" % s, g, rect(a, 0.0, b, 0.026), front=0.0035, mat=TRIM)
    # folded moulding along the springing line, broken by the window frame
    for s in (-1, 1):
        a, b = sorted((cx + s*(W/2 - 0.004), cx + s*(win_w/2 + ring + 0.004)))
        if b - a > 0.012: pleat_rail(name + "_Rail%+d" % s, g, a, b, vs, 0.018, 0.008)
    if crest:
        base = vs + rise - crest_R*0.35
        for s in (-1, 1):
            card(name + "_Scroll%+d" % s, g, volute(cx + s*crest_R*1.12, base + crest_R*0.28, crest_R*0.5, flip=-s,
                                                   rot=math.pi/2 - s*math.pi/2), front=0.0025, mat=GOLD)
        card(name + "_Shell", g, fan(cx, base, crest_R, 4), front=0.0025, mat=GOLD)
        card(name + "_ShellIn", g, fan(cx, base, crest_R*0.62, 3), front=0.005, mat=CREAM, thick=0.002)
        card(name + "_ShellDot", g, circle(cx, base + crest_R*0.12, crest_R*0.16), front=0.0072, mat=GOLD, thick=0.002)
        card(name + "_Finial", g, finial(cx, base + crest_R*0.9, s=crest_R/0.06), front=0.0025, mat=GOLD)
        card(name + "_FinialIn", g, finial(cx, base + crest_R*0.9 + 0.006, s=crest_R/0.06*0.6), front=0.0048, mat=CREAM,
             thick=0.0015)
    return g


def pilaster(name, parent, u, top, w=0.03):
    g = empty(name, parent, (0, 0, 0))
    pleat_column(name + "_Shaft", g, u, 0.026, top, w, 0.012)
    card(name + "_Base", g, rect(u - w/2 - 0.006, 0.0, u + w/2 + 0.006, 0.03), front=0.014, thick=0.004, mat=TRIM)
    card(name + "_Cap", g, rect(u - w/2 - 0.006, top - 0.004, u + w/2 + 0.006, top + 0.01), front=0.014, thick=0.004, mat=TRIM)
    card(name + "_CapTop", g, rect(u - w/2 - 0.011, top + 0.01, u + w/2 + 0.011, top + 0.018), front=0.012, thick=0.004, mat=GOLD)
    card(name + "_Finial", g, finial(u, top + 0.018, s=0.9), front=0.006, mat=GOLD)
    card(name + "_FinialIn", g, finial(u, top + 0.023, s=0.52), front=0.0085, mat=CREAM, thick=0.0015)
    return g


# ------------------------------------------------------------------ build
if PART == "all":
    root = empty("MusicRoom_Paper", None, (0, 0, 0))

    # floor card: pink board, inlaid border, corner scrolls, lace-punched front edge
    fl = empty("Floor", root, (0, 0, 0))
    FW, FD, FCY = 0.98, 0.36, -0.01
    card("Floor_Board", fl, rrect(0, FCY, FW, FD, 0.012), thick=FLOOR_T, mat=FLOOR, flat=True, z=FLOOR_T/2)
    card("Floor_Inlay", fl, rrect(0, FCY - 0.004, FW - 0.04, FD - 0.05, 0.02),
         holes=[rrect(0, FCY - 0.004, FW - 0.056, FD - 0.066, 0.014)], thick=0.0012, mat=GOLD, flat=True, z=FLOOR_T + 0.0006)
    for s in (-1, 1):
        cu, cv = s*(FW/2 - 0.06), FCY - FD/2 + 0.055
        card("Floor_Scroll%+d" % s, fl, volute(cu, cv, 0.022, flip=s, rot=math.pi/2 + s*math.pi/2), thick=0.0012, mat=GOLD,
             flat=True, z=FLOOR_T + 0.0006)
    for s in (-1, 1):                                                          # bow motif front-centre
        card("Floor_Bow%+d" % s, fl, volute(s*0.024, FCY - FD/2 + 0.05, 0.018, flip=-s, rot=math.pi/2 - s*math.pi/2),
             thick=0.0012, mat=GOLD, flat=True, z=FLOOR_T + 0.0006)
    card("Floor_BowKnot", fl, circle(0, FCY - FD/2 + 0.05, 0.006, 10), thick=0.0012, mat=GOLD, flat=True, z=FLOOR_T + 0.0007)
    lace_y = FCY - FD/2 - 0.0008
    lace = empty("Floor_LaceRoot", fl, (0, lace_y, 0))
    holes = [circle(-FW/2 + 0.03 + i*0.024, FLOOR_T*0.5, 0.0028, 6) for i in range(int((FW - 0.06)/0.024) + 1)]
    card("Floor_Lace", lace, rect(-FW/2 + 0.012, 0.0015, FW/2 - 0.012, FLOOR_T - 0.0015), holes=holes, thick=0.0012, mat=CREAM)

    # back wall on its fold line
    back = empty("Hinge_Back", root, (0, WALL_Y, FLOOR_T))
    bay("Bay_Left", back, -0.42, 0.13, 0.28, 0.065, 0.06, 0.06, 0.25, 0.03, 2, 0.032)
    bay("Bay_Main", back, -0.12, 0.46, 0.36, 0.19, 0.2, 0.0, 0.33, 0.1, 3, 0.07)
    bay("Bay_Right", back, 0.29, 0.35, 0.34, 0.17, 0.15, 0.05, 0.31, 0.075, 2, 0.058)
    pilaster("Pilaster_L", back, -0.483, 0.3, w=0.022)
    pilaster("Pilaster_LM", back, -0.355, 0.38)
    pilaster("Pilaster_MR", back, 0.115, 0.38)
    pilaster("Pilaster_R", back, 0.465, 0.36, w=0.026)

    # main window dressing: rod, swag pelmet with tassels, two pleated curtains
    cur = empty("Curtains", back, (0, 0, 0))
    cx, ring = -0.12, 0.024
    card("Curtain_Rod", cur, rect(cx - 0.165, 0.447, cx + 0.165, 0.453), front=0.03, thick=0.004, mat=GOLD)
    for s in (-1, 1):
        card("Curtain_RodEnd%+d" % s, cur, circle(cx + s*0.168, 0.45, 0.008, 12), front=0.031, thick=0.004, mat=GOLD)
    card("Curtain_Pelmet", cur, pelmet(cx, 0.32, 0.475, 0.07), front=0.034, mat=ROSE_D)
    card("Curtain_PelmetTrim", cur, rect(cx - 0.162, 0.468, cx + 0.162, 0.478), front=0.037, thick=0.002, mat=GOLD)
    for k in range(4):
        card("Curtain_PelmetTassel%d" % k, cur, finial(cx - 0.16 + k*0.32/3, 0.46, s=0.5, flip=True), front=0.037, mat=GOLD)
    curtain("Curtain_L", cur, cx - 0.1 - ring - 0.022, 1, 0.095, 0.446, 0.2, 0.004, 0.02)
    curtain("Curtain_R", cur, cx + 0.1 + ring + 0.022, -1, 0.095, 0.446, 0.2, 0.004, 0.02)

    # right side panel, perpendicular to the back wall, folds about Z at the back corner
    side_h = empty("Hinge_SideR", root, (0.47, 0.15, FLOOR_T))
    side = empty("Side_Frame", side_h, (0, -0.095, 0), (0, 0, -math.pi/2))
    bay("Bay_Side", side, 0.0, 0.19, 0.3, 0.095, 0.08, 0.05, 0.27, 0.04, 2, 0.042)
    pilaster("Pilaster_SideFront", side, 0.095, 0.32, w=0.022)

else:                                                   # decor only: hang it on the existing room
    root, back = bpy.data.objects["MusicRoom_Paper"], bpy.data.objects["Hinge_Back"]

# ------------------------------------------------------------------ decor, laid out around the user's piano
# (Piano collection, x -0.10..0.28, y -0.38..0.01): rug front-left, plant by the left bay, vase in the back-right corner
col = fresh_collection(DECOR_COL)
deco = empty("Decor", root, (0, 0, FLOOR_T))

# oval rug with a gold ring
rug = empty("Rug", deco, (0, 0, 0))
card("Rug_Card", rug, ellipse(-0.29, -0.21, 0.15, 0.11, n=40), thick=0.0012, mat=RUG, flat=True, z=0.0016)
card("Rug_Ring", rug, ellipse(-0.29, -0.21, 0.13, 0.09, n=40), holes=[ellipse(-0.29, -0.21, 0.12, 0.08, n=40)],
     thick=0.0008, mat=GOLD, flat=True, z=0.0026)

# potted plant in front of the left bay: faceted pot + creased leaves
pl = empty("Plant", deco, (-0.41, 0.04, 0)); pl.scale = (1.5, 1.5, 1.5)
lathe("Plant_Pot", pl, (0, 0, 0), [(0.02, 0.0), (0.026, 0.04), (0.031, 0.042), (0.031, 0.05), (0.024, 0.05)], 6, TERRA)
lathe("Plant_Soil", pl, (0, 0, 0), [(0.024, 0.046), (0.024, 0.048)], 6, M("Paper_Soil", "7A5641"))
for k, (yaw, pitch, L) in enumerate([(-150, 52, 0.095), (-95, 62, 0.11), (-35, 50, 0.09), (20, 58, 0.1),
                                       (80, 64, 0.105), (140, 55, 0.088), (-60, 78, 0.12)]):
    leaf("Plant_Leaf%d" % k, pl, (0, 0, 0.047), L*0.85, L*0.4, yaw, pitch - 12, LEAF if k % 2 else LEAF_L)

# vase of three paper flowers beside the right side panel
vs_ = empty("Vase", deco, (0.4, 0.03, 0)); vs_.scale = (1.35, 1.35, 1.35)
lathe("Vase_Body", vs_, (0, 0, 0), [(0.016, 0.0), (0.024, 0.012), (0.026, 0.03), (0.016, 0.05), (0.011, 0.058),
                                     (0.015, 0.066)], 8, LEAF_L)
for k, (dx, dy, h, tilt, col_, R) in enumerate([(-0.012, -0.004, 0.11, 18, PINK, 0.019), (0.013, 0.0, 0.125, -14, CORAL, 0.021),
                                                 (0.0, 0.008, 0.145, 4, RUG, 0.017)]):
    top = (dx*2.2, dy, h)
    card("Vase_Stem%d" % k, vs_, [(-0.0013, 0.055), (0.0013, 0.055), (top[0] + 0.001, h), (top[0] - 0.001, h)],
         front=-dy, thick=0.002, mat=LEAF)
    head = empty("Vase_Flower%d" % k, vs_, top, (math.radians(-20), math.radians(tilt), 0))
    card("Vase_Petals%d" % k, head, flower(R, 5), front=0.0, thick=0.0015, mat=col_)
    card("Vase_PetalsIn%d" % k, head, flower(R*0.62, 5, rot=math.pi/5), front=0.002, thick=0.0015, mat=CREAM if col_ != PINK else CORAL)
    card("Vase_Heart%d" % k, head, circle(0, 0, R*0.22, 10), front=0.0035, thick=0.0015, mat=GOLD)
    leaf("Vase_Leaf%d" % k, vs_, (dx*0.8, dy, 0.07), 0.04, 0.011, 90 + 60*(1 if dx >= 0 else -1), 35, LEAF)

# candle sconces on the two pilasters beside the main window (fold with the wall)
for name, u in (("Sconce_L", -0.355), ("Sconce_R", 0.115)):
    g = empty(name, back, (0, 0, 0))
    card(name + "_Plate", g, ellipse(u, 0.24, 0.011, 0.02, n=16), front=0.0145, thick=0.002, mat=GOLD)
    card(name + "_Arm", g, rect(u - 0.002, 0.22, u + 0.002, 0.226), front=0.025, thick=0.02, mat=GOLD)
    card(name + "_Cup", g, ellipse(u, -0.035, 0.012, 0.012, n=12), thick=0.003, mat=GOLD, flat=True, z=0.226)
    lathe(name + "_Candle", g, (u, -0.035, 0.2275), [(0.0045, 0.0), (0.0045, 0.03)], 6, CREAM)
    for r in (0.0, math.pi/2):                                       # two crossed cards: a flame from any side
        fl_ = empty(name + "_FlameRoot%d" % int(r > 0), g, (0, -0.035, 0), (0, 0, r))
        fl_.location = (u, -0.035, 0)
        card(name + "_Flame%d" % int(r > 0), fl_, teardrop(0, 0.259, 0.009, 0.02), thick=0.0008, mat=FLAME)

# two gold music notes floating above the right bay (own pivots, free to bob)
notes = empty("Notes", deco, (0, 0.06, 0))
n1 = empty("Note_Eighth", notes, (0.17, 0, 0.5), (0, math.radians(-12), 0))
card("Note_Eighth_Head", n1, ellipse(0, 0, 0.013, 0.009, ang=math.radians(25), n=16), mat=GOLD)
card("Note_Eighth_Stem", n1, rect(0.0095, 0.002, 0.0135, 0.058), mat=GOLD)
card("Note_Eighth_Flag", n1, [(0.0095, 0.058), (0.0135, 0.058), (0.03, 0.04), (0.028, 0.028), (0.0135, 0.044)], mat=GOLD)
n2 = empty("Note_Beamed", notes, (0.4, 0, 0.56), (0, math.radians(10), 0))
for k, (u, v) in enumerate(((0.0, 0.0), (0.036, 0.006))):
    card("Note_Beamed_Head%d" % k, n2, ellipse(u, v, 0.012, 0.0085, ang=math.radians(25), n=16), mat=GOLD)
    card("Note_Beamed_Stem%d" % k, n2, rect(u + 0.0085, v + 0.002, u + 0.0125, v + 0.052), mat=GOLD)
card("Note_Beamed_Beam", n2, [(0.0085, 0.046), (0.0485, 0.052), (0.0485, 0.062), (0.0085, 0.056)], mat=GOLD)

# ------------------------------------------------------------------ curves -> meshes, apply solidify
bpy.ops.object.select_all(action='DESELECT')
for o in curves: o.select_set(True)
bpy.context.view_layer.objects.active = curves[0]
bpy.ops.object.convert(target='MESH')
for o in made:
    if o.type == 'MESH':
        for m in list(o.modifiers):
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=m.name)
        for p in o.data.polygons: p.use_smooth = False
bpy.ops.object.select_all(action='DESELECT')

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in made if o.type == 'MESH')
print("[musicroom_paper] part:", PART, "objects:", len([o for o in made if o.type == 'MESH']), "tris:", tris)
