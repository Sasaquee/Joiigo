using System.IO;
using Game.Arena;
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
        private const float PostExposure = 0.35f;
        private const float VignetteIntensity = 0.3f;
        private const float VignetteSmoothness = 0.4f;
        private const float WhiteBalanceTemperature = -8f;
        private static readonly Vector4 CoolShadows = new Vector4(0.9f, 0.97f, 1.1f, 0f);

        // Ambiente, neblina e câmera
        private static readonly Color AmbientSky = new Color(0.16f, 0.19f, 0.27f);
        private static readonly Color AmbientEquator = new Color(0.12f, 0.11f, 0.12f);
        private static readonly Color AmbientGround = new Color(0.07f, 0.055f, 0.05f);
        private static readonly Color FogColor = new Color(0.04f, 0.05f, 0.07f);
        private const float FogDensity = 0.009f;

        // Luar frio
        private static readonly Color MoonColor = new Color(0.55f, 0.65f, 1f);
        private const float MoonIntensity = 0.55f;
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

        private static Material ironMat, brassMat, furnaceMouthMat, emberMat, steamMat, dustMat;

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
