using Game.Core.Math;
using Game.Core.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Pegada da vista da câmera no chão (plano do passe do mapa §0 e §5): números da câmera 14 m / 50° / giro 30° / FOV 40°.</summary>
    public class CameraFootprintTests
    {
        // Mesmos números do CameraSettings padrão (o teste confere a geometria, não o asset).
        private static CameraRig Rig() => CameraFootprint.OrbitRig(14f, 50f, 30f, 1f, 40f);

        /// <summary>Distância do ponto ao longo da direção de visão (yaw 30°), a partir do foco: positivo = à frente.</summary>
        private static float Forward(Float2 p, Float2 focus)
        {
            const float yaw = 30f * (System.MathF.PI / 180f);
            return (p.X - focus.X) * System.MathF.Sin(yaw) + (p.Y - focus.Y) * System.MathF.Cos(yaw);
        }

        [Test]
        public void Camera_FicaA9mDoFocoE11e7mDeAltura()
        {
            var rig = Rig();
            Assert.AreEqual(9.0f, rig.Offset.Length, 0.05f);
            Assert.AreEqual(-4.5f, rig.Offset.X, 0.05f);
            Assert.AreEqual(-7.8f, rig.Offset.Y, 0.05f);
            Assert.AreEqual(11.7f, rig.Height, 0.05f);
        }

        [Test]
        public void Cantos_BordaDeCimaToca11m3AFrente_EAtrasVe4m7()
        {
            var corners = new Float2[4];
            Assert.IsTrue(CameraFootprint.TryCorners(Rig(), Float2.Zero, CameraFootprint.Aspect16x9, corners));
            // Ordem: baixo-esquerda, baixo-direita, cima-direita, cima-esquerda; os da borda têm a mesma distância à frente.
            Assert.AreEqual(Forward(corners[2], Float2.Zero), Forward(corners[3], Float2.Zero), 1e-3f);
            Assert.AreEqual(Forward(corners[0], Float2.Zero), Forward(corners[1], Float2.Zero), 1e-3f);
            Assert.AreEqual(-4.7f, Forward(corners[0], Float2.Zero), 0.1f, "Atrás");
            Assert.Greater(Forward(corners[2], Float2.Zero), 11.3f, "Os cantos de cima estão além do meio da borda");
        }

        [Test]
        public void Cantos_MeioDaBordaDeCimaToca11m3AFrente()
        {
            // Com aspecto ~0 os cantos de cima viram o meio da borda.
            var corners = new Float2[4];
            CameraFootprint.TryCorners(Rig(), Float2.Zero, 0.0001f, corners);
            Assert.AreEqual(11.3f, Forward(corners[2], Float2.Zero), 0.1f);
        }

        [Test]
        public void Cantos_Longe18m2Em16x9_E21m9Em21x9()
        {
            var corners = new Float2[4];
            CameraFootprint.TryCorners(Rig(), Float2.Zero, CameraFootprint.Aspect16x9, corners);
            float d169 = CameraFootprint.FarthestDistance(corners, Float2.Zero);
            // O plano dizia ~16 m em 16:9; a conta exata dá 18,2 m (em 21:9 bate: 21,9 m).
            Assert.AreEqual(18.2f, d169, 0.3f, "16:9");

            CameraFootprint.TryCorners(Rig(), Float2.Zero, CameraFootprint.Aspect21x9, corners);
            float d219 = CameraFootprint.FarthestDistance(corners, Float2.Zero);
            Assert.AreEqual(21.9f, d219, 0.3f, "21:9");
            Assert.Greater(d219, d169);
        }

        [Test]
        public void Cantos_AcompanhamOFoco()
        {
            var a = new Float2[4];
            var b = new Float2[4];
            CameraFootprint.TryCorners(Rig(), Float2.Zero, CameraFootprint.Aspect16x9, a);
            var focus = new Float2(10f, -7f);
            CameraFootprint.TryCorners(Rig(), focus, CameraFootprint.Aspect16x9, b);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(a[i].X + 10f, b[i].X, 1e-3f);
                Assert.AreEqual(a[i].Y - 7f, b[i].Y, 1e-3f);
            }
        }

        [Test]
        public void Cantos_FocoLongeDaOrigem_RaioAPartirDaOrigem()
        {
            var corners = new Float2[4];
            var focus = new Float2(0f, 54.5f);
            CameraFootprint.TryCorners(Rig(), focus, CameraFootprint.Aspect21x9, corners);
            float r = CameraFootprint.FarthestDistance(corners, Float2.Zero);
            Assert.Greater(r, 54.5f);
            Assert.Less(r, 80f, "Mesmo no limite do jogador a vista fica dentro de r 80 (plano §1)");
        }

        [Test]
        public void Cantos_HorizonteVisivel_DevolveFalsoEFicaNoAlcanceMaximo()
        {
            // Câmera quase horizontal: a borda de cima passa do horizonte.
            var rig = CameraFootprint.OrbitRig(14f, 15f, 0f, 1f, 60f);
            var corners = new Float2[4];
            bool hit = CameraFootprint.TryCorners(rig, Float2.Zero, CameraFootprint.Aspect16x9, corners, 100f);
            Assert.IsFalse(hit);
            Assert.AreEqual(100f, (corners[2] - rig.Offset).Length, 0.5f);
        }
    }
}
