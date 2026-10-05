using Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace Game.Dice
{
    /// <summary>
    /// Carta no chão (D-046): verso dourado, flutuando baixo e girando devagar, com um brilho ciano subindo.
    /// O verso esconde o que ela é. Pegar (F) pede ao host para rolar o D20 (CardDropService).
    /// O visual (filho "Visual") só gira e balança; a posição da rede é fixa.
    /// </summary>
    public class FloorCard : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform visual;
        [Tooltip("Altura do centro da carta acima do chão (m).")]
        [SerializeField] private float hoverHeight = 1.1f;
        [Tooltip("Quanto ela sobe e desce (m).")]
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobSpeed = 1.6f;
        [Tooltip("Giro em graus por segundo.")]
        [SerializeField] private float spinSpeed = 55f;

        private CardDropService service;
        private bool taken;
        private float phase;

        public override void OnNetworkSpawn()
        {
            phase = Random.value * 10f;
            service = FindFirstObjectByType<CardDropService>();
        }

        public void ServerInteract(ulong requesterClientId)
        {
            if (!IsServer || taken)
                return;
            if (service == null)
                service = FindFirstObjectByType<CardDropService>();
            if (service == null)
                return;
            taken = true;
            service.ServerPickUp(this, requesterClientId);
        }

        private void Update()
        {
            if (visual == null)
                return;
            float t = Time.time + phase;
            visual.localPosition = new Vector3(0f, hoverHeight + Mathf.Sin(t * bobSpeed) * bobAmplitude, 0f);
            visual.localRotation = Quaternion.Euler(0f, t * spinSpeed, 0f);
        }
    }
}
