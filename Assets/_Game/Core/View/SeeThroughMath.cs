using System;

namespace Game.Core.View
{
    /// <summary>
    /// Contas da translucidez dos prédios (D-076, D-079), sem Unity. O SeeThroughDriver (Camera) faz os linecasts e
    /// publica o resultado; o shader LitVazado recorta um círculo em volta de cada alvo tapado. As funções
    /// <see cref="IsInsideHole"/> e <see cref="ShouldCutPixel"/> espelham a regra do shader, para os testes e para conferência.
    /// </summary>
    public static class SeeThroughMath
    {
        /// <summary>Tamanho do array global do shader (_VazadoAlvos[12]). É contrato com o shader, não número de jogo.</summary>
        public const int MaxShaderTargets = 12;

        private const float MinDepth = 0.01f;

        /// <summary>
        /// Raio do buraco como fração da ALTURA DA TELA. Na profundidade <paramref name="depthMeters"/> a tela vê
        /// 2 · profundidade · tan(fov/2) metros de altura, então raio = raioMundo / (2 · profundidade · tan(fov/2)).
        /// Inversamente proporcional à profundidade: o dobro de distância, metade do raio.
        /// </summary>
        public static float ScreenRadius(float worldRadius, float depthMeters, float verticalFovDegrees)
        {
            float tanHalf = MathF.Tan(verticalFovDegrees * 0.5f * (MathF.PI / 180f));
            float visibleHeight = 2f * MathF.Max(depthMeters, MinDepth) * MathF.Max(tanHalf, 1e-4f);
            return MathF.Max(0f, worldRadius) / visibleHeight;
        }

        /// <summary>
        /// Avança a abertura do buraco (0 = fechado, 1 = aberto) em direção ao alvo: abre em
        /// <paramref name="fadeInSeconds"/> e fecha em <paramref name="fadeOutSeconds"/> (linear; a suavização é de <see cref="Smooth"/>).
        /// </summary>
        public static float StepFade(float current, bool open, float deltaTime, float fadeInSeconds, float fadeOutSeconds)
        {
            if (open)
            {
                float rate = fadeInSeconds > 1e-4f ? deltaTime / fadeInSeconds : 1f;
                return MathF.Min(1f, current + rate);
            }
            float rateOut = fadeOutSeconds > 1e-4f ? deltaTime / fadeOutSeconds : 1f;
            return MathF.Max(0f, current - rateOut);
        }

        /// <summary>Suavização (smoothstep) da abertura 0..1, para o raio não "estalar" ao abrir e fechar.</summary>
        public static float Smooth(float t)
        {
            t = MathF.Min(1f, MathF.Max(0f, t));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Alvo tapado: QUALQUER das três alturas (pés, tronco, cabeça) bate no cenário antes do alvo.
        /// Cada argumento é o resultado do linecast da câmera até aquela altura.
        /// </summary>
        public static bool IsOccluded(bool lowBlocked, bool midBlocked, bool highBlocked)
            => lowBlocked || midBlocked || highBlocked;

        /// <summary>
        /// Relevância de um alvo para entrar entre os 12 do shader: jogadores antes de inimigos, depois os mais abertos
        /// (já vistos pelo jogador) e por fim os mais perto da câmera (buraco maior).
        /// </summary>
        public static float Relevance(bool isPlayer, float openness, float depthMeters)
            => (isPlayer ? 1000f : 0f) + openness * 100f - depthMeters;

        /// <summary>
        /// Escolhe os até <paramref name="max"/> maiores valores de <paramref name="scores"/> (primeiros
        /// <paramref name="count"/>) e escreve os índices em <paramref name="selected"/>, do mais ao menos relevante.
        /// Devolve quantos foram escolhidos. Sem alocação.
        /// </summary>
        public static int SelectTop(float[] scores, int count, int max, int[] selected)
        {
            int limit = System.Math.Min(System.Math.Min(max, selected.Length), count);
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                int pos = n;
                while (pos > 0 && scores[selected[pos - 1]] < scores[i])
                    pos--;
                if (pos >= limit)
                    continue;
                int last = n < limit ? n : limit - 1;
                for (int k = last; k > pos; k--)
                    selected[k] = selected[k - 1];
                selected[pos] = i;
                if (n < limit)
                    n++;
            }
            return n;
        }

        /// <summary>
        /// Pixel dentro do círculo do alvo. As posições estão em UV de tela (0..1); o raio é fração da altura da tela,
        /// então o eixo x é multiplicado pelo aspecto (largura / altura) para o buraco ser redondo.
        /// </summary>
        public static bool IsInsideHole(float pixelX, float pixelY, float targetX, float targetY, float radius, float aspect)
        {
            float dx = (pixelX - targetX) * aspect;
            float dy = pixelY - targetY;
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>
        /// Regra de recorte do shader (por pixel de uma peça com a rendering layer Vazavel): corta tudo a menos de
        /// <paramref name="nearCut"/> m da câmera, e o que está dentro do buraco e mais perto que o alvo menos
        /// <paramref name="depthMargin"/> m.
        /// </summary>
        public static bool ShouldCutPixel(float pixelDepth, bool insideHole, float targetDepth, float depthMargin, float nearCut)
            => pixelDepth < nearCut || (insideHole && pixelDepth < targetDepth - depthMargin);
    }
}
