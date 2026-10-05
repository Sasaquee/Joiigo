using System.Collections.Generic;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// D-047: o D20 tem de parar com o número sorteado de frente e em pé. Confere as 20 faces do modelo
    /// (Empties Centro_N / Topo_N de Tools/Blender/build_d20.py) com a mesma tabela que a tela do dado usa.
    /// </summary>
    public class DiceFaceTests
    {
        private const string D20ModelPath = "Assets/_Game/Art/Models/Dice/D20.fbx";
        private GameObject die;

        [TearDown]
        public void Limpa()
        {
            if (die != null)
                Object.DestroyImmediate(die);
        }

        [Test]
        public void TodasAsVinteFacesParamDeFrenteEEmPe()
        {
#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(D20ModelPath);
            Assert.IsNotNull(model, "Modelo do D20 (rodar Tools/Blender/build_d20.py)");
            die = Object.Instantiate(model);
            die.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);

            Dictionary<int, Quaternion> faces = DiceRollUi.FaceRotations(die.transform);
            Assert.AreEqual(20, faces.Count, "Uma rotação para cada número de 1 a 20");

            for (int n = 1; n <= 20; n++)
            {
                Assert.IsTrue(faces.ContainsKey(n), $"Falta a face {n}");
                Transform center = Find(die.transform, "Centro_" + n);
                Transform top = Find(die.transform, "Topo_" + n);
                Vector3 c = die.transform.InverseTransformPoint(center.position);
                Vector3 t = die.transform.InverseTransformPoint(top.position);

                Vector3 normal = (faces[n] * c).normalized;
                Vector3 up = (faces[n] * (t - c)).normalized;
                Assert.Greater(Vector3.Dot(normal, Vector3.back), 0.999f, $"Face {n} de frente para a câmera");
                Assert.Greater(Vector3.Dot(up, Vector3.up), 0.999f, $"Número {n} em pé");
            }
#else
            Assert.Ignore("Só no editor (carrega o FBX pelo AssetDatabase).");
#endif
        }

        [Test]
        public void FacesOpostasSomamVinteEUm()
        {
#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(D20ModelPath);
            die = Object.Instantiate(model);
            for (int n = 1; n <= 10; n++)
            {
                Vector3 a = die.transform.InverseTransformPoint(Find(die.transform, "Centro_" + n).position);
                Vector3 b = die.transform.InverseTransformPoint(Find(die.transform, "Centro_" + (21 - n)).position);
                Assert.Less(Vector3.Dot(a.normalized, b.normalized), -0.999f, $"{n} e {21 - n} em lados opostos");
            }
#else
            Assert.Ignore("Só no editor.");
#endif
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            Assert.Fail($"O modelo não tem {name}");
            return null;
        }
    }
}
