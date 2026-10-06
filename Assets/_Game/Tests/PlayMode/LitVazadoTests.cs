using System.Collections;
using Game.Arena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Shader Game/LitVazado (D-076, D-079): compila, tem as passadas, os materiais da cidade o usam, sem alvos fica
    /// igual ao Lit do URP e, com alvo, o recorte abre o buraco no lugar certo (inclusive a orientação do y da tela).
    /// Depende de Game → Setup → Construir Arena (paleta e renderer do URP). Os testes de imagem renderizam numa textura.
    /// </summary>
    public class LitVazadoTests
    {
        private const string ShaderName = "Game/LitVazado";
        private const string LitName = "Universal Render Pipeline/Lit";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials/";
        private const int Size = 128;

        // Igual a CityBuilder.PaletteNames: os materiais que as peças da cidade usam.
        private static readonly string[] CityMaterials =
        {
            "FerroEscuro", "FerroMedio", "Cobre", "CobreOxidado", "Latao", "LataoEscuro", "Pedra", "PedraEscura",
            "Tijolo", "Madeira", "Telhado", "Tecido", "TecidoEscuro", "Couro", "Bandeira", "JanelaQuente",
            "CristalArcano", "BrasaFornalha"
        };

        private readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void Limpa()
        {
            ResetGlobals();
            foreach (var o in created)
                if (o != null)
                    Object.DestroyImmediate(o);
            created.Clear();
        }

        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat("_VazadoContagem", 0f);
            Shader.SetGlobalFloat("_VazadoPerto", 0f);
            Shader.SetGlobalFloat("_VazadoMargem", 0.8f);
            Shader.SetGlobalFloat("_VazadoFantasma", 0.25f);
        }

        // ---------- Shader e materiais ----------

        [Test]
        public void Shader_CompilaETemAsPassadas()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, $"{ShaderName} não encontrado (rodar Game → Setup → Construir Arena)");
            Assert.IsTrue(shader.isSupported, "Shader suportado");
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader), "Shader sem erro de compilação");
#endif
            var material = new Material(shader);
            created.Add(material);
            foreach (string pass in new[] { "ForwardLit", "DepthOnly", "DepthNormals", "ShadowCaster", "VazadoFantasma" })
                Assert.GreaterOrEqual(material.FindPass(pass), 0, $"Passada {pass}");
        }

        [Test]
        public void MateriaisDaCidade_UsamOShaderVazado()
        {
#if UNITY_EDITOR
            foreach (string name in CityMaterials)
            {
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}{name}.mat");
                Assert.IsNotNull(mat, $"Material {name}");
                Assert.AreEqual(ShaderName, mat.shader.name, $"{name} usa o shader vazado");
                Assert.GreaterOrEqual(mat.FindPass("DepthNormals"), 0, $"{name} tem a passada DepthNormals");
            }
#else
            Assert.Ignore("Só no editor.");
