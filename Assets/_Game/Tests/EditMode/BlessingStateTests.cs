using Game.Core.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Bênção de dano do 20 do D20 no coop (D-085): regra pura de duração e multiplicador.</summary>
    public class BlessingStateTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void Nova_NasceInativaEComMultiplicadorUm()
        {
            var blessing = new BlessingState();
            Assert.IsFalse(blessing.Active);
            Assert.AreEqual(0f, blessing.Remaining, Tol);
            Assert.AreEqual(1f, blessing.Multiplier, Tol);
        }

        [Test]
        public void Begin_Ativa_ComADuracaoEOMultiplicador()
        {
            var blessing = new BlessingState();
            blessing.Begin(30f, 1.5f);
            Assert.IsTrue(blessing.Active);
            Assert.AreEqual(30f, blessing.Remaining, Tol);
            Assert.AreEqual(1.5f, blessing.Multiplier, Tol);
        }

        [Test]
        public void Tick_DesceORelogioEExpiraNoFim()
        {
            var blessing = new BlessingState();
            blessing.Begin(2f, 1.5f);

            blessing.Tick(0.5f);
            Assert.IsTrue(blessing.Active);
            Assert.AreEqual(1.5f, blessing.Remaining, Tol);

            blessing.Tick(1.49f);
            Assert.IsTrue(blessing.Active, "Falta um pouco");

            blessing.Tick(0.02f);
            Assert.IsFalse(blessing.Active, "Passou da duração: expira");
            Assert.AreEqual(0f, blessing.Remaining, Tol, "O relógio não fica negativo");
        }

        [Test]
        public void Inativa_MultiplicadorVoltaAUm()
        {
            var blessing = new BlessingState();
            blessing.Begin(1f, 2f);
            Assert.AreEqual(2f, blessing.Multiplier, Tol);
            blessing.Tick(1.5f);
            Assert.AreEqual(1f, blessing.Multiplier, Tol, "Expirou: o dano volta ao normal");
        }

        [Test]
        public void Renovar_ReiniciaADuracao()
        {
            var blessing = new BlessingState();
            blessing.Begin(10f, 1.5f);
            blessing.Tick(7f);
            Assert.AreEqual(3f, blessing.Remaining, Tol);

            blessing.Begin(10f, 1.5f);
            Assert.AreEqual(10f, blessing.Remaining, Tol, "Renovou: a duração volta ao cheio (não soma 3 + 10)");
        }

        [Test]
        public void Renovar_NaoEmpilhaOMultiplicador()
        {
            var blessing = new BlessingState();
            blessing.Begin(10f, 1.5f);
            blessing.Begin(10f, 1.5f);
            blessing.Begin(10f, 1.5f);
            Assert.AreEqual(1.5f, blessing.Multiplier, Tol, "Três 20 seguidos continuam em ×1,5 (não 3,375)");
        }

        [Test]
        public void Renovar_ComOutroMultiplicador_ValeONovo()
        {
            var blessing = new BlessingState();
            blessing.Begin(10f, 1.5f);
            blessing.Begin(10f, 1.25f);
            Assert.AreEqual(1.25f, blessing.Multiplier, Tol);
        }

        [Test]
        public void DepoisDeExpirar_PodeComecarDeNovo()
        {
            var blessing = new BlessingState();
            blessing.Begin(1f, 1.5f);
            blessing.Tick(2f);
            Assert.IsFalse(blessing.Active);

            blessing.Begin(5f, 1.5f);
            Assert.IsTrue(blessing.Active);
            Assert.AreEqual(5f, blessing.Remaining, Tol);
        }

        [Test]
        public void Clear_EncerraNaHora()
        {
            var blessing = new BlessingState();
            blessing.Begin(30f, 1.5f);
            blessing.Clear();
            Assert.IsFalse(blessing.Active);
            Assert.AreEqual(1f, blessing.Multiplier, Tol);
        }

        [Test]
        public void Entradas_Invalidas_NaoQuebram()
        {
            var blessing = new BlessingState();

            blessing.Begin(0f, 1.5f);
            Assert.IsFalse(blessing.Active, "Duração zero não ativa");
            blessing.Begin(-3f, 1.5f);
            Assert.IsFalse(blessing.Active, "Duração negativa não ativa");
            blessing.Begin(float.NaN, 1.5f);
            Assert.IsFalse(blessing.Active, "Duração NaN não ativa");

            blessing.Begin(5f, float.NaN);
            Assert.IsTrue(blessing.Active);
            Assert.AreEqual(1f, blessing.Multiplier, Tol, "Multiplicador NaN vale 1");

            blessing.Tick(float.NaN);
            blessing.Tick(-1f);
            Assert.AreEqual(5f, blessing.Remaining, Tol, "Passo negativo ou NaN não anda o relógio");
        }
    }
}
