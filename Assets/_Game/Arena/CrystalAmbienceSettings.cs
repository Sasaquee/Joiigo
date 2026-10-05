using UnityEngine;

namespace Game.Arena
{
    /// <summary>
    /// Números do cristal vivo no mundo (D-069): veios luminosos nas máquinas, cristais das torres pulsando
    /// e poeira mágica na rua em volta do muro. Asset em Data/Ambience/CrystalAmbienceSettings.asset.
    /// Os brilhos são multiplicadores da emissão do material CristalArcano (PixelPalette); acima de ~1,3 o ciano
    /// estoura em branco com o bloom. Veios e torres leem estes números em jogo (dá para ajustar em Play);
    /// a poeira é montada pelo construtor e pede "Construir Arena" de novo.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Arena/Crystal Ambience Settings", fileName = "CrystalAmbienceSettings")]
    public class CrystalAmbienceSettings : ScriptableObject
    {
        [Header("Veios nas máquinas (caldeiras, cano do muro, portões, pontes de canos)")]
        [Tooltip("Brilho do veio entre dois pulsos (multiplica a emissão do CristalArcano).")]
        [Range(0f, 2f)] public float veinMinGlow = 0.45f;
        [Tooltip("Brilho do veio na crista do pulso.")]
        [Range(0f, 2f)] public float veinMaxGlow = 1.1f;
        [Tooltip("Pulsos por segundo que passam por um ponto do veio.")]
        [Min(0f)] public float veinPulseSpeed = 0.22f;
        [Tooltip("Distância (m) entre duas cristas de luz correndo pelo veio. Velocidade da luz = esta distância × pulsos por segundo.")]
        [Min(0.5f)] public float veinWaveLength = 14f;

        [Header("Cristais das máquinas (caldeira, fornalha, poste)")]
        [Range(0f, 2f)] public float machineMinGlow = 0.75f;
        [Range(0f, 2f)] public float machineMaxGlow = 1.15f;
        [Min(0f)] public float machinePulseSpeed = 0.3f;

        [Header("Cristais das torres (relógio e pilões)")]
        [Range(0f, 2f)] public float towerMinGlow = 0.6f;
        [Range(0f, 2f)] public float towerMaxGlow = 1.3f;
        [Min(0f)] public float towerPulseSpeed = 0.18f;
        [Tooltip("Quanto a luz da torre sobe e desce junto com o cristal (fração da intensidade).")]
        [Range(0f, 0.8f)] public float towerLightAmplitude = 0.3f;

        [Header("Poeira mágica na rua do anel, fora do muro (pede Construir Arena)")]
        // A câmera de jogo (14 m, 50°, FOV 40°) não tem horizonte: a borda de cima da tela cai no chão ~20 m à frente
        // dela. Poeira alta ou longe nunca aparece; poeira baixa logo atrás do muro aparece nas bordas da tela quando
        // o jogador chega perto do muro. O construtor só emite no lado longe da câmera (lá ela nunca fica entre a
        // câmera e a praça); CrystalAmbienceTests confere isso com a câmera real.
        [Tooltip("Máximo de partículas vivas no anel inteiro (a câmera vê poucas por vez: ~3, ~6 perto do muro).")]
        [Range(0, 400)] public int streetDustMax = 120;
        [Tooltip("Partículas novas por segundo.")]
        [Min(0f)] public float streetDustPerSecond = 12f;
        [Tooltip("Brilho da poeira (abaixo de ~1,4 fica fora do bloom e não compete com a aura).")]
        [Range(0f, 3f)] public float dustBrightness = 1.2f;
        [Tooltip("Fração da poeira em violeta (o resto em ciano, menos o dourado).")]
        [Range(0f, 0.5f)] public float dustVioletShare = 0.12f;
        [Tooltip("Fração da poeira em dourado.")]
        [Range(0f, 0.5f)] public float dustGoldShare = 0.1f;
        [Tooltip("Raio interno do anel (m). O muro vai até 26,9 m; abaixo de ~27,8 a poeira entra no muro.")]
        [Min(0f)] public float streetDustInnerRadius = 27.8f;
        [Tooltip("Raio externo do anel (m). As fachadas do primeiro anel começam em ~30,6 m.")]
        [Min(1f)] public float streetDustOuterRadius = 31f;
        [Tooltip("Altura média (m); varia ±0,9 m e sobe até ~0,7 m na vida. Acima de ~4 m ela some da tela de jogo.")]
        [Min(0f)] public float streetDustHeight = 2f;
    }
}
