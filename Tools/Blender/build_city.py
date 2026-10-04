"""
Kit modular da cidade steampunk-mágica em volta da praça (D-042, bíblia em Docs/Design/arte-pixel.md).

Uso:
    blender --background --factory-startup --python Tools/Blender/build_city.py
    blender --background --factory-startup --python Tools/Blender/build_city.py -- --preview
    (com --preview, depois de exportar, reimporta os FBX e renderiza imagens em Art/Blender/City/Previews)
    (com --only Nome1,Nome2 gera só essas peças)

Saídas:
    Art/Blender/City/<Peca>.blend
    Assets/_Game/Art/Models/City/<Peca>.fbx

Convenções (lidas pelo CityBuilder na Unity):
    - Raiz Empty com o nome da peça. Z para cima no Blender (Y na Unity). Frente = -Y (na Unity, +Z local).
    - Prédios: pivô no chão, no CENTRO DA FACHADA DA FRENTE; o prédio cresce para +Y (para trás).
      Torre, props e dirigível: pivô no centro da base (dirigível: centro do balão).
    - Filhos:
        Corpo              malha estática principal
        JanelaAndar<N>     vidros JanelaQuente de um andar (WindowFlicker liga/desliga)
        JanelaCristal      vidros CristalArcano (a casa é movida a cristal)
        CristalPulso       cristais que pulsam (EmissivePulse)
        Engrenagem<N>      giram no próprio eixo (fachada: eixo Y do Blender = Z local na Unity)
        Ponteiro*          ponteiros do relógio (eixo Z local na Unity)
        Placa*             placas que balançam (Sway); PlacaArco balança no eixo X
        Bandeirola         franja do toldo (Sway no eixo X)
        Lanterna           lanterna pendurada do lampião (Sway no eixo X)
        Helice             hélice do dirigível (eixo Z local na Unity)
        CristalFlutuante   cristal do pilão (gira no eixo Y da Unity)
    - Marcadores (Empty, sem malha): Fumaca*, Vapor*, Faisca*, Arcano*, LuzQuente*, LuzCristal*.
      O CityBuilder põe partículas e luzes neles.

Pilar 4: a máquina e a magia aparecem na mesma peça (medidor de cristal na casa, engrenagem com núcleo
de cristal, filtro de cristal na chaminé, tanque aquecido por anel de cristal, lampião alimentado por cano).
Escala: 1 px ≈ 3 cm na câmera de jogo; nenhum detalhe com menos de 8 cm.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLEND_DIR = os.path.join(ROOT, "Art", "Blender", "City")
FBX_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Models", "City")
PREVIEW_DIR = os.path.join(BLEND_DIR, "Previews")

GROUND_H = 4.0   # térreo
FLOOR_H = 3.2    # andares de cima

# Paleta da bíblia de arte (hex sRGB, metal, rugosidade, força de emissão).
PALETTE = {
    "FerroEscuro": ("2B2A30", 0.6, 0.6, 0),
    "FerroMedio": ("4A4752", 0.6, 0.55, 0),
    "Cobre": ("B5653A", 0.7, 0.45, 0),
    "CobreOxidado": ("4F8C7A", 0.3, 0.6, 0),
    "Latao": ("C9A04A", 0.8, 0.4, 0),
    "LataoEscuro": ("7A5F2C", 0.6, 0.5, 0),
    "Pedra": ("6A625A", 0.0, 0.9, 0),
    "PedraEscura": ("3A3634", 0.0, 0.9, 0),
    "Tijolo": ("7A3E2E", 0.0, 0.9, 0),
    "Madeira": ("6B4630", 0.0, 0.8, 0),
    "Telhado": ("3B3F52", 0.1, 0.7, 0),
    "Tecido": ("4E4A44", 0.0, 1.0, 0),
    "TecidoEscuro": ("2F2C2A", 0.0, 1.0, 0),
    "Couro": ("6E4A32", 0.0, 0.7, 0),
    "Bandeira": ("7C2F3A", 0.0, 0.9, 0),
    "JanelaQuente": ("FFC070", 0.0, 0.5, 1.6),
    "CristalArcano": ("6FF0FF", 0.0, 0.2, 2.0),
    "BrasaFornalha": ("FF7A20", 0.0, 0.8, 3.0),
}


# ---------- utilidades ----------

def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexcol(h):
    return tuple(_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    hx, metallic, rough, emit = PALETTE[name]
    col = hexcol(hx)
    mat = bpy.data.materials.new(name)
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = col
        bsdf.inputs["Emission Strength"].default_value = emit
    mat.diffuse_color = col
    return mat


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


class Kit:
    """Peça em construção: baldes de objetos (cada balde vira um filho), pivôs e marcadores."""

    def __init__(self, name):
        reset_scene()
        self.name = name
        self.M = Matrix.Identity(4)
        self.buckets = {}
        self.pivots = {}
        self.markers = []
        self.counters = {}


KIT = None


def begin(name):
    global KIT
    KIT = Kit(name)
    return KIT


class xf:
    """Contexto de transformação: tudo criado dentro dele passa pela matriz (fachadas laterais, peças giradas)."""

    def __init__(self, M):
        self.local = M

    def __enter__(self):
        self.prev = KIT.M
        KIT.M = self.prev @ self.local
        return self

    def __exit__(self, *args):
        KIT.M = self.prev


def T(x=0.0, y=0.0, z=0.0, rz=0.0, rx=0.0, ry=0.0):
    return (Matrix.Translation((x, y, z)) @ Matrix.Rotation(rz, 4, "Z")
            @ Matrix.Rotation(ry, 4, "Y") @ Matrix.Rotation(rx, 4, "X"))


def face_m(face, W, D):
    """Matriz que leva a fachada da frente (parede em y=0, normal -Y, u ao longo de +X) para outra face."""
    if face == "front":
        return Matrix.Identity(4)
    if face == "back":
        return T(0, D, 0, rz=math.pi)
    if face == "right":
        return T(W / 2, D / 2, 0, rz=math.pi / 2)
    if face == "left":
        return T(-W / 2, D / 2, 0, rz=-math.pi / 2)
    raise ValueError(face)


def pivot(bucket, loc):
    KIT.pivots[bucket] = KIT.M @ Vector(loc)


def marker(prefix, loc, parent=None):
    n = KIT.counters.get(prefix, 0) + 1
    KIT.counters[prefix] = n
    KIT.markers.append((f"{prefix}{n}", KIT.M @ Vector(loc), parent))


def _finish(obj, mat, bevel=0.0, smooth=False, b="Corpo"):
    obj.data.materials.append(material(mat))
    select_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if smooth:
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
        except Exception:
            bpy.ops.object.shade_smooth()
    obj.matrix_world = KIT.M @ obj.matrix_world
    KIT.buckets.setdefault(b, []).append(obj)
    return obj


def box(mat, size, loc, rot=(0, 0, 0), bevel=0.0, b="Corpo"):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return _finish(o, mat, bevel=bevel, b=b)


def cyl(mat, r, depth, loc, rot=(0, 0, 0), verts=16, scale=(1, 1, 1), bevel=0.0, b="Corpo", r2=None, smooth=True):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return _finish(o, mat, bevel=bevel, smooth=smooth, b=b)


def sphere(mat, r, loc, scale=(1, 1, 1), segments=12, b="Corpo"):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=segments, ring_count=max(4, segments // 2))
    o = bpy.context.active_object
    o.scale = scale
    return _finish(o, mat, smooth=True, b=b)


def torus(mat, major, minor, loc, rot=(0, 0, 0), seg=20, minor_seg=6, b="Corpo"):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot,
                                     major_segments=seg, minor_segments=minor_seg)
    return _finish(bpy.context.active_object, mat, smooth=True, b=b)


def gem(r, loc, scale=(1, 1, 1), rot=(0, 0, 0), b="CristalPulso"):
    """Cristal facetado (icosfera de baixa resolução, sem suavizar)."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return _finish(o, "CristalArcano", b=b)


