using Game.Combat;
using Game.Core.Combat;
using Game.Core.Math;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>Corte em arco largo e forte (Lâmina Sedenta). O custo em vida é da carta, não deste efeito.</summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Arc Slash", fileName = "ArcSlashEffect")]
    public class ArcSlashEffect : CardEffect
    {
        [Min(0f)] public float damage = 40f;
        [Range(0f, 1f)] public float arcaneFraction = 0.5f;
        [Tooltip("Alcance do corte a partir do jogador (m).")]
        [Min(0.5f)] public float range = 3f;
        [Tooltip("Meia-abertura do arco (graus). 80 = arco de 160°. O visual usa 80°.")]
        [Range(5f, 180f)] public float halfAngle = 80f;

        public override void Execute(ICardUser user, CardData card)
        {
            Vector3 p = user.Transform.position;
            Vector3 dir = user.AimDirection;
            var origin = new Float2(p.x, p.z);
            var facing = new Float2(dir.x, dir.z);
            var packet = new DamagePacket(damage * user.Potency, arcaneFraction); // qualidade da carta (D-048)

            foreach (IDamageable target in user.EnemiesInRadius(p, range))
            {
                Vector3 t = target.Transform.position;
                if (ArcHit.IsInArc(origin, facing, new Float2(t.x, t.z), range, halfAngle, target.Radius))
                    target.ServerApplyDamage(packet, user.ClientId);
            }

            user.BroadcastVisual("slash_wide", p, dir, range);
        }
    }
}
