using UnityEngine;

namespace Game.Player
{
    /// <summary>Números provisórios do movimento do personagem (D-007).</summary>
    [CreateAssetMenu(menuName = "Game/Player/Movement Settings", fileName = "MovementSettings")]
    public class MovementSettings : ScriptableObject
    {
        [Tooltip("Velocidade máxima no plano (m/s).")]
        [Min(0f)] public float moveSpeed = 6f;

        [Tooltip("Ganho de velocidade (m/s²). Alto = arranque quase instantâneo.")]
        [Min(0f)] public float acceleration = 60f;

        [Tooltip("Perda de velocidade ao soltar ou frear (m/s²).")]
        [Min(0f)] public float deceleration = 80f;

        [Tooltip("Velocidade de giro para olhar o mouse (graus/s).")]
        [Min(0f)] public float turnSpeed = 1080f;

        [Tooltip("Gravidade que mantém o personagem no chão (m/s²).")]
        [Min(0f)] public float gravity = 20f;
    }
}
