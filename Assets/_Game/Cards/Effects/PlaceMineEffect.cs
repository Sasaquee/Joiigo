using Unity.Netcode;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Planta uma mina no chão (Mina de Engrenagem). A mina é um objeto de rede com o componente Mine:
    /// explode quando um inimigo chega perto ou quando o pavio acaba, e fere em área.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Place Mine", fileName = "PlaceMineEffect")]
    public class PlaceMineEffect : CardEffect
    {
        [Tooltip("Prefab de rede da mina (NetworkObject + Mine), registrado nos prefabs de rede.")]
        public GameObject minePrefab;
        [Min(0f)] public float damage = 45f;
        [Range(0f, 1f)] public float arcaneFraction = 0.4f;
        [Tooltip("Raio da explosão (m).")]
        [Min(0.5f)] public float areaRadius = 2.5f;
        [Tooltip("Distância em que um inimigo dispara a mina (m).")]
        [Min(0.2f)] public float triggerRadius = 1.3f;
        [Tooltip("Explode sozinha depois deste tempo (s).")]
        [Min(0.5f)] public float fuse = 10f;
        [Tooltip("Tempo depois de plantada até ela poder disparar (s).")]
        [Min(0f)] public float armDelay = 0.3f;
        [Tooltip("Onde ela é plantada, à frente do jogador, na direção da mira (m).")]
        [Min(0f)] public float placeDistance = 1.2f;

        public override void Execute(ICardUser user, CardData card)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
                return;
            if (minePrefab == null)
            {
                Debug.LogWarning($"{name}: sem prefab da mina.");
                return;
            }

            Vector3 origin = user.Transform.position;
            Vector3 dir = user.AimDirection;
            Vector3 position = origin + dir * placeDistance;
            position.y = origin.y;

            GameObject go = Instantiate(minePrefab, position, Quaternion.identity);
            var mine = go.GetComponent<Mine>();
            var networkObject = go.GetComponent<NetworkObject>();
            if (mine == null || networkObject == null)
            {
                Debug.LogWarning($"{name}: o prefab da mina precisa de NetworkObject e Mine.");
                Destroy(go);
                return;
            }

            mine.ServerInit(damage * user.Potency, arcaneFraction, areaRadius, triggerRadius, fuse, armDelay, user.ClientId);
            networkObject.Spawn(true);
            user.BroadcastVisual("mine_place", position, dir, areaRadius);
        }
    }
}
