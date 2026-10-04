using Game.Cards;
using Unity.Netcode;

namespace Game.UI
{
    /// <summary>
    /// Acha as cartas do jogador local (o dono desta máquina) para a barra de skills e a tela de tiragem,
    /// que vivem na cena e não sabem quando o jogador nasce.
    /// </summary>
    public static class LocalPlayerCards
    {
        /// <summary>Cartas do jogador local, ou null se não há sessão, o jogador ainda não nasceu ou o prefab não tem PlayerCards.</summary>
        public static PlayerCards Get()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
                return null;

            NetworkObject player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            if (player == null)
                return null;

            PlayerCards cards = player.GetComponent<PlayerCards>();
            return cards != null && cards.IsSpawned ? cards : null;
        }
    }
}
