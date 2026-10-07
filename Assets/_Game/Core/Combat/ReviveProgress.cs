using System.Collections.Generic;

namespace Game.Core.Combat
{
    /// <summary>
    /// Levantar um aliado segurando E por perto (D-083): acumula o tempo segurado por UM aliado dentro do raio.
    /// Soltar ou sair do raio faz o progresso decair a zero em releaseDecaySeconds (0 = zera na hora).
    /// Só quem está caído avança; se a queda acaba antes (volta no spawn, D-003), o progresso zera.
    /// C# puro: o host chama Update a cada quadro e a aura mostra Progress (sem número nem barra).
    /// </summary>
    public sealed class ReviveProgress
    {
        // Evita dividir por zero se os dados vierem zerados: segurar vale completar no primeiro quadro.
        private const float MinSeconds = 1e-4f;

        private readonly float holdSeconds;
        private readonly float releaseDecaySeconds;

        public ReviveProgress(float holdSeconds, float releaseDecaySeconds)
        {
            this.holdSeconds = System.Math.Max(MinSeconds, holdSeconds);
            this.releaseDecaySeconds = System.Math.Max(0f, releaseDecaySeconds);
        }

        /// <summary>Progresso de 0 a 1 (1 = levantou).</summary>
        public float Progress { get; private set; }

        /// <summary>Chegou a 1 no último Update. Fica ligado até o Reset ou até quem caiu deixar de estar caído.</summary>
        public bool Completed { get; private set; }

        /// <summary>Quem caiu está sendo levantado agora (tem progresso e ainda não completou).</summary>
        public bool InProgress => Progress > 0f && !Completed;

        /// <param name="deltaTime">Tempo do quadro (s).</param>
        /// <param name="targetDowned">Quem caiu ainda está caído.</param>
        /// <param name="rescuerHolding">Há um aliado vivo segurando E dentro do raio.</param>
        public void Update(float deltaTime, bool targetDowned, bool rescuerHolding)
        {
            if (!targetDowned)
            {
                Reset();
                return;
            }
            if (Completed || deltaTime <= 0f)
                return;

            if (rescuerHolding)
            {
                Progress += deltaTime / holdSeconds;
                if (Progress >= 1f)
                {
                    Progress = 1f;
                    Completed = true;
                }
            }
            else if (releaseDecaySeconds <= 0f)
            {
                Progress = 0f;
            }
            else
            {
                Progress -= deltaTime / releaseDecaySeconds;
                if (Progress < 0f)
                    Progress = 0f;
            }
        }

        public void Reset()
        {
            Progress = 0f;
            Completed = false;
        }
    }

    /// <summary>Um aliado visto por quem caiu: distância no plano e se ele pode levantar agora (vivo, segurando E, livre).</summary>
    public readonly struct ReviveCandidate
    {
        public readonly float Distance;
        public readonly bool Holding;

        public ReviveCandidate(float distance, bool holding)
        {
            Distance = distance;
            Holding = holding;
        }
    }

    /// <summary>Escolhe o aliado que levanta (D-083): um caído só avança com UM aliado de cada vez, o mais perto.</summary>
    public static class ReviveRescuer
    {
        /// <summary>
        /// Índice do aliado escolhido, ou -1 se ninguém serve. Serve quem segura E e está a no máximo radius.
        /// Quem já levantava (keepIndex) continua enquanto servir, para a escolha não oscilar entre dois aliados a
        /// distâncias parecidas; senão vale o mais perto.
        /// </summary>
        public static int Choose(IReadOnlyList<ReviveCandidate> candidates, float radius, int keepIndex = -1)
        {
            if (candidates == null)
                return -1;
            if (keepIndex >= 0 && keepIndex < candidates.Count && Qualifies(candidates[keepIndex], radius))
                return keepIndex;

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!Qualifies(candidates[i], radius) || candidates[i].Distance >= bestDistance)
                    continue;
                best = i;
                bestDistance = candidates[i].Distance;
            }
            return best;
        }

        private static bool Qualifies(ReviveCandidate c, float radius) => c.Holding && c.Distance <= radius;
    }
}
