using System;
using System.Collections.Generic;
using Game.Core.Cards;
using Game.Core.Dice;
using UnityEngine;

namespace Game.Dice
{
    /// <summary>Uma faixa da tabela do D20, editável no Inspector (D-048).</summary>
    [Serializable]
    public struct DiceBandEntry
    {
        [Range(1, D20.Faces)] public int min;
        [Range(1, D20.Faces)] public int max;
        public Rarity rarity;
        public CardQuality quality;
        [Min(0)] public int extraCommons;
        public CardQuality extraQuality;
        [Tooltip("Crítico 1: a carta é amaldiçoada.")]
        public bool cursed;
        [Tooltip("Crítico 1: atrai perigo (emboscada, D-049).")]
        public bool danger;
        [Tooltip("Crítico 20: arcano maior ou carta única.")]
        public bool majorArcana;

        public DiceBand ToCore() => new DiceBand(min, max,
            new DiceOutcome(rarity, quality, extraCommons, extraQuality, cursed, danger, majorArcana));
    }

    /// <summary>
    /// Números provisórios da carta do chão e do D20 (Fase 6). O D20 em si é puro (D20 no Core);
    /// aqui ficam só a tabela, o peso do caminho e o perigo do 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dice/Dice Settings", fileName = "DiceSettings")]
    public class DiceSettings : ScriptableObject
    {
        [Header("Tabela do D20 (D-048)")]
        public DiceBandEntry[] bands = DefaultBands();

        [Header("Tema (caminho + lugar)")]
        [Tooltip("Peso extra das tags que o jogador mais usa no sorteio do tema (0 = o caminho não pesa).")]
        [Min(0f)] public float pathBias = 2f;
        [Tooltip("Quantas das tags mais usadas contam como o caminho do jogador.")]
        [Min(0)] public int pathTopTags = 2;

        [Header("Rolagem (D-047)")]
        [Tooltip("Quanto tempo o dado rola na tela até parar (s).")]
        [Min(0.5f)] public float rollDuration = 2.2f;
        [Tooltip("Depois que o dado para, espera isto antes de entregar a carta (s).")]
        [Min(0f)] public float grantDelay = 0.6f;

        [Header("Emboscada do 1 (D-049)")]
        [Min(0)] public int ambushMin = 2;
        [Min(0)] public int ambushMax = 3;
        [Tooltip("Distância do jogador onde os inimigos da emboscada surgem (m).")]
        [Min(1f)] public float ambushRadius = 5f;
        [Tooltip("Raio para puxar o ponto da emboscada até o chão andável da NavMesh (m). Nunca nasce dentro de prédio (passe do mapa).")]
        [Min(0.1f)] public float ambushNavSampleRadius = 2.5f;
        [Tooltip("Tentativas por inimigo até achar ponto andável e alcançável a partir do jogador. A cada 3 tentativas o raio encolhe " +
                 "(ambushRetryRadiusFactor); dentro de cada trio, 0°, +ambushRetryAngle e -ambushRetryAngle.")]
        [Min(1)] public int ambushTries = 6;
        [Tooltip("Nas tentativas seguintes, quanto o ângulo gira para um lado e para o outro (graus).")]
        [Range(0f, 90f)] public float ambushRetryAngle = 30f;
        [Tooltip("A cada trio de tentativas o raio da emboscada fica multiplicado por isto.")]
        [Range(0.1f, 1f)] public float ambushRetryRadiusFactor = 0.7f;

        [Header("Carta do fim da onda (D-082)")]
        [Tooltip("Se o ponto onde caiu o último inimigo não é andável, a carta vai para o ponto andável mais próximo dentro deste raio (m).")]
        [Min(0.1f)] public float dropNavSampleRadius = 4f;

