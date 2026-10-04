namespace Game.Net
{
    /// <summary>Algo do mundo que o jogador aciona com E. Só roda no host, que valida.</summary>
    public interface IInteractable
    {
        void ServerInteract(ulong requesterClientId);
    }
}
