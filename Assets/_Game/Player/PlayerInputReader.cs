using System;
using Game.Core.Math;
using Game.Core.Movement;
using Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Lê o input local (WASD + mouse + E) e transforma em intenção no mundo:
    /// direção relativa à tela (D-006) e ponto de mira no chão (D-002, D-005).
    /// Em rede, a intenção vai para o NetworkPlayer, que prevê e envia ao host. Fora de rede, vai direto ao motor.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private Camera viewCamera;

        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction aimAction;
        private InputAction interactAction;
        private InputAction attackAction;
        private PlayerMotor motor;
        private NetworkPlayer networkPlayer;

        /// <summary>Clique de ataque (D-002): leva o ponto de mira no chão. Só dispara no dono, com a mira válida.</summary>
        public event Action<Vector3> AttackPressed;

        /// <summary>Mapa de input ligado (usado em testes).</summary>
        public bool InputEnabled => playerMap != null && playerMap.enabled;

        public Camera ViewCamera
        {
            get => viewCamera;
            set => viewCamera = value;
        }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            networkPlayer = GetComponent<NetworkPlayer>();
            // Clone por instância: o asset é compartilhado e desligar o mapa de outro jogador desligaria o do dono.
            runtimeActions = Instantiate(actions);
            playerMap = runtimeActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            aimAction = playerMap.FindAction("Aim", throwIfNotFound: true);
            interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
            attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
        }

        private void OnEnable() => playerMap.Enable();

        private void OnDisable() => playerMap.Disable();

        private void OnDestroy()
        {
            if (runtimeActions != null)
                Destroy(runtimeActions);
        }

        private void Update()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null)
                return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            Float2 world = MovementMath.ScreenRelativeDirection(new Float2(input.x, input.y),
                viewCamera.transform.eulerAngles.y);
            var move = new Vector3(world.X, 0f, world.Y);
            Vector3? aim = ReadAimPoint();

            bool networked = networkPlayer != null && networkPlayer.IsSpawned;
            if (networked)
            {
                networkPlayer.SubmitLocalIntent(move, aim);
                if (interactAction.WasPressedThisFrame())
                    networkPlayer.RequestInteract();
                if (attackAction.WasPressedThisFrame() && aim.HasValue)
                    AttackPressed?.Invoke(aim.Value);
            }
            else
            {
                motor.SetIntent(move, aim);
            }
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
