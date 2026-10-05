using System.Collections;
using System.Linq;
using Game.Arena;
using Game.Arena.Life;
using Game.Cameras;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Cristal vivo no mundo (D-069): o construtor cria os veios nas máquinas, o pulso das torres e a poeira mágica,
    /// sem acender os portões antes da largada (D-013) e sem pôr a poeira na frente da praça na câmera de jogo.
    /// Os números vêm da cena e dos modelos (não fixos), para avisar se o ArenaBuilder ou o build_props.py mudarem.
    /// Depende da cena gerada por Game → Setup → Construir Arena.
    /// </summary>
    public class CrystalAmbienceTests
    {
        private const string ModelsFolder = "Assets/_Game/Art/Models/";
        // Folga para "rente": o veio encosta na superfície da peça com no máximo 2 cm de erro.
        private const float FlushTolerance = 0.02f;

        [UnitySetUp]
        public IEnumerator CarregaArena()
        {
            yield return ArenaTestScene.Load();
        }

        [UnityTearDown]
        public IEnumerator Limpa()
        {
            yield return ArenaTestScene.Cleanup();
        }

        private static CrystalPulse[] Pulses() => Object.FindObjectsByType<CrystalPulse>(FindObjectsSortMode.None);

        private static Transform Wall()
        {
            var wall = GameObject.Find("Limite");
            Assert.IsNotNull(wall, "Muro da arena");
            return wall.transform;
        }

        /// <summary>Raio do muro lido da cena (centro dos segmentos).</summary>
        private static float WallRadius()
        {
            var wall = Wall();
            float sum = 0f;
            foreach (Transform segment in wall)
                sum += new Vector2(segment.position.x, segment.position.z).magnitude;
            return sum / wall.childCount;
        }

        private static Transform[] Children(Transform parent, string name)
            => parent.Cast<Transform>().Where(t => t.name == name).ToArray();

        // ---------- Veios ----------

        [Test]
        public void Veios_CaldeirasEMuroTemVeioPorSegmento()
        {
            var wall = Wall();
            int segments = wall.childCount;
            int wallVeins = wall.GetComponentsInChildren<Transform>().Count(t => t.name == "VeioCristal");
            Assert.AreEqual(segments, wallVeins, "Um veio de cristal no cano de cada segmento do muro");

            var boilerPulse = Pulses().FirstOrDefault(p => p.name == "VeiosCaldeirasMuro");
            Assert.IsNotNull(boilerPulse, "Pulso dos veios das caldeiras e do muro");
            Assert.AreEqual(CrystalPulseRole.Veio, boilerPulse.Role);
            Assert.GreaterOrEqual(boilerPulse.RendererCount, segments + 2, "Muro e caldeiras no mesmo veio");
            Assert.AreEqual(0, boilerPulse.LightCount, "Veios são só emissão, sem luz nova");
        }

        [UnityTest]
        public IEnumerator Veios_PulsamEmJogo()
        {
            yield return null;
            yield return null;
            var vein = Wall().GetComponentsInChildren<Renderer>().First(r => r.name == "VeioCristal");
            Assert.IsTrue(vein.HasPropertyBlock(), "O veio do muro recebe o pulso de emissão");
        }

        /// <summary>
        /// MaterialPropertyBlock num renderer estático (combinado pelo static batching): a imagem tem de mudar de
        /// verdade entre dois instantes. Renderiza só a peça numa câmera de teste, com as luzes da cena apagadas
        /// (a única coisa que muda é a emissão do cristal), ao longo de mais de um pulso inteiro.
        /// </summary>
        [UnityTest]
        public IEnumerator Maquina_EmissaoVariaDeVerdadeEmRendererEstatico()
        {
            yield return null;
            var machine = Pulses().Where(p => p.Role == CrystalPulseRole.Maquina)
                .SelectMany(p => p.Renderers).Where(r => r != null).ToArray();
            Assert.IsNotEmpty(machine, "Fornalhas e postes pulsam");
            var target = machine.FirstOrDefault(r => r.isPartOfStaticBatch) ?? machine.FirstOrDefault(r => r.gameObject.isStatic);
            Assert.IsNotNull(target, "Há uma máquina estática pulsando (fornalha ou poste)");

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                light.enabled = false;

            int layer = Enumerable.Range(8, 24).Reverse().First(i => string.IsNullOrEmpty(LayerMask.LayerToName(i)));
            target.gameObject.layer = layer;

            var camGo = new GameObject("CameraTesteCristal");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            cam.fieldOfView = 30f;
            var b = target.bounds;
            var dir = new Vector3(b.center.x, 0f, b.center.z);
            dir = dir.sqrMagnitude > 0.01f ? -dir.normalized : Vector3.forward; // olha de dentro da praça
            float distance = b.extents.magnitude / Mathf.Tan(15f * Mathf.Deg2Rad) + 0.5f;
            camGo.transform.position = b.center + dir * distance;
            camGo.transform.LookAt(b.center);

            const int size = 96;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBHalf);
            var tex = new Texture2D(size, size, TextureFormat.RGBAHalf, false);
            cam.targetTexture = rt;
            float min = float.MaxValue, max = float.MinValue;
            // 45 amostras em 3,6 s: mais de um pulso inteiro das máquinas (0,3 pulso/s em CrystalAmbienceSettings).
            for (int k = 0; k < 45; k++)
            {
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply(false);
                RenderTexture.active = null;
                float sum = tex.GetPixels().Sum(c => c.r + c.g + c.b);
                min = Mathf.Min(min, sum);
                max = Mathf.Max(max, sum);
                yield return new WaitForSeconds(0.08f);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(tex);
            Object.Destroy(camGo);

            Assert.Greater(max, 0f, $"{target.name} aparece na câmera de teste");
            Assert.Greater(max - min, max * 0.02f,
                $"{target.name} (estático: {target.isPartOfStaticBatch}) deveria mudar de brilho com o pulso");
        }

        // ---------- Portões ----------

        [UnityTest]
        public IEnumerator Portoes_VeiosApagadosAteALargada()
        {
            yield return null;
            var gates = Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None);
            var gatePulses = Pulses().Where(p => p.WaitsForStart).ToArray();
            Assert.AreEqual(gates.Length, gatePulses.Length, "Um circuito de cristal por portão");
            foreach (var pulse in gatePulses)
                Assert.IsFalse(pulse.IsLit, "Circuito do portão apagado antes da largada (D-013)");

            int veins = 0;
            foreach (var gate in gates)
            {
                foreach (var r in gate.GetComponentsInChildren<Renderer>())
                {
                    if (r.name != "VeioCristal")
                        continue;
                    veins++;
                    StringAssert.StartsWith("CristalApagado", r.sharedMaterial.name);
                }
            }
            Assert.Greater(veins, 0, "Os portões têm veios");
        }

        // ---------- Geometria copiada do ArenaBuilder / build_props.py ----------

