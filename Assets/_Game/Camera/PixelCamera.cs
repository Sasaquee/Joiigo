using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Cameras
{
    /// <summary>
    /// Resolução do mundo (D-056): por padrão o mundo é renderizado na resolução da tela, com contorno e luz em
    /// faixas suaves (PixelPost). Se `ImageQualitySettings.worldHeight` pedir menos linhas, volta ao 3D pixelado de D-039:
    /// Render Scale + ampliação "Point" do URP, com a câmera andando de pixel em pixel (snap) para a imagem não tremer.
    /// Também passa a espessura do contorno para o shader. Roda depois do CameraFollow e soma o tremor do CameraShake.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public class PixelCamera : MonoBehaviour
    {
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        [SerializeField] private ImageQualitySettings settings;
        [SerializeField] private bool snapToPixelGrid = true;

        private Camera cam;
        private UniversalRenderPipelineAsset pipeline;
        private float originalScale = 1f;
        private UpscalingFilterSelection originalFilter;
        private bool applied;
        private int renderHeight;

        public ImageQualitySettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Linhas do mundo no último quadro.</summary>
        public int RenderHeight => renderHeight;

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                return;
            originalScale = pipeline.renderScale;
            originalFilter = pipeline.upscalingFilter;
            applied = true;
            pipeline.upscalingFilter = UpscalingFilterSelection.Point;
        }

        private void OnDisable()
        {
            // O asset do URP é compartilhado: devolve como estava (evita sujar o arquivo no editor).
            if (!applied || pipeline == null)
                return;
            pipeline.renderScale = originalScale;
            pipeline.upscalingFilter = originalFilter;
            applied = false;
        }

        private void LateUpdate()
        {
            int screen = Mathf.Max(1, Screen.height);
            renderHeight = settings != null ? settings.RenderHeight(screen) : screen;
            if (pipeline != null)
                pipeline.renderScale = Mathf.Clamp((float)renderHeight / screen, 0.1f, 1f);
            float outline = settings != null ? settings.OutlinePixels(renderHeight) : 1f;
            Shader.SetGlobalFloat(OutlineWidthId, outline);

            Vector3 position = transform.position + CameraShake.CurrentOffset;
            if (snapToPixelGrid && renderHeight < screen)
                position = Snap(position);
            transform.position = position;
        }

        /// <summary>Arredonda a posição no plano da câmera para múltiplos do tamanho de um pixel no ponto de foco.</summary>
        private Vector3 Snap(Vector3 position)
        {
            float focusDistance = 14f;
            if (TryGetComponent(out CameraFollow follow) && follow.Settings != null)
                focusDistance = follow.Settings.distance;

            float worldPerPixel = 2f * focusDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / renderHeight;
            if (worldPerPixel <= 0f)
                return position;

            Quaternion rotation = transform.rotation;
            Vector3 local = Quaternion.Inverse(rotation) * position;
            local.x = Mathf.Round(local.x / worldPerPixel) * worldPerPixel;
            local.y = Mathf.Round(local.y / worldPerPixel) * worldPerPixel;
            return rotation * local;
        }
    }
}
