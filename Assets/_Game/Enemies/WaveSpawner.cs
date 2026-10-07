using System.Collections.Generic;
using Game.Arena;
using Game.Core.AI;
using Game.Core.Math;
using Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Ondas com pausa (D-023), só no host. Depois da largada, o WaveDirector (Core) diz quando sai cada
    /// onda; aqui os inimigos nascem pelas bocas das ruas (D-077), em rodízio, pulando a boca com jogador vivo perto,
    /// numa fila com espaçamento por boca. O WaveDirector recebe vivos + fila, então a onda só acaba quando a fila esvazia
    /// e todos caíram. Fora do host não faz nada.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private WaveSettings settings;
        [SerializeField] private MatchState match;

        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private readonly List<Vector3> lastPositions = new List<Vector3>(); // paralela a enemies: última posição conhecida
        private readonly List<int> requested = new List<int>();
        private readonly List<Transform> spawnPoints = new List<Transform>();
        private readonly List<float> spawnRadii = new List<float>();
        private readonly List<Float2> mouthPositions = new List<Float2>();
        private readonly List<Float2> playerPositions = new List<Float2>();
        private readonly List<QueuedSpawn> ready = new List<QueuedSpawn>();
        private WaveDirector director;
        private MouthSpawnQueue queue;
        private int nextMouth;
        private Vector3 lastDeathPosition;
        private bool hasDeathPosition;

        public WaveSettings Settings => settings;

        /// <summary>Inimigos em pé (os que já caíram e estão virando sucata não contam).</summary>
        public int AliveCount
        {
            get
            {
                Prune();
                return enemies.Count;
            }
        }

        /// <summary>Inimigos da onda que o diretor já soltou mas esperam a vez de nascer na boca (espaçamento).</summary>
        public int PendingCount => queue != null ? queue.Pending : 0;

        public int WavesReleased => director?.WavesReleased ?? 0;

        /// <summary>
        /// Host: uma onda acabou de ser vencida (D-050, D-082: deixa uma carta no chão). Recebe o total de ondas vencidas e a
        /// posição onde caiu o último inimigo.
        /// </summary>
        public event System.Action<int, Vector3> WaveCleared;
        private int clearedSeen;

        public void Configure(WaveSettings newSettings, MatchState newMatch)
        {
            settings = newSettings;
            match = newMatch;
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !manager.IsListening || match == null || !match.IsStarted || settings == null)
            {
                director = null; // sessão nova recomeça da primeira onda
                queue = null;
                hasDeathPosition = false;
                return;
            }

            if (director == null)
            {
                director = settings.CreateDirector();
                clearedSeen = 0;
                queue = null;
                hasDeathPosition = false;
            }

            float dt = Time.deltaTime;
            Prune();
            SpawnReady(dt);

            // O diretor vê vivos + fila: a onda só acaba quando ninguém espera para nascer.
            bool released = director.Tick(dt, enemies.Count + PendingCount, requested);
            if (director.WavesCleared != clearedSeen)
            {
                clearedSeen = director.WavesCleared;
                WaveCleared?.Invoke(clearedSeen, hasDeathPosition ? lastDeathPosition : Vector3.zero);
            }
            if (!released)
                return;

            EnqueueWave();
            SpawnReady(0f); // o primeiro de cada boca nasce já
        }

        /// <summary>
        /// Host: recomeço da partida depois da queda total (D-084). Tira todos os inimigos da arena (vivos, em queda e a fila
        /// das bocas) e volta o diretor à primeira onda. A próxima onda só sai quando a partida estiver iniciada de novo e
        /// passar o atraso da primeira (o Update recria o diretor).
        /// </summary>
        public void ServerResetForRestart()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
                return;

            // Todos os EnemyController da cena, não só a lista: os que já caíram e viram sucata saíram dela, mas ainda estão na rede.
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy != null && enemy.IsSpawned)
                    enemy.NetworkObject.Despawn(true);
            }

            enemies.Clear();
            lastPositions.Clear();
            requested.Clear();
            ready.Clear();
            queue = null;
            director = null;
            nextMouth = 0;
            clearedSeen = 0;
            hasDeathPosition = false;
        }

        /// <summary>Host: gera um inimigo. Também serve para debug e testes.</summary>
        public EnemyController ServerSpawn(EnemyDefinition def, Vector3 position)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || def == null || def.prefab == null)
                return null;

            Vector3 toCenter = -new Vector3(position.x, 0f, position.z);
            Quaternion rotation = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter.normalized) : Quaternion.identity;

            var go = Instantiate(def.prefab, position, rotation);
            var enemy = go.GetComponent<EnemyController>();
            if (enemy == null)
            {
                Debug.LogError($"{def.name}: o prefab não tem EnemyController.");
                Destroy(go);
                return null;
            }

            enemy.ServerInit(def);
            go.GetComponent<NetworkObject>().Spawn(true);
            enemies.Add(enemy);
            lastPositions.Add(position);
            return enemy;
        }

        /// <summary>
        /// Tira da lista quem caiu. Quem sai por último guarda onde caiu (D-082): o corpo fica parado no ponto da morte
        /// até virar sucata, e se o objeto já sumiu vale a última posição vista.
        /// </summary>
        private void Prune()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (enemy != null && enemy.IsAlive)
                {
                    lastPositions[i] = enemy.transform.position;
                    continue;
                }

                lastDeathPosition = enemy != null ? enemy.transform.position : lastPositions[i];
                hasDeathPosition = true;
                enemies.RemoveAt(i);
                lastPositions.RemoveAt(i);
            }
        }

        /// <summary>Dá a cada inimigo da onda uma boca (rodízio, pulando as ocupadas) e uma hora de nascer na fila.</summary>
        private void EnqueueWave()
        {
            if (spawnPoints.Count == 0 || HasDeadPoint())
                FindSpawnPoints();
            queue ??= new MouthSpawnQueue(spawnPoints.Count, settings.spawnStagger);
            CollectPlayerPositions();

            foreach (int type in requested)
            {
                if (type < 0 || type >= settings.enemyTypes.Length || settings.enemyTypes[type] == null)
                    continue;

                int mouth = 0;
                if (mouthPositions.Count > 0)
                {
                    mouth = SpawnMouthPicker.Pick(mouthPositions, playerPositions, nextMouth, settings.mouthPlayerClearance);
                    nextMouth = (mouth + 1) % mouthPositions.Count;
                }
                queue.Enqueue(type, mouth);
            }
        }

        private void SpawnReady(float deltaTime)
        {
            if (queue == null)
                return;

            queue.Advance(deltaTime, ready);
            foreach (QueuedSpawn spawn in ready)
            {
                if (spawn.Type < 0 || spawn.Type >= settings.enemyTypes.Length || settings.enemyTypes[spawn.Type] == null)
                    continue;
                ServerSpawn(settings.enemyTypes[spawn.Type], MouthSpawnPosition(spawn.Mouth));
            }
        }

        /// <summary>Posição de nascimento na boca: sorteada em volta do marcador e puxada para a NavMesh; sem NavMesh por perto, o marcador.</summary>
        private Vector3 MouthSpawnPosition(int mouth)
        {
            if (mouth < 0 || mouth >= spawnPoints.Count || spawnPoints[mouth] == null)
                return Vector3.zero;

            Vector3 marker = spawnPoints[mouth].position;
            Vector2 offset = Random.insideUnitCircle * (spawnRadii[mouth] * settings.mouthSpawnSpread);
            var wanted = new Vector3(marker.x + offset.x, marker.y, marker.z + offset.y);
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, settings.mouthNavSampleRadius, NavMesh.AllAreas))
                return hit.position;
            return marker;
        }

        /// <summary>Jogadores vivos (no plano), para saber que boca está ocupada.</summary>
        private void CollectPlayerPositions()
        {
            playerPositions.Clear();
            var manager = NetworkManager.Singleton;
            if (manager == null)
                return;
            var clients = manager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                if (!EnemyTargets.TryGetAlivePlayer(clients[i], out var health))
                    continue;
                Vector3 p = health.transform.position;
                playerPositions.Add(new Float2(p.x, p.z));
            }
        }

        private bool HasDeadPoint()
        {
            foreach (var point in spawnPoints)
                if (point == null)
                    return true;
            return false;
        }

        private void FindSpawnPoints()
        {
            spawnPoints.Clear();
            spawnRadii.Clear();
            mouthPositions.Clear();
            var markers = new List<ArenaMarker>();
            foreach (var m in FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None))
                if (m.Kind == ArenaMarkerKind.EnemySpawn)
                    markers.Add(m);
            markers.Sort((a, b) => string.CompareOrdinal(a.name, b.name) != 0
                ? string.CompareOrdinal(a.name, b.name)
                : a.transform.position.x.CompareTo(b.transform.position.x));
            foreach (var m in markers)
            {
                spawnPoints.Add(m.transform);
                spawnRadii.Add(m.Radius);
                Vector3 p = m.transform.position;
                mouthPositions.Add(new Float2(p.x, p.z));
            }
        }
    }
}
