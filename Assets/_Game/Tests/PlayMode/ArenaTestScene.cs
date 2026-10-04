using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.PlayMode
{
    /// <summary>Carrega a Arena do zero, encerrando e destruindo qualquer NetworkManager de um teste anterior.</summary>
    public static class ArenaTestScene
    {
        public static IEnumerator Load()
        {
            yield return Cleanup();
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
        }

        public static IEnumerator Cleanup()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
                yield break;

            if (manager.IsListening)
                manager.Shutdown();
            while (manager != null && manager.ShutdownInProgress)
                yield return null;
            if (manager != null)
                Object.Destroy(manager.gameObject);
            yield return null;
        }
    }
}
