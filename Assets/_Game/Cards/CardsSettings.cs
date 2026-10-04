using UnityEngine;

namespace Game.Cards
{
    /// <summary>Números provisórios de energia das cartas (D-030). Valores finais são do dono do design.</summary>
    [CreateAssetMenu(menuName = "Game/Cards/Cards Settings", fileName = "CardsSettings")]
    public class CardsSettings : ScriptableObject
    {
        [Header("Energia")]
        [Min(1f)] public float maxEnergy = 100f;
        [Tooltip("Energia recuperada por segundo, sempre.")]
        [Min(0f)] public float regenPerSecond = 4f;
        [Tooltip("Energia ganha por golpe básico que acerta (antes do bônus da Caldeira Interna).")]
        [Min(0f)] public float energyPerHit = 8f;
        [Tooltip("Fração da energia cheia com que o jogador nasce.")]
        [Range(0f, 1f)] public float startEnergyFraction = 1f;

        [Header("Passivas")]
        [Tooltip("Mola de Recuo: por quanto tempo depois de levar dano o próximo golpe sai reforçado (s).")]
        [Min(0f)] public float hurtBonusWindow = 3f;
    }
}
