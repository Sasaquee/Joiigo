using UnityEngine;

namespace Game.Cameras
{
    /// <summary>Câmera 2.5D com perspectiva diagonal, seguindo o jogador local.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private CameraSettings settings;
        [SerializeField] private Transform target;

        private Camera cam;
        private Vector3 focus;

        public CameraSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (target != null)
                    focus = target.position;
            }
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (target != null)
                focus = target.position;
        }

        private void LateUpdate()
        {
            if (settings == null)
                return;

            if (target != null)
            {
                float t = 1f - Mathf.Exp(-settings.followSharpness * Time.deltaTime);
                focus = Vector3.Lerp(focus, target.position, t);
            }

            Apply(focus);
        }

        /// <summary>Posiciona a câmera olhando para o ponto de foco, segundo os números do settings.</summary>
        public void Apply(Vector3 focusPoint)
        {
            if (cam == null)
                cam = GetComponent<Camera>();

            Quaternion rotation = Quaternion.Euler(settings.pitch, settings.yaw, 0f);
            Vector3 lookAt = focusPoint + Vector3.up * settings.focusHeight;
            transform.SetPositionAndRotation(lookAt - rotation * Vector3.forward * settings.distance, rotation);
            cam.fieldOfView = settings.fieldOfView;
        }
    }
}
