namespace Game.Core.Combat
{
    public enum LifeState
    {
        Alive,
        Downed
    }

    /// <summary>
    /// HP zero não mata: o jogador cai (D-003). Sem ninguém para levantar, volta no spawn
    /// depois de downedDuration (D-022). Levantar por um aliado fica para quando o coop voltar (P-003).
    /// </summary>
    public class DownedState
    {
        private readonly float downedDuration;
        private float timer;

        public DownedState(float downedDuration)
        {
            this.downedDuration = downedDuration;
        }

        public LifeState State { get; private set; } = LifeState.Alive;
        public float TimeLeft => State == LifeState.Downed ? timer : 0f;

        public void Fall()
        {
            if (State == LifeState.Downed)
                return;
            State = LifeState.Downed;
            timer = downedDuration;
        }

        /// <summary>Avança o tempo caído. Devolve true no frame em que o jogador deve voltar no spawn.</summary>
        public bool Tick(float deltaTime)
        {
            if (State != LifeState.Downed)
                return false;
            timer -= deltaTime;
            if (timer > 0f)
                return false;
            State = LifeState.Alive;
            timer = 0f;
            return true;
        }

        /// <summary>Levantado antes do tempo (aliado, cura).</summary>
        public void Revive()
        {
            State = LifeState.Alive;
            timer = 0f;
        }
    }
}
