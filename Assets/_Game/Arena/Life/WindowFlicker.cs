using System.Collections.Generic;
using UnityEngine;

namespace Game.Arena.Life
{
    /// <summary>
    /// Sensação de cidade habitada: de vez em quando uma janela (material "JanelaQuente...") escurece por instantes,
    /// como se alguém passasse na frente do lampião. Se as janelas dividem um único renderer/submesh,
    /// escurece o submesh inteiro por um instante. Usa MaterialPropertyBlock, sem instanciar materiais.
    /// </summary>
    [DisallowMultipleComponent]
    public class WindowFlicker : MonoBehaviour
    {
        private const string WindowMaterialPrefix = "JanelaQuente";

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Quanto da cor original sobra enquanto a janela está "apagada".
        private const float DimEmissionMin = 0.05f;
        private const float DimEmissionMax = 0.3f;
        private const float DimBaseFactor = 0.4f;
        private const float DurationMin = 0.12f;
        private const float DurationMax = 0.6f;

        [SerializeField] private float chancePerSecond = 0.2f;

        private struct Entry
        {
            public Renderer renderer;
            public int materialIndex;
            public Color baseEmission;
            public Color baseColor;
            public bool hasBaseColor;
            public float remaining; // > 0 enquanto escurecida
        }

        private Entry[] entries;
        private MaterialPropertyBlock block;
        private int activeCount;

        /// <summary>chancePerSecond: probabilidade, por segundo, de alguma janela do conjunto escurecer.</summary>
        public void Configure(float chance)
        {
            chancePerSecond = Mathf.Max(0f, chance);
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
            for (int i = 0; i < entries.Length; i++)
                Restore(ref entries[i]);
            activeCount = 0;
        }

        private void Collect()
        {
            block = new MaterialPropertyBlock();
            var list = new List<Entry>();
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend is ParticleSystemRenderer)
                    continue;

                var mats = rend.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null || !mat.name.StartsWith(WindowMaterialPrefix, System.StringComparison.Ordinal))
                        continue;

                    var e = new Entry { renderer = rend, materialIndex = m };
                    e.baseEmission = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black;
                    e.hasBaseColor = mat.HasProperty(BaseColorId);
                    e.baseColor = e.hasBaseColor ? mat.GetColor(BaseColorId) : Color.white;
                    list.Add(e);
                }
            }
            entries = list.ToArray();
        }

        private void Update()
        {
            if (entries == null || entries.Length == 0)
                return;

            float dt = Time.deltaTime;

            // Sorteio: no máximo uma janela nova por quadro.
            if (chancePerSecond > 0f && Random.value < chancePerSecond * dt)
            {
                int idx = Random.Range(0, entries.Length);
                if (entries[idx].renderer != null && entries[idx].remaining <= 0f)
                {
                    entries[idx].remaining = Random.Range(DurationMin, DurationMax);
                    activeCount++;
                    Apply(ref entries[idx], Random.Range(DimEmissionMin, DimEmissionMax));
                }
            }

            if (activeCount <= 0)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].remaining <= 0f)
                    continue;
                entries[i].remaining -= dt;
                if (entries[i].remaining <= 0f)
                {
                    Restore(ref entries[i]);
                    activeCount--;
                }
            }
        }

        private void Apply(ref Entry e, float emissionFactor)
        {
            block.Clear();
            block.SetColor(EmissionColorId, e.baseEmission * emissionFactor);
            if (e.hasBaseColor)
                block.SetColor(BaseColorId, e.baseColor * DimBaseFactor);
            e.renderer.SetPropertyBlock(block, e.materialIndex);
        }

        private static void Restore(ref Entry e)
        {
            if (e.renderer != null)
                e.renderer.SetPropertyBlock(null, e.materialIndex);
            e.remaining = 0f;
        }
    }
}
