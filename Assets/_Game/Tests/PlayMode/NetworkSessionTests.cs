using System.Collections;
using Game.Arena;
using Game.Cameras;
using Game.Net;
using Game.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>Sobe a arena como host no próprio teste e confere spawn, movimento e largada.</summary>
    public class NetworkSessionTests
    {
        private NetSession session;

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
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            yield return ArenaTestScene.Cleanup();
        }

        private static NetworkPlayer LocalPlayer() =>
            NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();

        [Test]
        public void Host_NasceNumaVagaDoSpawn()
        {
            var player = LocalPlayer();
            var spawn = Object.FindFirstObjectByType<PlayerSpawnPoints>();

            Assert.IsTrue(player.IsOwner);
            Assert.Less(Vector3.Distance(player.transform.position, spawn.Get(0).position), 0.5f);
        }

        [Test]
        public void Host_TemMarcaLocalECameraSegue()
        {
            var player = LocalPlayer();
            Assert.IsTrue(player.transform.Find("MarcadorLocal").gameObject.activeSelf, "Marca local ligada (D-010)");
            Assert.AreEqual(player.transform, Camera.main.GetComponent<CameraFollow>().Target);
        }

        [UnityTest]
        public IEnumerator Host_AndaComIntencao()
        {
            var player = LocalPlayer();
            player.GetComponent<PlayerInputReader>().enabled = false; // sem teclado no teste
            Vector3 start = player.transform.position;

            player.SubmitLocalIntent(Vector3.forward, null);
            yield return new WaitForSeconds(0.4f);
            player.SubmitLocalIntent(Vector3.zero, null);

            Assert.Greater(player.transform.position.z - start.z, 1f);
            Assert.AreEqual(0f, player.transform.position.y, 0.15f, "Continua no chão");
        }

        [UnityTest]
        public IEnumerator Alavanca_HostDaALargadaEOsPortoesAcordam()
        {
            var player = LocalPlayer();
            var lever = Object.FindFirstObjectByType<StartLever>();
            var matchState = Object.FindFirstObjectByType<MatchState>();
            Assert.IsFalse(matchState.IsStarted);

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = lever.transform.position + lever.transform.forward * 1.2f;
            controller.enabled = true;
            yield return null;

            player.RequestInteract();
            yield return null;
            yield return null;

            Assert.IsTrue(matchState.IsStarted, "Largada dada pelo host");
            foreach (var gate in Object.FindObjectsByType<GateActivation>(FindObjectsSortMode.None))
                Assert.IsTrue(gate.GetComponentInChildren<Spinner>().enabled, "Engrenagem do portão girando");
        }

        [UnityTest]
        public IEnumerator Input_DesligarLeitorDeOutroJogadorNaoDesligaODono()
        {
            var localReader = LocalPlayer().GetComponent<PlayerInputReader>();
            Assert.IsTrue(localReader.InputEnabled, "Leitor do dono ligado");

            // Instância fora da rede: o NetworkObject só nasce na rede se for spawnado.
            var other = Object.Instantiate(NetworkManager.Singleton.NetworkConfig.PlayerPrefab);
            other.GetComponent<PlayerInputReader>().enabled = false;
            yield return null;
            Assert.IsTrue(localReader.InputEnabled, "Desligar outro leitor não desliga o do dono");

            Object.Destroy(other);
            yield return null;
            Assert.IsTrue(localReader.InputEnabled, "Destruir o outro jogador não desliga o do dono");
        }

        [UnityTest]
        public IEnumerator Largada_ZeraAoHospedarDeNovo()
        {
            var matchState = Object.FindFirstObjectByType<MatchState>();
            matchState.ServerStart();
            Assert.IsTrue(matchState.IsStarted);

            session.Leave();
            float timeout = 5f;
            while ((NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ShutdownInProgress) && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(NetworkManager.Singleton.IsListening, "Sessão encerrou");

            Assert.IsTrue(session.Host(), "Host iniciou de novo");
            timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(NetworkManager.Singleton.LocalClient?.PlayerObject, "Jogador nasceu");

            Assert.IsFalse(matchState.IsStarted, "Nova sessão começa sem largada");
        }

        [UnityTest]
        public IEnumerator Alavanca_LongeNaoFazNada()
        {
            var player = LocalPlayer();
            var lever = Object.FindFirstObjectByType<StartLever>();
            var matchState = Object.FindFirstObjectByType<MatchState>();

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = lever.transform.position + lever.transform.forward * 4f;
            controller.enabled = true;
            yield return null;

            player.RequestInteract();
            yield return null;
            yield return null;

            Assert.IsFalse(matchState.IsStarted, "Fora do alcance não dá largada");
        }
    }
}
