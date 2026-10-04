using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Arena.Life
{
    /// <summary>
    /// Resolve o material de partícula de cada SmokeKind.
    /// No editor carrega os materiais de Assets/_Game/Art/Ambience (criados pelo AmbienceBuilder).
    /// Em tempo de execução, se o material serializado estiver vazio, cria um Particles/Unlit simples.
    /// </summary>
    public static class SmokeMaterials
    {
        private const string AmbienceFolder = "Assets/_Game/Art/Ambience/";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

        private static readonly Material[] fallbacks = new Material[4];

#if UNITY_EDITOR
        private static string AssetPath(SmokeKind kind)
        {
            switch (kind)
            {
                case SmokeKind.Sparks: return AmbienceFolder + "ParticulaBrasa.mat";
                case SmokeKind.ArcaneMotes: return AmbienceFolder + "ParticulaPoeira.mat";
                default: return AmbienceFolder + "ParticulaVapor.mat";
            }
        }
#endif

        /// <summary>
        /// Só materiais que existem como asset (referência que serializa na cena).
        /// Fora do editor sempre devolve null.
        /// </summary>
        public static Material GetPersistent(SmokeKind kind)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Material>(AssetPath(kind));
#else
            return null;
#endif
        }

        /// <summary>Asset se existir; senão um material criado em tempo de execução (não serializável).</summary>
        public static Material Get(SmokeKind kind)
        {
            var m = GetPersistent(kind);
            return m != null ? m : GetFallback(kind);
        }

        private static bool IsAdditive(SmokeKind kind)
        {
            return kind == SmokeKind.Sparks || kind == SmokeKind.ArcaneMotes;
        }

        private static Material GetFallback(SmokeKind kind)
        {
            int i = (int)kind;
            if (fallbacks[i] != null)
                return fallbacks[i];

            var shader = Shader.Find(ShaderName);
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            bool additive = IsAdditive(kind);
            var mat = new Material(shader) { name = "SmokeFallback_" + kind };
            // Sem textura: o quadrado branco já combina com o visual pixelado.
            Color color = kind == SmokeKind.Sparks ? new Color(3f, 1.1f, 0.3f, 1f)
                : kind == SmokeKind.ArcaneMotes ? new Color(0.5f, 2f, 2.4f, 1f)
                : Color.white;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.hideFlags = HideFlags.DontSave;

            fallbacks[i] = mat;
            return mat;
        }
    }
}
