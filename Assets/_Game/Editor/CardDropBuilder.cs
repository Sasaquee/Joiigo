using System.IO;
using Game.Cards;
using Game.Dice;
using Game.Enemies;
using Game.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Fase 6 — carta no chão e D20. Cria os dados (DiceSettings), o prefab de rede da carta no chão (D-046:
    /// verso dourado flutuando, brilho ciano subindo), põe o CardDropService no objeto "Sessao" da cena e a tela do
    /// dado (D-047) na Canvas "UI". Chamado pelo ArenaBuilder.
    /// </summary>
    public static class CardDropBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Dice";
        private const string SettingsPath = DataFolder + "/DiceSettings.asset";
        private const string PrefabFolder = "Assets/_Game/Dice/Prefabs";
        private const string PrefabPath = PrefabFolder + "/CartaNoChao.prefab";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string BackTexturePath = "Assets/_Game/Art/Cards/verso.png";
        private const string SoftParticlePath = "Assets/_Game/Art/Ambience/particula_suave.png";
        private const string D20ModelPath = "Assets/_Game/Art/Models/Dice/D20.fbx";
        private const string CardDatabasePath = "Assets/_Game/Data/Cards/CardDatabase.asset";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        // Carta no chão: proporção da face (112 x 192) e tamanho no mundo (≈ 23 x 40 px na câmera do jogo).
        private const float CardWidth = 0.7f;
        private const float CardHeight = 1.2f;
        private const float InteractRadius = 1.1f;
        // Onde a carta aparece no fim da onda: centro da arena, um pouco para o lado do spawn (sul).
        private static readonly Vector3 DropPoint = new Vector3(0f, 0f, -3.5f);
        private static readonly Color Cyan = new Color(0.35f, 0.9f, 1f);

        /// <summary>Cria/atualiza os assets da Fase 6 (antes de abrir a cena).</summary>
        public static void BuildAssets()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);
            LoadOrCreate<DiceSettings>(SettingsPath);
            ConfigureBackTexture();
            BuildFloorCardPrefab(CreateBackMaterial(), CreateGlowMaterial());
            AssetDatabase.SaveAssets();
        }

        /// <summary>Põe o serviço do dado no objeto "Sessao" e a tela do dado na Canvas "UI" (cena aberta).</summary>
        public static void AddToScene(GameObject session, WaveSpawner spawner)
        {
            // Recarrega pelo caminho: referências de um passo anterior podem ter morrido em reimportações.
            var settings = AssetDatabase.LoadAssetAtPath<DiceSettings>(SettingsPath);
            var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardDatabasePath);
            var prefab = AssetDatabase.LoadAssetAtPath<FloorCard>(PrefabPath);

            var drop = new GameObject("PontoDaCarta").transform;
            drop.SetParent(session.transform, false);
            drop.localPosition = DropPoint;

            var service = session.AddComponent<CardDropService>();
            var so = new SerializedObject(service);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("floorCardPrefab").objectReferenceValue = prefab;
            so.FindProperty("dropPoint").objectReferenceValue = drop;
            so.ApplyModifiedPropertiesWithoutUndo();

            AddDiceUi();
        }

        private static void AddDiceUi()
        {
            Canvas canvas = null;
            for (int s = 0; s < SceneManager.sceneCount && canvas == null; s++)
                foreach (GameObject root in SceneManager.GetSceneAt(s).GetRootGameObjects())
                    if (root.name == "UI" && root.TryGetComponent(out canvas))
                        break;
            if (canvas == null)
            {
                Debug.LogError("CardDropBuilder: Canvas \"UI\" não encontrada; a tela do dado não foi criada.");
                return;
            }

            var rect = UiFactory.MakeRect("TelaDoDado", canvas.transform);
            UiFactory.Stretch(rect);
            rect.SetAsLastSibling(); // por cima da barra de cartas
            var ui = rect.gameObject.AddComponent<DiceRollUi>();
            var so = new SerializedObject(ui);
            so.FindProperty("diePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(D20ModelPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Carta no chão ----------

        private static void BuildFloorCardPrefab(Material back, Material glow)
        {
            var root = new GameObject("CartaNoChao");
            root.AddComponent<NetworkObject>();
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRadius;
            trigger.center = new Vector3(0f, CardHeight * 0.5f + 0.5f, 0f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, 1.1f, 0f);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Verso";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(visual, false);
            quad.transform.localScale = new Vector3(CardWidth, CardHeight, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = back;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("BrilhoCiano");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Cyan;
            light.range = 3.5f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;

            AddRisingGlow(root.transform, glow);

            var card = root.AddComponent<FloorCard>();
            var so = new SerializedObject(card);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            EnsureNetworkObjectHash(prefab);
            RegisterNetworkPrefab(prefab);
        }

        /// <summary>Faíscas ciano subindo devagar de um anel aos pés da carta.</summary>
        private static void AddRisingGlow(Transform parent, Material glow)
        {
            var go = new GameObject("FaiscasSubindo");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.45f;
            shape.radiusThickness = 0.2f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Cyan, 0f), new GradientColorKey(new Color(0.8f, 1f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void ConfigureBackTexture()
        {
            if (AssetImporter.GetAtPath(BackTexturePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Trilinear; // verso em alta resolução (D-059)
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        private static Material CreateBackMaterial()
        {
            string path = $"{MaterialsFolder}/CartaVerso.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackTexturePath);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", (float)CullMode.Off); // o verso aparece dos dois lados enquanto gira
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateGlowMaterial()
        {
            string path = $"{MaterialsFolder}/FaiscaCarta.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            SetFloat(mat, "_Surface", 1f);
            SetFloat(mat, "_Blend", 2f);
            SetFloat(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(mat, "_DstBlend", (float)BlendMode.One);
            SetFloat(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(mat, "_DstBlendAlpha", (float)BlendMode.One);
            SetFloat(mat, "_ZWrite", 0f);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SoftParticlePath));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Utilidades ----------

        private static void SetFloat(Material mat, string property, float value)
        {
            if (mat.HasProperty(property))
                mat.SetFloat(property, value);
        }

        private static void RegisterNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            }
            if (!list.Contains(prefab))
                list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }

        /// <summary>Prefab criado por script fica com GlobalObjectIdHash 0 até o OnValidate rodar (ver AGENTS.md).</summary>
        private static void EnsureNetworkObjectHash(GameObject prefabAsset)
        {
            var networkObject = prefabAsset.GetComponent<NetworkObject>();
            typeof(NetworkObject)
                .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            AssetDatabase.SaveAssetIfDirty(prefabAsset);
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
