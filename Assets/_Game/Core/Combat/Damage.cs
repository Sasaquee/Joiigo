using System;

namespace Game.Core.Combat
{
    /// <summary>
    /// Um golpe. Todo dano mistura o mecânico e o arcano (D-021, Pilar 4):
    /// ArcaneFraction = 0 é puro mecânico, 1 é puro arcano.
    /// </summary>
    public readonly struct DamagePacket
    {
        public readonly float Amount;
        public readonly float ArcaneFraction;

        public DamagePacket(float amount, float arcaneFraction)
        {
            Amount = MathF.Max(0f, amount);
            ArcaneFraction = System.Math.Clamp(arcaneFraction, 0f, 1f);
        }

        public float MechanicalPart => Amount * (1f - ArcaneFraction);
        public float ArcanePart => Amount * ArcaneFraction;
    }

    /// <summary>Quanto de cada parte do golpe é cortado (0 = nada, 1 = tudo).</summary>
    public readonly struct Resistances
    {
        public readonly float Mechanical;
        public readonly float Arcane;

        public Resistances(float mechanical, float arcane)
        {
            Mechanical = System.Math.Clamp(mechanical, 0f, 1f);
            Arcane = System.Math.Clamp(arcane, 0f, 1f);
        }

        public static Resistances None => new Resistances(0f, 0f);
    }

    public static class DamageCalculator
    {
        /// <summary>Dano final: cada parte do golpe é reduzida pela resistência correspondente.</summary>
        public static float Compute(DamagePacket packet, Resistances resist) =>
            packet.MechanicalPart * (1f - resist.Mechanical) + packet.ArcanePart * (1f - resist.Arcane);
    }
}
