using Game.Net;
using UnityEngine;

namespace Game.Arena
{
    /// <summary>Portão-máquina parado e apagado até a largada; depois a engrenagem gira e o cristal acende (D-013).</summary>
    public class GateActivation : MonoBehaviour
    {
        [SerializeField] private MatchState session;
        [SerializeField] private Spinner gearSpinner;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private Material crystalLit;
        [SerializeField] private Material crystalOff;

        private bool? active;

        public void Configure(MatchState newSession, Spinner spinner, Renderer[] newRenderers, Material lit, Material off)
        {
            session = newSession;
            gearSpinner = spinner;
            renderers = newRenderers;
            crystalLit = lit;
            crystalOff = off;
        }

        private void Start() => Apply(false);

        private void Update()
        {
            bool on = session != null && session.IsStarted;
            if (active != on)
                Apply(on);
        }

        private void Apply(bool on)
        {
            active = on;
            if (gearSpinner != null)
                gearSpinner.enabled = on;

            Material from = on ? crystalOff : crystalLit;
            Material to = on ? crystalLit : crystalOff;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == from)
                        mats[i] = to;
                r.sharedMaterials = mats;
            }
        }
    }
}
