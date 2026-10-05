using Game.Cards;
using Game.Combat;
using Game.Core.Aura;
using Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Aura
{
    /// <summary>
    /// Aura do jogador (Fase 7): a única forma de ver HP, energia e estados (D-060 a D-063). Cada cliente monta a aura
    /// de todos os jogadores a partir do que já vem pela rede (vida, energia, espaços, caído e os sinais do host).
    /// O mapeamento estado → aura é do Core (AuraMapper); aqui só se junta a entrada, suaviza e entrega ao
    /// visual (AuraVisual) e, no seu personagem, aos sons (AuraAudio).
    /// </summary>
    public class PlayerAura : MonoBehaviour
    {
        [SerializeField] private AuraSettings settings;
        [SerializeField] private AuraVisual visual;
        [SerializeField] private AuraAudio audioCues;

        private NetworkHealth health;
        private PlayerCards cards;
        private PlayerLife life;
        private NetworkObject netObject;
        private AuraTuning tuning;

        private float shownHealth = 1f;
        private float shownEnergy;
        private bool initialized;

        /// <summary>Estado mapeado no último quadro (para testes e para o visual).</summary>
        public AuraState Current { get; private set; }

        /// <summary>Sinais vistos no último quadro.</summary>
        public AuraSignals Signals => Current.Signals;

        public AuraSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public void Configure(AuraSettings auraSettings, AuraVisual auraVisual, AuraAudio auraAudio)
        {
            settings = auraSettings;
            visual = auraVisual;
            audioCues = auraAudio;
        }

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            cards = GetComponent<PlayerCards>();
            life = GetComponent<PlayerLife>();
            netObject = GetComponent<NetworkObject>();
        }

        private void LateUpdate()
        {
            if (settings == null)
                return;
            tuning ??= settings.ToTuning();

            float targetHealth = health != null ? health.Fraction : 1f;
            float targetEnergy = cards != null ? cards.EnergyFraction : 0f;
            if (!initialized)
            {
                shownHealth = targetHealth;
                shownEnergy = targetEnergy;
                initialized = true;
            }
            float k = 1f - Mathf.Exp(-settings.smoothing * Time.deltaTime);
            shownHealth = Mathf.Lerp(shownHealth, targetHealth, k);
            shownEnergy = Mathf.Lerp(shownEnergy, targetEnergy, k);

            Current = AuraMapper.Map(new AuraInput(shownHealth, shownEnergy, ReadSignals()), tuning, AuraPaletteSwitch.Current);

            if (visual != null)
                visual.Apply(Current, Time.deltaTime);
            if (audioCues != null)
                audioCues.Tick(Current, IsLocal, Time.deltaTime);
        }

        private bool IsLocal => netObject != null && netObject.IsSpawned && netObject.IsOwner;

        private AuraSignals ReadSignals()
        {
            AuraSignals signals = AuraSignals.None;
            if (life != null && life.IsDowned)
                signals |= AuraSignals.Downed;
            if (cards != null)
            {
                if (cards.ShieldActive)
                    signals |= AuraSignals.Shield;
                if (cards.CursedEquipped)
                    signals |= AuraSignals.Curse;
                if (cards.HurtBonusActive)
                    signals |= AuraSignals.HurtBonus;
            }
            return signals;
        }
    }
}
