using UnityEngine;

namespace Game.Arena
{
    /// <summary>
    /// Oscila a intensidade de uma luz de fogo de forma suave com ruído de Perlin, sem falhas bruscas.
    /// Diferente do LightFlicker, que simula uma instalação falhando.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.6f)] private float amplitude = 0.2f;
        [SerializeField] private float speed = 2.5f;
        [SerializeField, Range(0f, 0.3f)] private float rangeAmplitude = 0.08f;

        private Light lamp;
        private float baseIntensity;
        private float baseRange;
        private float seed;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            baseRange = lamp.range;
            // Cada fornalha usa um trecho diferente do ruído para não piscarem em sincronia.
            Vector3 p = transform.position;
            seed = Mathf.Abs(p.x * 0.37f + p.z * 0.53f) + 3.1f;
        }

        private void Update()
        {
            float t = Time.time * speed;
            // Duas oitavas: variação lenta mais um tremor mais rápido. Resultado em [-1, 1].
            float n = Mathf.PerlinNoise(t, seed) * 0.65f + Mathf.PerlinNoise(t * 2.7f, seed + 11f) * 0.35f;
            float k = (n - 0.5f) * 2f;
            lamp.intensity = baseIntensity * (1f + k * amplitude);
            lamp.range = baseRange * (1f + k * rangeAmplitude);
        }
    }
}
