using Game.Aura;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Fase 7 — aura. Cria os dados (AuraSettings) e liga PlayerAura, AuraVisual e AuraAudio ao prefab do jogador.
    /// Chamado pelo ArenaBuilder ao montar o jogador. As texturas vêm de Tools/Aura/aura_art.py (Art/Aura).
    /// </summary>
    public static class AuraBuilder
    {
        public const string SettingsPath = "Assets/_Game/Data/Aura/AuraSettings.asset";
        private const string ArtFolder = "Assets/_Game/Art/Aura/";
        // Raio do anel do AnelMarcador.fbx (Tools/Blender/build_props.py, anel_marcador).
        private const float MarkerModelRadius = 1.08f;

        public static AuraSettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AuraSettings>(SettingsPath);
            if (settings != null)
                return settings;
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Aura"))
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Aura");
            settings = ScriptableObject.CreateInstance<AuraSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        /// <summary>Liga a aura ao jogador (raiz do prefab, antes de salvar) e põe o anel local por fora dela (D-066).</summary>
        public static void ConfigurePlayerPrefab(GameObject root, Transform localMarker)
        {
            var settings = LoadSettings();

            // Sem "??": no editor, GetComponent pode devolver um objeto "nulo falso" que o "??" não reconhece.
            var visual = Ensure<AuraVisual>(root);
            visual.Configure(settings, Tex("aura_circulo"), Tex("aura_runas"), Tex("aura_luz"), Tex("aura_faisca"),
                Tex("aura_fiapo"), Tex("aura_brasa"), Tex("aura_casca"));
            var audio = Ensure<AuraAudio>(root);
            audio.Configure(settings);
            var aura = Ensure<PlayerAura>(root);
            aura.Configure(settings, visual, audio);

            if (localMarker != null)
            {
                float s = settings.localMarkerRadius / MarkerModelRadius;
                localMarker.localScale = new Vector3(s, 1f, s);
            }
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static Texture2D Tex(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + name + ".png");
            if (texture == null)
                Debug.LogWarning($"[AuraBuilder] Falta {ArtFolder}{name}.png (rode Tools/Aura/aura_art.py).");
            return texture;
        }
    }

    /// <summary>Importação das texturas da aura: suaves, com mipmaps e alfa; a luz e a casca repetem sem emenda.</summary>
    public class AuraTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Art/Aura/"))
                return;
            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            bool tiles = file == "aura_luz" || file == "aura_casca";
            importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (file == "aura_luz")
                importer.wrapModeV = TextureWrapMode.Clamp; // repete só na volta, não na altura
        }
    }
}
