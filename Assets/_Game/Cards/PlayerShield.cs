using System.Collections.Generic;
using Game.Combat;
using Game.Core.Combat;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Escudo ativo do jogador (Broquel Cantante). A carta liga o escudo por alguns segundos; durante
    /// esse tempo os projéteis inimigos que chegam pela frente são bloqueados e devolvem um pulso arcano.
    /// Golpes corpo a corpo não são bloqueados. Só o host consulta (o EnemyProjectile roda no host).
    /// </summary>
    public class PlayerShield : MonoBehaviour
    {
        private static readonly List<PlayerShield> shields = new List<PlayerShield>();
        private static readonly List<IDamageable> found = new List<IDamageable>();

        private ICardUser user;
        private NetworkHealth health;
        private float endTime = -1f;
        private float pulseDamage;
        private float pulseArcane;
        private float pulseRadius = 3f;
        private float reach = 1.5f;
        private float halfAngle = 65f;

        public bool IsActive => Time.time < endTime && (health == null || health.IsAlive);

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
        }

        private void OnEnable() => shields.Add(this);

        private void OnDisable() => shields.Remove(this);

        /// <summary>Host: liga o escudo. reach e halfAngle definem a zona à frente que bloqueia.</summary>
        public void Activate(ICardUser owner, float duration, float pulseDamage, float pulseArcane,
            float pulseRadius, float reach, float halfAngle)
        {
            user = owner;
            endTime = Time.time + duration;
            this.pulseDamage = pulseDamage;
            this.pulseArcane = pulseArcane;
            this.pulseRadius = pulseRadius;
            this.reach = reach;
            this.halfAngle = halfAngle;
        }

        /// <summary>Host: apaga o escudo na hora (recomeço da partida, D-084).</summary>
        public void Deactivate() => endTime = -1f;

        /// <summary>Host: o ponto (um projétil de raio pointRadius) está na zona do escudo? Se sim, bloqueia e devolve o pulso.</summary>
        public bool TryBlockPoint(Vector3 point, float pointRadius)
        {
            if (!IsActive)
                return false;

            Vector3 p = transform.position;
            var d = new Vector3(point.x - p.x, 0f, point.z - p.z);
            float distance = d.magnitude;
            if (distance > reach + pointRadius)
                return false;

            if (distance > 0.05f)
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                    return false;
                float cos = Vector3.Dot(forward.normalized, d / distance);
                if (cos < Mathf.Cos(halfAngle * Mathf.Deg2Rad))
                    return false; // pelo lado ou por trás
            }

            Pulse();
            return true;
        }

        /// <summary>Host: algum escudo ativo bloqueia este projétil? Chamado pelo EnemyProjectile a cada quadro.</summary>
        public static bool TryBlock(Vector3 point, float pointRadius)
        {
            for (int i = 0; i < shields.Count; i++)
                if (shields[i].TryBlockPoint(point, pointRadius))
                    return true;
            return false;
        }

        private void Pulse()
        {
            Vector3 p = transform.position;
            if (user != null && pulseDamage > 0f)
            {
                CardTargets.Collect(p, pulseRadius, found);
                var packet = new DamagePacket(pulseDamage, pulseArcane);
                foreach (IDamageable target in found)
                    target.ServerApplyDamage(packet, user.ClientId);
                found.Clear();
            }

            user?.BroadcastVisual("pulse", p + Vector3.up * 0.1f, transform.forward, pulseRadius);
        }
    }
}
