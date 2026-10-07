using System.IO;
using Game.Enemies;
using Game.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Recomeço da partida depois da queda total (D-084, D-086, D-087). Cria os dados (MatchSettings) e põe o MatchReset
    /// no objeto de cena "Sessao" (o mesmo NetworkObject do MatchState, como o CardDropService), ligado ao MatchState e ao WaveSpawner.
    /// A tela que escurece (WipeFadeUi) não precisa de construtor: ela se cria sozinha sob a Canvas "UI".
    /// Chamado pelo ArenaBuilder, depois de CardDropBuilder.AddToScene.
    /// </summary>
    public static class MatchResetSetup
    {
        private const string SettingsPath = "Assets/_Game/Data/Net/MatchSettings.asset";

        /// <summary>Cria/atualiza o MatchSettings e põe o MatchReset na cena de arenaRoot. Devolve o componente.</summary>
        public static MatchReset Apply(Transform arenaRoot)
        {
            Scene scene = arenaRoot != null ? arenaRoot.gameObject.scene : SceneManager.GetActiveScene();

            // Recarrega pelo caminho: referências de um passo anterior podem ter morrido em reimportações.
            LoadOrCreate<MatchSettings>(SettingsPath);
            AssetDatabase.SaveAssets();
            var settings = AssetDatabase.LoadAssetAtPath<MatchSettings>(SettingsPath);

            MatchState match = FindInScene<MatchState>(scene);
            WaveSpawner spawner = FindInScene<WaveSpawner>(scene);

            GameObject host;
            if (match != null)
            {
                host = match.gameObject; // o mesmo NetworkObject do MatchState e do CardDropService
            }
            else
            {
                Debug.LogWarning("MatchResetSetup: MatchState não encontrado; criando um objeto de cena próprio para o MatchReset.");
                host = new GameObject("QuedaTotal");
                SceneManager.MoveGameObjectToScene(host, scene);
            }

            if (!host.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject = host.AddComponent<NetworkObject>();
                EnsureNetworkObjectHash(networkObject);
            }

            var reset = host.GetComponent<MatchReset>();
            if (reset == null)
                reset = host.AddComponent<MatchReset>();

            var so = new SerializedObject(reset);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("match").objectReferenceValue = match;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.ApplyModifiedPropertiesWithoutUndo();
            return reset;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>O Netcode só gera o GlobalObjectIdHash no OnValidate do editor; objeto criado por script precisa dele.</summary>
        private static void EnsureNetworkObjectHash(NetworkObject networkObject)
        {
            typeof(NetworkObject)
                .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
