namespace Game.Core.Dice
{
    /// <summary>Fonte de aleatoriedade injetada no Core (seed fixa nos testes; em jogo, uniforme).</summary>
    public interface IRandomSource
    {
        /// <summary>Inteiro uniforme em [minInclusive, maxExclusive).</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>IRandomSource sobre System.Random, com seed.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private readonly System.Random random;

        public SeededRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }
    }
}
