using Game.Combat;
using Game.Net;
using Game.Player;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Ligações do combate do jogador, chamadas pelo ArenaBuilder (o prefab e a cena são gerados por código).</summary>
    public static class PlayerCombatSetup
    {
        /// <summary>
        /// Adiciona vida, golpe e queda ao jogador antes de salvar o prefab. A ordem importa:
        /// o NetworkHealth vem primeiro para nascer antes do PlayerLife.
        /// </summary>
        public static void ConfigurePlayerPrefab(GameObject root, CombatSettings settings)
        {
            var health = root.GetComponent<NetworkHealth>() ?? root.AddComponent<NetworkHealth>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("radius").floatValue = settings.bodyRadius;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var combat = root.GetComponent<PlayerCombat>() ?? root.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("settings").objectReferenceValue = settings;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            Transform body = root.transform.Find("Corpo");
            // A placa do peito acompanha o corpo quando ele deita.
            Transform front = root.transform.Find("Frente");
            if (body != null && front != null)
                front.SetParent(body, worldPositionStays: true);

            var life = root.GetComponent<PlayerLife>() ?? root.AddComponent<PlayerLife>();
            var lifeSo = new SerializedObject(life);
            lifeSo.FindProperty("settings").objectReferenceValue = settings;
            lifeSo.FindProperty("body").objectReferenceValue = body;
            lifeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Cria o "SoloBootstrap" na cena aberta (D-018: entra direto, solo).</summary>
        public static void AddSoloBootstrap(NetSession session, MatchState match)
        {
            foreach (var old in Object.FindObjectsByType<SoloBootstrap>(FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("SoloBootstrap");
            var bootstrap = go.AddComponent<SoloBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("session").objectReferenceValue = session;
            so.FindProperty("matchState").objectReferenceValue = match;
            so.FindProperty("autoStartSolo").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
