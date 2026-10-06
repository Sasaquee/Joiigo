using Game.Arena;
using Game.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Chão andável e vedação do mapa novo (passe do mapa, D-073 a D-082), gerados a partir do <see cref="MapLayout"/>:
    /// - chão COM collider só onde se anda (disco da praça, retângulos das avenidas e das bocas, discos das praças menores),
    ///   na camada Cenario e com o mesmo material de piso; fora disso não há chão (não sobram ilhas de NavMesh);
    /// - vedação invisível (caixas finas e altas, camada Cenario) ao longo do contorno da região andável, vinda de
    ///   <see cref="MapBoundary"/>: o jogador e os inimigos não saem do mapa, mesmo onde não há prédio.
    /// Os números do mapa (raios, larguras, altura da vedação) vêm do MapLayoutSettings; aqui só a resolução da malha.
    /// </summary>
    public static class WalkableBuilder
    {
        // Resolução da malha e da vedação: passo de arco em graus (corda de 5° num raio de 28,3 m erra a borda em ~2,7 cm).
        private const float ArcStepDeg = 5f;
        // Espessura do chão e da vedação (técnica, não é gameplay): a vedação fica para FORA da borda.
        private const float FloorThickness = 0.2f;
        private const float FenceThickness = 0.5f;
        // Sobra de cada caixa de vedação nas pontas, para as caixas vizinhas se sobreporem (m).
        private const float FenceOverlap = 0.1f;
        // As regiões que se sobrepõem (avenida na praça, avenida na praça menor) ficam 4 mm abaixo da praça:
        // evita z-fighting entre superfícies coplanares (o contorno do PixelPost lê profundidade).
        private const float RectDrop = 0.004f;
        // A textura do piso se repete a cada tantos metros (UV em metros do mundo).
        private const float UvMeters = 4f;

        /// <summary>
        /// Chão com collider nas regiões andáveis, em <paramref name="parent"/>/Andavel (camada Cenario). O topo de tudo fica em y = 0
        /// (a praça) e 4 mm abaixo nas avenidas e bocas.
        /// </summary>
        public static Transform BuildGround(Transform parent, MapLayout layout, Material floorMaterial)
        {
            var group = new GameObject("Andavel").transform;
            group.SetParent(parent, false);

            Disc("Praca", group, layout.Plaza.Center.X, layout.Plaza.Center.Y, layout.Plaza.Radius, 0f,
                floorMaterial, collider: true);

            for (int i = 0; i < layout.StreetCount; i++)
            {
                var avenue = layout.Avenues[i];
                Slab($"Avenida{i + 1}", group, avenue.Rect, avenue.AngleDeg, -RectDrop, floorMaterial);

                var plaza = layout.SmallPlazas[i];
                Disc($"PracaMenor{i + 1}", group, plaza.Center.X, plaza.Center.Y, plaza.Radius, 0f,
                    floorMaterial, collider: true);

                var mouth = layout.Mouths[i];
                Slab($"Boca{i + 1}", group, mouth.Rect, mouth.AngleDeg, -RectDrop, floorMaterial);
            }
            return group;
        }

        /// <summary>
        /// Vedação invisível em <paramref name="parent"/>/Vedacao: uma caixa de altura <c>FenceHeight</c> por segmento de
        /// <see cref="MapBoundary"/>, encostada por fora da borda andável, na camada Cenario. Sem renderer.
        /// </summary>
        public static Transform BuildFence(Transform parent, MapLayout layout)
        {
            var group = new GameObject("Vedacao").transform;
            group.SetParent(parent, false);

            float height = layout.FenceHeight;
            var segments = MapBoundary.Segments(layout, ArcStepDeg);
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                float dx = s.B.X - s.A.X, dz = s.B.Y - s.A.Y;
                float length = Mathf.Sqrt(dx * dx + dz * dz);
                var go = new GameObject($"Vedacao{i:000}");
                go.transform.SetParent(group, false);
                go.layer = MapLayers.Cenario;
                go.isStatic = true;
                Vector3 mid = new Vector3(s.Midpoint.X, 0f, s.Midpoint.Y);
                Vector3 outward = new Vector3(s.Outward.X, 0f, s.Outward.Y);
                go.transform.position = mid + outward * (FenceThickness * 0.5f) + Vector3.up * (height * 0.5f);
                go.transform.rotation = Quaternion.LookRotation(new Vector3(dx, 0f, dz)); // +Z local ao longo do segmento
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(FenceThickness, height, length + FenceOverlap * 2f);
            }
            return group;
        }

        /// <summary>
        /// Disco plano com a superfície no alto em <paramref name="topY"/> e <see cref="FloorThickness"/> de espessura, com malha
        /// própria (círculo de verdade, não o cilindro de 20 lados do Unity). Camada Cenario quando tem collider.
        /// </summary>
        public static GameObject Disc(string name, Transform parent, float x, float z, float radius, float topY,
            Material material, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, topY, z);
            go.isStatic = true;

            var mesh = DiscMesh(radius, FloorThickness, x, z);
            mesh.name = $"{name}_malha";
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // chão só recebe sombra
            if (collider)
            {
                go.layer = MapLayers.Cenario;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return go;
        }

        /// <summary>Laje retangular orientada (avenida ou boca) com box collider, topo em <paramref name="topY"/>, camada Cenario.</summary>
        private static void Slab(string name, Transform parent, MapRect rect, float angleDeg, float topY, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(rect.Center.X, topY - FloorThickness * 0.5f, rect.Center.Y);
            go.transform.rotation = Quaternion.Euler(0f, angleDeg, 0f); // +Z local = eixo da rua
            go.transform.localScale = new Vector3(rect.Width, FloorThickness, rect.Length);
            go.layer = MapLayers.Cenario;
            go.isStatic = true;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Prisma de disco: tampa em cima (y = 0, normal para cima) e lateral descendo <paramref name="thickness"/>.
        /// Vértices no referencial local do objeto, que fica no centro do disco. UV em metros do mundo (o centro em
        /// <paramref name="worldX"/>, <paramref name="worldZ"/>) dividido por <see cref="UvMeters"/>.
        /// </summary>
        private static Mesh DiscMesh(float radius, float thickness, float worldX, float worldZ)
        {
            int n = Mathf.Max(24, Mathf.CeilToInt(360f / ArcStepDeg));
            // Tampa: centro + n vértices do aro. Lateral: 2 vértices por ponto do aro (n + 1 para fechar a costura do UV).
            var vertices = new Vector3[1 + n + 2 * (n + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[n * 3 + n * 6];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(worldX / UvMeters, worldZ / UvMeters);
            for (int k = 0; k < n; k++)
            {
                float a = 2f * Mathf.PI * k / n;
                var rim = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                vertices[1 + k] = rim;
                normals[1 + k] = Vector3.up;
                uvs[1 + k] = new Vector2((worldX + rim.x) / UvMeters, (worldZ + rim.z) / UvMeters);
            }

            int side = 1 + n;
            for (int k = 0; k <= n; k++)
            {
                float a = 2f * Mathf.PI * k / n;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var top = radial * radius;
                vertices[side + k * 2] = top;
                vertices[side + k * 2 + 1] = top + Vector3.down * thickness;
                normals[side + k * 2] = radial;
                normals[side + k * 2 + 1] = radial;
                uvs[side + k * 2] = new Vector2((worldX + top.x) / UvMeters, (worldZ + top.z) / UvMeters);
                uvs[side + k * 2 + 1] = uvs[side + k * 2];
            }

            int t = 0;
            for (int k = 0; k < n; k++)
            {
                // Tampa no sentido horário visto de cima (face para cima no Unity).
                triangles[t++] = 0;
                triangles[t++] = 1 + (k + 1) % n;
                triangles[t++] = 1 + k;
            }
            for (int k = 0; k < n; k++)
            {
                int tk = side + k * 2, bk = tk + 1, tk1 = side + (k + 1) * 2, bk1 = tk1 + 1;
                triangles[t++] = tk;
                triangles[t++] = tk1;
                triangles[t++] = bk;
                triangles[t++] = tk1;
                triangles[t++] = bk1;
                triangles[t++] = bk;
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
