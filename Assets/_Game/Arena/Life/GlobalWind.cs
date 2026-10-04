using UnityEngine;

namespace Game.Arena.Life
{
    /// <summary>
    /// Vento global da cidade: varia devagar com ruído de Perlin sobre o tempo.
    /// Fumaça, vapor e brasas leem daqui para derivar todos na mesma direção, sem alocar nada por quadro.
    /// </summary>
    public static class GlobalWind
    {
        // Direção média (graus a partir de +Z) e o quanto ela pode se desviar.
        private const float BaseAngle = 70f;
        private const float AngleSwing = 70f;
        private const float MinStrength = 0.25f;
        private const float MaxStrength = 1.1f;
        private const float DirectionSpeed = 0.03f;
        private const float StrengthSpeed = 0.05f;

        private static int cachedFrame = -1;
        private static Vector3 cachedWind;

        /// <summary>Velocidade do vento em m/s, no plano XZ (y sempre 0).</summary>
        public static Vector3 Current
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != cachedFrame)
                {
                    cachedFrame = frame;
                    cachedWind = Evaluate(Time.time);
                }
                return cachedWind;
            }
        }

        private static Vector3 Evaluate(float time)
        {
            float dirNoise = Mathf.PerlinNoise(time * DirectionSpeed, 17.3f) * 2f - 1f;
            float strNoise = Mathf.PerlinNoise(time * StrengthSpeed, 41.7f);
            float angle = (BaseAngle + dirNoise * AngleSwing) * Mathf.Deg2Rad;
            float strength = Mathf.Lerp(MinStrength, MaxStrength, strNoise);
            return new Vector3(Mathf.Sin(angle) * strength, 0f, Mathf.Cos(angle) * strength);
        }
    }
}