def beam(mat, p0, p1, w, h=None, b="Corpo"):
    """Viga de seção quadrada entre dois pontos."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bpy.ops.mesh.primitive_cube_add(size=1, location=(p0 + p1) / 2)
    o = bpy.context.active_object
    o.scale = (w, h if h else w, d.length)
    o.rotation_mode = "QUATERNION"
    up = "Y" if abs(d.normalized().y) < 0.99 else "X"
    o.rotation_quaternion = d.to_track_quat("Z", up)
    return _finish(o, mat, b=b)


def mesh(mat, verts, faces, b="Corpo"):
    me = bpy.data.meshes.new("m")
    me.from_pydata(verts, [], faces)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()
    o = bpy.data.objects.new("m", me)
    bpy.context.scene.collection.objects.link(o)
    return _finish(o, mat, b=b)


def multi_prism(mat, polys, a0, a1, axis="Y", b="Corpo"):
    """Extruda polígonos convexos 2D (p, z) ao longo de um eixo, de a0 até a1, numa malha só."""
    verts, faces = [], []
    for pts in polys:
        n = len(pts)
        base = len(verts)
        for a in (a0, a1):
            for p, z in pts:
                verts.append((p, a, z) if axis == "Y" else (a, p, z))
        faces.append([base + i for i in range(n)][::-1])
        faces.append([base + n + i for i in range(n)])
        for i in range(n):
            j = (i + 1) % n
            faces.append([base + i, base + j, base + n + j, base + n + i])
    return mesh(mat, verts, faces, b=b)


def prism(mat, pts, a0, a1, axis="Y", b="Corpo"):
    return multi_prism(mat, [pts], a0, a1, axis, b)


def arch_ring(mat, cx, cz, r0, r1, y0, y1, seg=8, b="Corpo"):
    polys = []
    for i in range(seg):
        a, c = math.pi * i / seg, math.pi * (i + 1) / seg
        polys.append([(cx + r1 * math.cos(a), cz + r1 * math.sin(a)), (cx + r1 * math.cos(c), cz + r1 * math.sin(c)),
                      (cx + r0 * math.cos(c), cz + r0 * math.sin(c)), (cx + r0 * math.cos(a), cz + r0 * math.sin(a))])
    return multi_prism(mat, polys, y0, y1, b=b)


def half_disc(mat, cx, cz, r, y0, y1, seg=8, b="Corpo"):
    pts = [(cx + r * math.cos(math.pi * i / seg), cz + r * math.sin(math.pi * i / seg)) for i in range(seg + 1)]
    return prism(mat, pts, y0, y1, b=b)


def frustum(mat, c, z0, z1, s0, s1, b="Corpo", top_offset=(0, 0)):
    """Tronco de pirâmide: base s0=(largura, profundidade) em z0, topo s1 em z1."""
    cx, cy = c
    ox, oy = top_offset
    w0, d0 = s0[0] / 2, s0[1] / 2
    w1, d1 = s1[0] / 2, s1[1] / 2
    v = [(cx - w0, cy - d0, z0), (cx + w0, cy - d0, z0), (cx + w0, cy + d0, z0), (cx - w0, cy + d0, z0),
         (cx + ox - w1, cy + oy - d1, z1), (cx + ox + w1, cy + oy - d1, z1),
         (cx + ox + w1, cy + oy + d1, z1), (cx + ox - w1, cy + oy + d1, z1)]
    f = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return mesh(mat, v, f, b=b)


def gear(mat, r, teeth, thick, center, axis="Y", b="Engrenagem1", core=True, holes=True):
    """Engrenagem de latão com núcleo de cristal (Pilar 4). Pivô no centro."""
    rot = {"Z": Matrix.Identity(4), "Y": Matrix.Rotation(math.pi / 2, 4, "X"),
           "X": Matrix.Rotation(math.pi / 2, 4, "Y")}[axis]
    pivot(b, center)
    with xf(Matrix.Translation(center) @ rot):
        cyl(mat, r * 0.84, thick, (0, 0, 0), verts=max(16, teeth * 2), b=b)
        tooth_w = 2 * math.pi * r / teeth * 0.5
        for i in range(teeth):
            a = 2 * math.pi * i / teeth
            box(mat, (r * 0.3, max(0.09, tooth_w), thick), (math.cos(a) * r * 0.92, math.sin(a) * r * 0.92, 0),
                rot=(0, 0, a), b=b)
        cyl("FerroEscuro", r * 0.34, thick * 1.5, (0, 0, 0), verts=12, b=b)
        if holes and r > 0.5:
            for i in range(5):
                a = 2 * math.pi * i / 5 + 0.3
                cyl("LataoEscuro", r * 0.16, thick + 0.04, (math.cos(a) * r * 0.58, math.sin(a) * r * 0.58, 0),
                    verts=8, b=b)
        if core:
            gem(r * 0.24, (0, 0, 0), scale=(1, 1, max(1.0, thick * 2.2 / (r * 0.48))), b=b)


def export():
    k = KIT
    os.makedirs(BLEND_DIR, exist_ok=True)
    os.makedirs(FBX_DIR, exist_ok=True)
    root = bpy.data.objects.new(k.name, None)
    bpy.context.scene.collection.objects.link(root)
    parts = {}
    faces = 0
    zmax, xmin, xmax, ymin, ymax = -1e9, 1e9, -1e9, 1e9, -1e9
    for bname, objs in k.buckets.items():
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        p = bpy.context.active_object
        p.name = bname
        p.data.name = f"{k.name}_{bname}"
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for v in p.data.vertices:
            zmax = max(zmax, v.co.z)
            xmin, xmax = min(xmin, v.co.x), max(xmax, v.co.x)
            ymin, ymax = min(ymin, v.co.y), max(ymax, v.co.y)
        if bname in k.pivots:
            bpy.context.scene.cursor.location = k.pivots[bname]
            bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
            bpy.context.scene.cursor.location = (0, 0, 0)
        p.parent = root
        parts[bname] = p
        faces += len(p.data.polygons)
    empties = []
    for name, loc, parent in k.markers:
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.3
        bpy.context.scene.collection.objects.link(e)
        if parent and parent in parts:
            e.parent = parts[parent]
            e.location = loc - parts[parent].location
        else:
            e.parent = root
            e.location = loc
        empties.append(e)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, f"{k.name}.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    for o in [root] + list(parts.values()) + empties:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, f"{k.name}.fbx"),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )
    print(f"[build_city] {k.name}: {faces} faces, {len(parts)} filhos, {len(empties)} marcadores, "
          f"x[{xmin:.1f},{xmax:.1f}] y[{ymin:.1f},{ymax:.1f}] altura {zmax:.1f}")


# ---------- elementos de fachada (coordenadas da fachada: parede em y=0, para fora = -Y) ----------

def window(u, z, w, h, warm=True, floor=1, frame="Madeira", trim="Pedra", mullion=True, shutters=False, top="lintel"):
    b = f"JanelaAndar{floor}" if warm else "JanelaCristal"
    glass = "JanelaQuente" if warm else "CristalArcano"
    ft = 0.12
    box(glass, (w, 0.08, h), (u, -0.01, z + h / 2), b=b)
    box(frame, (w + 2 * ft, 0.14, ft), (u, -0.07, z - ft / 2))
    box(frame, (ft, 0.14, h), (u - w / 2 - ft / 2, -0.07, z + h / 2))
    box(frame, (ft, 0.14, h), (u + w / 2 + ft / 2, -0.07, z + h / 2))
    if mullion:
        box(frame, (0.1, 0.09, h), (u, -0.08, z + h / 2))
        box(frame, (w, 0.09, 0.1), (u, -0.08, z + h * 0.62))
    box(trim, (w + 0.5, 0.28, 0.14), (u, -0.14, z - ft - 0.07))
    if top == "arch":
        half_disc(glass, u, z + h, w / 2, -0.05, 0.03, b=b)
        arch_ring(frame, u, z + h, w / 2, w / 2 + ft, -0.14, 0.0)
        arch_ring(trim, u, z + h, w / 2 + ft, w / 2 + ft + 0.16, -0.12, 0.0)
        box(trim, (0.26, 0.22, 0.34), (u, -0.13, z + h + w / 2 + ft + 0.1))
    else:
        box(frame, (w + 2 * ft, 0.14, ft), (u, -0.07, z + h + ft / 2))
        if top == "lintel":
            box(trim, (w + 0.44, 0.2, 0.22), (u, -0.1, z + h + ft + 0.11))
            box(trim, (0.26, 0.26, 0.32), (u, -0.14, z + h + ft + 0.12))
    if shutters:
        for s in (-1, 1):
            box("Madeira", (w * 0.5, 0.08, h + 0.1), (u + s * (w * 0.75 + ft + 0.04), -0.05, z + h / 2))
            box("LataoEscuro", (w * 0.5, 0.1, 0.1), (u + s * (w * 0.75 + ft + 0.04), -0.06, z + h * 0.3))


def door(u, w, h, mat="Madeira", frame="Latao", fan=True, floor=0):
    box(mat, (w, 0.12, h), (u, -0.02, h / 2))
    for zz in (0.3, 0.68):
        for s in (-1, 1):
            box(mat, (w * 0.36, 0.08, h * 0.26), (u + s * w * 0.22, -0.1, h * zz))
    sphere("Latao", 0.08, (u + w * 0.36, -0.14, h * 0.5), segments=8)
    for s in (-1, 1):
        box(frame, (0.16, 0.2, h), (u + s * (w / 2 + 0.08), -0.08, h / 2))
    box(frame, (w + 0.32, 0.2, 0.14), (u, -0.08, h + 0.07))
    if fan:
        half_disc("JanelaQuente", u, h + 0.14, w / 2, -0.05, 0.03, b=f"JanelaAndar{floor}")
        box(frame, (0.1, 0.09, w / 2), (u, -0.08, h + 0.14 + w / 4))
        arch_ring(frame, u, h + 0.14, w / 2, w / 2 + 0.16, -0.16, 0.0)
    box("PedraEscura", (w + 0.6, 0.5, 0.16), (u, -0.25, 0.08))


def awning(u, w, z_top, depth=1.2, drop=0.55, stripes=True):
    """Toldo listrado inclinado (Bandeira e Tecido) com mãos-francesas de ferro."""
    n = max(3, round(w / 0.5))
    sw = w / n
    th = math.atan2(drop, depth)
    L = math.hypot(depth, drop)
    for i in range(n):
        mat = "Bandeira" if (i % 2 == 0 or not stripes) else "Tecido"
        box(mat, (sw, L, 0.07), (u - w / 2 + sw * (i + 0.5), -depth / 2, z_top - drop / 2), rot=(th, 0, 0))
    box("Bandeira", (w, 0.06, 0.24), (u, -depth - 0.02, z_top - drop - 0.1))
    for s in (-1, 1):
        beam("FerroEscuro", (u + s * (w / 2 - 0.1), 0, z_top - drop - 0.7), (u + s * (w / 2 - 0.1), -depth + 0.1, z_top - drop),
             0.08)


def shopfront(u, w, h=2.5, z0=0.55, floor=0, awn=True, sign=True):
    """Vitrine de loja: base de madeira, vidro quente em gomos de latão, testeira e toldo."""
    box("Madeira", (w + 0.2, 0.24, z0), (u, -0.1, z0 / 2))
    box("JanelaQuente", (w, 0.08, h), (u, -0.01, z0 + h / 2), b=f"JanelaAndar{floor}")
    n = max(2, round(w / 0.9))
    for i in range(1, n):
        box("Latao", (0.1, 0.1, h), (u - w / 2 + w * i / n, -0.08, z0 + h / 2))
    box("Latao", (w, 0.1, 0.1), (u, -0.08, z0 + h * 0.75))
    for s in (-1, 1):
        box("Madeira", (0.2, 0.22, h + 0.2), (u + s * (w / 2 + 0.1), -0.1, z0 + h / 2))
    box("Madeira", (w + 0.6, 0.26, 0.2), (u, -0.12, z0 - 0.02))
    if sign:
        box("Madeira", (w + 0.4, 0.22, 0.55), (u, -0.12, z0 + h + 0.3))
        box("Latao", (w + 0.5, 0.26, 0.09), (u, -0.13, z0 + h + 0.6))
        box("Latao", (w * 0.5, 0.08, 0.16), (u, -0.26, z0 + h + 0.3))
    if awn:
        awning(u, w + 0.3, z0 + h + 0.95, depth=1.1, drop=0.55)


def chimney(x, y, z0, h, mat="Tijolo", smoke=True):
    box(mat, (0.9, 0.7, h), (x, y, z0 + h / 2), bevel=0.03)
    box("PedraEscura", (1.1, 0.9, 0.16), (x, y, z0 + h + 0.08))
    for dx in (-0.2, 0.2):
        cyl("FerroEscuro", 0.13, 0.45, (x + dx, y, z0 + h + 0.38), verts=8)
    if smoke:
        marker("Fumaca", (x - 0.2, y, z0 + h + 0.65))


def vpipe(x, y, z0, z1, r=0.12, mat="Cobre", clamp_every=1.6):
    cyl(mat, r, z1 - z0, (x, y, (z0 + z1) / 2), verts=10)
    z = z0 + 0.6
    while z < z1 - 0.3:
        cyl("Latao", r + 0.04, 0.1, (x, y, z), verts=10)
        z += clamp_every


def valve(x, y, z, facing="-Y", r=0.22):
    """Volante de válvula de latão, voltado para fora da parede (-Y)."""
    rot = (math.pi / 2, 0, 0) if facing in ("-Y", "Y") else (0, math.pi / 2, 0)
    torus("Latao", r, 0.05, (x, y, z), rot=rot, seg=12, minor_seg=6)
    if facing in ("-Y", "Y"):
        box("Latao", (r * 2, 0.07, 0.08), (x, y, z))
        box("Latao", (0.08, 0.07, r * 2), (x, y, z))
        cyl("FerroEscuro", 0.06, 0.3, (x, y + 0.15, z), rot=rot, verts=8)
    else:
        box("Latao", (0.07, r * 2, 0.08), (x, y, z))
        box("Latao", (0.07, 0.08, r * 2), (x, y, z))


def crystal_meter(u, z):
    """Medidor de cristal na fachada: a casa funciona com cristal (Pilar 4)."""
    box("Latao", (0.5, 0.3, 0.85), (u, -0.15, z), bevel=0.03)
    box("FerroEscuro", (0.36, 0.1, 0.62), (u, -0.3, z))
    cyl("CristalArcano", 0.11, 0.5, (u, -0.33, z), verts=6, b="CristalPulso", smooth=False)
    cyl("Latao", 0.13, 0.08, (u, -0.33, z + 0.28), verts=8)
    cyl("Latao", 0.13, 0.08, (u, -0.33, z - 0.28), verts=8)
    cyl("Cobre", 0.08, 1.6, (u, -0.12, z + 1.2), verts=8)


def hanging_sign(u, z, length=1.0, bucket="Placa"):
    """Braço de ferro saindo da parede com placa pendurada (filho que balança). Placa no plano YZ."""
    box("Latao", (0.14, 0.08, 0.5), (u, -0.04, z - 0.15))
    box("FerroEscuro", (0.1, length + 0.1, 0.1), (u, -length / 2 - 0.05, z))
    sphere("Latao", 0.09, (u, -length - 0.08, z), segments=8)
    beam("FerroEscuro", (u, -0.04, z - 0.55), (u, -length * 0.55, z - 0.04), 0.08)
    hy = -length * 0.62
    pivot(bucket, (u, hy, z - 0.05))
    bw = min(0.9, length * 0.85)
    for dy in (-bw * 0.38, bw * 0.38):
        box("FerroEscuro", (0.08, 0.08, 0.2), (u, hy + dy, z - 0.15), b=bucket)
    box("Madeira", (0.1, bw, 0.62), (u, hy, z - 0.56), b=bucket)
    box("Latao", (0.14, bw + 0.08, 0.08), (u, hy, z - 0.25), b=bucket)
    box("Latao", (0.14, bw + 0.08, 0.08), (u, hy, z - 0.87), b=bucket)
    cyl("Latao", 0.2, 0.16, (u, hy, z - 0.56), rot=(0, math.pi / 2, 0), verts=12, b=bucket)
    for i in range(8):
        a = i * math.pi / 4
        box("Latao", (0.16, 0.09, 0.09), (u, hy + math.cos(a) * 0.22, z - 0.56 + math.sin(a) * 0.22), rot=(a, 0, 0),
            b=bucket)
    gem(0.09, (u, hy, z - 0.56), scale=(1.4, 1, 1), b=bucket)


def gable_roof(W, D, z0, rise, roof="Telhado", gable="Tijolo", ov=0.35, ridge="Latao"):
    """Telhado de duas águas com a empena virada para a frente."""
    prism(gable, [(-W / 2, z0), (W / 2, z0), (0, z0 + rise)], 0, D)
    th = math.atan2(rise, W / 2)
    s = W / 2 + ov
    L = s / math.cos(th)
    t = 0.24
    for sgn in (-1, 1):
        cx = sgn * s / 2
        cz = z0 + rise - (s / 2) * math.tan(th) + t / 2 / math.cos(th)
        box(roof, (L, D + 2 * ov, t), (cx, D / 2, cz), rot=(0, sgn * th, 0))
    box(ridge, (0.3, D + 2 * ov + 0.1, 0.2), (0, D / 2, z0 + rise + t / math.cos(th)))
    # Faixa de beiral na empena (borda grossa lê melhor em pixel).
    for sgn in (-1, 1):
        beam("PedraEscura", (0, -ov + 0.05, z0 + rise + 0.1), (sgn * s, -ov + 0.05, z0 - ov * math.tan(th) + 0.1), 0.18)
    return z0 + rise


# ---------- prédios ----------

def casa_estreita(name, floors, upper="Tijolo", roof="Telhado", crystal_floor=1, sign_side=1, second_chimney=False):
    begin(name)
    W, D = 6.0, 8.0
    zt = GROUND_H + floors * FLOOR_H
    box("PedraEscura", (W + 0.2, D + 0.2, 0.4), (0, D / 2, 0.2), bevel=0.04)
    box("Pedra", (W, D, GROUND_H), (0, D / 2, GROUND_H / 2), bevel=0.04)
    box(upper, (W, D, zt - GROUND_H), (0, D / 2, (GROUND_H + zt) / 2), bevel=0.04)
    for s in (-1, 1):
        box("Pedra", (0.4, 0.4, zt - GROUND_H), (s * (W / 2 - 0.15), 0.12, (GROUND_H + zt) / 2))
    box("Pedra", (W + 0.2, 0.32, 0.3), (0, 0.0, GROUND_H))
    for i in range(1, floors):
        box("PedraEscura", (W, 0.2, 0.14), (0, 0.0, GROUND_H + i * FLOOR_H))
    box("PedraEscura", (W + 0.4, 0.5, 0.35), (0, 0.0, zt - 0.1))

    # Térreo: vitrine, medidor de cristal e porta.
    shopfront(-1.0, 2.6)
    marker("LuzQuente", (-1.0, -1.3, 2.6))
    crystal_meter(0.75, 1.7)
    door(1.75, 1.15, 2.6)
    hanging_sign(sign_side * 2.75, 3.65, length=1.1)

    for i in range(floors):
        z = GROUND_H + i * FLOOR_H + 0.85
        for j, u in enumerate((-1.4, 1.4)):
            warm = not (i == crystal_floor and j == 1)
            window(u, z, 1.05, 1.7, warm=warm, floor=i + 1, shutters=(upper == "Tijolo" and i % 2 == 0),
                   top="arch" if i == floors - 1 else "lintel")
    # Varanda de ferro no primeiro andar.
    box("FerroEscuro", (W - 1.2, 0.8, 0.14), (0, -0.4, GROUND_H + 0.55))
    box("Latao", (W - 1.2, 0.08, 0.08), (0, -0.76, GROUND_H + 1.5))
    for i in range(9):
        box("FerroEscuro", (0.08, 0.08, 0.9), (-(W - 1.3) / 2 + i * (W - 1.3) / 8, -0.76, GROUND_H + 1.05))

    ridge = gable_roof(W, D, zt, 3.0, roof=roof, gable=upper)
    cyl("CristalArcano", 0.45, 0.1, (0, -0.02, zt + 1.15), rot=(math.pi / 2, 0, 0), verts=12, b="JanelaCristal",
        smooth=False)
    torus("Latao", 0.52, 0.09, (0, -0.07, zt + 1.15), rot=(math.pi / 2, 0, 0), seg=14)
    box("Latao", (0.9, 0.06, 0.09), (0, -0.1, zt + 1.15))
    box("Latao", (0.09, 0.06, 0.9), (0, -0.1, zt + 1.15))
    chimney(W / 2 - 0.8, D * 0.62, zt, ridge - zt + 1.2)
    if second_chimney:
        chimney(-W / 2 + 0.8, D * 0.3, zt, ridge - zt + 0.8)

    # Calha de cobre no canto, com válvula de alívio perto do chão (vapor visível da praça).
    vpipe(-W / 2 - 0.12, 0.25, 0.4, zt, r=0.12)
    cyl("Cobre", 0.12, 0.45, (-W / 2 - 0.12, 0.02, 1.0), rot=(math.pi / 2, 0, 0), verts=10)
    valve(-W / 2 - 0.12, -0.25, 1.0)
    marker("Vapor", (-W / 2 - 0.12, -0.45, 0.75))

    with xf(face_m("back", W, D)):
        for i in range(floors):
            window(0, GROUND_H + i * FLOOR_H + 0.85, 1.0, 1.6, floor=i + 1, mullion=False)
        door(1.6, 1.0, 2.4, fan=False)
    for face in ("left", "right"):
        with xf(face_m(face, W, D)):
            window(0.8, GROUND_H + 0.85, 0.8, 1.4, floor=1, mullion=False, top="none")
    export()


def casa_larga():
    begin("CasaLarga")
    W, D = 10.0, 9.0
    zu = GROUND_H + 3.4
    box("PedraEscura", (W + 0.2, D + 0.2, 0.4), (0, D / 2, 0.2), bevel=0.04)
    box("Pedra", (W, D, GROUND_H), (0, D / 2, GROUND_H / 2), bevel=0.04)
    # Andar de cima em balanço (0,3 m) com enxaimel de madeira.
    J = 0.3
    box("Tijolo", (W, D + J, zu - GROUND_H), (0, (D - J) / 2, (GROUND_H + zu) / 2), bevel=0.03)
    for u in (-W / 2 + 0.12, -2.5, 0.0, 2.5, W / 2 - 0.12):
        box("Madeira", (0.24, 0.14, zu - GROUND_H), (u, -J - 0.05, (GROUND_H + zu) / 2))
        beam("Madeira", (u, -0.02, GROUND_H - 0.7), (u, -J, GROUND_H + 0.02), 0.18)
    for z in (GROUND_H + 0.12, zu - 0.12):
        box("Madeira", (W + 0.1, 0.16, 0.24), (0, -J - 0.06, z))
    for u0 in (-5.0, 2.5):
        beam("Madeira", (u0 + 0.15, -J - 0.04, GROUND_H + 0.25), (u0 + 2.35, -J - 0.04, GROUND_H + 1.1), 0.16)

    shopfront(-3.1, 2.8)
    shopfront(3.1, 2.8)
    marker("LuzQuente", (3.1, -1.3, 2.6))
    crystal_meter(-1.25, 1.7)
    door(0.4, 1.5, 2.8)
    hanging_sign(-4.95, 3.6, length=1.1)

    with xf(Matrix.Translation((0, -J, 0))):
        for j, u in enumerate((-3.75, -1.25, 1.25, 3.75)):
            window(u, GROUND_H + 1.05, 1.15, 1.55, warm=(j != 2), floor=1, frame="Madeira", trim="Madeira",
                   shutters=(j in (0, 3)))

    # Mansarda de cobre oxidado com trapeiras.
    zm = zu + 2.6
    frustum("CobreOxidado", (0, D / 2 - J / 2), zu, zm, (W + 0.3, D + J + 0.3), (W - 1.8, D - 1.8))
    box("Telhado", (W - 1.6, D - 1.6, 0.25), (0, D / 2 - J / 2, zm + 0.12))
    box("Latao", (W + 0.4, D + J + 0.4, 0.14), (0, D / 2 - J / 2, zu + 0.07))
    for j, u in enumerate((-3.0, 0.0, 3.0)):
        yf = -J + 0.15
        box("Madeira", (1.5, 1.6, 1.7), (u, yf + 0.8, zu + 0.95))
        prism("CobreOxidado", [(u - 0.95, zu + 1.75), (u + 0.95, zu + 1.75), (u, zu + 2.5)], yf - 0.2, yf + 1.6)
        with xf(Matrix.Translation((0, yf, 0))):
            window(u, zu + 0.35, 0.8, 1.05, warm=(j != 1), floor=2, mullion=False, top="none", trim="Madeira")
    chimney(-W / 2 + 1.3, D * 0.62, zm - 0.6, 2.0)
    chimney(W / 2 - 1.3, D * 0.62, zm - 0.6, 2.4)
    # Tubulação de cristal subindo pela lateral até a mansarda.
    vpipe(W / 2 + 0.14, 1.0, 0.4, zu + 0.6, r=0.13)
    box("Latao", (0.45, 0.5, 0.7), (W / 2 + 0.2, 1.0, 5.6), bevel=0.03)
    gem(0.2, (W / 2 + 0.44, 1.0, 5.6), scale=(1, 1, 1.4))
    valve(W / 2 + 0.36, 1.0, 1.2, facing="X")
    marker("Vapor", (W / 2 + 0.4, 1.0, 0.8))

    with xf(face_m("back", W, D)):
        for u in (-3, 0, 3):
            window(u, GROUND_H + 1.05, 1.0, 1.5, floor=1, mullion=False)
    for face in ("left", "right"):
        with xf(face_m(face, W, D)):
            window(-1.5, GROUND_H + 1.05, 0.9, 1.4, floor=1, mullion=False, top="none")
            beam("Madeira", (-3.8, -0.05, GROUND_H + 0.2), (3.8, -0.05, zu - 0.2), 0.16)
    export()


def casa_alta():
    begin("CasaAlta")
    W, D = 7.0, 8.0
    G = 4.5
    floors = 4
    zt = G + floors * FLOOR_H
    box("PedraEscura", (W + 0.2, D + 0.2, 0.5), (0, D / 2, 0.25), bevel=0.04)
    box("Pedra", (W, D, G), (0, D / 2, G / 2), bevel=0.04)
    box("FerroMedio", (W, D, zt - G), (0, D / 2, (G + zt) / 2), bevel=0.04)
    # Chapas rebitadas: faixas de ferro em cada andar e costuras verticais.
    for i in range(floors + 1):
        box("FerroEscuro", (W + 0.1, D + 0.1, 0.16), (0, D / 2, G + i * FLOOR_H))
    for u in (-W / 2 + 0.06, 0.0, W / 2 - 0.06):
        box("FerroEscuro", (0.16, 0.12, zt - G), (u, -0.04, (G + zt) / 2))
    box("Pedra", (W + 0.3, 0.4, 0.35), (0, 0, G))

    door(0, 1.8, 3.0, frame="Latao")
    for u in (-2.3, 2.3):
        window(u, 1.1, 0.95, 2.0, floor=0, frame="Latao", trim="Pedra", top="arch")
    hanging_sign(-W / 2 + 0.3, 4.0, length=1.1)
    marker("LuzQuente", (0, -1.0, 3.6))

    for i in range(floors):
        z = G + i * FLOOR_H + 0.8
        crystal = i in (1, 2)
        for u in (-1.6, 1.3):
            window(u, z, 1.2, 1.75, warm=not crystal, floor=i + 1, frame="Latao", trim="FerroEscuro",
                   top="arch" if i == floors - 1 else "lintel")
    # Varanda no primeiro andar.
    box("FerroEscuro", (W - 0.8, 1.0, 0.16), (0, -0.5, G + 0.1))
    box("Latao", (W - 0.8, 0.09, 0.09), (0, -0.96, G + 1.1))
    for i in range(12):
        box("FerroEscuro", (0.08, 0.08, 0.95), (-(W - 0.9) / 2 + i * (W - 0.9) / 11, -0.96, G + 0.62))
    for s in (-1, 1):
        beam("FerroEscuro", (s * 2.8, -0.02, G - 0.8), (s * 2.8, -0.9, G + 0.02), 0.12)

    # Canos de cobre pela fachada, com junção de cristal (energia da casa).
    for u in (2.85, 3.18):
        vpipe(u, -0.22, 0.5, zt + 0.4, r=0.12)
    box("Latao", (0.85, 0.5, 0.7), (3.0, -0.25, 12.6), bevel=0.04)
    gem(0.24, (3.0, -0.52, 12.6), scale=(1, 0.7, 1.4))
    box("Latao", (0.85, 0.5, 0.5), (3.0, -0.25, 7.0), bevel=0.04)
    gem(0.18, (3.0, -0.52, 7.0), scale=(1, 0.7, 1.2))
    valve(3.0, -0.5, 1.2)
    marker("Vapor", (3.25, -0.6, 0.8))

    # Coroamento: platibanda, cúpula de cobre oxidado com lanterna de cristal e chaminé.
    box("FerroEscuro", (W + 0.3, D + 0.3, 0.7), (0, D / 2, zt + 0.35), bevel=0.03)
    box("FerroMedio", (W - 0.4, D - 0.4, 0.3), (0, D / 2, zt + 0.5))
    frustum("CobreOxidado", (0, D * 0.55), zt + 0.6, zt + 2.6, (3.2, 3.2), (1.0, 1.0))
    cyl("Latao", 0.55, 0.2, (0, D * 0.55, zt + 2.7), verts=12)
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        box("Latao", (0.12, 0.12, 1.0), (math.cos(a) * 0.4, D * 0.55 + math.sin(a) * 0.4, zt + 3.3))
    gem(0.32, (0, D * 0.55, zt + 3.3), scale=(1, 1, 1.4))
    cyl("CobreOxidado", 0.6, 0.6, (0, D * 0.55, zt + 4.05), r2=0.05, verts=8, smooth=False)
    marker("LuzCristal", (0, D * 0.55, zt + 3.3))
    cyl("Cobre", 0.3, 3.0, (-2.4, D * 0.75, zt + 1.6), verts=12)
    cyl("FerroEscuro", 0.42, 0.3, (-2.4, D * 0.75, zt + 3.2), verts=12)
    marker("Fumaca", (-2.4, D * 0.75, zt + 3.5))

    with xf(face_m("back", W, D)):
        for i in range(floors):
            window(0, G + i * FLOOR_H + 0.8, 1.1, 1.6, floor=i + 1, mullion=False, frame="Latao", trim="FerroEscuro")
    for face in ("left", "right"):
        with xf(face_m(face, W, D)):
            for i in (0, 2):
                window(-1.2, G + i * FLOOR_H + 0.8, 0.9, 1.5, floor=i + 1, mullion=False, top="none",
                       frame="Latao", trim="FerroEscuro")
    export()


def oficina():
    begin("Oficina")
    W, D = 8.0, 7.0
    H = 4.6
    box("PedraEscura", (W + 0.2, D + 0.2, 0.35), (0, D / 2, 0.17), bevel=0.04)
    box("Tijolo", (W, D, H), (0, D / 2, H / 2), bevel=0.04)
    for s in (-1, 1):
        box("Pedra", (0.45, 0.4, H + 0.3), (s * (W / 2 - 0.15), 0.12, (H + 0.3) / 2))
    # Platibanda escalonada na frente; telhado de uma água caindo para trás.
    box("Tijolo", (W, 0.45, 1.1), (0, 0.22, H + 0.55))
    box("Tijolo", (W * 0.5, 0.45, 0.6), (0, 0.22, H + 1.4))
    box("Pedra", (W + 0.15, 0.6, 0.14), (0, 0.22, H + 1.12))
    box("Pedra", (W * 0.5 + 0.15, 0.6, 0.14), (0, 0.22, H + 1.75))
    th = math.atan2(1.1, D)
    box("Telhado", (W + 0.3, math.hypot(D, 1.1) + 0.3, 0.22), (0, D / 2 + 0.3, H + 0.55), rot=(-th, 0, 0))
    box("JanelaQuente", (2.4, 2.0, 0.12), (-1.2, D * 0.45, H + 0.72), rot=(-th, 0, 0), b="JanelaAndar1")
    box("FerroEscuro", (2.7, 2.3, 0.1), (-1.2, D * 0.45, H + 0.66), rot=(-th, 0, 0))

    # Portão grande de madeira com cintas de ferro e bandeira em arco.
    door(-1.9, 2.9, 2.9, frame="FerroEscuro")
    for z in (0.8, 2.1):
        box("FerroEscuro", (2.9, 0.16, 0.14), (-1.9, -0.12, z))
    window(1.0, 1.2, 1.6, 1.35, floor=0, frame="FerroEscuro", trim="Pedra")
    marker("LuzQuente", (1.0, -1.0, 2.4))

    # Engrenagens na fachada movidas por um motor de cristal (Pilar 4).
    cyl("LataoEscuro", 1.1, 0.12, (2.75, -0.06, 3.0), rot=(math.pi / 2, 0, 0), verts=16)
    gear("Latao", 0.95, 12, 0.2, (2.75, -0.28, 3.0), b="Engrenagem1")
    gear("Latao", 0.55, 8, 0.2, (1.55, -0.28, 3.72), b="Engrenagem2", holes=False)
    box("FerroEscuro", (0.9, 0.7, 1.1), (3.2, -0.35, 0.95), bevel=0.04)
    box("CristalArcano", (0.5, 0.08, 0.6), (3.2, -0.71, 1.0), b="CristalPulso")
    box("Latao", (0.6, 0.1, 0.1), (3.2, -0.72, 1.35))
    vpipe(3.2, -0.3, 1.5, 2.2, r=0.1)
    marker("Faisca", (3.2, -0.8, 1.6))

    # Chaminé de metal com chapéu de faíscas.
    cyl("FerroEscuro", 0.36, 4.4, (2.6, D - 1.6, H + 1.6), verts=12)
    for z in (H + 0.9, H + 2.3):
        cyl("Cobre", 0.42, 0.18, (2.6, D - 1.6, z), verts=12)
    cyl("FerroEscuro", 0.6, 0.45, (2.6, D - 1.6, H + 4.05), r2=0.15, verts=12)
    marker("Fumaca", (2.6, D - 1.6, H + 3.75))
    marker("Faisca", (2.6, D - 1.6, H + 3.85))

    vpipe(-W / 2 - 0.12, 1.0, 0.3, H, r=0.11)
    valve(-W / 2 - 0.12, -0.15, 0.9)
    cyl("Cobre", 0.11, 1.0, (-W / 2 - 0.12, 0.5, 0.9), rot=(math.pi / 2, 0, 0), verts=10)
    marker("Vapor", (-W / 2 - 0.15, -0.35, 0.6))

    with xf(face_m("back", W, D)):
        window(-1.5, 1.2, 1.4, 1.3, floor=1, frame="FerroEscuro", mullion=False)
    with xf(face_m("right", W, D)):
        window(0.5, 1.2, 1.4, 1.3, floor=1, frame="FerroEscuro", mullion=False)
    export()


def torre_relogio():
    begin("TorreRelogio")
    B = 6.4
    box("PedraEscura", (B + 0.8, B + 0.8, 0.6), (0, 0, 0.3), bevel=0.05)
    box("Pedra", (B, B, 7.0), (0, 0, 3.5), bevel=0.05)
    for z in (2.6, 5.0):
        box("PedraEscura", (B + 0.16, B + 0.16, 0.22), (0, 0, z))
    with xf(Matrix.Translation((0, -B / 2, 0))):
        door(0, 2.2, 3.2, frame="Latao")
        marker("LuzQuente", (0, -1.0, 3.3))
    S = 5.4
    box("Tijolo", (S, S, 10.0), (0, 0, 12.0), bevel=0.04)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Pedra", (0.7, 0.7, 10.0), (sx * (S / 2 - 0.2), sy * (S / 2 - 0.2), 12.0))
    for face in ("front", "back", "left", "right"):
        with xf(T(0, 0, 0, rz={"front": 0, "back": math.pi, "left": -math.pi / 2, "right": math.pi / 2}[face])
                @ Matrix.Translation((0, -S / 2, 0))):
            for z in (9.0, 12.8):
                window(0, z, 0.7, 2.0, floor=1, mullion=False, top="arch", trim="Pedra", frame="FerroEscuro")
    box("Pedra", (6.2, 6.2, 0.4), (0, 0, 17.2))
    # Andar do relógio: mostradores de cristal nos quatro lados.
    C = 6.0
    box("Pedra", (C, C, 4.4), (0, 0, 19.6), bevel=0.05)
    box("Latao", (C + 0.25, C + 0.25, 0.25), (0, 0, 21.8))
    zc = 19.6
    for k, face in enumerate(("front", "right", "back", "left")):
        rz = {"front": 0, "back": math.pi, "left": -math.pi / 2, "right": math.pi / 2}[face]
        with xf(T(0, 0, 0, rz=rz) @ Matrix.Translation((0, -C / 2, 0))):
            cyl("CristalArcano", 1.75, 0.1, (0, -0.02, zc), rot=(math.pi / 2, 0, 0), verts=24, b="CristalPulso",
                smooth=False)
            torus("Latao", 1.86, 0.13, (0, -0.06, zc), rot=(math.pi / 2, 0, 0), seg=24)
            for i in range(12):
                a = i * math.pi / 6
                box("FerroEscuro", (0.12, 0.08, 0.36 if i % 3 == 0 else 0.22),
                    (math.sin(a) * 1.45, -0.1, zc + math.cos(a) * 1.45), rot=(0, a, 0))
            cyl("Latao", 0.18, 0.2, (0, -0.15, zc), rot=(math.pi / 2, 0, 0), verts=10)
            if face == "front":
                pivot("PonteiroHora", (0, -0.2, zc))
                box("FerroEscuro", (0.16, 0.07, 0.95), (0, -0.2, zc + 0.42), b="PonteiroHora")
                box("Latao", (0.26, 0.08, 0.26), (0, -0.2, zc + 0.85), rot=(0, math.pi / 4, 0), b="PonteiroHora")
                pivot("PonteiroMinuto", (0, -0.28, zc))
                box("FerroEscuro", (0.11, 0.07, 1.45), (0, -0.28, zc + 0.62), b="PonteiroMinuto")
                box("FerroEscuro", (0.1, 0.07, 0.4), (0, -0.28, zc - 0.15), b="PonteiroMinuto")
            else:
                box("FerroEscuro", (0.16, 0.07, 0.95), (0.3, -0.2, zc + 0.3), rot=(0, 0.7 + k, 0))
                box("FerroEscuro", (0.11, 0.07, 1.4), (-0.4, -0.28, zc + 0.4), rot=(0, -1.0 - k, 0))
    # Campanário aberto com as engrenagens do relógio à mostra.
    box("FerroEscuro", (C, C, 0.3), (0, 0, 22.0))
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("FerroEscuro", (0.55, 0.55, 3.4), (sx * (C / 2 - 0.3), sy * (C / 2 - 0.3), 23.85))
            cyl("CobreOxidado", 0.4, 1.3, (sx * (C / 2 - 0.3), sy * (C / 2 - 0.3), 26.4), r2=0.04, verts=8,
                smooth=False)
            sphere("Latao", 0.16, (sx * (C / 2 - 0.3), sy * (C / 2 - 0.3), 27.1), segments=8)
    for face_rz in (0, math.pi / 2, math.pi, -math.pi / 2):
        with xf(T(0, 0, 0, rz=face_rz) @ Matrix.Translation((0, -C / 2 + 0.3, 0))):
            arch_ring("FerroEscuro", 0, 24.0, 2.15, 2.45, -0.2, 0.2, seg=8)
            box("Latao", (C - 0.6, 0.3, 0.16), (0, 0, 22.8))
    gear("Latao", 1.35, 14, 0.25, (-0.5, 0, 23.6), b="Engrenagem1")
    gear("Latao", 0.85, 10, 0.25, (1.62, 0, 23.25), b="Engrenagem2")
    gear("LataoEscuro", 0.7, 9, 0.22, (-0.2, 0, 22.6), axis="Z", b="Engrenagem3", holes=False)
    cyl("FerroEscuro", 0.12, C - 0.6, (0, 0, 23.6), rot=(math.pi / 2, 0, 0), verts=8)
    box("FerroEscuro", (C + 0.2, C + 0.2, 0.3), (0, 0, 25.7))
    box("Latao", (C + 0.35, C + 0.35, 0.14), (0, 0, 25.55))
    # Agulha de cobre oxidado com cristal no topo.
    frustum("CobreOxidado", (0, 0), 25.85, 31.0, (C - 0.4, C - 0.4), (0.35, 0.35))
    for face_rz in (0, math.pi / 2, math.pi, -math.pi / 2):
        with xf(T(0, 0, 0, rz=face_rz)):
            box("Latao", (0.1, 0.12, 4.0), (0, -1.55, 28.2), rot=(-0.52, 0, 0))
    cyl("Latao", 0.22, 0.6, (0, 0, 31.3), verts=8)
    torus("Latao", 0.45, 0.07, (0, 0, 32.0), seg=12)
    gem(0.4, (0, 0, 32.1), scale=(1, 1, 1.8))
    marker("LuzCristal", (0, 0, 32.1))
    marker("Arcano", (0, 0, 32.1))
    export()


def ponte_canos():
    begin("PonteCanos")
    L = 6.0
    for s in (-1, 1):
        box("FerroEscuro", (L, 0.16, 0.22), (0, s * 0.62, 0.11))
        box("FerroEscuro", (L, 0.14, 0.14), (0, s * 0.62, 1.15))
    box("FerroMedio", (L, 1.3, 0.08), (0, 0, 0.24))
    n = 5
    for i in range(n + 1):
        x = -L / 2 + 0.07 + i * (L - 0.14) / n
        for s in (-1, 1):
            box("FerroEscuro", (0.12, 0.12, 1.1), (x, s * 0.62, 0.65))
            if i < n:
                x2 = -L / 2 + 0.07 + (i + 1) * (L - 0.14) / n
                beam("FerroEscuro", (x, s * 0.62, 0.2), (x2, s * 0.62, 1.1), 0.09)
    pipes = [(-0.32, 0.55, 0.22, "Cobre"), (0.34, 0.5, 0.18, "Cobre"), (0.0, 0.92, 0.15, "CobreOxidado")]
    for y, z, r, mat in pipes:
        cyl(mat, r, L, (0, y, z), rot=(0, math.pi / 2, 0), verts=12)
        for x in (-L / 2 + 0.06, L / 2 - 0.06):
            cyl("Latao", r + 0.08, 0.12, (x, y, z), rot=(0, math.pi / 2, 0), verts=12)
    for x in (-2.0, 0.0, 2.0):
        box("Latao", (0.16, 1.36, 0.14), (x, 0, 0.32))
        box("Latao", (0.14, 0.12, 0.8), (x, -0.62, 0.65))
    # Junções de cristal: a energia corre pelos canos.
    for x in (-1.2, 1.2):
        cyl("Latao", 0.36, 0.55, (x, -0.32, 0.55), rot=(0, math.pi / 2, 0), verts=12)
        gem(0.3, (x, -0.32, 0.55), scale=(0.8, 1.15, 1.15))
        for dx in (-0.3, 0.3):
            torus("Latao", 0.3, 0.05, (x + dx, -0.32, 0.55), rot=(0, math.pi / 2, 0), seg=12)
    cyl("Cobre", 0.08, 0.5, (0.6, 0.34, 0.9), verts=8)
    cyl("FerroEscuro", 0.13, 0.12, (0.6, 0.34, 1.18), verts=8)
    marker("Vapor", (0.6, 0.34, 1.3))
    export()


def banca_mercado():
    begin("BancaMercado")
    box("Madeira", (2.8, 0.8, 1.0), (0, -0.4, 0.5), bevel=0.03)
    box("LataoEscuro", (2.95, 0.95, 0.08), (0, -0.4, 1.04))
    for u in (-0.95, 0.0, 0.95):
        box("Madeira", (0.8, 0.06, 0.6), (u, -0.82, 0.5))
    box("Madeira", (2.8, 0.45, 1.7), (0, 0.75, 0.85), bevel=0.03)
    for z in (1.2, 1.6):
        box("Madeira", (2.8, 0.55, 0.06), (0, 0.6, z))
    for sx in (-1, 1):
        box("Madeira", (0.13, 0.13, 2.4), (sx * 1.4, -0.85, 1.2))
        box("Madeira", (0.13, 0.13, 2.85), (sx * 1.4, 0.95, 1.42))
    th = math.atan2(0.55, 2.2)
    for i in range(6):
        mat = "Bandeira" if i % 2 == 0 else "Tecido"
        box(mat, (0.55, math.hypot(2.2, 0.55), 0.07), (-1.375 + 0.55 * i, -0.1, 2.6), rot=(th, 0, 0))
    pivot("Bandeirola", (0, -1.2, 2.33))
    box("Bandeira", (3.3, 0.06, 0.2), (0, -1.22, 2.24), b="Bandeirola")
    for i in range(7):
        box("Bandeira" if i % 2 else "Tecido", (0.24, 0.06, 0.24), (-1.5 + i * 0.5, -1.22, 2.1),
            rot=(0, math.pi / 4, 0), b="Bandeirola")
    # Mercadoria: caixotes, potes de cristal e uma balança de latão.
    for u, s in ((-1.0, 0.42), (-0.55, 0.32)):
        box("Madeira", (s, s, s), (u, -0.45, 1.08 + s / 2), rot=(0, 0, 0.2), bevel=0.02)
    for u in (0.15, 0.45, 0.75):
        cyl("Latao", 0.13, 0.1, (u, -0.5, 1.13), verts=8)
        gem(0.13, (u, -0.5, 1.3), scale=(1, 1, 1.4))
    cyl("Latao", 0.06, 0.5, (1.15, -0.4, 1.33), verts=8)
    box("Latao", (0.6, 0.08, 0.08), (1.15, -0.4, 1.58))
    for dx in (-0.27, 0.27):
        cyl("Latao", 0.13, 0.05, (1.15 + dx, -0.4, 1.4), verts=8)
    for u in (-0.9, 0.0, 0.9):
        cyl("Tecido" if u else "Couro", 0.13, 0.7, (u, 0.65, 1.38), rot=(0, math.pi / 2, 0), verts=8)
        cyl("Cobre", 0.16, 0.3, (u, 0.65, 1.79), verts=8)
    # Lanterna sob o toldo.
    box("Latao", (0.28, 0.28, 0.06), (0.95, -0.7, 2.18))
    box("JanelaQuente", (0.2, 0.2, 0.28), (0.95, -0.7, 2.0), b="JanelaAndar0")
    box("Latao", (0.28, 0.28, 0.06), (0.95, -0.7, 1.84))
    marker("LuzQuente", (0.95, -0.7, 2.0))
    export()


def caixotes():
    begin("Caixotes")

    def crate(s, loc, rz):
        with xf(T(*loc, rz=rz)):
            box("Madeira", (s, s, s), (0, 0, s / 2), bevel=0.03)
            for z in (0.12, s - 0.12):
                box("FerroEscuro", (s + 0.04, s + 0.04, 0.1), (0, 0, z))
            beam("Madeira", (-s / 2 + 0.1, -s / 2 - 0.02, 0.15), (s / 2 - 0.1, -s / 2 - 0.02, s - 0.15), 0.12, 0.06)

    crate(1.0, (-0.55, 0.1, 0), 0.0)
    crate(0.75, (-0.45, 0.15, 1.0), 0.3)
    crate(0.85, (0.55, 0.35, 0), -0.25)
    # Caixote de cristal: ripas abertas e o brilho de dentro (carga arcana).
    with xf(T(0.55, -0.85, 0, rz=0.15)):
        s = 0.9
        box("Madeira", (s, s, 0.12), (0, 0, 0.06))
        for sx in (-1, 1):
            for sy in (-1, 1):
                box("Madeira", (0.12, 0.12, s), (sx * (s / 2 - 0.06), sy * (s / 2 - 0.06), s / 2))
        for z in (0.35, 0.65, s - 0.04):
            for sy in (-1, 1):
                box("Madeira", (s, 0.08, 0.12), (0, sy * (s / 2 - 0.04), z))
            for sx in (-1, 1):
                box("Madeira", (0.08, s, 0.12), (sx * (s / 2 - 0.04), 0, z))
        for (x, y, r) in ((-0.15, -0.1, 0.22), (0.18, 0.12, 0.18), (0.1, -0.2, 0.15)):
            gem(r, (x, y, 0.12 + r * 1.2), scale=(1, 1, 1.6), rot=(0.2, 0.3, 0))
    for (x, y) in ((-1.6, 0.6), (-1.55, -0.4)):
        cyl("Madeira", 0.38, 1.0, (x, y, 0.5), verts=12)
        for z in (0.2, 0.8):
            cyl("FerroEscuro", 0.41, 0.09, (x, y, z), verts=12)
        cyl("LataoEscuro", 0.3, 0.04, (x, y, 1.01), verts=12)
    cyl("Madeira", 0.38, 1.0, (-1.0, -1.0, 0.38), rot=(math.pi / 2, 0, 0.6), verts=12)
    sphere("Tecido", 0.45, (1.45, -0.2, 0.28), scale=(1, 0.75, 0.65), segments=10)
    sphere("TecidoEscuro", 0.38, (1.35, 0.5, 0.25), scale=(1, 0.8, 0.65), segments=10)
    export()


def lampiao_rua():
    begin("LampiaoRua")
    cyl("PedraEscura", 0.34, 0.5, (0, 0, 0.25), verts=8, smooth=False)
    cyl("FerroEscuro", 0.22, 0.35, (0, 0, 0.67), r2=0.12, verts=8, smooth=False)
    cyl("FerroEscuro", 0.1, 3.4, (0, 0, 2.2), verts=8)
    for z in (1.2, 2.7, 3.85):
        cyl("Latao", 0.15, 0.1, (0, 0, z), verts=8)
    sphere("Latao", 0.13, (0, 0, 4.0), segments=8)
    box("FerroEscuro", (0.1, 1.1, 0.12), (0, -0.5, 3.85))
    beam("FerroEscuro", (0, -0.05, 3.2), (0, -0.62, 3.82), 0.08)
    sphere("Latao", 0.08, (0, -1.06, 3.85), segments=8)
    # Regulador de cristal no poste: o lampião é alimentado pelo cano.
    box("Latao", (0.3, 0.24, 0.4), (0, -0.14, 1.7), bevel=0.02)
    gem(0.1, (0, -0.27, 1.7), scale=(1, 0.6, 1.5))
    cyl("Cobre", 0.06, 1.9, (0.0, -0.12, 2.85), verts=6)
    pivot("Lanterna", (0, -0.9, 3.78))
    box("FerroEscuro", (0.08, 0.08, 0.16), (0, -0.9, 3.7), b="Lanterna")
    cyl("Latao", 0.27, 0.24, (0, -0.9, 3.52), r2=0.06, verts=6, b="Lanterna", smooth=False)
    cyl("Latao", 0.22, 0.08, (0, -0.9, 2.98), verts=6, b="Lanterna")
    for i in range(3):
        a = i * 2 * math.pi / 3
        box("Latao", (0.08, 0.08, 0.5), (math.cos(a) * 0.19, -0.9 + math.sin(a) * 0.19, 3.21), b="Lanterna")
    gem(0.15, (0, -0.9, 3.22), scale=(1, 1, 1.5), b="Lanterna")
    marker("LuzCristal", (0, -0.9, 3.22), parent="Lanterna")
    export()


def placa_pendurada():
    begin("PlacaPendurada")
    hanging_sign(0, 0, length=1.2)
    export()


def tanque_agua():
    begin("TanqueAgua")
    a = 1.3
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("FerroEscuro", (0.28, 0.28, 5.3), (sx * a, sy * a, 2.65))
            box("PedraEscura", (0.6, 0.6, 0.3), (sx * a, sy * a, 0.15))
    for (p0, p1) in (((-a, -a), (a, -a)), ((a, -a), (a, a)), ((a, a), (-a, a)), ((-a, a), (-a, -a))):
        beam("FerroEscuro", (p0[0], p0[1], 0.5), (p1[0], p1[1], 4.7), 0.12)
        beam("FerroEscuro", (p1[0], p1[1], 0.5), (p0[0], p0[1], 4.7), 0.12)
        beam("FerroEscuro", (p0[0], p0[1], 2.6), (p1[0], p1[1], 2.6), 0.14)
    cyl("FerroEscuro", 2.1, 0.22, (0, 0, 5.4), verts=16)
    torus("FerroEscuro", 2.05, 0.06, (0, 0, 6.3), seg=20)
    for i in range(8):
        ang = i * math.pi / 4
        box("FerroEscuro", (0.09, 0.09, 0.85), (math.cos(ang) * 2.05, math.sin(ang) * 2.05, 5.9))
    cyl("Madeira", 1.75, 3.0, (0, 0, 7.0), verts=16)
    for z in (5.75, 7.4, 8.3):
        cyl("FerroEscuro", 1.8, 0.12, (0, 0, z), verts=16)
    # Anel de cristal que aquece a água (o vapor da cidade nasce do cristal).
    cyl("CristalArcano", 1.81, 0.3, (0, 0, 6.55), verts=16, b="CristalPulso", smooth=False)
    for z in (6.35, 6.75):
        cyl("Latao", 1.85, 0.09, (0, 0, z), verts=16)
    cyl("Telhado", 2.0, 1.3, (0, 0, 9.15), r2=0.12, verts=16)
    sphere("Latao", 0.2, (0, 0, 9.9), segments=8)
    cyl("Cobre", 0.16, 5.0, (0, -0.5, 2.8), verts=10)
    valve(0, -0.85, 1.3)
    marker("Vapor", (0.25, -0.8, 1.0))
    for s in (-1, 1):
        box("FerroEscuro", (0.08, 0.08, 5.3), (s * 0.25, -1.55, 2.65))
    for i in range(12):
        box("FerroEscuro", (0.5, 0.08, 0.08), (0, -1.55, 0.4 + i * 0.42))
    export()


def pilao_arcano():
    begin("PilaoArcano")
    box("PedraEscura", (2.8, 2.8, 0.6), (0, 0, 0.3), bevel=0.05)
    box("Latao", (2.9, 2.9, 0.12), (0, 0, 0.62))

    def half(z):
        return 1.1 - (z - 0.6) / 11.4 * 0.65

    for sx in (-1, 1):
        for sy in (-1, 1):
            beam("FerroEscuro", (sx * 1.1, sy * 1.1, 0.6), (sx * 0.45, sy * 0.45, 12.0), 0.22)
    rings = (0.9, 3.5, 7.0, 10.5)
    for i, z in enumerate(rings):
        h = half(z)
        for (p0, p1) in (((-h, -h), (h, -h)), ((h, -h), (h, h)), ((h, h), (-h, h)), ((-h, h), (-h, -h))):
            beam("Latao" if i else "FerroEscuro", (p0[0], p0[1], z), (p1[0], p1[1], z), 0.14)
        if i < len(rings) - 1:
            z2 = rings[i + 1]
            h2 = half(z2)
            for k in range(4):
                c0 = [(-h, -h), (h, -h), (h, h), (-h, h)]
                c1 = [(-h2, -h2), (h2, -h2), (h2, h2), (-h2, h2)]
                j = (k + 1) % 4
                beam("FerroEscuro", (c0[k][0], c0[k][1], z), (c1[j][0], c1[j][1], z2), 0.09)
    cyl("Cobre", 0.14, 11.2, (0, 0, 6.2), verts=8)
    for z in (3.5, 7.0):
        cyl("Latao", 0.3, 0.5, (0, 0, z), verts=8)
        gem(0.28, (0, 0, z), scale=(1.2, 1.2, 0.8))
    for z in (2.2, 5.3, 8.7):
        torus("Cobre", half(z) * 1.45, 0.07, (0, 0, z), rot=(0, 0, math.pi / 4), seg=4, minor_seg=4)
    box("FerroEscuro", (1.3, 1.3, 0.18), (0, 0, 12.05))
    for sx in (-1, 1):
        for sy in (-1, 1):
            beam("Latao", (sx * 0.45, sy * 0.45, 12.1), (sx * 0.8, sy * 0.8, 14.3), 0.12)
            for z in (12.7, 13.3):
                t = (z - 12.1) / 2.2
                torus("Cobre", 0.14, 0.05, (sx * (0.45 + 0.35 * t), sy * (0.45 + 0.35 * t), z), seg=8, minor_seg=4)
            sphere("Latao", 0.12, (sx * 0.8, sy * 0.8, 14.35), segments=8)
    torus("Latao", 1.0, 0.08, (0, 0, 14.0), seg=16)
    pivot("CristalFlutuante", (0, 0, 13.3))
    gem(0.55, (0, 0, 13.3), scale=(1, 1, 1.7), b="CristalFlutuante")
    marker("LuzCristal", (0, 0, 13.3))
    marker("Arcano", (0, 0, 13.3))
    marker("Faisca", (0, 0, 14.4))
    export()


def dirigivel():
    begin("Dirigivel")
    sphere("Tecido", 1.0, (0, 0, 0), scale=(2.3, 6.0, 2.3), segments=16)
    for y in (-3.6, 3.6):
        r = 2.3 * math.sqrt(1 - (y / 6.0) ** 2) + 0.04
        cyl("Bandeira", r, 0.7, (0, y, 0), rot=(math.pi / 2, 0, 0), verts=16)
    for y in (-1.6, 0.0, 1.6):
        r = 2.3 * math.sqrt(1 - (y / 6.0) ** 2) + 0.03
        torus("Latao", r, 0.08, (0, y, 0), rot=(math.pi / 2, 0, 0), seg=16, minor_seg=4)
    box("Latao", (0.16, 11.0, 0.16), (0, 0, 2.3))
    sphere("Latao", 0.4, (0, -5.95, 0), segments=8)
    cyl("Latao", 0.3, 0.6, (0, 6.0, 0), rot=(math.pi / 2, 0, 0), verts=8)
    for k in range(4):
        with xf(Matrix.Rotation(k * math.pi / 2, 4, "Y")):
            box("FerroMedio", (0.12, 2.2, 1.5), (0, 5.0, 1.9))
            box("Bandeira", (0.14, 0.5, 1.5), (0, 6.0, 1.9))
            box("Latao", (0.16, 2.3, 0.1), (0, 5.0, 2.65))
    # Gôndola de latão e madeira.
    box("Madeira", (1.4, 4.2, 0.95), (0, 0.3, -3.3), bevel=0.08)
    box("Latao", (1.55, 4.4, 0.18), (0, 0.3, -3.85), bevel=0.03)
    box("Latao", (1.5, 4.3, 0.12), (0, 0.3, -2.8))
    prism("Madeira", [(-0.7, -3.75), (0.7, -3.75), (0.7, -2.82), (-0.7, -2.82)], -1.8, -2.4)
    for s in (-1, 1):
        for y in (-1.0, -0.2, 0.6, 1.4):
            box("JanelaQuente", (0.08, 0.45, 0.32), (s * 0.7, y, -3.2), b="JanelaAndar0")
        for (y0, y1) in ((-1.4, -2.0), (1.8, 2.4)):
            beam("FerroEscuro", (s * 0.55, y0, -2.8), (s * 1.2, y1, -1.85), 0.1)
    # Motor de cristal e hélice.
    cyl("Latao", 0.45, 1.2, (0, 3.0, -3.3), rot=(math.pi / 2, 0, 0), verts=12)
    cyl("CristalArcano", 0.48, 0.26, (0, 2.85, -3.3), rot=(math.pi / 2, 0, 0), verts=12, b="CristalPulso", smooth=False)
    cyl("FerroEscuro", 0.12, 0.5, (0, 3.2, -2.75), verts=8)
    marker("Fumaca", (0, 3.2, -2.45))
    pivot("Helice", (0, 3.75, -3.3))
    cyl("FerroEscuro", 0.16, 0.35, (0, 3.75, -3.3), rot=(math.pi / 2, 0, 0), verts=8, b="Helice")
    for i in range(3):
        a = i * 2 * math.pi / 3
        box("Madeira", (0.26, 0.07, 1.0), (math.sin(a) * 0.6, 3.8, -3.3 + math.cos(a) * 0.6), rot=(0, a, 0),
            b="Helice")
    export()


def fabrica():
    begin("Fabrica")
    W, D, H = 14.0, 10.0, 7.0
    box("PedraEscura", (W + 0.2, D + 0.2, 0.5), (0, D / 2, 0.25), bevel=0.04)
    box("Tijolo", (W, D, H), (0, D / 2, H / 2), bevel=0.05)
    for u in (-W / 2 + 0.15, -2.3, 2.3, W / 2 - 0.15):
        box("Pedra", (0.6, 0.45, H + 0.4), (u, 0.1, (H + 0.4) / 2))
    box("Pedra", (W + 0.4, 0.5, 0.35), (0, 0.0, 3.0))
    box("PedraEscura", (W + 0.4, D + 0.4, 0.4), (0, D / 2, H + 0.2))
    door(0, 3.2, 3.0, frame="FerroEscuro")
    for z in (0.8, 2.0):
        box("FerroEscuro", (3.2, 0.16, 0.14), (0, -0.12, z))
    box("Madeira", (4.2, 0.22, 0.8), (0, -0.15, 5.7))
    box("Latao", (4.4, 0.26, 0.1), (0, -0.16, 6.12))
    box("Latao", (4.4, 0.26, 0.1), (0, -0.16, 5.28))
    gear("Latao", 0.55, 10, 0.16, (0, -0.35, 5.7), b="Engrenagem1", holes=False)
    for u in (-5.55, -3.6, 3.6, 5.55):
        window(u, 3.6, 1.2, 2.2, floor=1, frame="FerroEscuro", trim="Pedra", top="arch")
    # Boca de fornalha no térreo (brasa visível da praça) e grade de ferro.
    box("PedraEscura", (2.6, 0.3, 1.5), (-4.6, -0.12, 1.15))
    box("BrasaFornalha", (2.1, 0.08, 1.0), (-4.6, -0.28, 1.15))
    for i in range(6):
        box("FerroEscuro", (0.1, 0.12, 1.0), (-5.5 + i * 0.36, -0.34, 1.15))
    marker("Faisca", (-4.6, -0.6, 1.4))
    marker("LuzFornalha", (-4.6, -1.2, 1.4))
    # Válvulas e manômetro de cristal à direita.
    vpipe(4.0, -0.25, 0.5, 3.0, r=0.14)
    vpipe(5.2, -0.25, 0.5, 3.0, r=0.14)
    cyl("Cobre", 0.16, 1.6, (4.6, -0.3, 1.6), rot=(0, math.pi / 2, 0), verts=10)
    valve(4.6, -0.55, 1.6)
    cyl("Latao", 0.24, 0.12, (5.9, -0.2, 1.9), rot=(math.pi / 2, 0, 0), verts=12)
    cyl("CristalArcano", 0.18, 0.04, (5.9, -0.28, 1.9), rot=(math.pi / 2, 0, 0), verts=12, b="CristalPulso",
        smooth=False)
    marker("Vapor", (4.6, -0.7, 1.2))
    # Telhado em shed: vidros voltados para a frente.
    for i in range(3):
        y0, y1 = i * D / 3, (i + 1) * D / 3
        warm = i != 1
        box("JanelaQuente" if warm else "CristalArcano", (W - 0.8, 0.1, 1.6), (0, y0 + 0.2, H + 1.2),
            b="JanelaAndar2" if warm else "JanelaCristal")
        for k in range(5):
            box("FerroEscuro", (0.12, 0.16, 1.7), (-W / 2 + 0.4 + k * (W - 0.8) / 4, y0 + 0.14, H + 1.2))
        th = math.atan2(1.9, D / 3)
        box("Telhado", (W + 0.2, math.hypot(D / 3, 1.9) + 0.1, 0.22), (0, (y0 + y1) / 2, H + 0.4 + 0.95 + 0.1),
            rot=(-th, 0, 0))
        for s in (-1, 1):
            prism("Tijolo", [(y0 + 0.05, H + 0.4), (y0 + 0.05, H + 2.3), (y1, H + 0.4)], s * W / 2 - 0.15,
                  s * W / 2 + 0.15, axis="X")
    # Chaminés altas com filtro de cristal (a fumaça passa pelo cristal).
    for sx in (-1, 1):
        x, y = sx * (W / 2 - 1.6), D - 1.4
        cyl("Tijolo", 0.95, 13.0, (x, y, 6.5 + H), r2=0.65, verts=12)
        cyl("PedraEscura", 1.15, 0.6, (x, y, H + 0.6), verts=12)
        for z in (H + 4, H + 8.5):
            rr = 0.95 - (z - H) / 13.0 * 0.3
            cyl("Latao", rr + 0.07, 0.25, (x, y, z), verts=12)
        rr = 0.95 - 11.0 / 13.0 * 0.3
        cyl("CristalArcano", rr + 0.06, 0.55, (x, y, H + 11.0), verts=12, b="CristalPulso", smooth=False)
        for dz in (-0.35, 0.35):
            cyl("Latao", rr + 0.1, 0.12, (x, y, H + 11.0 + dz), verts=12)
        cyl("FerroEscuro", 0.82, 0.5, (x, y, H + 13.1), verts=12)
        marker("Fumaca", (x, y, H + 13.5))
    export()


def canos_parede():
    begin("CanosParede")
    box("PedraEscura", (2.9, 0.6, 0.16), (0, -0.3, 0.08))
    for (u, top) in ((-1.0, 3.6), (0.3, 3.0)):
        vpipe(u, -0.3, 0.16, top, r=0.14)
        cyl("Cobre", 0.14, 0.32, (u, -0.16, top), rot=(math.pi / 2, 0, 0), verts=10)
        sphere("Cobre", 0.15, (u, -0.3, top), segments=8)
        cyl("Latao", 0.24, 0.08, (u, -0.02, top), rot=(math.pi / 2, 0, 0), verts=10)
    cyl("Cobre", 0.18, 2.7, (0, -0.35, 1.7), rot=(0, math.pi / 2, 0), verts=10)
    for x in (-1.35, 1.35, -1.0, 0.3):
        cyl("Latao", 0.24, 0.1, (x, -0.35, 1.7), rot=(0, math.pi / 2, 0), verts=10)
    cyl("FerroEscuro", 0.07, 0.3, (1.0, -0.55, 1.7), rot=(math.pi / 2, 0, 0), verts=8)
    valve(1.0, -0.72, 1.7, r=0.26)
    cyl("Latao", 0.24, 0.12, (-0.35, -0.38, 2.45), rot=(math.pi / 2, 0, 0), verts=12)
    cyl("CristalArcano", 0.18, 0.04, (-0.35, -0.46, 2.45), rot=(math.pi / 2, 0, 0), verts=12, b="CristalPulso",
        smooth=False)
    box("FerroEscuro", (0.04 + 0.05, 0.03, 0.2), (-0.35, -0.49, 2.5), rot=(0, 0.6, 0))
    cyl("Latao", 0.26, 0.5, (-1.0, -0.3, 2.65), verts=10)
    gem(0.2, (-1.0, -0.5, 2.65), scale=(1, 0.7, 1.4))
    cyl("Cobre", 0.09, 0.45, (1.35, -0.35, 1.42), verts=8)
    cyl("Latao", 0.12, 0.18, (1.35, -0.35, 1.15), r2=0.18, verts=8)
    marker("Vapor", (1.35, -0.35, 1.0))
    export()


def arco():
    begin("Arco")
    for s in (-1, 1):
        box("PedraEscura", (1.0, 1.0, 0.5), (s * 2.7, 0, 0.25), bevel=0.04)
        box("Pedra", (0.8, 0.8, 3.7), (s * 2.7, 0, 2.35), bevel=0.04)
        box("Latao", (0.95, 0.95, 0.2), (s * 2.7, 0, 4.3))
    arch_ring("FerroEscuro", 0, 4.3, 2.3, 2.7, -0.22, 0.22, seg=10)
    for i in range(10):
        a, c = math.pi * i / 10, math.pi * (i + 1) / 10
        beam("Cobre", (2.85 * math.cos(a), -0.3, 4.3 + 2.85 * math.sin(a)),
             (2.85 * math.cos(c), -0.3, 4.3 + 2.85 * math.sin(c)), 0.2)
    box("Latao", (0.75, 0.6, 0.9), (0, 0, 6.85), bevel=0.04)
    for y in (-0.32, 0.32):
        gem(0.24, (0, y, 6.85), scale=(1, 0.6, 1.5))
    marker("LuzCristal", (0, -0.6, 6.85))
    pivot("PlacaArco", (0, 0, 6.55))
    for x in (-0.6, 0.6):
        box("FerroEscuro", (0.08, 0.08, 0.95), (x, 0, 6.1), b="PlacaArco")
    box("Madeira", (1.7, 0.12, 0.6), (0, 0, 5.35), b="PlacaArco")
    box("Latao", (1.8, 0.16, 0.08), (0, 0, 5.68), b="PlacaArco")
    box("Latao", (1.8, 0.16, 0.08), (0, 0, 5.02), b="PlacaArco")
    cyl("Latao", 0.2, 0.18, (0, 0, 5.35), rot=(math.pi / 2, 0, 0), verts=10, b="PlacaArco")
    gem(0.1, (0, 0, 5.35), scale=(1, 1.4, 1), b="PlacaArco")
    export()


def bueiro_vapor():
    begin("BueiroVapor")
    cyl("FerroEscuro", 0.62, 0.08, (0, 0, 0.04), verts=16)
    torus("Latao", 0.64, 0.06, (0, 0, 0.06), seg=16, minor_seg=4)
    torus("CristalArcano", 0.47, 0.05, (0, 0, 0.07), seg=16, minor_seg=4, b="CristalPulso")
    for i in range(4):
        box("FerroMedio", (0.8, 0.1, 0.05), (0, -0.3 + i * 0.2, 0.1))
    marker("Vapor", (0, 0, 0.12))
    export()


# ---------- prévias (só para conferir; não vão para a Unity) ----------

def _import(name, loc=(0, 0, 0), rz=0.0, scale=(1, 1, 1)):
    """Reimporta o FBX (confere a exportação) e posiciona a raiz."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(FBX_DIR, f"{name}.fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    for r in [o for o in new if o.parent is None]:
        r.matrix_world = T(*loc, rz=rz) @ Matrix.Diagonal((*scale, 1.0)) @ r.matrix_world
    return new


