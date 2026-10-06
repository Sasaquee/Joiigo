using System;
using Game.Core.Map;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Geometria do mapa (passe do mapa, D-073 a D-082): praça, avenidas, praças menores, bocas, spawns, portões,
    /// ponto andável mais próximo e envelope da câmera. Só o Core, com os números padrão da tabela do plano.
    /// </summary>
    public class MapLayoutTests
    {
        private const float Tol = 1e-3f;

        private MapLayout _layout;

        [SetUp]
        public void SetUp() => _layout = new MapLayout();

        private static Float2 P(float angleDeg, float radius) => MapLayout.Polar(angleDeg, radius);

        [Test]
        public void Polar_SegueAConvencaoDoArenaBuilder()
        {
            Assert.AreEqual(0f, P(0f, 10f).X, Tol);
            Assert.AreEqual(10f, P(0f, 10f).Y, Tol);
            Assert.AreEqual(10f, P(90f, 10f).X, Tol);
            Assert.AreEqual(0f, P(90f, 10f).Y, Tol);
            Assert.AreEqual(-7.071f, P(-45f, 10f).X, Tol);
            Assert.AreEqual(7.071f, P(-45f, 10f).Y, Tol);
        }

        [Test]
        public void Praca_AndaAteORaioDeAndar_ENaoDepois()
        {
            Assert.IsTrue(_layout.IsWalkable(0f, 0f));
            Assert.IsTrue(_layout.IsWalkable(P(180f, 28.2f)));  // sul, longe das avenidas
            Assert.IsFalse(_layout.IsWalkable(P(180f, 28.5f)));
            Assert.AreEqual(MapRegionKind.Plaza, _layout.Region(0f, 0f).Kind);
        }

        [Test]
        public void Avenidas_SaoAndaveisDeUmaPontaAOutra_ComALarguraCerta()
        {
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                MapAvenue av = _layout.Avenues[i];
                for (float s = av.StartS; s <= av.EndS; s += 0.25f)
                {
                    Assert.IsTrue(_layout.IsWalkable(av.PointAt(s)), $"avenida {i}, s={s}");
                    Assert.IsTrue(_layout.IsWalkable(av.PointAt(s, 4.4f)), $"avenida {i}, s={s}, lado +");
                    Assert.IsTrue(_layout.IsWalkable(av.PointAt(s, -4.4f)), $"avenida {i}, s={s}, lado -");
                }
                Assert.AreEqual(MapRegionKind.Avenue, _layout.Region(av.PointAt(33f)).Kind);
                Assert.AreEqual(i, _layout.Region(av.PointAt(33f)).Index);
            }
        }

        [Test]
        public void Avenida_NaoAnda_ForaDaLargura()
        {
            MapAvenue av = _layout.Avenues[1]; // norte: eixo +Z, lado +X
            Assert.IsFalse(_layout.IsWalkable(av.PointAt(33f, 4.7f)));
            Assert.IsFalse(_layout.IsWalkable(av.PointAt(33f, -4.7f)));
        }

        [Test]
        public void PracaEAvenidaEBocaEPracaMenor_FormamUmCaminhoSemBuracoAteAoPortao()
        {
            // Do centro até o fim da boca, pelo eixo de cada rua, sem trecho não andável.
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                float end = _layout.Mouths[i].EndS;
                for (float s = 0f; s <= end; s += 0.25f)
                    Assert.IsTrue(_layout.IsWalkable(P(_layout.Avenues[i].AngleDeg, s)), $"rua {i}, s={s}");
            }
        }

        [Test]
        public void CunhaEntreDuasAvenidas_NaoEAndavel()
        {
            foreach (float angle in new[] { -67.5f, -22.5f, 22.5f, 67.5f })
                Assert.IsFalse(_layout.IsWalkable(P(angle, 40f)), $"ângulo {angle}");
            Assert.AreEqual(MapRegionKind.None, _layout.Region(P(22.5f, 40f)).Kind);
        }

        [Test]
        public void PracasMenores_SaoAndaveis_ENaoPassamDoRaio()
        {
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                MapSmallPlaza sp = _layout.SmallPlazas[i];
                Assert.IsTrue(_layout.IsWalkable(sp.Center), $"centro da praça {i}");
                Assert.AreEqual(MapRegionKind.SmallPlaza, _layout.Region(sp.Center).Kind);
                Assert.AreEqual(i, _layout.Region(sp.Center).Index);
                // Perpendicular ao eixo, onde não há avenida nem boca.
                Float2 side = new Float2(sp.Center.Y / sp.Center.Length, -sp.Center.X / sp.Center.Length);
                Assert.IsTrue(_layout.IsWalkable(sp.Center + side * 8.9f));
                Assert.IsFalse(_layout.IsWalkable(sp.Center + side * 9.2f));
            }
        }

        [Test]
        public void EntreDuasPracasMenores_NaoEAndavel()
        {
            for (int i = 0; i < _layout.StreetCount - 1; i++)
            {
                Float2 a = _layout.SmallPlazas[i].Center, b = _layout.SmallPlazas[i + 1].Center;
                Float2 mid = (a + b) * 0.5f;
                Assert.IsFalse(_layout.IsWalkable(mid), $"entre {i} e {i + 1}");
                Assert.Greater(_layout.DistanceToWalkable(mid), 5f);
            }
        }

        [Test]
        public void Bocas_SaoAndaveis_ComLarguraDeSeisMetros()
        {
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                MapMouth m = _layout.Mouths[i];
                Assert.AreEqual(MapRegionKind.Mouth, _layout.Region(m.PointAt(58f)).Kind);
                Assert.IsTrue(_layout.IsWalkable(m.PointAt(58f, 2.9f)));
                Assert.IsFalse(_layout.IsWalkable(m.PointAt(58f, 3.2f)));
                Assert.IsTrue(_layout.IsWalkable(m.PointAt(60.9f)));
                Assert.IsFalse(_layout.IsWalkable(m.PointAt(61.5f)), "além do fim da boca");
            }
        }

        [Test]
        public void Spawns_FicamNasBocas_LongeDoCentro_ECabeOMarcador()
        {
            Assert.AreEqual(3, _layout.EnemySpawns.Count);
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                Float2 sp = _layout.EnemySpawns[i];
                Assert.GreaterOrEqual(sp.Length, 50f);
                Assert.AreEqual(57.5f, sp.Length, Tol);
                Assert.IsTrue(_layout.IsWalkable(sp));
                Assert.AreEqual(MapRegionKind.Mouth, _layout.Region(sp).Kind);
                Assert.IsTrue(_layout.IsWalkable(sp, _layout.Params.EnemySpawnRadius), "o marcador (raio 1,5) cabe na boca");
            }
        }

        [Test]
        public void Portoes_FicamNasBocas_ViradosParaOCentro()
        {
            Assert.AreEqual(3, _layout.Gates.Count);
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                MapGate g = _layout.Gates[i];
                Assert.IsTrue(_layout.IsWalkable(g.Position), $"portão {i}");
                Assert.AreEqual(MapRegionKind.Mouth, _layout.Region(g.Position).Kind);
                Assert.AreEqual(60.6f, g.Position.Length, Tol);
                Assert.GreaterOrEqual(g.YawDeg, 0f);
                Assert.Less(g.YawDeg, 360f);

                // Frente do portão (+Z local girado por yaw) aponta para o centro.
                float yaw = g.YawDeg * MathF.PI / 180f;
                float fx = MathF.Sin(yaw), fz = MathF.Cos(yaw);
                float len = g.Position.Length;
                float dot = fx * (-g.Position.X / len) + fz * (-g.Position.Y / len);
                Assert.AreEqual(1f, dot, 1e-4f, $"portão {i}");
            }
        }

        [Test]
        public void SpawnsEPortoes_FicamNoMesmoEixoDaMesmaRua()
        {
            for (int i = 0; i < _layout.StreetCount; i++)
            {
                Float2 sp = _layout.EnemySpawns[i], gate = _layout.Gates[i].Position;
                Assert.AreEqual(sp.X / sp.Length, gate.X / gate.Length, 1e-4f);
                Assert.AreEqual(sp.Y / sp.Length, gate.Y / gate.Length, 1e-4f);
                Assert.AreEqual(_layout.Avenues[i].AngleDeg, _layout.Mouths[i].AngleDeg);
            }
        }

        [Test]
        public void Region_ForaDeTudo_EhNone()
        {
            Assert.AreEqual(MapRegionKind.None, _layout.Region(0f, -40f).Kind);
            Assert.AreEqual(MapRegionKind.None, _layout.Region(100f, 100f).Kind);
            Assert.IsFalse(_layout.Region(0f, -40f).IsWalkable);
        }

        [Test]
        public void IsWalkable_ComRaioDoAgente_EhMaisRestritivo()
        {
            Float2 nearEdge = P(180f, 27.8f);
            Assert.IsTrue(_layout.IsWalkable(nearEdge, 0f));
            Assert.IsTrue(_layout.IsWalkable(nearEdge, 0.4f));
            Assert.IsFalse(_layout.IsWalkable(nearEdge, 0.9f));
            // A boca tem 6 m: um agente de raio 0,9 passa; de raio 3,5 não.
            Float2 mouth = _layout.Mouths[1].PointAt(58f);
            Assert.IsTrue(_layout.IsWalkable(mouth, 0.9f));
            Assert.IsFalse(_layout.IsWalkable(mouth, 3.5f));
        }

        [Test]
        public void DistanceToWalkable_ZeroDentro_EExataFora()
        {
            Assert.AreEqual(0f, _layout.DistanceToWalkable(0f, 0f), 1e-6f);
            Assert.AreEqual(0f, _layout.DistanceToWalkable(_layout.EnemySpawns[0]), 1e-6f);
            // Ao sul da praça (sem avenida nem boca): 40 - 28,3.
            Assert.AreEqual(11.7f, _layout.DistanceToWalkable(0f, -40f), Tol);
        }

        [Test]
        public void NearestWalkable_DevolveAndavel_EEhIdempotente()
        {
            int outside = 0;
            for (float x = -90f; x <= 90f; x += 3.7f)
                for (float z = -60f; z <= 90f; z += 3.3f)
                {
                    Float2 n = _layout.NearestWalkable(x, z);
                    Assert.IsTrue(_layout.IsWalkable(n), $"({x},{z}) -> {n}");
                    Float2 again = _layout.NearestWalkable(n);
                    Assert.AreEqual(n.X, again.X, 1e-6f);
                    Assert.AreEqual(n.Y, again.Y, 1e-6f);
                    if (!_layout.IsWalkable(x, z)) outside++;
                }
            Assert.Greater(outside, 100, "a grade precisa cobrir muito ponto não andável");
        }

        [Test]
        public void NearestWalkable_PontoAndavelFicaOndeEstava_EForaVaiParaOMaisPerto()
        {
            Float2 inside = P(30f, 10f);
            Assert.AreEqual(inside, _layout.NearestWalkable(inside));

            // Ao sul: o ponto mais perto é a borda da praça, empurrada para dentro.
            Float2 n = _layout.NearestWalkable(0f, -40f);
            Assert.AreEqual(0f, n.X, Tol);
            Assert.AreEqual(-(28.3f - MapLayout.DefaultInset), n.Y, Tol);
            // Nunca mais longe do que a distância exata + o empurrão.
            Float2 w = P(22.5f, 40f);
            float dist = MathF.Sqrt((n.X - 0f) * (n.X - 0f) + (n.Y + 40f) * (n.Y + 40f));
            Assert.LessOrEqual(dist, _layout.DistanceToWalkable(0f, -40f) + MapLayout.DefaultInset + Tol);
            Assert.IsTrue(_layout.IsWalkable(_layout.NearestWalkable(w)));
        }

        [Test]
        public void MaxPlayerRadius_EhABordaDeForaDasPracasMenores()
        {
            Assert.AreEqual(56f, _layout.MaxPlayerRadius, Tol);
            Assert.AreEqual(_layout.Params.SmallPlazaS + _layout.Params.SmallPlazaRadius, _layout.MaxPlayerRadius, Tol);
            for (int i = 0; i < _layout.StreetCount; i++)
                Assert.IsTrue(_layout.IsWalkable(P(_layout.Avenues[i].AngleDeg, _layout.MaxPlayerRadius - 0.01f)));
        }

        [Test]
        public void EnvelopeDaCamera_LimitaAAlturaPertoDaPracaSul_ENaoLimitaNoNorteDistante()
        {
            Assert.AreEqual(10.2f, _layout.EnvelopeHeightLimit, 1e-4f);
            // Prédio logo atrás da praça, ao sul (lado da câmera): a câmera passa por cima dele.
            Assert.AreEqual(10.2f, _layout.MaxBuildingHeight(new Float2(0f, -33f), 3f, 3f), 1e-4f);
            Assert.AreEqual(10.2f, _layout.MaxBuildingHeight(0f, -20f), 1e-4f, "um ponto dentro da praça também");
            // Norte distante, depois das bocas: sem limite.
            Assert.IsTrue(float.IsPositiveInfinity(_layout.MaxBuildingHeight(new Float2(0f, 80f), 3f, 3f)));
            Assert.IsTrue(float.IsPositiveInfinity(_layout.MaxBuildingHeight(new Float2(60f, 60f), 3f, 3f)));
            // Muito longe ao sul, também livre.
            Assert.IsTrue(float.IsPositiveInfinity(_layout.MaxBuildingHeight(new Float2(0f, -60f), 3f, 3f)));
        }

        [Test]
        public void EnvelopeDaCamera_RespeitaODeslocamentoELargura()
        {
            // O envelope é a praça deslocada (-4,5; -7,8): vai até z = -7,8 - (28,3 + 1) = -37,1 em x = -4,5.
            Assert.IsTrue(_layout.TouchesCameraEnvelope(new Float2(-4.5f, -36.9f), 0f, 0f));
            Assert.IsFalse(_layout.TouchesCameraEnvelope(new Float2(-4.5f, -37.5f), 0f, 0f));
            // Planta grande que só encosta na ponta conta; girada também.
            Assert.IsTrue(_layout.TouchesCameraEnvelope(new Float2(-4.5f, -40f), 4f, 4f));
            Assert.IsTrue(_layout.TouchesCameraEnvelope(new Float2(-4.5f, -40f), 6f, 1f, 45f));
            Assert.IsFalse(_layout.TouchesCameraEnvelope(new Float2(-4.5f, -50f), 4f, 4f, 30f));
        }

        [Test]
        public void EnvelopeDaCamera_CobreAsAvenidasDeslocadas()
        {
            // Meio da avenida N deslocado pela câmera: perto de (-4,5; 33 - 7,8).
            Assert.IsTrue(_layout.TouchesCameraEnvelope(new Float2(-9f, 25f), 0.5f, 0.5f));
            // Um prédio ao lado da avenida N, 12 m para o lado e mais para o norte do envelope, não toca.
            Assert.IsFalse(_layout.TouchesCameraEnvelope(new Float2(16f, 45f), 2f, 2f));
        }

        [Test]
        public void MapaEhSimetrico_NoENe_EspelhoEmX()
        {
            Assert.AreEqual(-_layout.EnemySpawns[2].X, _layout.EnemySpawns[0].X, 1e-4f);
            Assert.AreEqual(_layout.EnemySpawns[2].Y, _layout.EnemySpawns[0].Y, 1e-4f);
            Assert.AreEqual(-_layout.Gates[2].Position.X, _layout.Gates[0].Position.X, 1e-4f);
            Assert.AreEqual(-_layout.SmallPlazas[2].Center.X, _layout.SmallPlazas[0].Center.X, 1e-4f);
            Assert.AreEqual(_layout.SmallPlazas[2].Center.Y, _layout.SmallPlazas[0].Center.Y, 1e-4f);
            // N está no eixo.
            Assert.AreEqual(0f, _layout.SmallPlazas[1].Center.X, 1e-4f);

            for (float x = 0.13f; x <= 70f; x += 0.97f)
                for (float z = -45.1f; z <= 75f; z += 0.89f)
                    Assert.AreEqual(_layout.IsWalkable(x, z), _layout.IsWalkable(-x, z), $"({x},{z})");
        }

        [Test]
        public void NumerosPadrao_BatemComATabelaDoPlano()
        {
            MapLayoutParams p = MapLayoutParams.Default;
            Assert.AreEqual(29f, p.PlazaRadius);
            Assert.AreEqual(28.3f, p.PlazaWalkRadius);
            CollectionAssert.AreEqual(new[] { -45f, 0f, 45f }, p.AvenueAnglesDeg);
            Assert.AreEqual(9f, p.AvenueWidth);
            Assert.AreEqual(26f, p.AvenueStartS);
            Assert.AreEqual(41f, p.AvenueEndS);
            Assert.AreEqual(47f, p.SmallPlazaS);
            Assert.AreEqual(9f, p.SmallPlazaRadius);
            Assert.AreEqual(6f, p.MouthWidth);
            Assert.AreEqual(53f, p.MouthStartS);
            Assert.AreEqual(61f, p.MouthEndS);
            Assert.AreEqual(57.5f, p.EnemySpawnS);
            Assert.AreEqual(60.6f, p.GateS);
            Assert.AreEqual(6f, p.FenceHeight);
            Assert.AreEqual(1.5f, p.CameraHeadroom);
            Assert.AreEqual(-4.5f, p.CameraGroundOffset.X);
            Assert.AreEqual(-7.8f, p.CameraGroundOffset.Y);
            Assert.AreEqual(11.7f, p.CameraHeight);
        }

        [Test]
        public void ValoresDaTabela_DaoAsCoordenadasDoPlano()
        {
            Assert.AreEqual(-33.2f, _layout.SmallPlazas[0].Center.X, 0.05f);
            Assert.AreEqual(33.2f, _layout.SmallPlazas[0].Center.Y, 0.05f);
            Assert.AreEqual(-40.7f, _layout.EnemySpawns[0].X, 0.05f);
            Assert.AreEqual(0f, _layout.EnemySpawns[1].X, 1e-4f);
            Assert.AreEqual(57.5f, _layout.EnemySpawns[1].Y, 1e-4f);
            Assert.AreEqual(-42.9f, _layout.Gates[0].Position.X, 0.05f);
            Assert.AreEqual(180f, _layout.Gates[1].YawDeg, 1e-3f);
        }

        [Test]
        public void Deterministico_DuasInstanciasDaoOMesmoResultado()
        {
            var a = new MapLayout();
            var b = new MapLayout();
            for (float x = -80f; x <= 80f; x += 4.1f)
                for (float z = -50f; z <= 80f; z += 3.9f)
                {
                    Assert.AreEqual(a.Region(x, z), b.Region(x, z));
                    Assert.AreEqual(a.NearestWalkable(x, z), b.NearestWalkable(x, z));
                    Assert.AreEqual(a.MaxBuildingHeight(x, z), b.MaxBuildingHeight(x, z));
                }
        }

        [Test]
        public void ParametrosPersonalizados_MudamAGeometria()
        {
            var layout = new MapLayout(new MapLayoutParams(avenueAnglesDeg: new[] { 90f }, plazaWalkRadius: 10f));
            Assert.AreEqual(1, layout.StreetCount);
            Assert.IsTrue(layout.IsWalkable(P(90f, 33f)));
            Assert.IsFalse(layout.IsWalkable(P(0f, 33f)));
            Assert.IsFalse(layout.IsWalkable(P(180f, 20f)));
        }

        [Test]
        public void Parametros_InvalidosLancam()
        {
            Assert.Throws<ArgumentException>(() => new MapLayoutParams(avenueAnglesDeg: new float[0]));
            Assert.Throws<ArgumentException>(() => new MapLayoutParams(avenueStartS: 30f, avenueEndS: 20f));
        }
    }
}
