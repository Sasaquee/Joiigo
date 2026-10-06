using UnityEngine;

namespace Game.Arena
{
    /// <summary>
    /// Marca renderers como "vazáveis" (D-076, D-079): o shader Game/LitVazado só recorta peças com a rendering layer 8
    /// (<see cref="MapLayers.VazavelRenderingLayerMask"/>). O CityBuilder chama <see cref="Mark(Renderer)"/> nas peças da cidade;
    /// personagem, inimigos e máquinas não são marcados. O bit é somado ao que o renderer já tem, para as luzes e
    /// as demais regras da layer padrão continuarem valendo.
    /// </summary>
    public static class VazadoLayer
    {
        public static void Mark(Renderer renderer)
        {
            if (renderer == null)
                return;
            renderer.renderingLayerMask |= MapLayers.VazavelRenderingLayerMask;
        }

        /// <summary>Marca todos os MeshRenderer de um objeto e de seus filhos (inclusive desativados).</summary>
        public static void MarkAll(GameObject root)
        {
            if (root == null)
                return;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                Mark(renderer);
        }

        public static void Unmark(Renderer renderer)
        {
            if (renderer == null)
                return;
            renderer.renderingLayerMask &= ~MapLayers.VazavelRenderingLayerMask;
        }

        public static bool IsMarked(Renderer renderer)
            => renderer != null && (renderer.renderingLayerMask & MapLayers.VazavelRenderingLayerMask) != 0u;
    }
}
