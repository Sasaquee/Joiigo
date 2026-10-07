using System.Collections.Generic;
using Game.Core.Combat;

namespace Game.Core.Session
{
    /// <summary>
    /// Queda total (D-084): quando TODOS os jogadores conectados estão caídos ao mesmo tempo, a partida recomeça.
    /// Regras puras; o host alimenta com o estado de vida de cada jogador.
    /// </summary>
    public static class WipeRule
    {
        /// <summary>
        /// Todos caídos? Lista vazia (ninguém conectado, jogador ainda não nasceu) não é queda total.
        /// Um jogador só (solo) vale: se ele cai, é queda total (D-084, "igual ao coop").
        /// </summary>
        public static bool AllDowned(IReadOnlyList<LifeState> players)
        {
            if (players == null || players.Count == 0)
                return false;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != LifeState.Downed)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Solo (D-018, D-087) = no máximo um jogador conectado no host. No solo a largada depois do recomeço é automática;
        /// com dois ou mais, todos esperam o host puxar a alavanca de novo.
        /// </summary>
        public static bool IsSolo(int connectedPlayers) => connectedPlayers <= 1;
    }

    /// <summary>
    /// Dispara uma vez por queda total. Alimente a cada quadro com o estado de todos os jogadores:
    /// devolve true só no quadro em que a lista passa de "alguém de pé" para "todos caídos". Enquanto a queda total
    /// continua, devolve false; ele rearma sozinho quando alguém volta a ficar de pé (ou a lista esvazia).
    /// </summary>
    public sealed class WipeDetector
    {
        /// <summary>A última lista alimentada era uma queda total.</summary>
        public bool IsWiped { get; private set; }

        public bool Update(IReadOnlyList<LifeState> players)
        {
            bool now = WipeRule.AllDowned(players);
            bool fired = now && !IsWiped;
            IsWiped = now;
            return fired;
        }

        /// <summary>Rearma na hora (depois do recomeço, quando todos acordam).</summary>
        public void Reset() => IsWiped = false;
    }
}
