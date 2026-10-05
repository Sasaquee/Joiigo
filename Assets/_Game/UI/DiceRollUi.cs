using System.Collections.Generic;
using Game.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// D20 grande na tela (D-047): quando alguém pega uma carta do chão, o dado de latão e cristal rola no centro
    /// da tela, desacelera e assenta com o número sorteado de frente e em pé. O jogo segue por trás; nada aqui recebe o mouse.
    /// O dado é 3D (Art/Models/Dice/D20.fbx) num "palco" longe da arena, renderizado numa textura transparente do
    /// tamanho real em que aparece na tela, com bordas suaves (D-058). Todos os jogadores veem a mesma rolagem
    /// (CardDropService.RollStarted).
    /// O 1 é teatral (D-068): perto do fim da rolagem o dado e as luzes do palco viram sangue e carmesim, um véu escuro
    /// com bordas vermelhas em faixas escurece o jogo (primeiro filho da Canvas: a HUD e o dado ficam por cima), e um
    /// baque grave toca quando ele assenta. O véu segura até depois da emboscada e de o dado sumir, e some. Nos outros números nada disso acontece. Números em DiceSettings.
    /// Criado pelo construtor da arena sob a Canvas "UI".
    /// </summary>
    public class DiceRollUi : MonoBehaviour
    {
        // Palco do dado: longe da arena e da cidade, onde nenhuma outra câmera olha.
        private static readonly Vector3 StagePosition = new Vector3(0f, -500f, 0f);
        private const float CameraDistance = 2.6f;
        private const float CameraFov = 30f;
        // Tamanho do dado na tela (fração da altura) e suavização das bordas da textura do palco.
        private const float ScreenHeightFraction = 0.42f;
        private const int TextureAntiAliasing = 8;
        private const int MinTextureSide = 64;
        // Animação (frações da duração recebida).
        private const float SettleStart = 0.72f;
        private const float FadeTime = 0.35f;
        private const float HoldAfter = 0.9f;

        // O resultado que é crítico de perigo (D-048, D-049).
        private const int CriticalFace = 1;
        // Bordas vermelhas do 1: textura gerada aqui, em faixas suaves como a luz do jogo (D-057).
        private const int VignetteSize = 256;
        private const int VignetteBands = 4;
        private const float VignetteInner = 0.45f;   // do centro (0) à borda (1): até aqui a tela fica limpa
        private const float VignetteOuter = 1.25f;   // daqui para fora a borda está cheia (cantos ≈ 1,41)
        private const float PulseBoost = 0.6f;       // quanto o pulso do baque reforça as bordas
        // Latão de referência: o metal avermelhado mantém o claro/escuro de cada peça em relação a ele.
        private const float BrassValue = 0.79f;

        // Luzes do palco: quente de frente e contorno ciano atrás (latão e cristal, Pilar 4); no 1, sangue e carmesim.
        private static readonly Color KeyLightColor = new Color(1f, 0.85f, 0.6f);
        private static readonly Color RimLightColor = new Color(0.35f, 0.9f, 1f);
        private static readonly Color CriticalKeyLight = new Color(1f, 0.42f, 0.32f);
        private static readonly Color CriticalRimLight = new Color(1f, 0.08f, 0.1f);
        // No 1: o latão escurece em sangue e o cristal acende em carmesim; o número continua o mais claro do dado.
        private static readonly Color CriticalMetal = new Color(0.42f, 0.06f, 0.06f);
        private static readonly Color CriticalCrystal = new Color(1f, 0.3f, 0.22f);
        private static readonly Color CriticalCrystalGlow = new Color(1.5f, 0.2f, 0.14f);
        private static readonly Color VeilColor = new Color(0.03f, 0f, 0.01f);
        private static readonly Color VignetteColor = new Color(0.5f, 0.02f, 0.05f);
        private static readonly Color VignettePulseColor = new Color(0.85f, 0.06f, 0.08f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private GameObject diePrefab;
        [Tooltip("Números do 1 teatral (D-068). Vazio: usa os do CardDropService da cena.")]
        [SerializeField] private DiceSettings settings;

        /// <summary>Material do dado copiado para esta tela, com as cores originais para voltar do vermelho.</summary>
        private struct TintedMaterial
        {
            public Material Material;
            public Color Base;
            public Color Emission;
            public Color Target;
            public bool Glows;
        }

        private RawImage image;
        private RenderTexture texture;
        private Camera stageCamera;
        private Transform die;
        private Light keyLight;
        private Light rimLight;
        private readonly Dictionary<int, Quaternion> faceUp = new Dictionary<int, Quaternion>();
        private readonly List<TintedMaterial> dieMaterials = new List<TintedMaterial>();

        private bool playing;
        private float time;
        private float duration;
        private int result;
        private Vector3 spinAxis;
        private float spinSpeed;
        private Quaternion settleFrom;
        private Quaternion settleTo;
        private bool settling;

        // Crítico 1 (D-068).
        private RectTransform veil;
        private Image veilShade;
        private RawImage vignette;
        private Texture2D vignetteTexture;
        private bool critical;
        private float criticalTime;
        private float criticalDuration;
        private float veilStrength;
        private float appliedTint;
        private bool thudPlayed;
        private float pulseAge;
        private AudioSource audioSource;
        private AudioClip thud;
        private DiceSettings fallbackSettings;

        /// <summary>Número mostrado na rolagem atual (0 se nenhuma). Para testes.</summary>
        public int ShowingResult => playing ? result : 0;
        public bool IsPlaying => playing;
        /// <summary>Textura onde o palco desenha o dado (para testes).</summary>
        public RenderTexture Texture => texture;
        /// <summary>O 1 teatral está em cena (dado vermelho e/ou véu). Para testes.</summary>
        public bool IsCritical => critical;
        /// <summary>O véu escuro do 1 está na tela. Para testes.</summary>
        public bool VeilActive => veil != null && veil.gameObject.activeSelf;
        /// <summary>Força do véu agora (0 a 1). Para testes.</summary>
        public float VeilStrength => critical ? veilStrength : 0f;
        /// <summary>Quanto o dado está vermelho (0 = latão e ciano, 1 = sangue e carmesim). Para testes.</summary>
        public float CriticalTint => appliedTint;
        /// <summary>Quantos baques graves tocaram. Para testes.</summary>
        public int ThudsPlayed { get; private set; }
        /// <summary>O véu do 1: primeiro filho da Canvas, abaixo da HUD (barra de cartas) e do dado. Para testes.</summary>
        public RectTransform Veil => veil;
        /// <summary>O dado e as luzes do palco (para os testes conferirem a volta ao latão e ao ciano).</summary>
        public Transform Die => die;
        public Light KeyLight => keyLight;
        public Light RimLight => rimLight;

        public void Configure(GameObject prefab, DiceSettings diceSettings = null)
        {
            diePrefab = prefab;
            if (diceSettings != null)
                settings = diceSettings;
        }

        private DiceSettings Settings
        {
            get
            {
                if (settings != null)
                    return settings;
                if (fallbackSettings != null)
                    return fallbackSettings;
                var service = FindFirstObjectByType<CardDropService>();
                if (service != null && service.Settings != null)
                    return settings = service.Settings;
                fallbackSettings = ScriptableObject.CreateInstance<DiceSettings>();
                fallbackSettings.hideFlags = HideFlags.HideAndDontSave;
                return fallbackSettings;
            }
        }

        private void Awake()
        {
            // Tudo do 1 fica pronto antes de rolar: gerar o som e a textura na hora do baque engasgava o quadro.
            EnsureVeil();
            EnsureAudio();
        }

        private void OnEnable() => CardDropService.RollStarted += OnRollStarted;

        private void OnDisable()
        {
            CardDropService.RollStarted -= OnRollStarted;
            playing = false;
            if (image != null)
                image.enabled = false;
            EndCritical();
        }

        private void OnDestroy()
        {
            // O véu mora na Canvas, fora desta tela: sai junto com ela.
            if (veil != null && veil.parent != transform)
                Destroy(veil.gameObject);
            if (texture != null)
                texture.Release();
            if (stageCamera != null)
                Destroy(stageCamera.transform.parent.gameObject);
            foreach (var m in dieMaterials)
                if (m.Material != null)
                    Destroy(m.Material);
            dieMaterials.Clear();
            if (vignetteTexture != null)
                Destroy(vignetteTexture);
            if (thud != null)
                Destroy(thud);
            if (fallbackSettings != null)
                Destroy(fallbackSettings);
        }

        private void EnsureStage()
        {
            if (die != null || diePrefab == null)
                return;

            var stage = new GameObject("PalcoDoDado").transform;
            stage.position = StagePosition;

            die = Instantiate(diePrefab, stage).transform;
            die.localPosition = Vector3.zero;
            die.localRotation = Quaternion.identity;
            BuildFaceTable();
            PrepareDieMaterials();

            var camGo = new GameObject("CameraDoDado");
            camGo.transform.SetParent(stage, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);
            camGo.transform.localRotation = Quaternion.identity;
            stageCamera = camGo.AddComponent<Camera>();
            stageCamera.fieldOfView = CameraFov;
            stageCamera.nearClipPlane = 0.5f;
            stageCamera.farClipPlane = CameraDistance + 2f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            // Sem HDR: o buffer HDR do URP não tem canal alfa e o fundo transparente virava um quadrado preto.
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = true;
            stageCamera.enabled = false;

            // Luz quente de frente e um contorno ciano atrás: latão e cristal (Pilar 4).
            keyLight = AddLight(stage, new Vector3(-1.2f, 1.6f, -2f), KeyLightColor, 6f);
            rimLight = AddLight(stage, new Vector3(1.4f, -0.4f, 1.6f), RimLightColor, 5f);

            EnsureTexture();

            var rect = UiFactory.MakeRect("Dado", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.52f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = false;
        }

        /// <summary>Textura do tamanho, em pixels, que o dado ocupa na tela (refeita se a janela mudar de tamanho).</summary>
        private void EnsureTexture()
        {
            int side = Mathf.Max(MinTextureSide, Mathf.RoundToInt(Screen.height * ScreenHeightFraction));
            if (texture != null && texture.width == side)
                return;
            if (texture != null)
            {
                stageCamera.targetTexture = null;
                texture.Release();
                Destroy(texture);
            }
            texture = new RenderTexture(side, side, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                antiAliasing = TextureAntiAliasing
            };
            texture.Create();
            stageCamera.targetTexture = texture;
            if (image != null)
                image.texture = texture;
        }

        private static Light AddLight(Transform parent, Vector3 localPosition, Color color, float intensity)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 6f;
            return light;
        }

        /// <summary>
        /// Copia os materiais do dado só para esta tela (o latão e o cristal do resto do jogo não mudam) e guarda as
        /// cores originais. O que tem emissão é cristal (os números); o resto é metal.
        /// </summary>
        private void PrepareDieMaterials()
        {
            dieMaterials.Clear();
            appliedTint = 0f;
            foreach (Renderer r in die.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null)
                        continue;
                    var copy = new Material(shared[i]) { name = shared[i].name + " (dado)" };
                    Color baseColor = copy.HasProperty(BaseColorId) ? copy.GetColor(BaseColorId)
                        : copy.HasProperty(ColorId) ? copy.GetColor(ColorId) : Color.white;
                    Color emission = copy.HasProperty(EmissionColorId) ? copy.GetColor(EmissionColorId) : Color.black;
                    bool glows = copy.IsKeywordEnabled("_EMISSION") && emission.maxColorComponent > 0.05f;
                    float value = Mathf.Clamp(baseColor.maxColorComponent / BrassValue, 0.3f, 1.5f);
                    Color target = glows ? CriticalCrystal
                        : new Color(CriticalMetal.r * value, CriticalMetal.g * value, CriticalMetal.b * value, baseColor.a);
                    dieMaterials.Add(new TintedMaterial { Material = copy, Base = baseColor, Emission = emission, Target = target, Glows = glows });
                    shared[i] = copy;
                }
                r.sharedMaterials = shared;
            }
        }

        /// <summary>Leva o dado e as luzes do palco do latão/ciano (0) ao sangue/carmesim (1).</summary>
        private void ApplyTint(float k)
        {
            k = Mathf.Clamp01(k);
            if (Mathf.Approximately(k, appliedTint))
                return;
            appliedTint = k;
            foreach (var m in dieMaterials)
            {
                if (m.Material == null)
                    continue;
                Color c = Color.Lerp(m.Base, m.Target, k);
                if (m.Material.HasProperty(BaseColorId))
                    m.Material.SetColor(BaseColorId, c);
                if (m.Material.HasProperty(ColorId))
                    m.Material.SetColor(ColorId, c);
                if (m.Glows)
                    m.Material.SetColor(EmissionColorId, Color.Lerp(m.Emission, CriticalCrystalGlow, k));
            }
            if (keyLight != null)
                keyLight.color = Color.Lerp(KeyLightColor, CriticalKeyLight, k);
            if (rimLight != null)
                rimLight.color = Color.Lerp(RimLightColor, CriticalRimLight, k);
        }

        /// <summary>
        /// Para cada número N, a rotação do dado que deixa a face N de frente para a câmera (-Z do palco) e o
        /// número em pé (Topo_N para cima). Vem dos Empties Centro_N e Topo_N do modelo.
        /// </summary>
        private void BuildFaceTable()
        {
            faceUp.Clear();
            foreach (var pair in FaceRotations(die))
                faceUp[pair.Key] = pair.Value;
        }

        /// <summary>
        /// Rotação local do dado para cada número: face N de frente para a câmera do palco (-Z) e número em pé.
        /// Público para os testes conferirem as 20 faces.
        /// </summary>
        public static Dictionary<int, Quaternion> FaceRotations(Transform die)
        {
            var table = new Dictionary<int, Quaternion>();
            foreach (Transform t in die.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Centro_") || !int.TryParse(t.name.Substring(7), out int n))
                    continue;
                Transform top = FindChild(die, "Topo_" + n);
                if (top == null)
                    continue;
                Vector3 normal = die.InverseTransformPoint(t.position).normalized;
                Vector3 up = die.InverseTransformPoint(top.position) - die.InverseTransformPoint(t.position);
                if (normal.sqrMagnitude < 0.5f || up.sqrMagnitude < 1e-6f)
                    continue;
                Quaternion faceFrame = Quaternion.LookRotation(normal, up.normalized);
                Quaternion viewFrame = Quaternion.LookRotation(Vector3.back, Vector3.up); // de frente para a câmera
                table[n] = viewFrame * Quaternion.Inverse(faceFrame);
            }
            return table;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void OnRollStarted(ulong roller, int value, float rollDuration) => Play(value, rollDuration);

        /// <summary>Começa a rolagem mostrando `value` no fim. Chamado pela rede (RollStarted) e pelos testes.</summary>
        public void Play(int value, float rollDuration)
        {
            EnsureStage();

            // O 1 teatral (D-068) não depende do modelo do dado: o véu e o som entram mesmo sem ele.
            if (value == CriticalFace)
                StartCritical(Mathf.Max(0.5f, rollDuration));
            else
                EndCritical();

            if (die == null)
                return;
            EnsureTexture();

            result = value;
            duration = Mathf.Max(0.5f, rollDuration);
            time = 0f;
            playing = true;
            settling = false;
            spinAxis = Random.onUnitSphere;
            spinSpeed = Random.Range(900f, 1300f);
            die.localRotation = Random.rotationUniform;
            image.enabled = true;
            stageCamera.enabled = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (playing && die != null)
                AnimateDie(dt);
            if (critical)
                TickCritical(dt);
        }

        private void AnimateDie(float dt)
        {
            time += dt;
            float settleAt = duration * SettleStart;

            if (time < settleAt)
            {
                // Rola desacelerando, com um quique que vai baixando.
                float k = time / settleAt;
                float speed = spinSpeed * (1f - k) * (1f - k) + 120f;
                die.localRotation = Quaternion.AngleAxis(speed * dt, spinAxis) * die.localRotation;
                float bounce = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * 0.35f;
                die.localPosition = new Vector3(0f, bounce, 0f);
            }
            else if (time < duration)
            {
                if (!settling)
                {
                    settling = true;
                    settleFrom = die.localRotation;
                    settleTo = faceUp.TryGetValue(result, out var q) ? q : die.localRotation;
                }
                float k = Mathf.Clamp01((time - settleAt) / (duration - settleAt));
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                die.localRotation = Quaternion.Slerp(settleFrom, settleTo, ease);
                die.localPosition = Vector3.Lerp(die.localPosition, Vector3.zero, ease);
            }
            else
            {
                die.localRotation = settleTo;
                die.localPosition = Vector3.zero;
            }

            // Tamanho na tela e desaparecimento no fim.
            var parent = transform as RectTransform;
            float height = (parent != null && parent.rect.height > 1f ? parent.rect.height : Screen.height) * ScreenHeightFraction;
            image.rectTransform.sizeDelta = new Vector2(height, height);
            float end = duration + HoldAfter;
            float alpha = time < end ? 1f : 1f - (time - end) / FadeTime;
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

            if (time >= end + FadeTime)
            {
                playing = false;
                image.enabled = false;
                stageCamera.enabled = false;
            }
        }

        // ---------- O 1 teatral (D-068) ----------

        private void StartCritical(float rollDuration)
        {
            EnsureVeil();
            critical = true;
            criticalTime = 0f;
            criticalDuration = rollDuration;
            veilStrength = 0f;
            thudPlayed = false;
            pulseAge = 0f;
            ApplyTint(0f);
            veil.gameObject.SetActive(true);
            veil.SetAsFirstSibling(); // primeiro da Canvas: por cima do jogo, por baixo da HUD e do dado
            PaintVeil(0f, 0f, Settings);
        }

        private void EndCritical()
        {
            critical = false;
            veilStrength = 0f;
            if (veil != null)
                veil.gameObject.SetActive(false);
            ApplyTint(0f);
        }

        /// <summary>
        /// Linha do tempo do 1: escurece e avermelha do início do giro final até o dado parar; segura cheio por
        /// veilHold (a emboscada chega nesse meio); some em veilFadeOut. O baque toca quando o dado assenta.
        /// O cheio nunca acaba antes da emboscada (grantDelay + veilAmbushMargin) nem antes de o dado sumir da tela,
        /// então o dado nunca volta ao latão diante do jogador.
        /// </summary>
        private void TickCritical(float dt)
        {
            DiceSettings s = Settings;
            criticalTime += dt;
            float startAt = criticalDuration * Mathf.Clamp01(s.criticalTintStart);
            float fullAt = criticalDuration;
            float hold = Mathf.Max(s.veilHold, s.grantDelay + s.veilAmbushMargin, HoldAfter + FadeTime);
            float holdEnd = fullAt + hold;
            float fadeOut = Mathf.Max(0.05f, s.veilFadeOut);

            float k;
            if (criticalTime < startAt)
                k = 0f;
            else if (criticalTime < fullAt)
            {
                float x = Mathf.Clamp01((criticalTime - startAt) / Mathf.Max(0.01f, fullAt - startAt));
                k = x * x * (3f - 2f * x);
            }
            else if (criticalTime < holdEnd)
                k = 1f;
            else
                k = 1f - Mathf.Clamp01((criticalTime - holdEnd) / fadeOut);
            veilStrength = k;

            // O dado só avermelha (não volta ao latão enquanto aparece); volta ao fim, já fora da tela.
            ApplyTint(criticalTime < fullAt ? k : 1f);

            if (!thudPlayed && criticalTime >= fullAt)
            {
                thudPlayed = true;
                pulseAge = 0f;
                PlayThud(s);
            }
            float pulse = 0f;
            if (thudPlayed && s.criticalPulseTime > 0f)
            {
                pulse = 1f - Mathf.Clamp01(pulseAge / s.criticalPulseTime);
                pulse *= pulse;
                pulseAge += dt;
            }

            PaintVeil(k, pulse, s);

            // Só termina com o dado fora da tela (a limpeza devolve o latão ao dado).
            if (criticalTime >= holdEnd + fadeOut && !playing)
                EndCritical();
        }

        private void PaintVeil(float k, float pulse, DiceSettings s)
        {
            if (veilShade != null)
                veilShade.color = new Color(VeilColor.r, VeilColor.g, VeilColor.b, s.veilOpacity * k);
            if (vignette != null)
            {
                Color c = Color.Lerp(VignetteColor, VignettePulseColor, pulse);
                c.a = Mathf.Clamp01(s.vignetteOpacity * k * (1f + PulseBoost * pulse));
                vignette.color = c;
            }
        }

        private void PlayThud(DiceSettings s)
        {
            EnsureAudio();
            audioSource.PlayOneShot(thud, s.criticalThudVolume);
            ThudsPlayed++;
        }

        /// <summary>Fonte de som da tela e o clipe do baque, gerados uma vez (Awake).</summary>
        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // som da tela, não do mundo
            }
            if (thud == null)
                thud = CriticalSounds.MakeThud();
        }

        /// <summary>Véu do 1: sombra que cobre a tela e bordas vermelhas em faixas. Nada recebe o mouse.</summary>
        private void EnsureVeil()
        {
            if (veil != null)
                return;
            // Filho direto da Canvas, em primeiro: escurece o jogo, mas a HUD (barra de cartas) e o dado ficam por cima.
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform host = canvas != null ? canvas.rootCanvas.transform : transform;
            veil = UiFactory.MakeRect("VeuDoUm", host);
            UiFactory.Stretch(veil);
            veil.SetAsFirstSibling();

            veilShade = UiFactory.MakeImage("Sombra", veil, Color.clear);
            UiFactory.Stretch(veilShade.rectTransform);

            var edges = UiFactory.MakeRect("BordasVermelhas", veil);
            UiFactory.Stretch(edges);
            vignette = edges.gameObject.AddComponent<RawImage>();
            vignette.texture = VignetteTexture();
            vignette.color = Color.clear;
            vignette.raycastTarget = false;

            veil.gameObject.SetActive(false);
        }

        /// <summary>Alfa que cresce do centro para as bordas em degraus (a cor vem da RawImage).</summary>
        private Texture2D VignetteTexture()
        {
            if (vignetteTexture != null)
                return vignetteTexture;
            vignetteTexture = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
            {
                name = "BordasDoUm (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[VignetteSize * VignetteSize];
            for (int y = 0; y < VignetteSize; y++)
            {
                float v = (y + 0.5f) / VignetteSize * 2f - 1f;
                for (int x = 0; x < VignetteSize; x++)
                {
                    float u = (x + 0.5f) / VignetteSize * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float k = Mathf.Clamp01((d - VignetteInner) / (VignetteOuter - VignetteInner));
                    float banded = Mathf.Ceil(k * VignetteBands) / VignetteBands;
                    pixels[y * VignetteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(banded * 255f));
                }
            }
            vignetteTexture.SetPixels32(pixels);
            vignetteTexture.Apply(false, true);
            return vignetteTexture;
        }
    }
}
