using System;
using System.Collections.Generic;
using Game.Cards;
using Game.Combat;
using Game.Core.Combat;
using Game.Core.Session;
using Game.Dice;
using Game.Enemies;
using Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Recomeço da partida depois da queda total (D-084, D-086, D-087). Só o host decide: quando TODOS os jogadores conectados
    /// estão caídos ao mesmo tempo (no solo, o único jogador), avisa todos os clientes para escurecer a tela; no fim do escurecer
    /// zera a arena (inimigos, projéteis, minas, cartas do chão, onda 1), leva todos de volta ao spawn de pé e sem NENHUMA carta;
    /// segura o preto e avisa para clarear. No coop a partida volta a "não iniciada" (portões apagados, o host puxa a alavanca
    /// de novo); no solo a largada é automática de novo (D-018). Os clientes só desenham o escurecer (WipeFadeUi).
    ///
    /// Mora no objeto de cena "Sessao" (o mesmo NetworkObject do MatchState e do CardDropService; MatchResetSetup coloca).
    /// Só dispara com a partida iniciada: na sala de espera ninguém recomeça nada.
    /// </summary>
    public class MatchReset : NetworkBehaviour
    {
        [SerializeField] private MatchSettings settings;
        [SerializeField] private MatchState match;
        [SerializeField] private WaveSpawner spawner;

        private readonly WipeDetector detector = new WipeDetector();
        private readonly WipeSequence idleSequence = new WipeSequence(0f, 0f);
        private WipeSequence sequence;
        private readonly List<PlayerLife> players = new List<PlayerLife>();
        private readonly List<LifeState> states = new List<LifeState>();
        private MatchSettings fallbackSettings;

        /// <summary>Em todos: a queda total começou; escurecer em (segundos, volume do som grave). Escuta o WipeFadeUi.</summary>
        public static event Action<float, float> FadeOutRequested;

        /// <summary>Em todos: a arena já zerou; clarear em (segundos). Escuta o WipeFadeUi.</summary>
        public static event Action<float> FadeInRequested;

        /// <summary>
        /// Só no host: a arena acabou de ser zerada (jogadores de pé no spawn, sem cartas, sem inimigos). Ponto de extensão para
        /// quem guarda estado de partida fora daqui.
        /// </summary>
        public static event Action ServerRestarted;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Só teste: jogadores extras (instâncias do prefab do jogador spawnadas pelo host) que contam como jogadores conectados,
        /// já que um teste só tem um cliente de verdade. Não existe em build de jogo.
        /// </summary>
        public static Func<IReadOnlyList<PlayerLife>> TestExtraPlayers;
#endif

        public MatchSettings Settings
        {
            get => settings != null ? settings : (fallbackSettings ??= ScriptableObject.CreateInstance<MatchSettings>());
            set => settings = value;
        }

        /// <summary>Fase da sequência do host (Idle fora de uma queda total).</summary>
        public WipePhase Phase => (sequence ?? idleSequence).Phase;

        /// <summary>Host: quedas totais que começaram nesta sessão. Para testes e debug.</summary>
        public int WipesStarted { get; private set; }

        /// <summary>Host: recomeços concluídos (tela já clareando). Para testes e debug.</summary>
        public int RestartsCompleted { get; private set; }

        /// <summary>Host: o último recomeço concluído foi no solo (largada automática)?</summary>
        public bool LastRestartWasSolo { get; private set; }

        public void Configure(MatchSettings newSettings, MatchState newMatch, WaveSpawner newSpawner)
        {
            settings = newSettings;
            match = newMatch;
            spawner = newSpawner;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            detector.Reset();
            sequence = null;
            WipesStarted = 0;
            RestartsCompleted = 0;
            LastRestartWasSolo = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            CollectPlayers();
            bool allDowned = detector.Update(states);

            if (sequence != null && sequence.IsRunning)
            {
                // Uma vez começada, a sequência sempre termina (WipeSequence): ninguém levanta ninguém numa queda total.
                switch (sequence.Tick(Time.unscaledDeltaTime))
                {
                    case WipeStep.ResetNow:
                        ServerResetArena();
                        break;
                    case WipeStep.Finished:
                        ServerFinishRestart();
                        break;
                }
                return;
            }

            if (allDowned && match != null && match.IsStarted)
                ServerBeginWipe();
        }

        private void ServerBeginWipe()
        {
            MatchSettings s = Settings;
            sequence = s.CreateSequence();
            sequence.Begin();
            WipesStarted++;
            WipeFadeOutRpc(s.wipeFadeSeconds, s.wipeSoundVolume);
        }

        /// <summary>No escuro: zera tudo o que a partida acumulou.</summary>
        private void ServerResetArena()
        {
            spawner?.ServerResetForRestart();

            // Projéteis e minas no ar e cartas no chão. (Os inimigos e a fila das bocas ficam por conta do spawner.)
            DespawnAll<EnemyProjectile>();
            DespawnAll<Mine>();
            var drops = FindFirstObjectByType<CardDropService>();
            if (drops != null)
                drops.ServerResetForRestart();

            foreach (PlayerLife life in players)
            {
                if (life == null || !life.IsSpawned)
                    continue;
                life.ServerResetForRestart();
                if (life.TryGetComponent(out PlayerCards cards))
                    cards.ServerClearAll();
                if (life.TryGetComponent(out PlayerBlessing blessing))
                    blessing.ServerClear(); // a bênção do 20 (D-085) também é estado da partida
            }

            // Coop: sala de espera, portões apagados, o host puxa a alavanca (D-087). No solo isto é só um intervalo curto
            // sem partida (a largada volta em ServerFinishRestart).
            if (match != null)
                match.ServerResetStarted();
            detector.Reset();
            ServerRestarted?.Invoke();
        }

        /// <summary>O preto acabou: clareia e, no solo, a partida recomeça sozinha (D-018, D-087).</summary>
        private void ServerFinishRestart()
        {
            bool solo = WipeRule.IsSolo(players.Count);
            LastRestartWasSolo = solo;
            RestartsCompleted++;
            WipeFadeInRpc(Settings.wipeFadeInSeconds);
            if (solo && match != null && match.IsSpawned)
                match.ServerStart();
        }

        /// <summary>Jogadores conectados (com o PlayerLife nascido) e o estado de vida de cada um.</summary>
        private void CollectPlayers()
        {
            players.Clear();
            states.Clear();

            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer)
            {
                var clients = manager.ConnectedClientsList;
                for (int i = 0; i < clients.Count; i++)
                {
                    NetworkObject obj = clients[i]?.PlayerObject;
                    if (obj != null && obj.IsSpawned && obj.TryGetComponent(out PlayerLife life))
                        players.Add(life);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestExtraPlayers != null)
            {
                IReadOnlyList<PlayerLife> extra = TestExtraPlayers();
                for (int i = 0; extra != null && i < extra.Count; i++)
                {
                    if (extra[i] != null && extra[i].IsSpawned && !players.Contains(extra[i]))
                        players.Add(extra[i]);
                }
            }
#endif

            for (int i = 0; i < players.Count; i++)
                states.Add(players[i].IsDowned ? LifeState.Downed : LifeState.Alive);
        }

        private static void DespawnAll<T>() where T : NetworkBehaviour
        {
            foreach (T item in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (item != null && item.IsSpawned)
                    item.NetworkObject.Despawn(true);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void WipeFadeOutRpc(float fadeSeconds, float soundVolume)
        {
            FadeOutRequested?.Invoke(fadeSeconds, soundVolume);
        }

        [Rpc(SendTo.Everyone)]
        private void WipeFadeInRpc(float fadeInSeconds)
        {
            FadeInRequested?.Invoke(fadeInSeconds);
        }
    }
}