        [Header("O 1 teatral (D-068): só apresentação, a regra do 1 não muda")]
        [Tooltip("Fração da rolagem em que o dado começa a avermelhar e a tela a escurecer (0 = desde o início; 1 = só quando para).")]
        [Range(0f, 1f)] public float criticalTintStart = 0.7f;
        [Tooltip("Quanto o véu escuro cobre a tela no máximo (0 a 1). O jogo continua visível por trás (D-047).")]
        [Range(0f, 1f)] public float veilOpacity = 0.55f;
        [Tooltip("Força das bordas vermelhas da tela no máximo (0 a 1).")]
        [Range(0f, 1f)] public float vignetteOpacity = 0.85f;
        [Tooltip("Depois que o dado para no 1, o véu fica cheio por isto (s). Nunca menos que a chegada da emboscada " +
                 "(grantDelay + veilAmbushMargin) nem que o tempo do dado sumir da tela: o código trava nesses mínimos.")]
        [Min(0f)] public float veilHold = 1.5f;
        [Tooltip("Folga depois da chegada da emboscada (grantDelay) antes de o véu poder começar a sumir (s); cobre o atraso da rede.")]
        [Min(0f)] public float veilAmbushMargin = 0.5f;
        [Tooltip("Quanto o véu leva para sumir (s).")]
        [Min(0.05f)] public float veilFadeOut = 0.9f;
        [Tooltip("Pulso das bordas vermelhas no baque do dado (s); 0 = sem pulso.")]
        [Min(0f)] public float criticalPulseTime = 0.5f;
        [Tooltip("Volume do baque grave quando o dado assenta no 1 (0 a 1).")]
        [Range(0f, 1f)] public float criticalThudVolume = 0.9f;
        [Tooltip("Volume do ronco grave quando os inimigos da emboscada surgem (0 a 1).")]
        [Range(0f, 1f)] public float ambushRumbleVolume = 0.8f;
        [Tooltip("Raio da rachadura vermelha no chão onde cada inimigo da emboscada surge (m).")]
        [Min(0.1f)] public float ambushFxRadius = 1.4f;
        [Tooltip("Quanto a rachadura fica no chão até sumir (s).")]
        [Min(0.2f)] public float ambushFxDuration = 1.8f;
        [Tooltip("Altura da coluna de luz vermelha que sobe onde o inimigo surge (m).")]
        [Min(0f)] public float ambushFxPillarHeight = 3f;
        [Tooltip("Quanto dura a coluna de luz (s).")]
        [Min(0.05f)] public float ambushFxPillarTime = 0.55f;
        [Tooltip("Intensidade do clarão vermelho no surgimento (luz pontual).")]
        [Min(0f)] public float ambushFxFlashIntensity = 8f;
        [Tooltip("Quanto dura o clarão vermelho (s).")]
        [Min(0.05f)] public float ambushFxFlashTime = 0.45f;

        public DiceTable CreateTable()
        {
            var list = new List<DiceBand>();
            foreach (var b in bands)
                list.Add(b.ToCore());
            return new DiceTable(list);
        }

        /// <summary>D-048: 1 maldita + perigo · 2–6 comum gasta · 7–11 comum boa · 12–15 incomum boa · 16–18 incomum perfeita · 19 incomum perfeita + 1 comum · 20 arcano maior.</summary>
        public static DiceBandEntry[] DefaultBands() => new[]
        {
            new DiceBandEntry { min = 1, max = 1, rarity = Rarity.Common, quality = CardQuality.Worn, cursed = true, danger = true },
            new DiceBandEntry { min = 2, max = 6, rarity = Rarity.Common, quality = CardQuality.Worn },
            new DiceBandEntry { min = 7, max = 11, rarity = Rarity.Common, quality = CardQuality.Good },
            new DiceBandEntry { min = 12, max = 15, rarity = Rarity.Uncommon, quality = CardQuality.Good },
            new DiceBandEntry { min = 16, max = 18, rarity = Rarity.Uncommon, quality = CardQuality.Perfect },
            new DiceBandEntry { min = 19, max = 19, rarity = Rarity.Uncommon, quality = CardQuality.Perfect, extraCommons = 1, extraQuality = CardQuality.Good },
            new DiceBandEntry { min = 20, max = 20, rarity = Rarity.Unique, quality = CardQuality.Perfect, majorArcana = true },
        };
    }
}
