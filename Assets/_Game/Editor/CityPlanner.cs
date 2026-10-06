using System;
using System.Collections.Generic;
using Game.Core.Map;
using Game.Core.Math;

namespace Game.EditorTools
{
    /// <summary>Peça do kit de prédios (medidas do Tools/Blender/build_city.py).</summary>
    internal sealed class PieceSpec
    {
        public string Name;
        public float Width;   // ao longo da fachada (com canos e placas que saem dos lados)
        public float Body;    // largura da parede em si
        public float Depth;   // para trás da fachada
        public float Height;  // total (com chaminés)
        public float Eave;    // altura da parede (para apoiar pontes de canos)
        public float Weight;  // peso base no sorteio
        public float FarBias; // quanto o peso cresce no lado longe da câmera
        public bool Centered; // pivô no centro (e não na fachada)
        public bool Filler;   // só entra quando nenhuma outra cabe (tanque d'água)
    }

    /// <summary>Um prédio decidido pelo planejador: onde fica, para onde a frente olha e as caixas dele no plano.</summary>
    internal sealed class BuildingPlan
    {
        public PieceSpec Piece;
        public string Region;      // "Avenida0", "Boca1", "PracaMenor2", "Praca", "Quarteirao", "Fundo"...
        public int Street = -1;    // índice da rua (avenida, boca ou praça menor) ou -1
        public int Side;           // -1 ou +1 numa faixa de avenida
        public float Along = float.NaN; // posição s ao longo da rua (centro da peça) numa faixa
        public Float2 Pivot;       // pivô da peça (centro da fachada; centro da base se Centered)
        public Float2 Forward;     // direção da frente (para a rua ou praça)
        public float ScaleX = 1f;  // esticada no sentido da fachada
        public bool Mirror;
        public MapRect Footprint;  // planta: da fachada ao fundo, com a largura toda
        public MapRect Collider;   // caixa do collider: corpo x fundo, 0,3 m à frente da fachada
        public float ColliderHeight => Piece.Height;
    }

    /// <summary>Vão na borda andável que nenhum prédio fecha: o construtor põe Caixotes (bloqueio só visual).</summary>
    internal struct GapSpot
    {
        public Float2 Position;
        public Float2 Tangent;
        public float Length;
    }

    /// <summary>
    /// Planejador da cidade do mapa novo (passe do mapa, D-073 a D-082). Só matemática, sem UnityEngine: decide onde
    /// cada prédio fica a partir do <see cref="MapLayout"/>; o CityBuilder instancia os modelos e cria os colliders.
    /// Ordem de prioridade: faixas das avenidas, faixas das bocas e fecho atrás do portão, arcos das praças menores,
    /// arco da praça central, quarteirões e fundo (varreduras até a distância de cobertura). Cada peça só entra se a planta
    /// não encosta na área andável (a caixa de colisão chega até 2 cm da borda), não bate noutra peça e respeita
    /// <see cref="MapLayout.MaxBuildingHeight(MapRect)"/> (D-076).
    /// </summary>
    internal sealed class CityPlanner
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        /// <summary>Quanto o collider avança à frente da fachada (degrau e soleira).</summary>
        public const float ColliderFront = 0.3f;
        /// <summary>Fachadas das ruas ficam esta distância atrás da borda andável: o collider chega 2 cm além da borda.</summary>
        public const float FacadeSetback = 0.32f;
        /// <summary>Folga mínima (m) entre a caixa de colisão e a área andável.</summary>
        public const float WalkableClearance = 0.02f;
        /// <summary>Tolerância de encosto entre plantas vizinhas (m).</summary>
        private const float TouchTolerance = 0.05f;
        /// <summary>Até que distância da área andável (m) há prédios: a câmera nunca vê mais longe que ~22 m dela.</summary>
        public const float DefaultCoverDistance = 30f;
        /// <summary>Raio máximo do centro de um prédio (o chão visual vai até 85; o fundo do prédio chega a ~7 m além do centro).</summary>
        public const float DefaultMaxCenterRadius = 78f;
        /// <summary>Peça esticada para fechar a faixa: no máximo +25%.</summary>
        private const float MaxStretch = 1.25f;
        private const int StripTrials = 40;
        /// <summary>Na escolha da melhor tentativa, cada tanque d'água custa estes metros de sobra (prefere fachada a tanque) e o sorteio varia o resultado.</summary>
        private const float FillerPenalty = 1.5f;
        private const float ScoreJitter = 1.5f;
        /// <summary>Tanques d'água (peça de enchimento) no máximo: a cidade não vira um campo de tanques.</summary>
        private const int MaxFillers = 24;
        private const float MinGap = 0.25f, MaxGap = 0.7f;
        private const float SmallPlazaMaxWidth = 8.8f; // peças estreitas ao redor das praças menores
        private const float MinPieceWidth = 4.6f;  // o menor Width do catálogo (tanque d'água)
        private const float GapSample = 0.75f;
        /// <summary>Profundidade (m) atrás da borda andável em que se procura um collider: só buraco fundo vira vão (as frestas rasas entre a quina reta de um prédio e o arco da praça não).</summary>
        private const float GapProbe = 1.6f;
        private const int GapMinSamples = 3;

