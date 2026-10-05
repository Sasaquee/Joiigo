using System.Collections;
using Game.Combat;
using Game.Core.Combat;
using Game.Core.Math;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Cone de dano contínuo à frente (Sopro de Caldeira). A direção fica travada na mira do uso;
    /// a origem acompanha o jogador. Os golpes saem em passos curtos e somam damagePerSecond * duration.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Cone Damage", fileName = "ConeDamageEffect")]
    public class ConeDamageEffect : CardEffect
    {
        [Min(0f)] public float damagePerSecond = 30f;
        [Min(0.05f)] public float duration = 1f;
        [Min(0.5f)] public float range = 4.5f;
        [Tooltip("Meia-abertura do cone (graus). O visual usa um cone fixo de cerca de 30°.")]
        [Range(5f, 90f)] public float halfAngle = 30f;
        [Range(0f, 1f)] public float arcaneFraction = 0.5f;
        [Tooltip("Tempo entre golpes de dano (s).")]
        [Min(0.02f)] public float tickInterval = 0.1f;
        [Tooltip("Tempo entre as baforadas visuais (s).")]
        [Min(0.05f)] public float visualInterval = 0.25f;

        public override void Execute(ICardUser user, CardData card)
        {
            user.Run(Routine(user, user.AimDirection, user.Potency)); // a qualidade vale para o sopro inteiro (D-048)
        }

        private IEnumerator Routine(ICardUser user, Vector3 aim, float potency)
        {
            Vector3 dir = new Vector3(aim.x, 0f, aim.z);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

            float elapsed = 0f;
            float sinceVisual = visualInterval;
            while (elapsed < duration)
            {
                if (user.Health != null && !user.Health.IsAlive)
                    yield break; // caiu no meio do sopro

                float step = Mathf.Min(tickInterval, duration - elapsed);
                Vector3 origin = user.Transform.position;

                if (sinceVisual >= visualInterval)
                {
                    sinceVisual = 0f;
                    user.BroadcastVisual("cone_vapor", origin + Vector3.up, dir, range);
                }

                Tick(user, origin, dir, damagePerSecond * step * potency);

                yield return new WaitForSeconds(step);
                elapsed += step;
                sinceVisual += step;
            }
        }

        private void Tick(ICardUser user, Vector3 origin, Vector3 dir, float amount)
        {
            var from = new Float2(origin.x, origin.z);
            var facing = new Float2(dir.x, dir.z);
            var packet = new DamagePacket(amount, arcaneFraction);

            foreach (IDamageable target in user.EnemiesInRadius(origin, range))
            {
                Vector3 t = target.Transform.position;
                if (ArcHit.IsInArc(from, facing, new Float2(t.x, t.z), range, halfAngle, target.Radius))
                    target.ServerApplyDamage(packet, user.ClientId);
            }
        }
    }
}
