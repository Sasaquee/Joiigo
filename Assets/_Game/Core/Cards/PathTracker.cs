using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Cards
{
    /// <summary>
    /// "Caminho" interno do jogador (§4.4): conta as tags das cartas que ele mais usa.
    /// Nunca aparece na tela, só no overlay de debug. Na Fase 6 entra no sorteio do tema.
    /// </summary>
    public class PathTracker
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Counts => counts;

        public void Record(IEnumerable<string> tags)
        {
            foreach (string tag in tags)
            {
                if (string.IsNullOrEmpty(tag))
                    continue;
                counts.TryGetValue(tag, out int c);
                counts[tag] = c + 1;
            }
        }

        /// <summary>As n tags mais usadas, da mais usada para a menos (empate em ordem alfabética).</summary>
        public IReadOnlyList<string> Top(int n) =>
            counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, System.StringComparer.Ordinal)
                .Take(n).Select(kv => kv.Key).ToList();
    }
}
