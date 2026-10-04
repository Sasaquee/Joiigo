using System;

namespace Game.Net
{
    /// <summary>
    /// Token que identifica este jogo aberto enquanto ele roda. Permite voltar à sessão
    /// depois de cair (D-014). Se o jogo for fechado, o token muda e a vaga se perde.
    /// </summary>
    public static class ClientIdentity
    {
        public static readonly string Token = Guid.NewGuid().ToString("N");
    }
}
