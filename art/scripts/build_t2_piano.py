"""T2 弹琴: thin tabletop keyboard + small desk props. Saves art/blender/HexR_T2_Piano.blend.

Run headless:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_t2_piano.py

Spec: docs/art/t2-piano-brief.md. Hard requirements (Unity code depends on them):
  * root empty `T2_Keyboard`: origin on the centre line of key E4, at the WHITE-KEY TOP, 35 mm behind the key
    fronts ("fingertip line"). Patient on the -Y side looking +Y, +X = patient's right, +Z up (= Unity +Y).
  * whole keyboard <= 12 mm below the key tops (it sits on the real table); the front lip stays below the key tops
  * playable keys C4..G4 are separate objects `Key_C` .. `Key_G`, origin at the hinge (back edge, top, centre),
    ONE material slot each (`T2_KeyWhite`, shared by all white keys; Unity swaps it to light a key)
  * other white keys -> `T2_WhiteKeys_Static`, black keys -> `Black_C#2` .. `Black_A#5` (one each, origin at the
    key centre), case -> `T2_Case`
  * white key pitch 22 mm (Unity moves and widens only Key_C..Key_G and moves the black keys between them)
Range C2..C6 (four octaves, 2026-10-20: user wanted more keys): 29 white keys, 20 black keys. <= 4k triangles.
"""
import bpy, bmesh, math, os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "art", "blender", "HexR_T2_Piano.blend")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


# ------------------------------------------------------------------ materials
def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def hx(h): h = h.lstrip('#'); return tuple(lin(int(h[i:i+2], 16)/255) for i in (0, 2, 4))

_mats = {}
def M(name, h, rough=0.6, emit=0.0):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = hx(h); b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.3
    if emit:
        b.inputs["Emission Color"].default_value = (*c, 1); b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*c, 1)
    _mats[name] = m
    return m


# ------------------------------------------------------------------ mesh helpers
def box(bm, x0, x1, y0, y1, z0, z1, mi=0, bevel=0.0, seg=1, taper=None):
    """Axis box; optional bevel on all its edges; taper=(dx, dy_front) moves the top verts inward."""
    v = bmesh.ops.create_cube(bm, size=1)["verts"]
    for p in v:
        p.co.x = x0 + (p.co.x + 0.5)*(x1 - x0)
        p.co.y = y0 + (p.co.y + 0.5)*(y1 - y0)
        p.co.z = z0 + (p.co.z + 0.5)*(z1 - z0)
    if taper:
        cx = (x0 + x1)/2
        for p in v:
            if p.co.z > (z0 + z1)/2:
                p.co.x = cx + (p.co.x - cx)*(1 - taper[0])
                if p.co.y < (y0 + y1)/2: p.co.y += taper[1]
    faces = {f for p in v for f in p.link_faces}
    for f in faces: f.material_index = mi
    if bevel > 0:
        edges = list({e for p in v for e in p.link_edges})
        res = bmesh.ops.bevel(bm, geom=edges + v, offset=bevel, offset_type='OFFSET', segments=seg,
                              profile=0.5, affect='EDGES', clamp_overlap=True)
        for f in res.get("faces", []): f.material_index = mi
    return v

def cyl(bm, x, y, z0, z1, r, mi, segs=12):
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r, depth=z1 - z0)
    for p in res["verts"]:
        p.co.x += x; p.co.y += y; p.co.z += (z0 + z1)/2
    for f in {f for p in res["verts"] for f in p.link_faces}: f.material_index = mi
    return res["verts"]

def to_obj(name, bm, mats, col, parent=None, loc=(0, 0, 0)):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces: f.smooth = True
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if parent: o.parent = parent
    o.location = loc
    wn = o.modifiers.new("SoftNormals", 'WEIGHTED_NORMAL'); wn.keep_sharp = True; wn.weight = 50
    return o

def tris(o):
    me = o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    me.calc_loop_triangles(); n = len(me.loop_triangles)
    o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    return n


# ------------------------------------------------------------------ keyboard dimensions (metres, root-local)
PITCH, KEY_W = 0.022, 0.0205
FRONT, BACK = -0.035, 0.095            # white key front / back (hinge) edge
KEY_T = 0.010                          # white key thickness, top at z = 0
BOTTOM = -0.012                        # case bottom (sits on the table)
WHITE = ["C", "D", "E", "F", "G", "A", "B"]
N_WHITE = 29                           # C2 .. C6
E4 = 16                                # white-key index of E4 (origin)
PLAYABLE = {14: "C", 15: "D", 16: "E", 17: "F", 18: "G"}   # C4..G4

