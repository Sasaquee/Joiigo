using System;

namespace Game.Core.Aura
{
    /// <summary>
    /// Mapeamento estado → aura (D-060 a D-064): C# puro, sem UnityEngine.
    /// A apresentação lê o AuraState e anima; número de jogo fica no ScriptableObject (AuraTuning).
    /// </summary>
    public static class AuraMapper
    {
        /// <summary>A partir desta fração a energia conta como cheia: runas inteiras (D-062) e pulso (D-067).</summary>
        public const float FullEnergy = 0.999f;

        /// <summary>Lerp(a, b, t) = a + (b - a) * t.</summary>
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Converte HP, energia e sinais nos parâmetros visuais da aura.</summary>
        public static AuraState Map(AuraInput input, AuraTuning tuning, AuraPalette palette)
        {
            if (tuning == null)
                throw new ArgumentNullException(nameof(tuning));

            float h = input.Health;
            float e = input.Energy;
            bool downed = (input.Signals & AuraSignals.Downed) != 0;

            float radius;
            float intensity;
            float flicker;
            float sparkRate;
            float sparkSpeed;
            bool runesFull;
            AuraSignals signals;
            float heartbeatBpm;
            float reviveProgress = 0f;

            if (downed)
            {
                // Caído: aura apagada, só brasa (D-063); os outros sinais somem. Com um aliado levantando (D-083), a aura
                // ganha luz e raio conforme o progresso; as runas acendem em anel no visual (ReviveProgress).
                reviveProgress = input.ReviveProgress;
                radius = Lerp(tuning.MinRadius, System.Math.Max(tuning.MinRadius, tuning.ReviveRadius), reviveProgress);
                intensity = Lerp(tuning.DownedIntensity, System.Math.Max(tuning.DownedIntensity, tuning.ReviveIntensity), reviveProgress);
                flicker = 0f;
                sparkRate = 0f;
                sparkSpeed = 0f;
                runesFull = false;
                signals = AuraSignals.Downed;
                heartbeatBpm = 0f;
            }
            else
            {
                // HP vira força e tamanho (D-061).
                radius = Lerp(tuning.MinRadius, 1f, h);
                intensity = Lerp(tuning.MinIntensity, 1f, h);
                flicker = tuning.FlickerBelow > 0f && h < tuning.FlickerBelow
                    ? (tuning.FlickerBelow - h) / tuning.FlickerBelow
                    : 0f;

                // Energia vira faíscas (D-062); cheia, acende as runas inteiras.
                sparkRate = e;
                sparkSpeed = Lerp(tuning.SparkMinSpeed, 1f, e);
                runesFull = e >= FullEnergy;
                signals = input.Signals;

                // Batimento grave com HP baixo, acelerando perto do zero (D-064).
                heartbeatBpm = tuning.HeartbeatBelow > 0f && h < tuning.HeartbeatBelow
                    ? Lerp(tuning.HeartbeatFastBpm, tuning.HeartbeatSlowBpm, h / tuning.HeartbeatBelow)
                    : 0f;
            }

            return new AuraState(radius, intensity, flicker, sparkRate, sparkSpeed, runesFull, signals, heartbeatBpm,
                AuraPalettes.Base(palette), AuraPalettes.Curse(palette),
                AuraPalettes.Ember(palette), AuraPalettes.Shell(palette), AuraPalettes.Gold(palette), reviveProgress);
        }
    }
}
