namespace Game.Arena
{
    /// <summary>
    /// Camadas do mapa (passe do mapa, D-073 a D-082). Os nomes e os índices ficam em ProjectSettings/TagManager.asset:
    /// a camada 8 "Cenario" (prédios, vedações e chão andável da cidade) e a rendering layer 8 "Vazavel" (peças da
    /// cidade que o shader pode recortar quando tapam jogador ou inimigo, D-076 e D-079).
    /// </summary>
    public static class MapLayers
    {
        public const string CenarioName = "Cenario";
        public const int Cenario = 8;
        public const int CenarioMask = 1 << Cenario;

        public const string VazavelName = "Vazavel";
        public const int VazavelRenderingLayerIndex = 8;
        public const uint VazavelRenderingLayerMask = 1u << VazavelRenderingLayerIndex;
    }
}
