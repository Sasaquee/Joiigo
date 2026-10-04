using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Materiais que os visuais das cartas usam em runtime (o código não acessa o AssetDatabase).
    /// Preenchido pelo CardAssetsBuilder. Faltando algum, o CardVisuals cria um material simples no lugar.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cards/Visual Library", fileName = "CardVisualLibrary")]
    public class CardVisualLibrary : ScriptableObject
    {
        [Tooltip("Vapor: alfa, partícula suave branca (Art/Ambience/ParticulaVapor).")]
        public Material steam;
        [Tooltip("Brasa e faísca laranja: aditivo (Art/Ambience/ParticulaBrasa).")]
        public Material ember;
        [Tooltip("Runas e faíscas arcanas ciano: aditivo (Art/Ambience/ParticulaPoeira).")]
        public Material arcane;
        [Tooltip("Latão para peças em malha (escudo).")]
        public Material brass;
        [Tooltip("Cristal emissivo para peças em malha.")]
        public Material crystal;
    }
}