#if UNITY_EDITOR
        /// <summary>Caixa da parte do modelo com um material, no espaço da raiz do modelo (sem static batching).</summary>
        private static Bounds PartBounds(string model, string material)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + model + ".fbx");
            Assert.IsNotNull(asset, model);
            foreach (var mf in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                var mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
                for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
                {
                    if (mats[i] == null || mats[i].name != material)
                        continue;
                    var sub = mf.sharedMesh.GetSubMesh(i).bounds;
                    var result = new Bounds(asset.transform.InverseTransformPoint(mf.transform.TransformPoint(sub.center)), Vector3.zero);
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = sub.center + Vector3.Scale(sub.extents,
                            new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        result.Encapsulate(asset.transform.InverseTransformPoint(mf.transform.TransformPoint(corner)));
                    }
                    return result;
                }
            }
            Assert.Fail($"{model}.fbx sem a parte {material}");
            return default;
        }

        /// <summary>Faixa de espessura <paramref name="thickness"/> centrada em <paramref name="center"/> cruza a superfície em <paramref name="surface"/>.</summary>
        private static void AssertFlush(float center, float thickness, float surface, string what)
        {
            float back = center - thickness * 0.5f;
            float front = center + thickness * 0.5f;
            Assert.LessOrEqual(back, surface + 0.005f, $"{what}: o veio flutua fora da peça");
            Assert.GreaterOrEqual(back, surface - FlushTolerance, $"{what}: o veio afunda na peça");
            Assert.Greater(front, surface, $"{what}: o veio fica escondido dentro da peça");
        }
#endif

        [Test]
        public void Portoes_VeiosRentesACarcacaEAViga()
        {
#if UNITY_EDITOR
            var body = PartBounds("PortaoMaquina", "FerroEscuro"); // carcaça
            var copper = PartBounds("PortaoMaquina", "Cobre");     // viga do alto e colunas
            foreach (var gate in Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None))
            {
                var veins = Children(gate.transform, "VeioCristal");
                Assert.IsNotEmpty(veins, gate.name);
                foreach (var v in veins)
                {
                    var p = v.localPosition;
                    var s = v.localScale;
                    bool lintel = p.y > body.max.y;
                    var part = lintel ? copper : body;
                    AssertFlush(p.z, s.z, part.max.z, $"{gate.name} {(lintel ? "viga" : "carcaça")} y={p.y:0.00}");
                    Assert.LessOrEqual(Mathf.Abs(p.x) + s.x * 0.5f, part.max.x + 0.01f, $"{gate.name}: veio passa da lateral");
                    Assert.GreaterOrEqual(p.y - s.y * 0.5f, part.min.y - 0.01f, $"{gate.name}: veio passa de baixo");
                    Assert.LessOrEqual(p.y + s.y * 0.5f, part.max.y + 0.01f, $"{gate.name}: veio passa de cima");
                }
            }
#else
            Assert.Ignore("Lê os modelos pelo AssetDatabase (só no editor).");
