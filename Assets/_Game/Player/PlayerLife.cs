using Game.Combat;
using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Cair e voltar (D-003, D-022). HP zero não mata: o jogador fica caído por downedDuration
    /// e volta no spawn com a vida cheia. Só o host decide; todos veem o corpo deitar.
    /// Um aliado pode levantar quem caiu antes disso, no lugar da queda (D-083): quem decide é o PlayerRevive,
    /// que usa ServerRevive e publica aqui o progresso (a aura mostra) e se o aliado está parado levantando.
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

        // D-083: progresso (0 a 100, em passos de 1% para a rede falar pouco) de quem caiu e aliado parado levantando.
        private readonly NetworkVariable<byte> reviveProgress = new NetworkVariable<byte>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> reviving = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkHealth health;
        private PlayerMotor motor;
        private CharacterController controller;
        private DownedState downedState;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool pendingHealthInit;
        private float maxHealthBonus; // das cartas (MaxHealth); o PlayerCards avisa

        public bool IsDowned => downed.Value;

        /// <summary>Pode andar e atacar? O host ignora as intenções de quem está caído ou parado levantando um aliado (D-083).</summary>
        public bool CanAct => !downed.Value && !reviving.Value;

        /// <summary>Este jogador está parado levantando um aliado (D-083).</summary>
        public bool IsReviving => reviving.Value;

        /// <summary>Progresso de 0 a 1 de um aliado levantando este jogador caído (D-083). Sem número na tela: é a aura que mostra.</summary>
        public float ReviveProgress => reviveProgress.Value / 100f;

        /// <summary>Em todos: este jogador foi levantado por um aliado (D-083). A aura dá o pulso de fechamento.</summary>
        public event System.Action Revived;

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
                reviving.Value = false;
                reviveProgress.Value = 0;
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
            health.ServerInitialize(MaxHealthWithCards(), Resistances.None, settings.bodyRadius);
        }

        private float MaxHealthWithCards() => Mathf.Max(1f, settings.maxHealth + maxHealthBonus);

        /// <summary>
        /// Host: soma das cartas de vida máxima (MaxHealth) em uso. A vida máxima vira CombatSettings.maxHealth + bônus (nunca abaixo de 1)
        /// e a vida atual mantém a fração: equipar com a vida cheia deixa cheia, desequipar não mata.
        /// </summary>
        public void ServerSetMaxHealthBonus(float bonus)
        {
            if (!IsServer)
                return;
            maxHealthBonus = float.IsNaN(bonus) ? 0f : bonus;
            if (!pendingHealthInit && settings != null && health != null && health.IsSpawned)
                health.ServerSetMax(MaxHealthWithCards());
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
            reviving.Value = false; // quem cai deixa de levantar o outro
            if (motor != null)
                motor.SetIntent(Vector3.zero, null);
        }

        /// <summary>
        /// Host: um aliado terminou de levantar este jogador (D-083). Fica de pé NO LUGAR da queda, com a fração de vida
        /// dada, e o tempo de queda deixa de valer. Devolve false se não estava caído.
        /// </summary>
        public bool ServerRevive(float healthFraction)
        {
            if (!IsServer || downedState == null || downedState.State != LifeState.Downed)
                return false;

            downedState.Revive();
            health.ServerRestoreFraction(healthFraction);
            downed.Value = false;
            reviveProgress.Value = 0;
            RevivedRpc();
            return true;
        }

        /// <summary>Host: progresso de quem caiu sendo levantado (0 a 1), publicado em passos de 1%.</summary>
        public void ServerSetReviveProgress(float progress)
        {
            if (!IsServer)
                return;
            float p = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
            byte percent = (byte)Mathf.RoundToInt(p * 100f);
            if (reviveProgress.Value != percent)
                reviveProgress.Value = percent;
        }

        /// <summary>Host: este jogador está parado levantando um aliado (D-083); o host ignora a intenção dele.</summary>
        public void ServerSetReviving(bool value)
        {
            if (!IsServer || reviving.Value == value)
                return;
            reviving.Value = value;
            if (value && motor != null)
                motor.SetIntent(Vector3.zero, null); // para na hora, sem esperar a próxima intenção do dono
        }

        [Rpc(SendTo.Everyone)]
        private void RevivedRpc() => Revived?.Invoke();

        /// <summary>
        /// Host: recomeço da partida depois da queda total (D-084). Volta ao spawn de pé, com a vida cheia, sem tempo de queda
        /// pendente nem levantada em andamento. Quem chama (MatchReset) limpa as cartas e o resto.
        /// </summary>
        public void ServerResetForRestart()
        {
            if (!IsServer || !IsSpawned)
                return;

            downedState = null;
            reviving.Value = false;
            if (motor != null)
                motor.SetIntent(Vector3.zero, null);
            Respawn(); // spawn, vida cheia, de pé e progresso de levantar zerado
        }

        private void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;

            health.ServerRestore();
            downed.Value = false;
            reviveProgress.Value = 0;
        }
    }
}
