using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class ArenaSceneTests
    {
        [UnityTest]
        public IEnumerator Arena_Carrega()
        {
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);

            Assert.AreEqual("Arena", SceneManager.GetActiveScene().name);
        }
    }
}
