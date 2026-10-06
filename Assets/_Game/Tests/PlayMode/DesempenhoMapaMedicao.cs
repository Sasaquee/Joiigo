using System.Collections;
using System.Collections.Generic;
using Game.Cameras;
using Game.Enemies;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Medição de desempenho do mapa novo em Play (passe do mapa, relatório §8): tempo de quadro e contagens de render em
    /// alguns pontos do mapa e com uma onda em andamento. Só mede e escreve no log ("[Desempenho]"); não afirma nada.
    /// Roda só quando pedido: -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.DesempenhoMapaMedicao.
    /// Em batchmode a janela não existe; a câmera renderiza fora da tela, então os números são relativos, não absolutos.
    /// </summary>
    [Explicit("Só mede; rodar quando for conferir o desempenho.")]
    public class DesempenhoMapaMedicao
    {
        private GameObject player;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        }

        [UnityTearDown]
        public IEnumerator Encerra() => ArenaTestScene.Cleanup();

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        private RenderTexture target;
        private Texture2D readback;

        private IEnumerator Measure(string label, int frames)
        {
            var cam = Camera.main;
            if (target == null)
            {
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                readback = new Texture2D(1, 1, TextureFormat.RGB24, false);
            }
            var previousTarget = cam.targetTexture;
            cam.targetTexture = target;

            // Aquece (shaders, caches); deixa o jogo rodar alguns quadros para o SeeThroughDriver e as animações andarem.
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                cam.Render();
            }

            var times = new List<float>(frames);
            long dc = 0, ba = 0, sp = 0, tr = 0;
            var watch = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                watch.Restart();
                cam.Render();
                RenderTexture.active = target; // ReadPixels de 1 pixel força o fim do trabalho da GPU
                readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                RenderTexture.active = null;
                watch.Stop();
                times.Add((float)watch.Elapsed.TotalMilliseconds);
#if UNITY_EDITOR
                dc += UnityEditor.UnityStats.drawCalls;
                ba += UnityEditor.UnityStats.batches;
                sp += UnityEditor.UnityStats.setPassCalls;
                tr += UnityEditor.UnityStats.triangles;
#endif
            }
            cam.targetTexture = previousTarget;
            times.Sort();
            float avg = 0f;
            foreach (float t in times) avg += t;
            avg /= times.Count;
            float p95 = times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.95f))];

            int lights = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled && l.gameObject.activeInHierarchy && l.type != LightType.Directional) lights++;
            int particles = 0;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) particles += ps.particleCount;

            Debug.Log($"[Desempenho] {label}: render 1080p médio {avg:0.0} ms, p95 {p95:0.0} ms | draw calls {dc / frames}, batches {ba / frames}, set-pass {sp / frames}, triângulos {tr / frames / 1000}k | luzes pontuais ativas {lights}, partículas vivas {particles}");
        }

        [UnityTest]
        public IEnumerator Medir()
        {
            int renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            int colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
            Debug.Log($"[Desempenho] cena: {renderers} renderers, {colliders} colliders, {Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length} sistemas de partículas");

            MovePlayer(new Vector3(0f, 0f, -18f));
            yield return Measure("spawn dos jogadores", 120);
            MovePlayer(new Vector3(0f, 0f, 0f));
            yield return Measure("centro da praça", 120);
            MovePlayer(new Vector3(0f, 0f, 33f));
            yield return Measure("avenida norte", 120);
            MovePlayer(new Vector3(0f, 0f, 50f));
            yield return Measure("praça menor norte", 120);
            MovePlayer(new Vector3(23.3f, 0f, 23.3f));
            yield return Measure("avenida nordeste", 120);

            // Com a partida e uma onda em andamento: inimigos nas bocas e cruzando a cidade.
            var match = Object.FindFirstObjectByType<MatchState>();
            if (match != null && match.IsSpawned)
                match.ServerStart();
            MovePlayer(new Vector3(0f, 0f, 0f));
            float wait = 12f;
            while (wait > 0f) { wait -= Time.deltaTime; yield return null; }
            int enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length;
            yield return Measure($"onda em andamento ({enemies} inimigos)", 180);
        }
    }
}
