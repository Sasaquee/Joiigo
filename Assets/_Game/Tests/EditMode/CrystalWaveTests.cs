using System;
using Game.Core.Ambience;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Onda do cristal vivo (D-069): brilho entre o mínimo e o máximo, variando no tempo, e a crista correndo.</summary>
    public class CrystalWaveTests
    {
        private const float Tol = 1e-4f;

        // Mesmos padrões de CrystalAmbienceSettings (veios).
        private const float Speed = 0.22f;
        private const float WaveLength = 14f;
        private const float Min = 0.45f;
        private const float Max = 1.1f;

        private static float VeinGlow(float time, float offset)
            => CrystalWave.Glow(CrystalWave.Cycle(time, Speed, 0f, offset, WaveLength), Min, Max, CrystalWave.VeinSharpness);

        [Test]
        public void Brilho_FicaEntreMinimoEMaximo_EChegaNosDois()
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float t = 0f; t < 10f; t += 0.005f)
            {
                float g = VeinGlow(t, 0f);
                Assert.GreaterOrEqual(g, Min - Tol);
                Assert.LessOrEqual(g, Max + Tol);
                lo = Math.Min(lo, g);
                hi = Math.Max(hi, g);
            }
            Assert.AreEqual(Min, lo, 1e-3f, "Chega no vale");
            Assert.AreEqual(Max, hi, 1e-3f, "Chega na crista");
        }

        [Test]
        public void Brilho_VariaNoTempo()
        {
            // Um quarto de pulso depois (1 / 0,22 / 4 ≈ 1,14 s) o brilho já é outro.
            float a = VeinGlow(0f, 0f);
            float b = VeinGlow(1f / Speed / 4f, 0f);
            Assert.Greater(Math.Abs(a - b), 0.1f);
        }

        [Test]
        public void Crista_NoCiclo025()
        {
            Assert.AreEqual(1f, CrystalWave.Shape(0.25f, CrystalWave.VeinSharpness), Tol);
            Assert.AreEqual(0f, CrystalWave.Shape(0.75f, CrystalWave.VeinSharpness), Tol);
            Assert.AreEqual(0.5f, CrystalWave.Shape(0f, CrystalWave.BreathSharpness), Tol);
        }

        /// <summary>Acha o deslocamento (0 a um comprimento de onda) mais brilhante no instante t.</summary>
        private static float CrestOffset(float t)
        {
            float best = 0f, bestGlow = float.MinValue;
            for (float x = 0f; x < WaveLength; x += 0.01f)
            {
                float g = VeinGlow(t, x);
                if (g > bestGlow)
                {
                    bestGlow = g;
                    best = x;
                }
            }
            return best;
        }

        [Test]
        public void Crista_CorreParaOsDeslocamentosMaiores()
        {
            // A energia sai da caldeira (deslocamento 0) e corre pelo cano: a crista anda velocidade × comprimento m/s.
            float dt = 1f;
            float c0 = CrestOffset(2f);
            float c1 = CrestOffset(2f + dt);
            float moved = (c1 - c0 + WaveLength) % WaveLength;
            Assert.AreEqual(Speed * WaveLength * dt, moved, 0.05f);
        }

        [Test]
        public void Brilho_SemDeslocamento_NaoDependeDoComprimentoDeOnda()
        {
            float t = 3.3f;
            float a = CrystalWave.Glow(CrystalWave.Cycle(t, Speed, 0.2f, 0f, 5f), Min, Max, 1f);
            float b = CrystalWave.Glow(CrystalWave.Cycle(t, Speed, 0.2f, 0f, 50f), Min, Max, 1f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void Luz_SobeEDesceDentroDaAmplitude()
        {
            for (float c = 0f; c < 1f; c += 0.01f)
            {
                float k = CrystalWave.LightFactor(c, 0.3f);
                Assert.GreaterOrEqual(k, 0.7f - Tol);
                Assert.LessOrEqual(k, 1.3f + Tol);
            }
            Assert.AreEqual(1.3f, CrystalWave.LightFactor(0.25f, 0.3f), Tol);
            Assert.AreEqual(0.7f, CrystalWave.LightFactor(0.75f, 0.3f), Tol);
        }
    }
}
