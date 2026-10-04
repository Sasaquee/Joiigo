namespace Game.Core.Cards
{
    public enum CardKind
    {
        Skill,
        Item,
        Passive,
        Equipment
    }

    /// <summary>Naipes dos arcanos menores. Arcanos maiores não têm naipe.</summary>
    public enum Suit
    {
        None,
        Swords,
        Cups,
        Wands,
        Pentacles
    }

    public enum Arcana
    {
        Minor,
        Major
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Unique
    }

    public enum SlotType
    {
        Skill,
        Passive,
        Equipment,
        Belt
    }

    /// <summary>Estrutura do tarô aplicada ao jogo (§3.3, D-036).</summary>
    public static class CardRules
    {
        public const int SkillSlots = 4;
        public const int PassiveSlots = 2;
        public const int EquipmentSlots = 2;
        public const int BeltSlots = 3; // D-029

        /// <summary>Espadas = skill, Copas = consumível, Paus = passiva, Ouros = equipamento (D-036).</summary>
        public static CardKind KindOf(Suit suit) => suit switch
        {
            Suit.Swords => CardKind.Skill,
            Suit.Cups => CardKind.Item,
            Suit.Wands => CardKind.Passive,
            Suit.Pentacles => CardKind.Equipment,
            _ => CardKind.Skill
        };

        public static SlotType SlotFor(CardKind kind) => kind switch
        {
            CardKind.Skill => SlotType.Skill,
            CardKind.Item => SlotType.Belt,
            CardKind.Passive => SlotType.Passive,
            _ => SlotType.Equipment
        };

        public static int SlotCount(SlotType slot) => slot switch
        {
            SlotType.Skill => SkillSlots,
            SlotType.Passive => PassiveSlots,
            SlotType.Equipment => EquipmentSlots,
            _ => BeltSlots
        };

        /// <summary>Numeral romano (1 a 21) para os arcanos maiores e o Ás.</summary>
        public static string Roman(int n)
        {
            if (n <= 0)
                return "0";
            int[] values = { 10, 9, 5, 4, 1 };
            string[] symbols = { "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < values.Length; i++)
                while (n >= values[i])
                {
                    sb.Append(symbols[i]);
                    n -= values[i];
                }
            return sb.ToString();
        }
    }
}
