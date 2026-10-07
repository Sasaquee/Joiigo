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
        // Levantar um aliado (D-083). aura_runas.png tem 8 runas, uma no topo (+Z), a cada 45°; cada fatia cobre uma runa.
        private const int ReviveRuneCount = 8;
        private const float ReviveRuneHeight = 0.032f;
        private const float ReviveRuneGlow = 1.25f;
        private const float ReviveLightFrom = 0.02f;
        private const int ReviveWedgeArcSteps = 6;
        private static Mesh[] reviveWedges; // as fatias não mudam: uma vez só, compartilhadas por todas as auras
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
        private MeshRenderer[] reviveRunes; // uma fatia do disco das runas por runa, acesa em sentido horário (D-083)
        private MeshRenderer blessingRing; // anel dourado que respira dentro do círculo (bênção do 20, D-085)
        private ParticleSystem goldSparks; // faíscas douradas mais fortes que as da energia (bênção do 20, D-085)
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

        /// <summary>Anel dourado e faíscas da bênção do 20 aparecendo agora (D-085, para testes).</summary>
        public bool BlessingVisible => blessingRing != null && blessingRing.enabled;

        /// <summary>Cor do anel dourado no último quadro (D-085, para testes).</summary>
        public Color BlessingColor { get; private set; }

        /// <summary>Quantos pulsos de energia cheia começaram (D-067, para testes).</summary>
        public int PulsesPlayed { get; private set; }

        /// <summary>Quantas fatias de runa estão acesas pelo levantar de um aliado (D-083, para testes). Zero fora do levantar.</summary>
        public int ReviveRunesLit { get; private set; }

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
            // Levantar um aliado (D-083): o círculo das runas em fatias, uma por runa, para acender uma a uma em anel.
            // Giram com o círculo (filhas do giro) e ficam apagadas até haver progresso.
            reviveRunes = new MeshRenderer[ReviveRuneCount];
            Material reviveMaterial = Textured(FxKit.FlatAdditive, runesTexture);
            for (int i = 0; i < ReviveRuneCount; i++)
            {
                reviveRunes[i] = Layer(spin, "RunaLevantar" + i, ReviveWedge(i), reviveMaterial, ReviveRuneHeight);
                reviveRunes[i].enabled = false;
            }
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

            // Bênção do 20 (D-085): anel dourado dentro do círculo, que respira. Chapado, sem textura; a cor vai pelo
            // MaterialPropertyBlock. Não gira (filho da raiz) e fica abaixo do pulso de energia cheia.
            blessingRing = Layer(root, "BencaoAnel",
                FxKit.Ring(1f - Mathf.Clamp(settings.blessingRingBand, 0.03f, 0.5f)), FxKit.FlatAdditive, RuneHeight);
            blessingRing.enabled = false;

            sparks = Emitter("Faiscas", sparkTexture, 0.05f, 0.11f, 0.6f, 1.0f, 64);
            wisps = Emitter("Fiapos", wispTexture, 0.28f, 0.5f, 1.2f, 1.8f, 24);
            embers = Emitter("Brasas", emberTexture, 0.05f, 0.1f, 0.8f, 1.4f, 24);
            goldSparks = Emitter("FaiscasOuro", sparkTexture, 0.07f, 0.14f, 0.7f, 1.1f, 48);
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
            bool blessed = !downed && (state.Signals & AuraSignals.Blessing) != 0;
            float radius = settings.fullRadius * state.Radius;
            float glow = state.Intensity * FlickerFactor(state.Flicker);

            spin.localRotation = Quaternion.Euler(0f, Time.time * RingSpinDegPerSec, 0f);
            circle.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            runes.transform.localScale = circle.transform.localScale;

            // Latão: o metal não some, só escurece com pouca vida (D-061).
            float brass = Mathf.Lerp(BrassMinAlpha, 1f, state.Intensity);
            // Caído o latão fica apagado e volta conforme o aliado levanta (D-083).
            Paint(circle, new Color(brass, brass, brass), downed ? Mathf.Lerp(BrassMinAlpha, 1f, state.ReviveProgress) : 1f);

            // Runas: cristal na cor da aura, ou em brasa laranja no reforço da Mola (D-063); cheias com energia cheia (D-062).
            // Abençoado (D-085): runas e luz em dourado; o reforço da Mola continua piscando (forma), a cor é da bênção.
            Color runeColor = ToColor(blessed ? state.Gold : hurtBonus || downed ? state.Ember : state.Base);
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
            UpdateReviveRunes(state, downed);

            // Luz subindo em volta do corpo (D-060); apagada em quem caiu, até um aliado começar a levantá-lo (D-083).
            bool lit = !downed || state.ReviveProgress > ReviveLightFrom;
            lightColumn.enabled = lit;
            if (lit)
            {
                float r = radius * LightRadiusRatio;
                float h = settings.lightHeight * (0.55f + 0.45f * state.Intensity);
                lightColumn.transform.localScale = new Vector3(r, h, r);
                lightScroll = (lightScroll + dt * LightScrollPerSec) % 1f;
                lightColumn.GetPropertyBlock(block);
                block.SetVector(BaseMapStId, new Vector4(1f, 1f, lightScroll, 0f));
                // No reforço da Mola a luz também fica em brasa: só as runas laranja somem no círculo pequeno.
                Color c = ToColor(blessed ? state.Gold : hurtBonus ? state.Ember : state.Base) * (glow * LightGlow);
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
            Drive(embers, downed ? settings.embersPerSecond * (1f - state.ReviveProgress) : 0f, 0.45f, radius * 0.7f,
                ToColor(state.Ember));

            UpdateBlessing(state, blessed, radius);

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

        /// <summary>
        /// Bênção de dano do 20 (D-085): além da luz e das runas douradas, um anel interno luminoso que respira e faíscas
        /// douradas mais fortes e mais rápidas que as da energia. A forma carrega o sinal quando a cor não basta (D-063, D-065).
        /// </summary>
        private void UpdateBlessing(AuraState state, bool blessed, float radius)
        {
            Color gold = ToColor(state.Gold);
            BlessingColor = gold;
            blessingRing.enabled = blessed;
            if (blessed)
            {
                float breath = Mathf.Sin(Time.time * settings.blessingRingRate * Mathf.PI * 2f);
                float r = Mathf.Max(0.05f, radius * settings.blessingRingRadius * (1f + settings.blessingRingBreath * breath));
                blessingRing.transform.localScale = new Vector3(r, 1f, r);
                float strength = Mathf.Lerp(0.5f, 1f, state.Intensity) * (0.8f + 0.2f * (breath * 0.5f + 0.5f));
                Paint(blessingRing, gold * (settings.blessingRingGlow * strength), 1f);
            }
            Drive(goldSparks, blessed ? settings.blessingSparksPerSecond : 0f, settings.blessingSparkRiseSpeed, radius, gold);
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

        /// <summary>
        /// Levantar um aliado (D-083): as runas acendem uma a uma em sentido horário, como um anel que se completa
        /// (cor Base da paleta, a alternativa também). Cada fatia acende por inteiro numa fração 1/8 do progresso.
        /// Sem número nem barra: o anel e o crescer da aura são o único sinal.
        /// </summary>
        private void UpdateReviveRunes(AuraState state, bool downed)
        {
            float p = downed ? state.ReviveProgress : 0f;
            Color color = ToColor(state.Base);
            int lit = 0;
            for (int i = 0; i < reviveRunes.Length; i++)
            {
                float a = Mathf.Clamp01(p * reviveRunes.Length - i);
                MeshRenderer wedge = reviveRunes[i];
                wedge.enabled = a > 0.001f;
                if (!wedge.enabled)
                    continue;
                lit++;
                wedge.transform.localScale = circle.transform.localScale;
                Paint(wedge, color * (ReviveRuneGlow * a), 1f);
            }
            ReviveRunesLit = lit;
        }

        /// <summary>
        /// Fatia i do disco das runas, centrada na runa i (0 no topo, +Z; sobe de 45° em 45° rumo a +X, sentido horário
        /// visto de cima). Raio 0,5 e UV de 0 a 1 como o TexturedQuad, para pegar a mesma máscara de runas.
        /// </summary>
        private static Mesh ReviveWedge(int index)
        {
            reviveWedges ??= new Mesh[ReviveRuneCount];
            if (reviveWedges[index] != null)
                return reviveWedges[index];

            float span = Mathf.PI * 2f / ReviveRuneCount;
            float start = index * span - span * 0.5f;
            int count = ReviveWedgeArcSteps + 2;
            var vertices = new Vector3[count];
            var uv = new Vector2[count];
            var triangles = new int[ReviveWedgeArcSteps * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int s = 0; s <= ReviveWedgeArcSteps; s++)
            {
                float a = start + span * s / ReviveWedgeArcSteps;
                var p = new Vector3(Mathf.Sin(a) * 0.5f, 0f, Mathf.Cos(a) * 0.5f);
                vertices[s + 1] = p;
                uv[s + 1] = new Vector2(p.x + 0.5f, p.z + 0.5f);
            }
            for (int s = 0; s < ReviveWedgeArcSteps; s++)
            {
                triangles[s * 3] = 0;
                triangles[s * 3 + 1] = s + 2;
                triangles[s * 3 + 2] = s + 1;
            }
            var mesh = new Mesh { name = "AuraFatiaRuna" + index, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            reviveWedges[index] = mesh;
            return mesh;
        }

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
