using System;
using Game.Core.Math;

namespace Game.Core.View
{
    /// <summary>Posição da câmera em relação ao foco no chão e orientação, em números simples (sem Unity).</summary>
    public readonly struct CameraRig
    {
        /// <summary>Deslocamento horizontal da câmera em relação ao foco no chão (X = X, Y = Z do mundo).</summary>
        public readonly Float2 Offset;
        /// <summary>Altura da câmera acima do chão (m).</summary>
        public readonly float Height;
        /// <summary>Inclinação para baixo (graus).</summary>
        public readonly float PitchDegrees;
        /// <summary>Giro em volta do eixo vertical, contado de +Z para +X (graus).</summary>
        public readonly float YawDegrees;
        /// <summary>Campo de visão vertical (graus).</summary>
        public readonly float FovDegrees;

        public CameraRig(Float2 offset, float height, float pitchDegrees, float yawDegrees, float fovDegrees)
        {
            Offset = offset;
            Height = height;
            PitchDegrees = pitchDegrees;
            YawDegrees = yawDegrees;
            FovDegrees = fovDegrees;
        }
    }

    /// <summary>
    /// Pegada da vista da câmera no chão (passe do mapa, plano §5): para um foco, os quatro cantos da tela projetados
    /// no plano y = 0. Serve para saber até onde o mapa precisa ter cenário e chão. Mesma geometria do CameraFollow.
    /// </summary>
    public static class CameraFootprint
    {
        public const int CornerCount = 4;

        public const float Aspect16x9 = 16f / 9f;
        public const float Aspect21x9 = 21f / 9f;

        private const float DegToRad = MathF.PI / 180f;

        /// <summary>
        /// Câmera em órbita do CameraFollow: olha para o foco (a <paramref name="focusHeight"/> acima do chão) de
        /// <paramref name="distance"/> m, inclinada e girada. Deslocamento e altura saem do foco.
        /// </summary>
        public static CameraRig OrbitRig(float distance, float pitchDegrees, float yawDegrees, float focusHeight, float fovDegrees)
        {
            float pitch = pitchDegrees * DegToRad;
            float yaw = yawDegrees * DegToRad;
            float horizontal = distance * MathF.Cos(pitch);
            var offset = new Float2(-MathF.Sin(yaw) * horizontal, -MathF.Cos(yaw) * horizontal);
            float height = focusHeight + distance * MathF.Sin(pitch);
            return new CameraRig(offset, height, pitchDegrees, yawDegrees, fovDegrees);
        }

        /// <summary>
        /// Escreve em <paramref name="corners"/> (4 posições) os cantos da tela no chão, na ordem: baixo-esquerda,
        /// baixo-direita, cima-direita, cima-esquerda. <paramref name="aspect"/> = largura / altura. Devolve falso se algum
        /// raio não alcança o chão (horizonte visível); esse canto fica a <paramref name="maxRange"/> m da câmera.
        /// </summary>
        public static bool TryCorners(in CameraRig rig, Float2 focus, float aspect, Float2[] corners, float maxRange = 1000f)
        {
            float pitch = rig.PitchDegrees * DegToRad;
            float yaw = rig.YawDegrees * DegToRad;
            float sinP = MathF.Sin(pitch), cosP = MathF.Cos(pitch);
            float sinY = MathF.Sin(yaw), cosY = MathF.Cos(yaw);

            // Base da câmera (Euler(pitch, yaw, 0) da Unity): frente, direita e cima.
            float fx = cosP * sinY, fy = -sinP, fz = cosP * cosY;
            float rx = cosY, rz = -sinY;
            float ux = sinP * sinY, uy = cosP, uz = sinP * cosY;

            float tanV = MathF.Tan(rig.FovDegrees * 0.5f * DegToRad);
            float tanH = tanV * aspect;
            var camera = focus + rig.Offset;

            bool allHit = true;
            for (int i = 0; i < CornerCount; i++)
            {
                float sx = (i == 1 || i == 2) ? 1f : -1f;
                float sy = i >= 2 ? 1f : -1f;
                float dx = fx + sx * tanH * rx + sy * tanV * ux;
                float dy = fy + sy * tanV * uy;
                float dz = fz + sx * tanH * rz + sy * tanV * uz;

                float horizontalLength = MathF.Sqrt(dx * dx + dz * dz);
                float t;
                if (dy < -1e-5f)
                {
                    t = rig.Height / -dy;
                    if (t * horizontalLength > maxRange)
                    {
                        t = maxRange / MathF.Max(horizontalLength, 1e-5f);
                        allHit = false;
                    }
                }
                else
                {
                    t = maxRange / MathF.Max(horizontalLength, 1e-5f);
                    allHit = false;
                }
                corners[i] = new Float2(camera.X + dx * t, camera.Y + dz * t);
            }
            return allHit;
        }

        /// <summary>Distância do canto mais longe a partir de <paramref name="origin"/> (ex.: o centro do mapa, para o raio da cena).</summary>
        public static float FarthestDistance(Float2[] corners, Float2 origin)
        {
            float best = 0f;
            for (int i = 0; i < CornerCount; i++)
                best = MathF.Max(best, (corners[i] - origin).Length);
            return best;
        }
    }
}
