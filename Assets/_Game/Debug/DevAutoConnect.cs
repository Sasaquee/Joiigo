using System;
using System.Collections;
using Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build. Conecta sozinho pela linha de comando, para testar a LAN sem clicar:
    ///   -autohost                 hospeda
    ///   -autojoin 127.0.0.1       entra no IP
    ///   -automove                 (cliente) anda para frente por 1 s depois de conectar
    ///   -autostart                (host) dá a largada sozinho depois de 2 s
    ///   -autoquit 10              registra as posições no log e fecha depois de N segundos
    /// </summary>
    public class DevAutoConnect : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-autohost") < 0 && Array.IndexOf(args, "-autojoin") < 0)
                return;
            var go = new GameObject(nameof(DevAutoConnect));
            DontDestroyOnLoad(go);
            go.AddComponent<DevAutoConnect>();
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            var session = FindFirstObjectByType<NetSession>();
            if (session == null)
            {
                Debug.LogError("[DevAutoConnect] NetSession não encontrada.");
                yield break;
            }

            bool host = Array.IndexOf(args, "-autohost") >= 0;
            bool ok = host ? session.Host() : session.Join(Value(args, "-autojoin") ?? "127.0.0.1");
            Debug.Log($"[DevAutoConnect] {(host ? "host" : "cliente")} iniciado: {ok}");

            float quitAfter = float.TryParse(Value(args, "-autoquit"), out float q) ? q : 0f;
            float t0 = Time.realtimeSinceStartup;

            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && Time.realtimeSinceStartup - t0 < 10f)
                yield return null;
            yield return null;
            var me = NetworkManager.Singleton.LocalClient?.PlayerObject;
            Debug.Log($"[DevAutoConnect] meu jogador: {(me != null ? me.transform.position.ToString("F2") : "nenhum")}");

            if (me != null && Array.IndexOf(args, "-automove") >= 0)
            {
                var reader = me.GetComponent<Game.Player.PlayerInputReader>();
                if (reader != null)
                    reader.enabled = false;
                var player = me.GetComponent<NetworkPlayer>();
                yield return new WaitForSecondsRealtime(1f);
                player.SubmitLocalIntent(Vector3.forward, null);
                yield return new WaitForSecondsRealtime(1f);
                player.SubmitLocalIntent(Vector3.zero, null);
                Debug.Log($"[DevAutoConnect] andei; previsto agora: {me.transform.position:F2}");
            }

            if (host && Array.IndexOf(args, "-autostart") >= 0)
            {
                yield return new WaitForSecondsRealtime(2f);
                FindFirstObjectByType<MatchState>()?.ServerStart();
                Debug.Log("[DevAutoConnect] largada dada");
            }

            if (quitAfter <= 0f)
                yield break;
            float nextLog = 0f;
            while (Time.realtimeSinceStartup - t0 < quitAfter)
            {
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    nextLog = Time.realtimeSinceStartup + 2f;
                    foreach (var p in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.InstanceID))
                        Debug.Log($"[DevAutoConnect] t={Time.realtimeSinceStartup - t0:0.0} dono={p.OwnerClientId} pos={p.transform.position:F2} estado={p.ServerState.Position:F2} tick={p.ServerState.Tick} ack={p.ServerState.AckSeq}");
                }
                yield return null;
            }

            foreach (var p in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.InstanceID))
                Debug.Log($"[DevAutoConnect] jogador dono={p.OwnerClientId} pos={p.transform.position:F2}");
            var match = FindFirstObjectByType<MatchState>();
            Debug.Log($"[DevAutoConnect] largada={match != null && match.IsStarted} conectado={NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost}");
            Application.Quit();
        }

        private static string Value(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
