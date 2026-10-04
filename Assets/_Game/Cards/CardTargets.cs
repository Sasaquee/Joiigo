using System.Collections.Generic;
using Game.Combat;
using Game.Net;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>Busca de alvos das cartas (só no host): inimigos vivos, nunca jogadores.</summary>
    public static class CardTargets
    {
        // Folga técnica da busca de colisores; o teste de verdade usa o raio do alvo.
        private const float MaxTargetRadius = 3f;

        private static readonly Collider[] overlap = new Collider[128];

        /// <summary>Preenche results com os inimigos vivos cujo corpo toca o círculo (center, radius) no plano.</summary>
        public static void Collect(Vector3 center, float radius, List<IDamageable> results)
        {
            results.Clear();
            Physics.SyncTransforms(); // corpos recém-nascidos ou movidos no mesmo quadro
            int count = Physics.OverlapSphereNonAlloc(center, radius + MaxTargetRadius, overlap, ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = overlap[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || results.Contains(target))
                    continue;
                if (IsPlayer(target))
                    continue;

                Vector3 t = target.Transform.position;
                float planar = new Vector2(t.x - center.x, t.z - center.z).magnitude;
                if (planar - target.Radius > radius)
                    continue;

                results.Add(target);
            }
        }

        /// <summary>Distância no plano de p até o segmento a-b.</summary>
        public static float PlanarDistanceToSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var pa = new Vector2(p.x - a.x, p.z - a.z);
            var ba = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSqr = ba.sqrMagnitude;
            float t = lengthSqr > 0.0001f ? Mathf.Clamp01(Vector2.Dot(pa, ba) / lengthSqr) : 0f;
            return (pa - ba * t).magnitude;
        }

        private static bool IsPlayer(IDamageable target) =>
            target.Transform.GetComponentInParent<NetworkPlayer>() != null;
    }
}