def wx(i): return (i - E4)*PITCH       # centre X of white key i

X_LEFT = wx(0) - PITCH/2 - 0.015       # case outer edges (15 mm cheeks)
X_RIGHT = wx(N_WHITE - 1) + PITCH/2 + 0.015

C_CASE, C_DARK, C_FELT, C_KNOB, C_SCREEN = "5A4A40", "3B322D", "B9675A", "E6D8C0", "4F8E93"
m_white = M("T2_KeyWhite", "F7F3EA", rough=0.45)
m_black = M("T2_KeyBlack", "2E2A2B", rough=0.5)
case_mats = [M("T2_Case", C_CASE), M("T2_CaseDark", C_DARK), M("T2_Felt", C_FELT, rough=0.9),
             M("T2_Knob", C_KNOB), M("T2_Screen", C_SCREEN, rough=0.3)]

col_kb = bpy.data.collections.new("T2_Keyboard"); sc.collection.children.link(col_kb)
root = bpy.data.objects.new("T2_Keyboard", None); root.empty_display_type = 'PLAIN_AXES'; root.empty_display_size = 0.03
col_kb.objects.link(root)

# ---- white keys: one hinge-origin mesh shared by the 5 playable keys; the rest merged
def white_key(bm, cx, hinge_y):
    box(bm, cx - KEY_W/2, cx + KEY_W/2, FRONT - hinge_y, BACK - hinge_y, -KEY_T, 0.0, 0, bevel=0.0009)

play_me = None
for i, name in PLAYABLE.items():
    if play_me is None:
        bm = bmesh.new(); white_key(bm, 0.0, BACK)
        o = to_obj("Key_" + name, bm, [m_white], col_kb, root, (wx(i), BACK, 0.0))
        play_me = o.data; play_me.name = "T2_WhiteKey"
    else:
        o = bpy.data.objects.new("Key_" + name, play_me); col_kb.objects.link(o)
        o.parent = root; o.location = (wx(i), BACK, 0.0)
        wn = o.modifiers.new("SoftNormals", 'WEIGHTED_NORMAL'); wn.keep_sharp = True; wn.weight = 50
    o["playable_key"] = name

bm = bmesh.new()
for i in range(N_WHITE):
    if i not in PLAYABLE: white_key(bm, wx(i), 0.0)
to_obj("T2_WhiteKeys_Static", bm, [m_white], col_kb, root)

