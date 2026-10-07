using System.Collections.Generic;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Session;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Queda total (D-084, D-086, D-087): regra, detector, sequência e o que o recomeço apaga no Core.</summary>
    public class WipeRuleTests
    {
        private const LifeState Up = LifeState.Alive;
        private const LifeState Down = LifeState.Downed;

        private static List<LifeState> L(params LifeState[] states) => new List<LifeState>(states);

        // ---------- WipeRule ----------

        [Test]
        public void Regra_UmJogadorCaido_EQuedaTotal()
        {
            Assert.IsTrue(WipeRule.AllDowned(L(Down)), "No solo, o único jogador caído é queda total (D-084, igual ao coop)");
        }

        [Test]
        public void Regra_UmJogadorDePe_NaoEQuedaTotal()
        {
            Assert.IsFalse(WipeRule.AllDowned(L(Up)));
        }

        [Test]
        public void Regra_VariosTodosCaidos_EQuedaTotal()
        {
            Assert.IsTrue(WipeRule.AllDowned(L(Down, Down, Down, Down)));
        }

        [Test]
        public void Regra_SoAlgunsCaidos_NaoEQuedaTotal()
        {
            Assert.IsFalse(WipeRule.AllDowned(L(Down, Up)));
            Assert.IsFalse(WipeRule.AllDowned(L(Up, Down, Down)));
            Assert.IsFalse(WipeRule.AllDowned(L(Down, Down, Down, Up)));
        }

        [Test]
        public void Regra_ListaVaziaOuNula_NaoEQuedaTotal()
        {
            Assert.IsFalse(WipeRule.AllDowned(L()), "Ninguém conectado não é queda total");
            Assert.IsFalse(WipeRule.AllDowned(null));
        }

        [Test]
        public void Solo_EUmJogadorConectadoNoMaximo()
        {
            Assert.IsTrue(WipeRule.IsSolo(1), "Um jogador é solo");
            Assert.IsTrue(WipeRule.IsSolo(0), "Sem jogador não há quem espere a alavanca: trata como solo");
            Assert.IsFalse(WipeRule.IsSolo(2), "Dois ou mais é coop (D-087: espera a alavanca)");
            Assert.IsFalse(WipeRule.IsSolo(4));
        }

        // ---------- WipeDetector ----------

        [Test]
        public void Detector_UltimoACairDispara()
        {
            var detector = new WipeDetector();
            Assert.IsFalse(detector.Update(L(Up, Up)));
            Assert.IsFalse(detector.Update(L(Down, Up)), "Só um caiu: continua D-003");
            Assert.IsTrue(detector.Update(L(Down, Down)), "O último a cair dispara a queda total");
        }

        [Test]
        public void Detector_SoloUmJogadorCaidoDispara()
        {
            var detector = new WipeDetector();
            Assert.IsFalse(detector.Update(L(Up)));
            Assert.IsTrue(detector.Update(L(Down)));
        }

        [Test]
        public void Detector_DisparaUmaVezPorQuedaTotal()
        {
            var detector = new WipeDetector();
            Assert.IsTrue(detector.Update(L(Down, Down)));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(detector.Update(L(Down, Down)), "Enquanto todos seguem caídos não dispara de novo");
            Assert.IsTrue(detector.IsWiped);
        }

        [Test]
        public void Detector_RearmaQuandoAlguemLevanta()
        {
            var detector = new WipeDetector();
            Assert.IsTrue(detector.Update(L(Down, Down)));
            Assert.IsFalse(detector.Update(L(Up, Up)), "Todos acordaram");
            Assert.IsFalse(detector.IsWiped);
            Assert.IsFalse(detector.Update(L(Down, Up)));
            Assert.IsTrue(detector.Update(L(Down, Down)), "Uma nova queda total dispara outra vez");
        }

        [Test]
        public void Detector_ResetRearmaNaHora()
        {
            var detector = new WipeDetector();
            Assert.IsTrue(detector.Update(L(Down)));
            detector.Reset();
            Assert.IsTrue(detector.Update(L(Down)), "Depois do Reset a mesma lista dispara de novo");
        }

        [Test]
        public void Detector_ListaVaziaNaoDisparaERearma()
        {
            var detector = new WipeDetector();
            Assert.IsFalse(detector.Update(L()));
            Assert.IsTrue(detector.Update(L(Down)));
            Assert.IsFalse(detector.Update(L()), "Todos saíram da conexão: nada a recomeçar");
            Assert.IsFalse(detector.IsWiped);
            Assert.IsTrue(detector.Update(L(Down)), "Quem volta e cai de novo dispara");
        }

        [Test]
        public void Detector_SoAlgunsCaidosNuncaDispara()
        {
            var detector = new WipeDetector();
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(detector.Update(L(Down, Up, Down)));
        }

        // ---------- WipeSequence ----------

        [Test]
        public void Sequencia_ComecaParada()
        {
            var sequence = new WipeSequence(2f, 0.6f);
            Assert.AreEqual(WipePhase.Idle, sequence.Phase);
            Assert.IsFalse(sequence.IsRunning);
            Assert.AreEqual(WipeStep.None, sequence.Tick(1f), "Parada, o relógio não faz nada");
            Assert.AreEqual(WipePhase.Idle, sequence.Phase);
        }

        [Test]
        public void Sequencia_EscurecePedeOResetNoFimEDepoisSegura()
        {
            var sequence = new WipeSequence(2f, 0.6f);
            Assert.IsTrue(sequence.Begin());
            Assert.AreEqual(WipePhase.Fading, sequence.Phase);

            Assert.AreEqual(WipeStep.None, sequence.Tick(1f));
            Assert.AreEqual(WipeStep.None, sequence.Tick(0.9f));
            Assert.AreEqual(WipePhase.Fading, sequence.Phase, "Ainda escurecendo (1,9 s de 2 s)");

            Assert.AreEqual(WipeStep.ResetNow, sequence.Tick(0.2f), "No fim do escurecer zera a arena");
            Assert.AreEqual(WipePhase.Resetting, sequence.Phase);

            Assert.AreEqual(WipeStep.None, sequence.Tick(0.5f));
            Assert.AreEqual(WipePhase.Resetting, sequence.Phase, "O preto ainda segura (0,5 s de 0,6 s)");
            Assert.AreEqual(WipeStep.Finished, sequence.Tick(0.2f));
            Assert.AreEqual(WipePhase.Idle, sequence.Phase);
            Assert.IsFalse(sequence.IsRunning);
        }

        [Test]
        public void Sequencia_ResetNowSoUmaVezPorSequencia()
        {
            var sequence = new WipeSequence(1f, 1f);
            sequence.Begin();
            int resets = 0;
            int finished = 0;
            for (int i = 0; i < 100; i++)
            {
                WipeStep step = sequence.Tick(0.1f);
                if (step == WipeStep.ResetNow) resets++;
                if (step == WipeStep.Finished) finished++;
            }
            Assert.AreEqual(1, resets);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void Sequencia_NaoReiniciaEnquantoRoda_EPodeComecarDeNovoDepois()
        {
            var sequence = new WipeSequence(1f, 0.5f);
            Assert.IsTrue(sequence.Begin());
            sequence.Tick(0.5f);
            Assert.IsFalse(sequence.Begin(), "Já escurecendo: não reinicia o relógio");
            Assert.AreEqual(0.5f, sequence.Elapsed, 0.0001f);

            sequence.Tick(0.6f);
            Assert.AreEqual(WipePhase.Resetting, sequence.Phase);
            Assert.IsFalse(sequence.Begin(), "Também não reinicia no preto");

            sequence.Tick(0.6f);
            Assert.AreEqual(WipePhase.Idle, sequence.Phase);
            Assert.IsTrue(sequence.Begin(), "Acabou: uma nova queda total pode começar outra sequência");
        }

        [Test]
        public void Sequencia_AlguemLevantarDuranteOEscurecerNaoCancela()
        {
            // Com a queda total ninguém pode levantar ninguém; se alguém voltar de pé no meio (fim do tempo caído),
            // a sequência já começada segue até o fim. O detector rearma, mas não mexe na sequência.
            var detector = new WipeDetector();
            var sequence = new WipeSequence(2f, 0.6f);
            Assert.IsTrue(detector.Update(L(Down, Down)));
            sequence.Begin();

            sequence.Tick(1f);
            Assert.IsFalse(detector.Update(L(Up, Down)), "Um voltou de pé durante o Fading");
            Assert.AreEqual(WipePhase.Fading, sequence.Phase, "A sequência continua");

            Assert.AreEqual(WipeStep.ResetNow, sequence.Tick(1.1f));
            Assert.AreEqual(WipeStep.Finished, sequence.Tick(0.7f));
        }

        [Test]
        public void Sequencia_DepoisDoRecomecoUmaNovaQuedaTotalDisparaDeNovo()
        {
            var detector = new WipeDetector();
            var sequence = new WipeSequence(1f, 0.5f);

            Assert.IsTrue(detector.Update(L(Down, Down)));
            sequence.Begin();
            Assert.AreEqual(WipeStep.ResetNow, sequence.Tick(1f));
            detector.Reset();                          // todos acordam no spawn
            Assert.IsFalse(detector.Update(L(Up, Up)));
            Assert.AreEqual(WipeStep.Finished, sequence.Tick(0.5f));

            Assert.IsTrue(detector.Update(L(Down, Down)), "A segunda queda total do mesmo grupo também recomeça");
            Assert.IsTrue(sequence.Begin());
        }

        [Test]
        public void Sequencia_TemposZeroPassamUmaFasePorTick()
        {
            var sequence = new WipeSequence(0f, 0f);
            sequence.Begin();
            Assert.AreEqual(WipeStep.ResetNow, sequence.Tick(0.016f));
            Assert.AreEqual(WipeStep.Finished, sequence.Tick(0.016f));
            Assert.AreEqual(WipePhase.Idle, sequence.Phase);
        }

        [Test]
        public void Sequencia_PassoInvalidoNaoFazNada()
        {
            var sequence = new WipeSequence(1f, 1f);
            sequence.Begin();
            Assert.AreEqual(WipeStep.None, sequence.Tick(0f));
            Assert.AreEqual(WipeStep.None, sequence.Tick(-5f));
            Assert.AreEqual(WipeStep.None, sequence.Tick(float.NaN));
            Assert.AreEqual(WipePhase.Fading, sequence.Phase);
            Assert.AreEqual(0f, sequence.Elapsed, 0.0001f);
        }

        [Test]
        public void Sequencia_TemposNegativosViramZero()
        {
            var sequence = new WipeSequence(-3f, -1f);
            sequence.Begin();
            Assert.AreEqual(WipeStep.ResetNow, sequence.Tick(0.01f));
            Assert.AreEqual(WipeStep.Finished, sequence.Tick(0.01f));
        }

        // ---------- O que o recomeço apaga (Core) ----------

        [Test]
        public void Loadout_ClearApagaInventarioEspacosECinto()
        {
            var loadout = new Loadout(id => id switch
            {
                0 => CardKind.Skill,
                1 => CardKind.Passive,
                2 => CardKind.Equipment,
                _ => CardKind.Item
            });
            for (int id = 0; id < 4; id++)
                loadout.AddToInventory(id);
            loadout.AddToInventory(3);                     // segunda cópia do consumível
            loadout.AddToInventory(0);                     // uma skill fica no inventário
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(0, SlotType.Skill, 0));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(1, SlotType.Passive, 0));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(2, SlotType.Equipment, 1));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(3, SlotType.Belt, 0));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(3, SlotType.Belt, 0));
            Assert.AreEqual(2, loadout.BeltCount(0));
            int versionBefore = loadout.Version;

            loadout.Clear();

            Assert.AreEqual(0, loadout.Inventory.Count);
            foreach (SlotType slot in new[] { SlotType.Skill, SlotType.Passive, SlotType.Equipment, SlotType.Belt })
                for (int i = 0; i < CardRules.SlotCount(slot); i++)
                    Assert.AreEqual(Loadout.Empty, loadout.Get(slot, i), $"{slot} {i} vazio");
            for (int i = 0; i < CardRules.BeltSlots; i++)
                Assert.AreEqual(0, loadout.BeltCount(i), $"Cinto {i} sem cópias");
            for (int id = 0; id < 4; id++)
                Assert.IsFalse(loadout.Has(id), $"Carta {id} sumiu");
            Assert.AreNotEqual(versionBefore, loadout.Version, "A versão muda: a rede republica");
            CollectionAssert.IsEmpty(new List<int>(loadout.EquippedModifierCards()));

            // Depois de limpar, o jogador volta a receber e equipar normalmente.
            loadout.AddToInventory(0);
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(0, SlotType.Skill, 2));
        }

        [Test]
        public void Caminho_ClearEsqueceAsTags()
        {
            var path = new PathTracker();
            path.Record(new[] { "engrenagem", "vapor" });
            path.Record(new[] { "engrenagem" });
            Assert.AreEqual(2, path.Counts["engrenagem"]);

            path.Clear();

            Assert.AreEqual(0, path.Counts.Count);
            Assert.AreEqual(0, path.Top(2).Count);
            path.Record(new[] { "vapor" });
            Assert.AreEqual(1, path.Counts["vapor"], "Recomeça a contar do zero");
        }
    }
}
