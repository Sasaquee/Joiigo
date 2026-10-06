using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Arena.Life
{
    public enum AmbienceParticleKind { Embers, Steam, ArcaneDust, MagicDust }

    /// <summary>Forma da emissão da poeira mágica: caixa (avenida) ou disco deitado (praça menor).</summary>
    public enum AmbienceDustShape { Box, Disc }

    /// <summary>
    /// Partículas da ambientação da arena (D-017, D-069): brasas das fornalhas, vapor, poeira arcana da praça e poeira
    /// mágica das avenidas e praças menores. No estilo do SmokeEmitter: Configure*() só guarda os parâmetros (campos
    /// simples, cena leve) e o ParticleSystem nasce em tempo de execução, no Awake, na própria peça, com
    /// cullingMode = Pause (fora da tela a simulação para). Em modo de edição nada de partícula é criado, então nada
    /// disso serializa na cena (cada ParticleSystem gravado custava ~114 KB no Arena.unity).
    /// O lugar e o giro da peça (por exemplo Euler(-90, 0, 0) para o cone apontar para cima) vêm do Transform da peça,
    /// definido pelo AmbienceBuilder.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbienceParticles : MonoBehaviour
    {
        // ---------- Valores de ajuste visual (não são regra de jogo) ----------

        // Brasas: cone fino acima da boca da fornalha.
        private const int EmbersMax = 40;
        private const float EmbersRate = 6f;

        // Vapor
        private const int SteamMax = 30;

        // Poeira arcana da praça: pontinhos ciano de 3 a 6 cm, parados no ar.
        private const int ArcaneDustMax = 60;
        private const float ArcaneDustRate = 5f;

        // Poeira mágica (D-069). Baixa e parada, sobe devagar como fagulha fria; o brilho e o fade perto da câmera
        // vêm do material (AmbienceBuilder).
        private const float MagicDuration = 8f;
        private const float MagicHeightJitter = 0.9f;     // ± no disco da praça menor (a caixa da avenida tem a própria altura)
        private const float MagicMinLifetime = 8f;
        private const float MagicMaxLifetime = 12f;
        private const float MagicMinSize = 0.1f;
        private const float MagicMaxSize = 0.22f;
        private const float MagicMinRise = 0.02f;         // m/s: sobe no máximo ~0,7 m na vida
        private const float MagicMaxRise = 0.06f;
        private const float MagicNoise = 0.2f;
        private const float MagicPeakAlpha = 0.75f;
        private const float MagicMaxScreenSize = 0.02f;   // fração da tela: perto da câmera não vira borrão
        private static readonly Color DustCyan = new Color(0.44f, 0.94f, 1f);     // #6FF0FF, o CristalArcano
        private static readonly Color DustViolet = new Color(0.69f, 0.49f, 1f);   // #B07CFF
        private static readonly Color DustGold = new Color(1f, 0.82f, 0.48f);     // #FFD27A, latão aceso

        [SerializeField] private AmbienceParticleKind kind = AmbienceParticleKind.Embers;
        // Só referência a asset (serializa como GUID).
        [SerializeField] private Material material;
        // Vapor
        [SerializeField] private float rate = 3f;
        [SerializeField] private float sizeMin = 1f;
        [SerializeField] private float sizeMax = 2f;
        [SerializeField] private float speed = 1f;
        // Poeira: caixa (tamanho em m, nos eixos locais) ou disco (raio em m)
        [SerializeField] private AmbienceDustShape dustShape = AmbienceDustShape.Box;
        [SerializeField] private Vector3 area = Vector3.one;
        [SerializeField] private float radius = 1f;
        [SerializeField] private int maxParticles = 40;
        [SerializeField] private float violetShare;
        [SerializeField] private float goldShare;

        // Montado em jogo; não serializa.
        private ParticleSystem system;

        public AmbienceParticleKind Kind => kind;

        /// <summary>Sistema de partículas criado em jogo (null em modo de edição).</summary>
        public ParticleSystem Particles => system;

        // ---------- Configuração (guarda os parâmetros; em jogo refaz o sistema) ----------

        public void ConfigureEmbers(Material mat)
        {
            kind = AmbienceParticleKind.Embers;
            material = mat;
            Rebuild();
        }

        /// <summary>Vapor suave: partículas grandes, lentas e de alfa baixo, cinza azulado.</summary>
        public void ConfigureSteam(Material mat, float perSecond, float minSize, float maxSize, float riseSpeed)
        {
            kind = AmbienceParticleKind.Steam;
            material = mat;
            rate = perSecond;
            sizeMin = minSize;
            sizeMax = maxSize;
            speed = riseSpeed;
            Rebuild();
        }

        /// <summary>Poeira arcana fraca sobre a praça (caixa de <paramref name="size"/> m).</summary>
        public void ConfigureArcaneDust(Material mat, Vector3 size)
        {
            kind = AmbienceParticleKind.ArcaneDust;
            material = mat;
            dustShape = AmbienceDustShape.Box;
            area = size;
            Rebuild();
        }

        /// <summary>
        /// Poeira mágica. Caixa (<paramref name="size"/> nos eixos locais da peça) ou disco de
        /// <paramref name="discRadius"/> m deitado no plano local XY (gire a peça em Euler(-90, 0, 0)).
        /// </summary>
        public void ConfigureMagicDust(Material mat, AmbienceDustShape shape, Vector3 size, float discRadius,
            int max, float perSecond, float violet, float gold)
        {
            kind = AmbienceParticleKind.MagicDust;
            material = mat;
            dustShape = shape;
            area = size;
            radius = discRadius;
            maxParticles = max;
            rate = perSecond;
            violetShare = violet;
            goldShare = gold;
            Rebuild();
        }

        private void Rebuild()
        {
            if (Application.isPlaying)
                BuildSystem();
        }

        private void Awake()
        {
            BuildSystem();
        }

        // ---------- Construção do sistema ----------

        private void BuildSystem()
        {
            if (system == null)
            {
                system = GetComponent<ParticleSystem>();
                if (system == null)
                    system = gameObject.AddComponent<ParticleSystem>();
            }
            // Os módulos só aceitam mudanças de duração com o sistema parado.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var rend = system.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = material;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            switch (kind)
            {
                case AmbienceParticleKind.Embers: BuildEmbers(); break;
                case AmbienceParticleKind.Steam: BuildSteam(); break;
                case AmbienceParticleKind.ArcaneDust: BuildArcaneDust(); break;
                default: BuildMagicDust(rend); break;
            }

            // Fora da tela a simulação para (retoma de onde parou).
            var main = system.main;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            system.Play();
        }

        private void BuildEmbers()
        {
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.35f, 0.1f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = EmbersMax;

            var emission = system.emission;
            emission.rateOverTime = EmbersRate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.25f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.6f;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 1f, 0.1f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        }

        private void BuildSteam()
        {
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.68f, 0.75f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = SteamMax;

            var emission = system.emission;
            emission.rateOverTime = rate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.35f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.25f;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 0.14f, 0.25f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        }

        private void BuildArcaneDust()
        {
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(0.4f, 1f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = ArcaneDustMax;

            var emission = system.emission;
            emission.rateOverTime = ArcaneDustRate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.3f;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 1f, 0.3f));
        }

        private void BuildMagicDust(ParticleSystemRenderer rend)
        {
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = MagicDuration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(MagicMinLifetime, MagicMaxLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(MagicMinSize, MagicMaxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(DustColors(violetShare, goldShare))
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            var emission = system.emission;
            emission.rateOverTime = rate;

            var shape = system.shape;
            if (dustShape == AmbienceDustShape.Box)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = area;
            }
            else
            {
                // O círculo do Shape fica no plano XY local; a peça gira em Euler(-90) e ele deita no chão.
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = radius;
                shape.radiusThickness = 1f;
                shape.randomPositionAmount = MagicHeightJitter;
            }

            // Sobe devagar, como fagulha fria saindo do chão.
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(MagicMinRise, MagicMaxRise);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = MagicNoise;
            noise.frequency = 0.15f;

            var col = system.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, MagicPeakAlpha, 0.2f));

            rend.maxParticleSize = MagicMaxScreenSize;
        }

        // ---------- Utilitários ----------

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

        /// <summary>Cores sorteadas por partícula: ciano na maior parte, um pouco de violeta (D-070) e de dourado.</summary>
        private static Gradient DustColors(float violetShare, float goldShare)
        {
            float gold = Mathf.Clamp01(goldShare);
            float violet = Mathf.Clamp(violetShare, 0f, 1f - gold);
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
    }
}
