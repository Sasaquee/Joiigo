using System.IO;
using Game.Combat;
using Game.Enemies;
using Game.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Gera os três inimigos provisórios (definições, prefabs e projétil) e as ondas, chamado pelo ArenaBuilder.
    /// Usa o modelo do Blender (Art/Models/Enemy*.fbx) se existir; senão monta um placeholder de primitivas
    /// com a mesma convenção de nomes: Corpo, Parte_*, Cristal, Arma.
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Enemies";
        private const string PrefabFolder = "Assets/_Game/Enemies/Prefabs";
        private const string ModelsFolder = "Assets/_Game/Art/Models";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string NavigationSettingsPath = DataFolder + "/EnemyNavigationSettings.asset";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string SpawnerName = "Inimigos";

        private enum Kind
        {
            Automato,
            Drone,
            Constructo
        }

        public static WaveSettings BuildAll(Material crystalLit, Material crystalOff)
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);

            EnsureNavigationSettings();
            var particleMaterial = CreateParticleMaterial();
            var projectile = BuildProjectilePrefab(crystalLit, particleMaterial);

            var automato = BuildDefinition(Kind.Automato);
            var drone = BuildDefinition(Kind.Drone);
            var constructo = BuildDefinition(Kind.Constructo);

            BuildEnemyPrefab(Kind.Automato, automato, crystalLit, crystalOff, particleMaterial, null);
            BuildEnemyPrefab(Kind.Drone, drone, crystalLit, crystalOff, particleMaterial, projectile);
            BuildEnemyPrefab(Kind.Constructo, constructo, crystalLit, crystalOff, particleMaterial, null);

            var waves = BuildWaveSettings(automato, drone, constructo);
            AssetDatabase.SaveAssets();
            return waves;
        }

        /// <summary>Cria o objeto "Inimigos" na cena aberta (substitui o anterior, se houver).</summary>
        public static WaveSpawner AddWaveSpawner(MatchState match, WaveSettings waves)
        {
            foreach (var old in Object.FindObjectsByType<WaveSpawner>(FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            // O objeto devolvido pelo BuildAll pode ter sido recarregado por reimportações no meio do build
            // (vira referência morta e serializa como vazia). Recarrega pelo caminho.
            waves = AssetDatabase.LoadAssetAtPath<WaveSettings>($"{DataFolder}/WaveSettings.asset");

            var go = new GameObject(SpawnerName);
            var spawner = go.AddComponent<WaveSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("settings").objectReferenceValue = waves;
            so.FindProperty("match").objectReferenceValue = match;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        // ---------- Definições e ondas (números provisórios) ----------

        private static EnemyDefinition BuildDefinition(Kind kind)
        {
            string id = kind.ToString().ToLowerInvariant();
            var def = LoadOrCreate<EnemyDefinition>($"{DataFolder}/{id}.asset");
            def.id = id;

            switch (kind)
            {
                case Kind.Automato:
                    def.maxHealth = 60f; def.bodyRadius = 0.55f;
                    def.resistances = new ResistanceValues { mechanical = 0f, arcane = 0f };
                    def.moveSpeed = 3.5f; def.turnSpeed = 540f;
                    def.attackRange = 1.6f; def.minRange = 0f; def.windupTime = 0.6f; def.recoverTime = 0.8f;
                    def.damage = 12f; def.arcaneFraction = 0.1f; def.meleeHalfAngle = 50f;
                    def.usesProjectile = false;
                    def.debrisLifetime = 4f;
                    break;
                case Kind.Drone:
                    def.maxHealth = 35f; def.bodyRadius = 0.5f;
                    def.resistances = new ResistanceValues { mechanical = 0f, arcane = 0.2f };
                    def.moveSpeed = 3f; def.turnSpeed = 360f;
                    def.attackRange = 9f; def.minRange = 5f; def.windupTime = 0.8f; def.recoverTime = 1.2f;
                    def.damage = 10f; def.arcaneFraction = 0.8f; def.meleeHalfAngle = 50f;
                    def.usesProjectile = true; def.projectileSpeed = 7f; def.projectileRadius = 0.35f; def.projectileLifetime = 3f;
                    def.debrisLifetime = 4f;
                    break;
                default:
                    def.maxHealth = 140f; def.bodyRadius = 0.9f;
                    def.resistances = new ResistanceValues { mechanical = 0.1f, arcane = 0.75f };
                    def.moveSpeed = 2f; def.turnSpeed = 240f;
                    def.attackRange = 2.3f; def.minRange = 0f; def.windupTime = 1f; def.recoverTime = 1.2f;
                    def.damage = 22f; def.arcaneFraction = 0.5f; def.meleeHalfAngle = 60f;
                    def.usesProjectile = false;
                    def.debrisLifetime = 5f;
                    break;
            }

            EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>
        /// Navegação dos inimigos (D-077, D-080). Cria o asset com os valores padrão da classe só na primeira vez:
        /// depois, o que o dono ajustar no asset é mantido.
        /// </summary>
        private static void EnsureNavigationSettings()
        {
            var settings = LoadOrCreate<EnemyNavigationSettings>(NavigationSettingsPath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static WaveSettings BuildWaveSettings(EnemyDefinition automato, EnemyDefinition drone, EnemyDefinition constructo)
        {
            var waves = LoadOrCreate<WaveSettings>($"{DataFolder}/WaveSettings.asset");
            waves.enemyTypes = new[] { automato, drone, constructo };
            waves.waves.Clear();
            foreach (var counts in new[] { new[] { 3, 0, 0 }, new[] { 3, 2, 0 }, new[] { 2, 2, 1 }, new[] { 4, 2, 1 } })
                waves.waves.Add(new WaveSettings.Wave { counts = counts });
            waves.firstWaveDelay = 3f;
            waves.pauseBetweenWaves = 6f;
            EditorUtility.SetDirty(waves);
            return waves;
        }

        // ---------- Prefabs ----------

        private static GameObject BuildEnemyPrefab(Kind kind, EnemyDefinition def, Material crystalLit, Material crystalOff,
            Material particleMaterial, GameObject projectilePrefab)
        {
            var root = new GameObject(kind.ToString());
            root.AddComponent<NetworkObject>();

            var sync = root.AddComponent<NetworkTransform>();
            sync.SyncPositionX = sync.SyncPositionY = sync.SyncPositionZ = true;
            sync.SyncRotAngleX = false;
            sync.SyncRotAngleY = true;
            sync.SyncRotAngleZ = false;
            sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
            sync.Interpolate = true;

            float height = Mathf.Max(1.8f, def.bodyRadius * 2f);
            var controller = root.AddComponent<CharacterController>();
            controller.radius = def.bodyRadius;
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            var health = root.AddComponent<NetworkHealth>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("radius").floatValue = def.bodyRadius;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var enemy = root.AddComponent<EnemyController>();
            var so = new SerializedObject(enemy);
            so.FindProperty("definition").objectReferenceValue = def;
            so.FindProperty("crystalOff").objectReferenceValue = crystalOff;
            so.FindProperty("particleMaterial").objectReferenceValue = particleMaterial;
            so.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Caminho entre prédios (só no host). Recarrega o asset pelo caminho: o objeto criado antes pode ter virado referência morta.
            var follower = root.AddComponent<EnemyPathFollower>();
            var followerSo = new SerializedObject(follower);
            followerSo.FindProperty("settings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyNavigationSettings>(NavigationSettingsPath);
            followerSo.ApplyModifiedPropertiesWithoutUndo();

            BuildVisual(kind, root.transform, crystalLit);

            string path = $"{PrefabFolder}/{kind}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            EnsureNetworkObjectHash(prefab);
            RegisterNetworkPrefab(prefab);

            def.prefab = prefab;
            EditorUtility.SetDirty(def);
            return prefab;
        }

        private static GameObject BuildProjectilePrefab(Material crystalLit, Material particleMaterial)
        {
            var root = new GameObject("ProjetilDrone");
            root.AddComponent<NetworkObject>();

            var sync = root.AddComponent<NetworkTransform>();
            sync.SyncPositionX = sync.SyncPositionY = sync.SyncPositionZ = true;
            sync.SyncRotAngleX = sync.SyncRotAngleY = sync.SyncRotAngleZ = false;
            sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
            sync.Interpolate = true;

            root.AddComponent<EnemyProjectile>();

            // Orbe cristalino, sem collider: o acerto é calculado pelo host.
            var orb = Primitive(PrimitiveType.Sphere, "Orbe", root.transform, Vector3.zero, Vector3.one * 0.7f, crystalLit);
            orb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.75f, 1f);
            light.range = 4f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;

            var trailGo = new GameObject("Rastro");
            trailGo.transform.SetParent(root.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = 0.4f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.sharedMaterial = particleMaterial;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.55f, 0.4f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ProjetilDrone.prefab");
            Object.DestroyImmediate(root);
            EnsureNetworkObjectHash(prefab);
            RegisterNetworkPrefab(prefab);
            return prefab;
        }

        // ---------- Visual ----------

        private static void BuildVisual(Kind kind, Transform root, Material crystalLit)
        {
            var model = LoadModel(kind);
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                instance.name = "Modelo";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                FaceWeaponForward(instance.transform);
                return;
            }

            var modelo = new GameObject("Modelo").transform;
            modelo.SetParent(root, false);
            BuildPlaceholder(kind, modelo, crystalLit);
        }

        private static GameObject LoadModel(Kind kind)
        {
            string[] names = kind switch
            {
                Kind.Automato => new[] { "EnemyAutomato" },
                Kind.Drone => new[] { "EnemyDrone" },
                _ => new[] { "EnemyConstructo", "EnemyConstruto" }
            };
            foreach (string name in names)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{name}.fbx");
                if (asset != null)
                    return asset;
            }
            return null;
        }

        /// <summary>O FBX pode sair com a frente em +Z ou -Z. Se a Arma está atrás da origem, gira o modelo 180° para ela ficar em +Z.</summary>
        private static void FaceWeaponForward(Transform modelo)
        {
            Transform weapon = null;
            foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
                if (t != modelo && t.name == "Arma")
                {
                    weapon = t;
                    break;
                }
            if (weapon == null)
                return;

            var renderers = weapon.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            if (modelo.InverseTransformPoint(bounds.center).z < 0f)
                modelo.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static void BuildPlaceholder(Kind kind, Transform modelo, Material crystalLit)
        {
            var iron = LoadMaterial("FerroEscuro");
            var brass = LoadMaterial("Latao");

            switch (kind)
            {
                case Kind.Automato:
                    Primitive(PrimitiveType.Cube, "Corpo", modelo, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.6f, 0.55f), iron);
                    Primitive(PrimitiveType.Sphere, "Parte_Cabeca", modelo, new Vector3(0f, 1.25f, 0.05f), Vector3.one * 0.4f, brass);
                    Primitive(PrimitiveType.Cylinder, "Parte_PernaEsq", modelo, new Vector3(-0.2f, 0.25f, 0f), new Vector3(0.2f, 0.25f, 0.2f), iron);
                    Primitive(PrimitiveType.Cylinder, "Parte_PernaDir", modelo, new Vector3(0.2f, 0.25f, 0f), new Vector3(0.2f, 0.25f, 0.2f), iron);
                    Primitive(PrimitiveType.Sphere, "Cristal", modelo, new Vector3(0f, 0.8f, 0.3f), Vector3.one * 0.22f, crystalLit);
                    Primitive(PrimitiveType.Cube, "Arma", modelo, new Vector3(0.45f, 0.75f, 0.35f), new Vector3(0.14f, 0.14f, 0.5f), brass);
                    break;

                case Kind.Drone:
                    Primitive(PrimitiveType.Sphere, "Corpo", modelo, new Vector3(0f, 1.4f, 0f), Vector3.one * 0.7f, iron);
                    Primitive(PrimitiveType.Cylinder, "Parte_Anel", modelo, new Vector3(0f, 1.4f, 0f), new Vector3(1.1f, 0.03f, 1.1f), brass);
                    Primitive(PrimitiveType.Cube, "Parte_RotorEsq", modelo, new Vector3(-0.5f, 1.7f, 0f), new Vector3(0.7f, 0.03f, 0.12f), brass);
                    Primitive(PrimitiveType.Cube, "Parte_RotorDir", modelo, new Vector3(0.5f, 1.7f, 0f), new Vector3(0.7f, 0.03f, 0.12f), brass);
                    Primitive(PrimitiveType.Sphere, "Cristal", modelo, new Vector3(0f, 1.15f, 0.1f), Vector3.one * 0.25f, crystalLit);
                    var cannon = Primitive(PrimitiveType.Cylinder, "Arma", modelo, new Vector3(0f, 1.4f, 0.55f), new Vector3(0.14f, 0.3f, 0.14f), brass);
                    cannon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;

                default:
                    Primitive(PrimitiveType.Cube, "Corpo", modelo, new Vector3(0f, 1.2f, 0f), new Vector3(1.3f, 1.3f, 1f), iron);
                    Primitive(PrimitiveType.Cube, "Parte_Peitoral", modelo, new Vector3(0f, 1.4f, 0.5f), new Vector3(1f, 0.7f, 0.2f), brass);
                    Primitive(PrimitiveType.Sphere, "Cristal", modelo, new Vector3(0f, 1.4f, 0.65f), Vector3.one * 0.4f, crystalLit);
                    Primitive(PrimitiveType.Cube, "Parte_Cabeca", modelo, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.5f, iron);
                    Primitive(PrimitiveType.Sphere, "Parte_OmbroEsq", modelo, new Vector3(-0.85f, 1.8f, 0f), Vector3.one * 0.45f, brass);
                    Primitive(PrimitiveType.Sphere, "Parte_OmbroDir", modelo, new Vector3(0.85f, 1.8f, 0f), Vector3.one * 0.45f, brass);
                    Primitive(PrimitiveType.Cylinder, "Parte_PernaEsq", modelo, new Vector3(-0.4f, 0.5f, 0f), new Vector3(0.4f, 0.5f, 0.4f), iron);
                    Primitive(PrimitiveType.Cylinder, "Parte_PernaDir", modelo, new Vector3(0.4f, 0.5f, 0f), new Vector3(0.4f, 0.5f, 0.4f), iron);
                    Primitive(PrimitiveType.Cube, "Arma", modelo, new Vector3(0.95f, 1.2f, 0.5f), new Vector3(0.4f, 0.4f, 1f), brass);
                    break;
            }
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material LoadMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");

        /// <summary>Material aditivo e sem luz das faíscas, do vapor e do rastro do projétil.</summary>
        private static Material CreateParticleMaterial()
        {
            string path = $"{MaterialsFolder}/FaiscaVapor.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    return null;
                EnsureFolder(MaterialsFolder);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            SetFloat(mat, "_Surface", 1f);
            SetFloat(mat, "_Blend", 2f);
            SetFloat(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(mat, "_DstBlend", (float)BlendMode.One);
            SetFloat(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(mat, "_DstBlendAlpha", (float)BlendMode.One);
            SetFloat(mat, "_ZWrite", 0f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetFloat(Material mat, string property, float value)
        {
            if (mat.HasProperty(property))
                mat.SetFloat(property, value);
        }

        // ---------- Utilidades ----------

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
    }
}
