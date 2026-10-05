using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// Importação das faces das cartas em alta resolução (D-059), geradas por Tools/Cards/card_hd.py
    /// em Assets/_Game/Art/Cards: filtro suave com mipmaps (a carta aparece bem menor que a imagem), sem compressão.
    /// As máscaras de brilho (<id>_brilho.png) usam o alfa como transparência.
    /// </summary>
    public class CardTextureImport : AssetPostprocessor
    {
        private const string CardsFolder = "Assets/_Game/Art/Cards/";
        private const string GlowSuffix = "_brilho";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CardsFolder))
                return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = file.EndsWith(GlowSuffix);
        }
    }
}
