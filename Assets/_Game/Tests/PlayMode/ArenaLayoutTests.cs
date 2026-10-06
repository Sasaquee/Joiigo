using System.Collections;
using System.Linq;
using Game.Arena;
using Game.Cameras;
using Game.Core.Map;
using Game.Core.Math;
using Game.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// A arena do mapa novo (passe do mapa, D-073 a D-082): praça central, 3 avenidas, 3 praças menores e 3 bocas de rua, com
    /// chão andável só nas regiões do MapLayout, vedação invisível nas bordas, portões fechando as bocas, spawn dos inimigos
    /// nas bocas e a NavMesh dos caminhos. Depende da cena gerada por Game → Setup → Construir Arena. Os prédios
    /// (colliders do CityBuilder) não entram aqui: o que vale sem eles é o chão, a vedação, os portões e a NavMesh.
    /// </summary>
    public class ArenaLayoutTests
    {
        private const string LayoutSettingsPath = "Assets/_Game/Data/Map/MapLayoutSettings.asset";

        private MapLayout layout;

        [UnitySetUp]
        public IEnumerator CarregaArena()
        {
            yield return ArenaTestScene.Load();
            layout = LoadLayout();
            // Colliders recém-carregados só entram nas consultas depois de uma sincronização de física.
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Limpa()
        {
            yield return ArenaTestScene.Cleanup();
        }

        /// <summary>Os mesmos números do asset do mapa (no editor); sem ele, os padrões da tabela do plano.</summary>
        private static MapLayout LoadLayout()
        {
#if UNITY_EDITOR
            var settings = AssetDatabase.LoadAssetAtPath<MapLayoutSettings>(LayoutSettingsPath);
            if (settings != null)
                return settings.ToLayout();
#endif
            return new MapLayout();
        }

        private static Vector3 ToWorld(Float2 p, float y = 0f) => new Vector3(p.X, y, p.Y);

        private static ArenaMarker[] MarkersOf(ArenaMarkerKind kind)
            => Object.FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None).Where(m => m.Kind == kind).ToArray();

        private static ArenaMarker EnemySpawnMarker(int index)
        {
            string markerName = $"SpawnInimigo{index + 1}";
            var marker = MarkersOf(ArenaMarkerKind.EnemySpawn).FirstOrDefault(m => m.name == markerName);
            Assert.IsNotNull(marker, $"Sem o marcador EnemySpawn '{markerName}'");
            return marker;
        }

        /// <summary>O chão do Cenario embaixo do ponto do plano (raio para baixo a partir de 2 m).</summary>
        private static bool GroundAt(Float2 p, out RaycastHit hit)
            => Physics.Raycast(ToWorld(p, 2f), Vector3.down, out hit, 4f, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);

        private static bool BlockedByCenario(Vector3 footPosition)
            => Physics.CheckCapsule(footPosition + Vector3.up * 0.5f, footPosition + Vector3.up * 1.5f, 0.4f,
                MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);

        // ---------- Marcadores e peças que continuam ----------

        [Test]
        public void Arena_TemAsAreasPedidas()
        {
            var markers = Object.FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None);

            Assert.AreEqual(1, markers.Count(m => m.Kind == ArenaMarkerKind.PlayerSpawn), "Spawn dos jogadores");
            Assert.AreEqual(3, markers.Count(m => m.Kind == ArenaMarkerKind.EnemySpawn), "Spawns de inimigos");
            Assert.AreEqual(1, markers.Count(m => m.Kind == ArenaMarkerKind.CardTestArea), "Área de testes de cartas");
            Assert.AreEqual(1, markers.Count(m => m.Kind == ArenaMarkerKind.CombatCenter), "Centro de combate");
        }

        [Test]
        public void Arena_TemQuatroVagasDeSpawn()
        {
            var spawn = Object.FindFirstObjectByType<PlayerSpawnPoints>();
            Assert.IsNotNull(spawn);
            Assert.AreEqual(4, spawn.Count);
        }

        [Test]
        public void Arena_TemCameraConfiguradaESessaoDeRede()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            Assert.IsNotNull(follow, "A câmera principal tem CameraFollow");
            Assert.IsNotNull(follow.Settings);
            Assert.IsNotNull(Object.FindFirstObjectByType<NetSession>(), "Existe a sessão de rede");
            Assert.IsNotNull(Object.FindFirstObjectByType<StartLever>(), "Existe a alavanca de largada");
        }

        [Test]
        public void Camera_TemOVazadoDosPredios()
        {
            var driver = Camera.main != null ? Camera.main.GetComponent<SeeThroughDriver>() : null;
            Assert.IsNotNull(driver, "A câmera principal tem o SeeThroughDriver (D-076)");
            Assert.IsNotNull(driver.Settings, "O SeeThroughDriver tem os números (SeeThroughSettings)");
        }

        [UnityTest]
        public IEnumerator Arena_PortoesComecamParados()
        {
            yield return null;
            foreach (var spinner in Object.FindObjectsByType<Spinner>(FindObjectsSortMode.None))
            {
                if (spinner.GetComponentInParent<GateActivation>() != null)
                    Assert.IsFalse(spinner.enabled, "Engrenagem de portão parada antes da largada (D-013)");
            }
        }

        // ---------- Mapa novo: estrutura ----------

        [Test]
        public void Arena_NaoTemMaisOMuroBaixo()
        {
            Assert.IsNull(GameObject.Find("Arena/Limite"), "O muro baixo (BuildBoundary) saiu do mapa (D-073)");
        }

        [Test]
        public void Arena_PecasDeGameplayFicamEmChaoAndavel()
        {
            var spawn = MarkersOf(ArenaMarkerKind.PlayerSpawn).Single();
            Assert.IsTrue(layout.IsWalkable(spawn.transform.position.x, spawn.transform.position.z, spawn.Radius), "Plataforma de spawn dos jogadores");

            var alcove = MarkersOf(ArenaMarkerKind.CardTestArea).Single();
            Assert.IsTrue(layout.IsWalkable(alcove.transform.position.x, alcove.transform.position.z, alcove.Radius), "Alcova de cartas");

            var lever = Object.FindFirstObjectByType<StartLever>();
            Assert.IsTrue(layout.IsWalkable(lever.transform.position.x, lever.transform.position.z, 0.7f), "Alavanca");

            foreach (var group in new[] { "Caldeiras", "PostesArcanos" })
            {
                var root = GameObject.Find($"Arena/{group}");
                Assert.IsNotNull(root, $"Arena/{group}");
                foreach (Transform piece in root.transform)
                    Assert.IsTrue(layout.IsWalkable(piece.position.x, piece.position.z, 0.35f), $"{group}/{piece.name} fora do chão andável");
            }
        }

        [Test]
        public void Arena_PisoVisualNaoTemCollider_EChaoAndavelTemEEstaNoCenario()
        {
            var piso = GameObject.Find("Arena/Chao/Piso");
            Assert.IsNotNull(piso, "Piso visual");
            Assert.IsNull(piso.GetComponent<Collider>(), "O Piso virou só visual (sem collider)");

            var ground = GameObject.Find("Arena/Chao/Andavel");
            Assert.IsNotNull(ground, "Arena/Chao/Andavel");
            // Praça, 3 avenidas, 3 praças menores e 3 bocas.
            Assert.AreEqual(1 + layout.StreetCount * 3, ground.transform.childCount);
            foreach (Transform piece in ground.transform)
            {
                Assert.IsNotNull(piece.GetComponent<Collider>(), $"{piece.name} tem collider");
                Assert.AreEqual(MapLayers.Cenario, piece.gameObject.layer, $"{piece.name} está na camada Cenario");
            }
        }

        [UnityTest]
        public IEnumerator Chao_ExisteEmCadaRegiaoAndavel()
        {
            yield return null;
            var points = new System.Collections.Generic.List<(string name, Float2 p)>
            {
                ("praça, centro-sul", MapLayout.Polar(180f, 8f)),
                ("praça, oeste", MapLayout.Polar(-120f, 10f)),
            };
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var avenue = layout.Avenues[i];
                var small = layout.SmallPlazas[i];
                var mouth = layout.Mouths[i];
                // Na praça menor, no anel de 5,5 m do centro (o meio pode ter um marco da cidade).
                var side = avenue.Rect.Side;
                points.Add(($"avenida {i + 1}", avenue.PointAt((avenue.StartS + avenue.EndS) * 0.5f)));
                points.Add(($"praça menor {i + 1}", new Float2(small.Center.X + side.X * 5.5f, small.Center.Y + side.Y * 5.5f)));
                points.Add(($"boca {i + 1}", mouth.PointAt((mouth.StartS + mouth.EndS) * 0.5f)));
            }

            foreach (var (name, p) in points)
            {
                Assert.IsTrue(layout.IsWalkable(p), $"Ponto de teste da região '{name}' devia estar na área andável");
                Assert.IsTrue(GroundAt(p, out var hit), $"Sem chão do Cenario em '{name}' {p}");
                Assert.AreEqual(0f, hit.point.y, 0.01f, $"Chão de '{name}' fora do nível 0");
                Assert.Greater(hit.normal.y, 0.99f, $"Chão de '{name}' devia ser plano");
            }
        }

        [UnityTest]
        public IEnumerator Chao_NaoExisteEntreAsAvenidasNemForaDaPraca()
        {
            yield return null;
            var voids = new[]
            {
                MapLayout.Polar(-22.5f, 35f), // cunha NO-N, logo depois da praça
                MapLayout.Polar(22.5f, 35f),  // cunha N-NE
                MapLayout.Polar(-22.5f, 47f), // entre as praças menores NO e N
                MapLayout.Polar(22.5f, 47f),
                MapLayout.Polar(-22.5f, 60f),
                MapLayout.Polar(180f, 31f),   // atrás da praça, fora do raio de andar
                MapLayout.Polar(90f, 40f),
            };
            foreach (var p in voids)
            {
                Assert.IsFalse(layout.IsWalkable(p), $"O ponto {p} devia estar fora da área andável");
                bool floor = GroundAt(p, out var hit) && Mathf.Abs(hit.point.y) < 0.05f;
                Assert.IsFalse(floor, $"Não devia haver chão em {p} (entre avenidas / fora da praça)");
            }
        }

        // ---------- Portões ----------

        [UnityTest]
        public IEnumerator Portoes_Fecham_CadaBocaViradosParaOCentro()
        {
            yield return null;
            var gates = Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None);
            Assert.AreEqual(layout.StreetCount, gates.Length, "Um portão por boca");

            for (int i = 0; i < layout.StreetCount; i++)
            {
                var mapGate = layout.Gates[i];
                var gate = gates.SingleOrDefault(g => g.name == $"PortaoMaquina{i + 1}");
                Assert.IsNotNull(gate, $"PortaoMaquina{i + 1} (o AmbienceBuilder procura por esse nome, filho direto da Arena)");

                Vector3 pos = gate.transform.position;
                Assert.AreEqual(mapGate.Position.X, pos.x, 0.01f, $"Portão {i + 1}: X");
                Assert.AreEqual(mapGate.Position.Y, pos.z, 0.01f, $"Portão {i + 1}: Z");
                Assert.IsTrue(layout.Mouths[i].Rect.Contains(pos.x, pos.z), $"O portão {i + 1} fica dentro da boca {i + 1}");

                // +Z do portão aponta para o centro (D-081).
                Vector3 toCenter = new Vector3(-pos.x, 0f, -pos.z).normalized;
                Assert.Greater(Vector3.Dot(gate.transform.forward, toCenter), 0.999f, $"Portão {i + 1} virado para o centro");

                // Fecha a boca: o corpo do portão (collider) ocupa o meio do fim da rua.
                Assert.IsTrue(Physics.CheckBox(pos + Vector3.up * 1.5f, new Vector3(0.5f, 0.5f, 0.2f), gate.transform.rotation,
                    ~0, QueryTriggerInteraction.Ignore), $"Portão {i + 1} sem collider no lugar");
            }
        }

        // ---------- Spawn dos inimigos e NavMesh ----------

        [Test]
        public void Spawn_DosInimigosFicaNasBocas_LongeDoCentro_ESobreANavMesh()
        {
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var marker = EnemySpawnMarker(i);
                Vector3 pos = marker.transform.position;
                Assert.AreEqual(layout.EnemySpawns[i].X, pos.x, 0.01f, $"SpawnInimigo{i + 1}: X");
                Assert.AreEqual(layout.EnemySpawns[i].Y, pos.z, 0.01f, $"SpawnInimigo{i + 1}: Z");
                Assert.AreEqual(layout.Params.EnemySpawnRadius, marker.Radius, 1e-3f, "Raio do marcador");
                Assert.GreaterOrEqual(new Vector2(pos.x, pos.z).magnitude, 50f, $"SpawnInimigo{i + 1} a r >= 50 m (D-077)");
                Assert.AreEqual(MapRegionKind.Mouth, layout.Region(pos.x, pos.z).Kind, $"SpawnInimigo{i + 1} dentro de uma boca");
                Assert.IsTrue(layout.IsWalkable(pos.x, pos.z, marker.Radius), $"SpawnInimigo{i + 1} com folga do raio dentro da boca");

                Assert.IsTrue(NavMesh.SamplePosition(pos, out var hit, 0.5f, NavMesh.AllAreas),
                    $"SpawnInimigo{i + 1} sobre a NavMesh (reconstruir a arena assa a NavMesh)");
                Assert.AreEqual(0f, hit.position.y, 0.3f);
            }
        }

        [Test]
        public void Spawn_DosInimigosSaoOsTresNomeados()
        {
            var names = MarkersOf(ArenaMarkerKind.EnemySpawn).Select(m => m.name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "SpawnInimigo1", "SpawnInimigo2", "SpawnInimigo3" }, names);
        }

        [Test]
        public void NavMesh_LigaCadaBocaAPlataformaDosJogadores()
        {
            var platform = MarkersOf(ArenaMarkerKind.PlayerSpawn).Single().transform.position;
            Assert.IsTrue(NavMesh.SamplePosition(platform, out var platformHit, 1f, NavMesh.AllAreas), "Plataforma de spawn sobre a NavMesh");

            for (int i = 0; i < layout.StreetCount; i++)
            {
                Vector3 from = EnemySpawnMarker(i).transform.position;
                Assert.IsTrue(NavMesh.SamplePosition(from, out var fromHit, 1f, NavMesh.AllAreas), $"Boca {i + 1} sobre a NavMesh");

                var path = new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(fromHit.position, platformHit.position, NavMesh.AllAreas, path), $"Caminho da boca {i + 1}");
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Caminho da boca {i + 1} até a plataforma completo");

                float length = 0f;
                for (int c = 1; c < path.corners.Length; c++)
                    length += Vector3.Distance(path.corners[c - 1], path.corners[c]);
                float straight = Vector3.Distance(fromHit.position, platformHit.position);
                Assert.GreaterOrEqual(length, straight - 0.01f);
                Assert.Less(length, straight * 1.6f, $"Caminho da boca {i + 1} dá voltas demais ({length:0.#} m para {straight:0.#} m em linha reta)");
            }
        }

        [UnityTest]
        public IEnumerator NavMesh_NaoCobreAsCunhasEntreAsAvenidas()
        {
            yield return null;
            foreach (var p in new[] { MapLayout.Polar(-22.5f, 40f), MapLayout.Polar(22.5f, 40f), MapLayout.Polar(-22.5f, 52f), MapLayout.Polar(180f, 33f) })
                Assert.IsFalse(NavMesh.SamplePosition(ToWorld(p), out _, 1.5f, NavMesh.AllAreas), $"NavMesh fora da área andável em {p}");
        }

        // ---------- Vedação ----------

        [UnityTest]
        public IEnumerator Vedacao_FechaTodoOContornoDaRegiaoAndavel()
        {
            yield return null;
            var fence = GameObject.Find("Arena/Vedacao");
            Assert.IsNotNull(fence, "Arena/Vedacao");
            foreach (Transform piece in fence.transform)
            {
                Assert.AreEqual(MapLayers.Cenario, piece.gameObject.layer, $"{piece.name} na camada Cenario");
                Assert.IsNull(piece.GetComponent<Renderer>(), $"{piece.name} é invisível");
            }

            // Logo depois de cada segmento do contorno (por fora, com o corpo do jogador inteiro), há parede.
            var segments = MapBoundary.Segments(layout);
            Assert.Greater(segments.Count, 60);
            foreach (var s in segments)
            {
                Float2 outside = s.Midpoint + s.Outward * 0.45f;
                Assert.IsTrue(BlockedByCenario(ToWorld(outside)), $"Sem vedação por fora do segmento {s.A}-{s.B}");
            }
        }

        [UnityTest]
        public IEnumerator Vedacao_NaoAtrapalhaOChaoAndavel()
        {
            yield return null;
            // Longe das peças da praça: os centros das regiões andáveis estão livres de collider do Cenario (a 0,5 m do chão para cima).
            Assert.IsFalse(BlockedByCenario(ToWorld(MapLayout.Polar(-120f, 10f))), "Praça livre");
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var avenue = layout.Avenues[i];
                Assert.IsFalse(BlockedByCenario(ToWorld(avenue.PointAt((avenue.StartS + avenue.EndS) * 0.5f))), $"Avenida {i + 1} livre");
                var mouth = layout.Mouths[i];
                Assert.IsFalse(BlockedByCenario(ToWorld(mouth.PointAt(mouth.StartS + 1f))), $"Boca {i + 1} livre");
            }
        }

        [UnityTest]
        public IEnumerator Vedacao_ImpedeASaidaDaPraca_NaPraticaComOCharacterController()
        {
            yield return null;
            // Um corpo de jogador andando para fora do sul da praça (sem avenida ali) para na vedação.
            var go = new GameObject("TesteVedacao");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1f, 0f);
            cc.enabled = false;
            float startRadius = layout.Params.PlazaWalkRadius - 3f;
            go.transform.position = ToWorld(MapLayout.Polar(180f, startRadius));
            cc.enabled = true;

            for (int i = 0; i < 120; i++)
            {
                cc.Move(Vector3.back * 0.2f + Vector3.down * 0.1f);
                yield return null;
            }

            float radius = new Vector2(go.transform.position.x, go.transform.position.z).magnitude;
            Object.Destroy(go);
            Assert.Less(radius, layout.Params.PlazaWalkRadius + 0.1f, "O CharacterController saiu da praça pela borda sul");
            Assert.Greater(radius, startRadius + 1.5f, "O corpo devia ter andado para fora, até a vedação");
        }
    }
}
