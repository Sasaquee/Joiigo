using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Arena;
using Game.Arena.Life;
using Game.Cameras;
using Game.Core.Map;
using Game.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Cristal vivo no mundo (D-069) no mapa novo (D-073 a D-082): veios nas máquinas, trilhos de cristal do núcleo
    /// até os portões (apagados antes da largada, D-013 e D-081), lampiões nas avenidas, pulso das torres e poeira
    /// mágica nas avenidas e praças menores, sem pôr a poeira na frente do combate na câmera de jogo, e cristal visível
    /// da câmera real em cada foco do mapa.
    /// Os números vêm da cena, dos modelos e dos assets (não fixos), para avisar se o ArenaBuilder, o CityBuilder ou o
    /// build_props.py mudarem. Depende da cena gerada por Game → Setup → Construir Arena.
    /// </summary>
    public class CrystalAmbienceTests
    {
        private const string ModelsFolder = "Assets/_Game/Art/Models/";
        private const string MapLayoutPath = "Assets/_Game/Data/Map/MapLayoutSettings.asset";
        private const string CrystalSettingsPath = "Assets/_Game/Data/Ambience/CrystalAmbienceSettings.asset";
        // Folga para "rente": o veio encosta na superfície da peça com no máximo 2 cm de erro.
        private const float FlushTolerance = 0.02f;
        // O PortaoMaquina.fbx tem a frente a 0,8 m do centro (AmbienceBuilder.GateFront).
        private const float GateFront = 0.8f;
        private const float LampMinFacadeGap = 0.6f;

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

        private static CrystalPulse[] TrailPulses()
            => Pulses().Where(p => p.name.StartsWith("VeiosTrilho")).OrderBy(p => p.name).ToArray();

        private static Transform[] Children(Transform parent, string name)
            => parent.Cast<Transform>().Where(t => t.name == name).ToArray();

        /// <summary>Geometria do mapa lida do asset (a mesma que o construtor usou); sem o asset, o padrão do plano.</summary>
        private static MapLayout Layout()
        {
#if UNITY_EDITOR
            var settings = AssetDatabase.LoadAssetAtPath<MapLayoutSettings>(MapLayoutPath);
            if (settings != null)
                return settings.ToLayout();
#endif
            return new MapLayout();
        }

        /// <summary>Números do cristal lidos do asset; sem ele, os padrões do código.</summary>
        private static CrystalAmbienceSettings Settings()
        {
#if UNITY_EDITOR
            var settings = AssetDatabase.LoadAssetAtPath<CrystalAmbienceSettings>(CrystalSettingsPath);
            if (settings != null)
                return settings;
#endif
            return ScriptableObject.CreateInstance<CrystalAmbienceSettings>();
        }

        private static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        // ---------- Veios ----------

        [Test]
        public void Veios_CaldeirasTemVeiosPulsando()
        {
            var boilers = GameObject.Find("Caldeiras");
            Assert.IsNotNull(boilers, "Caldeiras da arena");
            int boilerCount = boilers.transform.childCount;
            Assert.Greater(boilerCount, 0);

            var pulse = Pulses().FirstOrDefault(p => p.name == "VeiosCaldeiras");
            Assert.IsNotNull(pulse, "Pulso dos veios das caldeiras");
            Assert.AreEqual(CrystalPulseRole.Veio, pulse.Role);
            // Por caldeira: o cobre do modelo e 4 veios de 4 trechos.
            Assert.GreaterOrEqual(pulse.RendererCount, boilerCount * (1 + 4 * 4), "Veios de cada caldeira");
            Assert.AreEqual(0, pulse.LightCount, "Veios são só emissão, sem luz nova");
        }

        [Test]
        public void Muro_NaoTemMaisVeios()
        {
            // O muro baixo saiu no passe do mapa (D-073): nem veio de muro nem o pulso que o ligava às caldeiras.
            Assert.IsFalse(Pulses().Any(p => p.name == "VeiosCaldeirasMuro"), "Pulso do muro sumiu");
            var wall = GameObject.Find("Limite");
            if (wall != null)
                Assert.AreEqual(0, wall.GetComponentsInChildren<CrystalPulse>(true).Length, "Nada de cristal no muro");
        }

        [UnityTest]
        public IEnumerator Veios_PulsamEmJogo()
        {
            yield return null;
            yield return null;
            var boilers = GameObject.Find("Caldeiras");
            Assert.IsNotNull(boilers, "Caldeiras da arena");
            var vein = boilers.GetComponentsInChildren<Renderer>().First(r => r.name == "VeioCristal");
            Assert.IsTrue(vein.HasPropertyBlock(), "O veio da caldeira recebe o pulso de emissão");
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
            // Fornalhas e postes (os lampiões têm só uma gema pequena: a diferença some no resto da peça).
            var machine = Pulses().Where(p => p.Role == CrystalPulseRole.Maquina && !p.name.StartsWith("Lampiao"))
                .SelectMany(p => p.Renderers).Where(r => r != null).ToArray();
            Assert.IsNotEmpty(machine, "Fornalhas e postes pulsam");
            var target = machine.FirstOrDefault(r => r.isPartOfStaticBatch) ?? machine.FirstOrDefault(r => r.gameObject.isStatic);
            Assert.IsNotNull(target, "Há uma máquina estática pulsando (fornalha ou poste)");

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                light.enabled = false;

            int layer = Enumerable.Range(9, 23).Reverse().First(i => string.IsNullOrEmpty(LayerMask.LayerToName(i)));
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

        /// <summary>Um circuito de cristal por portão, achado pelo nome do portão (VeiosPortaoMaquinaN).</summary>
        private static CrystalPulse[] GatePulses()
            => Pulses().Where(p => p.name.StartsWith("VeiosPortao")).ToArray();

        [UnityTest]
        public IEnumerator Portoes_VeiosApagadosAteALargada()
        {
            yield return null;
            var gates = Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None);
            var gatePulses = GatePulses();
            Assert.AreEqual(Layout().StreetCount, gates.Length, "Um portão por rua");
            Assert.AreEqual(gates.Length, gatePulses.Length, "Um circuito de cristal por portão");
            foreach (var pulse in gatePulses)
            {
                Assert.IsTrue(pulse.WaitsForStart, $"{pulse.name} espera a largada");
                Assert.IsFalse(pulse.IsLit, "Circuito do portão apagado antes da largada (D-013)");
            }

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

        [Test]
        public void Portoes_FicamNoFimDeCadaBoca()
        {
            var layout = Layout();
            var gates = Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None);
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var expected = new Vector2(layout.Gates[i].Position.X, layout.Gates[i].Position.Y);
                var nearest = gates.OrderBy(g => Vector2.Distance(Flat(g.transform.position), expected)).First();
                Assert.Less(Vector2.Distance(Flat(nearest.transform.position), expected), 1f,
                    $"Portão da rua {i + 1} no fim da boca (o AmbienceBuilder acha os portões pelo GateActivation)");
            }
        }

        // ---------- Trilhos de cristal ----------

        /// <summary>Quanto o ponto avança a partir do centro ao longo do eixo (vetor unitário no plano XZ: x = X, y = Z).</summary>
        private static float Along(Vector3 p, Vector2 axis) => p.x * axis.x + p.z * axis.y;

        private static IEnumerable<Renderer> TrailVeins(CrystalPulse pulse) => pulse.Renderers.Where(r => r != null);

        [Test]
        public void Trilhos_UmPorRua_DoNucleoAteAFrenteDoPortao()
        {
            var layout = Layout();
            var settings = Settings();
            var trails = TrailPulses();
            Assert.AreEqual(layout.StreetCount, trails.Length, "Um trilho de cristal por rua");
            var gates = Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None);

            for (int i = 0; i < layout.StreetCount; i++)
            {
                var pulse = trails[i];
                var axis2 = layout.Avenues[i].Axis;
                var axis = new Vector2(axis2.X, axis2.Y);
                var side = new Vector2(axis2.Y, -axis2.X);
                Assert.AreEqual(CrystalPulseRole.Veio, pulse.Role, pulse.name);
                Assert.AreEqual(0, pulse.LightCount, "Trilho é só emissão");

                var veins = TrailVeins(pulse).OrderBy(r => Along(r.transform.position, axis)).ToArray();
                Assert.GreaterOrEqual(veins.Length, 8, $"{pulse.name}: trechos de veio");
                float start = float.MaxValue, end = float.MinValue, previousEnd = float.NaN;
                foreach (var v in veins)
                {
                    float center = Along(v.transform.position, axis);
                    float half = v.transform.localScale.z * 0.5f;
                    start = Mathf.Min(start, center - half);
                    end = Mathf.Max(end, center + half);
                    Assert.AreEqual(0f, Along(v.transform.position, side), 0.02f, $"{pulse.name}: veio fora do eixo da rua");
                    Assert.AreEqual(settings.trailVeinWidth, v.transform.localScale.x, 0.001f, "Largura do veio");
                    if (!float.IsNaN(previousEnd))
                        Assert.LessOrEqual(center - half - previousEnd, 0.1f, $"{pulse.name}: buraco no meio do trilho");
                    previousEnd = center + half;
                }
                Assert.AreEqual(settings.trailStartS, start, 0.1f, $"{pulse.name}: começa logo depois do núcleo");

                // O trilho termina na frente do portão da mesma rua.
                var gatePos = new Vector2(layout.Gates[i].Position.X, layout.Gates[i].Position.Y);
                var gate = gates.OrderBy(g => Vector2.Distance(Flat(g.transform.position), gatePos)).First();
                var front = gate.transform.position + gate.transform.forward * GateFront;
                Assert.AreEqual(Along(front, axis), end, 0.3f, $"{pulse.name}: termina na frente do portão");
            }
        }

        [Test]
        public void Trilhos_FicamRentesAoChao()
        {
            Physics.SyncTransforms();
            var layout = Layout();
            var trails = TrailPulses();
            Assert.AreEqual(layout.StreetCount, trails.Length);
            int checkedVeins = 0;
            foreach (var pulse in trails)
            {
                foreach (var v in TrailVeins(pulse))
                {
                    var p = v.transform.position;
                    if (Flat(p).magnitude < 14f)
                        continue; // dentro da plataforma de combate o chão sobe 6 cm
                    Assert.IsTrue(Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0,
                        QueryTriggerInteraction.Ignore), $"{pulse.name} em {p}: sem chão (collider) debaixo do trilho");
                    float baseHeight = v.bounds.min.y - hit.point.y; // altura da base de cobre
                    Assert.That(baseHeight, Is.InRange(0.06f, 0.12f), $"{pulse.name} em {p}: trilho flutua ou afunda no chão (topo do chão em y {hit.point.y:0.000})");
                    checkedVeins++;
                }
            }
            Assert.Greater(checkedVeins, 20, "Trechos conferidos fora da plataforma");
        }

        [UnityTest]
        public IEnumerator Trilhos_ApagadosAntesDaLargada()
        {
            yield return null;
            yield return null;
            var trails = TrailPulses();
            Assert.AreEqual(Layout().StreetCount, trails.Length);
            foreach (var pulse in trails)
            {
                Assert.IsTrue(pulse.WaitsForStart, $"{pulse.name} espera a largada");
                Assert.IsFalse(pulse.IsLit, $"{pulse.name}: apagado antes da largada (D-013, D-081)");
                foreach (var v in TrailVeins(pulse))
                    StringAssert.StartsWith("CristalApagado", v.sharedMaterial.name, $"{pulse.name}: veio apagado");
            }
        }

        [UnityTest]
        public IEnumerator Largada_AcendeTrilhosEPortoesJuntos()
        {
            yield return null;
            var session = Object.FindFirstObjectByType<NetSession>();
            Assert.IsTrue(session.Host(), "Host iniciou");
            var match = Object.FindFirstObjectByType<MatchState>();
            float timeout = 5f;
            while (!match.IsServer && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            match.ServerStart();
            yield return null;
            yield return null;
            Assert.IsTrue(match.IsStarted, "Largada dada pelo host");

            var waiting = Pulses().Where(p => p.WaitsForStart).ToArray();
            Assert.AreEqual(Layout().StreetCount * 2, waiting.Length, "Um trilho e um circuito de portão por rua");
            foreach (var pulse in waiting)
            {
                Assert.IsTrue(pulse.IsLit, $"{pulse.name}: aceso depois da largada");
                foreach (var v in pulse.Renderers.Where(r => r != null))
                    Assert.AreEqual("CristalArcano", v.sharedMaterial.name, $"{pulse.name}: veio aceso");
            }
        }

        // ---------- Lampiões ----------

        private static Transform[] Lamps()
        {
            var group = GameObject.Find("LampioesCristal");
            Assert.IsNotNull(group, "Lampiões de cristal das avenidas");
            return group.transform.Cast<Transform>().ToArray();
        }

        [Test]
        public void Lampioes_TemColisaoPequenaEmCenarioEPulsam()
        {
            var lamps = Lamps();
            Assert.GreaterOrEqual(lamps.Length, Layout().StreetCount * 2, "Ao menos dois lampiões por avenida");
            foreach (var lamp in lamps)
            {
                Assert.AreEqual(MapLayers.Cenario, lamp.gameObject.layer, $"{lamp.name}: camada Cenario");
                var capsule = lamp.GetComponent<CapsuleCollider>();
                Assert.IsNotNull(capsule, $"{lamp.name}: CapsuleCollider");
                Assert.IsFalse(capsule.isTrigger, $"{lamp.name}: bloqueia de verdade");
                Assert.LessOrEqual(capsule.radius, 0.5f, $"{lamp.name}: colisão pequena");
                var pulse = lamp.GetComponent<CrystalPulse>();
                Assert.IsNotNull(pulse, $"{lamp.name}: o cristal respira");
                Assert.Greater(pulse.RendererCount, 0, $"{lamp.name}: peças do modelo");
                Assert.IsTrue(pulse.Renderers.Any(r => r.sharedMaterials.Any(m => m != null && m.name == "CristalArcano")),
                    $"{lamp.name}: a gema usa o material CristalArcano (senão o pulso não acha o cristal)");
            }
        }

        [Test]
        public void Lampioes_FicamNaBordaDaAvenida_ECorredorFicaLivre()
        {
            var layout = Layout();
            var lamps = Lamps();
            var inwardEdge = new float[layout.StreetCount];
            for (int i = 0; i < inwardEdge.Length; i++)
                inwardEdge[i] = float.MaxValue;

            foreach (var lamp in lamps)
            {
                var p = lamp.position;
                int street = -1;
                float across = 0f;
                for (int i = 0; i < layout.StreetCount; i++)
                {
                    if (!layout.Avenues[i].Rect.Contains(p.x, p.z))
                        continue;
                    layout.Avenues[i].Rect.ToLocal(p.x, p.z, out _, out across);
                    street = i;
                }
                Assert.GreaterOrEqual(street, 0, $"{lamp.name} em {p}: fora de toda avenida");

                float radius = lamp.GetComponent<CapsuleCollider>().radius;
                float facadeGap = layout.Avenues[street].Width * 0.5f - (Mathf.Abs(across) + radius);
                Assert.GreaterOrEqual(facadeGap, LampMinFacadeGap - 0.001f, $"{lamp.name}: a menos de {LampMinFacadeGap} m da fachada");
                Assert.GreaterOrEqual(Flat(p).magnitude, layout.PlazaFacadeRadius, $"{lamp.name}: dentro da praça");
                inwardEdge[street] = Mathf.Min(inwardEdge[street], Mathf.Abs(across) - radius);
            }

            for (int i = 0; i < inwardEdge.Length; i++)
            {
                // Largura livre entre os pés dos lampiões dos dois lados: sobra um corredor de pelo menos 6 m.
                Assert.GreaterOrEqual(inwardEdge[i] * 2f, 6f, $"Avenida {i + 1}: corredor andável entre os lampiões");
            }
        }

        [Test]
        public void Lampioes_LuzesFracasSemSombraEPoucas()
        {
            var lights = Lamps().SelectMany(l => l.GetComponentsInChildren<Light>(true)).ToArray();
            Assert.LessOrEqual(lights.Length, 6, "No máximo ~6 luzes novas (o CityBuilder já usa ~34)");
            Assert.Greater(lights.Length, 0, "Ao menos um lampião acende uma luz");
            foreach (var light in lights)
            {
                Assert.AreEqual(LightType.Point, light.type);
                Assert.AreEqual(LightShadows.None, light.shadows, "Sem sombra");
                Assert.LessOrEqual(light.intensity, 5f, "Mais fraca que a dos lampiões da cidade (5)");
                Assert.LessOrEqual(light.range, 10f);
            }
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

        // ---------- Cristal visível da câmera real ----------

        /// <summary>
        /// Em cada foco do mapa (spawn dos jogadores, centro da praça, meio de cada avenida, cada praça menor) a câmera
        /// de jogo real (CameraFollow e CameraSettings da cena, em 16:9 e 21:9) vê ao menos dois renderers de
        /// CrystalPulse dentro do frustum. O foco é onde o jogador estaria: a câmera segue o jogador.
        /// </summary>
        [UnityTest]
        public IEnumerator Cristal_AparecenaCameraReal_EmCadaFoco()
        {
            yield return null;
            var cam = Camera.main;
            var follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "Câmera de jogo com CameraFollow");
            Assert.IsNotNull(follow.Settings, "Câmera de jogo configurada");
            var layout = Layout();

            var foci = new List<KeyValuePair<string, Vector3>>();
            var spawn = Object.FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None).First(m => m.Kind == ArenaMarkerKind.PlayerSpawn);
            foci.Add(new KeyValuePair<string, Vector3>("spawn dos jogadores", spawn.transform.position));
            foci.Add(new KeyValuePair<string, Vector3>("centro da praça", Vector3.zero));
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var a = layout.Avenues[i];
                var mid = a.PointAt((a.StartS + a.EndS) * 0.5f);
                foci.Add(new KeyValuePair<string, Vector3>($"meio da avenida {i + 1}", new Vector3(mid.X, 0f, mid.Y)));
                var plaza = layout.SmallPlazas[i];
                foci.Add(new KeyValuePair<string, Vector3>($"praça menor {i + 1}", new Vector3(plaza.Center.X, 0f, plaza.Center.Y)));
            }

            var renderers = Pulses().SelectMany(p => p.Renderers).Where(r => r != null).Distinct().ToArray();
            float savedAspect = cam.aspect;
            var problems = new List<string>();
            try
            {
                foreach (float aspect in new[] { 16f / 9f, 21f / 9f })
                {
                    cam.aspect = aspect;
                    foreach (var focus in foci)
                    {
                        follow.Apply(focus.Value);
                        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                        int visible = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy
                                                           && GeometryUtility.TestPlanesAABB(planes, r.bounds));
                        if (visible < 2)
                            problems.Add($"{focus.Key} ({aspect:0.00}): {visible} renderer(s) de cristal na vista");
                    }
                }
            }
            finally
            {
                cam.aspect = savedAspect;
            }
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        // ---------- Poeira ----------

        private static AmbienceParticles[] MagicDust()
            => Object.FindObjectsByType<AmbienceParticles>(FindObjectsSortMode.None)
                .Where(d => d.Kind == AmbienceParticleKind.MagicDust).OrderBy(d => d.name).ToArray();

        /// <summary>
        /// Pontos da região de emissão do sistema: a caixa (Box) ou o disco (Circle) do módulo Shape, em coordenadas do
        /// mundo, em grade. A altura inclui o salto aleatório do disco (±0,9 m).
        /// </summary>
        private static List<Vector3> EmissionSamples(ParticleSystem ps)
        {
            var shape = ps.shape;
            var t = ps.transform;
            var points = new List<Vector3>();
            if (shape.shapeType == ParticleSystemShapeType.Box)
            {
                var s = shape.scale;
                for (int ix = 0; ix <= 4; ix++)
                    for (int iy = 0; iy <= 2; iy++)
                        for (int iz = 0; iz <= 4; iz++)
                            points.Add(t.TransformPoint(Vector3.Scale(s, new Vector3(ix / 4f - 0.5f, iy / 2f - 0.5f, iz / 4f - 0.5f))));
            }
            else
            {
                float jitter = shape.randomPositionAmount;
                float r = shape.radius;
                for (int ring = 0; ring <= 3; ring++)
                {
                    int count = ring == 0 ? 1 : 12;
                    for (int k = 0; k < count; k++)
                    {
                        float a = k * 2f * Mathf.PI / count;
                        var local = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (r * ring / 3f);
                        var p = t.TransformPoint(local);
                        foreach (float dy in new[] { -jitter, 0f, jitter })
                            points.Add(p + Vector3.up * dy);
                    }
                }
            }
            return points;
        }

        [UnityTest]
        public IEnumerator PoeiraMagica_UmEmissorPorAvenidaEPracaMenor_AlemDoDiscoDeCombate()
        {
            yield return null;
            var layout = Layout();
            var settings = Settings();
            var dust = MagicDust();
            Assert.AreEqual(layout.StreetCount * 2, dust.Length, "Um emissor por avenida e um por praça menor");
            var old = GameObject.Find("PoeiraMagica");
            Assert.IsNotNull(old, "Grupo da poeira mágica");
            Assert.IsNull(old.GetComponent<ParticleSystem>(), "O anel antigo da rua do anel saiu");

            int avenues = 0, plazas = 0;
            foreach (var d in dust)
            {
                Assert.IsNotNull(d.Particles, $"{d.name}: sistema montado em jogo");
                var ps = d.Particles;
                Assert.LessOrEqual(ps.main.maxParticles, 200, $"{d.name}: poeira barata");
                foreach (var p in EmissionSamples(ps))
                    Assert.GreaterOrEqual(Flat(p).magnitude, settings.dustCombatRadius - 0.01f,
                        $"{d.name}: emite sobre o disco de combate (r < {settings.dustCombatRadius}) em {p}");

                if (d.name.StartsWith("PoeiraAvenida"))
                {
                    avenues++;
                    Assert.AreEqual(ParticleSystemShapeType.Box, ps.shape.shapeType, d.name);
                    var scale = ps.shape.scale;
                    Assert.AreEqual(layout.Avenues[0].Width, scale.x, 0.01f, $"{d.name}: largura da avenida");
                    Assert.AreEqual(settings.avenueDustBoxHeight, scale.y, 0.01f, $"{d.name}: altura");
                    Assert.AreEqual(settings.avenueDustLength, scale.z, 0.01f, $"{d.name}: comprimento");
                }
                else
                {
                    plazas++;
                    Assert.AreEqual(ParticleSystemShapeType.Circle, ps.shape.shapeType, d.name);
                    Assert.AreEqual(layout.SmallPlazas[0].Radius, ps.shape.radius, 0.01f, $"{d.name}: círculo do tamanho da praça");
                }
            }
            Assert.AreEqual(layout.StreetCount, avenues, "Avenidas");
            Assert.AreEqual(layout.StreetCount, plazas, "Praças menores");
        }

        [UnityTest]
        public IEnumerator PoeiraMagica_ViolaSoA3PorCentoEPausaForaDaTela()
        {
            yield return null;
            var settings = Settings();
            foreach (var d in MagicDust())
            {
                var main = d.Particles.main;
                Assert.AreEqual(ParticleSystemCullingMode.Pause, main.cullingMode, $"{d.name}: pausa fora da tela");
                var keys = main.startColor.gradient.colorKeys;
                // Fixed: [ciano até 1 - violeta - dourado, violeta até 1 - dourado, dourado até 1].
                Assert.AreEqual(3, keys.Length, $"{d.name}: ciano, violeta e dourado");
                float violet = keys[1].time - keys[0].time;
                Assert.AreEqual(settings.dustVioletShare, violet, 0.001f, $"{d.name}: fatia de violeta (D-070)");
                Assert.AreEqual(0.03f, settings.dustVioletShare, 0.001f, "D-070: violeta a 3%");
            }
        }

        /// <summary>
        /// Com a câmera de jogo real (CameraFollow e CameraSettings da cena) em cada ponto do disco de combate, nenhuma
        /// partícula de poeira visível (além da distância em que some perto da câmera) pode cair sobre o chão do disco
        /// de combate: a poeira fica sempre atrás do combate, nunca na frente do jogador. Confere a região de emissão
        /// de cada emissor e onde as partículas estarão quando subirem até o fim da vida.
        /// </summary>
        [UnityTest]
        public IEnumerator PoeiraMagica_NuncaFicaNaFrenteDoCombate()
        {
            yield return null;
            var dust = MagicDust();
            Assert.IsNotEmpty(dust, "Poeira mágica");
            var settings = Settings();
            var cam = Camera.main;
            var follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "Câmera de jogo com CameraFollow");
            Assert.IsNotNull(follow.Settings, "Câmera de jogo configurada");

            // Pontos de emissão e quanto cada partícula sobe até o fim da vida (a subida mais o desvio do ruído).
            var samples = new List<Vector3>();
            float rise = 0f;
            float fadeNear = 0f;
            foreach (var d in dust)
            {
                samples.AddRange(EmissionSamples(d.Particles));
                rise = Mathf.Max(rise, d.Particles.velocityOverLifetime.y.constantMax * d.Particles.main.startLifetime.constantMax);
                var mat = d.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (mat != null && mat.HasProperty("_CameraNearFadeDistance"))
                    fadeNear = Mathf.Max(fadeNear, mat.GetFloat("_CameraNearFadeDistance"));
            }
            float combat = settings.dustCombatRadius;
            float savedAspect = cam.aspect;
            cam.aspect = 16f / 9f;

            int bad = 0;
            string example = null;
            try
            {
                for (float r = 0f; r <= combat - 0.5f; r += 1.25f)
                {
                    for (float a = 0f; a < 360f; a += 10f)
                    {
                        var player = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * r);
                        follow.Apply(player);
                        var c = cam.transform.position;
                        foreach (var sample in samples)
                        {
                            foreach (float up in new[] { 0f, rise })
                            {
                                var p = sample + Vector3.up * up;
                                var vp = cam.WorldToViewportPoint(p);
                                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                                    continue;
                                if ((p - c).magnitude <= fadeNear || p.y >= c.y)
                                    continue;
                                // Onde o raio da câmera por esta partícula toca o chão: ali é o que ela "cobre" na tela.
                                var ground = c + (p - c) * (c.y / (c.y - p.y));
                                if (new Vector2(ground.x, ground.z).magnitude < combat)
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
            Assert.AreEqual(0, bad, $"Poeira na frente do combate ({example})");
        }

        [UnityTest]
        public IEnumerator Particulas_NascemEmJogoComPausaForaDaTela()
        {
            yield return null;
            var all = Object.FindObjectsByType<AmbienceParticles>(FindObjectsSortMode.None);
            // 4 fornalhas (brasas) + caldeiras e respiradouros (vapor) + poeira arcana + poeira mágica das ruas.
            Assert.GreaterOrEqual(all.Length, 4 + 2 + 3 + 1 + Layout().StreetCount * 2, "Emissores de ambientação");
            foreach (var p in all)
            {
                Assert.IsNotNull(p.Particles, $"{p.name}: ParticleSystem criado em jogo");
                Assert.AreEqual(ParticleSystemCullingMode.Pause, p.Particles.main.cullingMode, $"{p.name}: pausa fora da tela");
            }
        }
    }
}
