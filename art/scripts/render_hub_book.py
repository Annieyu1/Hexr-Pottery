"""Render the Hub book sample from its three cameras into docs/art/preview/hub/.

    /Applications/Blender.app/Contents/MacOS/Blender -b art/blender/HexR_Hub_Book.blend --python art/scripts/render_hub_book.py
"""
import bpy, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(bpy.data.filepath))))
OUT = os.path.join(REPO, "docs", "art", "preview", "hub")
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
for name in ("Cam_Seat", "Cam_Hero", "Cam_Close"):
    sc.camera = bpy.data.objects[name]
    sc.render.filepath = os.path.join(OUT, "hub_book_%s.png" % name[4:].lower())
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)
