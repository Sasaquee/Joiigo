using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.AI
{
    /// <summary>Números do seguidor de caminho (vêm do EnemyNavigationSettings, um ScriptableObject).</summary>
    public readonly struct PathCursorParams
    {
        /// <summary>Distância (m) a que uma quina conta como alcançada.</summary>
        public readonly float ReachRadius;
        /// <summary>Recalcula o caminho a cada tanto (s).</summary>
        public readonly float RepathInterval;
        /// <summary>Recalcula se o alvo andou mais que isto (m) desde o último cálculo.</summary>
        public readonly float TargetMoveThreshold;
        /// <summary>Janela (s) para decidir que o inimigo está preso.</summary>
        public readonly float StuckTime;
        /// <summary>Preso = andou menos que isto (m) na janela.</summary>
        public readonly float StuckDistance;

        public PathCursorParams(float reachRadius, float repathInterval, float targetMoveThreshold, float stuckTime, float stuckDistance)
        {
            ReachRadius = reachRadius;
            RepathInterval = repathInterval;
            TargetMoveThreshold = targetMoveThreshold;
            StuckTime = stuckTime;
            StuckDistance = stuckDistance;
        }
    }

    /// <summary>Por que o caminho precisa ser calculado de novo.</summary>
    public enum RepathReason
    {
        None,
        /// <summary>Ainda não há caminho calculado (ou foi invalidado).</summary>
        NoPath,
        Time,
        TargetMoved,
        Stuck
    }

    /// <summary>
    /// Percorre as quinas de um caminho (calculado pela NavMesh na cena) e diz quando recalcular: por tempo, por quanto o alvo
    /// andou e por inimigo preso. Puro: no plano, X = X e Y = Z do mundo. Só no host.
    /// </summary>
    public class PathCursor
    {
        private readonly PathCursorParams p;
        private readonly List<Float2> corners = new List<Float2>();
        private int index;
        private bool valid;
        private float sinceRepath;
        private Float2 targetAtRepath;

        private bool hasAnchor;
        private Float2 anchor;
        private float stuckTimer;

        public PathCursor(PathCursorParams parameters)
        {
            p = parameters;
        }

        /// <summary>Ainda há quinas a seguir.</summary>
        public bool HasPath => index < corners.Count;

        /// <summary>Quinas que faltam.</summary>
        public int CornersLeft => System.Math.Max(0, corners.Count - index);

        /// <summary>Esquece o caminho: a próxima chamada de Update pede o cálculo (NoPath).</summary>
        public void Invalidate()
        {
            valid = false;
            corners.Clear();
            index = 0;
            ResetStuck();
        }

        /// <summary>
        /// Guarda um caminho novo. Uma lista vazia vale como "sem caminho agora" (fora da NavMesh): o relógio de recálculo
        /// recomeça, para não tentar de novo a cada quadro.
        /// </summary>
        public void SetPath(IReadOnlyList<Float2> newCorners, Float2 position, Float2 target)
        {
            corners.Clear();
            for (int i = 0; i < newCorners.Count; i++)
                corners.Add(newCorners[i]);
            index = 0;
            valid = true;
            sinceRepath = 0f;
            targetAtRepath = target;
            // A janela de "preso" (1,5 s) é maior que o intervalo de recálculo (0,4 s): só começa uma se não houver, senão nunca fecharia.
            if (!hasAnchor)
            {
                anchor = position;
                hasAnchor = true;
                stuckTimer = 0f;
            }
            SkipReached(position);
        }

        /// <summary>
        /// Avança o relógio e diz se é hora de recalcular. <paramref name="moving"/> = o inimigo está andando por este caminho
        /// neste quadro (parado de propósito não conta como preso).
        /// </summary>
        public RepathReason Update(Float2 position, Float2 target, float deltaTime, bool moving)
        {
            if (!valid)
                return RepathReason.NoPath;

            sinceRepath += deltaTime;

            bool stuck = false;
            if (!moving)
            {
                anchor = position;
                hasAnchor = true;
                stuckTimer = 0f;
            }
            else
            {
                if (!hasAnchor)
                {
                    anchor = position;
                    hasAnchor = true;
                    stuckTimer = 0f;
                }
                stuckTimer += deltaTime;
                if (stuckTimer >= p.StuckTime)
                {
                    stuck = Distance(position, anchor) < p.StuckDistance;
                    anchor = position;
                    stuckTimer = 0f;
                }
            }

            if (stuck)
                return RepathReason.Stuck;
            if (Distance(target, targetAtRepath) > p.TargetMoveThreshold)
                return RepathReason.TargetMoved;
            if (sinceRepath >= p.RepathInterval)
                return RepathReason.Time;
            return RepathReason.None;
        }

        /// <summary>Próxima quina a seguir, depois de dar por alcançadas as que estão a ReachRadius ou menos. False se acabaram.</summary>
        public bool TryGetWaypoint(Float2 position, out Float2 waypoint)
        {
            SkipReached(position);
            if (index >= corners.Count)
            {
                waypoint = Float2.Zero;
                return false;
            }
            waypoint = corners[index];
            return true;
        }

        private void SkipReached(Float2 position)
        {
            while (index < corners.Count && Distance(position, corners[index]) <= p.ReachRadius)
                index++;
        }

        private void ResetStuck()
        {
            hasAnchor = false;
            stuckTimer = 0f;
        }

        private static float Distance(Float2 a, Float2 b) => (a - b).Length;
    }

    /// <summary>Regra pura da corrida a distância (D-080).</summary>
    public static class EnemySpeed
    {
        /// <summary>
        /// Multiplicador da velocidade: <paramref name="farMultiplier"/> quando o jogador vivo mais próximo está a mais de
        /// <paramref name="farDistance"/>; senão 1.
        /// </summary>
        public static float FarSprintMultiplier(float distanceToNearestPlayer, float farDistance, float farMultiplier) =>
            distanceToNearestPlayer > farDistance ? farMultiplier : 1f;
    }
}
