using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Ajusta a importação das texturas PBR em Assets/_Game/Art/Textures:
    /// normal map como NormalMap, mapas de dados (rugosidade, metal, AO, empacotado) em linear.
    /// </summary>
    public class TextureImportSetup : AssetPostprocessor
    {
        private const string TexturesFolder = "Assets/_Game/Art/Textures/";
        private const int MaxSize = 1024;
        private const int AnisoLevel = 4;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder))
                return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            if (file.Contains("_nor_gl"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (file.Contains("_rough") || file.Contains("_metal") || file.Contains("_ao_") || file.EndsWith("_ao") || file.EndsWith("_ms"))
            {
                importer.sRGBTexture = false;
            }

            importer.maxTextureSize = MaxSize;
            importer.mipmapEnabled = true;
            importer.anisoLevel = AnisoLevel;
        }
    }
}
