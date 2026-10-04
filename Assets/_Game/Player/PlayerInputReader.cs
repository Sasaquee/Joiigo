using System;
using Game.Cards;
using Game.Core.Cards;
using Game.Core.Math;
using Game.Core.Movement;
using Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Lê o input local (WASD + mouse + F + cartas) e transforma em intenção no mundo:
    /// direção relativa à tela (D-006) e ponto de mira no chão (D-002, D-005).
    /// Teclas: 1–4 skills, Q/E/R cinto (D-029), F interagir (D-031), Tab tiragem (D-032).
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
        private InputAction loadoutAction;
        private readonly InputAction[] skillActions = new InputAction[CardRules.SkillSlots];
        private readonly InputAction[] beltActions = new InputAction[CardRules.BeltSlots];
        private PlayerMotor motor;
        private NetworkPlayer networkPlayer;
        private PlayerCards cards;

        /// <summary>Clique de ataque (D-002): leva o ponto de mira no chão. Só dispara no dono, com a mira válida.</summary>
        public event Action<Vector3> AttackPressed;

        /// <summary>Tab (D-032): abrir/fechar a tela de tiragem. Dispara mesmo sem câmera ou mira.</summary>
        public event Action LoadoutToggled;

        /// <summary>
        /// Ligado pela tela de tiragem enquanto aberta (D-027): ataque, skills e cinto ficam bloqueados.
        /// O movimento e o interagir continuam, porque o jogo segue rodando por trás.
        /// </summary>
        public bool UiBlocksActions { get; set; }

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
            cards = GetComponent<PlayerCards>();
            // Clone por instância: o asset é compartilhado e desligar o mapa de outro jogador desligaria o do dono.
            runtimeActions = Instantiate(actions);
            playerMap = runtimeActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            aimAction = playerMap.FindAction("Aim", throwIfNotFound: true);
            interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
            attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
            loadoutAction = playerMap.FindAction("OpenLoadout", throwIfNotFound: true);
            for (int i = 0; i < skillActions.Length; i++)
                skillActions[i] = playerMap.FindAction("Skill" + (i + 1), throwIfNotFound: true);
            for (int i = 0; i < beltActions.Length; i++)
                beltActions[i] = playerMap.FindAction("Belt" + (i + 1), throwIfNotFound: true);
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
            // A tiragem não depende de câmera nem de mira.
            if (loadoutAction.WasPressedThisFrame())
                LoadoutToggled?.Invoke();

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

                if (!UiBlocksActions && aim.HasValue)
                {
                    if (attackAction.WasPressedThisFrame())
                        AttackPressed?.Invoke(aim.Value);
                    ReadCardInput(aim.Value);
                }
            }
            else
            {
                motor.SetIntent(move, aim);
            }
        }

        /// <summary>Skills 1–4 e cinto Q/E/R: o dono só pede; o host valida energia, recarga e estoque.</summary>
        private void ReadCardInput(Vector3 aimPoint)
        {
            if (cards == null)
                return;

            for (int i = 0; i < skillActions.Length; i++)
                if (skillActions[i].WasPressedThisFrame())
                    cards.RequestUseSkill(i, aimPoint);

            for (int i = 0; i < beltActions.Length; i++)
                if (beltActions[i].WasPressedThisFrame())
                    cards.RequestUseBelt(i, aimPoint);
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
