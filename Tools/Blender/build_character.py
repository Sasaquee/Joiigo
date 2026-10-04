"""
Gera o personagem do jogador, o Andarilho encapuzado (D-040), e exporta FBX para a Unity.

Uso:
    blender --background --factory-startup --python Tools/Blender/build_character.py
    blender --background --factory-startup --python Tools/Blender/build_character.py -- --preview <pasta>

Com --preview, depois de exportar, renderiza imagens de conferência na pasta indicada
(close na câmera do jogo e uma versão em 640x360 ampliada sem suavizar).

Peças separadas, filhas do Empty raiz "Andarilho", para a animação procedural da Unity
(PlayerVisualAnimator). A origem de cada peça fica na junta, não no centro da malha:
    Torso      corpo, túnica, capuz-ombreira, cinto e bolsas   (pivô na cintura)
    Cabeca     capuz e máscara de latão                        (pivô no pescoço)
    BracoEsq   braço esquerdo                                  (pivô no ombro)
    BracoDir   braço direito                                   (pivô no ombro)
    PernaEsq   perna esquerda                                  (pivô no quadril)
    PernaDir   perna direita                                   (pivô no quadril)
    Capa       painel de trás da capa, para balançar           (pivô nos ombros)
    Lanterna   lanterna de latão com cristal, presa no cinto   (pivô no gancho)

A frente é -Y no Blender. O lado esquerdo do personagem é +X (olhando para -Y, a direita fica em -X).
Pivô do conjunto no chão. Só nomes de material da paleta (Docs/Design/arte-pixel.md).

Pilar 1: nada de arma ou símbolo de classe. Pilar 4: o único sinal mágico é o brilho ciano
das frestas da máscara e o cristal dentro da lanterna de latão (máquina e magia na mesma peça).
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLEND_DIR = os.path.join(ROOT, "Art", "Blender", "Character")
FBX_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Models", "Character")
NAME = "Andarilho"

# Cores da paleta, só para o Blender (a Unity troca pelo material de mesmo nome).
# nome: (cor sRGB hex, metálico, rugosidade, emissivo)
PALETTE = {
    "FerroMedio": ("4A4752", 0.7, 0.6, False),
    "Latao": ("C9A04A", 0.9, 0.45, False),
    "LataoEscuro": ("7A5F2C", 0.8, 0.6, False),
    "Madeira": ("6B4630", 0.0, 0.8, False),
    "Tecido": ("4E4A44", 0.0, 0.95, False),
    "TecidoEscuro": ("2F2C2A", 0.0, 0.95, False),
    "Couro": ("6E4A32", 0.0, 0.7, False),
    "CristalArcano": ("6FF0FF", 0.0, 0.1, True),
}

random.seed(40)  # D-040: a mesma "aleatoriedade" a cada geração


# ---------- utilidades ----------

def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_color(h):
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    hexc, metallic, roughness, emissive = PALETTE[name]
    color = hex_color(hexc)
    mat = bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emissive:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = 4.0
    mat.diffuse_color = color
    return mat


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, mat_name, bevel=0.0):
    obj.data.materials.append(material(mat_name))
    select_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1  # chanfro simples: lê melhor em pixel e gasta pouca face
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def box(mat, size, loc, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return finish(o, mat, bevel=bevel)


def cyl(mat, r, depth, loc, rot=(0, 0, 0), verts=8, scale=(1, 1, 1), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, mat, bevel=bevel)


def cone(mat, r1, r2, depth, loc, rot=(0, 0, 0), verts=8):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.active_object, mat)


def sphere(mat, r, loc, scale=(1, 1, 1), segments=10, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=segments, ring_count=rings)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, mat)


def torus(mat, major, minor, loc, rot=(0, 0, 0), major_seg=10, minor_seg=6):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot,
                                     major_segments=major_seg, minor_segments=minor_seg)
    return finish(bpy.context.active_object, mat)


def ellipse(cx, cy, z, rx, ry, n, wobble=0.0, hem=None):
    """Anel horizontal (no plano XY) com n pontos; ângulo 0 aponta para +X, 90° para +Y (costas).
    wobble: variação radial aleatória (pano gasto). hem(i): deslocamento em Z por vértice (barra rasgada)."""
    pts = []
    for i in range(n):
        a = i * math.tau / n
        k = 1.0 + (random.uniform(-wobble, wobble) if wobble else 0.0)
        dz = hem(i) if hem else 0.0
        pts.append(Vector((cx + rx * k * math.cos(a), cy + ry * k * math.sin(a), z + dz)))
    return pts


def loft(name, rings, mats, band_mats=None, cap_start=False, cap_end=False, tip_start=None, tip_end=None,
         cap_mats=(0, 0), closed=True, outward_axis=None, inward=False):
    """Costura anéis (listas de pontos do mesmo tamanho) numa malha.
    band_mats[k]: índice de material da faixa entre o anel k e k+1.
    outward_axis=(x, y): para malhas abertas, vira as faces para fora desse eixo vertical.
    inward: vira tudo para dentro (cavidade vista por dentro; a Unity descarta a face de trás)."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in mats:
        mesh.materials.append(material(m))

    bm = bmesh.new()
    vrings = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(rings[0])
    seg = n if closed else n - 1
    for k in range(len(vrings) - 1):
        a, b = vrings[k], vrings[k + 1]
        for i in range(seg):
            j = (i + 1) % n
            f = bm.faces.new((a[i], a[j], b[j], b[i]))
            f.material_index = band_mats[k] if band_mats else 0
    if cap_start:
        f = bm.faces.new(list(reversed(vrings[0])))
        f.material_index = cap_mats[0]
    if cap_end:
        f = bm.faces.new(vrings[-1])
        f.material_index = cap_mats[1]
    if tip_start is not None:
        t = bm.verts.new(tip_start)
        for i in range(seg):
            f = bm.faces.new((vrings[0][(i + 1) % n], vrings[0][i], t))
            f.material_index = cap_mats[0]
    if tip_end is not None:
        t = bm.verts.new(tip_end)
        for i in range(seg):
            f = bm.faces.new((vrings[-1][i], vrings[-1][(i + 1) % n], t))
            f.material_index = cap_mats[1]

    bm.normal_update()
    if outward_axis is None:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if inward:
            for f in bm.faces:
                f.normal_flip()
    else:
        ax = Vector((outward_axis[0], outward_axis[1]))
        for f in bm.faces:
            c = f.calc_center_median()
            d = Vector((c.x, c.y)) - ax
            if Vector((f.normal.x, f.normal.y)).dot(d) < 0:
                f.normal_flip()
    bm.to_mesh(mesh)
    bm.free()
    return obj


