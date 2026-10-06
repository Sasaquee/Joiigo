using System;
using Game.Core.Math;

namespace Game.Core.Map
{
    /// <summary>
    /// Disco no plano do chão (X, Z do mundo guardados como X, Y do Float2). Usado pela praça central e pelas
    /// praças menores. Sem UnityEngine: é o Core que decide o que é andável (passe do mapa, D-073 a D-082).
    /// </summary>
    public readonly struct MapDisc
    {
        public readonly Float2 Center;
        public readonly float Radius;

        public MapDisc(Float2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>Distância do ponto à borda do disco (0 se estiver dentro).</summary>
        public float DistanceTo(float x, float z)
        {
            float dx = x - Center.X, dz = z - Center.Y;
            return MathF.Max(0f, MathF.Sqrt(dx * dx + dz * dz) - Radius);
        }

        /// <summary>Dentro do disco encolhido por <paramref name="margin"/> (com a tolerância de <see cref="MapLayout.Tolerance"/>).</summary>
        public bool Contains(float x, float z, float margin = 0f)
        {
            float r = Radius - margin;
            if (r < 0f) return false;
            float dx = x - Center.X, dz = z - Center.Y;
            return MathF.Sqrt(dx * dx + dz * dz) <= r + MapLayout.Tolerance;
        }

        /// <summary>Ponto do disco encolhido por <paramref name="inset"/> mais perto de (x, z).</summary>
        public Float2 ClosestPoint(float x, float z, float inset = 0f)
        {
            float r = MathF.Max(0f, Radius - inset);
            float dx = x - Center.X, dz = z - Center.Y;
            float d = MathF.Sqrt(dx * dx + dz * dz);
            if (d <= r) return new Float2(x, z);
            if (d <= 0f) return Center;
            float k = r / d;
            return new Float2(Center.X + dx * k, Center.Y + dz * k);
        }

        /// <summary>O disco encosta no retângulo?</summary>
        public bool Intersects(MapRect rect)
            => rect.DistanceTo(Center.X, Center.Y) <= Radius + MapLayout.Tolerance;
    }

    /// <summary>
    /// Retângulo orientado no plano do chão: <see cref="Axis"/> é o sentido do comprimento (unitário, no mundo:
    /// (sen a, cos a) para o ângulo a contado a partir de +Z, sentido horário) e <see cref="Side"/> o sentido da
    /// largura (a 90° do eixo, para a direita de quem olha ao longo do eixo). Usado por avenidas, bocas de rua
    /// e pela planta dos prédios (envelope da câmera).
    /// </summary>
    public readonly struct MapRect
    {
        public readonly Float2 Center;
        public readonly Float2 Axis;
        public readonly float HalfLength;
        public readonly float HalfWidth;

        public MapRect(Float2 center, Float2 axis, float halfLength, float halfWidth)
        {
            Center = center;
            Axis = axis;
            HalfLength = halfLength;
            HalfWidth = halfWidth;
        }

        /// <summary>Direção da largura (perpendicular ao eixo).</summary>
        public Float2 Side => new Float2(Axis.Y, -Axis.X);

        public float Length => HalfLength * 2f;
        public float Width => HalfWidth * 2f;

        /// <summary>Coordenadas do ponto no referencial do retângulo: along = ao longo do eixo, across = para o lado.</summary>
        public void ToLocal(float x, float z, out float along, out float across)
        {
            float dx = x - Center.X, dz = z - Center.Y;
            along = dx * Axis.X + dz * Axis.Y;
            across = dx * Axis.Y - dz * Axis.X;
        }

        /// <summary>Ponto do mundo a partir das coordenadas locais.</summary>
        public Float2 ToWorld(float along, float across)
            => new Float2(Center.X + Axis.X * along + Axis.Y * across, Center.Y + Axis.Y * along - Axis.X * across);

        /// <summary>Dentro do retângulo encolhido por <paramref name="margin"/> em todos os lados.</summary>
        public bool Contains(float x, float z, float margin = 0f)
        {
            float hl = HalfLength - margin, hw = HalfWidth - margin;
            if (hl < 0f || hw < 0f) return false;
            ToLocal(x, z, out float a, out float c);
            return MathF.Abs(a) <= hl + MapLayout.Tolerance && MathF.Abs(c) <= hw + MapLayout.Tolerance;
        }

        /// <summary>Distância do ponto ao retângulo (0 se estiver dentro).</summary>
        public float DistanceTo(float x, float z)
        {
            ToLocal(x, z, out float a, out float c);
            float ea = MathF.Max(0f, MathF.Abs(a) - HalfLength);
            float ec = MathF.Max(0f, MathF.Abs(c) - HalfWidth);
            return MathF.Sqrt(ea * ea + ec * ec);
        }

        /// <summary>Ponto do retângulo encolhido por <paramref name="inset"/> mais perto de (x, z).</summary>
        public Float2 ClosestPoint(float x, float z, float inset = 0f)
        {
            float hl = MathF.Max(0f, HalfLength - inset), hw = MathF.Max(0f, HalfWidth - inset);
            ToLocal(x, z, out float a, out float c);
            return ToWorld(MathF.Max(-hl, MathF.Min(hl, a)), MathF.Max(-hw, MathF.Min(hw, c)));
        }

        /// <summary>Retângulo alargado em <paramref name="amount"/> em todos os lados e deslocado por <paramref name="offset"/>.</summary>
        public MapRect Grown(float amount, Float2 offset)
            => new MapRect(Center + offset, Axis, HalfLength + amount, HalfWidth + amount);

        /// <summary>Teste de eixo separador (SAT) entre dois retângulos orientados; tocar na borda conta como interseção.</summary>
        public bool Intersects(MapRect other)
        {
            // Os eixos candidatos são os quatro eixos dos dois retângulos (dois de cada, por simetria).
            Float2 d = other.Center - Center;
            return !Separated(Axis, d, other)
                && !Separated(Side, d, other)
                && !Separated(other.Axis, d, other)
                && !Separated(other.Side, d, other);
        }

        private bool Separated(Float2 n, Float2 d, MapRect other)
        {
            float dist = MathF.Abs(d.X * n.X + d.Y * n.Y);
            return dist > Radius(n) + other.Radius(n) + MapLayout.Tolerance;
        }

        // Meia-extensão do retângulo projetada no eixo n (unitário).
        private float Radius(Float2 n)
        {
            Float2 s = Side;
            return HalfLength * MathF.Abs(Axis.X * n.X + Axis.Y * n.Y) + HalfWidth * MathF.Abs(s.X * n.X + s.Y * n.Y);
        }
    }

    /// <summary>Quais pedaços do mapa existem (ver <see cref="MapRegion"/>).</summary>
    public enum MapRegionKind
    {
        /// <summary>Fora de toda área andável (prédios, vazio).</summary>
        None = 0,
        Plaza,
        Avenue,
        SmallPlaza,
        Mouth,
    }

    /// <summary>Em que pedaço do mapa um ponto está: tipo e índice (0 a N-1 para avenida, praça menor e boca; 0 para a praça central).</summary>
    public readonly struct MapRegion : IEquatable<MapRegion>
    {
        public readonly MapRegionKind Kind;
        public readonly int Index;

        public MapRegion(MapRegionKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static MapRegion None => new MapRegion(MapRegionKind.None, -1);
        public bool IsWalkable => Kind != MapRegionKind.None;

        public bool Equals(MapRegion other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is MapRegion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Index);
        public override string ToString() => Kind == MapRegionKind.None ? "None" : $"{Kind}({Index})";
    }

    /// <summary>Avenida: sai da praça central e vai até uma praça menor. Largura entre fachadas = largura do retângulo andável.</summary>
    public readonly struct MapAvenue
    {
        public readonly int Index;
        /// <summary>Ângulo a partir de +Z, sentido horário, em graus.</summary>
        public readonly float AngleDeg;
        /// <summary>Eixo unitário (do centro para fora).</summary>
        public readonly Float2 Axis;
        public readonly float Width;
        /// <summary>Distância do centro (ao longo do eixo) onde começa e onde termina.</summary>
        public readonly float StartS;
        public readonly float EndS;
        /// <summary>Retângulo andável.</summary>
        public readonly MapRect Rect;

        public MapAvenue(int index, float angleDeg, Float2 axis, float width, float startS, float endS)
        {
            Index = index;
            AngleDeg = angleDeg;
            Axis = axis;
            Width = width;
            StartS = startS;
            EndS = endS;
            float mid = (startS + endS) * 0.5f;
            Rect = new MapRect(axis * mid, axis, (endS - startS) * 0.5f, width * 0.5f);
        }

        public Float2 Center => Rect.Center;
        public float Length => EndS - StartS;
        /// <summary>Ponto sobre o eixo à distância <paramref name="s"/> do centro, deslocado <paramref name="across"/> para o lado.</summary>
        public Float2 PointAt(float s, float across = 0f) => Rect.ToWorld(s - (StartS + EndS) * 0.5f, across);
    }

    /// <summary>Praça menor no fim de uma avenida: disco com centro no eixo da avenida.</summary>
    public readonly struct MapSmallPlaza
    {
        public readonly int Index;
        public readonly Float2 Center;
        public readonly float Radius;
        public MapDisc Disc => new MapDisc(Center, Radius);

        public MapSmallPlaza(int index, Float2 center, float radius)
        {
            Index = index;
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>Boca de rua: o corredor que sai da praça menor até o portão. Os inimigos nascem nela.</summary>
    public readonly struct MapMouth
    {
        public readonly int Index;
        public readonly float AngleDeg;
        public readonly Float2 Axis;
        public readonly float Width;
        public readonly float StartS;
        public readonly float EndS;
        /// <summary>Retângulo andável.</summary>
        public readonly MapRect Rect;

        public MapMouth(int index, float angleDeg, Float2 axis, float width, float startS, float endS)
        {
            Index = index;
            AngleDeg = angleDeg;
            Axis = axis;
            Width = width;
            StartS = startS;
            EndS = endS;
            float mid = (startS + endS) * 0.5f;
            Rect = new MapRect(axis * mid, axis, (endS - startS) * 0.5f, width * 0.5f);
        }

        public Float2 Center => Rect.Center;
        public float Length => EndS - StartS;
        public Float2 PointAt(float s, float across = 0f) => Rect.ToWorld(s - (StartS + EndS) * 0.5f, across);
    }

    /// <summary>Portão-máquina no fim da boca (D-081): posição e giro (graus, mesma convenção do Unity: frente = (sen yaw, cos yaw)) virado para o centro.</summary>
    public readonly struct MapGate
    {
        public readonly int Index;
        public readonly Float2 Position;
        /// <summary>Yaw em graus, de 0 a 360, para o +Z local do portão apontar para o centro (como LookRotation(-posição)).</summary>
        public readonly float YawDeg;

        public MapGate(int index, Float2 position, float yawDeg)
        {
            Index = index;
            Position = position;
            YawDeg = yawDeg;
        }
    }
}
