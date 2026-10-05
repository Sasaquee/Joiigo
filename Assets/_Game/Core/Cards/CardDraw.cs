using System;
using System.Collections.Generic;
using Game.Core.Dice;

namespace Game.Core.Cards
{
    /// <summary>Uma carta que pode sair do chão, vista pelo sorteio.</summary>
    public readonly struct DrawCandidate
    {
        public readonly int Id;
        public readonly Rarity Rarity;
        public readonly bool Cursed;
        public readonly bool Major;
        public readonly IReadOnlyList<string> Tags;

        public DrawCandidate(int id, Rarity rarity, bool cursed, bool major, IReadOnlyList<string> tags)
        {
            Id = id;
            Rarity = rarity;
            Cursed = cursed;
            Major = major;
            Tags = tags ?? Array.Empty<string>();
        }
    }

    public enum GrantKind
    {
        New,      // carta que ele não tinha
        Upgrade,  // repetida: sobe um degrau de qualidade (D-051)
        Copy      // repetida de carta já perfeita: cópia extra (D-052)
    }

    /// <summary>Uma carta entregue pelo resultado do dado.</summary>
    public readonly struct CardGrant
    {
        public readonly int CardId;
        public readonly GrantKind Kind;
        /// <summary>Qualidade final da carta depois da entrega.</summary>
        public readonly CardQuality Quality;

        public CardGrant(int cardId, GrantKind kind, CardQuality quality)
        {
            CardId = cardId;
            Kind = kind;
            Quality = quality;
        }
    }

    /// <summary>
    /// Sorteio da carta do chão (§3.3, §4.4, D-048, D-051, D-052).
    /// O TEMA vem do caminho do jogador + o lugar e é escolhido ANTES e SEM o resultado do dado;
    /// o dado decide só raridade, qualidade e quantidade (Pilar 2).
    /// </summary>
    public sealed class CardDraw
    {
        // Escala em milésimos do sorteio ponderado do tema; não é número de jogo (os pesos vêm do pathBias).
        private const int WeightScale = 1000;

        private readonly IReadOnlyList<DrawCandidate> pool;
        private readonly float pathBias;

