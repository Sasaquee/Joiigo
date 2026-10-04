using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Paleta do 3D pixelado (Docs/Design/arte-pixel.md, D-039): materiais de cor chapada, sem textura.
    /// O detalhe vem da forma, da luz em faixas e do contorno. Os modelos do Blender usam estes
    /// mesmos nomes e a importação remapeia pelo nome.
    /// </summary>
    public static class PixelPalette
    {
        public const string MaterialsFolder = "Assets/_Game/Art/Materials";

        private readonly struct Entry
        {
            public readonly string Hex;
            public readonly float Metallic;
            public readonly float Smoothness;
            public readonly string EmissionHex;
            public readonly float EmissionIntensity;

            public Entry(string hex, float metallic, float smoothness, string emissionHex = null, float emissionIntensity = 0f)
            {
                Hex = hex;
                Metallic = metallic;
                Smoothness = smoothness;
                EmissionHex = emissionHex;
                EmissionIntensity = emissionIntensity;
            }
        }

        // Metal com metallic baixo: sem céu para refletir, metal "de verdade" fica preto no pixel.
        private static readonly Dictionary<string, Entry> Palette = new Dictionary<string, Entry>
        {
            ["FerroEscuro"] = new Entry("#2B2A30", 0.35f, 0.35f),
            ["FerroMedio"] = new Entry("#4A4752", 0.35f, 0.35f),
            ["Cobre"] = new Entry("#B5653A", 0.4f, 0.45f),
            ["CobreOxidado"] = new Entry("#4F8C7A", 0.15f, 0.25f),
            ["Latao"] = new Entry("#C9A04A", 0.45f, 0.5f),
            ["LataoEscuro"] = new Entry("#7A5F2C", 0.35f, 0.4f),
            ["Pedra"] = new Entry("#6A625A", 0f, 0.15f),
            ["PedraEscura"] = new Entry("#3A3634", 0f, 0.15f),
            ["Tijolo"] = new Entry("#7A3E2E", 0f, 0.12f),
            ["Madeira"] = new Entry("#6B4630", 0f, 0.2f),
            ["Telhado"] = new Entry("#3B3F52", 0.1f, 0.3f),
            ["Tecido"] = new Entry("#4E4A44", 0f, 0.1f),
            ["TecidoEscuro"] = new Entry("#2F2C2A", 0f, 0.1f),
            ["Couro"] = new Entry("#6E4A32", 0f, 0.25f),
            ["Bandeira"] = new Entry("#7C2F3A", 0f, 0.15f),
            ["JanelaQuente"] = new Entry("#FFC070", 0f, 0.6f, "#FFB060", 2.2f),
            ["CristalArcano"] = new Entry("#6FF0FF", 0f, 0.8f, "#55E8FF", 3.2f),
            ["CristalArcanoFraco"] = new Entry("#2E6E78", 0f, 0.7f, "#1A8FA0", 0.9f),
            ["CristalApagado"] = new Entry("#1E2A2E", 0f, 0.6f),
            ["BrasaFornalha"] = new Entry("#FF7A20", 0f, 0.3f, "#FF6A10", 3.5f),
            ["PersonagemNeutro"] = new Entry("#8C8A86", 0f, 0.3f),
            ["MarcadorLocal"] = new Entry("#EBE0C7", 0f, 0.3f, "#EBE0C7", 0.4f),
            // Materiais antigos das primitivas, agora chapados.
            ["PisoPedra"] = new Entry("#6E665C", 0f, 0.12f),
            ["PisoGrade"] = new Entry("#3A3842", 0.3f, 0.35f),
            ["FerroCorrugado"] = new Entry("#3E3C46", 0.3f, 0.3f),
            ["FerroPintado"] = new Entry("#3D4A44", 0.2f, 0.3f),
            ["MadeiraEscura"] = new Entry("#4A3022", 0f, 0.2f),
        };

        public static IEnumerable<string> Names => Palette.Keys;

        [MenuItem("Game/Setup/Aplicar Paleta Pixel")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Art", "Materials");

            foreach (var kv in Palette)
            {
                string path = $"{MaterialsFolder}/{kv.Key}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                Configure(mat, kv.Value);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Configure(Material mat, Entry e)
        {
            ColorUtility.TryParseHtmlString(e.Hex, out Color baseColor);
            mat.SetColor("_BaseColor", baseColor);
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_BumpMap", null);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetTexture("_OcclusionMap", null);
            mat.DisableKeyword("_NORMALMAP");
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_OCCLUSIONMAP");
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetFloat("_Metallic", e.Metallic);
            mat.SetFloat("_Smoothness", e.Smoothness);

            if (e.EmissionHex != null)
            {
                ColorUtility.TryParseHtmlString(e.EmissionHex, out Color emission);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission * e.EmissionIntensity);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }
}
