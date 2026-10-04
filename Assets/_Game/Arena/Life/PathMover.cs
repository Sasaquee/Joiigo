using UnityEngine;

namespace Game.Arena.Life
{
    /// <summary>
    /// Move o objeto por uma lista de pontos (espaço do mundo) a velocidade constante,
    /// virando suavemente (só yaw) para a direção do trajeto e, opcionalmente, balançando na vertical.
    /// Com loop, o último ponto volta ao primeiro; sem loop, vai e volta (ping-pong).
    /// Serve para carroças, dirigíveis, pessoas e o que mais circular pela cidade.
    /// </summary>
    [DisallowMultipleComponent]
    public class PathMover : MonoBehaviour
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        private const float TurnSharpness = 6f;
        private const float BobAmplitude = 0.08f;
        private const float BobFrequency = 2.2f;
        private const float ArriveEpsilon = 0.0001f;

        [SerializeField] private Vector3[] waypoints = new Vector3[0];
        [SerializeField] private float speed = 2f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool bob;

        // Estado de execução (não serializa).
        private bool initialized;
        private Vector3 pathPosition;
        private int target;
        private int direction = 1;
        private float bobPhase;

        /// <summary>
        /// Define o trajeto (coordenadas de mundo). Posiciona o objeto no primeiro ponto
        /// e o vira para o segundo, o que também vale em modo de edição.
        /// </summary>
        public void Configure(Vector3[] newWaypoints, float newSpeed, bool newLoop, bool newBob)
        {
            waypoints = newWaypoints != null ? (Vector3[])newWaypoints.Clone() : new Vector3[0];
            speed = newSpeed;
            loop = newLoop;
            bob = newBob;
            initialized = false;

            if (waypoints.Length > 0)
            {
                transform.position = waypoints[0];
                if (waypoints.Length > 1)
                    FaceDirection(waypoints[1] - waypoints[0], true);
            }
        }

        private void Initialize()
        {
            initialized = true;
            pathPosition = waypoints[0];
            direction = 1;
            target = 1;
            bobPhase = Random.value * Mathf.PI * 2f;
            transform.position = pathPosition;
            FaceDirection(waypoints[1] - waypoints[0], true);
        }

        private void Update()
        {
            if (waypoints == null || waypoints.Length < 2 || speed <= 0f)
                return;
            if (!initialized)
                Initialize();

            float remaining = speed * Time.deltaTime;
            Vector3 moveDir = Vector3.zero;

            // Limite de iterações: evita laço infinito com pontos repetidos.
            for (int guard = 0; guard <= waypoints.Length && remaining > 0f; guard++)
            {
                Vector3 toTarget = waypoints[target] - pathPosition;
                float dist = toTarget.magnitude;
                if (dist <= remaining + ArriveEpsilon)
                {
                    if (dist > ArriveEpsilon)
                        moveDir = toTarget / dist;
                    pathPosition = waypoints[target];
                    remaining -= dist;
                    AdvanceTarget();
                }
                else
                {
                    moveDir = toTarget / dist;
                    pathPosition += moveDir * remaining;
                    remaining = 0f;
                }
            }

            Vector3 pos = pathPosition;
            if (bob)
                pos.y += Mathf.Sin(Time.time * BobFrequency * Mathf.PI * 2f + bobPhase) * BobAmplitude;
            transform.position = pos;

            if (moveDir.sqrMagnitude > 0f)
                FaceDirection(moveDir, false);
        }

        private void AdvanceTarget()
        {
            int next = target + direction;
            if (next >= waypoints.Length)
            {
                if (loop)
                    next = 0;
                else
                {
                    direction = -1;
                    next = waypoints.Length - 2;
                }
            }
            else if (next < 0)
            {
                direction = 1;
                next = 1;
            }
            target = next;
        }

        /// <summary>Gira só em yaw para olhar na direção dada (ignora o componente vertical).</summary>
        private void FaceDirection(Vector3 dir, bool snap)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
                return;

            Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (snap || !Application.isPlaying)
                transform.rotation = desired;
            else
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, 1f - Mathf.Exp(-TurnSharpness * Time.deltaTime));
        }
    }
}
