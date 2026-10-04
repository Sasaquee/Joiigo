"""
Gera os três inimigos provisórios no Blender e exporta FBX para a Unity.

Uso:
    blender --background --factory-startup --python Tools/Blender/build_enemies.py

Diferente dos props, os inimigos NÃO são unidos numa malha só: cada peça é um objeto,
filho de um Empty raiz, para poder se soltar na morte (D-026). Convenção de nomes
(lida pelo EnemyPrefabBuilder na Unity):
    Corpo          corpo principal
    Parte_*        peças que se soltam
    Cristal        núcleo arcano (material CristalArcano; apaga na morte)
    Arma           peça que recua no aviso (D-024); no drone, o canhão
A frente do inimigo é -Y no Blender. O pivô fica no chão.

Pilar 4: o cristal é o que move a máquina. Ele fica embutido no corpo, preso por metal,
e não pendurado ao lado.
"""

import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLEND_DIR = os.path.join(ROOT, "Art", "Blender")
FBX_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Models")

MATERIALS = {
    "Cobre": ((0.72, 0.42, 0.25, 1), 0.9, 0.45, None),
    "Latao": ((0.78, 0.62, 0.30, 1), 0.9, 0.5, None),
    "FerroEscuro": ((0.18, 0.18, 0.20, 1), 0.7, 0.65, None),
    "CristalArcano": ((0.30, 0.90, 0.95, 1), 0.0, 0.1, (0.2, 1.0, 1.0, 1)),
}


# ---------- utilidades ----------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"


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


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, mat_name, bevel=0.0, smooth=False):
    obj.data.materials.append(material(mat_name))
    select_only(obj)
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


def cyl(mat, r, depth, loc, rot=(0, 0, 0), verts=24, scale=(1, 1, 1), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, mat, bevel=bevel, smooth=True)


def box(mat, size, loc, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return finish(o, mat, bevel=bevel)


def sphere(mat, r, loc, scale=(1, 1, 1), segments=20):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=segments, ring_count=segments // 2)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, mat, smooth=True)


def torus(mat, major, minor, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot,
                                     major_segments=28, minor_segments=8)
    return finish(bpy.context.active_object, mat, smooth=True)


def gem(r, loc, scale=(1, 1, 1), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, "CristalArcano")


def part(name, objs):
    """Une vários objetos numa peça só, com a origem no centro da peça (para girar ao se soltar)."""
    select_only(objs[0])
    for o in objs[1:]:
        o.select_set(True)
    bpy.ops.object.join()
    p = bpy.context.active_object
    p.name = name
    p.data.name = name
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    # UVs simples para as texturas PBR da Unity.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    return p


def export(name, parts):
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    for p in parts:
        p.parent = root
    os.makedirs(BLEND_DIR, exist_ok=True)
    os.makedirs(FBX_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, f"{name}.blend"))

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for p in parts:
        p.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, f"{name}.fbx"),
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
    faces = sum(len(p.data.polygons) for p in parts)
    print(f"[build_enemies] {name}: {len(parts)} peças, {faces} faces")


# ---------- inimigos (frente = -Y) ----------

