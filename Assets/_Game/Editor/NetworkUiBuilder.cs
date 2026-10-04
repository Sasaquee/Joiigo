using Game.Net;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Monta a tela de conexão (Hospedar / Entrar com IP) na cena aberta.</summary>
    public static class NetworkUiBuilder
    {
        private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.12f, 0.92f);
        private static readonly Color BrassColor = new Color(0.78f, 0.62f, 0.30f);
        private static readonly Color TextColor = new Color(0.93f, 0.90f, 0.82f);

        public static void Build(NetSession session)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetAsLastSibling();

            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var resources = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd")
            };

            // Painel central.
            var panel = DefaultControls.CreatePanel(resources);
            panel.name = "PainelConexao";
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 300f);
            panel.GetComponent<Image>().color = PanelColor;

            var ipGo = DefaultControls.CreateInputField(resources);
            ipGo.name = "CampoIP";
            Place(ipGo, panel.transform, new Vector2(0f, 80f), new Vector2(420f, 56f));
            var ipField = ipGo.GetComponent<InputField>();
            ipField.text = "127.0.0.1";
            ipField.characterLimit = 64;
            StyleText(ipField.textComponent, 26);
            var placeholder = (Text)ipField.placeholder;
            StyleText(placeholder, 26);
            placeholder.text = "IP do host";
            placeholder.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.4f);
            ipGo.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f);

            var hostButton = MakeButton(resources, "BotaoHospedar", "Hospedar", panel.transform, new Vector2(-105f, -5f));
            var joinButton = MakeButton(resources, "BotaoEntrar", "Entrar", panel.transform, new Vector2(105f, -5f));

            var status = MakeText("Status", panel.transform, new Vector2(0f, -95f), new Vector2(460f, 40f), 22);
            status.text = string.Empty;

            // IP do host no canto (D-012), fora do painel para continuar visível depois de hospedar.
            var hostIp = MakeText("IPHost", canvasGo.transform, Vector2.zero, new Vector2(700f, 36f), 20);
            var ipRect = (RectTransform)hostIp.transform;
            ipRect.anchorMin = ipRect.anchorMax = ipRect.pivot = new Vector2(0f, 0f);
            ipRect.anchoredPosition = new Vector2(16f, 12f);
            hostIp.alignment = TextAnchor.LowerLeft;
            hostIp.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f);
            hostIp.gameObject.SetActive(false);

            canvasGo.AddComponent<ConnectionScreen>().Configure(session, panel, ipField, hostButton, joinButton, status, hostIp);
        }

        private static Button MakeButton(DefaultControls.Resources resources, string name, string label, Transform parent, Vector2 pos)
        {
            var go = DefaultControls.CreateButton(resources);
            go.name = name;
            Place(go, parent, pos, new Vector2(190f, 56f));
            go.GetComponent<Image>().color = BrassColor;
            var text = go.GetComponentInChildren<Text>();
            StyleText(text, 26);
            text.text = label;
            text.color = new Color(0.12f, 0.1f, 0.08f);
            return go.GetComponent<Button>();
        }

        private static Text MakeText(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize)
        {
            var go = DefaultControls.CreateText(new DefaultControls.Resources());
            go.name = name;
            Place(go, parent, pos, size);
            var text = go.GetComponent<Text>();
            StyleText(text, fontSize);
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private static void StyleText(Text text, int fontSize)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = TextColor;
        }

        private static void Place(GameObject go, Transform parent, Vector2 pos, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }
    }
}
