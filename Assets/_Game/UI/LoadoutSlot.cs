using Game.Cards;
using Game.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public enum SlotRole
    {
        /// <summary>Espaço fixo da tiragem (skill, passiva, equipamento ou cinto): recebe cartas e entrega a que tem.</summary>
        Slot,
        /// <summary>Uma carta do inventário no leque: só entrega (arrasta).</summary>
        Inventory,
        /// <summary>O fundo do leque: soltar aqui uma carta equipada devolve ao inventário.</summary>
        InventoryArea
    }

    /// <summary>
    /// Tudo o que o mouse faz sobre um espaço da tiragem: arrastar, soltar, passar por cima.
    /// Fica no objeto que tem a área de clique (o CardView é filho e não recebe o mouse), e repassa tudo à LoadoutScreen.
    /// </summary>
    public class LoadoutSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SlotRole role;
        [SerializeField] private SlotType slotType;
        [SerializeField] private int index;
        [SerializeField] private LoadoutScreen owner;
        [SerializeField] private CardView view;

        public SlotRole Role => role;

        /// <summary>Tipo do espaço (só vale quando Role == Slot).</summary>
        public SlotType Slot => slotType;

        /// <summary>Posição do espaço dentro do tipo (só vale quando Role == Slot).</summary>
        public int Index => index;

        public CardView View => view;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Carta que o espaço mostra agora (id do banco), ou -1.</summary>
        public int CardId { get; private set; } = Loadout.Empty;

        /// <summary>Quantas cópias empilhadas (cinto).</summary>
        public int Copies { get; private set; }

        public void Configure(SlotRole newRole, SlotType newSlot, int newIndex, LoadoutScreen newOwner, CardView newView)
        {
            role = newRole;
            slotType = newSlot;
            index = newIndex;
            owner = newOwner;
            view = newView;
        }

        /// <summary>Atualiza o que o espaço mostra. Id inválido ou -1 deixa o espaço vazio.</summary>
        public void SetContent(CardDatabase database, int cardId, int copyCount)
        {
            bool valid = database != null && database.Get(cardId) != null;
            CardId = valid ? cardId : Loadout.Empty;
            Copies = valid ? Mathf.Max(1, copyCount) : 0;
            if (view == null)
                return;

            if (valid)
                view.SetCard(database, cardId, Copies);
            else
                view.Clear();
        }

        public void SetGlow(CardGlow glow)
        {
            if (view != null)
                view.SetGlow(glow);
        }

        /// <summary>A carta de origem fica esmaecida enquanto o fantasma dela é arrastado.</summary>
        public void SetDimmed(bool dimmed)
        {
            if (view != null)
                view.SetAlpha(dimmed ? 0.35f : 1f);
        }

        /// <summary>Mouse por cima: a carta cresce um pouco; no leque, também sobe, para não ficar escondida pelas vizinhas.</summary>
        public void SetHover(bool hovered)
        {
            if (view == null)
                return;

            bool lift = hovered && CardId >= 0;
            float scale = lift ? 1.08f : 1f;
            view.Rect.localScale = new Vector3(scale, scale, 1f);
            view.Rect.anchoredPosition = lift && role == SlotRole.Inventory
                ? new Vector2(0f, Rect.rect.height * 0.14f)
                : Vector2.zero;
        }

        private void OnDisable() => SetHover(false);

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner != null && eventData.button == PointerEventData.InputButton.Left)
                owner.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (owner != null)
                owner.Drag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (owner != null)
                owner.EndDrag(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (owner != null)
                owner.Drop(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null)
                owner.PointerEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (owner != null)
                owner.PointerExit(this);
        }
    }
}
