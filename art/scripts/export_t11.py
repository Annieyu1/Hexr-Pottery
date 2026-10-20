"""Export the T11 containers to Unity: one FBX per group in Assets/Art/Props/T11/.

Run headless (does not touch your open Blender session or the .blend):
    /Applications/Blender.app/Contents/MacOS/Blender -b art/blender/HexR_T11_Bottles.blend --python art/scripts/export_t11.py

  * opaque parts (Base, Neck, Cap) -> one colour-palette atlas (palette.py), art/textures/t11/Palette_Base.png
  * Body keeps its own translucent glass material + label material (alpha is not carried by the palette)
  * Liquid keeps its own material (Unity replaces it with the liquid shader); it is the whole inner cavity
  * each group is exported with its root empty moved to the origin, so the group origin = centre of the base bottom
  * axes as in export_room.py: Blender +Z = Unity +Y, 1 unit = 1 m
"""
import bpy, os, math

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(bpy.data.filepath))))
GROUPS = ["Bottle_Tight", "Bottle_Loose", "Jar"]
OUT_DIR = os.path.join(REPO, "Assets", "Art", "Props", "T11")
TEX_DIR = os.path.join(REPO, "art", "textures", "t11")
os.makedirs(OUT_DIR, exist_ok=True)

exec(open(os.path.join(REPO, "art", "scripts", "palette.py")).read())

opaque = [f"{g}_{p}" for g in GROUPS for p in ("Base", "Neck", "Cap") if f"{g}_{p}" in bpy.data.objects]
apply_palette(opaque, TEX_DIR)

report = []
for g in GROUPS:
    root = bpy.data.objects[g]
    root.location = (0, 0, 0)
    for o in bpy.data.objects:
        o.select_set(False)
    kids = [o for o in root.children]
    for o in [root] + kids:
        o.select_set(True)
    path = os.path.join(OUT_DIR, g + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
        path_mode='COPY', embed_textures=True)
    tris = 0
    for o in kids:
        o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
    cap = bpy.data.objects[g + "_Cap"]
    zs = [v.co.z for v in cap.data.vertices]; xs = [v.co.x for v in cap.data.vertices]
    report.append(f"{g}: {os.path.getsize(path)//1024} KB, {tris} tris, cap centre z={cap.location.z:.3f}, cap radius={max(abs(x) for x in xs):.4f}, cap top z={cap.location.z+max(zs):.3f}")
for line in report:
    print("T11EXPORT", line)
