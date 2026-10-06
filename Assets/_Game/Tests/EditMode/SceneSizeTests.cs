using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Mantém a cena da arena leve: o SmokeEmitter não pode gravar ParticleSystem na cena (a cidade dobrou no passe do mapa).
    /// ATENÇÃO: este teste só passa depois de o orquestrador reconstruir a arena (Game → Setup → Construir Arena),
    /// porque a Arena.unity antiga (~10,5 MB) ainda traz os 67 ParticleSystem gravados.
    /// </summary>
    public class SceneSizeTests
    {
        // Teto em bytes. A cena enxuta fica em ~3 MB; 6 MB dá folga para a cidade maior.
        private const long MaxSceneBytes = 6L * 1024 * 1024;

        [Test]
        public void ArenaUnity_FicaAbaixoDe6MB()
        {
            string path = Path.Combine(Application.dataPath, "_Game/Arena/Arena.unity");
            Assert.IsTrue(File.Exists(path), "Arena.unity não encontrada em " + path);

            long length = new FileInfo(path).Length;
            Assert.Less(length, MaxSceneBytes,
                $"Arena.unity com {length / (1024f * 1024f):F1} MB: reconstrua a arena e confira se algum construtor grava ParticleSystem na cena.");
        }
    }
}
