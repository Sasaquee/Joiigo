using Game.Cards;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Player
{
    /// <summary>
    /// Visual do golpe em arco (D-020, D-042), pensado para o 3D pixelado: um crescente grosso de latão quente com
    /// miolo branco-quente e borda ciano (metal e arcano, Pilar 4). O golpe "varre" o arco em 4 quadros curtos
    /// (rastro que corre da direita para a esquerda, mais grosso na ponta) e depois segura o arco inteiro e some,
    /// com faíscas na borda e um clarão de luz. Só desenho: o acerto é decidido pelo host.
    /// </summary>
    public class SwingVisual : MonoBehaviour
    {
        private const float Lift = 0.08f;          // acima do chão, para não brigar com o piso
        private const int Segments = 14;
        private const int SweepFrames = 4;
        private const float StepTime = 0.03f;      // duração de cada quadro do rastro
        private const float HoldTime = 0.09f;      // arco inteiro: segura e some
        private const float TailLength = 0.6f;     // comprimento do rastro, em fração do arco

        // Posição da ponta do rastro em cada quadro (0 = começo do arco, 1 = fim).
        private static readonly float[] Heads = { 0.3f, 0.55f, 0.8f, 1f };

        private static readonly Color DefaultFill = new Color(1f, 0.58f, 0.2f, 1f);
        private static readonly Color DefaultCore = new Color(1f, 0.97f, 0.8f, 1f);
        private static readonly Color DefaultRim = new Color(0.45f, 0.95f, 1f, 1f);

        private static int[] bandTriangles;

        private struct Layer
        {
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public float Thickness;
            public float Outer;
        }

        private Layer fill, core, rim;
        private Transform fan;
        private Light flash;
        private MaterialPropertyBlock block;
        private Color fillColor, coreColor, rimColor;
        private float halfAngle;
        private float range;
        private float age;
        private int lastFrame = -1;

        private static float TotalDuration => SweepFrames * StepTime + HoldTime;

        /// <summary>Cria um golpe na posição do jogador, apontando para dir (XZ, normalizado).</summary>
        public static void Play(Vector3 position, Vector3 dir, float range, float halfAngleDegrees)
        {
            Play(position, dir, range, halfAngleDegrees, DefaultFill, DefaultCore, DefaultRim, 0.5f);
        }

        /// <summary>
        /// Igual ao Play, com cores próprias (corte amaldiçoado da Lâmina Sedenta, por exemplo).
        /// thickness é a espessura do crescente em fração do alcance (0.5 é o golpe básico).
        /// </summary>
        public static void Play(Vector3 position, Vector3 dir, float range, float halfAngleDegrees,
            Color fillColor, Color coreColor, Color rimColor, float thickness)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f || !float.IsFinite(range) || !float.IsFinite(position.x) || !float.IsFinite(position.z))
                return;
            if (FxKit.FlatAlpha == null || FxKit.FlatAdditive == null)
                return;

            var go = new GameObject("Golpe");
            go.transform.SetPositionAndRotation(position + Vector3.up * Lift, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var visual = go.AddComponent<SwingVisual>();
            visual.Build(Mathf.Max(0.3f, range), Mathf.Clamp(halfAngleDegrees, 5f, 180f), fillColor, coreColor, rimColor,
                Mathf.Clamp(thickness, 0.15f, 0.9f));
        }

        private void Build(float swingRange, float swingHalfAngle, Color fillTint, Color coreTint, Color rimTint, float thickness)
        {
            range = swingRange;
            halfAngle = swingHalfAngle;
            fillColor = fillTint;
            coreColor = coreTint;
            rimColor = rimTint;
            block = new MaterialPropertyBlock();
            EnsureTriangles();

            fan = new GameObject("Leque").transform;
            fan.SetParent(transform, false);

            fill = MakeLayer("Preenchimento", FxKit.FlatAlpha, thickness, 1f, 0f);
            core = MakeLayer("Miolo", FxKit.FlatAdditive, thickness * 0.4f, 0.97f, 0.02f);
            rim = MakeLayer("Borda", FxKit.FlatAdditive, 0.1f, 1.04f, 0.04f);

            var lightGo = new GameObject("Clarao");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, range * 0.5f);
            flash = lightGo.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = Color.Lerp(fillColor, Color.white, 0.2f);
            flash.range = range * 2.2f;
            flash.shadows = LightShadows.None;

            SpawnEdgeSparks();
            Rebuild(0);
            Apply(0f);
        }

        private Layer MakeLayer(string layerName, Material material, float thickness, float outer, float lift)
        {
            var child = new GameObject(layerName);
            child.transform.SetParent(fan, false);
            child.transform.localPosition = new Vector3(0f, lift, 0f);

            var mesh = new Mesh { name = "CrescenteGolpe", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = child.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return new Layer { Mesh = mesh, Renderer = r, Thickness = thickness, Outer = outer };
        }

        private void OnDestroy()
        {
            DestroyMesh(fill.Mesh);
            DestroyMesh(core.Mesh);
            DestroyMesh(rim.Mesh);
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh != null)
                Destroy(mesh);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= TotalDuration)
            {
                Destroy(gameObject);
                return;
            }

            int frame = Mathf.Min(SweepFrames, Mathf.FloorToInt(age / StepTime));
            if (frame != lastFrame)
                Rebuild(frame);
            Apply(age);
        }

        /// <summary>Refaz as malhas do quadro: o rastro corre pelo arco (frames 0 a 3) e depois o arco inteiro (frame 4).</summary>
        private void Rebuild(int frame)
        {
            lastFrame = frame;
            float head, tail;
            if (frame < SweepFrames)
            {
                head = Heads[frame];
                tail = Mathf.Max(0f, head - TailLength);
            }
            else
            {
                head = 1f;
                tail = 0f;
            }

            FillBand(fill, tail, head);
            FillBand(core, tail, head);
            FillBand(rim, tail, head);
        }

        private void FillBand(Layer layer, float from, float to)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            for (int i = 0; i <= Segments; i++)
            {
                float f = i / (float)Segments;
                float u = Mathf.Lerp(from, to, f);
                // O arco corre da direita (+meia-abertura) para a esquerda.
                float angle = Mathf.Lerp(halfAngle, -halfAngle, u) * Mathf.Deg2Rad;
                // Grosso no meio do arco e fino nas pontas; o rastro afina na cauda e engrossa na ponta.
                float profile = Mathf.Max(0.15f, Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)), 0.6f));
                float taper = Mathf.Lerp(0.25f, 1f, f);
                float width = Mathf.Min(0.95f, layer.Thickness * profile * taper);
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = dir * (layer.Outer * (1f - width));
                vertices[i * 2 + 1] = dir * layer.Outer;
            }

            layer.Mesh.Clear();
            layer.Mesh.vertices = vertices;
            layer.Mesh.triangles = bandTriangles;
            layer.Mesh.RecalculateBounds();
        }

        private static void EnsureTriangles()
        {
            if (bandTriangles != null)
                return;

            bandTriangles = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2;
                int t = i * 6;
                bandTriangles[t] = a;
                bandTriangles[t + 1] = a + 1;
                bandTriangles[t + 2] = a + 3;
                bandTriangles[t + 3] = a;
                bandTriangles[t + 4] = a + 3;
                bandTriangles[t + 5] = a + 2;
            }
        }

        private void Apply(float time)
        {
            float holdStart = SweepFrames * StepTime;
            float fade = time <= holdStart ? 1f : 1f - Mathf.Clamp01((time - holdStart) / HoldTime);
            float t = time / TotalDuration;

            // O leque abre rápido (ease-out) e cresce um pouco.
            float open = 1f - (1f - t) * (1f - t);
            float scale = range * Mathf.Lerp(0.88f, 1.06f, open);
            fan.localScale = new Vector3(scale, 1f, scale);

            FxKit.Paint(fill.Renderer, block, fillColor, 0.85f * fade);
            FxKit.Paint(core.Renderer, block, coreColor, fade);
            FxKit.Paint(rim.Renderer, block, rimColor, fade);
            if (flash != null)
                flash.intensity = 4f * fade * fade;
        }

        /// <summary>Faíscas grossas na borda externa do arco, mais numerosas perto de onde o rastro termina.</summary>
        private void SpawnEdgeSparks()
        {
            Vector3 origin = transform.position;
            Quaternion rotation = transform.rotation;

            GameObject root = FxKit.Root("Fx_GolpeFaiscas", origin, Quaternion.identity, 0.6f);
            var ps = FxKit.Emitter(root.transform, "Faiscas", origin, Quaternion.identity, FxKit.SolidParticle);
            FxKit.Configure(ps, 0.4f, 0.2f, 0.4f, 0f, 0f, 0.1f, 0.18f, Color.white, Color.white, 1.4f);
            FxKit.Shrink(ps);
            ps.Play();

            const int count = 9;
            for (int i = 0; i < count; i++)
            {
                // Mais densas no fim do arco (onde o golpe termina).
                float u = Mathf.Sqrt((i + Random.value * 0.8f) / count);
                float angle = Mathf.Lerp(halfAngle, -halfAngle, u) * Mathf.Deg2Rad;
                Vector3 radial = rotation * new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 tangent = rotation * new Vector3(-Mathf.Cos(angle), 0f, Mathf.Sin(angle)); // sentido do varrido
                Vector3 position = origin + radial * (range * 1.02f) + Vector3.up * Random.Range(0.1f, 0.5f);
                Vector3 velocity = radial * Random.Range(1.5f, 4f) + tangent * Random.Range(1f, 3f) + Vector3.up * Random.Range(0.5f, 2.5f);
                Color color = i % 3 == 0 ? rimColor : (i % 3 == 1 ? coreColor : fillColor);
                FxKit.Emit(ps, position, velocity, Random.Range(0.1f, 0.18f), color, Random.Range(0.2f, 0.38f));
            }
        }
    }
}