        /// <summary>pool = cartas que podem sair nesta zona (o lugar); pathBias >= 0 é o peso extra das tags do caminho.</summary>
        public CardDraw(IReadOnlyList<DrawCandidate> pool, float pathBias)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool), "O pool de cartas não pode ser nulo.");
            if (pathBias < 0f)
                throw new ArgumentOutOfRangeException(nameof(pathBias), pathBias,
                    "O peso do caminho não pode ser negativo.");
            this.pathBias = pathBias;
        }

        /// <summary>
        /// Escolhe o tema (uma tag).
        /// - Tags possíveis = união das tags das cartas NÃO amaldiçoadas e NÃO maiores do pool, sem repetição,
        ///   em ordem alfabética ordinal (para ser determinístico).
        /// - Peso de cada tag = 1 + pathBias se ela estiver em pathTopTags (pode ser nulo/vazio), senão 1.
        /// - Sorteio ponderado com random, determinístico para a mesma seed
        ///   (random.Next(0, total em milésimos) e percorrer os pesos em milésimos).
        /// - Sem tags possíveis: devolve string.Empty.
        /// Não recebe nada do dado.
        /// </summary>
        public string ChooseTheme(IReadOnlyList<string> pathTopTags, IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random), "A fonte de aleatoriedade não pode ser nula.");

            // União das tags possíveis, sem repetição; a ordenação ordinal deixa o sorteio determinístico.
            var tags = new List<string>();
            foreach (DrawCandidate card in pool)
            {
                if (card.Cursed || card.Major)
                    continue;
                foreach (string tag in card.Tags)
                {
                    if (string.IsNullOrEmpty(tag) || tags.Contains(tag))
                        continue;
                    tags.Add(tag);
                }
            }
            if (tags.Count == 0)
                return string.Empty;
            tags.Sort(StringComparer.Ordinal);

            int pathWeight = (int)System.MathF.Round((1f + pathBias) * WeightScale, MidpointRounding.AwayFromZero);
            var weights = new int[tags.Count];
            int total = 0;
            for (int i = 0; i < tags.Count; i++)
            {
                weights[i] = ContainsTag(pathTopTags, tags[i]) ? pathWeight : WeightScale;
                total += weights[i];
            }

            int pick = random.Next(0, total);
            for (int i = 0; i < tags.Count; i++)
            {
                pick -= weights[i];
                if (pick < 0)
                    return tags[i];
            }
            return tags[tags.Count - 1]; // inalcançável: os pesos somam exatamente o total sorteado
        }

        /// <summary>
        /// Monta as cartas do resultado.
        /// 1. Carta principal:
        ///    - outcome.Cursed: candidatas = cartas amaldiçoadas do pool;
        ///    - senão outcome.MajorArcana: candidatas = cartas maiores (Major) do pool, qualquer raridade;
        ///    - senão: cartas não amaldiçoadas e não maiores com Rarity == outcome.Rarity; se não houver nenhuma,
        ///      desce a raridade um degrau por vez (Unique, Rare, Uncommon, Common) até achar.
        ///    - Dentro das candidatas, se alguma tiver a tag `theme`, sorteia só entre as que têm; senão entre todas.
        ///    - Qualidade de carta nova = outcome.Quality.
        /// 2. outcome.ExtraCommons cartas extras: comuns, não amaldiçoadas, não maiores, preferindo o tema,
        ///    sem repetir carta já sorteada nesta mesma chamada (se não houver outra, pode repetir);
        ///    qualidade de carta nova = outcome.ExtraQuality.
        /// 3. Para cada carta sorteada, olhe ownedQuality(id) (null = o jogador não tem):
        ///    - null: GrantKind.New com a qualidade dos itens 1/2;
        ///    - tem e não é Perfect: GrantKind.Upgrade com QualityRules.Next(atual) (um degrau; a qualidade do dado é ignorada);
        ///    - tem e é Perfect: GrantKind.Copy com Perfect.
        ///    Se a mesma carta sair duas vezes na chamada, a segunda considera a qualidade já atualizada pela primeira.
        /// 4. Nenhuma candidata possível para a principal: lista vazia.
        /// Ordem da lista: principal primeiro, depois as extras.
        /// </summary>
        public List<CardGrant> Draw(DiceOutcome outcome, string theme, Func<int, CardQuality?> ownedQuality, IRandomSource random)
        {
            if (ownedQuality == null)
                throw new ArgumentNullException(nameof(ownedQuality), "A consulta de qualidade não pode ser nula.");
            if (random == null)
                throw new ArgumentNullException(nameof(random), "A fonte de aleatoriedade não pode ser nula.");

            var grants = new List<CardGrant>();
            var qualityInCall = new Dictionary<int, CardQuality?>(); // qualidade já entregue nesta chamada
            var drawnIds = new List<int>(); // cartas já sorteadas nesta chamada

            List<DrawCandidate> mainCandidates = MainCandidates(outcome, theme);
            if (mainCandidates.Count == 0)
                return grants; // nenhuma candidata possível para a principal: lista vazia

            DrawCandidate main = mainCandidates[random.Next(0, mainCandidates.Count)];
            drawnIds.Add(main.Id);
            grants.Add(BuildGrant(main.Id, outcome.Quality, ownedQuality, qualityInCall));

            for (int extra = 0; extra < outcome.ExtraCommons; extra++)
            {
                List<DrawCandidate> candidates = ExtraCandidates(theme, drawnIds);
                if (candidates.Count == 0)
                    break; // não existe comum que sirva; não há mais o que sortear
                DrawCandidate card = candidates[random.Next(0, candidates.Count)];
                drawnIds.Add(card.Id);
                grants.Add(BuildGrant(card.Id, outcome.ExtraQuality, ownedQuality, qualityInCall));
            }
            return grants;
        }

        // ---------- Auxiliares ----------

        /// <summary>Candidatas para a carta principal, já filtradas pelo tema.</summary>
        private List<DrawCandidate> MainCandidates(DiceOutcome outcome, string theme)
        {
            var candidates = new List<DrawCandidate>();
            if (outcome.Cursed)
            {
                foreach (DrawCandidate card in pool)
                    if (card.Cursed)
                        candidates.Add(card);
            }
            else if (outcome.MajorArcana)
            {
                foreach (DrawCandidate card in pool)
                    if (card.Major)
                        candidates.Add(card);
            }
            else
            {
                // Desce a raridade um degrau por vez (Unique, Rare, Uncommon, Common) até achar.
                for (int rarity = (int)outcome.Rarity; rarity >= (int)Rarity.Common && candidates.Count == 0; rarity--)
                    foreach (DrawCandidate card in pool)
                        if (!card.Cursed && !card.Major && card.Rarity == (Rarity)rarity)
                            candidates.Add(card);
            }
            return FilterByTheme(candidates, theme);
        }

        /// <summary>Comuns não amaldiçoadas e não maiores, preferindo o tema, sem repetir as já sorteadas.</summary>
        private List<DrawCandidate> ExtraCandidates(string theme, List<int> alreadyDrawn)
        {
            var commons = new List<DrawCandidate>();
            foreach (DrawCandidate card in pool)
                if (!card.Cursed && !card.Major && card.Rarity == Rarity.Common)
                    commons.Add(card);
            if (commons.Count == 0)
                return commons;

            List<DrawCandidate> candidates = FilterByTheme(commons, theme);
            var fresh = new List<DrawCandidate>();
            foreach (DrawCandidate card in candidates)
                if (!alreadyDrawn.Contains(card.Id))
                    fresh.Add(card);
            // Se não houver outra, pode repetir.
            return fresh.Count > 0 ? fresh : candidates;
        }

        /// <summary>Se alguma candidata tiver a tag, sorteia só entre as que têm; senão entre todas.</summary>
        private static List<DrawCandidate> FilterByTheme(List<DrawCandidate> candidates, string theme)
        {
            if (string.IsNullOrEmpty(theme))
                return candidates;
            var themed = new List<DrawCandidate>();
            foreach (DrawCandidate card in candidates)
                if (HasTag(card, theme))
                    themed.Add(card);
            return themed.Count > 0 ? themed : candidates;
        }

        private static bool HasTag(DrawCandidate card, string tag)
        {
            foreach (string t in card.Tags)
                if (t == tag)
                    return true;
            return false;
        }

        private static bool ContainsTag(IReadOnlyList<string> tags, string tag)
        {
            if (tags == null)
                return false;
            foreach (string t in tags)
                if (t == tag)
                    return true;
            return false;
        }

        /// <summary>Entrega uma carta: nova, melhoria de um degrau (D-051) ou cópia extra (D-052).</summary>
        private static CardGrant BuildGrant(int cardId, CardQuality newQuality,
            Func<int, CardQuality?> ownedQuality, Dictionary<int, CardQuality?> qualityInCall)
        {
            // A qualidade da chamada sobrepõe a do inventário: a segunda ocorrência da mesma carta
            // considera o que a primeira já entregou.
            if (!qualityInCall.TryGetValue(cardId, out CardQuality? owned))
            {
                owned = ownedQuality(cardId);
                qualityInCall[cardId] = owned;
            }

            if (owned == null)
            {
                qualityInCall[cardId] = newQuality; // agora o jogador tem a carta, com a qualidade do dado
                return new CardGrant(cardId, GrantKind.New, newQuality);
            }
            if (owned.Value != CardQuality.Perfect)
            {
                CardQuality next = QualityRules.Next(owned.Value);
                qualityInCall[cardId] = next;
                return new CardGrant(cardId, GrantKind.Upgrade, next);
            }
            return new CardGrant(cardId, GrantKind.Copy, CardQuality.Perfect);
        }
    }
}
