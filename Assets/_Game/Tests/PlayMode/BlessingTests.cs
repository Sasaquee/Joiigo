using System.Collections;
using Game.Aura;
using Game.Cards;
using Game.Combat;
using Game.Core.Aura;
using Game.Core.Cards;
using Game.Dice;
using Game.Net;
using Game.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Bênção de dano do 20 do D20 no coop (D-085), de ponta a ponta no host. Só há um cliente real nos testes, então o mínimo de
    /// jogadores (blessingMinPlayers) vai para 1 e é restaurado no fim. O resultado vem forçado pela ferramenta de debug do serviço
    /// (CardDropService.DebugForceNextRoll), que só existe no editor.
    /// </summary>
    public class BlessingTests
    {
        private const float Tol = 0.01f;

        private NetworkPlayer player;
        private PlayerCombat combat;
        private PlayerCards cards;
        private PlayerBlessing blessing;
        private PlayerAura aura;
        private AuraVisual auraVisual;
        private CardDropService service;
        private DummyTarget dummy;
        private float savedDuration;
        private float savedMultiplier;
        private int savedMinPlayers;
        private int blessedCount;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(NetworkManager.Singleton.LocalClient?.PlayerObject, "Jogador nasceu");

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            player.GetComponent<PlayerInputReader>().enabled = false; // sem mouse no teste
            combat = player.GetComponent<PlayerCombat>();
            cards = player.GetComponent<PlayerCards>();
            blessing = player.GetComponent<PlayerBlessing>();
            aura = player.GetComponent<PlayerAura>();
            auraVisual = player.GetComponent<AuraVisual>();
            service = Object.FindFirstObjectByType<CardDropService>();
            Assert.IsNotNull(blessing, "Jogador tem o PlayerBlessing (reconstruir: Game > Setup > Construir Arena)");
            Assert.IsNotNull(service, "Cena tem o CardDropService");

            // Números do teste: 1 jogador basta e a bênção é curta. O asset é restaurado no TearDown.
            DiceSettings dice = service.Settings;
            savedDuration = dice.blessingDuration;
            savedMultiplier = dice.blessingDamageMultiplier;
            savedMinPlayers = dice.blessingMinPlayers;
            dice.blessingMinPlayers = 1;
            dice.blessingDuration = 2f;
            dice.blessingDamageMultiplier = 1.5f;

            blessedCount = 0;
            service.Blessed += OnBlessed;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            if (service != null)
            {
                service.Blessed -= OnBlessed;
                DiceSettings dice = service.Settings;
                dice.blessingDuration = savedDuration;
                dice.blessingDamageMultiplier = savedMultiplier;
                dice.blessingMinPlayers = savedMinPlayers;
            }
            CardDropService.DebugForceNextRoll(null);
            AuraPaletteSwitch.Set(AuraPalette.Normal);
            if (dummy != null)
                Object.Destroy(dummy.gameObject);
            yield return ArenaTestScene.Cleanup();
        }

        private void OnBlessed(int players) => blessedCount = players;

        private float WaitForGrant => service.Settings.rollDuration + service.Settings.grantDelay + 0.6f;

        private DummyTarget CreateDummy()
        {
            if (dummy != null)
                Object.Destroy(dummy.gameObject);
            var go = new GameObject("AlvoTeste");
            go.transform.position = player.transform.position + new Vector3(0f, 0f, 1.5f);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 1f, 0f);
            sphere.radius = 0.4f;
            dummy = go.AddComponent<DummyTarget>();
            return dummy;
        }

        private Vector3 AimForward() => player.transform.position + Vector3.forward * 5f;

        /// <summary>Um golpe básico no alvo novo; devolve o dano que ele recebeu.</summary>
        private IEnumerator BasicHit(System.Action<float> taken)
        {
            DummyTarget target = CreateDummy();
            yield return new WaitForSeconds(combat.Settings.basicCooldown + 0.05f); // recarga do golpe anterior
            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(combat.Settings.basicHitDelay + 0.2f);
            taken(target.Taken);
        }

        /// <summary>Equipa a Lâmina Sedenta (corte em arco, sem custo de energia) e usa; devolve o dano no alvo.</summary>
        private IEnumerator SlashHit(System.Action<float> taken)
        {
            int id = cards.FindCardId("lamina_sedenta");
            Assert.GreaterOrEqual(id, 0, "Carta lamina_sedenta existe no banco");
            if (cards.GetSlot(SlotType.Skill, 1) != id)
            {
                cards.ServerGiveCard(id);
                cards.RequestEquip(id, SlotType.Skill, 1);
            }
            Assert.AreEqual(id, cards.GetSlot(SlotType.Skill, 1), "Lâmina equipada");

            float timeout = 5f;
            while (cards.SkillCooldownFraction(1) > 0f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            DummyTarget target = CreateDummy();
            cards.RequestUseSkill(1, AimForward());
            yield return null;
            taken(target.Taken);
        }

        private IEnumerator PickUpWithRoll(int forced)
        {
            Vector3 near = cards.transform.position + Vector3.forward * 1.5f;
            FloorCard card = service.ServerSpawnFloorCard(new Vector3(near.x, 0f, near.z));
            Assert.IsNotNull(card, "A carta apareceu no chão");
            yield return null;

            CardDropService.DebugForceNextRoll(forced);
            card.ServerInteract(NetworkManager.Singleton.LocalClientId);
            yield return null;
        }

        private IEnumerator WaitUntilBlessed(float timeout)
        {
            while (!blessing.Active && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        // ---------- O 20 dá a bênção (só no coop) ----------

        [UnityTest]
        public IEnumerator Vinte_NoCoop_BencaoChegaDepoisDoDado_DanoSobeEVoltaAoExpirar()
        {
            float basicDamage = combat.Settings.basicDamage;
            float multiplier = service.Settings.blessingDamageMultiplier;

            float before = 0f;
            yield return BasicHit(t => before = t);
            Assert.AreEqual(basicDamage, before, Tol, "Sem bênção o golpe causa o dano básico");
            Assert.IsFalse(blessing.Active, "Sem 20 não há bênção");

            yield return PickUpWithRoll(20);
            Assert.IsFalse(blessing.Active, "O dado ainda rola: a bênção não pode entregar o 20 antes da revelação (D-047)");
            Assert.AreEqual(0, blessedCount);

            yield return WaitUntilBlessed(WaitForGrant + 1f);
            Assert.IsTrue(blessing.Active, "Depois do dado e da entrega da carta, a bênção vale (D-085)");
            Assert.AreEqual(1, blessedCount, "A bênção foi dada a todos os jogadores conectados (aqui, um)");
            Assert.AreEqual(multiplier, blessing.ServerMultiplier, Tol);
            Assert.Greater(blessing.ServerRemaining, 0f);

            float blessed = 0f;
            yield return BasicHit(t => blessed = t);
            Assert.AreEqual(basicDamage * multiplier, blessed, Tol, "Abençoado: o golpe causa o dano multiplicado");
            Assert.AreEqual(multiplier, blessed / before, Tol, "Mesmo alvo, mesmo golpe: só muda o multiplicador");

            yield return WaitSeconds(service.Settings.blessingDuration + 0.3f);
            Assert.IsFalse(blessing.Active, "Passou a duração: a bênção expira");
            Assert.AreEqual(1f, blessing.ServerMultiplier, Tol);

            float after = 0f;
            yield return BasicHit(t => after = t);
            Assert.AreEqual(basicDamage, after, Tol, "Expirou: o dano volta ao normal");
        }

        [UnityTest]
        public IEnumerator Vinte_ComMinimoDeDoisJogadoresESoUm_NaoDaBencao()
        {
            service.Settings.blessingMinPlayers = 2; // o padrão do jogo: coop de verdade

            yield return PickUpWithRoll(20);
            yield return new WaitForSeconds(WaitForGrant);

            Assert.AreEqual(20, service.LastRoll);
            Assert.IsFalse(blessing.Active, "No solo o 20 continua só a carta (D-048, D-085)");
            Assert.AreEqual(0, blessedCount);

            float dealt = 0f;
            yield return BasicHit(t => dealt = t);
            Assert.AreEqual(combat.Settings.basicDamage, dealt, Tol, "Sem bênção o dano não muda");

            bool hasMajor = false;
            foreach (int id in cards.Inventory)
                hasMajor |= cards.Database.Get(id).arcana == Arcana.Major;
            Assert.IsTrue(hasMajor, "O 20 no solo ainda entrega o arcano maior (D-048)");
        }

        [UnityTest]
        public IEnumerator OutroNumero_NaoDaBencao()
        {
            yield return PickUpWithRoll(19);
            yield return new WaitForSeconds(WaitForGrant);

            Assert.AreEqual(19, service.LastRoll);
            Assert.IsFalse(blessing.Active, "Só o 20 dá a bênção");
            Assert.AreEqual(0, blessedCount);
        }

        [UnityTest]
        public IEnumerator DoisVintes_RenovamAduracaoSemEmpilharOMultiplicador()
        {
            service.Settings.blessingDuration = 6f; // sobra para o segundo dado dentro da primeira bênção
            float multiplier = service.Settings.blessingDamageMultiplier;

            yield return PickUpWithRoll(20);
            yield return WaitUntilBlessed(WaitForGrant + 1f);
            Assert.IsTrue(blessing.Active, "Primeira bênção");
            float remainingFirst = blessing.ServerRemaining;

            yield return WaitSeconds(2f); // a bênção gasta parte do tempo antes da segunda
            float remainingBeforeSecond = blessing.ServerRemaining;
            Assert.Less(remainingBeforeSecond, remainingFirst, "O relógio anda");

            blessedCount = 0;
            yield return PickUpWithRoll(20);
            float timeout = WaitForGrant + 1f;
            while (blessedCount == 0 && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(1, blessedCount, "A segunda bênção foi dada");
            Assert.Greater(blessing.ServerRemaining, remainingBeforeSecond, "Renovou: a duração reiniciou");
            Assert.AreEqual(multiplier, blessing.ServerMultiplier, Tol, "Renovar não empilha o multiplicador");

            float dealt = 0f;
            yield return BasicHit(t => dealt = t);
            Assert.AreEqual(combat.Settings.basicDamage * multiplier, dealt, Tol, "Dois 20 seguidos continuam em um multiplicador só");
        }

        // ---------- Os caminhos de dano do jogador (host) ----------

        [UnityTest]
        public IEnumerator Bencao_SkillDeDanoTambemSobePeloMultiplicador()
        {
            float multiplier = service.Settings.blessingDamageMultiplier;

            float plain = 0f;
            yield return SlashHit(t => plain = t);
            Assert.Greater(plain, 0f, "O corte da Lâmina acertou o alvo");

            // A Lâmina recarrega em 3 s antes do segundo corte: a bênção do teste (2 s) acabaria antes do golpe.
            service.Settings.blessingDuration = 10f; // restaurado no TearDown
            blessing.ServerGrant(service.Settings.blessingDuration, multiplier);
            Assert.IsTrue(blessing.Active);

            float blessed = 0f;
            yield return SlashHit(t => blessed = t);
            Assert.AreEqual(plain * multiplier, blessed, Tol, "A skill de dano também multiplica (D-085)");
        }

        [UnityTest]
        public IEnumerator Bencao_MultiplicaJuntoComOReforcoDaMola()
        {
            // Mola de Recuo: depois de levar golpe, o próximo golpe básico causa mais. A bênção multiplica por cima, sem anular.
            int id = cards.FindCardId("mola_recuo");
            Assert.GreaterOrEqual(id, 0, "Carta mola_recuo existe no banco");
            cards.ServerGiveCard(id);
            cards.RequestEquip(id, SlotType.Passive, 0);
            Assert.AreEqual(id, cards.GetSlot(SlotType.Passive, 0), "Mola equipada");

            float bonus = cards.Modifiers.Get(ModifierKind.HurtNextHitBonus);
            Assert.Greater(bonus, 0f, "A Mola dá bônus ao golpe depois de levar dano");
            float multiplier = service.Settings.blessingDamageMultiplier;
            blessing.ServerGrant(service.Settings.blessingDuration, multiplier);

            player.GetComponent<NetworkHealth>().ServerApplyDamage(new Game.Core.Combat.DamagePacket(1f, 0f), 0);
            yield return null; // o aviso de dano chega pelo RPC
            Assert.IsTrue(cards.ServerHurtBonus > 0f, "O reforço da Mola está valendo");

            float dealt = 0f;
            yield return BasicHit(t => dealt = t);
            Assert.AreEqual(combat.Settings.basicDamage * (1f + bonus) * multiplier, dealt, Tol,
                "O reforço da Mola e a bênção multiplicam juntos");
        }

        // ---------- Aura dourada (D-085, D-063, D-065) ----------

        [UnityTest]
        public IEnumerator Bencao_AuraSinalizaDouradoEApagaQuandoExpira()
        {
            Assert.IsNotNull(aura, "Jogador tem a aura");
            Assert.IsNotNull(auraVisual, "Jogador tem o desenho da aura");
            yield return null;
            Assert.AreEqual(0, (int)(aura.Signals & AuraSignals.Blessing), "Sem bênção a aura não tem o sinal");
            Assert.IsFalse(auraVisual.BlessingVisible);

            yield return PickUpWithRoll(20);
            yield return WaitUntilBlessed(WaitForGrant + 1f);
            yield return null;
            yield return null;
            Assert.AreNotEqual(0, (int)(aura.Signals & AuraSignals.Blessing), "A aura sinaliza a bênção (sinal lido da rede)");
            Assert.IsTrue(auraVisual.BlessingVisible, "Anel dourado interno aparece (forma própria, D-063)");
            AssertColor(AuraPalettes.Gold(AuraPalette.Normal), aura.Current.Gold);

            AuraPaletteSwitch.Set(AuraPalette.Alternative);
            yield return null;
            yield return null;
            AssertColor(AuraPalettes.Gold(AuraPalette.Alternative), aura.Current.Gold);
            AuraPaletteSwitch.Set(AuraPalette.Normal);

            yield return WaitSeconds(service.Settings.blessingDuration + 0.4f);
            yield return null;
            Assert.IsFalse(blessing.Active);
            Assert.AreEqual(0, (int)(aura.Signals & AuraSignals.Blessing), "Expirou: a aura volta ao normal");
            Assert.IsFalse(auraVisual.BlessingVisible, "O anel dourado some");
        }

        [UnityTest]
        public IEnumerator Bencao_CaidoNaoMostraDouradoMasGuardaABencao()
        {
            blessing.ServerGrant(service.Settings.blessingDuration, service.Settings.blessingDamageMultiplier);
            player.GetComponent<NetworkHealth>().ServerApplyDamage(new Game.Core.Combat.DamagePacket(100000f, 0f), 0);
            float timeout = 2f;
            while (!player.GetComponent<PlayerLife>().IsDowned && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            yield return null;
            yield return null;

            Assert.IsTrue(blessing.Active, "A bênção vale também para quem está caído (D-085): volta junto com o jogador");
            Assert.AreEqual(AuraSignals.Downed, aura.Signals, "Caído: aura só brasa, sem dourado (D-063)");
            Assert.IsFalse(auraVisual.BlessingVisible);
        }

        private static void AssertColor(AuraColor expected, AuraColor actual)
        {
            Assert.AreEqual(expected.R, actual.R, 1e-3f);
            Assert.AreEqual(expected.G, actual.G, 1e-3f);
            Assert.AreEqual(expected.B, actual.B, 1e-3f);
        }
    }
}
