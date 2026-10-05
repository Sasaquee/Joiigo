using System.Collections.Generic;
using Game.Core.Cards;

namespace Game.Core.Dice
{
    /// <summary>O que um resultado do D20 dá (D-048). Nunca diz o tema da carta (Pilar 2).</summary>
    public readonly struct DiceOutcome
    {
        /// <summary>Raridade da carta principal (ignorada se Cursed ou MajorArcana).</summary>
        public readonly Rarity Rarity;
        /// <summary>Qualidade da carta principal quando ela é nova.</summary>
        public readonly CardQuality Quality;
        /// <summary>Cartas comuns extras (D-048: 1 no 19).</summary>
        public readonly int ExtraCommons;
        /// <summary>Qualidade das comuns extras quando são novas.</summary>
        public readonly CardQuality ExtraQuality;
        /// <summary>Crítico 1: a carta principal é amaldiçoada.</summary>
        public readonly bool Cursed;
        /// <summary>Crítico 1: o resultado atrai perigo (emboscada, D-049).</summary>
        public readonly bool Danger;
        /// <summary>Crítico 20: a carta principal é um arcano maior ou carta única.</summary>
        public readonly bool MajorArcana;

        public DiceOutcome(Rarity rarity, CardQuality quality, int extraCommons, CardQuality extraQuality,
            bool cursed, bool danger, bool majorArcana)
        {
            Rarity = rarity;
            Quality = quality;
            ExtraCommons = extraCommons;
            ExtraQuality = extraQuality;
            Cursed = cursed;
            Danger = danger;
            MajorArcana = majorArcana;
        }
    }

    /// <summary>Faixa [Min, Max] (inclusiva) da tabela.</summary>
    public readonly struct DiceBand
    {
        public readonly int Min;
        public readonly int Max;
        public readonly DiceOutcome Outcome;

        public DiceBand(int min, int max, DiceOutcome outcome)
        {
            Min = min;
            Max = max;
            Outcome = outcome;
        }
    }

    /// <summary>Tabela do D20 (§4.5). As faixas vêm de dados (DiceSettings); aqui só a regra.</summary>
    public sealed class DiceTable
    {
        private readonly List<DiceBand> sortedBands;

        /// <summary>
        /// Guarda uma cópia das faixas ordenadas por Min. A tabela precisa cobrir 1..D20.Faces
        /// sem buraco nem sobreposição (a ordem de entrada é livre) e cada faixa precisa ter
        /// Min <= Max dentro dos limites do dado.
        /// </summary>
        public DiceTable(IReadOnlyList<DiceBand> bands)
        {
            if (bands == null || bands.Count == 0)
                throw new System.ArgumentException("A tabela do D20 precisa de pelo menos uma faixa.", nameof(bands));

            sortedBands = new List<DiceBand>(bands);
            sortedBands.Sort((a, b) => a.Min.CompareTo(b.Min));

            int expected = 1; // próximo número que precisa estar coberto
            foreach (DiceBand band in sortedBands)
            {
                if (band.Min > band.Max)
                    throw new System.ArgumentException(
                        $"Faixa invertida na tabela do D20: {band.Min} a {band.Max}.", nameof(bands));
                if (band.Min < 1 || band.Max > D20.Faces)
                    throw new System.ArgumentException(
                        $"Faixa fora do dado na tabela do D20: {band.Min} a {band.Max} (o dado vai de 1 a {D20.Faces}).",
                        nameof(bands));
                if (band.Min > expected)
                    throw new System.ArgumentException(
                        $"Buraco na tabela do D20: falta o {expected}.", nameof(bands));
                if (band.Min < expected)
                    throw new System.ArgumentException(
                        $"Sobreposição na tabela do D20: o {band.Min} aparece em mais de uma faixa.", nameof(bands));
                expected = band.Max + 1;
            }
            if (expected <= D20.Faces)
                throw new System.ArgumentException(
                    $"Buraco na tabela do D20: falta o {expected}.", nameof(bands));
        }

        /// <summary>Faixas ordenadas por Min.</summary>
        public IReadOnlyList<DiceBand> Bands => sortedBands;

        /// <summary>Resultado da rolagem. Roll fora de 1..20 gera System.ArgumentOutOfRangeException.</summary>
        public DiceOutcome Resolve(int roll)
        {
            if (roll < 1 || roll > D20.Faces)
                throw new System.ArgumentOutOfRangeException(nameof(roll), roll,
                    $"A rolagem precisa estar entre 1 e {D20.Faces}; veio {roll}.");
            foreach (DiceBand band in sortedBands)
                if (roll >= band.Min && roll <= band.Max)
                    return band.Outcome;
            // Inalcançável: a validação do construtor garante que as faixas cobrem 1..Faces.
            throw new System.InvalidOperationException($"A tabela do D20 não tem faixa para a rolagem {roll}.");
        }
    }
}
