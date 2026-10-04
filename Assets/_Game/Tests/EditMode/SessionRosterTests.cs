using Game.Core.Session;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class SessionRosterTests
    {
        [Test]
        public void AntesDaLargada_AdmiteAteOLimite()
        {
            var roster = new SessionRoster(4);
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(AdmitResult.Admitted, roster.TryAdmit($"j{i}", out _));

            Assert.AreEqual(AdmitResult.Full, roster.TryAdmit("j4", out _));
        }

        [Test]
        public void Vagas_SaoDistintasEReaproveitadasAntesDaLargada()
        {
            var roster = new SessionRoster(4);
            roster.TryAdmit("a", out int a);
            roster.TryAdmit("b", out int b);
            Assert.AreNotEqual(a, b);

            roster.Disconnect("a");
            roster.TryAdmit("c", out int c);
            Assert.AreEqual(a, c, "Antes da largada, a vaga de quem saiu fica livre.");
        }

        [Test]
        public void DepoisDaLargada_NinguemNovoEntra()
        {
            var roster = new SessionRoster(4);
            roster.TryAdmit("host", out _);
            roster.Start();

            Assert.AreEqual(AdmitResult.AlreadyStarted, roster.TryAdmit("novo", out _));
        }

        [Test]
        public void DepoisDaLargada_QuemCaiuVoltaNaMesmaVaga()
        {
            var roster = new SessionRoster(4);
            roster.TryAdmit("host", out _);
            roster.TryAdmit("amigo", out int slot);
            roster.Start();

            roster.Disconnect("amigo");
            Assert.AreEqual(AdmitResult.Admitted, roster.TryAdmit("amigo", out int again));
            Assert.AreEqual(slot, again);
        }

        [Test]
        public void MesmoTokenConectado_EhRecusado()
        {
            var roster = new SessionRoster(4);
            roster.TryAdmit("a", out _);
            Assert.AreEqual(AdmitResult.AlreadyConnected, roster.TryAdmit("a", out _));
        }

        [Test]
        public void DepoisDaLargada_QuemSaiuNaoLiberaVagaParaNovos()
        {
            var roster = new SessionRoster(2);
            roster.TryAdmit("a", out _);
            roster.TryAdmit("b", out _);
            roster.Start();
            roster.Disconnect("b");

            Assert.AreEqual(1, roster.ConnectedCount);
            Assert.AreEqual(AdmitResult.AlreadyStarted, roster.TryAdmit("c", out _));
            Assert.IsTrue(roster.IsMember("b"));
        }
    }
}
