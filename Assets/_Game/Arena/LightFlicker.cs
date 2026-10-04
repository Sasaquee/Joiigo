using UnityEngine;

namespace Game.Arena
{
    /// <summary>Faz uma luz arcana falhar de vez em quando: instalação parcialmente funcionando.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float failChance = 0.35f;
        [SerializeField] private float speed = 6f;

        private Light lamp;
        private float baseIntensity;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * speed, transform.position.z);
            lamp.intensity = n < failChance ? baseIntensity * 0.1f : baseIntensity;
        }
    }
}
