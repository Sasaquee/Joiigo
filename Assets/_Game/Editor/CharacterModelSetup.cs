using System.IO;
using Game.Player;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Põe o modelo do Andarilho (D-040) no prefab do jogador, chamado pelo ArenaBuilder no lugar do Manequim.
    /// O modelo vem de Tools/Blender/build_character.py: peças separadas com o pivô na junta.
    /// </summary>
    public static class CharacterModelSetup
    {
        private const string ModelPath = "Assets/_Game/Art/Models/Character/Andarilho.fbx";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";

        /// <summary>
        /// Instancia o Andarilho como filho "Corpo" da raiz do jogador (o PlayerLife deita esse objeto na queda)
        /// e liga o PlayerVisualAnimator às peças.
        /// </summary>
        public static GameObject CreateBody(Transform playerRoot)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
                throw new FileNotFoundException($"Modelo {ModelPath} não encontrado. Rode Tools/Blender/build_character.py.");

            var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, playerRoot);
            body.name = "Corpo";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = false;

            ApplyPaletteMaterials(body);

            var lantern = Find(body.transform, "Lanterna");
            if (lantern != null && body.transform.InverseTransformPoint(lantern.position).z < 0f)
                Debug.LogWarning("Andarilho: a lanterna ficou atrás do corpo; o FBX parece ter saído com a frente em -Z.");

            var animator = body.AddComponent<PlayerVisualAnimator>();
            animator.SetParts(
                Find(body.transform, "Torso"),
                Find(body.transform, "Cabeca"),
                Find(body.transform, "BracoEsq"),
                Find(body.transform, "BracoDir"),
                Find(body.transform, "PernaEsq"),
                Find(body.transform, "PernaDir"),
                Find(body.transform, "Capa"),
                lantern,
                playerRoot);
            return body;
        }

        /// <summary>Troca cada material do FBX pelo da paleta com o mesmo nome, se ele existir em Art/Materials.</summary>
        private static void ApplyPaletteMaterials(GameObject body)
        {
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                        continue;
                    var palette = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{mats[i].name}.mat");
                    if (palette != null && palette != mats[i])
                    {
                        mats[i] = palette;
                        changed = true;
                    }
                }
                if (changed)
                    r.sharedMaterials = mats;
            }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            Debug.LogWarning($"Andarilho: peça \"{name}\" não encontrada no modelo.");
            return null;
        }
    }
}
