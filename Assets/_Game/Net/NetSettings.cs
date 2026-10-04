using UnityEngine;

namespace Game.Net
{
    /// <summary>Números provisórios da rede e da previsão de movimento.</summary>
    [CreateAssetMenu(menuName = "Game/Net/Net Settings", fileName = "NetSettings")]
    public class NetSettings : ScriptableObject
    {
        [Tooltip("Porta UDP usada na LAN.")]
        public ushort port = 7777;

        [Tooltip("Máximo de jogadores na sessão (§7.2: 2 a 4).")]
        [Range(1, 4)] public int maxPlayers = 4;

        [Tooltip("Intenções de movimento enviadas por segundo pelo cliente.")]
        [Min(1f)] public float intentSendRate = 30f;

        [Tooltip("Erro de previsão ignorado (m). Abaixo disso o cliente não é corrigido.")]
        [Min(0f)] public float correctionDeadZone = 0.15f;

        [Tooltip("Erro a partir do qual o cliente é teleportado em vez de corrigido aos poucos (m).")]
        [Min(0f)] public float snapDistance = 2f;

        [Tooltip("Suavização dos outros jogadores na tela. Maior = segue mais colado.")]
        [Min(0.1f)] public float remoteSmoothing = 15f;
    }
}
