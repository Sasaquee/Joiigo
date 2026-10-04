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

        public WaveDirector CreateDirector()
        {
            var specs = new List<WaveSpec>();
            foreach (var w in waves)
                specs.Add(new WaveSpec(w.counts));
            return new WaveDirector(specs, pauseBetweenWaves, firstWaveDelay);
        }
    }
}
