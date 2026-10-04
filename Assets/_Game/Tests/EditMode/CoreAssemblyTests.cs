using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CoreAssemblyTests
    {
        [Test]
        public void Core_NaoReferenciaAUnity()
        {
            var referencias = typeof(CoreAssembly).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor") || n.StartsWith("Unity."))
                .ToArray();

            Assert.That(referencias, Is.Empty, "Game.Core deve ser C# puro.");
        }
    }
}
