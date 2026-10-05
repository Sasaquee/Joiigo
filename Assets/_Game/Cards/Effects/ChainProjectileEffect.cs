using System.Collections.Generic;
using Game.Combat;
using Game.Core.Combat;
using Game.Core.Math;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Descarga que salta de inimigo em inimigo (Arco Voltaico). Instantânea: o primeiro alvo é o inimigo mais
    /// próximo dentro do cone da mira; os saltos vão para o mais próximo que ainda não foi atingido.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Chain Projectile", fileName = "ChainProjectileEffect")]
    public class ChainProjectileEffect : CardEffect
    {
        [Tooltip("Dano em cada inimigo atingido.")]
        [Min(0f)] public float damage = 22f;
        [Range(0f, 1f)] public float arcaneFraction = 0.8f;
        [Tooltip("Quantos inimigos no máximo a descarga atinge (o primeiro e os saltos seguintes).")]
        [Min(1)] public int jumps = 3;
        [Tooltip("Alcance de um inimigo para o próximo (m).")]
        [Min(0.5f)] public float jumpRange = 5f;
        [Tooltip("Alcance até o primeiro inimigo, a partir do jogador (m).")]
        [Min(0.5f)] public float firstRange = 10f;
        [Tooltip("Meia-abertura do cone da mira para escolher o primeiro inimigo (graus).")]
        [Range(5f, 90f)] public float firstHalfAngle = 30f;

        // Altura do peito: de onde sai e onde chega a descarga.
        private const float ChestHeight = 1f;
        // Comprimento do raio que erra tudo, só para o visual.
        private const float MissLength = 6f;

        public override void Execute(ICardUser user, CardData card)
        {
            Vector3 self = user.Transform.position;
            Vector3 aim = user.AimDirection;
            Vector3 from = self + Vector3.up * ChestHeight;

            IDamageable current = FindFirst(user, self, aim);
            if (current == null)
            {
                user.BroadcastVisual("arc_chain", from, aim, Mathf.Min(firstRange, MissLength));
                return;
            }

            var packet = new DamagePacket(damage * user.Potency, arcaneFraction); // qualidade da carta (D-048)
            var struck = new List<IDamageable>();
            for (int i = 0; i < jumps && current != null; i++)
            {
                Vector3 to = current.Transform.position + Vector3.up * ChestHeight;
                Vector3 segment = to - from;
                float length = segment.magnitude;
                user.BroadcastVisual("arc_chain", from, length > 0.01f ? segment / length : aim, length);

                current.ServerApplyDamage(packet, user.ClientId);
                struck.Add(current);

                from = to;
                current = FindNext(user, current.Transform.position, struck);
            }
        }

        private IDamageable FindFirst(ICardUser user, Vector3 self, Vector3 aim)
        {
            var origin = new Float2(self.x, self.z);
            var facing = new Float2(aim.x, aim.z);
            IDamageable best = null;
            float bestDistance = float.MaxValue;

            foreach (IDamageable target in user.EnemiesInRadius(self, firstRange))
            {
                Vector3 t = target.Transform.position;
                if (!ArcHit.IsInArc(origin, facing, new Float2(t.x, t.z), firstRange, firstHalfAngle, target.Radius))
                    continue;
                float d = new Vector2(t.x - self.x, t.z - self.z).magnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = target;
                }
            }
            return best;
        }

        private IDamageable FindNext(ICardUser user, Vector3 from, List<IDamageable> struck)
        {
            IDamageable best = null;
            float bestDistance = float.MaxValue;
            foreach (IDamageable target in user.EnemiesInRadius(from, jumpRange))
            {
                if (struck.Contains(target))
                    continue;
                Vector3 t = target.Transform.position;
                float d = new Vector2(t.x - from.x, t.z - from.z).magnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = target;
                }
            }
            return best;
        }
    }
}
