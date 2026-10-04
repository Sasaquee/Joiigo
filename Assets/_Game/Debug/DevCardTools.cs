using Game.Cards;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build (D-033: ninguém começa com cartas; a Fase 6 traz as do chão).
    /// F3 = "dar carta": o host dá a próxima carta do banco, em ciclo. Shift+F3 dá todas de uma vez.
    /// A ação GiveCard também está no GameControls (mapa Debug) como documentação, mas aqui usa uma
    /// InputAction própria, porque este objeto não vive na cena (mesmo padrão do DevConnectionToggle).
    /// </summary>
    public class DevCardTools : MonoBehaviour
    {
        private static bool created;
        private InputAction giveAction;
        private int nextCard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (created)
                return;
            created = true;
            var go = new GameObject(nameof(DevCardTools));
            DontDestroyOnLoad(go);
            go.AddComponent<DevCardTools>();
        }

        private void Awake()
        {
            giveAction = new InputAction("GiveCard", InputActionType.Button, "<Keyboard>/f3");
            giveAction.Enable();
        }

        private void OnDestroy()
        {
            created = false;
            giveAction?.Dispose();
        }

        private void Update()
        {
            if (!giveAction.WasPressedThisFrame())
                return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer)
                return; // só o host dá cartas

            NetworkObject player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            PlayerCards cards = player != null ? player.GetComponent<PlayerCards>() : null;
            if (cards == null || !cards.IsSpawned || cards.Database == null || cards.Database.Count == 0)
                return;

            int count = cards.Database.Count;
            if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
            {
                for (int id = 0; id < count; id++)
                    cards.ServerGiveCard(id);
                Debug.Log($"[DevCardTools] Todas as cartas ({count}) entregues.");
                return;
            }

            nextCard %= count;
            cards.ServerGiveCard(nextCard);
            var given = cards.Database.Get(nextCard);
            Debug.Log($"[DevCardTools] Carta entregue: {(given != null ? given.displayName : "?")} (id {nextCard}).");
            nextCard = (nextCard + 1) % count;
        }
    }
}
