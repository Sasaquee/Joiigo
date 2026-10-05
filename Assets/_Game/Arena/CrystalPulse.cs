using System.Collections.Generic;
using Game.Core.Ambience;
using Game.Net;
using UnityEngine;

namespace Game.Arena
{
    public enum CrystalPulseRole { Veio, Maquina, Torre }

    /// <summary>
    /// Cristal vivo (D-069): pulsa a emissão das partes em CristalArcano de um conjunto de renderers e, se houver,
    /// a luz junto. Nos veios, cada renderer tem um deslocamento em metros e a crista do pulso corre pelo veio
    /// (a energia sai da caldeira e corre pelo cano). Os números vêm de CrystalAmbienceSettings, lidos a cada quadro.
    /// Com uma MatchState ligada, o conjunto fica com o material apagado até a largada (como o portão, D-013).
    /// A conta da onda fica no Core (CrystalWave), testada sem Unity.
    /// Usa MaterialPropertyBlock, sem instanciar material. Não use junto com EmissivePulse/WindowFlicker no mesmo
    /// renderer e índice de material (os três escrevem _EmissionColor).
    /// </summary>
    [DisallowMultipleComponent]
    public class CrystalPulse : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CrystalAmbienceSettings settings;
        [SerializeField] private CrystalPulseRole role = CrystalPulseRole.Veio;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [Tooltip("Deslocamento (m) de cada renderer ao longo do veio. Vazio = todos em 0.")]
        [SerializeField] private float[] offsets = new float[0];
        [SerializeField] private Light[] lights = new Light[0];
        [SerializeField] private Material litMaterial;
        [Tooltip("Opcional: material enquanto a partida não começou (só com MatchState).")]
        [SerializeField] private Material offMaterial;
        [SerializeField] private MatchState session;

        private struct Entry
        {
            public Renderer renderer;
            public int materialIndex;
            public float offset;
        }

        private Entry[] entries;
        private float[] lightBase;
        private MaterialPropertyBlock block;
        private Color baseEmission;
        private float phase;
        private bool? lit;

        public CrystalPulseRole Role => role;
        public int RendererCount => renderers != null ? renderers.Length : 0;
        public IReadOnlyList<Renderer> Renderers => renderers;
        public int LightCount => lights != null ? lights.Length : 0;
        public bool IsLit => lit ?? false;
        public bool WaitsForStart => session != null && offMaterial != null;

        public void Configure(CrystalAmbienceSettings newSettings, CrystalPulseRole newRole, Renderer[] newRenderers,
            float[] newOffsets, Light[] newLights, Material lit, Material off = null, MatchState newSession = null)
        {
            settings = newSettings;
            role = newRole;
            renderers = newRenderers ?? new Renderer[0];
            offsets = newOffsets ?? new float[0];
            lights = newLights ?? new Light[0];
            litMaterial = lit;
            offMaterial = off;
            session = newSession;
        }

        private void Awake()
        {
            if (settings == null)
                settings = ScriptableObject.CreateInstance<CrystalAmbienceSettings>(); // valores padrão
            block = new MaterialPropertyBlock();
            // Veios em fase com o mundo (a crista corre de uma peça para a outra); torres e máquinas cada uma no seu tempo.
            phase = role == CrystalPulseRole.Veio ? 0f : Random.value;
            baseEmission = litMaterial != null && litMaterial.HasProperty(EmissionColorId)
                ? litMaterial.GetColor(EmissionColorId)
                : Color.black;
            Collect();
        }

        private void Collect()
        {
            var list = new List<Entry>();
            var mats = new List<Material>();
            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend == null)
                    continue;
                rend.GetSharedMaterials(mats);
                for (int m = 0; m < mats.Count; m++)
                {
                    if (mats[m] == null || (mats[m] != litMaterial && (offMaterial == null || mats[m] != offMaterial)))
                        continue;
                    list.Add(new Entry { renderer = rend, materialIndex = m, offset = r < offsets.Length ? offsets[r] : 0f });
                }
            }
            entries = list.ToArray();

            lightBase = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                lightBase[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        private void OnDisable()
        {
            if (entries == null)
                return;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].renderer != null)
                    entries[i].renderer.SetPropertyBlock(null, entries[i].materialIndex);
            }
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].intensity = lightBase[i];
            }
            lit = null;
        }

        private void Update()
        {
            if (entries == null)
                return;

            bool on = !WaitsForStart || session.IsStarted;
            if (lit != on)
                SetLit(on);
            if (!on)
                return;

            GetNumbers(out float min, out float max, out float speed);
            bool vein = role == CrystalPulseRole.Veio;
            float sharpness = vein ? CrystalWave.VeinSharpness : CrystalWave.BreathSharpness;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.renderer == null)
                    continue;
                float cycle = CrystalWave.Cycle(Time.time, speed, phase, vein ? e.offset : 0f, settings.veinWaveLength);
                float factor = CrystalWave.Glow(cycle, min, max, sharpness);
                block.Clear();
                block.SetColor(EmissionColorId, baseEmission * factor);
                e.renderer.SetPropertyBlock(block, e.materialIndex);
            }

            if (lights.Length > 0)
            {
                float amplitude = role == CrystalPulseRole.Torre ? settings.towerLightAmplitude : 0f;
                float k = CrystalWave.LightFactor(CrystalWave.Cycle(Time.time, speed, phase, 0f, 1f), amplitude);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                        lights[i].intensity = lightBase[i] * k;
                }
            }
        }

        private void GetNumbers(out float min, out float max, out float speed)
        {
            switch (role)
            {
                case CrystalPulseRole.Torre:
                    min = settings.towerMinGlow; max = settings.towerMaxGlow; speed = settings.towerPulseSpeed;
                    break;
                case CrystalPulseRole.Maquina:
                    min = settings.machineMinGlow; max = settings.machineMaxGlow; speed = settings.machinePulseSpeed;
                    break;
                default:
                    min = settings.veinMinGlow; max = settings.veinMaxGlow; speed = settings.veinPulseSpeed;
                    break;
            }
        }

        /// <summary>Troca entre o material aceso e o apagado (só nos conjuntos que esperam a largada).</summary>
        private void SetLit(bool on)
        {
            lit = on;
            if (!WaitsForStart)
                return;

            Material from = on ? offMaterial : litMaterial;
            Material to = on ? litMaterial : offMaterial;
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == from)
                    {
                        mats[i] = to;
                        changed = true;
                    }
                }
                if (changed)
                    r.sharedMaterials = mats;
            }
            if (!on)
            {
                foreach (var e in entries)
                {
                    if (e.renderer != null)
                        e.renderer.SetPropertyBlock(null, e.materialIndex);
                }
            }
        }
    }
}
