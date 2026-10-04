using UnityEngine;

namespace Game.Cards
{
    /// <summary>Cura parte da vida e devolve energia (Tônico de Óleo e Luz).</summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Heal", fileName = "HealEffect")]
    public class HealEffect : CardEffect
    {
        [Min(0f)] public float heal = 35f;
        [Min(0f)] public float energy = 40f;

        public override void Execute(ICardUser user, CardData card)
        {
            if (user.Health != null)
                user.Health.ServerHeal(heal);
            user.AddEnergy(energy);
            user.BroadcastVisual("heal", user.Transform.position, Vector3.up, 1f);
        }
    }
}
