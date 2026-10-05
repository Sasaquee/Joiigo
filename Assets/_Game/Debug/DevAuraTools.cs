using Game.Aura;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build (§4.7). F8 = alternar a paleta da aura entre a normal e a alternativa para
    /// daltônicos (D-065: azul forte, amarelo e branco). Mesmo padrão do DevDiceTools: objeto próprio, fora da cena.
    /// </summary>
    public class DevAuraTools : MonoBehaviour
    {
        private static bool created;
        private InputAction paletteAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (created)
                return;
            created = true;
            var go = new GameObject(nameof(DevAuraTools));
            DontDestroyOnLoad(go);
            go.AddComponent<DevAuraTools>();
        }

        private void Awake()
        {
            paletteAction = new InputAction("ToggleAuraPalette", InputActionType.Button, "<Keyboard>/f8");
            paletteAction.Enable();
        }

        private void OnDestroy()
        {
            paletteAction?.Disable();
            paletteAction?.Dispose();
        }

        private void Update()
        {
            if (paletteAction == null || !paletteAction.WasPressedThisFrame())
                return;
            AuraPaletteSwitch.Toggle();
            Debug.Log($"[Aura] Paleta: {AuraPaletteSwitch.Current}");
        }
    }
}
