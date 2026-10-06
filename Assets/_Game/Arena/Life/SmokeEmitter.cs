using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Arena.Life
{
    public enum SmokeKind { Chimney, Steam, Sparks, ArcaneMotes }

    /// <summary>
    /// Emissor de fumaça, vapor, faíscas ou motes arcanos para a cidade steampunk (D-042).
    /// Partículas grossas e poucas, para ler bem na câmera de jogo.
    /// Configure() só guarda tipo e intensidade (campos simples, cena leve): o ParticleSystem filho nasce em
    /// tempo de execução, no Awake. Em modo de edição nada de partícula é criado, então nada disso serializa na cena.
    /// O sistema pausa quando sai da tela (cullingMode = Pause).
    /// </summary>
    [DisallowMultipleComponent]
    public class SmokeEmitter : MonoBehaviour
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        private const string ChildName = "Particulas";
        private const float MinIntensity = 0.1f;
        private const float MaxIntensity = 3f;
        private const float WindUpdateInterval = 0.12f;

        // Máximo de partículas vivas com intensidade 1.
        private const int ChimneyMax = 20;
        private const int SteamMax = 25;
        private const int SparksMax = 30;
        private const int MotesMax = 20;

        [SerializeField] private SmokeKind kind = SmokeKind.Chimney;
        [SerializeField, Range(MinIntensity, MaxIntensity)] private float intensity = 1f;
        // Só referência a asset (serializa como GUID). Vazia, o Awake usa o material de reserva.
        [SerializeField] private Material material;

        // Montados em jogo; não serializam.
        private ParticleSystem system;
        // Fração do vento global aplicada a este tipo (0 = ignora o vento).
        private float windFactor = 1f;
        private float windTimer;

        /// <summary>Tipo de fumaça configurado.</summary>
        public SmokeKind Kind => kind;

        /// <summary>Sistema de partículas criado em jogo (null em modo de edição).</summary>
        public ParticleSystem Particles => system;

        /// <summary>
        /// Define o tipo e a intensidade. Em modo de edição só guarda os parâmetros;
        /// em jogo (re)constrói o sistema de partículas.
        /// </summary>
        public void Configure(SmokeKind newKind, float newIntensity)
        {
            kind = newKind;
            intensity = Mathf.Clamp(newIntensity, MinIntensity, MaxIntensity);
            material = SmokeMaterials.GetPersistent(kind);

            if (Application.isPlaying)
                BuildRuntime();
        }

        private void Awake()
        {
            BuildRuntime();
        }

        /// <summary>Cria (ou refaz) o ParticleSystem a partir dos parâmetros guardados.</summary>
        private void BuildRuntime()
        {
            if (material == null)
                material = SmokeMaterials.Get(kind);
            BuildSystem();
        }

        private void OnEnable()
        {
            windTimer = 0f;
        }

        private void Update()
        {
            if (windFactor <= 0f || system == null)
                return;

            windTimer -= Time.deltaTime;
            if (windTimer > 0f)
                return;
            windTimer = WindUpdateInterval;

            Vector3 w = GlobalWind.Current * windFactor;
            var vel = system.velocityOverLifetime;
            vel.x = w.x;
            vel.z = w.z;
        }

        // ---------- Construção do sistema ----------

        private void BuildSystem()
        {
            GameObject go;
            if (system != null)
            {
                go = system.gameObject;
            }
            else
            {
                // Cena antiga ainda pode trazer o filho gravado: reaproveita em vez de duplicar.
                Transform child = transform.Find(ChildName);
                go = child != null ? child.gameObject : new GameObject(ChildName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                system = go.GetComponent<ParticleSystem>();
                if (system == null)
                    system = go.AddComponent<ParticleSystem>();
            }

            // Os módulos só aceitam mudar a duração com o sistema parado.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = material;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

            ResetModules();

            switch (kind)
            {
                case SmokeKind.Chimney: BuildChimney(); break;
                case SmokeKind.Steam: BuildSteam(); break;
                case SmokeKind.Sparks: BuildSparks(); break;
                default: BuildMotes(); break;
            }

            system.Play();
        }

        /// <summary>Desliga os módulos opcionais, para reconfigurar um sistema existente sem sobras.</summary>
        private void ResetModules()
        {
            var noise = system.noise;
            noise.enabled = false;
            var col = system.colorOverLifetime;
            col.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = false;
            var vel = system.velocityOverLifetime;
            vel.enabled = false;
            var emission = system.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        private int Cap(int baseMax)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseMax * intensity));
        }

        private int Count(int baseCount)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * intensity));
        }

        /// <summary>Alfa sobe até o pico, segura e desce a zero no fim da vida.</summary>
        private static Gradient Fade(float peak, float fadeIn, float fadeOutStart)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peak, fadeIn),
                    new GradientAlphaKey(peak, fadeOutStart),
                    new GradientAlphaKey(0f, 1f)
                });
            return g;
        }

        /// <summary>Configurações comuns: laço, espaço do mundo, cone apontando para cima (+Y).</summary>
        private void SetupCommon(float duration, float lifeMin, float lifeMax, int maxParticles,
            ParticleSystemSimulationSpace space, bool prewarm)
        {
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = duration;
            main.prewarm = prewarm;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = maxParticles;
            // Fora da tela a fumaça para de simular (retoma de onde parou).
            main.cullingMode = ParticleSystemCullingMode.Pause;
        }

        private void SetupWindVelocity(float factor)
        {
            windFactor = factor;
            var vel = system.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // Todas as curvas precisam estar no mesmo modo (constante).
            vel.x = 0f;
            vel.y = 0f;
            vel.z = 0f;
        }

        // ---------- Tipos ----------

        private void BuildChimney()
        {
            int max = Cap(ChimneyMax);
            const float lifeMin = 3.5f, lifeMax = 5f;
            SetupCommon(5f, lifeMin, lifeMax, max, ParticleSystemSimulationSpace.World, true);

            var main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            // Blocos grandes: 0,5 a 0,9 m (cerca de 15 a 30 pixels na câmera de jogo).
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.16f, 0.19f, 1f), new Color(0.3f, 0.3f, 0.34f, 1f));
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.rateOverTime = max * 0.85f / ((lifeMin + lifeMax) * 0.5f);

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.75f, 0.15f, 0.55f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.7f));

            SetupWindVelocity(1f);
        }

        private void BuildSteam()
        {
            int max = Cap(SteamMax);
            SetupCommon(6f, 1.5f, 2.5f, max, ParticleSystemSimulationSpace.World, false);

            var main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.82f, 0.86f, 1f), new Color(0.95f, 0.96f, 0.98f, 1f));
            main.gravityModifier = -0.04f;

            // Vazamento contínuo fraco, mais o ritmo da válvula: chiado em rajadas curtas e, de vez em quando, um sopro forte.
            var emission = system.emission;
            emission.rateOverTime = 1.5f * intensity;

            var hiss = new ParticleSystem.Burst(0f, (short)Count(2), (short)Count(3), 5, 0.12f);
            var puff = new ParticleSystem.Burst(3.2f, (short)Count(6), (short)Count(9), 1, 0f);
            puff.probability = 0.6f;
            var small = new ParticleSystem.Burst(4.6f, (short)Count(2), (short)Count(3), 3, 0.15f);
            small.probability = 0.4f;
            emission.SetBursts(new[] { hiss, puff, small });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.5f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.8f, 0.1f, 0.45f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            SetupWindVelocity(0.6f);
        }

        private void BuildSparks()
        {
            int max = Cap(SparksMax);
            SetupCommon(4f, 0.6f, 1.4f, max, ParticleSystemSimulationSpace.World, false);

            var main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            // Mínimo de ~0,08 m (cerca de 3 pixels) para não cintilar.
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
            main.startRotation = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.85f, 0.35f, 1f));
            main.gravityModifier = 1f;

            var emission = system.emission;
            emission.rateOverTime = 4f * intensity;
            // Rajadas irregulares: infinitas, espaçadas, nem sempre disparam.
            var burst = new ParticleSystem.Burst(0.4f, (short)Count(3), (short)Count(6), 0, 1.3f);
            burst.probability = 0.5f;
            emission.SetBursts(new[] { burst });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(1f, 0.05f, 0.6f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            SetupWindVelocity(0.3f);
        }

        private void BuildMotes()
        {
            int max = Cap(MotesMax);
            const float lifeMin = 4f, lifeMax = 6f;
            // Espaço local: a órbita gira em torno do próprio emissor, mesmo que ele se mova.
            SetupCommon(5f, lifeMin, lifeMax, max, ParticleSystemSimulationSpace.Local, true);

            var main = system.main;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.17f);
            main.startRotation = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1f, 1f, 1f), new Color(0.65f, 1f, 1f, 1f));
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.rateOverTime = max * 0.85f / ((lifeMin + lifeMax) * 0.5f);

            // Círculo no plano horizontal (XZ), com largura suficiente para a órbita ser visível.
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.7f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // Sobe devagar e gira em torno do eixo Y local. Todas as curvas no mesmo modo (constante).
            var vel = system.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = 0f;
            vel.y = 0.35f;
            vel.z = 0f;
            vel.orbitalX = 0f;
            vel.orbitalY = 0.9f;
            vel.orbitalZ = 0f;
            vel.orbitalOffsetX = 0f;
            vel.orbitalOffsetY = 0f;
            vel.orbitalOffsetZ = 0f;
            vel.radial = 0f;
            windFactor = 0f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.5f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(1f, 0.2f, 0.7f));
        }
    }
}
