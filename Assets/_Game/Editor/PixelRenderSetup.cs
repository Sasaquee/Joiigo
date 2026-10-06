using Game.Cameras;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Instala o pós-processo do mundo no URP (D-039, D-056, D-057): contorno e luz em faixas (FullScreenPass)
    /// em todos os renderizadores, e a qualidade de imagem de ImageQualitySettings (faixas, pontilhado, MSAA).
    /// Também instala a passada do fantasma dos prédios vazados (D-076, D-079): um RenderObjects que desenha a
    /// passada "VazadoFantasma" do Game/LitVazado depois dos transparentes, só na cor.
    /// </summary>
    public static class PixelRenderSetup
    {
        private const string ShaderName = "Hidden/Game/PixelPost";
        private const string MaterialPath = "Assets/_Game/Art/Shaders/PixelPost.mat";
        private const string FeatureName = "PixelPost";
        public const string GhostFeatureName = "VazadoFantasma";
        public const string GhostPassName = "VazadoFantasma"; // LightMode da passada no LitVazado.shader
        public const string QualitySettingsPath = "Assets/_Game/Data/Camera/ImageQualitySettings.asset";

        /// <summary>Carrega (ou cria com os valores padrão) os números de qualidade de imagem.</summary>
        public static ImageQualitySettings LoadQualitySettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ImageQualitySettings>(QualitySettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<ImageQualitySettings>();
            AssetDatabase.CreateAsset(settings, QualitySettingsPath);
            return settings;
        }

        [MenuItem("Game/Setup/Instalar Render Pixelado")]
        public static void Apply()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[PixelRenderSetup] Shader {ShaderName} não encontrado.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            var quality = LoadQualitySettings();
            material.SetFloat("_Levels", quality.lightBands);
            material.SetFloat("_Dither", quality.dither);
            material.SetFloat("_BandSoftness", quality.bandSoftness);
            EditorUtility.SetDirty(material);

            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null)
                    continue;
                EnsureFeature(data, material);
                EnsureGhostFeature(data);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;
                asset.upscalingFilter = UpscalingFilterSelection.Point;
                // Prévia no editor igual ao jogo; em jogo o PixelCamera ajusta pela altura da tela.
                asset.renderScale = quality.worldHeight > 0 ? Mathf.Clamp01(quality.worldHeight / 1080f) : 1f;
                // Bordas suaves só com o mundo na resolução da tela; no modo pixelado, pixel nítido.
                asset.msaaSampleCount = quality.worldHeight > 0 ? 1 : ValidMsaa(quality.msaa);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
        }

        private static int ValidMsaa(int samples) => samples >= 8 ? 8 : samples >= 4 ? 4 : samples >= 2 ? 2 : 1;

        private static void EnsureFeature(UniversalRendererData data, Material material)
        {
            FullScreenPassRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
            {
                if (f is FullScreenPassRendererFeature fs && f.name == FeatureName)
                {
                    feature = fs;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AddFeatureToRenderer(data, feature);
            }

            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// Fantasma dos prédios vazados (D-079): desenha só a passada de LightMode "VazadoFantasma" (existe apenas no
        /// Game/LitVazado), depois dos transparentes, por cima da cor. Não toca profundidade nem normais.
        /// </summary>
        private static void EnsureGhostFeature(UniversalRendererData data)
        {
            RenderObjects feature = null;
            foreach (var f in data.rendererFeatures)
            {
                if (f is RenderObjects ro && f.name == GhostFeatureName)
                {
                    feature = ro;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RenderObjects>();
                feature.name = GhostFeatureName;
                AddFeatureToRenderer(data, feature);
            }

            var settings = feature.settings;
            settings.passTag = GhostFeatureName;
            settings.Event = RenderPassEvent.AfterRenderingTransparents;
            settings.filterSettings.RenderQueueType = RenderQueueType.Opaque; // os prédios são opacos
            settings.filterSettings.LayerMask = ~0;
            settings.filterSettings.PassNames = new[] { GhostPassName };
            settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            settings.overrideMaterial = null;
            settings.overrideShader = null;
            settings.overrideDepthState = false;   // ZTest/ZWrite/stencil vêm da própria passada
            settings.stencilSettings.overrideStencilState = false;
            settings.cameraSettings.overrideCamera = false;
            feature.Create(); // reaplica as configurações na passada
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        private static void AddFeatureToRenderer(UniversalRendererData data, ScriptableRendererFeature feature)
        {
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            // A lista de features e o mapa de ids precisam andar juntos (é o que o Inspector do URP faz).
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
