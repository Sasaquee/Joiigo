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
                follow.Apply(new Vector3(0f, 0f, -19f));
                Render(cam, Path.Combine(dir, "arena_spawn.png"));

                // Inimigos colocados só para a foto (não são salvos na cena).
                var temp = new System.Collections.Generic.List<GameObject>();
                string[] enemies = { "Automato", "Drone", "Constructo" };
                for (int i = 0; i < enemies.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Enemies/Prefabs/{enemies[i]}.prefab");
                    if (prefab == null)
                        continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetPositionAndRotation(new Vector3(-2.5f + i * 2.5f, 0f, 2f), Quaternion.Euler(0f, 200f, 0f));
                    temp.Add(go);
                }
                if (temp.Count > 0)
                {
                    follow.Apply(new Vector3(0f, 0f, 1f));
                    float distance = follow.Settings.distance;
                    follow.Settings.distance = 7f; // mais perto, só para a foto
                    follow.Apply(new Vector3(0f, 0f, 1.5f));
                    Render(cam, Path.Combine(dir, "inimigos.png"));
                    follow.Settings.distance = distance;
                    foreach (var go in temp)
                        UnityEngine.Object.DestroyImmediate(go);
                }
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
            const int width = 1920, height = 1080; // mundo na resolução da tela (D-056)
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            // O PixelCamera não roda fora do Play: passa a espessura do contorno daqui.
            Shader.SetGlobalFloat("_OutlineWidth", PixelRenderSetup.LoadQualitySettings().OutlinePixels(height));
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
