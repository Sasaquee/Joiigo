namespace Game.Core.Dice
{
    /// <summary>
    /// O D20 do jogo (§3.3, Pilar 2): puro e visível. Nada altera a rolagem — por isso a API não tem
    /// nenhum parâmetro de modificador (um teste falha se aparecer um).
    /// </summary>
    public sealed class D20
    {
        /// <summary>Faces do dado. É a estrutura do dado, não um número de balanceamento.</summary>
        public const int Faces = 20;

        private readonly IRandomSource random;

        /// <summary>Fonte de aleatoriedade das rolagens; não pode ser nula.</summary>
        public D20(IRandomSource random)
        {
            this.random = random ?? throw new System.ArgumentNullException(nameof(random),
                "A fonte de aleatoriedade do D20 não pode ser nula.");
        }

        /// <summary>Rola: inteiro uniforme de 1 a 20 (random.Next(1, Faces + 1)).</summary>
        public int Roll()
        {
            return random.Next(1, Faces + 1);
        }
    }
}
