using System;
using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Combat;
using Game.Core.Cards;
using Game.Core.Dice;
using Game.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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
        [Tooltip("Sem uso desde D-082: a carta do fim da onda surge onde caiu o último inimigo. Fica serializado só para não quebrar a cena e o construtor.")]
        [SerializeField] private Transform dropPoint;

        /// <summary>Em todos: um jogador pegou uma carta e o dado começou a rolar (quem rolou, resultado, duração da rolagem).</summary>
        public static event Action<ulong, int, float> RollStarted;

        /// <summary>
        /// Em todos: os inimigos da emboscada do 1 acabaram de nascer nestas posições (D-068). Só para a apresentação
        /// (AmbushFx); não muda nem atrasa nada na regra.
        /// </summary>
        public static event Action<Vector3[]> AmbushRevealed;

        /// <summary>
        /// O mesmo aviso, só deste serviço: o AmbushFx dele escuta aqui, para que dois serviços na cena não mostrem o
        /// surgimento duas vezes.
        /// </summary>
        public event Action<Vector3[]> Ambushed;

        /// <summary>Só no host: o 20 deu a bênção de dano ao grupo (D-085); o argumento é quantos jogadores a receberam. Para testes e debug.</summary>
        public event Action<int> Blessed;

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

        private void Awake()
        {
            // O surgimento da emboscada (D-068) aparece em todos os clientes: o efeito mora no mesmo objeto do serviço.
            if (!TryGetComponent(out AmbushFx fx))
                fx = gameObject.AddComponent<AmbushFx>();
            fx.Bind(this);
        }

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

        /// <summary>D-082: a carta surge onde caiu o último inimigo da onda; se o ponto não é andável, no ponto andável mais próximo.</summary>
        private void OnWaveCleared(int wavesCleared, Vector3 lastDeathPosition)
        {
            Vector3 position = lastDeathPosition;
            float radius = settings != null ? settings.dropNavSampleRadius : 4f;
            if (NavMesh.SamplePosition(lastDeathPosition, out NavMeshHit hit, radius, NavMesh.AllAreas))
                position = hit.position;
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

        /// <summary>
        /// Host: recomeço da partida depois da queda total (D-084). Some com as cartas do chão e cancela o que ainda esperava
        /// a rolagem acabar (cartas a entregar, bênção do 20, emboscada), para nada chegar a quem já recomeçou sem cartas.
        /// </summary>
        public void ServerResetForRestart()
        {
            if (!IsServer)
                return;

            StopAllCoroutines();
            foreach (var card in FindObjectsByType<FloorCard>(FindObjectsSortMode.None))
            {
                if (card != null && card.IsSpawned)
                    card.NetworkObject.Despawn(true);
            }
            cardsOnFloor.Clear();
            LastRoll = 0;
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
            // D-085: o 20 puro do dado (não a faixa da tabela) dá a bênção ao grupo no coop; o dado e o tema não mudam.
            StartCoroutine(GrantAfter(player, grants, outcome.Danger, roll == D20.Faces, delay));
        }

        private IEnumerator GrantAfter(PlayerCards player, List<CardGrant> grants, bool danger, bool blessGroup, float delay)
        {
            yield return new WaitForSeconds(delay);

            // D-085: a bênção chega junto com a carta, depois que o dado parou (D-047). Dar antes (no clique) acenderia as
            // auras douradas enquanto o dado ainda rola e entregaria o 20 antes da revelação. Vale mesmo que quem pegou tenha saído.
            if (blessGroup)
                ServerBlessGroup();

            if (player == null || !player.IsSpawned)
                yield break;
            player.ServerApplyGrants(grants);
            if (danger)
                ServerAmbush(player.transform.position);
        }

        /// <summary>
        /// Host, D-085: dá a bênção de dano a TODOS os jogadores conectados (vivos ou caídos) se há pelo menos blessingMinPlayers
        /// (2 no coop; no solo o 20 continua só a carta, D-048). Renova a bênção de quem já a tem, sem empilhar.
        /// </summary>
        private void ServerBlessGroup()
        {
            var manager = NetworkManager.Singleton;
            if (!IsServer || settings == null || manager == null)
                return;

            var blessed = new List<PlayerBlessing>();
            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !client.PlayerObject.IsSpawned)
                    continue;
                if (client.PlayerObject.TryGetComponent(out PlayerBlessing blessing))
                    blessed.Add(blessing);
                else
                    Debug.LogWarning("Jogador sem PlayerBlessing: reconstruir o prefab (Game > Setup > Construir Arena).");
            }

            if (blessed.Count < Mathf.Max(1, settings.blessingMinPlayers))
                return;
            foreach (PlayerBlessing blessing in blessed)
                blessing.ServerGrant(settings.blessingDuration, settings.blessingDamageMultiplier);
            Blessed?.Invoke(blessed.Count);
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
            var revealed = new List<Vector3>(count);
            float start = random.Next(0, 360);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                Vector3 position = AmbushPosition(center, angle, radius);
                var def = types[random.Next(0, types.Length)];
                if (def == null)
                    continue;
                var enemy = spawner.ServerSpawn(def, position);
                if (enemy != null)
                    revealed.Add(enemy.transform.position);
            }

            // D-068: todos mostram o surgimento junto com o nascimento (sem atrasar nem mudar os inimigos).
            if (revealed.Count > 0)
                ShowAmbushRpc(revealed.ToArray());
        }

        /// <summary>
        /// Onde nasce um inimigo da emboscada. Com NavMesh: ponto andável perto do sorteado e alcançável a pé a partir do jogador,
        /// sem consumir o sorteio do jogo (a regra do 1 não muda, D-068); tentativas em ordem fixa (a cada trio: 0°, +ângulo, -ângulo;
        /// depois o raio encolhe). Sem NavMesh (cena antiga), o ponto sorteado como antes.
        /// </summary>
        private Vector3 AmbushPosition(Vector3 center, float angle, float radius)
        {
            Vector3 Around(float a, float r) => center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;

            Vector3 plain = Around(angle, radius);
            float sampleRadius = settings != null ? settings.ambushNavSampleRadius : 2.5f;
            if (!NavMesh.SamplePosition(center, out NavMeshHit origin, sampleRadius, NavMesh.AllAreas))
                return plain;

            int tries = Mathf.Max(1, settings != null ? settings.ambushTries : 6);
            float retryAngle = (settings != null ? settings.ambushRetryAngle : 30f) * Mathf.Deg2Rad;
            float retryFactor = settings != null ? settings.ambushRetryRadiusFactor : 0.7f;
            var path = new NavMeshPath();
            for (int t = 0; t < tries; t++)
            {
                float turn = (t % 3) switch { 1 => retryAngle, 2 => -retryAngle, _ => 0f };
                float reach = radius * Mathf.Pow(retryFactor, t / 3);
                if (!NavMesh.SamplePosition(Around(angle + turn, reach), out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                    continue;
                if (NavMesh.CalculatePath(origin.position, hit.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                    return hit.position;
            }
            return origin.position; // nenhuma tentativa serviu: o chão do próprio jogador, que é andável por definição
        }

        [Rpc(SendTo.Everyone)]
        private void ShowRollRpc(ulong roller, int result, float duration)
        {
            RollStarted?.Invoke(roller, result, duration);
        }

        [Rpc(SendTo.Everyone)]
        private void ShowAmbushRpc(Vector3[] positions)
        {
            Ambushed?.Invoke(positions);
            AmbushRevealed?.Invoke(positions);
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
