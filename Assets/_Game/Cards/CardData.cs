using System;
using System.Collections.Generic;
using Game.Core.Cards;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>Um modificador de passiva ou equipamento, editável no Inspector.</summary>
    [Serializable]
    public struct ModifierEntry
    {
        public ModifierKind kind;
        public float value;

        public Modifier ToCore() => new Modifier(kind, value);
    }

    /// <summary>
    /// Uma carta de tarô (§3.3, §4.4). Criar uma carta nova = criar um asset destes e combinar
    /// efeitos já existentes, sem código. Conteúdo aprovado em Docs/Design/cartas-prototipo.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Card", fileName = "Card")]
    public class CardData : ScriptableObject
    {
        [Header("Identidade")]
        [Tooltip("Identificador estável (não muda ao renomear).")]
        public string id = "carta";
        [Tooltip("Nome curto mostrado na carta.")]
        public string displayName = "Carta";
        public Arcana arcana = Arcana.Minor;
        [Tooltip("Só arcanos menores.")]
        public Suit suit = Suit.Swords;
        [Tooltip("Menores: 1 (Ás) a 10, 11 Pajem, 12 Cavaleiro, 13 Rainha, 14 Rei. Maiores: 0 a 21.")]
        public int number = 1;
        [Tooltip("Tipo de jogo. Nos menores segue o naipe (D-036); nos maiores é escolhido aqui.")]
        public CardKind kind = CardKind.Skill;
        [Tooltip("Temas internos (vapor, engrenagem, faisca, cristal). Nunca aparecem na tela.")]
        public string[] tags = Array.Empty<string>();
        public Rarity rarity = Rarity.Common;
        public bool cursed;

        [Header("Uso (skills e consumíveis)")]
        [Min(0f)] public float energyCost;
        [Min(0f)] public float cooldown;
        [Tooltip("Vida cobrada a cada uso (cartas amaldiçoadas).")]
        [Min(0f)] public float healthCostOnUse;
        public List<CardEffect> effects = new List<CardEffect>();

        [Header("Passivas e equipamentos")]
        public List<ModifierEntry> modifiers = new List<ModifierEntry>();

        [Header("Visual")]
        [Tooltip("Carta pronta (moldura + ilustração), renderizada no Blender (D-034).")]
        public Texture2D face;

        /// <summary>Tipo efetivo: nos menores vem do naipe, nos maiores do campo kind.</summary>
        public CardKind Kind => arcana == Arcana.Minor ? CardRules.KindOf(suit) : kind;

        /// <summary>"Ás de Espadas", "Rainha de Ouros", "XVI".</summary>
        public string TarotLabel
        {
            get
            {
                if (arcana == Arcana.Major)
                    return CardRules.Roman(number);
                string rank = number switch
                {
                    1 => "Ás",
                    11 => "Pajem",
                    12 => "Cavaleiro",
                    13 => "Rainha",
                    14 => "Rei",
                    _ => number.ToString()
                };
                string suitName = suit switch
                {
                    Suit.Swords => "Espadas",
                    Suit.Cups => "Copas",
                    Suit.Wands => "Paus",
                    Suit.Pentacles => "Ouros",
                    _ => ""
                };
                return $"{rank} de {suitName}";
            }
        }
    }
}
