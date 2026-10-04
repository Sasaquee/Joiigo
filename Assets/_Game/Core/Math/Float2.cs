using System;

namespace Game.Core.Math
{
    /// <summary>Vetor 2D do Core, que não pode usar UnityEngine.Vector2. No mundo, X = X e Y = Z.</summary>
    public readonly struct Float2 : IEquatable<Float2>
    {
        public readonly float X;
        public readonly float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Float2 Zero => new Float2(0f, 0f);

        public float Length => MathF.Sqrt(X * X + Y * Y);

        public Float2 ClampedToLength(float maxLength)
        {
            float length = Length;
            return length > maxLength && length > 0f ? this * (maxLength / length) : this;
        }

        public static Float2 operator +(Float2 a, Float2 b) => new Float2(a.X + b.X, a.Y + b.Y);
        public static Float2 operator -(Float2 a, Float2 b) => new Float2(a.X - b.X, a.Y - b.Y);
        public static Float2 operator *(Float2 a, float s) => new Float2(a.X * s, a.Y * s);

        public bool Equals(Float2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Float2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
