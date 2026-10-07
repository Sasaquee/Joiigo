using System.Collections.Generic;

namespace Game.Core.Cards
{
    /// <summary>O que passivas e equipamentos alteram. Os valores ficam nos assets das cartas.</summary>
    public enum ModifierKind
    {
        /// <summary>Dano extra do golpe básico (soma, unidades de dano).</summary>
        BasicDamage,
        /// <summary>Alcance extra do golpe básico (soma, metros).</summary>
        BasicRange,
        /// <summary>Desloca a mistura do golpe para o arcano (soma na fração, 0 a 1).</summary>
        BasicArcaneShift,
        /// <summary>Energia extra por golpe que acerta (fração: 0,5 = +50%).</summary>
        EnergyOnHitBonus,
        /// <summary>Muda a recarga das skills (fração: -0,25 = 25% mais rápido).</summary>
        CooldownChange,
        /// <summary>Depois de levar dano, o próximo golpe causa mais (fração: 0,6 = +60%).</summary>
        HurtNextHitBonus,
        /// <summary>Velocidade de andar (fração: 0,1 = 10% mais rápido). Só no fim do enum: os valores são serializados nos assets (Fase 10).</summary>
        MoveSpeed,
        /// <summary>Vida máxima (soma, unidades de vida). A vida atual mantém a fração ao mudar.</summary>
        MaxHealth,
        /// <summary>Dano do golpe básico (fração somada ao multiplicador: 0,6 = +60%), por cima do BasicDamage.</summary>
        BasicDamageMultiplier,
        /// <summary>Vida que o jogador perde por golpe básico que acerta (soma, unidades de vida; uma vez por golpe).</summary>
        LifeCostPerHit
    }

    public readonly struct Modifier
    {
        public readonly ModifierKind Kind;
        public readonly float Value;

        public Modifier(ModifierKind kind, float value)
        {
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>Soma dos modificadores ativos de um jogador.</summary>
    public class ModifierSet
    {
        private readonly Dictionary<ModifierKind, float> totals = new Dictionary<ModifierKind, float>();

        public void Clear() => totals.Clear();

        public void Add(Modifier m)
        {
            totals.TryGetValue(m.Kind, out float v);
            totals[m.Kind] = v + m.Value;
        }

        public float Get(ModifierKind kind) => totals.TryGetValue(kind, out float v) ? v : 0f;

        /// <summary>Multiplicador de recarga, nunca abaixo de 10% da original.</summary>
        public float CooldownMultiplier => System.MathF.Max(0.1f, 1f + Get(ModifierKind.CooldownChange));

        /// <summary>Multiplicador da velocidade de andar (1 = normal), nunca abaixo de zero.</summary>
        public float MoveSpeedMultiplier => System.MathF.Max(0f, 1f + Get(ModifierKind.MoveSpeed));

        /// <summary>Multiplicador do dano do golpe básico vindo de BasicDamageMultiplier (1 = normal), nunca abaixo de zero.</summary>
        public float BasicDamageScale => System.MathF.Max(0f, 1f + Get(ModifierKind.BasicDamageMultiplier));
    }
}
