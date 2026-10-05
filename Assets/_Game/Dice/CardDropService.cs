using System;
using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Core.Cards;
using Game.Core.Dice;
using Game.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace Game.Dice
{
    /// <summary>
    /// Carta do chão e D20 (Fase 6). Tudo que decide roda no host (autoridade): cria a carta no fim de cada onda
    /// (D-050), rola o D20 puro quando alguém pega (§3.3), avisa todos para mostrarem o mesmo dado (D-047),
    /// entrega as cartas depois da rolagem (D-048, D-051, D-052) e chama a emboscada no 1 (D-049).
    /// Fica num objeto da cena (não é singleton: quem precisa acha pela cena).
    /// </summary>
    public class CardDropService : NetworkBehaviour
    {
        [SerializeField] private DiceSettings settings;
        [SerializeField] private CardDatabase database;
        [SerializeField] private WaveSpawner spawner;
        [SerializeField] private FloorCard floorCardPrefab;
        [Tooltip("Onde a carta aparece no fim da onda (centro da arena).")]
        [SerializeField] private Transform dropPoint;
        [Tooltip("Raio útil da arena: a emboscada nunca nasce além disto a partir do centro (m).")]
        [SerializeField] private float arenaInnerRadius = 23f;

        /// <summary>Em todos: um jogador pegou uma carta e o dado começou a rolar (quem rolou, resultado, duração da rolagem).</summary>
        public static event Action<ulong, int, float> RollStarted;

        private D20 dice;
        private DiceTable table;
        private CardDraw draw;
        private IRandomSource random;
        private readonly List<FloorCard> cardsOnFloor = new List<FloorCard>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int? debugNextRoll;

        /// <summary>Só debug (§4.7): o próximo resultado. Não existe em build de jogo; o D20 do Core continua puro.</summary>
        public static void DebugForceNextRoll(int? value) => debugNextRoll = value;
        public static int? DebugNextRoll => debugNextRoll;
#endif

        public DiceSettings Settings => settings;
        public IReadOnlyList<FloorCard> CardsOnFloor => cardsOnFloor;
        /// <summary>Host: último resultado rolado (0 = nenhum). Para o overlay de debug e os testes.</summary>
        public int LastRoll { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            random = new SeededRandom(Environment.TickCount);
            dice = new D20(random);
            table = settings != null ? settings.CreateTable() : null;
            draw = new CardDraw(BuildPool(), settings != null ? settings.pathBias : 0f);
            if (spawner != null)
                spawner.WaveCleared += OnWaveCleared;
        }

        public override void OnNetworkDespawn()
        {
            if (spawner != null)
                spawner.WaveCleared -= OnWaveCleared;
            cardsOnFloor.Clear();
        }

        private List<DrawCandidate> BuildPool()
        {
            var pool = new List<DrawCandidate>();
            if (database == null)
                return pool;
            for (int id = 0; id < database.Count; id++)
            {
                CardData card = database.Get(id);
                if (card == null)
                    continue;
                pool.Add(new DrawCandidate(id, card.rarity, card.cursed, card.arcana == Arcana.Major, card.tags));
            }
            return pool;
        }

        private void OnWaveCleared(int wavesCleared)
        {
            Vector3 position = dropPoint != null ? dropPoint.position : Vector3.zero;
            ServerSpawnFloorCard(position);
        }

        /// <summary>Host: põe uma carta flutuando no chão (fim de onda, debug e testes).</summary>
        public FloorCard ServerSpawnFloorCard(Vector3 position)
        {
            if (!IsServer || floorCardPrefab == null)
                return null;
            var card = Instantiate(floorCardPrefab, position, Quaternion.identity);
            card.GetComponent<NetworkObject>().Spawn(true);
            cardsOnFloor.Add(card);
            return card;
        }

        /// <summary>Host: o jogador apertou F perto da carta. Rola, mostra a todos e entrega depois da rolagem.</summary>
        public void ServerPickUp(FloorCard card, ulong clientId)
        {
            if (!IsServer || card == null || !card.IsSpawned || table == null)
                return;
            var player = FindPlayerCards(clientId);
            if (player == null)
                return;

            cardsOnFloor.Remove(card);
            card.GetComponent<NetworkObject>().Despawn(true);

            int roll = dice.Roll();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugNextRoll.HasValue)
            {
                roll = Mathf.Clamp(debugNextRoll.Value, 1, D20.Faces);
                debugNextRoll = null;
            }
#endif
            LastRoll = roll;

            // O tema vem do caminho do jogador + o lugar, escolhido sem olhar o dado (Pilar 2).
            int topCount = settings != null ? settings.pathTopTags : 2;
            string theme = draw.ChooseTheme(player.Path.Top(topCount), random);
            DiceOutcome outcome = table.Resolve(roll);
            List<CardGrant> grants = draw.Draw(outcome, theme, player.OwnedQuality, random);

            float duration = settings != null ? settings.rollDuration : 2.2f;
            ShowRollRpc(clientId, roll, duration);
            float delay = duration + (settings != null ? settings.grantDelay : 0.6f);
            StartCoroutine(GrantAfter(player, grants, outcome.Danger, delay));
        }

        private IEnumerator GrantAfter(PlayerCards player, List<CardGrant> grants, bool danger, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (player == null || !player.IsSpawned)
                yield break;
            player.ServerApplyGrants(grants);
            if (danger)
                ServerAmbush(player.transform.position);
        }

        /// <summary>Host: D-049 — 2 ou 3 inimigos surgem em volta do jogador.</summary>
        private void ServerAmbush(Vector3 center)
        {
            if (spawner == null || spawner.Settings == null || spawner.Settings.enemyTypes.Length == 0)
                return;
            int min = settings != null ? settings.ambushMin : 2;
            int max = settings != null ? Mathf.Max(min, settings.ambushMax) : 3;
            float radius = settings != null ? settings.ambushRadius : 5f;
            int count = random.Next(min, max + 1);
            var types = spawner.Settings.enemyTypes;
            float start = random.Next(0, 360);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                Vector3 position = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                var flat = new Vector2(position.x, position.z);
                if (flat.magnitude > arenaInnerRadius)
                {
                    flat = flat.normalized * arenaInnerRadius;
                    position = new Vector3(flat.x, position.y, flat.y);
                }
                var def = types[random.Next(0, types.Length)];
                if (def != null)
                    spawner.ServerSpawn(def, position);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void ShowRollRpc(ulong roller, int result, float duration)
        {
            RollStarted?.Invoke(roller, result, duration);
        }

        private static PlayerCards FindPlayerCards(ulong clientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
                return null;
            return client.PlayerObject.GetComponent<PlayerCards>();
        }
    }
}
