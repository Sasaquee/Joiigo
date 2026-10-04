using UnityEngine;

namespace Game.Arena
{
    /// <summary>Vagas de spawn dos jogadores na plataforma de entrada.</summary>
    public class PlayerSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] slots = new Transform[0];

        public int Count => slots.Length;

        public void Configure(Transform[] newSlots) => slots = newSlots;

        public Transform Get(int slot)
        {
            if (slots.Length == 0)
                return transform;
            return slots[Mathf.Clamp(slot, 0, slots.Length - 1)];
        }
    }
}
