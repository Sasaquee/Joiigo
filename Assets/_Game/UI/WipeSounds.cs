using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Som grave da queda total (D-086), sintetizado aqui (sem arquivo de áudio), no mesmo espírito do CriticalSounds:
    /// um rosnado que cai de ~70 Hz para ~34 Hz, cresce enquanto a tela escurece, e um baque surdo quando fica tudo preto.
    /// A 2ª e a 3ª harmônica e uma saturação leve deixam o grave audível em alto-falante pequeno.
    /// </summary>
    public static class WipeSounds
    {
        private const int SampleRate = 44100;
        private const float Tail = 1.2f;   // quanto o som segue depois do preto cheio (só desenho do som, não é regra de jogo)
        private const float Second = 0.45f;
        private const float Third = 0.3f;
        private const float Drive = 1.6f;

        /// <summary>
        /// Rosnado que acompanha o escurecer: cresce durante swellSeconds, bate no preto cheio e morre devagar.
        /// Dura swellSeconds mais a cauda.
        /// </summary>
        public static AudioClip MakeFadeRumble(float swellSeconds)
        {
            float swell = Mathf.Max(0.05f, swellSeconds);
            int n = Mathf.RoundToInt(SampleRate * (swell + Tail));
            var data = new float[n];
            float[] rumble = LowNoise(n, 0.015f, 41);
            float phase = 0f;
            float hitPhase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                // Envelope: sobe devagar até o preto cheio (curva suave) e depois cai.
                float env = t < swell
                    ? Mathf.Pow(t / swell, 1.6f)
                    : Mathf.Exp(-(t - swell) * 2.6f);

                // Rosnado: cai de ~70 Hz para ~34 Hz, com um tremor lento.
                float f = 34f + 36f * Mathf.Exp(-t * 0.9f);
                phase += 2f * Mathf.PI * f / SampleRate;
                float tremor = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 4.5f * t);
                float tone = Harmonics(phase) * tremor;

                // Baque surdo quando o preto fecha.
                float after = t - swell;
                float hit = 0f;
                if (after >= 0f)
                {
                    float fHit = 38f + 60f * Mathf.Exp(-after * 16f);
                    hitPhase += 2f * Mathf.PI * fHit / SampleRate;
                    hit = Harmonics(hitPhase) * Mathf.Min(1f, after / 0.004f) * Mathf.Exp(-after * 5f);
                }

                data[i] = env * (0.7f * tone + 0.5f * rumble[i]) + 0.9f * hit;
            }

            Saturate(data);
            var clip = AudioClip.Create("QuedaTotalGrave", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Harmonics(float phase) =>
            Mathf.Sin(phase) + Second * Mathf.Sin(2f * phase) + Third * Mathf.Sin(3f * phase);

        /// <summary>Normaliza para pico 1, passa por tanh (soma harmônicas ímpares ao grave) e normaliza de novo para 0,92.</summary>
        private static void Saturate(float[] data)
        {
            Normalize(data, 1f);
            float scale = 1f / (float)System.Math.Tanh(Drive);
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)System.Math.Tanh(Drive * data[i]) * scale;
            Normalize(data, 0.92f);
        }

        /// <summary>Ruído só com o grave (dois filtros passa-baixa de um polo), com pico 1.</summary>
        private static float[] LowNoise(int n, float coefficient, int seed)
        {
            var rng = new System.Random(seed);
            var data = new float[n];
            float a = 0f;
            float b = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                a += coefficient * (white - a);
                b += coefficient * (a - b);
                data[i] = b;
            }
            Normalize(data, 1f);
            return data;
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-6f)
                return;
            float k = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= k;
        }
    }
}
