using System;

namespace Game.Core.Aura
{
    /// <summary>
    /// Suavização do HP e da energia mostrados pela aura. Enquanto o jogador não está pronto (fora da rede, a vida e a
    /// energia ainda não chegaram), acompanha o alvo sem suavizar; o primeiro quadro pronto também copia o alvo, e só
    /// depois suaviza. Assim a energia não "sobe" do zero ao nascer (e o pulso de energia cheia não toca ao nascer).
    /// </summary>
    public sealed class AuraSmoother
    {
        public float Health { get; private set; } = 1f;
        public float Energy { get; private set; }
        public bool Initialized { get; private set; }

        /// <param name="ready">O jogador já está em rede (os valores alvo são de verdade).</param>
        /// <param name="smoothing">Rapidez (maior = mais rápida); 1 - e^(-smoothing·dt) do caminho por quadro.</param>
        public void Step(float targetHealth, float targetEnergy, bool ready, float smoothing, float dt)
        {
            if (!Initialized || !ready)
            {
                Health = targetHealth;
                Energy = targetEnergy;
                Initialized = ready;
                return;
            }
            float k = dt > 0f && smoothing > 0f ? 1f - MathF.Exp(-smoothing * dt) : 0f;
            Health += (targetHealth - Health) * k;
            Energy += (targetEnergy - Energy) * k;
        }
    }
}
