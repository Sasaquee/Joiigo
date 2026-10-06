using System.Collections.Generic;
using Game.Core.AI;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class PathCursorTests
    {
        // Mesmos números do EnemyNavigationSettings: quina 0,5 m, recálculo 0,4 s / 1,5 m, preso 1,5 s / 0,3 m.
        private static PathCursor Cursor() => new PathCursor(new PathCursorParams(0.5f, 0.4f, 1.5f, 1.5f, 0.3f));

        private static List<Float2> Path(params float[] xy)
        {
            var list = new List<Float2>();
            for (int i = 0; i < xy.Length; i += 2)
                list.Add(new Float2(xy[i], xy[i + 1]));
            return list;
        }

        [Test]
        public void SemCaminhoPedeOCalculo()
        {
            var c = Cursor();
            Assert.AreEqual(RepathReason.NoPath, c.Update(Float2.Zero, new Float2(10f, 0f), 0.1f, true));
            Assert.IsFalse(c.HasPath);
        }

        [Test]
        public void PrimeiraQuinaNaPosicaoDoInimigoEPulada()
        {
            var c = Cursor();
            c.SetPath(Path(0f, 0f, 5f, 0f, 5f, 5f), Float2.Zero, new Float2(5f, 5f));
            Assert.IsTrue(c.TryGetWaypoint(Float2.Zero, out var wp));
            Assert.AreEqual(5f, wp.X, 0.001f);
            Assert.AreEqual(0f, wp.Y, 0.001f);
        }

        [Test]
        public void AvancaDeQuinaQuandoChegaPerto()
        {
            var c = Cursor();
            c.SetPath(Path(0f, 0f, 5f, 0f, 5f, 5f), Float2.Zero, new Float2(5f, 5f));

            Assert.IsTrue(c.TryGetWaypoint(new Float2(4.7f, 0f), out var wp));
            Assert.AreEqual(5f, wp.Y, 0.001f, "Dentro de 0,5 m da quina: vai para a seguinte");
            Assert.AreEqual(1, c.CornersLeft);
        }

        [Test]
        public void NaoAvancaForaDoAlcanceDaQuina()
        {
            var c = Cursor();
            c.SetPath(Path(0f, 0f, 5f, 0f, 5f, 5f), Float2.Zero, new Float2(5f, 5f));

            Assert.IsTrue(c.TryGetWaypoint(new Float2(4.3f, 0f), out var wp));
            Assert.AreEqual(0f, wp.Y, 0.001f, "A 0,7 m ainda segue a mesma quina");
            Assert.AreEqual(2, c.CornersLeft);
        }

        [Test]
        public void ChegarNaUltimaQuinaAcabaOCaminho()
        {
            var c = Cursor();
            c.SetPath(Path(0f, 0f, 5f, 0f), Float2.Zero, new Float2(5f, 0f));
            Assert.IsFalse(c.TryGetWaypoint(new Float2(5f, 0f), out _));
            Assert.IsFalse(c.HasPath);
        }

        [Test]
        public void RecalculaPorTempo()
        {
            var c = Cursor();
            var target = new Float2(5f, 5f);
            c.SetPath(Path(0f, 0f, 5f, 0f, 5f, 5f), Float2.Zero, target);

            Assert.AreEqual(RepathReason.None, c.Update(Float2.Zero, target, 0.3f, false));
            Assert.AreEqual(RepathReason.Time, c.Update(Float2.Zero, target, 0.15f, false));
        }

        [Test]
        public void RecalculaQuandoOAlvoAndaMaisQueOLimite()
        {
            var c = Cursor();
            c.SetPath(Path(0f, 0f, 5f, 0f), Float2.Zero, new Float2(5f, 0f));

            Assert.AreEqual(RepathReason.None, c.Update(Float2.Zero, new Float2(6f, 0f), 0.05f, false), "1 m: dentro do limite");
            Assert.AreEqual(RepathReason.TargetMoved, c.Update(Float2.Zero, new Float2(7f, 0f), 0.05f, false), "2 m: passou de 1,5 m");
        }

        [Test]
        public void RecalculaQuandoOInimigoFicaPreso()
        {
            var c = Cursor();
            var target = new Float2(50f, 0f);
            c.SetPath(Path(0f, 0f, 50f, 0f), Float2.Zero, target);

            // Anda 0,1 m em 1,5 s: preso. O tempo de recálculo é renovado a cada 0,4 s para isolar a regra.
            var pos = Float2.Zero;
            RepathReason last = RepathReason.None;
            for (int i = 0; i < 3; i++)
            {
                last = c.Update(pos, target, 0.5f, true);
                if (last == RepathReason.Time)
                    c.SetPath(Path(0f, 0f, 50f, 0f), pos, target);
                pos = new Float2(pos.X + 0.033f, 0f);
            }
            Assert.AreEqual(RepathReason.Stuck, last);
        }

        [Test]
        public void QuemAndaNormalmenteNaoFicaPreso()
        {
            var c = Cursor();
            var target = new Float2(50f, 0f);
            c.SetPath(Path(0f, 0f, 50f, 0f), Float2.Zero, target);

            var pos = Float2.Zero;
            for (int i = 0; i < 6; i++)
            {
                pos = new Float2(pos.X + 0.5f, 0f);
                var reason = c.Update(pos, target, 0.5f, true);
                Assert.AreNotEqual(RepathReason.Stuck, reason);
                if (reason == RepathReason.Time)
                    c.SetPath(Path(0f, 0f, 50f, 0f), pos, target);
            }
        }

        [Test]
        public void ParadoDeProposito_NaoContaComoPreso()
        {
            var c = Cursor();
            var target = new Float2(50f, 0f);
            c.SetPath(Path(0f, 0f, 50f, 0f), Float2.Zero, target);

            for (int i = 0; i < 6; i++)
            {
                var reason = c.Update(Float2.Zero, target, 0.5f, false);
                Assert.AreNotEqual(RepathReason.Stuck, reason);
                if (reason == RepathReason.Time)
                    c.SetPath(Path(0f, 0f, 50f, 0f), Float2.Zero, target);
            }
        }

        [Test]
        public void InvalidarPedeOCalculoDeNovo()
        {
            var c = Cursor();
            c.SetPath(Path(0f, 0f, 5f, 0f), Float2.Zero, new Float2(5f, 0f));
            c.Invalidate();
            Assert.IsFalse(c.HasPath);
            Assert.AreEqual(RepathReason.NoPath, c.Update(Float2.Zero, new Float2(5f, 0f), 0.1f, true));
        }

        [Test]
        public void CaminhoVazioNaoRepeteOCalculoACadaQuadro()
        {
            var c = Cursor();
            var target = new Float2(5f, 0f);
            c.SetPath(new List<Float2>(), Float2.Zero, target);

            Assert.IsFalse(c.HasPath);
            Assert.AreEqual(RepathReason.None, c.Update(Float2.Zero, target, 0.016f, true));
            Assert.IsFalse(c.TryGetWaypoint(Float2.Zero, out _));
        }

        // ---------- Corrida a distância (D-080) ----------

        [Test]
        public void Corrida_LongeMultiplica()
        {
            Assert.AreEqual(2f, EnemySpeed.FarSprintMultiplier(18.5f, 18f, 2f), 0.0001f);
            Assert.AreEqual(2f, EnemySpeed.FarSprintMultiplier(60f, 18f, 2f), 0.0001f);
        }

        [Test]
        public void Corrida_PertoOuNoLimiteNaoMultiplica()
        {
            Assert.AreEqual(1f, EnemySpeed.FarSprintMultiplier(5f, 18f, 2f), 0.0001f);
            Assert.AreEqual(1f, EnemySpeed.FarSprintMultiplier(18f, 18f, 2f), 0.0001f, "Só acima da distância");
        }
    }
}
