using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Renderiza a cena Arena pela câmera do jogo e por uma vista geral, e salva PNG.
    /// Batchmode: -executeMethod Game.EditorTools.SceneSnapshot.CaptureArena -snapshotDir &lt;pasta&gt;
    /// </summary>
    public static class SceneSnapshot
    {
        [MenuItem("Game/Debug/Capturar vistas da Arena")]
        public static void CaptureArena()
        {
            string dir = ArgValue("-snapshotDir") ?? Path.Combine(Application.dataPath, "..", "Logs", "Snapshots");
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene(ArenaSceneSetup.ArenaScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
                throw new InvalidOperationException("Cena sem Main Camera.");

            // Sem isso, o primeiro frame mostra shaders ainda compilando (magenta/ciano provisório).
            bool asyncCompile = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;

            // Primeira renderização descartada: aquece os shaders, senão sai magenta.
            Render(cam, Path.Combine(dir, "aquecimento.png"));
            File.Delete(Path.Combine(dir, "aquecimento.png"));
            Render(cam, Path.Combine(dir, "arena_jogo.png"));

            // Mesma câmera de jogo, centrada perto do portão norte e da parede.
            var follow = cam.GetComponent<Game.Cameras.CameraFollow>();
            if (follow != null && follow.Settings != null)
            {
                follow.Apply(new Vector3(0f, 0f, 16f));
                Render(cam, Path.Combine(dir, "arena_portao.png"));
                follow.Apply(new Vector3(-15f, 0f, -8f));
                Render(cam, Path.Combine(dir, "arena_alcova.png"));
            }

            Vector3 pos = cam.transform.position;
            Quaternion rot = cam.transform.rotation;
            float fov = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 52f, -38f), Quaternion.Euler(55f, 0f, 0f));
            cam.fieldOfView = 50f;
            Render(cam, Path.Combine(dir, "arena_geral.png"));
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;

            EditorSettings.asyncShaderCompilation = asyncCompile;
            Debug.Log($"Capturas salvas em {dir}");
        }

        private static void Render(Camera cam, string path)
        {
            const int width = 1600, height = 900;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = previous;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
