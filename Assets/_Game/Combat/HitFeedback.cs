using Game.Cameras;
using Game.Cards;
using Game.Enemies;
using Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Retorno visual de cada acerto (D-042), em todos os clientes: faíscas grossas e um estouro branco no ponto do dano
    /// (laranja e branco quando o golpe é mais mecânico, ciano quando é mais arcano), tremor de câmera e, só no solo,
    /// hit-stop quando o jogador local acerta. Liga-se sozinho ao NetworkHealth.AnyDamaged no início do jogo,
    /// então não precisa de nada na cena. Só desenho: nunca altera vida nem posição.
    /// </summary>
    public static class HitFeedback
    {
        // Altura do peito, onde a faísca nasce.
        private const float ChestHeight = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            NetworkHealth.AnyDamaged -= OnAnyDamaged;
            NetworkHealth.AnyDamaged += OnAnyDamaged;
        }

        private static void OnAnyDamaged(NetworkHealth victim, float applied, float arcaneFraction, ulong attackerClientId)
        {
            if (victim == null || applied <= 0f)
                return;

            try
            {
                bool victimIsPlayer = victim.GetComponentInParent<PlayerCombat>() != null;
                Vector3 point = victim.transform.position + Vector3.up * ChestHeight;

                SpawnSparks(point, victim.Radius, applied, arcaneFraction, victimIsPlayer);

                NetworkManager manager = NetworkManager.Singleton;
                if (victimIsPlayer)
                {
                    // O jogador local levou dano: tremor médio. Quem viu o aliado levar dano só vê as faíscas.
                    if (victim.IsOwner)
                        CameraShake.Add(CameraShake.Medium, 0.28f);
                    return;
                }

                bool victimIsEnemy = victim.GetComponentInParent<EnemyController>() != null;
                bool localAttacker = manager != null && manager.IsListening && attackerClientId == manager.LocalClientId;
                if (victimIsEnemy && localAttacker)
                {
                    CameraShake.Add(CameraShake.Small, 0.12f);
                    HitStop.Request(0.05f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Retorno do acerto falhou: {e.Message}"); // desenho nunca quebra o jogo
            }
        }

        /// <summary>Faíscas grossas (quadrados opacos) + estouro branco + clarão curto. Poucas partículas, grandes.</summary>
        public static void SpawnSparks(Vector3 point, float bodyRadius, float amount, float arcaneFraction, bool onPlayer = false)
        {
            if (!FxKit.IsFinite(point))
                return;

            bool arcane = arcaneFraction >= 0.5f;
            Color hot = arcane ? FxKit.CyanWhite : FxKit.WhiteHot;
            Color body = arcane ? FxKit.CyanBright : FxKit.EmberOrange;
            if (onPlayer && !arcane)
                body = new Color(1f, 0.38f, 0.22f, 1f); // dano no jogador: mais avermelhado, para destacar

            int count = Mathf.Clamp(Mathf.RoundToInt(4f + amount * 0.18f), 5, 12);
            float spread = Mathf.Clamp(bodyRadius, 0.2f, 1f);

            GameObject root = FxKit.Root("Fx_Acerto", point, Quaternion.identity, 0.7f);
            FxKit.Sparks(root.transform, point, count, 3f, 7f, 0.1f, 0.2f, hot, body, 0.22f, 0.42f, 1.8f, spread * 0.5f);

            // Estouro branco grande e curto: o "tapa" visual do golpe. Uma estrela quadrada de partícula aditiva.
            var star = FxKit.Emitter(root.transform, "Estouro", point, Quaternion.identity, FxKit.GlowParticle);
            FxKit.Configure(star, 0.1f, 0.09f, 0.11f, 0f, 0f, 0.85f, 0.85f, hot, hot);
            var starSize = star.sizeOverLifetime;
            starSize.enabled = true;
            starSize.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            FxKit.Shape(star, ParticleSystemShapeType.Sphere, radius: 0.01f);
            FxKit.Burst(star, 1);
            star.Play();

            FxKit.Flash(point, body, 3f, 4f, 0.12f);
        }
    }
}
