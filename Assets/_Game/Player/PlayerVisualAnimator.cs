using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Animação procedural do Andarilho (D-040). Só visual e só local: cada cliente anima o seu jeito,
    /// a partir do deslocamento do jogador entre quadros, então funciona igual para o dono, o host e os remotos.
    /// Fica no "Corpo" (a instância do modelo). O PlayerLife gira o Corpo quando o jogador cai; aqui só se
    /// mexe nas peças filhas, no espaço do Corpo, então as duas coisas se somam sem brigar.
    /// Peças (pivô na junta, vindas do Tools/Blender/build_character.py): Torso, Cabeca, BracoEsq, BracoDir,
    /// PernaEsq, PernaDir, Capa, Lanterna.
    /// </summary>
    public class PlayerVisualAnimator : MonoBehaviour
    {
        [Header("Peças do modelo")]
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Transform legLeft;
        [SerializeField] private Transform legRight;
        [SerializeField] private Transform cape;
        [SerializeField] private Transform lantern;

        [Tooltip("Quem anda de verdade (a raiz do Player). Vazio = o pai deste objeto.")]
        [SerializeField] private Transform mover;

        [Header("Caminhada")]
        [Tooltip("Velocidade (m/s) em que a passada chega ao máximo.")]
        [SerializeField] private float fullStrideSpeed = 5f;
        [Tooltip("Metros andados por ciclo completo (dois passos).")]
        [SerializeField] private float strideLength = 1.7f;
        [SerializeField] private float legSwing = 30f;
        [SerializeField] private float armSwing = 22f;
        [SerializeField] private float bobHeight = 0.045f;
        [SerializeField] private float walkLean = 7f;

        [Header("Respiração (parado)")]
        [SerializeField] private float breathPeriod = 3.4f;
        [SerializeField] private float breathHeight = 0.01f;

        [Header("Capa e lanterna")]
        [Tooltip("Graus que a capa levanta por m/s de velocidade.")]
        [SerializeField] private float capeTrail = 7f;
        [SerializeField] private float capeMaxAngle = 50f;
        [SerializeField] private float capeStiffness = 60f;
        [SerializeField] private float capeDamping = 9f;
        [SerializeField] private float lanternLength = 0.25f;
        [SerializeField] private float lanternDamping = 2.5f;

        [Header("Golpe")]
        [SerializeField] private float swingDuration = 0.34f;

        // Velocidade suavizada e quanto da passada está valendo (0 parado, 1 andando).
        private const float VelocitySmoothing = 14f;
        private const float WeightSmoothing = 8f;
        private const float TeleportDistance = 3f;
        private const float Gravity = 9.81f;

        private struct Pose
        {
            public Transform part;
            public Vector3 position;   // no espaço do Corpo
            public Quaternion rotation; // no espaço do Corpo
        }

        private Pose torsoPose, headPose, armLPose, armRPose, legLPose, legRPose, capePose, lanternPose;
        private bool captured;

        private PlayerLife life;
        private PlayerCombat combat;

        private Vector3 lastMoverPosition;
        private bool hasLastPosition;
        private Vector3 velocity;        // mundo, suavizada
        private Vector3 lastVelocity;
        private float walkWeight;
        private float phase;              // radianos
        private float breathTime;

        private Vector2 capeAngle, capeAngularVelocity;       // x = para trás (graus), y = para o lado (graus)
        private Vector2 lanternAngle, lanternAngularVelocity; // radianos

        private float swingAge = -1f;
        private float swingYaw;

        private void Awake()
        {
            if (mover == null)
                mover = transform.parent != null ? transform.parent : transform;
            life = GetComponentInParent<PlayerLife>();
            combat = GetComponentInParent<PlayerCombat>();
            Capture();
        }

        private void OnEnable()
        {
            if (combat != null)
                combat.SwingPlayed += OnSwing;
            hasLastPosition = false;
        }

        private void OnDisable()
        {
            if (combat != null)
                combat.SwingPlayed -= OnSwing;
        }

        /// <summary>Liga as peças por código (usado pelo editor ao montar o prefab).</summary>
        public void SetParts(Transform torsoPart, Transform headPart, Transform leftArm, Transform rightArm,
            Transform leftLeg, Transform rightLeg, Transform capePart, Transform lanternPart, Transform moverRoot)
        {
            torso = torsoPart;
            head = headPart;
            armLeft = leftArm;
            armRight = rightArm;
            legLeft = leftLeg;
            legRight = rightLeg;
            cape = capePart;
            lantern = lanternPart;
            mover = moverRoot;
            captured = false;
        }

        private void Capture()
        {
            torsoPose = PoseOf(torso);
            headPose = PoseOf(head);
            armLPose = PoseOf(armLeft);
            armRPose = PoseOf(armRight);
            legLPose = PoseOf(legLeft);
            legRPose = PoseOf(legRight);
            capePose = PoseOf(cape);
            lanternPose = PoseOf(lantern);
            captured = true;
        }

        private Pose PoseOf(Transform part)
        {
            if (part == null)
                return default;
            return new Pose
            {
                part = part,
                position = transform.InverseTransformPoint(part.position),
                rotation = Quaternion.Inverse(transform.rotation) * part.rotation
            };
        }

        private void OnSwing(Vector3 worldDir)
        {
            swingAge = 0f;
            // Gira o tronco um pouco para a direção do golpe, caso o corpo ainda não tenha virado.
            Vector3 fwd = mover.forward;
            fwd.y = 0f;
            worldDir.y = 0f;
            swingYaw = fwd.sqrMagnitude > 0.0001f && worldDir.sqrMagnitude > 0.0001f
                ? Mathf.Clamp(Vector3.SignedAngle(fwd, worldDir, Vector3.up), -45f, 45f)
                : 0f;
        }

        private void LateUpdate()
        {
            if (!captured)
                Capture();
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // ---------- velocidade pelo deslocamento ----------
            Vector3 p = mover.position;
            Vector3 rawVelocity = Vector3.zero;
            if (hasLastPosition)
            {
                Vector3 delta = p - lastMoverPosition;
                delta.y = 0f;
                if (delta.magnitude < TeleportDistance)
                    rawVelocity = delta / dt;
            }
            lastMoverPosition = p;
            hasLastPosition = true;

            lastVelocity = velocity;
            velocity = Vector3.Lerp(velocity, rawVelocity, 1f - Mathf.Exp(-VelocitySmoothing * dt));
            Vector3 acceleration = (velocity - lastVelocity) / dt;

            bool downed = life != null && life.IsDowned;
            float speed = downed ? 0f : velocity.magnitude;
            float targetWeight = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullStrideSpeed));
            walkWeight = Mathf.Lerp(walkWeight, targetWeight, 1f - Mathf.Exp(-WeightSmoothing * dt));

            // Espaço do Corpo, sem a queda: a direção de andar é relativa para onde o jogador olha.
            Quaternion facing = Quaternion.Euler(0f, mover.eulerAngles.y, 0f);
            Vector3 localVel = Quaternion.Inverse(facing) * velocity;
            Vector3 localAcc = Quaternion.Inverse(facing) * acceleration;
            Vector3 moveDir = localVel.sqrMagnitude > 0.01f ? localVel.normalized : Vector3.forward;

            phase = Mathf.Repeat(phase + speed / Mathf.Max(0.1f, strideLength) * Mathf.PI * 2f * dt, Mathf.PI * 2f);
            breathTime += dt;

            float stride = Mathf.Sin(phase);
            float breath = Mathf.Sin(breathTime * Mathf.PI * 2f / Mathf.Max(0.5f, breathPeriod));
            float idle = 1f - walkWeight;

            // ---------- golpe ----------
            float swingArmPitch = 0f, swingArmYaw = 0f, swingTorsoYaw = 0f, swingWeight = 0f;
            if (swingAge >= 0f)
            {
                swingAge += dt;
                float t = swingAge / Mathf.Max(0.05f, swingDuration);
                if (t >= 1f)
                    swingAge = -1f;
                else
                    SwingCurve(t, out swingArmPitch, out swingArmYaw, out swingTorsoYaw, out swingWeight);
            }

            // ---------- tronco ----------
            // Mais baixo com as pernas abertas (|sen| = 1), mais alto quando elas se cruzam.
            float bob = -Mathf.Abs(stride) * bobHeight * walkWeight + breath * breathHeight * idle;
            Quaternion torsoOffset =
                Quaternion.Euler(0f, swingTorsoYaw + swingYaw * swingWeight + stride * 4f * walkWeight, 0f) *
                Swing(walkLean * walkWeight, moveDir);
            Vector3 torsoPos = torsoPose.position + Vector3.up * bob;
            Apply(torsoPose, torsoPos, torsoOffset);

            // ---------- pernas (a frente vai e volta; positivo = pé para trás) ----------
            float legAngle = stride * legSwing * walkWeight;
            Apply(legLPose, legLPose.position, Swing(-legAngle, moveDir));
            Apply(legRPose, legRPose.position, Swing(legAngle, moveDir));

            // ---------- peças presas ao tronco ----------
            float armAngle = stride * armSwing * walkWeight;
            float armRest = 3f + breath * 1.2f * idle; // braços levemente abertos (Z positivo leva a mão para +X)
            AttachToTorso(armLPose, torsoPose.position, torsoPos, torsoOffset,
                Quaternion.Euler(armAngle, 0f, -armRest));
            Quaternion rightArm = Quaternion.Euler(-armAngle, 0f, armRest);
            if (swingWeight > 0f)
                rightArm = Quaternion.Slerp(rightArm, Quaternion.Euler(0f, swingArmYaw, 0f) * Quaternion.Euler(swingArmPitch, 0f, 0f), swingWeight);
            AttachToTorso(armRPose, torsoPose.position, torsoPos, torsoOffset, rightArm);

            // Cabeça: contrabalança o balanço do tronco e acompanha a respiração.
            Quaternion headOffset = Quaternion.Euler(-walkLean * 0.5f * walkWeight + breath * 1.5f * idle,
                -stride * 3f * walkWeight - swingTorsoYaw * 0.5f, 0f);
            AttachToTorso(headPose, torsoPose.position, torsoPos, torsoOffset, headOffset);

            // ---------- capa: mola atrasada que levanta com a velocidade ----------
            Vector2 capeTarget = new Vector2(localVel.z, -localVel.x) * capeTrail;
            capeTarget.x += Mathf.Sin(phase * 2f) * 3f * walkWeight;
            capeTarget = Vector2.ClampMagnitude(capeTarget, capeMaxAngle);
            capeAngularVelocity += ((capeTarget - capeAngle) * capeStiffness - capeAngularVelocity * capeDamping) * dt;
            capeAngle += capeAngularVelocity * dt;
            capeAngle.x = Mathf.Clamp(capeAngle.x, -6f, capeMaxAngle); // nunca entra nas pernas
            capeAngle.y = Mathf.Clamp(capeAngle.y, -capeMaxAngle * 0.6f, capeMaxAngle * 0.6f);
            AttachToTorso(capePose, torsoPose.position, torsoPos, torsoOffset,
                Quaternion.Euler(capeAngle.x, 0f, 0f) * Quaternion.Euler(0f, 0f, capeAngle.y));

            // ---------- lanterna: pêndulo empurrado pela aceleração e pelo passo ----------
            float omega2 = Gravity / Mathf.Max(0.05f, lanternLength);
            Vector2 drive = new Vector2(localAcc.z, -localAcc.x) / Mathf.Max(0.05f, lanternLength);
            drive.x += Mathf.Cos(phase * 2f) * 6f * walkWeight; // tranco de cada passo
            lanternAngularVelocity += (-lanternAngle * omega2 + drive - lanternAngularVelocity * lanternDamping) * dt;
            lanternAngle += lanternAngularVelocity * dt;
            lanternAngle = Vector2.ClampMagnitude(lanternAngle, 1.0f);
            AttachToTorso(lanternPose, torsoPose.position, torsoPos, torsoOffset,
                Quaternion.Euler(lanternAngle.x * Mathf.Rad2Deg, 0f, 0f) * Quaternion.Euler(0f, 0f, lanternAngle.y * Mathf.Rad2Deg));
        }

        /// <summary>
        /// Arco do braço direito: prepara do lado direito, varre pela frente até a esquerda e volta.
        /// pitch negativo levanta o braço para a frente; yaw positivo leva para a direita.
        /// </summary>
        private static void SwingCurve(float t, out float armPitch, out float armYaw, out float torsoYaw, out float weight)
        {
            const float windUp = 0.22f;
            const float strike = 0.55f;
            if (t < windUp)
            {
                float k = Ease(t / windUp);
                armPitch = Mathf.Lerp(0f, -70f, k);
                armYaw = Mathf.Lerp(0f, 75f, k);
                torsoYaw = Mathf.Lerp(0f, 14f, k);
                weight = k;
            }
            else if (t < strike)
            {
                float k = Ease((t - windUp) / (strike - windUp));
                armPitch = Mathf.Lerp(-70f, -88f, k);
                armYaw = Mathf.Lerp(75f, -45f, k);
                torsoYaw = Mathf.Lerp(14f, -16f, k);
                weight = 1f;
            }
            else
            {
                float k = Ease((t - strike) / (1f - strike));
                armPitch = -88f;
                armYaw = -45f;
                torsoYaw = Mathf.Lerp(-16f, 0f, k);
                weight = 1f - k;
            }
        }

        private static float Ease(float x) => x * x * (3f - 2f * x);

        /// <summary>Gira em volta do eixo perpendicular à direção de andar: positivo leva a ponta de baixo para trás.</summary>
        private static Quaternion Swing(float degrees, Vector3 localDir)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, localDir);
            if (axis.sqrMagnitude < 0.0001f)
                axis = Vector3.right;
            return Quaternion.AngleAxis(degrees, axis.normalized);
        }

        /// <summary>Peça presa ao tronco: acompanha o sobe-e-desce e o giro dele, mais o próprio movimento.</summary>
        private void AttachToTorso(Pose pose, Vector3 torsoBase, Vector3 torsoNow, Quaternion torsoOffset, Quaternion ownOffset)
        {
            if (pose.part == null)
                return;
            Vector3 position = torsoNow + torsoOffset * (pose.position - torsoBase);
            Apply(pose, position, torsoOffset * ownOffset);
        }

        private void Apply(Pose pose, Vector3 corpoPosition, Quaternion corpoOffset)
        {
            if (pose.part == null)
                return;
            pose.part.SetPositionAndRotation(transform.TransformPoint(corpoPosition),
                transform.rotation * corpoOffset * pose.rotation);
        }
    }
}
