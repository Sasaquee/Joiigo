using System;
using Game.Core.Math;

namespace Game.Core.Combat
{
    /// <summary>Teste de acerto do golpe em arco (D-020), no plano.</summary>
    public static class ArcHit
    {
        /// <summary>
        /// O alvo (círculo de raio targetRadius) está dentro do arco que sai de origin,
        /// apontando para facing, com alcance range e meia-abertura halfAngleDegrees?
        /// </summary>
        public static bool IsInArc(Float2 origin, Float2 facing, Float2 target, float range, float halfAngleDegrees, float targetRadius)
        {
            Float2 toTarget = target - origin;
            float distance = toTarget.Length;
            if (distance - targetRadius > range)
                return false;
            if (distance <= targetRadius)
                return true; // encostado: sempre acerta

            float facingLength = facing.Length;
            if (facingLength <= 0f)
                return false;

            float cos = (toTarget.X * facing.X + toTarget.Y * facing.Y) / (distance * facingLength);
            float angle = MathF.Acos(System.Math.Clamp(cos, -1f, 1f)) * 180f / MathF.PI;
            // A borda do alvo conta: alarga o arco pelo ângulo que o raio do alvo ocupa.
            float slack = MathF.Asin(System.Math.Clamp(targetRadius / distance, 0f, 1f)) * 180f / MathF.PI;
            return angle <= halfAngleDegrees + slack;
        }
    }
}
