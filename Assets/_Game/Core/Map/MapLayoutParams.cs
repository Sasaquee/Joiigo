using System;
using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Map
{
    /// <summary>
    /// Números do layout do mapa (passe do mapa, D-073 a D-082; tabela da seção 1 de Docs/Tecnico/plano-passe-mapa.md).
    /// Imutável. Os valores padrão são só o ponto de partida: em jogo e no construtor eles vêm do
    /// ScriptableObject <c>MapLayoutSettings</c> (Assets/_Game/Data/Map), que chama <c>ToParams()</c>.
    /// Convenção de ângulo: graus a partir de +Z, sentido horário (como Polar() do ArenaBuilder). Distâncias "s":
    /// medidas do centro da praça ao longo do eixo de cada avenida. Avenida, praça menor, boca, spawn e portão de
    /// mesmo índice pertencem à mesma rua.
    /// </summary>
    public sealed class MapLayoutParams
    {
        // Praça central.
        /// <summary>Raio da linha das fachadas da praça central (m).</summary>
        public float PlazaRadius { get; }
        /// <summary>Raio até onde se anda na praça central (m); um pouco menor que a fachada.</summary>
        public float PlazaWalkRadius { get; }

        // Avenidas.
        /// <summary>Ângulo de cada avenida (graus). O tamanho da lista é o número de ruas (o padrão tem 3).</summary>
        public IReadOnlyList<float> AvenueAnglesDeg { get; }
        /// <summary>Largura entre fachadas, e do retângulo andável (m).</summary>
        public float AvenueWidth { get; }
        public float AvenueStartS { get; }
        public float AvenueEndS { get; }

        // Praças menores.
        public float SmallPlazaS { get; }
        public float SmallPlazaRadius { get; }

        // Bocas de rua.
        public float MouthWidth { get; }
        public float MouthStartS { get; }
        public float MouthEndS { get; }

        // Inimigos e portões.
        public float EnemySpawnS { get; }
        /// <summary>Raio do marcador de spawn (m): o spawn precisa ser andável com essa folga.</summary>
        public float EnemySpawnRadius { get; }
        public float GateS { get; }

        // Limite do jogador e vedação.
        /// <summary>Raio máximo em que o jogador pode ficar (m). Padrão = borda de fora das praças menores.</summary>
        public float PlayerLimitRadius { get; }
        /// <summary>Altura das paredes de vedação invisíveis atrás dos prédios (m).</summary>
        public float FenceHeight { get; }

        // Câmera (envelope de altura dos prédios).
        /// <summary>Deslocamento da câmera em relação ao foco, no chão (m, mundo X e Z).</summary>
        public Float2 CameraGroundOffset { get; }
        public float CameraHeight { get; }
        /// <summary>Folga abaixo da câmera: um prédio no envelope tem no máximo CameraHeight - CameraHeadroom (m).</summary>
        public float CameraHeadroom { get; }
        /// <summary>Quanto se alarga a área andável deslocada para formar o envelope (m).</summary>
        public float CameraEnvelopeWiden { get; }

        public MapLayoutParams(
            float plazaRadius = 29f,
            float plazaWalkRadius = 28.3f,
            IReadOnlyList<float> avenueAnglesDeg = null,
            float avenueWidth = 9f,
            float avenueStartS = 26f,
            float avenueEndS = 41f,
            float smallPlazaS = 47f,
            float smallPlazaRadius = 9f,
            float mouthWidth = 6f,
            float mouthStartS = 53f,
            float mouthEndS = 61f,
            float enemySpawnS = 57.5f,
            float enemySpawnRadius = 1.5f,
            float gateS = 60.6f,
            float playerLimitRadius = 56f,
            float fenceHeight = 6f,
            float cameraGroundOffsetX = -4.5f,
            float cameraGroundOffsetZ = -7.8f,
            float cameraHeight = 11.7f,
            float cameraHeadroom = 1.5f,
            float cameraEnvelopeWiden = 1f)
        {
            if (avenueAnglesDeg == null) avenueAnglesDeg = new[] { -45f, 0f, 45f };
            if (avenueAnglesDeg.Count == 0) throw new ArgumentException("O mapa precisa de ao menos uma avenida.", nameof(avenueAnglesDeg));
            if (avenueEndS <= avenueStartS) throw new ArgumentException("A avenida precisa ter comprimento positivo.", nameof(avenueEndS));
            if (mouthEndS <= mouthStartS) throw new ArgumentException("A boca de rua precisa ter comprimento positivo.", nameof(mouthEndS));
            if (avenueWidth <= 0f || mouthWidth <= 0f) throw new ArgumentException("Larguras precisam ser positivas.");

            PlazaRadius = plazaRadius;
            PlazaWalkRadius = plazaWalkRadius;
            var angles = new float[avenueAnglesDeg.Count];
            for (int i = 0; i < angles.Length; i++) angles[i] = avenueAnglesDeg[i];
            AvenueAnglesDeg = Array.AsReadOnly(angles);
            AvenueWidth = avenueWidth;
            AvenueStartS = avenueStartS;
            AvenueEndS = avenueEndS;
            SmallPlazaS = smallPlazaS;
            SmallPlazaRadius = smallPlazaRadius;
            MouthWidth = mouthWidth;
            MouthStartS = mouthStartS;
            MouthEndS = mouthEndS;
            EnemySpawnS = enemySpawnS;
            EnemySpawnRadius = enemySpawnRadius;
            GateS = gateS;
            PlayerLimitRadius = playerLimitRadius;
            FenceHeight = fenceHeight;
            CameraGroundOffset = new Float2(cameraGroundOffsetX, cameraGroundOffsetZ);
            CameraHeight = cameraHeight;
            CameraHeadroom = cameraHeadroom;
            CameraEnvelopeWiden = cameraEnvelopeWiden;
        }

        /// <summary>Valores da tabela da seção 1 do plano.</summary>
        public static MapLayoutParams Default => new MapLayoutParams();
    }
}
