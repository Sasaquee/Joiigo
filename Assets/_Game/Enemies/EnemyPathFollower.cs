using System.Collections.Generic;
using Game.Arena;
using Game.Core.AI;
using Game.Core.Math;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Só no host. Usa a NavMesh apenas para calcular o caminho até o alvo (sem NavMeshAgent): devolve a direção da próxima
    /// quina e o EnemyController move o CharacterController. Sem NavMesh na cena (ou inimigo fora dela), diz que não há caminho
    /// e o EnemyController cai no comportamento antigo (reto + Steer). Nada vai pela rede.
    /// </summary>
    public class EnemyPathFollower : MonoBehaviour
    {
        // Limite da memória de quinas, não é número de jogo: caminhos mais longos são cortados e recalculados a cada 0,4 s.
        private const int MaxCorners = 128;

        [SerializeField] private EnemyNavigationSettings settings;

        private PathCursor cursor;
        private NavMeshPath path;
        private readonly Vector3[] cornerBuffer = new Vector3[MaxCorners];
        private readonly List<Float2> corners = new List<Float2>(MaxCorners);

        public EnemyNavigationSettings Settings => settings;

        /// <summary>Há quinas a seguir.</summary>
        public bool HasPath => cursor != null && cursor.HasPath;

        /// <summary>Para testes e para montar o componente por código.</summary>
        public void Configure(EnemyNavigationSettings newSettings)
        {
            settings = newSettings;
            cursor = null;
        }

        /// <summary>Esquece o caminho (o inimigo foi reto, parou ou morreu): o próximo uso recalcula.</summary>
        public void Release() => cursor?.Invalidate();

        /// <summary>Linha de visão até o alvo contra o cenário (camada Cenario). Jogadores e inimigos não tapam.</summary>
        public bool HasLineOfSight(Vector3 from, Vector3 to, float thickness = 0f)
        {
            if (settings == null)
                return true;
            Vector3 lift = Vector3.up * settings.lineOfSightHeight;
            Vector3 a = from + lift;
            Vector3 b = to + lift;
            if (thickness <= 0.001f)
                return !Physics.Linecast(a, b, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);

            // Com espessura (o orbe do drone tem raio): o raio fino passa rente a uma quina onde o orbe bateria.
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f)
                return true;
            return !Physics.SphereCast(a, thickness, delta / length, out _, length, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Direção (no plano) da próxima quina do caminho até o alvo. False se não há NavMesh, o inimigo ou o alvo estão fora
        /// dela, ou o caminho acabou: quem chama segue reto como antes.
        /// </summary>
        public bool TryGetDirection(Vector3 position, Vector3 target, float deltaTime, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (settings == null)
                return false;

            cursor ??= new PathCursor(settings.ToCursorParams());
            var pos = new Float2(position.x, position.z);
            var tgt = new Float2(target.x, target.z);

            if (cursor.Update(pos, tgt, deltaTime, true) != RepathReason.None)
                Recompute(position, target, pos, tgt);

            if (!cursor.TryGetWaypoint(pos, out Float2 waypoint))
                return false;

            var d = new Vector3(waypoint.X - position.x, 0f, waypoint.Y - position.z);
            if (d.sqrMagnitude < 0.000001f)
                return false;
            direction = d.normalized;
            return true;
        }

        /// <summary>
        /// Recuo do drone: olha a NavMesh à frente. Devolve false se não há NavMesh aqui (quem chama usa o desvio antigo);
        /// com NavMesh, <paramref name="direction"/> é a direção pedida, ou zero se a borda da NavMesh está à frente (fica parado).
        /// </summary>
        public bool TryGetRetreat(Vector3 position, Vector3 away, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (settings == null || !NavMesh.SamplePosition(position, out NavMeshHit from, settings.sampleRadius, NavMesh.AllAreas))
                return false;

            Vector3 end = from.position + away.normalized * settings.retreatProbe;
            if (!NavMesh.Raycast(from.position, end, out _, NavMesh.AllAreas))
                direction = away;
            return true;
        }

        private void Recompute(Vector3 position, Vector3 target, Float2 pos, Float2 tgt)
        {
            corners.Clear();
            path ??= new NavMeshPath();

            if (NavMesh.SamplePosition(position, out NavMeshHit from, settings.sampleRadius, NavMesh.AllAreas)
                && NavMesh.SamplePosition(target, out NavMeshHit to, settings.sampleRadius, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                && path.status != NavMeshPathStatus.PathInvalid)
            {
                int count = path.GetCornersNonAlloc(cornerBuffer);
                for (int i = 0; i < count; i++)
                    corners.Add(new Float2(cornerBuffer[i].x, cornerBuffer[i].z));
            }

            cursor.SetPath(corners, pos, tgt);
        }
    }
}
