using Game.Core.Map;
using UnityEngine;

namespace Game.Arena
{
    /// <summary>
    /// Números do layout do mapa (passe do mapa, D-073 a D-082; tabela da seção 1 de Docs/Tecnico/plano-passe-mapa.md):
    /// praça central, avenidas, praças menores, bocas de rua, spawns, portões, limite do jogador, vedação e envelope
    /// da câmera. Asset em Data/Map/MapLayoutSettings.asset (criado pelo construtor). Quem precisa da geometria lê o
    /// <see cref="Game.Core.Map.MapLayout"/> montado por <see cref="ToLayout"/>: é a fonte única, usada pelo
    /// ArenaBuilder, pelo CityBuilder, pelo spawn e pelos testes. Mudar um número aqui pede "Construir Arena".
    /// Ângulos em graus a partir de +Z, sentido horário. "s" = distância do centro ao longo do eixo da avenida.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Arena/Map Layout Settings", fileName = "MapLayoutSettings")]
    public class MapLayoutSettings : ScriptableObject
    {
        [Header("Praça central")]
        [Tooltip("Raio da linha das fachadas dos prédios em volta da praça (m).")]
        [Min(1f)] public float plazaRadius = 29f;
        [Tooltip("Raio até onde se anda na praça (m); um pouco menor que a fachada.")]
        [Min(1f)] public float plazaWalkRadius = 28.3f;

        [Header("Avenidas")]
        [Tooltip("Um ângulo por rua (graus a partir de +Z, sentido horário). Avenida, praça menor, boca, spawn e portão de mesmo índice formam uma rua. Padrão: NO, N, NE.")]
        public float[] avenueAnglesDeg = { -45f, 0f, 45f };
        [Tooltip("Largura entre fachadas, igual à do trecho andável (m).")]
        [Min(1f)] public float avenueWidth = 9f;
        [Tooltip("Distância s onde a avenida começa (m).")]
        [Min(0f)] public float avenueStartS = 26f;
        [Tooltip("Distância s onde a avenida termina (m).")]
        [Min(0f)] public float avenueEndS = 41f;

        [Header("Praças menores")]
        [Tooltip("Distância s do centro de cada praça menor (m).")]
        [Min(0f)] public float smallPlazaS = 47f;
        [Tooltip("Raio da praça menor (m).")]
        [Min(1f)] public float smallPlazaRadius = 9f;

        [Header("Bocas de rua")]
        [Tooltip("Largura da boca (m).")]
        [Min(1f)] public float mouthWidth = 6f;
        [Tooltip("Distância s onde a boca começa (m).")]
        [Min(0f)] public float mouthStartS = 53f;
        [Tooltip("Distância s onde a boca termina (m).")]
        [Min(0f)] public float mouthEndS = 61f;

        [Header("Inimigos e portões")]
        [Tooltip("Distância s do marcador de spawn dos inimigos, no meio da boca (m).")]
        [Min(0f)] public float enemySpawnS = 57.5f;
        [Tooltip("Raio do marcador de spawn (m).")]
        [Min(0f)] public float enemySpawnRadius = 1.5f;
        [Tooltip("Distância s do portão-máquina que fecha a boca, virado para o centro (m).")]
        [Min(0f)] public float gateS = 60.6f;

        [Header("Limite do jogador e vedação")]
        [Tooltip("Raio máximo em que o jogador pode ficar (m). Padrão: borda de fora das praças menores.")]
        [Min(1f)] public float playerLimitRadius = 56f;
        [Tooltip("Altura das paredes de vedação invisíveis atrás dos prédios (m).")]
        [Min(0.5f)] public float fenceHeight = 6f;

        [Header("Envelope da câmera (altura dos prédios)")]
        [Tooltip("Deslocamento da câmera em relação ao foco no chão, em X (m). Vem de CameraSettings (14 m, 50°, giro 30°).")]
        public float cameraGroundOffsetX = -4.5f;
        [Tooltip("Idem, em Z (m).")]
        public float cameraGroundOffsetZ = -7.8f;
        [Tooltip("Altura da câmera acima do chão (m).")]
        [Min(1f)] public float cameraHeight = 11.7f;
        [Tooltip("Folga abaixo da câmera: prédio no envelope tem no máximo altura da câmera menos esta folga (m).")]
        [Min(0f)] public float cameraHeadroom = 1.5f;
        [Tooltip("Quanto se alarga a área andável deslocada para formar o envelope (m).")]
        [Min(0f)] public float cameraEnvelopeWiden = 1f;

        /// <summary>Números para o Core (imutável).</summary>
        public MapLayoutParams ToParams() => new MapLayoutParams(
            plazaRadius: plazaRadius,
            plazaWalkRadius: plazaWalkRadius,
            avenueAnglesDeg: avenueAnglesDeg,
            avenueWidth: avenueWidth,
            avenueStartS: avenueStartS,
            avenueEndS: avenueEndS,
            smallPlazaS: smallPlazaS,
            smallPlazaRadius: smallPlazaRadius,
            mouthWidth: mouthWidth,
            mouthStartS: mouthStartS,
            mouthEndS: mouthEndS,
            enemySpawnS: enemySpawnS,
            enemySpawnRadius: enemySpawnRadius,
            gateS: gateS,
            playerLimitRadius: playerLimitRadius,
            fenceHeight: fenceHeight,
            cameraGroundOffsetX: cameraGroundOffsetX,
            cameraGroundOffsetZ: cameraGroundOffsetZ,
            cameraHeight: cameraHeight,
            cameraHeadroom: cameraHeadroom,
            cameraEnvelopeWiden: cameraEnvelopeWiden);

        /// <summary>Atalho: a geometria pronta a partir destes números.</summary>
        public MapLayout ToLayout() => new MapLayout(ToParams());
    }
}
