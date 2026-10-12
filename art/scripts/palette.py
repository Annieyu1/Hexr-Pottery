"""Palette consolidation: many flat-colour materials -> one palette atlas + UV0.

Called from export_room.py (works on the open session, so it is destructive; revert the .blend afterwards).

  * every plain-colour material is replaced by one material `M_Palette`
  * each face's UV0 is moved to the centre of its colour cell in `Palette_Base.png` (1024 px, 16x16 cells of 64 px)
  * textured materials (lamp, plant leaf, wall posters) are kept but simplified (base colour only, 512 px)
  * Sky_Backdrop gets its own emissive `M_Sky`

Unity: set Palette_Base.png to Point filter, no mipmaps, so neighbouring cells never bleed.
"""
import bpy, os, json, zlib, struct

CELL, GRID = 64, 16          # 16 x 16 cells of 64 px = 1024 px
TEX_SIZE = 512               # max size for the kept textured materials


def _srgb8(c):
    c = max(0.0, min(1.0, c))
    v = 12.92*c if c <= 0.0031308 else 1.055*(c ** (1/2.4)) - 0.055
    return int(round(v*255))


def _png(path, w, h, rows):
    raw = b"".join(b"\x00" + r for r in rows)
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def _principled(m):
    return next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m.use_nodes else None


def _base_image(m):
    """Image feeding Base Color, or None for a plain-colour material."""
    b = _principled(m)
    if not b:
        return None
    sock = b.inputs["Base Color"]
    if not sock.is_linked:
        return None
    # walk upstream of Base Color only (some imports go through a Mix node)
    stack, seen = [sock.links[0].from_node], set()
    while stack:
        n = stack.pop()
        if n in seen:
            continue
        seen.add(n)
        if n.type == "TEX_IMAGE" and n.image:
            return n.image
        for i in n.inputs:
            for l in i.links:
                stack.append(l.from_node)
    return None


def _plain_rgb(m):
    b = _principled(m)
    if b and not b.inputs["Base Color"].is_linked:
        return tuple(b.inputs["Base Color"].default_value[:3])
    return tuple(m.diffuse_color[:3])


def _new_mat(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = _principled(m)
    b.inputs["Roughness"].default_value = 1.0
    b.inputs["Metallic"].default_value = 0.0
    for k in ("Specular IOR Level", "Specular"):
        if k in b.inputs:
            b.inputs[k].default_value = 0.0
    return m, b


def _tex_mat(name, image, closest=False):
    m, b = _new_mat(name)
    t = m.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = image
    if closest:
        t.interpolation = "Closest"
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m


def _small_copy(img, name, out_dir):
    # build a fresh image from the pixels: .copy() of a packed image tries to re-read the (missing) temp file
    import numpy as np
    w, h = img.size
    buf = np.empty(w*h*4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    c = bpy.data.images.new(name, w, h, alpha=False)
    c.colorspace_settings.name = "sRGB"
    c.pixels.foreach_set(buf)
    if max(w, h) > TEX_SIZE:
        c.scale(TEX_SIZE, TEX_SIZE)
    # write to disk so the FBX exporter and Unity can use it (a generated image has no file otherwise)
    c.filepath_raw = os.path.join(out_dir, name + ".png")
    c.file_format = "PNG"
    c.save()
    return c


def apply_palette(names, out_dir):
    objs = [bpy.data.objects[n] for n in names]
    os.makedirs(out_dir, exist_ok=True)
    meshes = []
    for o in objs:
        if o.data not in meshes:
            meshes.append(o.data)

    # 1) classify materials and collect plain colours
    colours = {}                                   # srgb8 tuple -> cell index
    textured = {}                                  # source material -> simplified material
    sky_meshes = {o.data for o in objs if o.name == "Sky_Backdrop"}
    for me in meshes:
        if me in sky_meshes:
            continue
        for m in me.materials:
            if not m or m in textured:
                continue
            img = _base_image(m)
            if img:
                textured[m] = _tex_mat("M_" + m.name.replace(" ", "_"), _small_copy(img, os.path.splitext(img.name)[0] + "_s", out_dir))
            else:
                key = tuple(_srgb8(c) for c in _plain_rgb(m))
                colours.setdefault(key, len(colours))
    if len(colours) > GRID*GRID:
        raise RuntimeError(f"palette overflow: {len(colours)} colours")

    # 2) atlas png (written by hand so the sRGB bytes are exact)
    os.makedirs(out_dir, exist_ok=True)
    fill = (0xF6, 0xEA, 0xD2)
    cells = [fill]*(GRID*GRID)
    for key, i in colours.items():
        cells[i] = key
    rows = []
    for r in range(GRID):
        line = b"".join(bytes(cells[r*GRID + c]) * CELL for c in range(GRID))
        rows += [line]*CELL
    png_path = os.path.join(out_dir, "Palette_Base.png")
    _png(png_path, GRID*CELL, GRID*CELL, rows)
    with open(os.path.join(out_dir, "palette_map.json"), "w") as f:
        json.dump({"size": GRID*CELL, "cell": CELL, "colours": {"#%02X%02X%02X" % k: i for k, i in colours.items()}}, f, indent=1)

    atlas = bpy.data.images.load(png_path, check_existing=False)
    atlas.colorspace_settings.name = "sRGB"
    atlas.alpha_mode = "NONE"
    atlas.pack()
    m_pal = _tex_mat("M_Palette", atlas, closest=True)

    m_sky, b = _new_mat("M_Sky")
    b.inputs["Base Color"].default_value = (1, 0.88, 0.66, 1)
    b.inputs["Emission Color"].default_value = (1, 0.88, 0.66, 1)
    b.inputs["Emission Strength"].default_value = 3.0

    # 3) rewrite every mesh
    def centre(i):
        c, r = i % GRID, i // GRID
        return ((c + 0.5)/GRID, 1.0 - (r + 0.5)/GRID)

    for me in meshes:
        if me in sky_meshes:
            me.materials.clear(); me.materials.append(m_sky)
            continue
        uv = me.uv_layers[0] if me.uv_layers else me.uv_layers.new(name="UVMap")
        old = [me.materials[p.material_index] if p.material_index < len(me.materials) else None for p in me.polygons]
        new_slots, idx = [], []
        for p, m in zip(me.polygons, old):
            if m in textured:
                target = textured[m]
            else:
                target = m_pal
                rgb = _plain_rgb(m) if m else (0.96, 0.92, 0.82)
                u, v = centre(colours.get(tuple(_srgb8(c) for c in rgb), 0))
                for li in range(p.loop_start, p.loop_start + p.loop_total):
                    uv.data[li].uv = (u, v)
            if target not in new_slots:
                new_slots.append(target)
            idx.append(new_slots.index(target))
        me.materials.clear()
        for m in new_slots:
            me.materials.append(m)
        for p, i in zip(me.polygons, idx):
            p.material_index = i
    used = {m.name for me in meshes for m in me.materials if m}
    print(f"palette: {len(colours)} colours -> {sorted(used)}")
    return png_path