        public static readonly PieceSpec[] Catalog =
        {
            new PieceSpec { Name = "Oficina", Width = 8.7f, Body = 8f, Depth = 7.5f, Height = 8.9f, Eave = 4.6f, Weight = 1.1f, FarBias = -0.5f },
            new PieceSpec { Name = "CasaLarga", Width = 10.9f, Body = 10f, Depth = 9.3f, Height = 12.4f, Eave = 7.4f, Weight = 1f, FarBias = 0f },
            new PieceSpec { Name = "CasaEstreitaA", Width = 6.8f, Body = 6f, Depth = 8.5f, Height = 15.2f, Eave = 10.4f, Weight = 1f, FarBias = 0.2f },
            new PieceSpec { Name = "CasaEstreitaB", Width = 6.8f, Body = 6f, Depth = 8.5f, Height = 18.4f, Eave = 13.6f, Weight = 0.7f, FarBias = 0.5f },
            new PieceSpec { Name = "CasaAlta", Width = 7.6f, Body = 7f, Depth = 8.3f, Height = 21.6f, Eave = 17.3f, Weight = 0.4f, FarBias = 0.8f },
            new PieceSpec { Name = "Fabrica", Width = 14.4f, Body = 14f, Depth = 10.2f, Height = 20.4f, Eave = 7f, Weight = 0.25f, FarBias = 0.5f },
            new PieceSpec { Name = "TanqueAgua", Width = 4.6f, Body = 4.2f, Depth = 4.6f, Height = 10.1f, Eave = 0f, Weight = 0.35f, FarBias = 0f, Centered = true, Filler = true },
        };

        private struct Occupant
        {
            public MapRect Rect;
            public Float2 Center;
            public float Radius;
        }

        private sealed class Strip
        {
            public string Region;
            public int Street;
            public int Side;
            public Float2 Front;              // frente dos prédios (para o eixo da rua)
            public Func<float, Float2> Facade; // ponto da linha das fachadas à distância s do centro
            public float S0, S1;
        }

        private readonly MapLayout layout;
        private readonly Random rng;
        private readonly float cover;
        private readonly float maxRadius;
        private readonly List<BuildingPlan> plans = new List<BuildingPlan>();
        private readonly List<Occupant> occupied = new List<Occupant>();
        private readonly List<GapSpot> gaps = new List<GapSpot>();

