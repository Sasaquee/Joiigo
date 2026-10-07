using Game.Combat;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Liga a bênção de dano do 20 no coop (D-085) ao prefab do jogador. Chamado pelo ArenaBuilder (o prefab é gerado por código);
    /// não guarda número: duração e bônus ficam no DiceSettings.
    /// </summary>
    public static class BlessingSetup
    {
        /// <summary>
        /// Adiciona o PlayerBlessing à raiz do prefab do jogador (se ainda não tem). Chamar antes de salvar o prefab e junto
        /// com os outros componentes do jogador (sem ordem especial: ele só guarda a flag da bênção).
        /// </summary>
        public static void Apply(GameObject playerPrefabRoot)
        {
            if (playerPrefabRoot == null)
                return;
            if (playerPrefabRoot.GetComponent<PlayerBlessing>() == null)
                playerPrefabRoot.AddComponent<PlayerBlessing>();
        }
    }
}
