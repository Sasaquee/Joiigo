using Game.Cards;
using Game.Core.Cards;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Cartas pequenas no canto de baixo (D-028): as 4 skills (teclas 1–4) e as 3 do cinto (Q, E, R, D-029).
    /// A recarga é uma sombra que desce sobre a ilustração, sem número. Espaço vazio aparece apagado.
    /// Não mostra energia: ela vive na aura (§3.2, Fase 7). Nada aqui recebe o mouse, para o clique chegar ao ataque.
    /// </summary>
    public class SkillBar : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private CardView[] skillViews = new CardView[CardRules.SkillSlots];
        [SerializeField] private CardView[] beltViews = new CardView[CardRules.BeltSlots];

        public void Configure(CanvasGroup newGroup, CardView[] skills, CardView[] belt)
        {
            group = newGroup;
            skillViews = skills;
            beltViews = belt;
        }

        /// <summary>Carta mostrada no espaço de skill (-1 vazio). Usado em testes.</summary>
        public int SkillCardId(int index) => index >= 0 && index < skillViews.Length && skillViews[index] != null ? skillViews[index].CardId : Loadout.Empty;

        /// <summary>Carta mostrada no espaço do cinto (-1 vazio). Usado em testes.</summary>
        public int BeltCardId(int index) => index >= 0 && index < beltViews.Length && beltViews[index] != null ? beltViews[index].CardId : Loadout.Empty;

        private void Update()
        {
            PlayerCards cards = LocalPlayerCards.Get();
            bool visible = cards != null;
            if (group != null)
                group.alpha = visible ? 1f : 0f; // sem jogador local (tela de conexão), a barra some
            if (!visible)
                return;

            CardDatabase database = cards.Database;
            for (int i = 0; i < skillViews.Length; i++)
            {
                if (skillViews[i] == null)
                    continue;
                skillViews[i].SetCard(database, cards.GetSlot(SlotType.Skill, i));
                skillViews[i].SetCooldown(cards.SkillCooldownFraction(i));
            }

            for (int i = 0; i < beltViews.Length; i++)
            {
                if (beltViews[i] == null)
                    continue;
                int id = cards.GetSlot(SlotType.Belt, i);
                beltViews[i].SetCard(database, id, id >= 0 ? Mathf.Max(1, cards.BeltCount(i)) : 1);
            }
        }
    }
}
