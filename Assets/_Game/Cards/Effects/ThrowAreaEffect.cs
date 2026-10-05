using System.Collections;
using Game.Combat;
using Game.Core.Combat;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Arremessa até o ponto da mira (Granada de Cristal Rachado). O ponto fica limitado a maxRange;
    /// depois de flightTime o cristal racha e fere em área.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Throw Area", fileName = "ThrowAreaEffect")]
    public class ThrowAreaEffect : CardEffect
    {
        [Min(0f)] public float damage = 40f;
        [Range(0f, 1f)] public float arcaneFraction = 0.6f;
        [Tooltip("Raio da explosão (m).")]
        [Min(0.5f)] public float radius = 3f;
        [Tooltip("Alcance máximo do arremesso a partir do jogador (m).")]
        [Min(1f)] public float maxRange = 10f;
        [Tooltip("Tempo de voo até explodir (s).")]
        [Min(0.05f)] public float flightTime = 0.6f;

        // De onde a granada sai (altura da mão).
        private const float HandHeight = 1.2f;

        public override void Execute(ICardUser user, CardData card)
        {
            Vector3 origin = user.Transform.position;
            Vector3 flat = user.AimPoint - origin;
            flat.y = 0f;
            float distance = Mathf.Min(flat.magnitude, maxRange);
            Vector3 dir = distance > 0.01f ? flat / flat.magnitude : user.AimDirection;
            Vector3 landing = origin + dir * distance;

            // No visual "grenade_throw", a direção é o vetor até o ponto de queda e o tamanho é o tempo de voo.
            Vector3 start = origin + Vector3.up * HandHeight;
            user.BroadcastVisual("grenade_throw", start, landing - start, flightTime);
            user.Run(Land(user, landing, user.Potency)); // a qualidade é a do momento do arremesso (D-048)
        }

        private IEnumerator Land(ICardUser user, Vector3 landing, float potency)
        {
            yield return new WaitForSeconds(flightTime);

            var packet = new DamagePacket(damage * potency, arcaneFraction); // qualidade da carta (D-048)
            foreach (IDamageable target in user.EnemiesInRadius(landing, radius))
                target.ServerApplyDamage(packet, user.ClientId);
            user.BroadcastVisual("grenade_explode", landing + Vector3.up * 0.1f, Vector3.up, radius);
        }
    }
}
