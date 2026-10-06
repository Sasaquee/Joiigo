using UnityEngine;

namespace Game.Cameras
{
    /// <summary>
    /// Números da translucidez dos prédios (D-076, D-079): quando um prédio tapa jogador ou inimigo, o shader recorta um
    /// círculo nele em volta do alvo e deixa um fantasma. Lido pelo SeeThroughDriver; o shader recebe os valores por globais.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Camera/See Through Settings", fileName = "SeeThroughSettings")]
    public class SeeThroughSettings : ScriptableObject
    {
        [Tooltip("Quantos alvos tapados podem ter buraco ao mesmo tempo (o array do shader tem 12).")]
        [Range(1, 12)] public int maxTargets = 12;

        [Tooltip("Raio do buraco em volta de um jogador (m, no mundo, na profundidade do jogador).")]
        [Min(0.1f)] public float playerRadius = 1.8f;

        [Tooltip("Raio do buraco em volta de um inimigo (m).")]
        [Min(0.1f)] public float enemyRadius = 1.4f;

        [Tooltip("Só é cortado o que está mais perto da câmera que o alvo menos esta margem (m). Mantém a parede de fundo.")]
        [Min(0f)] public float depthMargin = 0.8f;

        [Tooltip("Tudo a menos desta distância da câmera é cortado (rede de segurança, m).")]
        [Min(0f)] public float nearCut = 3f;

        [Tooltip("Tempo do buraco para abrir quando o alvo fica tapado (s).")]
        [Min(0f)] public float fadeInSeconds = 0.12f;

        [Tooltip("Tempo do buraco para fechar quando o alvo aparece (s).")]
        [Min(0f)] public float fadeOutSeconds = 0.3f;

        [Tooltip("Opacidade do fantasma do prédio desenhado dentro do buraco (0..1).")]
        [Range(0f, 1f)] public float ghostOpacity = 0.25f;

        [Tooltip("Alturas dos pontos de checagem acima dos pés (m): pés, tronco, cabeça. O alvo está tapado se qualquer uma for bloqueada. A do meio é o centro do buraco.")]
        public Vector3 checkHeights = new Vector3(0.4f, 1.2f, 1.9f);

        [Tooltip("De quanto em quanto tempo a lista de jogadores e inimigos vivos é refeita (s).")]
        [Min(0.02f)] public float targetScanInterval = 0.25f;
    }
}
