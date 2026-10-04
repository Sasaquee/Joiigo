using System;
using System.Collections;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Início solo (D-018): ao abrir a arena, o jogador vira host sozinho e a largada já está dada,
    /// sem passar pela tela de conexão. Não faz nada se uma sessão já estiver rodando ou se a linha
    /// de comando pedir -autohost / -autojoin (DevAutoConnect assume).
    ///
    /// Testes: carregam a Arena e chamam session.Host() por conta própria. Por isso
    /// <see cref="SuppressAutoStart"/> precisa estar true antes de carregar a cena
    /// (ArenaTestScene.Load e o SetUpFixture do PlayMode fazem isso).
    /// </summary>
    public class SoloBootstrap : MonoBehaviour
    {
        /// <summary>Desliga o início solo (usado pelos testes). Estático: vale para a próxima cena carregada.</summary>
        public static bool SuppressAutoStart;

        [SerializeField] private NetSession session;
        [SerializeField] private MatchState matchState;
        [SerializeField] private bool autoStartSolo = true;

        private const float SpawnTimeout = 5f;

        public void Configure(NetSession newSession, MatchState newMatchState)
        {
            session = newSession;
            matchState = newMatchState;
        }

        private IEnumerator Start()
        {
            if (!autoStartSolo || SuppressAutoStart || session == null || HasDevConnectArgs())
                yield break;

            // Espera um quadro: assim todos os Start (inclusive o da tela de conexão, que mostra o painel
            // se não houver sessão) já rodaram, e o evento Connected esconde o painel depois.
            yield return null;
            if (SuppressAutoStart || session.IsRunning)
                yield break;

            if (!session.Host())
            {
                Debug.LogError("[SoloBootstrap] Não foi possível iniciar o host solo.");
                yield break;
            }

            // O MatchState zera a largada ao nascer, então a largada vem depois do spawn.
            float timeout = SpawnTimeout;
            while (matchState != null && !matchState.IsSpawned && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (matchState != null && matchState.IsSpawned)
                matchState.ServerStart();
            else
                Debug.LogError("[SoloBootstrap] O MatchState não nasceu a tempo; sem largada.");
        }

        private static bool HasDevConnectArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            return Array.IndexOf(args, "-autohost") >= 0 || Array.IndexOf(args, "-autojoin") >= 0;
        }
    }
}
