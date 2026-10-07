using Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A tela escurece e volta na queda total (D-086): preto em tela cheia por cima de toda a UI, que escurece devagar
    /// (com um som grave), segura o preto enquanto o host zera a arena e clareia quando o host avisa. Sem texto. Nada aqui
    /// recebe o mouse. Só desenha: quem decide é o host (MatchReset), que manda dois avisos a todos os clientes.
    /// O componente fica sob a Canvas "UI" e se cria sozinho a cada cena carregada (como o CardRevealFx).
    /// </summary>
    public class WipeFadeUi : MonoBehaviour
    {
        // Por cima de tudo na Canvas "UI" (HUD, dado, revelação de carta); só uma ordem de desenho, não é regra de jogo.
        private const int SortingOrder = 30000;
        private static readonly Color Black = new Color(0.01f, 0.008f, 0.012f, 1f);

        private enum Stage
        {
            Clear,
            Out,
            Black,
            In
        }

        private Image shade;
        private AudioSource audioSource;
        private AudioClip rumble;
        private float rumbleSeconds = -1f;
        private Stage stage = Stage.Clear;
        private float stageTime;
        private float stageDuration;
        private float stageFrom;
        private bool sawSession;

        /// <summary>Quão preta está a tela agora (0 = clara, 1 = preta inteira).</summary>
        public float FadeAlpha { get; private set; }

        /// <summary>Há escurecer, preto ou clarear em andamento (a tela ainda não voltou ao normal).</summary>
        public bool Active => stage != Stage.Clear;

        /// <summary>A tela está preta inteira (segurando o preto).</summary>
        public bool IsBlack => stage == Stage.Black;

        /// <summary>O preto intercepta o mouse? Nunca (para testes).</summary>
        public bool BlocksMouse => shade != null && shade.raycastTarget;

        /// <summary>Quantos escurecer começaram. Para testes.</summary>
        public int FadesStarted { get; private set; }

        /// <summary>Quantos sons graves tocaram. Para testes.</summary>
        public int SoundsPlayed { get; private set; }

        /// <summary>Cria o componente sob a Canvas "UI" da cena, se ainda não existir um. Devolve null se não há a Canvas.</summary>
        public static WipeFadeUi EnsureOnCanvas()
        {
            var existing = FindFirstObjectByType<WipeFadeUi>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            GameObject canvasGo = GameObject.Find("UI");
            Canvas canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            if (canvas == null)
                return null;

            RectTransform root = UiFactory.MakeRect("QuedaTotal", canvas.transform);
            UiFactory.Stretch(root);
            return root.gameObject.AddComponent<WipeFadeUi>();
        }

        // A cada cena carregada (não só a primeira): ao voltar pelo F9 ou recarregar a Arena, a Canvas é outra.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            MatchReset.FadeOutRequested -= OnFadeOutRequested;
            MatchReset.FadeOutRequested += OnFadeOutRequested;
            MatchReset.FadeInRequested -= OnFadeInRequested;
            MatchReset.FadeInRequested += OnFadeInRequested;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOnCanvas();

        private static void OnFadeOutRequested(float seconds, float volume) => EnsureOnCanvas()?.BeginFadeOut(seconds, volume);

        private static void OnFadeInRequested(float seconds) => EnsureOnCanvas()?.BeginFadeIn(seconds);

        private void Awake()
        {
            // Canvas própria por cima de tudo (sortingOrder alto): a revelação de carta e o dado ficam atrás do preto.
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            shade = UiFactory.MakeImage("Preto", transform, new Color(Black.r, Black.g, Black.b, 0f));
            UiFactory.Stretch(shade.rectTransform);
            shade.raycastTarget = false;
            shade.enabled = false;
        }

        private void OnDisable() => ForceClear();

        // ---------- API (o host manda; testes também chamam) ----------

        /// <summary>Começa a escurecer em `seconds` e toca o som grave com o volume dado (0 a 1).</summary>
        public void BeginFadeOut(float seconds, float volume)
        {
            FadesStarted++;
            var manager = NetworkManager.Singleton;
            sawSession = manager != null && manager.IsListening;

            stageFrom = FadeAlpha;
            stageTime = 0f;
            stageDuration = Mathf.Max(0f, seconds);
            stage = Stage.Out;
            if (stageDuration <= 0f)
                SetAlpha(1f, Stage.Black);

            if (volume > 0f)
                PlayRumble(Mathf.Max(0.05f, seconds), volume);
        }

        /// <summary>Começa a clarear em `seconds`, a partir de onde a tela estiver.</summary>
        public void BeginFadeIn(float seconds)
        {
            if (stage == Stage.Clear)
                return;
            stageFrom = FadeAlpha;
            stageTime = 0f;
            stageDuration = Mathf.Max(0f, seconds);
            stage = Stage.In;
            if (stageDuration <= 0f)
                SetAlpha(0f, Stage.Clear);
        }

        // ---------- Quadro a quadro ----------

        private void Update()
        {
            if (stage == Stage.Clear)
                return;

            // A sessão acabou no meio do preto (host saiu, F9): não deixa a tela presa.
            var manager = NetworkManager.Singleton;
            if (sawSession && (manager == null || !manager.IsListening))
            {
                ForceClear();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            switch (stage)
            {
                case Stage.Out:
                {
                    stageTime += dt;
                    float k = stageDuration > 0f ? Mathf.Clamp01(stageTime / stageDuration) : 1f;
                    float alpha = Mathf.Lerp(stageFrom, 1f, Smooth(k));
                    SetAlpha(alpha, k >= 1f ? Stage.Black : Stage.Out); // segura o preto até o host avisar
                    break;
                }
                case Stage.In:
                {
                    stageTime += dt;
                    float k = stageDuration > 0f ? Mathf.Clamp01(stageTime / stageDuration) : 1f;
                    float alpha = Mathf.Lerp(stageFrom, 0f, Smooth(k));
                    SetAlpha(alpha, k >= 1f ? Stage.Clear : Stage.In);
                    break;
                }
            }
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);

        private void SetAlpha(float alpha, Stage next)
        {
            FadeAlpha = Mathf.Clamp01(alpha);
            stage = next;
            if (shade == null)
                return;
            shade.color = new Color(Black.r, Black.g, Black.b, FadeAlpha);
            shade.enabled = FadeAlpha > 0.001f;
        }

        private void ForceClear()
        {
            stage = Stage.Clear;
            sawSession = false;
            FadeAlpha = 0f;
            if (shade != null)
                shade.enabled = false;
        }

        // ---------- Som ----------

        private void PlayRumble(float seconds, float volume)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // som da tela, igual para todos
            }
            if (rumble == null || !Mathf.Approximately(rumbleSeconds, seconds))
            {
                if (rumble != null)
                    Destroy(rumble);
                rumble = WipeSounds.MakeFadeRumble(seconds);
                rumbleSeconds = seconds;
            }
            audioSource.PlayOneShot(rumble, Mathf.Clamp01(volume));
            SoundsPlayed++;
        }
    }
}
