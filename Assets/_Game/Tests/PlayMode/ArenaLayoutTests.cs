using System.Collections;
using System.Linq;
using Game.Arena;
using Game.Cameras;
using Game.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class ArenaLayoutTests
    {
        [UnitySetUp]
        public IEnumerator CarregaArena()
        {
            yield return ArenaTestScene.Load();
        }

        [UnityTearDown]
        public IEnumerator Limpa()
        {
            yield return ArenaTestScene.Cleanup();
        }

        [Test]
        public void Arena_TemAsAreasPedidas()
        {
            var markers = Object.FindObjectsByType<ArenaMarker>(FindObjectsSortMode.None);

            Assert.AreEqual(1, markers.Count(m => m.Kind == ArenaMarkerKind.PlayerSpawn), "Spawn dos jogadores");
            Assert.AreEqual(3, markers.Count(m => m.Kind == ArenaMarkerKind.EnemySpawn), "Spawns de inimigos");
            Assert.AreEqual(1, markers.Count(m => m.Kind == ArenaMarkerKind.CardTestArea), "Área de testes de cartas");
            Assert.AreEqual(1, markers.Count(m => m.Kind == ArenaMarkerKind.CombatCenter), "Centro de combate");
        }

        [Test]
        public void Arena_TemQuatroVagasDeSpawn()
        {
            var spawn = Object.FindFirstObjectByType<PlayerSpawnPoints>();
            Assert.IsNotNull(spawn);
            Assert.AreEqual(4, spawn.Count);
        }

        [Test]
        public void Arena_TemCameraConfiguradaESessaoDeRede()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            Assert.IsNotNull(follow, "A câmera principal tem CameraFollow");
            Assert.IsNotNull(follow.Settings);
            Assert.IsNotNull(Object.FindFirstObjectByType<NetSession>(), "Existe a sessão de rede");
            Assert.IsNotNull(Object.FindFirstObjectByType<StartLever>(), "Existe a alavanca de largada");
        }

        [UnityTest]
        public IEnumerator Arena_PortoesComecamParados()
        {
            yield return null;
            foreach (var spinner in Object.FindObjectsByType<Spinner>(FindObjectsSortMode.None))
            {
                if (spinner.GetComponentInParent<GateActivation>() != null)
                    Assert.IsFalse(spinner.enabled, "Engrenagem de portão parada antes da largada (D-013)");
            }
        }
    }
}