#endif
        }

        [Test]
        public void Muro_VeioRenteAoCano()
        {
#if UNITY_EDITOR
            float radius = PartBounds("Cano", "Cobre").extents.y; // cano ao longo de X: raio = meia altura
            foreach (Transform segment in Wall())
            {
                var vein = Children(segment, "VeioCristal").Single();
                var pipe = segment.Cast<Transform>().First(t => t.name == "Cano" || t.name == "CanoCristal");
                var axis = new Vector2(pipe.localPosition.y, pipe.localPosition.z);
                var p = new Vector2(vein.localPosition.y, vein.localPosition.z) - axis;
                AssertFlush(p.magnitude, vein.localScale.y, radius, segment.name);
                Assert.Less(p.y, 0f, $"{segment.name}: o veio fica do lado da praça (−Z local)");
            }
#else
            Assert.Ignore("Lê os modelos pelo AssetDatabase (só no editor).");
#endif
        }

        [Test]
        public void Caldeiras_VeiosRentesAoCobre()
        {
#if UNITY_EDITOR
            var copper = PartBounds("Caldeira", "Cobre");
            float radius = copper.extents.x;
            var boilers = GameObject.Find("Caldeiras");
            Assert.IsNotNull(boilers, "Caldeiras da arena");
            foreach (Transform boiler in boilers.transform)
            {
                var veins = Children(boiler, "VeioCristal");
                Assert.IsNotEmpty(veins, boiler.name);
                foreach (var v in veins)
                {
                    var p = v.localPosition;
                    AssertFlush(new Vector2(p.x, p.z).magnitude, v.localScale.z, radius, $"{boiler.name} y={p.y:0.00}");
                    Assert.GreaterOrEqual(p.y - v.localScale.y * 0.5f, copper.min.y, "Veio abaixo do cobre");
                    Assert.LessOrEqual(p.y + v.localScale.y * 0.5f, copper.max.y, "Veio acima do cobre");
                }
            }
#else
            Assert.Ignore("Lê os modelos pelo AssetDatabase (só no editor).");
#endif
        }

        // ---------- Torres ----------

        [Test]
        public void Torres_CristalELuzPulsamJuntos()
        {
            var towers = Pulses().Where(p => p.Role == CrystalPulseRole.Torre).ToArray();
            Assert.Greater(towers.Length, 0, "Torre do relógio e pilões pulsam");
            Assert.IsTrue(towers.Any(t => t.LightCount > 0), "Ao menos uma torre pulsa a luz junto");
            foreach (var tower in towers)
            {
                Assert.AreEqual(0, tower.GetComponentsInChildren<EmissivePulse>(true).Length,
                    $"{tower.name}: EmissivePulse e CrystalPulse brigariam pelo mesmo cristal");
            }
        }

        // ---------- Poeira ----------

        /// <summary>
        /// Com a câmera de jogo real (CameraFollow e CameraSettings da cena) em cada ponto da praça, nenhuma partícula
        /// de poeira visível (além da distância em que some perto da câmera) pode cair sobre o chão da praça.
        /// Confere as partículas de verdade e onde elas estarão quando subirem até o fim da vida.
        /// </summary>
        [UnityTest]
        public IEnumerator PoeiraMagica_NuncaFicaNaFrenteDaPraca()
        {
            yield return null;
            var dust = GameObject.Find("PoeiraMagica");
            Assert.IsNotNull(dust, "Poeira mágica");
            var ps = dust.GetComponent<ParticleSystem>();
            Assert.LessOrEqual(ps.main.maxParticles, 400, "Poeira barata");
            var particles = new ParticleSystem.Particle[ps.main.maxParticles];
            int count = ps.GetParticles(particles);
            Assert.Greater(count, 0, "A poeira já existe ao carregar (prewarm)");

            var cam = Camera.main;
            var follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "Câmera de jogo com CameraFollow");
            Assert.IsNotNull(follow.Settings, "Câmera de jogo configurada");
            float rise = ps.velocityOverLifetime.y.constantMax * ps.main.startLifetime.constantMax;
            var mat = dust.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            float fadeNear = mat != null && mat.HasProperty("_CameraNearFadeDistance") ? mat.GetFloat("_CameraNearFadeDistance") : 0f;
            float arena = WallRadius() - 0.5f;
            float savedAspect = cam.aspect;
            cam.aspect = 16f / 9f;

            int bad = 0;
            string example = null;
            try
            {
                for (float r = 0f; r <= arena - 0.5f; r += 1.25f)
                {
                    for (float a = 0f; a < 360f; a += 5f)
                    {
                        var player = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * r);
                        follow.Apply(player);
                        var c = cam.transform.position;
                        for (int i = 0; i < count; i++)
                        {
                            foreach (float up in new[] { 0f, rise })
                            {
                                var p = particles[i].position + Vector3.up * up;
                                var vp = cam.WorldToViewportPoint(p);
                                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                                    continue;
                                if ((p - c).magnitude <= fadeNear || p.y >= c.y)
                                    continue;
                                var ground = c + (p - c) * (c.y / (c.y - p.y));
                                if (new Vector2(ground.x, ground.z).magnitude < arena)
                                {
                                    bad++;
                                    example ??= $"jogador em {player}, partícula em {p}";
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                cam.aspect = savedAspect;
            }
            Assert.AreEqual(0, bad, $"Poeira na frente da praça ({example})");
        }
    }
}
