using System.Collections;
using Game.Arena;
using Game.Cameras;
using Game.Core.Map;
using Game.Enemies;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Capturas do passe do mapa em Play (D-073 a D-082): a avenida com o jogador, a translucidez do prédio que tapa o jogador
    /// (recorte com fantasma, D-079) e os trilhos de cristal acesos depois da largada. Só tira fotos; roda só quando pedido:
    /// -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.PasseMapaCapturas (pasta: variável JOIIGO_CAPTURAS).
    /// </summary>
    [Explicit("Só tira capturas; rodar quando for conferir o visual.")]
    public class PasseMapaCapturas
    {
        private string dir;
        private GameObject player;
        private SeeThroughDriver driver;
        private MapLayout layout;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            dir = PlayCapture.Folder("passe-mapa");
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            driver = Camera.main.GetComponent<SeeThroughDriver>();
            Assert.IsNotNull(driver, "A câmera tem o SeeThroughDriver");
            layout = new MapLayout();
        }

        [UnityTearDown]
        public IEnumerator Encerra() => ArenaTestScene.Cleanup();

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator CapturaAvenidaTranslucidezETrilhos()
        {
            // 1) Jogador no meio da avenida norte.
            MovePlayer(new Vector3(0f, 0f, 33f));
            yield return Wait(1f);
            yield return PlayCapture.Shot(dir, "p_01_avenida_norte");

            // 2) Procura um ponto andável onde um prédio tapa o jogador da câmera (grade de 2 m) e fotografa.
            Vector3 hidden = default;
            bool found = false;
            for (float x = -50f; x <= 50f && !found; x += 2f)
            {
                for (float z = -20f; z <= 55f && !found; z += 2f)
                {
                    if (!layout.IsWalkable(x, z, 0.9f))
                        continue;
                    MovePlayer(new Vector3(x, 0f, z));
                    yield return null;
                    yield return null;
                    if (driver.IsOccluded(player.transform))
                    {
                        hidden = new Vector3(x, 0f, z);
                        found = true;
                    }
                }
            }
            Assert.IsTrue(found, "Existe um ponto andável onde um prédio tapa o jogador");
            Debug.Log($"[Captura] translucidez em {hidden}");
            yield return Wait(0.8f); // o buraco abre em ~0,12 s
            yield return PlayCapture.Shot(dir, "p_02_predio_translucido");

            // 3) Largada: os trilhos de cristal e os portões acendem. Foto na boca norte e no meio da avenida.
            var match = Object.FindFirstObjectByType<MatchState>();
            if (match != null && match.IsSpawned)
                match.ServerStart();
            MovePlayer(new Vector3(0f, 0f, 33f));
            yield return Wait(2f);
            yield return PlayCapture.Shot(dir, "p_03_trilhos_acesos");

            MovePlayer(new Vector3(0f, 0f, 50f));
            yield return Wait(1.5f);
            yield return PlayCapture.Shot(dir, "p_04_praca_menor_norte");
        }
    }
}
