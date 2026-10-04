using UnityEngine;

namespace Game.Arena
{
    /// <summary>Gira uma peça do cenário (engrenagens). Com engasgo, a máquina parece funcionar só em parte.</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] private Vector3 axis = Vector3.forward;
        [SerializeField] private float degreesPerSecond = 30f;

        [Tooltip("0 = gira liso. Acima de 0, a peça trava e solta em ciclos.")]
        [SerializeField, Range(0f, 1f)] private float stutter;

        public void Configure(Vector3 newAxis, float speed, float newStutter)
        {
            axis = newAxis;
            degreesPerSecond = speed;
            stutter = newStutter;
        }

        private void Update()
        {
            float factor = 1f;
            if (stutter > 0f)
                factor = Mathf.PerlinNoise(Time.time * 0.7f, transform.position.x) > stutter ? 1f : 0f;

            transform.Rotate(axis, degreesPerSecond * factor * Time.deltaTime, Space.Self);
        }
    }
}