def automato():
    """Autômato pequeno de corpo a corpo (~1,4 m). Barril de ferro movido por um cristal no peito."""
    reset_scene()
    parts = []

    corpo = [
        cyl("FerroEscuro", 0.36, 0.62, (0, 0, 0.86), verts=20, bevel=0.02),
        torus("Latao", 0.37, 0.035, (0, 0, 0.6)),
        torus("Latao", 0.37, 0.035, (0, 0, 1.12)),
        cyl("Cobre", 0.3, 0.12, (0, 0, 1.2), verts=20),
        box("FerroEscuro", (0.5, 0.28, 0.14), (0, 0, 0.5), bevel=0.02),  # quadril
    ]
    for i in range(10):  # rebites
        a = i * math.tau / 10
        corpo.append(sphere("Latao", 0.025, (math.cos(a) * 0.37, math.sin(a) * 0.37, 0.86), segments=8))
    parts.append(part("Corpo", corpo))

    # Moldura de latão que segura o cristal no peito.
    parts.append(part("Parte_Moldura", [torus("Latao", 0.13, 0.03, (0, -0.36, 0.9), rot=(math.pi / 2, 0, 0))]))
    parts.append(part("Cristal", [gem(0.12, (0, -0.37, 0.9), scale=(1, 0.7, 1.2))]))

    cabeca = [
        cyl("FerroEscuro", 0.17, 0.22, (0, 0, 1.38), verts=16, bevel=0.015),
        cyl("Cobre", 0.06, 0.06, (0, -0.16, 1.4), rot=(math.pi / 2, 0, 0), verts=12),  # lente
        cyl("Latao", 0.19, 0.03, (0, 0, 1.5), verts=16),
    ]
    parts.append(part("Parte_Cabeca", cabeca))

    # Braço direito = arma: pistão com garra virada para a frente.
    arma = [
        sphere("Latao", 0.1, (0.44, 0, 1.02)),
        cyl("FerroEscuro", 0.06, 0.36, (0.46, -0.12, 0.86), rot=(math.radians(55), 0, 0), verts=10),
        cyl("Cobre", 0.04, 0.3, (0.46, -0.3, 0.78), rot=(math.pi / 2, 0, 0), verts=10),
        box("FerroEscuro", (0.18, 0.16, 0.2), (0.46, -0.48, 0.76), bevel=0.02),
        box("Latao", (0.04, 0.14, 0.04), (0.39, -0.6, 0.7)),
        box("Latao", (0.04, 0.14, 0.04), (0.53, -0.6, 0.7)),
    ]
    parts.append(part("Arma", arma))

    braco_esq = [
        sphere("Latao", 0.1, (-0.44, 0, 1.02)),
        cyl("FerroEscuro", 0.06, 0.42, (-0.47, 0, 0.78), verts=10),
        box("FerroEscuro", (0.14, 0.14, 0.14), (-0.47, 0, 0.54), bevel=0.02),
    ]
    parts.append(part("Parte_BracoEsq", braco_esq))

    for side, name in ((-1, "Parte_PernaEsq"), (1, "Parte_PernaDir")):
        x = 0.16 * side
        perna = [
            cyl("FerroEscuro", 0.075, 0.34, (x, 0, 0.3), verts=12),
            cyl("Cobre", 0.045, 0.3, (x, 0.05, 0.32), verts=10),  # pistão
            box("FerroEscuro", (0.16, 0.26, 0.08), (x, -0.04, 0.04), bevel=0.02),
        ]
        parts.append(part(name, perna))

    costas = [
        cyl("Latao", 0.16, 0.05, (0, 0.38, 0.95), rot=(math.pi / 2, 0, 0), verts=20),
        cyl("FerroEscuro", 0.05, 0.08, (0, 0.42, 0.95), rot=(math.pi / 2, 0, 0), verts=10),
    ]
    for i in range(8):
        a = i * math.tau / 8
        costas.append(box("Latao", (0.05, 0.04, 0.05), (math.cos(a) * 0.18, 0.38, 0.95 + math.sin(a) * 0.18)))
    parts.append(part("Parte_Engrenagem", costas))
    parts.append(part("Parte_Chamine", [
        cyl("FerroEscuro", 0.05, 0.35, (0.15, 0.3, 1.32), verts=10),
        cyl("Cobre", 0.065, 0.04, (0.15, 0.3, 1.5), verts=10),
    ]))

    export("EnemyAutomato", parts)


def drone():
    """Drone de ataque à distância. Flutua a ~1,4 m. O cristal no anel alimenta rotores e canhão."""
    reset_scene()
    parts = []
    h = 1.4

    parts.append(part("Corpo", [
        sphere("FerroEscuro", 0.34, (0, 0, h), scale=(1, 1, 0.72)),
        cyl("Cobre", 0.36, 0.06, (0, 0, h), verts=24),
        cyl("FerroEscuro", 0.12, 0.18, (0, 0, h - 0.28), verts=12),
    ]))
    parts.append(part("Parte_Anel", [torus("Latao", 0.42, 0.04, (0, 0, h))]))
    parts.append(part("Cristal", [gem(0.13, (0, 0, h - 0.38), scale=(1, 1, 1.5))]))

    for i, (sx, sy) in enumerate(((1, 1), (-1, 1), (1, -1), (-1, -1))):
        x, y = 0.5 * sx, 0.5 * sy
        rotor = [
            cyl("FerroEscuro", 0.03, 0.55, (x * 0.55, y * 0.55, h + 0.08),
                rot=(0, math.pi / 2, math.atan2(y, x)), verts=8),
            cyl("Latao", 0.07, 0.08, (x, y, h + 0.1), verts=12),
            cyl("Cobre", 0.2, 0.012, (x, y, h + 0.16), verts=20),
        ]
        parts.append(part(f"Parte_Rotor{i + 1}", rotor))

    # Canhão à frente (-Y), com bocal de cristal: o tiro é energia do próprio núcleo.
    parts.append(part("Arma", [
        cyl("Cobre", 0.075, 0.42, (0, -0.42, h - 0.05), rot=(math.pi / 2, 0, 0), verts=14),
        torus("Latao", 0.08, 0.02, (0, -0.6, h - 0.05), rot=(math.pi / 2, 0, 0)),
        torus("Latao", 0.08, 0.02, (0, -0.32, h - 0.05), rot=(math.pi / 2, 0, 0)),
    ]))
    parts.append(part("Parte_Antena", [
        cyl("FerroEscuro", 0.012, 0.3, (0.1, 0.15, h + 0.36), verts=6),
        sphere("Latao", 0.03, (0.1, 0.15, h + 0.52), segments=8),
    ]))

    export("EnemyDrone", parts)


