using UnityEngine;

namespace Game.Arena
{
    public enum ArenaMarkerKind
    {
        PlayerSpawn,
        EnemySpawn,
        CardTestArea,
        CombatCenter
    }

    /// <summary>Marca um ponto da arena para os sistemas das próximas fases (spawns, área de cartas).</summary>
    public class ArenaMarker : MonoBehaviour
    {
        [SerializeField] private ArenaMarkerKind kind;
        [SerializeField, Min(0f)] private float radius = 1f;

        public ArenaMarkerKind Kind => kind;
        public float Radius => radius;

        public void Configure(ArenaMarkerKind newKind, float newRadius)
        {
            kind = newKind;
            radius = newRadius;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                ArenaMarkerKind.PlayerSpawn => Color.green,
                ArenaMarkerKind.EnemySpawn => Color.red,
                ArenaMarkerKind.CardTestArea => Color.cyan,
                _ => Color.yellow
            };
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
