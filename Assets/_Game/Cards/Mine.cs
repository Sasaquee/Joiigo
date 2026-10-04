using System.Collections.Generic;
using Game.Combat;
using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Mina de engrenagem (objeto de rede). Só o host decide: dispara quando um inimigo entra no raio de
    /// gatilho (depois do tempo de armar) ou quando o pavio acaba, fere em área e some. Os clientes só
    /// veem a malha e o visual da explosão.
    /// </summary>
    public class Mine : NetworkBehaviour
    {
        // Depois de explodir, o objeto espera um instante para o clarão chegar a todos antes de sumir.
        private const float DespawnDelay = 0.3f;

        private readonly List<IDamageable> found = new List<IDamageable>();

        private float damage = 45f;
        private float arcaneFraction = 0.4f;
        private float areaRadius = 2.5f;
        private float triggerRadius = 1.3f;
        private float fuse = 10f;
        private float armDelay = 0.3f;
        private ulong ownerClientId;

        private float age;
        private bool exploded;
        private bool despawning;
        private float despawnTimer;

        /// <summary>Host: chamar antes do Spawn().</summary>
        public void ServerInit(float damage, float arcaneFraction, float areaRadius, float triggerRadius,
            float fuse, float armDelay, ulong ownerClientId)
        {
            this.damage = damage;
            this.arcaneFraction = arcaneFraction;
            this.areaRadius = areaRadius;
            this.triggerRadius = triggerRadius;
            this.fuse = fuse;
            this.armDelay = armDelay;
            this.ownerClientId = ownerClientId;
        }

        public override void OnNetworkSpawn()
        {
            age = 0f;
            exploded = false;
            despawning = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || despawning)
                return;

            float dt = Time.deltaTime;
            if (exploded)
            {
                despawnTimer -= dt;
                if (despawnTimer <= 0f)
                {
                    despawning = true;
                    NetworkObject.Despawn(true);
                }
                return;
            }

            age += dt;
            bool trigger = age >= fuse;
            if (!trigger && age >= armDelay)
            {
                CardTargets.Collect(transform.position, triggerRadius, found);
                trigger = found.Count > 0;
            }

            if (trigger)
                Explode();
        }

        private void Explode()
        {
            exploded = true;
            despawnTimer = DespawnDelay;

            Vector3 position = transform.position;
            CardTargets.Collect(position, areaRadius, found);
            var packet = new DamagePacket(damage, arcaneFraction);
            foreach (IDamageable target in found)
                target.ServerApplyDamage(packet, ownerClientId);
            found.Clear();

            ExplodeRpc(position, areaRadius);
        }

        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(Vector3 position, float radius)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            foreach (var l in GetComponentsInChildren<Light>())
                l.enabled = false;
            CardVisuals.Play("mine_explode", position + Vector3.up * 0.1f, Vector3.up, radius);
        }
    }
}
