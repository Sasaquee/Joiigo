using System;

namespace Game.Core.Combat
{
    /// <summary>
    /// Bênção de dano do 20 do D20 no coop (D-085): por um tempo, o jogador causa mais dano.
    /// C# puro: o host guarda um por jogador e anda o relógio; os números (duração e bônus) vêm do DiceSettings.
    /// Renovar enquanto ainda está ativa reinicia a duração e troca o multiplicador, não empilha nem soma.
    /// </summary>
    public sealed class BlessingState
    {
        private float multiplier = 1f;

        /// <summary>Segundos que faltam (0 = inativa).</summary>
        public float Remaining { get; private set; }

        public bool Active => Remaining > 0f;

        /// <summary>Multiplicador do dano causado: o da bênção enquanto ativa, 1 quando inativa.</summary>
        public float Multiplier => Active ? multiplier : 1f;

        /// <summary>
        /// Começa a bênção, ou renova se já está ativa: a duração volta ao valor cheio e o multiplicador é o novo,
        /// sem empilhar. Duração zero, negativa ou NaN não faz nada; multiplicador inválido (NaN, infinito ou negativo) vale 1.
        /// </summary>
        public void Begin(float duration, float damageMultiplier)
        {
            if (!(duration > 0f) || float.IsInfinity(duration))
                return;
            Remaining = duration;
            multiplier = float.IsNaN(damageMultiplier) || float.IsInfinity(damageMultiplier) || damageMultiplier < 0f
                ? 1f
                : damageMultiplier;
        }

        /// <summary>Anda o relógio. Passo negativo, zero ou NaN não faz nada; a bênção expira ao chegar a zero.</summary>
        public void Tick(float dt)
        {
            if (!(dt > 0f) || Remaining <= 0f)
                return;
            Remaining = MathF.Max(0f, Remaining - dt);
        }

        /// <summary>Encerra na hora (recomeço da partida, jogador saindo).</summary>
        public void Clear()
        {
            Remaining = 0f;
            multiplier = 1f;
        }
    }
}
