using System.Collections.Generic;
using Game.Arena;
using Game.Arena.Life;
using Game.Cameras;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Cidade steampunk-mágica em volta da praça (D-042), com o kit de Tools/Blender/build_city.py.
    /// Cria "Cidade" sob a raiz da arena: calçamento, três anéis de prédios fora do muro, pontes de canos,
    /// torre do relógio ao norte, pilões arcanos, mercado na rua do anel, dirigíveis no céu,
    /// partículas (SmokeEmitter), pulsos, janelas, balanços e poucas luzes pontuais.
    ///
    /// Regra da câmera: nada pode tapar a praça. Cada peça só entra se a altura dela couber abaixo da linha
    /// entre a câmera de jogo (CameraSettings) e qualquer ponto da arena (AllowedHeight). Por isso o lado sul/oeste
    /// (o lado da câmera) fica baixo ou vazio perto do muro, e o lado norte/leste recebe os prédios altos.
    /// Nada fora do muro tem collider (o jogador não chega lá).
    /// Chamar depois da geometria da arena: CityBuilder.Build(arena).
    /// </summary>
    public static class CityBuilder
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        private const string ModelsFolder = "Assets/_Game/Art/Models/City";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string CameraSettingsPath = "Assets/_Game/Data/Camera/CameraSettings.asset";
        private const string RootName = "Cidade";
        private const int Seed = 4207;

        // Nomes da paleta (Docs/Design/arte-pixel.md); os que existirem em Materials substituem os do FBX.
        private static readonly string[] PaletteNames =
        {
            "FerroEscuro", "FerroMedio", "Cobre", "CobreOxidado", "Latao", "LataoEscuro", "Pedra", "PedraEscura",
            "Tijolo", "Madeira", "Telhado", "Tecido", "TecidoEscuro", "Couro", "Bandeira", "JanelaQuente",
            "CristalArcano", "BrasaFornalha"
        };

        // Muro da arena (ArenaBuilder): segmentos em r26 com 0,6 m de espessura para fora.
        private const float WallOuterRadius = 26.9f;
        private const float WallHeight = 3f;

        // Anéis de prédios (raio da fachada da frente). O terceiro anel só existe no lado longe da câmera.
        private const float Ring1Radius = 30.6f;
        private const float Ring2Radius = 44f;
        private const float Ring3Radius = 57f;
        private const float Ring3MinAngle = -80f;
        private const float Ring3MaxAngle = 140f;
        private const float BackStreetInner = 41f;
        private const float GroundOuterRadius = 85f;

        // Ruas radiais: ângulo (graus a partir de +Z, horário) e largura (m). A avenida norte leva à torre.
        private static readonly Vector2[] Streets =
        {
            new Vector2(0f, 7f), new Vector2(52f, 4f), new Vector2(105f, 4.5f), new Vector2(160f, 5f),
            new Vector2(215f, 4f), new Vector2(270f, 5f), new Vector2(320f, 4f)
        };
        private const float AvenueRing2Width = 15f; // praça da torre no fim da avenida
        private const float TowerRadius = 50.5f;

        // Câmera: margem de segurança e raio da área que nunca pode ser tapada.
        private const float VisibleRadius = 25.5f;
        private const float HeightMargin = 0.9f;

        // Orçamento de desempenho.
        private const int MaxPointLights = 22;
        private const int MaxSmokeEmitters = 56;
        private const float MinLightSpacing = 7f;

        private static readonly Color WarmLight = new Color(1f, 0.7f, 0.4f);
        private static readonly Color CrystalLight = new Color(0.35f, 0.9f, 1f);
        private static readonly Color FurnaceLight = new Color(1f, 0.45f, 0.15f);

        // ---------- Catálogo do kit ----------

        private sealed class Piece
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
            public int MinRing = 1;
        }

        private static readonly Piece[] Buildings =
        {
            new Piece { Name = "Oficina", Width = 8.7f, Body = 8f, Depth = 7.5f, Height = 8.9f, Eave = 4.6f, Weight = 1.1f, FarBias = -0.5f },
            new Piece { Name = "CasaLarga", Width = 10.9f, Body = 10f, Depth = 9.3f, Height = 12.4f, Eave = 7.4f, Weight = 1f, FarBias = 0f },
            new Piece { Name = "CasaEstreitaA", Width = 6.8f, Body = 6f, Depth = 8.5f, Height = 15.2f, Eave = 10.4f, Weight = 1f, FarBias = 0.2f },
            new Piece { Name = "CasaEstreitaB", Width = 6.8f, Body = 6f, Depth = 8.5f, Height = 18.4f, Eave = 13.6f, Weight = 0.7f, FarBias = 0.5f },
            new Piece { Name = "CasaAlta", Width = 7.6f, Body = 7f, Depth = 8.3f, Height = 21.6f, Eave = 17.3f, Weight = 0.4f, FarBias = 0.8f },
            new Piece { Name = "Fabrica", Width = 14.4f, Body = 14f, Depth = 10.2f, Height = 20.4f, Eave = 7f, Weight = 0.25f, FarBias = 0.5f },
            new Piece { Name = "TanqueAgua", Width = 4.6f, Body = 4.2f, Depth = 4.6f, Height = 10.1f, Eave = 0f, Weight = 0.35f, FarBias = 0f, Centered = true, MinRing = 2 },
        };

        private const float BridgeModelLength = 6f;   // PonteCanos.fbx

        // ---------- Estado da construção ----------

        private sealed class Placed
        {
            public Piece Piece;
            public Transform Transform;
            public float Angle;
            public int Ring;
        }

        private struct Request
        {
            public Transform Target;
            public int Kind;       // fumaça: SmokeKind; luz: 0 quente, 1 cristal, 2 fornalha, 3 cristal grande
            public float Priority;
            public float Intensity;
        }

        private static System.Random rng;
        private static Vector2 camOffset;
        private static float camHeight;
        private static Material stoneMat, darkStoneMat, ironMat;
        private static List<Placed> placed;
        private static List<Request> smokeRequests, lightRequests;
        private static HashSet<string> missingModels;
        private static List<Vector2> streetSlots; // ocupação da rua do anel: x = ângulo, y = meia largura (graus)

        public static void Build(Transform arenaRoot)
        {
            var old = arenaRoot.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            ConfigureImports();
            LoadMaterials();
            LoadCamera();
            rng = new System.Random(Seed);
            placed = new List<Placed>();
            smokeRequests = new List<Request>();
            lightRequests = new List<Request>();
            missingModels = new HashSet<string>();
            streetSlots = new List<Vector2>();

            var root = new GameObject(RootName).transform;
            root.SetParent(arenaRoot, false);

            BuildGround(Group("Chao", root));
            var buildings = Group("Predios", root);
            FillRing(buildings, 1, Ring1Radius, -180f, 180f);
            FillRing(buildings, 2, Ring2Radius, -180f, 180f);
            FillRing(buildings, 3, Ring3Radius, Ring3MinAngle, Ring3MaxAngle);
            BuildLandmarks(Group("Marcos", root));
            BuildBridges(Group("PontesCanos", root));
            BuildStreetLife(Group("RuaDoAnel", root));
            BuildSky(Group("Ceu", root));
            ApplySmoke();
            ApplyLights();

            foreach (string name in missingModels)
                Debug.LogWarning($"CityBuilder: {name}.fbx não encontrado em {ModelsFolder}. Rode Tools/Blender/build_city.py.");
            Debug.Log($"Cidade construída: {placed.Count} prédios.");
        }

        // ---------- Chão ----------

        private static void BuildGround(Transform parent)
        {
            // Anéis por cima do piso da arena (que é um quadrado): fora do muro manda o calçamento da cidade.
            Annulus("RuaDoAnel", parent, WallOuterRadius - 0.1f, Ring1Radius - 0.2f, 0.012f, darkStoneMat);
            Annulus("Calcada", parent, Ring1Radius - 0.2f, GroundOuterRadius, 0.012f, stoneMat);
            Annulus("RuaDeTras", parent, BackStreetInner, Ring2Radius - 0.2f, 0.016f, darkStoneMat);
            foreach (var s in Streets)
            {
                float length = GroundOuterRadius - Ring1Radius;
                var street = Box("RuaRadial", parent, Polar(s.x, Ring1Radius + length * 0.5f) + Vector3.up * 0.01f,
                    new Vector3(s.y, 0.02f, length), darkStoneMat);
                street.transform.localRotation = Quaternion.Euler(0f, s.x, 0f);
            }
        }

        // ---------- Prédios ----------

        private static void FillRing(Transform parent, int ring, float radius, float minAngle, float maxAngle)
        {
            var group = Group($"Anel{ring}", parent);
            for (int i = 0; i < Streets.Length; i++)
            {
                var a = Streets[i];
                var b = Streets[(i + 1) % Streets.Length];
                float start = a.x + HalfAngle(StreetWidth(a, ring) * 0.5f + 0.4f, radius);
                float end = (b.x <= a.x ? b.x + 360f : b.x) - HalfAngle(StreetWidth(b, ring) * 0.5f + 0.4f, radius);
                FillSector(group, ring, radius, start, end, minAngle, maxAngle);
            }
        }

        private static float StreetWidth(Vector2 street, int ring)
            => ring == 2 && Mathf.Approximately(street.x, 0f) ? AvenueRing2Width : street.y;

        private static void FillSector(Transform parent, int ring, float radius, float start, float end, float minAngle, float maxAngle)
        {
            float cursor = start;
            Piece previous = null;
            int guard = 0;
            while (guard++ < 64)
            {
                float remaining = (end - cursor) * Mathf.Deg2Rad * radius;
                if (remaining < 4f)
                    break;
                float step = 4f / radius * Mathf.Rad2Deg;
                if (!InRange(Mathf.DeltaAngle(0f, cursor + step), minAngle, maxAngle))
                {
                    cursor += step;
                    continue;
                }
                float far = Farness(cursor);
                var piece = PickBuilding(ring, far, previous, remaining, cursor, radius);
                if (piece == null)
                {
                    // Nada alto cabe sem tapar a câmera (lado sul perto do muro): fica uma banca baixa ou a rua aberta.
                    if (ring == 1)
                        LowFiller(parent, cursor + 2.5f / radius * Mathf.Rad2Deg, radius);
                    cursor += 5.5f / radius * Mathf.Rad2Deg;
                    previous = null;
                    continue;
                }
                if (!InRange(Mathf.DeltaAngle(0f, cursor + HalfAngle(piece.Width, radius)), minAngle, maxAngle))
                {
                    cursor += step;
                    continue;
                }

                float center = cursor + HalfAngle(piece.Width * 0.5f, radius);
                var go = PlaceBuilding(parent, piece, ring, radius, center);
                if (go != null)
                    placed.Add(new Placed { Piece = piece, Transform = go.transform, Angle = Mathf.DeltaAngle(0f, center), Ring = ring });

                // Vão entre prédios; às vezes um beco com canos, placa e bueiro.
                float gap = Range(0.25f, 0.7f);
                if (ring == 1 && rng.NextDouble() < 0.2 && go != null)
                {
                    gap = Range(2.4f, 3.2f);
                    float alleyAngle = center + HalfAngle(piece.Width * 0.5f, radius) + HalfAngle(gap * 0.5f, radius);
                    BuildAlley(parent, go.transform, piece, alleyAngle, radius);
                }
                cursor = center + HalfAngle(piece.Width * 0.5f, radius) + gap / radius * Mathf.Rad2Deg;
                previous = piece;
            }
        }

        private static void LowFiller(Transform parent, float angle, float radius)
        {
            var pos = Polar(angle, radius);
            if (AllowedHeight(pos + pos.normalized * 1.2f) < 3f || AllowedHeight(pos - pos.normalized * 1.2f) < 3f)
                return;
            var rot = Quaternion.LookRotation(-pos.normalized) * Quaternion.Euler(0f, Range(-10f, 10f), 0f);
            var stall = Model("BancaMercado", parent, pos, rot, Vector3.one);
            if (stall != null)
                lightRequests.Add(new Request { Target = Find(stall.transform, "LuzQuente"), Kind = 0, Priority = Visibility(pos) * 0.7f });
            var cratePos = Polar(angle + 3.2f / radius * Mathf.Rad2Deg, radius + 0.8f);
            if (AllowedHeight(cratePos) >= 1.9f)
                Model("Caixotes", parent, cratePos, Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one * 0.9f);
        }

        private static Piece PickBuilding(int ring, float far, Piece previous, float remaining, float cursor, float radius)
        {
            var candidates = new List<Piece>();
            var weights = new List<float>();
            foreach (var p in Buildings)
            {
                if (ring < p.MinRing || p.Width > remaining)
                    continue;
                float center = cursor + HalfAngle(p.Width * 0.5f, radius);
                Footprint(p, radius, center, out var pos, out var rot);
                if (FootprintAllowedHeight(pos, rot, p) < p.Height)
                    continue;
                float w = Mathf.Max(0.05f, p.Weight + p.FarBias * far);
                if (ring == 1 && p.Name == "Fabrica")
                    w *= 0.6f;
                if (ring == 3)
                    w *= p.Height > 14f ? 1.6f : 0.6f; // silhueta alta ao fundo
                if (p == previous)
                    w *= 0.25f;
                candidates.Add(p);
                weights.Add(w);
            }
            if (candidates.Count == 0)
                return null;

            float total = 0f;
            foreach (float w in weights)
                total += w;
            float roll = Range(0f, total);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private static void Footprint(Piece p, float radius, float angle, out Vector3 pos, out Quaternion rot)
        {
            float r = p.Centered ? radius + p.Depth * 0.5f : radius;
            pos = Polar(angle, r);
            rot = Quaternion.LookRotation(-pos.normalized); // +Z local (frente do kit) olha para a praça
        }

        private static GameObject PlaceBuilding(Transform parent, Piece piece, int ring, float radius, float angle)
        {
            Footprint(piece, radius, angle, out var pos, out var rot);
            if (piece.Centered)
                rot *= Quaternion.Euler(0f, Range(0f, 90f), 0f);
            // Espelhar varia a fachada (porta e placa trocam de lado) sem custo.
            var scale = new Vector3(!piece.Centered && rng.NextDouble() < 0.5 ? -1f : 1f, 1f, 1f);
            var go = Model(piece.Name, parent, pos, rot, scale);
            if (go == null)
                return null;
            if (Find(go.transform, "JanelaAndar") != null && rng.NextDouble() < 0.45)
            {
                go.AddComponent<WindowFlicker>().Configure(Range(0.12f, 0.35f));
                ClearStatic(go.transform, "Janela");
            }
            return go;
        }

        private static void BuildAlley(Transform parent, Transform building, Piece piece, float alleyAngle, float radius)
        {
            // Lado do prédio virado para o beco.
            var alleyPoint = Polar(alleyAngle, radius + 2f);
            var right = building.right;
            float side = Vector3.Dot(alleyPoint - building.localPosition, right) >= 0f ? 1f : -1f;
            var wallDir = right * side;
            var back = -building.forward;
            var wallBase = building.localPosition + wallDir * (piece.Body * 0.5f);

            Model("CanosParede", parent, wallBase + back * Range(2.2f, 4f), Quaternion.LookRotation(wallDir), Vector3.one);
            Model("PlacaPendurada", parent, wallBase + back * 0.7f + Vector3.up * 3.4f, Quaternion.LookRotation(wallDir), Vector3.one);
            Model("BueiroVapor", parent, Polar(alleyAngle, radius + 1.2f), Quaternion.identity, Vector3.one);
        }

        // ---------- Marcos ----------

        private static void BuildLandmarks(Transform parent)
        {
            // Torre do relógio no fim da avenida norte, com o mostrador virado para a praça.
            var towerPos = Polar(0f, TowerRadius);
            var tower = Model("TorreRelogio", parent, towerPos, Quaternion.LookRotation(-towerPos.normalized), Vector3.one);
            if (tower != null)
                lightRequests.Add(new Request { Target = Find(tower.transform, "LuzCristal"), Kind = 3, Priority = 0.35f });

            // Pilões arcanos nos cruzamentos da rua de trás com as ruas radiais.
            float crossRadius = (BackStreetInner + Ring2Radius) * 0.5f;
            foreach (float angle in new[] { 52f, 105f, 320f, 270f, 160f })
            {
                var pos = Polar(angle, crossRadius);
                if (AllowedHeight(pos) < 14.5f)
                    continue;
                var pylon = Model("PilaoArcano", parent, pos, Quaternion.Euler(0f, Range(0f, 90f), 0f), Vector3.one);
                if (pylon != null)
                    lightRequests.Add(new Request { Target = Find(pylon.transform, "LuzCristal"), Kind = 3, Priority = 0.4f });
            }
        }

        // ---------- Pontes de canos ----------

        private static void BuildBridges(Transform parent)
        {
            // 1) Sobre as ruas radiais, entre os prédios vizinhos de cada anel.
            foreach (int ring in new[] { 1, 2 })
            {
                foreach (var s in Streets)
                {
                    Placed left = null, right = null;
                    float bestL = 30f, bestR = 30f;
                    foreach (var p in placed)
                    {
                        if (p.Ring != ring || p.Piece.Centered)
                            continue;
                        float d = Mathf.DeltaAngle(s.x, p.Angle);
                        if (d < 0f && -d < bestL) { bestL = -d; left = p; }
                        if (d > 0f && d < bestR) { bestR = d; right = p; }
                    }
                    if (left == null || right == null || rng.NextDouble() < 0.25)
                        continue;
                    float height = Mathf.Min(left.Piece.Eave, right.Piece.Eave) - 1.4f;
                    if (height < 5.5f)
                        continue;
                    float depth = Mathf.Min(left.Piece.Depth, right.Piece.Depth) * Range(0.3f, 0.6f);
                    var a = SidePoint(left, right.Transform.localPosition) - left.Transform.forward * depth;
                    var b = SidePoint(right, left.Transform.localPosition) - right.Transform.forward * depth;
                    Bridge(parent, a, b, height);
                    // Às vezes uma segunda ponte, mais baixa e mais funda, no mesmo vão.
                    if (height - 3.5f >= 5.5f && rng.NextDouble() < 0.45)
                        Bridge(parent, a - left.Transform.forward * 1.6f, b - right.Transform.forward * 1.6f, height - 3.5f);
                }
            }

            // 2) Atravessando a rua de trás: do fundo de um prédio do anel 1 à fachada de um do anel 2.
            int count = 0;
            foreach (var outer in placed)
            {
                if (outer.Ring != 2 || outer.Piece.Centered || count >= 8)
                    continue;
                Placed inner = null;
                float best = 3f;
                foreach (var p in placed)
                {
                    if (p.Ring != 1)
                        continue;
                    float d = Mathf.Abs(Mathf.DeltaAngle(p.Angle, outer.Angle));
                    if (d < best) { best = d; inner = p; }
                }
                if (inner == null || rng.NextDouble() < 0.4)
                    continue;
                float height = Mathf.Min(inner.Piece.Eave, outer.Piece.Eave) - 1.2f;
                if (height < 5.5f)
                    continue;
                var a = Polar(outer.Angle, Ring1Radius + inner.Piece.Depth - 0.4f);
                var b = Polar(outer.Angle, Ring2Radius + 0.4f);
                if (Bridge(parent, a, b, height))
                    count++;
            }

            // 3) Do muro da arena até a fachada: a cidade alimenta a praça (Pilar 4).
            count = 0;
            foreach (var p in placed)
            {
                if (p.Ring != 1 || count >= 6)
                    continue;
                // Só onde a fachada tem um vão livre acima dos toldos: pilar central da CasaLarga, pilastra da Fábrica.
                float lateral, wallBridgeHeight, widthScale;
                if (p.Piece.Name == "CasaLarga") { lateral = 0f; wallBridgeHeight = 4.4f; widthScale = 1f; }
                else if (p.Piece.Name == "Fabrica") { lateral = 2.3f; wallBridgeHeight = 4.0f; widthScale = 0.7f; }
                else continue;
                var facade = p.Transform.localPosition + p.Transform.right * lateral;
                var dir = facade.normalized;
                var start = dir * (WallOuterRadius - 0.6f);
                var mid = (start + facade) * 0.5f;
                if (AllowedHeight(mid) < wallBridgeHeight + 1.4f || AllowedHeight(start) < wallBridgeHeight + 1.4f)
                    continue;
                if (!Bridge(parent, start, facade + dir * 0.3f, wallBridgeHeight, widthScale))
                    continue;
                // Pé de ferro apoiando a ponte no topo do muro.
                Box("PeDaPonte", parent, start + dir * 0.4f + Vector3.up * (WallHeight + wallBridgeHeight) * 0.5f,
                    new Vector3(0.35f, wallBridgeHeight - WallHeight + 0.1f, 0.35f), ironMat);
                count++;
            }
        }

        private static Vector3 SidePoint(Placed p, Vector3 toward)
        {
            // A ponte entra 0,2 m na parede.
            var right = p.Transform.right;
            var a = p.Transform.localPosition + right * (p.Piece.Body * 0.5f - 0.2f);
            var b = p.Transform.localPosition - right * (p.Piece.Body * 0.5f - 0.2f);
            return (a - toward).sqrMagnitude < (b - toward).sqrMagnitude ? a : b;
        }

        private static bool Bridge(Transform parent, Vector3 a, Vector3 b, float height, float widthScale = 1f)
        {
            a.y = b.y = 0f;
            var dir = b - a;
            float length = dir.magnitude;
            if (length < 2.5f || length > 12f)
                return false;
            var mid = (a + b) * 0.5f;
            if (AllowedHeight(mid) < height + 1.3f)
                return false;
            var rot = Quaternion.LookRotation(dir / length) * Quaternion.Euler(0f, -90f, 0f); // X local ao longo do vão
            return Model("PonteCanos", parent, mid + Vector3.up * height, rot,
                new Vector3(length / BridgeModelLength, 1f, widthScale)) != null;
        }

        // ---------- Rua do anel: mercado, caixotes, lampiões, bueiros ----------

        private static void BuildStreetLife(Transform parent)
        {
            const float streetRadius = 28.3f; // meio da faixa livre entre o muro (26,9) e os toldos (~29,4)

            // Três trechos de mercado nas laterais e no norte (onde a câmera vê a rua).
            foreach (var zone in new[] { new Vector2(68f, 102f), new Vector2(-102f, -70f), new Vector2(18f, 40f) })
            {
                for (float a = zone.x; a <= zone.y; a += Range(8f, 11f))
                {
                    var pos = Polar(a, 28.4f);
                    if (AllowedHeight(pos) < 3f || !TakeSlot(a, 2f, 28.4f))
                        continue;
                    // Banca de costas para o muro, virada para a rua, um pouco torta.
                    var rot = Quaternion.LookRotation(pos.normalized) * Quaternion.Euler(0f, Range(-10f, 10f), 0f);
                    var stall = Model("BancaMercado", parent, pos, rot, Vector3.one);
                    if (stall != null)
                        lightRequests.Add(new Request { Target = Find(stall.transform, "LuzQuente"), Kind = 0, Priority = Visibility(pos) * 0.8f });
                    if (rng.NextDouble() < 0.7)
                        Crates(parent, a + 4.2f / streetRadius * Mathf.Rad2Deg, 0.9f);
                }
            }

            // Lampiões encostados no muro, com o braço sobre a rua.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 15f + 7.5f;
                var pos = Polar(angle, WallOuterRadius + 0.45f);
                if (AllowedHeight(pos) < 4.2f || !TakeSlot(angle, 0.6f, streetRadius))
                    continue;
                var lamp = Model("LampiaoRua", parent, pos, Quaternion.LookRotation(pos.normalized), Vector3.one);
                if (lamp != null)
                    lightRequests.Add(new Request { Target = Find(lamp.transform, "LuzCristal"), Kind = 1, Priority = Visibility(pos) });
            }

            // Caixotes e bueiros espalhados pela rua do anel.
            for (int i = 0; i < 14; i++)
                Crates(parent, Range(-180f, 180f), Range(0.8f, 1f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + Range(-8f, 8f);
                if (TakeSlot(a, 0.7f, streetRadius))
                    Model("BueiroVapor", parent, Polar(a, Range(28.6f, 29.1f)), Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one);
            }

            // Arcos na boca das ruas radiais, com os pilares nas bordas da rua (só onde não tapam a câmera).
            foreach (var s in Streets)
            {
                var pos = Polar(s.x, Ring1Radius + 0.6f);
                if (AllowedHeight(pos) < 7.5f)
                    continue;
                float scale = (s.y * 0.5f + 0.2f) / 2.7f; // pilar do Arco.fbx em x = ±2,7
                Model("Arco", parent, pos, Quaternion.LookRotation(-pos.normalized), new Vector3(scale, 1f, 1f));
            }
        }

        /// <summary>Caixotes com o lado comprido ao longo da rua, sem encostar no muro.</summary>
        private static void Crates(Transform parent, float angle, float scale)
        {
            var pos = Polar(angle, 28.1f);
            if (AllowedHeight(pos) < 1.9f || !TakeSlot(angle, 2.1f * scale, 28.1f))
                return;
            var rot = Quaternion.LookRotation(pos.normalized) * Quaternion.Euler(0f, Range(-10f, 10f) + (rng.NextDouble() < 0.5 ? 180f : 0f), 0f);
            Model("Caixotes", parent, pos, rot, Vector3.one * scale);
        }

        /// <summary>Reserva um trecho da rua do anel (meia largura em metros); falso se já estiver ocupado.</summary>
        private static bool TakeSlot(float angle, float halfWidth, float radius)
        {
            float half = halfWidth / radius * Mathf.Rad2Deg;
            foreach (var slot in streetSlots)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(slot.x, angle)) < slot.y + half)
                    return false;
            }
            streetSlots.Add(new Vector2(angle, half));
            return true;
        }

        // ---------- Céu ----------

        private static void BuildSky(Transform parent)
        {
            // Volta grande em torno da cidade.
            Airship(parent, Circle(Vector3.zero, 64f, 16, 24f, 2f, 0f), 3.2f);
            // Elipse sobre o lado norte-leste.
            var center = Polar(40f, 54f);
            var ellipse = new Vector3[10];
            for (int i = 0; i < ellipse.Length; i++)
            {
                float t = -i * Mathf.PI * 2f / ellipse.Length;
                ellipse[i] = center + new Vector3(Mathf.Cos(t) * 24f, 18f + Mathf.Sin(t * 2f) * 1.5f, Mathf.Sin(t) * 12f);
            }
            Airship(parent, ellipse, 2.4f);
            // Volta lenta em torno da torre do relógio.
            Airship(parent, Circle(Polar(0f, TowerRadius), 19f, 12, 29f, 1f, 1f), 1.8f);
        }

        private static Vector3[] Circle(Vector3 center, float radius, int count, float height, float wave, float phase)
        {
            var pts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = i * Mathf.PI * 2f / count + phase;
                pts[i] = center + new Vector3(Mathf.Sin(t) * radius, height + Mathf.Sin(t * 3f) * wave, Mathf.Cos(t) * radius);
            }
            return pts;
        }

        private static void Airship(Transform parent, Vector3[] localPoints, float speed)
        {
            var go = Model("Dirigivel", parent, localPoints[0], Quaternion.identity, Vector3.one, isStatic: false);
            if (go == null)
                return;
            var world = new Vector3[localPoints.Length];
            for (int i = 0; i < world.Length; i++)
                world[i] = parent.TransformPoint(localPoints[i]);
            go.AddComponent<PathMover>().Configure(world, speed, true, true);
        }

        // ---------- Modelos e vida ----------

        private static GameObject Model(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale, bool isStatic = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{name}.fbx");
            if (asset == null)
            {
                missingModels.Add(name);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = isStatic;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = name == "Dirigivel" || name == "BueiroVapor" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            Decorate(go.transform, name);
            return go;
        }

        /// <summary>Liga os componentes de vida pelos nomes dos filhos do FBX (convenção em build_city.py).</summary>
        private static void Decorate(Transform root, string model)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (t == root || n == model)
                    continue;
                if (n.StartsWith("Engrenagem"))
                {
                    // Eixo da fachada: Y do Blender = Z local. A Engrenagem3 da torre fica deitada (eixo Y).
                    var axis = model == "TorreRelogio" && n == "Engrenagem3" ? Vector3.up : Vector3.forward;
                    float speed = n == "Engrenagem2" ? -48f : n == "Engrenagem3" ? 35f : 28f;
                    Dynamic(t).AddComponent<Spinner>().Configure(axis, speed, rng.NextDouble() < 0.25 ? 0.4f : 0f);
                }
                else if (n == "PonteiroMinuto")
                    Dynamic(t).AddComponent<Spinner>().Configure(Vector3.forward, 6f, 0f);
                else if (n == "PonteiroHora")
                    Dynamic(t).AddComponent<Spinner>().Configure(Vector3.forward, 0.5f, 0f);
                else if (n == "Helice")
                    Dynamic(t).AddComponent<Spinner>().Configure(Vector3.forward, 540f, 0f);
                else if (n == "CristalFlutuante")
                {
                    Dynamic(t).AddComponent<Spinner>().Configure(Vector3.up, 25f, 0f);
                    t.gameObject.AddComponent<EmissivePulse>().Configure(Range(0.4f, 0.8f), 0.55f, 1.35f);
                }
                else if (n == "CristalPulso")
                    Dynamic(t).AddComponent<EmissivePulse>().Configure(Range(0.3f, 0.9f), 0.6f, 1.25f);
                else if (n == "PlacaArco" || n == "Bandeirola" || n == "Lanterna")
                    Dynamic(t).AddComponent<Sway>().Configure(Vector3.right, n == "Bandeirola" ? 7f : 4f, Range(0.6f, 1.1f));
                else if (n.StartsWith("Placa"))
                    Dynamic(t).AddComponent<Sway>().Configure(Vector3.forward, 6f, Range(0.5f, 1f));
                else if (n.StartsWith("Fumaca"))
                    QueueSmoke(t, SmokeKind.Chimney, Range(0.6f, 1.1f), model == "Dirigivel" ? 0.5f : Visibility(t.position) * 0.6f);
                else if (n.StartsWith("Vapor"))
                    QueueSmoke(t, SmokeKind.Steam, Range(0.6f, 1f), Visibility(t.position) + 0.4f);
                else if (n.StartsWith("Faisca"))
                    QueueSmoke(t, SmokeKind.Sparks, Range(0.6f, 1f), Visibility(t.position) + 0.3f);
                else if (n.StartsWith("Arcano"))
                    QueueSmoke(t, SmokeKind.ArcaneMotes, 1f, 1f);
                else if (n.StartsWith("LuzFornalha"))
                    lightRequests.Add(new Request { Target = t, Kind = 2, Priority = Visibility(t.position) + 0.5f });
                else if (n.StartsWith("LuzQuente") && model != "BancaMercado")
                    lightRequests.Add(new Request { Target = t, Kind = 0, Priority = Visibility(t.position) });
            }
        }

        private static GameObject Dynamic(Transform t)
        {
            t.gameObject.isStatic = false;
            return t.gameObject;
        }

        private static void ClearStatic(Transform root, string prefix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(prefix))
                    t.gameObject.isStatic = false;
            }
        }

        private static void QueueSmoke(Transform t, SmokeKind kind, float intensity, float priority)
        {
            smokeRequests.Add(new Request { Target = t, Kind = (int)kind, Intensity = intensity, Priority = priority + Range(0f, 0.35f) });
        }

        private static void ApplySmoke()
        {
            smokeRequests.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            for (int i = 0; i < smokeRequests.Count && i < MaxSmokeEmitters; i++)
            {
                var r = smokeRequests[i];
                if (r.Target == null)
                    continue;
                Dynamic(r.Target).AddComponent<SmokeEmitter>().Configure((SmokeKind)r.Kind, r.Intensity);
            }
        }

        /// <summary>Poucas luzes pontuais, priorizando as que a câmera de jogo vê e espaçadas entre si.</summary>
        private static void ApplyLights()
        {
            lightRequests.RemoveAll(r => r.Target == null);
            for (int i = 0; i < lightRequests.Count; i++)
            {
                var r = lightRequests[i];
                r.Priority += Range(0f, 0.2f);
                lightRequests[i] = r;
            }
            lightRequests.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            var chosen = new List<Vector3>();
            int flickers = 0;
            foreach (var r in lightRequests)
            {
                if (chosen.Count >= MaxPointLights)
                    break;
                var pos = r.Target.position;
                bool tooClose = false;
                foreach (var c in chosen)
                    tooClose |= (c - pos).sqrMagnitude < MinLightSpacing * MinLightSpacing;
                if (tooClose)
                    continue;
                chosen.Add(pos);

                var go = new GameObject("Luz");
                go.transform.SetParent(r.Target, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                switch (r.Kind)
                {
                    case 0: light.color = WarmLight; light.range = 7f; light.intensity = 4f; break;
                    case 1: light.color = CrystalLight; light.range = 7.5f; light.intensity = 5f; break;
                    case 2: light.color = FurnaceLight; light.range = 8f; light.intensity = 6f; go.AddComponent<FireFlicker>(); break;
                    default: light.color = CrystalLight; light.range = 14f; light.intensity = 9f; break;
                }
                // Um lampião ou outro com mau contato.
                if (r.Kind == 1 && flickers < 2 && rng.NextDouble() < 0.3)
                {
                    go.AddComponent<LightFlicker>();
                    flickers++;
                }
            }
        }

        // ---------- Câmera: altura permitida ----------

        private static void LoadCamera()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CameraSettings>(CameraSettingsPath);
            float distance = settings != null ? settings.distance : 14f;
            float pitch = (settings != null ? settings.pitch : 50f) * Mathf.Deg2Rad;
            float yaw = (settings != null ? settings.yaw : 30f) * Mathf.Deg2Rad;
            float focus = settings != null ? settings.focusHeight : 1f;
            // A câmera fica atrás do foco, no sentido oposto ao giro.
            camOffset = -new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * distance * Mathf.Cos(pitch);
            camHeight = focus + distance * Mathf.Sin(pitch);
        }

        /// <summary>
        /// Altura máxima num ponto (plano XZ) sem entrar na linha entre a câmera e um ponto da arena.
        /// Infinito quando o ponto nunca fica entre a câmera e a praça (lado longe, ou atrás da câmera).
        /// </summary>
        private static float AllowedHeight(Vector3 p)
        {
            var b = new Vector2(p.x, p.z);
            float rr = VisibleRadius * VisibleRadius;
            float bb = Vector2.Dot(b, b);
            if (bb <= rr)
                return 0f;
            float oo = Vector2.Dot(camOffset, camOffset);
            float bo = Vector2.Dot(b, camOffset);
            float disc = bo * bo - oo * (bb - rr);
            if (disc < 0f)
                return float.PositiveInfinity;
            float t = (bo - Mathf.Sqrt(disc)) / oo;
            if (t < 0f || t > 1f)
                return float.PositiveInfinity;
            return camHeight * t * HeightMargin;
        }

        private static float FootprintAllowedHeight(Vector3 pos, Quaternion rot, Piece p)
        {
            float hw = p.Width * 0.5f;
            float front = p.Centered ? p.Depth * 0.5f : 1.2f; // toldos avançam ~1,2 m
            float back = p.Centered ? -p.Depth * 0.5f : -p.Depth;
            float min = float.PositiveInfinity;
            foreach (var local in new[]
                     {
                         new Vector3(-hw, 0f, front), new Vector3(0f, 0f, front), new Vector3(hw, 0f, front),
                         new Vector3(-hw, 0f, (front + back) * 0.5f), new Vector3(hw, 0f, (front + back) * 0.5f),
                         new Vector3(-hw, 0f, back), new Vector3(0f, 0f, back), new Vector3(hw, 0f, back)
                     })
                min = Mathf.Min(min, AllowedHeight(pos + rot * local));
            return min;
        }

        /// <summary>0 a 1: quanto um ponto aparece na câmera de jogo (perto do muro e no lado longe ou lateral).</summary>
        private static float Visibility(Vector3 p)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            float radial = Mathf.Clamp01(1f - (r - WallOuterRadius) / 12f);
            float angle = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            return radial * (0.55f + 0.45f * Farness(angle));
        }

        /// <summary>1 no lado oposto à câmera (giro 30°), -1 no lado da câmera.</summary>
        private static float Farness(float angle) => Mathf.Cos((angle - 30f) * Mathf.Deg2Rad);

        // ---------- Importação e materiais ----------

        /// <summary>FBX da cidade: sem animação/câmera/luz e com os materiais da paleta que existirem no projeto.</summary>
        private static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    continue;

                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.useFileScale = true;
                importer.globalScale = 1f;
                importer.isReadable = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (string name in PaletteNames)
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");
                    if (mat != null)
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                }
                importer.SaveAndReimport();
            }
        }

        private static void LoadMaterials()
        {
            stoneMat = LoadMat("Pedra", "PisoPedra");
            darkStoneMat = LoadMat("PedraEscura", "PisoPedra");
            ironMat = LoadMat("FerroEscuro", "PedraEscura");
        }

        private static Material LoadMat(string name, string fallback)
            => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat")
               ?? AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{fallback}.mat");

        // ---------- Utilitários ----------

        private static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static float HalfAngle(float halfWidth, float radius) => Mathf.Atan2(halfWidth, radius) * Mathf.Rad2Deg;

        private static bool InRange(float angle, float min, float max) => angle >= min && angle <= max;

        private static Vector3 Polar(float angleDegrees, float radius)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }

        private static Transform Find(Transform root, string prefix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(prefix))
                    return t;
            }
            return null;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            return go.transform;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        /// <summary>Anel plano (malha gerada, guardada na cena) com UV em metros.</summary>
        private static void Annulus(string name, Transform parent, float inner, float outer, float y, Material mat)
        {
            const int segments = 128;
            var verts = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var normals = new Vector3[verts.Length];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = dir * inner + Vector3.up * y;
                verts[i * 2 + 1] = dir * outer + Vector3.up * y;
                uvs[i * 2] = new Vector2(verts[i * 2].x, verts[i * 2].z) * 0.25f;
                uvs[i * 2 + 1] = new Vector2(verts[i * 2 + 1].x, verts[i * 2 + 1].z) * 0.25f;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
            }
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                // Ordem horária vista de cima (face para +Y na Unity).
                tris[i * 6] = v; tris[i * 6 + 1] = v + 1; tris[i * 6 + 2] = v + 3;
                tris[i * 6 + 3] = v; tris[i * 6 + 4] = v + 3; tris[i * 6 + 5] = v + 2;
            }
            var mesh = new Mesh { name = $"Cidade_{name}", vertices = verts, uv = uvs, normals = normals, triangles = tris };
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
        }
    }
}
