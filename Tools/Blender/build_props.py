"""
Gera os modelos placeholder da arena no Blender e exporta FBX para a Unity.

Uso (de qualquer pasta):
    blender --background --factory-startup --python Tools/Blender/build_props.py

Saídas:
    Art/Blender/<Modelo>.blend          (fonte editável, fora de Assets/)
    Assets/_Game/Art/Models/<Modelo>.fbx (importado pela Unity)

Regra do Pilar 4: cada peça junta máquina e magia na mesma forma
(cristal embutido no cano, núcleo de cristal na engrenagem, poste com cristal engaiolado).
Os nomes dos materiais batem com os materiais da Unity em Assets/_Game/Art/Materials,
e o ArenaBuilder faz o remapeamento pelo nome.
Todos os modelos são simétricos de frente e de trás, então não dependem de convenção de eixo.
"""

import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLEND_DIR = os.path.join(ROOT, "Art", "Blender")
FBX_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Models")

# Cores aproximadas, só para a visualização no Blender. Na Unity valem os materiais do projeto.
MATERIALS = {
    "Cobre": ((0.72, 0.42, 0.25, 1), 0.9, 0.45, None),
    "Latao": ((0.78, 0.62, 0.30, 1), 0.9, 0.5, None),
    "FerroEscuro": ((0.18, 0.18, 0.20, 1), 0.7, 0.65, None),
    "CristalArcano": ((0.30, 0.90, 0.95, 1), 0.0, 0.1, (0.2, 1.0, 1.0, 1)),
    "PersonagemNeutro": ((0.55, 0.56, 0.60, 1), 0.1, 0.6, None),
    "MarcadorLocal": ((0.92, 0.88, 0.78, 1), 0.0, 0.5, None),
}


# ---------- utilidades ----------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    color, metallic, roughness, emission = MATERIALS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = 2.0
    return mat


def finish(obj, mat_name, bevel=0.0, smooth=False):
    obj.data.materials.append(material(mat_name))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if smooth:
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
        except Exception:
            bpy.ops.object.shade_smooth()
    return obj


