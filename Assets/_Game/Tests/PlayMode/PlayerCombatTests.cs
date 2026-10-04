using System.Collections;
using Game.Combat;
using Game.Core.Combat;
using Game.Net;
using Game.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>Golpe em arco e queda do jogador, com o host sozinho na Arena.</summary>
    public class PlayerCombatTests
    {
        private NetworkPlayer player;
        private PlayerCombat combat;
        private PlayerLife life;
        private NetworkHealth health;
        private CombatSettings settings;
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
            combat = player.GetComponent<PlayerCombat>();
            life = player.GetComponent<PlayerLife>();
            health = player.GetComponent<NetworkHealth>();
            Assert.IsNotNull(combat, "Jogador tem PlayerCombat");
            Assert.IsNotNull(life, "Jogador tem PlayerLife");
            Assert.IsNotNull(health, "Jogador tem NetworkHealth");

            // Números do teste, independentes do asset: dano 20, alcance 2,2, recarga 0,45, atraso 0,1.
            settings = ScriptableObject.CreateInstance<CombatSettings>();
            settings.downedDuration = 0.3f;
            combat.Settings = settings;
            life.Settings = settings;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            if (dummy != null)
                Object.Destroy(dummy.gameObject);
            Object.Destroy(settings);
            yield return ArenaTestScene.Cleanup();
        }

        private DummyTarget CreateDummy(Vector3 offset)
        {
            var go = new GameObject("AlvoTeste");
            go.transform.position = player.transform.position + offset;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 1f, 0f);
            sphere.radius = 0.4f;
            dummy = go.AddComponent<DummyTarget>();
            return dummy;
        }

        private Vector3 AimForward() => player.transform.position + Vector3.forward * 5f;

        [UnityTest]
        public IEnumerator Golpe_AlvoNaFrenteRecebeODanoBasico()
        {
            var target = CreateDummy(new Vector3(0f, 0f, 1.5f));

            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(settings.basicHitDelay + 0.2f);

            Assert.AreEqual(settings.basicDamage, target.Taken, 0.001f);
        }

        [UnityTest]
        public IEnumerator Golpe_AlvoAtrasNaoRecebeDano()
        {
            var target = CreateDummy(new Vector3(0f, 0f, -1.5f));

            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(settings.basicHitDelay + 0.2f);

            Assert.AreEqual(0f, target.Taken, 0.001f);
        }

        [UnityTest]
        public IEnumerator Golpe_AlvoForaDoAlcanceNaoRecebeDano()
        {
            var target = CreateDummy(new Vector3(0f, 0f, settings.basicRange + 1.5f));

            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(settings.basicHitDelay + 0.2f);

            Assert.AreEqual(0f, target.Taken, 0.001f);
        }

        [UnityTest]
        public IEnumerator Golpe_RecargaImpedeDoisGolpesSeguidos()
        {
            var target = CreateDummy(new Vector3(0f, 0f, 1.5f));

            combat.RequestAttack(AimForward());
            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(settings.basicHitDelay + 0.2f);

            Assert.AreEqual(settings.basicDamage, target.Taken, 0.001f, "Só o primeiro golpe valeu");
        }

        [UnityTest]
        public IEnumerator Golpe_MiraInvalidaEIgnorada()
        {
            var target = CreateDummy(new Vector3(0f, 0f, 1.5f));

            combat.RequestAttack(new Vector3(float.NaN, 0f, 1f));
            yield return new WaitForSeconds(settings.basicHitDelay + 0.2f);

            Assert.AreEqual(0f, target.Taken, 0.001f);
        }

        [UnityTest]
        public IEnumerator Golpe_NaoAcertaOProprioJogador()
        {
            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(settings.basicHitDelay + 0.2f);

            Assert.AreEqual(health.Max, health.Current, 0.001f);
        }

        [UnityTest]
        public IEnumerator Queda_CaidoNaoAndaNemAtacaEVoltaNoSpawn()
        {
            Vector3 spawn = player.transform.position;

            // Afasta um pouco do spawn para provar a volta.
            player.SubmitLocalIntent(Vector3.forward, null);
            yield return new WaitForSeconds(0.3f);
            player.SubmitLocalIntent(Vector3.zero, null);
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(Vector3.Distance(player.transform.position, spawn), 1f, "Saiu do spawn");

            health.ServerApplyDamage(new DamagePacket(100000f, 0f), 0);
            Assert.IsTrue(life.IsDowned, "Caiu com HP zero");
            Assert.IsFalse(health.IsAlive);
            Assert.IsFalse(life.CanAct);

            // Caído: a intenção de movimento é ignorada.
            Vector3 downedPos = player.transform.position;
            player.SubmitLocalIntent(Vector3.forward, null);
            yield return new WaitForSeconds(0.1f);
            Assert.Less(Vector3.Distance(player.transform.position, downedPos), 0.05f, "Caído não anda");

            // Caído: não ataca.
            var target = CreateDummy(Vector3.forward * 1.5f);
            combat.RequestAttack(AimForward());
            yield return new WaitForSeconds(settings.basicHitDelay + 0.05f);
            Assert.AreEqual(0f, target.Taken, 0.001f, "Caído não ataca");

            // Passado o tempo caído, volta no spawn com a vida cheia.
            float timeout = 3f;
            while (life.IsDowned && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;

            Assert.IsFalse(life.IsDowned, "Levantou");
            Assert.IsTrue(life.CanAct);
            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(health.Max, health.Current, 0.001f, "Vida cheia");
            Vector3 flat = player.transform.position - spawn;
            flat.y = 0f;
            Assert.Less(flat.magnitude, 0.5f, "Voltou no spawn");
        }
    }
}
