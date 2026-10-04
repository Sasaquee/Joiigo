using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Cria a cena Arena e a registra no Build Settings.
    /// Também roda em batchmode: -executeMethod Game.EditorTools.ArenaSceneSetup.CreateArenaScene
    /// </summary>
    public static class ArenaSceneSetup
    {
        public const string ArenaScenePath = "Assets/_Game/Arena/Arena.unity";

        [MenuItem("Game/Setup/Criar cena Arena")]
        public static void CreateArenaScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaScenePath) == null)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ArenaScenePath);
                Debug.Log($"Cena criada em {ArenaScenePath}");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