def solidify(obj, thickness, inner_mat_offset=1):
    """Dá espessura ao pano: a face de fora fica com o material 0 (Tecido), a de dentro e a borda com o forro."""
    select_only(obj)
    mod = obj.modifiers.new("Espessura", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = -1.0
    mod.material_offset = inner_mat_offset
    mod.material_offset_rim = inner_mat_offset
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def part(name, objs, pivot, parent):
    """Une as peças num objeto só e põe a origem na junta (pivot), para girar certo na Unity."""
    select_only(objs[0])
    for o in objs[1:]:
        o.select_set(True)
    bpy.ops.object.join()
    p = bpy.context.active_object
    p.name = name
    p.data.name = name
    bpy.context.scene.cursor.location = pivot
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    except Exception:
        bpy.ops.object.shade_smooth()
    # UVs simples, caso a Unity use texturas de detalhe nos materiais da paleta.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    p.parent = parent
    return p


# ---------- medidas (metros; frente = -Y; esquerda do personagem = +X) ----------

HIP_Z = 0.92        # junta do quadril
HIP_X = 0.11
WAIST_Z = 0.95      # pivô do Torso
SHOULDER_Z = 1.38   # junta do ombro
SHOULDER_X = 0.25
NECK_Z = 1.5        # pivô da Cabeca
CAPE_PIVOT = (0.0, 0.1, 1.42)
HOOK = (0.17, -0.17, 0.92)  # gancho da lanterna no cinto, à frente do quadril esquerdo


# ---------- peças ----------

def build_leg(side):
    """Perna: calça escura afunilando e bota de couro grossa com bico para a frente."""
    x = HIP_X * side
    ax = 0.13 * side  # tornozelo um pouco mais aberto que o quadril
    rings = [
        ellipse(x, 0.0, HIP_Z + 0.04, 0.09, 0.095, 8),
        ellipse(x + (ax - x) * 0.45, 0.0, 0.62, 0.08, 0.085, 8),
        ellipse(ax, 0.0, 0.38, 0.068, 0.072, 8),
    ]
    calca = loft("Calca", rings, ["TecidoEscuro"], cap_start=True, cap_end=True)
    objs = [calca]
    # Cano da bota com dobra no topo (forma grossa, lê bem de cima).
    objs.append(loft("Cano", [
        ellipse(ax, 0.005, 0.4, 0.082, 0.085, 8),
        ellipse(ax, 0.005, 0.33, 0.08, 0.083, 8),
        ellipse(ax, 0.005, 0.3, 0.07, 0.074, 8),
        ellipse(ax, 0.0, 0.08, 0.068, 0.074, 8),
    ], ["Couro"], cap_start=True, cap_end=True))
    objs.append(box("Couro", (0.115, 0.22, 0.09), (ax, -0.045, 0.06), bevel=0.022))
    objs.append(box("TecidoEscuro", (0.125, 0.235, 0.03), (ax, -0.045, 0.015), bevel=0.008))
    name = "PernaEsq" if side > 0 else "PernaDir"
    return objs, (x, 0.0, HIP_Z), name


def build_arm(side):
    """Braço pendurado e levemente aberto: manga escura, bracelete e luva de couro."""
    sx = SHOULDER_X * side
    rings = [
        ellipse(sx, 0.0, SHOULDER_Z + 0.06, 0.08, 0.085, 8),
        ellipse(sx + 0.025 * side, 0.0, 1.16, 0.075, 0.08, 8),
        ellipse(sx + 0.045 * side, -0.005, 1.0, 0.085, 0.088, 8),
    ]
    objs = [loft("Manga", rings, ["TecidoEscuro"], cap_start=True, cap_end=True)]
    wx = sx + 0.05 * side
    objs.append(loft("Bracelete", [
        ellipse(wx, -0.005, 1.03, 0.072, 0.075, 8),
        ellipse(wx, -0.005, 0.86, 0.064, 0.068, 8),
    ], ["Couro"], cap_start=True, cap_end=True))
    # Luva: mão fechada, chunky (abaixo de 8 cm os dedos sumiriam mesmo).
    objs.append(box("Couro", (0.085, 0.1, 0.12), (wx + 0.005 * side, -0.01, 0.79), bevel=0.025))
    name = "BracoEsq" if side > 0 else "BracoDir"
    return objs, (sx, 0.0, SHOULDER_Z), name


def build_torso():
    objs = []
    # Túnica: veste por baixo da capa, barra solta e gasta acima do joelho.
    hem = lambda i: (-0.04 if i % 2 == 0 else 0.0)
    tunica = loft("Tunica", [
        ellipse(0, 0.0, 0.6, 0.26, 0.21, 12, wobble=0.04, hem=hem),
        ellipse(0, 0.0, 0.8, 0.225, 0.175, 12),
        ellipse(0, 0.0, WAIST_Z, 0.2, 0.15, 12),
        ellipse(0, 0.0, 1.15, 0.22, 0.16, 12),
        ellipse(0, 0.01, 1.32, 0.23, 0.155, 12),
        ellipse(0, 0.01, 1.47, 0.11, 0.1, 12),
    ], ["TecidoEscuro"], cap_start=True, cap_end=True)
    objs.append(tunica)

    # Cinto de couro com fivela de latão.
    objs.append(loft("Cinto", [
        ellipse(0, 0.0, 0.9, 0.218, 0.168, 12),
        ellipse(0, 0.0, 1.0, 0.212, 0.162, 12),
    ], ["Couro"], cap_start=True, cap_end=True))
    objs.append(box("Latao", (0.1, 0.035, 0.085), (0, -0.172, 0.95), bevel=0.012))

    # Alça transversal (ombro direito ao quadril esquerdo), por cima da túnica.
    strap = box("Couro", (0.07, 0.03, 0.5), (0.0, -0.165, 1.17), rot=(0, math.radians(-38), 0), bevel=0.01)
    objs.append(strap)

    # Bolsas no cinto: frente-direita e trás-esquerda (a lanterna fica na frente-esquerda).
    objs.append(box("Couro", (0.12, 0.08, 0.13), (-0.15, -0.15, 0.87), rot=(0, 0, math.radians(28)), bevel=0.02))
    objs.append(box("TecidoEscuro", (0.13, 0.09, 0.04), (-0.15, -0.152, 0.935), rot=(0, 0, math.radians(28)), bevel=0.01))
    objs.append(box("Couro", (0.12, 0.08, 0.12), (0.19, 0.11, 0.88), rot=(0, 0, math.radians(-52)), bevel=0.02))

    # Rolo de ferramentas no quadril direito, deitado; só uma chave de ferro aparece atrás (nada que lembre arma).
    roll_c = Vector((-0.235, 0.04, 0.86))
    objs.append(cyl("Couro", 0.06, 0.26, roll_c, rot=(math.radians(90), 0, 0), verts=8, bevel=0.01))
    for dy in (-0.07, 0.07):
        objs.append(cyl("TecidoEscuro", 0.066, 0.03, roll_c + Vector((0, dy, 0)), rot=(math.radians(90), 0, 0), verts=8))
    objs.append(box("FerroMedio", (0.05, 0.09, 0.03), roll_c + Vector((0.0, 0.16, 0.03)), bevel=0.008))
    objs.append(cyl("Madeira", 0.024, 0.08, roll_c + Vector((0.0, 0.15, -0.03)), rot=(math.radians(90), 0, 0), verts=6))

    # Capuz-ombreira: pano largo e grosso sobre os ombros, barra rasgada. Forma a silhueta vista de cima.
    # Barra mais alta na frente (abre o peito: cinto e alça aparecem) e mais baixa atrás.
    def hem_c(i):
        front = max(0.0, -math.sin(i * math.tau / 16))
        return (-0.055 if i % 2 == 0 else 0.0) + 0.09 * front + random.uniform(-0.012, 0.012)
    capelet = loft("Ombreira", [
        ellipse(0, 0.02, 1.54, 0.13, 0.12, 16),
        ellipse(0, 0.02, 1.48, 0.25, 0.2, 16),
        ellipse(0, 0.03, 1.39, 0.33, 0.24, 16, wobble=0.02),
        ellipse(0, 0.04, 1.24, 0.37, 0.28, 16, wobble=0.03, hem=hem_c),
    ], ["Tecido", "TecidoEscuro"], outward_axis=(0, 0.03))
    objs.append(solidify(capelet, 0.03))
    # Broche de latão fechando a ombreira no pescoço.
    objs.append(cyl("Latao", 0.045, 0.03, (0, -0.15, 1.45), rot=(math.radians(80), 0, 0), verts=8, bevel=0.008))
    return objs, (0.0, 0.0, WAIST_Z), "Torso"


def hood_ring(y, cz, rx, rz, peak, n=14, lean=0.0):
    """Anel vertical (plano XZ) do capuz, com bico no topo; lean empurra o topo para trás."""
    pts = []
    for i in range(n):
        a = i * math.tau / n  # 0 = topo
        up = max(0.0, math.cos(a))
        z = cz + rz * math.cos(a) + peak * up ** 6
        pts.append(Vector((rx * math.sin(a), y + lean * up, z)))
    return pts


def build_head():
    """Capuz pontudo caído para trás, com a abertura escura e a máscara de latão lá dentro."""
    objs = []
    rings = [
        hood_ring(-0.135, 1.665, 0.12, 0.145, 0.03),             # beirada interna (forro)
        hood_ring(-0.16, 1.67, 0.158, 0.19, 0.05),               # beirada externa
        hood_ring(-0.07, 1.68, 0.19, 0.225, 0.06),
        hood_ring(0.06, 1.69, 0.185, 0.21, 0.05, lean=0.03),
        hood_ring(0.16, 1.72, 0.12, 0.14, 0.03, lean=0.04),
        hood_ring(0.24, 1.77, 0.05, 0.06, 0.0),
    ]
    # Faixa 0 (beirada interna->externa) é a borda do capuz; o fundo da cavidade é forro escuro.
    capuz = loft("Capuz", rings, ["Tecido", "TecidoEscuro"], band_mats=[0, 0, 0, 0, 0],
                 tip_end=(0.0, 0.35, 1.8), cap_mats=(1, 0))
    objs.append(capuz)
    # Cavidade do rosto: tubo escuro da beirada interna até o fundo.
    objs.append(loft("Cavidade", [
        hood_ring(-0.135, 1.665, 0.125, 0.15, 0.03),
        hood_ring(-0.05, 1.665, 0.11, 0.135, 0.02),
    ], ["TecidoEscuro"], cap_end=True, inward=True))

    # Máscara: placa oval de latão, um pouco inclinada para cima (aparece na câmera alta).
    mask_c = Vector((0.0, -0.1, 1.655))
    mask = sphere("Latao", 1.0, mask_c, scale=(0.092, 0.05, 0.118), segments=10, rings=8)
    mask.rotation_euler = (math.radians(-12), 0, 0)
    select_only(mask)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    objs.append(mask)
    # Sobrancelha de latão escuro: dá sombra às frestas e lê como "olhar".
    objs.append(box("LataoEscuro", (0.15, 0.03, 0.025), (0.0, -0.145, 1.71), rot=(math.radians(-12), 0, 0), bevel=0.006))
    # Duas frestas estreitas com brilho ciano fraco: o único sinal mágico no corpo.
    for side in (1, -1):
        slit = box("CristalArcano", (0.06, 0.02, 0.02), (0.042 * side, -0.143, 1.684),
                   rot=(math.radians(-12), math.radians(-10 * side), 0))
        objs.append(slit)
    return objs, (0.0, 0.0, NECK_Z), "Cabeca"


def build_cape():
    """Painel de trás da capa: largo embaixo, envolvendo os lados, com dobras e barra rasgada."""
    rows = [  # (z, raio, abertura em graus a partir das costas)
        (1.45, 0.2, 70),
        (1.25, 0.27, 78),
        (1.0, 0.32, 84),
        (0.72, 0.36, 88),
        (0.45, 0.39, 90),
        (0.24, 0.41, 92),
    ]
    cols = 11
    rings = []
    for r_i, (z, radius, spread) in enumerate(rows):
        t = r_i / (len(rows) - 1)
        ring = []
        for c in range(cols):
            u = c / (cols - 1)
            ang = math.radians(-spread + 2 * spread * u)  # 0 = costas (+Y)
            fold = 1.0 + 0.07 * t * math.sin(u * math.pi * 5)  # dobras que abrem para baixo
            rr = radius * fold
            dz = 0.0
            if r_i == len(rows) - 1:
                dz = (0.07 if c % 2 else 0.0) + random.uniform(-0.02, 0.02)
            ring.append(Vector((rr * math.sin(ang), 0.02 + rr * math.cos(ang) * 0.85, z + dz)))
        rings.append(ring)
    capa = loft("CapaPainel", rings, ["Tecido", "TecidoEscuro"], closed=False, outward_axis=(0, 0.02))
    return [solidify(capa, 0.03)], CAPE_PIVOT, "Capa"


def build_lantern():
    """Lanterna de latão pendurada no cinto, com um cristal arcano no lugar da chama (Pilar 4)."""
    hx, hy, hz = HOOK
    objs = [
        torus("Latao", 0.03, 0.012, (hx, hy, hz - 0.02), rot=(0, math.radians(90), 0), major_seg=8, minor_seg=4),
        box("LataoEscuro", (0.02, 0.02, 0.06), (hx, hy, hz - 0.075)),
        cone("Latao", 0.07, 0.02, 0.05, (hx, hy, hz - 0.125), verts=6),
        cyl("LataoEscuro", 0.072, 0.02, (hx, hy, hz - 0.155), verts=6),
        cyl("LataoEscuro", 0.072, 0.025, (hx, hy, hz - 0.29), verts=6),
        cone("Latao", 0.05, 0.015, 0.04, (hx, hy, hz - 0.322), rot=(math.pi, 0, 0), verts=6),
        # Cristal no centro, maior que as grades para o brilho aparecer entre elas.
        cyl("CristalArcano", 0.048, 0.115, (hx, hy, hz - 0.222), verts=6),
    ]
    for i in range(3):  # grades verticais
        a = math.radians(30 + i * 120)
        objs.append(box("Latao", (0.022, 0.022, 0.12), (hx + 0.06 * math.cos(a), hy + 0.06 * math.sin(a), hz - 0.222)))
    return objs, HOOK, "Lanterna"


# ---------- montagem e exportação ----------

def build():
    reset_scene()
    root = bpy.data.objects.new(NAME, None)
    root.empty_display_size = 0.3
    bpy.context.scene.collection.objects.link(root)

    builders = [build_torso, build_head, lambda: build_arm(1), lambda: build_arm(-1),
                lambda: build_leg(1), lambda: build_leg(-1), build_cape, build_lantern]
    parts = []
    for b in builders:
        objs, pivot, name = b()
        parts.append(part(name, objs, pivot, root))
    bpy.context.scene.cursor.location = (0, 0, 0)
    return root, parts


def export(root, parts):
    os.makedirs(BLEND_DIR, exist_ok=True)
    os.makedirs(FBX_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, f"{NAME}.blend"))

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for p in parts:
        p.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, f"{NAME}.fbx"),
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
    total = 0
    for p in parts:
        faces = len(p.data.polygons)
        total += faces
        print(f"[build_character] {p.name}: {faces} faces, pivô {tuple(round(v, 3) for v in p.location)}")
    print(f"[build_character] {NAME}: {len(parts)} peças, {total} faces")


