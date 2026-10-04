using UnityEngine;

namespace Game.Arena.Life
{
    /// <summary>
    /// Pulsa a emissão de todos os renderers filhos cujo material tem _EMISSION ativo (cristais, janelas, fornalhas).
    /// Usa MaterialPropertyBlock, sem instanciar materiais. Fase aleatória por instância.
    /// Atenção: compartilha o _EmissionColor do bloco com o WindowFlicker; não use os dois no mesmo renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public class EmissivePulse : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Ciclos por segundo.")]
        [SerializeField] private float speed = 0.6f;
        [SerializeField] private float minFactor = 0.6f;
        [SerializeField] private float maxFactor = 1.2f;

        private struct Entry
        {
            public Renderer renderer;
            public int materialIndex;
            public Color baseEmission;
        }

        private Entry[] entries;
        private MaterialPropertyBlock block;
        private float phase;

        /// <summary>speed em ciclos por segundo; min e max são multiplicadores da emissão original.</summary>
        public void Configure(float newSpeed, float min, float max)
        {
            speed = newSpeed;
            minFactor = Mathf.Min(min, max);
            maxFactor = Mathf.Max(min, max);
        }

        private void Awake()
        {
            phase = Random.value * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            if (entries == null)
                Collect();
        }

        private void OnDisable()
        {
            if (entries == null)
                return;
            // Devolve os renderers ao material original.
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].renderer != null)
                    entries[i].renderer.SetPropertyBlock(null, entries[i].materialIndex);
            }
        }

        private void Collect()
        {
            block = new MaterialPropertyBlock();
            var renderers = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Entry>();
            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                    continue;

                var mats = rend.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null || !mat.HasProperty(EmissionColorId) || !mat.IsKeywordEnabled("_EMISSION"))
                        continue;
                    Color e = mat.GetColor(EmissionColorId);
                    if (e.maxColorComponent <= 0f)
                        continue;
                    list.Add(new Entry { renderer = rend, materialIndex = m, baseEmission = e });
                }
            }
            entries = list.ToArray();
        }

        private void Update()
        {
            if (entries == null || entries.Length == 0)
                return;

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f + phase);
            float factor = Mathf.Lerp(minFactor, maxFactor, wave);

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.renderer == null)
                    continue;
                // O bloco é reaproveitado: SetPropertyBlock copia os valores para o renderer.
                block.Clear();
                block.SetColor(EmissionColorId, e.baseEmission * factor);
                e.renderer.SetPropertyBlock(block, e.materialIndex);
            }
        }
    }
}
