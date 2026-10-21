"""T2 menu icons: simple filled 24-unit SVG shapes rendered to white-on-transparent PNG masks (tinted in Unity).

Run headless:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/make_t2_icons.py

Output: Assets/Learning/Art/UI/T2/icon_<name>.png (128 px). Shapes are drawn here (no third-party icons);
they match the Figma page "T2 Piano Menu" in file fnupP6kEzs5Z5mMhjKOyJL.
"""
import bpy, os, tempfile, mathutils

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "Assets", "Learning", "Art", "UI", "T2")
os.makedirs(OUT, exist_ok=True)
SIZE = 128

ICONS = {
    "listen":  "M12 3a7 7 0 0 0-7 7v5a3 3 0 0 0 3 3h1v-7H7v-1a5 5 0 0 1 10 0v1h-2v7h1a3 3 0 0 0 3-3v-5a7 7 0 0 0-7-7z",
    "learn":   "M9 11V5.5a1.5 1.5 0 0 1 3 0V10h.5V3.5a1.5 1.5 0 0 1 3 0V10h.5V5a1.5 1.5 0 0 1 3 0v8.5c0 4-2.8 7.5-7 7.5-2.6 0-4.3-1.3-5.5-3.3L4 13.6a1.5 1.5 0 0 1 2.4-1.8L9 14z",
    "star":    "M12 1.8l3 6.4 7 .8-5.2 4.7 1.4 6.9L12 17.1l-6.2 3.5 1.4-6.9L2 9l7-.8z",
    "piano":   "M3 6h18a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M4.5 9v7.5h3V9z M8.75 9v7.5h3V9z M13 9v7.5h3V9z M17.25 9v7.5h2.25V9z",
    "play":    "M7 4.5v15l12-7.5z",
    "pause":   "M6 5h4v14H6z M14 5h4v14h-4z",
    "skip":    "M5 5l9 7-9 7z M15 5h3v14h-3z",
    "gear":    "M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7z M20.5 13.1v-2.2l-2.3-.5a6.8 6.8 0 0 0-.8-1.9l1.3-2-1.6-1.6-2 1.3a6.8 6.8 0 0 0-1.9-.8l-.5-2.3h-2.2l-.5 2.3a6.8 6.8 0 0 0-1.9.8l-2-1.3-1.6 1.6 1.3 2a6.8 6.8 0 0 0-.8 1.9l-2.3.5v2.2l2.3.5c.2.7.4 1.3.8 1.9l-1.3 2 1.6 1.6 2-1.3c.6.4 1.2.6 1.9.8l.5 2.3h2.2l.5-2.3c.7-.2 1.3-.4 1.9-.8l2 1.3 1.6-1.6-1.3-2c.4-.6.6-1.2.8-1.9z",
    "left":    "M15.7 4.3l1.4 1.4L10.8 12l6.3 6.3-1.4 1.4L8 12z",
    "right":   "M8.3 4.3L16 12l-7.7 7.7-1.4-1.4L13.2 12 6.9 5.7z",
    "note":    "M11 2h2c0 3 6 4 6 9h-2c0-2.5-2.5-4-4-4.2V17a4 4 0 1 1-2-3.46z",
    "sparkle": "M12 2l2.2 7.8L22 12l-7.8 2.2L12 22l-2.2-7.8L2 12l7.8-2.2z",
    "home":    "M12 3l9 8h-3v9h-5v-6h-2v6H6v-9H3z",
}

def render(name, d):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    svg = os.path.join(tempfile.gettempdir(), f"t2icon_{name}.svg")
    with open(svg, "w") as f:
        f.write(f'<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><path d="{d}" fill="#000"/></svg>')
    bpy.ops.import_curve.svg(filepath=svg)
    objs = [o for o in bpy.context.scene.objects if o.type == 'CURVE']
    lo = mathutils.Vector((1e9, 1e9, 0)); hi = mathutils.Vector((-1e9, -1e9, 0))
    for o in objs:
        o.data.fill_mode = 'BOTH'
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            lo.x = min(lo.x, w.x); lo.y = min(lo.y, w.y); hi.x = max(hi.x, w.x); hi.y = max(hi.y, w.y)
    sc = bpy.context.scene
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'
    # each icon is framed on its own shape (6 % margin), so every icon fills its slot
    span = max(hi.x - lo.x, hi.y - lo.y)
    cam.data.ortho_scale = span * 1.12
    cam.location = ((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, 5)
    r = sc.render
    r.engine = 'BLENDER_WORKBENCH'; r.resolution_x = r.resolution_y = SIZE; r.film_transparent = True
    sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (1, 1, 1)
    sc.view_settings.view_transform = 'Standard'
    r.image_settings.file_format = 'PNG'; r.image_settings.color_mode = 'RGBA'
    r.filepath = os.path.join(OUT, f"icon_{name}.png")
    bpy.ops.render.render(write_still=True)
    print("T2ICON", name, r.filepath)

for n, d in ICONS.items():
    render(n, d)
