using Game.Combat;
using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Cair e voltar (D-003, D-022). HP zero não mata: o jogador fica caído por downedDuration
    /// e volta no spawn com a vida cheia. Só o host decide; todos veem o corpo deitar.
    /// </summary>
    public class PlayerLife : NetworkBehaviour
    {
        // Só visual: quão deitado fica o corpo e quão rápido ele cai.
        private const float FallAngle = 85f;
        private const float FallSmoothing = 10f;

        [SerializeField] private CombatSettings settings;
        [Tooltip("Filho que deita quando o jogador cai (o Corpo).")]
        [SerializeField] private Transform body;

        private readonly NetworkVariable<bool> downed = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private PlayerMotor motor;
        private CharacterController controller;
        private DownedState downedState;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool pendingHealthInit;

        public bool IsDowned => downed.Value;

        /// <summary>Pode andar e atacar? O host ignora as intenções de quem está caído.</summary>
        public bool CanAct => !downed.Value;

        public CombatSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            motor = GetComponent<PlayerMotor>();
            controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                spawnPosition = transform.position;
                spawnRotation = transform.rotation;
                downedState = null;
                downed.Value = false;
                // Se o NetworkHealth ainda não nasceu (ordem dos componentes), inicializa no primeiro Update.
                pendingHealthInit = true;
                TryInitializeHealth();
                health.Depleted += OnDepleted;
            }

            if (body != null)
                body.localRotation = TargetRotation();
        }

        public override void OnNetworkDespawn()
        {
            if (health != null)
                health.Depleted -= OnDepleted;
        }

        private void TryInitializeHealth()
        {
            if (!pendingHealthInit || settings == null || !health.IsSpawned)
                return;
            pendingHealthInit = false;
            health.ServerInitialize(settings.maxHealth, Resistances.None, settings.bodyRadius);
        }

        private void Update()
        {
            if (IsSpawned && IsServer)
                TryInitializeHealth();
            if (IsSpawned && IsServer && downedState != null && downedState.Tick(Time.deltaTime))
                Respawn();

            if (body != null)
            {
                float t = 1f - Mathf.Exp(-FallSmoothing * Time.deltaTime);
                body.localRotation = Quaternion.Slerp(body.localRotation, TargetRotation(), t);
            }
        }

        private Quaternion TargetRotation() => downed.Value ? Quaternion.Euler(FallAngle, 0f, 0f) : Quaternion.identity;

        private void OnDepleted()
        {
            // O tempo é lido na queda, então trocar o CombatSettings vale a partir da próxima queda.
            downedState = new DownedState(settings != null ? settings.downedDuration : 5f);
            downedState.Fall();
            downed.Value = true;
            if (motor != null)
                motor.SetIntent(Vector3.zero, null);
        }

        private void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;

            health.ServerRestore();
            downed.Value = false;
        }
    }
}
