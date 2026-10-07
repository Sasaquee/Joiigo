using Game.Combat;
using Game.Core.Combat;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Investida curta na direção da mira (Pistão Rúnico). O host teleporta o jogador até onde o caminho
    /// está livre (para na parede) e fere uma vez quem estiver na faixa. Atravessa inimigos e aliados.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Dash", fileName = "DashEffect")]
    public class DashEffect : CardEffect
    {
        [Tooltip("Distância máxima da investida (m).")]
        [Min(0f)] public float distance = 5f;
        [Min(0f)] public float damage = 18f;
        [Range(0f, 1f)] public float arcaneFraction = 0.3f;
        [Tooltip("Largura da faixa que fere (m).")]
        [Min(0.1f)] public float width = 1.4f;

        // Folga contra a parede: o jogador para um pouco antes.
        private const float WallMargin = 0.1f;
        private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        public override void Execute(ICardUser user, CardData card)
        {
            Transform self = user.Transform;
            Vector3 dir = user.AimDirection;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : self.forward;

            Vector3 start = self.position;
            float allowed = ClearDistance(self, start, dir, distance);
            Vector3 end = start + dir * allowed;
            if (allowed > 0.01f)
                Teleport(self, end);

            DamageAlongPath(user, start, end, user.Potency * user.DamageMultiplier); // qualidade (D-048) e bênção do 20 (D-085)
            user.BroadcastVisual("dash", start, dir, allowed);
        }

        /// <summary>Quanto dá para andar em dir antes de bater em algo fixo (cápsula do tamanho do personagem).</summary>
        private static float ClearDistance(Transform self, Vector3 start, Vector3 dir, float maxDistance)
        {
            var controller = self.GetComponent<CharacterController>();
            float radius = controller != null ? Mathf.Max(0.1f, controller.radius - controller.skinWidth) : 0.35f;
            float height = controller != null ? controller.height : 2f;

            // Cápsula um pouco acima do piso, para não raspar nele.
            Vector3 p1 = start + Vector3.up * (radius + 0.15f);
            Vector3 p2 = start + Vector3.up * Mathf.Max(radius + 0.2f, height - radius - 0.1f);

            Physics.SyncTransforms();
            int count = Physics.CapsuleCastNonAlloc(p1, p2, radius, dir, hitBuffer, maxDistance + WallMargin, ~0,
                QueryTriggerInteraction.Ignore);

            float best = maxDistance;
            for (int i = 0; i < count; i++)
            {
                Collider c = hitBuffer[i].collider;
                if (c.transform.IsChildOf(self) || c.attachedRigidbody != null || c is CharacterController)
                    continue; // o próprio jogador e restos soltos
                if (c.GetComponentInParent<NetworkHealth>() != null)
                    continue; // inimigos e aliados: atravessa
                best = Mathf.Min(best, Mathf.Max(0f, hitBuffer[i].distance - WallMargin));
            }
            return best;
        }

        /// <summary>O CharacterController ignora transform movido por fora: desliga e religa ao teleportar.</summary>
        private static void Teleport(Transform self, Vector3 position)
        {
            var controller = self.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            self.position = position;
            if (controller != null)
                controller.enabled = true;
        }

        private void DamageAlongPath(ICardUser user, Vector3 start, Vector3 end, float potency)
        {
            Vector3 mid = (start + end) * 0.5f;
            float half = Vector3.Distance(start, end) * 0.5f;
            var packet = new DamagePacket(damage * potency, arcaneFraction); // qualidade da carta (D-048)

            foreach (IDamageable target in user.EnemiesInRadius(mid, half + width * 0.5f + 1f))
            {
                float d = CardTargets.PlanarDistanceToSegment(start, end, target.Transform.position);
                if (d <= width * 0.5f + target.Radius)
                    target.ServerApplyDamage(packet, user.ClientId);
            }
        }
    }
}
