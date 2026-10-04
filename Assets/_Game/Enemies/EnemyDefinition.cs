using Game.Combat;
using Game.Core.AI;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Um tipo de inimigo. Criar um inimigo novo = criar um asset destes, sem código.
    /// Nomes e comportamentos finais são perguntas de design (P-006); estes são provisórios.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Identificador interno. Nunca aparece na tela.")]
        public string id = "inimigo";
        public GameObject prefab;

        [Header("Corpo")]
        [Min(1f)] public float maxHealth = 60f;
        [Min(0.1f)] public float bodyRadius = 0.6f;
        public ResistanceValues resistances;

        [Header("Movimento")]
        [Min(0f)] public float moveSpeed = 3.5f;
        [Min(0f)] public float turnSpeed = 360f;

        [Header("Ataque")]
        [Min(0f)] public float attackRange = 1.6f;
        [Tooltip("Recua se o alvo chegar mais perto que isto (drone). 0 = nunca recua.")]
        [Min(0f)] public float minRange;
        [Tooltip("Aviso antes do golpe (D-024).")]
        [Min(0f)] public float windupTime = 0.6f;
        [Min(0f)] public float recoverTime = 0.8f;
        [Min(0f)] public float damage = 12f;
        [Range(0f, 1f)] public float arcaneFraction = 0.1f;
        [Tooltip("Meia-abertura do golpe corpo a corpo (graus).")]
        [Range(5f, 180f)] public float meleeHalfAngle = 50f;

        [Header("Projétil (drone, D-025)")]
        public bool usesProjectile;
        [Min(0f)] public float projectileSpeed = 7f;
        [Min(0.05f)] public float projectileRadius = 0.35f;
        [Min(0.1f)] public float projectileLifetime = 3f;

        [Header("Morte (D-026)")]
        [Tooltip("Tempo até os restos sumirem (s).")]
        [Min(0f)] public float debrisLifetime = 4f;

        public BrainParams ToBrainParams() => new BrainParams(attackRange, minRange, windupTime, recoverTime);
    }
}
