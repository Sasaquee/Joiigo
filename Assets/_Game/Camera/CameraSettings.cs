using UnityEngine;

namespace Game.Cameras
{
    /// <summary>Números provisórios da câmera 2.5D (D-004: média, ~14 m, 50°, giro 30°).</summary>
    [CreateAssetMenu(menuName = "Game/Camera/Camera Settings", fileName = "CameraSettings")]
    public class CameraSettings : ScriptableObject
    {
        [Tooltip("Distância da câmera até o ponto de foco (m).")]
        [Min(1f)] public float distance = 14f;

        [Tooltip("Inclinação para baixo (graus). 90 = vista de cima.")]
        [Range(10f, 89f)] public float pitch = 50f;

        [Tooltip("Giro diagonal em volta do personagem (graus).")]
        [Range(-180f, 180f)] public float yaw = 30f;

        [Tooltip("Altura do ponto de foco acima dos pés do personagem (m).")]
        public float focusHeight = 1f;

        [Tooltip("Zoom: campo de visão vertical da câmera (graus).")]
        [Range(10f, 90f)] public float fieldOfView = 40f;

        [Tooltip("Rapidez com que a câmera alcança o personagem. Maior = mais colada.")]
        [Min(0.1f)] public float followSharpness = 12f;
    }
}
