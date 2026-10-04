using System.Collections;
using System.Linq;
using Game.Arena;
using Game.Cameras;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class ArenaLayoutTests
    {
        [UnitySetUp]
        public IEnumerator CarregaArena()
        {
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
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
        public void Arena_CameraSegueOJogador()
        {
            var player = Object.FindFirstObjectByType<PlayerMotor>();
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;

            Assert.IsNotNull(player, "Existe um jogador na arena");
            Assert.IsNotNull(follow, "A câmera principal tem CameraFollow");
            Assert.AreEqual(player.transform, follow.Target);
            Assert.IsNotNull(follow.Settings);
        }

        [UnityTest]
        public IEnumerator Arena_JogadorFicaNoChao()
        {
            var player = Object.FindFirstObjectByType<PlayerMotor>();
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(0f, player.transform.position.y, 0.15f);
        }
    }
}
