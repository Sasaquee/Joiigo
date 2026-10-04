using Game.Combat;
using Game.Core.Combat;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    /// <summary>Alvo de teste: só soma o dano que recebe (sem resistência).</summary>
    public class DummyTarget : MonoBehaviour, IDamageable
    {
        public float Taken;
        public bool IsAlive => true;
        public Transform Transform => transform;
        public float Radius => 0.4f;

        public float ServerApplyDamage(DamagePacket packet, ulong attackerClientId)
        {
            float applied = DamageCalculator.Compute(packet, Resistances.None);
            Taken += applied;
            return applied;
        }
    }
}
