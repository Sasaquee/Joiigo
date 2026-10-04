using System;
using System.Collections;
using Game.Combat;
using Game.Core.Combat;
using Game.Enemies;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>Teste de fumaça do combate (§5): sobe a arena, inicia o host, gera inimigos e confere dano, morte e ondas.</summary>
    public class EnemyCombatSmokeTests
    {
        private NetSession session;
        private WaveSpawner spawner;
        private NetworkPlayer player;
        private NetworkHealth playerHealth;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            session = Object.FindFirstObjectByType<NetSession>();
            Assert.IsTrue(session.Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            playerHealth = player.GetComponent<NetworkHealth>();
            Assert.IsNotNull(playerHealth, "Jogador tem NetworkHealth");
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Cena tem o WaveSpawner");

            // Campo aberto no centro da arena, sem alavanca nem poste por perto.
            MovePlayer(Vector3.zero);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            yield return ArenaTestScene.Cleanup();
        }

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        private EnemyDefinition Def(int index) => spawner.Settings.enemyTypes[index];

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            while (!condition() && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static int BodyCount() => Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator Automato_MorreDesmontaESomeDepoisDoTempoDosRestos()
        {
            var def = Object.Instantiate(Def(0));
            def.debrisLifetime = 0.5f;
            int baselineBodies = BodyCount();

            var enemy = spawner.ServerSpawn(def, new Vector3(0f, 0f, 10f));
            Assert.IsNotNull(enemy, "Inimigo nasceu");
            yield return null;

            var health = enemy.GetComponent<NetworkHealth>();
            Assert.AreEqual(def.maxHealth, health.Max, 0.01f, "Vida vem da definição");

            bool depleted = false;
            health.Depleted += () => depleted = true;
            ulong host = NetworkManager.ServerClientId;
            for (int i = 0; i < 20 && health.IsAlive; i++)
                health.ServerApplyDamage(new DamagePacket(25f, 0f), host);

            Assert.IsTrue(depleted, "Vida zerou");
            Assert.AreEqual(EnemyPhase.Dead, enemy.Phase, "Fase publicada: morto");
            Assert.IsFalse(enemy.IsAlive);

            yield return null;
            Assert.Greater(BodyCount(), baselineBodies, "Peças soltas com física");

            yield return WaitFor(() => enemy == null, 0.5f + 3f);
            Assert.IsTrue(enemy == null, "Objeto de rede sumiu depois do tempo dos restos");

            yield return WaitFor(() => BodyCount() <= baselineBodies, 2f);
            Assert.LessOrEqual(BodyCount(), baselineBodies, "Restos destruídos");
        }

        [UnityTest]
        public IEnumerator Automato_AcertaOJogadorDepoisDoAviso()
        {
            Assert.AreEqual(playerHealth.Max, playerHealth.Current, 0.01f, "Jogador começa com vida cheia");

            Vector3 at = player.transform.position + Vector3.forward * 1.2f;
            var enemy = spawner.ServerSpawn(Def(0), at);
            Assert.IsNotNull(enemy);

            yield return WaitFor(() => playerHealth.Current < playerHealth.Max, Def(0).windupTime + 2f);
            Assert.Less(playerHealth.Current, playerHealth.Max, "Golpe do autômato tirou vida do jogador");
        }

        [UnityTest]
        public IEnumerator Drone_AtiraOrbeQueAcertaOJogador()
        {
            Vector3 at = player.transform.position + Vector3.forward * 7f;
            var enemy = spawner.ServerSpawn(Def(1), at);
            Assert.IsNotNull(enemy);

            yield return WaitFor(() => playerHealth.Current < playerHealth.Max, 6f);
            Assert.Less(playerHealth.Current, playerHealth.Max, "Orbe do drone acertou o jogador");
        }

        [UnityTest]
        public IEnumerator Constructo_ResisteAMaisDanoArcanoQueOAutomato()
        {
            var automato = spawner.ServerSpawn(Def(0), new Vector3(8f, 0f, 8f));
            var constructo = spawner.ServerSpawn(Def(2), new Vector3(-8f, 0f, 8f));
            yield return null;

            var packet = new DamagePacket(20f, 1f);
            ulong host = NetworkManager.ServerClientId;
            float onAutomato = automato.GetComponent<NetworkHealth>().ServerApplyDamage(packet, host);
            float onConstructo = constructo.GetComponent<NetworkHealth>().ServerApplyDamage(packet, host);

            Assert.AreEqual(20f, onAutomato, 0.01f);
            Assert.Less(onConstructo, onAutomato, "Constructo corta a parte arcana");
            Assert.AreEqual(20f * (1f - Def(2).resistances.arcane), onConstructo, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ondas_PrimeiraOndaNasceDepoisDaLargada()
        {
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, spawner.AliveCount, "Sem largada não nasce ninguém");

            Object.FindFirstObjectByType<MatchState>().ServerStart();
            int first = 0;
            foreach (int c in spawner.Settings.waves[0].counts)
                first += c;

            yield return WaitFor(() => spawner.AliveCount >= first, spawner.Settings.firstWaveDelay + 3f);
            Assert.AreEqual(first, spawner.AliveCount, "Primeira onda completa");
        }
    }
}
