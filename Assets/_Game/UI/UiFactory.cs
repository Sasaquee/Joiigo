using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Peças básicas de uGUI criadas por código, sem sprites nem prefabs: retângulos de cor, textos e contornos.
    /// Usado pelo CardView (em runtime), pela tela de tiragem e pelo CardUiBuilder (no editor).
    /// Tudo nasce com raycastTarget desligado; quem precisa receber clique liga de propósito.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color Brass = new Color(0.78f, 0.62f, 0.30f);
        public static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f);
        public static readonly Color Ink = new Color(0.04f, 0.035f, 0.05f);
        public static readonly Color Crystal = new Color(0.35f, 0.85f, 0.95f); // cristal arcano (D-017)

        private static Font font;

        /// <summary>Fonte embutida do uGUI legado (a mesma da tela de conexão).</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image MakeImage(string name, Transform parent, Color color)
        {
            RectTransform rect = MakeRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Área invisível que recebe o mouse (arrastar, soltar, passar por cima). O uGUI só entrega esses eventos
        /// a um Graphic com raycastTarget; transparente não basta se a malha for descartada, por isso o cull sai.
        /// </summary>
        public static Image AddHitArea(GameObject target)
        {
            var image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
            return image;
        }

        public static Text MakeText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = MakeRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>Contorno de cor sólida em volta do retângulo (efeito Outline do uGUI).</summary>
        public static Outline AddOutline(Graphic graphic, Color color, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
            return outline;
        }

        /// <summary>Ocupa todo o pai, com folga opcional nas quatro bordas.</summary>
        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Ocupa todo o pai, deslocado sem mudar de tamanho (cartas empilhadas atrás).</summary>
        public static void StretchOffset(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offset;
            rect.offsetMax = offset;
        }

        /// <summary>Âncora, pivô, posição e tamanho de uma vez (âncora == pivô quando só uma é passada).</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Centro do pai: posição relativa ao centro.</summary>
        public static void PlaceCentered(RectTransform rect, Vector2 position, Vector2 size)
        {
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }
    }
}
