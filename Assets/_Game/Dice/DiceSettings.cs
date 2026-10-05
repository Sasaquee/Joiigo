using System;
using System.Collections.Generic;
using Game.Core.Cards;
using Game.Core.Dice;
using UnityEngine;

namespace Game.Dice
{
    /// <summary>Uma faixa da tabela do D20, editável no Inspector (D-048).</summary>
    [Serializable]
    public struct DiceBandEntry
    {
        [Range(1, D20.Faces)] public int min;
        [Range(1, D20.Faces)] public int max;
        public Rarity rarity;
        public CardQuality quality;
        [Min(0)] public int extraCommons;
        public CardQuality extraQuality;
        [Tooltip("Crítico 1: a carta é amaldiçoada.")]
        public bool cursed;
        [Tooltip("Crítico 1: atrai perigo (emboscada, D-049).")]
        public bool danger;
        [Tooltip("Crítico 20: arcano maior ou carta única.")]
        public bool majorArcana;

        public DiceBand ToCore() => new DiceBand(min, max,
            new DiceOutcome(rarity, quality, extraCommons, extraQuality, cursed, danger, majorArcana));
    }

    /// <summary>
    /// Números provisórios da carta do chão e do D20 (Fase 6). O D20 em si é puro (D20 no Core);
    /// aqui ficam só a tabela, o peso do caminho e o perigo do 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dice/Dice Settings", fileName = "DiceSettings")]
    public class DiceSettings : ScriptableObject
    {
        [Header("Tabela do D20 (D-048)")]
        public DiceBandEntry[] bands = DefaultBands();

        [Header("Tema (caminho + lugar)")]
        [Tooltip("Peso extra das tags que o jogador mais usa no sorteio do tema (0 = o caminho não pesa).")]
        [Min(0f)] public float pathBias = 2f;
        [Tooltip("Quantas das tags mais usadas contam como o caminho do jogador.")]
        [Min(0)] public int pathTopTags = 2;

        [Header("Rolagem (D-047)")]
        [Tooltip("Quanto tempo o dado rola na tela até parar (s).")]
        [Min(0.5f)] public float rollDuration = 2.2f;
        [Tooltip("Depois que o dado para, espera isto antes de entregar a carta (s).")]
        [Min(0f)] public float grantDelay = 0.6f;

        [Header("Emboscada do 1 (D-049)")]
        [Min(0)] public int ambushMin = 2;
        [Min(0)] public int ambushMax = 3;
        [Tooltip("Distância do jogador onde os inimigos da emboscada surgem (m).")]
        [Min(1f)] public float ambushRadius = 5f;

        public DiceTable CreateTable()
        {
            var list = new List<DiceBand>();
            foreach (var b in bands)
                list.Add(b.ToCore());
            return new DiceTable(list);
        }

        /// <summary>D-048: 1 maldita + perigo · 2–6 comum gasta · 7–11 comum boa · 12–15 incomum boa · 16–18 incomum perfeita · 19 incomum perfeita + 1 comum · 20 arcano maior.</summary>
        public static DiceBandEntry[] DefaultBands() => new[]
        {
            new DiceBandEntry { min = 1, max = 1, rarity = Rarity.Common, quality = CardQuality.Worn, cursed = true, danger = true },
            new DiceBandEntry { min = 2, max = 6, rarity = Rarity.Common, quality = CardQuality.Worn },
            new DiceBandEntry { min = 7, max = 11, rarity = Rarity.Common, quality = CardQuality.Good },
            new DiceBandEntry { min = 12, max = 15, rarity = Rarity.Uncommon, quality = CardQuality.Good },
            new DiceBandEntry { min = 16, max = 18, rarity = Rarity.Uncommon, quality = CardQuality.Perfect },
            new DiceBandEntry { min = 19, max = 19, rarity = Rarity.Uncommon, quality = CardQuality.Perfect, extraCommons = 1, extraQuality = CardQuality.Good },
            new DiceBandEntry { min = 20, max = 20, rarity = Rarity.Unique, quality = CardQuality.Perfect, majorArcana = true },
        };
    }
}
