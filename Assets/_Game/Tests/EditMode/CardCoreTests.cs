using System.Collections.Generic;
using System.Linq;
using Game.Core.Cards;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CardCoreTests
    {
        private const float Tol = 1e-3f;

        // Ids falsos: o Loadout só precisa saber o tipo de cada carta.
        private const int SkillA = 1;
        private const int SkillB = 2;
        private const int SkillC = 3;
        private const int SkillD = 4;
        private const int SkillE = 5;
        private const int PassiveA = 10;
        private const int PassiveB = 11;
        private const int PassiveC = 12;
        private const int ItemA = 20;
        private const int ItemB = 21;
        private const int EquipA = 30;
        private const int EquipB = 31;
        private const int EquipC = 32;
        private const int NotOwned = 99;

        private static readonly Dictionary<int, CardKind> Kinds = new Dictionary<int, CardKind>
        {
            { SkillA, CardKind.Skill }, { SkillB, CardKind.Skill }, { SkillC, CardKind.Skill },
            { SkillD, CardKind.Skill }, { SkillE, CardKind.Skill },
            { PassiveA, CardKind.Passive }, { PassiveB, CardKind.Passive }, { PassiveC, CardKind.Passive },
            { ItemA, CardKind.Item }, { ItemB, CardKind.Item },
            { EquipA, CardKind.Equipment }, { EquipB, CardKind.Equipment }, { EquipC, CardKind.Equipment },
            { NotOwned, CardKind.Skill }
        };

        private static Loadout NewLoadout(params int[] owned)
        {
            var loadout = new Loadout(id => Kinds[id]);
            foreach (int id in owned)
                loadout.AddToInventory(id);
            return loadout;
        }

        // ---------- CardRules ----------

        [Test]
        public void Regras_NaipeDefineOTipo()
        {
            Assert.AreEqual(CardKind.Skill, CardRules.KindOf(Suit.Swords));
            Assert.AreEqual(CardKind.Item, CardRules.KindOf(Suit.Cups));
            Assert.AreEqual(CardKind.Passive, CardRules.KindOf(Suit.Wands));
            Assert.AreEqual(CardKind.Equipment, CardRules.KindOf(Suit.Pentacles));
        }

        [Test]
        public void Regras_CadaTipoTemSeuEspaco()
        {
            Assert.AreEqual(SlotType.Skill, CardRules.SlotFor(CardKind.Skill));
            Assert.AreEqual(SlotType.Belt, CardRules.SlotFor(CardKind.Item));
            Assert.AreEqual(SlotType.Passive, CardRules.SlotFor(CardKind.Passive));
            Assert.AreEqual(SlotType.Equipment, CardRules.SlotFor(CardKind.Equipment));
        }

        [Test]
        public void Regras_QuantidadeDeEspacos()
        {
            Assert.AreEqual(4, CardRules.SlotCount(SlotType.Skill));
            Assert.AreEqual(2, CardRules.SlotCount(SlotType.Passive));
            Assert.AreEqual(2, CardRules.SlotCount(SlotType.Equipment));
            Assert.AreEqual(3, CardRules.SlotCount(SlotType.Belt), "D-029: cinto de 3 espaços");
        }

        [TestCase(1, "I")]
        [TestCase(4, "IV")]
        [TestCase(9, "IX")]
        [TestCase(14, "XIV")]
        [TestCase(16, "XVI")]
        [TestCase(21, "XXI")]
        public void Regras_NumeralRomano(int n, string esperado)
        {
            Assert.AreEqual(esperado, CardRules.Roman(n));
        }

        // ---------- Loadout: equipar ----------

        [Test]
        public void Loadout_EquipaSkillNoEspacoDeSkill_ERetiraDoInventario()
        {
            var loadout = NewLoadout(SkillA);

            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(SkillA, SlotType.Skill, 0));

            Assert.AreEqual(SkillA, loadout.Get(SlotType.Skill, 0));
            Assert.IsFalse(loadout.Inventory.Contains(SkillA));
            Assert.AreEqual(0, loadout.Inventory.Count);
        }

        [Test]
        public void Loadout_EquipaCadaTipoNoSeuEspaco()
        {
            var loadout = NewLoadout(SkillA, PassiveA, EquipA, ItemA);

            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(SkillA, SlotType.Skill, 3));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(PassiveA, SlotType.Passive, 1));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(EquipA, SlotType.Equipment, 1));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(ItemA, SlotType.Belt, 2));

            Assert.AreEqual(SkillA, loadout.Get(SlotType.Skill, 3));
            Assert.AreEqual(PassiveA, loadout.Get(SlotType.Passive, 1));
            Assert.AreEqual(EquipA, loadout.Get(SlotType.Equipment, 1));
            Assert.AreEqual(ItemA, loadout.Get(SlotType.Belt, 2));
        }

        [Test]
        public void Loadout_NaoEquipaCartaQueNaoTem()
        {
            var loadout = NewLoadout(SkillA);
            int versao = loadout.Version;

            Assert.AreEqual(EquipResult.NotOwned, loadout.TryEquip(NotOwned, SlotType.Skill, 0));

            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Skill, 0));
            Assert.AreEqual(versao, loadout.Version);
        }

        [Test]
        public void Loadout_NaoEquipaDuasVezesAMesmaCopia()
        {
            var loadout = NewLoadout(SkillA);
            loadout.TryEquip(SkillA, SlotType.Skill, 0);

            Assert.AreEqual(EquipResult.NotOwned, loadout.TryEquip(SkillA, SlotType.Skill, 1));
            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Skill, 1));
        }

        [Test]
        public void Loadout_NaoEquipaNoEspacoErrado()
        {
            var loadout = NewLoadout(SkillA, PassiveA, ItemA, EquipA);
            int versao = loadout.Version;

            Assert.AreEqual(EquipResult.WrongSlot, loadout.TryEquip(PassiveA, SlotType.Skill, 0), "passiva no espaço de skill");
            Assert.AreEqual(EquipResult.WrongSlot, loadout.TryEquip(ItemA, SlotType.Passive, 0), "item no espaço de passiva");
            Assert.AreEqual(EquipResult.WrongSlot, loadout.TryEquip(SkillA, SlotType.Belt, 0), "skill no cinto");
            Assert.AreEqual(EquipResult.WrongSlot, loadout.TryEquip(EquipA, SlotType.Passive, 0), "equipamento no espaço de passiva");
            Assert.AreEqual(EquipResult.WrongSlot, loadout.TryEquip(SkillA, SlotType.Equipment, 0), "skill no espaço de equipamento");

            Assert.AreEqual(4, loadout.Inventory.Count, "nada saiu do inventário");
            Assert.AreEqual(versao, loadout.Version);
        }

        [Test]
        public void Loadout_NaoPassaDoLimiteDeEspacos()
        {
            var loadout = NewLoadout(SkillA, PassiveA, ItemA, EquipA);
            int versao = loadout.Version;

            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(SkillA, SlotType.Skill, 4), "skill 4 (limite é 4 espaços)");
            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(PassiveA, SlotType.Passive, 2), "passiva 2 (limite é 2)");
            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(EquipA, SlotType.Equipment, 2), "equipamento 2 (limite é 2)");
            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(ItemA, SlotType.Belt, 3), "cinto 3 (limite é 3)");

            Assert.AreEqual(4, loadout.Inventory.Count, "nada saiu do inventário");
            Assert.AreEqual(versao, loadout.Version);
        }

        [Test]
        public void Loadout_IndiceNegativoEhInvalido()
        {
            var loadout = NewLoadout(SkillA, PassiveA, ItemA, EquipA);

            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(SkillA, SlotType.Skill, -1));
            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(PassiveA, SlotType.Passive, -1));
            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(EquipA, SlotType.Equipment, -1));
            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(ItemA, SlotType.Belt, -1));
            Assert.AreEqual(4, loadout.Inventory.Count);
        }

        [Test]
        public void Loadout_QuatroSkillsCabemENenhumaAMais()
        {
            var loadout = NewLoadout(SkillA, SkillB, SkillC, SkillD, SkillE);
            int[] ids = { SkillA, SkillB, SkillC, SkillD };

            for (int i = 0; i < 4; i++)
                Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(ids[i], SlotType.Skill, i));

            Assert.AreEqual(EquipResult.InvalidIndex, loadout.TryEquip(SkillE, SlotType.Skill, 4));
            CollectionAssert.AreEqual(new[] { SkillE }, loadout.Inventory);
        }

        [Test]
        public void Loadout_LeituraForaDosLimitesDevolveVazio()
        {
            var loadout = NewLoadout();

            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Skill, 4));
            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Belt, -1));
            Assert.AreEqual(0, loadout.BeltCount(3));
            Assert.AreEqual(0, loadout.BeltCount(-1));
        }

        [Test]
        public void Loadout_TrocarDevolveACartaAnteriorAoInventario()
        {
            var loadout = NewLoadout(SkillA, SkillB);
            loadout.TryEquip(SkillA, SlotType.Skill, 0);

            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(SkillB, SlotType.Skill, 0));

            Assert.AreEqual(SkillB, loadout.Get(SlotType.Skill, 0));
            CollectionAssert.AreEqual(new[] { SkillA }, loadout.Inventory);
        }

        // ---------- Loadout: cinto ----------

        [Test]
        public void Loadout_CintoEmpilhaOMesmoItem()
        {
            var loadout = NewLoadout(ItemA, ItemA);

            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(ItemA, SlotType.Belt, 0));
            Assert.AreEqual(1, loadout.BeltCount(0));
            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(ItemA, SlotType.Belt, 0));

            Assert.AreEqual(ItemA, loadout.Get(SlotType.Belt, 0));
            Assert.AreEqual(2, loadout.BeltCount(0));
            Assert.AreEqual(0, loadout.Inventory.Count);
        }

        [Test]
        public void Loadout_CintoItemDiferenteSubstituiEDevolveTodasAsCopias()
        {
            var loadout = NewLoadout(ItemA, ItemA, ItemB);
            loadout.TryEquip(ItemA, SlotType.Belt, 0);
            loadout.TryEquip(ItemA, SlotType.Belt, 0);

            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(ItemB, SlotType.Belt, 0));

            Assert.AreEqual(ItemB, loadout.Get(SlotType.Belt, 0));
            Assert.AreEqual(1, loadout.BeltCount(0));
            Assert.AreEqual(2, loadout.Inventory.Count(id => id == ItemA), "as duas cópias voltaram");
            Assert.AreEqual(0, loadout.Inventory.Count(id => id == ItemB));
        }

        [Test]
        public void Loadout_CintoOMesmoItemEmOutroEspacoNaoEmpilha()
        {
            var loadout = NewLoadout(ItemA, ItemA);
            loadout.TryEquip(ItemA, SlotType.Belt, 0);

            Assert.AreEqual(EquipResult.Ok, loadout.TryEquip(ItemA, SlotType.Belt, 1));

            Assert.AreEqual(1, loadout.BeltCount(0));
            Assert.AreEqual(1, loadout.BeltCount(1));
        }

        [Test]
        public void Loadout_ConsumirDiminuiEEsvaziaEmZero()
        {
            var loadout = NewLoadout(ItemA, ItemA);
            loadout.TryEquip(ItemA, SlotType.Belt, 1);
            loadout.TryEquip(ItemA, SlotType.Belt, 1);

            Assert.AreEqual(ItemA, loadout.ConsumeBelt(1));
            Assert.AreEqual(1, loadout.BeltCount(1));
            Assert.AreEqual(ItemA, loadout.Get(SlotType.Belt, 1), "ainda resta uma");

            Assert.AreEqual(ItemA, loadout.ConsumeBelt(1));
            Assert.AreEqual(0, loadout.BeltCount(1));
            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Belt, 1));
            Assert.AreEqual(0, loadout.Inventory.Count, "consumido não volta ao inventário");
        }

        [Test]
        public void Loadout_ConsumirCintoVazioDevolveVazioESemMudarVersao()
        {
            var loadout = NewLoadout();
            int versao = loadout.Version;

            Assert.AreEqual(Loadout.Empty, loadout.ConsumeBelt(0));
            Assert.AreEqual(Loadout.Empty, loadout.ConsumeBelt(3), "fora do limite");
            Assert.AreEqual(Loadout.Empty, loadout.ConsumeBelt(-1));
            Assert.AreEqual(versao, loadout.Version);
        }

        // ---------- Loadout: desequipar ----------

        [Test]
        public void Loadout_DesequiparDevolveAoInventario()
        {
            var loadout = NewLoadout(PassiveA);
            loadout.TryEquip(PassiveA, SlotType.Passive, 0);

            Assert.IsTrue(loadout.Unequip(SlotType.Passive, 0));

            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Passive, 0));
            CollectionAssert.AreEqual(new[] { PassiveA }, loadout.Inventory);
        }

        [Test]
        public void Loadout_DesequiparCintoDevolveTodasAsCopias()
        {
            var loadout = NewLoadout(ItemA, ItemA);
            loadout.TryEquip(ItemA, SlotType.Belt, 2);
            loadout.TryEquip(ItemA, SlotType.Belt, 2);

            Assert.IsTrue(loadout.Unequip(SlotType.Belt, 2));

            Assert.AreEqual(Loadout.Empty, loadout.Get(SlotType.Belt, 2));
            Assert.AreEqual(0, loadout.BeltCount(2));
            Assert.AreEqual(2, loadout.Inventory.Count(id => id == ItemA));
        }

        [Test]
        public void Loadout_DesequiparEspacoVazioOuInvalidoFalhaSemMudarVersao()
        {
            var loadout = NewLoadout(SkillA);
            int versao = loadout.Version;

            Assert.IsFalse(loadout.Unequip(SlotType.Skill, 0), "vazio");
            Assert.IsFalse(loadout.Unequip(SlotType.Skill, 4), "fora do limite");
            Assert.IsFalse(loadout.Unequip(SlotType.Belt, -1), "negativo");
            Assert.AreEqual(versao, loadout.Version);
        }

        // ---------- Loadout: versão ----------

        [Test]
        public void Loadout_VersaoSobeEmCadaMudanca()
        {
            var loadout = NewLoadout();
            int v = loadout.Version;

            loadout.AddToInventory(SkillA);
            Assert.Greater(loadout.Version, v, "adicionar");
            v = loadout.Version;

            loadout.TryEquip(SkillA, SlotType.Skill, 0);
            Assert.Greater(loadout.Version, v, "equipar");
            v = loadout.Version;

            loadout.Unequip(SlotType.Skill, 0);
            Assert.Greater(loadout.Version, v, "desequipar");
            v = loadout.Version;

            loadout.RemoveFromInventory(SkillA);
            Assert.Greater(loadout.Version, v, "remover do inventário");
            v = loadout.Version;

            loadout.AddToInventory(ItemA);
            loadout.TryEquip(ItemA, SlotType.Belt, 0);
            v = loadout.Version;
            loadout.ConsumeBelt(0);
            Assert.Greater(loadout.Version, v, "consumir");
        }

        [Test]
        public void Loadout_RemoverDoInventarioQueNaoTemFalhaSemMudarVersao()
        {
            var loadout = NewLoadout(SkillA);
            int versao = loadout.Version;

            Assert.IsFalse(loadout.RemoveFromInventory(SkillB));
            Assert.AreEqual(versao, loadout.Version);
        }

        // ---------- Loadout: modificadores ----------

        [Test]
        public void Loadout_CartasDeModificadorSaoPassivasEEquipamentos()
        {
            var loadout = NewLoadout(SkillA, PassiveA, PassiveB, EquipA, EquipB, ItemA);
            loadout.TryEquip(SkillA, SlotType.Skill, 0);
            loadout.TryEquip(PassiveA, SlotType.Passive, 0);
            loadout.TryEquip(PassiveB, SlotType.Passive, 1);
            loadout.TryEquip(EquipA, SlotType.Equipment, 0);
            loadout.TryEquip(EquipB, SlotType.Equipment, 1);
            loadout.TryEquip(ItemA, SlotType.Belt, 0);

            var ids = loadout.EquippedModifierCards().ToList();

            CollectionAssert.AreEquivalent(new[] { PassiveA, PassiveB, EquipA, EquipB }, ids);
            Assert.IsFalse(ids.Contains(SkillA));
            Assert.IsFalse(ids.Contains(ItemA));
        }

        [Test]
        public void Loadout_CartasDeModificadorIgnoraEspacosVazios()
        {
            var loadout = NewLoadout(PassiveC, EquipC);
            Assert.AreEqual(0, loadout.EquippedModifierCards().Count());

            loadout.TryEquip(PassiveC, SlotType.Passive, 1);
            loadout.TryEquip(EquipC, SlotType.Equipment, 0);

            CollectionAssert.AreEquivalent(new[] { PassiveC, EquipC }, loadout.EquippedModifierCards().ToList());
        }

        // ---------- EnergyModel ----------

        [Test]
        public void Energia_ComecaNaFracaoInicialDoMaximo()
        {
            var energy = new EnergyModel(100f, 10f, 0.5f);

            Assert.AreEqual(100f, energy.Max, Tol);
            Assert.AreEqual(50f, energy.Current, Tol);
            Assert.AreEqual(0.5f, energy.Fraction, Tol);
        }

        [Test]
        public void Energia_PadraoComecaCheia()
        {
            var energy = new EnergyModel(80f, 5f);

            Assert.AreEqual(80f, energy.Current, Tol);
        }

        [Test]
        public void Energia_RegeneraComOTempoESoAteOMaximo()
        {
            var energy = new EnergyModel(100f, 10f, 0.5f);

            energy.Tick(1f);
            Assert.AreEqual(60f, energy.Current, Tol);

            energy.Tick(100f);
            Assert.AreEqual(100f, energy.Current, Tol);
        }

        [Test]
        public void Energia_GastarSemSaldoFalhaESemMudarOAtual()
        {
            var energy = new EnergyModel(100f, 10f, 0.2f);

            Assert.IsFalse(energy.CanSpend(30f));
            Assert.IsFalse(energy.TrySpend(30f));
            Assert.AreEqual(20f, energy.Current, Tol);
        }

        [Test]
        public void Energia_GastarComSaldoDesconta()
        {
            var energy = new EnergyModel(100f, 10f);

            Assert.IsTrue(energy.TrySpend(30f));
            Assert.AreEqual(70f, energy.Current, Tol);

            Assert.IsTrue(energy.TrySpend(70f), "gasta até zerar");
            Assert.AreEqual(0f, energy.Current, Tol);
            Assert.IsFalse(energy.TrySpend(1f));
        }

        [Test]
        public void Energia_AdicionarIgnoraValorNegativo()
        {
            var energy = new EnergyModel(100f, 10f, 0.5f);

            energy.Add(-20f);
            Assert.AreEqual(50f, energy.Current, Tol);

            energy.Add(20f);
            Assert.AreEqual(70f, energy.Current, Tol);

            energy.Add(500f);
            Assert.AreEqual(100f, energy.Current, Tol, "não passa do máximo");
        }

        // ---------- ModifierSet ----------

        [Test]
        public void Modificadores_SomaPorTipo()
        {
            var set = new ModifierSet();
            set.Add(new Modifier(ModifierKind.BasicDamage, 3f));
            set.Add(new Modifier(ModifierKind.BasicDamage, 2f));
            set.Add(new Modifier(ModifierKind.BasicRange, 0.5f));

            Assert.AreEqual(5f, set.Get(ModifierKind.BasicDamage), Tol);
            Assert.AreEqual(0.5f, set.Get(ModifierKind.BasicRange), Tol);
            Assert.AreEqual(0f, set.Get(ModifierKind.HurtNextHitBonus), Tol, "sem modificador vale zero");
        }

        [Test]
        public void Modificadores_ClearZeraTudo()
        {
            var set = new ModifierSet();
            set.Add(new Modifier(ModifierKind.BasicDamage, 3f));

            set.Clear();

            Assert.AreEqual(0f, set.Get(ModifierKind.BasicDamage), Tol);
        }

        [Test]
        public void Modificadores_RecargaSemModificadorEUm()
        {
            Assert.AreEqual(1f, new ModifierSet().CooldownMultiplier, Tol);
        }

        [Test]
        public void Modificadores_RecargaMenos25PorCentoVira075()
        {
            var set = new ModifierSet();
            set.Add(new Modifier(ModifierKind.CooldownChange, -0.25f));

            Assert.AreEqual(0.75f, set.CooldownMultiplier, Tol);
        }

        [Test]
        public void Modificadores_RecargaNuncaFicaAbaixoDeDezPorCento()
        {
            var set = new ModifierSet();
            set.Add(new Modifier(ModifierKind.CooldownChange, -2f));

            Assert.AreEqual(0.1f, set.CooldownMultiplier, Tol);
        }

        // ---------- PathTracker ----------

        [Test]
        public void Caminho_ContaAsTags()
        {
            var path = new PathTracker();
            path.Record(new[] { "fogo", "fogo", "escudo" });
            path.Record(new[] { "fogo" });

            Assert.AreEqual(3, path.Counts["fogo"]);
            Assert.AreEqual(1, path.Counts["escudo"]);
            Assert.AreEqual(2, path.Counts.Count);
        }

        [Test]
        public void Caminho_IgnoraTagVaziaOuNula()
        {
            var path = new PathTracker();
            path.Record(new[] { "", null, "fogo" });

            Assert.AreEqual(1, path.Counts.Count);
            Assert.AreEqual(1, path.Counts["fogo"]);
        }

        [Test]
        public void Caminho_TopOrdenaPorContagemEDepoisAlfabetico()
        {
            var path = new PathTracker();
            path.Record(new[] { "zeta", "alfa", "beta", "beta", "gama", "gama", "ouro", "ouro", "ouro" });

            // ouro (3), depois beta e gama (2, alfabético), depois alfa e zeta (1, alfabético).
            CollectionAssert.AreEqual(new[] { "ouro", "beta", "gama", "alfa", "zeta" }, path.Top(5).ToList());
            CollectionAssert.AreEqual(new[] { "ouro", "beta" }, path.Top(2).ToList());
        }

        [Test]
        public void Caminho_TopMaiorQueOTotalDevolveTudoESemTagsDevolveVazio()
        {
            var path = new PathTracker();
            Assert.AreEqual(0, path.Top(3).Count);

            path.Record(new[] { "a", "b" });
            Assert.AreEqual(2, path.Top(10).Count);
        }
    }
}
