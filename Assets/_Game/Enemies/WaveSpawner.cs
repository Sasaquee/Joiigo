using System.Collections.Generic;
using Game.Arena;
using Game.Core.AI;
using Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Ondas com pausa (D-023), só no host. Depois da largada, o WaveDirector (Core) diz quando sai cada
    /// onda; aqui os inimigos nascem nos pontos de spawn dos portões, em rodízio. Fora do host não faz nada.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private WaveSettings settings;
        [SerializeField] private MatchState match;

        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private readonly List<int> requested = new List<int>();
        private readonly List<Transform> spawnPoints = new List<Transform>();
        private readonly List<float> spawnRadii = new List<float>();
        private WaveDirector director;
        private int nextSpawnPoint;

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

        public int WavesReleased => director?.WavesReleased ?? 0;

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
                return;
            }

            director ??= settings.CreateDirector();
            Prune();
            if (!director.Tick(Time.deltaTime, enemies.Count, requested))
                return;

            foreach (int type in requested)
            {
                if (type < 0 || type >= settings.enemyTypes.Length || settings.enemyTypes[type] == null)
                    continue;
                ServerSpawn(settings.enemyTypes[type], NextSpawnPosition());
            }
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
            return enemy;
        }

        private void Prune()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
                if (enemies[i] == null || !enemies[i].IsAlive)
                    enemies.RemoveAt(i);
        }

        private Vector3 NextSpawnPosition()
        {
            if (spawnPoints.Count == 0)
                FindSpawnPoints();
            if (spawnPoints.Count == 0)
                return Vector3.zero;

            int index = nextSpawnPoint++ % spawnPoints.Count;
            Vector2 offset = Random.insideUnitCircle * (spawnRadii[index] * 0.6f);
            Vector3 p = spawnPoints[index].position;
            return new Vector3(p.x + offset.x, p.y, p.z + offset.y);
        }

        private void FindSpawnPoints()
        {
            spawnPoints.Clear();
            spawnRadii.Clear();
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
            }
        }
    }
}
