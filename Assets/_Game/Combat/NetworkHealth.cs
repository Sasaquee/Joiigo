using System;
using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>Resistências editáveis no Inspector (0 = nada, 1 = corta tudo).</summary>
    [Serializable]
    public struct ResistanceValues
    {
        [Range(0f, 1f)] public float mechanical;
        [Range(0f, 1f)] public float arcane;

        public Resistances ToCore() => new Resistances(mechanical, arcane);
    }

    /// <summary>Algo que recebe dano. Só o host chama.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }
        float Radius { get; }
        float ServerApplyDamage(DamagePacket packet, ulong attackerClientId);
    }

    /// <summary>
    /// Vida em rede, de jogador ou inimigo. O host calcula (Core) e publica;
    /// os outros só leem para desenhar (aura na Fase 7, flash de acerto agora).
    /// </summary>
    public class NetworkHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private float radius = 0.5f;

        private readonly NetworkVariable<float> current = new NetworkVariable<float>(1f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> max = new NetworkVariable<float>(1f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private HealthModel model;
        private Resistances resistances = Resistances.None;

        /// <summary>Em todos: dano recebido (quantidade efetiva). Para flash de acerto e som.</summary>
        public event Action<float> Damaged;

        /// <summary>Só no host: a vida chegou a zero.</summary>
        public event Action Depleted;

        public float Current => current.Value;
        public float Max => max.Value;
        public float Fraction => max.Value > 0f ? current.Value / max.Value : 0f;
        public bool IsAlive => current.Value > 0f;
        public Transform Transform => transform;
        public float Radius => radius;

        /// <summary>Host: define vida e resistências. Chamar antes ou logo depois do spawn.</summary>
        public void ServerInitialize(float maxHealth, Resistances resist, float bodyRadius)
        {
            model = new HealthModel(maxHealth);
            resistances = resist;
            radius = bodyRadius;
            if (IsServer)
                Publish();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                model ??= new HealthModel(max.Value > 1f ? max.Value : 100f);
                Publish();
            }
        }

        public float ServerApplyDamage(DamagePacket packet, ulong attackerClientId)
        {
            if (!IsServer || model == null || model.IsDepleted)
                return 0f;

            float applied = model.ApplyDamage(DamageCalculator.Compute(packet, resistances));
            if (applied <= 0f)
                return 0f;

            Publish();
            DamagedRpc(applied);
            if (model.IsDepleted)
                Depleted?.Invoke();
            return applied;
        }

        public void ServerHeal(float amount)
        {
            if (!IsServer || model == null)
                return;
            model.Heal(amount);
            Publish();
        }

        public void ServerRestore()
        {
            if (!IsServer || model == null)
                return;
            model.Restore();
            Publish();
        }

        private void Publish()
        {
            max.Value = model.Max;
            current.Value = model.Current;
        }

        [Rpc(SendTo.Everyone)]
        private void DamagedRpc(float applied)
        {
            Damaged?.Invoke(applied);
        }
    }
}
