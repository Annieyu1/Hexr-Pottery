import bpy, math, random, bmesh
from mathutils import Vector, Euler, Matrix

def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def hexc(h):
    h = h.lstrip('#')
    return tuple(lin(int(h[i:i+2], 16)/255) for i in (0, 2, 4))

def mat(name, h, rough=1.0, emit=None, estr=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = hexc(h)
    b.inputs["Base Color"].default_value = (*c, 1)
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*c, 1)
    if emit:
        b.inputs["Emission Color"].default_value = (*hexc(emit), 1)
        b.inputs["Emission Strength"].default_value = estr
    return m

def coll(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    return c

def mk(name, bm, m, col, loc=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    o.location = loc
    col.objects.link(o)
    if m is not None:
        me.materials.append(m)
    return o

def _rot(bm, rot):
    if any(rot):
        R = Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=R, verts=bm.verts)

def box(name, size, loc, m, col, bottom=False, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.transform(bm, matrix=Matrix.Diagonal((*size, 1)), verts=bm.verts)
    _rot(bm, rot)
    z = loc[2] + (size[2]/2 if bottom else 0)
    return mk(name, bm, m, col, (loc[0], loc[1], z))

def cyl(name, r1, r2, h, loc, m, col, seg=12, bottom=True, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h)
    _rot(bm, rot)
    z = loc[2] + (h/2 if bottom else 0)
    return mk(name, bm, m, col, (loc[0], loc[1], z))

def poly(name, verts, m, col):
    """flat polygon (list of 3D points), single face"""
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], [list(range(len(verts)))])
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    if m is not None:
        me.materials.append(m)
    return o

def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name; o.data.name = name
    bpy.ops.object.select_all(action='DESELECT')
    return o

def remove(*names):
    for n in names:
        o = bpy.data.objects.get(n)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)

# palette (art-brief s4 + roomvisual ref1)
P = dict(wall="F5E6CC", deep="8A5A3B", desk="D9A366", honey="C98F57", teal="6F9EA8",
         coral="E58E73", sage="8FAE6A", amber="FFC46B", shade="F2E3BF", frame="3A3A3F",
         blue="A9D3E8", blind="C4935E", glass="FFF1D2", ball="EFE6D6", sun="F9DDB0")
