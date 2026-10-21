"""Hub menu study-room page: an isometric, low-poly folded-paper study room (docs/art/hub-studyroom-brief.md,
concept Art ref/Menu/concept/menu_concept_studyroom_iso_v1.png).

Run headless (does not touch an open Blender session):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_hub_studyroom.py

Output: art/blender/HexR_Hub_StudyRoom.blend, Assets/Art/Props/Hub/Hub_StudyRoom.fbx,
        docs/art/preview/hub/studyroom_iso.png, studyroom_front.png

Room frame (Blender): floor square centred on the origin, floor top z = FT; walls on the west (x = -S/2) and
north (y = +S/2) sides, so the shared corner is at the back left. The patient is on -Y. In the menu the
room is turned 45° so that corner points away from the seat (the isometric view of the concept).
Every piece is a flat-faceted card with a 2 mm bevel; named groups are what the Unity builder hinges:
  Floor, Rug, Hinge_Back (both walls + prints + shelf + window), Desk (with lamp, books, mug, tennis balls),
  Chair, Bookshelf, FloorLamp, SideTable (with record player), Plant.
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
BLEND = os.path.join(REPO, "art", "blender", "HexR_Hub_StudyRoom.blend")
FBX = os.path.join(REPO, "Assets", "Art", "Props", "Hub", "Hub_StudyRoom.fbx")
PREVIEW = os.path.join(REPO, "docs", "art", "preview", "hub")

S = 0.62            # floor square
FT = 0.008          # floor card thickness (top of floor = FT)
WH = 0.40           # wall height
WT = 0.006          # wall card thickness
BEV = 0.002

PAL = {
    "Paper_Wall": "F3E3C8", "Paper_WoodLight": "C98F57", "Paper_WoodDark": "8A5A3B", "Paper_Sage": "8FAE6A",
    "Paper_Teal": "6F9EA8", "Paper_Coral": "E58E73", "Paper_Gold": "D9B45A", "Paper_Sky": "BFDCEB",
    "Paper_White": "F7F2E8", "Paper_Mustard": "E3B85C", "Paper_Lavender": "B7A6C9", "Paper_Peach": "F2C29E",
    "Paper_Blue": "A9C6D6", "Paper_Cream": "FBF1DC", "Paper_Tennis": "D8E356", "Paper_Black": "3A302B",
    "Paper_Leaf": "6E9A58", "Paper_LeafLight": "9DBF7A", "Paper_Plank": "C08650",
}


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mats():
    out = {}
    for name, h in PAL.items():
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        c = tuple(lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4))
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Roughness"].default_value = 0.9
        m.diffuse_color = (*c, 1)
        out[name] = m
    return out


M = None
ROOT = None


def group(name, parent=None):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent or ROOT
    return e


def finish(name, bm, mat, parent, bevel=True):
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=BEV, segments=1, affect='EDGES', clamp_overlap=True)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.parent = parent
    if isinstance(mat, list):
        for m in mat:
            me.materials.append(M[m])
    else:
        me.materials.append(M[mat])
    return o


def box(name, size, loc, mat, parent, rot=(0, 0, 0), bevel=True):
    """size (x, y, z), loc = centre of the bottom face"""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.transform(bm, matrix=Matrix.Diagonal((*size, 1)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, 0, size[2] / 2), verts=bm.verts)
    if any(rot):
        bmesh.ops.transform(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix().to_4x4(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(name, bm, mat, parent, bevel and min(size) > 3 * BEV)


def cyl(name, r1, r2, h, loc, mat, parent, seg=8, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h)
    bmesh.ops.translate(bm, vec=(0, 0, h / 2), verts=bm.verts)
    if any(rot):
        bmesh.ops.transform(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix().to_4x4(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(name, bm, mat, parent, bevel=False)


def ico(name, r, loc, mat, parent, sub=1):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(name, bm, mat, parent, bevel=False)


def card(name, pts2d, plane, offset, mat, parent, thick=0.002):
    """flat cut-out from a 2D outline. plane 'xz' (on a north wall, facing -y) or 'yz' (west wall, facing +x)."""
    bm = bmesh.new()
    vs = []
    for (a, b) in pts2d:
        if plane == "xz":
            vs.append(bm.verts.new((a, offset, b)))
        else:
            vs.append(bm.verts.new((offset, a, b)))
    f = bm.faces.new(vs)
    n = Vector((0, -thick, 0)) if plane == "xz" else Vector((thick, 0, 0))
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=n, verts=[e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm, mat, parent, bevel=False)


def rect(cx, cz, w, h):
    return [(cx - w / 2, cz - h / 2), (cx + w / 2, cz - h / 2), (cx + w / 2, cz + h / 2), (cx - w / 2, cz + h / 2)]


def circle(cx, cz, r, seg=10):
    return [(cx + r * math.cos(2 * math.pi * i / seg), cz + r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]


# ---------------------------------------------------------------------------------------------- pieces

def floor_and_rug():
    g = group("Floor")
    box("Floor_Board", (S, S, FT), (0, 0, 0), "Paper_Plank", g)
    # plank lines: thin darker strips laid on the board
    for i in range(1, 6):
        x = -S / 2 + i * S / 6
        box(f"Floor_Line{i}", (0.003, S - 0.02, 0.0006), (x, 0, FT), "Paper_WoodLight", g, bevel=False)
    box("Floor_EdgeGold", (S + 0.006, S + 0.006, 0.003), (0, 0, -0.0005), "Paper_Gold", g, bevel=False)

    r = group("Rug")
    n, size = 5, 0.068
    colours = ["Paper_Sage", "Paper_Peach", "Paper_Blue", "Paper_Cream", "Paper_Coral"]
    bm = bmesh.new()
    idx = []
    x0 = 0.03 - n * size / 2
    y0 = -0.04 - n * size / 2
    for i in range(n):
        for j in range(n):
            c = colours[(i * 2 + j * 3) % len(colours)]
            q = [bm.verts.new((x0 + i * size + dx, y0 + j * size + dy, FT + 0.0015)) for dx, dy in
                 ((0, 0), (size, 0), (size, size), (0, size))]
            f = bm.faces.new(q)
            idx.append((f, colours.index(c)))
    for f, k in idx:
        f.material_index = k
    o = finish("Rug_Tiles", bm, colours, r, bevel=False)
    box("Rug_Border", (n * size + 0.012, n * size + 0.012, 0.001), (x0 + n * size / 2, y0 + n * size / 2, FT), "Paper_White", r, bevel=False)


def walls():
    g = group("Hinge_Back")
    # west wall (left in the menu view) and north wall (right), meeting at the back-left corner
    box("Wall_West", (WT, S, WH), (-S / 2 - WT / 2, 0, 0), "Paper_Wall", g)
    box("Wall_North", (S + WT, WT, WH), (-WT / 2, S / 2 + WT / 2, 0), "Paper_Wall", g)
    box("Wall_West_Trim", (WT + 0.004, S, 0.008), (-S / 2 - WT / 2, 0, WH), "Paper_Gold", g, bevel=False)
    box("Wall_North_Trim", (S + WT + 0.004, WT + 0.004, 0.008), (-WT / 2, S / 2 + WT / 2, WH), "Paper_Gold", g, bevel=False)
    box("Wall_West_Skirt", (0.004, S, 0.02), (-S / 2 + 0.002, 0, FT), "Paper_WoodLight", g, bevel=False)
    box("Wall_North_Skirt", (S, 0.004, 0.02), (0, S / 2 - 0.002, FT), "Paper_WoodLight", g, bevel=False)

    # four prints on the west wall (2 x 2), facing +x
    wx = -S / 2 + 0.002
    prints = [(0.13, 0.31, "mount"), (0.23, 0.31, "leaf"), (0.13, 0.2, "sun"), (0.23, 0.2, "hills")]
    for k, (y, z, kind) in enumerate(prints):
        card(f"Print{k}_Frame", rect(y, z, 0.085, 0.095), "yz", wx, "Paper_WoodLight", g, 0.004)
        card(f"Print{k}_Card", rect(y, z, 0.068, 0.078), "yz", wx + 0.004, "Paper_Cream", g, 0.001)
        o = wx + 0.005
        if kind == "mount":
            card(f"Print{k}_A", [(y - 0.028, z - 0.03), (y - 0.005, z + 0.005), (y + 0.012, z - 0.03)], "yz", o, "Paper_Sage", g, 0.001)
            card(f"Print{k}_B", [(y - 0.002, z - 0.03), (y + 0.016, z - 0.008), (y + 0.03, z - 0.03)], "yz", o, "Paper_Teal", g, 0.001)
            card(f"Print{k}_Sun", circle(y + 0.016, z + 0.018, 0.008, 8), "yz", o, "Paper_Mustard", g, 0.001)
        elif kind == "leaf":
            card(f"Print{k}_Stem", rect(y, z - 0.005, 0.004, 0.06), "yz", o, "Paper_Leaf", g, 0.001)
            for s in (-1, 1):
                for dz in (-0.02, 0.0, 0.02):
                    card(f"Print{k}_L{s}{int(dz*100)}", [(y, z + dz - 0.008), (y + s * 0.02, z + dz + 0.002), (y, z + dz + 0.006)], "yz", o, "Paper_Leaf", g, 0.001)
        elif kind == "sun":
            card(f"Print{k}_Sun", circle(y, z, 0.02, 10), "yz", o, "Paper_Coral", g, 0.001)
        else:
            card(f"Print{k}_A", [(y - 0.03, z - 0.03), (y - 0.03, z + 0.0), (y + 0.0, z + 0.012), (y + 0.03, z - 0.005), (y + 0.03, z - 0.03)], "yz", o, "Paper_Teal", g, 0.001)
            card(f"Print{k}_B", [(y - 0.03, z - 0.03), (y - 0.01, z - 0.012), (y + 0.03, z - 0.022), (y + 0.03, z - 0.03)], "yz", o, "Paper_Sage", g, 0.001)

    # wall shelf on the north wall near the corner, with three book blocks
    sx, sz = -0.14, 0.27
    box("Shelf_Board", (0.15, 0.04, 0.008), (sx, S / 2 - 0.02, sz), "Paper_WoodDark", g)
    box("Shelf_Bracket", (0.006, 0.03, 0.025), (sx - 0.05, S / 2 - 0.018, sz - 0.025), "Paper_WoodDark", g, bevel=False)
    for k, (dx, h, m) in enumerate(((-0.04, 0.06, "Paper_Sage"), (-0.015, 0.055, "Paper_Coral"), (0.01, 0.065, "Paper_Teal"))):
        box(f"Shelf_Book{k}", (0.022, 0.032, h), (sx + dx, S / 2 - 0.02, sz + 0.008), m, g)

    # window on the north wall, right part: frame, sky, mullions, folded paper blind
    wx0, wz0, ww, wh = 0.13, 0.12, 0.2, 0.2
    yy = S / 2 - 0.001
    card("Window_Sky", rect(wx0, wz0 + wh / 2, ww - 0.01, wh - 0.01), "xz", yy, "Paper_Sky", g, 0.001)
    card("Window_Hill", [(wx0 - ww / 2 + 0.005, wz0 + 0.005), (wx0 - 0.03, wz0 + 0.05), (wx0 + 0.03, wz0 + 0.03),
                         (wx0 + ww / 2 - 0.005, wz0 + 0.06), (wx0 + ww / 2 - 0.005, wz0 + 0.005)], "xz", yy - 0.0012, "Paper_LeafLight", g, 0.001)
    for k, r in enumerate((rect(wx0, wz0, ww + 0.02, 0.014), rect(wx0, wz0 + wh, ww + 0.02, 0.014),
                           rect(wx0 - ww / 2, wz0 + wh / 2, 0.014, wh + 0.02), rect(wx0 + ww / 2, wz0 + wh / 2, 0.014, wh + 0.02))):
        card(f"Window_Frame{k}", r, "xz", yy - 0.002, "Paper_WoodLight", g, 0.006)
    card("Window_MullionV", rect(wx0, wz0 + wh / 2, 0.006, wh), "xz", yy - 0.003, "Paper_WoodLight", g, 0.003)
    card("Window_MullionH", rect(wx0, wz0 + wh / 2, ww, 0.006), "xz", yy - 0.003, "Paper_WoodLight", g, 0.003)
    box("Window_Sill", (ww + 0.04, 0.03, 0.008), (wx0, S / 2 - 0.015, wz0 - 0.008), "Paper_WoodLight", g)
    for k in range(3):   # blind: three folded slats tilted out from the wall
        box(f"Window_Blind{k}", (ww + 0.004, 0.025, 0.003), (wx0, S / 2 - 0.016, wz0 + wh - 0.02 - k * 0.022), "Paper_Cream", g,
            rot=(-25, 0, 0), bevel=False)


def desk():
    g = group("Desk")
    dx, dy, w, d, h = -0.02, S / 2 - 0.13, 0.34, 0.2, 0.15     # larger desk top
    box("Desk_Top", (w, d, 0.012), (dx, dy, h), "Paper_WoodLight", g)
    for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        box(f"Desk_Leg{k}", (0.014, 0.014, h), (dx + sx * (w / 2 - 0.016), dy + sy * (d / 2 - 0.016), 0), "Paper_WoodDark", g)
    top = h + 0.012
    # lamp (faceted)
    lx, ly = dx + 0.12, dy + 0.05
    cyl("Lamp_Base", 0.022, 0.02, 0.008, (lx, ly, top), "Paper_Sage", g, seg=6)
    box("Lamp_Arm", (0.006, 0.006, 0.075), (lx, ly, top + 0.008), "Paper_Sage", g, rot=(20, 0, 0), bevel=False)
    box("Lamp_Arm2", (0.006, 0.006, 0.045), (lx, ly - 0.025, top + 0.078), "Paper_Sage", g, rot=(-70, 0, 0), bevel=False)
    # shade: cone, wide end down, tipped towards the front of the desk
    cyl("Lamp_Shade", 0.026, 0.01, 0.032, (lx, ly - 0.06, top + 0.07), "Paper_Sage", g, seg=6, rot=(-35, 0, 0))
    # books
    box("Desk_Book0", (0.07, 0.05, 0.014), (dx - 0.08, dy + 0.03, top), "Paper_Sage", g)
    box("Desk_Book1", (0.065, 0.048, 0.012), (dx - 0.08, dy + 0.03, top + 0.014), "Paper_Coral", g, rot=(0, 0, 8))
    # mug
    cyl("Mug", 0.014, 0.014, 0.026, (dx + 0.06, dy + 0.06, top), "Paper_White", g, seg=8)
    # two tennis balls
    for k, (bx, by) in enumerate(((dx - 0.01, dy - 0.04), (dx + 0.045, dy - 0.05))):
        r = 0.02
        ico(f"TennisBall{k}", r, (bx, by, top + r), "Paper_Tennis", g, sub=1)
        # seam: a thin white band around the ball, tilted
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=False, segments=10, radius1=r * 1.04, radius2=r * 1.04, depth=0.005)
        bmesh.ops.transform(bm, matrix=Euler((math.radians(60), math.radians(20 + k * 50), 0)).to_matrix().to_4x4(), verts=bm.verts)
        bmesh.ops.translate(bm, vec=(bx, by, top + r), verts=bm.verts)
        finish(f"TennisBall{k}_Seam", bm, "Paper_White", g, bevel=False)


def chair():
    g = group("Chair")
    cx, cy = -0.02, S / 2 - 0.29
    box("Chair_Seat", (0.1, 0.1, 0.012), (cx, cy, 0.085), "Paper_WoodLight", g)
    box("Chair_Cushion", (0.09, 0.09, 0.008), (cx, cy, 0.097), "Paper_Sage", g)
    box("Chair_Back", (0.1, 0.012, 0.13), (cx, cy - 0.044, 0.097), "Paper_WoodLight", g)
    for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        box(f"Chair_Leg{k}", (0.012, 0.012, 0.085), (cx + sx * 0.042, cy + sy * 0.042, 0), "Paper_WoodDark", g)


def bookshelf():
    g = group("Bookshelf")
    bx, by, w, d, h = -S / 2 + 0.06, -0.12, 0.11, 0.2, 0.32
    box("Bookshelf_Back", (0.008, d, h), (bx - w / 2 + 0.004, by, 0), "Paper_WoodDark", g)
    for k, side in enumerate((-1, 1)):
        box(f"Bookshelf_Side{k}", (w, 0.01, h), (bx, by + side * (d / 2 - 0.005), 0), "Paper_WoodDark", g)
    for k, z in enumerate((0.0, 0.1, 0.2, 0.31)):
        box(f"Bookshelf_Shelf{k}", (w, d, 0.01), (bx, by, z), "Paper_WoodDark", g)
    colours = ["Paper_Lavender", "Paper_Sage", "Paper_Mustard", "Paper_Coral", "Paper_Teal"]
    for row, z in enumerate((0.01, 0.11, 0.21)):
        y = by - d / 2 + 0.018
        k = 0
        while y < by + d / 2 - 0.03:
            t = 0.018 + 0.006 * ((row + k) % 3)
            hh = 0.07 + 0.012 * ((row * 2 + k) % 3)
            box(f"Bookshelf_Book{row}_{k}", (0.07, t, hh), (bx + 0.008, y + t / 2, z), colours[(row + k) % 5], g)
            y += t + 0.003
            k += 1


def floor_lamp():
    g = group("FloorLamp")
    lx, ly = -S / 2 + 0.06, 0.045
    cyl("FloorLamp_Base", 0.03, 0.028, 0.008, (lx, ly, FT), "Paper_Gold", g, seg=8)
    box("FloorLamp_Pole", (0.006, 0.006, 0.27), (lx, ly, FT + 0.008), "Paper_Gold", g, bevel=False)
    cyl("FloorLamp_Shade", 0.045, 0.028, 0.06, (lx, ly, FT + 0.26), "Paper_Cream", g, seg=6)


def side_table():
    g = group("SideTable")
    tx, ty = S / 2 - 0.08, S / 2 - 0.11
    box("SideTable_Top", (0.11, 0.11, 0.01), (tx, ty, 0.1), "Paper_WoodDark", g)
    for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        box(f"SideTable_Leg{k}", (0.01, 0.01, 0.1), (tx + sx * 0.045, ty + sy * 0.045, 0), "Paper_WoodDark", g)
    box("Record_Base", (0.09, 0.08, 0.025), (tx, ty, 0.11), "Paper_Teal", g)
    cyl("Record_Disc", 0.03, 0.03, 0.002, (tx - 0.008, ty, 0.135), "Paper_Black", g, seg=12)
    cyl("Record_Label", 0.009, 0.009, 0.001, (tx - 0.008, ty, 0.137), "Paper_Coral", g, seg=8)
    box("Record_Lid", (0.09, 0.006, 0.07), (tx, ty + 0.043, 0.135), "Paper_Teal", g, rot=(-15, 0, 0))
    box("Record_Arm", (0.004, 0.035, 0.003), (tx + 0.03, ty - 0.005, 0.136), "Paper_White", g, rot=(0, 0, 25), bevel=False)


def plant():
    g = group("Plant")
    px, py = -0.1, -S / 2 + 0.08
    cyl("Plant_Pot", 0.035, 0.026, 0.06, (px, py, FT), "Paper_White", g, seg=6)
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.3
        lean = 30 + 10 * (k % 2)
        L = 0.1 + 0.02 * (k % 3)
        bm = bmesh.new()
        base, tip = bm.verts.new((0, 0, 0)), bm.verts.new((0, 0, L))
        left, right = bm.verts.new((0, -0.022, L * 0.45)), bm.verts.new((0, 0.022, L * 0.45))
        crease = bm.verts.new((0.006, 0, L * 0.45))      # the fold along the middle of the leaf
        for f in ((base, left, crease), (left, tip, crease), (base, crease, right), (crease, tip, right)):
            bm.faces.new(f)
        R = Euler((0, math.radians(lean), a)).to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=R, verts=bm.verts)
        bmesh.ops.translate(bm, vec=(px, py, FT + 0.055), verts=bm.verts)
        finish(f"Plant_Leaf{k}", bm, "Paper_Leaf" if k % 2 else "Paper_LeafLight", g, bevel=False)


# ---------------------------------------------------------------------------------------------- run

def main():
    global M, ROOT
    bpy.ops.wm.read_factory_settings(use_empty=True)
    M = mats()
    ROOT = bpy.data.objects.new("StudyRoom_Paper", None)
    bpy.context.scene.collection.objects.link(ROOT)
    floor_and_rug()
    walls()
    desk()
    chair()
    bookshelf()
    floor_lamp()
    side_table()
    plant()

    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    per = {}
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
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
    print("STUDYROOM tris", total, per)

    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)

    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,   # baking breaks meshes under group empties
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False)
    print("STUDYROOM fbx", FBX)

    render()


def render():
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1050
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (lin(0.96), lin(0.92), lin(0.85), 1)
    bg.inputs["Strength"].default_value = 0.9
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(8)
    sun.rotation_euler = Euler((math.radians(50), math.radians(10), math.radians(-30)))
    sc.collection.objects.link(sun)
    try:
        sc.eevee.use_shadows = True
    except AttributeError:
        pass

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    target = Vector((0.0, 0.0, 0.14))
    for name, direction, ortho in (("studyroom_iso", Vector((1, -1, 0.82)), True), ("studyroom_front", Vector((0.15, -1, 0.75)), False)):
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
        print("STUDYROOM render", sc.render.filepath)


main()
