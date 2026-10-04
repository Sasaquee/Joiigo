using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Vida curta de um visual de carta: anima por OnTick (0 a 1), pode seguir um Transform
    /// (escudo) e destrói o objeto ao fim. Só desenho: nada aqui afeta o jogo.
    /// </summary>
    public class CardVisualLife : MonoBehaviour
    {
        public float Life = 1f;
        public System.Action<float> OnTick;

        public Transform Follow;
        public Vector3 FollowOffset;

        private float age;
        private bool hadFollow;

        private void Update()
        {
            age += Time.deltaTime;
            float t = Life > 0f ? Mathf.Clamp01(age / Life) : 1f;
            OnTick?.Invoke(t);
            if (age >= Life)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            if (Follow == null)
            {
                if (hadFollow)
                    Destroy(gameObject); // quem ele seguia sumiu
                return;
            }
            hadFollow = true;
            transform.SetPositionAndRotation(Follow.TransformPoint(FollowOffset), Follow.rotation);
        }

        /// <summary>Coloca o objeto já na posição de quem ele segue (antes do primeiro LateUpdate).</summary>
        public void SnapToFollow()
        {
            if (Follow == null)
                return;
            hadFollow = true;
            transform.SetPositionAndRotation(Follow.TransformPoint(FollowOffset), Follow.rotation);
        }
    }
}
