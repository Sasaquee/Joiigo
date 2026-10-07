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

        /// <summary>
        /// Muda a vida máxima (cartas de vida máxima, Fase 10) mantendo a FRAÇÃO da vida atual: equipar +30 com a vida cheia deixa
        /// a vida cheia; desequipar nunca mata. O máximo nunca fica abaixo de 1. Quem já está sem vida continua sem vida.
        /// </summary>
        public void SetMax(float newMax)
        {
            float max = float.IsNaN(newMax) ? Max : MathF.Max(1f, newMax);
            if (max == Max)
                return;
            float fraction = IsDepleted ? 0f : Current / Max;
            Max = max;
            Current = MathF.Min(Max, Max * fraction);
        }

        /// <summary>
        /// Volta com uma fração da vida máxima (quem foi levantado por um aliado, D-083). Nunca deixa zero:
        /// quem levanta está vivo, então o mínimo é 1 de vida.
        /// </summary>
        public void RestoreTo(float fraction)
        {
            float f = float.IsNaN(fraction) ? 0f : MathF.Min(1f, MathF.Max(0f, fraction));
            Current = MathF.Max(MathF.Min(1f, Max), Max * f);
        }
    }
}
