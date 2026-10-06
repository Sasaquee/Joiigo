using System.Collections.Generic;
using Game.Arena;
using Game.Combat;
using Game.Core.View;
using Game.Enemies;
using Game.Net;
using UnityEngine;

namespace Game.Cameras
{
    /// <summary>
    /// Lado C# da translucidez dos prédios (D-076, D-079). Na câmera de jogo, depois do PixelCamera: a cada
    /// `targetScanInterval` junta jogadores e inimigos vivos; a cada quadro faz Linecast da câmera a três alturas de cada
    /// alvo na camada Cenario (só os tapados abrem buraco, com fade) e publica, para o shader LitVazado, as globais
    /// `_VazadoContagem`, `_VazadoAlvos[12]` (xy = posição do alvo em UV de tela, z = raio em fração da altura da tela,
    /// w = profundidade em metros), `_VazadoMargem`, `_VazadoPerto` e `_VazadoFantasma`.
    /// Cada cliente usa a própria câmera: nada vai pela rede. Sem alocação por quadro (arrays pré-alocados).
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [RequireComponent(typeof(Camera))]
    public class SeeThroughDriver : MonoBehaviour
    {
        public const string CountGlobalName = "_VazadoContagem";
        public const string TargetsGlobalName = "_VazadoAlvos";
        public const string MarginGlobalName = "_VazadoMargem";
        public const string NearGlobalName = "_VazadoPerto";
        public const string GhostGlobalName = "_VazadoFantasma";

        private static readonly int CountId = Shader.PropertyToID(CountGlobalName);
        private static readonly int TargetsId = Shader.PropertyToID(TargetsGlobalName);
        private static readonly int MarginId = Shader.PropertyToID(MarginGlobalName);
        private static readonly int NearId = Shader.PropertyToID(NearGlobalName);
        private static readonly int GhostId = Shader.PropertyToID(GhostGlobalName);

        // Capacidade das listas (jogadores + inimigos + alvos manuais). Passando disso, os que sobram não abrem buraco.
        private const int Capacity = 48;
        // Menor abertura que ainda conta como buraco (evita publicar alvo já fechado).
        private const float MinOpen = 0.001f;
        // Margem da tela (em UV) além da qual o alvo nem é testado.
        private const float ScreenSlack = 0.5f;

        [SerializeField] private SeeThroughSettings settings;

        private struct Entry
        {
            public Transform Transform;
            public bool IsPlayer;
            public float WorldRadius;
            public float Open;
            public bool Occluded;
            public float Depth;
            public Vector2 Uv;
        }

        private struct ManualTarget
        {
            public Transform Transform;
            public float WorldRadius;
            public bool IsPlayer;
        }

        private Camera cam;
        private SeeThroughSettings defaults;
        private Entry[] entries = new Entry[Capacity];
        private Entry[] scratch = new Entry[Capacity];
        private int entryCount;
        private readonly List<ManualTarget> manual = new List<ManualTarget>();
        private readonly float[] scores = new float[Capacity];
        private readonly int[] candidates = new int[Capacity];
        private readonly int[] selected = new int[SeeThroughMath.MaxShaderTargets];
        private readonly Vector4[] published = new Vector4[SeeThroughMath.MaxShaderTargets];
        private float scanTimer;
        private bool scanned;
        private int publishedCount;

        public SeeThroughSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Liga os números (como as outras classes do projeto). Nulo usa os padrões do SeeThroughSettings.</summary>
        public void Configure(SeeThroughSettings value) => settings = value;

        /// <summary>Quantos buracos foram publicados no último quadro.</summary>
        public int PublishedCount => publishedCount;

