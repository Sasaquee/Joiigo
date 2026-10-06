using Game.Core.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Contas da translucidez dos prédios (D-076, D-079): raio na tela, fade, alvo tapado, seleção e regra do recorte.</summary>
    public class SeeThroughMathTests
    {
        private const float Fov = 40f;

        [Test]
        public void RaioNaTela_EInversamenteProporcionalAProfundidade()
        {
            float perto = SeeThroughMath.ScreenRadius(1.8f, 10f, Fov);
            float longe = SeeThroughMath.ScreenRadius(1.8f, 20f, Fov);
            Assert.AreEqual(perto, longe * 2f, 1e-5f, "O dobro da distância, metade do raio");
        }

        [Test]
        public void RaioNaTela_E_FracaoDaAlturaDaTela()
        {
            // A 14 m com FOV 40° a tela vê 2·14·tan(20°) = 10,19 m de altura: 1,8 m de raio = 17,7% dela.
            float r = SeeThroughMath.ScreenRadius(1.8f, 14f, Fov);
            Assert.AreEqual(1.8f / 10.19f, r, 1e-3f);
        }

        [Test]
        public void RaioNaTela_CresceComORaioDoMundo_ECaiComFovMaior()
        {
            Assert.Greater(SeeThroughMath.ScreenRadius(1.8f, 14f, Fov), SeeThroughMath.ScreenRadius(1.4f, 14f, Fov));
            Assert.Greater(SeeThroughMath.ScreenRadius(1.8f, 14f, 30f), SeeThroughMath.ScreenRadius(1.8f, 14f, 60f));
        }

        [Test]
        public void RaioNaTela_ProfundidadeZeroNaoDaInfinito()
        {
            Assert.IsFalse(float.IsInfinity(SeeThroughMath.ScreenRadius(1.8f, 0f, Fov)));
            Assert.IsFalse(float.IsNaN(SeeThroughMath.ScreenRadius(1.8f, -3f, Fov)));
        }

        [Test]
        public void Fade_AbreEm012s()
        {
            float open = 0f;
            for (int i = 0; i < 6; i++)
                open = SeeThroughMath.StepFade(open, true, 0.02f, 0.12f, 0.3f);
            Assert.AreEqual(1f, open, 1e-4f, "6 passos de 0,02 s = 0,12 s");
            Assert.AreEqual(0.5f, SeeThroughMath.StepFade(0f, true, 0.06f, 0.12f, 0.3f), 1e-5f);
        }

        [Test]
        public void Fade_FechaEm03s_EFicaEntreZeroEUm()
        {
            float open = 1f;
            for (int i = 0; i < 15; i++)
                open = SeeThroughMath.StepFade(open, false, 0.02f, 0.12f, 0.3f);
            Assert.AreEqual(0f, open, 1e-4f, "15 passos de 0,02 s = 0,3 s");
            Assert.AreEqual(1f, SeeThroughMath.StepFade(0.95f, true, 1f, 0.12f, 0.3f), "Não passa de 1");
            Assert.AreEqual(0f, SeeThroughMath.StepFade(0.05f, false, 1f, 0.12f, 0.3f), "Não passa de 0");
        }

        [Test]
        public void Fade_TempoZeroAbreEFechaNaHora()
        {
            Assert.AreEqual(1f, SeeThroughMath.StepFade(0f, true, 0.016f, 0f, 0.3f));
            Assert.AreEqual(0f, SeeThroughMath.StepFade(1f, false, 0.016f, 0.12f, 0f));
        }

        [Test]
        public void Smooth_VaiDeZeroAUmSemSair()
        {
            Assert.AreEqual(0f, SeeThroughMath.Smooth(-1f));
            Assert.AreEqual(0f, SeeThroughMath.Smooth(0f));
            Assert.AreEqual(0.5f, SeeThroughMath.Smooth(0.5f), 1e-6f);
            Assert.AreEqual(1f, SeeThroughMath.Smooth(2f));
        }

        [Test]
        public void Tapado_QualquerDasTresAlturas()
        {
            Assert.IsFalse(SeeThroughMath.IsOccluded(false, false, false));
            Assert.IsTrue(SeeThroughMath.IsOccluded(true, false, false), "Só os pés");
            Assert.IsTrue(SeeThroughMath.IsOccluded(false, true, false), "Só o tronco");
            Assert.IsTrue(SeeThroughMath.IsOccluded(false, false, true), "Só a cabeça");
            Assert.IsTrue(SeeThroughMath.IsOccluded(true, true, true));
        }

        [Test]
        public void Selecao_PegaOsMaisRelevantesEmOrdem()
        {
            float[] scores = { 5f, 9f, 1f, 7f, 3f };
            var selected = new int[3];
            int n = SeeThroughMath.SelectTop(scores, scores.Length, 3, selected);
            Assert.AreEqual(3, n);
            CollectionAssert.AreEqual(new[] { 1, 3, 0 }, selected);
        }

        [Test]
        public void Selecao_ComMenosCandidatosQueOLimite()
        {
            float[] scores = { 2f, 8f, 100f };
            var selected = new int[12];
            int n = SeeThroughMath.SelectTop(scores, 2, 12, selected);
            Assert.AreEqual(2, n, "Só os 2 primeiros valem");
            Assert.AreEqual(1, selected[0]);
            Assert.AreEqual(0, selected[1]);
            Assert.AreEqual(0, SeeThroughMath.SelectTop(scores, 0, 12, selected));
        }

        [Test]
        public void Selecao_LimitaEmDoze()
        {
            var scores = new float[20];
            for (int i = 0; i < scores.Length; i++)
                scores[i] = i;
            var selected = new int[SeeThroughMath.MaxShaderTargets];
            int n = SeeThroughMath.SelectTop(scores, scores.Length, SeeThroughMath.MaxShaderTargets, selected);
            Assert.AreEqual(12, n);
            Assert.AreEqual(19, selected[0]);
            Assert.AreEqual(8, selected[11]);
        }

        [Test]
        public void Relevancia_JogadorAntesDeInimigo_DepoisMaisAbertoEMaisPerto()
        {
            float jogadorLonge = SeeThroughMath.Relevance(true, 0.1f, 30f);
            float inimigoPerto = SeeThroughMath.Relevance(false, 1f, 5f);
            Assert.Greater(jogadorLonge, inimigoPerto);
            Assert.Greater(SeeThroughMath.Relevance(false, 1f, 10f), SeeThroughMath.Relevance(false, 0.2f, 10f));
            Assert.Greater(SeeThroughMath.Relevance(false, 1f, 8f), SeeThroughMath.Relevance(false, 1f, 12f));
        }

        [Test]
        public void DentroDoBuraco_UsaOAspectoParaFicarRedondo()
        {
            // Raio 0,2 da altura, tela 16:9: horizontalmente 0,2 / 1,78 = 0,1125 em UV.
            const float aspect = 16f / 9f;
            Assert.IsTrue(SeeThroughMath.IsInsideHole(0.5f + 0.10f, 0.5f, 0.5f, 0.5f, 0.2f, aspect));
            Assert.IsFalse(SeeThroughMath.IsInsideHole(0.5f + 0.12f, 0.5f, 0.5f, 0.5f, 0.2f, aspect));
            Assert.IsTrue(SeeThroughMath.IsInsideHole(0.5f, 0.5f + 0.19f, 0.5f, 0.5f, 0.2f, aspect));
            Assert.IsFalse(SeeThroughMath.IsInsideHole(0.5f, 0.5f + 0.21f, 0.5f, 0.5f, 0.2f, aspect));
        }

        [Test]
        public void RegraDoRecorte_SoCortaPerto_OuDentroDoBuracoAntesDoAlvo()
        {
            // Alvo a 14 m, margem 0,8, recorte perto 3.
            Assert.IsTrue(SeeThroughMath.ShouldCutPixel(10f, true, 14f, 0.8f, 3f), "Prédio na frente do alvo, no buraco");
            Assert.IsFalse(SeeThroughMath.ShouldCutPixel(10f, false, 14f, 0.8f, 3f), "Fora do buraco");
            Assert.IsFalse(SeeThroughMath.ShouldCutPixel(13.5f, true, 14f, 0.8f, 3f), "Parede atrás do alvo menos a margem fica");
            Assert.IsFalse(SeeThroughMath.ShouldCutPixel(15f, true, 14f, 0.8f, 3f), "Atrás do alvo");
            Assert.IsTrue(SeeThroughMath.ShouldCutPixel(2.5f, false, 14f, 0.8f, 3f), "Perto da câmera sempre corta");
        }
    }
}
