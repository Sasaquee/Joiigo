using System.IO;
using Game.Arena;
using Game.Cameras;
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

        // ---------- Cristal vivo (D-069): veios, pulso e poeira mágica ----------
        // Números balanceáveis em Data/Ambience/CrystalAmbienceSettings.asset; aqui só a forma e o lugar das peças.

        public const string CrystalSettingsPath = "Assets/_Game/Data/Ambience/CrystalAmbienceSettings.asset";
        private const string MagicDustMaterialPath = AmbienceFolder + "/ParticulaPoeiraMagica.mat";

        // Geometria da arena copiada do ArenaBuilder e de build_props.py (só leitura; se lá mudar, mude aqui).
        private const float WallRadius = 26f;              // ArenaBuilder.WallRadius
        private const float WallSegmentOverlap = 0.3f;     // ArenaBuilder.BuildBoundary: +0,3 m por segmento
        private const float WallPipeHeight = 1f;           // Cano.fbx a 1 m do chão, raio 0,25
        private const float WallPipeRadius = 0.25f;
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
        private const float WallVeinWidth = 0.11f;         // na quarta parte de cima do cano, virada para a praça
        private const float BoilerVeinWidth = 0.14f;
        private const int BoilerVeinCount = 4;             // entre as hastes de latão (que estão a cada 45°)
        private const float GateVeinWidth = 0.12f;
        private const float GateVeinX = 1.8f;

        // Poeira mágica: rua do anel, logo atrás do muro e baixa (a câmera de jogo não tem horizonte: a borda de cima
        // da tela cai no chão ~20 m à frente dela, então poeira alta ou longe nunca aparece).
        // Só no lado longe da câmera: no lado dela, com o jogador encostado no muro, a câmera fica fora do muro e a
        // poeira entre ela e a praça cobriria o combate. 210° em volta do giro da câmera é o maior arco que, com
        // altura até 5 m, nunca cai sobre a praça (conferido em todas as posições do jogador; CrystalAmbienceTests).
        private const string CameraSettingsPath = "Assets/_Game/Data/Camera/CameraSettings.asset";
        private const float DustArcDegrees = 210f;
        private const float DustHeightJitter = 0.9f;       // ± em volta de streetDustHeight
        private const float DustMinLifetime = 8f;
        private const float DustMaxLifetime = 12f;
        private const float DustMinSize = 0.1f;
        private const float DustMaxSize = 0.22f;
        private const float DustMinRise = 0.02f;           // m/s: sobe no máximo ~0,7 m na vida
        private const float DustMaxRise = 0.06f;
        private const float DustNoise = 0.2f;
        private const float DustPeakAlpha = 0.75f;
        private const float DustMaxScreenSize = 0.02f;     // fração da tela: perto da câmera não vira borrão
        private const float DustFadeNear = 4f;             // some perto da câmera (m)
        private const float DustFadeFar = 9f;
        private static readonly Color DustCyan = new Color(0.44f, 0.94f, 1f);     // #6FF0FF, o CristalArcano
        private static readonly Color DustViolet = new Color(0.69f, 0.49f, 1f);   // #B07CFF
        private static readonly Color DustGold = new Color(1f, 0.82f, 0.48f);     // #FFD27A, latão aceso

        private static Material ironMat, brassMat, furnaceMouthMat, emberMat, steamMat, dustMat;
        private static Material crystalLitMat, crystalOffMat, magicDustMat;
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
            foreach (float angle in BoilerAngles)
                BuildSteam(steam, "VaporCaldeira", Polar(angle, BoilerRadius) + Vector3.up * BoilerTopHeight, 4f, 1.6f, 3.2f, 0.9f);
            foreach (var v in VentPositions)
                BuildVent(steam, Polar(v.x, v.y));

            BuildArcaneDust(root);
            BuildLivingCrystal(arenaRoot, root); // D-069: veios, pulso dos cristais e poeira mágica

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
            var ps = CreateSystem("Brasas", furnace, new Vector3(0f, 1.5f, 0.95f), emberMat);
            // Aponta o cone para cima (+Y mundo).
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.35f, 0.1f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.25f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.6f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 1f, 0.1f));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ps.Play();
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

        /// <summary>Vapor suave: partículas grandes, lentas e de alfa baixo, cinza azulado.</summary>
        private static void BuildSteam(Transform parent, string name, Vector3 worldPosition, float rate,
            float sizeMin, float sizeMax, float speed)
        {
            var ps = CreateSystem(name, parent, Vector3.zero, steamMat);
            ps.transform.position = worldPosition;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.68f, 0.75f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.35f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.25f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 0.14f, 0.25f));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            ps.Play();
        }

        // ---------- Poeira arcana ----------

        private static void BuildArcaneDust(Transform parent)
        {
            var ps = CreateSystem("PoeiraArcana", parent, new Vector3(0f, 1.5f, 0f), dustMat);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(0.4f, 1f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = DustArea;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.3f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 1f, 0.3f));

            ps.Play();
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
        /// A luz sai da faixa de cristal de cada caldeira, sobe e desce pela caldeira e corre pelo cano do muro;
        /// os portões ganham um circuito de cristal que só acende na largada (como os cristais deles, D-013);
        /// fornalhas e postes respiram; e uma poeira mágica fraca paira sobre a cidade, fora do muro.
        /// Nenhuma luz nova: tudo é emissão.
        /// </summary>
        private static void BuildLivingCrystal(Transform arenaRoot, Transform ambience)
        {
            if (crystalLitMat == null)
            {
                Debug.LogWarning("AmbienceBuilder: CristalArcano.mat não encontrado; cristal vivo (D-069) não construído.");
                return;
            }
            var group = new GameObject("CristalVivo").transform;
            group.SetParent(ambience, false);

            BuildBoilerAndWallVeins(arenaRoot, group);
            BuildGateVeins(arenaRoot, group);
            var posts = arenaRoot.Find("PostesArcanos");
            if (posts != null)
            {
                foreach (Transform post in posts)
                    PulseMachine(post.gameObject, post);
            }
            BuildMagicDust(group);
        }

        /// <summary>Cristal da peça respira devagar, cada uma no seu tempo (sem luz junto: fica perto do combate).</summary>
        private static void PulseMachine(GameObject host, Transform model)
        {
            if (crystalLitMat == null)
                return;
            host.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Maquina,
                MeshRenderers(model), null, null, crystalLitMat);
        }

        private static void BuildBoilerAndWallVeins(Transform arenaRoot, Transform group)
        {
            var renderers = new System.Collections.Generic.List<Renderer>();
            var offsets = new System.Collections.Generic.List<float>();
            var boilerAngles = new System.Collections.Generic.List<float>();
            float boilerToWall = 0f;

            // Caldeiras: a faixa de cristal do modelo é o coração (deslocamento 0); quatro veios sobem e descem dela.
            var boilers = arenaRoot.Find("Caldeiras");
            if (boilers != null)
            {
                foreach (Transform boiler in boilers)
                {
                    boilerAngles.Add(AngleOf(boiler.position));
                    boilerToWall = WallRadius - Flat(boiler.position).magnitude;
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

            // Cano do muro: uma fenda de cristal por segmento, na quarta parte de cima do cano, virada para a praça.
            // A crista sai da caldeira mais próxima e corre pelo muro nos dois sentidos.
            var wall = arenaRoot.Find("Limite");
            if (wall != null && wall.childCount > 0)
            {
                float segmentLength = 2f * Mathf.PI * WallRadius / wall.childCount + WallSegmentOverlap;
                float c = WallPipeRadius + 0.005f;
                var local = new Vector3(0f, WallPipeHeight + c * Mathf.Sin(45f * Mathf.Deg2Rad), -c * Mathf.Cos(45f * Mathf.Deg2Rad));
                foreach (Transform segment in wall)
                {
                    // +Z local do segmento aponta para fora; Euler(-45) deita a fenda na diagonal de cima, para dentro.
                    renderers.Add(Vein(segment, local, Quaternion.Euler(-45f, 0f, 0f),
                        new Vector3(segmentLength - 0.05f, VeinThickness, WallVeinWidth), crystalLitMat));
                    float angle = AngleOf(segment.position);
                    float arc = 180f;
                    foreach (float b in boilerAngles)
                        arc = Mathf.Min(arc, Mathf.Abs(Mathf.DeltaAngle(b, angle)));
                    offsets.Add(boilerToWall + arc * Mathf.Deg2Rad * WallRadius);
                }
            }

            if (renderers.Count == 0)
                return;
            var go = new GameObject("VeiosCaldeirasMuro");
            go.transform.SetParent(group, false);
            go.AddComponent<CrystalPulse>().Configure(crystalSettings, CrystalPulseRole.Veio, renderers.ToArray(),
                offsets.ToArray(), null, crystalLitMat);
        }

        /// <summary>
        /// Circuito de cristal na frente de cada portão-máquina: um veio na viga de cobre, dois descendo pela carcaça
        /// e quatro ligando as gemas das colunas ao anel da engrenagem. Apagado até a largada (D-013).
        /// </summary>
        private static void BuildGateVeins(Transform arenaRoot, Transform group)
        {
            var session = Object.FindFirstObjectByType<MatchState>();
            // Sem sessão na cena não há largada para esperar: o circuito nasce aceso.
            var startMat = session != null && crystalOffMat != null ? crystalOffMat : crystalLitMat;
            float z = GateFront + VeinThickness * 0.25f;
            foreach (Transform gate in arenaRoot)
            {
                if (!gate.name.StartsWith("PortaoMaquina"))
                    continue;
                var renderers = new System.Collections.Generic.List<Renderer>();
                var offsets = new System.Collections.Generic.List<float>();
                var heart = new Vector2(0f, 2f); // centro da engrenagem: a crista sai dela

                void Add(Vector3 pos, Vector3 size)
                {
                    renderers.Add(Vein(gate, pos, Quaternion.identity, size, startMat));
                    offsets.Add(Vector2.Distance(new Vector2(pos.x, pos.y), heart));
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
        /// Poeira mágica: partículas fracas na rua do anel, logo atrás do muro, no lado longe da câmera. Aparece nas
        /// bordas da tela, na frente das fachadas, quando o jogador chega perto do muro; nunca cai sobre a praça
        /// (inimigos, golpes, cartas e aura). Some perto da câmera e tem tamanho máximo na tela.
        /// </summary>
        private static void BuildMagicDust(Transform parent)
        {
            var s = crystalSettings;
            if (s == null || s.streetDustMax <= 0)
                return;
            float inner = s.streetDustInnerRadius;
            float outer = Mathf.Max(s.streetDustOuterRadius, inner + 0.5f);
            var cameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(CameraSettingsPath);
            float cameraYaw = cameraSettings != null ? cameraSettings.yaw : 30f;

            var ps = CreateSystem("PoeiraMagica", parent, new Vector3(0f, s.streetDustHeight, 0f), magicDustMat);
            // O círculo do Shape fica no plano XY local; Euler(-90) deita no chão (+X local = leste, +Y local = sul).
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(DustMinLifetime, DustMaxLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(DustMinSize, DustMaxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(DustColors(s)) { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = s.streetDustMax;

            var emission = ps.emission;
            emission.rateOverTime = s.streetDustPerSecond;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = outer;
            shape.radiusThickness = Mathf.Clamp01((outer - inner) / outer); // só o anel da rua, nada dentro do muro
            shape.arc = DustArcDegrees;
            // O arco começa no +X local (bússola 90°) e cresce no sentido horário visto de cima (para o sul).
            // Girar o Shape em Z desloca o começo: o arco fica centrado no giro da câmera (o lado longe dela).
            shape.rotation = new Vector3(0f, 0f, cameraYaw - DustArcDegrees * 0.5f - 90f);
            shape.randomPositionAmount = DustHeightJitter;

            // Sobe devagar, como fagulha fria saindo da rua.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(DustMinRise, DustMaxRise);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = DustNoise;
            noise.frequency = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, DustPeakAlpha, 0.2f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = DustMaxScreenSize;

            ps.Play();
        }

        /// <summary>Cores sorteadas por partícula: ciano na maior parte, um pouco de violeta e de dourado.</summary>
        private static Gradient DustColors(CrystalAmbienceSettings s)
        {
            float gold = Mathf.Clamp01(s.dustGoldShare);
            float violet = Mathf.Clamp(s.dustVioletShare, 0f, 1f - gold);
            float cyanEnd = 1f - gold - violet;
            // Modo Fixed: cada ponto do gradiente usa a próxima chave, então as faixas não se misturam.
            var keys = new System.Collections.Generic.List<GradientColorKey>();
            if (cyanEnd > 0.001f)
                keys.Add(new GradientColorKey(DustCyan, cyanEnd));
            if (violet > 0.001f)
                keys.Add(new GradientColorKey(DustViolet, 1f - gold));
            if (gold > 0.001f)
                keys.Add(new GradientColorKey(DustGold, 1f));
            if (keys.Count == 0)
                keys.Add(new GradientColorKey(DustCyan, 1f));
            var g = new Gradient { mode = GradientMode.Fixed };
            g.SetKeys(keys.ToArray(), new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
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

        private static float AngleOf(Vector3 p) => Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;

        // ---------- Partículas: utilitários ----------

        private static ParticleSystem CreateSystem(string name, Transform parent, Vector3 localPos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            // Os módulos só aceitam mudanças de duração com o sistema parado.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Alfa sobe rápido até o pico e desce até zero no fim da vida.</summary>
        private static Gradient FadeGradient(Color color, float peakAlpha, float fadeInTime)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, fadeInTime),
                    new GradientAlphaKey(0f, 1f)
                });
            return g;
        }

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
