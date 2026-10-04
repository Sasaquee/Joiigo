using Game.Core.Combat;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CombatCoreTests
    {
        private const float Tol = 1e-3f;

        // ---------- DamageCalculator ----------

        [Test]
        public void Dano_PuroMecanico_SoSofreResistenciaMecanica()
        {
            float d = DamageCalculator.Compute(new DamagePacket(100f, 0f), new Resistances(0.5f, 0.9f));
            Assert.AreEqual(50f, d, Tol);
        }

        [Test]
        public void Dano_PuroArcano_SoSofreResistenciaArcana()
        {
            float d = DamageCalculator.Compute(new DamagePacket(100f, 1f), new Resistances(0.5f, 0.25f));
            Assert.AreEqual(75f, d, Tol);
        }

        [Test]
        public void Dano_Misto_SomaAsDuasPartesReduzidas()
        {
            float d = DamageCalculator.Compute(new DamagePacket(100f, 0.5f), new Resistances(0.5f, 0.5f));
            Assert.AreEqual(50f, d, Tol);
            Assert.AreEqual(100f, DamageCalculator.Compute(new DamagePacket(100f, 0.5f), Resistances.None), Tol);
        }

        [Test]
        public void Dano_ConstructoCortaMuitoDaParteArcana()
        {
            var construct = new Resistances(0f, 0.75f);

            // 80 mecânico + 20 arcano * 0,25 = 85: só a parte arcana encolhe.
            Assert.AreEqual(85f, DamageCalculator.Compute(new DamagePacket(100f, 0.2f), construct), Tol);
            // Puro arcano passa só um quarto.
            Assert.AreEqual(25f, DamageCalculator.Compute(new DamagePacket(100f, 1f), construct), Tol);
        }

        [Test]
        public void Dano_PacoteNormalizaValoresForaDaFaixa()
        {
            var packet = new DamagePacket(-5f, 3f);
            Assert.AreEqual(0f, packet.Amount);
            Assert.AreEqual(1f, packet.ArcaneFraction);
        }

        // ---------- HealthModel ----------

        [Test]
        public void Vida_DanoNaoPassaDeZero()
        {
            var health = new HealthModel(100f);

            Assert.AreEqual(30f, health.ApplyDamage(30f), Tol);
            Assert.AreEqual(70f, health.Current, Tol);

            Assert.AreEqual(70f, health.ApplyDamage(500f), Tol);
            Assert.AreEqual(0f, health.Current, Tol);
            Assert.IsTrue(health.IsDepleted);
        }

        [Test]
        public void Vida_CuraNaoPassaDoMaximo()
        {
            var health = new HealthModel(100f);
            health.ApplyDamage(30f);

            Assert.AreEqual(30f, health.Heal(100f), Tol);
            Assert.AreEqual(100f, health.Current, Tol);
        }

        [Test]
        public void Vida_SemDanoDepoisDeEsgotada()
        {
            var health = new HealthModel(50f);
            health.ApplyDamage(50f);

            Assert.AreEqual(0f, health.ApplyDamage(10f));
            Assert.AreEqual(0f, health.Current, Tol);
        }

        [Test]
        public void Vida_RestauraEncheTudo()
        {
            var health = new HealthModel(50f);
            health.ApplyDamage(50f);
            health.Restore();

            Assert.AreEqual(50f, health.Current, Tol);
            Assert.IsFalse(health.IsDepleted);
        }

        // ---------- ArcHit ----------

        private static readonly Float2 Origin = Float2.Zero;
        private static readonly Float2 Facing = new Float2(0f, 1f);

        [Test]
        public void Arco_AFrenteDentroDoAlcanceAcerta()
        {
            Assert.IsTrue(ArcHit.IsInArc(Origin, Facing, new Float2(0f, 1.5f), 2f, 60f, 0.3f));
        }

        [Test]
        public void Arco_AtrasErra()
        {
            Assert.IsFalse(ArcHit.IsInArc(Origin, Facing, new Float2(0f, -1.5f), 2f, 60f, 0.3f));
        }

        [Test]
        public void Arco_NaBordaAcertaGracasAoRaioDoAlvo()
        {
            // 65 graus, com meia-abertura de 60: só o raio do alvo (folga angular) faz acertar.
            float a = 65f * System.MathF.PI / 180f;
            var target = new Float2(System.MathF.Sin(a) * 1.5f, System.MathF.Cos(a) * 1.5f);

            Assert.IsTrue(ArcHit.IsInArc(Origin, Facing, target, 2f, 60f, 0.3f), "com raio");
            Assert.IsFalse(ArcHit.IsInArc(Origin, Facing, target, 2f, 60f, 0f), "ponto sem raio");
        }

        [Test]
        public void Arco_ForaDoAlcanceErra()
        {
            Assert.IsFalse(ArcHit.IsInArc(Origin, Facing, new Float2(0f, 3f), 2f, 60f, 0.3f));
        }

        [Test]
        public void Arco_AlcanceContaORaioDoAlvo()
        {
            // Centro a 2,2 m, borda a 1,9 m: está ao alcance de 2 m.
            Assert.IsTrue(ArcHit.IsInArc(Origin, Facing, new Float2(0f, 2.2f), 2f, 60f, 0.3f));
        }

        [Test]
        public void Arco_EncostadoSempreAcerta_MesmoAtras()
        {
            Assert.IsTrue(ArcHit.IsInArc(Origin, Facing, new Float2(0f, -0.2f), 2f, 60f, 0.5f));
        }

        [Test]
        public void Arco_SemDirecaoNaoAcertaQuemNaoEstaEncostado()
        {
            Assert.IsFalse(ArcHit.IsInArc(Origin, Float2.Zero, new Float2(0f, 1f), 2f, 60f, 0.3f));
        }

        // ---------- Cooldown ----------

        [Test]
        public void Recarga_UsaEEsperaOTempo()
        {
            var cooldown = new Cooldown();
            Assert.IsTrue(cooldown.Ready);

            Assert.IsTrue(cooldown.TryUse(1f));
            Assert.IsFalse(cooldown.Ready);
            Assert.IsFalse(cooldown.TryUse(1f), "ainda recarregando");

            cooldown.Tick(0.5f);
            Assert.IsFalse(cooldown.Ready);
            Assert.AreEqual(0.5f, cooldown.Remaining, Tol);

            cooldown.Tick(0.5f);
            Assert.IsTrue(cooldown.Ready);
            Assert.IsTrue(cooldown.TryUse(1f));
        }

        [Test]
        public void Recarga_ResetLibera()
        {
            var cooldown = new Cooldown();
            cooldown.TryUse(5f);
            cooldown.Reset();
            Assert.IsTrue(cooldown.Ready);
        }

        // ---------- DownedState ----------

        [Test]
        public void Caido_CaiEVoltaUmaSoVez()
        {
            var state = new DownedState(2f);
            Assert.AreEqual(LifeState.Alive, state.State);
            Assert.IsFalse(state.Tick(5f), "vivo não tem o que voltar");

            state.Fall();
            Assert.AreEqual(LifeState.Downed, state.State);
            Assert.AreEqual(2f, state.TimeLeft, Tol);

            Assert.IsFalse(state.Tick(1f));
            Assert.IsTrue(state.Tick(1.5f), "volta no spawn");
            Assert.AreEqual(LifeState.Alive, state.State);
            Assert.IsFalse(state.Tick(1f), "só avisa uma vez");
        }

        [Test]
        public void Caido_CairDeNovoNaoReiniciaOTempo()
        {
            var state = new DownedState(2f);
            state.Fall();
            state.Tick(1f);
            state.Fall();

            Assert.AreEqual(1f, state.TimeLeft, Tol);
        }

        [Test]
        public void Caido_LevantarAntesDoTempoVolta()
        {
            var state = new DownedState(2f);
            state.Fall();
            state.Revive();

            Assert.AreEqual(LifeState.Alive, state.State);
            Assert.AreEqual(0f, state.TimeLeft);
            Assert.IsFalse(state.Tick(5f));
        }
    }
}
