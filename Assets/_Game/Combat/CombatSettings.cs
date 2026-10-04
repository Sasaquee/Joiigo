using UnityEngine;

namespace Game.Combat
{
    /// <summary>Números provisórios do combate do jogador (D-020, D-021, D-022).</summary>
    [CreateAssetMenu(menuName = "Game/Combat/Combat Settings", fileName = "CombatSettings")]
    public class CombatSettings : ScriptableObject
    {
        [Header("Vida")]
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("Tempo caído antes de voltar no spawn (s).")]
        [Min(0f)] public float downedDuration = 5f;

        [Header("Golpe em arco (botão esquerdo)")]
        [Min(0f)] public float basicDamage = 20f;
        [Tooltip("Parte arcana do golpe (0 = puro mecânico, 1 = puro arcano).")]
        [Range(0f, 1f)] public float basicArcaneFraction = 0.2f;
        [Tooltip("Alcance do golpe a partir do centro do personagem (m).")]
        [Min(0f)] public float basicRange = 2.2f;
        [Tooltip("Meia-abertura do arco (graus). 60 = arco de 120°.")]
        [Range(5f, 180f)] public float basicHalfAngle = 60f;
        [Tooltip("Tempo entre golpes (s).")]
        [Min(0f)] public float basicCooldown = 0.45f;
        [Tooltip("Atraso entre o clique e o acerto, para o golpe ter peso (s).")]
        [Min(0f)] public float basicHitDelay = 0.1f;
        [Tooltip("Raio do corpo do jogador para ser acertado (m).")]
        [Min(0.1f)] public float bodyRadius = 0.45f;
    }
}
