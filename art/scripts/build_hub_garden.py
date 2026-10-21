"""Hub menu hand-hold page: an isometric, low-poly folded-paper tea garden (concept
Art ref/Menu/concept/menu_concept_garden_iso_v1.png, Figma node 52:2; brief docs/art/hub-garden-brief.md).

Run headless (does not touch an open Blender session):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_hub_garden.py

Output: art/blender/HexR_Hub_Garden.blend, Assets/Art/Props/Hub/Hub_Garden.fbx,
        docs/art/preview/hub/garden_iso.png, garden_front.png

Hand edits are kept: everything this script makes carries the custom property "paper_build". If the .blend
already exists it is opened, only tagged objects are deleted and rebuilt; anything else (your own models,
untagged copies) stays as it is.

Frame (Blender), same as the study room: grass square centred on the origin, top z = FT; hedges and fence on
the west (x = -S/2) and north (y = +S/2) sides, rose arch across the back-left corner. The patient is on -Y.
In the menu the garden is turned 45° so that corner points away from the seat; then the room diagonal
(1, 1) runs left-right across the view and (1, -1) points at the seat.
Named groups (what the Unity builder hinges): Floor, Path, Hinge_Back (hedges, fence, arch, roses, bird),
Tree_L, Tree_R, Table (with tea set and cakes), Stools, Flowers_L, Flowers_R.
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
BLEND = os.path.join(REPO, "art", "blender", "HexR_Hub_Garden.blend")
FBX = os.path.join(REPO, "Assets", "Art", "Props", "Hub", "Hub_Garden.fbx")
PREVIEW = os.path.join(REPO, "docs", "art", "preview", "hub")

TAG = "paper_build"
S = 0.62            # grass square
FT = 0.01           # grass card thickness
BEV = 0.002
D = Vector((1, 1, 0)).normalized()      # left-right across the menu view
F = Vector((1, -1, 0)).normalized()     # towards the seat

PAL = {
    "Paper_Grass": "8FAE6A", "Paper_GrassLight": "A9C47F", "Paper_Hedge": "7FA35F", "Paper_HedgeLight": "9BBB78",
    "Paper_Cream": "F3E3C8", "Paper_White": "F7F2E8", "Paper_Stone": "CFC3AE", "Paper_StoneDark": "B9AC95",
    "Paper_Coral": "E58E73", "Paper_Peach": "F2C29E", "Paper_Mustard": "E3B85C", "Paper_Lavender": "B7A6C9",
    "Paper_WoodLight": "C98F57", "Paper_Trunk": "9B6B45", "Paper_Gold": "D9B45A", "Paper_Tea": "C9853E",
    "Paper_Cake": "F6E2C4", "Paper_Strawberry": "D9575A", "Paper_Biscuit": "D8A464", "Paper_Leaf": "6E9A58",
    "Paper_Bird": "C99A6B", "Paper_Plate": "EDE6DA",
}


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


M = {}
ROOT = None


def materials():
    for name, h in PAL.items():
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        c = tuple(lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4))
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Roughness"].default_value = 0.9
        m.diffuse_color = (*c, 1)
        M[name] = m


def tag(o):
    o[TAG] = 1
    return o


def group(name, parent=None):
    e = tag(bpy.data.objects.new(name, None))
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent or ROOT
    return e


def finish(name, bm, mat, parent, bevel=False):
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=BEV, segments=1, affect='EDGES', clamp_overlap=True)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    o = tag(bpy.data.objects.new(name, me))
    bpy.context.scene.collection.objects.link(o)
    o.parent = parent
    for m in (mat if isinstance(mat, list) else [mat]):
        me.materials.append(M[m])
    return o


def _place(bm, rot, loc, scale=None):
    if scale:
        bmesh.ops.transform(bm, matrix=Matrix.Diagonal((*scale, 1)), verts=bm.verts)
    if any(rot):
        bmesh.ops.transform(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix().to_4x4(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)


def box(name, size, loc, mat, parent, rot=(0, 0, 0), bevel=True):
    """size (x, y, z), loc = centre of the bottom face"""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.transform(bm, matrix=Matrix.Diagonal((*size, 1)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, 0, size[2] / 2), verts=bm.verts)
    _place(bm, rot, loc)
    return finish(name, bm, mat, parent, bevel and min(size) > 3 * BEV)


def cyl(name, r1, r2, h, loc, mat, parent, seg=8, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h)
    bmesh.ops.translate(bm, vec=(0, 0, h / 2), verts=bm.verts)
    _place(bm, rot, loc)
    return finish(name, bm, mat, parent)


def ico(name, r, loc, mat, parent, sub=1, scale=None):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    _place(bm, (0, 0, 0), loc, scale)
    return finish(name, bm, mat, parent)


def card(name, pts, mat, parent, thick=0.003, rot=(0, 0, 0), loc=(0, 0, 0)):
    """flat cut-out in the XZ plane (outline in x, z), extruded along +y, then rotated and moved"""
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new((x, 0, z)) for x, z in pts])
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, thick, 0), verts=[e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.translate(bm, vec=(0, -thick / 2, 0), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _place(bm, rot, loc)
    return finish(name, bm, mat, parent)


def ngon(cx, cy, r, n, rot=0.0):
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), cy + r * math.sin(rot + 2 * math.pi * i / n)) for i in range(n)]


def flat(name, pts, z, h, mat, parent):
    """flat slab from a 2D outline in the XY plane, bottom at z"""
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new((x, y, z)) for x, y in pts])
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0, h), verts=[e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm, mat, parent)


def yaw_of(v):
    return math.degrees(math.atan2(v.y, v.x))


# ---------------------------------------------------------------------------------------------- pieces

def floor_and_path():
    g = group("Floor")
    box("Grass_Board", (S, S, FT), (0, 0, 0), "Paper_Grass", g)
    box("Grass_EdgeGold", (S + 0.006, S + 0.006, 0.004), (0, 0, -0.001), "Paper_Gold", g, bevel=False)
    # a few lighter grass facets laid on the board
    for k, (x, y, r, a) in enumerate(((-0.12, -0.16, 0.07, 0.2), (0.17, 0.05, 0.06, 0.8), (-0.02, 0.2, 0.05, 1.3))):
        flat(f"Grass_Patch{k}", ngon(x, y, r, 5, a), FT, 0.0008, "Paper_GrassLight", g)

    p = group("Path")
    for k in range(4):
        c = Vector((0, 0, 0)) + F * (0.12 + k * 0.055)
        flat(f"Path_Stone{k}", ngon(c.x, c.y, 0.024 - k * 0.0, 8, 0.2 * k), FT, 0.003, "Paper_Stone", p)


def back():
    g = group("Hinge_Back")
    # hedge: a row of faceted blocks along the west and north edges, behind the fence
    for k in range(5):
        y = -S / 2 + 0.07 + k * 0.12
        h = 0.11 + 0.02 * (k % 2)
        box(f"Hedge_W{k}", (0.07, 0.12, h), (-S / 2 + 0.035, y, FT), "Paper_Hedge" if k % 2 else "Paper_HedgeLight", g)
    for k in range(5):
        x = -S / 2 + 0.07 + k * 0.12
        h = 0.11 + 0.02 * ((k + 1) % 2)
        box(f"Hedge_N{k}", (0.12, 0.07, h), (x, S / 2 - 0.035, FT), "Paper_Hedge" if k % 2 else "Paper_HedgeLight", g)
    # picket fence in front of the hedges
    picket = [(-0.008, 0), (0.008, 0), (0.008, 0.07), (0, 0.082), (-0.008, 0.07)]
    for side in ("W", "N"):
        for k in range(9):
            t = -S / 2 + 0.1 + k * 0.055
            if side == "W":
                if t > 0.12:                    # leave room for the arch near the back corner
                    continue
                card(f"Fence_{side}{k}", picket, "Paper_White", g, rot=(0, 0, 90), loc=(-S / 2 + 0.085, t, FT))
            else:
                if t < -0.12:
                    continue
                card(f"Fence_{side}{k}", picket, "Paper_White", g, loc=(t, S / 2 - 0.085, FT))
        if side == "W":
            box("Fence_W_Rail", (0.004, 0.33, 0.008), (-S / 2 + 0.085, -0.04, FT + 0.045), "Paper_White", g, bevel=False)
        else:
            box("Fence_N_Rail", (0.36, 0.004, 0.008), (0.07, S / 2 - 0.085, FT + 0.045), "Paper_White", g, bevel=False)

    # rose arch across the back-left corner, facing the seat diagonally
    c = Vector((-S / 2 + 0.16, S / 2 - 0.16, 0))    # in front of the corner hedge
    yaw = yaw_of(D)
    for k, s in enumerate((-1, 1)):
        p = c + D * (s * 0.075)
        box(f"Arch_Post{k}", (0.016, 0.016, 0.2), (p.x, p.y, FT), "Paper_White", g, rot=(0, 0, yaw))
    arch = []
    for i in range(7):
        a = math.pi * i / 6
        arch.append((-0.075 * math.cos(a), 0.2 + 0.06 * math.sin(a)))
    inner = [(x * 0.82, 0.2 + (z - 0.2) * 0.7) for x, z in reversed(arch)]
    card("Arch_Top", arch + inner, "Paper_White", g, thick=0.016, rot=(0, 0, yaw), loc=(c.x, c.y, FT))
    roses = ((-0.075, 0.12, "Paper_Coral"), (-0.075, 0.2, "Paper_Peach"), (-0.04, 0.255, "Paper_Coral"),
             (0.03, 0.258, "Paper_Peach"), (0.075, 0.2, "Paper_Coral"), (0.075, 0.13, "Paper_Cream"))
    for k, (u, z, m) in enumerate(roses):
        p = c + D * u
        ico(f"Arch_Rose{k}", 0.017, (p.x, p.y, FT + z), m, g, sub=1, scale=(1, 1, 0.8))
        ico(f"Arch_Leaf{k}", 0.012, (p.x - 0.01, p.y - 0.01, FT + z - 0.015), "Paper_Leaf", g, sub=0)

    # a small bird on the north fence rail
    bx, by, bz = 0.17, S / 2 - 0.085, FT + 0.085
    ico("Bird_Body", 0.016, (bx, by, bz + 0.012), "Paper_Bird", g, sub=0, scale=(1.3, 0.9, 0.9))
    ico("Bird_Head", 0.01, (bx + 0.017, by, bz + 0.026), "Paper_Bird", g, sub=0)
    card("Bird_Tail", [(0, 0), (-0.02, 0.012), (-0.02, -0.004)], "Paper_Bird", g, thick=0.004, loc=(bx - 0.016, by, bz + 0.012))
    card("Bird_Beak", [(0, 0.004), (0.008, 0), (0, -0.004)], "Paper_Mustard", g, thick=0.003, loc=(bx + 0.026, by, bz + 0.026))


def tree(name, x, y):
    g = group(name)
    cyl(f"{name}_Trunk", 0.018, 0.013, 0.14, (x, y, FT), "Paper_Trunk", g, seg=6)
    ico(f"{name}_Crown", 0.085, (x, y, FT + 0.2), "Paper_Hedge", g, sub=1)


def table():
    g = group("Table")
    tz = 0.13
    cyl("Table_Foot", 0.04, 0.03, 0.012, (0, 0, FT), "Paper_White", g, seg=8)
    cyl("Table_Stem", 0.014, 0.014, tz - 0.012, (0, 0, FT + 0.012), "Paper_White", g, seg=6)
    cyl("Table_Top", 0.095, 0.095, 0.012, (0, 0, FT + tz), "Paper_Cream", g, seg=10)
    top = FT + tz + 0.012
    # teapot at the back of the table
    pt = Vector((0, 0, 0)) - F * 0.045
    ico("Teapot_Body", 0.034, (pt.x, pt.y, top + 0.027), "Paper_White", g, sub=1, scale=(1, 1, 0.85))
    cyl("Teapot_Lid", 0.018, 0.007, 0.012, (pt.x, pt.y, top + 0.053), "Paper_Coral", g, seg=6)
    ico("Teapot_Knob", 0.006, (pt.x, pt.y, top + 0.067), "Paper_Gold", g, sub=0)
    sp = pt + D * 0.03
    cyl("Teapot_Spout", 0.006, 0.004, 0.03, (sp.x, sp.y, top + 0.02), "Paper_White", g, seg=5, rot=(0, 50, yaw_of(D)))
    hd = pt - D * 0.032
    card("Teapot_Handle", [(-0.004, 0.014), (0.004, 0.014), (0.012, 0.03), (0.004, 0.046), (-0.004, 0.046), (0.004, 0.03)],
         "Paper_White", g, thick=0.004, rot=(0, 0, yaw_of(D) + 180), loc=(hd.x, hd.y, top))
    # two cups of tea on saucers, one each side (towards each stool)
    for k, s in enumerate((-1, 1)):
        c = D * (s * 0.055)
        cyl(f"Saucer{k}", 0.025, 0.022, 0.003, (c.x, c.y, top), "Paper_Coral", g, seg=8)
        cyl(f"Cup{k}", 0.014, 0.018, 0.024, (c.x, c.y, top + 0.003), "Paper_White", g, seg=8)
        cyl(f"Cup{k}_Tea", 0.0165, 0.0165, 0.001, (c.x, c.y, top + 0.024), "Paper_Tea", g, seg=8)
        h = c - F * 0.017
        box(f"Cup{k}_Handle", (0.004, 0.01, 0.012), (h.x, h.y, top + 0.007), "Paper_White", g, rot=(0, 0, yaw_of(F)), bevel=False)
        # a folded-paper steam curl
        curl = [(0, 0), (0.006, 0.012), (-0.004, 0.024), (0.004, 0.036), (0.0, 0.036), (-0.008, 0.024), (0.002, 0.012), (-0.004, 0)]
        card(f"Cup{k}_Steam", curl, "Paper_White", g, thick=0.002, rot=(0, 0, yaw_of(D)), loc=(c.x, c.y, top + 0.026))
    # a plate of cakes at the front of the table
    pl = F * 0.045
    cyl("Plate", 0.034, 0.03, 0.004, (pl.x, pl.y, top), "Paper_Plate", g, seg=10)
    cake = [(0, 0), (0.034, -0.01), (0.034, 0.01)]
    flat("Cake_Slice", [(pl.x - 0.016 + x, pl.y - 0.004 + y) for x, y in cake], top + 0.004, 0.02, "Paper_Cake", g)
    flat("Cake_Cream", [(pl.x - 0.016 + x, pl.y - 0.004 + y) for x, y in cake], top + 0.024, 0.004, "Paper_White", g)
    ico("Cake_Strawberry", 0.006, (pl.x + 0.008, pl.y - 0.004, top + 0.032), "Paper_Strawberry", g, sub=0)
    for k, (dx, dy) in enumerate(((-0.012, 0.016), (0.008, 0.02))):
        cyl(f"Biscuit{k}", 0.009, 0.009, 0.004, (pl.x + dx, pl.y + dy, top + 0.004), "Paper_Biscuit", g, seg=8)


def stools():
    g = group("Stools")
    for k, s in enumerate((-1, 1)):
        c = D * (s * 0.16)
        cyl(f"Stool{k}", 0.042, 0.036, 0.075, (c.x, c.y, FT), "Paper_Stone", g, seg=7)
        cyl(f"Stool{k}_Top", 0.036, 0.034, 0.003, (c.x, c.y, FT + 0.075), "Paper_StoneDark", g, seg=7)


def flower(g, name, x, y, kind, colour, h=0.06):
    if kind == "hyacinth":
        box(f"{name}_Stem", (0.004, 0.004, h * 0.5), (x, y, FT), "Paper_Leaf", g, bevel=False)
        cyl(f"{name}_Head", 0.014, 0.004, h * 0.6, (x, y, FT + h * 0.45), colour, g, seg=5)
    elif kind == "tulip":
        box(f"{name}_Stem", (0.004, 0.004, h), (x, y, FT), "Paper_Leaf", g, bevel=False)
        cyl(f"{name}_Head", 0.008, 0.014, 0.022, (x, y, FT + h), colour, g, seg=5)
    else:   # daisy: flat five-point star on a stem, facing the seat
        box(f"{name}_Stem", (0.004, 0.004, h), (x, y, FT), "Paper_Leaf", g, bevel=False)
        star = []
        for i in range(10):
            r = 0.016 if i % 2 == 0 else 0.007
            a = math.pi / 2 + i * math.pi / 5
            star.append((r * math.cos(a), r * math.sin(a)))
        card(f"{name}_Head", star, colour, g, thick=0.003, rot=(0, 0, yaw_of(D)), loc=(x, y, FT + h))
    for s in (-1, 1):
        card(f"{name}_Leaf{s}", [(0, 0), (s * 0.016, 0.018), (0, 0.03)], "Paper_Leaf", g, thick=0.002,
             rot=(0, 0, yaw_of(D)), loc=(x, y, FT))


def flowers():
    gl = group("Flowers_L")      # left of the table in the menu view: lavender hyacinths
    base = -D * 0.25 + F * 0.04
    for k, (u, v) in enumerate(((0, 0), (0.03, 0.02), (-0.025, 0.025), (0.01, -0.03), (-0.03, -0.015))):
        p = base + D * u + F * v
        flower(gl, f"Hyacinth{k}", p.x, p.y, "hyacinth", "Paper_Lavender", 0.06 + 0.01 * (k % 2))
    gr = group("Flowers_R")      # right: tulips and yellow daisies
    base = D * 0.25 + F * 0.03
    kinds = (("tulip", "Paper_Coral"), ("daisy", "Paper_Mustard"), ("tulip", "Paper_Peach"), ("daisy", "Paper_Mustard"), ("tulip", "Paper_Coral"))
    for k, ((u, v), (kind, col)) in enumerate(zip(((0, 0), (0.03, 0.02), (-0.025, 0.025), (0.015, -0.03), (-0.03, -0.012)), kinds)):
        p = base + D * u + F * v
        flower(gr, f"Bloom{k}", p.x, p.y, kind, col, 0.055 + 0.012 * (k % 2))


# ---------------------------------------------------------------------------------------------- run

def main():
    global ROOT
    if os.path.exists(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
        mine = [o for o in bpy.data.objects if o.get(TAG)]
        kept = [o.name for o in bpy.data.objects if not o.get(TAG)]
        for o in mine:
            bpy.data.objects.remove(o, do_unlink=True)
        print("GARDEN reopened; removed", len(mine), "tagged objects; kept untagged:", kept)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    materials()
    ROOT = tag(bpy.data.objects.new("Garden_Paper", None))
    bpy.context.scene.collection.objects.link(ROOT)
    floor_and_path()
    back()
    tree("Tree_L", -S / 2 + 0.07, -S / 2 + 0.09)     # left corner of the menu view
    tree("Tree_R", S / 2 - 0.09, S / 2 - 0.07)       # right corner
    table()
    stools()
    flowers()

    dg = bpy.context.evaluated_depsgraph_get()
    total, per = 0, {}
    for o in bpy.context.scene.objects:
        if o.type != 'MESH' or not o.get(TAG):
            continue
        me = o.evaluated_get(dg).to_mesh()
        me.calc_loop_triangles()
        n = len(me.loop_triangles)
        o.evaluated_get(dg).to_mesh_clear()
        total += n
        top = o
        while top.parent and top.parent != ROOT:
            top = top.parent
        per[top.name] = per.get(top.name, 0) + n
    print("GARDEN tris", total, per)

    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    for o in bpy.context.scene.objects:
        o.select_set(o.type in {'MESH', 'EMPTY'})
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,   # baking breaks meshes under group empties
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False)
    print("GARDEN fbx", FBX)
    render()


def render():
    sc = bpy.context.scene
    for o in [o for o in bpy.data.objects if o.name in ("GardenPreviewSun", "GardenPreviewCam")]:
        bpy.data.objects.remove(o, do_unlink=True)
    try:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1050
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.get("GardenPreviewWorld") or bpy.data.worlds.new("GardenPreviewWorld")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (lin(0.96), lin(0.92), lin(0.85), 1)
    bg.inputs["Strength"].default_value = 0.9
    sun = bpy.data.objects.new("GardenPreviewSun", bpy.data.lights.new("GardenPreviewSun", 'SUN'))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(8)
    sun.rotation_euler = Euler((math.radians(50), math.radians(10), math.radians(-30)))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("GardenPreviewCam", bpy.data.cameras.new("GardenPreviewCam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    target = Vector((0.0, 0.0, 0.12))
    for name, direction, ortho in (("garden_iso", Vector((1, -1, 0.82)), True), ("garden_front", Vector((0.15, -1, 0.75)), False)):
        d = direction.normalized()
        cam.location = target + d * 2.0
        cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
        if ortho:
            cam.data.type = 'ORTHO'
            cam.data.ortho_scale = 1.05
        else:
            cam.data.type = 'PERSP'
            cam.data.lens = 85
        sc.render.filepath = os.path.join(PREVIEW, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("GARDEN render", sc.render.filepath)
    # preview helpers are not part of the model
    for o in (sun, cam):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


main()
