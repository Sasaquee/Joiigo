using System;

namespace Game.Core.Ambience
{
    /// <summary>
    /// Onda do cristal vivo (D-069), sem Unity: o CrystalPulse (Arena) só aplica o resultado na emissão e na luz.
    /// O ciclo é medido em voltas (1 = um pulso inteiro). Nos veios, cada peça tem um deslocamento em metros e a
    /// crista anda para o lado dos deslocamentos maiores (a energia sai da caldeira e corre pelo cano).
    /// </summary>
    public static class CrystalWave
    {
        /// <summary>Nos veios a crista é mais estreita que o vale, para ler como luz correndo (e não como pisca).</summary>
        public const float VeinSharpness = 2.2f;

        /// <summary>Peças que só respiram (torres, máquinas): seno puro.</summary>
        public const float BreathSharpness = 1f;

        private const float MinWaveLength = 0.5f;

        /// <summary>
        /// Ciclo (em voltas) de uma peça no instante <paramref name="time"/>. Com deslocamento maior, a peça chega
        /// à crista depois: a crista anda <paramref name="pulsesPerSecond"/> × <paramref name="waveLength"/> m/s.
        /// </summary>
        public static float Cycle(float time, float pulsesPerSecond, float phase, float offsetMeters, float waveLength)
            => time * pulsesPerSecond + phase - offsetMeters / MathF.Max(MinWaveLength, waveLength);

        /// <summary>Forma da onda, de 0 (vale) a 1 (crista). A crista fica em ciclo 0,25 + n.</summary>
        public static float Shape(float cycle, float sharpness)
        {
            float w = 0.5f + 0.5f * MathF.Sin(cycle * MathF.PI * 2f);
            return sharpness == 1f ? w : MathF.Pow(w, MathF.Max(0.01f, sharpness));
        }

        /// <summary>Multiplicador da emissão: <paramref name="min"/> no vale, <paramref name="max"/> na crista.</summary>
        public static float Glow(float cycle, float min, float max, float sharpness)
            => min + (max - min) * Shape(cycle, sharpness);

        /// <summary>Multiplicador da luz que acompanha o cristal: 1 ± <paramref name="amplitude"/>.</summary>
        public static float LightFactor(float cycle, float amplitude)
            => 1f + amplitude * (Shape(cycle, BreathSharpness) * 2f - 1f);
    }
}
