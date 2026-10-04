namespace Game.Core.AI
{
    public enum BrainState
    {
        Idle,
        Chase,
        Windup,
        Recover
    }

    public enum BrainMove
    {
        Hold,
        Toward,
        Away
    }

    /// <summary>Parâmetros de comportamento (vêm do EnemyDefinition, um ScriptableObject).</summary>
    public readonly struct BrainParams
    {
        public readonly float AttackRange;
        /// <summary>Abaixo desta distância o inimigo recua (drone que mantém distância). 0 = nunca recua.</summary>
        public readonly float MinRange;
        /// <summary>Aviso antes do ataque (D-024): carrega por este tempo, depois solta o golpe.</summary>
        public readonly float WindupTime;
        public readonly float RecoverTime;

        public BrainParams(float attackRange, float minRange, float windupTime, float recoverTime)
        {
            AttackRange = attackRange;
            MinRange = minRange;
            WindupTime = windupTime;
            RecoverTime = recoverTime;
        }
    }

    /// <summary>Resultado de um passo do cérebro: para onde andar e se o ataque sai neste frame.</summary>
    public readonly struct BrainOutput
    {
        public readonly BrainState State;
        public readonly BrainMove Move;
        public readonly bool AttackReleased;
        public readonly bool WindupStarted;

        public BrainOutput(BrainState state, BrainMove move, bool attackReleased, bool windupStarted)
        {
            State = state;
            Move = move;
            AttackReleased = attackReleased;
            WindupStarted = windupStarted;
        }
    }

    /// <summary>
    /// Máquina de estados dos inimigos, no host: persegue, para no alcance, avisa (windup),
    /// solta o ataque e se recupera. O drone também recua se o alvo chega perto demais.
    /// A cena só aplica Move e reage a WindupStarted/AttackReleased.
    /// </summary>
    public class EnemyBrain
    {
        private readonly BrainParams p;
        private float timer;

        public EnemyBrain(BrainParams parameters)
        {
            p = parameters;
        }

        public BrainState State { get; private set; } = BrainState.Idle;

        public BrainOutput Tick(bool hasTarget, float distanceToTarget, float deltaTime)
        {
            switch (State)
            {
                case BrainState.Windup:
                    timer -= deltaTime;
                    if (timer > 0f)
                        return new BrainOutput(State, BrainMove.Hold, false, false);
                    State = BrainState.Recover;
                    timer = p.RecoverTime;
                    return new BrainOutput(State, BrainMove.Hold, true, false);

                case BrainState.Recover:
                    timer -= deltaTime;
                    if (timer > 0f)
                        return new BrainOutput(State, BrainMove.Hold, false, false);
                    State = hasTarget ? BrainState.Chase : BrainState.Idle;
                    break;
            }

            if (!hasTarget)
            {
                State = BrainState.Idle;
                return new BrainOutput(State, BrainMove.Hold, false, false);
            }

            if (distanceToTarget <= p.AttackRange && distanceToTarget >= p.MinRange)
            {
                State = BrainState.Windup;
                timer = p.WindupTime;
                return new BrainOutput(State, BrainMove.Hold, false, true);
            }

            State = BrainState.Chase;
            BrainMove move = distanceToTarget < p.MinRange ? BrainMove.Away : BrainMove.Toward;
            return new BrainOutput(State, move, false, false);
        }

        /// <summary>Interrompe o que estiver fazendo (morte, atordoamento futuro).</summary>
        public void Reset()
        {
            State = BrainState.Idle;
            timer = 0f;
        }
    }
}
