using Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Alavanca-máquina da largada (D-013). Só o host pode puxar. Os cristais da alavanca acendem
    /// na tela do host quando ele está perto, como pista do que fazer, sem texto.
    /// </summary>
    public class StartLever : NetworkBehaviour, IInteractable
    {
        [SerializeField] private MatchState session;
        [SerializeField] private InteractionSettings interaction;
        [SerializeField] private Transform arm;
        [SerializeField] private Renderer[] crystalRenderers = new Renderer[0];
        [SerializeField] private Material crystalOff;
        [SerializeField] private Material crystalLit;
        [SerializeField] private float armUpAngle = -35f;
        [SerializeField] private float armDownAngle = 35f;
        [SerializeField] private float armSpeed = 180f;

        private bool? lit;

        public void Configure(MatchState newSession, InteractionSettings newInteraction, Transform newArm,
            Renderer[] renderers, Material off, Material on)
        {
            session = newSession;
            interaction = newInteraction;
            arm = newArm;
            crystalRenderers = renderers;
            crystalOff = off;
            crystalLit = on;
        }

        public void ServerInteract(ulong requesterClientId)
        {
            if (!IsServer || requesterClientId != NetworkManager.ServerClientId)
                return;
            session.ServerStart();
        }

        private void Update()
        {
            bool started = session != null && session.IsStarted;

            if (arm != null)
            {
                Quaternion target = Quaternion.Euler(started ? armDownAngle : armUpAngle, 0f, 0f);
                arm.localRotation = Quaternion.RotateTowards(arm.localRotation, target, armSpeed * Time.deltaTime);
            }

            bool shouldLight = started || HostIsNear();
            if (lit != shouldLight)
                SetLit(shouldLight);
        }

        private void SetLit(bool on)
        {
            lit = on;
            Material to = on ? crystalLit : crystalOff;
            foreach (var r in crystalRenderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == crystalLit || mats[i] == crystalOff)
                        mats[i] = to;
                r.sharedMaterials = mats;
            }
        }

        private bool HostIsNear()
        {
            if (!IsSpawned || !IsHost || interaction == null)
                return false;
            var player = NetworkManager.LocalClient?.PlayerObject;
            if (player == null)
                return false;
            float r = interaction.interactRadius;
            return (player.transform.position - transform.position).sqrMagnitude <= r * r;
        }
    }
}
