using System.Collections;
using Game.Cards;
using Game.Core.Cards;
using Game.Dice;
using Game.Enemies;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Fase 6 — carta no chão e D20, de ponta a ponta no host: a carta aparece, o F (ServerInteract) rola o dado no host,
    /// todos recebem a rolagem (RollStarted) e as cartas chegam depois da rolagem. O resultado é forçado pela
    /// ferramenta de debug do serviço (só existe no editor), o que deixa o teste determinístico.
    /// </summary>
    public class CardDropTests
    {
        private PlayerCards cards;
        private CardDropService service;
        private WaveSpawner spawner;
        private int rolledSeen;

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
            Assert.IsNotNull(NetworkManager.Singleton.LocalClient?.PlayerObject, "Jogador nasceu");
            cards = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerCards>();
            service = Object.FindFirstObjectByType<CardDropService>();
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(service, "Cena tem o CardDropService (reconstruir: Game > Setup > Construir Arena)");
            Assert.IsNotNull(Object.FindFirstObjectByType<Game.UI.DiceRollUi>(), "Cena tem a tela do dado");

            rolledSeen = 0;
            CardDropService.RollStarted += OnRoll;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            CardDropService.RollStarted -= OnRoll;
            CardDropService.DebugForceNextRoll(null);
            yield return ArenaTestScene.Cleanup();
        }

        private void OnRoll(ulong roller, int result, float duration) => rolledSeen = result;

        private float WaitForGrant => service.Settings.rollDuration + service.Settings.grantDelay + 0.6f;

        private IEnumerator PickUpWithRoll(int forced)
        {
            Vector3 near = cards.transform.position + Vector3.forward * 1.5f;
            FloorCard card = service.ServerSpawnFloorCard(new Vector3(near.x, 0f, near.z));
            Assert.IsNotNull(card, "A carta apareceu no chão");
            yield return null;

            CardDropService.DebugForceNextRoll(forced);
            card.ServerInteract(NetworkManager.Singleton.LocalClientId);
            yield return null;
            Assert.AreEqual(forced, rolledSeen, "Todos recebem a rolagem com o número do host");
            Assert.IsFalse(card != null && card.IsSpawned, "A carta some do chão ao ser pega");
        }

        [UnityTest]
        public IEnumerator CartaNoChao_PegarRolaODadoEEntregaDepoisDaRolagem()
        {
            int before = cards.Inventory.Count;
            yield return PickUpWithRoll(10);
            Assert.AreEqual(before, cards.Inventory.Count, "A carta só chega depois que o dado para");
            yield return new WaitForSeconds(WaitForGrant);
            Assert.AreEqual(before + 1, cards.Inventory.Count, "10 entrega uma carta");
            Assert.AreEqual(10, service.LastRoll);
        }

        [UnityTest]
        public IEnumerator CartaEntregue_TocaARevelacao()
        {
            // Bug: a revelação só existia se a Arena fosse a primeira cena do jogo; recarregada, não aparecia.
            var reveal = Object.FindFirstObjectByType<Game.UI.CardRevealFx>();
            Assert.IsNotNull(reveal, "A Arena carregada tem a revelação de carta");
            yield return PickUpWithRoll(10);
            float timeout = WaitForGrant + 1f;
            while (reveal.ActiveCount == 0 && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.Greater(reveal.ActiveCount, 0, "A carta entregue pelo dado é revelada na tela");
        }

        [UnityTest]
        public IEnumerator Vinte_EntregaArcanoMaior()
        {
            yield return PickUpWithRoll(20);
            yield return new WaitForSeconds(WaitForGrant);
            bool hasMajor = false;
            foreach (int id in cards.Inventory)
                hasMajor |= cards.Database.Get(id).arcana == Arcana.Major;
            Assert.IsTrue(hasMajor, "20 entrega um arcano maior (D-048)");
        }

        [UnityTest]
        public IEnumerator Um_EntregaAmaldicoadaEChamaEmboscada()
        {
            yield return PickUpWithRoll(1);
            yield return new WaitForSeconds(WaitForGrant);

            bool hasCursed = false;
            foreach (int id in cards.Inventory)
                hasCursed |= cards.Database.Get(id).cursed;
            Assert.IsTrue(hasCursed, "1 entrega a carta amaldiçoada");

            int near = 0;
            float reach = service.Settings.ambushRadius + 1.5f;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy.IsAlive && Vector3.Distance(enemy.transform.position, cards.transform.position) <= reach)
                    near++;
            Assert.GreaterOrEqual(near, service.Settings.ambushMin, "Emboscada: inimigos surgem em volta do jogador (D-049)");
        }

        [UnityTest]
        public IEnumerator Repetida_MelhoraAQualidadeDaQueJaTem()
        {
            // Dá todas as comuns como gastas: qualquer comum que o dado entregar será repetida.
            var grants = new System.Collections.Generic.List<CardGrant>();
            for (int id = 0; id < cards.Database.Count; id++)
            {
                var data = cards.Database.Get(id);
                if (data.rarity == Rarity.Common && !data.cursed && data.arcana == Arcana.Minor)
                    grants.Add(new CardGrant(id, GrantKind.New, CardQuality.Worn));
            }
            cards.ServerApplyGrants(grants);
            yield return null;
            int countBefore = cards.Inventory.Count;

            yield return PickUpWithRoll(3); // 2–6: comum gasta
            yield return new WaitForSeconds(WaitForGrant);

            int upgraded = 0;
            foreach (var g in grants)
                if (cards.QualityOf(g.CardId) == CardQuality.Good)
                    upgraded++;
            Assert.AreEqual(1, upgraded, "Uma das comuns subiu de gasta para boa (D-051)");
            Assert.AreEqual(countBefore, cards.Inventory.Count, "Melhoria não cria carta nova");
        }
    }
}
