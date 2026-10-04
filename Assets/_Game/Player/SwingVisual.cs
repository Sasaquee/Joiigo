using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Player
{
    /// <summary>
    /// Visual do golpe em arco (D-020): um leque achatado de latão quente com borda ciano fina
    /// (metal e arcano, Pilar 4) que some em ~0,15 s, mais um clarão de luz. Malha e material
    /// são criados uma vez em runtime. Só desenho: o acerto é decidido pelo host.
    /// </summary>
    public class SwingVisual : MonoBehaviour
    {
        private const float Duration = 0.15f;
        private const float Lift = 0.08f;          // acima do chão, para não brigar com o piso
        private const float EdgeWidth = 0.08f;     // largura da borda, em fração do alcance
        private const int Segments = 16;

        private static readonly Color FillColor = new Color(1f, 0.62f, 0.25f, 1f);
        private static readonly Color EdgeColor = new Color(0.45f, 0.95f, 1f, 1f);
        private static readonly Color LightColor = new Color(1f, 0.68f, 0.35f, 1f);

        private static Mesh fillMesh;
        private static Mesh edgeMesh;
        private static float meshHalfAngle = -1f;
        private static Material material;

        private MeshRenderer fillRenderer;
        private MeshRenderer edgeRenderer;
        private Light flash;
        private Transform fan;
        private MaterialPropertyBlock block;
        private float age;
        private float range;

        /// <summary>Cria um golpe na posição do jogador, apontando para dir (XZ, normalizado).</summary>
        public static void Play(Vector3 position, Vector3 dir, float range, float halfAngleDegrees)
        {
            if (dir.sqrMagnitude < 0.0001f)
                return;
            EnsureAssets(halfAngleDegrees);
            if (material == null)
                return;

            var go = new GameObject("Golpe");
            go.transform.SetPositionAndRotation(position + Vector3.up * Lift, Quaternion.LookRotation(dir, Vector3.up));
            var visual = go.AddComponent<SwingVisual>();
            visual.Build(range);
        }

        private void Build(float swingRange)
        {
            range = swingRange;
            fan = new GameObject("Leque").transform;
            fan.SetParent(transform, false);
            fan.localScale = new Vector3(range, 1f, range);

            fillRenderer = AddRenderer("Preenchimento", fillMesh);
            edgeRenderer = AddRenderer("Borda", edgeMesh);
            block = new MaterialPropertyBlock();

            var lightGo = new GameObject("Clarao");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, range * 0.5f);
            flash = lightGo.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = LightColor;
            flash.range = range * 2f;
            flash.shadows = LightShadows.None;

            Apply(0f);
        }

        private MeshRenderer AddRenderer(string childName, Mesh mesh)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(fan, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = child.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Duration)
            {
                Destroy(gameObject);
                return;
            }
            Apply(age / Duration);
        }

        private void Apply(float t)
        {
            float fade = 1f - t;
            // O leque abre rápido (ease-out) e some; a borda dura um pouco mais.
            float open = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(0.7f, 1f, open);
            fan.localScale = new Vector3(range * scale, 1f, range * scale);

            Paint(fillRenderer, FillColor, 0.35f * fade * fade);
            Paint(edgeRenderer, EdgeColor, 0.9f * fade);
            flash.intensity = 3f * fade * fade;
        }

        private void Paint(Renderer r, Color color, float alpha)
        {
            color.a = alpha;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color); // URP Unlit
            block.SetColor("_Color", color);     // Sprites/Default (reserva)
            r.SetPropertyBlock(block);
        }

        // ---------- Recursos compartilhados ----------

        private static void EnsureAssets(float halfAngleDegrees)
        {
            if (material == null)
                material = CreateMaterial();
            if (fillMesh == null || edgeMesh == null || !Mathf.Approximately(meshHalfAngle, halfAngleDegrees))
            {
                if (fillMesh != null) Destroy(fillMesh);
                if (edgeMesh != null) Destroy(edgeMesh);
                fillMesh = BuildFan(halfAngleDegrees);
                edgeMesh = BuildEdge(halfAngleDegrees);
                meshHalfAngle = halfAngleDegrees;
            }
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            bool urp = shader != null;
            if (!urp)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var m = new Material(shader) { name = "Golpe (runtime)", hideFlags = HideFlags.HideAndDontSave };
            if (urp)
            {
                m.SetFloat("_Surface", 1f);   // transparente
                m.SetFloat("_Blend", 0f);     // alpha
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Leque de raio 1 apontando para +Z, de -meia-abertura a +meia-abertura (sentido horário visto de cima).</summary>
        private static Mesh BuildFan(float halfAngleDegrees)
        {
            var vertices = new Vector3[Segments + 2];
            var triangles = new int[Segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= Segments; i++)
                vertices[i + 1] = ArcPoint(halfAngleDegrees, i, 1f);
            for (int i = 0; i < Segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            return MakeMesh("LequeGolpe", vertices, triangles);
        }

        /// <summary>Faixa fina na borda externa do arco.</summary>
        private static Mesh BuildEdge(float halfAngleDegrees)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                vertices[i * 2] = ArcPoint(halfAngleDegrees, i, 1f - EdgeWidth);
                vertices[i * 2 + 1] = ArcPoint(halfAngleDegrees, i, 1f);
            }
            for (int i = 0; i < Segments; i++)
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
            return MakeMesh("BordaGolpe", vertices, triangles);
        }

        private static Vector3 ArcPoint(float halfAngleDegrees, int index, float radius)
        {
            float angle = Mathf.Lerp(-halfAngleDegrees, halfAngleDegrees, index / (float)Segments) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        private static Mesh MakeMesh(string meshName, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = meshName, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
