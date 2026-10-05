using System;
using System.Collections.Generic;
using System.IO;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Passe de resolução (D-053, P-012): renderiza a mesma cena em várias resoluções do mundo e o D20 em vários
    /// tamanhos, ampliando como o jogo amplia (sem suavizar), para o dono comparar lado a lado.
    /// Batchmode: -executeMethod Game.EditorTools.ResolutionPreview.Capture -snapshotDir &lt;pasta&gt;
    /// </summary>
    public static class ResolutionPreview
    {
        private const int OutWidth = 1920, OutHeight = 1080; // tela de referência
        private static readonly int[] WorldHeights = { 360, 540, 720, 1080 };
        private const string PostMaterialPath = "Assets/_Game/Art/Shaders/PixelPost.mat";
        private const string D20ModelPath = "Assets/_Game/Art/Models/Dice/D20.fbx";

        [MenuItem("Game/Debug/Comparar resoluções")]
        public static void Capture()
        {
            string dir = ArgValue("-snapshotDir") ?? Path.Combine(Application.dataPath, "..", "Docs", "Capturas", "resolucao");
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene(ArenaSceneSetup.ArenaScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
                throw new InvalidOperationException("Cena sem Main Camera.");

            bool asyncCompile = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float scale = pipeline != null ? pipeline.renderScale : 1f;
            if (pipeline != null)
                pipeline.renderScale = 1f; // o tamanho vem da textura de destino
            var post = AssetDatabase.LoadAssetAtPath<Material>(PostMaterialPath);
            float levels = post != null ? post.GetFloat("_Levels") : 12f;
            float dither = post != null ? post.GetFloat("_Dither") : 0f;

            var temp = new List<GameObject>();
            try
            {
                // Personagem, inimigos e uma carta no chão, só para a foto.
                Vector3 focus = new Vector3(0f, 0f, 1f);
                Place(temp, "Assets/_Game/Player/Player.prefab", new Vector3(0f, 0f, 0f), 160f);
                Place(temp, "Assets/_Game/Enemies/Prefabs/Automato.prefab", new Vector3(-2.6f, 0f, 3f), 200f);
                Place(temp, "Assets/_Game/Enemies/Prefabs/Drone.prefab", new Vector3(0.4f, 0f, 3.8f), 190f);
                Place(temp, "Assets/_Game/Enemies/Prefabs/Constructo.prefab", new Vector3(3f, 0f, 3.2f), 210f);
                Place(temp, "Assets/_Game/Dice/Prefabs/CartaNoChao.prefab", new Vector3(2.2f, 0f, 0.6f), 30f);

                var follow = cam.GetComponent<Game.Cameras.CameraFollow>();
                if (follow != null && follow.Settings != null)
                    follow.Apply(focus);

                RenderWorld(cam, OutHeight, Path.Combine(dir, "aquecimento.png")); // aquece os shaders
                File.Delete(Path.Combine(dir, "aquecimento.png"));
                foreach (int h in WorldHeights)
                    RenderWorld(cam, h, Path.Combine(dir, $"mundo_{h}.png"));

                // Resolução cheia sem as faixas de luz (só contorno), para ver o efeito das faixas.
                if (post != null)
                {
                    post.SetFloat("_Levels", 255f);
                    post.SetFloat("_Dither", 0f);
                    RenderWorld(cam, OutHeight, Path.Combine(dir, "mundo_1080_sem_faixas.png"));
                }
            }
            finally
            {
                if (post != null)
                {
                    post.SetFloat("_Levels", levels);
                    post.SetFloat("_Dither", dither);
                }
                foreach (var go in temp)
                    UnityEngine.Object.DestroyImmediate(go);
            }

            try
            {
                CaptureDie(dir);
            }
            finally
            {
                if (pipeline != null)
                    pipeline.renderScale = scale;
                EditorSettings.asyncShaderCompilation = asyncCompile;
            }
            Debug.Log($"[ResolutionPreview] Capturas salvas em {dir}");
        }

        private static void Place(List<GameObject> temp, string path, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[ResolutionPreview] Sem {path}");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            temp.Add(go);
        }

        /// <summary>Renderiza o mundo com `height` linhas e amplia até 1920x1080 sem suavizar, como o jogo faz.</summary>
        private static void RenderWorld(Camera cam, int height, string path)
        {
            int width = Mathf.RoundToInt(height * 16f / 9f);
            Shader.SetGlobalFloat("_OutlineWidth", PixelRenderSetup.LoadQualitySettings().OutlinePixels(height));
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            SaveScaled(rt, OutWidth, OutHeight, path);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>O D20 no palco da tela do dado: tamanho de hoje (151 px, ampliado) e na resolução da tela.</summary>
        private static void CaptureDie(string dir)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(D20ModelPath);
            if (model == null)
                return;
            var stage = new GameObject("PalcoDoDado").transform;
            stage.position = new Vector3(0f, -500f, 0f);
            try
            {
                var die = UnityEngine.Object.Instantiate(model, stage).transform;
                die.localPosition = Vector3.zero;
                var faces = DiceRollUi.FaceRotations(die);
                die.localRotation = faces.TryGetValue(20, out var q) ? q : Quaternion.identity;

                var camGo = new GameObject("CameraDoDado");
                camGo.transform.SetParent(stage, false);
                camGo.transform.localPosition = new Vector3(0f, 0f, -2.6f);
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.5f;
                cam.farClipPlane = 4.6f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.07f, 0.09f, 1f);
                cam.enabled = false;
                AddLight(stage, new Vector3(-1.2f, 1.6f, -2f), new Color(1f, 0.85f, 0.6f), 6f);
                AddLight(stage, new Vector3(1.4f, -0.4f, 1.6f), new Color(0.35f, 0.9f, 1f), 5f);

                int big = Mathf.RoundToInt(OutHeight * 0.42f); // tamanho do dado numa tela 1080p
                foreach (int side in new[] { 151, 227, big })
                {
                    var rt = new RenderTexture(side, side, 24, RenderTextureFormat.ARGB32)
                    { antiAliasing = side >= big ? 8 : 1, filterMode = FilterMode.Point };
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    SaveScaled(rt, big, big, Path.Combine(dir, $"dado_{side}.png"));
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage.gameObject);
            }
        }

        private static void AddLight(Transform parent, Vector3 localPosition, Color color, float intensity)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 6f;
        }

        private static void SaveScaled(RenderTexture source, int width, int height, string path)
        {
            var big = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            source.filterMode = FilterMode.Point;
            Graphics.Blit(source, big);
            RenderTexture.active = big;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(big);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
