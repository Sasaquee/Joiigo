using System;
using System.Collections;
using Game.Cards;
using Game.Combat;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Enemies;
using Game.Net;
using Game.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>Uso das cartas com o host sozinho na Arena: custo, recarga, equipar, efeitos e modificadores.</summary>
    public class CardUseTests
    {
        private NetworkPlayer player;
        private NetworkHealth health;
        private PlayerCards cards;
        private WaveSpawner spawner;
        private DummyTarget dummy;

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
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(cards, "Jogador tem PlayerCards (reconstrua a arena)");
            Assert.IsNotNull(cards.Database, "PlayerCards tem o banco de cartas");
            Assert.IsNotNull(spawner, "Cena tem o WaveSpawner");

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
            if (dummy != null)
                Object.Destroy(dummy.gameObject);
            yield return ArenaTestScene.Cleanup();
        }

        private int Give(string cardId)
        {
            int id = cards.FindCardId(cardId);
            Assert.GreaterOrEqual(id, 0, $"Carta {cardId} existe no banco");
            cards.ServerGiveCard(id);
            return id;
        }

        private int GiveAndEquip(string cardId, SlotType slot, int index)
        {
            int id = Give(cardId);
            cards.RequestEquip(id, slot, index);
            Assert.AreEqual(id, cards.GetSlot(slot, index), $"{cardId} equipada");
            return id;
        }

        private Vector3 AimForward() => player.transform.position + Vector3.forward * 8f;

        private EnemyDefinition Def(int index) => spawner.Settings.enemyTypes[index];

        private static T FirstEffect<T>(CardData card) where T : CardEffect
        {
            foreach (var effect in card.effects)
                if (effect is T typed)
                    return typed;
            Assert.Fail($"{card.id} não tem efeito {typeof(T).Name}");
            return null;
        }

        [UnityTest]
        public IEnumerator Pistao_InvesteParaFrenteGastaEnergiaEIniciaRecarga()
        {
            int id = GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            CardData card = cards.Database.Get(id);
            var dash = FirstEffect<DashEffect>(card);
            Vector3 start = player.transform.position;
            float energyBefore = cards.Energy;
            Assert.AreEqual(0f, cards.SkillCooldownFraction(0), 0.001f, "Pronta antes de usar");

            cards.RequestUseSkill(0, AimForward());

            Vector3 moved = player.transform.position - start;
            Assert.AreEqual(dash.distance, moved.z, 0.4f, "Andou a distância da investida para a frente");
            Assert.AreEqual(0f, moved.x, 0.2f);
            Assert.AreEqual(energyBefore - card.energyCost, cards.Energy, 0.5f, "Gastou a energia da carta");
            Assert.Greater(cards.SkillCooldownFraction(0), 0.9f, "Recarga começou");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pistao_UsarDeNovoNaRecargaFalha()
        {
            GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            cards.RequestUseSkill(0, AimForward());
            float energyAfter = cards.Energy;
            Vector3 positionAfter = player.transform.position;

            cards.RequestUseSkill(0, AimForward());

            Assert.AreEqual(energyAfter, cards.Energy, 0.001f, "Não gastou de novo");
            Assert.Less(Vector3.Distance(positionAfter, player.transform.position), 0.01f, "Não investiu de novo");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Equipar_PassivaNoEspacoDeSkillERejeitada()
        {
            int passive = Give("caldeira_interna");

            cards.RequestEquip(passive, SlotType.Skill, 0);

            Assert.AreEqual(Loadout.Empty, cards.GetSlot(SlotType.Skill, 0), "Espaço de skill segue vazio");
            CollectionAssert.Contains(cards.Inventory, passive, "A carta continua no inventário");

            cards.RequestEquip(passive, SlotType.Passive, 0);
            Assert.AreEqual(passive, cards.GetSlot(SlotType.Passive, 0), "No espaço certo entra");
            CollectionAssert.DoesNotContain(cards.Inventory, passive);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cinto_TonicoEmpilhaCuraDevolveEnergiaEReduzAContagem()
        {
            // Gasta energia antes, para ver a devolução do tônico.
            GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            cards.RequestUseSkill(0, AimForward());
            float energyBefore = cards.Energy;

            int tonic = Give("tonico_oleo_luz");
            Give("tonico_oleo_luz");
            cards.RequestEquip(tonic, SlotType.Belt, 0);
            cards.RequestEquip(tonic, SlotType.Belt, 0);
            Assert.AreEqual(tonic, cards.GetSlot(SlotType.Belt, 0));
            Assert.AreEqual(2, cards.BeltCount(0), "As duas cópias empilham");
            CollectionAssert.DoesNotContain(cards.Inventory, tonic);

            var heal = FirstEffect<HealEffect>(cards.Database.Get(tonic));
            health.ServerApplyDamage(new DamagePacket(50f, 0f), 0);
            float hpBefore = health.Current;

            cards.RequestUseBelt(0, AimForward());

            Assert.AreEqual(hpBefore + heal.heal, health.Current, 0.01f, "Curou parte da vida");
            Assert.Greater(cards.Energy, energyBefore + 1f, "Devolveu energia");
            Assert.AreEqual(1, cards.BeltCount(0), "Sobrou uma cópia");

            cards.RequestUseBelt(0, AimForward());
            Assert.AreEqual(0, cards.BeltCount(0));
            Assert.AreEqual(Loadout.Empty, cards.GetSlot(SlotType.Belt, 0), "Espaço esvaziou");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lente_SomaOModificadorDeMisturaArcana()
        {
            Assert.AreEqual(0f, cards.Modifiers.Get(ModifierKind.BasicArcaneShift), 0.0001f);

            GiveAndEquip("lente_prismatica", SlotType.Equipment, 0);
            Assert.AreEqual(0.35f, cards.Modifiers.Get(ModifierKind.BasicArcaneShift), 0.0001f);

            cards.RequestUnequip(SlotType.Equipment, 0);
            Assert.AreEqual(0f, cards.Modifiers.Get(ModifierKind.BasicArcaneShift), 0.0001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Golpe_ComLenteCausaMenosDanoNoConstructo()
        {
            var constructo = spawner.ServerSpawn(Def(2), player.transform.position + Vector3.forward * 2.2f);
            Assert.IsNotNull(constructo);
            yield return null;
            var combat = player.GetComponent<PlayerCombat>();
            float hp0 = constructo.Health.Current;

            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(combat.Settings.basicHitDelay + 0.1f);
            float plain = hp0 - constructo.Health.Current;
            Assert.Greater(plain, 0f, "O primeiro golpe acertou o constructo");

            yield return new WaitForSeconds(combat.Settings.basicCooldown + 0.1f);
            GiveAndEquip("lente_prismatica", SlotType.Equipment, 0);
            float hp1 = constructo.Health.Current;
            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(combat.Settings.basicHitDelay + 0.1f);
            float withLens = hp1 - constructo.Health.Current;

            Assert.Greater(withLens, 0f, "O segundo golpe acertou");
            Assert.Less(withLens, plain, "A parte arcana maior é cortada pelo constructo");
        }

        [UnityTest]
        public IEnumerator Golpe_AcertarGeraEnergia()
        {
            GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            cards.RequestUseSkill(0, AimForward()); // abre espaço na energia
            float before = cards.Energy;

            var go = new GameObject("AlvoTeste");
            go.transform.position = player.transform.position + Vector3.forward * 1.5f;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 1f, 0f);
            sphere.radius = 0.4f;
            dummy = go.AddComponent<DummyTarget>();

            var combat = player.GetComponent<PlayerCombat>();
            combat.RequestAttack(player.transform.position + Vector3.forward * 5f);
            yield return new WaitForSeconds(combat.Settings.basicHitDelay + 0.1f);

            Assert.Greater(dummy.Taken, 0f, "O golpe acertou");
            Assert.GreaterOrEqual(cards.Energy, before + cards.Settings.energyPerHit - 0.01f, "Golpe que acerta gera energia");
        }

        [UnityTest]
        public IEnumerator Lamina_CobraVidaAoUsarESemCustoDeEnergia()
        {
            int id = GiveAndEquip("lamina_sedenta", SlotType.Skill, 1);
            CardData card = cards.Database.Get(id);
            Assert.IsTrue(card.cursed);
            float hp = health.Current;
            float energy = cards.Energy;

            cards.RequestUseSkill(1, AimForward());

            Assert.AreEqual(hp - card.healthCostOnUse, health.Current, 0.01f, "Cobrou vida");
            Assert.AreEqual(energy, cards.Energy, 0.01f, "Sem custo de energia");
            Assert.Greater(cards.SkillCooldownFraction(1), 0.9f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Caminho_RegistraAsTagsDaCartaUsada()
        {
            GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            Assert.IsFalse(cards.Path.Counts.ContainsKey("engrenagem"));

            cards.RequestUseSkill(0, AimForward());

            Assert.AreEqual(1, cards.Path.Counts["engrenagem"]);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArcoVoltaico_FereOInimigoNaFrente()
        {
            int id = GiveAndEquip("arco_voltaico", SlotType.Skill, 2);
            var chain = FirstEffect<ChainProjectileEffect>(cards.Database.Get(id));
            var enemy = spawner.ServerSpawn(Def(0), player.transform.position + Vector3.forward * 6f);
            Assert.IsNotNull(enemy);
            yield return null;
            yield return null;
            float hp = enemy.Health.Current;

            Assert.IsTrue(cards.ServerUseSkill(2, enemy.transform.position), "A skill foi usada");

            Assert.AreEqual(hp - chain.damage, enemy.Health.Current, 0.5f, "Descarga feriu o inimigo");
        }

        [UnityTest]
        public IEnumerator Pistao_FereQuemEstaNoCaminho()
        {
            int id = GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            var dash = FirstEffect<DashEffect>(cards.Database.Get(id));
            var enemy = spawner.ServerSpawn(Def(0), player.transform.position + Vector3.forward * 3f);
            Assert.IsNotNull(enemy);
            yield return null;
            yield return null;
            float hp = enemy.Health.Current;

            Assert.IsTrue(cards.ServerUseSkill(0, AimForward()));

            Assert.AreEqual(hp - dash.damage, enemy.Health.Current, 0.5f, "A investida feriu o inimigo");
        }

        [UnityTest]
        public IEnumerator Broquel_BloqueiaPelaFrenteENaoPorTras()
        {
            GiveAndEquip("broquel_cantante", SlotType.Skill, 3);
            cards.RequestUseSkill(3, AimForward());

            var shield = player.GetComponent<PlayerShield>();
            Assert.IsNotNull(shield);
            Assert.IsTrue(shield.IsActive, "Escudo ligado");

            Vector3 p = player.transform.position;
            Vector3 fwd = player.transform.forward;
            Assert.IsFalse(PlayerShield.TryBlock(p - fwd * 1f, 0.35f), "Por trás passa");
            Assert.IsTrue(PlayerShield.TryBlock(p + fwd * 1f, 0.35f), "Pela frente bloqueia");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mina_ExplodeQuandoUmInimigoChegaPerto()
        {
            int id = GiveAndEquip("mina_engrenagem", SlotType.Skill, 3);
            var mineEffect = FirstEffect<PlaceMineEffect>(cards.Database.Get(id));

            Assert.IsTrue(cards.ServerUseSkill(3, AimForward()));
            Assert.IsNotNull(Object.FindFirstObjectByType<Mine>(), "A mina nasceu");

            var enemy = spawner.ServerSpawn(Def(0),
                player.transform.position + Vector3.forward * (mineEffect.placeDistance + 0.8f));
            Assert.IsNotNull(enemy);
            float max = enemy.Health.Max;

            float timeout = 2f;
            while (enemy.Health.Current >= max && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.LessOrEqual(enemy.Health.Current, max - mineEffect.damage + 0.5f, "A mina feriu o inimigo");
        }

        [UnityTest]
        public IEnumerator Caido_NaoUsaCarta()
        {
            GiveAndEquip("pistao_runico", SlotType.Skill, 0);
            health.ServerApplyDamage(new DamagePacket(100000f, 0f), 0);
            Vector3 position = player.transform.position;
            float energy = cards.Energy;

            Assert.IsFalse(cards.ServerUseSkill(0, AimForward()));

            Assert.AreEqual(energy, cards.Energy, 0.001f);
            Assert.Less(Vector3.Distance(position, player.transform.position), 0.01f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Changed_DisparaQuandoAsCartasMudam()
        {
            yield return null; // deixa passar o aviso inicial do nascimento
            int count = 0;
            Action handler = () => count++;
            cards.Changed += handler;

            Give("pistao_runico");
            yield return null;
            cards.Changed -= handler;

            Assert.GreaterOrEqual(count, 1, "Mudar o inventário avisa a UI");
        }
    }
}
