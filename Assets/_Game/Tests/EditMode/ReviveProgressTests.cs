using Game.Core.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Levantar um aliado segurando E por perto (D-083): progresso, escolha do aliado e vida de volta.</summary>
    public class ReviveProgressTests
    {
        private const float Hold = 2f;
        private const float Decay = 0.3f;
        private const float Tol = 1e-4f;

        private static ReviveProgress Novo() => new ReviveProgress(Hold, Decay);

        // Roda o tempo em passos de 1/60 s e devolve quanto passou de fato.
        private static void Passa(ReviveProgress p, float seconds, bool downed, bool holding)
        {
            const float step = 1f / 60f;
            float left = seconds;
            while (left > 1e-6f)
            {
                float dt = System.Math.Min(step, left);
                p.Update(dt, downed, holding);
                left -= dt;
            }
        }

        [Test]
        public void ComecaZerado()
        {
            var p = Novo();
            Assert.AreEqual(0f, p.Progress, Tol);
            Assert.IsFalse(p.Completed);
            Assert.IsFalse(p.InProgress);
        }

        [Test]
        public void SegurandoPorHoldSeconds_Completa()
        {
            var p = Novo();
            Passa(p, Hold - 0.05f, downed: true, holding: true);
            Assert.IsFalse(p.Completed, "Ainda falta um pouco");
            Assert.AreEqual(1f, p.Progress + 0.05f / Hold, 0.02f);

            Passa(p, 0.1f, downed: true, holding: true);
            Assert.IsTrue(p.Completed);
            Assert.AreEqual(1f, p.Progress, Tol);
        }

        [Test]
        public void ProgressoSobeLinearmenteEFicaEntreZeroEUm()
        {
            var p = Novo();
            Passa(p, Hold * 0.5f, downed: true, holding: true);
            Assert.AreEqual(0.5f, p.Progress, 0.02f);
            Assert.IsTrue(p.InProgress);

            Passa(p, Hold * 3f, downed: true, holding: true);
            Assert.AreEqual(1f, p.Progress, Tol, "Não passa de 1");
        }

        [Test]
        public void Soltar_ZeraDepoisDoDecaimento()
        {
            var p = Novo();
            Passa(p, Hold * 0.5f, downed: true, holding: true);
            float segurado = p.Progress;
            Assert.Greater(segurado, 0.4f);

            Passa(p, 0.05f, downed: true, holding: false);
            Assert.Less(p.Progress, segurado, "Começou a decair");
            Assert.Greater(p.Progress, 0f, "Ainda não zerou: o decaimento leva um instante");

            Passa(p, Decay, downed: true, holding: false);
            Assert.AreEqual(0f, p.Progress, Tol, "Soltou: zerou");
            Assert.IsFalse(p.Completed);
        }

        [Test]
        public void SairDoRaio_ZeraComoSoltar()
        {
            // Sair do raio chega ao Core como "ninguém segurando": o mesmo caminho de soltar.
            var p = Novo();
            Passa(p, 1f, downed: true, holding: true);
            Passa(p, Decay + 0.1f, downed: true, holding: false);
            Assert.AreEqual(0f, p.Progress, Tol);
        }

        [Test]
        public void DecaimentoZero_ZeraNaHora()
        {
            var p = new ReviveProgress(Hold, 0f);
            Passa(p, 1f, downed: true, holding: true);
            p.Update(1f / 60f, targetDowned: true, rescuerHolding: false);
            Assert.AreEqual(0f, p.Progress, Tol);
        }

        [Test]
        public void SegurarDeNovoDepoisDeSoltar_ContinuaDeOndeEstava()
        {
            var p = Novo();
            Passa(p, 1f, downed: true, holding: true);          // 0,5
            Passa(p, 0.03f, downed: true, holding: false);       // decai pouco
            float depoisDeSoltar = p.Progress;
            Passa(p, 0.1f, downed: true, holding: true);
            Assert.Greater(p.Progress, depoisDeSoltar, "Voltou a subir sem recomeçar do zero");
        }

        [Test]
        public void NaoRevive_QuemNaoEstaCaido()
        {
            var p = Novo();
            Passa(p, Hold * 2f, downed: false, holding: true);
            Assert.AreEqual(0f, p.Progress, Tol);
            Assert.IsFalse(p.Completed, "Quem está de pé não 'completa' nada");
        }

        [Test]
        public void TempoDaQuedaAcabandoAntes_ZeraSemCompletar()
        {
            // downedDuration 1,5 s < holdSeconds 2 s: a queda acaba (volta no spawn) antes de o aliado terminar.
            var queda = new DownedState(1.5f);
            queda.Fall();
            var p = Novo();

            bool voltouNoSpawn = false;
            for (int i = 0; i < 600 && !voltouNoSpawn; i++)
            {
                p.Update(1f / 60f, queda.State == LifeState.Downed, rescuerHolding: true);
                voltouNoSpawn = queda.Tick(1f / 60f);
            }

            Assert.IsTrue(voltouNoSpawn, "A queda acabou");
            Assert.IsFalse(p.Completed, "O aliado não chegou a levantar");
            Assert.Greater(p.Progress, 0f, "Tinha progresso quando a queda acabou");

            p.Update(1f / 60f, queda.State == LifeState.Downed, rescuerHolding: true);
            Assert.AreEqual(0f, p.Progress, Tol, "Já de pé: zera");
        }

        [Test]
        public void Levantado_NaoVoltaNoSpawnPeloTempo()
        {
            var queda = new DownedState(5f);
            queda.Fall();
            var p = Novo();
            Passa(p, Hold + 0.1f, downed: true, holding: true);
            Assert.IsTrue(p.Completed);

            queda.Revive();
            Assert.AreEqual(LifeState.Alive, queda.State);
            Assert.IsFalse(queda.Tick(10f), "Levantado: o fim do tempo caído não volta no spawn");
            p.Update(1f / 60f, queda.State == LifeState.Downed, rescuerHolding: true);
            Assert.IsFalse(p.Completed, "De pé: o progresso recomeça limpo");
            Assert.AreEqual(0f, p.Progress, Tol);
        }

        [Test]
        public void Completo_FicaCompletoMesmoSoltando()
        {
            var p = Novo();
            Passa(p, Hold + 0.1f, downed: true, holding: true);
            Passa(p, 1f, downed: true, holding: false);
            Assert.IsTrue(p.Completed, "O host levanta no mesmo quadro; completo não decai");
            Assert.AreEqual(1f, p.Progress, Tol);
        }

        [Test]
        public void ReiniciarLimpa()
        {
            var p = Novo();
            Passa(p, Hold + 0.1f, downed: true, holding: true);
            p.Reset();
            Assert.AreEqual(0f, p.Progress, Tol);
            Assert.IsFalse(p.Completed);
        }

        [Test]
        public void DadosZerados_NaoQuebram()
        {
            var p = new ReviveProgress(0f, -1f);
            p.Update(1f / 60f, targetDowned: true, rescuerHolding: true);
            Assert.IsTrue(p.Completed);
            Assert.IsFalse(float.IsNaN(p.Progress));
        }

        // ---------- Escolha do aliado ----------

        [Test]
        public void VariosAliados_SoOMaisPertoLevanta()
        {
            var lista = new[]
            {
                new ReviveCandidate(2.0f, true),
                new ReviveCandidate(1.0f, true),
                new ReviveCandidate(1.5f, true),
            };
            Assert.AreEqual(1, ReviveRescuer.Choose(lista, 2.5f), "O mais perto");
        }

        [Test]
        public void VariosAliados_NaoSomamMaisRapido()
        {
            // O host entrega ao ReviveProgress um único "tem alguém segurando": com 3 aliados o tempo é o mesmo.
            var lista = new[]
            {
                new ReviveCandidate(1.0f, true),
                new ReviveCandidate(1.2f, true),
                new ReviveCandidate(1.4f, true),
            };
            bool alguem = ReviveRescuer.Choose(lista, 2.5f) >= 0;

            var tres = Novo();
            var um = Novo();
            Passa(tres, Hold * 0.5f, downed: true, holding: alguem);
            Passa(um, Hold * 0.5f, downed: true, holding: true);
            Assert.AreEqual(um.Progress, tres.Progress, Tol);
            Assert.AreEqual(0.5f, tres.Progress, 0.02f);
        }

        [Test]
        public void AliadoForaDoRaioOuSemSegurarNaoServe()
        {
            var lista = new[]
            {
                new ReviveCandidate(3.0f, true),   // fora do raio
                new ReviveCandidate(1.0f, false),  // perto, mas não segura E
            };
            Assert.AreEqual(-1, ReviveRescuer.Choose(lista, 2.5f));
        }

        [Test]
        public void SemAliados_NinguemLevanta()
        {
            Assert.AreEqual(-1, ReviveRescuer.Choose(new ReviveCandidate[0], 2.5f));
            Assert.AreEqual(-1, ReviveRescuer.Choose(null, 2.5f));
        }

        [Test]
        public void QuemJaLevantava_ContinuaEnquantoServir()
        {
            var lista = new[]
            {
                new ReviveCandidate(2.0f, true),  // já levantava, um pouco mais longe
                new ReviveCandidate(1.5f, true),  // apareceu um aliado mais perto
            };
            Assert.AreEqual(0, ReviveRescuer.Choose(lista, 2.5f, keepIndex: 0), "Não troca de aliado no meio");

            lista[0] = new ReviveCandidate(2.0f, false); // soltou
            Assert.AreEqual(1, ReviveRescuer.Choose(lista, 2.5f, keepIndex: 0), "Soltou: passa ao outro");
        }

        [Test]
        public void IndiceDeManterInvalido_EIgnorado()
        {
            var lista = new[] { new ReviveCandidate(1.0f, true) };
            Assert.AreEqual(0, ReviveRescuer.Choose(lista, 2.5f, keepIndex: 7));
            Assert.AreEqual(0, ReviveRescuer.Choose(lista, 2.5f, keepIndex: -1));
        }

        // ---------- Vida de volta ----------

        [Test]
        public void RestoreTo_VoltaComAFracaoDaVida()
        {
            var vida = new HealthModel(100f);
            vida.ApplyDamage(100f);
            Assert.IsTrue(vida.IsDepleted);

            vida.RestoreTo(0.5f);
            Assert.IsFalse(vida.IsDepleted);
            Assert.AreEqual(50f, vida.Current, Tol);
        }

        [Test]
        public void RestoreTo_LimitaEMantemVivo()
        {
            var vida = new HealthModel(100f);
            vida.ApplyDamage(100f);

            vida.RestoreTo(0f);
            Assert.IsFalse(vida.IsDepleted, "Quem levanta nunca fica com zero");
            vida.RestoreTo(7f);
            Assert.AreEqual(100f, vida.Current, Tol, "Acima de 1 vale a vida cheia");
            vida.RestoreTo(float.NaN);
            Assert.IsFalse(vida.IsDepleted);
        }
    }
}
