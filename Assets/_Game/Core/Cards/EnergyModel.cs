using System;

namespace Game.Core.Cards
{
    /// <summary>
    /// Energia que as skills gastam. Volta devagar com o tempo, e mais rápido quando
    /// o golpe básico acerta (D-030): o golpe mecânico alimenta o arcano.
    /// </summary>
    public class EnergyModel
    {
        private readonly float regenPerSecond;

        public EnergyModel(float max, float regenPerSecond, float startFraction = 1f)
        {
            Max = MathF.Max(1f, max);
            this.regenPerSecond = regenPerSecond;
            Current = Max * System.Math.Clamp(startFraction, 0f, 1f);
        }

        public float Max { get; }
        public float Current { get; private set; }
        public float Fraction => Current / Max;

        public void Tick(float deltaTime) => Add(regenPerSecond * deltaTime);

        public void Add(float amount)
        {
            if (amount > 0f)
                Current = MathF.Min(Max, Current + amount);
        }

        public bool CanSpend(float cost) => Current >= cost;

        public bool TrySpend(float cost)
        {
            if (cost < 0f || Current < cost)
                return false;
            Current -= cost;
            return true;
        }
    }
}
