using System;
using System.Collections.Generic;
using Game.Core.AI;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Números provisórios das ondas (D-023).</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Wave Settings", fileName = "WaveSettings")]
    public class WaveSettings : ScriptableObject
    {
        [Serializable]
        public class Wave
        {
            [Tooltip("Quantidade de cada tipo, na mesma ordem de enemyTypes.")]
            public int[] counts = new int[0];
        }

        [Tooltip("Tipos de inimigo, na ordem usada pelas ondas.")]
        public EnemyDefinition[] enemyTypes = new EnemyDefinition[0];
        public List<Wave> waves = new List<Wave>();

        [Tooltip("Espera depois da largada até a primeira onda (s).")]
        [Min(0f)] public float firstWaveDelay = 3f;
        [Tooltip("Pausa depois que uma onda acaba (s). Espaço para cartas.")]
        [Min(0f)] public float pauseBetweenWaves = 6f;

        [Header("Bocas das ruas (D-077)")]
        [Tooltip("A boca com jogador vivo a menos disto é pulada no rodízio (m). Se todas estiverem assim, usa a mais longe.")]
        [Min(0f)] public float mouthPlayerClearance = 12f;
        [Tooltip("Dois inimigos da mesma boca nascem com pelo menos esta diferença (s), para os corpos não nascerem um dentro do outro.")]
        [Min(0f)] public float spawnStagger = 0.5f;
        [Tooltip("Onde nasce em volta do marcador da boca: fração do raio do marcador (0 = no centro).")]
        [Range(0f, 1f)] public float mouthSpawnSpread = 0.6f;
        [Tooltip("Raio para puxar a posição sorteada até o chão andável da NavMesh (m). Se não achar, usa o marcador.")]
        [Min(0.1f)] public float mouthNavSampleRadius = 2f;

        public WaveDirector CreateDirector()
        {
            var specs = new List<WaveSpec>();
            foreach (var w in waves)
                specs.Add(new WaveSpec(w.counts));
            return new WaveDirector(specs, pauseBetweenWaves, firstWaveDelay);
        }
    }
}
