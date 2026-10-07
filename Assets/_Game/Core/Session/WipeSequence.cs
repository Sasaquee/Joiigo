namespace Game.Core.Session
{
    public enum WipePhase
    {
        Idle,
        /// <summary>A tela escurece (D-086). A arena ainda está como estava.</summary>
        Fading,
        /// <summary>A arena zerou e a tela segura o preto antes de clarear.</summary>
        Resetting
    }

    /// <summary>O que aconteceu no Tick.</summary>
    public enum WipeStep
    {
        None,
        /// <summary>O escurecer acabou: zere a arena agora (uma vez por sequência).</summary>
        ResetNow,
        /// <summary>O preto acabou: todos acordam, a tela clareia e, no solo, a partida recomeça sozinha.</summary>
        Finished
    }

    /// <summary>
    /// Sequência do recomeço (D-084, D-086): Idle → Fading (fadeSeconds) → Resetting (holdBlackSeconds) → Idle.
    /// Uma vez começada, sempre termina: com a queda total ninguém pode levantar ninguém, então não há cancelamento
    /// (alguém voltar a ficar de pé durante o Fading, por fim do tempo caído, não interrompe o recomeço).
    /// C# puro: o host anda o relógio e reage aos passos devolvidos pelo Tick.
    /// </summary>
    public sealed class WipeSequence
    {
        private readonly float fadeSeconds;
        private readonly float holdBlackSeconds;
        private float timer;

        public WipeSequence(float fadeSeconds, float holdBlackSeconds)
        {
            this.fadeSeconds = fadeSeconds > 0f ? fadeSeconds : 0f;
            this.holdBlackSeconds = holdBlackSeconds > 0f ? holdBlackSeconds : 0f;
        }

        public WipePhase Phase { get; private set; } = WipePhase.Idle;

        public bool IsRunning => Phase != WipePhase.Idle;

        /// <summary>Quanto da fase atual já passou, em segundos (0 no Idle).</summary>
        public float Elapsed => Phase == WipePhase.Idle ? 0f : timer;

        /// <summary>Começa o escurecer. Só vale no Idle: devolve false se já há uma sequência rodando.</summary>
        public bool Begin()
        {
            if (Phase != WipePhase.Idle)
                return false;
            Phase = WipePhase.Fading;
            timer = 0f;
            return true;
        }

        /// <summary>Anda o relógio. Passo zero, negativo ou NaN não faz nada. Cada Tick avança no máximo uma fase.</summary>
        public WipeStep Tick(float deltaTime)
        {
            if (!(deltaTime > 0f) || Phase == WipePhase.Idle)
                return WipeStep.None;

            timer += deltaTime;
            if (Phase == WipePhase.Fading)
            {
                if (timer < fadeSeconds)
                    return WipeStep.None;
                Phase = WipePhase.Resetting;
                timer = 0f;
                return WipeStep.ResetNow;
            }

            if (timer < holdBlackSeconds)
                return WipeStep.None;
            Phase = WipePhase.Idle;
            timer = 0f;
            return WipeStep.Finished;
        }
    }
}
