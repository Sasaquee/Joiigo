using System.Collections;
using Game.Arena;
using Game.Cameras;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Lado C# da translucidez dos prédios (D-076, D-079), por contrato e numa cena mínima (não depende do mapa novo):
    /// o SeeThroughDriver publica as globais do shader LitVazado. Cubo na camada Cenario entre a câmera e o alvo = tapado.
    /// </summary>
    public class SeeThroughTests
    {
        private GameObject cameraObject;
        private GameObject target;
        private SeeThroughDriver driver;
        private SeeThroughSettings settings;

        [SetUp]
        public void CriaCena()
        {
            cameraObject = new GameObject("CameraTeste");
            var cam = cameraObject.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.LookAt(Vector3.zero);
            driver = cameraObject.AddComponent<SeeThroughDriver>();

            target = new GameObject("AlvoTeste");
            target.transform.position = Vector3.zero;
            driver.AddManualTarget(target.transform, 1.8f);
        }

        [TearDown]
        public void Limpa()
        {
            if (cameraObject != null)
                Object.Destroy(cameraObject);
            if (target != null)
                Object.Destroy(target);
            if (settings != null)
                Object.Destroy(settings);
        }

        private static float Contagem() => Shader.GetGlobalFloat(SeeThroughDriver.CountGlobalName);

        /// <summary>Cubo grande na camada Cenario no meio do caminho entre a câmera e o alvo.</summary>
        private static GameObject CriaPredio()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PredioTeste";
            cube.layer = MapLayers.Cenario;
            cube.transform.position = new Vector3(0f, 5.6f, -5f);
            cube.transform.localScale = new Vector3(6f, 6f, 2f);
            Physics.SyncTransforms();
            return cube;
        }

        [Test]
        public void CamadaCenario_TemONomeCerto()
        {
            Assert.AreEqual(MapLayers.CenarioName, LayerMask.LayerToName(MapLayers.Cenario),
                "A camada 8 precisa se chamar Cenario no TagManager");
        }

        [UnityTest]
        public IEnumerator SemPredios_ContagemEZero()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0f, Contagem(), "Nada tapa o alvo");
            Assert.AreEqual(0, driver.PublishedCount);
            Assert.IsFalse(driver.IsOccluded(target.transform));
        }

        [UnityTest]
        public IEnumerator PredioEntreCameraEAlvo_MarcaTapado_EDepoisDoFadeVoltaAZero()
        {
            var predio = CriaPredio();
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.IsTrue(driver.IsOccluded(target.transform), "O cubo tapa o alvo");
            Assert.GreaterOrEqual(Contagem(), 1f, "O alvo tapado entra na lista do shader");
            var published = Shader.GetGlobalVectorArray(SeeThroughDriver.TargetsGlobalName);
            Assert.AreEqual(12, published.Length);
            Assert.That(published[0].x, Is.InRange(0.3f, 0.7f), "Posição do alvo em UV de tela");
            Assert.That(published[0].y, Is.InRange(0.3f, 0.7f));
            Assert.Greater(published[0].z, 0.05f, "Buraco aberto (raio em fração da altura da tela)");
            Assert.That(published[0].w, Is.InRange(12f, 15f), "Profundidade do alvo em metros");

            predio.SetActive(false); // o collider sai na hora
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.7f); // fade-out de 0,3 s, com folga

            Assert.IsFalse(driver.IsOccluded(target.transform), "O cubo saiu do caminho");
            Assert.AreEqual(0f, Contagem(), "Buraco fechou: contagem volta a zero");
            Object.Destroy(predio);
        }

        [UnityTest]
        public IEnumerator PredioForaDaCamadaCenario_NaoTapa()
        {
            var predio = CriaPredio();
            predio.layer = 0; // Default: o driver só olha a camada Cenario
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0f, Contagem());
            Object.Destroy(predio);
        }

        [UnityTest]
        public IEnumerator Fade_ORaioCrescePoucoAPoucoNaAbertura()
        {
            settings = ScriptableObject.CreateInstance<SeeThroughSettings>();
            settings.fadeInSeconds = 0.5f;
            driver.Configure(settings);
            var predio = CriaPredio();

            yield return null;
            yield return null;
            float early = Shader.GetGlobalVectorArray(SeeThroughDriver.TargetsGlobalName)[0].z;
            yield return new WaitForSecondsRealtime(0.8f);
            float late = Shader.GetGlobalVectorArray(SeeThroughDriver.TargetsGlobalName)[0].z;

            Assert.Greater(late, early, "O raio cresce durante o fade-in");
            Object.Destroy(predio);
        }

        [UnityTest]
        public IEnumerator PublicaMargemPertoEFantasmaDoSettings()
        {
            settings = ScriptableObject.CreateInstance<SeeThroughSettings>();
            settings.depthMargin = 1.25f;
            settings.nearCut = 4.5f;
            settings.ghostOpacity = 0.4f;
            driver.Configure(settings);
            yield return null;
            Assert.AreEqual(1.25f, Shader.GetGlobalFloat(SeeThroughDriver.MarginGlobalName), 1e-5f);
            Assert.AreEqual(4.5f, Shader.GetGlobalFloat(SeeThroughDriver.NearGlobalName), 1e-5f);
            Assert.AreEqual(0.4f, Shader.GetGlobalFloat(SeeThroughDriver.GhostGlobalName), 1e-5f);
        }

        [UnityTest]
        public IEnumerator SemSettings_UsaOsPadroesDoAsset()
        {
            var padrao = ScriptableObject.CreateInstance<SeeThroughSettings>();
            yield return null;
            Assert.AreEqual(padrao.depthMargin, Shader.GetGlobalFloat(SeeThroughDriver.MarginGlobalName), 1e-5f);
            Assert.AreEqual(padrao.nearCut, Shader.GetGlobalFloat(SeeThroughDriver.NearGlobalName), 1e-5f);
            Assert.AreEqual(padrao.ghostOpacity, Shader.GetGlobalFloat(SeeThroughDriver.GhostGlobalName), 1e-5f);
            Object.Destroy(padrao);
        }

        [UnityTest]
        public IEnumerator DriverDesligado_ZeraAContagem()
        {
            var predio = CriaPredio();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.GreaterOrEqual(Contagem(), 1f);

            driver.enabled = false;
            Assert.AreEqual(0f, Contagem(), "Sem driver, o shader não recorta");
            Object.Destroy(predio);
        }

        [UnityTest]
        public IEnumerator AlvoDestruido_SaiDaLista()
        {
            var predio = CriaPredio();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.GreaterOrEqual(Contagem(), 1f);

            Object.Destroy(target);
            yield return null;
            yield return null;
            Assert.AreEqual(0f, Contagem(), "Alvo destruído não deixa buraco");
            Object.Destroy(predio);
        }
    }
}
