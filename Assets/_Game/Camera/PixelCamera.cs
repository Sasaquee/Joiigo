using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Cameras
{
    /// <summary>
    /// 3D pixelado (D-039): o mundo é renderizado em ~targetHeight linhas e ampliado sem filtro
    /// (Render Scale + Upscaling "Point" do URP); a UI em Overlay continua nítida.
    /// A câmera anda de pixel em pixel (snap) para a imagem não "tremer" quando ela se move.
    /// Roda depois do CameraFollow e soma o tremor do CameraShake.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public class PixelCamera : MonoBehaviour
    {
        [Tooltip("Linhas de pixel do mundo na tela (360 = 640x360 em 16:9).")]
        [SerializeField, Min(90)] private int targetHeight = 360;
        [SerializeField] private bool snapToPixelGrid = true;

        private Camera cam;
        private UniversalRenderPipelineAsset pipeline;
        private float originalScale = 1f;
        private UpscalingFilterSelection originalFilter;
        private bool applied;

        public int TargetHeight => targetHeight;

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
            if (pipeline != null)
                pipeline.renderScale = Mathf.Clamp((float)targetHeight / Mathf.Max(1, Screen.height), 0.1f, 1f);

            Vector3 position = transform.position + CameraShake.CurrentOffset;
            if (snapToPixelGrid)
                position = Snap(position);
            transform.position = position;
        }

        /// <summary>Arredonda a posição no plano da câmera para múltiplos do tamanho de um pixel no ponto de foco.</summary>
        private Vector3 Snap(Vector3 position)
        {
            float focusDistance = 14f;
            if (TryGetComponent(out CameraFollow follow) && follow.Settings != null)
                focusDistance = follow.Settings.distance;

            float worldPerPixel = 2f * focusDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / targetHeight;
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
