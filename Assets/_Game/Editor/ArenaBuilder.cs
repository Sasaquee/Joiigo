using System.IO;
using Game.Arena;
using Game.Cameras;
using Game.Combat;
using Game.Core.Map;
using Game.Net;
using Game.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Constrói a arena de protótipo com os modelos do Blender (Tools/Blender/build_props.py) e primitivas.
    /// O mapa vem do <see cref="MapLayout"/> (passe do mapa, D-073 a D-082; números em Data/Map/MapLayoutSettings.asset):
    /// praça central, 3 avenidas, 3 praças menores e 3 bocas de rua, com chão e vedação só nas regiões andáveis
    /// (<see cref="WalkableBuilder"/>), portões fechando as bocas, spawn dos inimigos nas bocas e a NavMesh dos
    /// caminhos (<see cref="ArenaNavMeshBuilder"/>).
    /// Tudo mostra a fusão: cobre com cristal embutido, engrenagem com núcleo arcano, poste com luz arcana.
    /// Rodar de novo reconstrói a arena do zero. Batchmode: -executeMethod Game.EditorTools.ArenaBuilder.Build
    /// </summary>
    public static class ArenaBuilder
    {
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string ModelsFolder = "Assets/_Game/Art/Models";
        private const string CityModelsFolder = ModelsFolder + "/City";
        private const string DataFolder = "Assets/_Game/Data";
        private const string PlayerPrefabPath = "Assets/_Game/Player/Player.prefab";
        private const string InputActionsPath = "Assets/_Game/Player/Input/GameControls.inputactions";
        private const string RootName = "Arena";
        private const string MapLayoutSettingsPath = DataFolder + "/Map/MapLayoutSettings.asset";
        private const string SeeThroughSettingsPath = DataFolder + "/Camera/SeeThroughSettings.asset";

        // Peças da praça em metros e graus a partir de +Z (geometria de placeholder, não balanceamento). O mapa em si
        // (praça, avenidas, bocas, portões, spawns dos inimigos) vem do MapLayout.
        private const float PlatformRadius = 12f;
        private const float BoilerRadius = 23f;
        private const float PlayerSpawnAngle = 180f;
        private const float PlayerSpawnRadius = 18f;
        private const float CardAlcoveAngle = -115f;
        private const float CardAlcoveRadius = 19f;

        // Nomes iguais aos materiais do Blender, para o remapeamento na importação.
        private static readonly string[] SharedMaterialNames = { "Cobre", "Latao", "FerroEscuro", "CristalArcano", "PersonagemNeutro", "MarcadorLocal", "BrasaFornalha" };

        private static Material copper, brass, darkIron, floorStone, crystal, crystalDim, crystalOff, playerBody, grate;
        private static MapLayout layout;

        // Objetos de cena gerados que são substituídos a cada reconstrução.
        private static readonly string[] GeneratedRoots = { RootName, "Player", "NetworkManager", "Sessao", "UI", "EventSystem" };

        [MenuItem("Game/Setup/Construir Arena")]
        public static void Build()
        {
            ArenaSceneSetup.CreateArenaScene();
            PlayerSettings.runInBackground = true; // várias janelas na LAN / Multiplayer Play Mode

            CreateMaterials();
            PixelPalette.Apply();      // cores chapadas do 3D pixelado (D-039), no lugar das texturas PBR
            PixelRenderSetup.Apply();  // contorno, faixas e ampliação sem filtro
            grate = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/PisoGrade.mat") ?? darkIron;
            ConfigureModelImports();
            layout = LoadOrCreate<MapLayoutSettings>(MapLayoutSettingsPath).ToLayout(); // fonte única da geometria do mapa
            var movement = LoadOrCreate<MovementSettings>($"{DataFolder}/Player/MovementSettings.asset");
            var interaction = LoadOrCreate<InteractionSettings>($"{DataFolder}/Player/InteractionSettings.asset");
            var cameraSettings = LoadOrCreate<CameraSettings>($"{DataFolder}/Camera/CameraSettings.asset");
            var netSettings = LoadOrCreate<NetSettings>($"{DataFolder}/Net/NetSettings.asset");
            var waves = EnemyPrefabBuilder.BuildAll(crystal, crystalOff); // inimigos e ondas (D-023 a D-026)
            var combatSettings = LoadOrCreate<CombatSettings>($"{DataFolder}/Combat/CombatSettings.asset");
            var cardDb = CardAssetsBuilder.BuildAll(); // 14 cartas aprovadas (D-035)
            CardDropBuilder.BuildAssets(); // carta no chão e D20 (Fase 6)
            var playerPrefab = CreatePlayerPrefab(movement, interaction, netSettings, combatSettings, cardDb);

            var scene = EditorSceneManager.OpenScene(ArenaSceneSetup.ArenaScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(GeneratedRoots, root.name) >= 0)
                    Object.DestroyImmediate(root);
            }

            var session = new GameObject("Sessao");
            session.AddComponent<NetworkObject>();
            var matchState = session.AddComponent<MatchState>();

            var arena = new GameObject(RootName).transform;
            BuildFloor(arena);
            WalkableBuilder.BuildFence(arena, layout); // vedação invisível nas bordas da região andável (D-073)
            var spawnPoints = BuildPlayerSpawn(arena, matchState, interaction);
            for (int i = 0; i < layout.StreetCount; i++)
                BuildEnemyGate(arena, i, matchState); // portão fechando cada boca e o marcador de spawn dos inimigos (D-077, D-081)
            BuildCardAlcove(arena);
            BuildLampPosts(arena);
            BuildBoilers(arena);
            CityBuilder.Build(arena); // cidade steampunk em volta da praça e das avenidas (D-042, D-073); depois do chão e dos portões
            SetupLighting();
            AmbienceBuilder.Build(arena); // noite arcana com fornalhas (D-017); sobrescreve a luz acima

            var netSession = BuildNetworkManager(playerPrefab, netSettings, spawnPoints, matchState);
            NetworkUiBuilder.Build(netSession);
            CardUiBuilder.Build(); // tiragem (Tab) e barra de cartas (D-027, D-028)
            PlayerCombatSetup.AddSoloBootstrap(netSession, matchState); // entra direto, solo (D-018)
            var spawner = EnemyPrefabBuilder.AddWaveSpawner(matchState, waves);
            CardDropBuilder.AddToScene(session, spawner); // carta no fim da onda, D20 na tela (D-046 a D-052)
            MatchResetSetup.Apply(arena); // queda total: recomeço da partida (D-084, D-086, D-087)

            SetupCamera(cameraSettings, spawnPoints.transform);

            // Por último: a NavMesh junta os colliders de tudo que já está na cena (chão, portões, prédios, vedação, máquinas).
            ArenaNavMeshBuilder.Bake(arena, layout);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Arena construída.");
        }

        // ---------- Áreas ----------

        private static void BuildFloor(Transform parent)
        {
            var group = Group("Chao", parent);
            // Piso só visual (sem collider): cobre as frestas sob as fachadas. O chão que segura o personagem é o andável.
            WalkableBuilder.Disc("Piso", group, 0f, 0f, layout.PlazaFacadeRadius + 0.5f, -0.03f, floorStone, collider: false);
            WalkableBuilder.BuildGround(group, layout, floorStone);

            var center = Group("CentroCombate", group);
            Marker(center, ArenaMarkerKind.CombatCenter, PlatformRadius);
            // Anel arcano embutido na borda de metal da plataforma.
            Disc("AnelArcano", center, new Vector3(0f, 0.01f, 0f), PlatformRadius + 0.35f, 0.04f, crystalDim);
            Disc("Plataforma", center, new Vector3(0f, 0.03f, 0f), PlatformRadius, 0.06f, grate);
            Disc("NucleoArcano", center, new Vector3(0f, 0.065f, 0f), 1.4f, 0.02f, crystal);
            // Trilhos de cobre que levam a energia do núcleo até cada avenida (o ambiente continua cada um até o portão).
            foreach (var avenue in layout.Avenues)
            {
                var rail = Box("TrilhoCobre", center, Polar(avenue.AngleDeg, PlatformRadius * 0.55f) + Vector3.up * 0.07f,
                    new Vector3(0.25f, 0.02f, PlatformRadius * 0.9f), copper, collider: false);
                rail.transform.rotation = Quaternion.Euler(0f, avenue.AngleDeg, 0f);
            }
        }

        private static PlayerSpawnPoints BuildPlayerSpawn(Transform parent, MatchState session, InteractionSettings interaction)
        {
            var spawn = Group("SpawnJogadores", parent);
            spawn.position = Polar(PlayerSpawnAngle, PlayerSpawnRadius);
            spawn.rotation = Quaternion.LookRotation(-spawn.position.normalized); // +Z local aponta para o centro
            Marker(spawn, ArenaMarkerKind.PlayerSpawn, 3f);
            CheckWalkable("SpawnJogadores", spawn.position, 3f);
            Disc("PlataformaLatao", spawn, new Vector3(0f, 0.02f, 0f), 3f, 0.04f, brass);
            Disc("RunaCentral", spawn, new Vector3(0f, 0.045f, 0f), 0.8f, 0.01f, crystalDim);

            // 4 vagas (§7.2: até 4 jogadores), todas olhando para o centro da arena.
            var slots = new Transform[4];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = Group($"Vaga{i + 1}", spawn);
                slot.localPosition = Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0f, 1.6f);
                slot.localRotation = Quaternion.identity;
                slots[i] = slot;
            }
            var points = spawn.gameObject.AddComponent<PlayerSpawnPoints>();
            points.Configure(slots);

            BuildStartLever(spawn, session, interaction);
            return points;
        }

        /// <summary>Alavanca-máquina da largada, no fundo da plataforma de spawn (D-013).</summary>
        private static void BuildStartLever(Transform spawn, MatchState session, InteractionSettings interaction)
        {
            var lever = new GameObject("AlavancaLargada").transform;
            lever.SetParent(spawn, false);
            lever.localPosition = new Vector3(0f, 0f, -2.4f);
            lever.localRotation = Quaternion.identity;
            lever.gameObject.AddComponent<NetworkObject>();
            CheckWalkable("AlavancaLargada", lever.position, 0.7f);
            AddBoxCollider(lever, new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 0.8f));

            var baseModel = Model("AlavancaBase", lever, Vector3.zero, Quaternion.identity, Vector3.one, isStatic: false);
            var pivot = Group("Pivo", lever);
            pivot.localPosition = new Vector3(0f, 1.1f, 0f);
            Model("AlavancaBraco", pivot, Vector3.zero, Quaternion.identity, Vector3.one, isStatic: false);

            lever.gameObject.AddComponent<StartLever>().Configure(session, interaction, pivot,
                baseModel.GetComponentsInChildren<Renderer>(), crystalOff, crystal);
        }

        /// <summary>
        /// Portão-máquina que fecha o fim da boca de rua <paramref name="index"/>, virado para o centro (D-081), e o marcador
        /// de spawn dos inimigos no meio da boca (D-077). Os nomes PortaoMaquina1..N (filhos diretos da Arena) são
        /// procurados pelo AmbienceBuilder; SpawnInimigo1..N, pelo WaveSpawner.
        /// </summary>
        private static void BuildEnemyGate(Transform parent, int index, MatchState session)
        {
            var mapGate = layout.Gates[index];
            var gate = Group($"PortaoMaquina{index + 1}", parent);
            gate.position = new Vector3(mapGate.Position.X, 0f, mapGate.Position.Y);
            gate.rotation = Quaternion.Euler(0f, mapGate.YawDeg, 0f); // +Z local aponta para o centro

            Model("PortaoMaquina", gate, Vector3.zero, Quaternion.identity, Vector3.one);
            AddBoxCollider(gate, new Vector3(0f, 2.2f, 0f), new Vector3(5.6f, 4.4f, 1.6f));

            // Engrenagem com núcleo de cristal: a mesma peça move e canaliza.
            var gear = Model("Engrenagem", gate, new Vector3(0f, 2f, 0.92f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, isStatic: false);
            // Um portão engasga: a instalação funciona só em parte.
            var spinner = gear.AddComponent<Spinner>();
            spinner.Configure(Vector3.up, 40f, index == 1 ? 0.45f : 0f);
            spinner.enabled = false;

            // Parado e apagado até a largada (D-013). Os cristais do portão não são estáticos para poder trocar material.
            foreach (var t in gate.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = false;
            gate.gameObject.AddComponent<GateActivation>().Configure(session, spinner,
                gate.GetComponentsInChildren<Renderer>(), crystal, crystalOff);

            var spawn = Group($"SpawnInimigo{index + 1}", parent);
            var spawnPoint = layout.EnemySpawns[index];
            spawn.position = new Vector3(spawnPoint.X, 0f, spawnPoint.Y);
            Marker(spawn, ArenaMarkerKind.EnemySpawn, layout.Params.EnemySpawnRadius);
            CheckWalkable("SpawnInimigo", spawn.position, layout.Params.EnemySpawnRadius);
        }

        private static void BuildCardAlcove(Transform parent)
        {
            var alcove = Group("AlcovaCartas", parent);
            alcove.position = Polar(CardAlcoveAngle, CardAlcoveRadius);
            alcove.rotation = Quaternion.LookRotation(-alcove.position.normalized); // +Z local aponta para o centro
            Marker(alcove, ArenaMarkerKind.CardTestArea, 4f);
            CheckWalkable("AlcovaCartas", alcove.position, 4f);

            Disc("Piso", alcove, new Vector3(0f, 0.02f, 0f), 4f, 0.04f, darkIron);
            Disc("CirculoArcano", alcove, new Vector3(0f, 0.045f, 0f), 3.2f, 0.01f, crystalDim);

            // Mesa de leitura no fundo da alcova: maquinário de latão com lente de cristal e engrenagem.
            var table = Group("MesaLeitura", alcove);
            table.localPosition = new Vector3(0f, 0f, -2.8f);
            Box("Base", table, new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 0.9f), brass);
            Cylinder("Lente", table, new Vector3(-0.35f, 1.04f, 0f), new Vector3(0.7f, 0.04f, 0.7f), crystal, collider: false);
            var gear = Model("Engrenagem", table, new Vector3(0.55f, 1.06f, 0f), Quaternion.identity, Vector3.one * 0.3f, isStatic: false);
            gear.AddComponent<Spinner>().Configure(Vector3.up, 25f, 0f);
        }

        private static void BuildLampPosts(Transform parent)
        {
            var group = Group("PostesArcanos", parent);
            for (int i = 0; i < 6; i++)
            {
                float angle = 30f + i * 60f;
                var post = Group($"Poste{i + 1}", group);
                post.position = Polar(angle, PlatformRadius + 3.5f);

                Model("PosteArcano", post, Vector3.zero, Quaternion.identity, Vector3.one);
                AddCapsuleCollider(post, 0.35f, 3.6f);
                CheckWalkable("PosteArcano", post.position, 0.35f);

                var lightGo = new GameObject("LuzArcana");
                lightGo.transform.SetParent(post, false);
                lightGo.transform.localPosition = new Vector3(0f, 3.3f, 0f);
                var lamp = lightGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(0.35f, 0.9f, 1f);
                lamp.range = 12f;
                lamp.intensity = 10f;
                if (i == 4)
                    lightGo.AddComponent<LightFlicker>();
            }
        }

        private static void BuildBoilers(Transform parent)
        {
            var group = Group("Caldeiras", parent);
            foreach (float angle in new[] { 120f, -150f })
            {
                var boiler = Group("Caldeira", group);
                boiler.position = Polar(angle, BoilerRadius);
                Model("Caldeira", boiler, Vector3.zero, Quaternion.identity, Vector3.one);
                AddCapsuleCollider(boiler, 1.3f, 5f);
                CheckWalkable("Caldeira", boiler.position, 1.3f);
            }
        }

        private static void SetupLighting()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional)
                    continue;
                l.color = new Color(1f, 0.86f, 0.7f);
                l.intensity = 0.8f;
                l.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
                break;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.26f);
        }

        private static NetSession BuildNetworkManager(GameObject playerPrefab, NetSettings settings,
            PlayerSpawnPoints spawnPoints, MatchState matchState)
        {
            var go = new GameObject("NetworkManager");
            var manager = go.AddComponent<NetworkManager>();
            go.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = go.GetComponent<UnityTransport>(),
                PlayerPrefab = playerPrefab,
                ConnectionApproval = true,
                TickRate = 30
            };

            // Lista padrão do Netcode (Assets/DefaultNetworkPrefabs.asset), gerenciada pelo pacote.
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, "Assets/DefaultNetworkPrefabs.asset");
            }
            if (!list.Contains(playerPrefab))
                list.Add(new NetworkPrefab { Prefab = playerPrefab });
            EditorUtility.SetDirty(list);
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);

            var netSession = go.AddComponent<NetSession>();
            var so = new SerializedObject(netSession);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("spawnPoints").objectReferenceValue = spawnPoints;
            so.FindProperty("matchState").objectReferenceValue = matchState;
            so.ApplyModifiedPropertiesWithoutUndo();
            return netSession;
        }

        private static void SetupCamera(CameraSettings settings, Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<CameraFollow>();
            var pixel = cam.GetComponent<PixelCamera>();
            if (pixel == null)
                pixel = cam.gameObject.AddComponent<PixelCamera>(); // resolução do mundo e contorno (D-039, D-056)
            pixel.Settings = PixelRenderSetup.LoadQualitySettings();
            EditorUtility.SetDirty(pixel);
            // Recorte dos prédios que tapam jogador ou inimigo (D-076, D-079); depois do PixelCamera. O asset é
            // carregado pelo caminho aqui, perto do uso, porque reimportações anteriores deixam referências mortas.
            var seeThroughSettings = LoadOrCreate<SeeThroughSettings>(SeeThroughSettingsPath);
            var seeThrough = cam.GetComponent<SeeThroughDriver>();
            if (seeThrough == null)
                seeThrough = cam.gameObject.AddComponent<SeeThroughDriver>();
            seeThrough.Configure(seeThroughSettings);
            EditorUtility.SetDirty(seeThrough);
            follow.Settings = settings;
            follow.Target = target;
            follow.Apply(target.position);
            EditorUtility.SetDirty(follow);
        }

        // ---------- Player ----------

        private static GameObject CreatePlayerPrefab(MovementSettings movement, InteractionSettings interaction,
            NetSettings netSettings, CombatSettings combatSettings, Game.Cards.CardDatabase cardDb)
        {
            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            var controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);

            var motor = root.AddComponent<PlayerMotor>();
            motor.Settings = movement;

            var reader = root.AddComponent<PlayerInputReader>();
            var so = new SerializedObject(reader);
            so.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Andarilho encapuzado (D-040). A máscara mostra para onde ele olha (D-005).
            CharacterModelSetup.CreateBody(root.transform);

            // Anel visível só para o dono (D-010). O NetworkPlayer liga no spawn.
            var marker = Model("AnelMarcador", root.transform, Vector3.zero, Quaternion.identity, Vector3.one, isStatic: false);
            marker.name = "MarcadorLocal";
            marker.SetActive(false);

            var netPlayer = root.AddComponent<NetworkPlayer>();
            var nso = new SerializedObject(netPlayer);
            nso.FindProperty("netSettings").objectReferenceValue = netSettings;
            nso.FindProperty("interaction").objectReferenceValue = interaction;
            nso.FindProperty("localMarker").objectReferenceValue = marker;
            nso.ApplyModifiedPropertiesWithoutUndo();

            PlayerCombatSetup.ConfigurePlayerPrefab(root, combatSettings);
            CardAssetsBuilder.ConfigurePlayerPrefab(root, cardDb);
            AuraBuilder.ConfigurePlayerPrefab(root, marker.transform); // aura (Fase 7) e anel local por fora dela (D-066)
            ReviveSetup.Apply(root); // levantar aliado segurando E (D-083)
            BlessingSetup.Apply(root); // bênção de dano do 20 no coop (D-085)

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            EnsureNetworkObjectHash(prefab);
            return prefab;
        }

        /// <summary>
        /// O Netcode só gera o GlobalObjectIdHash no OnValidate do editor. Prefab criado por script
        /// fica com hash 0 e não spawna; por isso o OnValidate é chamado aqui.
        /// </summary>
        private static void EnsureNetworkObjectHash(GameObject prefabAsset)
        {
            var networkObject = prefabAsset.GetComponent<NetworkObject>();
            typeof(NetworkObject)
                .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            AssetDatabase.SaveAssetIfDirty(prefabAsset);
        }

        // ---------- Materiais, modelos e dados ----------

        private static void CreateMaterials()
        {
            copper = Mat("Cobre", new Color(0.72f, 0.42f, 0.25f), 0.9f, 0.55f);
            brass = Mat("Latao", new Color(0.78f, 0.62f, 0.3f), 0.9f, 0.5f);
            darkIron = Mat("FerroEscuro", new Color(0.18f, 0.18f, 0.2f), 0.7f, 0.35f);
            floorStone = Mat("PisoPedra", new Color(0.28f, 0.27f, 0.26f), 0f, 0.2f);
            crystal = Mat("CristalArcano", new Color(0.3f, 0.9f, 0.95f), 0f, 0.9f, new Color(0.2f, 1.4f, 1.6f));
            crystalDim = Mat("CristalArcanoFraco", new Color(0.08f, 0.22f, 0.25f), 0f, 0.8f, new Color(0.02f, 0.2f, 0.26f));
            crystalOff = Mat("CristalApagado", new Color(0.08f, 0.14f, 0.16f), 0f, 0.85f, new Color(0f, 0.03f, 0.04f));
            playerBody = Mat("PersonagemNeutro", new Color(0.55f, 0.56f, 0.6f), 0.1f, 0.4f);
            Mat("MarcadorLocal", new Color(0.92f, 0.88f, 0.78f), 0f, 0.5f);
            // Criado aqui para existir antes da importação dos modelos (remapeamento pelo nome).
            Mat("BrasaFornalha", new Color(0.35f, 0.08f, 0.02f), 0f, 0.2f, new Color(4f, 1.2f, 0.2f));
            AssetDatabase.SaveAssets();
        }

        private static Material Mat(string name, Color baseColor, float metallic, float smoothness, Color? emission = null)
        {
            EnsureFolder(MaterialsFolder);
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Importa os FBX do Blender sem animação, câmera ou luz e usa os materiais do projeto.</summary>
        private static void ConfigureModelImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(CityModelsFolder + "/"))
                    continue; // os modelos da cidade são reimportados pelo CityBuilder
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    continue;

                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.useFileScale = true;
                importer.globalScale = 1f;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (string name in System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Concat(SharedMaterialNames, PixelPalette.Names)))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                }
                importer.SaveAndReimport();
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---------- Construção ----------

        private static Vector3 Polar(float angleDegrees, float radius)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Avisa se uma peça de gameplay (spawn, alcova, caldeira, poste) caiu fora da região andável do mapa.</summary>
        private static void CheckWalkable(string what, Vector3 position, float radius)
        {
            if (!layout.IsWalkable(position.x, position.z, radius))
                Debug.LogWarning($"ArenaBuilder: {what} em ({position.x:0.#}, {position.z:0.#}) com raio {radius:0.#} m não cabe na região andável do mapa; ajuste a posição.");
        }

        private static void Marker(Transform t, ArenaMarkerKind kind, float radius)
        {
            t.gameObject.AddComponent<ArenaMarker>().Configure(kind, radius);
        }

        private static GameObject Model(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale, bool isStatic = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{name}.fbx");
            if (asset == null)
                throw new FileNotFoundException($"Modelo {name}.fbx não encontrado. Rode Tools/Blender/build_props.py.");

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = isStatic;
            return go;
        }

        private static void AddBoxCollider(Transform t, Vector3 center, Vector3 size)
        {
            var col = t.gameObject.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
        }

        private static void AddCapsuleCollider(Transform t, float radius, float height)
        {
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = height;
            col.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
            bool collider = true, bool isStatic = true)
            => Primitive(PrimitiveType.Cube, name, parent, localPos, scale, mat, collider, isStatic);

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = true)
            => Primitive(PrimitiveType.Cylinder, name, parent, localPos, scale, mat, collider, true);

        /// <summary>Disco plano no chão (sem collider: o piso já segura o personagem).</summary>
        private static GameObject Disc(string name, Transform parent, Vector3 localPos, float radius, float thickness, Material mat)
            => Primitive(PrimitiveType.Cylinder, name, parent, localPos, new Vector3(radius * 2f, thickness * 0.5f, radius * 2f), mat, false, true);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
            Material mat, bool collider, bool isStatic)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = isStatic;
            return go;
        }
    }
}
