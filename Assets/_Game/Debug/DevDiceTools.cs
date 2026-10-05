using Game.Dice;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build (§4.7). No host:
    /// F2 = forçar o próximo resultado do D20, em ciclo: normal → 1 → 20 → 10 → normal (o D20 do Core continua puro;
    /// isto só troca o próximo número no serviço, e não existe em build de jogo).
    /// Shift+F2 = pôr uma carta no chão à frente do jogador, sem esperar o fim da onda.
    /// Mesmo padrão do DevCardTools: objeto próprio, fora da cena.
    /// </summary>
    public class DevDiceTools : MonoBehaviour
    {
        private static readonly int?[] Cycle = { null, 1, 20, 10 };
        private static bool created;
        private InputAction diceAction;
        private int cycleIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (created)
                return;
            created = true;
            var go = new GameObject(nameof(DevDiceTools));
            DontDestroyOnLoad(go);
            go.AddComponent<DevDiceTools>();
        }

        private void Awake()
        {
            diceAction = new InputAction("ForceNextDice", InputActionType.Button, "<Keyboard>/f2");
            diceAction.Enable();
        }

        private void OnDestroy()
        {
            created = false;
            diceAction?.Dispose();
        }

        private void Update()
        {
            if (!diceAction.WasPressedThisFrame())
                return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer)
                return; // só o host decide o dado

            if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
            {
                SpawnCardNearPlayer(manager);
                return;
            }

            cycleIndex = (cycleIndex + 1) % Cycle.Length;
            CardDropService.DebugForceNextRoll(Cycle[cycleIndex]);
            Debug.Log(Cycle[cycleIndex].HasValue
                ? $"[DevDiceTools] Próximo D20 forçado: {Cycle[cycleIndex].Value}."
                : "[DevDiceTools] Próximo D20 normal.");
        }

        private static void SpawnCardNearPlayer(NetworkManager manager)
        {
            var service = FindFirstObjectByType<CardDropService>();
            NetworkObject player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            if (service == null || player == null)
                return;
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            Vector3 position = player.transform.position + (forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward) * 2.5f;
            position.y = 0f;
            service.ServerSpawnFloorCard(position);
            Debug.Log("[DevDiceTools] Carta posta no chão.");
        }
    }
}
