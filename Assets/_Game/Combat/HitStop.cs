using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Combat
{
    /// <summary>
    /// Hit-stop (D-042): uma parada quase total (Time.timeScale = 0.05) por uns 0,05 s de tempo real quando o jogador
    /// local acerta um golpe. SÓ no solo (host sozinho): no coop mexer no tempo de um desincronizaria os outros.
    /// O fim é medido em tempo real (unscaled) e o timeScale volta a 1 em qualquer saída: fim do tempo, sessão que
    /// deixa de ser solo, troca de cena, objeto destruído, fechar o jogo. Nunca deixa o jogo parado.
    /// </summary>
    public static class HitStop
    {
        /// <summary>Velocidade do jogo durante a parada.</summary>
        public const float SlowScale = 0.05f;

        // Entre uma parada e a próxima: o sopro de caldeira acerta a cada 0,1 s e não pode travar o jogo.
        private const float MinGap = 0.2f;

        private static bool active;
        private static float endTime;
        private static float lastEndTime = -10f;

        public static bool IsActive => active;

        /// <summary>Host sozinho na sessão (o único caso em que o hit-stop vale).</summary>
        public static bool IsSolo()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening && manager.IsHost && manager.ConnectedClientsIds.Count == 1;
        }

        /// <summary>Pede uma parada de seconds segundos de tempo real. Ignorada fora do solo ou logo depois de outra.</summary>
        public static void Request(float seconds = 0.05f)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f || !IsSolo())
                return;

            float now = Time.unscaledTime;
            if (!active)
            {
                if (now - lastEndTime < MinGap || !Mathf.Approximately(Time.timeScale, 1f))
                    return; // outra coisa controla o tempo (ou acabou de parar)
                active = true;
                Time.timeScale = SlowScale;
                endTime = now + seconds;
            }
            else
            {
                endTime = Mathf.Max(endTime, now + seconds);
            }
        }

        /// <summary>Volta o jogo à velocidade normal na hora.</summary>
        public static void End()
        {
            if (!active)
                return;
            active = false;
            lastEndTime = Time.unscaledTime;
            Time.timeScale = 1f;
        }

        private static void Tick()
        {
            if (active && (Time.unscaledTime >= endTime || !IsSolo()))
                End();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = false;
            lastEndTime = -10f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRunner()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
            Application.quitting -= End;
            Application.quitting += End;

            var go = new GameObject("HitStop (runtime)") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<HitStopRunner>();
        }

        private static void OnSceneChanged(Scene previous, Scene next) => End();

        /// <summary>Objeto escondido que confere o fim da parada com o tempo real.</summary>
        private sealed class HitStopRunner : MonoBehaviour
        {
            private void Update() => Tick();

            private void OnDisable() => End();

            private void OnDestroy() => End();

            private void OnApplicationQuit() => End();
        }
    }
}
