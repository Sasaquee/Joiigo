using System.Collections;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Teste de regressão para o bug do rastro do golpe (SwingVisual): garante que os bounds da malha nunca contenham NaN.
    /// O bug ocorria no último quadro do golpe (u = 1) devido a Math.Sin(Math.PI) levemente negativo causando NaN em Pow.
    /// </summary>
    public class SwingVisualTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return ArenaTestScene.Load();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ArenaTestScene.Cleanup();
        }

        [UnityTest]
        public IEnumerator GolpeCom60GrausAlcance2_5_NaoProduzNaNNosBounds()
        {
            SwingVisual.Play(Vector3.zero, Vector3.forward, 2.5f, 60f);
            yield return AguardarEVerificarBoundsDoGolpe();
        }

        [UnityTest]
        public IEnumerator GolpeCom180GrausAlcance1_NaoProduzNaNNosBounds()
        {
            SwingVisual.Play(Vector3.zero, Vector3.forward, 1f, 180f);
            yield return AguardarEVerificarBoundsDoGolpe();
        }

        [UnityTest]
        public IEnumerator GolpeCom5GrausAlcance6_NaoProduzNaNNosBounds()
        {
            SwingVisual.Play(Vector3.zero, Vector3.forward, 6f, 5f);
            yield return AguardarEVerificarBoundsDoGolpe();
        }

        private IEnumerator AguardarEVerificarBoundsDoGolpe()
        {
            // Aguarda o objeto aparecer (até 0.1 segundos)
            GameObject golpeObj = null;
            float waitForAppearance = 0.1f;
            while (waitForAppearance > 0 && golpeObj == null)
            {
                golpeObj = GameObject.Find("Golpe");
                waitForAppearance -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(golpeObj, "Objeto Golpe não foi criado");

            // Verifica os bounds durante a duração do golpe (até 0.5 segundos, conforme instruções)
            float remainingTimeout = 0.5f;
            while (remainingTimeout > 0 && golpeObj != null)
            {
                var meshFilters = golpeObj.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in meshFilters)
                {
                    var bounds = mf.sharedMesh.bounds;
                    Assert.IsTrue(float.IsFinite(bounds.center.x) && float.IsFinite(bounds.center.y) && float.IsFinite(bounds.center.z),
                        $"Centro do bounds não é finito: {bounds.center}");
                    Assert.IsTrue(float.IsFinite(bounds.size.x) && float.IsFinite(bounds.size.y) && float.IsFinite(bounds.size.z),
                        $"Tamanho do bounds não é finito: {bounds.size}");
                }

                remainingTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            // Garante que não há logs inesperados (como o assert "invalid MinMaxAABB")
            LogAssert.NoUnexpectedReceived();
        }
    }
}