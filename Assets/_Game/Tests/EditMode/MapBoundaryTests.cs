using System;
using System.Collections.Generic;
using Game.Core.Map;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Segmentos de vedação do mapa (passe do mapa, D-073 a D-082): o contorno da região andável sem as aberturas das
    /// avenidas e bocas, com o fechamento na ponta de cada boca (onde fica o portão). Só o Core.
    /// </summary>
    public class MapBoundaryTests
    {
        // Distância de teste para dentro e para fora da borda: maior que o erro de corda de um arco (5° em r 28,3 = 2,7 cm).
        private const float Probe = 0.06f;
        private const float MaxGap = 0.5f;

        private static IEnumerable<TestCaseData> Layouts()
        {
            yield return new TestCaseData(new MapLayout()).SetName("Padrao");
            yield return new TestCaseData(new MapLayout(new MapLayoutParams(avenueAnglesDeg: new[] { 0f })))
                .SetName("UmaRua");
            yield return new TestCaseData(new MapLayout(new MapLayoutParams(avenueAnglesDeg: new[] { -90f, 0f, 90f, 180f })))
                .SetName("QuatroRuas");
            yield return new TestCaseData(new MapLayout(new MapLayoutParams(avenueAnglesDeg: new[] { -30f, 40f, 130f },
                    avenueWidth: 7f, mouthWidth: 4f, smallPlazaRadius: 7f)))
                .SetName("RuasEstreitas");
        }

        private static Float2 P(float angleDeg, float radius) => MapLayout.Polar(angleDeg, radius);

        private static float Distance(Float2 a, Float2 b) => (a - b).Length;

        private static float DistanceToSegment(Float2 p, MapBoundarySegment s)
        {
            float vx = s.B.X - s.A.X, vz = s.B.Y - s.A.Y;
            float len2 = vx * vx + vz * vz;
            float t = len2 <= 0f ? 0f : ((p.X - s.A.X) * vx + (p.Y - s.A.Y) * vz) / len2;
            t = MathF.Max(0f, MathF.Min(1f, t));
            return Distance(p, new Float2(s.A.X + vx * t, s.A.Y + vz * t));
        }

        private static float DistanceToNearest(IReadOnlyList<MapBoundarySegment> segments, Float2 p)
        {
            float best = float.MaxValue;
            foreach (var s in segments)
                best = MathF.Min(best, DistanceToSegment(p, s));
            return best;
        }

        private static Float2 Step(Float2 p, Float2 dir, float amount) => new Float2(p.X + dir.X * amount, p.Y + dir.Y * amount);

        // ---------------------------------------------------------------- Forma geral

        [Test]
        public void Padrao_GeraSegmentosEmQuantidadeRazoavel()
        {
            var segments = MapBoundary.Segments(new MapLayout());
            // Praça + 3 praças menores em arcos de 5° mais os lados retos: dezenas a poucas centenas.
            Assert.Greater(segments.Count, 60);
            Assert.Less(segments.Count, 600);
            foreach (var s in segments)
                Assert.Greater(s.Length, 0f);
        }

        [TestCaseSource(nameof(Layouts))]
        public void NormalDeForaEUnitaria(MapLayout layout)
        {
            foreach (var s in MapBoundary.Segments(layout))
                Assert.AreEqual(1f, s.Outward.Length, 1e-3f);
        }

        // ---------------------------------------------------------------- Os segmentos ficam na borda

        [TestCaseSource(nameof(Layouts))]
        public void PontoMedioFicaSobreABordaDaRegiaoAndavel(MapLayout layout)
        {
            foreach (var s in MapBoundary.Segments(layout))
            {
                Float2 mid = s.Midpoint;
                Assert.AreEqual(0f, layout.DistanceToWalkable(mid), 1e-3f, $"Ponto médio {mid} do segmento {s.A}-{s.B} não é andável");
                Float2 outside = Step(mid, s.Outward, Probe);
                Assert.Greater(layout.DistanceToWalkable(outside), 0f, $"Logo fora do segmento {s.A}-{s.B} ainda é andável");
            }
        }

        [TestCaseSource(nameof(Layouts))]
        public void NenhumSegmentoAtravessaOInteriorAndavel(MapLayout layout)
        {
            // Em vários pontos de cada segmento: para dentro é andável, para fora não.
            foreach (var s in MapBoundary.Segments(layout))
            {
                for (int k = 1; k <= 4; k++)
                {
                    float t = k / 5f;
                    var p = new Float2(s.A.X + (s.B.X - s.A.X) * t, s.A.Y + (s.B.Y - s.A.Y) * t);
                    Assert.IsTrue(layout.IsWalkable(Step(p, s.Outward, -Probe)), $"Dentro de {p} (segmento {s.A}-{s.B}) deveria ser andável");
                    Assert.IsFalse(layout.IsWalkable(Step(p, s.Outward, Probe)), $"Fora de {p} (segmento {s.A}-{s.B}) deveria ser vazio");
                }
            }
        }

        // ---------------------------------------------------------------- Sem vão

        [TestCaseSource(nameof(Layouts))]
        public void NaoSobraVaoNoContorno(MapLayout layout)
        {
            var segments = MapBoundary.Segments(layout);
            int checkedPoints = 0;
            foreach (var p in OutlinePointsOnBorder(layout, 0.25f))
            {
                float d = DistanceToNearest(segments, p);
                Assert.LessOrEqual(d, MaxGap, $"Vão na borda perto de {p}: o segmento mais próximo está a {d:0.###} m");
                checkedPoints++;
            }
            Assert.Greater(checkedPoints, 500, "A amostragem do contorno devia ter muitos pontos");
        }

        [Test]
        public void FinerArcoGeraMaisSegmentosENaoDeixaVao()
        {
            var layout = new MapLayout();
            var coarse = MapBoundary.Segments(layout, 10f);
            var fine = MapBoundary.Segments(layout, 2.5f);
            Assert.Greater(fine.Count, coarse.Count);
            foreach (var p in OutlinePointsOnBorder(layout, 0.5f))
            {
                // Corda de 10° em r 28,3 erra 10,8 cm; o limite de 0,5 m continua valendo nas duas.
                Assert.LessOrEqual(DistanceToNearest(coarse, p), MaxGap);
                Assert.LessOrEqual(DistanceToNearest(fine, p), MaxGap);
            }
        }

        // ---------------------------------------------------------------- Aberturas e fechamentos do mapa padrão

        [Test]
        public void PontaDaBoca_VemFechadaComUmSegmentoDaLarguraDaBoca()
        {
            var layout = new MapLayout();
            var segments = MapBoundary.Segments(layout);
            foreach (var mouth in layout.Mouths)
            {
                Float2 endCenter = mouth.PointAt(mouth.EndS);
                bool found = false;
                foreach (var s in segments)
                {
                    if (Distance(s.Midpoint, endCenter) < 0.01f)
                    {
                        Assert.AreEqual(mouth.Width, s.Length, 0.01f, "O fechamento cobre a boca de lado a lado");
                        // Aponta para longe do centro, ao longo do eixo da rua.
                        Assert.AreEqual(1f, s.Outward.X * mouth.Axis.X + s.Outward.Y * mouth.Axis.Y, 1e-3f);
                        found = true;
                    }
                }
                Assert.IsTrue(found, $"Sem segmento de fechamento na ponta da boca {mouth.Index}");
            }
        }

        [Test]
        public void AberturasDasRuasFicamSemSegmento()
        {
            var layout = new MapLayout();
            var segments = MapBoundary.Segments(layout);
            foreach (var avenue in layout.Avenues)
            {
                // Avenida para a praça, avenida para a praça menor e praça menor para a boca: o meio da abertura fica livre.
                // A avenida tem 9 m, então a abertura tem 4,5 m de meia largura; a boca tem 3 m.
                Assert.Greater(DistanceToNearest(segments, P(avenue.AngleDeg, layout.Params.PlazaWalkRadius)), 4f,
                    $"Abertura da avenida {avenue.Index} para a praça");
                Assert.Greater(DistanceToNearest(segments, avenue.PointAt(layout.Params.SmallPlazaS - layout.Params.SmallPlazaRadius)), 4f,
                    $"Abertura da avenida {avenue.Index} para a praça menor");
                Assert.Greater(DistanceToNearest(segments, P(avenue.AngleDeg, layout.Params.SmallPlazaS + layout.Params.SmallPlazaRadius)), 2.9f,
                    $"Abertura da praça menor {avenue.Index} para a boca");
            }
        }

        [Test]
        public void EntreDuasAvenidasAPracaTemBordaFechada()
        {
            var layout = new MapLayout();
            var segments = MapBoundary.Segments(layout);
            // Meio da cunha entre as avenidas NO e N, e do lado sul (sem avenida): borda do disco inteira.
            foreach (float angle in new[] { -22.5f, 22.5f, 90f, 180f, -135f })
                Assert.LessOrEqual(DistanceToNearest(segments, P(angle, layout.Params.PlazaWalkRadius)), 0.1f, $"Praça aberta em {angle}°");
        }

        [Test]
        public void LadosDaAvenidaVemDoFimDaPracaAteAPracaMenor()
        {
            var layout = new MapLayout();
            var segments = MapBoundary.Segments(layout);
            var avenue = layout.Avenues[1]; // N, eixo +Z
            float half = layout.Params.AvenueWidth * 0.5f;
            // No meio da avenida, as duas paredes laterais.
            Assert.LessOrEqual(DistanceToNearest(segments, avenue.PointAt(34f, -half)), 0.01f);
            Assert.LessOrEqual(DistanceToNearest(segments, avenue.PointAt(34f, half)), 0.01f);
            // Dentro da praça (s 26 a 27,9) o lado da avenida não conta: é interior.
            Assert.Greater(DistanceToNearest(segments, avenue.PointAt(layout.Params.AvenueStartS + 0.5f, half)), 0.5f);
        }

        // ---------------------------------------------------------------- Argumentos

        [Test]
        public void PassoDeArcoInvalidoOuLayoutNuloSaoRecusados()
        {
            var layout = new MapLayout();
            Assert.Throws<ArgumentOutOfRangeException>(() => MapBoundary.Segments(layout, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapBoundary.Segments(layout, -5f));
            Assert.Throws<ArgumentNullException>(() => MapBoundary.Segments(null));
        }

        // ---------------------------------------------------------------- Amostragem independente do contorno

        /// <summary>
        /// Pontos do contorno da união das formas, achados sem usar o MapBoundary: percorre a borda de cada forma e fica
        /// com o que não está dentro de outra (a 1 cm de folga, para não pegar o toque exato do canto da avenida).
        /// </summary>
        private static IEnumerable<Float2> OutlinePointsOnBorder(MapLayout layout, float spacing)
        {
            var discs = new List<MapDisc> { layout.Plaza };
            var rects = new List<MapRect>();
            for (int i = 0; i < layout.StreetCount; i++)
            {
                discs.Add(layout.SmallPlazas[i].Disc);
                rects.Add(layout.Avenues[i].Rect);
                rects.Add(layout.Mouths[i].Rect);
            }

            bool InsideOther(Float2 p, int discIndex, int rectIndex)
            {
                for (int d = 0; d < discs.Count; d++)
                    if (d != discIndex && discs[d].Contains(p.X, p.Y, 0.01f)) return true;
                for (int r = 0; r < rects.Count; r++)
                    if (r != rectIndex && rects[r].Contains(p.X, p.Y, 0.01f)) return true;
                return false;
            }

            for (int d = 0; d < discs.Count; d++)
            {
                var disc = discs[d];
                int n = (int)MathF.Ceiling(2f * MathF.PI * disc.Radius / spacing);
                for (int k = 0; k < n; k++)
                {
                    float a = 2f * MathF.PI * k / n;
                    var p = new Float2(disc.Center.X + disc.Radius * MathF.Cos(a), disc.Center.Y + disc.Radius * MathF.Sin(a));
                    if (!InsideOther(p, d, -1)) yield return p;
                }
            }

            for (int r = 0; r < rects.Count; r++)
            {
                var rect = rects[r];
                foreach (float across in new[] { -rect.HalfWidth, rect.HalfWidth })
                    for (float along = -rect.HalfLength; along <= rect.HalfLength; along += spacing)
                    {
                        var p = rect.ToWorld(along, across);
                        if (!InsideOther(p, -1, r)) yield return p;
                    }
                foreach (float along in new[] { -rect.HalfLength, rect.HalfLength })
                    for (float across = -rect.HalfWidth; across <= rect.HalfWidth; across += spacing)
                    {
                        var p = rect.ToWorld(along, across);
                        if (!InsideOther(p, -1, r)) yield return p;
                    }
            }
        }
    }
}
