using System.Collections;
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
