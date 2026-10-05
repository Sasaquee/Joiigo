using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Fotos do jogo rodando, para conferir o visual nos testes de captura (explícitos). Em batchmode não há tela
    /// (nem WaitForEndOfFrame): renderiza a câmera de jogo numa textura, com a UI em Overlay trocada por um instante
    /// para "Screen Space - Camera". Nas fotos, o contorno do pós-processo também passa por cima da UI.
    /// </summary>
    public static class PlayCapture
    {
        public static string Folder(string fallbackName)
        {
            string dir = System.Environment.GetEnvironmentVariable("JOIIGO_CAPTURAS");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "Logs", "Capturas", fallbackName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Foto 1920x1080 da câmera de jogo com a UI por cima. Em batchmode não há tela (nem WaitForEndOfFrame):
        /// a UI em Overlay passa um instante para "Screen Space - Camera" para sair na mesma renderização.
        /// </summary>
        public static IEnumerator Shot(string dir, string name)
        {
            yield return null;
            const int width = 1920, height = 1080;
            var cam = Camera.main;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var overlays = new List<Canvas>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                overlays.Add(canvas);
            }
            Canvas.ForceUpdateCanvases();
            var dieCamera = GameObject.Find("CameraDoDado")?.GetComponent<Camera>();
            if (dieCamera != null && dieCamera.enabled)
                dieCamera.Render();

            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            foreach (var canvas in overlays)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[Captura] {name} {width}x{height}");
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