#endif
        }

        [UnityTest]
        public IEnumerator PecasMarcadasComoVazaveis_UsamOShaderVazado()
        {
            yield return ArenaTestScene.Load();
            int marked = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!VazadoLayer.IsMarked(renderer))
                    continue;
                marked++;
                foreach (var mat in renderer.sharedMaterials)
                    Assert.AreEqual(ShaderName, mat.shader.name, $"{renderer.name}: material {mat.name} de peça vazável");
            }
            yield return ArenaTestScene.Cleanup();
            if (marked == 0)
                Assert.Inconclusive("Nenhum renderer marcado ainda (o CityBuilder precisa chamar VazadoLayer.Mark).");
        }

        [Test]
        public void VazadoLayer_SomaOBitSemPerderAsOutras()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            created.Add(go);
            var renderer = go.GetComponent<MeshRenderer>();
            uint before = renderer.renderingLayerMask;
            VazadoLayer.Mark(renderer);
            Assert.IsTrue(VazadoLayer.IsMarked(renderer));
            Assert.AreEqual(before, renderer.renderingLayerMask & before, "Bits antigos continuam");
            VazadoLayer.Unmark(renderer);
            Assert.IsFalse(VazadoLayer.IsMarked(renderer));
            Assert.AreEqual(before, renderer.renderingLayerMask);
        }

        // ---------- Imagem ----------

        private class Rig
        {
            public Camera Camera;
            public MeshRenderer Quad;
            public RenderTexture Target;
            public Texture2D Readback;
        }

        // Origem do cenário de teste: bem fora da Arena e do palco do dado (y = -500).
        private static readonly Vector3 RigOrigin = new Vector3(0f, -2000f, 0f);

        private Rig BuildRig()
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            var rig = new Rig();

            var lightGo = new GameObject("TesteVazadoLuz");
            created.Add(lightGo);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightGo.AddComponent<Light>().type = LightType.Directional;

            var camGo = new GameObject("TesteVazadoCamera");
            created.Add(camGo);
            rig.Camera = camGo.AddComponent<Camera>();
            rig.Camera.clearFlags = CameraClearFlags.SolidColor;
            rig.Camera.backgroundColor = new Color(0f, 1f, 0f, 1f);
            rig.Camera.fieldOfView = 60f;
            rig.Camera.nearClipPlane = 0.3f;
            rig.Camera.farClipPlane = 50f;
            // Longe da Arena (que pode estar carregada): senão o que está atrás do buraco aparece no lugar do fundo verde.
            camGo.transform.SetPositionAndRotation(RigOrigin, Quaternion.identity);
            rig.Target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            created.Add(rig.Target);
            rig.Camera.targetTexture = rig.Target;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            created.Add(quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.position = RigOrigin + new Vector3(0f, 0f, 5f);
            quad.transform.localScale = new Vector3(20f, 20f, 1f); // cobre a tela inteira
            rig.Quad = quad.GetComponent<MeshRenderer>();

            rig.Readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            created.Add(rig.Readback);
            return rig;
        }

        private static Material NewMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, shaderName);
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0f, 0f, 1f));
            return material;
        }

        /// <summary>Renderiza e devolve o pixel (x, y) com y contado de baixo para cima (como o viewport da câmera).</summary>
        private static Color Shoot(Rig rig, int x, int y)
        {
            rig.Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rig.Target;
            rig.Readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            rig.Readback.Apply();
            RenderTexture.active = previous;
            return rig.Readback.GetPixel(x, y);
        }

        private static void SetTarget(int index, float u, float v, float radius, float depth)
        {
            var targets = new Vector4[12];
            targets[index] = new Vector4(u, v, radius, depth);
            Shader.SetGlobalVectorArray("_VazadoAlvos", targets);
        }

        [UnityTest]
        public IEnumerator SemAlvos_RenderizaIgualAoLitDoURP()
        {
            var rig = BuildRig();
            ResetGlobals();
            yield return null;

            var lit = NewMaterial(LitName);
            var vazado = NewMaterial(ShaderName);
            created.Add(lit);
            created.Add(vazado);

            // Mesmo com a peça marcada como vazável, sem alvos nada é cortado.
            VazadoLayer.Mark(rig.Quad);

            foreach (var (x, y) in new[] { (Size / 2, Size / 2), (10, 10), (Size - 10, Size - 10), (Size / 2, (int)(Size * 0.75f)) })
            {
                rig.Quad.sharedMaterial = lit;
                Color a = Shoot(rig, x, y);
                rig.Quad.sharedMaterial = vazado;
                Color b = Shoot(rig, x, y);
                Assert.Less(Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b), 0.02f,
                    $"Pixel ({x},{y}) igual ao Lit: {a} x {b}");
                Assert.Greater(b.r, b.g, "O quad vermelho está na imagem (não é o fundo verde)");
            }
        }

        [UnityTest]
        public IEnumerator ComAlvo_AbreBuracoSoOndeDeve_ERespeitaALayer()
        {
            var rig = BuildRig();
            ResetGlobals();
            yield return null;

            rig.Quad.sharedMaterial = NewMaterial(ShaderName);
            created.Add(rig.Quad.sharedMaterial);
            Shader.SetGlobalFloat("_VazadoContagem", 1f);
            Shader.SetGlobalFloat("_VazadoFantasma", 0f); // aqui só vale o recorte; o fantasma sobre o buraco é conferido em captura
            // Buraco em cima do centro (v = 0,75, y para cima), raio 0,12 da altura, alvo bem atrás do quad (5 m).
            SetTarget(0, 0.5f, 0.75f, 0.12f, 20f);

            int holeX = Size / 2, holeY = (int)(Size * 0.75f);
            int solidX = Size / 2, solidY = (int)(Size * 0.25f); // o espelho vertical (testa o y invertido)

            // Sem a rendering layer, o quad não é vazável: nada é cortado.
            VazadoLayer.Unmark(rig.Quad);
            Color unmarked = Shoot(rig, holeX, holeY);
            Assert.Greater(unmarked.r, unmarked.g, "Sem a layer 8 o pixel continua do quad");

            // Marcado: o pixel do buraco mostra o fundo verde; o do lado oposto continua do quad.
            VazadoLayer.Mark(rig.Quad);
            Color hole = Shoot(rig, holeX, holeY);
            Color solid = Shoot(rig, solidX, solidY);
            Assert.Greater(hole.g, hole.r, $"Pixel dentro do buraco está cortado (fundo verde): {hole}");
            Assert.Greater(solid.r, solid.g, $"Pixel fora do buraco continua do quad: {solid}");

            // Alvo mais perto que o quad (menos a margem): o quad está atrás dele e não é cortado.
            SetTarget(0, 0.5f, 0.75f, 0.12f, 3f);
            Color behind = Shoot(rig, holeX, holeY);
            Assert.Greater(behind.r, behind.g, "Quad atrás do alvo não é cortado");
        }

        [UnityTest]
        public IEnumerator Perto_CortaTudoSoEmPecaVazavel()
        {
            var rig = BuildRig();
            ResetGlobals();
            yield return null;

            rig.Quad.sharedMaterial = NewMaterial(ShaderName);
            created.Add(rig.Quad.sharedMaterial);
            Shader.SetGlobalFloat("_VazadoPerto", 8f); // o quad está a 5 m

            VazadoLayer.Unmark(rig.Quad);
            Color normal = Shoot(rig, Size / 2, Size / 2);
            Assert.Greater(normal.r, normal.g, "Peça não vazável não é cortada");

            VazadoLayer.Mark(rig.Quad);
            Color cut = Shoot(rig, Size / 2, Size / 2);
            Assert.Greater(cut.g, cut.r, "Peça vazável mais perto que _VazadoPerto é cortada");
        }
    }
}
