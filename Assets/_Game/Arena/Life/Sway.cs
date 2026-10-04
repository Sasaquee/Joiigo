using UnityEngine;

namespace Game.Arena.Life
{
    /// <summary>
    /// Balanço suave em torno de um eixo local, com ruído de Perlin, a partir da rotação inicial.
    /// Serve para placas, lampiões pendurados, bandeiras e cordas.
    /// </summary>
    [DisallowMultipleComponent]
    public class Sway : MonoBehaviour
    {
        [SerializeField] private Vector3 axis = Vector3.forward;
        [SerializeField] private float degrees = 4f;
        [SerializeField] private float speed = 0.6f;

        private Quaternion baseRotation;
        private bool hasBase;
        private float seed;

        /// <summary>axis: eixo local; degrees: amplitude máxima; speed: velocidade do ruído (1 = uma oscilação por segundo, aproximadamente).</summary>
        public void Configure(Vector3 newAxis, float newDegrees, float newSpeed)
        {
            axis = newAxis;
            degrees = newDegrees;
            speed = newSpeed;
        }

        private void Awake()
        {
            seed = Random.value * 100f;
        }

        private void Update()
        {
            // A rotação base é lida no primeiro quadro, depois de qualquer ajuste feito por quem montou a cena.
            if (!hasBase)
            {
                baseRotation = transform.localRotation;
                hasBase = true;
            }

            if (axis.sqrMagnitude < 1e-6f)
                return;

            float t = Time.time * speed + seed;
            // Duas oitavas: balanço principal mais um tremor leve. Resultado em [-1, 1].
            float n = (Mathf.PerlinNoise(t, 0.5f) * 2f - 1f) * 0.8f + (Mathf.PerlinNoise(t * 2.3f, 9.1f) * 2f - 1f) * 0.2f;
            transform.localRotation = baseRotation * Quaternion.AngleAxis(n * degrees, axis.normalized);
        }
    }
}
