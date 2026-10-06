using System.Collections;
using System.Linq;
using Game.Arena.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Os SmokeEmitter só guardam tipo e intensidade na cena; o ParticleSystem nasce em jogo (Awake),
    /// com culling por pausa, e emite partículas.
    /// Depende da cena gerada por Game → Setup → Construir Arena (com emissores).
    /// </summary>
    public class SmokeEmitterTests
    {
        [UnitySetUp]
        public IEnumerator CarregaArena()
        {
            yield return ArenaTestScene.Load();
        }

        [UnityTearDown]
        public IEnumerator Limpa()
        {
            yield return ArenaTestScene.Cleanup();
        }

        private static SmokeEmitter[] Emitters()
            => Object.FindObjectsByType<SmokeEmitter>(FindObjectsSortMode.None);

        [UnityTest]
        public IEnumerator Emissores_CriamParticleSystemEmJogo()
        {
            yield return null;
            var emitters = Emitters();
            Assert.Greater(emitters.Length, 0, "A arena tem emissores de fumaça");
            foreach (var e in emitters)
            {
                Assert.IsNotNull(e.Particles, $"{e.name}: ParticleSystem criado em jogo");
                Assert.AreEqual(ParticleSystemCullingMode.Pause, e.Particles.main.cullingMode,
                    $"{e.name}: pausa fora da tela");
                Assert.IsTrue(e.Particles.isPlaying, $"{e.name}: sistema tocando");
            }
        }

        [UnityTest]
        public IEnumerator Emissores_EmitemParticulas()
        {
            yield return null;
            var emitters = Emitters();
            Assert.Greater(emitters.Length, 0, "A arena tem emissores de fumaça");

            // O culling por pausa depende da câmera; aqui só interessa que o sistema emite.
            foreach (var e in emitters)
            {
                var main = e.Particles.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }

            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline && !emitters.Any(e => e.Particles.particleCount > 0))
                yield return null;

            Assert.IsTrue(emitters.Any(e => e.Particles.particleCount > 0), "Pelo menos um emissor soltou partículas");
        }

        [UnityTest]
        public IEnumerator Configure_EmJogoReconstroiSemDuplicarFilho()
        {
            var go = new GameObject("EmissorTeste");
            var emitter = go.AddComponent<SmokeEmitter>();
            yield return null;

            emitter.Configure(SmokeKind.Steam, 2f);
            yield return null;

            Assert.AreEqual(SmokeKind.Steam, emitter.Kind);
            Assert.AreEqual(1, go.GetComponentsInChildren<ParticleSystem>(true).Length, "Um único ParticleSystem filho");
            Object.Destroy(go);
        }
    }
}
