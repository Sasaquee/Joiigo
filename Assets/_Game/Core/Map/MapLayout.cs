using System;
using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Map
{
    /// <summary>
    /// Fonte única da geometria do mapa (passe do mapa, D-073 a D-082): praça central, avenidas, praças menores,
    /// bocas de rua, spawns, portões, limite do jogador e envelope da câmera para a altura dos prédios.
    /// Tudo é calculado uma vez a partir de <see cref="MapLayoutParams"/>, determinístico e sem UnityEngine:
    /// construtores (arena, cidade, ambiente), spawn de inimigos e testes leem a mesma verdade.
    /// Coordenadas: X e Z do mundo (Float2.X = X, Float2.Y = Z). Ângulos em graus a partir de +Z, sentido horário.
    /// Índice i vale para avenida, praça menor, boca, spawn e portão (padrão: 0 = NO -45°, 1 = N 0°, 2 = NE +45°).
    /// </summary>
    public sealed class MapLayout
    {
        /// <summary>Tolerância (m) dos testes "dentro de": pontos na borda contam como andáveis.</summary>
        public const float Tolerance = 1e-4f;

        /// <summary>Quanto <see cref="NearestWalkable"/> empurra o ponto para dentro da borda (m), para o resultado ser andável sem dúvida.</summary>
        public const float DefaultInset = 0.05f;

        private const float DegToRad = MathF.PI / 180f;

        private readonly MapAvenue[] _avenues;
        private readonly MapSmallPlaza[] _smallPlazas;
        private readonly MapMouth[] _mouths;
        private readonly Float2[] _enemySpawns;
        private readonly MapGate[] _gates;

        // Envelope da câmera: a área andável deslocada pelo deslocamento da câmera e alargada.
        private readonly MapDisc _envPlaza;
        private readonly MapRect[] _envRects;
        private readonly MapDisc[] _envDiscs;

        public MapLayout() : this(MapLayoutParams.Default) { }

        public MapLayout(MapLayoutParams parameters)
        {
            Params = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Plaza = new MapDisc(Float2.Zero, parameters.PlazaWalkRadius);

            int n = parameters.AvenueAnglesDeg.Count;
            _avenues = new MapAvenue[n];
            _smallPlazas = new MapSmallPlaza[n];
            _mouths = new MapMouth[n];
            _enemySpawns = new Float2[n];
            _gates = new MapGate[n];
            for (int i = 0; i < n; i++)
            {
                float a = parameters.AvenueAnglesDeg[i];
                Float2 axis = Polar(a, 1f);
                _avenues[i] = new MapAvenue(i, a, axis, parameters.AvenueWidth, parameters.AvenueStartS, parameters.AvenueEndS);
                _smallPlazas[i] = new MapSmallPlaza(i, axis * parameters.SmallPlazaS, parameters.SmallPlazaRadius);
                _mouths[i] = new MapMouth(i, a, axis, parameters.MouthWidth, parameters.MouthStartS, parameters.MouthEndS);
                _enemySpawns[i] = axis * parameters.EnemySpawnS;
                _gates[i] = new MapGate(i, axis * parameters.GateS, YawFacingCenter(a));
            }

            Float2 off = parameters.CameraGroundOffset;
            float widen = parameters.CameraEnvelopeWiden;
            _envPlaza = new MapDisc(off, parameters.PlazaWalkRadius + widen);
            _envDiscs = new MapDisc[n];
            _envRects = new MapRect[n * 2];
            for (int i = 0; i < n; i++)
            {
                _envDiscs[i] = new MapDisc(_smallPlazas[i].Center + off, parameters.SmallPlazaRadius + widen);
                _envRects[i * 2] = _avenues[i].Rect.Grown(widen, off);
                _envRects[i * 2 + 1] = _mouths[i].Rect.Grown(widen, off);
            }
        }

        public MapLayoutParams Params { get; }

        // ---------------------------------------------------------------- Geometria

        /// <summary>Praça central (disco andável, centro na origem).</summary>
        public MapDisc Plaza { get; }

        /// <summary>Raio da linha das fachadas da praça (onde os prédios começam).</summary>
        public float PlazaFacadeRadius => Params.PlazaRadius;

        /// <summary>Número de ruas (avenida + praça menor + boca + spawn + portão).</summary>
        public int StreetCount => _avenues.Length;

        public IReadOnlyList<MapAvenue> Avenues => _avenues;
        public IReadOnlyList<MapSmallPlaza> SmallPlazas => _smallPlazas;
        public IReadOnlyList<MapMouth> Mouths => _mouths;

        /// <summary>Onde nascem os inimigos (centro do marcador, no meio da boca).</summary>
        public IReadOnlyList<Float2> EnemySpawns => _enemySpawns;

        /// <summary>Portões-máquina, fechando o fim de cada boca e virados para o centro (D-081).</summary>
        public IReadOnlyList<MapGate> Gates => _gates;

        /// <summary>Raio máximo do jogador (m): borda de fora das praças menores. A boca vai além, mas é dos inimigos.</summary>
        public float MaxPlayerRadius => Params.PlayerLimitRadius;

        /// <summary>Altura das paredes de vedação invisíveis (m).</summary>
        public float FenceHeight => Params.FenceHeight;

        /// <summary>Ponto no plano a partir de ângulo (graus, de +Z, sentido horário) e raio. Mesma convenção de Polar() do ArenaBuilder.</summary>
        public static Float2 Polar(float angleDeg, float radius)
        {
            float a = angleDeg * DegToRad;
            return new Float2(MathF.Sin(a) * radius, MathF.Cos(a) * radius);
        }

        /// <summary>Yaw (graus, 0 a 360) que vira o +Z de um objeto no ângulo dado para o centro, como LookRotation(-posição).</summary>
        public static float YawFacingCenter(float angleDeg)
        {
            float y = (angleDeg + 180f) % 360f;
            return y < 0f ? y + 360f : y;
        }

        // ---------------------------------------------------------------- Andável

        /// <summary>
        /// O ponto está em área andável? Praça ∪ avenidas ∪ bocas ∪ praças menores (borda inclusa, tolerância 1e-4 m).
        /// </summary>
        public bool IsWalkable(float x, float z) => IsWalkable(x, z, 0f);

        public bool IsWalkable(Float2 p) => IsWalkable(p.X, p.Y, 0f);

        /// <summary>
        /// Variante com raio do agente: o centro do agente precisa estar a pelo menos <paramref name="radius"/> da borda.
        /// Conservadora: encolhe cada pedaço sozinho e une, então numa emenda (avenida com praça) pode negar um ponto
        /// que cabia; nunca aceita um que não cabe.
        /// </summary>
        public bool IsWalkable(float x, float z, float radius)
        {
            if (Plaza.Contains(x, z, radius)) return true;
            for (int i = 0; i < _avenues.Length; i++)
            {
                if (_avenues[i].Rect.Contains(x, z, radius)) return true;
                if (_mouths[i].Rect.Contains(x, z, radius)) return true;
                if (_smallPlazas[i].Disc.Contains(x, z, radius)) return true;
            }
            return false;
        }

        public bool IsWalkable(Float2 p, float radius) => IsWalkable(p.X, p.Y, radius);

        /// <summary>
        /// Pedaço do mapa do ponto. Nas sobreposições vale a ordem: praça central, praça menor, boca, avenida
        /// (ou seja, "Avenue" é só o que não é praça, praça menor nem boca).
        /// </summary>
        public MapRegion Region(float x, float z)
        {
            if (Plaza.Contains(x, z)) return new MapRegion(MapRegionKind.Plaza, 0);
            for (int i = 0; i < _smallPlazas.Length; i++)
                if (_smallPlazas[i].Disc.Contains(x, z)) return new MapRegion(MapRegionKind.SmallPlaza, i);
            for (int i = 0; i < _mouths.Length; i++)
                if (_mouths[i].Rect.Contains(x, z)) return new MapRegion(MapRegionKind.Mouth, i);
            for (int i = 0; i < _avenues.Length; i++)
                if (_avenues[i].Rect.Contains(x, z)) return new MapRegion(MapRegionKind.Avenue, i);
            return MapRegion.None;
        }

        public MapRegion Region(Float2 p) => Region(p.X, p.Y);

        /// <summary>Distância (m) do ponto à área andável: 0 se estiver dentro. Exata.</summary>
        public float DistanceToWalkable(float x, float z)
        {
            float best = Plaza.DistanceTo(x, z);
            for (int i = 0; i < _avenues.Length && best > 0f; i++)
            {
                best = MathF.Min(best, _avenues[i].Rect.DistanceTo(x, z));
                best = MathF.Min(best, _mouths[i].Rect.DistanceTo(x, z));
                best = MathF.Min(best, _smallPlazas[i].Disc.DistanceTo(x, z));
            }
            return best <= Tolerance ? 0f : best;
        }

        public float DistanceToWalkable(Float2 p) => DistanceToWalkable(p.X, p.Y);

        /// <summary>
        /// Ponto andável mais próximo (D-082: carta onde morreu o último inimigo). Se o ponto já é andável, volta
        /// ele mesmo; senão, o ponto da borda mais próximo empurrado <paramref name="inset"/> metros para dentro,
        /// de modo que o resultado seja andável e <c>NearestWalkable(NearestWalkable(p)) == NearestWalkable(p)</c>.
        /// </summary>
        public Float2 NearestWalkable(float x, float z, float inset = DefaultInset)
        {
            if (IsWalkable(x, z)) return new Float2(x, z);

            Float2 best = Plaza.ClosestPoint(x, z, inset);
            float bestD = Dist(best, x, z);
            for (int i = 0; i < _avenues.Length; i++)
            {
                Consider(_avenues[i].Rect.ClosestPoint(x, z, inset), x, z, ref best, ref bestD);
                Consider(_mouths[i].Rect.ClosestPoint(x, z, inset), x, z, ref best, ref bestD);
                Consider(_smallPlazas[i].Disc.ClosestPoint(x, z, inset), x, z, ref best, ref bestD);
            }
            return best;
        }

        public Float2 NearestWalkable(Float2 p, float inset = DefaultInset) => NearestWalkable(p.X, p.Y, inset);

        private static void Consider(Float2 candidate, float x, float z, ref Float2 best, ref float bestD)
        {
            float d = Dist(candidate, x, z);
            if (d < bestD)
            {
                best = candidate;
                bestD = d;
            }
        }

        private static float Dist(Float2 p, float x, float z)
        {
            float dx = p.X - x, dz = p.Y - z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        // ---------------------------------------------------------------- Envelope da câmera (altura dos prédios)

        /// <summary>
        /// Altura máxima (m) de um prédio dentro do envelope da câmera: CameraHeight - CameraHeadroom (11,7 - 1,5 = 10,2).
        /// </summary>
        public float EnvelopeHeightLimit => Params.CameraHeight - Params.CameraHeadroom;

        /// <summary>
        /// A planta (retângulo orientado: centro, meia-largura em X local, meia-profundidade em Z local, yaw em graus
        /// com a convenção do Unity) toca o envelope da câmera? O envelope é a área andável deslocada pelo
        /// deslocamento da câmera (-4,5; -7,8) e alargada 1 m: onde a câmera pode estar enquanto o jogador anda.
        /// </summary>
        public bool TouchesCameraEnvelope(Float2 center, float halfWidthX, float halfDepthZ, float yawDeg)
        {
            float a = yawDeg * DegToRad;
            var footprint = new MapRect(center, new Float2(MathF.Sin(a), MathF.Cos(a)), MathF.Max(0f, halfDepthZ), MathF.Max(0f, halfWidthX));
            return TouchesCameraEnvelope(footprint);
        }

        /// <summary>Planta alinhada aos eixos (yaw 0).</summary>
        public bool TouchesCameraEnvelope(Float2 center, float halfWidthX, float halfDepthZ)
            => TouchesCameraEnvelope(center, halfWidthX, halfDepthZ, 0f);

        public bool TouchesCameraEnvelope(MapRect footprint)
        {
            if (_envPlaza.Intersects(footprint)) return true;
            for (int i = 0; i < _envDiscs.Length; i++)
                if (_envDiscs[i].Intersects(footprint)) return true;
            for (int i = 0; i < _envRects.Length; i++)
                if (_envRects[i].Intersects(footprint)) return true;
            return false;
        }

        /// <summary>
        /// Altura máxima permitida para um prédio com esta planta: <see cref="EnvelopeHeightLimit"/> se a planta toca o
        /// envelope da câmera, senão <see cref="float.PositiveInfinity"/> (sem limite).
        /// </summary>
        public float MaxBuildingHeight(Float2 center, float halfWidthX, float halfDepthZ, float yawDeg = 0f)
            => TouchesCameraEnvelope(center, halfWidthX, halfDepthZ, yawDeg) ? EnvelopeHeightLimit : float.PositiveInfinity;

        /// <summary>Altura máxima para um prédio representado por um ponto (planta de tamanho zero).</summary>
        public float MaxBuildingHeight(float x, float z) => MaxBuildingHeight(new Float2(x, z), 0f, 0f, 0f);

        /// <summary>Altura máxima para uma planta já montada.</summary>
        public float MaxBuildingHeight(MapRect footprint)
            => TouchesCameraEnvelope(footprint) ? EnvelopeHeightLimit : float.PositiveInfinity;
    }
}
