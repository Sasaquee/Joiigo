using System;
using System.Collections;
using System.Collections.Generic;
using Game.Arena;
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
    /// Passe do mapa (D-077): os inimigos de cada onda nascem pelas bocas das ruas, em rodízio, pulando a boca com jogador vivo
    /// perto, numa fila com espaçamento por boca. Valem na arena atual e no mapa novo (os marcadores EnemySpawn são as bocas);
    /// no mapa novo a posição ainda é puxada para a NavMesh, o que fica coberto por `Ondas_NascemSobreANavMesh`.
    /// </summary>
    public class WaveSpawnerTests
    {
        private struct Sighting
        {
            public int Mouth;
            public float Time;
            public Vector3 Position;
        }

        private WaveSpawner spawner;
        private MatchState match;
        private NetworkPlayer player;
        private List<ArenaMarker> mouths;
        private WaveSettings originalSettings;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(spawner, "Cena tem o WaveSpawner");
            Assert.IsNotNull(match, "Cena tem a MatchState");
            originalSettings = spawner.Settings;

            // As bocas, na mesma ordem em que o WaveSpawner as numera (nome e depois x).
            mouths = new List<ArenaMarker>();
            foreach (var m in Object.FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None))
                if (m.Kind == ArenaMarkerKind.EnemySpawn)
                    mouths.Add(m);
            mouths.Sort((a, b) => string.CompareOrdinal(a.name, b.name) != 0
                ? string.CompareOrdinal(a.name, b.name)
                : a.transform.position.x.CompareTo(b.transform.position.x));
            Assert.AreEqual(3, mouths.Count, "Três bocas de rua");
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
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

        private int FirstWaveSize()
        {
            int total = 0;
            foreach (int c in spawner.Settings.waves[0].counts)
                total += c;
            return total;
        }

        private int NearestMouth(Vector3 position)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < mouths.Count; i++)
            {
                Vector3 p = mouths[i].transform.position;
                float d = new Vector2(p.x - position.x, p.z - position.z).magnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        private float PlanarDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Largada, e a cada quadro anota (boca mais próxima, hora, posição) de cada inimigo novo até haver `expected` ou estourar o tempo.</summary>
        private IEnumerator StartAndWatch(int expected, float timeout, List<Sighting> seen, Action<int> onNewEnemy = null)
        {
            match.ServerStart();
            var known = new HashSet<int>();
            while (seen.Count < expected && timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;
                foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (!enemy.IsAlive || !known.Add(enemy.GetInstanceID()))
                        continue;
                    seen.Add(new Sighting
                    {
                        Mouth = NearestMouth(enemy.transform.position),
                        Time = Time.time,
                        Position = enemy.transform.position
                    });
                    onNewEnemy?.Invoke(seen.Count);
                }
            }
        }

        // ---------- Testes ----------

        [UnityTest]
        public IEnumerator Ondas_PrimeiraOnda_UmInimigoEmCadaBoca()
        {
            // Com o jogador no centro nenhuma boca está ocupada: os três da primeira onda saem um por boca (rodízio).
            MovePlayer(Vector3.zero);
            yield return null;
            Assert.AreEqual(3, FirstWaveSize(), "A primeira onda tem 3 inimigos (um por boca)");

            var seen = new List<Sighting>();
            yield return StartAndWatch(3, spawner.Settings.firstWaveDelay + 4f, seen);
            Assert.AreEqual(3, seen.Count, "A primeira onda nasceu");

            var usedMouths = new HashSet<int>();
            foreach (var s in seen)
                usedMouths.Add(s.Mouth);
            Assert.AreEqual(3, usedMouths.Count, "Cada inimigo nasceu numa boca diferente");

            float slack = spawner.Settings.mouthNavSampleRadius + 1f;
            foreach (var s in seen)
            {
                float d = PlanarDistance(s.Position, mouths[s.Mouth].transform.position);
                Assert.LessOrEqual(d, mouths[s.Mouth].Radius + slack, "Nasce em volta do marcador da boca");
            }
        }

        [UnityTest]
        public IEnumerator Ondas_BocaComJogadorVivoEPulada_EFilaEspacaOsDaMesmaBoca()
        {
            // Jogador em cima da boca 1 (N): o rodízio 0 → 1 → 2 pula a 1 e a primeira onda sai 0, 2, 0.
            // O segundo inimigo da boca 0 espera o espaçamento da fila.
            Vector3 mouth = mouths[1].transform.position;
            MovePlayer(NavMesh.SamplePosition(mouth, out NavMeshHit onMesh, 3f, NavMesh.AllAreas) ? onMesh.position : mouth);
            yield return null;

            var seen = new List<Sighting>();
            int pendingAtFirstSighting = -1;
            yield return StartAndWatch(3, spawner.Settings.firstWaveDelay + 5f, seen, count =>
            {
                if (count == 1)
                    pendingAtFirstSighting = spawner.PendingCount;
            });

            Assert.AreEqual(3, seen.Count, "A primeira onda inteira nasceu");
            foreach (var s in seen)
                Assert.AreNotEqual(1, s.Mouth, "Ninguém nasce na boca onde está o jogador");
            Assert.GreaterOrEqual(PlanarDistance(seen[0].Position, player.transform.position), spawner.Settings.mouthPlayerClearance,
                "Nasce a pelo menos a folga do jogador");

            int atMouth0 = 0, atMouth2 = 0;
            foreach (var s in seen)
            {
                if (s.Mouth == 0) atMouth0++;
                if (s.Mouth == 2) atMouth2++;
            }
            Assert.AreEqual(2, atMouth0, "O rodízio volta à boca 0 depois de pular a 1");
            Assert.AreEqual(1, atMouth2);

            Assert.AreEqual(1, pendingAtFirstSighting, "Um inimigo ainda esperava na fila quando os primeiros nasceram");

            var sameMouth = seen.FindAll(s => s.Mouth == 0);
            float gap = Mathf.Abs(sameMouth[1].Time - sameMouth[0].Time);
            Assert.GreaterOrEqual(gap, spawner.Settings.spawnStagger - 0.06f, "Dois da mesma boca nascem com o espaçamento da fila");
            Assert.AreEqual(0, spawner.PendingCount, "A fila esvaziou");
        }

        [UnityTest]
        public IEnumerator Ondas_TodasAsBocasOcupadas_UsaAMaisLonge()
        {
            // Folga enorme: todas as bocas "ocupadas". Com o jogador na última boca, a mais longe dele é a primeira.
            var wide = Object.Instantiate(originalSettings);
            wide.mouthPlayerClearance = 500f;
            spawner.Configure(wide, match);

            Vector3 mouth = mouths[2].transform.position;
            MovePlayer(NavMesh.SamplePosition(mouth, out NavMeshHit onMesh, 3f, NavMesh.AllAreas) ? onMesh.position : mouth);
            yield return null;

            int farthest = 0;
            float farthestDistance = -1f;
            for (int i = 0; i < mouths.Count; i++)
            {
                float d = PlanarDistance(mouths[i].transform.position, player.transform.position);
                if (d > farthestDistance)
                {
                    farthestDistance = d;
                    farthest = i;
                }
            }

            var seen = new List<Sighting>();
            yield return StartAndWatch(3, wide.firstWaveDelay + 5f, seen);
            Assert.AreEqual(3, seen.Count, "A primeira onda nasceu");
            foreach (var s in seen)
                Assert.AreEqual(farthest, s.Mouth, "Com todas ocupadas, todos saem pela boca mais longe do jogador");

            spawner.Configure(originalSettings, match);
            Object.Destroy(wide);
        }

        [UnityTest]
        public IEnumerator Ondas_NascemSobreANavMesh()
        {
            // precisa do mapa novo (P2/P3) e do bake da NavMesh
            MovePlayer(Vector3.zero);
            yield return null;

            var seen = new List<Sighting>();
            yield return StartAndWatch(3, spawner.Settings.firstWaveDelay + 4f, seen);
            Assert.AreEqual(3, seen.Count, "A primeira onda nasceu");
            foreach (var s in seen)
            {
                Assert.IsTrue(NavMesh.SamplePosition(s.Position, out _, 0.5f, NavMesh.AllAreas),
                    $"Inimigo nasceu fora do chão andável: {s.Position} (precisa do mapa novo e da NavMesh assada)");
            }
        }
    }
}