def cyl(mat, radius, depth, loc, rot=(0, 0, 0), verts=32, scale=(1, 1, 1), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.scale = scale
    return finish(obj, mat, bevel=bevel, smooth=True)


def box(mat, size, loc, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.scale = size
    return finish(obj, mat, bevel=bevel)


def torus(mat, major, minor, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot,
                                     major_segments=32, minor_segments=10)
    return finish(bpy.context.active_object, mat, smooth=True)


def gem(radius, loc, scale=(1, 1, 1), rot=(0, 0, 0)):
    """Cristal facetado (icosfera de baixa resolução, sem suavização)."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.scale = scale
    return finish(obj, "CristalArcano")


def sphere(mat, radius, loc, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=loc, segments=24, ring_count=12)
    obj = bpy.context.active_object
    obj.scale = scale
    return finish(obj, mat, smooth=True)


def save_and_export(name):
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name
    joined.data.name = name
    # Rotação e escala embutidas na malha: na Unity o objeto chega sem transformação própria.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    os.makedirs(BLEND_DIR, exist_ok=True)
    os.makedirs(FBX_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, f"{name}.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, f"{name}.fbx"),
        use_selection=True,
        object_types={"MESH"},
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
    print(f"[build_props] {name}: {len(joined.data.polygons)} faces")


# ---------- modelos ----------
# Coordenadas do Blender: Z para cima. Na Unity, Z do Blender vira Y.

def engrenagem():
    """Engrenagem de latão com núcleo de cristal. Eixo em Z (na Unity, Y). Raio ~1.3 m."""
    reset_scene()
    cyl("Latao", 1.05, 0.16, (0, 0, 0), verts=48, bevel=0.02)
    for i in range(12):
        a = i * math.tau / 12
        box("Latao", (0.3, 0.3, 0.16), (math.cos(a) * 1.15, math.sin(a) * 1.15, 0), rot=(0, 0, a), bevel=0.02)
    cyl("FerroEscuro", 0.42, 0.24, (0, 0, 0), verts=24)
    for i in range(6):
        a = i * math.tau / 6
        cyl("FerroEscuro", 0.06, 0.2, (math.cos(a) * 0.75, math.sin(a) * 0.75, 0), verts=8)
    gem(0.3, (0, 0, 0), scale=(1, 1, 1.1))
    save_and_export("Engrenagem")


def cano(with_crystal):
    """Segmento de cano de cobre de 2 m ao longo de X, com abraçadeiras de latão."""
    reset_scene()
    cyl("Cobre", 0.25, 2.0, (0, 0, 0), rot=(0, math.pi / 2, 0), verts=24)
    for x in (-0.95, 0.95):
        cyl("Latao", 0.31, 0.08, (x, 0, 0), rot=(0, math.pi / 2, 0), verts=24, bevel=0.01)
    if with_crystal:
        # O cristal atravessa o cano: a energia corre por dentro do metal.
        gem(0.34, (0, 0, 0), scale=(0.9, 1, 1))
        for x in (-0.32, 0.32):
            torus("Latao", 0.27, 0.045, (x, 0, 0), rot=(0, math.pi / 2, 0))
        save_and_export("CanoCristal")
    else:
        torus("Latao", 0.26, 0.04, (0, 0, 0), rot=(0, math.pi / 2, 0))
        save_and_export("Cano")


def poste_arcano():
    """Poste de ferro com cristal engaiolado em latão. Altura ~3.65 m."""
    reset_scene()
    cyl("FerroEscuro", 0.35, 0.2, (0, 0, 0.1), verts=16, bevel=0.02)
    cyl("FerroEscuro", 0.11, 3.0, (0, 0, 1.6), verts=16)
    torus("Latao", 0.15, 0.04, (0, 0, 2.2))
    torus("Latao", 0.28, 0.04, (0, 0, 3.0))
    cyl("Latao", 0.32, 0.08, (0, 0, 3.6), verts=24)
    for i in range(4):
        a = i * math.tau / 4
        cyl("Latao", 0.025, 0.6, (math.cos(a) * 0.26, math.sin(a) * 0.26, 3.3), verts=6)
    gem(0.18, (0, 0, 3.3), scale=(1, 1, 1.7))
    save_and_export("PosteArcano")


def caldeira():
    """Caldeira de cobre com faixa de cristal: o vapor é arcano. Altura ~5.4 m."""
    reset_scene()
    cyl("FerroEscuro", 1.3, 0.3, (0, 0, 0.15), verts=40, bevel=0.03)
    cyl("Cobre", 1.2, 3.6, (0, 0, 2.0), verts=40)
    sphere("Latao", 1.2, (0, 0, 3.8), scale=(1, 1, 0.5))
    for z in (0.8, 3.4):
        torus("Latao", 1.22, 0.05, (0, 0, z))
    cyl("CristalArcano", 1.23, 0.45, (0, 0, 2.2), verts=40)
    for i in range(8):
        a = i * math.tau / 8
        cyl("Latao", 0.04, 0.55, (math.cos(a) * 1.26, math.sin(a) * 1.26, 2.2), verts=6)
    cyl("FerroEscuro", 0.25, 1.6, (0, 0, 4.6), verts=16)
    save_and_export("Caldeira")


def portao_maquina():
    """Carcaça do portão de inimigos. A engrenagem é um modelo separado, para girar."""
    reset_scene()
    box("FerroEscuro", (4.5, 1.6, 4.0), (0, 0, 2.0), bevel=0.06)
    box("Cobre", (5.2, 1.3, 0.4), (0, 0, 4.2), bevel=0.04)
    for x in (-2.45, 2.45):
        cyl("Cobre", 0.35, 4.4, (x, 0, 2.2), verts=20)
        for z in (1.2, 3.0):
            gem(0.4, (x, 0, z), scale=(1, 1, 0.8))
    for y in (-0.82, 0.82):
        torus("Latao", 1.45, 0.08, (0, y, 2.0), rot=(math.pi / 2, 0, 0))
    save_and_export("PortaoMaquina")


def manequim():
    """Personagem genérico, igual para todos. Neutro de propósito: a identidade aparece depois."""
    reset_scene()
    for x in (-0.15, 0.15):
        cyl("PersonagemNeutro", 0.12, 0.9, (x, 0, 0.45), verts=16)
    box("PersonagemNeutro", (0.45, 0.25, 0.22), (0, 0, 0.98), bevel=0.04)
    cyl("PersonagemNeutro", 0.26, 0.7, (0, 0, 1.38), verts=24, scale=(1, 0.7, 1))
    for x in (-0.34, 0.34):
        sphere("PersonagemNeutro", 0.1, (x, 0, 1.66))
        cyl("PersonagemNeutro", 0.08, 0.75, (x * 1.05, 0, 1.25), verts=12)
    cyl("PersonagemNeutro", 0.07, 0.12, (0, 0, 1.78), verts=12)
    sphere("PersonagemNeutro", 0.17, (0, 0, 1.95))
    save_and_export("Manequim")


def alavanca_base():
    """Base da alavanca de largada: caixa de ferro com engrenagens e cristal nas laterais."""
    reset_scene()
    box("FerroEscuro", (0.9, 0.7, 1.0), (0, 0, 0.5), bevel=0.04)
    box("Latao", (1.0, 0.8, 0.1), (0, 0, 1.03), bevel=0.02)
    box("FerroEscuro", (0.16, 0.5, 0.08), (0, 0, 1.1))  # fenda do braço
    for x in (-0.46, 0.46):
        cyl("Latao", 0.28, 0.06, (x, 0, 0.55), rot=(0, math.pi / 2, 0), verts=24)
        gem(0.12, (x * 1.06, 0, 0.55), scale=(0.6, 1, 1))
    save_and_export("AlavancaBase")


def alavanca_braco():
    """Braço da alavanca. O pivô fica na origem (gira em X); o braço sobe em Z."""
    reset_scene()
    cyl("Latao", 0.07, 0.2, (0, 0, 0), rot=(0, math.pi / 2, 0), verts=12)
    cyl("FerroEscuro", 0.045, 0.85, (0, 0, 0.42), verts=10)
    sphere("Latao", 0.11, (0, 0, 0.88))
    save_and_export("AlavancaBraco")


def anel_marcador():
    """Anel discreto no chão sob o próprio personagem (D-010). Não usa cristal para não competir com a aura."""
    reset_scene()
    torus("MarcadorLocal", 0.62, 0.035, (0, 0, 0.02))
    bpy.context.active_object.scale = (1, 1, 0.3)
    save_and_export("AnelMarcador")


if __name__ == "__main__":
    alavanca_base()
    alavanca_braco()
    anel_marcador()
    engrenagem()
    cano(with_crystal=False)
    cano(with_crystal=True)
    poste_arcano()
    caldeira()
    portao_maquina()
    manequim()
    print("[build_props] concluído")
