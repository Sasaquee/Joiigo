using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Escudo de latão à frente (Broquel Cantante). Liga o PlayerShield do jogador: projéteis inimigos que
    /// chegam pela frente são bloqueados e cada bloqueio devolve um pulso arcano em área. Corpo a corpo passa.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Effects/Shield", fileName = "ShieldEffect")]
    public class ShieldEffect : CardEffect
    {
        [Min(0.1f)] public float duration = 2f;
        [Tooltip("Dano do pulso devolvido a cada projétil bloqueado.")]
        [Min(0f)] public float pulseDamage = 15f;
        [Range(0f, 1f)] public float arcaneFraction = 0.7f;
        [Tooltip("Raio do pulso em volta do jogador (m).")]
        [Min(0.5f)] public float radius = 3f;
        [Tooltip("Até onde, à frente do jogador, o escudo pega projéteis (m).")]
        [Min(0.5f)] public float blockReach = 1.5f;
        [Tooltip("Meia-abertura da zona que bloqueia, à frente do jogador (graus).")]
        [Range(20f, 120f)] public float blockHalfAngle = 65f;

        public override void Execute(ICardUser user, CardData card)
        {
            var shield = user.Transform.GetComponent<PlayerShield>();
            if (shield == null)
                shield = user.Transform.gameObject.AddComponent<PlayerShield>();

            shield.Activate(user, duration, pulseDamage, arcaneFraction, radius, blockReach, blockHalfAngle);
            user.BroadcastVisual("shield", user.Transform.position, user.Transform.forward, duration);
        }
    }
}
