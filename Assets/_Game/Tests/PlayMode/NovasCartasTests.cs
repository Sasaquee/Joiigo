using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Combat;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Dice;
using Game.Dice;
using Game.Net;
using Game.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Fase 10 (D-088): as 22 cartas novas (ids 14 a 35 do banco, 15 a 36 do contrato em cartas-prototipo.md), com o host sozinho
    /// na Arena. Os números aqui são os do contrato (tabela), de propósito fixos no teste: mudar a carta no asset sem mudar o
    /// contrato quebra o teste. Alvos de teste: DummyTarget (só soma o dano) em gatilhos, que a investida atravessa.
    /// </summary>
    public class NovasCartasTests
    {
        private const float Tol = 0.01f;

        // Ordem do contrato: os 22 ids novos em sequência a partir do 14 do banco.
        private static readonly string[] NewIds =
        {
            "giro_engrenagem", "chicote_corrente", "tempestade_faiscas", "martelo_vapor",
            "vapor_condensado", "frasco_faisca", "calice_cheio", "tonico_fraco",
            "passo_pistao", "coracao_caldeira", "pavio_curto", "fornalha_faminta",
            "luva_cobre", "cristal_fenda", "engrenagem_mestra", "bracadeira_latao",
            "carro_vapor", "ceifadora_engrenagens", "estrela_cristal", "forca",
            "coroa_rebites", "pacto_cristal"
        };

        private static readonly string[] OldIds =
        {
            "pistao_runico", "sopro_caldeira", "arco_voltaico", "mina_engrenagem", "broquel_cantante", "tonico_oleo_luz",
            "granada_cristal", "caldeira_interna", "mola_recuo", "manopla_pistonada", "lente_prismatica", "lamina_sedenta",
            "chamine_partida", "artifice"
        };

        private static readonly HashSet<string> Themes = new HashSet<string> { "vapor", "engrenagem", "faisca", "cristal" };

        private NetworkPlayer player;
        private NetworkHealth health;
        private PlayerCards cards;
        private PlayerCombat combat;
        private PlayerMotor motor;
        private readonly List<DummyTarget> dummies = new List<DummyTarget>();

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            var session = Object.FindFirstObjectByType<NetSession>();
            Assert.IsTrue(session.Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            player.GetComponent<PlayerInputReader>().enabled = false; // sem mouse no teste
            health = player.GetComponent<NetworkHealth>();
            cards = player.GetComponent<PlayerCards>();
            combat = player.GetComponent<PlayerCombat>();
            motor = player.GetComponent<PlayerMotor>();
            Assert.IsNotNull(cards, "Jogador tem PlayerCards (reconstrua a arena)");
            Assert.IsNotNull(cards.Database, "PlayerCards tem o banco de cartas");

            // Campo aberto no centro da arena.
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = Vector3.zero;
            controller.enabled = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            foreach (DummyTarget dummy in dummies)
                if (dummy != null)
                    Object.Destroy(dummy.gameObject);
            dummies.Clear();
            yield return ArenaTestScene.Cleanup();
        }

        // ---------- Auxiliares ----------

        private int Give(string cardId)
        {
            int id = cards.FindCardId(cardId);
            Assert.GreaterOrEqual(id, 0, $"Carta {cardId} existe no banco (reconstrua as cartas)");
            cards.ServerGiveCard(id);
            return id;
        }

        private CardData GiveAndEquip(string cardId, SlotType slot, int index)
        {
            int id = Give(cardId);
            cards.RequestEquip(id, slot, index);
            Assert.AreEqual(id, cards.GetSlot(slot, index), $"{cardId} equipada");
            return cards.Database.Get(id);
        }

        private CardData Data(string cardId)
        {
            int id = cards.FindCardId(cardId);
            Assert.GreaterOrEqual(id, 0, $"Carta {cardId} existe no banco");
            return cards.Database.Get(id);
        }

        private Vector3 AimForward() => player.transform.position + Vector3.forward * 8f;

        /// <summary>Alvo de teste à frente (z) e ao lado (x) do jogador, num gatilho (nada o bloqueia nem o empurra).</summary>
        private DummyTarget Dummy(float forward, float side = 0f)
        {
            var go = new GameObject("AlvoTeste");
            go.transform.position = player.transform.position + new Vector3(side, 0f, forward);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 1f, 0f);
            sphere.radius = 0.4f;
            sphere.isTrigger = true;
            var dummy = go.AddComponent<DummyTarget>();
            dummies.Add(dummy);
            Physics.SyncTransforms();
            return dummy;
        }

        private static T FirstEffect<T>(CardData card) where T : CardEffect
        {
            foreach (var effect in card.effects)
                if (effect is T typed)
                    return typed;
            Assert.Fail($"{card.id} não tem efeito {typeof(T).Name}");
            return null;
        }

        /// <summary>Usa a skill do espaço 0 e confere o custo de energia, o início da recarga e a duração dela.</summary>
        private void UseAndCheckCost(CardData card, int slot = 0)
        {
            float energyBefore = cards.Energy;
            Assert.AreEqual(0f, cards.SkillCooldownFraction(slot), 0.001f, "Pronta antes de usar");

            Assert.IsTrue(cards.ServerUseSkill(slot, AimForward()), $"{card.id} foi usada");

            Assert.AreEqual(energyBefore - card.energyCost, cards.Energy, Tol, "Gastou a energia da carta");
            Assert.Greater(cards.SkillCooldownFraction(slot), 0.9f, "Recarga começou");
            Assert.AreEqual(card.cooldown * cards.Modifiers.CooldownMultiplier, cards.SkillCooldownDuration(slot), Tol, "Recarga da carta");
        }

        /// <summary>Gasta quase toda a energia (95) com três skills sem alvo, para sobrar espaço aos itens que devolvem energia.</summary>
        private void DrainEnergy()
        {
            GiveAndEquip("martelo_vapor", SlotType.Skill, 1);
            GiveAndEquip("tempestade_faiscas", SlotType.Skill, 2);
            GiveAndEquip("giro_engrenagem", SlotType.Skill, 3);
            Assert.IsTrue(cards.ServerUseSkill(1, AimForward()));
            Assert.IsTrue(cards.ServerUseSkill(2, AimForward()));
            Assert.IsTrue(cards.ServerUseSkill(3, AimForward()));
            Assert.Less(cards.Energy, 10f, "Energia quase toda gasta");
        }

        private IEnumerator Attack()
        {
            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(combat.Settings.basicHitDelay + 0.1f);
        }

        private IEnumerator WaitAttackCooldown()
        {
            yield return new WaitForSeconds(combat.Settings.basicCooldown + 0.1f);
        }

        // ---------- O banco e o contrato ----------

        [UnityTest]
        public IEnumerator Banco_Tem36CartasComOsIdsDoContratoNaOrdem()
        {
            var database = cards.Database;
            Assert.AreEqual(36, database.Count, "14 antigas + 22 novas (D-088)");

            for (int i = 0; i < OldIds.Length; i++)
                Assert.AreEqual(OldIds[i], database.Get(i).id, $"As 14 antigas não mudam de posição (ids da rede): {i}");
            for (int i = 0; i < NewIds.Length; i++)
                Assert.AreEqual(NewIds[i], database.Get(OldIds.Length + i).id, $"Nova carta {i + 15} do contrato na posição {OldIds.Length + i}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Banco_IdsUnicosETodaCartaTemEfeitoOuModificadorEDadosValidos()
        {
            var database = cards.Database;
            var seenIds = new HashSet<string>();
            var seenCards = new HashSet<CardData>();
            for (int i = 0; i < database.Count; i++)
            {
                CardData card = database.Get(i);
                Assert.IsNotNull(card, $"Carta {i} existe");
                Assert.IsTrue(seenIds.Add(card.id), $"Id repetido: {card.id}");
                Assert.IsTrue(seenCards.Add(card), $"Asset repetido no banco: {card.id}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(card.displayName), $"{card.id} tem nome");

                bool hasEffect = card.effects != null && card.effects.Count > 0 && card.effects.TrueForAll(e => e != null);
                bool hasModifier = card.modifiers != null && card.modifiers.Count > 0;
                Assert.IsTrue(hasEffect || hasModifier, $"{card.id} tem efeito ou modificador");
                if (card.Kind == CardKind.Skill || card.Kind == CardKind.Item)
                    Assert.IsTrue(hasEffect, $"{card.id} (skill ou item) tem efeito");
                else
                    Assert.IsTrue(hasModifier, $"{card.id} (passiva ou equipamento) tem modificador");

                Assert.IsTrue(System.Enum.IsDefined(typeof(Rarity), card.rarity), $"{card.id} tem raridade válida");
                Assert.IsNotNull(card.tags, $"{card.id} tem lista de temas");
                Assert.Greater(card.tags.Length, 0, $"{card.id} tem pelo menos um tema");
                foreach (string tag in card.tags)
                    Assert.IsTrue(Themes.Contains(tag), $"{card.id}: tema desconhecido '{tag}'");
                foreach (var entry in card.modifiers)
                    Assert.IsTrue(System.Enum.IsDefined(typeof(ModifierKind), entry.kind), $"{card.id}: modificador válido");
                if (card.arcana == Arcana.Minor)
                    Assert.AreEqual(CardRules.KindOf(card.suit), card.Kind, $"{card.id}: o naipe define o tipo (D-036)");
            }
            yield return null;
        }

        /// <summary>id, arcano, naipe, número, tipo, raridade, amaldiçoada, energia, recarga, vida cobrada, temas.</summary>
        private struct Row
        {
            public string Id; public Arcana Arcana; public Suit Suit; public int Number; public CardKind Kind;
            public Rarity Rarity; public bool Cursed; public float Energy; public float Cooldown; public float Health; public string Tags;
        }

        private static Row R(string id, Arcana arcana, Suit suit, int number, CardKind kind, Rarity rarity, bool cursed,
            float energy, float cooldown, float health, string tags) => new Row
        {
            Id = id, Arcana = arcana, Suit = suit, Number = number, Kind = kind, Rarity = rarity, Cursed = cursed,
            Energy = energy, Cooldown = cooldown, Health = health, Tags = tags
        };

        [UnityTest]
        public IEnumerator Banco_AsNovasCartasTemOsDadosDaTabela()
        {
            const Arcana mi = Arcana.Minor;
            const Arcana ma = Arcana.Major;
            var table = new[]
            {
                R("giro_engrenagem", mi, Suit.Swords, 2, CardKind.Skill, Rarity.Common, false, 20f, 5f, 0f, "engrenagem"),
                R("chicote_corrente", mi, Suit.Swords, 4, CardKind.Skill, Rarity.Uncommon, false, 20f, 4f, 0f, "engrenagem,faisca"),
                R("tempestade_faiscas", mi, Suit.Swords, 9, CardKind.Skill, Rarity.Uncommon, false, 35f, 8f, 0f, "faisca,cristal"),
                R("martelo_vapor", mi, Suit.Swords, 10, CardKind.Skill, Rarity.Rare, false, 40f, 10f, 0f, "vapor,engrenagem"),
                R("vapor_condensado", mi, Suit.Cups, 6, CardKind.Item, Rarity.Common, false, 0f, 0f, 0f, "vapor"),
                R("frasco_faisca", mi, Suit.Cups, 8, CardKind.Item, Rarity.Uncommon, false, 0f, 0f, 0f, "faisca"),
                R("calice_cheio", mi, Suit.Cups, 10, CardKind.Item, Rarity.Uncommon, false, 0f, 0f, 0f, "cristal,vapor"),
                R("tonico_fraco", mi, Suit.Cups, 11, CardKind.Item, Rarity.Common, false, 0f, 0f, 0f, "vapor"),
                R("passo_pistao", mi, Suit.Wands, 3, CardKind.Passive, Rarity.Common, false, 0f, 0f, 0f, "engrenagem"),
                R("coracao_caldeira", mi, Suit.Wands, 8, CardKind.Passive, Rarity.Uncommon, false, 0f, 0f, 0f, "vapor"),
                R("pavio_curto", mi, Suit.Wands, 2, CardKind.Passive, Rarity.Common, false, 0f, 0f, 0f, "faisca"),
                R("fornalha_faminta", mi, Suit.Wands, 5, CardKind.Passive, Rarity.Uncommon, false, 0f, 0f, 0f, "vapor,faisca"),
                R("luva_cobre", mi, Suit.Pentacles, 2, CardKind.Equipment, Rarity.Common, false, 0f, 0f, 0f, "engrenagem"),
                R("cristal_fenda", mi, Suit.Pentacles, 9, CardKind.Equipment, Rarity.Uncommon, false, 0f, 0f, 0f, "cristal"),
                R("engrenagem_mestra", mi, Suit.Pentacles, 14, CardKind.Equipment, Rarity.Uncommon, false, 0f, 0f, 0f, "engrenagem,cristal"),
                R("bracadeira_latao", mi, Suit.Pentacles, 5, CardKind.Equipment, Rarity.Uncommon, false, 0f, 0f, 0f, "engrenagem,vapor"),
                R("carro_vapor", ma, Suit.None, 7, CardKind.Skill, Rarity.Rare, false, 45f, 15f, 0f, "vapor,engrenagem"),
                R("ceifadora_engrenagens", ma, Suit.None, 13, CardKind.Skill, Rarity.Rare, false, 60f, 25f, 15f, "engrenagem,faisca"),
                R("estrela_cristal", ma, Suit.None, 17, CardKind.Skill, Rarity.Rare, false, 50f, 18f, 0f, "cristal,faisca"),
                R("forca", ma, Suit.None, 11, CardKind.Passive, Rarity.Unique, false, 0f, 0f, 0f, "engrenagem,vapor"),
                R("coroa_rebites", mi, Suit.Pentacles, 8, CardKind.Equipment, Rarity.Uncommon, true, 0f, 0f, 0f, "engrenagem,faisca"),
                R("pacto_cristal", mi, Suit.Wands, 7, CardKind.Passive, Rarity.Uncommon, true, 0f, 0f, 0f, "cristal"),
            };
            Assert.AreEqual(22, table.Length);

            foreach (Row row in table)
            {
                CardData card = Data(row.Id);
                Assert.AreEqual(row.Arcana, card.arcana, $"{row.Id}: arcano");
                Assert.AreEqual(row.Suit, card.suit, $"{row.Id}: naipe");
                Assert.AreEqual(row.Number, card.number, $"{row.Id}: número");
                Assert.AreEqual(row.Kind, card.Kind, $"{row.Id}: tipo");
                Assert.AreEqual(row.Rarity, card.rarity, $"{row.Id}: raridade");
                Assert.AreEqual(row.Cursed, card.cursed, $"{row.Id}: amaldiçoada");
                Assert.AreEqual(row.Energy, card.energyCost, Tol, $"{row.Id}: energia");
                Assert.AreEqual(row.Cooldown, card.cooldown, Tol, $"{row.Id}: recarga");
                Assert.AreEqual(row.Health, card.healthCostOnUse, Tol, $"{row.Id}: custo de vida");
                CollectionAssert.AreEquivalent(row.Tags.Split(','), card.tags, $"{row.Id}: temas");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Banco_AsNovasPassivasEEquipamentosTemOsModificadoresDaTabela()
        {
            AssertMods("passo_pistao", (ModifierKind.MoveSpeed, 0.10f));
            AssertMods("coracao_caldeira", (ModifierKind.MaxHealth, 30f));
            AssertMods("pavio_curto", (ModifierKind.CooldownChange, -0.12f), (ModifierKind.BasicDamage, -4f));
            AssertMods("fornalha_faminta", (ModifierKind.EnergyOnHitBonus, 0.9f), (ModifierKind.BasicDamage, -6f));
            AssertMods("luva_cobre", (ModifierKind.BasicDamage, 10f), (ModifierKind.BasicRange, -0.3f));
            AssertMods("cristal_fenda", (ModifierKind.BasicArcaneShift, 0.6f), (ModifierKind.BasicDamage, 4f));
            AssertMods("engrenagem_mestra", (ModifierKind.CooldownChange, -0.10f), (ModifierKind.BasicDamage, 4f));
            AssertMods("bracadeira_latao", (ModifierKind.BasicRange, 0.9f), (ModifierKind.EnergyOnHitBonus, 0.25f), (ModifierKind.BasicDamage, -3f));
            AssertMods("forca", (ModifierKind.BasicDamage, 14f));
            AssertMods("coroa_rebites", (ModifierKind.EnergyOnHitBonus, 1.2f), (ModifierKind.MaxHealth, -30f));
            AssertMods("pacto_cristal", (ModifierKind.BasicDamageMultiplier, 0.6f), (ModifierKind.LifeCostPerHit, 3f));
            yield return null;
        }

        private void AssertMods(string cardId, params (ModifierKind kind, float value)[] expected)
        {
            CardData card = Data(cardId);
            Assert.AreEqual(expected.Length, card.modifiers.Count, $"{cardId}: quantidade de modificadores");
            foreach (var (kind, value) in expected)
            {
                bool found = false;
                foreach (ModifierEntry entry in card.modifiers)
                {
                    if (entry.kind != kind)
                        continue;
                    found = true;
                    Assert.AreEqual(value, entry.value, 0.0001f, $"{cardId}: {kind}");
                }
                Assert.IsTrue(found, $"{cardId} tem o modificador {kind}");
            }
        }

        // ---------- O pool do D20 ----------

        private DiceTable CreateTable()
        {
            var service = Object.FindFirstObjectByType<CardDropService>();
            Assert.IsNotNull(service, "A cena tem o CardDropService");
            Assert.IsNotNull(service.Settings, "O serviço tem o DiceSettings");
            return service.Settings.CreateTable();
        }

        private CardDraw CreatePool()
        {
            var pool = new List<DrawCandidate>();
            var db = cards.Database;
            for (int id = 0; id < db.Count; id++)
            {
                CardData card = db.Get(id);
                pool.Add(new DrawCandidate(id, card.rarity, card.cursed, card.arcana == Arcana.Major, card.tags));
            }
            return new CardDraw(pool, 2f);
        }

        /// <summary>Todas as cartas principais que o resultado do dado pode entregar, sobre muitas sementes e temas.</summary>
        private HashSet<int> MainCardsFor(int roll)
        {
            DiceOutcome outcome = CreateTable().Resolve(roll);
            CardDraw draw = CreatePool();
            var found = new HashSet<int>();
            var themes = new List<string> { string.Empty };
            themes.AddRange(Themes);
            foreach (string theme in themes)
            {
                for (int seed = 0; seed < 400; seed++)
                {
                    var grants = draw.Draw(outcome, theme, id => null, new SeededRandom(seed));
                    if (grants.Count > 0)
                        found.Add(grants[0].CardId);
                }
            }
            return found;
        }

        [UnityTest]
        public IEnumerator Pool_NenhumaFaixaDoD20FicaSemCarta()
        {
            for (int roll = 1; roll <= D20.Faces; roll++)
            {
                HashSet<int> mains = MainCardsFor(roll);
                Assert.Greater(mains.Count, 0, $"O {roll} do D20 tem carta possível");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pool_O1SorteiaEntreAsTresAmaldicoadas()
        {
            var expected = new HashSet<int>
            {
                cards.FindCardId("lamina_sedenta"), cards.FindCardId("coroa_rebites"), cards.FindCardId("pacto_cristal")
            };
            var db = cards.Database;
            int cursedInBank = 0;
            for (int id = 0; id < db.Count; id++)
                if (db.Get(id).cursed)
                    cursedInBank++;
            Assert.AreEqual(3, cursedInBank, "Três amaldiçoadas no banco");

            HashSet<int> mains = MainCardsFor(1);

            Assert.IsTrue(mains.SetEquals(expected), "O 1 sorteia só entre as três amaldiçoadas e alcança as três");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pool_O20SorteiaEntreOsSeisArcanosMaiores()
        {
            var expected = new HashSet<int>();
            foreach (string id in new[] { "chamine_partida", "artifice", "carro_vapor", "ceifadora_engrenagens", "estrela_cristal", "forca" })
                expected.Add(cards.FindCardId(id));
            var db = cards.Database;
            int majorsInBank = 0;
            for (int id = 0; id < db.Count; id++)
                if (db.Get(id).arcana == Arcana.Major)
                    majorsInBank++;
            Assert.AreEqual(6, majorsInBank, "Seis arcanos maiores no banco (2 antigos + 4 novos)");

            HashSet<int> mains = MainCardsFor(20);

            Assert.IsTrue(mains.SetEquals(expected), "O 20 sorteia só entre os arcanos maiores e alcança os seis");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pool_AsFaixasComunsEIncomunsAlcancamCartasNovas()
        {
            HashSet<int> common = MainCardsFor(7); // 7 a 11: comum
            HashSet<int> uncommon = MainCardsFor(14); // 12 a 15: incomum
            Assert.IsTrue(common.Contains(cards.FindCardId("giro_engrenagem")), "Uma comum nova pode sair no 7 a 11");
            Assert.IsTrue(uncommon.Contains(cards.FindCardId("chicote_corrente")), "Uma incomum nova pode sair no 12 a 15");
            foreach (int id in common)
                Assert.AreEqual(Rarity.Common, cards.Database.Get(id).rarity);
            foreach (int id in uncommon)
                Assert.AreEqual(Rarity.Uncommon, cards.Database.Get(id).rarity);
            yield return null;
        }

        // ---------- Skills ----------

        [UnityTest]
        public IEnumerator Giro_FereQuemEstaEmVoltaEGastaEnergia()
        {
            CardData card = GiveAndEquip("giro_engrenagem", SlotType.Skill, 0);
            var burst = FirstEffect<AreaBurstEffect>(card);
            Assert.AreEqual(28f, burst.damage, Tol);
            Assert.AreEqual(0.3f, burst.arcaneFraction, Tol);
            Assert.AreEqual(2.5f, burst.radius, Tol);
            DummyTarget near = Dummy(1.5f);
            DummyTarget behind = Dummy(-1.5f);
            DummyTarget far = Dummy(5f);

            UseAndCheckCost(card);

            Assert.AreEqual(28f, near.Taken, Tol, "Quem está à frente, dentro do raio");
            Assert.AreEqual(28f, behind.Taken, Tol, "Quem está atrás, dentro do raio (golpe em volta)");
            Assert.AreEqual(0f, far.Taken, Tol, "Fora do raio");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Chicote_AlcancaLongeEPoucoPraOsLados()
        {
            CardData card = GiveAndEquip("chicote_corrente", SlotType.Skill, 0);
            var slash = FirstEffect<ArcSlashEffect>(card);
            Assert.AreEqual(30f, slash.damage, Tol);
            Assert.AreEqual(0.2f, slash.arcaneFraction, Tol);
            Assert.AreEqual(6f, slash.range, Tol);
            Assert.AreEqual(20f, slash.halfAngle, Tol);
            DummyTarget far = Dummy(5.5f);
            DummyTarget side = Dummy(3.06f, 2.57f); // a 40 graus do eixo
            DummyTarget beyond = Dummy(7.2f);

            UseAndCheckCost(card);

            Assert.AreEqual(30f, far.Taken, Tol, "Longe, no eixo");
            Assert.AreEqual(0f, side.Taken, Tol, "Fora da meia-abertura de 20 graus");
            Assert.AreEqual(0f, beyond.Taken, Tol, "Além do alcance de 6 m");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tempestade_SaltaEmCincoInimigos()
        {
            CardData card = GiveAndEquip("tempestade_faiscas", SlotType.Skill, 0);
            var chain = FirstEffect<ChainProjectileEffect>(card);
            Assert.AreEqual(14f, chain.damage, Tol);
            Assert.AreEqual(0.9f, chain.arcaneFraction, Tol);
            Assert.AreEqual(5, chain.jumps);
            var line = new List<DummyTarget>();
            for (int i = 0; i < 7; i++)
                line.Add(Dummy(3f + 1.5f * i));

            UseAndCheckCost(card);

            int hit = 0;
            foreach (DummyTarget dummy in line)
            {
                if (dummy.Taken > 0f)
                {
                    hit++;
                    Assert.AreEqual(14f, dummy.Taken, Tol, "Dano por salto");
                }
            }
            Assert.AreEqual(5, hit, "Cinco saltos, nem mais nem menos");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Martelo_PancadaPesadaNoArcoDe90Graus()
        {
            CardData card = GiveAndEquip("martelo_vapor", SlotType.Skill, 0);
            var slash = FirstEffect<ArcSlashEffect>(card);
            Assert.AreEqual(70f, slash.damage, Tol);
            Assert.AreEqual(0.5f, slash.arcaneFraction, Tol);
            Assert.AreEqual(3.5f, slash.range, Tol);
            Assert.AreEqual(45f, slash.halfAngle, Tol);
            DummyTarget front = Dummy(3f);
            DummyTarget diagonal = Dummy(2f, 1.5f); // cerca de 37 graus
            DummyTarget flank = Dummy(0.5f, 3f);    // quase de lado
            DummyTarget beyond = Dummy(5f);

            UseAndCheckCost(card);

            Assert.AreEqual(70f, front.Taken, Tol, "À frente");
            Assert.AreEqual(70f, diagonal.Taken, Tol, "Na diagonal, dentro dos 45 graus");
            Assert.AreEqual(0f, flank.Taken, Tol, "De lado");
            Assert.AreEqual(0f, beyond.Taken, Tol, "Além do alcance");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CarroDeVapor_InvesteNoveMetrosFerindoOCaminho()
        {
            CardData card = GiveAndEquip("carro_vapor", SlotType.Skill, 0);
            var dash = FirstEffect<DashEffect>(card);
            Assert.AreEqual(9f, dash.distance, Tol);
            Assert.AreEqual(55f, dash.damage, Tol);
            Assert.AreEqual(0.3f, dash.arcaneFraction, Tol);
            Assert.AreEqual(1.8f, dash.width, Tol);
            DummyTarget onPath = Dummy(4f);
            DummyTarget wide = Dummy(6f, 1.0f);    // dentro de 0,9 + raio 0,4
            DummyTarget outside = Dummy(6f, 2.5f); // fora da faixa
            Vector3 start = player.transform.position;

            UseAndCheckCost(card);

            Vector3 moved = player.transform.position - start;
            Assert.Greater(moved.z, 6f, "Investida longa para a frente");
            Assert.LessOrEqual(moved.z, 9f + 0.2f, "Não passa dos 9 m");
            Assert.AreEqual(55f, onPath.Taken, Tol, "No caminho");
            Assert.AreEqual(55f, wide.Taken, Tol, "Na largura da faixa");
            Assert.AreEqual(0f, outside.Taken, Tol, "Fora da faixa");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ceifadora_CeifaEmAreaEnormeECobraVida()
        {
            CardData card = GiveAndEquip("ceifadora_engrenagens", SlotType.Skill, 0);
            var burst = FirstEffect<AreaBurstEffect>(card);
            Assert.AreEqual(90f, burst.damage, Tol);
            Assert.AreEqual(0.5f, burst.arcaneFraction, Tol);
            Assert.AreEqual(7f, burst.radius, Tol);
            Assert.AreEqual(15f, card.healthCostOnUse, Tol);
            Assert.IsFalse(card.cursed, "Cobra vida, mas não é amaldiçoada (D-088)");
            DummyTarget inside = Dummy(6f);
            DummyTarget behind = Dummy(-5f);
            DummyTarget outside = Dummy(9f);
            float hp = health.Current;

            UseAndCheckCost(card);

            Assert.AreEqual(90f, inside.Taken, Tol, "Dentro dos 7 m");
            Assert.AreEqual(90f, behind.Taken, Tol, "Em volta, também atrás");
            Assert.AreEqual(0f, outside.Taken, Tol, "Fora dos 7 m");
            Assert.AreEqual(hp - 15f, health.Current, Tol, "Cobrou 15 de vida (a queda normal vale se zerar, D-003)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Estrela_SaltaEmOitoInimigos()
        {
            CardData card = GiveAndEquip("estrela_cristal", SlotType.Skill, 0);
            var chain = FirstEffect<ChainProjectileEffect>(card);
            Assert.AreEqual(25f, chain.damage, Tol);
            Assert.AreEqual(0.95f, chain.arcaneFraction, Tol);
            Assert.AreEqual(8, chain.jumps);
            Assert.AreEqual(6f, chain.jumpRange, Tol);
            var line = new List<DummyTarget>();
            for (int i = 0; i < 10; i++)
                line.Add(Dummy(3f + 1.5f * i));

            UseAndCheckCost(card);

            int hit = 0;
            foreach (DummyTarget dummy in line)
            {
                if (dummy.Taken > 0f)
                {
                    hit++;
                    Assert.AreEqual(25f, dummy.Taken, Tol, "Dano por salto");
                }
            }
            Assert.AreEqual(8, hit, "Oito saltos, nem mais nem menos");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CortesNovos_UsamOVisualQueAcompanhaAAbertura()
        {
            // O slash_arc é um visual novo no fim da lista da rede: a Lâmina Sedenta segue no slash_wide.
            Assert.GreaterOrEqual(CardVisuals.IndexOf("slash_arc"), 0, "slash_arc existe");
            Assert.Greater(CardVisuals.IndexOf("slash_arc"), CardVisuals.IndexOf("pulse"), "Só acrescentado no fim");
            Assert.AreEqual("slash_arc", FirstEffect<ArcSlashEffect>(Data("chicote_corrente")).visualId);
            Assert.AreEqual("slash_arc", FirstEffect<ArcSlashEffect>(Data("martelo_vapor")).visualId);
            Assert.AreEqual("slash_wide", FirstEffect<ArcSlashEffect>(Data("lamina_sedenta")).visualId);
            yield return null;
        }

        // ---------- Itens ----------

        [UnityTest]
        public IEnumerator VaporCondensado_CuraPoucoEDevolveMuitaEnergia()
        {
            CardData card = GiveAndEquip("vapor_condensado", SlotType.Belt, 0);
            var heal = FirstEffect<HealEffect>(card);
            Assert.AreEqual(15f, heal.heal, Tol);
            Assert.AreEqual(50f, heal.energy, Tol);
            DrainEnergy();
            health.ServerApplyDamage(new DamagePacket(50f, 0f), 0);
            float hp = health.Current;
            float energy = cards.Energy;

            cards.RequestUseBelt(0, AimForward());

            Assert.AreEqual(hp + 15f, health.Current, Tol, "Curou 15");
            Assert.AreEqual(energy + 50f, cards.Energy, Tol, "Devolveu 50 de energia");
            Assert.AreEqual(0, cards.BeltCount(0), "Item gasto");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaliceCheio_CuraMuito()
        {
            CardData card = GiveAndEquip("calice_cheio", SlotType.Belt, 0);
            var heal = FirstEffect<HealEffect>(card);
            Assert.AreEqual(70f, heal.heal, Tol);
            Assert.AreEqual(20f, heal.energy, Tol);
            DrainEnergy();
            health.ServerApplyDamage(new DamagePacket(80f, 0f), 0);
            float hp = health.Current;
            float energy = cards.Energy;

            cards.RequestUseBelt(0, AimForward());

            Assert.AreEqual(hp + 70f, health.Current, Tol, "Curou 70");
            Assert.AreEqual(energy + 20f, cards.Energy, Tol, "Devolveu 20 de energia");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TonicoFraco_CuraEDevolveUmPouco()
        {
            CardData card = GiveAndEquip("tonico_fraco", SlotType.Belt, 0);
            var heal = FirstEffect<HealEffect>(card);
            Assert.AreEqual(15f, heal.heal, Tol);
            Assert.AreEqual(15f, heal.energy, Tol);
            DrainEnergy();
            health.ServerApplyDamage(new DamagePacket(50f, 0f), 0);
            float hp = health.Current;
            float energy = cards.Energy;

            cards.RequestUseBelt(0, AimForward());

            Assert.AreEqual(hp + 15f, health.Current, Tol, "Curou 15");
            Assert.AreEqual(energy + 15f, cards.Energy, Tol, "Devolveu 15 de energia");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrascoDeFaisca_ArremessaEFereEmArea()
        {
            CardData card = GiveAndEquip("frasco_faisca", SlotType.Belt, 0);
            var throwArea = FirstEffect<ThrowAreaEffect>(card);
            Assert.AreEqual(25f, throwArea.damage, Tol);
            Assert.AreEqual(0.8f, throwArea.arcaneFraction, Tol);
            Assert.AreEqual(2f, throwArea.radius, Tol);
            Assert.AreEqual(9f, throwArea.maxRange, Tol);
            DummyTarget center = Dummy(6f);
            DummyTarget edge = Dummy(6f, 2.0f);   // 2,0 - raio 0,4 = 1,6: dentro dos 2 m
            DummyTarget outside = Dummy(6f, 3.5f);

            cards.RequestUseBelt(0, player.transform.position + Vector3.forward * 6f);
            Assert.AreEqual(0, cards.BeltCount(0), "Item gasto");
            Assert.AreEqual(0f, center.Taken, Tol, "Ainda no ar");
            yield return new WaitForSeconds(throwArea.flightTime + 0.3f);

            Assert.AreEqual(25f, center.Taken, Tol, "No centro");
            Assert.AreEqual(25f, edge.Taken, Tol, "Na borda do raio");
            Assert.AreEqual(0f, outside.Taken, Tol, "Fora do raio de 2 m");
        }

        // ---------- Passivas e equipamentos: modificadores medidos ----------

        [UnityTest]
        public IEnumerator PassoDePistao_AndaDezPorCentoMaisRapido()
        {
            float baseSpeed = motor.Settings.moveSpeed;
            motor.SetIntent(Vector3.forward, null);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(baseSpeed, motor.Velocity.magnitude, 0.05f, "Velocidade normal sem a carta");

            GiveAndEquip("passo_pistao", SlotType.Passive, 0);
            Assert.AreEqual(1.1f, cards.Modifiers.MoveSpeedMultiplier, 0.0001f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(baseSpeed * 1.1f, motor.Velocity.magnitude, 0.05f, "10% mais rápido com a carta");

            cards.RequestUnequip(SlotType.Passive, 0);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(baseSpeed, motor.Velocity.magnitude, 0.05f, "Volta ao normal sem a carta");
            motor.SetIntent(Vector3.zero, null);
        }

        [UnityTest]
        public IEnumerator CoracaoDeCaldeira_Soma30DeVidaMaximaMantendoAFracao()
        {
            float baseMax = health.Max;
            Assert.AreEqual(baseMax, health.Current, Tol, "Começa com a vida cheia");

            GiveAndEquip("coracao_caldeira", SlotType.Passive, 0);
            Assert.AreEqual(baseMax + 30f, health.Max, Tol, "Vida máxima +30");
            Assert.AreEqual(health.Max, health.Current, Tol, "Equipar com a vida cheia deixa a vida cheia");

            health.ServerApplyDamage(new DamagePacket(health.Max * 0.5f, 0f), 0);
            Assert.AreEqual(0.5f, health.Fraction, 0.001f);

            cards.RequestUnequip(SlotType.Passive, 0);
            Assert.AreEqual(baseMax, health.Max, Tol, "Voltou à vida máxima normal");
            Assert.AreEqual(0.5f, health.Fraction, 0.001f, "A fração da vida se mantém ao desequipar");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoracaoDeCaldeira_DesequiparComUmPontoDeVidaNaoMata()
        {
            GiveAndEquip("coracao_caldeira", SlotType.Passive, 0);
            health.ServerApplyDamage(new DamagePacket(health.Max - 1f, 0f), 0);
            Assert.AreEqual(1f, health.Current, Tol);

            cards.RequestUnequip(SlotType.Passive, 0);

            Assert.Greater(health.Current, 0f, "Continua vivo");
            Assert.IsTrue(health.IsAlive);
            Assert.IsFalse(player.GetComponent<PlayerLife>().IsDowned, "Não caiu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoroaDeRebites_CortaAVidaMaximaEDaMuitaEnergiaPorGolpe()
        {
            float baseMax = health.Max;
            var crown = GiveAndEquip("coroa_rebites", SlotType.Equipment, 0);
            Assert.IsTrue(crown.cursed);

            Assert.AreEqual(baseMax - 30f, health.Max, Tol, "Vida máxima -30");
            Assert.AreEqual(health.Max, health.Current, Tol, "Com a vida cheia continua cheia, sem morrer");

            DrainEnergy();
            float before = cards.Energy;
            cards.ServerOnBasicHit();
            Assert.AreEqual(cards.Settings.energyPerHit * 2.2f, cards.Energy - before, Tol, "+120% de energia por golpe");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoracaoECoroa_JuntosSeAnulam()
        {
            float baseMax = health.Max;
            GiveAndEquip("coracao_caldeira", SlotType.Passive, 0);
            GiveAndEquip("coroa_rebites", SlotType.Equipment, 0);

            Assert.AreEqual(baseMax, health.Max, Tol);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PavioCurto_RecarregaMaisRapidoEEnfraqueceOGolpe()
        {
            float baseDamage = combat.Settings.basicDamage;
            GiveAndEquip("pavio_curto", SlotType.Passive, 0);
            Assert.AreEqual(0.88f, cards.Modifiers.CooldownMultiplier, 0.0001f);

            CardData giro = GiveAndEquip("giro_engrenagem", SlotType.Skill, 0);
            UseAndCheckCost(giro);
            Assert.AreEqual(5f * 0.88f, cards.SkillCooldownDuration(0), Tol, "Recarga 12% menor");

            DummyTarget dummy = Dummy(1.5f);
            yield return Attack();
            Assert.AreEqual(baseDamage - 4f, dummy.Taken, Tol, "Golpe básico -4");
        }

        [UnityTest]
        public IEnumerator FornalhaFaminta_DaMuitaEnergiaPorGolpeEEnfraqueceOGolpe()
        {
            float baseDamage = combat.Settings.basicDamage;
            GiveAndEquip("fornalha_faminta", SlotType.Passive, 0);
            DrainEnergy();
            float before = cards.Energy;
            cards.ServerOnBasicHit();
            Assert.AreEqual(cards.Settings.energyPerHit * 1.9f, cards.Energy - before, Tol, "+90% de energia por golpe");

            DummyTarget dummy = Dummy(1.5f);
            yield return Attack();
            Assert.AreEqual(baseDamage - 6f, dummy.Taken, Tol, "Golpe básico -6");
        }

        [UnityTest]
        public IEnumerator LuvaDeCobre_GolpeMaisForteEComAlcanceMenor()
        {
            float range = combat.Settings.basicRange;
            float baseDamage = combat.Settings.basicDamage;
            DummyTarget edge = Dummy(range + 0.3f); // a borda do alvo (0,4) entra no alcance normal, não no reduzido

            yield return Attack();
            Assert.AreEqual(baseDamage, edge.Taken, Tol, "Sem a luva, alcança");
            edge.Taken = 0f;
            yield return WaitAttackCooldown();

            GiveAndEquip("luva_cobre", SlotType.Equipment, 0);
            yield return Attack();
            Assert.AreEqual(0f, edge.Taken, Tol, "Com a luva (-0,3 m) não alcança");

            DummyTarget close = Dummy(1.5f);
            yield return WaitAttackCooldown();
            yield return Attack();
            Assert.AreEqual(baseDamage + 10f, close.Taken, Tol, "Com a luva o golpe causa +10");
        }

        [UnityTest]
        public IEnumerator CristalDeFenda_SomaDanoEDesloca60PorCentoParaOArcano()
        {
            float baseDamage = combat.Settings.basicDamage;
            GiveAndEquip("cristal_fenda", SlotType.Equipment, 0);
            Assert.AreEqual(0.6f, cards.Modifiers.Get(ModifierKind.BasicArcaneShift), 0.0001f);
            DummyTarget dummy = Dummy(1.5f);

            yield return Attack();

            Assert.AreEqual(baseDamage + 4f, dummy.Taken, Tol, "Golpe básico +4 (o alvo não tem resistência)");
        }

        [UnityTest]
        public IEnumerator EngrenagemMestra_RecarregaMaisRapidoEGolpeMaisForte()
        {
            float baseDamage = combat.Settings.basicDamage;
            GiveAndEquip("engrenagem_mestra", SlotType.Equipment, 0);
            Assert.AreEqual(0.9f, cards.Modifiers.CooldownMultiplier, 0.0001f);

            CardData giro = GiveAndEquip("giro_engrenagem", SlotType.Skill, 0);
            UseAndCheckCost(giro);
            Assert.AreEqual(5f * 0.9f, cards.SkillCooldownDuration(0), Tol, "Recarga 10% menor");

            DummyTarget dummy = Dummy(1.5f);
            yield return Attack();
            Assert.AreEqual(baseDamage + 4f, dummy.Taken, Tol, "Golpe básico +4");
        }

        [UnityTest]
        public IEnumerator BracadeiraDeLatao_AlcanceMaiorEnergiaMaiorEGolpeMaisFraco()
        {
            float range = combat.Settings.basicRange;
            float baseDamage = combat.Settings.basicDamage;
            DummyTarget far = Dummy(range + 0.4f + 0.5f); // fora do alcance normal, dentro de +0,9 m

            yield return Attack();
            Assert.AreEqual(0f, far.Taken, Tol, "Sem a braçadeira não alcança");
            yield return WaitAttackCooldown();

            GiveAndEquip("bracadeira_latao", SlotType.Equipment, 0);
            yield return Attack();
            Assert.AreEqual(baseDamage - 3f, far.Taken, Tol, "Com a braçadeira (+0,9 m) alcança, com -3 de dano");

            DrainEnergy();
            float before = cards.Energy;
            cards.ServerOnBasicHit();
            Assert.AreEqual(cards.Settings.energyPerHit * 1.25f, cards.Energy - before, Tol, "+25% de energia por golpe");
        }

        [UnityTest]
        public IEnumerator AForca_SomaQuatorzeAoGolpe()
        {
            float baseDamage = combat.Settings.basicDamage;
            CardData card = GiveAndEquip("forca", SlotType.Passive, 0);
            Assert.AreEqual(Arcana.Major, card.arcana);
            DummyTarget dummy = Dummy(1.5f);

            yield return Attack();

            Assert.AreEqual(baseDamage + 14f, dummy.Taken, Tol);
        }

        [UnityTest]
        public IEnumerator PactoDeCristal_Golpe60PorCentoMaisForteECobraVidaUmaVezPorGolpe()
        {
            float baseDamage = combat.Settings.basicDamage;
            CardData card = GiveAndEquip("pacto_cristal", SlotType.Passive, 0);
            Assert.IsTrue(card.cursed);
            DummyTarget first = Dummy(1.5f);
            DummyTarget second = Dummy(1.5f, 0.6f); // dois alvos no mesmo golpe
            float hp = health.Current;

            yield return Attack();

            Assert.AreEqual(baseDamage * 1.6f, first.Taken, Tol, "+60% de dano no primeiro alvo");
            Assert.AreEqual(baseDamage * 1.6f, second.Taken, Tol, "+60% de dano no segundo alvo");
            Assert.AreEqual(hp - 3f, health.Current, Tol, "Custou 3 de vida, uma vez por golpe (não por alvo)");

            yield return WaitAttackCooldown();
            yield return Attack();
            Assert.AreEqual(hp - 6f, health.Current, Tol, "Cada golpe que acerta custa 3");
        }

        [UnityTest]
        public IEnumerator PactoDeCristal_GolpeQueErraNaoCustaVida()
        {
            GiveAndEquip("pacto_cristal", SlotType.Passive, 0);
            float hp = health.Current;

            yield return Attack(); // sem alvo

            Assert.AreEqual(hp, health.Current, Tol, "Errar não custa vida");
        }

        [UnityTest]
        public IEnumerator PactoDeCristal_CustoDeVidaNaoArmaAMolaDeRecuo()
        {
            float baseDamage = combat.Settings.basicDamage;
            GiveAndEquip("pacto_cristal", SlotType.Passive, 0);
            GiveAndEquip("mola_recuo", SlotType.Passive, 1);
            DummyTarget dummy = Dummy(1.5f);

            yield return Attack();
            Assert.AreEqual(baseDamage * 1.6f, dummy.Taken, Tol, "Primeiro golpe: só o pacto");
            Assert.AreEqual(0f, cards.ServerHurtBonus, 0.0001f, "O custo do próprio golpe não arma a Mola");
            float afterFirst = dummy.Taken;

            yield return WaitAttackCooldown();
            yield return Attack();
            Assert.AreEqual(baseDamage * 1.6f, dummy.Taken - afterFirst, Tol, "Segundo golpe sem reforço da Mola");
        }

        [UnityTest]
        public IEnumerator PactoDeCristal_VidaQueZeraDerrubaOJogadorNormalmente()
        {
            GiveAndEquip("pacto_cristal", SlotType.Passive, 0);
            DummyTarget dummy = Dummy(1.5f);
            health.ServerApplyDamage(new DamagePacket(health.Current - 2f, 0f), 0); // 2 de vida, o golpe custa 3

            yield return Attack();

            Assert.Greater(dummy.Taken, 0f, "O golpe saiu");
            Assert.IsFalse(health.IsAlive, "A vida zerou");
            Assert.IsTrue(player.GetComponent<PlayerLife>().IsDowned, "Valeu a queda normal (D-003)");
        }

        [UnityTest]
        public IEnumerator DanoDoGolpe_NuncaFicaAbaixoDeUmComTodasAsPenalidades()
        {
            GiveAndEquip("pavio_curto", SlotType.Passive, 0);          // -4
            GiveAndEquip("fornalha_faminta", SlotType.Passive, 1);     // -6
            GiveAndEquip("bracadeira_latao", SlotType.Equipment, 0);   // -3
            DummyTarget dummy = Dummy(1.5f);
            float expected = Mathf.Max(1f, combat.Settings.basicDamage - 13f);

            yield return Attack();

            Assert.AreEqual(expected, dummy.Taken, Tol, "Soma das penalidades, nunca abaixo de 1");
            Assert.GreaterOrEqual(dummy.Taken, 1f);
        }

        [UnityTest]
        public IEnumerator PassivasNovas_QualidadeEscalaOsNumeros()
        {
            // As cartas gastas (D-048) multiplicam os bônus: a vida máxima +30 vira +30 * multiplicador.
            int id = Give("coracao_caldeira");
            var grants = new List<CardGrant> { new CardGrant(id, GrantKind.Upgrade, CardQuality.Perfect) };
            cards.ServerApplyGrants(grants);
            float baseMax = health.Max;
            cards.RequestEquip(id, SlotType.Passive, 0);

            Assert.AreEqual(baseMax + 30f * cards.Settings.perfectMultiplier, health.Max, 0.05f);
            yield return null;
        }
    }
}
