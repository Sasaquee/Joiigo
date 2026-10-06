using UnityEngine;

namespace Game.Arena
{
    /// <summary>
    /// Números do cristal vivo no mundo (D-069): veios luminosos nas máquinas, trilhos de cristal no chão das avenidas,
    /// lampiões, cristais das torres pulsando e poeira mágica nas avenidas e praças menores do mapa novo (D-073 a D-082).
    /// Asset em Data/Ambience/CrystalAmbienceSettings.asset.
    /// Os brilhos são multiplicadores da emissão do material CristalArcano (PixelPalette); acima de ~1,3 o ciano
    /// estoura em branco com o bloom. Veios e torres leem estes números em jogo (dá para ajustar em Play);
    /// trilhos, lampiões e poeira são montados pelo construtor e pedem "Construir Arena" de novo.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Arena/Crystal Ambience Settings", fileName = "CrystalAmbienceSettings")]
    public class CrystalAmbienceSettings : ScriptableObject
    {
        [Header("Veios nas máquinas (caldeiras, portões, trilhos, pontes de canos)")]
        [Tooltip("Brilho do veio entre dois pulsos (multiplica a emissão do CristalArcano).")]
        [Range(0f, 2f)] public float veinMinGlow = 0.45f;
        [Tooltip("Brilho do veio na crista do pulso.")]
        [Range(0f, 2f)] public float veinMaxGlow = 1.1f;
        [Tooltip("Pulsos por segundo que passam por um ponto do veio.")]
        [Min(0f)] public float veinPulseSpeed = 0.22f;
        [Tooltip("Distância (m) entre duas cristas de luz correndo pelo veio. Velocidade da luz = esta distância × pulsos por segundo.")]
        [Min(0.5f)] public float veinWaveLength = 14f;

        [Header("Cristais das máquinas (caldeira, fornalha, poste, lampião)")]
        [Range(0f, 2f)] public float machineMinGlow = 0.75f;
        [Range(0f, 2f)] public float machineMaxGlow = 1.15f;
        [Min(0f)] public float machinePulseSpeed = 0.3f;

        [Header("Cristais das torres (relógio e pilões)")]
        [Range(0f, 2f)] public float towerMinGlow = 0.6f;
        [Range(0f, 2f)] public float towerMaxGlow = 1.3f;
        [Min(0f)] public float towerPulseSpeed = 0.18f;
        [Tooltip("Quanto a luz da torre sobe e desce junto com o cristal (fração da intensidade).")]
        [Range(0f, 0.8f)] public float towerLightAmplitude = 0.3f;

        [Header("Trilhos de cristal: do núcleo da praça até cada portão (pede Construir Arena)")]
        // O trilho corre pelo chão do meio de cada avenida e boca de rua: é o cristal que a câmera mais vê (ela olha
        // para o chão). Fica apagado até a largada e acende junto com o portão (D-013, D-081); a crista corre da
        // praça para os portões (deslocamento de cada trecho = distância até o núcleo).
        [Tooltip("Distância (m) do núcleo onde o trilho começa, ao longo da avenida: logo depois do núcleo arcano (raio 1,4 m). O trilho passa por cima do TrilhoCobre do ArenaBuilder (que vai até a borda da plataforma, a 12 m).")]
        [Min(0f)] public float trailStartS = 1.6f;
        [Tooltip("Comprimento (m) de cada trecho do veio. Cada trecho pulsa sozinho (um renderer cada); mais curto = crista mais lisa e mais renderers (2,5 m dá ~23 por rua).")]
        [Min(0.5f)] public float trailSegmentLength = 2.5f;
        [Tooltip("Largura (m) do veio de cristal sobre o trilho de cobre. A ~30 m ainda dá 5 a 6 pixels em 1080p.")]
        [Range(0.05f, 0.5f)] public float trailVeinWidth = 0.14f;

        [Header("Lampiões de cristal nas avenidas (pede Construir Arena)")]
        [Tooltip("Distância (m) entre dois lampiões do mesmo lado da avenida. Os dois lados ficam intercalados (meio espaçamento de diferença).")]
        [Min(2f)] public float lampSpacing = 8f;
        [Tooltip("Folga (m) entre o pé do lampião e a linha da fachada. O corredor andável fica livre.")]
        [Range(0.3f, 2f)] public float lampFacadeGap = 0.6f;
        [Tooltip("Uma luz a cada N lampiões de cada avenida (os outros só têm a emissão do cristal).")]
        [Min(1)] public int lampLightEvery = 2;
        [Tooltip("Máximo de luzes pontuais novas (o CityBuilder já usa ~34; o limite do Forward+ é 256, mas cada luz custa nos pixels que ela alcança).")]
        [Range(0, 12)] public int lampMaxLights = 6;
        [Min(1f)] public float lampLightRange = 7f;
        [Tooltip("Intensidade da luz do lampião (fraca, sem sombra; a do CityBuilder é 5).")]
        [Min(0f)] public float lampLightIntensity = 3f;

        [Header("Poeira mágica nas avenidas e praças menores (pede Construir Arena)")]
        // A câmera de jogo (14 m, 50°, FOV 40°) não tem horizonte: a borda de cima da tela cai no chão ~11 m à frente
        // do jogador. Poeira alta ou longe nunca aparece; poeira baixa nas avenidas e nas praças menores aparece quando
        // o jogador passa por lá. Os emissores ficam além do disco de combate e a poeira nunca cai sobre ele;
        // CrystalAmbienceTests confere isso com a câmera real.
        [Tooltip("Raio (m) do disco de combate: a poeira só nasce além dele.")]
        [Min(0f)] public float dustCombatRadius = 26f;
        [Tooltip("Brilho da poeira (abaixo de ~1,4 fica fora do bloom e não compete com a aura).")]
        [Range(0f, 3f)] public float dustBrightness = 1.2f;
        [Tooltip("Fração da poeira em violeta (o resto em ciano, menos o dourado). D-070: 3%.")]
        [Range(0f, 0.5f)] public float dustVioletShare = 0.03f;
        [Tooltip("Fração da poeira em dourado.")]
        [Range(0f, 0.5f)] public float dustGoldShare = 0.1f;
        [Tooltip("Altura (m) do centro da poeira. Varia na caixa da avenida e ±0,9 m na praça menor; sobe até ~0,7 m na vida. Acima de ~4 m ela some da tela de jogo.")]
        [Min(0f)] public float dustMidHeight = 1.5f;
        [Tooltip("Comprimento (m) da caixa de poeira de cada avenida, encostada na ponta de fora da avenida (a largura é a da avenida).")]
        [Min(1f)] public float avenueDustLength = 10.5f;
        [Tooltip("Altura (m) da caixa de poeira da avenida.")]
        [Min(0.5f)] public float avenueDustBoxHeight = 2f;
        [Tooltip("Máximo de partículas vivas por avenida (a câmera vê poucas por vez).")]
        [Range(0, 200)] public int avenueDustMax = 40;
        [Tooltip("Partículas novas por segundo, por avenida (≈ máximo ÷ vida média de 10 s).")]
        [Min(0f)] public float avenueDustPerSecond = 4f;
        [Tooltip("Máximo de partículas vivas por praça menor (o círculo tem o raio da praça).")]
        [Range(0, 200)] public int plazaDustMax = 60;
        [Tooltip("Partículas novas por segundo, por praça menor.")]
        [Min(0f)] public float plazaDustPerSecond = 6f;
    }
}
