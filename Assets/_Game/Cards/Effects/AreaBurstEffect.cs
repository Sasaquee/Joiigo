using Game.Combat;
using Game.Core.Combat;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>Explosão de vapor arcano em volta do jogador (A Chaminé Partida).</summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Area Burst", fileName = "AreaBurstEffect")]
    public class AreaBurstEffect : CardEffect
    {
        [Min(0f)] public float damage = 60f;
        [Range(0f, 1f)] public float arcaneFraction = 0.5f;
        [Tooltip("Raio da explosão a partir do jogador (m).")]
        [Min(0.5f)] public float radius = 5f;

        public override void Execute(ICardUser user, CardData card)
        {
            Vector3 center = user.Transform.position;
            var packet = new DamagePacket(damage, arcaneFraction);
            foreach (IDamageable target in user.EnemiesInRadius(center, radius))
                target.ServerApplyDamage(packet, user.ClientId);
            user.BroadcastVisual("burst", center + Vector3.up * 0.1f, Vector3.up, radius);
        }
    }
}
