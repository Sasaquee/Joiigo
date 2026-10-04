using System.Globalization;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class Float2Tests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void Length_ZeroVector_IsZero()
        {
            var v = new Float2(0f, 0f);
            Assert.AreEqual(0f, v.Length, Tol);
        }

        [Test]
        public void Length_PositiveX_IsX()
        {
            var v = new Float2(3f, 0f);
            Assert.AreEqual(3f, v.Length, Tol);
        }

        [Test]
        public void Length_PositiveY_IsY()
        {
            var v = new Float2(0f, 4f);
            Assert.AreEqual(4f, v.Length, Tol);
        }

        [Test]
        public void Length_Diagonal_IsPythagorean()
        {
            var v = new Float2(3f, 4f);
            Assert.AreEqual(5f, v.Length, Tol);
        }

        [Test]
        public void ClampedToLength_LessThanLimit_NoChange()
        {
            var v = new Float2(1f, 0f);
            var clamped = v.ClampedToLength(2f);
            Assert.AreEqual(v.X, clamped.X, Tol);
            Assert.AreEqual(v.Y, clamped.Y, Tol);
        }

        [Test]
        public void ClampedToLength_GreaterThanLimit_ScalesDown()
        {
            var v = new Float2(3f, 4f); // length 5
            var clamped = v.ClampedToLength(3f);
            Assert.AreEqual(3f, clamped.Length, Tol);
            // direction preserved: (3/5,4/5) scaled to length 3 => (1.8, 2.4)
            Assert.AreEqual(1.8f, clamped.X, Tol);
            Assert.AreEqual(2.4f, clamped.Y, Tol);
        }

        [Test]
        public void ClampedToLength_ZeroVector_StaysZero()
        {
            var v = Float2.Zero;
            var clamped = v.ClampedToLength(0f);
            Assert.AreEqual(0f, clamped.X, Tol);
            Assert.AreEqual(0f, clamped.Y, Tol);
        }

        [Test]
        public void ClampedToLength_LimitZero_ResultZero()
        {
            var v = new Float2(1f, 1f);
            var clamped = v.ClampedToLength(0f);
            Assert.AreEqual(0f, clamped.X, Tol);
            Assert.AreEqual(0f, clamped.Y, Tol);
        }

        [Test]
        public void Operator_Add_AddsComponents()
        {
            var a = new Float2(1f, 2f);
            var b = new Float2(3f, 4f);
            var sum = a + b;
            Assert.AreEqual(4f, sum.X, Tol);
            Assert.AreEqual(6f, sum.Y, Tol);
        }

        [Test]
        public void Operator_Subtract_SubtractsComponents()
        {
            var a = new Float2(5f, 5f);
            var b = new Float2(2f, 3f);
            var diff = a - b;
            Assert.AreEqual(3f, diff.X, Tol);
            Assert.AreEqual(2f, diff.Y, Tol);
        }

        [Test]
        public void Operator_Multiply_ScalesComponents()
        {
            var v = new Float2(2f, -3f);
            var scaled = v * 0.5f;
            Assert.AreEqual(1f, scaled.X, Tol);
            Assert.AreEqual(-1.5f, scaled.Y, Tol);
        }

        [Test]
        public void Equals_SameValues_ReturnsTrue()
        {
            var a = new Float2(1f, 2f);
            var b = new Float2(1f, 2f);
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentValues_ReturnsFalse()
        {
            var a = new Float2(1f, 2f);
            var b = new Float2(1f, 3f);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_SameValues_ReturnsSame()
        {
            var a = new Float2(1f, 2f);
            var b = new Float2(1f, 2f);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        // ToString usa a cultura atual (vírgula em pt-BR); os testes fixam a cultura invariante.
        [Test]
        public void ToString_FormatWithUpToThreeDecimals()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.AreEqual("(1.235, -2.346)", new Float2(1.2345f, -2.3456f).ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ToString_ZeroVector_HasNoDecimals()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.AreEqual("(0, 0)", Float2.Zero.ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}