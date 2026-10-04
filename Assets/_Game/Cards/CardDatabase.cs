using System.Collections.Generic;
using Game.Core.Cards;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Todas as cartas do jogo. O índice na lista é o id que trafega na rede e que o Loadout (Core) usa.
    /// Só acrescente no fim: mudar a ordem muda os ids.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        public List<CardData> cards = new List<CardData>();

        public int Count => cards.Count;

        public CardData Get(int id) => id >= 0 && id < cards.Count ? cards[id] : null;

        public int IdOf(CardData card) => cards.IndexOf(card);

        public CardKind KindOf(int id) => Get(id) != null ? Get(id).Kind : CardKind.Skill;
    }
}