def _upolar(angle_deg, radius, height=0.0):
    """Posição no padrão da Unity (ângulo a partir de +Z, horário) levada ao Blender (Unity +Z = Blender -Y)."""
    a = math.radians(angle_deg)
    return (-math.sin(a) * radius, -math.cos(a) * radius, height)


def _face_center_rz(angle_deg):
    """Rotação em Z para a frente (-Y) do modelo olhar para o centro da praça."""
    return math.pi - math.radians(angle_deg)


def _setup_render(res, engine="EEVEE"):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    if engine == "EEVEE":
        for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
            try:
                sc.render.engine = eng
                break
            except TypeError:
                continue
        try:
            sc.eevee.taa_render_samples = 16
        except Exception:
            pass
    else:
        sc.render.engine = "BLENDER_WORKBENCH"
        sc.display.shading.light = "STUDIO"
        sc.display.shading.color_type = "MATERIAL"
        sc.display.shading.show_cavity = True
        sc.display.shading.show_object_outline = True
    sc.view_settings.view_transform = "Standard"


def _world(color, strength=1.0):
    w = bpy.data.worlds.new("Noite")
    if w.node_tree is None:
        w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = color
    bg.inputs["Strength"].default_value = strength
    bpy.context.scene.world = w


def _sun(rot, energy, color):
    d = bpy.data.lights.new("Lua", "SUN")
    d.energy = energy
    d.color = color
    o = bpy.data.objects.new("Lua", d)
    o.rotation_euler = rot
    bpy.context.scene.collection.objects.link(o)


