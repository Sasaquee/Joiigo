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

        // ---------- Bênção de dano do 20 no coop (D-085) ----------

        [Test]
        public void Bencao_PassaComoSinalEmPeESozinha()
        {
            Assert.AreEqual(AuraSignals.Blessing, Mapear(1f, 1f, AuraSignals.Blessing).Signals);
            Assert.AreNotEqual(AuraSignals.Blessing, AuraSignals.HurtBonus, "Sinal próprio, não confunde com o reforço da Mola");
        }

        [Test]
        public void Bencao_JuntaComOsOutrosSinais()
        {
            var juntos = AuraSignals.Shield | AuraSignals.Curse | AuraSignals.HurtBonus | AuraSignals.Blessing;
            var signals = Mapear(1f, 1f, juntos).Signals;
            Assert.AreEqual(juntos, signals);
            Assert.AreNotEqual(0, (int)(signals & AuraSignals.Blessing));
        }

        [Test]
        public void Bencao_CaidoApagaABencaoNaAura()
        {
            var state = Mapear(0f, 1f, AuraSignals.Blessing | AuraSignals.Downed);
            Assert.AreEqual(AuraSignals.Downed, state.Signals, "Caído: aura só brasa, sem dourado (D-063)");
        }

        [Test]
        public void Bencao_PaletaNormalEDouradaAlternativaECremePalido()
        {
            AssertCores(AuraPalettes.Gold(AuraPalette.Normal), 1.00f, 0.84f, 0.30f);
            AssertCores(AuraPalettes.Gold(AuraPalette.Alternative), 1.00f, 0.95f, 0.62f);
            AssertCoresDiferentes(AuraPalettes.Gold(AuraPalette.Normal), AuraPalettes.Gold(AuraPalette.Alternative));
        }

        [Test]
        public void Bencao_DouradoNaoSeConfundeComBrasaNemMaldicao()
        {
            foreach (var palette in new[] { AuraPalette.Normal, AuraPalette.Alternative })
            {
                AuraColor gold = AuraPalettes.Gold(palette);
                Assert.Greater(Distancia(gold, AuraPalettes.Ember(palette)), 0.3f, $"Dourado longe da brasa ({palette})");
                Assert.Greater(Distancia(gold, AuraPalettes.Curse(palette)), 0.3f, $"Dourado longe da maldição ({palette})");
                Assert.Greater(Distancia(gold, AuraPalettes.Base(palette)), 0.3f, $"Dourado longe do cristal ({palette})");
                Assert.Greater(Distancia(gold, AuraPalettes.Shell(palette)), 0.3f, $"Dourado longe da casca do escudo ({palette})");
            }
        }

        [Test]
        public void Bencao_MapUsaAPaletaPedida()
        {
            AssertCores(Mapear(1f, 1f, AuraSignals.Blessing, palette: AuraPalette.Normal).Gold, 1.00f, 0.84f, 0.30f);
            AssertCores(Mapear(1f, 1f, AuraSignals.Blessing, palette: AuraPalette.Alternative).Gold, 1.00f, 0.95f, 0.62f);
            AssertCores(Mapear(0f, 0f, AuraSignals.Downed, palette: AuraPalette.Alternative).Gold,
                AuraPalettes.Gold(AuraPalette.Alternative));
        }

        [Test]
        public void Bencao_NaoMudaOsParametrosDeHpEEnergia()
        {
            var sem = Mapear(0.5f, 0.5f, AuraSignals.None);
            var com = Mapear(0.5f, 0.5f, AuraSignals.Blessing);
            Assert.AreEqual(sem.Radius, com.Radius, Tol);
            Assert.AreEqual(sem.Intensity, com.Intensity, Tol);
            Assert.AreEqual(sem.SparkRate, com.SparkRate, Tol);
            Assert.AreEqual(sem.HeartbeatBpm, com.HeartbeatBpm, Tol);
        }

        private static float Distancia(AuraColor a, AuraColor b)
        {
            float dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
            return (float)Math.Sqrt(dr * dr + dg * dg + db * db);
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

        // ---------- Pulso de energia cheia (D-067) ----------

        // Primeiro quadro (energia pela metade) já visto, para os testes partirem de um gatilho armado.
        private static AuraPulseTrigger GatilhoArmado()
        {
            var trigger = new AuraPulseTrigger(0.95f);
            Assert.IsFalse(trigger.Update(0.5f, false));
            return trigger;
        }

        [Test]
        public void Pulso_DisparaQuandoAEnergiaEnche()
        {
            var trigger = GatilhoArmado();
            Assert.IsFalse(trigger.Update(0.99f, false), "Quase cheia não pulsa");
            Assert.IsTrue(trigger.Update(1f, false), "Encheu: pulsa");
        }

        [Test]
        public void Pulso_CheiaEhAMesmaDasRunas()
        {
            // O pulso usa o mesmo limiar que acende as runas inteiras (AuraMapper.FullEnergy).
            var trigger = GatilhoArmado();
            Assert.IsFalse(Mapear(1f, 0.998f, AuraSignals.None).RunesFull);
            Assert.IsFalse(trigger.Update(0.998f, false));
            Assert.IsTrue(Mapear(1f, AuraMapper.FullEnergy, AuraSignals.None).RunesFull);
            Assert.IsTrue(trigger.Update(AuraMapper.FullEnergy, false));
        }

        [Test]
        public void Pulso_NaoRepeteEnquantoContinuaCheia()
        {
            var trigger = GatilhoArmado();
            Assert.IsTrue(trigger.Update(1f, false));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(trigger.Update(1f, false), "Cheia de novo no quadro seguinte não pulsa");
        }

        [Test]
        public void Pulso_GastaEEncheDeNovo_PulsaDeNovo()
        {
            var trigger = GatilhoArmado();
            Assert.IsTrue(trigger.Update(1f, false));
            Assert.IsFalse(trigger.Update(0.7f, false), "Gastar não pulsa");
            Assert.IsFalse(trigger.Update(0.9f, false));
            Assert.IsTrue(trigger.Update(1f, false), "Encheu outra vez: pulsa outra vez");
        }

        [Test]
        public void Pulso_OscilandoPertoDeCheiaNaoRepete()
        {
            // Bug: a energia suavizada oscilando em volta de 0,999 repetia o pulso; agora só rearma abaixo de RearmBelow.
            var trigger = GatilhoArmado();
            Assert.IsTrue(trigger.Update(1f, false));
            for (int i = 0; i < 20; i++)
            {
                Assert.IsFalse(trigger.Update(0.997f, false), "Caiu um pouco: não rearma");
                Assert.IsFalse(trigger.Update(0.9995f, false), "Voltou a cheia sem ter caído de verdade: não pulsa");
            }
            Assert.IsFalse(trigger.Update(0.94f, false), "Caiu abaixo de RearmBelow: rearma, sem pulsar");
            Assert.IsTrue(trigger.Update(1f, false), "Encheu depois de rearmar: pulsa");
        }

        [Test]
        public void Pulso_RearmeLimitadoAteCheia()
        {
            Assert.AreEqual(AuraMapper.FullEnergy, new AuraPulseTrigger(2f).RearmBelow, Tol);
            Assert.AreEqual(0f, new AuraPulseTrigger(-1f).RearmBelow, Tol);
            Assert.AreEqual(0.95f, new AuraPulseTrigger(float.NaN).RearmBelow, Tol);
        }

        [Test]
        public void Pulso_PrimeiroQuadroCheioNaoDispara()
        {
            var trigger = new AuraPulseTrigger();
            Assert.IsFalse(trigger.Update(1f, false), "Nasceu cheio: não pulsa");
            Assert.IsFalse(trigger.Update(1f, false), "Continua cheio: não pulsa");
        }

        [Test]
        public void Pulso_ResetVoltaAoPrimeiroQuadro()
        {
            var trigger = GatilhoArmado();
            trigger.Reset();
            Assert.IsFalse(trigger.Update(1f, false), "Depois do Reset, o primeiro quadro só registra");
        }

        [Test]
        public void Pulso_CaidoNaoDispara()
        {
            var trigger = GatilhoArmado();
            Assert.IsFalse(Mapear(0f, 1f, AuraSignals.Downed).RunesFull, "Caído: runas não ficam cheias (AuraMapper)");
            Assert.IsFalse(trigger.Update(1f, true), "Caído com energia cheia não pulsa");
            Assert.IsFalse(trigger.Update(1f, true));
        }

        [Test]
        public void Pulso_ReviverCheioNaoDispara()
        {
            // Bug: caído tem RunesFull falso; ao levantar com a energia cheia, a borda falso -> verdadeiro pulsava.
            var trigger = GatilhoArmado();
            Assert.IsFalse(trigger.Update(1f, true), "Caiu com a energia cheia");
            Assert.IsFalse(trigger.Update(1f, false), "Levantou cheio: primeiro quadro de pé, não pulsa");
            Assert.IsFalse(trigger.Update(1f, false), "Continua cheio: não pulsa");
            Assert.IsFalse(trigger.Update(0.5f, false), "Gastou");
            Assert.IsTrue(trigger.Update(1f, false), "Encheu de verdade depois de levantar: pulsa");
        }

        [Test]
        public void Pulso_LevantaComPoucaEnergia_EncherPulsa()
        {
            var trigger = GatilhoArmado();
            Assert.IsFalse(trigger.Update(0.3f, true));
            Assert.IsFalse(trigger.Update(0.4f, false), "Levantou com pouca energia");
            Assert.IsTrue(trigger.Update(1f, false), "Encheu: pulsa");
        }

        // ---------- Suavização (D-067: nada de energia "subindo" ao nascer) ----------

        [Test]
        public void Suavizacao_AntesDePronto_AcompanhaSemSuavizar()
        {
            var smoother = new AuraSmoother();
            smoother.Step(1f, 0f, false, 6f, 0.016f);
            smoother.Step(0.5f, 0.3f, false, 6f, 0.016f);
            Assert.AreEqual(0.5f, smoother.Health, Tol);
            Assert.AreEqual(0.3f, smoother.Energy, Tol);
            Assert.IsFalse(smoother.Initialized);
        }

        [Test]
        public void Suavizacao_PrimeiroQuadroProntoCopiaOAlvo_DepoisSuaviza()
        {
            var smoother = new AuraSmoother();
            smoother.Step(1f, 0f, false, 6f, 0.016f);   // fora da rede: energia ainda 0
            smoother.Step(1f, 1f, true, 6f, 0.016f);    // em rede: a energia de verdade chegou
            Assert.AreEqual(1f, smoother.Energy, Tol, "Primeiro quadro em rede copia o alvo");
            Assert.IsTrue(smoother.Initialized);

            smoother.Step(1f, 0f, true, 6f, 0.1f);
            Assert.AreEqual(MathF.Exp(-0.6f), smoother.Energy, Tol, "Depois suaviza");
        }

        [Test]
        public void Suavizacao_NascerAntesDaRede_NaoPulsa()
        {
            // Bug: a aura copiava o alvo só no primeiro quadro, mesmo fora da rede (energia 0); em rede, a energia
            // "subia" do zero até cheia e o pulso tocava logo ao nascer.
            var smoother = new AuraSmoother();
            var trigger = new AuraPulseTrigger();
            int pulses = 0;
            for (int i = 0; i < 3; i++)
            {
                smoother.Step(1f, 0f, false, 6f, 0.016f);
                trigger.Reset(); // como o PlayerAura faz fora da rede
            }
            for (int i = 0; i < 300; i++)
            {
                smoother.Step(1f, 1f, true, 6f, 0.016f);
                if (trigger.Update(smoother.Energy, false))
                    pulses++;
            }
            Assert.AreEqual(1f, smoother.Energy, Tol);
            Assert.AreEqual(0, pulses, "Nascer com a energia cheia não pulsa");
        }

        [Test]
        public void Suavizacao_SemTempoNaoMexe()
        {
            var smoother = new AuraSmoother();
            smoother.Step(1f, 1f, true, 6f, 0.016f);
            smoother.Step(0f, 0f, true, 6f, 0f);
            Assert.AreEqual(1f, smoother.Health, Tol);
            Assert.AreEqual(1f, smoother.Energy, Tol);
        }

        // ---------- Levantar um aliado (D-083) ----------

        private static AuraState MapearLevantando(float reviveProgress, AuraPalette palette = AuraPalette.Normal,
            AuraSignals signals = AuraSignals.Downed) =>
            AuraMapper.Map(new AuraInput(0f, 0f, signals, reviveProgress), AjustePadrao(), palette);

        [Test]
        public void Levantar_SemProgressoFicaIgualAoCaidoDeSempre()
        {
            var estado = MapearLevantando(0f);
            Assert.AreEqual(0.55f, estado.Radius, Tol);     // MinRadius
            Assert.AreEqual(0.08f, estado.Intensity, Tol);  // DownedIntensity
            Assert.AreEqual(0f, estado.ReviveProgress, Tol);
            Assert.AreEqual(AuraSignals.Downed, estado.Signals);
        }

        [Test]
        public void Levantar_ProgressoDaLuzERaioAoCaido()
        {
            var meio = MapearLevantando(0.5f);
            var cheio = MapearLevantando(1f);

            Assert.AreEqual(0.5f, meio.ReviveProgress, Tol);
            Assert.Greater(meio.Radius, 0.55f, "Ganha raio");
            Assert.Greater(meio.Intensity, 0.08f, "Ganha luz");
            Assert.Greater(cheio.Radius, meio.Radius);
            Assert.Greater(cheio.Intensity, meio.Intensity);
            Assert.AreEqual(0.9f, cheio.Radius, Tol);     // ReviveRadius
            Assert.AreEqual(0.8f, cheio.Intensity, Tol);  // ReviveIntensity
        }

        [Test]
        public void Levantar_ContinuaCaidoSemBatimentoNemFaiscas()
        {
            var estado = MapearLevantando(0.7f);
            Assert.AreEqual(AuraSignals.Downed, estado.Signals, "Segue caído até completar");
            Assert.AreEqual(0f, estado.HeartbeatBpm, Tol);
            Assert.AreEqual(0f, estado.SparkRate, Tol);
            Assert.IsFalse(estado.RunesFull);
        }

        [Test]
        public void Levantar_ProgressoEmQuemNaoCaiuNaoMudaNada()
        {
            var normal = Mapear(0.6f, 0.4f, AuraSignals.None);
            var comProgresso = AuraMapper.Map(new AuraInput(0.6f, 0.4f, AuraSignals.None, 0.9f), AjustePadrao(),
                AuraPalette.Normal);
            Assert.AreEqual(normal.Radius, comProgresso.Radius, Tol);
            Assert.AreEqual(normal.Intensity, comProgresso.Intensity, Tol);
            Assert.AreEqual(0f, comProgresso.ReviveProgress, Tol, "De pé: não há progresso de levantar");
        }

        [Test]
        public void Levantar_ProgressoInvalidoELimitado()
        {
            Assert.AreEqual(0f, new AuraInput(0f, 0f, AuraSignals.Downed, float.NaN).ReviveProgress, Tol);
            Assert.AreEqual(1f, new AuraInput(0f, 0f, AuraSignals.Downed, 7f).ReviveProgress, Tol);
            Assert.AreEqual(0f, new AuraInput(0f, 0f, AuraSignals.Downed, -3f).ReviveProgress, Tol);
        }

        [Test]
        public void Levantar_ProgressoSobeMonotonicamente()
        {
            float raio = -1f;
            float forca = -1f;
            for (int i = 0; i <= 20; i++)
            {
                var estado = MapearLevantando(i / 20f);
                Assert.GreaterOrEqual(estado.Radius, raio - Tol);
                Assert.GreaterOrEqual(estado.Intensity, forca - Tol);
                raio = estado.Radius;
                forca = estado.Intensity;
            }
        }

        [Test]
        public void Levantar_RespeitaAPaletaAlternativaNaCorBase()
        {
            var normal = MapearLevantando(0.5f);
            var alternativa = MapearLevantando(0.5f, AuraPalette.Alternative);
            AssertCores(normal.Base, AuraPalettes.Base(AuraPalette.Normal));
            AssertCores(alternativa.Base, AuraPalettes.Base(AuraPalette.Alternative));
            AssertCoresDiferentes(normal.Base, alternativa.Base);
            Assert.AreEqual(normal.ReviveProgress, alternativa.ReviveProgress, Tol, "A forma é a mesma nas duas paletas");
        }

        [Test]
        public void Levantar_RaioNuncaFicaAbaixoDoMinimoMesmoComAjusteTorto()
        {
            var torto = new AuraTuning { ReviveRadius = 0.1f, ReviveIntensity = 0f };
            var estado = AuraMapper.Map(new AuraInput(0f, 0f, AuraSignals.Downed, 1f), torto, AuraPalette.Normal);
            Assert.GreaterOrEqual(estado.Radius, torto.MinRadius - Tol, "Levantar nunca encolhe a aura");
            Assert.GreaterOrEqual(estado.Intensity, torto.DownedIntensity - Tol, "Levantar nunca apaga a aura");
        }
    }
}
