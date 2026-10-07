using Game.Core.Session;
using UnityEngine;

namespace Game.Net
{
    /// <summary>Números provisórios do recomeço da partida depois da queda total (D-084, D-086, D-087).</summary>
    [CreateAssetMenu(menuName = "Game/Net/Match Settings", fileName = "MatchSettings")]
    public class MatchSettings : ScriptableObject
    {
        [Header("Queda total: escurece e volta (D-086)")]
        [Tooltip("Tempo que a tela leva para escurecer por completo, em todos os clientes (s).")]
        [Min(0f)] public float wipeFadeSeconds = 2f;
        [Tooltip("Tempo que o preto fica inteiro enquanto a arena zera, antes de clarear (s).")]
        [Min(0f)] public float wipeHoldBlackSeconds = 0.6f;
        [Tooltip("Tempo que a tela leva para clarear depois do recomeço (s).")]
        [Min(0f)] public float wipeFadeInSeconds = 1f;
        [Tooltip("Volume do som grave que toca ao escurecer (0 a 1).")]
        [Range(0f, 1f)] public float wipeSoundVolume = 0.8f;

        /// <summary>Sequência nova com os tempos atuais (o host cria uma por queda total).</summary>
        public WipeSequence CreateSequence() => new WipeSequence(wipeFadeSeconds, wipeHoldBlackSeconds);
    }
}
