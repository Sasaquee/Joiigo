using System.Collections.Generic;
using Game.Core.AI;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Passe do mapa (D-077): rodízio das bocas, boca ocupada por jogador e fila com espaçamento por boca.</summary>
    public class SpawnMouthTests
    {
        private const float Clearance = 12f;

        // As três bocas do mapa novo (NO, N, NE), no plano.
        private static readonly Float2[] Mouths = { new Float2(-40.7f, 40.7f), new Float2(0f, 57.5f), new Float2(40.7f, 40.7f) };

        private static readonly Float2[] NoPlayers = new Float2[0];

        // ---------- SpawnMouthPicker ----------

        [Test]
        public void Rodizio_SemJogadorPerto_UmaBocaDeCadaVez()
        {
            var picks = new List<int>();
            int turn = 0;
            for (int i = 0; i < 6; i++)
            {
                int mouth = SpawnMouthPicker.Pick(Mouths, new[] { Float2.Zero }, turn, Clearance);
                picks.Add(mouth);
                turn = mouth + 1;
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 1, 2 }, picks, "Três inimigos saem um por boca, e o rodízio dá a volta");
        }

        [Test]
        public void Rodizio_SemNenhumJogador_NaoPulaNada()
        {
            Assert.AreEqual(1, SpawnMouthPicker.Pick(Mouths, NoPlayers, 1, Clearance));
        }

        [Test]
        public void BocaComJogadorVivoPerto_EPulada()
        {
            var players = new[] { new Float2(0f, 55f) }; // quase em cima da boca N (2,5 m)
            Assert.AreEqual(2, SpawnMouthPicker.Pick(Mouths, players, 1, Clearance), "A vez era da boca N: pula para a NE");
            Assert.AreEqual(0, SpawnMouthPicker.Pick(Mouths, players, 0, Clearance), "As outras bocas seguem no rodízio");
        }

        [Test]
        public void BocaNoLimiteDaFolga_NaoEstaOcupada()
        {
            var players = new[] { new Float2(0f, 57.5f - Clearance) }; // exatamente a 12 m da boca N
            Assert.AreEqual(1, SpawnMouthPicker.Pick(Mouths, players, 1, Clearance), "A 12 m já está livre; só menos que isso ocupa");
        }

        [Test]
        public void TodasOcupadas_UsaAMaisLonge()
        {
            // Dois jogadores: um em cima da boca NO, outro entre N e NE, bem mais perto de NE.
            var players = new[] { new Float2(-40.7f, 40.7f), new Float2(30f, 50f) };
            int mouth = SpawnMouthPicker.Pick(Mouths, players, 0, 45f); // folga enorme: todas "ocupadas"
            // Distância ao jogador mais perto: NO = 0; N = 30,8 ao jogador 2; NE = 14,4 ao jogador 2. A mais longe é N.
            Assert.AreEqual(1, mouth);
        }

        [Test]
        public void SemBocas_DevolveMenosUm()
        {
            Assert.AreEqual(-1, SpawnMouthPicker.Pick(new Float2[0], NoPlayers, 0, Clearance));
        }

        [Test]
        public void VezNegativaOuGrande_DaAVolta()
        {
            Assert.AreEqual(2, SpawnMouthPicker.Pick(Mouths, NoPlayers, -1, Clearance));
            Assert.AreEqual(1, SpawnMouthPicker.Pick(Mouths, NoPlayers, 7, Clearance));
        }

        // ---------- MouthSpawnQueue ----------

        private static List<QueuedSpawn> Step(MouthSpawnQueue queue, float dt)
        {
            var ready = new List<QueuedSpawn>();
            queue.Advance(dt, ready);
            return ready;
        }

        [Test]
        public void Fila_BocasDiferentesNascemNaMesmaHora()
        {
            var queue = new MouthSpawnQueue(3, 0.5f);
            queue.Enqueue(0, 0);
            queue.Enqueue(0, 1);
            queue.Enqueue(0, 2);
            Assert.AreEqual(3, queue.Pending);

            var ready = Step(queue, 0f);
            Assert.AreEqual(3, ready.Count, "Uma por boca: ninguém espera ninguém");
            Assert.AreEqual(0, queue.Pending);
        }

        [Test]
        public void Fila_MesmaBocaEsperaOEspacamento()
        {
            var queue = new MouthSpawnQueue(3, 0.5f);
            queue.Enqueue(0, 1);
            queue.Enqueue(1, 1);
            queue.Enqueue(2, 1);

            var ready = Step(queue, 0f);
            Assert.AreEqual(1, ready.Count, "O primeiro sai já");
            Assert.AreEqual(0, ready[0].Type);

            Assert.AreEqual(0, Step(queue, 0.25f).Count, "Aos 0,25 s o segundo ainda espera");
            ready = Step(queue, 0.25f);
            Assert.AreEqual(1, ready.Count, "Aos 0,5 s o segundo sai");
            Assert.AreEqual(1, ready[0].Type);

            Assert.AreEqual(0, Step(queue, 0.25f).Count);
            ready = Step(queue, 0.25f);
            Assert.AreEqual(1, ready.Count, "Aos 1,0 s o terceiro sai");
            Assert.AreEqual(2, ready[0].Type);
            Assert.AreEqual(0, queue.Pending);
        }

        [Test]
        public void Fila_ContaOsQueAindaNaoNasceram()
        {
            var queue = new MouthSpawnQueue(1, 0.5f);
            for (int i = 0; i < 4; i++)
                queue.Enqueue(0, 0);
            Assert.AreEqual(4, queue.Pending, "O WaveDirector recebe vivos + fila");
            Step(queue, 0f);
            Assert.AreEqual(3, queue.Pending);
            Step(queue, 1.5f);
            Assert.AreEqual(0, queue.Pending);
        }

        [Test]
        public void Fila_BocaLivreDepoisDoEspacamentoNascePronto()
        {
            var queue = new MouthSpawnQueue(2, 0.5f);
            queue.Enqueue(0, 0);
            Step(queue, 0f);
            Step(queue, 5f); // muito depois: a boca está livre
            queue.Enqueue(0, 0);
            Assert.AreEqual(1, Step(queue, 0f).Count, "Não herda espera de uma onda antiga");
        }

        [Test]
        public void Fila_SaiNaOrdemDeChegada()
        {
            var queue = new MouthSpawnQueue(2, 0.5f);
            queue.Enqueue(10, 0);
            queue.Enqueue(11, 1);
            queue.Enqueue(12, 0);
            queue.Enqueue(13, 1);

            var ready = Step(queue, 0.5f);
            CollectionAssert.AreEqual(new[] { 10, 11, 12, 13 }, ready.ConvertAll(r => r.Type));
        }

        [Test]
        public void Fila_BocaForaDoLimiteAvisa()
        {
            var queue = new MouthSpawnQueue(3, 0.5f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => queue.Enqueue(0, 3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => queue.Enqueue(0, -1));
        }

        [Test]
        public void Fila_SemEspacamento_NascemTodosJuntos()
        {
            var queue = new MouthSpawnQueue(1, 0f);
            queue.Enqueue(0, 0);
            queue.Enqueue(0, 0);
            Assert.AreEqual(2, Step(queue, 0f).Count);
        }

        [Test]
        public void FilaComDiretor_OndaSoAcabaQuandoAFilaEsvazia()
        {
            // O spawner passa vivos + fila ao diretor: com inimigos esperando para nascer a onda não acaba.
            var director = new WaveDirector(new List<WaveSpec> { new WaveSpec(3) }, 6f, 0f);
            var spawns = new List<int>();
            Assert.IsTrue(director.Tick(0.1f, 0, spawns), "A onda sai");
            Assert.AreEqual(3, spawns.Count);

            var queue = new MouthSpawnQueue(1, 0.5f);
            foreach (int type in spawns)
                queue.Enqueue(type, 0);
            Step(queue, 0f); // um nasceu, dois esperam
            director.Tick(0.1f, 0 /* nenhum vivo: o único nasceu e caiu */ + queue.Pending, spawns);
            Assert.AreEqual(0, director.WavesCleared, "Fila não vazia: a onda não acabou");

            Step(queue, 5f);
            director.Tick(0.1f, 0 + queue.Pending, spawns);
            Assert.AreEqual(1, director.WavesCleared, "Fila vazia e ninguém vivo: a onda acabou");
        }
    }
}
