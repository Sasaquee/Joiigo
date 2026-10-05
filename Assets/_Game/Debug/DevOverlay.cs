using System.Globalization;
using System.Text;
using Game.Aura;
using Game.Cards;
using Game.Combat;
using Game.Dice;
using Game.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build (§4.7). F1 liga e desliga (começa desligado) o overlay de debug no
    /// canto superior esquerdo: HP, energia, último dado, caminho, paleta da aura e ondas do jogador local,
    /// mais a lista das teclas de debug. Tudo é lido a cada OnGUI, sem referência guardada entre cenas;
    /// sem jogador local, mostra "sem jogador". A ação ToggleOverlay também está no GameControls (mapa
    /// Debug) como documentação, mas aqui usa uma InputAction própria, porque este objeto não vive na
    /// cena (mesmo padrão do DevDiceTools).
    /// </summary>
    public class DevOverlay : MonoBehaviour
    {
        private const string HelpLine =
            "F1 overlay · F2 dado · Shift+F2 carta no chão · F3 dar carta · F4 inimigo · F5 dano · F6 cura · F7 paleta · F9 conexão";

        private static bool created;
        private InputAction toggleAction;
        private bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (created)
                return;
            created = true;
            var go = new GameObject(nameof(DevOverlay));
            DontDestroyOnLoad(go);
            go.AddComponent<DevOverlay>();
        }

        private void Awake()
        {
            toggleAction = new InputAction("ToggleOverlay", InputActionType.Button, "<Keyboard>/f1");
            toggleAction.Enable();
        }

        private void OnDestroy()
        {
            created = false;
            toggleAction?.Dispose();
        }

        private void Update()
        {
            if (toggleAction.WasPressedThisFrame())
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            string text = BuildText();
            var style = new GUIStyle(GUI.skin.label) { padding = new RectOffset(8, 8, 6, 6) };
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            var box = new Rect(8f, 8f, size.x + 4f, size.y + 4f);
            Color before = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f); // fundo escuro semitransparente
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = before;
            GUI.Label(box, content, style);
        }

        /// <summary>Monta o texto na hora, relendo a cena a cada chamada (não guarda nada entre cenas).</summary>
        private static string BuildText()
        {
            var lines = new StringBuilder();

            NetworkManager manager = NetworkManager.Singleton;
            NetworkObject player = manager != null && manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            if (player == null)
            {
                lines.Append("sem jogador\n\n").Append(HelpLine);
                return lines.ToString();
            }

            NetworkHealth health = player.GetComponent<NetworkHealth>();
            lines.Append("HP: ")
                .Append(health != null ? Num(health.Current) + " / " + Num(health.Max) : "—")
                .Append('\n');

            PlayerCards cards = player.GetComponent<PlayerCards>();
            lines.Append("energia: ")
                .Append(cards != null ? Num(cards.Energy) + " / " + Num(cards.EnergyMax) : "—")
                .Append('\n');

            CardDropService drop = FindFirstObjectByType<CardDropService>();
            lines.Append("último dado: ")
                .Append(drop != null && drop.LastRoll > 0 ? Num(drop.LastRoll) : "nenhum")
                .Append('\n');

            lines.Append("caminho: ").Append(DescribePath(cards)).Append('\n');
            lines.Append("paleta da aura: ").Append(AuraPaletteSwitch.Current).Append('\n');

            WaveSpawner spawner = FindFirstObjectByType<WaveSpawner>();
            lines.Append("ondas: ")
                .Append(spawner != null
                    ? Num(spawner.WavesReleased) + " liberadas · " + Num(spawner.AliveCount) + " vivos"
                    : "—")
                .Append('\n');

            lines.Append('\n').Append(HelpLine);
            return lines.ToString();
        }

        /// <summary>As 3 tags mais usadas com a contagem de cada uma (§4.4); "—" quando não há caminho.</summary>
        private static string DescribePath(PlayerCards cards)
        {
            if (cards == null)
                return "—";
            var text = new StringBuilder();
            bool any = false;
            foreach (string tag in cards.Path.Top(3))
            {
                if (any)
                    text.Append(" · ");
                any = true;
                cards.Path.Counts.TryGetValue(tag, out int count);
                text.Append(tag).Append(" x").Append(Num(count));
            }
            return any ? text.ToString() : "—";
        }

        // Números sempre em cultura fixa, para não mudarem com o idioma do sistema.
        private static string Num(float value) => Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
