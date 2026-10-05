using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Cards
{
    /// <summary>
    /// Caixa de ferramentas dos efeitos (D-042), só desenho. Pensado para ler bem na câmera de jogo:
    /// partículas são quadrados chapados e grandes, anéis e leques são grossos, flashes são fortes.
    /// Materiais e malhas são criados uma vez em runtime (URP Unlit / Particles Unlit, sem textura) e compartilhados.
    /// Usado pelo CardVisuals, pelo SwingVisual, pelo HitFeedback e pelo aviso dos inimigos.
    /// </summary>
    public static class FxKit
    {
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int ColorId = Shader.PropertyToID("_Color");

        // Paleta dos efeitos (cores vivas: o contraste vem daqui, a luz da cena é escura).
        public static readonly Color CyanBright = new Color(0.45f, 0.95f, 1f, 1f);
        public static readonly Color CyanWhite = new Color(0.85f, 1f, 1f, 1f);
        public static readonly Color Violet = new Color(0.62f, 0.42f, 1f, 1f);
        public static readonly Color EmberOrange = new Color(1f, 0.5f, 0.15f, 1f);
        public static readonly Color EmberHot = new Color(1f, 0.85f, 0.45f, 1f);
        public static readonly Color WhiteHot = new Color(1f, 0.96f, 0.85f, 1f);
        public static readonly Color OilGlow = new Color(1f, 0.72f, 0.36f, 1f);
        public static readonly Color SteamWhite = new Color(0.86f, 0.9f, 0.94f, 0.8f);
        public static readonly Color SteamWarm = new Color(0.95f, 0.88f, 0.78f, 0.8f);
        public static readonly Color SmokeDark = new Color(0.22f, 0.21f, 0.24f, 0.85f);
        public static readonly Color BrassLight = new Color(0.9f, 0.72f, 0.32f, 1f);

        private const int RingSegments = 40;
        private const int DiscSegments = 32;

        private enum Blend
        {
            Opaque,
            Alpha,
            Additive
        }

        private static Material solidParticle, puffParticle, glowParticle, flatSolid, flatAlpha, flatAdditive;
        private static Mesh quadMesh, cubeMesh, sphereMesh, cylinderMesh, capsuleMesh, discMesh;
        private static readonly Dictionary<int, Mesh> ringMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> gearMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> fanMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> fanBandMeshes = new Dictionary<int, Mesh>();

        // ---------- Materiais ----------

        /// <summary>Partícula opaca e chapada: quadrado de cor sólida (a cor vem da partícula). Para faíscas e estilhaços.</summary>
        public static Material SolidParticle => solidParticle != null ? solidParticle : (solidParticle = MakeParticle(Blend.Opaque, "FxSolido (runtime)"));

        /// <summary>Partícula com alfa, quadrada: fumaça e vapor em blocos.</summary>
        public static Material PuffParticle => puffParticle != null ? puffParticle : (puffParticle = MakeParticle(Blend.Alpha, "FxBloco (runtime)"));

        /// <summary>Partícula aditiva, quadrada: brilhos. Também serve a trilhas e linhas com cor por vértice.</summary>
        public static Material GlowParticle => glowParticle != null ? glowParticle : (glowParticle = MakeParticle(Blend.Additive, "FxBrilho (runtime)"));

        /// <summary>Unlit opaco para malhas (cor por MaterialPropertyBlock, _BaseColor).</summary>
        public static Material FlatSolid => flatSolid != null ? flatSolid : (flatSolid = MakeFlat(Blend.Opaque, "FxChapado (runtime)"));

        /// <summary>Unlit com alfa para malhas, anéis e leques (cor por MaterialPropertyBlock).</summary>
        public static Material FlatAlpha => flatAlpha != null ? flatAlpha : (flatAlpha = MakeFlat(Blend.Alpha, "FxAlfa (runtime)"));

        /// <summary>Unlit aditivo para malhas, anéis e linhas (cor por MaterialPropertyBlock).</summary>
        public static Material FlatAdditive => flatAdditive != null ? flatAdditive : (flatAdditive = MakeFlat(Blend.Additive, "FxAditivo (runtime)"));

        private static Material MakeParticle(Blend mode, string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var m = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            ApplyBlend(m, mode, true);
            return m;
        }

        private static Material MakeFlat(Blend mode, string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var m = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            ApplyBlend(m, mode, false);
            SetFloat(m, "_Cull", 0f); // dos dois lados
            return m;
        }

        private static void ApplyBlend(Material m, Blend mode, bool particle)
        {
            bool transparent = mode != Blend.Opaque;
            SetFloat(m, "_Surface", transparent ? 1f : 0f);
            SetFloat(m, "_Blend", mode == Blend.Additive ? 2f : 0f);
            SetFloat(m, "_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            SetFloat(m, "_DstBlend", mode == Blend.Additive ? (float)BlendMode.One
                : transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            SetFloat(m, "_ZWrite", transparent ? 0f : 1f);
            if (particle)
                SetFloat(m, "_Cull", 0f);

            if (transparent)
            {
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.renderQueue = (int)RenderQueue.Geometry;
            }
        }

        public static void SetFloat(Material m, string property, float value)
        {
            if (m != null && m.HasProperty(property))
                m.SetFloat(property, value);
        }

        // ---------- Malhas ----------

        /// <summary>Quadrado 1 x 1 no plano XZ, centrado, olhando para cima.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quadMesh == null)
                {
                    quadMesh = MakeMesh("FxQuadrado",
                        new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                        new[] { 0, 1, 2, 0, 2, 3 });
                }
                return quadMesh;
            }
        }

        public static Mesh Cube => cubeMesh != null ? cubeMesh : (cubeMesh = PrimitiveMesh(PrimitiveType.Cube));
        public static Mesh Sphere => sphereMesh != null ? sphereMesh : (sphereMesh = PrimitiveMesh(PrimitiveType.Sphere));
        public static Mesh Cylinder => cylinderMesh != null ? cylinderMesh : (cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder));
        public static Mesh Capsule => capsuleMesh != null ? capsuleMesh : (capsuleMesh = PrimitiveMesh(PrimitiveType.Capsule));

        /// <summary>Disco de raio 1 no plano XZ.</summary>
        public static Mesh Disc
        {
            get
            {
                if (discMesh == null)
                {
                    var vertices = new Vector3[DiscSegments + 1];
                    var triangles = new int[DiscSegments * 3];
                    for (int i = 0; i < DiscSegments; i++)
                    {
                        float a = i / (float)DiscSegments * Mathf.PI * 2f;
                        vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        triangles[i * 3] = 0;
                        triangles[i * 3 + 1] = i + 1;
                        triangles[i * 3 + 2] = (i + 1) % DiscSegments + 1;
                    }
                    discMesh = MakeMesh("FxDisco", vertices, triangles);
                }
                return discMesh;
            }
        }

        /// <summary>Pega a malha de uma primitiva e descarta o objeto (que traria um collider).</summary>
        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        /// <summary>Anel achatado de raio externo 1 no plano XZ; inner é o raio interno (0 a 1). Grosso por padrão (pixel).</summary>
        public static Mesh Ring(float inner = 0.8f)
        {
            int key = Mathf.RoundToInt(inner * 100f);
            if (ringMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            var vertices = new Vector3[RingSegments * 2];
            var triangles = new int[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir;

                int next = (i + 1) % RingSegments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = next * 2 + 1;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = next * 2;
            }
            Mesh mesh = MakeMesh("FxAnel", vertices, triangles);
            ringMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Engrenagem de raio externo 1 no plano XZ (Y é a espessura). thickness 0 = chapada (para marcas no chão,
        /// desenhada de um lado só). Com espessura, as faces são separadas (sombreamento chapado, bom para peças).
        /// </summary>
        public static Mesh Gear(int teeth = 8, float holeRatio = 0.3f, float thickness = 0.25f)
        {
            teeth = Mathf.Max(5, teeth);
            int key = teeth * 100000 + Mathf.RoundToInt(holeRatio * 100f) * 100 + Mathf.RoundToInt(thickness * 100f);
            if (gearMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            const float root = 0.78f;
            int count = teeth * 4;
            float pitch = Mathf.PI * 2f / teeth;
            float[] along = { 0f, 0.12f, 0.38f, 0.5f };
            float[] radius = { root, 1f, 1f, root };
            float holeRadius = root * Mathf.Clamp(holeRatio, 0.05f, 0.9f);

            var profile = new Vector2[count];
            var hole = new Vector2[count];
            for (int i = 0; i < teeth; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    float angle = i * pitch + along[j] * pitch;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    profile[i * 4 + j] = dir * radius[j];
                    hole[i * 4 + j] = dir * holeRadius;
                }
            }

            bool thick = thickness > 0.002f;
            float h = thick ? thickness * 0.5f : 0f;
            var v = new List<Vector3>();
            var tris = new List<int>();

            // Face de cima (normal para +Y): perfil e furo.
            int top = v.Count;
            for (int k = 0; k < count; k++) v.Add(new Vector3(profile[k].x, h, profile[k].y));
            int topHole = v.Count;
            for (int k = 0; k < count; k++) v.Add(new Vector3(hole[k].x, h, hole[k].y));
            for (int k = 0; k < count; k++)
            {
                int n = (k + 1) % count;
                tris.Add(top + k); tris.Add(topHole + n); tris.Add(top + n);
                tris.Add(top + k); tris.Add(topHole + k); tris.Add(topHole + n);
            }

            if (thick)
            {
                // Face de baixo (ordem invertida).
                int bottom = v.Count;
                for (int k = 0; k < count; k++) v.Add(new Vector3(profile[k].x, -h, profile[k].y));
                int bottomHole = v.Count;
                for (int k = 0; k < count; k++) v.Add(new Vector3(hole[k].x, -h, hole[k].y));
                for (int k = 0; k < count; k++)
                {
                    int n = (k + 1) % count;
                    tris.Add(bottom + k); tris.Add(bottom + n); tris.Add(bottomHole + n);
                    tris.Add(bottom + k); tris.Add(bottomHole + n); tris.Add(bottomHole + k);
                }

                // Parede externa (normal para fora).
                int outerTop = v.Count;
                for (int k = 0; k < count; k++) v.Add(new Vector3(profile[k].x, h, profile[k].y));
                int outerBottom = v.Count;
                for (int k = 0; k < count; k++) v.Add(new Vector3(profile[k].x, -h, profile[k].y));
                for (int k = 0; k < count; k++)
                {
                    int n = (k + 1) % count;
                    tris.Add(outerTop + k); tris.Add(outerTop + n); tris.Add(outerBottom + n);
                    tris.Add(outerTop + k); tris.Add(outerBottom + n); tris.Add(outerBottom + k);
                }

                // Parede do furo (normal para dentro).
                int innerTop = v.Count;
                for (int k = 0; k < count; k++) v.Add(new Vector3(hole[k].x, h, hole[k].y));
                int innerBottom = v.Count;
                for (int k = 0; k < count; k++) v.Add(new Vector3(hole[k].x, -h, hole[k].y));
                for (int k = 0; k < count; k++)
                {
                    int n = (k + 1) % count;
                    tris.Add(innerTop + k); tris.Add(innerBottom + n); tris.Add(innerTop + n);
                    tris.Add(innerTop + k); tris.Add(innerBottom + k); tris.Add(innerBottom + n);
                }
            }

            Mesh mesh = MakeMesh("FxEngrenagem", v.ToArray(), tris.ToArray());
            gearMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>Leque de raio 1 apontando para +Z, de -meia-abertura a +meia-abertura.</summary>
        public static Mesh Fan(float halfAngleDegrees, int segments = 20)
        {
            int key = Mathf.RoundToInt(halfAngleDegrees) * 1000 + segments;
            if (fanMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
                vertices[i + 1] = ArcPoint(halfAngleDegrees, i, segments, 1f);
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            Mesh mesh = MakeMesh("FxLeque", vertices, triangles);
            fanMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>Faixa na borda externa do arco (raio de inner a 1), para o contorno do leque.</summary>
        public static Mesh FanBand(float halfAngleDegrees, float inner = 0.88f, int segments = 20)
        {
            int key = Mathf.RoundToInt(halfAngleDegrees) * 100000 + Mathf.RoundToInt(inner * 100f) * 100 + segments;
            if (fanBandMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                vertices[i * 2] = ArcPoint(halfAngleDegrees, i, segments, inner);
                vertices[i * 2 + 1] = ArcPoint(halfAngleDegrees, i, segments, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 1;
                triangles[t + 2] = a + 3;
                triangles[t + 3] = a;
                triangles[t + 4] = a + 3;
                triangles[t + 5] = a + 2;
            }
            Mesh mesh = MakeMesh("FxBordaLeque", vertices, triangles);
            fanBandMeshes[key] = mesh;
            return mesh;
        }

        public static Vector3 ArcPoint(float halfAngleDegrees, int index, int segments, float radius)
        {
            float angle = Mathf.Lerp(-halfAngleDegrees, halfAngleDegrees, index / (float)segments) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        public static Mesh MakeMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Objetos ----------

        /// <summary>Objeto raiz de um efeito: some sozinho depois de life segundos (CardVisualLife).</summary>
        public static GameObject Root(string name, Vector3 position, Quaternion rotation, float life)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<CardVisualLife>().Life = life;
            return go;
        }

        public static MeshRenderer MeshChild(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>Linha com N pontos, material aditivo sem textura (a cor vai pelo MaterialPropertyBlock).</summary>
        public static LineRenderer Line(Transform parent, string name, int points, float startWidth, float endWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = points;
            line.useWorldSpace = true;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = FlatAdditive;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public static void Paint(Renderer renderer, MaterialPropertyBlock block, Color color, float alpha)
        {
            color.a = alpha;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color); // URP Unlit
            block.SetColor(ColorId, color);     // Sprites/Default (reserva)
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Luz pontual de um clarão: forte no começo e some rápido.</summary>
        public static void Flash(Vector3 position, Color color, float intensity, float range, float life)
        {
            var go = new GameObject("Clarao");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            var visual = go.AddComponent<CardVisualLife>();
            visual.Life = life;
            visual.OnTick = t => light.intensity = intensity * (1f - t) * (1f - t);
        }

        /// <summary>Anel grosso no chão que se abre até o raio e some (aditivo).</summary>
        public static void GroundRing(Vector3 position, float radius, Color color, float life, float startScale = 0.15f, float inner = 0.78f)
        {
            var go = new GameObject("Anel");
            go.transform.position = position;
            MeshRenderer renderer = MeshChild(go.transform, "Malha", Ring(inner), FlatAdditive);
            var block = new MaterialPropertyBlock();

            var visual = go.AddComponent<CardVisualLife>();
            visual.Life = life;
            visual.OnTick = t =>
            {
                float open = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(startScale, 1f, open) * radius;
                go.transform.localScale = new Vector3(s, 1f, s);
                Paint(renderer, block, color, 1f - t * t);
            };
            visual.OnTick(0f);
        }

        /// <summary>
        /// Clarão chapado e forte: uma forma cheia (esfera ou disco) que cresce rápido e some (aditivo).
        /// Lê bem em baixa resolução como um "estouro" branco.
        /// </summary>
        public static void Pop(Vector3 position, Color color, float radius, float life, bool flat = false, float startScale = 0.35f)
        {
            var go = new GameObject("Estouro");
            go.transform.position = position;
            MeshRenderer renderer = MeshChild(go.transform, "Malha", flat ? Disc : Sphere, FlatAdditive);
            var block = new MaterialPropertyBlock();

            var visual = go.AddComponent<CardVisualLife>();
            visual.Life = life;
            visual.OnTick = t =>
            {
                float open = 1f - (1f - t) * (1f - t);
                // A esfera primitiva tem diâmetro 1; o disco, raio 1.
                float s = Mathf.Lerp(startScale, 1f, open) * radius * (flat ? 1f : 2f);
                go.transform.localScale = flat ? new Vector3(s, 1f, s) : Vector3.one * s;
                Paint(renderer, block, color, 1f - t * t);
            };
            visual.OnTick(0f);
        }

        // ---------- Partículas ----------

        /// <summary>Sistema de partículas parado e vazio, com o material e os módulos básicos.</summary>
        public static ParticleSystem Emitter(Transform parent, string name, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        public static void Configure(ParticleSystem ps, float duration, float lifeMin, float lifeMax, float speedMin,
            float speedMax, float sizeMin, float sizeMax, Color colorA, Color colorB, float gravity = 0f)
        {
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
        }

        public static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        public static void Rate(ParticleSystem ps, float perSecond)
        {
            var emission = ps.emission;
            emission.rateOverTime = perSecond;
        }

        public static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius = 0.3f,
            float angle = 25f, float thickness = 1f, Vector3? scale = null)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
            shape.angle = angle;
            shape.radiusThickness = thickness;
            if (scale.HasValue)
                shape.scale = scale.Value;
        }

        public static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        /// <summary>Encolhe até sumir (para partículas opacas, que não têm alfa para apagar).</summary>
        public static void Shrink(ParticleSystem ps)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.85f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>Some no fim da vida; com fadeIn, também aparece devagar no começo.</summary>
        public static void Fade(ParticleSystem ps, float fadeIn = 0f)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(fadeIn > 0f ? 0f : 1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Max(fadeIn, 0.01f)),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        /// <summary>Emite uma partícula com posição, velocidade, tamanho, cor e vida próprios (mundo).</summary>
        public static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, Color color, float lifetime)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startColor = color,
                startLifetime = lifetime
            };
            ps.Emit(p, 1);
        }

        /// <summary>Estouro de faíscas: poucos quadrados grandes e opacos que encolhem. A faísca padrão do jogo em pixel.</summary>
        public static ParticleSystem Sparks(Transform parent, Vector3 position, int count, float speedMin, float speedMax,
            float sizeMin, float sizeMax, Color colorA, Color colorB, float lifeMin = 0.25f, float lifeMax = 0.5f,
            float gravity = 1.2f, float spawnRadius = 0.2f)
        {
            var ps = Emitter(parent, "Fagulhas", position, Quaternion.identity, SolidParticle);
            Configure(ps, 0.2f, lifeMin, lifeMax, speedMin, speedMax, sizeMin, sizeMax, colorA, colorB, gravity);
            Shape(ps, ParticleSystemShapeType.Sphere, radius: spawnRadius);
            Burst(ps, count);
            Shrink(ps);
            ps.Play();
            return ps;
        }

        /// <summary>Estouro de blocos de vapor ou fumaça: quadrados grandes com alfa que crescem e somem.</summary>
        public static ParticleSystem Puffs(Transform parent, Vector3 position, int count, float speedMin, float speedMax,
            float sizeMin, float sizeMax, Color colorA, Color colorB, float lifeMin = 0.6f, float lifeMax = 1f,
            float gravity = -0.1f, float spawnRadius = 0.4f)
        {
            var ps = Emitter(parent, "Vapor", position, Quaternion.identity, PuffParticle);
            Configure(ps, 0.2f, lifeMin, lifeMax, speedMin, speedMax, sizeMin, sizeMax, colorA, colorB, gravity);
            Shape(ps, ParticleSystemShapeType.Sphere, radius: spawnRadius);
            Burst(ps, count);
            Grow(ps, 0.7f, 1.7f);
            Fade(ps, 0.08f);
            ps.Play();
            return ps;
        }

        // ---------- Peças que voam ----------

        /// <summary>
        /// Peças em malha que saem em arco e caem no chão (engrenagens de latão, estilhaços de cristal), sem física:
        /// trajetória balística calculada, para quando chegam ao chão e somem encolhendo. groundY é a altura do chão.
        /// </summary>
        public static void Debris(Vector3 center, float groundY, int count, Mesh mesh, Material material,
            float scaleMin, float scaleMax, float horizontalMin, float horizontalMax, float upMin, float upMax, float life)
        {
            if (mesh == null || material == null)
                return;

            const float g = 9.8f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count + Random.Range(-0.4f, 0.4f) / count) * Mathf.PI * 2f;
                Vector3 velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(horizontalMin, horizontalMax);
                velocity.y = Random.Range(upMin, upMax);
                float scale = Random.Range(scaleMin, scaleMax);
                Vector3 spin = new Vector3(Random.Range(-600f, 600f), Random.Range(-600f, 600f), Random.Range(-600f, 600f));
                Quaternion start = Random.rotation;

                var go = new GameObject("Peca");
                go.transform.SetPositionAndRotation(center, start);
                go.transform.localScale = Vector3.one * scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float floor = groundY + scale * 0.25f;
                // Tempo até tocar o chão: y0 + vy t - g/2 t² = floor.
                float disc = velocity.y * velocity.y + 2f * g * (center.y - floor);
                float landing = disc > 0f ? (velocity.y + Mathf.Sqrt(disc)) / g : 0f;

                var visual = go.AddComponent<CardVisualLife>();
                visual.Life = life;
                visual.OnTick = t =>
                {
                    float tau = Mathf.Min(t * life, landing);
                    Vector3 p = center + new Vector3(velocity.x * tau, velocity.y * tau - 0.5f * g * tau * tau, velocity.z * tau);
                    go.transform.position = p;
                    go.transform.rotation = start * Quaternion.Euler(spin * tau);
                    float shrink = Mathf.Clamp01((1f - t) / 0.25f);
                    go.transform.localScale = Vector3.one * (scale * shrink);
                };
            }
        }

        // ---------- Utilidades ----------

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        public static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
