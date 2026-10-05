using System;

namespace Game.Core.Aura
{
    /// <summary>Paleta da aura: a alternativa (azul e amarelo) existe para daltônicos (D-065).</summary>
    public enum AuraPalette
    {
        Normal,
        Alternative
    }

    /// <summary>
    /// Estados que a aura sinaliza; cada um tem forma própria além da cor (D-063).
    /// Podem se combinar, exceto caído, que apaga os outros.
    /// </summary>
    [Flags]
    public enum AuraSignals
    {
        None = 0,
        Shield = 1,
        Curse = 2,
        Downed = 4,
        HurtBonus = 8
    }

    /// <summary>Cor RGB linear de 0 a 1 (o Core não conhece UnityEngine.Color).</summary>
    public readonly struct AuraColor
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;

        public AuraColor(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>
    /// Entrada: frações já normalizadas. O construtor limita a [0,1];
    /// NaN ou infinito viram 0.
    /// </summary>
    public readonly struct AuraInput
    {
        public readonly float Health;   // fração do HP (0 a 1)
        public readonly float Energy;   // fração da energia (0 a 1)
        public readonly AuraSignals Signals;

        public AuraInput(float health, float energy, AuraSignals signals)
        {
            Health = Limit(health);
            Energy = Limit(energy);
            Signals = signals;
        }

        private static float Limit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            return System.Math.Clamp(value, 0f, 1f);
        }
    }

    /// <summary>
    /// Números de ajuste. Os valores vêm de um ScriptableObject do jogo;
    /// os padrões abaixo servem para os testes.
    /// </summary>
    public sealed class AuraTuning
    {
        public float MinRadius = 0.55f;        // raio com HP 0 (fração do raio cheio)
        public float MinIntensity = 0.2f;      // força com HP 0
        public float FlickerBelow = 0.2f;      // abaixo desta fração de HP a aura falha como lâmpada
        public float HeartbeatBelow = 0.35f;   // abaixo desta fração de HP o batimento toca
        public float HeartbeatSlowBpm = 60f;   // batimento na fração HeartbeatBelow
        public float HeartbeatFastBpm = 140f;  // batimento com HP 0
        public float SparkMinSpeed = 0.3f;     // velocidade das faíscas com energia 0
        public float DownedIntensity = 0.08f;  // força da aura de quem está caído (só brasa)
    }

    /// <summary>Saída do mapeamento: os parâmetros visuais da aura, prontos para a apresentação.</summary>
    public readonly struct AuraState
    {
        public readonly float Radius;        // 0..1, multiplica o raio cheio
        public readonly float Intensity;     // 0..1
        public readonly float Flicker;       // 0 = firme, 1 = falhando ao máximo
        public readonly float SparkRate;     // 0..1 da quantidade máxima de faíscas
        public readonly float SparkSpeed;    // 0..1
        public readonly bool RunesFull;      // energia cheia: as runas acendem inteiras
        public readonly AuraSignals Signals; // sinais a mostrar
        public readonly float HeartbeatBpm;  // 0 = sem batimento
        public readonly AuraColor Base;      // cor da aura (cristal)
        public readonly AuraColor Curse;     // fiapos da maldição
        public readonly AuraColor Ember;     // brasa: runas do bônus de dano e aura de quem caiu
        public readonly AuraColor Shell;     // casca de cristal do escudo

        public AuraState(float radius, float intensity, float flicker, float sparkRate, float sparkSpeed,
            bool runesFull, AuraSignals signals, float heartbeatBpm,
            AuraColor baseColor, AuraColor curse, AuraColor ember, AuraColor shell)
        {
            Radius = radius;
            Intensity = intensity;
            Flicker = flicker;
            SparkRate = sparkRate;
            SparkSpeed = sparkSpeed;
            RunesFull = runesFull;
            Signals = signals;
            HeartbeatBpm = heartbeatBpm;
            Base = baseColor;
            Curse = curse;
            Ember = ember;
            Shell = shell;
        }
    }

    /// <summary>
    /// Cores de cada paleta (D-065).
    /// Normal: Base (0.43, 0.94, 1.00) ciano · Curse (0.78, 0.50, 1.00) violeta · Ember (1.00, 0.48, 0.13) laranja · Shell (0.80, 1.00, 1.00).
    /// Alternative: Base (0.15, 0.40, 1.00) azul forte · Curse (1.00, 1.00, 1.00) branco · Ember (1.00, 0.85, 0.10) amarelo · Shell (0.75, 0.85, 1.00).
    /// </summary>
    public static class AuraPalettes
    {
        /// <summary>Cor da aura (cristal): ciano na Normal, azul forte na Alternativa.</summary>
        public static AuraColor Base(AuraPalette palette) =>
            palette == AuraPalette.Normal
                ? new AuraColor(0.43f, 0.94f, 1.00f)
                : new AuraColor(0.15f, 0.40f, 1.00f);

        /// <summary>Fiapos da maldição: violeta na Normal, branco na Alternativa.</summary>
        public static AuraColor Curse(AuraPalette palette) =>
            palette == AuraPalette.Normal
                ? new AuraColor(0.78f, 0.50f, 1.00f)
                : new AuraColor(1.00f, 1.00f, 1.00f);

        /// <summary>Brasa (runas do bônus de dano e aura de quem caiu): laranja na Normal, amarelo na Alternativa.</summary>
        public static AuraColor Ember(AuraPalette palette) =>
            palette == AuraPalette.Normal
                ? new AuraColor(1.00f, 0.48f, 0.13f)
                : new AuraColor(1.00f, 0.85f, 0.10f);

        /// <summary>Casca de cristal do escudo.</summary>
        public static AuraColor Shell(AuraPalette palette) =>
            palette == AuraPalette.Normal
                ? new AuraColor(0.80f, 1.00f, 1.00f)
                : new AuraColor(0.75f, 0.85f, 1.00f);
    }
}
