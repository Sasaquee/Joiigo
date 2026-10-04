using System;
using Game.Core.Math;

namespace Game.Core.Movement
{
    /// <summary>Regras de movimento em plano (XZ do mundo, representado como X/Y).</summary>
    public static class MovementMath
    {
        private const float DegToRad = MathF.PI / 180f;
        private const float RadToDeg = 180f / MathF.PI;

        /// <summary>
        /// Converte o input do teclado (relativo à tela) em direção no mundo,
        /// usando o giro (yaw) da câmera. W sempre "sobe na tela" (D-006).
        /// </summary>
        public static Float2 ScreenRelativeDirection(Float2 input, float cameraYawDegrees)
        {
            Float2 clamped = input.ClampedToLength(1f);
            float yaw = cameraYawDegrees * DegToRad;
            float sin = MathF.Sin(yaw);
            float cos = MathF.Cos(yaw);
            return new Float2(clamped.X * cos + clamped.Y * sin, -clamped.X * sin + clamped.Y * cos);
        }

        /// <summary>Aproxima a velocidade atual da desejada sem ultrapassar, com passo máximo maxDelta.</summary>
        public static Float2 Approach(Float2 current, Float2 target, float maxDelta)
        {
            Float2 diff = target - current;
            float distance = diff.Length;
            if (distance <= maxDelta || distance == 0f)
                return target;
            return current + diff * (maxDelta / distance);
        }

        /// <summary>
        /// Passo de velocidade de um frame: usa aceleração ao ganhar velocidade
        /// e desaceleração ao parar ou frear (D-007).
        /// </summary>
        public static Float2 StepVelocity(Float2 current, Float2 direction, float maxSpeed,
            float acceleration, float deceleration, float deltaTime)
        {
            Float2 target = direction.ClampedToLength(1f) * maxSpeed;
            float rate = target.Length >= current.Length ? acceleration : deceleration;
            return Approach(current, target, rate * deltaTime);
        }

        /// <summary>Yaw em graus (0 = +Z, 90 = +X) para olhar de "from" até "to" (D-005).</summary>
        public static float FacingYawDegrees(Float2 from, Float2 to)
        {
            Float2 d = to - from;
            return MathF.Atan2(d.X, d.Y) * RadToDeg;
        }
    }
}
