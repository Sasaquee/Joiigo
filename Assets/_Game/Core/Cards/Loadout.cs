using System;
using System.Collections.Generic;

namespace Game.Core.Cards
{
    public enum EquipResult
    {
        Ok,
        NotOwned,
        WrongSlot,
        InvalidIndex
    }

    /// <summary>
    /// Cartas de um jogador: as que ele tem (inventário) e as equipadas
    /// (4 skills, 2 passivas, 2 equipamentos, cinto de 3 consumíveis). Só o host altera.
    /// As cartas são identificadas pelo índice no banco de cartas (CardDatabase).
    /// </summary>
    public class Loadout
    {
        public const int Empty = -1;

        private readonly Func<int, CardKind> kindOf;
        private readonly List<int> inventory = new List<int>();
        private readonly int[] skills = Filled(CardRules.SkillSlots);
        private readonly int[] passives = Filled(CardRules.PassiveSlots);
        private readonly int[] equipment = Filled(CardRules.EquipmentSlots);
        private readonly int[] belt = Filled(CardRules.BeltSlots);
        private readonly int[] beltCounts = new int[CardRules.BeltSlots];

        /// <param name="kindOf">Tipo de cada carta pelo id (vem do banco de cartas).</param>
        public Loadout(Func<int, CardKind> kindOf)
        {
            this.kindOf = kindOf;
        }

        /// <summary>Muda a cada alteração; serve para a rede saber quando republicar.</summary>
        public int Version { get; private set; }

        public IReadOnlyList<int> Inventory => inventory;

        public int Get(SlotType slot, int index) =>
            IsValid(slot, index) ? Array(slot)[index] : Empty;

        public int BeltCount(int index) =>
            index >= 0 && index < belt.Length ? beltCounts[index] : 0;

        public void AddToInventory(int cardId)
        {
            inventory.Add(cardId);
            Version++;
        }

        /// <summary>O jogador tem a carta (no inventário, equipada ou no cinto).</summary>
        public bool Has(int cardId)
        {
            if (cardId < 0)
                return false;
            if (inventory.Contains(cardId))
                return true;
            return System.Array.IndexOf(skills, cardId) >= 0 || System.Array.IndexOf(passives, cardId) >= 0
                || System.Array.IndexOf(equipment, cardId) >= 0 || System.Array.IndexOf(belt, cardId) >= 0;
        }

        public bool RemoveFromInventory(int cardId)
        {
            bool removed = inventory.Remove(cardId);
            if (removed)
                Version++;
            return removed;
        }

        /// <summary>
        /// Equipa uma carta do inventário no espaço pedido. A carta que estava lá volta ao inventário.
        /// No cinto, o mesmo consumível empilha.
        /// </summary>
        public EquipResult TryEquip(int cardId, SlotType slot, int index)
        {
            if (!IsValid(slot, index))
                return EquipResult.InvalidIndex;
            if (CardRules.SlotFor(kindOf(cardId)) != slot)
                return EquipResult.WrongSlot;
            if (!inventory.Contains(cardId))
                return EquipResult.NotOwned;

            int[] slots = Array(slot);
            inventory.Remove(cardId);

            if (slot == SlotType.Belt && slots[index] == cardId)
            {
                beltCounts[index]++;
            }
            else
            {
                ReturnToInventory(slot, index);
                slots[index] = cardId;
                if (slot == SlotType.Belt)
                    beltCounts[index] = 1;
            }

            Version++;
            return EquipResult.Ok;
        }

        /// <summary>Tira a carta do espaço e devolve ao inventário (todas as cópias, no cinto).</summary>
        public bool Unequip(SlotType slot, int index)
        {
            if (!IsValid(slot, index) || Array(slot)[index] == Empty)
                return false;
            ReturnToInventory(slot, index);
            Version++;
            return true;
        }

        /// <summary>Gasta um consumível do cinto. Devolve o id usado ou Empty.</summary>
        public int ConsumeBelt(int index)
        {
            if (!IsValid(SlotType.Belt, index) || belt[index] == Empty)
                return Empty;
            int id = belt[index];
            beltCounts[index]--;
            if (beltCounts[index] <= 0)
            {
                belt[index] = Empty;
                beltCounts[index] = 0;
            }
            Version++;
            return id;
        }

        /// <summary>Passivas e equipamentos em uso (para somar modificadores).</summary>
        public IEnumerable<int> EquippedModifierCards()
        {
            foreach (int id in passives)
                if (id != Empty)
                    yield return id;
            foreach (int id in equipment)
                if (id != Empty)
                    yield return id;
        }

        private void ReturnToInventory(SlotType slot, int index)
        {
            int[] slots = Array(slot);
            if (slots[index] == Empty)
                return;
            int copies = slot == SlotType.Belt ? beltCounts[index] : 1;
            for (int i = 0; i < copies; i++)
                inventory.Add(slots[index]);
            slots[index] = Empty;
            if (slot == SlotType.Belt)
                beltCounts[index] = 0;
        }

        private bool IsValid(SlotType slot, int index) => index >= 0 && index < CardRules.SlotCount(slot);

        private int[] Array(SlotType slot) => slot switch
        {
            SlotType.Skill => skills,
            SlotType.Passive => passives,
            SlotType.Equipment => equipment,
            _ => belt
        };

        private static int[] Filled(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++)
                a[i] = Empty;
            return a;
        }
    }
}
