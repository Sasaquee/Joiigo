using System;
using Game.Core.Cards;
using Unity.Netcode;

namespace Game.Cards
{
    /// <summary>
    /// Cartas equipadas, publicadas pelo host. Campos fixos (4 skills, 2 passivas, 2 equipamentos,
    /// 3 espaços de cinto com id e quantidade). Guarda id + 1, então o valor default é "tudo vazio".
    /// </summary>
    public struct LoadoutState : INetworkSerializable, IEquatable<LoadoutState>
    {
        public int Skill0, Skill1, Skill2, Skill3;
        public int Passive0, Passive1;
        public int Equip0, Equip1;
        public int Belt0, Belt1, Belt2;
        public int BeltCount0, BeltCount1, BeltCount2;

        /// <summary>Id da carta no espaço, ou Loadout.Empty (-1).</summary>
        public int Get(SlotType slot, int index) => Raw(slot, index) - 1;

        public int BeltCount(int index) => index switch
        {
            0 => BeltCount0,
            1 => BeltCount1,
            2 => BeltCount2,
            _ => 0
        };

        public static LoadoutState From(Loadout loadout)
        {
            var s = new LoadoutState();
            for (int i = 0; i < CardRules.SkillSlots; i++)
                s.Set(SlotType.Skill, i, loadout.Get(SlotType.Skill, i));
            for (int i = 0; i < CardRules.PassiveSlots; i++)
                s.Set(SlotType.Passive, i, loadout.Get(SlotType.Passive, i));
            for (int i = 0; i < CardRules.EquipmentSlots; i++)
                s.Set(SlotType.Equipment, i, loadout.Get(SlotType.Equipment, i));
            for (int i = 0; i < CardRules.BeltSlots; i++)
            {
                s.Set(SlotType.Belt, i, loadout.Get(SlotType.Belt, i));
                s.SetBeltCount(i, loadout.BeltCount(i));
            }
            return s;
        }

        private int Raw(SlotType slot, int index)
        {
            switch (slot)
            {
                case SlotType.Skill:
                    return index switch { 0 => Skill0, 1 => Skill1, 2 => Skill2, 3 => Skill3, _ => 0 };
                case SlotType.Passive:
                    return index switch { 0 => Passive0, 1 => Passive1, _ => 0 };
                case SlotType.Equipment:
                    return index switch { 0 => Equip0, 1 => Equip1, _ => 0 };
                default:
                    return index switch { 0 => Belt0, 1 => Belt1, 2 => Belt2, _ => 0 };
            }
        }

        private void Set(SlotType slot, int index, int cardId)
        {
            int raw = cardId + 1;
            switch (slot)
            {
                case SlotType.Skill:
                    if (index == 0) Skill0 = raw;
                    else if (index == 1) Skill1 = raw;
                    else if (index == 2) Skill2 = raw;
                    else if (index == 3) Skill3 = raw;
                    break;
                case SlotType.Passive:
                    if (index == 0) Passive0 = raw;
                    else if (index == 1) Passive1 = raw;
                    break;
                case SlotType.Equipment:
                    if (index == 0) Equip0 = raw;
                    else if (index == 1) Equip1 = raw;
                    break;
                default:
                    if (index == 0) Belt0 = raw;
                    else if (index == 1) Belt1 = raw;
                    else if (index == 2) Belt2 = raw;
                    break;
            }
        }

        private void SetBeltCount(int index, int count)
        {
            if (index == 0) BeltCount0 = count;
            else if (index == 1) BeltCount1 = count;
            else if (index == 2) BeltCount2 = count;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Skill0);
            serializer.SerializeValue(ref Skill1);
            serializer.SerializeValue(ref Skill2);
            serializer.SerializeValue(ref Skill3);
            serializer.SerializeValue(ref Passive0);
            serializer.SerializeValue(ref Passive1);
            serializer.SerializeValue(ref Equip0);
            serializer.SerializeValue(ref Equip1);
            serializer.SerializeValue(ref Belt0);
            serializer.SerializeValue(ref Belt1);
            serializer.SerializeValue(ref Belt2);
            serializer.SerializeValue(ref BeltCount0);
            serializer.SerializeValue(ref BeltCount1);
            serializer.SerializeValue(ref BeltCount2);
        }

        public bool Equals(LoadoutState o) =>
            Skill0 == o.Skill0 && Skill1 == o.Skill1 && Skill2 == o.Skill2 && Skill3 == o.Skill3
            && Passive0 == o.Passive0 && Passive1 == o.Passive1
            && Equip0 == o.Equip0 && Equip1 == o.Equip1
            && Belt0 == o.Belt0 && Belt1 == o.Belt1 && Belt2 == o.Belt2
            && BeltCount0 == o.BeltCount0 && BeltCount1 == o.BeltCount1 && BeltCount2 == o.BeltCount2;

        public override bool Equals(object obj) => obj is LoadoutState other && Equals(other);

        public override int GetHashCode()
        {
            var h = new HashCode();
            h.Add(Skill0); h.Add(Skill1); h.Add(Skill2); h.Add(Skill3);
            h.Add(Passive0); h.Add(Passive1); h.Add(Equip0); h.Add(Equip1);
            h.Add(Belt0); h.Add(Belt1); h.Add(Belt2);
            h.Add(BeltCount0); h.Add(BeltCount1); h.Add(BeltCount2);
            return h.ToHashCode();
        }
    }

    /// <summary>
    /// Recarga das 4 skills como "fim" em tempo de servidor (NetworkManager.ServerTime) + duração.
    /// O cliente calcula a fração que falta sozinho, sem receber um valor por quadro.
    /// </summary>
    public struct CooldownState : INetworkSerializable, IEquatable<CooldownState>
    {
        public double End0, End1, End2, End3;
        public float Duration0, Duration1, Duration2, Duration3;

        public double EndOf(int index) => index switch { 0 => End0, 1 => End1, 2 => End2, 3 => End3, _ => 0d };

        public float DurationOf(int index) =>
            index switch { 0 => Duration0, 1 => Duration1, 2 => Duration2, 3 => Duration3, _ => 0f };

        public void Set(int index, double end, float duration)
        {
            switch (index)
            {
                case 0: End0 = end; Duration0 = duration; break;
                case 1: End1 = end; Duration1 = duration; break;
                case 2: End2 = end; Duration2 = duration; break;
                case 3: End3 = end; Duration3 = duration; break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref End0);
            serializer.SerializeValue(ref End1);
            serializer.SerializeValue(ref End2);
            serializer.SerializeValue(ref End3);
            serializer.SerializeValue(ref Duration0);
            serializer.SerializeValue(ref Duration1);
            serializer.SerializeValue(ref Duration2);
            serializer.SerializeValue(ref Duration3);
        }

        public bool Equals(CooldownState o) =>
            End0 == o.End0 && End1 == o.End1 && End2 == o.End2 && End3 == o.End3
            && Duration0 == o.Duration0 && Duration1 == o.Duration1
            && Duration2 == o.Duration2 && Duration3 == o.Duration3;

        public override bool Equals(object obj) => obj is CooldownState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(End0, End1, End2, End3, HashCode.Combine(Duration0, Duration1, Duration2, Duration3));
    }
}
