using Game.Core.Aura;
using UnityEngine;

namespace Game.Aura
{
    /// <summary>
    /// Números da aura (Fase 7, D-060 a D-066). As regras do mapeamento ficam no Core (AuraMapper); aqui ficam os
    /// números de ajuste, os tamanhos do visual e os sons. Asset em Data/Aura/AuraSettings.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Aura/Aura Settings", fileName = "AuraSettings")]
    public class AuraSettings : ScriptableObject
    {
        [Header("Mapeamento (Core)")]
        [Tooltip("Raio da aura com HP zero, em fração do raio cheio (D-061).")]
        [Range(0.1f, 1f)] public float minRadius = 0.55f;
        [Tooltip("Força da aura com HP zero (D-061).")]
        [Range(0f, 1f)] public float minIntensity = 0.2f;
        [Tooltip("Abaixo desta fração de HP a aura falha como lâmpada (D-061).")]
        [Range(0f, 1f)] public float flickerBelow = 0.2f;
        [Tooltip("Abaixo desta fração de HP o batimento toca (D-064).")]
        [Range(0f, 1f)] public float heartbeatBelow = 0.35f;
        [Min(1f)] public float heartbeatSlowBpm = 60f;
        [Min(1f)] public float heartbeatFastBpm = 140f;
        [Tooltip("Velocidade das faíscas com energia zero, em fração da máxima (D-062).")]
        [Range(0f, 1f)] public float sparkMinSpeed = 0.3f;
        [Tooltip("Força da aura de quem está caído: só brasa (D-063).")]
        [Range(0f, 1f)] public float downedIntensity = 0.08f;

        [Header("Visual")]
        [Tooltip("Raio do círculo com HP cheio (m). O anel branco do seu personagem fica por fora (D-066).")]
        [Min(0.2f)] public float fullRadius = 0.9f;
        [Tooltip("Raio do anel branco do seu personagem (m), por fora da aura cheia.")]
        [Min(0.2f)] public float localMarkerRadius = 1.08f;
        [Tooltip("Altura da luz que sobe em volta do corpo (m).")]
        [Min(0.1f)] public float lightHeight = 1.5f;
        [Tooltip("Faíscas por segundo com energia cheia (D-062).")]
        [Min(0f)] public float maxSparksPerSecond = 16f;
        [Tooltip("Velocidade de subida das faíscas com energia cheia (m/s).")]
        [Min(0f)] public float sparkRiseSpeed = 1.8f;
        [Tooltip("Fiapos violeta por segundo com maldição (D-063).")]
        [Min(0f)] public float wispsPerSecond = 5f;
        [Tooltip("Brasas por segundo de quem está caído (D-063).")]
        [Min(0f)] public float embersPerSecond = 5f;
        [Tooltip("Raio da casca de cristal do escudo (m) (D-063).")]
        [Min(0.2f)] public float shellRadius = 1.05f;
        [Tooltip("Rapidez com que a aura acompanha a mudança de HP e energia (maior = mais rápida).")]
        [Min(0.1f)] public float smoothing = 6f;

        [Header("Sons (só para o seu personagem, D-064)")]
        [Range(0f, 1f)] public float heartbeatVolume = 0.35f;
        [Range(0f, 1f)] public float hissVolume = 0.28f;
        [Tooltip("Distância (m) em que um inimigo preparando golpe na sua direção dispara o chiado.")]
        [Min(0f)] public float dangerRadius = 5.5f;
        [Tooltip("Meia-abertura (graus) do cone à frente do inimigo que conta como 'vindo em você'.")]
        [Range(5f, 90f)] public float dangerHalfAngle = 35f;
        [Tooltip("Intervalo mínimo entre dois chiados (s).")]
        [Min(0f)] public float hissCooldown = 0.9f;

        public AuraTuning ToTuning() => new AuraTuning
        {
            MinRadius = minRadius,
            MinIntensity = minIntensity,
            FlickerBelow = flickerBelow,
            HeartbeatBelow = heartbeatBelow,
            HeartbeatSlowBpm = heartbeatSlowBpm,
            HeartbeatFastBpm = heartbeatFastBpm,
            SparkMinSpeed = sparkMinSpeed,
            DownedIntensity = downedIntensity
        };
    }

    /// <summary>Paleta da aura em uso (D-065). No protótipo, trocada por uma tecla de debug.</summary>
    public static class AuraPaletteSwitch
    {
        public static AuraPalette Current { get; private set; } = AuraPalette.Normal;

        public static event System.Action<AuraPalette> Changed;

        public static void Set(AuraPalette palette)
        {
            if (palette == Current)
                return;
            Current = palette;
            Changed?.Invoke(palette);
        }

        public static void Toggle() =>
            Set(Current == AuraPalette.Normal ? AuraPalette.Alternative : AuraPalette.Normal);
    }
}
