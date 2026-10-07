using Game.Combat;
using Game.Player;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Levantar um aliado segurando E (D-083). Cria os dados (ReviveSettings) e liga o PlayerRevive ao prefab do jogador.
    /// Chamado pelo ArenaBuilder ao montar o jogador, depois do PlayerCombatSetup (o PlayerLife já está no prefab).
    /// </summary>
    public static class ReviveSetup
    {
        public const string SettingsPath = "Assets/_Game/Data/Combat/ReviveSettings.asset";

        /// <summary>Carrega o asset pelo caminho; se não existir, cria com os valores provisórios do D-083.</summary>
        public static ReviveSettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ReviveSettings>(SettingsPath);
            if (settings != null)
                return settings;

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Combat"))
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Combat");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ReviveSettings>(), SettingsPath);
            AssetDatabase.SaveAssets();
            // Recarrega pelo caminho: a referência devolvida pelo CreateAsset pode virar morta após reimportações.
            return AssetDatabase.LoadAssetAtPath<ReviveSettings>(SettingsPath);
        }

        /// <summary>Adiciona o PlayerRevive à raiz do prefab do jogador (antes de salvar) e liga o ReviveSettings.</summary>
        public static void Apply(GameObject playerPrefabRoot)
        {
            var settings = LoadSettings();

            // Sem "??": no editor, GetComponent pode devolver um objeto "nulo falso" que o "??" não reconhece.
            var revive = playerPrefabRoot.GetComponent<PlayerRevive>();
            if (revive == null)
                revive = playerPrefabRoot.AddComponent<PlayerRevive>();

            var so = new SerializedObject(revive);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