def _marker_lights(max_lights=60):
    n = 0
    for o in list(bpy.data.objects):
        if o.type != "EMPTY" or n >= max_lights:
            continue
        if o.name.startswith(("LuzQuente", "LuzCristal", "LuzFornalha")):
            warm = not o.name.startswith("LuzCristal")
            light = bpy.data.lights.new(o.name + "_L", "POINT")
            light.energy = 220 if warm else 300
            light.color = (1.0, 0.65, 0.3) if warm else (0.35, 0.9, 1.0)
            light.shadow_soft_size = 0.3
            lo = bpy.data.objects.new(o.name + "_L", light)
            lo.location = o.matrix_world.translation
            bpy.context.scene.collection.objects.link(lo)
            n += 1


def _camera(loc, target=None, forward=None, fov=None):
    cd = bpy.data.cameras.new("Cam")
    co = bpy.data.objects.new("Cam", cd)
    bpy.context.scene.collection.objects.link(co)
    co.location = loc
    f = Vector(forward) if forward else (Vector(target) - Vector(loc))
    co.rotation_mode = "QUATERNION"
    co.rotation_quaternion = f.to_track_quat("-Z", "Y")
    if fov:
        cd.sensor_fit = "VERTICAL"
        cd.angle_y = math.radians(fov)
    cd.clip_end = 500
    bpy.context.scene.camera = co
    return co


