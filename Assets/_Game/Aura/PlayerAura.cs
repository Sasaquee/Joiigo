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
    /// visual (AuraVisual) e, no seu personagem, aos sons (AuraAudio). Quando a energia enche, dispara uma vez o pulso
    /// de luz (todos veem) e o "ding" (só no seu personagem) (D-067; a borda é decidida pelo Core, AuraPulseTrigger).
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
        private AuraPulseTrigger fullPulse;
        private readonly AuraSmoother smoother = new AuraSmoother();

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
            fullPulse ??= new AuraPulseTrigger(settings.fullPulseRearmBelow);

            float targetHealth = health != null ? health.Fraction : 1f;
            float targetEnergy = cards != null ? cards.EnergyFraction : 0f;
            // Antes de entrar na rede, a vida e a energia ainda não chegaram: o Core acompanha sem suavizar e só começa
            // a suavizar no primeiro quadro em rede (senão a energia "subiria" do zero e o pulso de cheia tocaria ao nascer).
            bool spawned = netObject == null || netObject.IsSpawned;
            smoother.Step(targetHealth, targetEnergy, spawned, settings.smoothing, Time.deltaTime);

            AuraSignals signals = ReadSignals();
            Current = AuraMapper.Map(new AuraInput(smoother.Health, smoother.Energy, signals), tuning, AuraPaletteSwitch.Current);

            // Energia acabou de encher (D-067): uma vez por enchida (rearma abaixo de fullPulseRearmBelow); nunca no
            // primeiro quadro em rede, nem caído, nem no primeiro quadro depois de levantar.
            bool downed = (signals & AuraSignals.Downed) != 0;
            bool pulse;
            if (spawned)
            {
                pulse = fullPulse.Update(smoother.Energy, downed);
            }
            else
            {
                fullPulse.Reset();
                pulse = false;
            }
            if (pulse)
            {
                if (visual != null)
                    visual.Pulse(Current);
                if (audioCues != null)
                    audioCues.PlayFullChime(IsLocal);
            }

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
