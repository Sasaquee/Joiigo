using System.IO;
using Game.Arena;
using Game.Arena.Life;
using Game.Core.Map;
using Game.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Ambientação da arena (D-016, D-017): noite arcana escura, cristais ciano, fornalhas laranja e vapor.
    /// Cria o Volume de pós-processamento, luz ambiente, neblina, fornalhas e partículas sob "Ambiente".
    /// As partículas só guardam parâmetros na cena (AmbienceParticles); o ParticleSystem nasce em jogo.
    /// Chamar depois que a geometria da arena existe e depois de ArenaBuilder.SetupLighting(),
    /// senão o SetupLighting sobrescreve o ambiente e a luz direcional.
    /// </summary>
    public static class AmbienceBuilder
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        private const string AmbienceFolder = "Assets/_Game/Art/Ambience";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string VolumePath = AmbienceFolder + "/ArenaVolume.asset";
        private const string SoftParticlePath = AmbienceFolder + "/particula_suave.png";
        private const int SoftParticleSize = 64;

        // Pós-processamento
        private const float BloomThreshold = 1f;
        private const float BloomIntensity = 0.8f;
        private const float BloomScatter = 0.7f;
        private const float Saturation = -10f;
        private const float PostExposure = 1.0f;
        private const float VignetteIntensity = 0.3f;
        private const float VignetteSmoothness = 0.4f;
        private const float WhiteBalanceTemperature = -3f;
        private static readonly Vector4 CoolShadows = new Vector4(0.96f, 0.99f, 1.05f, 0f);

        // Ambiente, neblina e câmera
        private static readonly Color AmbientSky = new Color(0.32f, 0.36f, 0.50f);
        private static readonly Color AmbientEquator = new Color(0.30f, 0.28f, 0.29f);
        private static readonly Color AmbientGround = new Color(0.22f, 0.18f, 0.15f);
        private static readonly Color FogColor = new Color(0.10f, 0.12f, 0.19f);
        private const float FogDensity = 0.0045f;

        // Luar frio
        private static readonly Color MoonColor = new Color(0.66f, 0.72f, 0.95f);
        private const float MoonIntensity = 1.5f;
        private static readonly Vector3 MoonRotation = new Vector3(50f, -30f, 0f);

        // Fornalhas: ângulo (graus a partir de +Z) livre entre portões, caldeiras, alcova e spawn.
        private static readonly float[] FurnaceAngles = { 75f, 150f, -78f, 190f };
        private const float FurnaceRadius = 24.5f;
        private static readonly Color FurnaceLightColor = new Color(1f, 0.45f, 0.15f);
        private const float FurnaceLightRange = 12f;
        private const float FurnaceLightIntensity = 9f;
        private static readonly Color EmberEmissionColor = new Color(4f, 1.2f, 0.2f);

        // Vapor
        private static readonly float[] BoilerAngles = { 120f, -150f };
        private const float BoilerRadius = 23f;
        private const float BoilerTopHeight = 4.8f;
        // x = ângulo, y = raio
        private static readonly Vector2[] VentPositions = { new Vector2(105f, 19f), new Vector2(-85f, 17f), new Vector2(60f, 19f) };

        // Poeira arcana perto da plataforma central
        private static readonly Vector3 DustArea = new Vector3(20f, 3f, 20f);

        // ---------- Cristal vivo (D-069): veios, trilhos, lampiões, pulso e poeira mágica ----------
        // Números balanceáveis em Data/Ambience/CrystalAmbienceSettings.asset; aqui só a forma e o lugar das peças.

        public const string CrystalSettingsPath = "Assets/_Game/Data/Ambience/CrystalAmbienceSettings.asset";
        private const string MagicDustMaterialPath = AmbienceFolder + "/ParticulaPoeiraMagica.mat";

        // O mapa (praça, avenidas, praças menores, portões) vem do MapLayout (Data/Map/MapLayoutSettings.asset).
        // O muro baixo saiu no passe do mapa (D-073): não há mais veio de muro nem rua do anel.
        private const string MapLayoutSettingsPath = "Assets/_Game/Data/Map/MapLayoutSettings.asset";

        // Geometria das peças copiada do ArenaBuilder e de build_props.py (só leitura; se lá mudar, mude aqui).
        private const float BoilerBodyRadius = 1.2f;       // Caldeira.fbx: cobre r1,2; faixa de cristal 1,975–2,425 m
        private const float BoilerBandCenter = 2.2f;
        private const float BoilerBandHalf = 0.225f;
        private const float BoilerVeinBottom = 0.86f;      // logo acima do anel de latão de 0,8 m
        private const float BoilerVeinTop = 3.34f;         // logo abaixo do anel de latão de 3,4 m
        private const float GateFront = 0.8f;              // PortaoMaquina.fbx: carcaça 4,5 x 4 x 1,6 m, frente para a praça
        private const float GateLintelFront = 0.65f;       // viga de cobre 5,2 x 0,4 x 1,3 m em 4,2 m
        private const float GateRingRadius = 1.45f;        // anel de latão em volta da engrenagem, centro em 2 m
        private const float GateColumnInner = 2.1f;        // colunas de cobre em x ±2,45, raio 0,35, gemas em 1,2 e 3,0 m

        // Veios: fendas de cristal largas o bastante para ler a ~30 m (≥ 4 px em 1080p), finas para não virar parede de luz.
        private const float VeinThickness = 0.04f;
        private const float BoilerVeinWidth = 0.14f;
        private const int BoilerVeinCount = 4;             // entre as hastes de latão (que estão a cada 45°)
        private const float GateVeinWidth = 0.12f;
        private const float GateVeinX = 1.8f;

        // Trilhos de cristal no chão (do núcleo da praça ao portão de cada rua). A câmera olha para o chão: é o cristal
        // que mais aparece. Base de cobre com o veio de cristal em trechos por cima (cada trecho pulsa sozinho).
        private const string CopperMaterialPath = MaterialsFolder + "/Cobre.mat";
        private const float TrailBaseWidth = 0.36f;        // cobre; o veio (trailVeinWidth) fica no meio
        private const float TrailBaseHeight = 0.09f;       // chão em y = 0; 1 cm acima do TrilhoCobre do ArenaBuilder (topo em 0,08 m)
        private const float TrailVeinThickness = 0.02f;    // sobre o cobre: topo em 0,11 m
        private const float TrailSegmentGap = 0.06f;       // vão entre trechos: o cobre aparece e o trilho lê como peças
        private const float TrailGateGap = 0.1f;           // o trilho para um pouco antes da frente do portão

        // Lampiões de cristal nas avenidas: o LampiaoRua da cidade, com colisão pequena e, em alguns, uma luz fraca.
        private const string LampModelPath = "Assets/_Game/Art/Models/City/LampiaoRua.fbx";
        private const float LampBaseRadius = 0.34f;        // base de pedra do LampiaoRua.fbx
        private const float LampColliderHeight = 3.6f;     // poste até a lanterna
        private const float LampPlazaMargin = 1.5f;        // o primeiro lampião fica 1,5 m além da quina da fachada da praça
        private const float LampEndMargin = 0.5f;          // e o último a 0,5 m do fim da avenida
        private const string LampLightMarker = "LuzCristal"; // marcador da lanterna no modelo
        private static readonly Color LampLightColor = new Color(0.35f, 0.9f, 1f); // igual aos lampiões do CityBuilder

        // Poeira mágica: baixa e fraca, nas avenidas e nas praças menores, nunca sobre o disco de combate.
        // Os parâmetros do sistema de partículas (tamanhos, vida, cores) ficam em AmbienceParticles.
        private const float DustFadeNear = 4f;             // some perto da câmera (m)
        private const float DustFadeFar = 9f;

        private static Material ironMat, brassMat, furnaceMouthMat, emberMat, steamMat, dustMat;
        private static Material crystalLitMat, crystalOffMat, magicDustMat, copperMat;
        private static CrystalAmbienceSettings crystalSettings;

        public static void Build(Transform arenaRoot)
        {
            EnsureFolder(AmbienceFolder);

            var root = new GameObject("Ambiente").transform;
            root.SetParent(arenaRoot, false);

            LoadMaterials();
            BuildVolume(root);
            SetupRenderSettings();
            SetupMoonlight();
            SetupCamera();

            var furnaces = new GameObject("Fornalhas").transform;
            furnaces.SetParent(root, false);
            foreach (float angle in FurnaceAngles)
                BuildFurnace(furnaces, angle);

            var steam = new GameObject("Vapor").transform;
            steam.SetParent(root, false);
            // O vapor sobe do topo de cada caldeira da cena (a posição vem do ArenaBuilder, não de uma cópia aqui).
            var boilers = arenaRoot.Find("Caldeiras");
            if (boilers != null && boilers.childCount > 0)
            {
                foreach (Transform boiler in boilers)
                    BuildSteam(steam, "VaporCaldeira", boiler.position + Vector3.up * BoilerTopHeight, 4f, 1.6f, 3.2f, 0.9f);
            }
            else
            {
                foreach (float angle in BoilerAngles)
                    BuildSteam(steam, "VaporCaldeira", Polar(angle, BoilerRadius) + Vector3.up * BoilerTopHeight, 4f, 1.6f, 3.2f, 0.9f);
            }
            foreach (var v in VentPositions)
                BuildVent(steam, Polar(v.x, v.y));

            BuildArcaneDust(root);
            BuildLivingCrystal(arenaRoot, root); // D-069: veios, trilhos, lampiões, pulso dos cristais e poeira mágica

            AssetDatabase.SaveAssets();
        }

        // ---------- Volume e render ----------

        private static void BuildVolume(Transform parent)
        {
            // Recria o asset do zero, para não acumular componentes órfãos.
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath) != null)
                AssetDatabase.DeleteAsset(VolumePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);

            var colorAdjustments = AddComponent<ColorAdjustments>(profile);
            colorAdjustments.saturation.Override(Saturation);
            colorAdjustments.postExposure.Override(PostExposure);

            var vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);

            var whiteBalance = AddComponent<WhiteBalance>(profile);
            whiteBalance.temperature.Override(WhiteBalanceTemperature);

            var smh = AddComponent<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(CoolShadows);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("VolumeGlobal");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        private static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void SetupRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
        }

        private static void SetupMoonlight()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional)
                    continue;
                l.color = MoonColor;
                l.intensity = MoonIntensity;
                l.shadows = LightShadows.Soft;
                l.transform.rotation = Quaternion.Euler(MoonRotation);
                break;
            }
        }

        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            AddReflectionProbe();
            EditorUtility.SetDirty(cam);
        }

        // ---------- Fornalhas ----------

        private static void BuildFurnace(Transform parent, float angle)
        {
            // +Z local aponta para o centro; a boca da fornalha fica virada para dentro da arena.
            var furnace = new GameObject("Fornalha").transform;
            furnace.SetParent(parent, false);
            furnace.position = Polar(angle, FurnaceRadius);
            furnace.rotation = Quaternion.LookRotation(-furnace.position.normalized);

            // Modelo do Blender (Tools/Blender/build_props.py → Fornalha.fbx). Sem ele, placeholder de primitivas.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/Fornalha.fbx");
            if (model != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, furnace);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    t.gameObject.isStatic = true;
                var col = furnace.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.2f, 0f);
                col.size = new Vector3(2.3f, 2.4f, 1.7f);
                // O cristal alimentador do topo respira (D-069); a brasa da boca não é tocada.
                PulseMachine(furnace.gameObject, go.transform);
            }
            else
            {
                Primitive(PrimitiveType.Cube, "Corpo", furnace, new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 2.4f, 1.6f), ironMat, true);
                Primitive(PrimitiveType.Cylinder, "Chamine", furnace, new Vector3(0f, 3.2f, -0.3f), new Vector3(0.8f, 1.6f, 0.8f), ironMat, true);
                Primitive(PrimitiveType.Cube, "Moldura", furnace, new Vector3(0f, 0.9f, 0.82f), new Vector3(1.4f, 1.1f, 0.12f), brassMat, false);
                Primitive(PrimitiveType.Cube, "BocaFornalha", furnace, new Vector3(0f, 0.9f, 0.9f), new Vector3(1f, 0.8f, 0.05f), furnaceMouthMat, false);
            }

            var lightGo = new GameObject("LuzFornalha");
            lightGo.transform.SetParent(furnace, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 1.6f);
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = FurnaceLightColor;
            lamp.range = FurnaceLightRange;
            lamp.intensity = FurnaceLightIntensity;
            lamp.shadows = LightShadows.None;
            lightGo.AddComponent<FireFlicker>();

            BuildEmbers(furnace);
        }

        private static void BuildEmbers(Transform furnace)
        {
            // O sistema nasce em jogo (AmbienceParticles): o cone aponta para cima (+Y mundo) pelo giro da peça.
            var go = new GameObject("Brasas");
            go.transform.SetParent(furnace, false);
            go.transform.localPosition = new Vector3(0f, 1.5f, 0.95f);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            go.AddComponent<AmbienceParticles>().ConfigureEmbers(emberMat);
        }

        // ---------- Vapor ----------

        private static void BuildVent(Transform parent, Vector3 position)
        {
            var vent = new GameObject("Respiradouro").transform;
            vent.SetParent(parent, false);
            vent.position = position;
            Primitive(PrimitiveType.Cube, "Grade", vent, new Vector3(0f, 0.01f, 0f), new Vector3(1.2f, 0.04f, 1.2f), ironMat, false);
            BuildSteam(vent, "VaporChao", position + Vector3.up * 0.1f, 2.5f, 1f, 2f, 1.1f);
        }

        /// <summary>Vapor suave: partículas grandes, lentas e de alfa baixo, cinza azulado (montado em jogo).</summary>
        private static void BuildSteam(Transform parent, string name, Vector3 worldPosition, float rate,
            float sizeMin, float sizeMax, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            go.AddComponent<AmbienceParticles>().ConfigureSteam(steamMat, rate, sizeMin, sizeMax, speed);
        }

        // ---------- Poeira arcana ----------

        private static void BuildArcaneDust(Transform parent)
        {
            var go = new GameObject("PoeiraArcana");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            go.AddComponent<AmbienceParticles>().ConfigureArcaneDust(dustMat, DustArea);
        }

        // ---------- Cristal vivo (D-069) ----------

        /// <summary>Dados do cristal vivo; cria o asset com os valores padrão se ainda não existir.</summary>
        public static CrystalAmbienceSettings LoadCrystalSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CrystalAmbienceSettings>(CrystalSettingsPath);
            if (settings != null)
                return settings;
            EnsureFolder(Path.GetDirectoryName(CrystalSettingsPath)!.Replace('\\', '/'));
            settings = ScriptableObject.CreateInstance<CrystalAmbienceSettings>();
            AssetDatabase.CreateAsset(settings, CrystalSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// Magia lida a olho (Pilar 4): o latão e o cobre da arena ganham veios de cristal na mesma peça.
        /// A luz sai da faixa de cristal de cada caldeira e sobe e desce por ela; um trilho de cristal corre pelo chão
        /// do núcleo da praça até o portão de cada rua e os portões ganham um circuito (os dois apagados até a largada,
        /// como os cristais dos portões, D-013 e D-081); lampiões de cristal marcam as avenidas; fornalhas, postes e
        /// lampiões respiram; e uma poeira mágica fraca paira nas avenidas e praças menores, atrás do combate.
        /// Luzes novas só nos lampiões (no máximo CrystalAmbienceSettings.lampMaxLights); o resto é emissão.
        /// </summary>
        private static void BuildLivingCrystal(Transform arenaRoot, Transform ambience)
        {
            if (crystalLitMat == null)
            {
                Debug.LogWarning("AmbienceBuilder: CristalArcano.mat não encontrado; cristal vivo (D-069) não construído.");
                return;
            }
            var layout = LoadMapLayout();
            var group = new GameObject("CristalVivo").transform;
            group.SetParent(ambience, false);

            BuildBoilerVeins(arenaRoot, group);
            BuildGateVeins(arenaRoot, group);
            BuildCrystalTrails(group, layout);
            BuildAvenueLamps(group, layout);
            var posts = arenaRoot.Find("PostesArcanos");
            if (posts != null)
            {
                foreach (Transform post in posts)
                    PulseMachine(post.gameObject, post);
            }
            BuildMagicDust(group, layout);
        }

        /// <summary>Geometria do mapa a partir de Data/Map/MapLayoutSettings.asset (criado aqui se o ArenaBuilder ainda não o criou).</summary>
        private static MapLayout LoadMapLayout()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MapLayoutSettings>(MapLayoutSettingsPath);
            if (settings == null)
            {
                EnsureFolder(Path.GetDirectoryName(MapLayoutSettingsPath)!.Replace('\\', '/'));
                settings = ScriptableObject.CreateInstance<MapLayoutSettings>();
                AssetDatabase.CreateAsset(settings, MapLayoutSettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings.ToLayout();
        }

        /// <summary>Cristal da peça respira devagar, cada uma no seu tempo (sem luz junto: fica perto do combate).</summary>
        private static void PulseMachine(GameObject host, Transform model)
        {
            if (crystalLitMat == null)
                return;
            host.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Maquina,
                MeshRenderers(model), null, null, crystalLitMat);
        }

        private static void BuildBoilerVeins(Transform arenaRoot, Transform group)
        {
            var renderers = new System.Collections.Generic.List<Renderer>();
            var offsets = new System.Collections.Generic.List<float>();

            // Caldeiras: a faixa de cristal do modelo é o coração (deslocamento 0); quatro veios sobem e descem dela.
            var boilers = arenaRoot.Find("Caldeiras");
            if (boilers != null)
            {
                foreach (Transform boiler in boilers)
                {
                    foreach (var r in MeshRenderers(boiler))
                    {
                        renderers.Add(r);
                        offsets.Add(0f);
                    }
                    for (int k = 0; k < BoilerVeinCount; k++)
                    {
                        float a = (22.5f + k * 360f / BoilerVeinCount) * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        float bandBottom = BoilerBandCenter - BoilerBandHalf;
                        float bandTop = BoilerBandCenter + BoilerBandHalf;
                        // Dois trechos embaixo e dois em cima, para a crista correr para longe da faixa.
                        foreach (var span in new[]
                                 {
                                     new Vector2(BoilerVeinBottom, (BoilerVeinBottom + bandBottom) * 0.5f),
                                     new Vector2((BoilerVeinBottom + bandBottom) * 0.5f, bandBottom),
                                     new Vector2(bandTop, (bandTop + BoilerVeinTop) * 0.5f),
                                     new Vector2((bandTop + BoilerVeinTop) * 0.5f, BoilerVeinTop)
                                 })
                        {
                            float height = span.y - span.x - 0.02f;
                            float y = (span.x + span.y) * 0.5f;
                            renderers.Add(Vein(boiler, dir * (BoilerBodyRadius + 0.015f) + Vector3.up * y,
                                Quaternion.LookRotation(dir), new Vector3(BoilerVeinWidth, height, VeinThickness * 1.25f),
                                crystalLitMat));
                            offsets.Add(Mathf.Abs(y - BoilerBandCenter));
                        }
                    }
                }
            }

            if (renderers.Count == 0)
                return;
            var go = new GameObject("VeiosCaldeiras");
            go.transform.SetParent(group, false);
            go.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Veio, renderers.ToArray(),
                offsets.ToArray(), null, crystalLitMat);
        }

        /// <summary>
        /// Circuito de cristal na frente de cada portão-máquina: um veio na viga de cobre, dois descendo pela carcaça
        /// e quatro ligando as gemas das colunas ao anel da engrenagem. Apagado até a largada (D-013). Os portões
        /// ficam no fim de cada boca de rua (D-081) e são achados pelo GateActivation, onde quer que estejam. O
        /// deslocamento começa na distância do portão ao núcleo: a crista que corre pelo trilho entra no circuito.
        /// </summary>
        private static void BuildGateVeins(Transform arenaRoot, Transform group)
        {
            var session = Object.FindFirstObjectByType<MatchState>();
            // Sem sessão na cena não há largada para esperar: o circuito nasce aceso.
            var startMat = session != null && crystalOffMat != null ? crystalOffMat : crystalLitMat;
            float z = GateFront + VeinThickness * 0.25f;
            foreach (var activation in arenaRoot.GetComponentsInChildren<GateActivation>(true))
            {
                var gate = activation.transform;
                var renderers = new System.Collections.Generic.List<Renderer>();
                var offsets = new System.Collections.Generic.List<float>();
                var heart = new Vector2(0f, 2f); // centro da engrenagem: a crista sai dela
                float entry = Mathf.Max(0f, Flat(gate.position).magnitude - GateFront); // distância da frente do portão ao núcleo

                void Add(Vector3 pos, Vector3 size)
                {
                    renderers.Add(Vein(gate, pos, Quaternion.identity, size, startMat));
                    offsets.Add(entry + Vector2.Distance(new Vector2(pos.x, pos.y), heart));
                }

                // Viga de cobre do alto (a frente dela fica recuada em relação à carcaça).
                Add(new Vector3(0f, 4.2f, GateLintelFront + VeinThickness * 0.25f), new Vector3(4f, GateVeinWidth, VeinThickness));
                foreach (float side in new[] { -1f, 1f })
                {
                    // Descida pela carcaça, em dois trechos.
                    Add(new Vector3(side * GateVeinX, 1.2f, z), new Vector3(GateVeinWidth, 1.78f, VeinThickness));
                    Add(new Vector3(side * GateVeinX, 3.0f, z), new Vector3(GateVeinWidth, 1.78f, VeinThickness));
                    // Das gemas da coluna (1,2 e 3,0 m) até o anel de latão.
                    foreach (float y in new[] { 1.2f, 3.0f })
                    {
                        float dy = y - 2f;
                        float ringX = Mathf.Sqrt(GateRingRadius * GateRingRadius - dy * dy) + 0.06f;
                        float length = GateColumnInner - ringX;
                        // Um pouco mais à frente que a descida, para não brigar com ela no cruzamento.
                        Add(new Vector3(side * (ringX + length * 0.5f), y, z + 0.012f), new Vector3(length, GateVeinWidth, VeinThickness));
                    }
                }

                var go = new GameObject($"Veios{gate.name}");
                go.transform.SetParent(group, false);
                go.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Veio, renderers.ToArray(),
                    offsets.ToArray(), null, crystalLitMat, crystalOffMat, session);
            }
        }

        /// <summary>
        /// Trilho de cristal de cada rua (D-069, D-081): o TrilhoCobre do núcleo continua pelo meio da avenida e da boca
        /// de rua até a frente do portão. Base de cobre com o veio de cristal em trechos por cima; o deslocamento de
        /// cada trecho é a distância até o núcleo, então a crista corre da praça para os portões. Apagado até a
        /// largada, como o circuito do portão. Fica no chão: a câmera de jogo o vê em quase toda posição do jogador.
        /// </summary>
        private static void BuildCrystalTrails(Transform group, MapLayout layout)
        {
            var s = crystalSettings;
            var session = Object.FindFirstObjectByType<MatchState>();
            var startMat = session != null && crystalOffMat != null ? crystalOffMat : crystalLitMat;
            var trails = new GameObject("TrilhosCristal").transform;
            trails.SetParent(group, false);

            float end = layout.Params.GateS - GateFront - TrailGateGap;
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var axis = layout.Avenues[i].Axis;
                var dir = new Vector3(axis.X, 0f, axis.Y);
                float start = Mathf.Min(s.trailStartS, end - 1f);
                float length = end - start;
                var rotation = Quaternion.LookRotation(dir);

                var street = new GameObject($"Trilho{i + 1}").transform;
                street.SetParent(trails, false);
                if (copperMat != null)
                {
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rail.name = "TrilhoCobre";
                    rail.transform.SetParent(street, false);
                    rail.transform.localPosition = dir * ((start + end) * 0.5f) + Vector3.up * (TrailBaseHeight * 0.5f);
                    rail.transform.localRotation = rotation;
                    rail.transform.localScale = new Vector3(TrailBaseWidth, TrailBaseHeight, length);
                    Object.DestroyImmediate(rail.GetComponent<Collider>());
                    rail.GetComponent<MeshRenderer>().sharedMaterial = copperMat;
                    rail.isStatic = true;
                }

                int count = Mathf.Max(1, Mathf.RoundToInt(length / s.trailSegmentLength));
                float segment = length / count;
                var renderers = new Renderer[count];
                var offsets = new float[count];
                for (int k = 0; k < count; k++)
                {
                    float along = start + (k + 0.5f) * segment;
                    renderers[k] = Vein(street, dir * along + Vector3.up * (TrailBaseHeight + TrailVeinThickness * 0.5f), rotation,
                        new Vector3(s.trailVeinWidth, TrailVeinThickness, Mathf.Max(0.1f, segment - TrailSegmentGap)), startMat);
                    offsets[k] = along; // distância ao núcleo (na origem)
                }

                var go = new GameObject($"VeiosTrilho{i + 1}");
                go.transform.SetParent(street, false);
                go.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Veio, renderers, offsets, null,
                    crystalLitMat, crystalOffMat, session);
            }
        }

        /// <summary>
        /// Lampiões de cristal ao longo das avenidas (D-069): o LampiaoRua da cidade, um a cada lampSpacing m em cada
        /// lado, intercalados, na borda da avenida (a lampFacadeGap da fachada; o corredor andável fica livre). Colisão
        /// de cápsula pequena na camada Cenario e o cristal respirando. Só um lampião em lampLightEvery recebe uma luz
        /// fraca, sem sombra, até lampMaxLights no mapa todo.
        /// </summary>
        private static void BuildAvenueLamps(Transform group, MapLayout layout)
        {
            var s = crystalSettings;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LampModelPath);
            if (model == null)
            {
                Debug.LogWarning($"AmbienceBuilder: {LampModelPath} não encontrado; lampiões de cristal não construídos.");
                return;
            }
            var lamps = new GameObject("LampioesCristal").transform;
            lamps.SetParent(group, false);

            int number = 0, withLight = 0;
            for (int i = 0; i < layout.StreetCount; i++)
            {
                var avenue = layout.Avenues[i];
                float across = avenue.Width * 0.5f - s.lampFacadeGap - LampBaseRadius;
                if (across <= 0f)
                    continue;
                float facade = layout.PlazaFacadeRadius;
                float first = Mathf.Sqrt(Mathf.Max(0f, facade * facade - across * across)) + LampPlazaMargin;
                float last = avenue.EndS - LampEndMargin;

                // x = distância s ao longo da avenida, y = lado (-1 esquerda, +1 direita); o lado direito vem meio espaçamento depois.
                var spots = new System.Collections.Generic.List<Vector2>();
                for (int k = 0; k < 2; k++)
                {
                    for (float along = first + k * s.lampSpacing * 0.5f; along <= last + 0.001f; along += s.lampSpacing)
                        spots.Add(new Vector2(along, k == 0 ? -1f : 1f));
                }
                spots.Sort((a, b) => a.x.CompareTo(b.x));

                for (int n = 0; n < spots.Count; n++)
                {
                    bool light = n % s.lampLightEvery == 0 && withLight < s.lampMaxLights;
                    var spot = avenue.PointAt(spots[n].x, spots[n].y * across);
                    var side = avenue.Rect.Side;
                    var toAxis = new Vector3(-side.X, 0f, -side.Y) * spots[n].y; // o braço da lanterna aponta para o meio da rua
                    BuildLamp(lamps, model, $"Lampiao{++number}", new Vector3(spot.X, 0f, spot.Y), toAxis, light);
                    if (light)
                        withLight++;
                }
            }
        }

        private static void BuildLamp(Transform parent, GameObject model, string name, Vector3 position, Vector3 facing, bool withLight)
        {
            var lamp = new GameObject(name);
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = position;
            lamp.transform.rotation = Quaternion.LookRotation(facing); // +Z local = braço da lanterna
            lamp.layer = MapLayers.Cenario; // a colisão entra na NavMesh e no recorte dos prédios como o resto do cenário

            var capsule = lamp.AddComponent<CapsuleCollider>();
            capsule.radius = LampBaseRadius;
            capsule.height = LampColliderHeight;
            capsule.center = new Vector3(0f, LampColliderHeight * 0.5f, 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, lamp.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
                r.gameObject.isStatic = true;
            PulseMachine(lamp, instance.transform);

            if (!withLight)
                return;
            Transform marker = null;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(LampLightMarker))
                {
                    marker = t;
                    break;
                }
            }
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(marker != null ? marker : lamp.transform, false);
            if (marker == null)
                lightGo.transform.localPosition = new Vector3(0f, 3.2f, 0.9f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LampLightColor;
            light.range = crystalSettings.lampLightRange;
            light.intensity = crystalSettings.lampLightIntensity;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Poeira mágica (D-069, D-070): partículas fracas e baixas nas avenidas (caixa com a largura da avenida,
        /// encostada na ponta de fora) e nas praças menores (círculo do tamanho da praça). Os emissores ficam além do
        /// disco de combate (dustCombatRadius): com a câmera de jogo, a poeira nunca cai sobre a praça nem fica na
        /// frente do combate (CrystalAmbienceTests). Some perto da câmera e tem tamanho máximo na tela. O sistema de
        /// partículas nasce em jogo (AmbienceParticles), então nada disso pesa na cena.
        /// </summary>
        private static void BuildMagicDust(Transform parent, MapLayout layout)
        {
            var s = crystalSettings;
            if (s == null || magicDustMat == null)
                return;
            var dust = new GameObject("PoeiraMagica").transform;
            dust.SetParent(parent, false);

            for (int i = 0; i < layout.StreetCount; i++)
            {
                var avenue = layout.Avenues[i];
                // Avenida: a caixa termina na ponta de fora e nunca começa antes do disco de combate.
                float start = Mathf.Max(avenue.EndS - s.avenueDustLength, s.dustCombatRadius);
                float length = avenue.EndS - start;
                if (s.avenueDustMax > 0 && length >= 1f)
                {
                    var c = avenue.Axis * ((start + avenue.EndS) * 0.5f);
                    var go = new GameObject($"PoeiraAvenida{i + 1}");
                    go.transform.SetParent(dust, false);
                    go.transform.position = new Vector3(c.X, s.dustMidHeight, c.Y);
                    go.transform.rotation = Quaternion.Euler(0f, avenue.AngleDeg, 0f); // +Z local ao longo da avenida
                    go.AddComponent<AmbienceParticles>().ConfigureMagicDust(magicDustMat, AmbienceDustShape.Box,
                        new Vector3(avenue.Width, s.avenueDustBoxHeight, length), 0f, s.avenueDustMax,
                        s.avenueDustPerSecond, s.dustVioletShare, s.dustGoldShare);
                }
                else if (s.avenueDustMax > 0)
                {
                    Debug.LogWarning($"AmbienceBuilder: avenida {i + 1} sem espaço para poeira além do disco de combate (r {s.dustCombatRadius}).");
                }

                // Praça menor: o círculo inteiro tem de ficar além do disco de combate.
                var plaza = layout.SmallPlazas[i];
                float nearest = new Vector2(plaza.Center.X, plaza.Center.Y).magnitude - plaza.Radius;
                if (s.plazaDustMax > 0 && nearest >= s.dustCombatRadius)
                {
                    var go = new GameObject($"PoeiraPraca{i + 1}");
                    go.transform.SetParent(dust, false);
                    go.transform.position = new Vector3(plaza.Center.X, s.dustMidHeight, plaza.Center.Y);
                    go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // o círculo deita no chão
                    go.AddComponent<AmbienceParticles>().ConfigureMagicDust(magicDustMat, AmbienceDustShape.Disc,
                        Vector3.one, plaza.Radius, s.plazaDustMax, s.plazaDustPerSecond, s.dustVioletShare, s.dustGoldShare);
                }
                else if (s.plazaDustMax > 0)
                {
                    Debug.LogWarning($"AmbienceBuilder: praça menor {i + 1} dentro do disco de combate (r {s.dustCombatRadius}); sem poeira.");
                }
            }
        }

        /// <summary>Faixa de cristal sem collider e sem sombra, filha da máquina (anda com ela).</summary>
        private static Renderer Vein(Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "VeioCristal";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = false; // pulsa por MaterialPropertyBlock
            return r;
        }

        private static Renderer[] MeshRenderers(Transform root)
            => System.Array.ConvertAll(root.GetComponentsInChildren<MeshRenderer>(true), r => (Renderer)r);

        private static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        // ---------- Materiais ----------

        private static void LoadMaterials()
        {
            ironMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/FerroEscuro.mat");
            brassMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Latao.mat");

            furnaceMouthMat = GetOrCreateLit(MaterialsFolder + "/BrasaFornalha.mat", new Color(0.35f, 0.08f, 0.02f), EmberEmissionColor);

            var soft = GetOrCreateSoftParticleTexture();
            // Brasa: aditiva e em HDR para passar do limiar do bloom.
            emberMat = GetOrCreateParticleMaterial(AmbienceFolder + "/ParticulaBrasa.mat", soft, new Color(3f, 1.1f, 0.3f, 1f), true);
            // Vapor: mistura por alfa, sem escrever profundidade.
            steamMat = GetOrCreateParticleMaterial(AmbienceFolder + "/ParticulaVapor.mat", soft, Color.white, false);
            // Poeira arcana: aditiva ciano, também HDR.
            dustMat = GetOrCreateParticleMaterial(AmbienceFolder + "/ParticulaPoeira.mat", soft, new Color(0.5f, 2f, 2.4f, 1f), true);

            // Cristal vivo (D-069). Recarregados pelo caminho: a importação dos modelos da cidade pode ter
            // deixado referências antigas mortas.
            crystalSettings = LoadCrystalSettings();
            crystalLitMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/CristalArcano.mat");
            crystalOffMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/CristalApagado.mat");
            copperMat = AssetDatabase.LoadAssetAtPath<Material>(CopperMaterialPath);
            // Poeira mágica: aditiva, branca (a cor vem de cada partícula) e com o brilho dos dados.
            float b = crystalSettings.dustBrightness;
            magicDustMat = GetOrCreateParticleMaterial(MagicDustMaterialPath, soft, new Color(b, b, b, 1f), true);
            EnableCameraFade(magicDustMat, DustFadeNear, DustFadeFar);
        }

        /// <summary>
        /// Some perto da câmera (Particles/Unlit do URP). O inspetor do shader calcula _CameraFadeParams sozinho;
        /// por script é preciso escrever o vetor e a palavra-chave.
        /// </summary>
        private static void EnableCameraFade(Material mat, float near, float far)
        {
            mat.SetFloat("_CameraFadingEnabled", 1f);
            mat.SetFloat("_CameraNearFadeDistance", near);
            mat.SetFloat("_CameraFarFadeDistance", far);
            mat.SetVector("_CameraFadeParams", new Vector4(near, 1f / Mathf.Max(0.01f, far - near), 0f, 0f));
            mat.EnableKeyword("_FADING_ON");
            EditorUtility.SetDirty(mat);
        }

        private static Material GetOrCreateLit(string path, Color baseColor, Color emission)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GetOrCreateParticleMaterial(string path, Texture2D texture, Color color, bool additive)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);

            // Superfície transparente (equivale a escolher Transparent no inspetor do shader).
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Gradiente radial branco com alfa suave, gerado uma única vez.</summary>
        private static Texture2D GetOrCreateSoftParticleTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftParticlePath);
            if (existing != null)
                return existing;

            int n = SoftParticleSize;
            var pixels = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep: borda sem corte
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(SoftParticlePath), ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(SoftParticlePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(SoftParticlePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftParticlePath);
        }

        // ---------- Construção ----------

        private static Vector3 Polar(float angleDegrees, float radius)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// Sem céu, os metais refletem preto. Uma sonda em tempo real no centro captura cristais e fornalhas
        /// uma vez ao carregar, e os metais passam a refletir o próprio ambiente.
        /// </summary>
        private static void AddReflectionProbe()
        {
            var old = GameObject.Find("SondaReflexo");
            if (old != null)
                Object.DestroyImmediate(old);
            var go = new GameObject("SondaReflexo");
            go.transform.position = new Vector3(0f, 3f, 0f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(60f, 14f, 60f);
            probe.resolution = 128;
            probe.intensity = 1.2f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = AmbientSky;
        }
    }
}
