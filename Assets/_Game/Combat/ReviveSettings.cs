using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Números provisórios de levantar um aliado caído (D-083): o aliado chega perto e segura E.
    /// Asset em Data/Combat/ReviveSettings.asset (criado pelo ReviveSetup).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Combat/Revive Settings", fileName = "ReviveSettings")]
    public class ReviveSettings : ScriptableObject
    {
        [Tooltip("Tempo que o aliado precisa segurar E para levantar quem caiu (s).")]
        [Min(0.1f)] public float holdSeconds = 2f;
        [Tooltip("Distância máxima (no plano) entre o aliado e quem caiu para levantar (m).")]
        [Min(0.1f)] public float reviveRadius = 2.5f;
        [Tooltip("Ao soltar E ou sair do raio, o progresso cai de cheio a zero neste tempo (s). 0 = zera na hora.")]
        [Min(0f)] public float graceAfterRelease = 0.3f;
        [Tooltip("Com que fração da vida máxima quem foi levantado volta (0 a 1). Levantar com a vida cheia é o spawn (D-003).")]
        [Range(0.05f, 1f)] public float reviveHealthFraction = 0.5f;
        [Tooltip("O aliado que levanta fica parado e sem atacar enquanto segura E (exposto, D-083).")]
        public bool rescuerFrozen = true;
    }
}
