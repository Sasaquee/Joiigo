using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Instala o 3D pixelado (D-039) no URP: pós-processo de contorno e faixas (FullScreenPass)
    /// em todos os renderizadores, e ampliação "Point" no asset do pipeline.
    /// </summary>
    public static class PixelRenderSetup
    {
        private const string ShaderName = "Hidden/Game/PixelPost";
        private const string MaterialPath = "Assets/_Game/Art/Shaders/PixelPost.mat";
        private const string FeatureName = "PixelPost";

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

            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null)
                    EnsureFeature(data, material);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;
                asset.upscalingFilter = UpscalingFilterSelection.Point;
                asset.renderScale = 1f / 3f; // prévia pixelada no editor; em jogo o PixelCamera ajusta pela altura da tela
                asset.msaaSampleCount = 1;   // pixel nítido: sem suavização de bordas
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
        }

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

            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
    }
}
