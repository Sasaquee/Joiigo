using Unity.Netcode;

namespace Game.Net
{
    /// <summary>Estado compartilhado da partida. Só o host escreve.</summary>
    public class MatchState : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> started = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsStarted => IsSpawned && started.Value;

        public event System.Action Started;

        public override void OnNetworkSpawn()
        {
            // Sessão nova sempre começa sem largada (o objeto da cena pode ter o valor da sessão anterior).
            if (IsServer)
                started.Value = false;
            started.OnValueChanged += OnStartedChanged;
            if (started.Value)
                Started?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            started.OnValueChanged -= OnStartedChanged;
        }

        /// <summary>Largada (D-013). Só tem efeito no host.</summary>
        public void ServerStart()
        {
            if (IsServer && !started.Value)
                started.Value = true;
        }

        private void OnStartedChanged(bool previous, bool current)
        {
            if (current && !previous)
                Started?.Invoke();
        }
    }
}
