using System.Collections.Generic;
using Game.Core.AI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class EnemyCoreTests
    {
        private static EnemyBrain Melee() => new EnemyBrain(new BrainParams(1.6f, 0f, 0.6f, 0.8f));
        private static EnemyBrain Ranged() => new EnemyBrain(new BrainParams(9f, 5f, 0.8f, 1.2f));

        // ---------- EnemyBrain ----------

        [Test]
        public void Brain_SemAlvoFicaParado()
        {
            var brain = Melee();
            var o = brain.Tick(false, 0f, 0.1f);
            Assert.AreEqual(BrainState.Idle, o.State);
            Assert.AreEqual(BrainMove.Hold, o.Move);
            Assert.IsFalse(o.AttackReleased);
        }

        [Test]
        public void Brain_LongeDoAlvoPersegue()
        {
            var o = Melee().Tick(true, 10f, 0.1f);
            Assert.AreEqual(BrainState.Chase, o.State);
            Assert.AreEqual(BrainMove.Toward, o.Move);
            Assert.IsFalse(o.WindupStarted);
        }

        [Test]
        public void Brain_NoAlcanceAvisaEDepoisSoltaOGolpeUmaVez()
        {
            var brain = Melee();
            var start = brain.Tick(true, 1.0f, 0.25f);
            Assert.IsTrue(start.WindupStarted, "Aviso começa ao chegar no alcance");
            Assert.AreEqual(BrainState.Windup, start.State);
            Assert.AreEqual(BrainMove.Hold, start.Move);

            int releases = 0;
            float elapsed = 0f;
            bool releasedBeforeWindup = false;
            for (int i = 0; i < 4; i++)
            {
                var o = brain.Tick(true, 1.0f, 0.25f);
                elapsed += 0.25f;
                if (o.AttackReleased)
                {
                    releases++;
                    if (elapsed < 0.6f)
                        releasedBeforeWindup = true;
                }
            }

            Assert.IsFalse(releasedBeforeWindup, "Não solta antes do tempo de aviso");
            Assert.AreEqual(1, releases, "Solta exatamente uma vez");
        }

        [Test]
        public void Brain_DepoisDoGolpeRecuperaEVoltaAPerseguir()
        {
            var brain = Melee();
            brain.Tick(true, 1.0f, 0.25f);
            for (int i = 0; i < 3; i++)
                brain.Tick(true, 1.0f, 0.25f); // 0,75 s: o golpe saiu, agora recupera
            Assert.AreEqual(BrainState.Recover, brain.State);

            var during = brain.Tick(true, 10f, 0.25f);
            Assert.AreEqual(BrainState.Recover, during.State);
            Assert.AreEqual(BrainMove.Hold, during.Move);

            BrainOutput last = during;
            for (int i = 0; i < 4; i++)
                last = brain.Tick(true, 10f, 0.25f);
            Assert.AreEqual(BrainState.Chase, last.State, "Passada a recuperação, persegue");
            Assert.AreEqual(BrainMove.Toward, last.Move);
        }

        [Test]
        public void Brain_DroneRecuaSeOAlvoChegaPerto()
        {
            var brain = Ranged();
            var near = brain.Tick(true, 3f, 0.1f);
            Assert.AreEqual(BrainMove.Away, near.Move);
            Assert.IsFalse(near.WindupStarted);

            var far = brain.Tick(true, 12f, 0.1f);
            Assert.AreEqual(BrainMove.Toward, far.Move);

            var ok = brain.Tick(true, 7f, 0.1f);
            Assert.IsTrue(ok.WindupStarted, "Entre o mínimo e o alcance, mira");
        }

        [Test]
        public void Brain_ResetVoltaAoRepouso()
        {
            var brain = Melee();
            brain.Tick(true, 1f, 0.1f);
            Assert.AreEqual(BrainState.Windup, brain.State);
            brain.Reset();
            Assert.AreEqual(BrainState.Idle, brain.State);
        }

        // ---------- WaveDirector ----------

        private static WaveDirector Director(float pause = 6f, float delay = 3f) =>
            new WaveDirector(new List<WaveSpec> { new WaveSpec(3, 0, 0), new WaveSpec(3, 2, 0) }, pause, delay);

        [Test]
        public void Ondas_PrimeiraSaiSoDepoisDoAtraso()
        {
            var director = Director();
            var spawns = new List<int>();
            Assert.IsFalse(director.Tick(2.9f, 0, spawns));
            Assert.IsEmpty(spawns);
            Assert.IsTrue(director.Tick(0.2f, 0, spawns));
            Assert.AreEqual(new[] { 0, 0, 0 }, spawns);
            Assert.AreEqual(1, director.WavesReleased);
        }

        [Test]
        public void Ondas_ListaTemAQuantidadeDeCadaTipo()
        {
            var director = Director(0f, 0f);
            var spawns = new List<int>();
            director.Tick(0.1f, 0, spawns);
            Assert.IsFalse(director.Tick(0.1f, 3, spawns), "Onda 1 ainda viva");
            Assert.IsTrue(director.Tick(0.1f, 0, spawns));
            Assert.AreEqual(new[] { 0, 0, 0, 1, 1 }, spawns);
        }

        [Test]
        public void Ondas_ProximaSoComTodosMortosEDepoisDaPausa()
        {
            var director = Director();
            var spawns = new List<int>();
            director.Tick(3.1f, 0, spawns);

            Assert.IsFalse(director.Tick(100f, 2, spawns), "Com inimigo vivo não sai onda");
            Assert.IsFalse(director.Tick(5f, 0, spawns), "Pausa ainda não acabou");
            Assert.IsTrue(director.Tick(1.1f, 0, spawns), "Pausa acabou");
            Assert.AreEqual(2, director.WavesReleased);
        }

        [Test]
        public void Ondas_RecomecamDepoisDaUltima()
        {
            var director = Director(0f, 0f);
            var spawns = new List<int>();
            director.Tick(0.1f, 0, spawns);
            director.Tick(0.1f, 0, spawns);
            Assert.IsTrue(director.Tick(0.1f, 0, spawns));
            Assert.AreEqual(new[] { 0, 0, 0 }, spawns, "Terceira onda é a primeira de novo");
            Assert.AreEqual(1, director.NextWaveIndex);
        }

        [Test]
        public void Ondas_SemOndasNaoFazNada()
        {
            var director = new WaveDirector(new List<WaveSpec>(), 1f, 0f);
            var spawns = new List<int>();
            Assert.IsFalse(director.Tick(10f, 0, spawns));
        }
    }
}
