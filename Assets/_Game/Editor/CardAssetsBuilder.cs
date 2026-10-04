using System.Collections.Generic;
using System.IO;
using Game.Arena;
using Game.Cards;
using Game.Core.Cards;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Gera os assets das 14 cartas do protótipo (Docs/Design/cartas-prototipo.md): configurações, efeitos,
    /// cartas, banco de cartas, biblioteca de visuais e o prefab de rede da mina. Rodar de novo atualiza os
    /// assets no lugar (os números provisórios vivem aqui só como valor inicial dos ScriptableObjects).
    /// Também liga o PlayerCards ao prefab do jogador (ConfigurePlayerPrefab).
    /// </summary>
    public static class CardAssetsBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Cards";
        private const string EffectsFolder = DataFolder + "/Effects";
        private const string SettingsPath = DataFolder + "/CardsSettings.asset";
        private const string LibraryPath = DataFolder + "/CardVisualLibrary.asset";
        private const string DatabasePath = DataFolder + "/CardDatabase.asset";
        private const string PrefabFolder = "Assets/_Game/Cards/Prefabs";
        private const string MinePrefabPath = PrefabFolder + "/Mina.prefab";
        private const string CardArtFolder = "Assets/_Game/Art/Cards";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string AmbienceFolder = "Assets/_Game/Art/Ambience";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Game/Setup/Construir Cartas")]
        public static void Build() => BuildAll();

        public static CardDatabase BuildAll()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(EffectsFolder);
            EnsureFolder(PrefabFolder);

            BuildSettings();
            BuildVisualLibrary();
            BuildMinePrefab();
            // Recarrega pelo caminho: o prefab recém-salvo pode virar referência morta depois de reimportações.
            var minePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MinePrefabPath);

            // ---------- Skills ----------
            var pistao = Card("pistao_runico", "Pistão Rúnico", Arcana.Minor, Suit.Swords, 1, CardKind.Skill, Rarity.Common,
                new[] { "engrenagem" }, false, 20f, 4f, 0f,
                Effect<DashEffect>("pistao_runico", "dash", e =>
                {
                    e.distance = 5f; e.damage = 18f; e.arcaneFraction = 0.3f; e.width = 1.4f;
                }));

            var sopro = Card("sopro_caldeira", "Sopro de Caldeira", Arcana.Minor, Suit.Swords, 3, CardKind.Skill, Rarity.Common,
                new[] { "vapor" }, false, 30f, 6f, 0f,
                Effect<ConeDamageEffect>("sopro_caldeira", "cone", e =>
                {
                    e.damagePerSecond = 30f; e.duration = 1f; e.range = 4.5f; e.halfAngle = 30f;
                    e.arcaneFraction = 0.5f; e.tickInterval = 0.1f; e.visualInterval = 0.25f;
                }));

            var arco = Card("arco_voltaico", "Arco Voltaico", Arcana.Minor, Suit.Swords, 5, CardKind.Skill, Rarity.Uncommon,
                new[] { "faisca", "cristal" }, false, 25f, 5f, 0f,
                Effect<ChainProjectileEffect>("arco_voltaico", "chain", e =>
                {
                    e.damage = 22f; e.arcaneFraction = 0.8f; e.jumps = 3; e.jumpRange = 5f;
                    e.firstRange = 10f; e.firstHalfAngle = 30f;
                }));

            var mina = Card("mina_engrenagem", "Mina de Engrenagem", Arcana.Minor, Suit.Swords, 7, CardKind.Skill, Rarity.Uncommon,
                new[] { "engrenagem", "cristal" }, false, 25f, 8f, 0f,
                Effect<PlaceMineEffect>("mina_engrenagem", "mina", e =>
                {
                    e.minePrefab = minePrefab; e.damage = 45f; e.arcaneFraction = 0.4f; e.areaRadius = 2.5f;
                    e.triggerRadius = 1.3f; e.fuse = 10f; e.armDelay = 0.3f; e.placeDistance = 1.2f;
                }));

            var broquel = Card("broquel_cantante", "Broquel Cantante", Arcana.Minor, Suit.Swords, 11, CardKind.Skill, Rarity.Uncommon,
                new[] { "engrenagem", "cristal" }, false, 20f, 7f, 0f,
                Effect<ShieldEffect>("broquel_cantante", "escudo", e =>
                {
                    e.duration = 2f; e.pulseDamage = 15f; e.arcaneFraction = 0.7f; e.radius = 3f;
                    e.blockReach = 1.5f; e.blockHalfAngle = 65f;
                }));

            var lamina = Card("lamina_sedenta", "Lâmina Sedenta", Arcana.Minor, Suit.Swords, 8, CardKind.Skill, Rarity.Uncommon,
                new[] { "faisca" }, true, 0f, 3f, 8f,
                Effect<ArcSlashEffect>("lamina_sedenta", "corte", e =>
                {
                    e.damage = 40f; e.arcaneFraction = 0.5f; e.range = 3f; e.halfAngle = 80f;
                }));

            var chamine = Card("chamine_partida", "A Chaminé Partida", Arcana.Major, Suit.None, 16, CardKind.Skill, Rarity.Rare,
                new[] { "vapor" }, false, 50f, 20f, 0f,
                Effect<AreaBurstEffect>("chamine_partida", "explosao", e =>
                {
                    e.damage = 60f; e.arcaneFraction = 0.5f; e.radius = 5f;
                }));

            // ---------- Consumíveis ----------
            var tonico = Card("tonico_oleo_luz", "Tônico de Óleo e Luz", Arcana.Minor, Suit.Cups, 2, CardKind.Item, Rarity.Common,
                new[] { "vapor" }, false, 0f, 0f, 0f,
                Effect<HealEffect>("tonico_oleo_luz", "cura", e =>
                {
                    e.heal = 35f; e.energy = 40f;
                }));

            var granada = Card("granada_cristal", "Granada de Cristal Rachado", Arcana.Minor, Suit.Cups, 4, CardKind.Item, Rarity.Uncommon,
                new[] { "cristal", "faisca" }, false, 0f, 0f, 0f,
                Effect<ThrowAreaEffect>("granada_cristal", "arremesso", e =>
                {
                    e.damage = 40f; e.arcaneFraction = 0.6f; e.radius = 3f; e.maxRange = 10f; e.flightTime = 0.6f;
                }));

            // ---------- Passivas e equipamentos ----------
            var caldeira = Card("caldeira_interna", "Caldeira Interna", Arcana.Minor, Suit.Wands, 1, CardKind.Passive, Rarity.Common,
                new[] { "vapor" }, false, 0f, 0f, 0f, null,
                Mod(ModifierKind.EnergyOnHitBonus, 0.5f));

            var mola = Card("mola_recuo", "Mola de Recuo", Arcana.Minor, Suit.Wands, 6, CardKind.Passive, Rarity.Common,
                new[] { "engrenagem" }, false, 0f, 0f, 0f, null,
                Mod(ModifierKind.HurtNextHitBonus, 0.6f));

            var manopla = Card("manopla_pistonada", "Manopla Pistonada", Arcana.Minor, Suit.Pentacles, 3, CardKind.Equipment, Rarity.Common,
                new[] { "engrenagem" }, false, 0f, 0f, 0f, null,
                Mod(ModifierKind.BasicRange, 0.6f), Mod(ModifierKind.BasicDamage, 6f));

            var lente = Card("lente_prismatica", "Lente Prismática", Arcana.Minor, Suit.Pentacles, 13, CardKind.Equipment, Rarity.Uncommon,
                new[] { "cristal" }, false, 0f, 0f, 0f, null,
                Mod(ModifierKind.BasicArcaneShift, 0.35f));

            var artifice = Card("artifice", "O Artífice", Arcana.Major, Suit.None, 1, CardKind.Passive, Rarity.Unique,
                new[] { "engrenagem", "cristal" }, false, 0f, 0f, 0f, null,
                Mod(ModifierKind.CooldownChange, -0.25f));

            // ---------- Banco de cartas (só acrescente no fim: a ordem são os ids da rede) ----------
            var database = LoadOrCreate<CardDatabase>(DatabasePath);
            database.cards = new List<CardData>
            {
                pistao, sopro, arco, mina, broquel, tonico, granada, caldeira, mola, manopla, lente, lamina, chamine, artifice
            };
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
        }

        /// <summary>
        /// Adiciona PlayerCards (e PlayerShield) ao prefab do jogador antes de salvá-lo. Chamado pelo ArenaBuilder
        /// depois de PlayerCombatSetup.ConfigurePlayerPrefab.
        /// </summary>
        public static void ConfigurePlayerPrefab(GameObject root, CardDatabase db)
        {
            // Recarrega pelo caminho: objetos de passos anteriores do build podem ter virado referência morta.
            var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (database == null)
                database = db;
            var settings = AssetDatabase.LoadAssetAtPath<CardsSettings>(SettingsPath);
            var library = AssetDatabase.LoadAssetAtPath<CardVisualLibrary>(LibraryPath);

            var cards = root.GetComponent<PlayerCards>();
            if (cards == null)
                cards = root.AddComponent<PlayerCards>();
            var so = new SerializedObject(cards);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("visualLibrary").objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<PlayerShield>() == null)
                root.AddComponent<PlayerShield>();
        }

        // ---------- Configurações e biblioteca ----------

        private static void BuildSettings()
        {
            var settings = LoadOrCreate<CardsSettings>(SettingsPath);
            settings.maxEnergy = 100f;
            settings.regenPerSecond = 4f;
            settings.energyPerHit = 8f;
            settings.startEnergyFraction = 1f;
            settings.hurtBonusWindow = 3f;
            EditorUtility.SetDirty(settings);
        }

        private static void BuildVisualLibrary()
        {
            var library = LoadOrCreate<CardVisualLibrary>(LibraryPath);
            library.steam = LoadMaterial($"{AmbienceFolder}/ParticulaVapor.mat");
            library.ember = LoadMaterial($"{AmbienceFolder}/ParticulaBrasa.mat");
            library.arcane = LoadMaterial($"{AmbienceFolder}/ParticulaPoeira.mat");
            library.brass = LoadMaterial($"{MaterialsFolder}/Latao.mat");
            library.crystal = LoadMaterial($"{MaterialsFolder}/CristalArcano.mat");
            EditorUtility.SetDirty(library);
        }

        // ---------- Cartas e efeitos ----------

        private static ModifierEntry Mod(ModifierKind kind, float value) => new ModifierEntry { kind = kind, value = value };

        private static CardEffect Effect<T>(string cardId, string suffix, System.Action<T> configure) where T : CardEffect
        {
            string path = $"{EffectsFolder}/{cardId}_{suffix}.asset";
            var effect = LoadOrCreate<T>(path);
            configure(effect);
            EditorUtility.SetDirty(effect);
            AssetDatabase.SaveAssetIfDirty(effect);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static CardData Card(string id, string displayName, Arcana arcana, Suit suit, int number, CardKind kind,
            Rarity rarity, string[] tags, bool cursed, float energyCost, float cooldown, float healthCost,
            CardEffect effect, params ModifierEntry[] modifiers)
        {
            string path = $"{DataFolder}/{id}.asset";
            var card = LoadOrCreate<CardData>(path);
            card.id = id;
            card.displayName = displayName;
            card.arcana = arcana;
            card.suit = suit;
            card.number = number;
            card.kind = arcana == Arcana.Minor ? CardRules.KindOf(suit) : kind;
            card.tags = tags;
            card.rarity = rarity;
            card.cursed = cursed;
            card.energyCost = energyCost;
            card.cooldown = cooldown;
            card.healthCostOnUse = healthCost;
            card.effects = new List<CardEffect>();
            if (effect != null)
                card.effects.Add(effect);
            card.modifiers = new List<ModifierEntry>(modifiers);
            card.face = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CardArtFolder}/{id}.png");
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssetIfDirty(card);
            return AssetDatabase.LoadAssetAtPath<CardData>(path);
        }

        // ---------- Prefab da mina ----------

        private static void BuildMinePrefab()
        {
            var brass = LoadMaterial($"{MaterialsFolder}/Latao.mat");
            var iron = LoadMaterial($"{MaterialsFolder}/FerroEscuro.mat");
            var crystal = LoadMaterial($"{MaterialsFolder}/CristalArcano.mat");

            var root = new GameObject("Mina");
            root.AddComponent<NetworkObject>();
            root.AddComponent<Mine>();

            Primitive(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.9f, 0.03f, 0.9f), brass);
            var gear = Primitive(PrimitiveType.Cylinder, "Engrenagem", root.transform, new Vector3(0f, 0.075f, 0f), new Vector3(0.62f, 0.02f, 0.62f), iron);
            for (int i = 0; i < 6; i++)
            {
                var tooth = Primitive(PrimitiveType.Cube, $"Dente{i + 1}", gear.transform, Vector3.zero, new Vector3(0.18f, 1.2f, 0.12f), iron);
                float angle = i * 60f;
                tooth.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.55f);
                tooth.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            gear.AddComponent<Spinner>().Configure(Vector3.up, 35f, 0f);

            Primitive(PrimitiveType.Sphere, "Cristal", root.transform, new Vector3(0f, 0.17f, 0f), Vector3.one * 0.24f, crystal);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.95f, 1f);
            light.range = 3.5f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MinePrefabPath);
            Object.DestroyImmediate(root);
            EnsureNetworkObjectHash(prefab);
            RegisterNetworkPrefab(prefab);
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

        // ---------- Utilidades ----------

        private static Material LoadMaterial(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

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