        private SeeThroughSettings Active
        {
            get
            {
                if (settings != null)
                    return settings;
                if (defaults == null)
                {
                    defaults = ScriptableObject.CreateInstance<SeeThroughSettings>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            scanned = false;
            Publish(0);
        }

        private void OnDisable() => Publish(0);

        private void OnDestroy()
        {
            if (defaults != null)
                Destroy(defaults);
        }

        /// <summary>Alvo extra (além de jogadores e inimigos), p.ex. para testes ou um NPC. Vale até <see cref="RemoveManualTarget"/>.</summary>
        public void AddManualTarget(Transform target, float worldRadius, bool isPlayer = true)
        {
            if (target == null)
                return;
            RemoveManualTarget(target);
            manual.Add(new ManualTarget { Transform = target, WorldRadius = worldRadius, IsPlayer = isPlayer });
            scanned = false;
        }

        public void RemoveManualTarget(Transform target)
        {
            for (int i = manual.Count - 1; i >= 0; i--)
            {
                if (manual[i].Transform == target)
                    manual.RemoveAt(i);
            }
            scanned = false;
        }

        /// <summary>Alvo conhecido pelo driver e tapado neste quadro (qualquer das três alturas bloqueada).</summary>
        public bool IsOccluded(Transform target)
        {
            for (int i = 0; i < entryCount; i++)
            {
                if (entries[i].Transform == target)
                    return entries[i].Occluded;
            }
            return false;
        }

        /// <summary>Refaz a lista de jogadores e inimigos vivos agora (o driver já faz sozinho a cada intervalo).</summary>
        public void Rescan()
        {
            var cfg = Active;
            int n = 0;

            // Raro (a cada intervalo), então a busca por tipo é aceitável; o que roda todo quadro não aloca.
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p.TryGetComponent(out NetworkHealth health) && !health.IsAlive)
                    continue;
                n = Collect(n, p.transform, true, cfg.playerRadius);
            }

            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i].IsAlive)
                    n = Collect(n, enemies[i].transform, false, cfg.enemyRadius);
            }

            for (int i = 0; i < manual.Count; i++)
                n = Collect(n, manual[i].Transform, manual[i].IsPlayer, manual[i].WorldRadius);

            (entries, scratch) = (scratch, entries);
            entryCount = n;
            scanned = true;
        }

        // Escreve o alvo em `scratch`, herdando a abertura do buraco se ele já estava na lista anterior.
        private int Collect(int n, Transform target, bool isPlayer, float worldRadius)
        {
            if (target == null || n >= Capacity)
                return n;
            for (int k = 0; k < n; k++)
            {
                if (scratch[k].Transform == target)
                    return n;
            }
            var entry = new Entry { Transform = target, IsPlayer = isPlayer, WorldRadius = worldRadius };
            for (int k = 0; k < entryCount; k++)
            {
                if (entries[k].Transform == target)
                {
                    entry.Open = entries[k].Open;
                    break;
                }
            }
            scratch[n] = entry;
            return n + 1;
        }

        private void LateUpdate()
        {
            if (cam == null)
                cam = GetComponent<Camera>();
            if (cam == null || !cam.isActiveAndEnabled)
            {
                Publish(0);
                return;
            }

            var cfg = Active;
            float dt = Time.unscaledDeltaTime;
            scanTimer -= dt;
            if (!scanned || scanTimer <= 0f)
            {
                Rescan();
                scanTimer = cfg.targetScanInterval;
            }

            Vector3 camPos = cam.transform.position;
            float fov = cam.fieldOfView;
            Vector3 heights = cfg.checkHeights;

            int candidateCount = 0;
            for (int i = entryCount - 1; i >= 0; i--)
            {
                if (entries[i].Transform == null)
                {
                    entries[i] = entries[--entryCount];
                    continue;
                }
            }

            for (int i = 0; i < entryCount; i++)
            {
                ref Entry e = ref entries[i];
                Vector3 pos = e.Transform.position;
                Vector3 vp = cam.WorldToViewportPoint(pos + Vector3.up * heights.y);
                e.Depth = vp.z;
                e.Uv = new Vector2(vp.x, vp.y);

                bool onScreen = vp.z > 0.1f
                    && vp.x > -ScreenSlack && vp.x < 1f + ScreenSlack
                    && vp.y > -ScreenSlack && vp.y < 1f + ScreenSlack;
                e.Occluded = onScreen && SeeThroughMath.IsOccluded(
                    Blocked(camPos, pos, heights.x), Blocked(camPos, pos, heights.y), Blocked(camPos, pos, heights.z));
                e.Open = SeeThroughMath.StepFade(e.Open, e.Occluded, dt, cfg.fadeInSeconds, cfg.fadeOutSeconds);

                if (e.Open > MinOpen && vp.z > 0.1f)
                {
                    scores[candidateCount] = SeeThroughMath.Relevance(e.IsPlayer, e.Open, e.Depth);
                    candidates[candidateCount] = i;
                    candidateCount++;
                }
            }

            int limit = Mathf.Min(cfg.maxTargets, SeeThroughMath.MaxShaderTargets);
            int count = SeeThroughMath.SelectTop(scores, candidateCount, limit, selected);
            for (int k = 0; k < SeeThroughMath.MaxShaderTargets; k++)
            {
                if (k >= count)
                {
                    published[k] = Vector4.zero;
                    continue;
                }
                ref Entry e = ref entries[candidates[selected[k]]];
                float radius = SeeThroughMath.ScreenRadius(e.WorldRadius, e.Depth, fov) * SeeThroughMath.Smooth(e.Open);
                published[k] = new Vector4(e.Uv.x, e.Uv.y, radius, e.Depth);
            }

            Shader.SetGlobalFloat(MarginId, cfg.depthMargin);
            Shader.SetGlobalFloat(NearId, cfg.nearCut);
            Shader.SetGlobalFloat(GhostId, cfg.ghostOpacity);
            Shader.SetGlobalVectorArray(TargetsId, published);
            Shader.SetGlobalFloat(CountId, count);
            publishedCount = count;
        }

        private static bool Blocked(Vector3 from, Vector3 feet, float height)
            => Physics.Linecast(from, feet + Vector3.up * height, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);

        // Zera a contagem (sem câmera, desligado ou sem alvo): o shader deixa de recortar.
        private void Publish(int count)
        {
            for (int k = 0; k < published.Length; k++)
                published[k] = Vector4.zero;
            Shader.SetGlobalVectorArray(TargetsId, published);
            Shader.SetGlobalFloat(CountId, count);
            publishedCount = count;
        }
    }
}
