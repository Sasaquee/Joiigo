using Game.Core.Math;
using Game.Core.Movement;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Aplica intenções de movimento e mira. Não lê input: recebe a intenção pronta,
    /// para que o host possa aplicar a intenção de qualquer jogador (Fase 3).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private MovementSettings settings;

        private CharacterController controller;
        private Float2 velocity;
        private float verticalSpeed;
        private Vector3 moveDirection;
        private Vector3? aimPoint;

        public MovementSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public Vector3 Velocity => new Vector3(velocity.X, 0f, velocity.Y);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>Direção de movimento no mundo (XZ, comprimento até 1) e ponto de mira no mundo.</summary>
        public void SetIntent(Vector3 worldMoveDirection, Vector3? worldAimPoint)
        {
            moveDirection = new Vector3(worldMoveDirection.x, 0f, worldMoveDirection.z);
            aimPoint = worldAimPoint;
        }

        private void Update()
        {
            if (settings == null)
                return;

            float dt = Time.deltaTime;
            velocity = MovementMath.StepVelocity(velocity, new Float2(moveDirection.x, moveDirection.z),
                settings.moveSpeed, settings.acceleration, settings.deceleration, dt);

            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - settings.gravity * dt;

            controller.Move(new Vector3(velocity.X, verticalSpeed, velocity.Y) * dt);

            FaceAimPoint(dt);
        }

        private void FaceAimPoint(float dt)
        {
            if (!aimPoint.HasValue)
                return;

            Vector3 p = transform.position;
            Vector3 a = aimPoint.Value;
            if (new Vector2(a.x - p.x, a.z - p.z).sqrMagnitude < 0.0001f)
                return;

            float yaw = MovementMath.FacingYawDegrees(new Float2(p.x, p.z), new Float2(a.x, a.z));
            Quaternion target = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, settings.turnSpeed * dt);
        }
    }
}
