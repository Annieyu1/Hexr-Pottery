"""Export T2 piano assets to Unity: Assets/Art/Props/T2/T2_Keyboard.fbx, T2_MusicBooks.fbx, T2_CatFigurine.fbx.

Run headless (does not touch your open Blender session or the .blend):
    /Applications/Blender.app/Contents/MacOS/Blender -b art/blender/HexR_T2_Piano.blend --python art/scripts/export_t2.py

  * flat colours (case, black keys, props) -> one palette atlas (palette.py), art/textures/t2/Palette_Base.png
  * all white keys keep their own material `T2_KeyWhite` (one slot per key: Unity swaps it to light a key)
  * each root empty is moved to the origin before export; axes as export_room.py (Blender +Z = Unity +Y, 1 unit = 1 m)
"""
import bpy, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(bpy.data.filepath))))
ROOTS = ["T2_Keyboard", "T2_MusicBooks", "T2_CatFigurine"]
OUT_DIR = os.path.join(REPO, "Assets", "Art", "Props", "T2")
TEX_DIR = os.path.join(REPO, "art", "textures", "t2")
os.makedirs(OUT_DIR, exist_ok=True)

exec(open(os.path.join(REPO, "art", "scripts", "palette.py")).read())

flat = ["T2_Case", "T2_BlackKeys", "T2_MusicBooks_Mesh", "T2_CatFigurine_Mesh"]
apply_palette(flat, TEX_DIR)

report = []
for name in ROOTS:
    root = bpy.data.objects[name]
    root.location = (0, 0, 0)
    for o in bpy.data.objects:
        o.select_set(False)
    kids = list(root.children)
    for o in [root] + kids:
        o.select_set(True)
    path = os.path.join(OUT_DIR, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
        path_mode='COPY', embed_textures=True)
    dg = bpy.context.evaluated_depsgraph_get()
    tris = 0
    for o in kids:
        me = o.evaluated_get(dg).to_mesh(); me.calc_loop_triangles(); tris += len(me.loop_triangles)
        o.evaluated_get(dg).to_mesh_clear()
    report.append(f"{name}: {os.path.getsize(path)//1024} KB, {tris} tris, parts: " + ", ".join(sorted(o.name for o in kids)))
for line in report:
    print("T2EXPORT", line)
for k in "CDEFG":
    o = bpy.data.objects["Key_" + k]
    print("T2EXPORT", o.name, "hinge", tuple(round(c, 4) for c in o.location), "materials", [m.name for m in o.data.materials])
