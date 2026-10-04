using Game.Net;
using NUnit.Framework;

namespace Game.Tests.PlayMode
{
    /// <summary>Vale para todos os testes deste namespace: o início solo (D-018) fica desligado, inclusive nos que carregam a cena direto.</summary>
    [SetUpFixture]
    public class PlayModeSetUp
    {
        [OneTimeSetUp]
        public void DesligaInicioSolo() => SoloBootstrap.SuppressAutoStart = true;
    }
}
