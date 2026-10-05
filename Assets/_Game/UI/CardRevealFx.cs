using System.Collections.Generic;
using Game.Cards;
using Game.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Revelação da carta nova (D-042). Quando o total de cartas do jogador local cresce (inventário + espaços equipados,
    /// então tirar uma carta do espaço não conta), a carta aparece no centro da tela com um estalo de escala,
    /// um estouro dourado (raios, losango e faíscas em quadrados) e um brilho que varre a face; depois voa até a barra de skills.
    /// Feito só com uGUI (retângulos), no estilo da UI do jogo, fora do 3D pixelado. Nada aqui recebe o mouse.
    /// O componente fica sob a Canvas "UI": o líder pode adicioná-lo ali, ou ele se cria sozinho no início da cena.
    /// </summary>
    public class CardRevealFx : MonoBehaviour
    {
        // Tempos da revelação (segundos de tempo real).
        private const float PopTime = 0.22f;
        private const float HoldTime = 0.75f;
        private const float FlyTime = 0.55f;
        private const float TailTime = 0.45f;
        private const float StaggerTime = 0.35f;   // entre uma revelação e a próxima, se vierem várias
        private const int MaxPending = 4;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color GoldLight = new Color(1f, 0.95f, 0.7f, 1f);
        private static readonly Color GoldDeep = new Color(1f, 0.6f, 0.2f, 1f);

        private readonly Dictionary<int, int> owned = new Dictionary<int, int>();
        private readonly Dictionary<int, int> scratch = new Dictionary<int, int>();
        private readonly Queue<int> pending = new Queue<int>();
        private readonly Dictionary<int, float> lastRevealed = new Dictionary<int, float>();
        private const float RepeatGuard = 2.5f; // carta nova com qualidade: inventário e qualidade mudam juntos, revela uma vez só
        private readonly List<Reveal> reveals = new List<Reveal>();

        private RectTransform rect;
        private Canvas rootCanvas;
        private SkillBar skillBar;
        private PlayerCards cards;
        private float nextStart;

        /// <summary>Revelações na tela agora (para testes).</summary>
        public int ActiveCount => reveals.Count;

        /// <summary>Cria o componente sob a Canvas "UI" da cena, se ainda não existir um. Devolve null se não há a Canvas.</summary>
        public static CardRevealFx EnsureOnCanvas()
        {
            var existing = Object.FindFirstObjectByType<CardRevealFx>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            GameObject canvasGo = GameObject.Find("UI");
            Canvas canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            if (canvas == null)
                return null;

            RectTransform root = UiFactory.MakeRect("RevelacaoDeCarta", canvas.transform);
            UiFactory.Stretch(root);
            return root.gameObject.AddComponent<CardRevealFx>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureOnCanvas();

        private void Awake()
        {
            rect = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            rootCanvas = canvas != null ? canvas.rootCanvas : null;
        }

        private void OnDisable()
        {
            Unbind();
            for (int i = 0; i < reveals.Count; i++)
                reveals[i].Dispose();
            reveals.Clear();
            pending.Clear();
        }

        // ---------- API ----------

        /// <summary>Toca a revelação de uma carta do banco do jogador local (depuração e testes).</summary>
        public void Play(int cardId)
        {
            if (cardId < 0 || pending.Count >= MaxPending)
                return;
            pending.Enqueue(cardId);
        }

        // ---------- Ligação com o jogador local ----------

        private void Update()
        {
            PlayerCards current = LocalPlayerCards.Get();
            if (current != cards)
            {
                Unbind();
                Bind(current);
            }

            float now = Time.unscaledTime;
            if (pending.Count > 0 && now >= nextStart && cards != null && cards.Database != null && rect != null)
            {
                StartReveal(pending.Dequeue());
                nextStart = now + StaggerTime;
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = reveals.Count - 1; i >= 0; i--)
            {
                if (!reveals[i].Tick(dt))
                {
                    reveals[i].Dispose();
                    reveals.RemoveAt(i);
                }
            }
        }

        private void Bind(PlayerCards next)
        {
            cards = next;
            if (cards == null)
                return;
            Snapshot(owned);                 // o que já existe não é revelação
            cards.Changed += OnCardsChanged;
            cards.QualityChanged += OnQualityChanged;
        }

        private void Unbind()
        {
            if (cards != null)
            {
                cards.Changed -= OnCardsChanged;
                cards.QualityChanged -= OnQualityChanged;
            }
            cards = null;
            owned.Clear();
            pending.Clear();
        }

        /// <summary>A repetida melhorou uma carta que o jogador já tinha (D-051): revela de novo, já com a moldura nova.</summary>
        private void OnQualityChanged(int cardId, CardQuality quality)
        {
            if (cards == null || !cards.Owns(cardId) || pending.Count >= MaxPending || pending.Contains(cardId))
                return;
            if (lastRevealed.TryGetValue(cardId, out float at) && Time.unscaledTime - at < RepeatGuard)
                return;
            pending.Enqueue(cardId);
        }

        /// <summary>Mudou algo nas cartas: compara o total de cada carta (inventário + equipadas) com o que havia.</summary>
        private void OnCardsChanged()
        {
            if (cards == null)
                return;

            Snapshot(scratch);
            foreach (KeyValuePair<int, int> entry in scratch)
            {
                owned.TryGetValue(entry.Key, out int before);
                for (int n = before; n < entry.Value && pending.Count < MaxPending; n++)
                    pending.Enqueue(entry.Key);
            }

            owned.Clear();
            foreach (KeyValuePair<int, int> entry in scratch)
                owned[entry.Key] = entry.Value;
        }

        /// <summary>Quantas cópias de cada carta o jogador tem, no inventário ou equipadas.</summary>
        private void Snapshot(Dictionary<int, int> into)
        {
            into.Clear();
            IReadOnlyList<int> inventory = cards.Inventory;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Count; i++)
                    Add(into, inventory[i], 1);
            }

            for (int t = 0; t < 4; t++)
            {
                var type = (SlotType)t;
                int slots = CardRules.SlotCount(type);
                for (int i = 0; i < slots; i++)
                {
                    int id = cards.GetSlot(type, i);
                    if (id < 0)
                        continue;
                    Add(into, id, type == SlotType.Belt ? Mathf.Max(1, cards.BeltCount(i)) : 1);
                }
            }
        }

        private static void Add(Dictionary<int, int> map, int id, int amount)
        {
            if (id < 0)
                return;
            map.TryGetValue(id, out int count);
            map[id] = count + amount;
        }

        // ---------- Revelação ----------

        private void StartReveal(int cardId)
        {
            lastRevealed[cardId] = Time.unscaledTime;
            float height = Mathf.Clamp(rect.rect.height > 1f ? rect.rect.height * 0.42f : 420f, 200f, 460f);
            var size = new Vector2(CardView.WidthForHeight(height), height);

            ResolveTarget(size, out Vector2 targetLocal, out float targetScale);
            transform.SetAsLastSibling();
            reveals.Add(new Reveal(rect, cards.Database, cardId, size, targetLocal, targetScale));
        }

        /// <summary>Para onde a carta voa: o centro da barra de skills (ou o canto de baixo, se não há barra).</summary>
        private void ResolveTarget(Vector2 cardSize, out Vector2 local, out float scale)
        {
            local = new Vector2(0f, -rect.rect.height * 0.5f + 100f);
            scale = Mathf.Clamp(96f / Mathf.Max(1f, cardSize.y), 0.15f, 0.6f);

            if (skillBar == null)
                skillBar = Object.FindFirstObjectByType<SkillBar>();
            var barRect = skillBar != null ? skillBar.transform as RectTransform : null;
            if (barRect == null)
                return;

            Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
            Vector3 world = barRect.TransformPoint(barRect.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, cam, out Vector2 point))
                local = point;
        }

        /// <summary>Uma revelação em andamento: objetos de uGUI criados na hora e destruídos no fim.</summary>
        private sealed class Reveal
        {
            private struct Spark
            {
                public Image Image;
                public Vector2 Position;
                public Vector2 Velocity;
                public float Life;
                public float Age;
                public float Size;
            }

            private readonly RectTransform root;
            private readonly RectTransform cardWrap;
            private readonly CardView card;
            private readonly Image diamond;
            private readonly Image[] rays = new Image[8];
            private readonly Image sweep;
            private readonly List<Spark> sparks = new List<Spark>();
            private readonly Vector2 size;
            private readonly Vector2 target;
            private readonly float targetScale;
            private float age;
            private bool arrived;
            private float arrivedAt;

            public Reveal(RectTransform parent, CardDatabase database, int cardId, Vector2 cardSize, Vector2 targetLocal, float endScale)
            {
                size = cardSize;
                target = targetLocal;
                targetScale = endScale;

                root = UiFactory.MakeRect("Revelacao", parent);
                UiFactory.Stretch(root);

                // Atrás da carta: losango dourado e raios.
                diamond = UiFactory.MakeImage("Losango", root, new Color(Gold.r, Gold.g, Gold.b, 0f));
                UiFactory.PlaceCentered(diamond.rectTransform, Vector2.zero, Vector2.one * size.y * 0.8f);
                diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                for (int i = 0; i < rays.Length; i++)
                {
                    rays[i] = UiFactory.MakeImage("Raio", root, new Color(GoldLight.r, GoldLight.g, GoldLight.b, 0f));
                    UiFactory.Place(rays[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero,
                        new Vector2(i % 2 == 0 ? 16f : 10f, size.y * 0.9f));
                    rays[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                }

                // A carta, dentro de uma moldura que recorta o brilho que varre a face.
                cardWrap = UiFactory.MakeRect("Carta", root);
                UiFactory.PlaceCentered(cardWrap, Vector2.zero, size + Vector2.one * 20f);
                cardWrap.gameObject.AddComponent<RectMask2D>();
                card = CardView.Create(cardWrap, "Face", size);
                card.SetCard(database, cardId);
                card.SetAlpha(0f);

                sweep = UiFactory.MakeImage("Brilho", cardWrap, new Color(1f, 1f, 0.92f, 0f));
                UiFactory.PlaceCentered(sweep.rectTransform, new Vector2(-size.x, 0f), new Vector2(size.x * 0.22f, size.y * 1.7f));
                sweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 20f);

                // Estouro de faíscas douradas logo na aparição.
                SpawnSparks(Vector2.zero, 16, 380f, 760f, 0.55f, 0.9f, 14f, 22f);
                cardWrap.localScale = Vector3.one * 0.2f;
            }

            /// <summary>Avança a animação. Devolve false quando acabou (e pode ser destruída).</summary>
            public bool Tick(float dt)
            {
                age += dt;
                TickBurst();
                TickSparks(dt);

                float flyStart = PopTime + HoldTime;
                float flyEnd = flyStart + FlyTime;

                if (age < PopTime)
                {
                    float k = age / PopTime;
                    SetCard(Vector2.zero, Mathf.LerpUnclamped(0.2f, 1f, FxKit.EaseOutBack(k)), 0f, Mathf.Clamp01(k * 3f));
                }
                else if (age < flyStart)
                {
                    float k = age - PopTime;
                    float bob = Mathf.Sin(k * 9f);
                    SetCard(new Vector2(0f, bob * 4f), 1f + 0.03f * bob, 0f, 1f);

                    // Brilho que varre a face da esquerda para a direita.
                    float sweepK = Mathf.Clamp01((k - 0.12f) / 0.45f);
                    UiFactory.PlaceCentered(sweep.rectTransform, new Vector2(Mathf.Lerp(-size.x * 0.9f, size.x * 0.9f, sweepK), 0f),
                        new Vector2(size.x * 0.22f, size.y * 1.7f));
                    sweep.color = new Color(1f, 1f, 0.92f, sweepK > 0f && sweepK < 1f ? 0.55f : 0f);
                }
                else if (age < flyEnd)
                {
                    sweep.color = new Color(1f, 1f, 0.92f, 0f);
                    float k = (age - flyStart) / FlyTime;
                    float e = k * k;
                    Vector2 control = (Vector2.zero + target) * 0.5f + new Vector2(0f, 220f);
                    Vector2 position = Bezier(Vector2.zero, control, target, e);
                    float tilt = Mathf.Sin(e * Mathf.PI) * -14f;
                    SetCard(position, Mathf.Lerp(1f, targetScale, e), tilt, 1f);
                }
                else
                {
                    if (!arrived)
                    {
                        // Chegou na barra: a carta some e solta um estouro pequeno.
                        arrived = true;
                        arrivedAt = age;
                        cardWrap.gameObject.SetActive(false);
                        SpawnSparks(target, 10, 220f, 420f, 0.3f, 0.5f, 10f, 16f);
                    }
                    if (age - arrivedAt > TailTime && sparks.Count == 0)
                        return false;
                    if (age - arrivedAt > TailTime * 2f)
                        return false;
                }

                return true;
            }

            private void SetCard(Vector2 position, float scale, float tilt, float alpha)
            {
                cardWrap.anchoredPosition = position;
                cardWrap.localScale = new Vector3(scale, scale, 1f);
                cardWrap.localRotation = Quaternion.Euler(0f, 0f, tilt);
                card.SetAlpha(alpha);
            }

            /// <summary>Losango e raios dourados: crescem rápido e somem em ~0,45 s.</summary>
            private void TickBurst()
            {
                float k = Mathf.Clamp01(age / 0.45f);
                float open = 1f - (1f - k) * (1f - k);
                float fade = 1f - k;

                diamond.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.8f, open);
                diamond.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f * fade * fade);

                for (int i = 0; i < rays.Length; i++)
                {
                    rays[i].rectTransform.localScale = new Vector3(1f, Mathf.Lerp(0.25f, 1.5f, open), 1f);
                    rays[i].color = new Color(GoldLight.r, GoldLight.g, GoldLight.b, (age < 0.45f ? 0.75f : 0f) * fade);
                }
            }

            private void SpawnSparks(Vector2 origin, int count, float speedMin, float speedMax, float lifeMin, float lifeMax,
                float sizeMin, float sizeMax)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float speed = Random.Range(speedMin, speedMax);
                    float s = Random.Range(sizeMin, sizeMax);
                    Color color = Random.value < 0.4f ? GoldLight : (Random.value < 0.5f ? Gold : GoldDeep);
                    Image image = UiFactory.MakeImage("Faisca", root, color);
                    UiFactory.PlaceCentered(image.rectTransform, origin, Vector2.one * s);
                    sparks.Add(new Spark
                    {
                        Image = image,
                        Position = origin,
                        Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                        Life = Random.Range(lifeMin, lifeMax),
                        Size = s
                    });
                }
            }

            private void TickSparks(float dt)
            {
                for (int i = sparks.Count - 1; i >= 0; i--)
                {
                    Spark sp = sparks[i];
                    sp.Age += dt;
                    if (sp.Age >= sp.Life)
                    {
                        if (sp.Image != null)
                            Object.Destroy(sp.Image.gameObject);
                        sparks.RemoveAt(i);
                        continue;
                    }

                    sp.Velocity.y -= 900f * dt;
                    sp.Position += sp.Velocity * dt;
                    float k = sp.Age / sp.Life;
                    float s = sp.Size * (1f - k * k); // encolhe até sumir, como as faíscas do jogo
                    sp.Image.rectTransform.anchoredPosition = sp.Position;
                    sp.Image.rectTransform.sizeDelta = Vector2.one * s;
                    sparks[i] = sp;
                }
            }

            private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
            {
                float u = 1f - t;
                return u * u * a + 2f * u * t * b + t * t * c;
            }

            public void Dispose()
            {
                if (root != null)
                    Object.Destroy(root.gameObject);
            }
        }
    }
}
