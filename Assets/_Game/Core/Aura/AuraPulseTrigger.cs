using System;

namespace Game.Core.Aura
{
    /// <summary>
    /// Aviso de energia cheia (D-067): decide o quadro em que a aura dá o pulso de luz e o som curto.
    /// - Dispara uma vez quando a energia (a mesma suavizada que acende as runas) chega a cheia (AuraMapper.FullEnergy).
    /// - Não repete enquanto continua cheia. Só rearma quando a energia cai abaixo de RearmBelow (histerese): a energia
    ///   suavizada oscilando em volta de cheia não repete o pulso.
    /// - O primeiro quadro só registra: quem nasce (ou acaba de inicializar) com a energia cheia não pulsa.
    /// - Caído não pulsa, e o primeiro quadro depois de levantar também conta como primeiro quadro: levantar com a
    ///   energia cheia não é "encher".
    /// C# puro, sem UnityEngine; a apresentação (PlayerAura) chama Update a cada quadro.
    /// </summary>
    public sealed class AuraPulseTrigger
    {
        private bool primed;  // já viu um quadro de pé desde a criação, o último Reset ou a última queda
        private bool armed;   // a energia esteve abaixo de RearmBelow desde o último pulso

        /// <param name="rearmBelow">Fração de energia abaixo da qual o pulso rearma (limitada a [0, FullEnergy]).</param>
        public AuraPulseTrigger(float rearmBelow = 0.95f)
        {
            RearmBelow = float.IsNaN(rearmBelow) ? 0.95f : System.Math.Clamp(rearmBelow, 0f, AuraMapper.FullEnergy);
        }

        public float RearmBelow { get; }

        /// <summary>
        /// Recebe a fração de energia mostrada (0 a 1) e se o jogador está caído; devolve true só no quadro em que a
        /// energia acabou de encher.
        /// </summary>
        public bool Update(float energy, bool downed)
        {
            if (downed || float.IsNaN(energy))
            {
                Reset();
                return false;
            }

            bool full = energy >= AuraMapper.FullEnergy;
            if (!primed)
            {
                primed = true;
                armed = !full;
                return false;
            }
            if (full)
            {
                if (!armed)
                    return false;
                armed = false;
                return true;
            }
            if (energy < RearmBelow)
                armed = true;
            return false;
        }

        /// <summary>Esquece o passado: o próximo Update volta a ser "primeiro quadro" (não dispara).</summary>
        public void Reset()
        {
            primed = false;
            armed = false;
        }
    }
}
