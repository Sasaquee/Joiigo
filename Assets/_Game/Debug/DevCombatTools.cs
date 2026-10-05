using System.Globalization;
using Game.Combat;
using Game.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// Só em editor/development build (§4.7). No host:
    /// F4 = gerar 1 inimigo a 6 m à frente do jogador local, em ciclo pelos tipos do WaveSpawner;
    /// F5 = dano de 25% da vida máxima em si; F6 = cura total em si.
    /// Os dois números abaixo são da ferramenta de debug, não de jogo: ficam aqui mesmo, comentados.
    /// As ações SpawnEnemy, DamageSelf e HealSelf também estão no GameControls (mapa Debug) como
    /// documentação, mas aqui usam InputAction própria, porque este objeto não vive na cena
    /// (mesmo padrão do DevDiceTools).
    /// </summary>
    public class DevCombatTools : MonoBehaviour
    {
        // Números da ferramenta de debug, não de jogo: não vão para ScriptableObject (§4.7).
        private const float SpawnAheadDistance = 6f;    // metros à frente do jogador local (F4)
        private const float SelfDamageFraction = 0.25f; // fração da vida máxima ferida em si (F5)

        private static bool created;
        private InputAction spawnAction;
        private InputAction damageAction;
        private InputAction healAction;
        private int typeIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (created)
                return;
            created = true;
            var go = new GameObject(nameof(DevCombatTools));
            DontDestroyOnLoad(go);
            go.AddComponent<DevCombatTools>();
        }

        private void Awake()
        {
            spawnAction = new InputAction("SpawnEnemy", InputActionType.Button, "<Keyboard>/f4");
            damageAction = new InputAction("DamageSelf", InputActionType.Button, "<Keyboard>/f5");
            healAction = new InputAction("HealSelf", InputActionType.Button, "<Keyboard>/f6");
            spawnAction.Enable();
            damageAction.Enable();
            healAction.Enable();
        }

        private void OnDestroy()
        {
            created = false;
            spawnAction?.Dispose();
            damageAction?.Dispose();
            healAction?.Dispose();
        }

        private void Update()
        {
            if (!spawnAction.WasPressedThisFrame() && !damageAction.WasPressedThisFrame() && !healAction.WasPressedThisFrame())
                return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer)
                return; // só o host gera inimigo e mexe na vida

            if (spawnAction.WasPressedThisFrame())
                SpawnEnemy(manager);
            if (damageAction.WasPressedThisFrame())
                DamageSelf(manager);
            if (healAction.WasPressedThisFrame())
                HealSelf(manager);
        }

        private void SpawnEnemy(NetworkManager manager)
        {
            var spawner = FindFirstObjectByType<WaveSpawner>();
            EnemyDefinition[] types = spawner != null && spawner.Settings != null ? spawner.Settings.enemyTypes : null;
            if (types == null || types.Length == 0)
                return; // sem tipos, não faz nada

            NetworkObject player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            if (player == null)
                return;

            int index = typeIndex % types.Length;
            typeIndex = (typeIndex + 1) % types.Length;
            EnemyDefinition def = types[index];
            if (def == null)
                return;

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            Vector3 position = player.transform.position + (forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward) * SpawnAheadDistance;
            spawner.ServerSpawn(def, position);
            Debug.Log($"[Debug] Inimigo gerado: {def.name} (tipo {Num(index + 1)} de {Num(types.Length)}).");
        }

        private void DamageSelf(NetworkManager manager)
        {
            NetworkHealth health = LocalHealth(manager);
            if (health == null)
                return;

            float amount = health.Max * SelfDamageFraction;
            health.ServerApplyDamage(new Game.Core.Combat.DamagePacket(amount, 0f), manager.LocalClientId);
            Debug.Log($"[Debug] Dano em si: {amount.ToString("0.#", CultureInfo.InvariantCulture)} ({Num(Mathf.RoundToInt(SelfDamageFraction * 100f))}% da vida máxima).");
        }

        private void HealSelf(NetworkManager manager)
        {
            NetworkHealth health = LocalHealth(manager);
            if (health == null)
                return;

            health.ServerRestore();
            Debug.Log("[Debug] Vida restaurada.");
        }

        private static NetworkHealth LocalHealth(NetworkManager manager)
        {
            NetworkObject player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            return player != null ? player.GetComponent<NetworkHealth>() : null;
        }

        // Número sempre em cultura fixa, para não mudar com o idioma do sistema.
        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
