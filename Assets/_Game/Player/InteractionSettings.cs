using UnityEngine;

namespace Game.Player
{
    /// <summary>Números provisórios de interação com o mundo (tecla E).</summary>
    [CreateAssetMenu(menuName = "Game/Player/Interaction Settings", fileName = "InteractionSettings")]
    public class InteractionSettings : ScriptableObject
    {
        [Tooltip("Distância máxima para acionar algo com E (m).")]
        [Min(0f)] public float interactRadius = 2.5f;
    }
}
