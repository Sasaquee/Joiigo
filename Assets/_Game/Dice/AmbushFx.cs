using System.Collections.Generic;
using Game.Cards;
using UnityEngine;

namespace Game.Dice
{
    /// <summary>
    /// Surgimento da emboscada do 1 (D-068), em todos os clientes. Onde cada inimigo nasce, o chão racha em vermelho
    /// (mancha escura, rachaduras e borda de cristal carmesim), sobe uma coluna de luz, estouram poeira e lascas de
    /// cristal, uma onda corre pelo chão e pisca um clarão. Um ronco grave toca uma vez por emboscada.
    /// Só desenho e som: não atrasa nem muda os inimigos (vem do evento Ambushed do próprio CardDropService, enviado no
    /// mesmo quadro em que eles nascem). Formas grandes e cheias, como o resto dos efeitos (FxKit).
    /// Criado e ligado (Bind) pelo CardDropService no mesmo objeto; os números ficam em DiceSettings.
    /// </summary>
    public class AmbushFx : MonoBehaviour
    {
        // Cores do crítico: sangue e carmesim (D-068); poeira de pedra da arena.
        private static readonly Color Crimson = new Color(1f, 0.12f, 0.1f, 1f);
        private static readonly Color CrimsonHot = new Color(1f, 0.45f, 0.3f, 1f);
        private static readonly Color BloodDark = new Color(0.12f, 0.015f, 0.02f, 1f);
        private static readonly Color Dust = new Color(0.42f, 0.38f, 0.35f, 0.85f);
        private static readonly Color DustDark = new Color(0.24f, 0.21f, 0.2f, 0.85f);

        // Desenho (frações do raio do efeito e da vida dele).
        private const float GroundLift = 0.03f;      // acima do chão, para não brigar com o piso
        private const int CrackCount = 6;
        private const float CrackStartWidth = 0.12f; // × raio
        private const float CrackEndWidth = 0.05f;   // × raio
        private const float CrackJitter = 0.12f;     // × raio, desvio lateral de cada quebra
        private const float CrackReach = 1.15f;      // × raio: passa um pouco da borda
        private const float OpenFraction = 0.12f;    // da vida: a rachadura abre rápido
        private const float FadeFrom = 0.6f;         // da vida: começa a sumir
        private const float ScorchAlpha = 0.85f;
        private const float RimInner = 0.82f;
        private const float PillarRadius = 0.7f;     // × raio: raio da coluna no começo (a coluna é um pouco mais estreita que a rachadura)
        private const float WaveReach = 2.2f;        // × raio: a onda corre além da rachadura
        private const float WaveLife = 0.55f;
        private const float PopLife = 0.3f;

        [SerializeField] private DiceSettings settings;

        private CardDropService owner;
        private readonly List<GameObject> active = new List<GameObject>();
        private DiceSettings fallbackSettings;
        private AudioSource source;
        private AudioClip rumble;

        /// <summary>Efeitos de surgimento ainda no chão (para testes).</summary>
        public int ActiveCount
        {
            get
            {
                active.RemoveAll(go => go == null);
                return active.Count;
            }
        }

        /// <summary>Quantos surgimentos foram mostrados (um por inimigo) e quantos roncos tocaram (um por emboscada).</summary>
        public int Shown { get; private set; }
        public int RumblesPlayed { get; private set; }

        /// <summary>
        /// Liga o efeito ao serviço dono (evento de instância Ambushed): cada serviço mostra só as próprias emboscadas,
        /// uma vez, mesmo que existam dois serviços na cena.
        /// </summary>
        public void Bind(CardDropService service)
        {
            if (owner != null)
                owner.Ambushed -= Show;
            owner = service;
            if (owner == null)
                return;
            if (owner.Settings != null)
                settings = owner.Settings;
            if (isActiveAndEnabled)
                Subscribe();
        }

        /// <summary>Uma assinatura só, mesmo se chamado de novo (Bind e OnEnable).</summary>
        private void Subscribe()
        {
            owner.Ambushed -= Show;
            owner.Ambushed += Show;
        }

        private DiceSettings Settings
        {
            get
            {
                if (settings != null)
                    return settings;
                if (fallbackSettings == null)
                {
                    fallbackSettings = ScriptableObject.CreateInstance<DiceSettings>();
                    fallbackSettings.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallbackSettings;
            }
        }

        private void Awake()
        {
            // Som pronto antes da emboscada: gerar o clipe na hora engasgava o quadro em que os inimigos nascem.
            EnsureAudio();
        }

        private void OnEnable()
        {
            if (owner != null)
                Subscribe();
        }

        private void OnDisable()
        {
            if (owner != null)
                owner.Ambushed -= Show;
        }

        private void OnDestroy()
        {
            if (fallbackSettings != null)
                Destroy(fallbackSettings);
            if (rumble != null)
                Destroy(rumble);
        }

        /// <summary>Mostra o surgimento em cada posição e toca o ronco uma vez. Chamado pela rede e pelos testes.</summary>
        public void Show(Vector3[] positions)
        {
            if (positions == null || positions.Length == 0)
                return;
            DiceSettings s = Settings;
            PlayRumble(s);
            for (int i = 0; i < positions.Length; i++)
            {
                if (!FxKit.IsFinite(positions[i]))
                    continue;
                Spawn(positions[i], s);
                Shown++;
            }
        }

        private void PlayRumble(DiceSettings s)
        {
            EnsureAudio();
            source.PlayOneShot(rumble, s.ambushRumbleVolume);
            RumblesPlayed++;
        }

        /// <summary>Fonte de som e clipe do ronco, gerados uma vez (Awake).</summary>
        private void EnsureAudio()
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // aviso para quem joga, ouvido igual em qualquer lugar da arena
            }
            if (rumble == null)
                rumble = CriticalSounds.MakeAmbushRumble();
        }