# ---- black keys (decorative). Offset like a real piano: C#/D# apart, F#/G#/A# spread.
# One object per key, origin at its centre (2026-10-25): Unity moves the ones between the five
# playable keys with the patient's finger spacing without making them wider.
OFFS = {"C": -0.15, "D": 0.15, "F": -0.2, "G": 0.0, "A": 0.2}
for i in range(N_WHITE - 1):
    n = WHITE[i % 7]
    if n not in OFFS: continue
    cx = (wx(i) + wx(i + 1))/2 + OFFS[n]*PITCH
    cy, cz = BACK - 0.040, 0.0025
    bm = bmesh.new()
    box(bm, -0.006, 0.006, BACK - 0.080 - cy, BACK - cy, -0.003 - cz, 0.008 - cz, 0, bevel=0.0007, taper=(0.18, 0.004))
    name = n + "#" + str(2 + i//7)
    o = to_obj("Black_" + name, bm, [m_black], col_kb, root, (cx, cy, cz))
    o["black_key"] = name

# ---- case: base plate, low front lip, side cheeks, back panel with felt, screen, knobs, speaker slots
Y_LIP0, Y_BACK0, Y_BACK1 = FRONT - 0.010, BACK + 0.001, BACK + 0.040
bm = bmesh.new()
box(bm, X_LEFT, X_RIGHT, Y_LIP0, Y_BACK0, BOTTOM, -KEY_T, 0)                                   # key bed
box(bm, X_LEFT, X_RIGHT, Y_LIP0, FRONT - 0.0008, BOTTOM, -0.005, 0, bevel=0.0025, seg=2)        # front lip
for x0, x1 in ((X_LEFT, wx(0) - PITCH/2 - 0.0006), (wx(N_WHITE - 1) + PITCH/2 + 0.0006, X_RIGHT)):
    box(bm, x0, x1, Y_LIP0, Y_BACK0, BOTTOM, 0.0005, 0, bevel=0.0025, seg=2)                    # cheeks
box(bm, X_LEFT, X_RIGHT, Y_BACK0, Y_BACK1, BOTTOM, 0.018, 0, bevel=0.004, seg=2)                # back panel
box(bm, wx(0) - PITCH/2, wx(N_WHITE - 1) + PITCH/2, BACK - 0.0015, Y_BACK0 + 0.0005, -0.003, 0.005, 2)   # felt
top = 0.018
box(bm, -0.075, -0.020, Y_BACK0 + 0.012, Y_BACK0 + 0.028, top - 0.001, top + 0.0012, 1, bevel=0.0012)   # screen bezel
box(bm, -0.071, -0.024, Y_BACK0 + 0.0145, Y_BACK0 + 0.0255, top + 0.0006, top + 0.0016, 4)              # screen
for kx in (0.010, 0.032):
    cyl(bm, kx, Y_BACK0 + 0.020, top - 0.001, top + 0.006, 0.0065, 3, segs=14)                          # knobs
    box(bm, kx - 0.0008, kx + 0.0008, Y_BACK0 + 0.0145, Y_BACK0 + 0.019, top + 0.006, top + 0.0068, 1)    # knob mark
for k in range(8):                                                                                       # preset buttons
    bx = 0.060 + k*0.011
    box(bm, bx - 0.004, bx + 0.004, Y_BACK0 + 0.016, Y_BACK0 + 0.024, top - 0.001, top + 0.0022, 3 if k % 4 else 4, bevel=0.0008)
for sx in (X_LEFT + 0.040, X_RIGHT - 0.040):
    for r in range(4):
        y = Y_BACK0 + 0.010 + r*0.006
        box(bm, sx - 0.022, sx + 0.022, y, y + 0.0022, top - 0.0004, top + 0.0004, 1)                  # speaker slots
case = to_obj("T2_Case", bm, case_mats, col_kb, root)

# ------------------------------------------------------------------ props (collection T2_Props)
col_pr = bpy.data.collections.new("T2_Props"); sc.collection.children.link(col_pr)

def prop_root(name, loc):
    r = bpy.data.objects.new(name, None); r.empty_display_type = 'PLAIN_AXES'; r.empty_display_size = 0.03
    r.location = loc; col_pr.objects.link(r); return r

# music books: three stacked books, a note emblem on the top cover. Origin = bottom centre.
books = prop_root("T2_MusicBooks", (-0.56, 0.12, BOTTOM))
mats_b = [M("T2_BookBlue", "6F9EA8"), M("T2_BookCoral", "E58E73"), M("T2_BookSage", "8FAE6A"),
          M("T2_Pages", "F3E3C8", rough=0.9), M("T2_Emblem", "FFF6E4")]
bm = bmesh.new()
z = 0.0
for k, (w, d, t, rot) in enumerate(((0.23, 0.30, 0.022, 0.0), (0.215, 0.285, 0.016, 0.07), (0.205, 0.275, 0.014, -0.05))):
    before = set(bm.verts)
    box(bm, -w/2, w/2, -d/2, d/2, z, z + t, k, bevel=0.0015)                         # cover
    box(bm, -w/2 + 0.004, w/2 + 0.0006, -d/2 + 0.004, d/2 - 0.004, z + 0.002, z + t - 0.002, 3)   # page block
    for p in set(bm.verts) - before:
        p.co.xy = Vector((p.co.x*math.cos(rot) - p.co.y*math.sin(rot), p.co.x*math.sin(rot) + p.co.y*math.cos(rot)))
    z += t
# eighth-note emblem (flat, raised 1 mm) on the top book
nx, ny = -0.02, 0.0
cyl(bm, nx, ny, z, z + 0.0012, 0.016, 4, segs=16)
box(bm, nx + 0.012, nx + 0.017, ny, ny + 0.065, z, z + 0.0012, 4)
box(bm, nx + 0.012, nx + 0.040, ny + 0.050, ny + 0.065, z, z + 0.0012, 4)
to_obj("T2_MusicBooks_Mesh", bm, mats_b, col_pr, books)

# cat figurine (sitting, about 9 cm). Origin = bottom centre, facing the patient (-Y).
cat = prop_root("T2_CatFigurine", (0.38, 0.22, BOTTOM))
mats_c = [M("T2_CatWhite", "F4EFE6"), M("T2_CatPink", "E8A9A0"), M("T2_CatEye", "3B322D")]
bm = bmesh.new()
def blob(bm, c, r, mi, seg=10, ring=7):
    res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring, radius=1.0)
    for p in res["verts"]: p.co = Vector((p.co.x*r[0] + c[0], p.co.y*r[1] + c[1], p.co.z*r[2] + c[2]))
    for f in {f for p in res["verts"] for f in p.link_faces}: f.material_index = mi
