"""Replace the generated egg trees of the garden diorama with the imported Sketchfab tree.

Needs, in the open scene (art/blender/HexR_Garden.blend built by build_garden_diorama.py), the object "Tree_Cute":
"Cute Toon Tree" by matrixrex (CC BY), flattened to one mesh with origin at the bottom centre, two materials
(slot 0 leaves, slot 1 trunk). It is appended from art/blender/HexR_Tree_Cute.blend when missing.
Run inside Blender:   exec(open("<repo>/art/scripts/place_garden_trees.py").read())

It recolours the leaves to the warm olive tone, makes teal and blossom-pink variants, scatters copies on the
island, and removes the old "Trees" object.
"""
import bpy, math, random
from mathutils import Vector

def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def col(h):
    h = h.lstrip('#'); return tuple(lin(int(h[i:i+2], 16)/255) for i in (0, 2, 4)) + (1,)

import os
if "Tree_Cute" not in bpy.data.objects:
    lib = os.path.join(os.path.dirname(bpy.data.filepath), "HexR_Tree_Cute.blend")
    with bpy.data.libraries.load(lib, link=False) as (src_, dst_):
        dst_.objects = ["Tree_Cute"]
    bpy.context.scene.collection.objects.link(dst_.objects[0])
src = bpy.data.objects["Tree_Cute"]
coll = bpy.data.collections.get("Garden") or bpy.context.scene.collection

def variant(name, hexcolor):
    m = src.data.materials[0].copy(); m.name = name
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = col(hexcolor); b.inputs["Roughness"].default_value = 0.9
    return m

leaf_mats = {"olive": variant("Leaf_Olive", "A9BE4E"), "teal": variant("Leaf_Teal", "6FAA62"), "pink": variant("Leaf_Blossom", "F2B2C4")}
trunk = src.data.materials[1].copy(); trunk.name = "Trunk_Warm"
tb = next(n for n in trunk.node_tree.nodes if n.type == "BSDF_PRINCIPLED"); tb.inputs["Base Color"].default_value = col("9A6A44")

for o in list(bpy.data.objects):
    if o.name == "Trees" or o.name.startswith("Tree_Inst"):
        bpy.data.objects.remove(o, do_unlink=True)

# (x, y, scale, rotation, kind)  -- back arc of the island, two on the sides, one blossom tree
spots = [(-3.0, 3.7, 1.00, 20, "olive"), (-1.4, 4.7, 1.12, 140, "teal"), (0.5, 4.9, 0.92, 260, "olive"), (2.2, 4.3, 1.08, 75, "olive"),
         (3.7, 2.7, 0.88, 200, "teal"), (-3.9, 1.7, 0.95, 310, "olive"), (-3.7, -0.8, 0.8, 100, "olive"), (3.3, -0.3, 0.85, 30, "pink")]
for i, (x, y, s, rz, kind) in enumerate(spots):
    me = src.data.copy(); me.name = f"Tree_Inst_{i}"
    me.materials[0] = leaf_mats[kind]; me.materials[1] = trunk      # replace in place: clear() would reset every face to slot 0
    o = bpy.data.objects.new(f"Tree_Inst_{i}", me)
    o.location = (x, y, -0.04); o.rotation_euler = (0, 0, math.radians(rz)); o.scale = (s, s, s*(1.0 + 0.1*math.sin(i*2.1)))
    coll.objects.link(o)
src.hide_viewport = True; src.hide_render = True            # keep the source around, out of the way
print("placed", len(spots), "trees")
