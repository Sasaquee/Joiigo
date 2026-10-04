using Game.Core.Math;
using Game.Core.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Lê o input local (WASD + mouse) e transforma em intenção no mundo:
    /// direção relativa à tela (D-006) e ponto de mira no chão (D-002, D-005).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private Camera viewCamera;

        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction aimAction;
        private PlayerMotor motor;

        public Camera ViewCamera
        {
            get => viewCamera;
            set => viewCamera = value;
        }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            aimAction = playerMap.FindAction("Aim", throwIfNotFound: true);
        }

        private void OnEnable() => playerMap.Enable();

        private void OnDisable() => playerMap.Disable();

        private void Update()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null)
                return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            Float2 world = MovementMath.ScreenRelativeDirection(new Float2(input.x, input.y),
                viewCamera.transform.eulerAngles.y);

            motor.SetIntent(new Vector3(world.X, 0f, world.Y), ReadAimPoint());
        }

        private Vector3? ReadAimPoint()
        {
            Vector2 screen = aimAction.ReadValue<Vector2>();
            Ray ray = viewCamera.ScreenPointToRay(screen);
            var ground = new Plane(Vector3.up, transform.position);
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : (Vector3?)null;
        }
    }
}