blob(bm, (0, 0.004, 0.026), (0.026, 0.024, 0.027), 0)                 # body
blob(bm, (0, -0.004, 0.066), (0.021, 0.019, 0.018), 0)               # head
for s in (-1, 1):
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.008, radius2=0.0, depth=0.014)
    for p in res["verts"]: p.co += Vector((s*0.012, -0.004, 0.084))
    for f in {f for p in res["verts"] for f in p.link_faces}: f.material_index = 0
    blob(bm, (s*0.012, -0.0105, 0.081), (0.003, 0.0012, 0.004), 1, seg=6, ring=4)      # inner ear
    blob(bm, (s*0.008, -0.022, 0.068), (0.0025, 0.0015, 0.003), 2, seg=6, ring=4)      # eyes
    blob(bm, (s*0.010, -0.019, 0.006), (0.006, 0.008, 0.005), 0, seg=8, ring=5)        # paws
blob(bm, (0, -0.0225, 0.062), (0.0025, 0.0015, 0.002), 1, seg=6, ring=4)             # nose
for k in range(5):                                                                   # tail curling round
    a = -0.4 + k*0.35
    blob(bm, (0.026*math.cos(a), 0.018 + 0.012*math.sin(a), 0.006 + k*0.0015), (0.006, 0.006, 0.005), 0, seg=6, ring=4)
to_obj("T2_CatFigurine_Mesh", bm, mats_c, col_pr, cat)


# ------------------------------------------------------------------ preview-only: desk, fingertip markers, light, cameras
pv = bpy.data.collections.new("Preview_Only"); sc.collection.children.link(pv)
bmx = bmesh.new(); box(bmx, -0.75, 0.6, -0.30, 0.45, BOTTOM - 0.03, BOTTOM, 0)
me = bpy.data.meshes.new("PreviewDesk"); bmx.to_mesh(me); bmx.free(); me.materials.append(M("Preview_Desk", "C98F57"))
pv.objects.link(bpy.data.objects.new("PreviewDesk", me))
# where the patient's fingertips rest (right hand thumb..little on C4..G4)
mk = M("Preview_Fingertip", "00B4C7", emit=1.5)
for i in PLAYABLE:
    bmx = bmesh.new(); bmesh.ops.create_uvsphere(bmx, u_segments=12, v_segments=8, radius=0.006)
    me = bpy.data.meshes.new("tip"); bmx.to_mesh(me); bmx.free(); me.materials.append(mk)
    o = bpy.data.objects.new("PreviewTip_" + PLAYABLE[i], me); o.location = (wx(i), 0.0, 0.007); pv.objects.link(o)
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 3.0; sun.color = (1.0, 0.92, 0.8); sun.angle = math.radians(10)
so = bpy.data.objects.new("Sun", sun); pv.objects.link(so)
so.rotation_euler = Vector((0.4, 0.6, -0.7)).to_track_quat('-Z', 'Y').to_euler()
wd = bpy.data.worlds.new("W"); sc.world = wd; wd.use_nodes = True
bg = next(n for n in wd.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.62, 0.5, 0.4, 1); bg.inputs["Strength"].default_value = 1.0
def cam(name, loc, look, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.01; cd.clip_end = 20
    co = bpy.data.objects.new(name, cd); pv.objects.link(co); co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
cam("Cam_Seat", (-0.04, -0.50, 0.42), (-0.06, 0.06, 0.0), 26)        # patient's eye, seated
cam("Cam_Front", (-0.044, -0.95, 0.12), (-0.044, 0.04, -0.003), 50)
cam("Cam_Close", (0.10, -0.20, 0.10), (0.0, 0.03, -0.003), 40)
sc.camera = bpy.data.objects["Cam_Seat"]
sc.render.engine = 'BLENDER_EEVEE_NEXT'; sc.render.resolution_x = 1400; sc.render.resolution_y = 800
try: sc.view_settings.view_transform = 'Standard'
except TypeError: pass

kb_tris = sum(tris(o) for o in root.children)
print(f"T2 keyboard: {kb_tris} tris, width {X_RIGHT - X_LEFT:.3f} m, depth {Y_BACK1 - Y_LIP0:.3f} m, height {0.018 - BOTTOM:.3f} m (keys {-BOTTOM:.3f})")
for r in (books, cat):
    print(f"T2 {r.name}: {sum(tris(o) for o in r.children)} tris")
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("T2 saved", OUT)
