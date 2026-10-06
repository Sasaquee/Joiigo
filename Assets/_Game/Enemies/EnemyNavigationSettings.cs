using Game.Core.AI;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Números da navegação dos inimigos (passe do mapa, D-077) e da corrida a distância (D-080). A NavMesh só calcula o
    /// caminho; quem move é o CharacterController. Ajuste aqui, sem código.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Enemy Navigation Settings", fileName = "EnemyNavigationSettings")]
    public class EnemyNavigationSettings : ScriptableObject
    {
        [Header("Caminho")]
        [Tooltip("Recalcula o caminho a cada tanto (s).")]
        [Min(0.05f)] public float repathInterval = 0.4f;
        [Tooltip("Recalcula se o alvo andou mais que isto desde o último cálculo (m).")]
        [Min(0.1f)] public float repathTargetDistance = 1.5f;
        [Tooltip("A quina do caminho conta como alcançada a esta distância (m).")]
        [Min(0.05f)] public float cornerReach = 0.5f;
        [Tooltip("Raio para achar o ponto da NavMesh mais perto do inimigo e do alvo (m).")]
        [Min(0.1f)] public float sampleRadius = 2f;

        [Header("Reto ou caminho")]
        [Tooltip("Com linha de visão até o alvo e a esta distância ou menos, segue reto (m).")]
        [Min(0f)] public float straightRange = 6f;
        [Tooltip("Altura dos pontos do teste de linha de visão contra o cenário (m).")]
        [Min(0.1f)] public float lineOfSightHeight = 1f;
        [Tooltip("Recuo do drone: distância olhada à frente na NavMesh antes de recuar; se bater na borda, fica parado (m).")]
        [Min(0.1f)] public float retreatProbe = 1f;

        [Header("Preso")]
        [Tooltip("Janela para decidir que o inimigo está preso (s).")]
        [Min(0.1f)] public float stuckTime = 1.5f;
        [Tooltip("Preso = andou menos que isto na janela (m).")]
        [Min(0.01f)] public float stuckDistance = 0.3f;

        [Header("Corrida a distância (D-080)")]
        [Tooltip("A mais de tanto do jogador vivo mais próximo, o inimigo corre (m).")]
        [Min(0f)] public float farSprintDistance = 18f;
        [Tooltip("Multiplicador da velocidade quando corre.")]
        [Min(1f)] public float farSprintMultiplier = 2f;

        public PathCursorParams ToCursorParams() =>
            new PathCursorParams(cornerReach, repathInterval, repathTargetDistance, stuckTime, stuckDistance);
    }
}
