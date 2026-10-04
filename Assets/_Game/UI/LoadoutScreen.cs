using System;
using System.Collections.Generic;
using Game.Cards;
using Game.Core.Cards;
using Game.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Tela de tiragem (D-027): as cartas dispostas como uma tiragem de tarô sobre um pano escuro.
    /// Tab abre e fecha (D-032). O jogo continua rodando por trás (sem Time.timeScale), então enquanto ela está
    /// aberta o leitor de input bloqueia ataque, skills e cinto (UiBlocksActions); o movimento segue livre.
    /// O jogador arrasta cartas do leque de inventário para os espaços compatíveis (equipar) e de volta ao leque
    /// (desequipar). Sem número e sem texto longo: só a ilustração e, por cima do mouse, nome curto + carta de tarô.
    /// </summary>
    public class LoadoutScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private LoadoutSlot[] skillSlots = Array.Empty<LoadoutSlot>();
        [SerializeField] private LoadoutSlot[] passiveSlots = Array.Empty<LoadoutSlot>();
        [SerializeField] private LoadoutSlot[] equipmentSlots = Array.Empty<LoadoutSlot>();
        [SerializeField] private LoadoutSlot[] beltSlots = Array.Empty<LoadoutSlot>();
        [SerializeField] private RectTransform inventoryContainer;
        [SerializeField] private Vector2 inventoryCardSize = new Vector2(88f, 150f);
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private RectTransform hoverBox;
        [SerializeField] private Text hoverText;

        // Soltou o botão há mais que isto sem o EventSystem avisar: cancela o arrastar.
        private const float StuckDragSeconds = 0.3f;

        private readonly List<LoadoutSlot> inventoryEntries = new List<LoadoutSlot>();
        private readonly List<int> sortedInventory = new List<int>();
        private PlayerCards cards;
        private PlayerInputReader reader;
        private Canvas rootCanvas;
        private bool dirty = true;
        private int inventoryCount;

        private CardView ghost;
        private LoadoutSlot dragSource;
        private int dragCardId = Loadout.Empty;
        private SlotType dragSlotType;
        private float releasedTime;
        private LoadoutSlot hovered;

        /// <summary>Tiragem na tela agora.</summary>
        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>Cartas do inventário desenhadas no leque (usado em testes).</summary>
        public int InventoryViewCount => inventoryCount;

        public bool IsDragging => dragSource != null;

        public void Configure(GameObject newPanel, RectTransform newPanelRect, LoadoutSlot[] skills, LoadoutSlot[] passives,
            LoadoutSlot[] equipment, LoadoutSlot[] belt, RectTransform inventory, Vector2 inventoryCard,
            RectTransform newDragLayer, RectTransform newHoverBox, Text newHoverText)
        {
            panel = newPanel;
            panelRect = newPanelRect;
            skillSlots = skills;
            passiveSlots = passives;
            equipmentSlots = equipment;
            beltSlots = belt;
            inventoryContainer = inventory;
            inventoryCardSize = inventoryCard;
            dragLayer = newDragLayer;
            hoverBox = newHoverBox;
            hoverText = newHoverText;
        }

        private void Awake()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            rootCanvas = canvas != null ? canvas.rootCanvas : null;
        }

        private void OnDisable() => Unbind();

        private void Update()
        {
            EnsureBound();
            if (cards == null)
            {
                if (IsOpen)
                    Close(); // o jogador local sumiu (sessão acabou): não deixa a tela aberta
                return;
            }

            WatchStuckDrag();
            // Reconstruir no meio de um arrastar destruiria a origem do arrasto; espera soltar.
            if (IsOpen && dirty && dragSource == null)
                Refresh();
        }

        // ---------- Abrir e fechar ----------

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            EnsureBound();
            if (panel == null || cards == null || IsOpen)
                return;

            panel.SetActive(true);
            if (reader != null)
                reader.UiBlocksActions = true;
            dirty = true;
            Refresh();
        }

        public void Close()
        {
            CancelDrag();
            HideHover();
            if (panel != null && panel.activeSelf)
                panel.SetActive(false);
            if (reader != null)
                reader.UiBlocksActions = false;
        }

        // ---------- Ligação com o jogador local ----------

        private void EnsureBound()
        {
            PlayerCards current = LocalPlayerCards.Get();
            if (current == cards)
                return;

            Unbind();
            cards = current;
            if (cards == null)
                return;

            reader = cards.GetComponent<PlayerInputReader>();
            cards.Changed += OnCardsChanged;
            if (reader != null)
            {
                reader.LoadoutToggled += Toggle;
                reader.UiBlocksActions = IsOpen;
            }
            dirty = true;
        }

        private void Unbind()
        {
            if (reader != null)
            {
                reader.LoadoutToggled -= Toggle;
                reader.UiBlocksActions = false;
            }
            if (cards != null)
                cards.Changed -= OnCardsChanged;
            reader = null;
            cards = null;
        }

        private void OnCardsChanged() => dirty = true;

        // ---------- Equipar e desequipar ----------

        /// <summary>
        /// Equipa a carta do inventário no espaço, se o tipo combina (D-036: skill nas Espadas, consumível nas Copas,
        /// passiva nos Paus, equipamento nos Ouros). O host decide; o resultado chega por PlayerCards.Changed.
        /// </summary>
        public bool TryDropOnSlot(int cardId, SlotType slot, int index)
        {
            EnsureBound();
            if (cards == null || cards.Database == null)
                return false;
            if (index < 0 || index >= CardRules.SlotCount(slot) || cards.Database.Get(cardId) == null)
                return false;
            if (CardRules.SlotFor(cards.Database.KindOf(cardId)) != slot)
                return false;

            cards.RequestEquip(cardId, slot, index);
            return true;
        }

        /// <summary>Devolve ao inventário a carta (ou a pilha, no cinto) que está no espaço.</summary>
        public bool TryUnequip(SlotType slot, int index)
        {
            EnsureBound();
            if (cards == null || index < 0 || index >= CardRules.SlotCount(slot))
                return false;
            if (cards.GetSlot(slot, index) < 0)
                return false;

            cards.RequestUnequip(slot, index);
            return true;
        }

        /// <summary>
        /// Troca de lugar entre dois espaços do mesmo tipo. Equipar exige a carta no inventário, então tira,
        /// põe no destino (a que estava lá volta ao inventário) e, se havia uma, põe-na na origem.
        /// No cinto, repete o equipar pelo tamanho da pilha para não desmanchá-la.
        /// </summary>
        private bool TryMove(LoadoutSlot origin, LoadoutSlot destination)
        {
            if (cards == null || origin == destination || origin.Slot != destination.Slot)
                return false;

            SlotType type = origin.Slot;
            int moving = cards.GetSlot(type, origin.Index);
            if (moving < 0)
                return false;

            int displaced = cards.GetSlot(type, destination.Index);
            int movingCopies = type == SlotType.Belt ? Mathf.Max(1, cards.BeltCount(origin.Index)) : 1;
            int displacedCopies = type == SlotType.Belt && displaced >= 0 ? Mathf.Max(1, cards.BeltCount(destination.Index)) : 1;

            cards.RequestUnequip(type, origin.Index);
            for (int i = 0; i < movingCopies; i++)
                cards.RequestEquip(moving, type, destination.Index);
            if (displaced >= 0)
            {
                for (int i = 0; i < displacedCopies; i++)
                    cards.RequestEquip(displaced, type, origin.Index);
            }
            return true;
        }

        // ---------- Desenho ----------

        private void Refresh()
        {
            dirty = false;
            if (cards == null)
                return;

            HideHover();
            CardDatabase database = cards.Database;
            RefreshSlots(skillSlots, SlotType.Skill, database);
            RefreshSlots(passiveSlots, SlotType.Passive, database);
            RefreshSlots(equipmentSlots, SlotType.Equipment, database);
            RefreshSlots(beltSlots, SlotType.Belt, database);
            RefreshInventory(database);
        }

        private void RefreshSlots(LoadoutSlot[] slots, SlotType type, CardDatabase database)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    continue;
                int id = cards.GetSlot(type, i);
                int copies = type == SlotType.Belt && id >= 0 ? Mathf.Max(1, cards.BeltCount(i)) : 1;
                slots[i].SetContent(database, id, copies);
            }
        }

        private void RefreshInventory(CardDatabase database)
        {
            sortedInventory.Clear();
            IReadOnlyList<int> inventory = cards.Inventory;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Count; i++)
                    sortedInventory.Add(inventory[i]);
            }
            sortedInventory.Sort(); // ids seguem a ordem do banco: skills primeiro, depois cinto, passivas e equipamentos

            int count = sortedInventory.Count;
            while (inventoryEntries.Count < count)
                inventoryEntries.Add(CreateInventoryEntry());

            // Leque: se couber, as cartas ficam lado a lado; se não, sobrepõem-se um pouco. Bordas mais baixas e inclinadas.
            float width = inventoryContainer != null && inventoryContainer.rect.width > inventoryCardSize.x
                ? inventoryContainer.rect.width
                : 1500f;
            float step = count <= 1 ? 0f : Mathf.Min(inventoryCardSize.x * 1.12f, (width - inventoryCardSize.x) / (count - 1));
            float spread = Mathf.Max(0, count - 1) * step;
            float fan = Mathf.Clamp01((count - 1) / 6f);

            for (int i = 0; i < inventoryEntries.Count; i++)
            {
                LoadoutSlot entry = inventoryEntries[i];
                bool used = i < count;
                if (entry.gameObject.activeSelf != used)
                    entry.gameObject.SetActive(used);
                if (!used)
                {
                    entry.SetContent(null, Loadout.Empty, 0);
                    continue;
                }

                entry.SetContent(database, sortedInventory[i], 1);
                float t = count <= 1 ? 0f : i / (float)(count - 1) * 2f - 1f;
                entry.Rect.anchoredPosition = new Vector2(-spread * 0.5f + i * step, -t * t * 16f * fan);
                entry.Rect.localRotation = Quaternion.Euler(0f, 0f, -t * 7f * fan);
            }

            inventoryCount = count;
        }

        private LoadoutSlot CreateInventoryEntry()
        {
            RectTransform rect = UiFactory.MakeRect("Carta", inventoryContainer);
            UiFactory.PlaceCentered(rect, Vector2.zero, inventoryCardSize);
            UiFactory.AddHitArea(rect.gameObject);

            CardView view = CardView.Create(rect, "Face", inventoryCardSize);
            UiFactory.Stretch(view.Rect);

            var slot = rect.gameObject.AddComponent<LoadoutSlot>();
            slot.Configure(SlotRole.Inventory, SlotType.Skill, -1, this, view);
            return slot;
        }

        private LoadoutSlot[] SlotsOf(SlotType type)
        {
            switch (type)
            {
                case SlotType.Skill: return skillSlots;
                case SlotType.Passive: return passiveSlots;
                case SlotType.Equipment: return equipmentSlots;
                default: return beltSlots;
            }
        }

        // ---------- Arrastar e soltar (chamado pelos LoadoutSlot) ----------

        internal void BeginDrag(LoadoutSlot source, PointerEventData eventData)
        {
            if (cards == null || cards.Database == null || source.CardId < 0 || dragLayer == null)
                return; // espaço vazio não arrasta

            CardDatabase database = cards.Database;
            if (database.Get(source.CardId) == null)
                return;

            dragSource = source;
            dragCardId = source.CardId;
            dragSlotType = CardRules.SlotFor(database.KindOf(dragCardId));
            releasedTime = 0f;
            HideHover();
            source.SetHover(false);

            Vector2 size = source.Rect.rect.size;
            if (ghost == null)
                ghost = CardView.Create(dragLayer, "Fantasma", size);
            ghost.Rect.sizeDelta = size;
            ghost.Rect.localScale = new Vector3(1.1f, 1.1f, 1f);
            ghost.gameObject.SetActive(true);
            ghost.SetCard(database, dragCardId, 1);
            ghost.SetAlpha(0.92f);
            ghost.Rect.position = eventData.position; // tela sobreposta: posição do mundo = pixel da tela

            source.SetDimmed(true);
            SetCompatibleGlow(true);
        }

        internal void Drag(LoadoutSlot source, PointerEventData eventData)
        {
            if (dragSource == source && ghost != null)
                ghost.Rect.position = eventData.position;
        }

        internal void EndDrag(LoadoutSlot source)
        {
            if (dragSource == source)
                FinishDrag();
        }

        internal void Drop(LoadoutSlot target)
        {
            if (dragSource == null || dragCardId < 0 || cards == null)
                return;

            LoadoutSlot source = dragSource;
            if (target.Role == SlotRole.Slot)
            {
                if (source.Role == SlotRole.Slot)
                    TryMove(source, target);
                else
                    TryDropOnSlot(dragCardId, target.Slot, target.Index);
            }
            else if (source.Role == SlotRole.Slot)
            {
                TryUnequip(source.Slot, source.Index); // soltou no leque: volta ao inventário
            }
        }

        private void CancelDrag()
        {
            if (dragSource != null)
                FinishDrag();
        }

        private void FinishDrag()
        {
            if (dragSource != null)
                dragSource.SetDimmed(false);
            if (ghost != null)
                ghost.gameObject.SetActive(false);
            SetCompatibleGlow(false);
            dragSource = null;
            dragCardId = Loadout.Empty;
            dirty = true; // o que mudou durante o arrastar é redesenhado agora
        }

        private void WatchStuckDrag()
        {
            if (dragSource == null)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || mouse.leftButton.isPressed)
            {
                releasedTime = 0f;
                return;
            }

            releasedTime += Time.unscaledDeltaTime;
            if (releasedTime > StuckDragSeconds)
                CancelDrag();
        }

        private bool IsDropTarget(LoadoutSlot slot) =>
            dragSource != null && slot != dragSource && slot.Role == SlotRole.Slot && slot.Slot == dragSlotType;

        /// <summary>Só os espaços do tipo certo brilham, discretamente. Os incompatíveis ficam como estão.</summary>
        private void SetCompatibleGlow(bool on)
        {
            for (int t = 0; t < 4; t++)
            {
                var type = (SlotType)t;
                LoadoutSlot[] slots = SlotsOf(type);
                bool compatible = on && type == dragSlotType;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null)
                        continue;
                    slots[i].SetGlow(compatible && slots[i] != dragSource ? CardGlow.Compatible : CardGlow.None);
                }
            }
        }

        // ---------- Mouse por cima ----------

        internal void PointerEnter(LoadoutSlot slot)
        {
            if (dragSource != null)
            {
                if (IsDropTarget(slot))
                    slot.SetGlow(CardGlow.Target);
                return;
            }

            slot.SetHover(true);
            ShowHover(slot);
        }

        internal void PointerExit(LoadoutSlot slot)
        {
            slot.SetHover(false);
            if (dragSource != null)
            {
                if (IsDropTarget(slot))
                    slot.SetGlow(CardGlow.Compatible);
                return;
            }

            if (hovered == slot)
                HideHover();
        }

        /// <summary>Nome curto + carta de tarô, acima da carta. Sem texto longo, sem número.</summary>
        private void ShowHover(LoadoutSlot slot)
        {
            if (slot.CardId < 0 || cards == null || cards.Database == null || hoverBox == null || hoverText == null || panelRect == null)
                return;
            CardData data = cards.Database.Get(slot.CardId);
            if (data == null)
                return;

            hovered = slot;
            hoverText.text = CardView.Caption(data);
            hoverBox.gameObject.SetActive(true);
            float width = Mathf.Ceil(hoverText.preferredWidth) + 40f;
            hoverBox.sizeDelta = new Vector2(width, 48f);

            // Topo da carta em coordenadas da tela, depois para o espaço local do painel (centro = origem).
            RectTransform rect = slot.Rect;
            Vector3 top = rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMax, 0f));
            Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, top);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, screen, cam, out Vector2 local))
                return;

            float lift = slot.Role == SlotRole.Inventory ? 34f : 14f;
            float limit = Mathf.Max(0f, panelRect.rect.width * 0.5f - width * 0.5f);
            hoverBox.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -limit, limit), local.y + lift);
        }

        private void HideHover()
        {
            hovered = null;
            if (hoverBox != null && hoverBox.gameObject.activeSelf)
                hoverBox.gameObject.SetActive(false);
        }
    }
}