def constructo():
    """Constructo arcano resistente (~2,3 m). Pedra-ferro amarrada por cobre, coração de cristal."""
    reset_scene()
    parts = []

    parts.append(part("Corpo", [
        box("FerroEscuro", (1.15, 0.8, 0.95), (0, 0, 1.45), bevel=0.08),
        box("FerroEscuro", (0.8, 0.6, 0.35), (0, 0, 0.85), bevel=0.06),
        box("Cobre", (1.2, 0.84, 0.08), (0, 0, 1.15), bevel=0.02),
        box("Cobre", (1.2, 0.84, 0.08), (0, 0, 1.75), bevel=0.02),
    ]))
    # Placa frontal de latão aberta no meio, deixando o coração à mostra.
    parts.append(part("Parte_Peitoral", [
        box("Latao", (0.18, 0.06, 0.6), (-0.3, -0.42, 1.45), bevel=0.02),
        box("Latao", (0.18, 0.06, 0.6), (0.3, -0.42, 1.45), bevel=0.02),
        torus("Latao", 0.22, 0.035, (0, -0.42, 1.45), rot=(math.pi / 2, 0, 0)),
    ]))
    parts.append(part("Cristal", [gem(0.24, (0, -0.4, 1.45), scale=(1, 0.6, 1.35))]))

    parts.append(part("Parte_Cabeca", [
        box("FerroEscuro", (0.42, 0.38, 0.3), (0, -0.05, 2.08), bevel=0.04),
        box("Cobre", (0.3, 0.04, 0.05), (0, -0.25, 2.1)),
    ]))

    for side, name in ((-1, "Parte_OmbroEsq"), (1, "Parte_OmbroDir")):
        parts.append(part(name, [sphere("Latao", 0.24, (0.72 * side, 0, 1.78), scale=(1, 1, 0.85))]))

    # Punho direito = arma: bloco enorme à frente, preso por anéis de cobre.
    parts.append(part("Arma", [
        cyl("FerroEscuro", 0.15, 0.6, (0.78, -0.15, 1.35), rot=(math.radians(30), 0, 0), verts=12),
        torus("Cobre", 0.17, 0.035, (0.78, -0.25, 1.2), rot=(math.radians(30), 0, 0)),
        box("FerroEscuro", (0.42, 0.42, 0.42), (0.78, -0.42, 0.98), bevel=0.06),
        box("Latao", (0.44, 0.06, 0.12), (0.78, -0.64, 0.98)),
    ]))
    parts.append(part("Parte_BracoEsq", [
        cyl("FerroEscuro", 0.15, 0.7, (-0.78, 0, 1.3), verts=12),
        torus("Cobre", 0.17, 0.035, (-0.78, 0, 1.2)),
        box("FerroEscuro", (0.38, 0.38, 0.38), (-0.78, 0, 0.82), bevel=0.06),
    ]))

    for side, name in ((-1, "Parte_PernaEsq"), (1, "Parte_PernaDir")):
        x = 0.32 * side
        parts.append(part(name, [
            cyl("FerroEscuro", 0.2, 0.7, (x, 0, 0.4), verts=14, bevel=0.02),
            torus("Cobre", 0.21, 0.035, (x, 0, 0.55)),
            box("FerroEscuro", (0.42, 0.55, 0.12), (x, -0.06, 0.06), bevel=0.03),
        ]))

    # Tubos nas costas que levam vapor ao coração: a máquina respira pelo cristal.
    parts.append(part("Parte_Tubos", [
        cyl("Cobre", 0.07, 0.9, (-0.25, 0.45, 1.6), verts=12),
        cyl("Cobre", 0.07, 0.9, (0.25, 0.45, 1.6), verts=12),
        torus("Latao", 0.08, 0.02, (-0.25, 0.45, 2.02)),
        torus("Latao", 0.08, 0.02, (0.25, 0.45, 2.02)),
    ]))

    export("EnemyConstruto", parts)


if __name__ == "__main__":
    automato()
    drone()
    constructo()
    print("[build_enemies] concluído")
