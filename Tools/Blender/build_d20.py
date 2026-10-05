"""D20 do jogo (Fase 6, D-047): icosaedro de latão com os números em cristal ciano.

Rodar:
  "E:/Program Files/Blender/blender.exe" --background --factory-startup --python Tools/Blender/build_d20.py

Saídas:
  Assets/_Game/Art/Models/Dice/D20.fbx  (o que a Unity usa)
  Art/Blender/Dice/D20.blend            (fonte editável)

Estrutura exportada (sob o Empty raiz "D20"):
  Dado      — corpo de latão chanfrado (material Latao)
  Numeros   — os 20 números em relevo (material CristalArcano; 6 e 9 com ponto)
  Centro_N  — Empty no centro da face N (a direção dele a partir do centro é a normal da face)
  Topo_N    — Empty deslocado do centro da face N para o lado de cima do número
A Unity usa Centro_N e Topo_N para girar o dado e parar com o número N de frente e em pé (DiceRollUi).
Faces opostas somam 21, como num dado de verdade.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FBX_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Models", "Dice")
BLEND_DIR = os.path.join(ROOT, "Art", "Blender", "Dice")

RADIUS = 0.5            # raio do icosaedro (o tamanho na tela vem da câmera da UI)
BEVEL = 0.035           # chanfro das arestas (lê bem no pixel)
TEXT_SIZE = 0.15        # altura dos números: cabem dentro da face plana, longe do chanfro (até o "20")
TEXT_DEPTH = 0.006       # relevo dos números (pouco: não pode soltar da face)
TEXT_LIFT = 0.001        # distância da face (evita brigar com o latão)
TOP_OFFSET = 0.12       # distância do Topo_N ao centro da face

PALETTE = {
    "Latao": ("C9A04A", 0.85, 0.35, None),
    "LataoEscuro": ("7A5F2C", 0.7, 0.5, None),
    "CristalArcano": ("6FF0FF", 0.0, 0.2, ("40E0FF", 1.6)),
}


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexcol(h):
    return tuple(lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    hx, metallic, rough, emit = PALETTE[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hexcol(hx)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = hexcol(emit[0])
        bsdf.inputs["Emission Strength"].default_value = emit[1]
    return mat


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def icosahedron():
    """Devolve (vértices, faces) de um icosaedro de raio RADIUS, faces em sentido anti-horário visto de fora."""
    phi = (1 + 5 ** 0.5) / 2
    raw = [(-1, phi, 0), (1, phi, 0), (-1, -phi, 0), (1, -phi, 0),
           (0, -1, phi), (0, 1, phi), (0, -1, -phi), (0, 1, -phi),
           (phi, 0, -1), (phi, 0, 1), (-phi, 0, -1), (-phi, 0, 1)]
    scale = RADIUS / Vector(raw[0]).length
    verts = [Vector(v) * scale for v in raw]
    faces = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
             (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
             (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
             (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    fixed = []
    for f in faces:
        a, b, c = (verts[i] for i in f)
        n = (b - a).cross(c - a)
        center = (a + b + c) / 3
        fixed.append(f if n.dot(center) > 0 else (f[0], f[2], f[1]))
    return verts, fixed


def assign_numbers(verts, faces):
    """Números 1..20 com faces opostas somando 21."""
    centers = [sum((verts[i] for i in f), Vector()) / 3 for f in faces]
    numbers = [0] * len(faces)
    # Ordena as faces de cima para baixo (e por ângulo) e numera em pares opostos.
    order = sorted(range(len(faces)), key=lambda i: (-round(centers[i].z, 3), math.atan2(centers[i].y, centers[i].x)))
    next_low = 1
    for i in order:
        if numbers[i]:
            continue
        opposite = min(range(len(faces)), key=lambda j: (centers[j] + centers[i]).length)
        numbers[i] = next_low
        numbers[opposite] = 21 - next_low
        next_low += 1
    return numbers, centers


def make_number(n, center, normal, up, mat):
    curve = bpy.data.curves.new(f"Num_{n}", type="FONT")
    curve.body = f"{n}." if n in (6, 9) else str(n)
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.size = TEXT_SIZE
    curve.extrude = TEXT_DEPTH
    curve.offset = 0.0
    obj = bpy.data.objects.new(f"Num_{n}", curve)
    bpy.context.scene.collection.objects.link(obj)
    right = up.cross(normal).normalized()
    rot = Matrix((right, up, normal)).transposed().to_4x4()
    obj.matrix_world = Matrix.Translation(center + normal * (TEXT_LIFT + TEXT_DEPTH)) @ rot
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return obj


def main():
    reset_scene()
    brass = material("Latao")
    crystal = material("CristalArcano")

    verts, faces = icosahedron()
    numbers, centers = assign_numbers(verts, faces)

    mesh = bpy.data.meshes.new("Dado")
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    mesh.update()
    body = bpy.data.objects.new("Dado", mesh)
    bpy.context.scene.collection.objects.link(body)
    body.data.materials.append(brass)
    bevel = body.modifiers.new("Chanfro", "BEVEL")
    bevel.width = BEVEL
    bevel.segments = 1
    bevel.limit_method = "NONE"

    root = bpy.data.objects.new("D20", None)
    bpy.context.scene.collection.objects.link(root)
    body.parent = root

    number_objs = []
    for fi, f in enumerate(faces):
        n = numbers[fi]
        a = verts[f[0]]
        center = centers[fi]
        normal = center.normalized()
        up = (a - center)
        up = (up - normal * up.dot(normal)).normalized()   # número em pé apontando para o vértice a
        number_objs.append(make_number(n, center, normal, up, crystal))

        c = bpy.data.objects.new(f"Centro_{n}", None)
        c.location = center
        c.empty_display_size = 0.03
        bpy.context.scene.collection.objects.link(c)
        c.parent = root
        t = bpy.data.objects.new(f"Topo_{n}", None)
        t.location = center + up * TOP_OFFSET
        t.empty_display_size = 0.02
        bpy.context.scene.collection.objects.link(t)
        t.parent = root

    # Junta os números num objeto só.
    bpy.ops.object.select_all(action="DESELECT")
    for o in number_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = number_objs[0]
    bpy.ops.object.join()
    nums = bpy.context.active_object
    nums.name = "Numeros"
    nums.data.name = "Numeros"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    nums.parent = root

    os.makedirs(FBX_DIR, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, "D20.fbx"),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
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
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "D20.blend"))

    pairs = sorted(zip(numbers, range(len(numbers))))
    print("[build_d20] faces:", len(body.data.polygons), "números:", [n for n, _ in pairs])
    # Conferência: faces opostas somam 21.
    for fi, n in enumerate(numbers):
        opp = min(range(len(faces)), key=lambda j: (centers[j] + centers[fi]).length)
        assert numbers[opp] == 21 - n, (n, numbers[opp])
    print("[build_d20] ok: faces opostas somam 21")


main()
