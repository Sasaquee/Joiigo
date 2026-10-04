using System.Collections.Generic;

namespace Game.Core.AI
{
    /// <summary>Uma onda: quantos inimigos de cada tipo (índice do tipo → quantidade).</summary>
    public class WaveSpec
    {
        public readonly int[] CountsPerType;

        public WaveSpec(params int[] countsPerType)
        {
            CountsPerType = countsPerType;
        }
    }

    /// <summary>
    /// Ondas com pausa (D-023), no host. A próxima onda só começa depois que a anterior acabou
    /// (nenhum inimigo vivo) e a pausa passou. Depois da última, recomeça da primeira.
    /// </summary>
    public class WaveDirector
    {
        private readonly IReadOnlyList<WaveSpec> waves;
        private readonly float pauseBetweenWaves;
        private float pauseTimer;
        private bool waitingForClear;

        public WaveDirector(IReadOnlyList<WaveSpec> waves, float pauseBetweenWaves, float firstWaveDelay)
        {
            this.waves = waves;
            this.pauseBetweenWaves = pauseBetweenWaves;
            pauseTimer = firstWaveDelay;
        }

        /// <summary>Índice da próxima onda a sair.</summary>
        public int NextWaveIndex { get; private set; }

        /// <summary>Ondas já soltas desde o começo.</summary>
        public int WavesReleased { get; private set; }

        public bool InPause => !waitingForClear;

        /// <summary>
        /// Avança. Quando uma onda sai, preenche spawns com o índice do tipo de cada inimigo
        /// a gerar e devolve true.
        /// </summary>
        public bool Tick(float deltaTime, int aliveEnemies, List<int> spawns)
        {
            spawns.Clear();
            if (waves.Count == 0)
                return false;

            if (waitingForClear)
            {
                if (aliveEnemies > 0)
                    return false;
                waitingForClear = false;
                pauseTimer = pauseBetweenWaves;
            }

            pauseTimer -= deltaTime;
            if (pauseTimer > 0f)
                return false;

            WaveSpec wave = waves[NextWaveIndex];
            for (int type = 0; type < wave.CountsPerType.Length; type++)
                for (int i = 0; i < wave.CountsPerType[type]; i++)
                    spawns.Add(type);

            NextWaveIndex = (NextWaveIndex + 1) % waves.Count;
            WavesReleased++;
            waitingForClear = true;
            return true;
        }
    }
}
