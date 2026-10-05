using System.Collections;
using System.Collections.Generic;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Tela do dado (D-047, D-058). Bug: ao rolar, aparecia um quadrado preto atrás do dado (o buffer HDR do URP e o
    /// pós-processo descartavam a transparência do palco). O fundo da textura tem de ficar transparente.
    /// </summary>
    public class DiceRollUiTests
    {
        private const string D20ModelPath = "Assets/_Game/Art/Models/Dice/D20.fbx";
        private GameObject canvas;

        [TearDown]
        public void Limpa()
        {
            if (canvas != null)
                Object.Destroy(canvas);
            var stage = GameObject.Find("PalcoDoDado");
            if (stage != null)
                Object.Destroy(stage);
        }

        [UnityTest]
        public IEnumerator DadoRolando_FundoTransparenteEDadoOpaco()
        {
#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(D20ModelPath);
            Assert.IsNotNull(model, "Modelo do D20");
            canvas = new GameObject("UI", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("TelaDoDado", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var ui = go.AddComponent<DiceRollUi>();
            ui.Configure(model);

            ui.Play(20, 2f);
            yield return null;
            yield return null;

            Assert.IsNotNull(ui.Texture, "A tela do dado criou a textura");
            var stageCamera = GameObject.Find("CameraDoDado")?.GetComponent<Camera>();
            Assert.IsNotNull(stageCamera, "Palco do dado com câmera");
            stageCamera.Render(); // em batchmode não há fim de quadro com tela; renderiza o palco na hora
            Assert.GreaterOrEqual(ui.Texture.height, Mathf.RoundToInt(Screen.height * 0.42f) - 1,
                "A textura tem o tamanho real do dado na tela (D-058)");

            Color corner = ReadPixel(ui.Texture, 2, 2);
            Color center = ReadPixel(ui.Texture, ui.Texture.width / 2, ui.Texture.height / 2);
            Assert.Less(corner.a, 0.05f, "Fora do dado a textura é transparente (sem quadrado preto)");
            Assert.Greater(center.a, 0.9f, "O dado em si aparece opaco");
#else
            Assert.Ignore("Só no editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator DepoisDoUm_OutroNumeroVoltaAoLataoECiano()
        {
            // D-068: o 1 avermelha o dado e as luzes do palco; a próxima rolagem (2 a 20) tem de voltar ao normal.
#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(D20ModelPath);
            Assert.IsNotNull(model, "Modelo do D20");
            canvas = new GameObject("UI", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("TelaDoDado", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var ui = go.AddComponent<DiceRollUi>();
            ui.Configure(model);

            ui.Play(10, 0.5f);
            yield return null;
            Assert.IsNotNull(ui.Die, "O palco do dado foi montado");
            List<Color> normal = Snapshot(ui);

            ui.Play(1, 0.5f);
            float timeout = 2f;
            while (ui.CriticalTint < 1f && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(1f, ui.CriticalTint, "O dado parou vermelho no 1");
            List<Color> red = Snapshot(ui);
            bool changed = false;
            for (int i = 0; i < normal.Count; i++)
                changed |= !Near(normal[i], red[i]);
            Assert.IsTrue(changed, "No 1 o material do dado e as luzes mudaram");

            ui.Play(10, 0.5f);
            yield return null;
            Assert.IsFalse(ui.IsCritical, "O 10 encerra o modo teatral");
            Assert.IsFalse(ui.VeilActive, "O 10 tira o véu");
            Assert.AreEqual(0f, ui.CriticalTint, "O 10 devolve o latão");
            List<Color> back = Snapshot(ui);
            Assert.AreEqual(normal.Count, back.Count);
            for (int i = 0; i < normal.Count; i++)
                Assert.IsTrue(Near(normal[i], back[i]), $"Cor {i} voltou ao latão/ciano ({back[i]} em vez de {normal[i]})");
#else
            Assert.Ignore("Só no editor.");
            yield break;
#endif
        }

        /// <summary>Cores de todos os materiais do dado (base e emissão) e das duas luzes do palco.</summary>
        private static List<Color> Snapshot(DiceRollUi ui)
        {
            var colors = new List<Color>();
            foreach (Renderer r in ui.Die.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null)
                        continue;
                    if (m.HasProperty("_BaseColor"))
                        colors.Add(m.GetColor("_BaseColor"));
                    if (m.HasProperty("_EmissionColor"))
                        colors.Add(m.GetColor("_EmissionColor"));
                }
            }
            colors.Add(ui.KeyLight.color);
            colors.Add(ui.RimLight.color);
            return colors;
        }

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;

        private static Color ReadPixel(RenderTexture rt, int x, int y)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Color c = tex.GetPixel(0, 0);
            Object.Destroy(tex);
            return c;
        }
    }
}
