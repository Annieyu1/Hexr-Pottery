"""T11: make the label and cloth textures look more natural, then put them on the existing models.

Run inside Blender with art/blender/HexR_T11_Bottles.blend open (MCP or headless):
    exec(open("<repo>/art/scripts/make_t11_textures.py").read())
Needs the clean labels rendered by build_t11_bottles.py (art/textures/t11/label_*.png).

  * labels: paper grain, a few paper fibres, slightly uneven ink, soft warm edges  -> label_*_nat.png
  * cloth:  plain-weave gingham (warp/weft threads, per-thread colour variation, rounded threads, fuzz)
            -> gingham_jar_nat.png
  * the T11_Label_* and T11_Gingham materials are pointed at the new images; geometry is untouched.
Everything is generated here (numpy), no third-party textures.
"""
import bpy, os
import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(bpy.data.filepath))))
TEX = os.path.join(REPO, "art", "textures", "t11")
rng = np.random.default_rng(7)


def load(path):
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.empty(w*h*4, np.float32); im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)[..., :3].copy()          # rows bottom->top, sRGB 0..1

def save(arr, path):
    h, w, _ = arr.shape
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    im.colorspace_settings.name = 'sRGB'
    rgba = np.concatenate([np.clip(arr, 0, 1), np.ones((h, w, 1), np.float32)], axis=2)
    im.pixels.foreach_set(rgba.astype(np.float32).ravel())
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im)

def blur(a, r):
    """box blur along both axes (separable, wraps), r in pixels"""
    if r < 1: return a
    out = a
    for ax in (0, 1):
        c = np.cumsum(np.concatenate([out.take(range(-r, 0), axis=ax), out, out.take(range(0, r+1), axis=ax)], axis=ax), axis=ax)
        n = out.shape[ax]
        hi = c.take(range(2*r+1, 2*r+1+n), axis=ax); lo = c.take(range(0, n), axis=ax)
        out = (hi - lo) / (2*r+1)
    return out

def noise(h, w, scales=((4, 0.5), (16, 0.3), (64, 0.2))):
    """tileable-ish value noise, mean 0, roughly -1..1"""
    out = np.zeros((h, w), np.float32)
    for cell, amp in scales:
        g = rng.standard_normal((h//cell + 2, w//cell + 2)).astype(np.float32)
        up = np.kron(g, np.ones((cell, cell), np.float32))[:h, :w]
        out += amp * blur(up, max(1, cell//2))
    return out / (np.abs(out).max() + 1e-6)


# ------------------------------------------------------------------ labels: paper + print
def naturalise_label(src, dst, warm=(1.0, 0.985, 0.95)):
    a = load(src); h, w, _ = a.shape
    bg = np.median(a.reshape(-1, 3), axis=0)
    ink = np.clip(np.abs(a - bg).sum(axis=2) * 3.0, 0, 1)[..., None]          # 0 = paper, 1 = printed

    grain = noise(h, w, ((2, 0.5), (6, 0.3), (24, 0.2)))[..., None]
    a = a * (1 + 0.035*grain)                                                  # paper grain everywhere

    # paper fibres: short faint strokes
    fib = np.zeros((h, w), np.float32)
    for _ in range(260):
        y, x = rng.integers(0, h), rng.integers(0, w)
        L = rng.integers(6, 22); ang = rng.uniform(0, np.pi)
        for t in range(L):
            yy, xx = int(y + t*np.sin(ang)), int(x + t*np.cos(ang))
            if 0 <= yy < h and 0 <= xx < w: fib[yy, xx] = rng.choice([-1.0, 1.0])
    a = a * (1 + 0.04*blur(fib, 1)[..., None])

    # ink: slightly uneven coverage + softened edges (a print, not a vector)
    soft = blur(a, 1)
    a = a*(1 - 0.5*ink) + soft*(0.5*ink)
    patchy = noise(h, w, ((8, 0.6), (32, 0.4)))[..., None]
    a = a + ink*(bg - a)*np.clip(0.10 + 0.08*patchy, 0, 0.25)                 # ink a little thinner in places

    # warm ageing toward the top/bottom edges + a faint darker rim where the label is glued
    yy = np.linspace(0, 1, h)[:, None, None]
    edge = np.clip(1 - np.minimum(yy, 1 - yy)/0.12, 0, 1)
    a = a * (1 - 0.06*edge) * np.array(warm)**(1 + 2*edge)
    a = a * (1 - 0.10*np.clip(1 - np.minimum(yy, 1 - yy)/0.02, 0, 1))
    save(a, dst)


# ------------------------------------------------------------------ gingham: woven plain weave
def gingham(dst, col=(0.90, 0.50, 0.46), size=512, checks=8, threads_per_check=10):
    t = size // (checks*threads_per_check)              # pixels per thread
    n = size // t + 1                                     # +1: the last partial thread
    white = np.array((0.98, 0.96, 0.92))
    c = np.array(col)
    # thread colours: alternate colour bands of `threads_per_check` threads, each thread slightly different
    band = (np.arange(n)//threads_per_check) % 2
    warp = np.where(band[:, None] == 1, c, white) * (1 + rng.normal(0, 0.035, (n, 1)))
    weft = np.where(band[:, None] == 1, c, white) * (1 + rng.normal(0, 0.035, (n, 1)))
    ix = np.arange(size)//t; iy = np.arange(size)//t
    WX = warp[ix][None, :, :].repeat(size, 0)            # vertical threads (vary along x)
    WY = weft[iy][:, None, :].repeat(size, 1)            # horizontal threads (vary along y)
    over = ((ix[None, :] + iy[:, None]) % 2 == 0)[..., None]       # plain weave: alternate which thread is on top
    top = np.where(over, WX, WY); under = np.where(over, WY, WX)
    a = 0.78*top + 0.22*under                            # coloured threads crossing -> darker squares
    # rounded threads: brighter in the middle of each thread, darker at the gaps
    px = (np.arange(size) % t + 0.5)/t; py = (np.arange(size) % t + 0.5)/t
    roundx = np.sin(np.pi*px)[None, :]; roundy = np.sin(np.pi*py)[:, None]
    shade = np.where(over[..., 0], roundx, roundy)
    a = a * (0.80 + 0.22*shade)[..., None]
    a = a * (1 + 0.05*noise(size, size, ((2, 0.6), (8, 0.4)))[..., None])     # fuzz
    a = a * (1 + 0.04*noise(size, size, ((64, 1.0),))[..., None])             # gentle unevenness over the cloth
    save(a, dst)


# ------------------------------------------------------------------ make + apply
pairs = {}
for g in ("tight", "loose", "jar"):
    src = os.path.join(TEX, f"label_{g}.png"); dst = os.path.join(TEX, f"label_{g}_nat.png")
    naturalise_label(src, dst); pairs[f"label_{g}"] = dst
gdst = os.path.join(TEX, "gingham_jar_nat.png"); gingham(gdst)

def repoint(mat_name, path):
    m = bpy.data.materials.get(mat_name)
    if not m: print("missing", mat_name); return
    for n in m.node_tree.nodes:
        if n.type == "TEX_IMAGE":
            n.image = bpy.data.images.load(path, check_existing=True)
            print("textured", mat_name, "->", os.path.basename(path))
repoint("T11_Label_Bottle_Tight", pairs["label_tight"])
repoint("T11_Label_Bottle_Loose", pairs["label_loose"])
repoint("T11_Label_Jar", pairs["label_jar"])
repoint("T11_Gingham", gdst)
