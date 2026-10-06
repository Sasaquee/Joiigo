using System;
using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Dice;
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
    /// <summary>
    /// Fase 8 — o ciclo completo do protótipo, de ponta a ponta no host:
    /// arena → largada → combate (golpe do jogador acerta) → fim da onda → carta no chão → D20 → carta entregue →
    /// equipar no espaço do tipo dela → usar.
    /// </summary>
    public class FullLoopTests
    {
        private GameObject player;
        private PlayerCards cards;
        private WaveSpawner spawner;
        private CardDropService drops;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            yield return WaitFor(() => NetworkManager.Singleton.LocalClient?.PlayerObject != null, 5f);
            player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            cards = player.GetComponent<PlayerCards>();
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            drops = Object.FindFirstObjectByType<CardDropService>();
            Assert.IsNotNull(spawner);
            Assert.IsNotNull(drops);
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            CardDropService.DebugForceNextRoll(null);
            yield return ArenaTestScene.Cleanup();
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            while (!condition() && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        private List<EnemyController> AliveEnemies()
        {
            var list = new List<EnemyController>();
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy.IsAlive)
                    list.Add(enemy);
            return list;
        }

        [UnityTest]
        public IEnumerator CicloCompleto_CombateCartaDadoEquiparUsar()
        {
            // 1. Largada: a primeira onda nasce.
            Object.FindFirstObjectByType<MatchState>().ServerStart();
            yield return WaitFor(() => spawner.AliveCount > 0, spawner.Settings.firstWaveDelay + 5f);
            Assert.Greater(spawner.AliveCount, 0, "A primeira onda nasceu");
            // Os inimigos saem pelas bocas numa fila com espaçamento (D-077): espera a onda inteira nascer.
            yield return WaitFor(() => spawner.PendingCount == 0, 3f);
            Assert.AreEqual(0, spawner.PendingCount, "A fila das bocas esvaziou");

            // 2. Combate: o golpe básico do jogador acerta um inimigo posto à frente dele.
            EnemyController target = spawner.ServerSpawn(spawner.Settings.enemyTypes[0],
                player.transform.position + Vector3.forward * 2.2f);
            Assert.IsNotNull(target);
            yield return null;
            float hpBefore = target.Health.Current;
            var combat = player.GetComponent<PlayerCombat>();
            combat.RequestAttack(player.transform.position + Vector3.forward * 8f);
            yield return WaitFor(() => target.Health.Current < hpBefore, combat.Settings.basicHitDelay + 1f);
            Assert.Less(target.Health.Current, hpBefore, "O golpe do jogador acertou");

            // 3. Fim da onda: o resto cai e uma carta aparece no chão, onde caiu o último inimigo (D-050, D-082).
            var fallen = new List<Vector3>();
            foreach (var enemy in AliveEnemies())
            {
                fallen.Add(enemy.transform.position);
                enemy.Health.ServerApplyDamage(new DamagePacket(enemy.Health.Max * 100f, 0f), NetworkManager.Singleton.LocalClientId);
            }
            FloorCard floorCard = null;
            yield return WaitFor(() => (floorCard = Object.FindFirstObjectByType<FloorCard>()) != null, 5f);
            Assert.IsNotNull(floorCard, "A carta apareceu no chão no fim da onda");
            float nearest = float.MaxValue;
            foreach (Vector3 p in fallen)
                nearest = Mathf.Min(nearest, new Vector2(p.x - floorCard.transform.position.x, p.z - floorCard.transform.position.z).magnitude);
            Assert.LessOrEqual(nearest, drops.Settings.dropNavSampleRadius + 0.1f, "A carta surgiu onde caiu um dos últimos inimigos (D-082)");

            // 4. Pegar: o D20 rola no host e a carta chega depois da rolagem.
            int before = cards.Inventory.Count;
            var owned = new HashSet<int>(cards.Inventory);
            CardDropService.DebugForceNextRoll(10);
            floorCard.ServerInteract(NetworkManager.Singleton.LocalClientId);
            yield return WaitFor(() => cards.Inventory.Count > before,
                drops.Settings.rollDuration + drops.Settings.grantDelay + 2f);
            Assert.AreEqual(10, drops.LastRoll, "O dado rolou o número do host");
            int cardId = -1;
            foreach (int id in cards.Inventory)
                if (!owned.Contains(id))
                    cardId = id;
            Assert.GreaterOrEqual(cardId, 0, "Uma carta nova chegou ao inventário");

            // 5. Equipar no espaço do tipo dela.
            CardData card = cards.Database.Get(cardId);
            SlotType slot = card.Kind switch
            {
                CardKind.Item => SlotType.Belt,
                CardKind.Passive => SlotType.Passive,
                CardKind.Equipment => SlotType.Equipment,
                _ => SlotType.Skill
            };
            cards.RequestEquip(cardId, slot, 0);
            yield return WaitFor(() => cards.GetSlot(slot, 0) == cardId, 2f);
            Assert.AreEqual(cardId, cards.GetSlot(slot, 0), $"{card.displayName} equipada em {slot}");

            // 6. Usar: skill e consumível se usam; passiva e equipamento valem só de estar equipados.
            cards.AddEnergy(10000f);
            yield return null;
            Vector3 aim = player.transform.position + player.transform.forward * 6f;
            if (slot == SlotType.Skill)
                Assert.IsTrue(cards.ServerUseSkill(0, aim), $"Usou a skill {card.displayName}");
            else if (slot == SlotType.Belt)
                Assert.IsTrue(cards.ServerUseBelt(0, aim), $"Usou o consumível {card.displayName}");
        }
    }
}
