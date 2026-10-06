using System.Collections;
using System.Collections.Generic;
using Game.Arena;
using Game.Cards;
using Game.Combat;
using Game.Dice;
using Game.Enemies;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Passe do mapa (D-077, D-080): os inimigos saem das bocas das ruas e chegam ao jogador contornando prédios, com a NavMesh
    /// só calculando o caminho. Escritos por contrato contra o mapa novo (marcadores SpawnInimigo1..3 nas bocas, NavMesh
    /// assada, camada Cenario nos prédios): precisam do mapa novo (P2/P3) e da NavMesh de ArenaNavMeshBuilder para passar.
    /// </summary>
    public class EnemyNavigationTests
    {
        // Centro da praça menor NE (plano do mapa, seção 1). O pilão arcano ocupa o meio: o ponto é puxado para o anel andável.
        private static readonly Vector3 SquareNE = new Vector3(33.2f, 0f, 33.2f);

        private NetSession session;
        private WaveSpawner spawner;
        private NetworkPlayer player;
        private NetworkHealth playerHealth;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            session = Object.FindFirstObjectByType<NetSession>();
            Assert.IsTrue(session.Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            playerHealth = player.GetComponent<NetworkHealth>();
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Cena tem o WaveSpawner");
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            Time.timeScale = 1f;
            CardDropService.DebugForceNextRoll(null);
            yield return ArenaTestScene.Cleanup();
        }

        // ---------- Ajudantes ----------

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        private EnemyDefinition Def(int index) => spawner.Settings.enemyTypes[index];

        private static ArenaMarker SpawnMarker(string markerName)
        {
            foreach (var m in Object.FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None))
                if (m.Kind == ArenaMarkerKind.EnemySpawn && m.name == markerName)
                    return m;
            Assert.Fail($"Sem o marcador EnemySpawn '{markerName}' (precisa do mapa novo: reconstruir Game > Setup > Construir Arena)");
            return null;
        }

        /// <summary>Ponto da NavMesh mais perto de `want`; falha se não há NavMesh assada por perto.</summary>
        private static Vector3 OnNavMesh(Vector3 want, float radius)
        {
            Assert.IsTrue(NavMesh.SamplePosition(want, out NavMeshHit hit, radius, NavMesh.AllAreas),
                $"Sem NavMesh a {radius} m de {want} (precisa do mapa novo e do bake da NavMesh)");
            return hit.position;
        }

        private static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        private float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>Linha de visão no plano do peito (1 m) contra o cenário.</summary>
        private static bool Blocked(Vector3 a, Vector3 b)
        {
            Vector3 lift = Vector3.up;
            return Physics.Linecast(a + lift, b + lift, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>A cápsula do inimigo (um pouco menor, acima do chão) não pode entrar em nada da camada Cenario.</summary>
        private static bool InsideScenery(EnemyController enemy)
        {
            var cc = enemy.GetComponent<CharacterController>();
            float radius = cc.radius * 0.8f;
            float lowest = 0.3f; // o chão andável também é Cenario
            Vector3 p = enemy.transform.position;
            Vector3 bottom = p + Vector3.up * (lowest + radius);
            Vector3 top = p + Vector3.up * Mathf.Max(cc.height - radius, lowest + radius);
            return Physics.CheckCapsule(bottom, top, radius, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);
        }

        // ---------- Testes ----------

        [UnityTest]
        public IEnumerator Inimigo_SaiDaBocaEChegaAoJogadorContornandoPredios()
        {
            // precisa do mapa novo (P2/P3)
            Vector3 square = OnNavMesh(SquareNE, 8f);
            MovePlayer(square);
            yield return null;
            yield return null;

            var mouth = SpawnMarker("SpawnInimigo1"); // boca NO, ~(-40,7; 40,7)
            Vector3 start = OnNavMesh(mouth.transform.position, 3f);

            Assert.IsTrue(Blocked(start, player.transform.position),
                "Da boca NO até a praça NE há prédios no meio (senão o teste não prova o contorno)");

            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(start, OnNavMesh(player.transform.position, 2f), NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "A NavMesh liga a boca à praça NE");
            float length = PathLength(path);
            Assert.Greater(length, PlanarDistance(start, player.transform.position), "O caminho dá a volta nos prédios");

            var def = Def(0); // autômato
            Time.timeScale = 3f;
            var enemy = spawner.ServerSpawn(def, start);
            Assert.IsNotNull(enemy, "Autômato nasceu na boca");

            float timeout = length / def.moveSpeed * 1.5f + 5f; // em segundos de jogo
            float arrive = def.attackRange + 0.5f;
            bool arrived = false;
            int framesInsideScenery = 0;
            while (timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;
                if (enemy == null || !enemy.IsAlive)
                    break;
                if (InsideScenery(enemy))
                    framesInsideScenery++;
                if (PlanarDistance(enemy.transform.position, player.transform.position) <= arrive)
                {
                    arrived = true;
                    break;
                }
            }

            Assert.AreEqual(0, framesInsideScenery, "O inimigo nunca atravessa prédio");
            Assert.IsTrue(arrived, "Chegou ao jogador dentro do tempo do caminho");
        }

        [UnityTest]
        public IEnumerator Drone_NaoAtiraAtravesDePredio()
        {
            // precisa do mapa novo (P2/P3)
            var def = Def(1); // drone
            FindBlockedPair(def, out Vector3 playerPos, out Vector3 dronePos);

            MovePlayer(playerPos);
            yield return null;
            yield return null;
            Assert.IsTrue(Blocked(dronePos, player.transform.position), "Ponto escolhido tem prédio entre drone e jogador");

            float healthBefore = playerHealth.Current;
            Time.timeScale = 3f;
            var drone = spawner.ServerSpawn(def, dronePos);
            Assert.IsNotNull(drone, "Drone nasceu");

            EnemyPhase previous = EnemyPhase.Idle;
            bool windupSeen = false;
            float timeout = 40f;
            while (timeout > 0f && playerHealth.Current >= healthBefore)
            {
                yield return null;
                timeout -= Time.deltaTime;
                if (drone == null || !drone.IsAlive)
                    break;

                EnemyPhase now = drone.Phase;
                if (now == EnemyPhase.Windup && previous != EnemyPhase.Windup)
                {
                    windupSeen = true;
                    Assert.IsFalse(Blocked(drone.transform.position, player.transform.position),
                        "O drone só começa o aviso do tiro com linha de visão (nada de atirar através do prédio)");
                }
                previous = now;
            }

            Assert.IsTrue(windupSeen, "O drone contorna os prédios, ganha linha de visão e atira");
            Assert.Less(playerHealth.Current, healthBefore, "O tiro, saído com linha de visão, acerta o jogador parado");
        }

        [UnityTest]
        public IEnumerator Emboscada_NasceEmChaoAndavelAlcancavel()
        {
            // precisa do mapa novo (P2/P3) e da emboscada com NavMesh (P6)
            Vector3 square = OnNavMesh(SquareNE, 8f);
            MovePlayer(square);
            yield return null;
            yield return null;

            var service = Object.FindFirstObjectByType<CardDropService>();
            var cards = player.GetComponent<PlayerCards>();
            Assert.IsNotNull(service, "Cena tem o CardDropService");

            Vector3[] seen = null;
            void OnAmbush(Vector3[] positions) => seen = positions;
            CardDropService.AmbushRevealed += OnAmbush;
            try
            {
                Vector3 near = cards.transform.position + Vector3.forward * 1.5f;
                FloorCard card = service.ServerSpawnFloorCard(new Vector3(near.x, 0f, near.z));
                Assert.IsNotNull(card, "A carta apareceu no chão");
                yield return null;

                CardDropService.DebugForceNextRoll(1); // o 1 chama a emboscada (D-049)
                card.ServerInteract(NetworkManager.Singleton.LocalClientId);

                float timeout = service.Settings.rollDuration + service.Settings.grantDelay + 2f;
                while (seen == null && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                CardDropService.AmbushRevealed -= OnAmbush;
            }

            Assert.IsNotNull(seen, "A emboscada nasceu");
            Vector3 target = OnNavMesh(player.transform.position, 2f);
            var path = new NavMeshPath();
            foreach (Vector3 position in seen)
            {
                Assert.IsTrue(NavMesh.SamplePosition(position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas),
                    $"Inimigo da emboscada nasceu fora do chão andável: {position}");
                Assert.IsTrue(NavMesh.CalculatePath(hit.position, target, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                    $"Inimigo da emboscada em {position} alcança o jogador");
            }
        }

        /// <summary>
        /// Acha, na NavMesh perto da praça NE, um jogador e um ponto do drone dentro do alcance de tiro (entre minRange e
        /// attackRange) com prédio no meio.
        /// </summary>
        private static void FindBlockedPair(EnemyDefinition def, out Vector3 playerPos, out Vector3 dronePos)
        {
            const float step = 2f;
            var points = new List<Vector3>();
            for (float x = -60f; x <= 60f; x += step)
                for (float z = -60f; z <= 60f; z += step)
                    if (NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
                        points.Add(hit.position);
            Assert.IsNotEmpty(points, "Sem NavMesh na cena (precisa do mapa novo e do bake da NavMesh)");

            points.Sort((a, b) => (a - SquareNE).sqrMagnitude.CompareTo((b - SquareNE).sqrMagnitude));

            float near = def.minRange + 0.5f;
            float far = def.attackRange - 0.5f;
            var path = new NavMeshPath();
            int limit = Mathf.Min(points.Count, 600);
            for (int i = 0; i < limit; i++)
            {
                for (int j = 0; j < points.Count; j++)
                {
                    Vector3 a = points[i];
                    Vector3 b = points[j];
                    float d = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    if (d < near || d > far || !Blocked(a, b))
                        continue;
                    if (!NavMesh.CalculatePath(b, a, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                        continue;
                    playerPos = a;
                    dronePos = b;
                    return;
                }
            }

            Assert.Fail("Não achei, na NavMesh, um par jogador-drone a tiro com prédio no meio");
            playerPos = dronePos = Vector3.zero;
        }
    }
}