        private void Spawn(Vector3 position, DiceSettings s)
        {
            float r = s.ambushFxRadius;
            float life = s.ambushFxDuration;
            Vector3 ground = position + Vector3.up * GroundLift;

            GameObject root = FxKit.Root("SurgimentoDaEmboscada", ground, Quaternion.identity, life);
            active.Add(root);

            // Mancha escura onde o chão abriu (por baixo de tudo).
            MeshRenderer scorch = FxKit.MeshChild(root.transform, "Mancha", FxKit.Disc, FxKit.FlatAlpha);
            scorch.sortingOrder = 0;

            // Borda de cristal carmesim em volta da mancha.
            MeshRenderer rim = FxKit.MeshChild(root.transform, "Borda", FxKit.Ring(RimInner), FxKit.FlatAdditive);
            rim.transform.localPosition = Vector3.up * 0.01f;
            rim.sortingOrder = 1;

            // Rachaduras: linhas quebradas do centro para fora, grossas no meio e finas na ponta.
            var cracks = new LineRenderer[CrackCount];
            var crackPoints = new Vector3[CrackCount][];
            for (int i = 0; i < CrackCount; i++)
            {
                float angle = (i * 360f / CrackCount + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
                crackPoints[i] = CrackShape(angle, r);
                cracks[i] = FxKit.Line(root.transform, "Rachadura", crackPoints[i].Length, CrackStartWidth * r, CrackEndWidth * r);
                cracks[i].sortingOrder = 1;
            }

            // Coluna de luz vermelha subindo do chão (o cilindro primitivo tem altura 2 e raio 0,5).
            float height = s.ambushFxPillarHeight;
            MeshRenderer pillar = height > 0.01f
                ? FxKit.MeshChild(root.transform, "Coluna", FxKit.Cylinder, FxKit.FlatAdditive)
                : null;
            if (pillar != null)
            {
                pillar.transform.localPosition = Vector3.up * (height * 0.5f);
                pillar.sortingOrder = 2;
            }
            float pillarShare = Mathf.Clamp01(s.ambushFxPillarTime / Mathf.Max(0.01f, life));

            var block = new MaterialPropertyBlock();
            var visual = root.GetComponent<CardVisualLife>();
            visual.OnTick = t =>
            {
                float open = Mathf.Clamp01(t / OpenFraction);
                open = 1f - (1f - open) * (1f - open);
                float fade = t < FadeFrom ? 1f : 1f - (t - FadeFrom) / (1f - FadeFrom);

                float size = r * Mathf.Max(0.05f, open);
                scorch.transform.localScale = new Vector3(size, 1f, size);
                FxKit.Paint(scorch, block, BloodDark, ScorchAlpha * fade);

                rim.transform.localScale = new Vector3(size, 1f, size);
                FxKit.Paint(rim, block, Crimson, fade);

                for (int i = 0; i < CrackCount; i++)
                {
                    Vector3[] points = crackPoints[i];
                    for (int p = 0; p < points.Length; p++)
                        cracks[i].SetPosition(p, ground + Vector3.up * 0.02f + points[p] * open);
                    FxKit.Paint(cracks[i], block, Color.Lerp(CrimsonHot, Crimson, t), fade);
                }

                if (pillar != null)
                {
                    float k = pillarShare > 0f ? t / pillarShare : 1f;
                    if (k >= 1f)
                    {
                        if (pillar.enabled)
                            pillar.enabled = false;
                    }
                    else
                    {
                        // O cilindro primitivo tem diâmetro 1: a escala é o diâmetro (2 × raio).
                        float w = PillarRadius * r * 2f * (1f - k * k);
                        pillar.transform.localScale = new Vector3(w, height * 0.5f, w);
                        FxKit.Paint(pillar, block, CrimsonHot, 1f - k);
                    }
                }
            };
            visual.OnTick(0f);

            // Estouro: clarão vermelho, disco quente no centro, onda pelo chão, poeira e lascas de cristal.
            FxKit.Flash(ground + Vector3.up * 1.2f, Crimson, s.ambushFxFlashIntensity, r * 4f, s.ambushFxFlashTime);
            FxKit.Pop(ground + Vector3.up * 0.02f, CrimsonHot, r * 1.1f, PopLife, flat: true);
            FxKit.GroundRing(ground + Vector3.up * 0.04f, r * WaveReach, Crimson, WaveLife);
            FxKit.Puffs(root.transform, ground + Vector3.up * 0.3f, 10, 1.2f, 2.6f, 0.45f, 0.8f, Dust, DustDark,
                0.6f, 1.1f, -0.05f, r * 0.5f);
            FxKit.Sparks(root.transform, ground + Vector3.up * 0.2f, 14, 3.5f, 6.5f, 0.12f, 0.22f, Crimson, CrimsonHot,
                0.4f, 0.8f, 1.6f, 0.3f);
        }

        /// <summary>Quatro pontos de uma rachadura, relativos ao centro: do meio para fora, com quebras para os lados.</summary>
        private static Vector3[] CrackShape(float angle, float radius)
        {
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var side = new Vector3(dir.z, 0f, -dir.x);
            float jitter = CrackJitter * radius;
            return new[]
            {
                dir * (0.15f * radius),
                dir * (0.45f * radius) + side * Random.Range(-jitter, jitter),
                dir * (0.78f * radius) + side * Random.Range(-jitter, jitter),
                dir * (CrackReach * radius) + side * Random.Range(-jitter * 0.5f, jitter * 0.5f)
            };
        }
    }
}
