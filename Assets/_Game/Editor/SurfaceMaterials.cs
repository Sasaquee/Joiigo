using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Aplica as texturas PBR (Poly Haven, CC0) aos materiais URP Lit do projeto.
    /// O URP Lit usa um mapa metálico/suavidade empacotado (R = metal, A = suavidade),
    /// então o mapa é gerado a partir do metal e da rugosidade e salvo em Art/Textures/_Packed.
    /// Chamar depois de ArenaBuilder.CreateMaterials(); é idempotente.
    /// </summary>
    public static class SurfaceMaterials
    {
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string TexturesFolder = "Assets/_Game/Art/Textures";
        private const string PackedFolder = TexturesFolder + "/_Packed";

        // Ligar para regerar os mapas empacotados mesmo que já existam (por exemplo, após mudar um metal padrão).
        private const bool ForceRepack = false;

        private struct Spec
        {
            public string Material;      // nome do .mat
            public string TextureId;     // id Poly Haven (pasta em Art/Textures)
            public Color Tint;           // multiplica a cor base; mantém a identidade (cobre, latão, ferro)
            public Vector2 Tiling;
            public float NormalScale;
            public float Smoothness;     // multiplicador sobre o alfa do mapa empacotado
            public float DefaultMetal;   // metal quando a textura não tem mapa de metal
            public bool BaseAndPacked;   // false: só normal (cristais)
        }

        private static readonly Spec[] Specs =
        {
            // Metais com identidade: o tint mantém cobre e latão legíveis à noite.
            New("Cobre", "metal_plate", new Color(0.95f, 0.55f, 0.35f), 1f, 1f, 1f, 1f, true),
            New("Latao", "metal_plate", new Color(1f, 0.82f, 0.45f), 1.5f, 1f, 1f, 1f, true),
            New("FerroEscuro", "rusty_metal_02", new Color(0.5f, 0.52f, 0.6f), 1f, 1f, 1f, 0.85f, true),
            // O piso é um cubo único de ~62 m: cada face tem UV 0-1, então o tiling é grande.
            New("PisoPedra", "concrete_floor_worn_001", new Color(0.65f, 0.66f, 0.72f), 10f, 1f, 0.9f, 0f, true),
            // Materiais novos, disponíveis para o ArenaBuilder e para os modelos do Blender.
            New("PisoGrade", "metal_grate_rusty", new Color(0.7f, 0.7f, 0.75f), 10f, 1f, 1f, 1f, true),
            New("PedraEscura", "dark_brick_wall", new Color(0.8f, 0.8f, 0.85f), 2f, 1f, 0.9f, 0f, true),
            New("MadeiraEscura", "dark_wood", new Color(0.8f, 0.8f, 0.8f), 2f, 1f, 0.9f, 0f, true),
            New("FerroCorrugado", "worn_corrugated_iron", new Color(0.6f, 0.62f, 0.68f), 2f, 1f, 1f, 0.8f, true),
            New("FerroPintado", "rusty_painted_metal", new Color(0.7f, 0.72f, 0.78f), 1f, 1f, 1f, 0.5f, true),
            // Cristais continuam emissivos e lisos; só um relevo sutil de superfície.
            New("CristalArcano", "concrete_floor_worn_001", Color.white, 3f, 0.4f, 0.9f, 0f, false),
            New("CristalArcanoFraco", "concrete_floor_worn_001", Color.white, 3f, 0.4f, 0.8f, 0f, false),
            New("CristalApagado", "concrete_floor_worn_001", Color.white, 3f, 0.4f, 0.85f, 0f, false),
            // PersonagemNeutro e MarcadorLocal ficam como estão (personagem genérico, Pilar 1).
        };

        private static Spec New(string material, string id, Color tint, float tiling, float normalScale,
            float smoothness, float defaultMetal, bool baseAndPacked)
            => new Spec
            {
                Material = material, TextureId = id, Tint = tint, Tiling = new Vector2(tiling, tiling),
                NormalScale = normalScale, Smoothness = smoothness, DefaultMetal = defaultMetal, BaseAndPacked = baseAndPacked
            };

        [MenuItem("Game/Setup/Aplicar Texturas nos Materiais")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var packedCache = new Dictionary<string, Texture2D>();

            foreach (var spec in Specs)
            {
                var mat = GetOrCreateMaterial(spec.Material);

                var normal = Load($"{TexturesFolder}/{spec.TextureId}/{spec.TextureId}_nor_gl_1k.jpg");
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", spec.NormalScale);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    Debug.LogWarning($"Normal não encontrada para {spec.TextureId}; as texturas foram importadas?");
                }

                if (spec.BaseAndPacked)
                {
                    var diffuse = Load($"{TexturesFolder}/{spec.TextureId}/{spec.TextureId}_diff_1k.jpg");
                    if (diffuse != null)
                    {
                        mat.SetTexture("_BaseMap", diffuse);
                        mat.SetColor("_BaseColor", spec.Tint);
                    }

                    if (!packedCache.TryGetValue(spec.TextureId, out var packed))
                    {
                        packed = GetPacked(spec.TextureId, spec.DefaultMetal);
                        packedCache[spec.TextureId] = packed;
                    }
                    if (packed != null)
                    {
                        mat.SetTexture("_MetallicGlossMap", packed);
                        mat.SetFloat("_SmoothnessTextureChannel", 0f); // alfa do mapa metálico
                        mat.SetFloat("_Smoothness", spec.Smoothness);
                        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                }

                mat.SetTextureScale("_BaseMap", spec.Tiling);
                mat.SetTextureScale("_BumpMap", spec.Tiling);
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
        }

        // ---------- Materiais ----------

        private static Material GetOrCreateMaterial(string name)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;

            EnsureFolder(MaterialsFolder);
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.4f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- Mapa metálico/suavidade empacotado ----------

        private static Texture2D GetPacked(string id, float defaultMetal)
        {
            string path = $"{PackedFolder}/{id}_ms.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && !ForceRepack)
                return existing;

            var rough = LoadRaw($"{TexturesFolder}/{id}/{id}_rough_1k.jpg");
            if (rough == null)
            {
                Debug.LogWarning($"Rugosidade não encontrada para {id}.");
                return null;
            }
            var metal = LoadRaw($"{TexturesFolder}/{id}/{id}_metal_1k.jpg"); // pode não existir

            int w = rough.width, h = rough.height;
            var roughPixels = rough.GetPixels32();
            bool useMetalMap = metal != null && metal.width == w && metal.height == h;
            var metalPixels = useMetalMap ? metal.GetPixels32() : null;
            byte constMetal = (byte)Mathf.RoundToInt(Mathf.Clamp01(defaultMetal) * 255f);

            var result = new Color32[w * h];
            for (int i = 0; i < result.Length; i++)
            {
                byte m = useMetalMap ? metalPixels[i].r : constMetal;
                byte smooth = (byte)(255 - roughPixels[i].r); // suavidade = 1 - rugosidade
                result[i] = new Color32(m, 0, 0, smooth);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(result);
            tex.Apply();
            EnsureFolder(PackedFolder);
            File.WriteAllBytes(Path.GetFullPath(path), ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rough);
            if (metal != null)
                Object.DestroyImmediate(metal);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.maxTextureSize = 1024;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Lê o arquivo do disco sem passar pelo importador.</summary>
        private static Texture2D LoadRaw(string assetPath)
        {
            string full = Path.GetFullPath(assetPath);
            if (!File.Exists(full))
                return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(full)))
            {
                Object.DestroyImmediate(tex);
                return null;
            }
            return tex;
        }

        private static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
