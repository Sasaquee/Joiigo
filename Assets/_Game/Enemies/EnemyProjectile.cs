using Game.Combat;
using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Orbe lento e esquivável do drone (D-025). Só o host move e decide o acerto; os clientes recebem a
    /// posição pelo NetworkTransform (autoridade do host) e desenham o orbe com luz e rastro.
    /// </summary>
    public class EnemyProjectile : NetworkBehaviour
    {
        private static readonly Collider[] overlapBuffer = new Collider[16];

        private float damage;
        private float arcaneFraction;
        private float speed;
        private float radius = 0.35f;
        private float lifetime = 3f;
        private Vector3 direction = Vector3.forward;
        private float age;
        private bool finished;

        /// <summary>Host: chamar antes do Spawn().</summary>
        public void ServerInit(float damage, float arcaneFraction, float speed, float radius, float lifetime, Vector3 direction)
        {
            this.damage = damage;
            this.arcaneFraction = arcaneFraction;
            this.speed = speed;
            this.radius = radius;
            this.lifetime = lifetime;
            direction.y = 0f;
            this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        public override void OnNetworkSpawn()
        {
            finished = false;
            age = 0f;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || finished)
                return;

            float dt = Time.deltaTime;
            age += dt;
            transform.position += direction * (speed * dt);

            if (age >= lifetime || HitPlayer() || HitGeometry())
            {
                finished = true;
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>Esfera contra o círculo de cada jogador vivo, no plano. Acerta no máximo um, uma vez.</summary>
        private bool HitPlayer()
        {
            Vector3 p = transform.position;
            var clients = NetworkManager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                if (!EnemyTargets.TryGetAlivePlayer(clients[i], out var victim))
                    continue;
                Vector3 v = victim.transform.position;
                float d = new Vector2(v.x - p.x, v.z - p.z).magnitude;
                if (d > radius + victim.Radius)
                    continue;

                victim.ServerApplyDamage(new DamagePacket(damage, arcaneFraction), NetworkManager.ServerClientId);
                return true;
            }
            return false;
        }

        /// <summary>Some ao bater em pilar, parede ou máquina. Ignora gente (CharacterControllers), gatilhos e restos soltos.</summary>
        private bool HitGeometry()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = overlapBuffer[i];
                if (c.attachedRigidbody != null || c is CharacterController)
                    continue;
                if (c.GetComponentInParent<NetworkHealth>() != null)
                    continue;
                return true;
            }
            return false;
        }
    }
}
