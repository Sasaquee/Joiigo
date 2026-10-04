using Game.Core.Cards;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Monta a interface das cartas na Canvas "UI" da cena aberta (criada pelo NetworkUiBuilder):
    /// "BarraDeCartas" (4 skills + 3 do cinto, D-028/D-029) e "Tiragem" (tela de loadout, D-027).
    /// Idempotente: apaga as duas e recria. Chamado pelo ArenaBuilder logo depois de NetworkUiBuilder.Build.
    /// Tamanhos em pixels da resolução de referência da Canvas (1920 x 1080); as cartas seguem a proporção
    /// das imagens do Blender (CardView.Aspect).
    /// </summary>
    public static class CardUiBuilder
    {
        public const string BarName = "BarraDeCartas";
        public const string ScreenName = "Tiragem";

        // Tiragem.
        private const float SlotHeight = 180f;
        private const float InventoryCardHeight = 150f;

        // Barra de baixo.
        private const float BarSkillHeight = 96f;
        private const float BarBeltHeight = 72f;
        private const float BarSkillGap = 12f;
        private const float BarBeltGap = 10f;
        private const float BarGroupGap = 40f;
        private const float BarMargin = 26f; // espaço sob as cartas para a tecla

        private static readonly Color KeyColor = new Color(UiFactory.Parchment.r, UiFactory.Parchment.g, UiFactory.Parchment.b, 0.7f);
        private static readonly Color BrassSoft = new Color(UiFactory.Brass.r, UiFactory.Brass.g, UiFactory.Brass.b, 0.35f);

        [MenuItem("Game/Setup/Reconstruir UI de Cartas")]
        public static void Build()
        {
            Canvas canvas = FindUiCanvas();
            if (canvas == null)
            {
                Debug.LogError("[CardUiBuilder] Canvas \"UI\" não encontrada na cena aberta. Rode o NetworkUiBuilder antes.");
                return;
            }

            Remove(canvas.transform, BarName);
            Remove(canvas.transform, ScreenName);

            BuildSkillBar(canvas.transform);
            BuildLoadoutScreen(canvas.transform);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("UI de cartas construída (BarraDeCartas, Tiragem).");
        }

        // ---------- Barra de skills e cinto ----------

        private static void BuildSkillBar(Transform canvas)
        {
            RectTransform root = UiFactory.MakeRect(BarName, canvas);
            UiFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(520f, 150f));

            // Nada da barra recebe o mouse: o clique tem que chegar ao ataque.
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            float skillWidth = CardView.WidthForHeight(BarSkillHeight);
            float beltWidth = CardView.WidthForHeight(BarBeltHeight);
            float skillsTotal = CardRules.SkillSlots * skillWidth + (CardRules.SkillSlots - 1) * BarSkillGap;
            float beltTotal = CardRules.BeltSlots * beltWidth + (CardRules.BeltSlots - 1) * BarBeltGap;
            float x = -(skillsTotal + BarGroupGap + beltTotal) * 0.5f;

            var skillViews = new CardView[CardRules.SkillSlots];
            for (int i = 0; i < skillViews.Length; i++)
            {
                skillViews[i] = MakeBarCard(root, "Skill" + (i + 1), x + skillWidth * 0.5f, new Vector2(skillWidth, BarSkillHeight),
                    (i + 1).ToString(), 18);
                x += skillWidth + BarSkillGap;
            }
            x += BarGroupGap - BarSkillGap;

            string[] beltKeys = { "Q", "E", "R" };
            var beltViews = new CardView[CardRules.BeltSlots];
            for (int i = 0; i < beltViews.Length; i++)
            {
                beltViews[i] = MakeBarCard(root, "Cinto" + (i + 1), x + beltWidth * 0.5f, new Vector2(beltWidth, BarBeltHeight),
                    beltKeys[i], 16);
                x += beltWidth + BarBeltGap;
            }

            root.gameObject.AddComponent<SkillBar>().Configure(group, skillViews, beltViews);
        }

        private static CardView MakeBarCard(RectTransform bar, string name, float centerX, Vector2 size, string key, int keySize)
        {
            RectTransform wrapper = UiFactory.MakeRect(name, bar);
            UiFactory.Place(wrapper, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(centerX, BarMargin), size);

            CardView view = CardView.Create(wrapper, "Carta", size);
            UiFactory.Stretch(view.Rect);

            AddKeyLabel(wrapper, key, keySize, new Vector2(size.x + 12f, keySize + 6f));
            return view;
        }

        // ---------- Tela de tiragem ----------

        private static void BuildLoadoutScreen(Transform canvas)
        {
            RectTransform root = UiFactory.MakeRect(ScreenName, canvas);
            UiFactory.Stretch(root);
            var screen = root.gameObject.AddComponent<LoadoutScreen>();

            // Tudo da tela fica no Painel, que a LoadoutScreen liga e desliga; o componente fica sempre ativo para ouvir o Tab.
            RectTransform panel = UiFactory.MakeRect("Painel", root);
            UiFactory.Stretch(panel);

            // Fundo escuro e translúcido: a arena continua visível por trás (o jogo segue rodando).
            Image dim = UiFactory.MakeImage("Fundo", panel, new Color(0.02f, 0.02f, 0.03f, 0.72f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            // O pano da tiragem.
            Image cloth = UiFactory.MakeImage("Pano", panel, new Color(0.05f, 0.08f, 0.07f, 0.60f));
            UiFactory.PlaceCentered(cloth.rectTransform, Vector2.zero, new Vector2(1700f, 1020f));
            UiFactory.AddOutline(cloth, BrassSoft, 2f);

            var slotSize = new Vector2(CardView.WidthForHeight(SlotHeight), SlotHeight);

            // Espadas (skills) em cima, teclas 1 a 4.
            var skills = new LoadoutSlot[CardRules.SkillSlots];
            float[] skillX = { -270f, -90f, 90f, 270f };
            for (int i = 0; i < skills.Length; i++)
                skills[i] = MakeSlot(panel, screen, "Skill" + (i + 1), SlotType.Skill, i, new Vector2(skillX[i], 260f), slotSize, (i + 1).ToString());

            // Paus (passivas) à esquerda, Ouros (equipamentos) à direita.
            var passives = new LoadoutSlot[CardRules.PassiveSlots];
            for (int i = 0; i < passives.Length; i++)
                passives[i] = MakeSlot(panel, screen, "Passiva" + (i + 1), SlotType.Passive, i, new Vector2(-540f, 70f - 220f * i), slotSize, null);

            var equipment = new LoadoutSlot[CardRules.EquipmentSlots];
            for (int i = 0; i < equipment.Length; i++)
                equipment[i] = MakeSlot(panel, screen, "Equipamento" + (i + 1), SlotType.Equipment, i, new Vector2(540f, 70f - 220f * i), slotSize, null);

            // Copas (cinto) embaixo no centro, só com a letra da tecla.
            string[] beltKeys = { "Q", "E", "R" };
            var belt = new LoadoutSlot[CardRules.BeltSlots];
            for (int i = 0; i < belt.Length; i++)
                belt[i] = MakeSlot(panel, screen, "Cinto" + (i + 1), SlotType.Belt, i, new Vector2(-150f + 150f * i, -110f), slotSize, beltKeys[i]);

            // Inventário: leque de cartas no fundo. O fundo também é espaço de soltar (devolve a carta equipada).
            Image area = UiFactory.MakeImage("AreaInventario", panel, new Color(0f, 0f, 0f, 0.28f));
            UiFactory.PlaceCentered(area.rectTransform, new Vector2(0f, -405f), new Vector2(1560f, 200f));
            area.raycastTarget = true;
            UiFactory.AddOutline(area, new Color(UiFactory.Brass.r, UiFactory.Brass.g, UiFactory.Brass.b, 0.22f), 2f);
            area.gameObject.AddComponent<LoadoutSlot>().Configure(SlotRole.InventoryArea, SlotType.Skill, -1, screen, null);

            RectTransform fan = UiFactory.MakeRect("Leque", area.transform);
            UiFactory.PlaceCentered(fan, Vector2.zero, new Vector2(1500f, 190f));

            // Dica do mouse por cima: nome curto + carta de tarô.
            Image hoverBox = UiFactory.MakeImage("Dica", panel, new Color(0.05f, 0.04f, 0.05f, 0.94f));
            UiFactory.Place(hoverBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320f, 48f));
            UiFactory.AddOutline(hoverBox, new Color(UiFactory.Brass.r, UiFactory.Brass.g, UiFactory.Brass.b, 0.6f), 2f);
            Text hoverText = UiFactory.MakeText("Texto", hoverBox.transform, string.Empty, 24, UiFactory.Parchment);
            UiFactory.Stretch(hoverText.rectTransform);
            hoverText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hoverText.verticalOverflow = VerticalWrapMode.Overflow;
            hoverBox.gameObject.SetActive(false);

            // Por último, acima de tudo: onde a carta arrastada (fantasma) aparece.
            RectTransform dragLayer = UiFactory.MakeRect("CamadaArrasto", panel);
            UiFactory.Stretch(dragLayer);

            screen.Configure(panel.gameObject, panel, skills, passives, equipment, belt, fan,
                new Vector2(CardView.WidthForHeight(InventoryCardHeight), InventoryCardHeight),
                dragLayer, hoverBox.rectTransform, hoverText);

            panel.gameObject.SetActive(false); // nasce fechada; Tab abre
        }

        private static LoadoutSlot MakeSlot(Transform parent, LoadoutScreen screen, string name, SlotType type, int index,
            Vector2 position, Vector2 size, string key)
        {
            RectTransform rect = UiFactory.MakeRect(name, parent);
            UiFactory.PlaceCentered(rect, position, size);
            UiFactory.AddHitArea(rect.gameObject);

            CardView view = CardView.Create(rect, "Carta", size);
            UiFactory.Stretch(view.Rect);

            var slot = rect.gameObject.AddComponent<LoadoutSlot>();
            slot.Configure(SlotRole.Slot, type, index, screen, view);

            if (!string.IsNullOrEmpty(key))
                AddKeyLabel(rect, key, 22, new Vector2(60f, 28f));
            return slot;
        }

        /// <summary>Letra ou dígito da tecla, pequeno, logo abaixo da carta.</summary>
        private static void AddKeyLabel(RectTransform card, string key, int fontSize, Vector2 size)
        {
            Text label = UiFactory.MakeText("Tecla", card, key, fontSize, KeyColor);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), size);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // ---------- Apoio ----------

        private static Canvas FindUiCanvas()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                foreach (GameObject root in SceneManager.GetSceneAt(s).GetRootGameObjects())
                {
                    if (root.name == "UI" && root.TryGetComponent(out Canvas canvas))
                        return canvas;
                }
            }
            return null;
        }

        private static void Remove(Transform parent, string childName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
