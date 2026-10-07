using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Quem usa a carta, visto pelos efeitos. Implementado pelo componente de cartas do jogador.
    /// Tudo aqui roda no host (autoridade).
    /// </summary>
    public interface ICardUser
    {
        Transform Transform { get; }
        ulong ClientId { get; }
        /// <summary>Ponto do mouse no chão quando a carta foi usada.</summary>
        Vector3 AimPoint { get; }
        /// <summary>Direção no plano, do jogador até o ponto de mira.</summary>
        Vector3 AimDirection { get; }
        NetworkHealth Health { get; }

        /// <summary>
        /// Multiplicador da qualidade da carta em uso (D-048: gasta, boa, perfeita). Todo efeito multiplica
        /// por ele os seus números de força (dano, cura, pulso). Leia no começo do Execute: fora de um uso vale 1.
        /// </summary>
        float Potency { get; }

        /// <summary>
        /// Multiplicador do dano causado por quem usa a carta agora (bênção do 20 no coop, D-085; 1 sem bênção). Só os efeitos que
        /// causam dano multiplicam por ele, junto com o Potency, no momento do uso (como a qualidade); cura e energia não.
        /// </summary>
        float DamageMultiplier { get; }

        void AddEnergy(float amount);

        /// <summary>Inimigos vivos (IDamageable que não são jogadores) dentro do raio.</summary>
        IEnumerable<IDamageable> EnemiesInRadius(Vector3 center, float radius);

        /// <summary>Para efeitos que duram (dano contínuo, atraso).</summary>
        Coroutine Run(IEnumerator routine);

        /// <summary>Mostra o visual do efeito em todos os jogadores (id do visual, posição, direção, tamanho).</summary>
        void BroadcastVisual(string visualId, Vector3 position, Vector3 direction, float size);
    }

    /// <summary>
    /// Bloco de efeito combinável (§4.4): uma carta é uma lista destes.
    /// Cada efeito declara a mistura mecânico/arcano do dano que causa (D-021, Pilar 4).
    /// </summary>
    public abstract class CardEffect : ScriptableObject
    {
        /// <summary>Executa no host. card é a carta usada (para tags, custo, etc.).</summary>
        public abstract void Execute(ICardUser user, CardData card);
    }
}
