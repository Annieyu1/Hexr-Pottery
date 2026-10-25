"""T2 弹琴: split the merged `T2_BlackKeys` into one object per black key (2026-10-25).

Unity widens the five playable keys (C4..G4) to the patient's finger spacing; the black keys between
them must move with them without getting wider, so each black key needs its own object.

Edits art/blender/HexR_T2_Piano.blend in place and touches nothing else in it (hand edits stay):
  * `T2_BlackKeys` (made by build_t2_piano.py) is replaced by `Black_C#2` .. `Black_A#5`, parented to
    `T2_Keyboard`, origin at the centre of each key (so a key can move without changing width),
    custom property `black_key` = note name, same material and soft-normals modifier as before.
  * Run again: does nothing if `T2_BlackKeys` is already gone.

Run headless, then export:
    /Applications/Blender.app/Contents/MacOS/Blender -b art/blender/HexR_T2_Piano.blend --python art/scripts/split_t2_black_keys.py
    /Applications/Blender.app/Contents/MacOS/Blender -b art/blender/HexR_T2_Piano.blend --python art/scripts/export_t2.py
"""
import bpy, bmesh
from mathutils import Vector

PITCH, E4 = 0.022, 16
WHITE = ["C", "D", "E", "F", "G", "A", "B"]
N_WHITE = 29
OFFS = {"C": -0.15, "D": 0.15, "F": -0.2, "G": 0.0, "A": 0.2}   # as in build_t2_piano.py


def wx(i): return (i - E4)*PITCH


def black_names():
    """(note name, centre x) for each black key, as build_t2_piano.py placed them."""
    out = []
    for i in range(N_WHITE - 1):
        n = WHITE[i % 7]
        if n not in OFFS: continue
        out.append((n + "#" + str(2 + i//7), (wx(i) + wx(i + 1))/2 + OFFS[n]*PITCH))
    return out


src = bpy.data.objects.get("T2_BlackKeys")
if src is None:
    print("T2SPLIT nothing to do: T2_BlackKeys is not in the file")
else:
    root = src.parent
    cols = list(src.users_collection)
    mats = list(src.data.materials)
    to_root = src.matrix_parent_inverse.copy() if root else None

    bm = bmesh.new(); bm.from_mesh(src.data)
    bm.verts.ensure_lookup_table()
    # loose parts: one connected piece per key
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, part = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop(); part.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index); stack.append(b)
        parts.append(part)

    names = black_names()
    made = []
    for part in parts:
        idx = {v.index for v in part}
        lo = Vector((min(v.co.x for v in part), min(v.co.y for v in part), min(v.co.z for v in part)))
        hi = Vector((max(v.co.x for v in part), max(v.co.y for v in part), max(v.co.z for v in part)))
        centre = (lo + hi)/2
        name = min(names, key=lambda n: abs(n[1] - centre.x))[0]

        pb = bmesh.new()
        vmap = {}
        for v in part:
            vmap[v.index] = pb.verts.new(v.co - centre)
        for f in bm.faces:
            if all(v.index in idx for v in f.verts):
                nf = pb.faces.new([vmap[v.index] for v in f.verts])
                nf.smooth = f.smooth
                nf.material_index = f.material_index
        me = bpy.data.meshes.new("Black_" + name)
        pb.to_mesh(me); pb.free()
        for m in mats: me.materials.append(m)
        o = bpy.data.objects.new("Black_" + name, me)
        for c in cols: c.objects.link(o)
        o.parent = root
        o.location = src.location + centre
        o["black_key"] = name
        wn = o.modifiers.new("SoftNormals", 'WEIGHTED_NORMAL'); wn.keep_sharp = True; wn.weight = 50
        made.append(o.name)
    bm.free()

    old_mesh = src.data
    bpy.data.objects.remove(src, do_unlink=True)
    if old_mesh.users == 0: bpy.data.meshes.remove(old_mesh)
    bpy.ops.wm.save_mainfile()
    print("T2SPLIT", len(made), "black keys:", ", ".join(sorted(made)))
