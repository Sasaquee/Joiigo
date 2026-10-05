namespace Game.Core.Cards
{
    /// <summary>Qualidade de uma carta (D-048): muda um pouco os números e aparece só na moldura.</summary>
    public enum CardQuality
    {
        Worn = 0,     // gasta
        Good = 1,     // boa
        Perfect = 2   // perfeita
    }

    public static class QualityRules
    {
        /// <summary>
        /// Um degrau acima (D-051: a repetida melhora a que o jogador já tem):
        /// Worn → Good, Good → Perfect, Perfect → Perfect.
        /// </summary>
        public static CardQuality Next(CardQuality quality) => quality switch
        {
            CardQuality.Worn => CardQuality.Good,
            CardQuality.Good => CardQuality.Perfect,
            _ => CardQuality.Perfect // Perfect já é o topo
        };
    }
}
