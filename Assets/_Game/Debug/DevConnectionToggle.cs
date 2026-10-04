using Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build (D-018). F9 encerra a sessão solo e traz a tela de conexão de volta,
    /// para testar o coop LAN. A ação também está no GameControls (mapa Debug) como documentação,
    /// mas aqui usa uma InputAction própria, porque este objeto não vive na cena.
    /// </summary>
    public class DevConnectionToggle : MonoBehaviour
    {
        private static bool created;
        private InputAction toggleAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (created)
                return;
            created = true;
            var go = new GameObject(nameof(DevConnectionToggle));
            DontDestroyOnLoad(go);
            go.AddComponent<DevConnectionToggle>();
        }

        private void Awake()
        {
            toggleAction = new InputAction("ToggleConnectionScreen", InputActionType.Button, "<Keyboard>/f9");
            toggleAction.Enable();
        }

        private void OnDestroy()
        {
            created = false;
            toggleAction?.Dispose();
        }

        private void Update()
        {
            if (!toggleAction.WasPressedThisFrame())
                return;

            var session = FindFirstObjectByType<NetSession>();
            if (session != null && session.IsRunning)
                session.Leave(); // ConnectionScreen.OnEnded mostra o painel
        }
    }
}
