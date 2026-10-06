using System.Collections.Generic;
using Game.Arena;
using Game.Arena.Life;
using Game.Core.Map;
using Game.Core.Math;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Cidade steampunk-mágica do mapa novo (D-042, passe do mapa D-073 a D-082), com o kit de Tools/Blender/build_city.py.
    /// Cria "Cidade" sob a raiz da arena: chão visual até r 85, prédios em volta da praça central, ao longo das três avenidas,
    /// nas praças menores, nas bocas das ruas e no fundo (os prédios SÃO o limite do mapa e bloqueiam a passagem), torre do
    /// relógio e pilões arcanos no meio das praças menores, pontes de canos sobre as avenidas, dirigíveis no céu,
    /// partículas (SmokeEmitter), pulsos, janelas, balanços e poucas luzes pontuais.
    ///
    /// Onde cada prédio fica é decidido pelo <see cref="CityPlanner"/> (puro, sem UnityEngine) a partir do
    /// <see cref="MapLayout"/> (Data/Map/MapLayoutSettings.asset). Aqui só se instancia:
    ///  - Cidade/Predios: os modelos; Cidade/Colisores: um BoxCollider por prédio (corpo x fundo x altura, 0,3 m à frente da
    ///    fachada), na camada Cenario e fora de qualquer transform espelhado; Cidade/ColisoresMarcos: os da torre e dos pilões.
    ///  - Altura: o limite vem de MapLayout.MaxBuildingHeight (planta que toca o envelope da câmera: 10,2 m; o resto é livre).
    ///    A translucidez (D-076) resolve a oclusão: as peças que podem tapar o jogador recebem a rendering layer 8 (VazadoLayer).
    /// Chamar depois da geometria da arena: CityBuilder.Build(arena).
    /// </summary>
    public static class CityBuilder
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        private const string ModelsFolder = "Assets/_Game/Art/Models/City";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string MapSettingsFolder = "Assets/_Game/Data/Map";
        private const string MapSettingsPath = MapSettingsFolder + "/MapLayoutSettings.asset";
        private const string RootName = "Cidade";
        private const int Seed = 4207;

        // Nomes da paleta (Docs/Design/arte-pixel.md); os que existirem em Materials substituem os do FBX.
        private static readonly string[] PaletteNames =
        {
            "FerroEscuro", "FerroMedio", "Cobre", "CobreOxidado", "Latao", "LataoEscuro", "Pedra", "PedraEscura",
            "Tijolo", "Madeira", "Telhado", "Tecido", "TecidoEscuro", "Couro", "Bandeira", "JanelaQuente",
            "CristalArcano", "BrasaFornalha"
        };

        // Chão visual: um disco só, um pouco abaixo dos pisos andáveis (que são da arena), sem collider.
        private const float GroundOuterRadius = 85f;
        private const float GroundY = -0.04f;

        // Marcos no meio das praças menores: base de colisão (m). Torre 7,2 m (sobra um anel andável de ~3,9 m), pilão 3 m.
        private const float TowerBase = 7.2f;
        private const float PylonBase = 3f;

        // Pontes de canos sobre as avenidas: pelo menos 6 m de altura, entram 0,2 m na parede, sem collider.
        private const float BridgeMinHeight = 6f;
        private const float BridgeInset = 0.2f;
        private const float BridgeEaveMargin = 1.4f; // a ponte fica abaixo do beiral
        private const float BridgeChance = 1f;
        private const float BridgeMinSpacing = 6f;
        private const int BridgesPerAvenue = 2;
        private const float BridgeMaxLength = 12f;

        // Bueiros de vapor ao longo das avenidas, nas praças menores e na borda da praça (nunca no disco de combate, r < 26).
        private const int ManholesPerAvenue = 2;
        private const int ManholesPerSmallPlaza = 1;
        private const int ManholesInPlaza = 5;
        private const float PlazaManholeRadius = 27.2f;

        // Vãos da borda andável: banca de mercado nos vãos longos, Caixotes nos curtos (bloqueio só visual).
        private const float StallMinGap = 3.4f;

        // Orçamento de desempenho (mapa maior que o anterior: 22 luzes e 56 emissores).
        private const int MaxPointLights = 34;
        private const int MaxSmokeEmitters = 90;
        private const float MinLightSpacing = 7f;
        private const float LightReach = 12f; // distância da área andável (m) em que a prioridade de luz e fumaça cai a zero

        private static readonly Color WarmLight = new Color(1f, 0.7f, 0.4f);
        private static readonly Color CrystalLight = new Color(0.35f, 0.9f, 1f);
        private static readonly Color FurnaceLight = new Color(1f, 0.45f, 0.15f);

        private const float BridgeModelLength = 6f;   // PonteCanos.fbx

        // Cristal vivo (D-069). Números balanceáveis em Data/Ambience/CrystalAmbienceSettings.asset.
        // Torres: o cristal e a luz delas pulsam juntos (CrystalPulse) no lugar do EmissivePulse.
        private static readonly string[] Towers = { "TorreRelogio", "PilaoArcano" };
        // Veio na crista do cano do meio da PonteCanos (cobre oxidado, raio 0,15 com o eixo a 0,92 m do pé):
        // a cidade manda energia para a praça.
        private const float BridgeVeinHeight = 1.075f;
        private const float BridgeVeinLength = 5.8f;  // entra nos flanges de latão das pontas
        private const float BridgeVeinWidth = 0.1f;
        private const float BridgeVeinThickness = 0.04f;

        // ---------- Estado da construção ----------

        private struct Request
        {
            public Transform Target;
            public int Kind;       // fumaça: SmokeKind; luz: 0 quente, 1 cristal, 2 fornalha, 3 cristal grande
            public float Priority;
            public float Intensity;
        }

        private static System.Random rng;
        private static MapLayout layout;
        private static Material darkStoneMat, crystalMat;
        private static CrystalAmbienceSettings crystalSettings;
        private static List<Transform> towers;
        private static List<Request> smokeRequests, lightRequests;
        private static HashSet<string> missingModels;
        private static Vector3 clockTowerPosition;

        public static void Build(Transform arenaRoot)
        {
            var old = arenaRoot.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            ConfigureImports();
            LoadMaterials();
            layout = LoadLayout();
            rng = new System.Random(Seed);
            smokeRequests = new List<Request>();
            lightRequests = new List<Request>();
            missingModels = new HashSet<string>();
            towers = new List<Transform>();

            var root = new GameObject(RootName).transform;
            root.SetParent(arenaRoot, false);

            BuildGround(Group("Chao", root));

            var planner = new CityPlanner(layout, Seed);
            planner.Plan();
            var buildings = Group("Predios", root);
            var counts = PlaceBuildings(buildings, Group("Colisores", root), planner.Buildings);

            var landmarks = Group("Marcos", root);
            BuildLandmarks(landmarks, Group("ColisoresMarcos", root));
            var bridges = Group("PontesCanos", root);
            BuildBridges(bridges, planner.Buildings);
            var gapProps = Group("Vaos", root);
            BuildGapProps(gapProps, planner.Gaps);
            BuildManholes(Group("Bueiros", root));
            BuildSky(Group("Ceu", root));
            ApplySmoke();
            ApplyLights();
            ApplyTowerPulse();

            // Tudo que pode tapar o jogador pode vazar (D-076); personagem, inimigos, máquinas, chão e céu não.
            foreach (var group in new[] { buildings, landmarks, bridges, gapProps })
                VazadoLayer.MarkAll(group.gameObject);

            foreach (string name in missingModels)
                Debug.LogWarning($"CityBuilder: {name}.fbx não encontrado em {ModelsFolder}. Rode Tools/Blender/build_city.py.");
            var summary = new System.Text.StringBuilder();
            foreach (var pair in counts)
                summary.Append($" {pair.Key}={pair.Value}");
            Debug.Log($"Cidade construída: {planner.Buildings.Count} prédios ({summary.ToString().Trim()}), {planner.Gaps.Count} vãos fechados com props.");
        }

        // ---------- Mapa ----------

        /// <summary>Layout do mapa lido de Data/Map/MapLayoutSettings.asset (criado com os números padrão se ainda não existir).</summary>
        private static MapLayout LoadLayout()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MapLayoutSettings>(MapSettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(MapSettingsFolder))
                    AssetDatabase.CreateFolder("Assets/_Game/Data", "Map");
                settings = ScriptableObject.CreateInstance<MapLayoutSettings>();
                AssetDatabase.CreateAsset(settings, MapSettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings.ToLayout();
        }

        // ---------- Chão ----------

        private static void BuildGround(Transform parent)
        {
            // Os pisos andáveis são da arena (ArenaBuilder); aqui só o chão visual por baixo deles, até onde a câmera alcança.
            Annulus("Calcada", parent, 0f, GroundOuterRadius, GroundY, darkStoneMat);
        }

        // ---------- Prédios ----------

        private static SortedDictionary<string, int> PlaceBuildings(Transform parent, Transform colliders, IReadOnlyList<BuildingPlan> plans)
        {
            var counts = new SortedDictionary<string, int>();
            var groups = new Dictionary<string, Transform>();
            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                if (!groups.TryGetValue(plan.Region, out var group))
                {
                    group = Group(plan.Region, parent);
                    groups[plan.Region] = group;
                }

                var pos = new Vector3(plan.Pivot.X, 0f, plan.Pivot.Y);
                var rot = Quaternion.LookRotation(new Vector3(plan.Forward.X, 0f, plan.Forward.Y));
                // Espelhar varia a fachada (porta e placa trocam de lado) sem custo; o collider fica fora do espelho.
                var scale = new Vector3(plan.Mirror ? -plan.ScaleX : plan.ScaleX, 1f, 1f);
                var go = Model(plan.Piece.Name, group, pos, rot, scale);
                if (go != null && Find(go.transform, "JanelaAndar") != null && rng.NextDouble() < 0.45)
                {
                    go.AddComponent<WindowFlicker>().Configure(Range(0.12f, 0.35f));
                    ClearStatic(go.transform, "Janela");
                }
                AddBuildingCollider(colliders, plan, i);

                string key = plan.Region.StartsWith("Avenida") ? "Avenida"
                    : plan.Region.StartsWith("Boca") ? "Boca"
                    : plan.Region.StartsWith("PracaMenor") ? "PracaMenor"
                    : plan.Region;
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            return counts;
        }

        /// <summary>
        /// Um BoxCollider por prédio (camada Cenario), fora do transform espelhado: caixa do corpo (largura da parede) x fundo,
        /// com a frente 0,3 m adiante da fachada. A altura é a do prédio todo: a linha da câmera para o jogador (SeeThroughDriver)
        /// precisa bater também no telhado e nas chaminés.
        /// </summary>
        private static void AddBuildingCollider(Transform parent, BuildingPlan plan, int index)
        {
            var rect = plan.Collider;
            float height = plan.ColliderHeight;
            var go = new GameObject($"Col_{plan.Piece.Name}_{index:000}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(rect.Center.X, 0f, rect.Center.Y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(rect.Axis.X, 0f, rect.Axis.Y));
            go.layer = MapLayers.Cenario;
            go.isStatic = true;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(rect.Width, height, rect.Length);
            box.center = new Vector3(0f, height * 0.5f, 0f);
        }

        // ---------- Marcos ----------

        private static void BuildLandmarks(Transform parent, Transform colliders)
        {
            // Torre do relógio no centro da praça menor da avenida do meio (a mais perto de 0°); pilões nas outras.
            int towerIndex = 0;
            float best = float.MaxValue;
            for (int i = 0; i < layout.StreetCount; i++)
            {
                float a = Mathf.Abs(layout.Avenues[i].AngleDeg);
                if (a < best)
                {
                    best = a;
                    towerIndex = i;
                }
            }

            for (int i = 0; i < layout.StreetCount; i++)
            {
                var center = layout.SmallPlazas[i].Center;
                var pos = new Vector3(center.X, 0f, center.Y);
                bool isTower = i == towerIndex;
                // O mostrador da torre fica virado para a praça central (e para a câmera, que olha do sul).
                var rot = isTower
                    ? Quaternion.LookRotation(-pos.normalized)
                    : Quaternion.Euler(0f, Range(0f, 90f), 0f);
                var go = Model(isTower ? "TorreRelogio" : "PilaoArcano", parent, pos, rot, Vector3.one);
                if (isTower)
                    clockTowerPosition = pos;
                if (go != null)
                    lightRequests.Add(new Request { Target = Find(go.transform, "LuzCristal"), Kind = 3, Priority = 0.9f });

                // Collider da base (a torre inteira, para a oclusão da câmera), na camada Cenario.
                float baseSize = isTower ? TowerBase : PylonBase;
                float height = 12f;
                if (go != null)
                {
                    var bounds = new Bounds(pos, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        bounds.Encapsulate(r.bounds);
                    height = Mathf.Max(2f, bounds.max.y);
                }
                var col = new GameObject(isTower ? $"ColTorre_{i}" : $"ColPilao_{i}");
                col.transform.SetParent(colliders, false);
                col.transform.position = pos;
                col.transform.rotation = rot;
                col.layer = MapLayers.Cenario;
                col.isStatic = true;
                var box = col.AddComponent<BoxCollider>();
                box.size = new Vector3(baseSize, height, baseSize);
                box.center = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        // ---------- Pontes de canos ----------

        /// <summary>
        /// Sobre as avenidas, entre dois prédios que se olham (um de cada lado), a pelo menos 6 m do chão e abaixo dos dois
        /// beirais. Sem collider: passam por cima de quem anda.
        /// </summary>
        private static void BuildBridges(Transform parent, IReadOnlyList<BuildingPlan> plans)
        {
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var left = new List<BuildingPlan>();
                var right = new List<BuildingPlan>();
                foreach (var p in plans)
                {
                    if (p.Region != $"Avenida{i}")
                        continue;
                    (p.Side < 0 ? left : right).Add(p);
                }
                left.Sort((a, b) => a.Along.CompareTo(b.Along));

                var avenue = layout.Avenues[i];
                float halfWall = avenue.Width * 0.5f + CityPlanner.FacadeSetback - BridgeInset;
                int made = 0;
                float lastS = float.NegativeInfinity;
                foreach (var l in left)
                {
                    if (made >= BridgesPerAvenue)
                        break;
                    float s = l.Along;
                    if (s - lastS < BridgeMinSpacing || rng.NextDouble() > BridgeChance)
                        continue;
                    BuildingPlan r = null;
                    foreach (var c in right)
                    {
                        // As duas paredes precisam existir nesse ponto da avenida.
                        float reach = Mathf.Min(c.Piece.Body * c.ScaleX, l.Piece.Body * l.ScaleX) * 0.5f - 0.3f;
                        if (Mathf.Abs(c.Along - s) <= reach)
                        {
                            r = c;
                            break;
                        }
                    }
                    if (r == null)
                        continue;
                    float height = Mathf.Min(WallHeight(l.Piece), WallHeight(r.Piece)) - BridgeEaveMargin;
                    if (height < BridgeMinHeight - 0.01f)
                        continue;
                    var a = avenue.PointAt(s, -(halfWall + BridgeInset * 2f));
                    var b = avenue.PointAt(s, halfWall + BridgeInset * 2f);
                    if (Bridge(parent, new Vector3(a.X, 0f, a.Y), new Vector3(b.X, 0f, b.Y), height))
                    {
                        made++;
                        lastS = s;
                    }
                }
            }
        }

        /// <summary>Altura até onde a parede serve de apoio: o beiral dos prédios; o tanque d'água (cilindro) vale a altura dele menos o teto.</summary>
        private static float WallHeight(PieceSpec piece) => piece.Centered ? piece.Height - 1.5f : piece.Eave;

        private static bool Bridge(Transform parent, Vector3 a, Vector3 b, float height)
        {
            a.y = b.y = 0f;
            var dir = b - a;
            float length = dir.magnitude;
            if (length < 2.5f || length > BridgeMaxLength)
                return false;
            var mid = (a + b) * 0.5f;
            var rot = Quaternion.LookRotation(dir / length) * Quaternion.Euler(0f, -90f, 0f); // X local ao longo do vão
            var bridge = Model("PonteCanos", parent, mid + Vector3.up * height, rot,
                new Vector3(length / BridgeModelLength, 1f, 1f));
            if (bridge == null)
                return false;
            AddBridgeVein(bridge.transform, new Vector2(mid.x, mid.z).magnitude);
            return true;
        }

        /// <summary>
        /// Veio de cristal ao longo da ponte (filho dela: estica junto com o vão). Pulsa junto com as junções de cristal
        /// da ponte; a crista corre de fora para dentro da cidade, rumo à praça (deslocamento = -raio).
        /// </summary>
        private static void AddBridgeVein(Transform bridge, float radius)
        {
            if (crystalMat == null)
                return;
            var vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vein.name = "VeioCristal";
            vein.transform.SetParent(bridge, false);
            vein.transform.localPosition = new Vector3(0f, BridgeVeinHeight, 0f);
            vein.transform.localRotation = Quaternion.identity;
            vein.transform.localScale = new Vector3(BridgeVeinLength, BridgeVeinThickness, BridgeVeinWidth);
            Object.DestroyImmediate(vein.GetComponent<Collider>());
            var veinRenderer = vein.GetComponent<MeshRenderer>();
            veinRenderer.sharedMaterial = crystalMat;
            veinRenderer.shadowCastingMode = ShadowCastingMode.Off;
            vein.isStatic = false;

            var renderers = CrystalRenderers(bridge);
            var offsets = new float[renderers.Length];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = -radius;
            bridge.gameObject.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Veio, renderers,
                offsets, null, crystalMat);
        }

        /// <summary>Torres: cristais e luz respirando juntos, devagar, cada torre no seu tempo.</summary>
        private static void ApplyTowerPulse()
        {
            if (crystalMat == null)
                return;
            foreach (var tower in towers)
            {
                if (tower == null)
                    continue;
                tower.gameObject.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Torre,
                    CrystalRenderers(tower), null, tower.GetComponentsInChildren<Light>(true), crystalMat);
            }
        }

        /// <summary>Renderers de malha da peça (o CrystalPulse escolhe sozinho os índices em CristalArcano).</summary>
        private static Renderer[] CrystalRenderers(Transform root)
            => System.Array.ConvertAll(root.GetComponentsInChildren<MeshRenderer>(true), r => (Renderer)r);

        /// <summary>Peças cujo cristal é pulsado por um CrystalPulse na raiz (não pode ter EmissivePulse junto).</summary>
        private static bool OwnsCrystalPulse(string model)
            => model == "PonteCanos" || System.Array.IndexOf(Towers, model) >= 0;

        // ---------- Vãos da borda: caixotes e bancas (bloqueio só visual) ----------

        /// <summary>
        /// Onde nenhum prédio fecha a borda andável (cantos entre a avenida e o arco da praça, por exemplo), um monte de
        /// caixotes ou uma banca de mercado esconde o vão. Sem collider: quem fecha de verdade é a vedação invisível da arena.
        /// </summary>
        private static void BuildGapProps(Transform parent, IReadOnlyList<GapSpot> gaps)
        {
            foreach (var gap in gaps)
            {
                // A normal para fora da área andável (a tangente guarda o giro de 90°).
                var normal = new Vector3(-gap.Tangent.Y, 0f, gap.Tangent.X);
                var pos = new Vector3(gap.Position.X, 0f, gap.Position.Y);
                if (gap.Length >= StallMinGap && rng.NextDouble() < 0.5)
                {
                    // Banca com a frente para a rua, um pouco torta.
                    var rot = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, Range(-10f, 10f), 0f);
                    var stall = Model("BancaMercado", parent, pos + normal * 0.5f, rot, Vector3.one);
                    if (stall != null)
                        lightRequests.Add(new Request { Target = Find(stall.transform, "LuzQuente"), Kind = 0, Priority = Visibility(pos) * 0.7f });
                }
                else
                {
                    // Caixotes com o lado comprido ao longo da borda.
                    var rot = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, Range(-10f, 10f) + (rng.NextDouble() < 0.5 ? 180f : 0f), 0f);
                    Model("Caixotes", parent, pos, rot, Vector3.one * Range(0.8f, 1f));
                }
            }
        }

        /// <summary>Bueiros de vapor sobre o chão andável: ao longo das avenidas, nas praças menores e na borda da praça.</summary>
        private static void BuildManholes(Transform parent)
        {
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var avenue = layout.Avenues[i];
                for (int k = 0; k < ManholesPerAvenue; k++)
                {
                    float s = Range(avenue.StartS + 6f, avenue.EndS - 2f);
                    float across = Range(1.2f, 2.8f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    Manhole(parent, avenue.PointAt(s, across));
                }
                var plaza = layout.SmallPlazas[i];
                for (int k = 0; k < ManholesPerSmallPlaza; k++)
                {
                    // Longe do pé da torre e dos corredores: no quarto de círculo de trás da avenida.
                    float angle = layout.Avenues[i].AngleDeg + Range(70f, 110f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    Manhole(parent, plaza.Center + MapLayout.Polar(angle, plaza.Radius * 0.85f));
                }
            }
            for (int k = 0; k < ManholesInPlaza; k++)
            {
                // Perto da fachada da praça (fora do disco de combate), fora das aberturas das avenidas.
                for (int tries = 0; tries < 8; tries++)
                {
                    var p = MapLayout.Polar(Range(-180f, 180f), PlazaManholeRadius);
                    if (layout.Region(p).Kind != MapRegionKind.Plaza)
                        continue;
                    Manhole(parent, p);
                    break;
                }
            }
        }

        private static void Manhole(Transform parent, Float2 p)
            => Model("BueiroVapor", parent, new Vector3(p.X, 0f, p.Y), Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one);

        // ---------- Céu ----------

        private static void BuildSky(Transform parent)
        {
            // Volta grande em torno da cidade.
            Airship(parent, Circle(Polar(0f, 22f), 62f, 16, 24f, 2f, 0f), 3.2f);
            // Elipse sobre o lado norte, além das praças menores.
            var center = Polar(15f, 62f);
            var ellipse = new Vector3[10];
            for (int i = 0; i < ellipse.Length; i++)
            {
                float t = -i * Mathf.PI * 2f / ellipse.Length;
                ellipse[i] = center + new Vector3(Mathf.Cos(t) * 24f, 18f + Mathf.Sin(t * 2f) * 1.5f, Mathf.Sin(t) * 12f);
            }
            Airship(parent, ellipse, 2.4f);
            // Volta lenta em torno da torre do relógio.
            Airship(parent, Circle(clockTowerPosition, 19f, 12, 29f, 1f, 1f), 1.8f);
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
            if (System.Array.IndexOf(Towers, name) >= 0)
                towers.Add(go.transform);
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
                    float speed = Range(0.4f, 0.8f); // sorteia sempre: pular o sorteio mudaria o resto da cidade
                    if (!OwnsCrystalPulse(model))
                        t.gameObject.AddComponent<EmissivePulse>().Configure(speed, 0.55f, 1.35f);
                }
                else if (n == "CristalPulso")
                {
                    // Torres e pontes: o CrystalPulse da raiz pulsa este cristal (D-069).
                    var pulsing = Dynamic(t);
                    float speed = Range(0.3f, 0.9f); // sorteia sempre: pular o sorteio mudaria o resto da cidade
                    if (!OwnsCrystalPulse(model))
                        pulsing.AddComponent<EmissivePulse>().Configure(speed, 0.6f, 1.25f);
                }
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

        /// <summary>Poucas luzes pontuais, priorizando as perto da área andável e do lado norte e espaçadas entre si.</summary>
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

        /// <summary>
        /// 0 a 1: quanto um ponto importa para a câmera de jogo: perto da área andável (cai a zero a <see cref="LightReach"/> m)
        /// e no lado longe da câmera (norte), onde ela olha.
        /// </summary>
        private static float Visibility(Vector3 p)
        {
            float radial = Mathf.Clamp01(1f - layout.DistanceToWalkable(p.x, p.z) / LightReach);
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
            darkStoneMat = LoadMat("PedraEscura", "PisoPedra");
            crystalMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/CristalArcano.mat");
            crystalSettings = AmbienceBuilder.LoadCrystalSettings();
        }

        private static Material LoadMat(string name, string fallback)
            => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat")
               ?? AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{fallback}.mat");

        // ---------- Utilitários ----------

        private static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

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

        /// <summary>Anel plano (malha gerada, guardada na cena) com UV em metros. Raio interno 0 = disco.</summary>
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
