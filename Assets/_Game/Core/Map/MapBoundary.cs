using System;
using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Map
{
    /// <summary>
    /// Um trecho reto da vedação: do ponto A ao ponto B (X e Z do mundo, como Float2.X e Float2.Y), com a normal
    /// <see cref="Outward"/> unitária apontando para FORA da região andável (o lado onde a parede invisível fica).
    /// </summary>
    public readonly struct MapBoundarySegment
    {
        public readonly Float2 A;
        public readonly Float2 B;
        public readonly Float2 Outward;

        public MapBoundarySegment(Float2 a, Float2 b, Float2 outward)
        {
            A = a;
            B = b;
            Outward = outward;
        }

        public Float2 Midpoint => new Float2((A.X + B.X) * 0.5f, (A.Y + B.Y) * 0.5f);

        public float Length => (B - A).Length;
    }

    /// <summary>
    /// Contorno da região andável do mapa (passe do mapa, D-073 a D-082) como segmentos de vedação: os arcos de cada disco
    /// (praça e praças menores) e os lados de cada retângulo (avenidas e bocas), menos os trechos que ficam DENTRO de outra
    /// forma andável (as aberturas das avenidas para as praças e das bocas para as praças menores). O lado de fora da ponta
    /// de cada boca (onde fica o portão) é um lado de retângulo como outro qualquer, então vem fechado.
    /// Sem UnityEngine: o construtor da arena transforma cada segmento numa caixa fina de collider e os testes conferem o resultado.
    /// Cada forma é cortada nos pontos exatos em que cruza as outras (círculo com círculo, círculo com reta, reta com reta),
    /// e cada pedaço entre dois cortes é mantido ou descartado pelo ponto do meio. Os arcos viram cordas de
    /// <c>arcStepDeg</c> graus no máximo.
    /// </summary>
    public static class MapBoundary
    {
        /// <summary>Passo de arco padrão (graus): uma corda de 5° num raio de 28 m erra a borda em ~2,7 cm.</summary>
        public const float DefaultArcStepDeg = 5f;

        // O ponto do meio de um pedaço só conta como "dentro" de outra forma se estiver pelo menos 1 mm para dentro:
        // pedaço que só encosta na borda de outra forma (toque exato) continua sendo contorno.
        private const float InsideMargin = 1e-3f;

        // Pedaço mais curto que isso (m) é resto numérico de um toque exato entre formas: descartado.
        private const float MinPieceLength = 1e-3f;

        // Parâmetro mínimo de distância dos extremos de um lado para um corte valer.
        private const float ParamEps = 1e-6f;

        // Folga (no parâmetro 0..1 do lado) para aceitar um corte que cai exatamente num extremo.
        private const float InclusiveSlack = 1e-4f;

        private const float TwoPi = 2f * MathF.PI;

        private readonly struct Shape
        {
            public readonly bool IsDisc;
            public readonly MapDisc Disc;
            public readonly MapRect Rect;

            public Shape(MapDisc disc)
            {
                IsDisc = true;
                Disc = disc;
                Rect = default;
            }

            public Shape(MapRect rect)
            {
                IsDisc = false;
                Disc = default;
                Rect = rect;
            }

            /// <summary>Dentro da forma encolhida de <see cref="InsideMargin"/> (sem a tolerância do Contains).</summary>
            public bool StrictlyContains(float x, float z)
            {
                if (IsDisc)
                {
                    float dx = x - Disc.Center.X, dz = z - Disc.Center.Y;
                    return MathF.Sqrt(dx * dx + dz * dz) < Disc.Radius - InsideMargin;
                }
                Rect.ToLocal(x, z, out float a, out float c);
                return MathF.Abs(a) < Rect.HalfLength - InsideMargin && MathF.Abs(c) < Rect.HalfWidth - InsideMargin;
            }
        }

        /// <summary>
        /// Segmentos de vedação do contorno da região andável de <paramref name="layout"/>.
        /// </summary>
        /// <param name="arcStepDeg">Passo máximo de cada corda nos arcos (graus, maior que 0).</param>
        public static List<MapBoundarySegment> Segments(MapLayout layout, float arcStepDeg = DefaultArcStepDeg)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (!(arcStepDeg > 0f)) throw new ArgumentOutOfRangeException(nameof(arcStepDeg), "O passo de arco precisa ser maior que 0.");

            float stepRad = arcStepDeg * (MathF.PI / 180f);
            var shapes = new List<Shape> { new Shape(layout.Plaza) };
            for (int i = 0; i < layout.StreetCount; i++)
            {
                shapes.Add(new Shape(layout.SmallPlazas[i].Disc));
                shapes.Add(new Shape(layout.Avenues[i].Rect));
                shapes.Add(new Shape(layout.Mouths[i].Rect));
            }

            var result = new List<MapBoundarySegment>();
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].IsDisc)
                    AddDisc(shapes, i, stepRad, result);
                else
                    AddRect(shapes, i, result);
            }
            return result;
        }

        // ---------------------------------------------------------------- Disco

        private static void AddDisc(List<Shape> shapes, int index, float stepRad, List<MapBoundarySegment> output)
        {
            MapDisc disc = shapes[index].Disc;
            var cuts = new List<float>();
            for (int j = 0; j < shapes.Count; j++)
            {
                if (j == index) continue;
                if (shapes[j].IsDisc)
                    CircleCircleCuts(disc, shapes[j].Disc, cuts);
                else
                    CircleRectCuts(disc, shapes[j].Rect, cuts);
            }
            cuts.Sort();

            // Pedaços: entre cortes consecutivos; sem corte nenhum é a circunferência inteira.
            if (cuts.Count == 0)
            {
                AddArcPiece(shapes, index, disc, 0f, TwoPi, stepRad, output);
                return;
            }
            for (int k = 0; k < cuts.Count; k++)
            {
                float t0 = cuts[k];
                float t1 = k + 1 < cuts.Count ? cuts[k + 1] : cuts[0] + TwoPi;
                AddArcPiece(shapes, index, disc, t0, t1, stepRad, output);
            }
        }

        private static void AddArcPiece(List<Shape> shapes, int index, MapDisc disc, float t0, float t1, float stepRad,
            List<MapBoundarySegment> output)
        {
            float span = t1 - t0;
            if (span * disc.Radius < MinPieceLength) return;

            float tm = (t0 + t1) * 0.5f;
            float mx = disc.Center.X + disc.Radius * MathF.Cos(tm);
            float mz = disc.Center.Y + disc.Radius * MathF.Sin(tm);
            if (InsideAnyOther(shapes, index, mx, mz)) return;

            int n = (int)MathF.Ceiling(span / stepRad - 1e-4f);
            if (n < 1) n = 1;
            Float2 prev = OnCircle(disc, t0);
            for (int s = 1; s <= n; s++)
            {
                float ta = t0 + span * s / n;
                Float2 next = s == n ? OnCircle(disc, t1) : OnCircle(disc, ta);
                float tc = t0 + span * (s - 0.5f) / n;
                output.Add(new MapBoundarySegment(prev, next, new Float2(MathF.Cos(tc), MathF.Sin(tc))));
                prev = next;
            }
        }

        private static Float2 OnCircle(MapDisc disc, float angle)
            => new Float2(disc.Center.X + disc.Radius * MathF.Cos(angle), disc.Center.Y + disc.Radius * MathF.Sin(angle));

        private static void CircleCircleCuts(MapDisc a, MapDisc b, List<float> cuts)
        {
            float dx = b.Center.X - a.Center.X, dz = b.Center.Y - a.Center.Y;
            float d = MathF.Sqrt(dx * dx + dz * dz);
            if (d < 1e-6f || d > a.Radius + b.Radius || d < MathF.Abs(a.Radius - b.Radius)) return;

            float along = (a.Radius * a.Radius - b.Radius * b.Radius + d * d) / (2f * d);
            float h2 = a.Radius * a.Radius - along * along;
            float h = h2 > 0f ? MathF.Sqrt(h2) : 0f;
            float ux = dx / d, uz = dz / d;
            float px = a.Center.X + ux * along, pz = a.Center.Y + uz * along;
            AddAngle(cuts, MathF.Atan2(pz + ux * h - a.Center.Y, px - uz * h - a.Center.X));
            AddAngle(cuts, MathF.Atan2(pz - ux * h - a.Center.Y, px + uz * h - a.Center.X));
        }

        private static void CircleRectCuts(MapDisc disc, MapRect rect, List<float> cuts)
        {
            for (int side = 0; side < 4; side++)
            {
                SideEnds(rect, side, out Float2 p, out Float2 q, out _);
                var ts = new List<float>(2);
                SegmentCircleParams(p, q, disc, ts, inclusive: true);
                foreach (float t in ts)
                {
                    float x = p.X + (q.X - p.X) * t, z = p.Y + (q.Y - p.Y) * t;
                    AddAngle(cuts, MathF.Atan2(z - disc.Center.Y, x - disc.Center.X));
                }
            }
        }

        private static void AddAngle(List<float> cuts, float angle)
        {
            if (angle < 0f) angle += TwoPi;
            if (angle >= TwoPi) angle -= TwoPi;
            cuts.Add(angle);
        }

        // ---------------------------------------------------------------- Retângulo

        private static void AddRect(List<Shape> shapes, int index, List<MapBoundarySegment> output)
        {
            MapRect rect = shapes[index].Rect;
            var ts = new List<float>();
            for (int side = 0; side < 4; side++)
            {
                SideEnds(rect, side, out Float2 p, out Float2 q, out Float2 outward);
                ts.Clear();
                ts.Add(0f);
                for (int j = 0; j < shapes.Count; j++)
                {
                    if (j == index) continue;
                    if (shapes[j].IsDisc)
                        SegmentCircleParams(p, q, shapes[j].Disc, ts, inclusive: false);
                    else
                        SegmentRectParams(p, q, shapes[j].Rect, ts);
                }
                ts.Add(1f);
                ts.Sort();

                float length = (q - p).Length;
                for (int k = 0; k + 1 < ts.Count; k++)
                {
                    float t0 = ts[k], t1 = ts[k + 1];
                    if ((t1 - t0) * length < MinPieceLength) continue;

                    float tm = (t0 + t1) * 0.5f;
                    float mx = p.X + (q.X - p.X) * tm, mz = p.Y + (q.Y - p.Y) * tm;
                    if (InsideAnyOther(shapes, index, mx, mz)) continue;

                    var a = new Float2(p.X + (q.X - p.X) * t0, p.Y + (q.Y - p.Y) * t0);
                    var b = new Float2(p.X + (q.X - p.X) * t1, p.Y + (q.Y - p.Y) * t1);
                    output.Add(new MapBoundarySegment(a, b, outward));
                }
            }
        }

        /// <summary>
        /// Extremos e normal de fora de um dos quatro lados do retângulo: 0 = across -hw, 1 = along +hl, 2 = across +hw,
        /// 3 = along -hl (percorridos em volta do retângulo).
        /// </summary>
        private static void SideEnds(MapRect r, int side, out Float2 p, out Float2 q, out Float2 outward)
        {
            float hl = r.HalfLength, hw = r.HalfWidth;
            Float2 s = r.Side;
            switch (side)
            {
                case 0:
                    p = r.ToWorld(-hl, -hw); q = r.ToWorld(hl, -hw); outward = new Float2(-s.X, -s.Y);
                    break;
                case 1:
                    p = r.ToWorld(hl, -hw); q = r.ToWorld(hl, hw); outward = r.Axis;
                    break;
                case 2:
                    p = r.ToWorld(hl, hw); q = r.ToWorld(-hl, hw); outward = s;
                    break;
                default:
                    p = r.ToWorld(-hl, hw); q = r.ToWorld(-hl, -hw); outward = new Float2(-r.Axis.X, -r.Axis.Y);
                    break;
            }
        }

        /// <summary>
        /// Parâmetros t em que o segmento PQ cruza a circunferência do disco. Sem <paramref name="inclusive"/>: só dentro de
        /// (0, 1), para cortar o lado. Com ele: de 0 a 1 inclusive, com folga numérica, porque a circunferência precisa ser
        /// cortada também onde passa exatamente por um canto do retângulo (o canto da avenida na praça menor).
        /// </summary>
        private static void SegmentCircleParams(Float2 p, Float2 q, MapDisc disc, List<float> ts, bool inclusive)
        {
            float dx = q.X - p.X, dz = q.Y - p.Y;
            float fx = p.X - disc.Center.X, fz = p.Y - disc.Center.Y;
            float a = dx * dx + dz * dz;
            if (a < 1e-12f) return;
            float b = 2f * (fx * dx + fz * dz);
            float c = fx * fx + fz * fz - disc.Radius * disc.Radius;
            float delta = b * b - 4f * a * c;
            if (delta < 0f) return;
            float root = MathF.Sqrt(delta);
            AddParam(ts, (-b - root) / (2f * a), inclusive);
            AddParam(ts, (-b + root) / (2f * a), inclusive);
        }

        /// <summary>Parâmetros t (0..1, sem os extremos) em que o segmento PQ cruza o contorno do retângulo.</summary>
        private static void SegmentRectParams(Float2 p, Float2 q, MapRect rect, List<float> ts)
        {
            float rx = q.X - p.X, rz = q.Y - p.Y;
            for (int side = 0; side < 4; side++)
            {
                SideEnds(rect, side, out Float2 a, out Float2 b, out _);
                float sx = b.X - a.X, sz = b.Y - a.Y;
                float denom = rx * sz - rz * sx;
                if (MathF.Abs(denom) < 1e-9f) continue; // paralelos: sem corte (o ponto do meio decide)
                float qpx = a.X - p.X, qpz = a.Y - p.Y;
                float t = (qpx * sz - qpz * sx) / denom;
                float u = (qpx * rz - qpz * rx) / denom;
                if (u < -ParamEps || u > 1f + ParamEps) continue;
                AddParam(ts, t);
            }
        }

        private static void AddParam(List<float> ts, float t, bool inclusive = false)
        {
            if (inclusive)
            {
                if (t >= -InclusiveSlack && t <= 1f + InclusiveSlack)
                    ts.Add(t < 0f ? 0f : t > 1f ? 1f : t);
            }
            else if (t > ParamEps && t < 1f - ParamEps)
                ts.Add(t);
        }

        // ---------------------------------------------------------------- Classificação

        private static bool InsideAnyOther(List<Shape> shapes, int index, float x, float z)
        {
            for (int j = 0; j < shapes.Count; j++)
                if (j != index && shapes[j].StrictlyContains(x, z))
                    return true;
            return false;
        }
    }
}
