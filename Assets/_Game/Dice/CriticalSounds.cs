using UnityEngine;

namespace Game.Dice
{
    /// <summary>
    /// Sons do 1 teatral (D-068), sintetizados aqui (sem arquivo): o baque grave do dado assentando no 1 e o ronco da
    /// terra quando os inimigos da emboscada surgem. Só desenho de som; quem toca é a tela do dado e o AmbushFx.
    /// </summary>
    public static class CriticalSounds
    {
        private const int SampleRate = 44100;
        // O grave mora em 33–56 Hz, que alto-falante pequeno (notebook, fone) quase não toca. A 2ª e a 3ª harmônica
        // (66–170 Hz) e uma saturação leve fazem o ouvido "completar" o grave nesses alto-falantes, sem perder o peso.
        private const float Second = 0.45f;
        private const float Third = 0.3f;
        private const float Drive = 1.8f;

        /// <summary>
        /// Baque: pancada grave com queda de altura, sub-grave longo que segura o peso, um toque curto de latão
        /// (o dado é de metal) e um ronco de terra que vai morrendo. ~2,2 s.
        /// </summary>
        public static AudioClip MakeThud()
        {
            int n = Mathf.RoundToInt(SampleRate * 2.2f);
            var low = new float[n];
            var brass = new float[n];
            float[] rumble = LowNoise(n, 0.02f, 11);
            float phaseHit = 0f;
            float phaseSub = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                // Pancada: de ~120 Hz caindo para 40 Hz em ~0,15 s.
                float fHit = 40f + 80f * Mathf.Exp(-t * 18f);
                phaseHit += 2f * Mathf.PI * fHit / SampleRate;
                float hit = Harmonics(phaseHit) * Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 6f);

                // Sub-grave: quase 33 Hz, longo.
                float fSub = 33f + 6f * Mathf.Exp(-t * 3f);
                phaseSub += 2f * Mathf.PI * fSub / SampleRate;
                float sub = Harmonics(phaseSub) * Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 1.6f);

                // Ronco de terra, oscilando devagar.
                float roll = rumble[i] * Mathf.Min(1f, t / 0.06f) * Mathf.Exp(-t * 1.3f) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t));

                low[i] = 0.9f * hit + 0.7f * sub + 0.55f * roll;

                // Latão: duas parciais inarmônicas que somem rápido (fora da saturação, para soar limpo).
                brass[i] = (Mathf.Sin(2f * Mathf.PI * 187f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 291f * t)) * Mathf.Exp(-t * 14f);
            }
            Saturate(low);
            for (int i = 0; i < n; i++)
                low[i] += 0.12f * brass[i];
            Normalize(low, 0.92f);
            return Clip("DadoBaqueDoUm", low);
        }

        /// <summary>
        /// Ronco da emboscada: três estalos de cristal rachando e um rosnado grave da terra que sobe e cai. ~1,6 s.
        /// </summary>
        public static AudioClip MakeAmbushRumble()
        {
            int n = Mathf.RoundToInt(SampleRate * 1.6f);
            var growlTrack = new float[n];
            var crackTrack = new float[n];
            float[] growl = LowNoise(n, 0.015f, 23);
            var rng = new System.Random(29);
            float[] crackStarts = { 0f, 0.05f, 0.13f };
            float low = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                // Estalos: ruído sem o grave, ataque seco e cauda muito curta.
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += 0.12f * (white - low);
                float bright = white - low;
                float crack = 0f;
                for (int c = 0; c < crackStarts.Length; c++)
                {
                    float dt = t - crackStarts[c];
                    if (dt >= 0f)
                        crack += Mathf.Exp(-dt * 45f) * (1f - c * 0.25f);
                }
                crackTrack[i] = bright * crack;

                // Rosnado: 44 Hz subindo a 56 Hz, com tremor, e ruído grave por baixo.
                float f = 44f + 12f * Mathf.Clamp01(t / 0.6f);
                phase += 2f * Mathf.PI * f / SampleRate;
                float env = Mathf.Min(1f, t / 0.1f) * Mathf.Exp(-t * 2f);
                float tone = Harmonics(phase) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                growlTrack[i] = env * (0.8f * tone + 0.6f * growl[i]);
            }
            Saturate(growlTrack);
            for (int i = 0; i < n; i++)
                growlTrack[i] += 0.35f * crackTrack[i];
            Normalize(growlTrack, 0.9f);
            return Clip("EmboscadaRonco", growlTrack);
        }

        /// <summary>Fundamental com 2ª e 3ª harmônicas (mesma fase), para o grave aparecer em alto-falante pequeno.</summary>
        private static float Harmonics(float phase) =>
            Mathf.Sin(phase) + Second * Mathf.Sin(2f * phase) + Third * Mathf.Sin(3f * phase);

        /// <summary>Normaliza para pico 1 e passa por uma saturação leve (tanh), que soma harmônicas ímpares ao grave.</summary>
        private static void Saturate(float[] data)
        {
            Normalize(data, 1f);
            float scale = 1f / (float)System.Math.Tanh(Drive);
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)System.Math.Tanh(Drive * data[i]) * scale;
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

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
