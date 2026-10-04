using Unity.Netcode;
using UnityEngine;

namespace Game.Cameras
{
    /// <summary>
    /// Tremor de câmera (D-042). Qualquer efeito chama Add/AddAt; a câmera (CameraFollow ou a câmera pixel do líder)
    /// só lê CurrentOffset e soma à própria posição. O tremor é ruído de Perlin que decai, no plano da tela
    /// (direita e cima da câmera), em metros do mundo. Usa o tempo real, então continua durante o hit-stop.
    /// Um objeto escondido (criado sozinho no início do jogo) atualiza o valor uma vez por quadro, antes dos LateUpdate.
    /// </summary>
    public static class CameraShake
    {
        /// <summary>Intensidades de referência em metros (a ~14 m da câmera, 1 pixel ≈ 3 cm).</summary>
        public const float Small = 0.07f;
        public const float Medium = 0.16f;
        public const float Large = 0.30f;

        private const float MaxStrength = 0.45f;
        private const float Frequency = 24f;

        private static float amplitude;
        private static float total = 1f;
        private static float remaining;
        private static float seed;

        /// <summary>Deslocamento atual da câmera, em coordenadas do mundo. Zero quando não há tremor.</summary>
        public static Vector3 CurrentOffset { get; private set; }

        /// <summary>Soma um tremor. Vários ao mesmo tempo não se acumulam sem limite: vale o mais forte, com um reforço leve.</summary>
        public static void Add(float strength, float duration)
        {
            if (!float.IsFinite(strength) || !float.IsFinite(duration) || strength <= 0f || duration <= 0f)
                return;

            strength = Mathf.Min(strength, MaxStrength);
            float current = CurrentAmplitude();
            float stronger = Mathf.Max(current, strength);
            float weaker = Mathf.Min(current, strength);
            amplitude = Mathf.Min(MaxStrength, stronger + weaker * 0.3f);
            total = Mathf.Max(duration, remaining);
            remaining = total;
        }

        /// <summary>Tremor de algo que aconteceu em um ponto do mundo: some com a distância até o jogador local.</summary>
        public static void AddAt(Vector3 worldPosition, float strength, float duration, float maxDistance = 16f)
        {
            if (!float.IsFinite(worldPosition.x) || !float.IsFinite(worldPosition.z))
                return;

            float k = 1f;
            if (TryGetViewerPosition(out Vector3 viewer))
            {
                float dx = worldPosition.x - viewer.x;
                float dz = worldPosition.z - viewer.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                k = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, maxDistance));
            }
            if (k > 0.05f)
                Add(strength * k, duration);
        }

        /// <summary>Zera o tremor (troca de cena, testes).</summary>
        public static void Clear()
        {
            amplitude = 0f;
            remaining = 0f;
            CurrentOffset = Vector3.zero;
        }

        private static float CurrentAmplitude()
        {
            if (remaining <= 0f || total <= 0f)
                return 0f;
            float f = remaining / total;
            return amplitude * f * f;
        }

        private static bool TryGetViewerPosition(out Vector3 position)
        {
            position = default;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || manager.LocalClient == null)
                return false;
            NetworkObject player = manager.LocalClient.PlayerObject;
            if (player == null)
                return false;
            position = player.transform.position;
            return true;
        }

        private static void Tick(float dt)
        {
            if (remaining <= 0f)
            {
                if (CurrentOffset != Vector3.zero)
                    CurrentOffset = Vector3.zero;
                return;
            }

            remaining = Mathf.Max(0f, remaining - dt);
            float strength = CurrentAmplitude();
            if (strength <= 0.0005f)
            {
                remaining = 0f;
                CurrentOffset = Vector3.zero;
                return;
            }

            float time = Time.unscaledTime * Frequency;
            float nx = Mathf.PerlinNoise(seed, time) * 2f - 1f;
            float ny = Mathf.PerlinNoise(seed + 37.1f, time) * 2f - 1f;

            Vector3 right = Vector3.right;
            Vector3 up = Vector3.up;
            Camera cam = Camera.main;
            if (cam != null)
            {
                right = cam.transform.right;
                up = cam.transform.up;
            }
            CurrentOffset = (right * nx + up * ny) * strength;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            amplitude = 0f;
            total = 1f;
            remaining = 0f;
            seed = 0f;
            CurrentOffset = Vector3.zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRunner()
        {
            seed = Random.Range(0f, 100f);
            var go = new GameObject("CameraShake (runtime)") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CameraShakeRunner>();
        }

        /// <summary>Objeto escondido que avança o tremor. Roda cedo, para o valor estar pronto antes dos LateUpdate das câmeras.</summary>
        [DefaultExecutionOrder(-1000)]
        private sealed class CameraShakeRunner : MonoBehaviour
        {
            private void Update() => Tick(Time.unscaledDeltaTime);

            private void OnDisable() => Clear();
        }
    }
}
