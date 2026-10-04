using Game.Cameras;
using Game.Core.Math;
using Game.Core.Movement;
using Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Jogador em rede, com autoridade do host.
    /// - Dono: lê o input, prevê o próprio movimento na hora (D-009) e envia intenções ao host.
    /// - Host: aplica as intenções de cada jogador e publica o estado.
    /// - Outros: só desenham o estado vindo do host, suavizado.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerInputReader))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private NetSettings netSettings;
        [SerializeField] private InteractionSettings interaction;
        [Tooltip("Marca no chão visível só para o dono (D-010).")]
        [SerializeField] private GameObject localMarker;

        private readonly NetworkVariable<PlayerNetState> state = new NetworkVariable<PlayerNetState>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly ReconciliationBuffer reconciliation = new ReconciliationBuffer();
        private PlayerMotor motor;
        private PlayerInputReader reader;
        private CharacterController controller;

        private Vector3 localMove;
        private Vector3? localAim;
        private uint nextSeq;
        private float sendTimer;

        private uint serverLastSeq;
        private Vector3 serverAckPosition;

        /// <summary>Último estado recebido do host (para debug e testes).</summary>
        public PlayerNetState ServerState => state.Value;

        public NetSettings NetSettings
        {
            get => netSettings;
            set => netSettings = value;
        }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            reader = GetComponent<PlayerInputReader>();
            controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            motor.enabled = IsServer || IsOwner;
            reader.enabled = IsOwner;
            if (localMarker != null)
                localMarker.SetActive(IsOwner);

            if (IsOwner)
                AttachCamera(transform);

            if (IsServer)
                PublishState();
            else
                state.OnValueChanged += OnStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            if (IsOwner)
                AttachCamera(null);
        }

        /// <summary>Chamado pelo leitor de input do dono.</summary>
        public void SubmitLocalIntent(Vector3 worldMove, Vector3? worldAim)
        {
            localMove = worldMove;
            localAim = worldAim;
            motor.SetIntent(worldMove, worldAim); // no host é o real; no cliente é a previsão
        }

        /// <summary>Tecla E do dono. O host procura o que está perto e valida.</summary>
        public void RequestInteract()
        {
            if (IsServer)
                ServerInteract();
            else
                InteractRpc();
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (IsOwner && !IsServer)
                SendIntentAtRate();

            if (IsServer)
                PublishState();
            else if (!IsOwner)
                FollowServerState();
        }

        // ---------- Dono (cliente) ----------

        private void SendIntentAtRate()
        {
            float interval = 1f / netSettings.intentSendRate;
            sendTimer += Time.deltaTime;
            if (sendTimer < interval)
                return;
            sendTimer = Mathf.Min(sendTimer - interval, interval); // mantém o ritmo sem acumular atraso

            uint seq = ++nextSeq;
            Vector3 p = transform.position;
            reconciliation.Record(seq, new Float2(p.x, p.z));
            SendIntentRpc(new PlayerIntent
            {
                Seq = seq,
                Move = new Vector2(localMove.x, localMove.z),
                Aim = localAim ?? Vector3.zero,
                HasAim = localAim.HasValue
            });
        }

        private void OnStateChanged(PlayerNetState previous, PlayerNetState current)
        {
            if (!IsOwner || current.AckSeq == 0)
                return;

            Float2 correction = reconciliation.Acknowledge(current.AckSeq,
                new Float2(current.AckPosition.x, current.AckPosition.z), netSettings.correctionDeadZone);
            if (correction.Length == 0f)
                return;

            var delta = new Vector3(correction.X, 0f, correction.Y);
            if (correction.Length >= netSettings.snapDistance)
                Teleport(transform.position + delta);
            else
                controller.Move(delta);
        }

        // ---------- Host ----------

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendIntentRpc(PlayerIntent intent, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || intent.Seq <= serverLastSeq)
                return;
            if (!float.IsFinite(intent.Move.x) || !float.IsFinite(intent.Move.y))
                return;
            if (intent.HasAim && (!float.IsFinite(intent.Aim.x) || !float.IsFinite(intent.Aim.y) || !float.IsFinite(intent.Aim.z)))
                return;

            serverLastSeq = intent.Seq;
            serverAckPosition = transform.position;

            Vector2 move = Vector2.ClampMagnitude(intent.Move, 1f);
            motor.SetIntent(new Vector3(move.x, 0f, move.y), intent.HasAim ? intent.Aim : (Vector3?)null);
        }

        [Rpc(SendTo.Server)]
        private void InteractRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                ServerInteract();
        }

        private void ServerInteract()
        {
            float radius = interaction != null ? interaction.interactRadius : 2.5f;
            IInteractable closest = null;
            float closestDistance = float.MaxValue;
            foreach (var hit in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var target = hit.GetComponentInParent<IInteractable>();
                if (target == null)
                    continue;
                float d = (hit.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (d < closestDistance)
                {
                    closestDistance = d;
                    closest = target;
                }
            }

            closest?.ServerInteract(OwnerClientId);
        }

        private void PublishState()
        {
            state.Value = new PlayerNetState
            {
                Tick = NetworkManager.ServerTime.Tick,
                Position = transform.position,
                Yaw = transform.eulerAngles.y,
                AckSeq = serverLastSeq,
                AckPosition = serverAckPosition
            };
        }

        // ---------- Outros jogadores ----------

        private void FollowServerState()
        {
            PlayerNetState s = state.Value;
            if ((s.Position - transform.position).sqrMagnitude > netSettings.snapDistance * netSettings.snapDistance * 4f)
            {
                Teleport(s.Position);
            }
            else
            {
                float t = 1f - Mathf.Exp(-netSettings.remoteSmoothing * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, s.Position, t);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, s.Yaw, 0f),
                1f - Mathf.Exp(-netSettings.remoteSmoothing * Time.deltaTime));
        }

        private void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        private static void AttachCamera(Transform target)
        {
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out CameraFollow follow))
                follow.Target = target;
        }
    }
}
