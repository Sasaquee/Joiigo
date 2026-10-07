using Game.Core.Cards;
using Game.Core.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Fase 10 (D-088): os 4 modificadores novos, o dano do golpe básico com eles e a vida máxima por fração.</summary>
    public class NovasCartasCoreTests
    {
        private const float Tol = 1e-3f;

        private static ModifierSet Set(params Modifier[] modifiers)
        {
            var set = new ModifierSet();
            foreach (Modifier m in modifiers)
                set.Add(m);
            return set;
        }

        // ---------- ModifierSet ----------

        [Test]
        public void Modificadores_NovosTiposSomamEComecamEmZero()
        {
            var set = new ModifierSet();
            Assert.AreEqual(0f, set.Get(ModifierKind.MoveSpeed), Tol);
            Assert.AreEqual(0f, set.Get(ModifierKind.MaxHealth), Tol);
            Assert.AreEqual(0f, set.Get(ModifierKind.BasicDamageMultiplier), Tol);
            Assert.AreEqual(0f, set.Get(ModifierKind.LifeCostPerHit), Tol);

            set.Add(new Modifier(ModifierKind.MoveSpeed, 0.10f));
            set.Add(new Modifier(ModifierKind.MoveSpeed, 0.10f));
            set.Add(new Modifier(ModifierKind.MaxHealth, 30f));
            set.Add(new Modifier(ModifierKind.MaxHealth, -30f));
            set.Add(new Modifier(ModifierKind.BasicDamageMultiplier, 0.6f));
            set.Add(new Modifier(ModifierKind.LifeCostPerHit, 3f));

            Assert.AreEqual(0.2f, set.Get(ModifierKind.MoveSpeed), Tol);
            Assert.AreEqual(0f, set.Get(ModifierKind.MaxHealth), Tol, "Coração de Caldeira e Coroa de Rebites se anulam");
            Assert.AreEqual(0.6f, set.Get(ModifierKind.BasicDamageMultiplier), Tol);
            Assert.AreEqual(3f, set.Get(ModifierKind.LifeCostPerHit), Tol);
        }

        [Test]
        public void Modificadores_ValoresNovosNaoMudamOsAntigosSerializados()
        {
            // Os assets guardam o número do enum: os antigos não podem mudar de posição.
            Assert.AreEqual(0, (int)ModifierKind.BasicDamage);
            Assert.AreEqual(1, (int)ModifierKind.BasicRange);
            Assert.AreEqual(2, (int)ModifierKind.BasicArcaneShift);
            Assert.AreEqual(3, (int)ModifierKind.EnergyOnHitBonus);
            Assert.AreEqual(4, (int)ModifierKind.CooldownChange);
            Assert.AreEqual(5, (int)ModifierKind.HurtNextHitBonus);
            Assert.AreEqual(6, (int)ModifierKind.MoveSpeed);
            Assert.AreEqual(7, (int)ModifierKind.MaxHealth);
            Assert.AreEqual(8, (int)ModifierKind.BasicDamageMultiplier);
            Assert.AreEqual(9, (int)ModifierKind.LifeCostPerHit);
        }

        [Test]
        public void Modificadores_VelocidadeSemModificadorEUm()
        {
            Assert.AreEqual(1f, new ModifierSet().MoveSpeedMultiplier, Tol);
        }

        [Test]
        public void Modificadores_VelocidadeMaisDezPorCentoVira110()
        {
            Assert.AreEqual(1.1f, Set(new Modifier(ModifierKind.MoveSpeed, 0.10f)).MoveSpeedMultiplier, Tol);
        }

        [Test]
        public void Modificadores_VelocidadeNegativaNuncaFicaAbaixoDeZero()
        {
            Assert.AreEqual(0.5f, Set(new Modifier(ModifierKind.MoveSpeed, -0.5f)).MoveSpeedMultiplier, Tol);
            Assert.AreEqual(0f, Set(new Modifier(ModifierKind.MoveSpeed, -3f)).MoveSpeedMultiplier, Tol);
        }

        [Test]
        public void Modificadores_DanoPercentualVira1MaisASoma()
        {
            Assert.AreEqual(1f, new ModifierSet().BasicDamageScale, Tol);
            Assert.AreEqual(1.6f, Set(new Modifier(ModifierKind.BasicDamageMultiplier, 0.6f)).BasicDamageScale, Tol);
            Assert.AreEqual(0f, Set(new Modifier(ModifierKind.BasicDamageMultiplier, -2f)).BasicDamageScale, Tol, "nunca abaixo de zero");
        }

        // ---------- BasicHitMath ----------

        [Test]
        public void Golpe_SemCartasEODanoBaseComAMisturaBase()
        {
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, new ModifierSet(), 0f, 1f);

            Assert.AreEqual(20f, p.Amount, Tol);
            Assert.AreEqual(0.2f, p.ArcaneFraction, Tol);
        }

        [Test]
        public void Golpe_ModificadoresNulosValemComoSemCartas()
        {
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, null, 0f, 1f);

            Assert.AreEqual(20f, p.Amount, Tol);
            Assert.AreEqual(0.2f, p.ArcaneFraction, Tol);
        }

        [Test]
        public void Golpe_DanoExtraPositivoSomaEFicaMecanico()
        {
            // Luva de Cobre: +10. A parte arcana (20% de 20 = 4) não cresce.
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicDamage, 10f)), 0f, 1f);

            Assert.AreEqual(30f, p.Amount, Tol);
            Assert.AreEqual(4f / 30f, p.ArcaneFraction, Tol);
        }

        [Test]
        public void Golpe_DanoExtraNegativoDiminui()
        {
            // Fornalha Faminta: -6.
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicDamage, -6f)), 0f, 1f);

            Assert.AreEqual(14f, p.Amount, Tol);
            Assert.AreEqual(4f / 14f, p.ArcaneFraction, Tol);
        }

        [Test]
        public void Golpe_DanoNuncaFicaAbaixoDeUm()
        {
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicDamage, -500f)), 0f, 1f);

            Assert.AreEqual(1f, p.Amount, Tol);
            Assert.GreaterOrEqual(p.ArcaneFraction, 0f);
            Assert.LessOrEqual(p.ArcaneFraction, 1f);
        }

        [Test]
        public void Golpe_DanoNuncaFicaAbaixoDeUmNemComMultiplicadorZerado()
        {
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicDamageMultiplier, -5f)), 0f, 1f);

            Assert.AreEqual(1f, p.Amount, Tol);
        }

        [Test]
        public void Golpe_MultiplicadorPercentualSomaAUm()
        {
            // Pacto de Cristal: +60%.
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicDamageMultiplier, 0.6f)), 0f, 1f);

            Assert.AreEqual(32f, p.Amount, Tol);
            Assert.AreEqual(0.2f, p.ArcaneFraction, Tol, "a mistura não muda");
        }

        [Test]
        public void Golpe_MolaPercentualEBencaoMultiplicamJuntosNaOrdemDasCartas()
        {
            // (base + extra) * (1 + Mola) * (1 + soma do percentual) * bênção.
            var mods = Set(new Modifier(ModifierKind.BasicDamage, 4f), new Modifier(ModifierKind.BasicDamageMultiplier, 0.6f));

            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, mods, 0.6f, 1.5f);

            Assert.AreEqual(24f * 1.6f * 1.6f * 1.5f, p.Amount, Tol);
        }

        [Test]
        public void Golpe_ReforcoDaMolaMultiplicaOTotalJaReduzido()
        {
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicDamage, -4f)), 0.6f, 1f);

            Assert.AreEqual(16f * 1.6f, p.Amount, Tol);
        }

        [Test]
        public void Golpe_DeslocamentoArcanoValeParaODanoBase()
        {
            // Cristal de Fenda: +0,6 de mistura e +4 de dano mecânico.
            var mods = Set(new Modifier(ModifierKind.BasicArcaneShift, 0.6f), new Modifier(ModifierKind.BasicDamage, 4f));

            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, mods, 0f, 1f);

            // arcano = 0,8 * 20 = 16; mecânico = 0,2 * 20 + 4 = 8.
            Assert.AreEqual(24f, p.Amount, Tol);
            Assert.AreEqual(16f / 24f, p.ArcaneFraction, Tol);
        }

        [Test]
        public void Golpe_DeslocamentoArcanoNuncaPassaDeUm()
        {
            DamagePacket p = BasicHitMath.Compute(20f, 0.2f, Set(new Modifier(ModifierKind.BasicArcaneShift, 5f)), 0f, 1f);

            Assert.AreEqual(1f, p.ArcaneFraction, Tol);
            Assert.AreEqual(20f, p.Amount, Tol);
        }

        // ---------- HealthModel.SetMax ----------

        [Test]
        public void VidaMaxima_EquiparComVidaCheiaDeixaCheia()
        {
            var health = new HealthModel(100f);

            health.SetMax(130f);

            Assert.AreEqual(130f, health.Max, Tol);
            Assert.AreEqual(130f, health.Current, Tol);
        }

        [Test]
        public void VidaMaxima_MantemAFracaoDaVidaAtual()
        {
            var health = new HealthModel(100f);
            health.ApplyDamage(50f); // metade

            health.SetMax(130f);
            Assert.AreEqual(65f, health.Current, Tol);

            health.SetMax(100f);
            Assert.AreEqual(50f, health.Current, Tol, "voltar ao valor antigo devolve a mesma vida");
        }

        [Test]
        public void VidaMaxima_DesequiparNuncaMata()
        {
            var health = new HealthModel(130f);
            health.ApplyDamage(129f); // 1 de vida

            health.SetMax(100f);

            Assert.Greater(health.Current, 0f);
            Assert.IsFalse(health.IsDepleted);
        }

        [Test]
        public void VidaMaxima_NuncaFicaAbaixoDeUm()
        {
            var health = new HealthModel(100f);

            health.SetMax(-50f);

            Assert.AreEqual(1f, health.Max, Tol);
            Assert.AreEqual(1f, health.Current, Tol);
        }

        [Test]
        public void VidaMaxima_CaidoContinuaCaido()
        {
            var health = new HealthModel(100f);
            health.ApplyDamage(500f);

            health.SetMax(130f);

            Assert.IsTrue(health.IsDepleted);
            Assert.AreEqual(130f, health.Max, Tol);

            health.Restore();
            Assert.AreEqual(130f, health.Current, Tol, "ao voltar, a vida cheia é a nova máxima");
        }

        [Test]
        public void VidaMaxima_ValorNaoNumericoNaoMudaNada()
        {
            var health = new HealthModel(100f);
            health.ApplyDamage(20f);

            health.SetMax(float.NaN);

            Assert.AreEqual(100f, health.Max, Tol);
            Assert.AreEqual(80f, health.Current, Tol);
        }

        [Test]
        public void VidaMaxima_CoroaDeRebitesComVidaCheiaCortaOMaximoSemMatar()
        {
            var health = new HealthModel(100f);

            health.SetMax(70f); // MaxHealth -30

            Assert.AreEqual(70f, health.Max, Tol);
            Assert.AreEqual(70f, health.Current, Tol);
        }
    }
}
