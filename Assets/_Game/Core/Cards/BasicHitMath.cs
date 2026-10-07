using System;
using Game.Core.Combat;

namespace Game.Core.Cards
{
    /// <summary>Dano do golpe básico do jogador com os modificadores das cartas (Core: o host só chama).</summary>
    public static class BasicHitMath
    {
        /// <summary>Menor dano que um golpe básico causa, por mais que as cartas o reduzam.</summary>
        public const float MinDamage = 1f;

        /// <summary>
        /// Monta o golpe: o deslocamento arcano (Lente) vale para o dano base; o dano extra (BasicDamage, positivo ou negativo) é
        /// mecânico (soma ao total sem mexer na parte arcana); depois multiplicam o reforço da Mola de Recuo (hurtBonus),
        /// BasicDamageMultiplier (mods.BasicDamageScale) e a bênção do 20 no coop (blessing, D-085). O dano final nunca fica
        /// abaixo de MinDamage e a mistura mecânico/arcano não muda com os multiplicadores. mods nulo = sem cartas.
        /// </summary>
        public static DamagePacket Compute(float baseDamage, float baseArcaneFraction, ModifierSet mods, float hurtBonus, float blessing)
        {
            float arcane = System.Math.Clamp(baseArcaneFraction + (mods != null ? mods.Get(ModifierKind.BasicArcaneShift) : 0f), 0f, 1f);
            float extra = mods != null ? mods.Get(ModifierKind.BasicDamage) : 0f;
            float damage = MathF.Max(0f, baseDamage);

            // O dano extra corta o total (nunca abaixo de zero); a parte arcana é a da base e nunca passa do total.
            float raw = MathF.Max(0f, damage + extra);
            float arcanePart = MathF.Min(raw, damage * arcane);

            float scale = mods != null ? mods.BasicDamageScale : 1f;
            float factor = (1f + MathF.Max(0f, hurtBonus)) * scale * MathF.Max(0f, blessing);
            float amount = MathF.Max(MinDamage, raw * factor);
            float fraction = raw > 0f ? arcanePart / raw : arcane;
            return new DamagePacket(amount, fraction);
        }
    }
}
