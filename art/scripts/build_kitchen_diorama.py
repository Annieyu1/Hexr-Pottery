"""Kitchen diorama: a small, cozy, fresh kitchen (walls come later). Saves art/blender/kitchen.blend.

Run headless:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python art/scripts/build_kitchen_diorama.py
Or inside a live Blender (wipes the open scene and rebuilds it; SAVE=False skips saving):
    exec(open(P).read(), {"__file__": P, "SAVE": False})

Style: soft low-poly, a little more refined. Every hard edge is bevelled and shaded smooth with weighted
normals, so nothing reads as a sharp edge; colour is a soft vertex-colour gradient (darker at the foot,
lighter at the top) on a handful of shared materials, so it can later bake into a palette atlas like the
garden. Palette: sage cabinets, honey wood, cream appliances, coral / butter / sky accents (garden accents).

Reference split: layout from an isometric L-kitchen diorama; props from a small stove-and-counter vignette;
colour and softness from a warm isometric bedroom render. Everything here is modelled from scratch.

Metres, Z up. Table centre = world origin, table top 0.74 m. The seated player sits at the open front (-Y)
facing +Y, eye at (0, -0.9, 1.15); the 0.9 x 0.5 m operation zone in the middle of the table stays clear.
Future walls: back wall plane y = 1.70, left wall plane x = -1.90, ceiling assumed at 2.60. Things that hang
on a wall or the ceiling are built now but sit in the hidden collection Kitchen_WallMounted.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "art", "blender", "kitchen.blend")
SAVE = globals().get("SAVE", True)


def reset():
    if bpy.app.background:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        return bpy.context.scene
    # live session: keep the UI and add-ons, drop the scene contents
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.worlds, bpy.data.curves):
        for b in list(blocks):
            if b.users == 0:
                blocks.remove(b)
    return bpy.context.scene


sc = reset()


# ------------------------------------------------------------------ colour helpers (hex is sRGB)
def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def hx(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16)/255 for i in (0, 2, 4))
def C(c): return hx(c) if isinstance(c, str) else tuple(c)
def tolin(c): return tuple(lin(x) for x in c)
def mix(a, b, t): return tuple(a[i]+(b[i]-a[i])*t for i in range(3))
def sm(t): t = max(0.0, min(1.0, t)); return t*t*(3-2*t)
WARM_W = (1.0, 0.99, 0.975)
def shade(c, a):
    """a > 0 lifts toward warm white, a < 0 darkens with a slightly warm (red-leaning) shadow."""
    c = C(c)
    if a >= 0: return mix(c, WARM_W, a)
    a = -a; return (c[0]*(1-a*0.8), c[1]*(1-a), c[2]*(1-a*1.15))
def grad(col, d=0.12, l=0.05):
    if isinstance(col, (tuple, list)) and len(col) == 2: return C(col[0]), C(col[1])
    return shade(col, -d), shade(col, l)

Z = Vector((0, 0, 1))
SUN = Vector((-0.30, -0.80, 0.62)).normalized()    # toward the sun: open front, a little left; shadows fall back toward the future walls


# ------------------------------------------------------------------ materials: all read the "Col" vertex colour
MAT_SPEC = {  # roughness, metallic, specular, emission strength
    "Matte": (0.80, 0.0, 0.30, 0.0),
    "Satin": (0.36, 0.0, 0.50, 0.0),
    "Metal": (0.30, 0.75, 0.50, 0.0),
    "Glass": (0.08, 0.0, 0.60, 0.0),
    "Leaf":  (0.60, 0.0, 0.35, 0.0),
    "Glow":  (0.50, 0.0, 0.30, 2.5),
}
def MAT(key):
    name = "K_" + key
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    a = nt.nodes.new("ShaderNodeAttribute"); a.attribute_name = "Col"; a.location = (-400, 200)
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    rough, metal, spec, emit = MAT_SPEC[key]
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = spec
    if emit:
        nt.links.new(a.outputs["Color"], b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (0.8, 0.75, 0.68, 1)
    return m

COLL = {}
def coll(name):
    if name not in COLL:
        c = bpy.data.collections.new(name); sc.collection.children.link(c); COLL[name] = c
    return COLL[name]


# ------------------------------------------------------------------ geometry helpers
def spline(pts, n=4):
    """Catmull-Rom through pts, n samples per span."""
    P = [Vector(p) for p in pts]; out = []
    for i in range(len(P)-1):
        p0, p1, p2, p3 = P[max(i-1, 0)], P[i], P[i+1], P[min(i+2, len(P)-1)]
        for k in range(n):
            t = k/n; t2 = t*t; t3 = t2*t
            out.append(0.5*((2*p1) + (-p0+p2)*t + (2*p0-5*p1+4*p2-p3)*t2 + (-p0+3*p1-3*p2+p3)*t3))
    out.append(P[-1])
    return out

def arc_xz(cx, cz, r, a0, a1, n, y=0.0):
    return [(cx + r*math.cos(math.radians(a0 + (a1-a0)*k/n)), y, cz + r*math.sin(math.radians(a0 + (a1-a0)*k/n))) for k in range(n+1)]

def look_rot(direction):
    """Euler (degrees) turning +Z toward direction."""
    e = Vector(direction).normalized().to_track_quat('Z', 'Y').to_euler()
    return tuple(math.degrees(a) for a in e)

def _xf(tb, rot=None, loc=(0, 0, 0)):
    if rot is not None:
        if isinstance(rot, (int, float)): rot = (0, 0, rot)
        R = Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
        bmesh.ops.transform(tb, matrix=R, verts=tb.verts)
    bmesh.ops.translate(tb, vec=Vector(loc), verts=tb.verts)


class K:
    """One object, many soft parts. Each part gets its own vertex-colour gradient along `axis`."""
    def __init__(self, name, cname, loc=(0, 0, 0), rot=0.0, ao=True):
        self.name, self.cname, self.loc, self.rot, self.ao = name, cname, Vector(loc), rot, ao
        self.bm = bmesh.new(); self.col = self.bm.loops.layers.float_color.new("Col"); self.mats = []

    def _mi(self, key):
        if key not in self.mats: self.mats.append(key)
        return self.mats.index(key)

    def _add(self, tb, key, col, axis="z", vcol=None, d=0.12, l=0.05):
        c0, c1 = grad(col, d, l)
        vs = list(tb.verts)
        if not vs: tb.free(); return
        if axis in ("x", "y", "z"):
            ai = "xyz".index(axis); lo = min(v.co[ai] for v in vs); hi = max(v.co[ai] for v in vs)
        elif axis == "r":
            cx = sum(v.co.x for v in vs)/len(vs); cy = sum(v.co.y for v in vs)/len(vs)
            lo = 0.0; hi = max(math.hypot(v.co.x-cx, v.co.y-cy) for v in vs)
        else:
            lo, hi = 0.0, 1.0
        span = max(hi - lo, 1e-6)
        vmap = {v: self.bm.verts.new(v.co) for v in vs}
        mi = self._mi(key)
        for f in tb.faces:
            try: nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError: continue
            nf.material_index = mi; nf.smooth = True
            for lp, ol in zip(nf.loops, f.loops):
                v = ol.vert
                if vcol is not None and vcol.get(v) is not None: c = vcol[v]
                elif axis in ("x", "y", "z"): c = mix(c0, c1, sm((v.co[ai]-lo)/span))
                elif axis == "r": c = mix(c0, c1, sm(1 - math.hypot(v.co.x-cx, v.co.y-cy)/span))
                else: c = mix(c0, c1, 0.5)
                if self.ao:   # soft contact shadow near the floor
                    c = mix(shade(c, -0.12), c, sm((v.co.z + self.loc.z)/0.22))
                lp[self.col] = (*tolin(c), 1.0)
        tb.free()

    # ---- primitives
    def rbox(self, size, loc, col, key="Matte", r=0.02, seg=3, rot=None, base=True, axis="z", taper=None, d=0.12, l=0.05):
        tb = bmesh.new(); bmesh.ops.create_cube(tb, size=1.0)
        sx, sy, sz = size
        for v in tb.verts:
            v.co.x *= sx; v.co.y *= sy; v.co.z *= sz
            if taper and v.co.z > 0: v.co.x *= taper[0]; v.co.y *= taper[1]
        r = min(r, min(size)*0.49)
        if r > 1e-4:
            bmesh.ops.bevel(tb, geom=list(tb.edges), offset=r, offset_type='OFFSET', segments=seg, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
        if base: bmesh.ops.translate(tb, vec=(0, 0, sz/2), verts=tb.verts)
        _xf(tb, rot, loc); self._add(tb, key, col, axis, d=d, l=l)

    def cyl(self, r1, r2, h, loc, col, key="Matte", seg=24, r=0.0, bseg=2, rot=None, base=True, axis="z", d=0.12, l=0.05):
        tb = bmesh.new()
        bmesh.ops.create_cone(tb, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h)
        if r > 1e-4:
            es = [e for e in tb.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > 0.6]
            rr = min(r, h*0.45, max(min(r1, r2), r*0.5)*0.9)
            if es: bmesh.ops.bevel(tb, geom=es, offset=rr, offset_type='OFFSET', segments=bseg, profile=0.5,
                                   affect='EDGES', clamp_overlap=True)
        if base: bmesh.ops.translate(tb, vec=(0, 0, h/2), verts=tb.verts)
        _xf(tb, rot, loc); self._add(tb, key, col, axis, d=d, l=l)

    def lathe(self, prof, loc, col, key="Satin", seg=32, rot=None, axis="z", cols=None, d=0.12, l=0.05):
        """Surface of revolution around Z. prof = [(radius, z), ...]; radius 0 makes a pole."""
        tb = bmesh.new(); angs = [i*math.tau/seg for i in range(seg)]
        rings = []; vcol = {} if cols else None
        for k, (rr, z) in enumerate(prof):
            ring = [tb.verts.new((0, 0, z))] if rr < 1e-6 else [tb.verts.new((rr*math.cos(a), rr*math.sin(a), z)) for a in angs]
            if cols and cols[k] is not None:
                for v in ring: vcol[v] = C(cols[k])
            rings.append(ring)
        for A, B in zip(rings, rings[1:]):
            if len(A) == 1 and len(B) == 1: continue
            for i in range(seg):
                j = (i+1) % seg
                if len(A) == 1: q = (A[0], B[j], B[i])
                elif len(B) == 1: q = (A[i], A[j], B[0])
                else: q = (A[i], A[j], B[j], B[i])
                try: tb.faces.new(q)
                except ValueError: pass
        _xf(tb, rot, loc); self._add(tb, key, col, axis, vcol=vcol, d=d, l=l)

    def sweep(self, pts, rad, loc, col, key="Matte", n=10, closed=False, cap=True, rot=None, axis="z", flat=1.0, d=0.12, l=0.05):
        """Tube along pts (radius may be a list, one per point)."""
        tb = bmesh.new(); P = [Vector(p) for p in pts]; N = len(P)
        R = list(rad) if isinstance(rad, (list, tuple)) else [rad]*N
        T = []
        for i in range(N):
            a, b = (P[i-1], P[(i+1) % N]) if closed else (P[max(i-1, 0)], P[min(i+1, N-1)])
            T.append((b - a).normalized())
        ref = Z if abs(T[0].dot(Z)) < 0.95 else Vector((1, 0, 0))
        nv = (ref - T[0]*ref.dot(T[0])).normalized()
        rings = []
        for i in range(N):
            nv = (nv - T[i]*nv.dot(T[i])).normalized()
            bv = T[i].cross(nv)
            rings.append([tb.verts.new(P[i] + (nv*math.cos(k*math.tau/n)*flat + bv*math.sin(k*math.tau/n))*R[i]) for k in range(n)])
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
        for A, B in pairs:
            for k in range(n):
                tb.faces.new((A[k], A[(k+1) % n], B[(k+1) % n], B[k]))
        if not closed and cap:
            for ring, p, tv, s in ((rings[0], P[0], -T[0], R[0]), (rings[-1], P[-1], T[-1], R[-1])):
                pole = tb.verts.new(p + tv*s*0.6)
                for k in range(n): tb.faces.new((ring[k], ring[(k+1) % n], pole))
        bmesh.ops.recalc_face_normals(tb, faces=tb.faces)
        _xf(tb, rot, loc); self._add(tb, key, col, axis, d=d, l=l)

    def blob(self, r, loc, col, key="Matte", sq=(1, 1, 1), u=16, v=10, rot=None, axis="z", jit=0.0, seed=0, d=0.12, l=0.05):
        tb = bmesh.new(); bmesh.ops.create_uvsphere(tb, u_segments=u, v_segments=v, radius=r)
        rnd = random.Random(seed)
        for p in tb.verts:
            if jit: p.co *= 1 + rnd.uniform(-jit, jit)
            p.co.x *= sq[0]; p.co.y *= sq[1]; p.co.z *= sq[2]
        _xf(tb, rot, loc); self._add(tb, key, col, axis, d=d, l=l)

    def leaf(self, base, direction, length, width, bend, col, segs=8, n=8, thick=0.2, arch=0.12, roll=0.0, key="Leaf"):
        """Chunky, soft leaf: a lens cross-section swept along a drooping curve; lighter toward the tip and on top."""
        c0, c1 = grad(col)
        dv = Vector(direction).normalized()
        side = dv.cross(Z); side = side.normalized() if side.length > 1e-4 else Vector((1, 0, 0))
        up = side.cross(dv).normalized()
        if roll:
            side, up = side*math.cos(roll) + up*math.sin(roll), up*math.cos(roll) - side*math.sin(roll)
        tb = bmesh.new(); vcol = {}; rings = []
        for i in range(segs+1):
            u = i/segs
            c = Vector(base) + dv*length*u - Z*(bend*length*u*u)
            w = width*(math.sin(math.pi*min(1.0, 0.10 + 0.90*u))**0.8)*(1 - 0.2*u)
            th = max(w*thick, 0.0015)
            base_c = mix(c0, c1, sm(0.1 + 0.9*u))
            if i == segs:
                p = tb.verts.new(c); vcol[p] = base_c; rings.append([p]); continue
            ring = []
            for k in range(n):
                a = k*math.tau/n
                p = tb.verts.new(c + side*math.cos(a)*w + up*(math.sin(a)*th + arch*w*(1 - math.cos(a)**2)))
                vcol[p] = base_c if math.sin(a) >= -1e-6 else shade(base_c, -0.15)
                ring.append(p)
            rings.append(ring)
        stem = tb.verts.new(Vector(base) - dv*0.01); vcol[stem] = c0
        for k in range(n): tb.faces.new((rings[0][(k+1) % n], rings[0][k], stem))
        for A, B in zip(rings, rings[1:]):
            for k in range(n):
                if len(B) == 1: tb.faces.new((A[k], A[(k+1) % n], B[0]))
                else: tb.faces.new((A[k], A[(k+1) % n], B[(k+1) % n], B[k]))
        bmesh.ops.recalc_face_normals(tb, faces=tb.faces)
        self._add(tb, key, col, vcol=vcol)

    def basin(self, size, loc, col, wall=0.04, depth=0.19, r=0.012, key="Matte"):
        """Rounded box with a pocket sunk into its top (farmhouse sink)."""
        tb = bmesh.new(); bmesh.ops.create_cube(tb, size=1.0)
        for v in tb.verts: v.co.x *= size[0]; v.co.y *= size[1]; v.co.z = (v.co.z + 0.5)*size[2]
        top = max(tb.faces, key=lambda f: f.calc_center_median().z)
        bmesh.ops.inset_region(tb, faces=[top], thickness=wall, depth=0.0, use_even_offset=True)
        ex = bmesh.ops.extrude_face_region(tb, geom=[top])
        nv = [g for g in ex["geom"] if isinstance(g, bmesh.types.BMVert)]
        bmesh.ops.translate(tb, vec=(0, 0, -depth), verts=nv)
        if top.is_valid and top.calc_center_median().z > size[2] - 1e-4:
            bmesh.ops.delete(tb, geom=[top], context='FACES_ONLY')
        es = [e for e in tb.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > 0.6]
        bmesh.ops.bevel(tb, geom=es, offset=r, offset_type='OFFSET', segments=3, profile=0.5, affect='EDGES', clamp_overlap=True)
        bmesh.ops.recalc_face_normals(tb, faces=tb.faces)
        c0, c1 = grad(col); vcol = {}
        for v in tb.verts:      # pocket: a shade deeper toward the bottom so it reads as a basin
            if abs(v.co.x) < size[0]/2 - wall*0.5 and abs(v.co.y) < size[1]/2 - wall*0.5 and v.co.z < size[2] - 0.004:
                t = (v.co.z - (size[2] - depth))/depth
                vcol[v] = mix(shade(c1, -0.30), shade(c1, -0.06), sm(t))
        _xf(tb, None, loc); self._add(tb, key, col, vcol=vcol)

    def curtain(self, x0, x1, y, z0, z1, col, folds=5, amp=0.025, key="Matte"):
        tb = bmesh.new(); nx, nz = folds*8, 10
        grid = []
        for kz in range(nz+1):
            row = []
            for ix in range(nx+1):
                u = ix/nx; t = kz/nz
                z = z1 - (z1 - z0)*t
                a = amp*(0.75 + 0.5*t)
                row.append(tb.verts.new((x0 + (x1 - x0)*u, y + a*math.sin(math.tau*folds*u), z)))
            grid.append(row)
        fs = [tb.faces.new((grid[k][i], grid[k][i+1], grid[k+1][i+1], grid[k+1][i])) for k in range(nz) for i in range(nx)]
        bmesh.ops.solidify(tb, geom=fs, thickness=0.01)
        bmesh.ops.recalc_face_normals(tb, faces=tb.faces)
        self._add(tb, key, col)

    def finish(self, weighted=True):
        me = bpy.data.meshes.new(self.name); self.bm.to_mesh(me); self.bm.free()
        for key in self.mats: me.materials.append(MAT(key))
        o = bpy.data.objects.new(self.name, me)
        o.location = self.loc; o.rotation_euler = (0, 0, math.radians(self.rot))
        coll(self.cname).objects.link(o)
        if weighted:
            wn = o.modifiers.new("SoftNormals", 'WEIGHTED_NORMAL'); wn.keep_sharp = True; wn.weight = 50
        return o


# ------------------------------------------------------------------ palette (sRGB; pairs are foot -> top)
SAGE = ("6FA375", "A4D39A")          # cabinets: fresh sage green
SAGE_P = ("80B183", "B4DDA9")        # raised door panels
KICK = ("527D56", "6A976A")          # toe kick
WOOD = ("D3A46D", "F0CC96")          # counter tops (butcher block)
OAK = ("BC8752", "DDAE78")           # table base, chair frames
TBL = ("CFA06B", "EDCB98")           # table top
CREAM = ("DAD8D3", "FFFFFF")         # stove, sink, crocks: clean white, not cream
FRIDGE = ("5FA7CC", "A3D5EE")        # retro fridge: sky blue
PAPER = ("ECEBE7", "FFFFFF")
BRASS = ("B5843A", "EDC97B")
CHAR = ("343B3B", "54605F")          # cooktop, burners
GLASS_D = ("2E2B2B", "57504E")       # oven window
CORAL = ("E0604C", "F89C85")
BUTTER = ("F1B42A", "FFDA5E")
SKY = ("5BA8D0", "9DD3EE")
MINT = ("58B996", "A0E1C3")
TERRA = ("C95E37", "EE9764")
SOIL = "5E3E2A"
LEAF = ("2C7E4B", "92D05C")
LEAF2 = ("448A2E", "B6DC55")
ORANGE = ("EE7218", "FFB446")
SLAB = ("D8D3CC", "F7F5F1")
PLANKS = ["DCAC79", "D4A36F", "E2B685", "D09E6A", "D9A874"]

X0, X1, Y0, Y1 = -1.90, 1.40, -1.50, 1.70     # floor; future walls stand on the back (y = Y1) and left (x = X0) edges
CT = 0.90                                      # counter top height
RUGZ = 0.012                                   # rug thickness: table and chairs stand on it


# ------------------------------------------------------------------ floor: thick cream base + honey planks, round mint rug
fl = K("Floor", "Kitchen_Floor", ao=False)
fl.rbox((X1-X0, Y1-Y0, 0.15), ((X0+X1)/2, (Y0+Y1)/2, -0.18), SLAB, r=0.04, seg=3)
rng = random.Random(11); PW = 0.20
for i in range(int(round((Y1-Y0)/PW))):
    y = Y0 + PW*(i+0.5); x = X0; L = rng.uniform(0.3, 1.1)
    while x < X1 - 1e-3:
        x2 = min(X1, x + L)
        if X1 - x2 < 0.3: x2 = X1
        c = hx(rng.choice(PLANKS))
        near = sm(min((x+x2)/2 - X0, Y1 - y)/0.8)          # a touch darker toward the future walls
        c = mix(shade(c, -0.10), c, near)
        pair = (shade(c, -0.06), shade(c, 0.05))
        fl.rbox((x2-x-0.005, PW-0.005, 0.035), ((x+x2)/2, y, -0.035), pair if rng.random() < 0.5 else pair[::-1],
                r=0.008, seg=2, axis="x")
        x = x2; L = rng.uniform(0.8, 1.6)
fl.finish()

rug = K("Rug", "Kitchen_Floor", loc=(0.0, -0.05, 0.0), ao=False)
rug.lathe([(0, RUGZ), (0.50, RUGZ), (0.53, RUGZ), (0.74, RUGZ), (0.76, RUGZ), (0.80, RUGZ), (0.82, RUGZ), (0.98, RUGZ),
           (1.03, RUGZ*0.85), (1.05, RUGZ*0.35), (1.04, 0.0), (0, 0.0)],
          (0, 0, 0), MINT, key="Matte", seg=64,
          cols=["D2F0E1", "C6EBD8", "7FCDAF", "76C7A8", "FAFAF7", "FAFAF7", "5CB794", "57B18E", "52AB89", "4A9F7F", "4A9F7F", "4A9F7F"])
rug.finish(weighted=False)


# ------------------------------------------------------------------ cabinets (built in a wall frame: wall at local y = 0, fronts face -y)
def knob(k, x, z, y=-0.604, col=BRASS):
    k.lathe([(0, 0), (0.008, 0), (0.007, 0.012), (0.016, 0.019), (0.017, 0.026), (0.012, 0.032), (0, 0.033)],
            (x, y, z), col, key="Metal", seg=12, rot=(90, 0, 0))

def bar_pull(k, x, z, L=0.12, y=-0.604):
    k.sweep(spline([(x-L/2, y, z), (x-L/2, y-0.013, z), (x-L/2+0.012, y-0.021, z), (x+L/2-0.012, y-0.021, z),
                    (x+L/2, y-0.013, z), (x+L/2, y, z)], 2), 0.0065, (0, 0, 0), BRASS, key="Metal", n=8)

def front(k, fx, fw, z0, z1, pull=None, kx=0.0):
    h = z1 - z0
    k.rbox((fw, 0.024, h), (fx, -0.592, z0), SAGE, r=0.010, seg=3)
    if h > 0.2 and fw > 0.2:
        k.rbox((fw - 0.11, 0.010, h - 0.11), (fx, -0.604, z0 + 0.055), SAGE_P, r=0.005, seg=2)
    if pull == "bar": bar_pull(k, fx, z0 + h*0.5, L=min(0.14, fw*0.4), y=-0.604)
    elif pull == "knob": knob(k, fx + kx, z1 - 0.075, y=-0.604)

def cab_unit(k, x0, x1, kind):
    w = x1 - x0; cx = (x0 + x1)/2
    k.rbox((w - 0.006, 0.56, 0.54 if kind == "sink" else 0.74), (cx, -0.30, 0.10), SAGE, r=0.012)   # sink unit stops under the basin
    k.rbox((w - 0.006, 0.50, 0.10), (cx, -0.27, 0.0), KICK, r=0.01, seg=2)
    if kind == "blank": return
    if kind == "drawers3":
        for z0, z1 in ((0.11, 0.36), (0.38, 0.62), (0.64, 0.86)):
            front(k, cx, w - 0.008, z0, z1, "bar")
        return
    top = 0.62 if kind == "sink" else 0.68
    if kind == "doors2":
        front(k, cx, w - 0.008, 0.70, 0.86, "bar")
    hw = (w - 0.012)/2
    front(k, cx - hw/2 - 0.002, hw, 0.11, top, "knob", kx=hw/2 - 0.05)
    front(k, cx + hw/2 + 0.002, hw, 0.11, top, "knob", kx=-(hw/2 - 0.05))

SX = -0.905                      # sink centre x
cb = K("Cabinets_Back", "Kitchen_Counters", loc=(0, Y1, 0))
cab_unit(cb, -1.30, -0.48, "sink")
cab_unit(cb, 0.16, 0.62, "drawers3")
cb.finish()

cl = K("Cabinets_Left", "Kitchen_Counters", loc=(X0, 0, 0), rot=90)   # local x = world y, fronts face +x
cab_unit(cl, 0.20, 1.10, "doors2")
cab_unit(cl, 1.10, 1.70, "blank")
cl.finish()

# counter tops: honey butcher block, 3 cm overhang
ct = K("Countertop", "Kitchen_Counters")
FY = Y1 - 0.635
def slab(x0, x1, y0, y1): ct.rbox((x1-x0, y1-y0, 0.04), ((x0+x1)/2, (y0+y1)/2, CT - 0.04), WOOD, r=0.012, seg=3)
slab(X0, SX - 0.35, 0.185, Y1)                 # left run incl. the corner
slab(SX - 0.35, SX + 0.35, Y1 - 0.12, Y1)      # strip behind the sink (faucet)
slab(SX + 0.35, -0.475, FY, Y1)                # narrow strip between sink and stove
slab(0.155, 0.645, FY, Y1)                     # over the drawers
ct.finish()

# farmhouse sink + gooseneck faucet
sk = K("Sink", "Kitchen_Counters")
sk.basin((0.70, 0.50, 0.255), (SX, Y1 - 0.62 + 0.25, CT + 0.004 - 0.255), CREAM, wall=0.035, depth=0.19, r=0.012)
sk.cyl(0.032, 0.032, 0.006, (SX, Y1 - 0.62 + 0.25, CT + 0.004 - 0.19 - 0.003), BRASS, key="Metal", seg=20, r=0.002)
sk.cyl(0.02, 0.02, 0.006, (SX, Y1 - 0.62 + 0.25, CT + 0.004 - 0.19 - 0.001), ("4A4542", "6A625C"), seg=16, r=0.002)
fx, fy = SX, Y1 - 0.065
sk.cyl(0.028, 0.025, 0.035, (fx, fy, CT), BRASS, key="Metal", seg=20, r=0.006)
sk.sweep(spline([(fx, fy, CT+0.03), (fx, fy, CT+0.20), (fx, fy-0.012, CT+0.275), (fx, fy-0.06, CT+0.31),
                 (fx, fy-0.115, CT+0.29), (fx, fy-0.145, CT+0.24), (fx, fy-0.15, CT+0.20)], 4),
         [0.013]*25, (0, 0, 0), BRASS, key="Metal", n=10)
sk.cyl(0.016, 0.015, 0.03, (fx+0.055, fy, CT), BRASS, key="Metal", seg=14, r=0.004)
sk.sweep(spline([(fx+0.055, fy, CT+0.025), (fx+0.055, fy, CT+0.07), (fx+0.06, fy-0.035, CT+0.085)], 3), 0.006, (0, 0, 0), BRASS, key="Metal", n=8)
sk.finish()


# ------------------------------------------------------------------ cream range: oven, 4 burners, coral knobs; enamel pot + butter kettle
st = K("Stove", "Kitchen_Counters", loc=(-0.16, Y1 - 0.33, 0))
for sx in (-1, 1):
    for sy in (-1, 1):
        st.cyl(0.022, 0.026, 0.045, (sx*0.25, sy*0.24, 0), CHAR, seg=12, r=0.006)
st.rbox((0.62, 0.60, 0.83), (0, 0.0, 0.045), CREAM, r=0.03)
st.rbox((0.60, 0.58, 0.026), (0, 0.0, 0.872), CHAR, r=0.01, seg=2)
st.rbox((0.62, 0.07, 0.13), (0, 0.275, 0.87), CREAM, r=0.03)
for kx in (-0.21, -0.07, 0.07, 0.21):
    st.lathe([(0, 0), (0.022, 0), (0.024, 0.005), (0.024, 0.02), (0.019, 0.028), (0, 0.03)], (kx, -0.30, 0.80), CORAL, seg=16, rot=(90, 0, 0))
st.rbox((0.54, 0.03, 0.47), (0, -0.305, 0.20), CREAM, r=0.012)
st.rbox((0.38, 0.012, 0.22), (0, -0.322, 0.33), GLASS_D, key="Glass", r=0.006)
st.sweep(spline([(-0.22, -0.32, 0.71), (-0.22, -0.345, 0.71), (-0.20, -0.356, 0.71), (0.20, -0.356, 0.71), (0.22, -0.345, 0.71), (0.22, -0.32, 0.71)], 2),
         0.009, (0, 0, 0), BRASS, key="Metal", n=10)
tw = 0.15                                    # tea towel folded over the oven handle
st.rbox((tw, 0.05, 0.032), (0.11, -0.36, 0.69), ("ECEBE7", "FFFFFF"), r=0.015, seg=3)   # fold over the bar
for i, (z0, h, c) in enumerate(((0.47, 0.17, ("ECEBE7", "FFFFFF")), (0.64, 0.025, CORAL), (0.665, 0.03, ("ECEBE7", "FFFFFF")))):
    st.rbox((tw, 0.012, h), (0.11, -0.378, z0), c, r=0.0055, seg=2)
st.rbox((tw, 0.010, 0.11), (0.11, -0.342, 0.60), ("ECEBE7", "FFFFFF"), r=0.0045, seg=2)
st.rbox((tw, 0.012, 0.02), (0.11, -0.378, 0.50), CORAL, r=0.0055, seg=2)
st.rbox((0.54, 0.025, 0.10), (0, -0.30, 0.075), CREAM, r=0.01)
bar_pull(st, 0.0, 0.125, L=0.12, y=-0.312)
for bx, by in ((-0.15, -0.13), (0.15, -0.13), (-0.15, 0.12), (0.15, 0.12)):
    st.cyl(0.055, 0.05, 0.012, (bx, by, 0.897), CHAR, seg=24, r=0.004)
    st.cyl(0.036, 0.034, 0.012, (bx, by, 0.907), ("2C302F", "474E4C"), seg=20, r=0.005)
    st.sweep([(bx + 0.085*math.cos(a*math.tau/20), by + 0.085*math.sin(a*math.tau/20), 0.935) for a in range(20)],
             0.0055, (0, 0, 0), CHAR, closed=True, n=6)
    for a in (45, 135, 225, 315):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        st.sweep([(bx + 0.10*ca, by + 0.10*sa, 0.899), (bx + 0.10*ca, by + 0.10*sa, 0.935), (bx + 0.035*ca, by + 0.035*sa, 0.935)],
                 0.0055, (0, 0, 0), CHAR, n=6)
st.finish()

def pot(k, loc, rotz=0.0):
    R = Matrix.Rotation(math.radians(rotz), 4, 'Z')
    def P(p): return tuple(Vector(loc) + R @ Vector(p))
    k.lathe([(0, 0), (0.09, 0), (0.10, 0.01), (0.102, 0.03), (0.102, 0.10), (0.107, 0.106), (0.105, 0.113), (0, 0.113)],
            loc, CORAL, seg=32)
    k.lathe([(0.104, 0.112), (0.10, 0.122), (0.08, 0.139), (0.05, 0.149), (0, 0.152)], loc, ("F07E6A", "FDB6A4"), seg=32)
    k.lathe([(0, 0.15), (0.012, 0.15), (0.010, 0.16), (0.02, 0.171), (0.018, 0.18), (0, 0.182)], loc, CREAM, seg=14)
    for s in (-1, 1):
        k.sweep(spline([P((s*0.098, -0.03, 0.088)), P((s*0.128, -0.022, 0.09)), P((s*0.138, 0, 0.09)), P((s*0.128, 0.022, 0.09)), P((s*0.098, 0.03, 0.088))], 3),
                0.009, (0, 0, 0), CORAL, n=8)

def kettle(k, loc, rotz=0.0):
    R = Matrix.Rotation(math.radians(rotz), 4, 'Z')
    def P(p): return tuple(Vector(loc) + R @ Vector(p))
    k.lathe([(0, 0), (0.085, 0), (0.095, 0.01), (0.098, 0.03), (0.094, 0.08), (0.08, 0.125), (0.06, 0.15), (0.046, 0.158),
             (0.045, 0.165), (0, 0.166)], loc, BUTTER, seg=32)
    k.lathe([(0, 0.164), (0.012, 0.164), (0.010, 0.174), (0.018, 0.184), (0.016, 0.194), (0, 0.196)], loc, CREAM, seg=14)
    k.sweep(spline([P((0.07, 0, 0.05)), P((0.115, 0, 0.085)), P((0.14, 0, 0.13)), P((0.155, 0, 0.15))], 3),
            [0.022, 0.020, 0.018, 0.016, 0.014, 0.013, 0.012, 0.011, 0.010, 0.010], (0, 0, 0), BUTTER, n=12)
    k.sweep(spline([P(p) for p in arc_xz(0.0, 0.15, 0.065, 180, 0, 8)], 2), 0.011, (0, 0, 0), ("4A4F4D", "6A716E"), n=8)

pk = K("Pot_Kettle", "Kitchen_Props")
pot(pk, (-0.16 - 0.15, Y1 - 0.33 - 0.13, 0.941), rotz=10)
kettle(pk, (-0.16 + 0.15, Y1 - 0.33 + 0.12, 0.941), rotz=200)
pk.finish(weighted=False)


# ------------------------------------------------------------------ retro fridge (cream, rounded, brass handles) + notes and magnets
fr = K("Fridge", "Kitchen_Counters", loc=(1.02, Y1 - 0.34, 0))
for sx in (-1, 1):
    for sy in (-1, 1):
        fr.cyl(0.022, 0.016, 0.07, (sx*0.29, sy*0.23, 0), BRASS, key="Metal", seg=12, r=0.004)
fr.rbox((0.72, 0.60, 1.66), (0, 0.02, 0.07), FRIDGE, r=0.075, seg=4)
fr.rbox((0.70, 0.07, 1.14), (0, -0.295, 0.12), FRIDGE, r=0.035, seg=4)
fr.rbox((0.70, 0.07, 0.42), (0, -0.295, 1.28), FRIDGE, r=0.035, seg=4)
for z0, z1 in ((0.98, 1.20), (1.33, 1.52)):
    fr.sweep(spline([(-0.29, -0.33, z0), (-0.29, -0.348, z0+0.012), (-0.29, -0.356, z0+0.035), (-0.29, -0.356, z1-0.035),
                     (-0.29, -0.348, z1-0.012), (-0.29, -0.33, z1)], 2), 0.012, (0, 0, 0), BRASS, key="Metal", n=10)
fr.rbox((0.11, 0.004, 0.14), (0.07, -0.333, 0.88), PAPER, r=0.0019, seg=1, rot=(0, 5, 0))
fr.rbox((0.15, 0.004, 0.11), (0.13, -0.333, 0.66), ("FFD27F", "FFE8B4"), r=0.0019, seg=1, rot=(0, -4, 0))
fr.cyl(0.022, 0.022, 0.003, (0.10, -0.336, 0.72), BUTTER, seg=16, rot=(90, 0, 0), base=False)
fr.cyl(0.03, 0.03, 0.003, (0.16, -0.336, 0.69), ("63BE58", "93D97E"), seg=16, rot=(90, 0, 0), base=False)
for (mx, mz, mc) in ((0.07, 1.01, CORAL), (0.13, 0.765, MINT), (-0.07, 0.86, BUTTER), (-0.12, 1.08, CORAL)):
    fr.cyl(0.013, 0.012, 0.009, (mx, -0.337, mz), mc, key="Satin", seg=16, r=0.003, rot=(90, 0, 0), base=False)
fr.finish()


# ------------------------------------------------------------------ round honey table on a domed pedestal; two Windsor-ish chairs with coral cushions
tb_ = K("Table", "Kitchen_Table", loc=(0, 0, RUGZ))
tb_.lathe([(0, 0.683), (0.575, 0.683), (0.594, 0.689), (0.601, 0.704), (0.599, 0.719), (0.590, 0.726), (0.570, 0.728), (0, 0.728)],
          (0, 0, 0), TBL, key="Matte", seg=56, cols=["C09060", "C69666", "D0A170", "D8AB79", "DDB27F", "DEB482", "DCB17F", "EED0A2"])
tb_.cyl(0.46, 0.46, 0.055, (0, 0, 0.63), OAK, seg=40, r=0.012)
tb_.lathe([(0, 0.10), (0.075, 0.10), (0.062, 0.16), (0.050, 0.30), (0.056, 0.45), (0.072, 0.58), (0.086, 0.62), (0.09, 0.632), (0, 0.632)],
          (0, 0, 0), OAK, key="Matte", seg=24)
tb_.lathe([(0, 0), (0.27, 0), (0.28, 0.012), (0.27, 0.032), (0.19, 0.072), (0.10, 0.10), (0.08, 0.11), (0, 0.11)],
          (0, 0, 0), OAK, key="Matte", seg=40)
tb_.finish()

def chair(name, loc, rotz):
    k = K(name, "Kitchen_Table", loc=(loc[0], loc[1], RUGZ), rot=rotz)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl(0.017, 0.022, 0.43, (sx*0.175, sy*0.16, 0.0), OAK, seg=12, r=0.006, rot=(-sy*3, sx*3, 0))
    for sx in (-1, 1):
        k.sweep([(sx*0.18, -0.16, 0.16), (sx*0.18, 0.16, 0.16)], 0.010, (0, 0, 0), OAK, n=8)
    k.sweep([(-0.18, 0.0, 0.16), (0.18, 0.0, 0.16)], 0.010, (0, 0, 0), OAK, n=8)
    k.rbox((0.44, 0.42, 0.035), (0, 0, 0.425), OAK, r=0.015)
    for sx in (-1, 1):
        k.sweep(spline([(sx*0.175, 0.17, 0.44), (sx*0.18, 0.195, 0.62), (sx*0.19, 0.225, 0.84)], 3), [0.018, 0.017, 0.016, 0.016, 0.015, 0.015, 0.015], (0, 0, 0), OAK, n=10)
    k.sweep(spline([(-0.215, 0.215, 0.83), (-0.11, 0.245, 0.855), (0.0, 0.255, 0.862), (0.11, 0.245, 0.855), (0.215, 0.215, 0.83)], 3),
            0.026, (0, 0, 0), OAK, n=12, flat=0.65)
    for x in (-0.09, 0.0, 0.09):
        k.sweep([(x, 0.18, 0.44), (x*1.08, 0.215, 0.64), (x*1.15, 0.245, 0.835)], 0.0095, (0, 0, 0), OAK, n=8)
    k.rbox((0.40, 0.38, 0.05), (0, -0.01, 0.455), CORAL, r=0.024, seg=4)
    k.finish()
chair("Chair_A", (-0.82, 0.04), 95)       # on the player's left, facing the table
chair("Chair_B", (0.82, 0.02), -86)       # on the player's right


# ------------------------------------------------------------------ table deco: only on the back arc (the middle 0.9 x 0.5 m stays empty)
def bowl_prof(R, H, t=0.008, foot=0.45):
    pts = [(0, 0), (R*foot, 0), (R*foot + 0.004, 0.006)]
    for k in range(1, 7):
        a = k/6*math.pi/2
        pts.append((R*foot + (R - R*foot)*math.sin(a), 0.006 + (H - 0.006)*(1 - math.cos(a))))
    pts += [(R - t*0.4, H + t*0.45), (R - t, H)]
    for k in range(6, 0, -1):
        a = k/6*math.pi/2
        pts.append(((R*foot + (R - R*foot)*math.sin(a))*(1 - t/R), 0.006 + t + (H - 0.006 - t)*(1 - math.cos(a))))
    pts.append((0, 0.006 + t))
    return pts

def fruit(k, kind, loc, seed=0):
    rnd = random.Random(seed); rz = rnd.uniform(0, 360)
    if kind == "orange":
        k.blob(0.036, loc, ORANGE, u=14, v=9, sq=(1, 1, 0.92), jit=0.015, seed=seed)
        k.blob(0.009, (loc[0], loc[1], loc[2] + 0.033), ("3E8128", "74B544"), u=6, v=4, sq=(1.6, 0.8, 0.4), rot=(0, 0, rz))
    elif kind == "apple":
        k.blob(0.037, loc, ("D2302B", "FA7C63"), u=14, v=9, sq=(1, 1, 0.88), jit=0.012, seed=seed)
        k.sweep([(loc[0], loc[1], loc[2] + 0.025), (loc[0] + 0.004, loc[1], loc[2] + 0.048)], 0.003, (0, 0, 0), ("6B4A2E", "8A6440"), n=5)
    elif kind == "lemon":
        k.blob(0.03, loc, ("F0C21C", "FFE658"), u=14, v=9, sq=(1.35, 1, 0.95), rot=(0, 0, rz), seed=seed)

def teapot(k, loc):
    x, y, z = loc
    def P(p): return (x + p[0], y + p[1], z + p[2])
    k.lathe([(0, 0), (0.05, 0), (0.058, 0.006), (0.075, 0.03), (0.082, 0.06), (0.078, 0.085), (0.065, 0.105), (0.048, 0.115), (0.046, 0.12), (0, 0.121)],
            loc, MINT, seg=32)
    k.lathe([(0.049, 0.117), (0.045, 0.125), (0.035, 0.135), (0.015, 0.14), (0, 0.141)], loc, ("7FD0B0", "C2F0DC"), seg=28)
    k.blob(0.014, P((0, 0, 0.152)), CREAM, u=12, v=8, sq=(1, 1, 0.85))
    k.sweep(spline([P((-0.065, 0, 0.04)), P((-0.10, 0, 0.06)), P((-0.125, 0, 0.10)), P((-0.137, 0, 0.125))], 3),
            [0.018, 0.016, 0.014, 0.013, 0.012, 0.011, 0.010, 0.010, 0.009, 0.009], (0, 0, 0), MINT, n=12)
    k.sweep(spline([P(p) for p in arc_xz(0.08, 0.065, 0.036, 105, -105, 8)], 2), 0.0085, (0, 0, 0), MINT, n=8)

def flower(k, base, top, head, seed):
    k.sweep(spline([base, ((base[0]+top[0])/2, (base[1]+top[1])/2 + 0.003, (base[2]+top[2])/2), top], 3), 0.003, (0, 0, 0), ("4C9334", "7CC04B"), n=5)
    tilt = look_rot(Vector(top) - Vector(base))
    if head == "daisy":
        k.cyl(0.026, 0.024, 0.005, top, PAPER, seg=16, r=0.002, rot=tilt, base=False)
        k.blob(0.010, top, BUTTER, u=8, v=6, sq=(1, 1, 0.7), rot=tilt)
    else:
        k.blob(0.019, top, CORAL, u=10, v=8, sq=(1, 1, 1.35), rot=tilt)

TZ = 0.728 + RUGZ
td = K("TableDeco", "Kitchen_Table")
teapot(td, (0.02, 0.44, TZ))
bx_, by_ = -0.29, 0.38
td.lathe(bowl_prof(0.105, 0.058), (bx_, by_, TZ), CREAM, seg=32)
for i, (kind, dx, dy, dz) in enumerate([("orange", -0.03, 0.025, 0.04), ("orange", 0.035, 0.02, 0.04), ("apple", 0.0, -0.035, 0.042),
                                        ("lemon", 0.012, 0.005, 0.075), ("orange", -0.04, -0.03, 0.045)]):
    fruit(td, kind, (bx_ + dx, by_ + dy, TZ + dz), seed=i)
vx_, vy_ = 0.31, 0.37
td.lathe([(0, 0), (0.033, 0), (0.042, 0.02), (0.044, 0.05), (0.034, 0.09), (0.020, 0.115), (0.022, 0.128), (0.026, 0.135),
          (0.020, 0.135), (0.016, 0.125), (0, 0.124)], (vx_, vy_, TZ), SKY, seg=24)
for i, (dx, dy, h, kind) in enumerate([(0.0, 0.0, 0.30, "daisy"), (0.05, 0.02, 0.25, "tulip"), (-0.045, 0.025, 0.26, "daisy"), (0.01, -0.05, 0.22, "daisy")]):
    flower(td, (vx_, vy_, TZ + 0.12), (vx_ + dx, vy_ + dy, TZ + h), kind, i)
td.finish(weighted=False)


# ------------------------------------------------------------------ counter props
def crock(k, loc, R=0.065, H=0.15, col=CREAM):
    k.lathe([(0, 0), (R-0.01, 0), (R-0.003, 0.008), (R, 0.06), (R-0.005, H-0.02), (R, H-0.008), (R-0.002, H), (R-0.01, H-0.004), (0, H-0.02)],
            loc, col, seg=28)

def tpot(k, loc, R, H, leaves=None, seed=0, col=TERRA):
    """Terracotta pot with a rim; soil top at 0.88 H."""
    k.lathe([(0, 0), (R*0.72, 0), (R*0.76, 0.008), (R*0.92, H*0.80), (R, H*0.82), (R*1.02, H), (R*0.93, H*1.01), (R*0.90, H*0.88),
             (R*0.88, H*0.88), (0, H*0.88)],
            loc, col, seg=28, cols=[None]*8 + [SOIL, SOIL])

pr = K("Props_Counter", "Kitchen_Props")
# left run: bread board, toaster, crock of spoons, basil
bb = (-1.57, 0.47, CT)
pr.rbox((0.25, 0.40, 0.022), bb, ("D9A469", "F2CC92"), r=0.008, rot=(0, 0, 8))
pr.rbox((0.07, 0.07, 0.022), (bb[0] - 0.016, bb[1] + 0.225, CT), ("D9A469", "F2CC92"), r=0.02, rot=(0, 0, 8))
pr.blob(0.1, (bb[0] + 0.005, bb[1] - 0.02, CT + 0.06), ("B4672A", "EFB15E"), sq=(0.72, 1.18, 0.56), u=20, v=12, rot=(0, 0, 8))
for i in range(3):
    pr.blob(0.03, (bb[0] + 0.003 - 0.005*(i-1), bb[1] - 0.10 + 0.07*i, CT + 0.112), ("EEB065", "F9D696"), sq=(1.6, 0.32, 0.25), u=10, v=6, rot=(0, 0, 8 - 30))
pr.blob(0.045, (bb[0] + 0.03, bb[1] + 0.17, CT + 0.03), ("EEB065", "F9D696"), sq=(1.0, 0.25, 0.9), u=12, v=8, rot=(0, 0, 10))
tx, ty = -1.69, 0.88
pr.rbox((0.17, 0.27, 0.18), (tx, ty, CT), SKY, key="Satin", r=0.05, seg=4)
for dx in (-0.035, 0.035):
    pr.rbox((0.024, 0.19, 0.02), (tx + dx, ty, CT + 0.168), ("343231", "4D4947"), r=0.008, seg=2)
pr.rbox((0.035, 0.022, 0.016), (tx, ty - 0.142, CT + 0.12), ("4A4F4D", "6A716E"), r=0.006, seg=2)
cx_, cy_ = -1.72, 1.50
crock(pr, (cx_, cy_, CT))
for i, (dx, dy) in enumerate([(-0.02, -0.01), (0.02, 0.0), (0.0, 0.025)]):
    a = Vector((cx_ + dx*0.4, cy_ + dy*0.4, CT + 0.06)); b = Vector((cx_ + dx*2.6, cy_ + dy*2.6, CT + 0.31))
    pr.sweep([a, b], 0.0065, (0, 0, 0), OAK, n=8)
    pr.blob(0.03, tuple(b + (b - a).normalized()*0.035), ("CF8F4E", "EAB676"), sq=(0.7, 0.28, 1.0), u=12, v=8, rot=look_rot(b - a))
tpot(pr, (-1.46, 1.55, CT), 0.06, 0.09)
rb = random.Random(3)
for i in range(11):
    a = rb.uniform(0, math.tau); rr = rb.uniform(0.0, 0.045)
    pr.blob(rb.uniform(0.022, 0.032), (-1.46 + rr*math.cos(a), 1.55 + rr*math.sin(a), CT + 0.10 + rb.uniform(0.0, 0.08)),
            LEAF2, key="Leaf", u=10, v=7, sq=(1.25, 0.8, 0.55), rot=(rb.uniform(-20, 20), rb.uniform(-20, 20), math.degrees(a)), seed=i)
# sink strip: soap + sponge
pr.lathe([(0, 0), (0.026, 0), (0.029, 0.006), (0.029, 0.12), (0.022, 0.142), (0.011, 0.15), (0, 0.15)], (-0.515, 1.53, CT), MINT, seg=20)
pr.cyl(0.008, 0.008, 0.03, (-0.515, 1.53, CT + 0.148), CREAM, seg=10, r=0.002)
pr.sweep([(-0.515, 1.53, CT + 0.172), (-0.515, 1.505, CT + 0.172)], 0.005, (0, 0, 0), CREAM, n=6)
SB = CT + 0.004 - 0.19                       # sink bottom
for i, c_ in enumerate((PAPER, SKY, PAPER)):
    pr.lathe([(0, 0), (0.08, 0), (0.085, 0.004), (0.108, 0.016), (0.111, 0.02), (0.107, 0.021), (0.083, 0.009), (0, 0.009)],
             (SX - 0.12, Y1 - 0.62 + 0.27, SB + 0.002 + i*0.014), c_, seg=32)
pr.lathe([(0, 0), (0.033, 0), (0.038, 0.005), (0.039, 0.02), (0.039, 0.086), (0.036, 0.09), (0.033, 0.086), (0.033, 0.012), (0, 0.012)],
         (SX + 0.14, Y1 - 0.62 + 0.20, SB + 0.002), CORAL, seg=24)
pr.sweep(spline([(SX + 0.14 + 0.04 + 0.024*math.cos(math.radians(a)), Y1 - 0.62 + 0.20, SB + 0.05 + 0.024*math.sin(math.radians(a))) for a in range(100, -101, -25)], 2),
         0.0065, (0, 0, 0), CORAL, n=8)
pr.rbox((0.045, 0.075, 0.026), (-0.515, 1.30, CT), BUTTER, r=0.01)
pr.rbox((0.045, 0.075, 0.008), (-0.515, 1.30, CT + 0.026), ("4FB074", "86D59E"), r=0.003, seg=2)
# right counter: canisters + bowl of lemons
for (x_, R_, H_) in ((0.25, 0.055, 0.20), (0.39, 0.05, 0.16), (0.53, 0.045, 0.12)):
    pr.lathe([(0, 0), (R_-0.006, 0), (R_, 0.006), (R_, H_-0.004), (R_-0.004, H_), (0, H_)], (x_, 1.56, CT), CREAM, seg=28)
    pr.cyl(R_+0.0012, R_+0.0012, 0.035, (x_, 1.56, CT + H_*0.42), SKY, seg=28)
    pr.cyl(R_+0.004, R_+0.004, 0.022, (x_, 1.56, CT + H_), OAK, seg=28, r=0.007)
    pr.blob(0.012, (x_, 1.56, CT + H_ + 0.028), OAK, u=10, v=7, sq=(1, 1, 0.75))
pr.lathe(bowl_prof(0.085, 0.048), (0.40, 1.25, CT), SKY, seg=28)
for i, (dx, dy, dz) in enumerate([(-0.025, 0.012, 0.038), (0.025, 0.01, 0.038), (0.0, -0.025, 0.04)]):
    fruit(pr, "lemon", (0.40 + dx, 1.25 + dy, CT + dz), seed=10 + i)
pr.finish(weighted=False)


# ------------------------------------------------------------------ plants: big floor plant, trailing pothos on the fridge; crate of oranges
pl = K("Plant_Floor", "Kitchen_Props", loc=(-1.56, -0.40, 0))
tpot(pl, (0, 0, 0), 0.17, 0.32)
rp = random.Random(5)
for i in range(9):
    a = i*math.tau/9 + rp.uniform(-0.25, 0.25)
    out = Vector((math.cos(a), math.sin(a), 0))
    p0 = Vector((0, 0, 0.27)) + out*0.02
    sl = rp.uniform(0.22, 0.55)
    p1 = p0 + out*sl*0.30 + Z*sl
    pl.sweep(spline([p0, p0 + out*sl*0.08 + Z*sl*0.5, p1], 3), 0.0075, (0, 0, 0), ("3E7F34", "68A746"), n=6)
    pl.leaf(p1, out*0.6 + Z*0.75, rp.uniform(0.28, 0.36), rp.uniform(0.10, 0.125), 0.28, LEAF, roll=rp.uniform(-0.3, 0.3))
pl.finish(weighted=False)

def vine(k, pts, seed, leaf_every=0.032):
    path = spline(pts, 5)
    k.sweep(path, 0.004, (0, 0, 0), ("3E7F34", "68A746"), n=5)
    rv = random.Random(seed); s = 0.0
    for p, q in zip(path, path[1:]):
        s += (q - p).length
        if s >= leaf_every:
            s = 0.0; side = (q - p).cross(Z); side = side.normalized() if side.length > 1e-5 else Vector((1, 0, 0))
            sgn = 1 if rv.random() < 0.5 else -1
            c = Vector(q) + side*sgn*0.018
            k.blob(0.03, tuple(c), LEAF, key="Leaf", u=10, v=6, sq=(1.0, 0.72, 0.30),
                   rot=(rv.uniform(-35, 35), rv.uniform(-35, 35), math.degrees(math.atan2(side.y*sgn, side.x*sgn))), seed=rv.randint(0, 99))

po = K("Plant_Pothos", "Kitchen_Props", loc=(0.88, Y1 - 0.30, 1.73))
tpot(po, (0, 0, 0), 0.075, 0.11, col=("E5E3DE", "FFFFFF"))
for i in range(16):
    a = i*math.tau/16
    po.blob(0.032, (0.03*math.cos(a), 0.03*math.sin(a), 0.12 + 0.02*(i % 3)), LEAF, key="Leaf", u=10, v=6, sq=(1.0, 0.75, 0.35),
            rot=(25*math.sin(a), 25*math.cos(a), math.degrees(a)), seed=i)
vine(po, [(0.05, -0.04, 0.10), (0.18, -0.20, 0.06), (0.32, -0.33, 0.0), (0.42, -0.36, -0.20), (0.44, -0.35, -0.42), (0.43, -0.37, -0.60)], 1)
vine(po, [(0.0, -0.06, 0.10), (0.02, -0.22, 0.04), (0.0, -0.36, -0.02), (-0.03, -0.38, -0.25), (-0.02, -0.385, -0.38)], 2)
vine(po, [(0.06, 0.0, 0.10), (0.24, -0.02, 0.05), (0.40, -0.06, -0.03), (0.42, -0.08, -0.22)], 3)
po.finish(weighted=False)

cr = K("Crate_Oranges", "Kitchen_Props", loc=(-1.60, -1.02, 0), rot=8)
cw, cd, ch = 0.30, 0.42, 0.20
for sx in (-1, 1):
    for sy in (-1, 1):
        cr.rbox((0.035, 0.035, ch), (sx*(cw/2 - 0.018), sy*(cd/2 - 0.018), 0), OAK, r=0.008, seg=2)
for z in (0.02, 0.12):
    for sx in (-1, 1):
        cr.rbox((0.016, cd - 0.03, 0.065), (sx*(cw/2 - 0.008), 0, z), ("DEA35D", "F2C47F"), r=0.006, seg=2)
    for sy in (-1, 1):
        cr.rbox((cw - 0.03, 0.016, 0.065), (0, sy*(cd/2 - 0.008), z), ("DEA35D", "F2C47F"), r=0.006, seg=2)
cr.rbox((cw - 0.02, cd - 0.02, 0.015), (0, 0, 0.012), ("C68B4D", "DEAB6F"), r=0.004, seg=2)
ro = random.Random(9)
for i, (x_, y_, z_) in enumerate([(-0.07, -0.13, 0.17), (0.07, -0.12, 0.17), (-0.065, 0.0, 0.17), (0.07, 0.01, 0.17), (-0.07, 0.13, 0.17),
                                  (0.065, 0.13, 0.17), (0.0, -0.06, 0.225), (0.01, 0.07, 0.225)]):
    fruit(cr, "orange", (x_ + ro.uniform(-0.01, 0.01), y_ + ro.uniform(-0.01, 0.01), z_), seed=20 + i)
cr.finish()


ct_ = K("CatBed", "Kitchen_Props", loc=(0.92, -1.02, 0), rot=-20)
ct_.lathe([(0, 0.0), (0.26, 0.0), (0.30, 0.03), (0.31, 0.08), (0.29, 0.13), (0.25, 0.15), (0.21, 0.13), (0.20, 0.07), (0.18, 0.05), (0, 0.05)],
          (0, 0, 0), ("E0644F", "F9A48F"), key="Matte", seg=40, cols=[None]*7 + ["F3F1EC", "FBFAF7", "FBFAF7"])
CAT = ("E3742A", "FAAE55")
ct_.blob(0.13, (0.0, 0.01, 0.12), CAT, sq=(1.05, 0.85, 0.62), u=20, v=12)                       # curled body
ct_.blob(0.072, (0.09, -0.08, 0.13), CAT, sq=(1.0, 0.95, 0.85), u=16, v=10)                      # head resting on the rim
for s in (-1, 1):
    ear = Vector((0.09 + s*0.045, -0.085, 0.185))
    ct_.cyl(0.03, 0.005, 0.058, tuple(ear), CAT, seg=10, r=0.004, rot=(15, s*25, 0), base=False)
ct_.blob(0.03, (0.09, -0.135, 0.115), ("F2F0EB", "FFFFFF"), sq=(1.2, 0.8, 0.75), u=10, v=7)        # muzzle
for s in (-1, 1):
    ct_.sweep(spline([(0.09 + s*0.022, -0.142, 0.142), (0.09 + s*0.03, -0.146, 0.138), (0.09 + s*0.038, -0.142, 0.142)], 2), 0.0025, (0, 0, 0),
              ("5A3C2C", "6E4C3A"), n=5)                                                               # closed eyes
ct_.sweep(spline([(-0.11, 0.06, 0.10), (-0.12, -0.06, 0.09), (-0.05, -0.14, 0.085), (0.03, -0.15, 0.08)], 4),
          [0.024, 0.024, 0.023, 0.022, 0.021, 0.02, 0.019, 0.018, 0.017, 0.016, 0.015, 0.015, 0.014], (0, 0, 0), CAT, n=10)   # tail around the front
for s in (-1, 1):
    ct_.blob(0.026, (0.04 + s*0.05, -0.15, 0.075), ("F2F0EB", "FFFFFF"), sq=(1.0, 1.3, 0.6), u=10, v=7)   # front paws
ct_.finish(weighted=False)


# ------------------------------------------------------------------ wall- and ceiling-mounted (hidden until the walls exist)
WM = "Kitchen_WallMounted"
wn = K("Window", WM)
wx, wz0, wz1, ww = SX, 1.06, 1.86, 0.86
FRAME = ("E9E9E6", "FFFFFF")
for (sx_, sz_, lx, lz) in ((ww, 0.06, wx, wz0), (ww, 0.06, wx, wz1 - 0.06), (0.06, wz1 - wz0, wx - ww/2 + 0.03, wz0), (0.06, wz1 - wz0, wx + ww/2 - 0.03, wz0),
                           (0.04, wz1 - wz0 - 0.1, wx, wz0 + 0.05), (ww - 0.1, 0.04, wx, wz0 + 0.50)):
    wn.rbox((sx_, 0.10, sz_), (lx, Y1, lz), FRAME, r=0.012)
wn.rbox((ww - 0.08, 0.012, wz1 - wz0 - 0.08), (wx, Y1 + 0.01, wz0 + 0.04), ("B8E3F2", "E6F7FC"), key="Glow", r=0.004, seg=1)
wn.rbox((ww + 0.12, 0.17, 0.035), (wx, Y1 - 0.06, wz0 - 0.035), WOOD, r=0.012)
tpot(wn, (wx + 0.28, Y1 - 0.07, wz0), 0.045, 0.07)
for i in range(7):
    a = i*math.tau/7
    wn.blob(0.022, (wx + 0.28 + 0.02*math.cos(a), Y1 - 0.07 + 0.02*math.sin(a), wz0 + 0.08 + 0.015*(i % 2)), LEAF2, key="Leaf", u=10, v=6, sq=(1.2, 0.8, 0.55),
            rot=(0, 0, math.degrees(a)))
wn.finish()

cu = K("Curtains", WM)
cu.curtain(wx - ww/2 - 0.16, wx - ww/2 + 0.08, Y1 - 0.09, 1.00, 1.96, ("82BF84", "B6E2AD"), folds=4)
cu.curtain(wx + ww/2 - 0.08, wx + ww/2 + 0.16, Y1 - 0.09, 1.00, 1.96, ("82BF84", "B6E2AD"), folds=4)
cu.sweep([(wx - ww/2 - 0.22, Y1 - 0.09, 1.99), (wx + ww/2 + 0.22, Y1 - 0.09, 1.99)], 0.011, (0, 0, 0), BRASS, key="Metal", n=10)
for s in (-1, 1):
    cu.blob(0.02, (wx + s*(ww/2 + 0.23), Y1 - 0.09, 1.99), BRASS, key="Metal", u=10, v=8)
    cu.sweep([(wx + s*(ww/2 + 0.17), Y1 - 0.09, 1.99), (wx + s*(ww/2 + 0.17), Y1, 1.99)], 0.007, (0, 0, 0), BRASS, key="Metal", n=8)
cu.finish(weighted=False)

hd = K("RangeHood", WM)
hd.rbox((0.66, 0.46, 0.20), (-0.16, Y1 - 0.23, 1.58), CREAM, r=0.03, taper=(0.5, 0.55))
hd.rbox((0.70, 0.48, 0.04), (-0.16, Y1 - 0.24, 1.56), CREAM, r=0.015)
hd.rbox((0.30, 0.24, 0.86), (-0.16, Y1 - 0.12, 1.74), CREAM, r=0.03)
hd.rbox((0.60, 0.012, 0.012), (-0.16, Y1 - 0.48, 1.585), BRASS, key="Metal", r=0.005, seg=2)
hd.finish()

sh = K("Shelves_Left", WM)
SHX = X0 + 0.13
for z in (1.42, 1.78):
    sh.rbox((0.25, 0.84, 0.035), (SHX, 0.65, z), WOOD, r=0.012)
    for y in (0.30, 1.00):
        sh.sweep(spline([(X0 + 0.005, y, z - 0.14), (X0 + 0.05, y, z - 0.06), (X0 + 0.13, y, z - 0.012), (X0 + 0.20, y, z - 0.004)], 3), 0.010, (0, 0, 0), BRASS, key="Metal", n=8)
def mug(k, loc, col, rotz=0.0):
    k.lathe([(0, 0), (0.033, 0), (0.038, 0.005), (0.039, 0.02), (0.039, 0.086), (0.036, 0.09), (0.033, 0.086), (0.033, 0.012), (0, 0.012)], loc, col, seg=24)
    R = Matrix.Rotation(math.radians(rotz), 4, 'Z')
    k.sweep(spline([tuple(Vector(loc) + R @ Vector(p)) for p in arc_xz(0.04, 0.048, 0.024, 100, -100, 8)], 2), 0.0065, (0, 0, 0), col, n=8)
for i in range(4):
    sh.lathe([(0, 0), (0.08, 0), (0.085, 0.004), (0.108, 0.016), (0.111, 0.02), (0.107, 0.021), (0.083, 0.009), (0, 0.009)],
             (SHX, 0.40, 1.455 + i*0.016), PAPER, seg=32)
for y_, c_, rz in ((0.62, CORAL, 120), (0.74, SKY, 90), (0.86, BUTTER, 60)):
    mug(sh, (SHX + 0.01, y_, 1.455), c_, rotz=rz)
sh.lathe([(0, 0), (0.045, 0), (0.05, 0.008), (0.05, 0.16), (0.04, 0.17), (0, 0.17)], (SHX, 0.98, 1.455), ("F0BD5E", "FADB97"), key="Glass", seg=24)
sh.cyl(0.044, 0.044, 0.025, (SHX, 0.98, 1.625), OAK, seg=24, r=0.006)
for y_, h_, c_ in ((0.33, 0.14, ("F08A45", "FAB57C")), (0.47, 0.11, ("7FC45C", "B3E28A"))):
    sh.lathe([(0, 0), (0.036, 0), (0.04, 0.007), (0.04, h_), (0.032, h_ + 0.008), (0, h_ + 0.008)], (SHX, y_, 1.815), c_, key="Glass", seg=20)
    sh.cyl(0.035, 0.035, 0.018, (SHX, y_, 1.815 + h_ + 0.004), CREAM, seg=20, r=0.005)
tpot(sh, (SHX, 0.72, 1.815), 0.055, 0.09, col=SKY)
vine(sh, [(SHX + 0.03, 0.70, 1.92), (SHX + 0.13, 0.66, 1.88), (SHX + 0.17, 0.62, 1.75), (SHX + 0.18, 0.60, 1.58)], 7)
vine(sh, [(SHX + 0.02, 0.75, 1.92), (SHX + 0.12, 0.80, 1.86), (SHX + 0.16, 0.84, 1.70)], 8)
for i, (c_, h_) in enumerate(((CORAL, 0.03), (SKY, 0.025), (BUTTER, 0.035))):
    sh.rbox((0.17, 0.22, h_), (SHX + 0.005, 0.96, 1.815 + sum(hh for _, hh in ((CORAL, 0.03), (SKY, 0.025), (BUTTER, 0.035))[:i])), c_, r=0.008, rot=(0, 0, 4 - 5*i))
sh.finish()

sp = K("SpiceShelf", WM)
sp.rbox((0.46, 0.12, 0.025), (0.40, Y1 - 0.06, 1.36), WOOD, r=0.01)
for i, c_ in enumerate((("D54A24", "F07F4E"), ("ECAF14", "FFD452"), ("5A9A36", "8FC860"), ("8B5E3C", "B48662"), ("ECEBE7", "FFFFFF"))):
    x_ = 0.22 + i*0.09
    sp.cyl(0.022, 0.022, 0.07, (x_, Y1 - 0.06, 1.385), c_, key="Glass", seg=16, r=0.004)
    sp.cyl(0.023, 0.023, 0.018, (x_, Y1 - 0.06, 1.455), CREAM, seg=16, r=0.004)
sp.finish()

ck = K("Clock", WM, loc=(X0 + 0.02, -0.55, 1.78))
ck.lathe([(0, 0), (0.15, 0), (0.158, 0.012), (0.155, 0.032), (0.145, 0.04), (0.135, 0.032), (0, 0.028)], (0, 0, 0), WOOD, seg=40, rot=(0, 90, 0))
ck.cyl(0.133, 0.133, 0.004, (0.028, 0, 0), ("BDE9D6", "E6F8EF"), seg=40, rot=(0, 90, 0), base=False)
for i in range(12):
    a = math.radians(i*30)
    ck.blob(0.008, (0.032, 0.11*math.cos(a), 0.11*math.sin(a)), OAK, u=8, v=6, sq=(0.4, 1, 1))
ck.rbox((0.006, 0.012, 0.075), (0.034, 0.0, 0.0), ("3F4442", "5A605E"), r=0.003, seg=1, rot=(-40, 0, 0))
ck.rbox((0.006, 0.010, 0.10), (0.036, 0.0, 0.0), ("3F4442", "5A605E"), r=0.003, seg=1, rot=(110, 0, 0))
ck.blob(0.01, (0.04, 0, 0), BRASS, key="Metal", u=8, v=6)
ck.finish()

lp = K("PendantLamp", WM, loc=(0.0, 0.0, 1.70))
lp.lathe([(0.025, 0.25), (0.06, 0.245), (0.13, 0.19), (0.18, 0.09), (0.20, 0.0), (0.205, -0.01), (0.198, -0.012), (0.19, -0.002),
          (0.17, 0.085), (0.12, 0.18), (0.055, 0.232), (0.022, 0.236)], (0, 0, 0), ("EFB233", "FFD966"), seg=40)
lp.cyl(0.03, 0.026, 0.06, (0, 0, 0.23), BRASS, key="Metal", seg=16, r=0.006)
lp.blob(0.055, (0, 0, 0.12), ("FFE2A6", "FFF1CF"), key="Glow", u=16, v=10, sq=(1, 1, 1.15))
lp.sweep([(0, 0, 0.29), (0, 0, 0.90)], 0.004, (0, 0, 0), ("3F4442", "5A605E"), n=6, cap=False)
lp.cyl(0.06, 0.06, 0.03, (0, 0, 0.87), CREAM, seg=24, r=0.008)
lp.finish()

vl = bpy.context.view_layer
def hide_collection(name):
    c = COLL.get(name)
    if not c: return
    c.hide_render = True
    lc = next(l for l in vl.layer_collection.children if l.collection == c)
    lc.hide_viewport = True
hide_collection(WM)


# ------------------------------------------------------------------ markers: seated eye + operation zone (not rendered)
mk = coll("Kitchen_Markers")
eye = bpy.data.objects.new("Player_Eye_Ref", None); eye.empty_display_type = 'ARROWS'; eye.empty_display_size = 0.15
eye.location = (0, -0.90, 1.15); eye.rotation_euler = (math.radians(90), 0, 0); mk.objects.link(eye)
zm = bpy.data.meshes.new("Marker_OperationZone")
zm.from_pydata([(-0.45, -0.25, 0), (0.45, -0.25, 0), (0.45, 0.25, 0), (-0.45, 0.25, 0)], [], [(0, 1, 2, 3)])
zo = bpy.data.objects.new("Marker_OperationZone", zm); zo.location = (0, 0, TZ + 0.002); zo.display_type = 'WIRE'; zo.hide_render = True; zo.visible_camera = False
mk.objects.link(zo)
mk.hide_render = True


# ------------------------------------------------------------------ light, world, cameras, render
sun = bpy.data.lights.new("Sun_Warm", 'SUN'); sun.energy = 4.8; sun.color = tolin(hx("FFE5C2")); sun.angle = math.radians(6)
so = bpy.data.objects.new("Sun_Warm", sun); coll("Lighting").objects.link(so)
so.rotation_euler = (-SUN).to_track_quat('-Z', 'Y').to_euler()

wd = bpy.data.worlds.new("Kitchen_Sky"); sc.world = wd; wd.use_nodes = True
nt = wd.node_tree; nt.nodes.clear()
tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); mr = nt.nodes.new("ShaderNodeMapRange")
cramp = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); wout = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(tc.outputs["Generated"], sep.inputs[0]); nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
mr.inputs["From Min"].default_value = 0.35; mr.inputs["From Max"].default_value = 0.85; mr.clamp = True
nt.links.new(mr.outputs["Result"], cramp.inputs["Fac"]); nt.links.new(cramp.outputs["Color"], bg.inputs["Color"])
lpth = nt.nodes.new("ShaderNodeLightPath"); bgc = nt.nodes.new("ShaderNodeBackground"); mixs = nt.nodes.new("ShaderNodeMixShader")
bgc.inputs["Color"].default_value = (*tolin(hx("F6D8C3")), 1); bgc.inputs["Strength"].default_value = 1.0
nt.links.new(lpth.outputs["Is Camera Ray"], mixs.inputs[0]); nt.links.new(bg.outputs["Background"], mixs.inputs[1])
nt.links.new(bgc.outputs["Background"], mixs.inputs[2]); nt.links.new(mixs.outputs["Shader"], wout.inputs["Surface"])
cramp.color_ramp.elements[0].color = (*tolin(hx("F3E7DC")), 1)
cramp.color_ramp.elements[1].color = (*tolin(hx("BFD9F2")), 1)
bg.inputs["Strength"].default_value = 0.55

def cam(name, loc, look, lens=24, ortho=None):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.05; cd.clip_end = 200
    if ortho: cd.type = 'ORTHO'; cd.ortho_scale = ortho
    co = bpy.data.objects.new(name, cd); coll("Cameras").objects.link(co); co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return co
ISO_T = Vector((-0.25, 0.10, 0.75)); ISO_D = Vector((1.0, -1.0, 0.92)).normalized()
cam("Cam_Iso", tuple(ISO_T + ISO_D*14), tuple(ISO_T), ortho=5.4)
cam("Cam_Seat", (0.0, -0.90, 1.15), (0.0, 1.70, 0.95), lens=16)
cam("Cam_Overview", (3.9, -4.7, 3.0), (-0.30, 0.15, 0.55), lens=30)
sc.camera = bpy.data.objects["Cam_Iso"]

r = sc.render
try: r.engine = 'BLENDER_EEVEE_NEXT'
except TypeError: r.engine = 'BLENDER_EEVEE'
r.resolution_x = 1200; r.resolution_y = 1200
sc.view_settings.view_transform = "Standard"     # what you see is what the vertex colours look like in Unity (no tone mapping)
sc.view_settings.look = "None"
for attr, val in (("use_raytracing", True), ("use_shadows", True), ("use_gtao", True), ("use_fast_gi", True), ("fast_gi_distance", 0.6)):
    try: setattr(sc.eevee, attr, val)
    except Exception: pass

tris = 0
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
vis = sum(len(o.data.loop_triangles) for o in bpy.data.objects if o.type == 'MESH' and o.name not in COLL[WM].objects)
print("KITCHEN built: objects", len([o for o in bpy.data.objects if o.type == 'MESH']), "tris", tris, "(visible", vis, ") materials", len(bpy.data.materials))
if SAVE:
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("saved", OUT)
