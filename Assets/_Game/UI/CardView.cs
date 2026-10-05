using Game.Cards;
using Game.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Brilho de um espaço durante o arrastar: compatível (discreto) ou o espaço sob o cursor (mais forte).</summary>
    public enum CardGlow
    {
        None,
        Compatible,
        Target
    }

    /// <summary>
    /// Uma carta na tela (uGUI legado), reaproveitada na barra de skills e na tela de tiragem.
    /// - Com carta: mostra a imagem pronta (D-034). Sem imagem, desenha moldura na cor do naipe e o nome de tarô.
    /// - Sem carta: só o contorno apagado do espaço, sem texto nem símbolo.
    /// - Recarga (D-028): sombra que desce sobre a ilustração, sem número.
    /// - Pilha do cinto: 1 a 3 cartas viradas, levemente deslocadas, em vez de um dígito (sem números na tela).
    /// Nada aqui recebe o mouse: quem arrasta e solta é o LoadoutSlot que envolve a carta.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        /// <summary>Proporção das cartas renderizadas no Blender (448 x 768).</summary>
        public const float Aspect = 448f / 768f;

        private static readonly Color EmptyFill = new Color(0.05f, 0.05f, 0.07f, 0.40f);
        private static readonly Color EmptyOutline = new Color(0.78f, 0.62f, 0.30f, 0.30f);
        private static readonly Color BackFill = new Color(0.13f, 0.10f, 0.08f, 1f);
        private static readonly Color BackInner = new Color(0.22f, 0.17f, 0.11f, 1f);
        private static readonly Color ShadowColor = new Color(0.01f, 0.01f, 0.02f, 0.72f);
        private static readonly Color ShimmerTint = new Color(0.55f, 1f, 1f, 1f);

        [SerializeField] private RectTransform[] backs = new RectTransform[2];
        [SerializeField] private Image frame;
        [SerializeField] private Outline frameOutline;
        [SerializeField] private Image qualityRing;
        [SerializeField] private Outline qualityOutline;
        [SerializeField] private RawImage face;
        [SerializeField] private RawImage shimmer;
        [SerializeField] private Text label;
        [SerializeField] private RectTransform shadow;
        [SerializeField] private Image glow;
        [SerializeField] private Outline glowOutline;
        [SerializeField] private CanvasGroup group;

        private CardData shownCard;
        private int copies;
        private float cooldown;
        private CardGlow glowState;
        private bool styled;

        /// <summary>Id da carta mostrada no banco de cartas, ou -1 com o espaço vazio.</summary>
        public int CardId { get; private set; } = Loadout.Empty;

        public bool IsEmpty => CardId < 0;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Texto curto do mouse por cima: nome e carta de tarô, ex.: "Pistão Rúnico · Ás de Espadas".</summary>
        public static string Caption(CardData data) => data == null ? string.Empty : data.displayName + " · " + data.TarotLabel;

        /// <summary>Largura que mantém a proporção da carta para uma altura.</summary>
        public static float WidthForHeight(float height) => height * Aspect;

        /// <summary>
        /// Cria uma carta vazia (centro do pai, tamanho dado). Serve ao CardUiBuilder no editor e à tela de tiragem em runtime.
        /// </summary>
        public static CardView Create(Transform parent, string name, Vector2 size)
        {
            RectTransform root = UiFactory.MakeRect(name, parent);
            UiFactory.PlaceCentered(root, Vector2.zero, size);
            var view = root.gameObject.AddComponent<CardView>();

            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.interactable = false;
            view.group.blocksRaycasts = false; // o mouse é do LoadoutSlot que envolve a carta

            // Cartas viradas atrás (pilha do cinto). A mais distante é criada primeiro para ficar no fundo.
            view.backs = new RectTransform[2];
            for (int i = view.backs.Length - 1; i >= 0; i--)
            {
                Vector2 offset = new Vector2(size.x * 0.07f * (i + 1), size.y * 0.04f * (i + 1));
                Image back = UiFactory.MakeImage("Costas" + (i + 1), root, BackFill);
                UiFactory.StretchOffset(back.rectTransform, offset);
                UiFactory.AddOutline(back, UiFactory.Brass, 2f);
                Image inner = UiFactory.MakeImage("Miolo", back.transform, BackInner);
                UiFactory.Stretch(inner.rectTransform, Mathf.Max(4f, size.x * 0.07f));
                back.gameObject.SetActive(false);
                view.backs[i] = back.rectTransform;
            }

            view.frame = UiFactory.MakeImage("Moldura", root, EmptyFill);
            UiFactory.Stretch(view.frame.rectTransform);
            view.frameOutline = UiFactory.AddOutline(view.frame, EmptyOutline, 2f);

            // Qualidade (D-048): só a borda muda — cobre oxidado na gasta, nada na boa, ouro na perfeita.
            view.qualityRing = UiFactory.MakeImage("Qualidade", root, new Color(1f, 1f, 1f, 0f));
            UiFactory.Stretch(view.qualityRing.rectTransform, -2f);
            view.qualityRing.raycastTarget = false;
            view.qualityOutline = UiFactory.AddOutline(view.qualityRing, Color.clear, 3f);

            var faceRect = UiFactory.MakeRect("Ilustracao", root);
            UiFactory.Stretch(faceRect);
            view.face = faceRect.gameObject.AddComponent<RawImage>();
            view.face.raycastTarget = false;
            view.face.enabled = false;

            // Brilho do cristal (D-041): máscara ciano por cima da ilustração, com alfa pulsando devagar. Só liga se a carta tiver máscara.
            var shimmerRect = UiFactory.MakeRect("BrilhoCristal", root);
            UiFactory.Stretch(shimmerRect);
            view.shimmer = shimmerRect.gameObject.AddComponent<RawImage>();
            view.shimmer.raycastTarget = false;
            view.shimmer.color = new Color(ShimmerTint.r, ShimmerTint.g, ShimmerTint.b, 0f);
            view.shimmer.enabled = false;

            view.label = UiFactory.MakeText("Rotulo", root, string.Empty, 20, UiFactory.Parchment);
            UiFactory.Stretch(view.label.rectTransform, Mathf.Max(3f, size.x * 0.06f));
            view.label.resizeTextForBestFit = true;
            view.label.resizeTextMinSize = 8;
            view.label.resizeTextMaxSize = 26;
            view.label.gameObject.SetActive(false);

            // Sombra da recarga: ocupa a parte de baixo da carta, proporcional à fração (âncora em vez de fillAmount,
            // porque Image preenchida exige sprite e aqui não há nenhum). Uma linha de latão marca o topo.
            Image shadowImage = UiFactory.MakeImage("Sombra", root, ShadowColor);
            view.shadow = shadowImage.rectTransform;
            view.shadow.anchorMin = Vector2.zero;
            view.shadow.anchorMax = new Vector2(1f, 0f);
            view.shadow.offsetMin = Vector2.zero;
            view.shadow.offsetMax = Vector2.zero;
            Image edge = UiFactory.MakeImage("Borda", shadowImage.transform,
                new Color(UiFactory.Brass.r, UiFactory.Brass.g, UiFactory.Brass.b, 0.55f));
            edge.rectTransform.anchorMin = new Vector2(0f, 1f);
            edge.rectTransform.anchorMax = new Vector2(1f, 1f);
            edge.rectTransform.pivot = new Vector2(0.5f, 1f);
            edge.rectTransform.anchoredPosition = Vector2.zero;
            edge.rectTransform.sizeDelta = new Vector2(0f, 2f);
            view.shadow.gameObject.SetActive(false);

            view.glow = UiFactory.MakeImage("Brilho", root, new Color(UiFactory.Crystal.r, UiFactory.Crystal.g, UiFactory.Crystal.b, 0f));
            UiFactory.Stretch(view.glow.rectTransform);
            view.glowOutline = UiFactory.AddOutline(view.glow, UiFactory.Crystal, 3f);
            view.glow.gameObject.SetActive(false);

            view.Clear();
            return view;
        }

        /// <summary>Mostra a carta. copyCount &gt; 1 empilha cartas viradas atrás (no máximo 2, ou seja, 3 no total).</summary>
        public void SetCard(CardDatabase database, int id, int copyCount = 1)
        {
            CardData data = database != null ? database.Get(id) : null;
            if (data == null)
            {
                Clear();
                return;
            }

            copyCount = Mathf.Max(1, copyCount);
            PlayerCards local = LocalPlayerCards.Get();
            CardQuality quality = local != null ? local.QualityOf(id) : CardQuality.Good;
            if (styled && data == shownCard && id == CardId && copyCount == copies && quality == shownQuality)
                return;
            shownQuality = quality;
            ShowQuality(quality);

            styled = true;
            shownCard = data;
            CardId = id;
            copies = copyCount;

            bool hasFace = data.face != null;
            face.enabled = hasFace;
            if (hasFace)
                face.texture = data.face;

            // Brilho animado do cristal, se a carta tem máscara (CardData.glow).
            bool hasShimmer = hasFace && data.glow != null && shimmer != null;
            if (shimmer != null)
            {
                shimmer.enabled = hasShimmer;
                if (hasShimmer)
                    shimmer.texture = data.glow;
            }

            // Sem imagem pronta, a moldura na cor do naipe e o nome de tarô seguram o lugar.
            frame.enabled = !hasFace;
            frame.color = FillColor(data);
            frameOutline.effectColor = UiFactory.Brass;
            label.gameObject.SetActive(!hasFace);
            if (!hasFace)
                label.text = data.TarotLabel;

            ShowBacks(copyCount - 1);
        }

        private static readonly Color WornEdge = new Color(0.31f, 0.55f, 0.47f, 0.95f);     // verdete (#4F8C7A)
        private static readonly Color PerfectEdge = new Color(1f, 0.86f, 0.38f, 1f);       // ouro vivo

        private CardQuality shownQuality = CardQuality.Good;

        private void ShowQuality(CardQuality quality)
        {
            if (qualityOutline == null)
                return;
            qualityOutline.effectColor = quality switch
            {
                CardQuality.Worn => WornEdge,
                CardQuality.Perfect => PerfectEdge,
                _ => Color.clear
            };
            qualityOutline.effectDistance = quality == CardQuality.Perfect ? new Vector2(4f, -4f) : new Vector2(3f, -3f);
        }

        /// <summary>Espaço vazio: contorno de latão apagado, só a forma.</summary>
        public void Clear()
        {
            if (styled && shownCard == null && CardId < 0)
                return;

            styled = true;
            shownCard = null;
            CardId = Loadout.Empty;
            copies = 0;

            face.enabled = false;
            if (shimmer != null)
                shimmer.enabled = false;
            label.gameObject.SetActive(false);
            frame.enabled = true;
            frame.color = EmptyFill;
            frameOutline.effectColor = EmptyOutline;
            ShowQuality(CardQuality.Good);
            ShowBacks(0);
            SetCooldown(0f);
        }

        /// <summary>D-028: 1 = acabou de usar (sombra cobre a carta), 0 = pronta. A sombra desce até sumir.</summary>
        public void SetCooldown(float fraction)
        {
            float value = CardId < 0 ? 0f : Mathf.Clamp01(fraction);
            if (Mathf.Approximately(value, cooldown))
                return;

            cooldown = value;
            bool visible = value > 0.001f;
            if (shadow.gameObject.activeSelf != visible)
                shadow.gameObject.SetActive(visible);
            if (visible)
                shadow.anchorMax = new Vector2(1f, value);
        }

        /// <summary>Brilho discreto nos espaços compatíveis enquanto se arrasta uma carta.</summary>
        public void SetGlow(CardGlow state)
        {
            if (state == glowState)
                return;

            glowState = state;
            glow.gameObject.SetActive(state != CardGlow.None);
            if (state != CardGlow.None)
                ApplyGlowAlpha(GlowBase(state));
        }

        /// <summary>Transparência da carta toda (a de origem fica esmaecida enquanto o fantasma é arrastado).</summary>
        public void SetAlpha(float alpha)
        {
            if (group != null)
                group.alpha = alpha;
        }

        private void Update()
        {
            UpdateShimmer();

            if (glowState == CardGlow.None)
                return;

            // Pulsa devagar, usando o tempo real: o jogo roda por trás, mas a tela não depende de Time.timeScale.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            ApplyGlowAlpha(GlowBase(glowState) + (glowState == CardGlow.Target ? 0.20f : 0.08f) * pulse);
        }

        /// <summary>Cristal respirando: alfa sobe e desce devagar, com um cintilar mais rápido por cima. Tempo real (não depende do timeScale).</summary>
        private void UpdateShimmer()
        {
            if (shimmer == null || !shimmer.enabled)
                return;

            float time = Time.unscaledTime + (GetInstanceID() & 0xFF) * 0.37f; // cada carta num ponto diferente do ciclo
            float slow = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);
            float spark = Mathf.Max(0f, Mathf.Sin(time * 7.3f)) * 0.12f;
            Color c = shimmer.color;
            c.a = Mathf.Clamp01(0.15f + 0.4f * slow + spark);
            shimmer.color = c;
        }

        private static float GlowBase(CardGlow state) => state == CardGlow.Target ? 0.24f : 0.06f;

        private void ApplyGlowAlpha(float alpha)
        {
            Color fill = glow.color;
            fill.a = alpha;
            glow.color = fill;

            Color line = glowOutline.effectColor;
            line.a = Mathf.Clamp01(alpha * 2.5f + 0.15f);
            glowOutline.effectColor = line;
        }

        private void ShowBacks(int extra)
        {
            extra = Mathf.Clamp(extra, 0, backs != null ? backs.Length : 0);
            for (int i = 0; backs != null && i < backs.Length; i++)
            {
                if (backs[i] != null)
                    backs[i].gameObject.SetActive(i < extra);
            }
        }

        private static Color FillColor(CardData data)
        {
            if (data.arcana == Arcana.Major)
                return new Color(0.25f, 0.16f, 0.36f); // arcano maior: violeta
            switch (data.suit)
            {
                case Suit.Swords: return new Color(0.20f, 0.28f, 0.37f);
                case Suit.Cups: return new Color(0.38f, 0.12f, 0.15f);
                case Suit.Wands: return new Color(0.19f, 0.30f, 0.16f);
                case Suit.Pentacles: return new Color(0.42f, 0.32f, 0.13f);
                default: return new Color(0.18f, 0.16f, 0.16f);
            }
        }
    }
}
