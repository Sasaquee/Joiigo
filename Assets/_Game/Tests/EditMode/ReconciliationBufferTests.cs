using Game.Core.Math;
using Game.Core.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ReconciliationBufferTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void SemErro_NaoCorrige()
        {
            var buffer = new ReconciliationBuffer();
            buffer.Record(1, new Float2(1f, 1f));

            Float2 c = buffer.Acknowledge(1, new Float2(1.05f, 1f), 0.15f);
            Assert.AreEqual(0f, c.Length, Tol);
        }

        [Test]
        public void ComErro_DevolveADiferencaEDescartaOsConfirmados()
        {
            var buffer = new ReconciliationBuffer();
            buffer.Record(1, new Float2(0f, 0f));
            buffer.Record(2, new Float2(0f, 1f));
            buffer.Record(3, new Float2(0f, 2f));

            Float2 c = buffer.Acknowledge(2, new Float2(0.5f, 1f), 0.15f);

            Assert.AreEqual(0.5f, c.X, Tol);
            Assert.AreEqual(0f, c.Y, Tol);
            Assert.AreEqual(1, buffer.PendingCount);
        }

        [Test]
        public void NaoCorrigeOMesmoErroDuasVezes()
        {
            var buffer = new ReconciliationBuffer();
            buffer.Record(1, new Float2(0f, 0f));
            buffer.Record(2, new Float2(0f, 1f));

            buffer.Acknowledge(1, new Float2(1f, 0f), 0.15f);
            // O host continua com o mesmo deslocamento de 1 m; o cliente já foi corrigido.
            Float2 c = buffer.Acknowledge(2, new Float2(1f, 1f), 0.15f);

            Assert.AreEqual(0f, c.Length, Tol);
        }

        [Test]
        public void ConfirmacaoDesconhecida_NaoCorrige()
        {
            var buffer = new ReconciliationBuffer();
            buffer.Record(5, new Float2(0f, 0f));

            Assert.AreEqual(0f, buffer.Acknowledge(3, new Float2(9f, 9f), 0.15f).Length, Tol);
            Assert.AreEqual(1, buffer.PendingCount);
        }
    }
}
