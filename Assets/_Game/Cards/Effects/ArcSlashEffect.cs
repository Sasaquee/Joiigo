using Game.Combat;
using Game.Core.Combat;
using Game.Core.Math;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Corte em arco (Lâmina Sedenta, Chicote de Corrente, Martelo a Vapor). O custo em vida é da carta, não deste efeito.
    /// O visual "slash_wide" é o corte carmim da Lâmina (arco fixo de 160°); "slash_arc" desenha o arco com a meia-abertura
    /// e o alcance do efeito (cartas que não são amaldiçoadas).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Arc Slash", fileName = "ArcSlashEffect")]
    public class ArcSlashEffect : CardEffect
    {
        [Min(0f)] public float damage = 40f;
        [Range(0f, 1f)] public float arcaneFraction = 0.5f;
        [Tooltip("Alcance do corte a partir do jogador (m).")]
        [Min(0.5f)] public float range = 3f;
        [Tooltip("Meia-abertura do arco (graus). 80 = arco de 160°. O visual usa 80°.")]
        [Range(5f, 180f)] public float halfAngle = 80f;

        [Tooltip("Visual do corte: slash_wide (carmim, arco fixo; amaldiçoada) ou slash_arc (acompanha a abertura e o alcance).")]
        public string visualId = "slash_wide";

        public override void Execute(ICardUser user, CardData card)
        {
            Vector3 p = user.Transform.position;
            Vector3 dir = user.AimDirection;
            var origin = new Float2(p.x, p.z);
            var facing = new Float2(dir.x, dir.z);
            var packet = new DamagePacket(damage * user.Potency * user.DamageMultiplier, arcaneFraction); // qualidade da carta (D-048) e bênção do 20 (D-085)

            foreach (IDamageable target in user.EnemiesInRadius(p, range))
            {
                Vector3 t = target.Transform.position;
                if (ArcHit.IsInArc(origin, facing, new Float2(t.x, t.z), range, halfAngle, target.Radius))
                    target.ServerApplyDamage(packet, user.ClientId);
            }

            // O Y da direção carrega a meia-abertura (graus) para o slash_arc; o slash_wide ignora o Y.
            user.BroadcastVisual(visualId, p, new Vector3(dir.x, halfAngle, dir.z), range);
        }
    }
}
