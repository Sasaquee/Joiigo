using System;
using Game.Core.Aura;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class AuraCoreTests
    {
        private const float Tol = 1e-4f;

        private static AuraTuning AjustePadrao() => new AuraTuning();

        // Mapeia com tuning padrão e paleta Normal por comodidade.
        private static AuraState Mapear(float health, float energy, AuraSignals signals,
            AuraTuning tuning = null, AuraPalette palette = AuraPalette.Normal)
        {
            return AuraMapper.Map(new AuraInput(health, energy, signals), tuning ?? AjustePadrao(), palette);
        }

        private static void AssertCores(AuraColor atual, float r, float g, float b)
        {
            Assert.AreEqual(r, atual.R, Tol);
            Assert.AreEqual(g, atual.G, Tol);
            Assert.AreEqual(b, atual.B, Tol);
        }

        private static void AssertCores(AuraColor atual, AuraColor esperada)
        {
            Assert.AreEqual(esperada.R, atual.R, Tol);
            Assert.AreEqual(esperada.G, atual.G, Tol);
            Assert.AreEqual(esperada.B, atual.B, Tol);
        }

        private static void AssertCoresDiferentes(AuraColor a, AuraColor b)
        {
            bool difere = Math.Abs(a.R - b.R) > Tol
                || Math.Abs(a.G - b.G) > Tol
                || Math.Abs(a.B - b.B) > Tol;
            Assert.IsTrue(difere, "As duas paletas deviam ter cores diferentes aqui");
        }

        // ---------- HP: força e tamanho (D-061) ----------

        [Test]
        public void Hp_CheioDeixaAuraForteLargaEFirme()
        {
            var state = Mapear(1f, 1f, AuraSignals.None);
            Assert.AreEqual(1f, state.Radius, Tol);
            Assert.AreEqual(1f, state.Intensity, Tol);
            Assert.AreEqual(0f, state.Flicker, Tol);
        }

        [Test]
        public void Hp_MedioDeixaRaioEForcaNoMeioDaFaixa()
        {
            var state = Mapear(0.5f, 1f, AuraSignals.None);
            Assert.AreEqual(0.775f, state.Radius, Tol);   // Lerp(0.55, 1, 0.5)
            Assert.AreEqual(0.6f, state.Intensity, Tol);  // Lerp(0.2, 1, 0.5)
            Assert.AreEqual(0f, state.Flicker, Tol);
        }

        [Test]
        public void Hp_BaixoEncolheAFeiaEComecaAFalhar()
        {
            var state = Mapear(0.1f, 1f, AuraSignals.None);
            Assert.AreEqual(0.595f, state.Radius, Tol);    // Lerp(0.55, 1, 0.1)
            Assert.AreEqual(0.28f, state.Intensity, Tol); // Lerp(0.2, 1, 0.1)
            Assert.Greater(state.Flicker, 0f);
        }

        [Test]
        public void Hp_ZeroDeixaRaioEForcaMinimosEFalhandoNoMaximo()
        {
            var state = Mapear(0f, 1f, AuraSignals.None);
            Assert.AreEqual(0.55f, state.Radius, Tol);   // MinRadius
            Assert.AreEqual(0.2f, state.Intensity, Tol); // MinIntensity
            Assert.AreEqual(1f, state.Flicker, Tol);
        }

        [Test]
        public void Hp_SubirNuncaEncolheRaioNemForca()
        {
            // Varredura de 0 a 1 em passos de 0.05.
            var anterior = Mapear(0f, 0f, AuraSignals.None);
            for (int passo = 1; passo <= 20; passo++)
            {
                var atual = Mapear(passo * 0.05f, 0f, AuraSignals.None);
                Assert.GreaterOrEqual(atual.Radius, anterior.Radius,
                    $"O raio diminuiu no passo {passo}");
                Assert.GreaterOrEqual(atual.Intensity, anterior.Intensity,
                    $"A força diminuiu no passo {passo}");
                anterior = atual;
            }
        }

        // ---------- Falha de lâmpada (D-061) ----------

        [Test]
        public void Falha_NaoFalhaEmCimaDoLimiar()
        {
            var noLimiar = Mapear(0.2f, 1f, AuraSignals.None);   // exatamente em FlickerBelow
            var acima = Mapear(0.21f, 1f, AuraSignals.None);
            Assert.AreEqual(0f, noLimiar.Flicker, Tol);
            Assert.AreEqual(0f, acima.Flicker, Tol);
        }

        [Test]
        public void Falha_LimiarZeradoOuNegativoNaoFalha()
        {
            var tuning = AjustePadrao();
            tuning.FlickerBelow = 0f;
            Assert.AreEqual(0f, Mapear(0f, 1f, AuraSignals.None, tuning).Flicker, Tol);

            tuning.FlickerBelow = -1f;
            Assert.AreEqual(0f, Mapear(0f, 1f, AuraSignals.None, tuning).Flicker, Tol);
        }

        // ---------- Batimento (D-064) ----------

        [Test]
        public void Batimento_CaladoAcimaDoLimiar()
        {
            Assert.AreEqual(0f, Mapear(1f, 1f, AuraSignals.None).HeartbeatBpm, Tol);
            Assert.AreEqual(0f, Mapear(0.5f, 1f, AuraSignals.None).HeartbeatBpm, Tol);
            Assert.AreEqual(0f, Mapear(0.35f, 1f, AuraSignals.None).HeartbeatBpm, Tol); // exatamente em HeartbeatBelow
        }

        [Test]
        public void Batimento_HpZeroTocaNoRitmoMaximo()
        {
            var state = Mapear(0f, 1f, AuraSignals.None);
            Assert.AreEqual(140f, state.HeartbeatBpm, Tol); // HeartbeatFastBpm
        }

        [Test]
        public void Batimento_QuantoMenosVidaMaisRapido()
        {
            float quaseNoLimiar = Mapear(0.34f, 1f, AuraSignals.None).HeartbeatBpm;
            float meioCaminho = Mapear(0.175f, 1f, AuraSignals.None).HeartbeatBpm; // metade de HeartbeatBelow
            float quaseZero = Mapear(0.05f, 1f, AuraSignals.None).HeartbeatBpm;

            Assert.AreEqual(100f, meioCaminho, Tol); // Lerp(140, 60, 0.5)
            Assert.Greater(quaseZero, meioCaminho);
            Assert.Greater(meioCaminho, quaseNoLimiar);
            Assert.Greater(quaseNoLimiar, 0f);
        }

        [Test]
        public void Batimento_LimiarZeradoOuNegativoSilencia()
        {
            var tuning = AjustePadrao();
            tuning.HeartbeatBelow = 0f;
            Assert.AreEqual(0f, Mapear(0f, 1f, AuraSignals.None, tuning).HeartbeatBpm, Tol);

            tuning.HeartbeatBelow = -1f;
            Assert.AreEqual(0f, Mapear(0f, 1f, AuraSignals.None, tuning).HeartbeatBpm, Tol);
        }

        // ---------- Energia: faíscas e runas (D-062) ----------

        [Test]
        public void Energia_FaiscasAcompanhamAFracaoDeEnergia()
        {
            var vazia = Mapear(1f, 0f, AuraSignals.None);
            Assert.AreEqual(0f, vazia.SparkRate, Tol);
            Assert.AreEqual(0.3f, vazia.SparkSpeed, Tol); // SparkMinSpeed

            var meia = Mapear(1f, 0.5f, AuraSignals.None);
            Assert.AreEqual(0.5f, meia.SparkRate, Tol);
            Assert.AreEqual(0.65f, meia.SparkSpeed, Tol); // Lerp(0.3, 1, 0.5)

            var cheia = Mapear(1f, 1f, AuraSignals.None);
            Assert.AreEqual(1f, cheia.SparkRate, Tol);
            Assert.AreEqual(1f, cheia.SparkSpeed, Tol);
        }

        [Test]
        public void Energia_RunasSoAcendemInteirasComEnergiaCheia()
        {
            Assert.IsTrue(Mapear(1f, 1f, AuraSignals.None).RunesFull);
            Assert.IsTrue(Mapear(1f, 0.999f, AuraSignals.None).RunesFull);
            Assert.IsFalse(Mapear(1f, 0.998f, AuraSignals.None).RunesFull);
            Assert.IsFalse(Mapear(1f, 0.5f, AuraSignals.None).RunesFull);
            Assert.IsFalse(Mapear(1f, 0f, AuraSignals.None).RunesFull);
        }

        // ---------- Sinais de estado (D-063) ----------

        [Test]
        public void Sinais_SemSinalPassaNenhum()
        {
            Assert.AreEqual(AuraSignals.None, Mapear(1f, 1f, AuraSignals.None).Signals);
        }

        [Test]
        public void Sinais_CadaUmPassaSozinhoEmPe()
        {
            Assert.AreEqual(AuraSignals.Shield, Mapear(1f, 1f, AuraSignals.Shield).Signals);
            Assert.AreEqual(AuraSignals.Curse, Mapear(1f, 1f, AuraSignals.Curse).Signals);
            Assert.AreEqual(AuraSignals.HurtBonus, Mapear(1f, 1f, AuraSignals.HurtBonus).Signals);
        }

        [Test]
        public void Sinais_VariosJuntosPassamTodos()
        {
            var juntos = AuraSignals.Shield | AuraSignals.Curse | AuraSignals.HurtBonus;
            Assert.AreEqual(juntos, Mapear(1f, 1f, juntos).Signals);
        }

        [Test]
        public void Caido_SuprimeOsOutrosSinais()
        {
            var todos = AuraSignals.Shield | AuraSignals.Curse | AuraSignals.Downed | AuraSignals.HurtBonus;
            Assert.AreEqual(AuraSignals.Downed, Mapear(0f, 1f, todos).Signals);
        }

        [Test]
        public void Caido_DeixaSoBrasaMesmoComPoucaVida()
        {
            // Com a mesma vida e energia, de pé a aura falharia no máximo e o coração bateria no máximo.
            var emPe = Mapear(0f, 1f, AuraSignals.None);
            var caido = Mapear(0f, 1f, AuraSignals.Downed);

            Assert.AreEqual(0.55f, caido.Radius, Tol);      // MinRadius
            Assert.AreEqual(0.08f, caido.Intensity, Tol);  // DownedIntensity
            Assert.Less(caido.Intensity, emPe.Intensity);
            Assert.AreEqual(0f, caido.Flicker, Tol);
            Assert.AreEqual(1f, emPe.Flicker, Tol);
            Assert.AreEqual(0f, caido.SparkRate, Tol);
            Assert.AreEqual(0f, caido.SparkSpeed, Tol);
            Assert.IsFalse(caido.RunesFull);                // energia cheia não acende as runas caído
            Assert.AreEqual(0f, caido.HeartbeatBpm, Tol);
            Assert.AreEqual(140f, emPe.HeartbeatBpm, Tol);
        }

        // ---------- Paletas (D-065) ----------

        [Test]
        public void Paleta_NormalTemAsCoresDeD065()
        {
            AssertCores(AuraPalettes.Base(AuraPalette.Normal), 0.43f, 0.94f, 1.00f);   // ciano
            AssertCores(AuraPalettes.Curse(AuraPalette.Normal), 0.78f, 0.50f, 1.00f);  // violeta
            AssertCores(AuraPalettes.Ember(AuraPalette.Normal), 1.00f, 0.48f, 0.13f);  // laranja
            AssertCores(AuraPalettes.Shell(AuraPalette.Normal), 0.80f, 1.00f, 1.00f);
        }

        [Test]
        public void Paleta_AlternativaTemAsCoresDeD065()
        {
            AssertCores(AuraPalettes.Base(AuraPalette.Alternative), 0.15f, 0.40f, 1.00f);  // azul forte
            AssertCores(AuraPalettes.Curse(AuraPalette.Alternative), 1.00f, 1.00f, 1.00f); // branco
            AssertCores(AuraPalettes.Ember(AuraPalette.Alternative), 1.00f, 0.85f, 0.10f); // amarelo
            AssertCores(AuraPalettes.Shell(AuraPalette.Alternative), 0.75f, 0.85f, 1.00f);
        }

        [Test]
        public void Paleta_AlternativaDiferenteDaNormal()
        {
            AssertCoresDiferentes(AuraPalettes.Base(AuraPalette.Normal), AuraPalettes.Base(AuraPalette.Alternative));
            AssertCoresDiferentes(AuraPalettes.Curse(AuraPalette.Normal), AuraPalettes.Curse(AuraPalette.Alternative));
            AssertCoresDiferentes(AuraPalettes.Ember(AuraPalette.Normal), AuraPalettes.Ember(AuraPalette.Alternative));
            AssertCoresDiferentes(AuraPalettes.Shell(AuraPalette.Normal), AuraPalettes.Shell(AuraPalette.Alternative));
        }

        [Test]
        public void Paleta_MapUsaAPaletaPedida()
        {
            var normal = Mapear(1f, 1f, AuraSignals.None, palette: AuraPalette.Normal);
            AssertCores(normal.Base, 0.43f, 0.94f, 1.00f);
            AssertCores(normal.Curse, 0.78f, 0.50f, 1.00f);
            AssertCores(normal.Ember, 1.00f, 0.48f, 0.13f);
            AssertCores(normal.Shell, 0.80f, 1.00f, 1.00f);

            var alternativa = Mapear(1f, 1f, AuraSignals.None, palette: AuraPalette.Alternative);
            AssertCores(alternativa.Base, 0.15f, 0.40f, 1.00f);
            AssertCores(alternativa.Curse, 1.00f, 1.00f, 1.00f);
            AssertCores(alternativa.Ember, 1.00f, 0.85f, 0.10f);
            AssertCores(alternativa.Shell, 0.75f, 0.85f, 1.00f);
        }

        [Test]
        public void Paleta_CaidoTambemUsaAPaletaPedida()
        {
            var state = Mapear(0f, 0f, AuraSignals.Downed, palette: AuraPalette.Alternative);
            AssertCores(state.Base, AuraPalettes.Base(AuraPalette.Alternative));
            AssertCores(state.Curse, AuraPalettes.Curse(AuraPalette.Alternative));
            AssertCores(state.Ember, AuraPalettes.Ember(AuraPalette.Alternative));
            AssertCores(state.Shell, AuraPalettes.Shell(AuraPalette.Alternative));
        }

        // ---------- Entrada fora de faixa ----------

        [Test]
        public void Entrada_ForadeFaixaELimitada()
        {
            var input = new AuraInput(-1f, 2f, AuraSignals.None);
            Assert.AreEqual(0f, input.Health, Tol);
            Assert.AreEqual(1f, input.Energy, Tol);
        }

        [Test]
        public void Entrada_NanEInfinitoViramZero()
        {
            var input = new AuraInput(float.NaN, float.PositiveInfinity, AuraSignals.None);
            Assert.AreEqual(0f, input.Health, Tol);
            Assert.AreEqual(0f, input.Energy, Tol);

            var inputNegativo = new AuraInput(float.NegativeInfinity, float.NaN, AuraSignals.None);
            Assert.AreEqual(0f, inputNegativo.Health, Tol);
            Assert.AreEqual(0f, inputNegativo.Energy, Tol);
        }

        [Test]
        public void Entrada_LimitadaAntesDeMapear()
        {
            var fora = Mapear(-1f, 2f, AuraSignals.None);
            var dentro = Mapear(0f, 1f, AuraSignals.None);
            Assert.AreEqual(dentro.Radius, fora.Radius, Tol);
            Assert.AreEqual(dentro.Intensity, fora.Intensity, Tol);
            Assert.AreEqual(dentro.Flicker, fora.Flicker, Tol);
            Assert.AreEqual(dentro.SparkRate, fora.SparkRate, Tol);
            Assert.AreEqual(dentro.SparkSpeed, fora.SparkSpeed, Tol);
        }

        // ---------- Ajuste ----------

        [Test]
        public void Ajuste_NuloLancaArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AuraMapper.Map(new AuraInput(1f, 1f, AuraSignals.None), null, AuraPalette.Normal));
        }
    }
}
