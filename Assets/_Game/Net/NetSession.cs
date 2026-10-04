using System;
using System.Collections.Generic;
using System.Text;
using Game.Arena;
using Game.Core.Session;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Liga o NetworkManager às regras do jogo: hospedar ou entrar por IP na LAN,
    /// aprovar quem entra (SessionRoster) e escolher a vaga de spawn.
    /// O host é a única fonte da verdade.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class NetSession : MonoBehaviour
    {
        [SerializeField] private NetSettings settings;
        [SerializeField] private PlayerSpawnPoints spawnPoints;
        [SerializeField] private MatchState matchState;

        private readonly Dictionary<ulong, string> tokensByClient = new Dictionary<ulong, string>();
        private NetworkManager manager;
        private UnityTransport transport;
        private SessionRoster roster;
        private bool connectedOnce;
        private bool leaving;

        /// <summary>A sessão terminou neste jogo. O texto é o motivo curto para a tela de conexão.</summary>
        public event Action<string> Ended;

        /// <summary>Esta máquina virou host ou entrou como cliente.</summary>
        public event Action Connected;

        public NetSettings Settings => settings;
        public bool IsRunning => manager != null && manager.IsListening;

        private void Awake()
        {
            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnect;
            manager.OnClientStopped += OnClientStopped;
            if (matchState != null)
                matchState.Started += OnSessionStarted;
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.ConnectionApprovalCallback = null;
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnClientDisconnectCallback -= OnClientDisconnect;
                manager.OnClientStopped -= OnClientStopped;
            }
            if (matchState != null)
                matchState.Started -= OnSessionStarted;
        }

        public bool Host()
        {
            roster = new SessionRoster(settings.maxPlayers);
            tokensByClient.Clear();
            connectedOnce = false;
            transport.SetConnectionData("127.0.0.1", settings.port, "0.0.0.0");
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ClientIdentity.Token);
            return manager.StartHost();
        }

        public bool Join(string ip)
        {
            connectedOnce = false;
            transport.SetConnectionData(ip.Trim(), settings.port);
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ClientIdentity.Token);
            return manager.StartClient();
        }

        public void Leave()
        {
            if (manager.IsListening)
            {
                leaving = true;
                manager.Shutdown();
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string token = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : string.Empty;
            int slot = -1;
            AdmitResult result = string.IsNullOrEmpty(token)
                ? AdmitResult.AlreadyConnected
                : roster.TryAdmit(token, out slot);

            response.Approved = result == AdmitResult.Admitted;
            Debug.Log($"[NetSession] pedido do cliente {request.ClientNetworkId}: {result}, vaga {slot}");
            if (!response.Approved)
            {
                response.Reason = ReasonText(result);
                return;
            }

            tokensByClient[request.ClientNetworkId] = token;

            Transform spawn = spawnPoints != null ? spawnPoints.Get(slot) : transform;
            response.CreatePlayerObject = true;
            response.Position = spawn.position;
            response.Rotation = spawn.rotation;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId == manager.LocalClientId)
            {
                connectedOnce = true;
                Connected?.Invoke();
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (!manager.IsServer)
                return;
            if (tokensByClient.TryGetValue(clientId, out string token))
            {
                tokensByClient.Remove(clientId);
                roster.Disconnect(token);
                Debug.Log($"[NetSession] cliente {clientId} saiu");
            }
        }

        private void OnClientStopped(bool wasHost)
        {
            string reason = manager.DisconnectReason;
            // O NGO preenche o motivo com texto técnico em quedas de transporte; ignora e usa o texto amigável.
            if (string.IsNullOrEmpty(reason) || reason.StartsWith("[Disconnect Event]", StringComparison.Ordinal))
                reason = leaving || wasHost ? string.Empty : connectedOnce ? "O host encerrou a sessão." : "Não foi possível conectar.";
            leaving = false;
            Ended?.Invoke(reason);
        }

        private void OnSessionStarted()
        {
            if (manager.IsServer)
                roster.Start();
        }

        private static string ReasonText(AdmitResult result) => result switch
        {
            AdmitResult.Full => "Sala cheia.",
            AdmitResult.AlreadyStarted => "A partida já começou.",
            AdmitResult.AlreadyConnected => "Este jogo já está conectado.",
            _ => "Entrada recusada."
        };
    }
}
