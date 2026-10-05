using UnityEngine;

namespace Game.Cameras
{
    /// <summary>
    /// Qualidade de imagem (passe de resolução, D-056 a D-058): resolução do mundo, suavização das bordas,
    /// luz em faixas e espessura do contorno. Lido pelo PixelCamera (em jogo) e pelo PixelRenderSetup (no material).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Camera/Image Quality Settings", fileName = "ImageQualitySettings")]
    public class ImageQualitySettings : ScriptableObject
    {
        [Tooltip("Linhas do mundo na tela. 0 = resolução da tela (D-056). Ex.: 360 = 640x360 ampliado sem suavizar.")]
        [Min(0)] public int worldHeight = 0;

        [Tooltip("Suavização das bordas 3D (MSAA): 1, 2, 4 ou 8. Só vale com o mundo na resolução da tela.")]
        public int msaa = 4;

        [Tooltip("Degraus da luz em faixas (D-057). Mais degraus = faixas mais suaves.")]
        [Range(2f, 64f)] public float lightBands = 24f;

        [Tooltip("Rampa entre uma faixa e outra (0 = degrau seco; 0,5 = luz contínua). Evita divisa serrilhada.")]
        [Range(0f, 0.5f)] public float bandSoftness = 0.25f;

        [Tooltip("Pontilhado entre as faixas (0 = sem pontilhado, D-057).")]
        [Range(0f, 1f)] public float dither = 0f;

        [Tooltip("Espessura do contorno em pixels numa tela de 1080 linhas; escala com a altura da tela.")]
        [Range(0.5f, 6f)] public float outlinePixelsAt1080 = 2f;

        /// <summary>Linhas do mundo para uma tela com `screenHeight` linhas.</summary>
        public int RenderHeight(int screenHeight)
        {
            int screen = Mathf.Max(1, screenHeight);
            return worldHeight <= 0 ? screen : Mathf.Min(worldHeight, screen);
        }

        /// <summary>Espessura do contorno, em pixels do mundo renderizado (no mínimo 1).</summary>
        public float OutlinePixels(int renderHeight) => Mathf.Max(1f, Mathf.Round(outlinePixelsAt1080 * renderHeight / 1080f));
    }
}
