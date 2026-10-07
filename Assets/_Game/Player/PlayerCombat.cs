using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Combat;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Math;
using Unity.Netcode;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Golpe em arco do jogador (D-020). O dono só envia o ponto de mira; o host valida (vivo, recarga),
    /// recalcula a direção a partir da própria posição e aplica o dano nos inimigos dentro do arco.
    /// Todos veem o golpe pelo SwingRpc.
    /// </summary>
    public class PlayerCombat : NetworkBehaviour
    {
        // Folga técnica da busca de colisores; o acerto de verdade é o ArcHit com o raio do alvo.
        private const float MaxTargetRadius = 3f;

        [SerializeField] private CombatSettings settings;

        private readonly Cooldown cooldown = new Cooldown();
        private readonly Collider[] overlap = new Collider[64];
        private readonly List<IDamageable> hits = new List<IDamageable>();
        private PlayerLife life;
        private PlayerInputReader reader;
        private PlayerCards cards; // pode faltar: sem cartas o golpe usa só o CombatSettings
        private PlayerBlessing blessing; // pode faltar: sem bênção o dano não muda (D-085)

        public CombatSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Disparado em todos os clientes quando o golpe aparece (só visual). Direção no mundo (XZ).</summary>
        public event System.Action<Vector3> SwingPlayed;

        private void Awake()
        {
            life = GetComponent<PlayerLife>();
            reader = GetComponent<PlayerInputReader>();
            cards = GetComponent<PlayerCards>();
            blessing = GetComponent<PlayerBlessing>();
        }

        /// <summary>Alcance do golpe com os modificadores das cartas (Manopla Pistonada).</summary>
        private float CurrentRange() =>
            Mathf.Max(0.1f, settings.basicRange + (cards != null ? cards.Modifiers.Get(ModifierKind.BasicRange) : 0f));

        public override void OnNetworkSpawn()
        {
            if (IsOwner && reader != null)
                reader.AttackPressed += RequestAttack;
        }

        public override void OnNetworkDespawn()
        {
            if (reader != null)
                reader.AttackPressed -= RequestAttack;
        }

        private void Update()
        {
            if (IsServer)
                cooldown.Tick(Time.deltaTime);
        }

        /// <summary>Dono: pede o golpe na direção do ponto de mira (mundo). No host vale na hora.</summary>
        public void RequestAttack(Vector3 aimPoint)
        {
            if (!IsSpawned || !IsFinite(aimPoint))
                return;
            if (life != null && !life.CanAct)
                return;

            if (IsServer)
                ServerAttack(aimPoint);
            else
                AttackRpc(aimPoint);
        }

        [Rpc(SendTo.Server)]
        private void AttackRpc(Vector3 aimPoint, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !IsFinite(aimPoint))
                return;
            ServerAttack(aimPoint);
        }

        private void ServerAttack(Vector3 aimPoint)
        {
            if (settings == null || (life != null && !life.CanAct))
                return;
            if (!cooldown.TryUse(settings.basicCooldown))
                return;

            // A direção sai da posição do host, não da do cliente.
            Vector3 p = transform.position;
            var dir = new Vector3(aimPoint.x - p.x, 0f, aimPoint.z - p.z);
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward;
            dir.y = 0f;
            dir.Normalize();

            SwingRpc(dir, CurrentRange());
            StartCoroutine(HitAfterDelay(dir));
        }

        private IEnumerator HitAfterDelay(Vector3 dir)
        {
            if (settings.basicHitDelay > 0f)
                yield return new WaitForSeconds(settings.basicHitDelay);
            if (life != null && !life.CanAct)
                yield break; // caiu no meio do golpe
            ServerResolveHits(dir);
        }

        private void ServerResolveHits(Vector3 dir)
        {
            Vector3 p = transform.position;
            var origin = new Float2(p.x, p.z);
            var facing = new Float2(dir.x, dir.z);

            float range = CurrentRange();
            int count = Physics.OverlapSphereNonAlloc(p, range + MaxTargetRadius, overlap, ~0,
                QueryTriggerInteraction.Collide);

            hits.Clear();
            for (int i = 0; i < count; i++)
            {
                IDamageable target = overlap[i].GetComponentInParent<IDamageable>();
                if (target == null || hits.Contains(target) || !target.IsAlive)
                    continue;
                if (target.Transform.GetComponentInParent<PlayerCombat>() != null)
                    continue; // jogador (ele mesmo ou aliado)

                Vector3 t = target.Transform.position;
                if (!ArcHit.IsInArc(origin, facing, new Float2(t.x, t.z), range,
                        settings.basicHalfAngle, target.Radius))
                    continue;

                hits.Add(target);
            }

            DamagePacket packet = BuildPacket();
            foreach (IDamageable target in hits)
                target.ServerApplyDamage(packet, OwnerClientId);

            // Uma vez por golpe que acertou: energia (D-030) e gasto do reforço da Mola de Recuo.
            if (hits.Count > 0 && cards != null)
                cards.ServerOnBasicHit();
            hits.Clear();
        }

        /// <summary>
        /// Dano do golpe com as cartas (BasicHitMath, no Core): o deslocamento arcano (Lente) vale para o dano base; o dano extra
        /// (Manopla, ou negativo nas passivas que o cortam) é mecânico; a Mola de Recuo, o multiplicador percentual
        /// (BasicDamageMultiplier) e a bênção do 20 no coop (D-085) multiplicam por cima, sem mudar a mistura
        /// mecânico/arcano. Nunca abaixo de 1.
        /// </summary>
        private DamagePacket BuildPacket()
        {
            float blessed = blessing != null ? blessing.ServerMultiplier : 1f;
            if (cards == null)
                return BasicHitMath.Compute(settings.basicDamage, settings.basicArcaneFraction, null, 0f, blessed);
            return BasicHitMath.Compute(settings.basicDamage, settings.basicArcaneFraction, cards.Modifiers, cards.ServerHurtBonus, blessed);
        }

        [Rpc(SendTo.Everyone)]
        private void SwingRpc(Vector3 dir, float range)
        {
            if (settings == null)
                return;
            SwingVisual.Play(transform.position, dir, range, settings.basicHalfAngle);
            SwingPlayed?.Invoke(dir);
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