# ---------- prévia (só para conferência; não vai para o FBX) ----------

def look_at(obj, target):
    d = Vector(target) - obj.location
    obj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def preview(out_dir, parts):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("PrevChao")
    fm.diffuse_color = hex_color("3A3634")
    try:
        fm.use_nodes = True
    except Exception:
        pass
    fm.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = hex_color("3A3634")
    floor.data.materials.append(fm)

    world = bpy.data.worlds.new("Prev")
    scene.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.05, 0.06, 0.09, 1)
    bg.inputs["Strength"].default_value = 1.0

    sun_data = bpy.data.lights.new("Sol", "SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("Sol", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(45), 0, math.radians(-35))
    fill_data = bpy.data.lights.new("Preenche", "SUN")
    fill_data.energy = 0.8
    fill_data.color = (0.6, 0.7, 1.0)
    fill = bpy.data.objects.new("Preenche", fill_data)
    scene.collection.objects.link(fill)
    fill.rotation_euler = (math.radians(60), 0, math.radians(150))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.sensor_fit = "VERTICAL"
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    try:
        scene.cycles.use_denoising = False
    except Exception:
        pass
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"

    def place(dist, yaw_deg, target=(0, 0, 0.95)):
        # Câmera do jogo: 50° de inclinação. yaw gira em volta do personagem (0 = de frente).
        pitch = math.radians(50)
        yaw = math.radians(yaw_deg)
        t = Vector(target)
        off = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch))) * dist
        cam.location = t + off
        look_at(cam, t)

    shots = [("frente34", 30), ("costas34", 150), ("lado", 95)]
    for label, yaw in shots:
        place(14.0, yaw)
        cam_data.angle = math.radians(10)
        scene.render.resolution_x = 700
        scene.render.resolution_y = 700
        scene.render.filepath = os.path.join(out_dir, f"andarilho_{label}.png")
        bpy.ops.render.render(write_still=True)

    # Resolução do jogo: 640x360, FOV vertical 40°, 14 m. Recorte em volta do personagem, ampliado 6x.
    scene.render.resolution_x = 640
    scene.render.resolution_y = 360
    cam_data.angle = math.radians(40)
    for label, yaw in (("frente34", 30), ("costas34", 150)):
        place(14.0, yaw)
        path = os.path.join(out_dir, f"andarilho_jogo_{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        upscale_crop(path, os.path.join(out_dir, f"andarilho_jogo_{label}_x6.png"), 120, 90, 6)

    # Conferência de normais: a Unity descarta a face de trás, então aqui também. Buraco = normal invertida.
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.show_backface_culling = True
    shading.color_type = "MATERIAL"
    shading.light = "STUDIO"
    scene.render.resolution_x = 500
    scene.render.resolution_y = 500
    cam_data.angle = math.radians(10)
    for label, yaw in (("frente34", 30), ("costas34", 150), ("baixo", 0)):
        place(14.0, yaw)
        if label == "baixo":  # de baixo, para ver o forro e as tampas
            cam.location = Vector((0.0, -6.0, -3.0)) + Vector((0, 0, 0.9))
            look_at(cam, (0, 0, 1.0))
        scene.render.filepath = os.path.join(out_dir, f"andarilho_normais_{label}.png")
        bpy.ops.render.render(write_still=True)


def upscale_crop(src, dst, w, h, factor):
    import numpy as np
    img = bpy.data.images.load(src)
    W, H = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
    cx, cy = W // 2, H // 2
    crop = px[cy - h // 2: cy + h // 2, cx - w // 2: cx + w // 2]
    big = np.repeat(np.repeat(crop, factor, axis=0), factor, axis=1)
    out = bpy.data.images.new("ampliada", big.shape[1], big.shape[0])
    out.pixels[:] = big.ravel()
    out.filepath_raw = dst
    out.file_format = "PNG"
    out.save()


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root, parts = build()
    export(root, parts)
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1], parts)


main()