def _render(path):
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"[build_city] prévia: {path}")


def _ground_box(mat, size, loc):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = size
    o.data.materials.append(material(mat))
    return o


def _night(sun_rz):
    _world((0.012, 0.016, 0.03, 1), 1.0)
    _sun((math.radians(50), 0, sun_rz), 0.4, (0.55, 0.65, 1.0))


def preview():
    # 1) Kit lado a lado (cores chapadas, luz de estúdio) para ler forma e silhueta.
    reset_scene()
    row1 = [("Oficina", 9), ("CasaEstreitaA", 7.5), ("CasaEstreitaB", 7.5), ("CasaLarga", 11.5), ("CasaAlta", 8.5),
            ("Fabrica", 16), ("TorreRelogio", 9)]
    x = 0.0
    for name, w in row1:
        _import(name, (x + w / 2, 0, 0))
        x += w
    row2 = [("BancaMercado", 4), ("Caixotes", 4.5), ("LampiaoRua", 2.5), ("PlacaPendurada", 2), ("CanosParede", 3.5),
            ("BueiroVapor", 2), ("Arco", 7), ("PonteCanos", 7.5), ("TanqueAgua", 5.5), ("PilaoArcano", 4.5)]
    x = 0.0
    for name, w in row2:
        _import(name, (x + w / 2, -14, 0))
        x += w
    _import("Dirigivel", (60, -14, 12), rz=math.pi / 2)
    _ground_box("PedraEscura", (200, 80, 0.1), (40, -5, -0.05))
    _setup_render((1920, 1080), engine="WORKBENCH")
    kit_cam = _camera((40, -75, 42), target=(40, -6, 7), fov=32)
    _render(os.path.join(PREVIEW_DIR, "kit_formas.png"))
    # Props na escala da câmera de jogo (640x360, 50°, 14 m): confere se os detalhes sobrevivem ao pixel.
    # Aqui a frente das peças é -Y, então a câmera fica em -Y olhando para +Y (como da praça).
    yaw, pitch = math.radians(30), math.radians(50)
    fwd = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    for tag, focus in (("props_a", Vector((7, -16, 1))), ("props_b", Vector((22, -16, 1))),
                       ("casas_terreo", Vector((22, -3.5, 1)))):
        cam = _camera(tuple(focus - fwd * 14.0), forward=tuple(fwd), fov=40)
        _setup_render((640, 360), engine="WORKBENCH")
        _render(os.path.join(PREVIEW_DIR, f"escala_jogo_{tag}.png"))
        bpy.data.objects.remove(cam)
    bpy.context.scene.camera = kit_cam

    # 2) Mesmo kit à noite (emissivos de janela e cristal).
    _night(math.radians(-30))
    _marker_lights()
    _setup_render((1920, 1080))
    _render(os.path.join(PREVIEW_DIR, "kit_noite.png"))

    # 3) Mini-composição: arco norte-leste da praça com o muro, ruas e prédios.
    reset_scene()
    _ground_box("PedraEscura", (160, 160, 0.1), (0, 0, -0.06))
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=27.3, depth=0.1, location=(0, 0, -0.01))
    bpy.context.active_object.data.materials.append(material("Pedra"))
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=12, depth=0.1, location=(0, 0, 0.0))
    bpy.context.active_object.data.materials.append(material("FerroMedio"))
    for i in range(28):
        a = i * 360 / 28
        bx, by, _ = _upolar(a, 26.6)
        _ground_box("FerroMedio", (6.2, 0.6, 3.0), (bx, by, 1.5)).rotation_euler.z = -math.radians(a)
    widths = {"CasaEstreitaA": 6, "CasaEstreitaB": 6, "CasaLarga": 10, "Oficina": 8, "CasaAlta": 7, "Fabrica": 14}
    front = ["CasaEstreitaA", "CasaLarga", "CasaEstreitaB", "Oficina", "CasaAlta", "CasaEstreitaA", "CasaLarga",
             "Oficina", "CasaEstreitaB", "CasaAlta", "CasaEstreitaA", "Oficina"]

    def fill(r, a0, a1, pool):
        a, k = a0, 0
        while True:
            name = pool[k % len(pool)]
            da = math.degrees((widths[name] + 0.4) / r)
            if a + da > a1:
                break
            c = a + da / 2
            _import(name, _upolar(c, r), rz=_face_center_rz(c))
            a += da
            k += 1

    def gap(m, r):
        return math.degrees(m / r)

    fill(30.5, -75, -60 - gap(2.0, 30.5), front)
    fill(30.5, -60 + gap(2.0, 30.5), -gap(3.5, 30.5), front[3:])
    fill(30.5, gap(3.5, 30.5), 60 - gap(2.0, 30.5), front[1:])
    fill(30.5, 60 + gap(2.0, 30.5), 118, front[5:])
    back = ["Fabrica", "CasaAlta", "CasaEstreitaB", "CasaAlta", "Fabrica", "CasaLarga"]
    fill(44, -60, -8, back)
    fill(44, 8, 60, back[2:])
    fill(44, 64, 115, back[1:])
    _import("TorreRelogio", _upolar(0, 52), rz=_face_center_rz(0))
    _import("PilaoArcano", _upolar(-35, 41))
    _import("PilaoArcano", _upolar(62, 42))
    _import("TanqueAgua", _upolar(95, 43), rz=0.3)
    p = _upolar(0, 34, 7.2)
    _import("PonteCanos", p, rz=_face_center_rz(0), scale=(8.0 / 6.0, 1, 1))
    for a in (30, 75, -25):
        _import("PonteCanos", _upolar(a, 28.7, 3.3), rz=_face_center_rz(a) + math.pi / 2, scale=(4.2 / 6.0, 1, 1))
    for i, a in enumerate((12, 20, 40, 48, 85, 100, -40, -15)):
        _import("BancaMercado" if i % 2 == 0 else "Caixotes", _upolar(a, 28.4),
                rz=_face_center_rz(a) + (math.pi / 2 if i % 4 == 0 else -math.pi / 2))
    for a in (5, 25, 45, 70, 90, 110, -10, -30, -50):
        _import("LampiaoRua", _upolar(a, 29.9), rz=_face_center_rz(a))
    for a in (16, 56, 96, -20):
        _import("BueiroVapor", _upolar(a, 28.8))
    _import("Arco", _upolar(60, 30.6), rz=_face_center_rz(60))
    _import("Dirigivel", _upolar(25, 46, 18), rz=0.8)
    _night(math.radians(150))
    _marker_lights(80)
    _setup_render((1920, 1080))
    _camera(_upolar(210, 30, 55), target=_upolar(30, 22, 0), fov=40)
    _render(os.path.join(PREVIEW_DIR, "composicao_visao_geral.png"))
    _camera(_upolar(30, 8, 9), target=_upolar(30, 40, 6), fov=50)
    _render(os.path.join(PREVIEW_DIR, "composicao_rua.png"))

    # 4) Câmera real do jogo (50°, giro 30°, 14 m, FOV 40, 640x360) com o jogador perto do muro.
    yaw, pitch = math.radians(30), math.radians(50)
    fwd = Vector((-math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    for tag, ang, rad in (("norte", 15, 23.5), ("leste", 95, 23.5), ("oeste", -60, 23.5)):
        focus = Vector(_upolar(ang, rad, 1.0))
        cam = _camera(tuple(focus - fwd * 14.0), forward=tuple(fwd), fov=40)
        _setup_render((640, 360))
        _render(os.path.join(PREVIEW_DIR, f"camera_jogo_{tag}.png"))
        bpy.data.objects.remove(cam)


# ---------- execução ----------

PIECES = {
    "CasaEstreitaA": lambda: casa_estreita("CasaEstreitaA", 2, upper="Tijolo", roof="Telhado", crystal_floor=1),
    "CasaEstreitaB": lambda: casa_estreita("CasaEstreitaB", 3, upper="Pedra", roof="CobreOxidado", crystal_floor=2,
                                           sign_side=-1, second_chimney=True),
    "CasaLarga": casa_larga,
    "CasaAlta": casa_alta,
    "Oficina": oficina,
    "TorreRelogio": torre_relogio,
    "Fabrica": fabrica,
    "PonteCanos": ponte_canos,
    "BancaMercado": banca_mercado,
    "Caixotes": caixotes,
    "LampiaoRua": lampiao_rua,
    "PlacaPendurada": placa_pendurada,
    "TanqueAgua": tanque_agua,
    "PilaoArcano": pilao_arcano,
    "Dirigivel": dirigivel,
    "CanosParede": canos_parede,
    "Arco": arco,
    "BueiroVapor": bueiro_vapor,
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in argv:
        only = argv[argv.index("--only") + 1].split(",")
    if "--skip-build" not in argv:
        for name, fn in PIECES.items():
            if only and name not in only:
                continue
            fn()
    if "--preview" in argv:
        preview()


if __name__ == "__main__":
    main()
