using System;

namespace Game.Core.Combat
{
    /// <summary>Vida de um personagem ou inimigo. Só o host usa; a cena só desenha o resultado.</summary>
    public class HealthModel
    {
        public HealthModel(float max)
        {
            Max = MathF.Max(1f, max);
            Current = Max;
        }

        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDepleted => Current <= 0f;
        public float Fraction => Current / Max;

        /// <summary>Aplica dano já calculado. Devolve o dano efetivo (não passa de zero).</summary>
        public float ApplyDamage(float amount)
        {
            if (IsDepleted || amount <= 0f)
                return 0f;
            float applied = MathF.Min(Current, amount);
            Current -= applied;
            return applied;
        }

        public float Heal(float amount)
        {
            if (amount <= 0f)
                return 0f;
            float applied = MathF.Min(Max - Current, amount);
            Current += applied;
            return applied;
        }

        public void Restore() => Current = Max;
    }
}
