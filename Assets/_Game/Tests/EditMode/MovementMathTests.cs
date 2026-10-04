using Game.Core.Math;
using Game.Core.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class MovementMathTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void TelaRelativa_SemGiro_WVaiParaFrenteDoMundo()
        {
            Float2 d = MovementMath.ScreenRelativeDirection(new Float2(0f, 1f), 0f);
            Assert.AreEqual(0f, d.X, Tol);
            Assert.AreEqual(1f, d.Y, Tol);
        }

        [Test]
        public void TelaRelativa_ComGiro30_WAcompanhaACamera()
        {
            Float2 d = MovementMath.ScreenRelativeDirection(new Float2(0f, 1f), 30f);
            Assert.AreEqual(0.5f, d.X, Tol);
            Assert.AreEqual(0.8660254f, d.Y, Tol);
        }

        [Test]
        public void TelaRelativa_ComGiro30_DVaiParaADireitaDaTela()
        {
            Float2 d = MovementMath.ScreenRelativeDirection(new Float2(1f, 0f), 30f);
            Assert.AreEqual(0.8660254f, d.X, Tol);
            Assert.AreEqual(-0.5f, d.Y, Tol);
        }

        [Test]
        public void TelaRelativa_Diagonal_NaoAndaMaisRapido()
        {
            Float2 d = MovementMath.ScreenRelativeDirection(new Float2(1f, 1f), 30f);
            Assert.AreEqual(1f, d.Length, Tol);
        }

        [Test]
        public void Approach_NaoUltrapassaOAlvo()
        {
            Float2 v = MovementMath.Approach(Float2.Zero, new Float2(1f, 0f), 5f);
            Assert.AreEqual(new Float2(1f, 0f), v);
        }

        [Test]
        public void Approach_AndaNoMaximoOPasso()
        {
            Float2 v = MovementMath.Approach(Float2.Zero, new Float2(10f, 0f), 2f);
            Assert.AreEqual(2f, v.X, Tol);
            Assert.AreEqual(0f, v.Y, Tol);
        }

        [Test]
        public void StepVelocity_AceleraAteAVelocidadeMaxima()
        {
            Float2 v = Float2.Zero;
            for (int i = 0; i < 60; i++)
                v = MovementMath.StepVelocity(v, new Float2(0f, 1f), 6f, 60f, 80f, 1f / 60f);
            Assert.AreEqual(6f, v.Length, Tol);
        }

        [Test]
        public void StepVelocity_UsaDesaceleracaoAoSoltar()
        {
            Float2 v = MovementMath.StepVelocity(new Float2(0f, 6f), Float2.Zero, 6f, 60f, 80f, 0.05f);
            Assert.AreEqual(6f - 80f * 0.05f, v.Length, Tol);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 90f)]
        [TestCase(-1f, 0f, -90f)]
        [TestCase(0f, -1f, 180f)]
        public void FacingYaw_ApontaParaOAlvo(float x, float y, float expected)
        {
            float yaw = MovementMath.FacingYawDegrees(Float2.Zero, new Float2(x, y));
            Assert.AreEqual(expected, yaw, Tol);
        }
    }
}