        public CityPlanner(MapLayout layout, int seed, float coverDistance = DefaultCoverDistance, float maxCenterRadius = DefaultMaxCenterRadius)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            rng = new Random(seed);
            cover = coverDistance;
            maxRadius = maxCenterRadius;
        }

        public MapLayout Layout => layout;
        public IReadOnlyList<BuildingPlan> Buildings => plans;
        public IReadOnlyList<GapSpot> Gaps => gaps;

        /// <summary>Planeja tudo, na ordem de prioridade, e fecha os vãos da borda andável.</summary>
        public void Plan()
        {
            plans.Clear();
            occupied.Clear();
            gaps.Clear();
            fillerCount = 0;

            int n = layout.StreetCount;
            // 1) Faixas retas: avenidas e bocas, com o fecho atrás do portão (a avenida tem prioridade: fachada reta e contínua).
            for (int i = 0; i < n; i++)
                for (int side = -1; side <= 1; side += 2)
                    CommitStrip(AvenueStrip(i, side));
            for (int i = 0; i < n; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                    CommitStrip(MouthStrip(i, side));
                PlanMouthCap(i);
            }
            // 2) Arcos das praças menores (peças estreitas) e da praça central, nos trechos que as faixas deixaram livres.
            for (int i = 0; i < n; i++)
            {
                var sp = layout.SmallPlazas[i];
                FillArc($"PracaMenor{i}", i, sp.Center, sp.Radius + FacadeSetback, SmallPlazaMaxWidth);
            }
            FillArc("Praca", -1, Float2.Zero, layout.PlazaFacadeRadius, float.PositiveInfinity);
            // 3) Quarteirões e fundo: varreduras de dentro para fora, cada peça no raio mais perto em que cabe.
            for (int pass = 0; pass < 8; pass++)
                if (Sweep() == 0)
                    break;
            // 4) O que sobrar de pátio: peças viradas para a área andável mais perto, da borda para fora.
            GridFill();
            SealGaps();
        }

        // ---------- Geometria das peças ----------

        private static float Dot(Float2 a, Float2 b) => a.X * b.X + a.Y * b.Y;

        private static Float2 Unit(Float2 v)
        {
            float l = v.Length;
            return l > 1e-6f ? v * (1f / l) : new Float2(0f, 1f);
        }

        /// <summary>Planta de uma peça: da fachada (mais <paramref name="frontExt"/> para a frente) ao fundo, com a largura dada.</summary>
        private static MapRect PieceRect(PieceSpec p, Float2 pivot, Float2 fwd, float width, float frontExt)
        {
            if (p.Centered)
                return new MapRect(pivot, fwd, p.Depth * 0.5f, width * 0.5f);
            Float2 center = pivot + fwd * ((frontExt - p.Depth) * 0.5f);
            return new MapRect(center, fwd, (p.Depth + frontExt) * 0.5f, width * 0.5f);
        }

        private static MapRect Shrunk(MapRect r, float amount)
            => new MapRect(r.Center, r.Axis, Math.Max(0f, r.HalfLength - amount), Math.Max(0f, r.HalfWidth - amount));

        private bool TouchesWalkable(MapRect rect)
        {
            if (layout.Plaza.Intersects(rect))
                return true;
            for (int i = 0; i < layout.StreetCount; i++)
            {
                if (layout.Avenues[i].Rect.Intersects(rect) || layout.Mouths[i].Rect.Intersects(rect))
                    return true;
                if (layout.SmallPlazas[i].Disc.Intersects(rect))
                    return true;
            }
            return false;
        }

        private bool Overlaps(MapRect shrunkFootprint)
        {
            float r = (float)Math.Sqrt(shrunkFootprint.HalfLength * shrunkFootprint.HalfLength + shrunkFootprint.HalfWidth * shrunkFootprint.HalfWidth);
            for (int i = 0; i < occupied.Count; i++)
            {
                var o = occupied[i];
                float dx = o.Center.X - shrunkFootprint.Center.X, dz = o.Center.Y - shrunkFootprint.Center.Y;
                float reach = o.Radius + r;
                if (dx * dx + dz * dz > reach * reach)
                    continue;
                if (o.Rect.Intersects(shrunkFootprint))
                    return true;
            }
            return false;
        }

        /// <summary>A peça cabe nesse lugar? Se sim, devolve o plano (sem registrar).</summary>
        private bool Fits(PieceSpec p, Float2 pivot, Float2 fwd, float scaleX, out BuildingPlan plan)
        {
            plan = null;
            if (p.Filler && fillerCount >= MaxFillers)
                return false;
            float width = p.Width * scaleX;
            var footprint = PieceRect(p, pivot, fwd, width, 0f);
            if (footprint.Center.Length > maxRadius)
                return false;
            if (layout.DistanceToWalkable(footprint.Center) > cover)
                return false;

            // A caixa de colisão (com o avanço à frente) não pode encostar na área andável.
            float checkExt = p.Centered ? 0f : ColliderFront - WalkableClearance;
            if (TouchesWalkable(PieceRect(p, pivot, fwd, width, checkExt)))
                return false;

            // Altura: o limite do envelope da câmera vale para a planta inteira, inclusive o avanço da frente.
            var heightRect = PieceRect(p, pivot, fwd, width, p.Centered ? 0f : ColliderFront);
            if (p.Height > layout.MaxBuildingHeight(heightRect))
                return false;

            if (Overlaps(Shrunk(footprint, TouchTolerance)))
                return false;

            float colliderWidth = p.Body * scaleX;
            plan = new BuildingPlan
            {
                Piece = p,
                Pivot = pivot,
                Forward = fwd,
                ScaleX = scaleX,
                Footprint = footprint,
                Collider = PieceRect(p, pivot, fwd, colliderWidth, ColliderFront),
            };
            return true;
        }

        private int fillerCount;

        private void Commit(BuildingPlan plan)
        {
            if (plan.Piece.Filler)
                fillerCount++;
            plans.Add(plan);
            var rect = plan.Footprint;
            occupied.Add(new Occupant
            {
                Rect = rect,
                Center = rect.Center,
                Radius = (float)Math.Sqrt(rect.HalfLength * rect.HalfLength + rect.HalfWidth * rect.HalfWidth),
            });
        }

        // ---------- Sorteio ----------

        private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>1 no lado oposto à câmera (giro 30°), -1 no lado da câmera.</summary>
        private static float Farness(float angleDeg) => (float)Math.Cos((angleDeg - 30f) * Math.PI / 180.0);

        private static float AngleOf(Float2 p) => (float)(Math.Atan2(p.X, p.Y) * 180.0 / Math.PI);

        private float WeightOf(PieceSpec p, PieceSpec previous, Float2 position)
        {
            float w = Math.Max(0.05f, p.Weight + p.FarBias * Farness(AngleOf(position)));
            if (position.Length >= 50f)
                w *= p.Height > 14f ? 1.6f : 0.6f; // silhueta alta ao fundo
            if (p == previous)
                w *= 0.25f;
            return w;
        }

        private int PickIndex(List<float> weights)
        {
            float total = 0f;
            foreach (float w in weights)
                total += w;
            float roll = Range(0f, total);
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                    return i;
            }
            return weights.Count - 1;
        }

        // ---------- Faixas retas (avenidas e bocas) ----------

        private Strip AvenueStrip(int i, int side)
        {
            var av = layout.Avenues[i];
            var sp = layout.SmallPlazas[i];
            float across = side * (av.Width * 0.5f + FacadeSetback);
            float e = Math.Abs(across);
            float spS = Dot(sp.Center, av.Axis);
            return new Strip
            {
                Region = $"Avenida{i}",
                Street = i,
                Side = side,
                Front = av.Rect.Side * (-side),
                Facade = s => av.PointAt(s, across),
                // Da linha das fachadas da praça central até a das praças menores.
                S0 = (float)Math.Sqrt(layout.PlazaFacadeRadius * layout.PlazaFacadeRadius - e * e),
                S1 = spS - (float)Math.Sqrt(Sq(sp.Radius + FacadeSetback) - e * e),
            };
        }

        private Strip MouthStrip(int i, int side)
        {
            var m = layout.Mouths[i];
            var sp = layout.SmallPlazas[i];
            float across = side * (m.Width * 0.5f + FacadeSetback);
            float e = Math.Abs(across);
            float spS = Dot(sp.Center, m.Axis);
            return new Strip
            {
                Region = $"Boca{i}",
                Street = i,
                Side = side,
                Front = m.Rect.Side * (-side),
                Facade = s => m.PointAt(s, across),
                S0 = spS + (float)Math.Sqrt(Sq(sp.Radius + FacadeSetback) - e * e),
                S1 = m.EndS + FacadeSetback,
            };
        }

        private static float Sq(float v) => v * v;

        /// <summary>
        /// Plano de uma sequência de peças na faixa, sem registrar. <paramref name="startS"/> = onde a primeira peça começa
        /// (o começo da faixa pode estar tomado por outro prédio); <paramref name="uncovered"/> = metros sem prédio
        /// depois dela; <paramref name="hasHoles"/> = houve buraco no meio.
        /// </summary>
        private List<BuildingPlan> TrialStrip(Strip st, out float startS, out float uncovered, out bool hasHoles, out List<float> gapList)
        {
            var list = new List<BuildingPlan>();
            gapList = new List<float>();
            uncovered = 0f;
            hasHoles = false;
            startS = st.S0;
            float cursor = st.S0;
            PieceSpec previous = null;
            int guard = 0;
            while (guard++ < 240)
            {
                float remaining = st.S1 - cursor;
                if (remaining < MinPieceWidth)
                    break; // sobra menor que qualquer peça: não é buraco
                var cands = new List<BuildingPlan>();
                var weights = new List<float>();
                CollectStripCandidates(st, cursor, remaining, previous, filler: false, cands, weights);
                if (cands.Count == 0)
                    CollectStripCandidates(st, cursor, remaining, previous, filler: true, cands, weights);
                if (cands.Count == 0)
                {
                    cursor += 0.25f; // nada cabe aqui
                    if (list.Count > 0)
                    {
                        uncovered += 0.25f;
                        hasHoles = true;
                    }
                    continue;
                }
                var plan = cands[PickIndex(weights)];
                float w = plan.Piece.Width;
                plan.Along = cursor + w * 0.5f;
                if (list.Count == 0)
                    startS = cursor;
                list.Add(plan);
                float gap = Range(MinGap, MaxGap);
                gapList.Add(gap);
                previous = plan.Piece;
                cursor += w + gap;
            }
            if (list.Count > 0)
                uncovered += Math.Max(0f, st.S1 - (cursor - gapList[gapList.Count - 1]));
            else
                uncovered = st.S1 - st.S0;
            return list;
        }

        private void CollectStripCandidates(Strip st, float cursor, float remaining, PieceSpec previous, bool filler,
            List<BuildingPlan> cands, List<float> weights)
        {
            foreach (var p in Catalog)
            {
                if (p.Filler != filler || p.Width > remaining)
                    continue;
                Float2 facade = st.Facade(cursor + p.Width * 0.5f);
                Float2 pivot = p.Centered ? facade - st.Front * (p.Depth * 0.5f) : facade;
                Float2 fwd = st.Front;
                if (p.Centered && rng.NextDouble() < 0.5)
                    fwd = new Float2(fwd.Y, -fwd.X); // tanque girado 90°
                if (!Fits(p, pivot, fwd, 1f, out var plan))
                    continue;
                plan.Region = st.Region;
                plan.Street = st.Street;
                plan.Side = st.Side;
                plan.Mirror = !p.Centered && rng.NextDouble() < 0.5;
                cands.Add(plan);
                weights.Add(WeightOf(p, previous, pivot));
            }
        }

        /// <summary>Várias tentativas sorteadas: fica a que deixa menos metros sem prédio; a sobra é diluída esticando as peças.</summary>
        private void CommitStrip(Strip st)
        {
            if (st.S1 - st.S0 < MinPieceWidth)
                return;
            List<BuildingPlan> best = null;
            List<float> bestGaps = null;
            float bestScore = float.MaxValue, bestStart = st.S0;
            bool bestHoles = true;
            for (int t = 0; t < StripTrials; t++)
            {
                var trial = TrialStrip(st, out float startS, out float uncovered, out bool holes, out var gapList);
                float score = uncovered + (holes ? 2f : 0f) + FillerPenalty * trial.FindAll(t => t.Piece.Filler).Count + Range(0f, ScoreJitter);
                if (trial.Count > 0 && score < bestScore)
                {
                    best = trial;
                    bestGaps = gapList;
                    bestScore = score;
                    bestStart = startS;
                    bestHoles = holes;
                }
            }
            if (best == null)
                return;
            if (!bestHoles)
                best = TryStretch(st, bestStart, best, bestGaps) ?? best;
            foreach (var plan in best)
                Commit(plan);
        }

        /// <summary>Distribui a sobra da faixa pelas peças (escala em X) se ficar até +25%; nulo se algo deixar de caber.</summary>
        private List<BuildingPlan> TryStretch(Strip st, float startS, List<BuildingPlan> plansIn, List<float> gapList)
        {
            float widths = 0f;
            foreach (var p in plansIn)
                widths += p.Piece.Width;
            float gapSum = 0f;
            for (int i = 0; i < plansIn.Count - 1; i++)
                gapSum += gapList[i];
            float available = (st.S1 - startS) - gapSum;
            float scale = available / widths;
            if (scale <= 1.01f || scale > MaxStretch)
                return null;

            var stretched = new List<BuildingPlan>();
            float cursor = startS;
            for (int i = 0; i < plansIn.Count; i++)
            {
                var old = plansIn[i];
                var p = old.Piece;
                // Tanque (centrado) não estica: fica no lugar e a sobra vira vão pequeno.
                float sx = p.Centered ? 1f : scale;
                float w = p.Width * sx;
                Float2 facade = st.Facade(cursor + w * 0.5f);
                Float2 pivot = p.Centered ? facade - st.Front * (p.Depth * 0.5f) : facade;
                if (!Fits(p, pivot, old.Forward, sx, out var plan))
                    return null;
                plan.Region = old.Region;
                plan.Street = old.Street;
                plan.Side = old.Side;
                plan.Mirror = old.Mirror;
                plan.Along = cursor + w * 0.5f;
                stretched.Add(plan);
                cursor += w + (i < gapList.Count ? gapList[i] : 0f);
            }
            return stretched;
        }

        /// <summary>Fecho atrás do portão: um prédio largo de frente para o fim da boca.</summary>
        private void PlanMouthCap(int i)
        {
            var m = layout.Mouths[i];
            float sCap = m.EndS + FacadeSetback;
            Float2 front = m.Axis * -1f;
            var cands = new List<BuildingPlan>();
            var weights = new List<float>();
            foreach (var p in Catalog)
            {
                if (p.Centered || p.Width < m.Width + 0.6f)
                    continue;
                if (!Fits(p, m.PointAt(sCap, 0f), front, 1f, out var plan))
                    continue;
                plan.Region = $"Boca{i}";
                plan.Street = i;
                plan.Mirror = rng.NextDouble() < 0.5;
                cands.Add(plan);
                weights.Add(WeightOf(p, null, plan.Pivot));
            }
            if (cands.Count > 0)
                Commit(cands[PickIndex(weights)]);
        }

        // ---------- Arcos em volta de uma praça (praça central e praças menores) ----------

        /// <summary>
        /// Prédios com a frente na linha de fachadas de raio <paramref name="radius"/> em volta de <paramref name="center"/>,
        /// virados para o centro. O arco é cortado nos trechos livres (sem avenida nem boca) e cada trecho é fechado de ponta
        /// a ponta com várias tentativas sorteadas; a sobra é diluída esticando as peças (até +25%).
        /// </summary>
        private void FillArc(string region, int street, Float2 center, float radius, float maxWidth)
        {
            const float step = 0.25f;
            int count = (int)(360f / step);
            // Livre = a linha da frente dos colliders não toca a área andável (fora dela, nem das aberturas).
            var free = new bool[count];
            int freeCount = 0;
            for (int i = 0; i < count; i++)
            {
                Float2 q = Polar(center, i * step, radius - ColliderFront);
                free[i] = layout.DistanceToWalkable(q) >= WalkableClearance * 0.5f;
                if (free[i])
                    freeCount++;
            }
            if (freeCount == 0)
                return;
            if (freeCount == count)
            {
                PlanArcSegment(region, street, center, radius, maxWidth, 0f, 360f);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                // Começo de um trecho livre: o anterior está fechado (abertura).
                if (!free[i] || free[(i + count - 1) % count])
                    continue;
                int run = 0;
                while (run < count && free[(i + run) % count])
                    run++;
                PlanArcSegment(region, street, center, radius, maxWidth, i * step, (i + run) * step);
            }
        }

        private struct ArcStep
        {
            public BuildingPlan Plan;
            public float Gap; // folga (m) até a peça seguinte
        }

        private void PlanArcSegment(string region, int street, Float2 center, float radius, float maxWidth, float a0, float a1)
        {
            float length = (a1 - a0) / Rad2Deg * radius;
            if (length < MinPieceWidth)
                return;
            List<ArcStep> best = null;
            float bestScore = float.MaxValue, bestStart = a0;
            bool bestHoles = true;
            for (int t = 0; t < StripTrials; t++)
            {
                var trial = TrialArc(region, street, center, radius, maxWidth, a0, a1, out float startAngle, out float uncovered, out bool holes);
                float score = uncovered + (holes ? 2f : 0f) + FillerPenalty * trial.FindAll(t => t.Plan.Piece.Filler).Count + Range(0f, ScoreJitter);
                if (trial.Count > 0 && score < bestScore)
                {
                    best = trial;
                    bestScore = score;
                    bestStart = startAngle;
                    bestHoles = holes;
                }
            }
            if (best == null)
                return;
            if (!bestHoles)
                best = StretchArc(region, street, center, radius, bestStart, a1, best) ?? best;
            foreach (var step in best)
                Commit(step.Plan);
        }

        private List<ArcStep> TrialArc(string region, int street, Float2 center, float radius, float maxWidth, float a0, float a1,
            out float startAngle, out float uncovered, out bool hasHoles)
        {
            var list = new List<ArcStep>();
            uncovered = 0f;
            hasHoles = false;
            startAngle = a0;
            float cursor = a0;
            PieceSpec previous = null;
            int guard = 0;
            while (guard++ < 240)
            {
                float remaining = (a1 - cursor) / Rad2Deg * radius;
                if (remaining < MinPieceWidth)
                    break;
                var cands = new List<BuildingPlan>();
                var weights = new List<float>();
                CollectArcCandidates(region, street, center, radius, maxWidth, cursor, a1, previous, filler: false, cands, weights);
                if (cands.Count == 0)
                    CollectArcCandidates(region, street, center, radius, maxWidth, cursor, a1, previous, filler: true, cands, weights);
                if (cands.Count == 0)
                {
                    cursor += 0.25f / radius * Rad2Deg;
                    if (list.Count > 0)
                    {
                        uncovered += 0.25f;
                        hasHoles = true;
                    }
                    continue;
                }
                var plan = cands[PickIndex(weights)];
                float gap = Range(MinGap, MaxGap);
                if (list.Count == 0)
                    startAngle = cursor;
                list.Add(new ArcStep { Plan = plan, Gap = gap });
                previous = plan.Piece;
                float half = HalfAngle(plan.Piece.Width * plan.ScaleX * 0.5f, radius);
                cursor = Unwrap(AngleAround(center, plan.Pivot), cursor) + half + gap / radius * Rad2Deg;
            }
            if (list.Count > 0)
            {
                var last = list[list.Count - 1];
                float endAngle = cursor - last.Gap / radius * Rad2Deg;
                uncovered += Math.Max(0f, (a1 - endAngle) / Rad2Deg * radius);
            }
            else
                uncovered = (a1 - a0) / Rad2Deg * radius;
            return list;
        }

        /// <summary>Acha a escala (1 a 1,25) que leva a última peça ao fim do trecho; nulo se não der ou algo deixar de caber.</summary>
        private List<ArcStep> StretchArc(string region, int street, Float2 center, float radius, float a0, float a1, List<ArcStep> steps)
        {
            float End(float scale)
            {
                float c = a0;
                for (int i = 0; i < steps.Count; i++)
                {
                    var p = steps[i].Plan.Piece;
                    float w = p.Width * (p.Centered ? 1f : scale);
                    c += 2f * HalfAngle(w * 0.5f, radius);
                    if (i < steps.Count - 1)
                        c += steps[i].Gap / radius * Rad2Deg;
                }
                return c;
            }
            if (End(1f) >= a1 - 1e-3f || End(MaxStretch) < a1)
                return null;
            float lo = 1f, hi = MaxStretch;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (End(mid) < a1) lo = mid; else hi = mid;
            }
            float scaleX = lo;

            var result = new List<ArcStep>();
            float cursor = a0;
            for (int i = 0; i < steps.Count; i++)
            {
                var old = steps[i].Plan;
                var p = old.Piece;
                float sx = p.Centered ? 1f : scaleX;
                float half = HalfAngle(p.Width * sx * 0.5f, radius);
                float a = cursor + half;
                Float2 facade = Polar(center, a, radius);
                Float2 inward = Unit(center - facade);
                Float2 pivot = p.Centered ? facade - inward * (p.Depth * 0.5f) : facade;
                Float2 fwd = p.Centered ? old.Forward : inward;
                if (!Fits(p, pivot, fwd, sx, out var plan))
                    return null;
                plan.Region = region;
                plan.Street = street;
                plan.Mirror = old.Mirror;
                result.Add(new ArcStep { Plan = plan, Gap = steps[i].Gap });
                cursor = a + half + steps[i].Gap / radius * Rad2Deg;
            }
            return result;
        }

        private const float Rad2Deg = 180f / (float)Math.PI;

        private static float HalfAngle(float halfWidth, float radius) => (float)Math.Atan2(halfWidth, radius) * Rad2Deg;

        private static float AngleAround(Float2 center, Float2 p) => AngleOf(p - center);

        /// <summary>Ângulo (graus) equivalente a <paramref name="angle"/> mais perto de <paramref name="reference"/>.</summary>
        private static float Unwrap(float angle, float reference)
        {
            while (angle < reference - 180f) angle += 360f;
            while (angle > reference + 180f) angle -= 360f;
            return angle;
        }

        private static Float2 Polar(Float2 center, float angleDeg, float radius)
            => center + MapLayout.Polar(angleDeg, radius);

        private void CollectArcCandidates(string region, int street, Float2 center, float radius, float maxWidth, float cursor,
            float segmentEnd, PieceSpec previous, bool filler, List<BuildingPlan> cands, List<float> weights)
        {
            foreach (var p in Catalog)
            {
                if (p.Filler != filler || p.Width > maxWidth)
                    continue;
                float half = HalfAngle(p.Width * 0.5f, radius);
                if (cursor + 2f * half > segmentEnd + 1e-3f)
                    continue; // passa do fim do trecho
                float a = cursor + half;
                Float2 facade = Polar(center, a, radius);
                Float2 inward = Unit(center - facade);
                Float2 pivot = p.Centered ? facade - inward * (p.Depth * 0.5f) : facade;
                Float2 fwd = inward;
                if (p.Centered && rng.NextDouble() < 0.5)
                    fwd = new Float2(fwd.Y, -fwd.X);
                if (!Fits(p, pivot, fwd, 1f, out var plan))
                    continue;
                plan.Region = region;
                plan.Street = street;
                plan.Mirror = !p.Centered && rng.NextDouble() < 0.5;
                cands.Add(plan);
                weights.Add(WeightOf(p, previous, pivot));
            }
        }

        // ---------- Quarteirões e fundo ----------

        /// <summary>
        /// Uma volta de 360° em torno da praça central: em cada ângulo, cada peça procura o raio mais perto da praça em que cabe
        /// (planta livre, fora da área andável, dentro da distância de cobertura). Várias voltas enchem o que sobra atrás
        /// das fileiras, até os anéis do fundo. Devolve quantas peças entraram.
        /// </summary>
        private int Sweep()
        {
            int placedCount = 0;
            float cursor = -180f;
            PieceSpec previous = null;
            int guard = 0;
            while (cursor < 180f && guard++ < 900)
            {
                var cands = new List<BuildingPlan>();
                var radii = new List<float>();
                for (int pass = 0; pass < 2 && cands.Count == 0; pass++)
                {
                    foreach (var p in Catalog)
                    {
                        if (p.Filler != (pass == 1))
                            continue;
                        for (float r = layout.PlazaFacadeRadius; r <= maxRadius; r += 0.5f)
                        {
                            float a = cursor + HalfAngle(p.Width * 0.5f, r);
                            Float2 facade = MapLayout.Polar(a, r);
                            Float2 inward = Unit(facade * -1f);
                            Float2 pivot = p.Centered ? facade - inward * (p.Depth * 0.5f) : facade;
                            Float2 fwd = inward;
                            if (p.Centered && rng.NextDouble() < 0.5)
                                fwd = new Float2(fwd.Y, -fwd.X);
                            if (!Fits(p, pivot, fwd, 1f, out var plan))
                                continue;
                            plan.Region = layout.DistanceToWalkable(plan.Footprint.Center) <= 14f ? "Quarteirao" : "Fundo";
                            plan.Mirror = !p.Centered && rng.NextDouble() < 0.5;
                            cands.Add(plan);
                            radii.Add(r);
                            break;
                        }
                    }
                }
                if (cands.Count == 0)
                {
                    cursor += 2f / 45f * Rad2Deg;
                    previous = null;
                    continue;
                }
                float minR = float.MaxValue;
                foreach (float r in radii)
                    minR = Math.Min(minR, r);
                var weights = new List<float>();
                for (int i = 0; i < cands.Count; i++)
                    weights.Add(radii[i] <= minR + 1.5f ? WeightOf(cands[i].Piece, previous, cands[i].Pivot) : 0f);
                int pick = PickIndex(weights);
                var chosen = cands[pick];
                Commit(chosen);
                placedCount++;
                previous = chosen.Piece;
                float radiusUsed = chosen.Pivot.Length;
                float width = chosen.Piece.Width * chosen.ScaleX;
                float angleUsed = AngleOf(chosen.Pivot);
                cursor = Unwrap(angleUsed, cursor) + HalfAngle(width * 0.5f, Math.Max(radiusUsed, 1f)) + Range(MinGap, MaxGap) / Math.Max(radiusUsed, 1f) * Rad2Deg;
            }
            return placedCount;
        }


        // ---------- Pátios que sobram ----------

        /// <summary>
        /// Último passe: pontos de uma grade (0,5 m perto da área andável, 1 m longe), da borda para fora; em cada ponto livre
        /// tenta as peças com a frente virada para o ponto andável mais próximo. Enche pátios entre as faixas e os anéis.
        /// </summary>
        private void GridFill()
        {
            var points = new List<(float x, float z, float d)>();
            float limit = maxRadius + 2f;
            for (float x = -limit; x <= limit; x += 0.5f)
            {
                for (float z = -limit; z <= limit; z += 0.5f)
                {
                    float d = layout.DistanceToWalkable(x, z);
                    if (d < ColliderFront || d > cover)
                        continue;
                    // Longe da borda a grade é de 1 m (pula os pontos de índice ímpar).
                    if (d > 6f && ((int)Math.Round(x * 2f) % 2 != 0 || (int)Math.Round(z * 2f) % 2 != 0))
                        continue;
                    points.Add((x, z, d));
                }
            }
            points.Sort((a, b) => a.d.CompareTo(b.d));

            foreach (var (x, z, d) in points)
            {
                if (InsideAnyFootprint(x, z))
                    continue;
                Float2 near = layout.NearestWalkable(x, z);
                Float2 fwd = Unit(near - new Float2(x, z));
                var pos = new Float2(x, z);
                for (int pass = 0; pass < 2; pass++)
                {
                    var cands = new List<BuildingPlan>();
                    var weights = new List<float>();
                    foreach (var p in Catalog)
                    {
                        if (p.Filler != (pass == 1))
                            continue;
                        Float2 pivot = p.Centered ? pos - fwd * (p.Depth * 0.5f) : pos;
                        Float2 f = fwd;
                        if (p.Centered && rng.NextDouble() < 0.5)
                            f = new Float2(f.Y, -f.X);
                        if (!Fits(p, pivot, f, 1f, out var plan))
                            continue;
                        plan.Region = d <= 14f ? "Quarteirao" : "Fundo";
                        plan.Mirror = !p.Centered && rng.NextDouble() < 0.5;
                        cands.Add(plan);
                        weights.Add(WeightOf(p, null, pivot));
                    }
                    if (cands.Count > 0)
                    {
                        Commit(cands[PickIndex(weights)]);
                        break;
                    }
                }
            }
        }

        private bool InsideAnyFootprint(float x, float z)
        {
            for (int i = 0; i < occupied.Count; i++)
            {
                var o = occupied[i];
                float dx = o.Center.X - x, dz = o.Center.Y - z;
                if (dx * dx + dz * dz > o.Radius * o.Radius)
                    continue;
                if (o.Rect.Contains(x, z))
                    return true;
            }
            return false;
        }

        // ---------- Vãos na borda ----------

        private bool Blocked(Float2 p)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i].Collider.Contains(p.X, p.Y))
                    return true;
            }
            return false;
        }

        /// <summary>Anda pela borda andável de cada pedaço do mapa; onde nenhum collider de prédio fica logo atrás, marca um vão.</summary>
        private void SealGaps()
        {
            var polylines = new List<List<(Float2 point, Float2 normal)>>();
            // Praça central e praças menores: arcos.
            AddCircleSamples(polylines, Float2.Zero, layout.Plaza.Radius);
            for (int i = 0; i < layout.StreetCount; i++)
            {
                AddCircleSamples(polylines, layout.SmallPlazas[i].Center, layout.SmallPlazas[i].Radius);
                AddRectSamples(polylines, layout.Avenues[i].Rect);
                AddRectSamples(polylines, layout.Mouths[i].Rect);
            }

            foreach (var line in polylines)
            {
                var run = new List<(Float2 point, Float2 normal)>();
                for (int k = 0; k <= line.Count; k++)
                {
                    bool isGap = false;
                    if (k < line.Count)
                    {
                        var (pt, nrm) = line[k];
                        // Trecho aberto para outra área andável (boca da avenida, da praça): não é vão.
                        if (!layout.IsWalkable(pt.X + nrm.X * 0.15f, pt.Y + nrm.Y * 0.15f))
                            isGap = !Blocked(pt + nrm * GapProbe);
                        if (isGap)
                            run.Add(line[k]);
                    }
                    if (!isGap)
                    {
                        FlushRun(run);
                        run.Clear();
                    }
                }
            }
        }

        private void FlushRun(List<(Float2 point, Float2 normal)> run)
        {
            if (run.Count < GapMinSamples)
                return;
            float length = (run.Count - 1) * GapSample;
            var first = run[0];
            var last = run[run.Count - 1];
            int pieces = Math.Max(1, (int)Math.Round(length / 2.2f));
            for (int i = 0; i < pieces; i++)
            {
                float t = (i + 0.5f) / pieces;
                int idx = Math.Min(run.Count - 1, (int)(t * run.Count));
                var (pt, nrm) = run[idx];
                var tangent = new Float2(nrm.Y, -nrm.X);
                gaps.Add(new GapSpot { Position = pt + nrm * 0.9f, Tangent = tangent, Length = Math.Max(1.2f, length / pieces) });
            }
        }

        private static void AddCircleSamples(List<List<(Float2, Float2)>> polylines, Float2 center, float radius)
        {
            int count = Math.Max(8, (int)(2f * (float)Math.PI * radius / GapSample));
            var list = new List<(Float2, Float2)>();
            for (int i = 0; i < count; i++)
            {
                float a = i * 360f / count;
                Float2 dir = MapLayout.Polar(a, 1f);
                list.Add((center + dir * radius, dir));
            }
            polylines.Add(list);
        }

        private static void AddRectSamples(List<List<(Float2, Float2)>> polylines, MapRect rect)
        {
            // Lados compridos (para a esquerda e a direita) e a ponta de fora.
            for (int side = -1; side <= 1; side += 2)
            {
                var list = new List<(Float2, Float2)>();
                Float2 normal = rect.Side * side;
                for (float along = -rect.HalfLength; along <= rect.HalfLength + 1e-3f; along += GapSample)
                    list.Add((rect.ToWorld(along, side * rect.HalfWidth), normal));
                polylines.Add(list);
            }
            var end = new List<(Float2, Float2)>();
            for (float across = -rect.HalfWidth; across <= rect.HalfWidth + 1e-3f; across += GapSample)
            {
                end.Add((rect.ToWorld(rect.HalfLength, across), rect.Axis));
            }
            polylines.Add(end);
        }
    }
}
