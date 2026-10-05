using Game.Cards;
using Game.Core.Aura;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Aura
{
    /// <summary>
    /// Desenho da aura (D-060 a D-065): círculo de latão com runas em cristal no chão, luz subindo em volta do corpo,
    /// faíscas da energia, fiapos violeta da maldição, brasas de quem caiu e casca de cristal do escudo.
    /// Quando a energia enche, uma onda de luz sai da borda do círculo e as runas piscam mais forte (D-067).
    /// Só desenha o AuraState que recebe; quem decide é o Core. As texturas vêm de Tools/Aura/aura_art.py.
    /// Montado em código no Awake (filho "Aura"), para o prefab gerado pelo construtor não guardar malhas nem materiais.
    /// </summary>
    public class AuraVisual : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        // Só desenho: alturas das camadas, giros e o quanto cada camada brilha em relação à força da aura.
        private const float CircleHeight = 0.025f;
        private const float RuneHeight = 0.03f;
        // aura_circulo.png: anel interno a 0,362 e externo a 0,444 da largura; a luz sobe entre os dois (0,40 = 0,8 do raio).
        private const float LightRadiusRatio = 0.8f;
        private const float RingSpinDegPerSec = 6f;
        private const float ShellSpinDegPerSec = 10f;
        private const float LightScrollPerSec = 0.12f;
        private const float BrassMinAlpha = 0.35f;
        private const float RuneGlow = 0.95f;
        private const float RuneFullGlow = 1.6f;
        private const float LightGlow = 0.8f;
        private const float ShellGlow = 0.55f;
        // Pulso de energia cheia (D-067). aura_circulo.png: o anel externo do latão termina a 0,444 da largura
        // (0,888 do raio); a onda nasce ali. O eco vem atrás (fração do caminho da onda) e é mais fino; o contorno
        // escuro fica colado por fora da faixa clara (o contorno do pós-processo não pega malha transparente).
        private const float PulseHeight = 0.035f;
        private const float BrassEdgeRatio = 0.888f;
        private const float PulseEchoLag = 0.7f;
        private const float PulseEchoBand = 0.5f;
        private const float PulseOutlineWidth = 0.05f;
        private static readonly Color PulseOutlineColor = new Color(0.169f, 0.165f, 0.188f); // FerroEscuro #2B2A30

        [SerializeField] private AuraSettings settings;
        [SerializeField] private Texture2D circleTexture;
        [SerializeField] private Texture2D runesTexture;
        [SerializeField] private Texture2D lightTexture;
        [SerializeField] private Texture2D sparkTexture;
        [SerializeField] private Texture2D wispTexture;
        [SerializeField] private Texture2D emberTexture;
        [SerializeField] private Texture2D shellTexture;

        private Transform root;
        private Transform spin;
        private MeshRenderer circle;
        private MeshRenderer runes;
        private MeshRenderer lightColumn;
        private MeshRenderer shell;
        private ParticleSystem sparks;
        private ParticleSystem wisps;
        private ParticleSystem embers;
        private MeshRenderer pulseWave;
        private MeshRenderer pulseEcho;
        private MeshRenderer pulseOutline;
        private MaterialPropertyBlock block;
        private float noiseSeed;
        private float lightScroll;
        private float sincePulse = float.PositiveInfinity; // segundos desde o último pulso de energia cheia
        private float pulseStartRadius;

        public void Configure(AuraSettings auraSettings, Texture2D circleTex, Texture2D runesTex, Texture2D lightTex,
            Texture2D sparkTex, Texture2D wispTex, Texture2D emberTex, Texture2D shellTex)
        {
            settings = auraSettings;
            circleTexture = circleTex;
            runesTexture = runesTex;
            lightTexture = lightTex;
            sparkTexture = sparkTex;
            wispTexture = wispTex;
            emberTexture = emberTex;
            shellTexture = shellTex;
        }

        /// <summary>Casca do escudo visível agora (para testes).</summary>
        public bool ShellVisible => shell != null && shell.enabled;

        /// <summary>Fiapos da maldição saindo agora (para testes).</summary>
        public bool WispsOn => wisps != null && wisps.emission.enabled;

        /// <summary>Quantos pulsos de energia cheia começaram (D-067, para testes).</summary>
        public int PulsesPlayed { get; private set; }

        /// <summary>Onda do pulso de energia cheia correndo agora (para testes).</summary>
        public bool PulseActive => settings != null && sincePulse < settings.fullPulseDuration;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            noiseSeed = Random.value * 100f;
            Build();
        }

        private void OnDestroy()
        {
            if (root != null)
                Destroy(root.gameObject);
        }

        // ---------- Montagem ----------

        private void Build()
        {
            if (settings == null || root != null)
                return;

            root = new GameObject("Aura").transform;
            root.SetParent(transform, false);
            spin = new GameObject("Giro").transform;
            spin.SetParent(root, false);

            // O FxKit.Quad não tem UV (é para cor chapada); o círculo precisa da textura inteira.
            Mesh quad = TexturedQuad();
            circle = Layer(spin, "Circulo", quad, Textured(FxKit.FlatAlpha, circleTexture), CircleHeight);
            runes = Layer(spin, "Runas", quad, Textured(FxKit.FlatAdditive, runesTexture), RuneHeight);
            lightColumn = Layer(root, "Luz", OpenCylinder(32), Textured(FxKit.FlatAdditive, lightTexture), 0f);
            shell = Layer(root, "Casca", Dome(24, 8), Textured(FxKit.FlatAdditive, shellTexture), 0f);
            shell.enabled = false;

            // Pulso de energia cheia (D-067): faixa clara, eco mais fino atrás e contorno escuro por fora. Anéis chapados
            // (FxKit.Ring, raio externo 1), sem textura; a cor vai pelo MaterialPropertyBlock. Não giram (filhos da raiz).
            float band = Mathf.Clamp(settings.fullPulseBand, 0.02f, 0.5f);
            pulseWave = Layer(root, "PulsoOnda", FxKit.Ring(1f - band), FxKit.FlatAdditive, PulseHeight);
            pulseEcho = Layer(root, "PulsoEco", FxKit.Ring(1f - band * PulseEchoBand), FxKit.FlatAdditive, PulseHeight);
            pulseOutline = Layer(root, "PulsoContorno", FxKit.Ring(1f / (1f + PulseOutlineWidth)), FxKit.FlatAlpha,
                PulseHeight);
            ShowPulse(false);

            sparks = Emitter("Faiscas", sparkTexture, 0.05f, 0.11f, 0.6f, 1.0f, 64);
            wisps = Emitter("Fiapos", wispTexture, 0.28f, 0.5f, 1.2f, 1.8f, 24);
            embers = Emitter("Brasas", emberTexture, 0.05f, 0.1f, 0.8f, 1.4f, 24);
        }

        private static Material Textured(Material template, Texture2D texture)
        {
            var m = new Material(template) { name = template.name + " (aura)", hideFlags = HideFlags.HideAndDontSave };
            if (texture != null)
                m.SetTexture(BaseMapId, texture);
            return m;
        }

        private static MeshRenderer Layer(Transform parent, string name, Mesh mesh, Material material, float height)
        {
            var renderer = FxKit.MeshChild(parent, name, mesh, material);
            renderer.transform.localPosition = new Vector3(0f, height, 0f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private ParticleSystem Emitter(string name, Texture2D texture, float sizeMin, float sizeMax, float lifeMin,
            float lifeMax, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // círculo de emissão deitado no chão
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0.15f; // nasce perto da borda do círculo

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Textured(FxKit.GlowParticle, texture);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }

        // ---------- Atualização ----------

        /// <summary>Aplica o estado mapeado pelo Core. Chamado pelo PlayerAura a cada quadro.</summary>
        public void Apply(AuraState state, float dt)
        {
            if (root == null)
                Build();
            if (root == null)
                return;

            bool downed = (state.Signals & AuraSignals.Downed) != 0;
            bool hurtBonus = (state.Signals & AuraSignals.HurtBonus) != 0;
            float radius = settings.fullRadius * state.Radius;
            float glow = state.Intensity * FlickerFactor(state.Flicker);

            spin.localRotation = Quaternion.Euler(0f, Time.time * RingSpinDegPerSec, 0f);
            circle.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            runes.transform.localScale = circle.transform.localScale;

            // Latão: o metal não some, só escurece com pouca vida (D-061).
            float brass = Mathf.Lerp(BrassMinAlpha, 1f, state.Intensity);
            Paint(circle, new Color(brass, brass, brass), downed ? BrassMinAlpha : 1f);

            // Runas: cristal na cor da aura, ou em brasa laranja no reforço da Mola (D-063); cheias com energia cheia (D-062).
            Color runeColor = ToColor(hurtBonus || downed ? state.Ember : state.Base);
            float runeGlow = glow * (state.RunesFull ? RuneFullGlow : RuneGlow);
            // No pulso de energia cheia as runas piscam mais forte e voltam ao normal (D-067).
            if (!downed && sincePulse < settings.fullPulseRuneTime)
            {
                float u = 1f - sincePulse / settings.fullPulseRuneTime;
                runeGlow *= 1f + settings.fullPulseRuneBoost * u * u;
            }
            if (hurtBonus)
                runeGlow *= 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
            Paint(runes, runeColor * runeGlow, 1f);

            // Luz subindo em volta do corpo (D-060); apagada em quem caiu.
            lightColumn.enabled = !downed;
            if (!downed)
            {
                float r = radius * LightRadiusRatio;
                float h = settings.lightHeight * (0.55f + 0.45f * state.Intensity);
                lightColumn.transform.localScale = new Vector3(r, h, r);
                lightScroll = (lightScroll + dt * LightScrollPerSec) % 1f;
                lightColumn.GetPropertyBlock(block);
                block.SetVector(BaseMapStId, new Vector4(1f, 1f, lightScroll, 0f));
                // No reforço da Mola a luz também fica em brasa: só as runas laranja somem no círculo pequeno.
                Color c = ToColor(hurtBonus ? state.Ember : state.Base) * (glow * LightGlow);
                block.SetColor(FxKit.BaseColorId, c);
                block.SetColor(FxKit.ColorId, c);
                lightColumn.SetPropertyBlock(block);
            }

            // Casca de cristal do escudo (D-063).
            bool shielded = (state.Signals & AuraSignals.Shield) != 0;
            shell.enabled = shielded;
            if (shielded)
            {
                shell.transform.localScale = Vector3.one * settings.shellRadius;
                shell.transform.localRotation = Quaternion.Euler(0f, -Time.time * ShellSpinDegPerSec, 0f);
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
                Paint(shell, ToColor(state.Shell) * (ShellGlow * pulse), 1f);
            }

            // Faíscas da energia (D-062), fiapos da maldição e brasas de quem caiu (D-063).
            Drive(sparks, settings.maxSparksPerSecond * state.SparkRate, settings.sparkRiseSpeed * state.SparkSpeed,
                radius, ToColor(state.Base));
            Drive(wisps, (state.Signals & AuraSignals.Curse) != 0 ? settings.wispsPerSecond : 0f, 0.55f,
                radius * 0.8f, ToColor(state.Curse));
            Drive(embers, downed ? settings.embersPerSecond : 0f, 0.45f, radius * 0.7f, ToColor(state.Ember));

            UpdatePulse(state, downed);

            // O relógio do pulso anda depois de desenhar: o quadro do Pulse() mostra a onda na borda do latão.
            if (sincePulse < Mathf.Max(settings.fullPulseDuration, settings.fullPulseRuneTime))
                sincePulse += dt;
            else
                sincePulse = float.PositiveInfinity;
        }

        /// <summary>
        /// Começa o pulso de energia cheia (D-067). O PlayerAura chama uma vez, no quadro em que a energia enche
        /// (AuraPulseTrigger); todos os jogadores veem o pulso de todos.
        /// </summary>
        public void Pulse(AuraState state)
        {
            if (root == null)
                Build();
            if (root == null)
                return;
            pulseStartRadius = settings.fullRadius * state.Radius * BrassEdgeRatio;
            sincePulse = 0f;
            PulsesPlayed++;
        }

        /// <summary>
        /// Onda do pulso: sai rápido da borda do latão e desacelera até o raio final, apagando no caminho.
        /// Faixa clara na cor Base da paleta, eco mais fraco atrás e contorno escuro por fora. Cai = some na hora.
        /// </summary>
        private void UpdatePulse(AuraState state, bool downed)
        {
            if (downed)
                sincePulse = float.PositiveInfinity;
            float duration = settings.fullPulseDuration;
            if (sincePulse >= duration)
            {
                ShowPulse(false);
                return;
            }

            float t = Mathf.Clamp01(sincePulse / duration);
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            float fade = (1f - t) * (1f - t);
            float end = Mathf.Max(pulseStartRadius, settings.fullPulseEndRadius);
            float r = Mathf.Lerp(pulseStartRadius, end, grow);
            float echo = Mathf.Lerp(pulseStartRadius, r, PulseEchoLag);
            Color baseColor = ToColor(state.Base);

            pulseWave.transform.localScale = new Vector3(r, 1f, r);
            Paint(pulseWave, baseColor * (settings.fullPulseGlow * fade), 1f);
            pulseEcho.transform.localScale = new Vector3(echo, 1f, echo);
            Paint(pulseEcho, baseColor * (settings.fullPulseGlow * settings.fullPulseEchoGlow * fade), 1f);
            float outline = r * (1f + PulseOutlineWidth);
            pulseOutline.transform.localScale = new Vector3(outline, 1f, outline);
            Paint(pulseOutline, PulseOutlineColor, settings.fullPulseOutlineAlpha * fade);

            ShowPulse(true);
            pulseOutline.enabled = settings.fullPulseOutlineAlpha > 0f;
        }

        private void ShowPulse(bool on)
        {
            pulseWave.enabled = on;
            pulseEcho.enabled = on;
            pulseOutline.enabled = on;
        }

        /// <summary>Falha de lâmpada (D-061): com Flicker alto, a aura cai quase a nada por instantes.</summary>
        private float FlickerFactor(float flicker)
        {
            if (flicker <= 0f)
                return 1f;
            float n = Mathf.PerlinNoise(Time.time * 11f, noiseSeed);
            float dip = Mathf.Clamp01((0.62f - n) * 4f);
            return 1f - flicker * dip * 0.85f;
        }

        private static void Drive(ParticleSystem ps, float rate, float rise, float radius, Color color)
        {
            var emission = ps.emission;
            emission.enabled = rate > 0.01f;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.radius = Mathf.Max(0.05f, radius);
            var velocity = ps.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.7f, rise * 1.2f);
            var main = ps.main;
            main.startColor = color;
        }

        private void Paint(Renderer renderer, Color color, float alpha) => FxKit.Paint(renderer, block, color, alpha);

        private static Color ToColor(AuraColor c) => new Color(c.R, c.G, c.B, 1f);

        // ---------- Malhas ----------

        /// <summary>Quadrado 1 x 1 deitado no plano XZ, olhando para cima, com UV de 0 a 1.</summary>
        private static Mesh TexturedQuad()
        {
            var mesh = new Mesh
            {
                name = "AuraQuadrado",
                vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Cilindro aberto de raio 1 e altura 1, base no chão; u dá a volta, v sobe.</summary>
        private static Mesh OpenCylinder(int segments)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = p;
                vertices[i * 2 + 1] = p + Vector3.up;
                uv[i * 2] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2, t = i * 6;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b + 2; triangles[t + 4] = b + 1; triangles[t + 5] = b + 3;
            }
            var mesh = new Mesh { name = "AuraLuz", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Cúpula (meia esfera) de raio 1, facetada (normais por face), para a casca de cristal.</summary>
        private static Mesh Dome(int segments, int rings)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            Vector3 P(int s, int r)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float e = r / (float)rings * Mathf.PI * 0.5f;
                return new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * Mathf.Cos(e));
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(Mathf.Atan2(a.z, a.x) / (Mathf.PI * 2f) * 3f, a.y * 2f));
                uv.Add(new Vector2(Mathf.Atan2(b.z, b.x) / (Mathf.PI * 2f) * 3f, b.y * 2f));
                uv.Add(new Vector2(Mathf.Atan2(c.z, c.x) / (Mathf.PI * 2f) * 3f, c.y * 2f));
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    Tri(P(s, r), P(s, r + 1), P(s + 1, r));
                    if (r + 1 < rings)
                        Tri(P(s + 1, r), P(s, r + 1), P(s + 1, r + 1));
                }
            var mesh = new Mesh { name = "AuraCasca" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
