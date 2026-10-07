using System;
using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Cards
{
    /// <summary>
    /// Cartas do jogador (§3.3, §4.4), com o host como autoridade. O dono só pede (Request*); o host valida
    /// (vivo, espaço, recarga, energia), executa os efeitos da carta e publica o resultado:
    /// inventário em NetworkList, cartas equipadas em LoadoutState, energia e recargas em variáveis de rede.
    /// A recarga é presa ao espaço de skill (trocar a carta não zera a recarga).
    /// </summary>
    public class PlayerCards : NetworkBehaviour, ICardUser
    {
        // Só republica a energia quando muda pelo menos isto (ou ao gastar/ganhar de uma vez).
        private const float EnergyPublishStep = 0.5f;

        [SerializeField] private CardDatabase database;
        [SerializeField] private CardsSettings settings;
        [SerializeField] private CardVisualLibrary visualLibrary;

        private readonly NetworkList<int> inventoryNet = new NetworkList<int>();
        private readonly NetworkVariable<LoadoutState> loadoutNet = new NetworkVariable<LoadoutState>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> energyNet = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> energyMaxNet = new NetworkVariable<float>(100f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Qualidade de cada carta do banco, por id (D-048). Vale para todas as cópias da carta.
        private readonly NetworkList<int> qualityNet = new NetworkList<int>();
        private readonly NetworkVariable<CooldownState> cooldownNet = new NetworkVariable<CooldownState>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Sinais da aura que só o host conhece (D-063): escudo ligado e reforço da Mola de Recuo valendo.
        private readonly NetworkVariable<byte> auraFlagsNet = new NetworkVariable<byte>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private const byte AuraFlagShield = 1;
        private const byte AuraFlagHurtBonus = 2;
        private PlayerShield shield;
        private PlayerBlessing blessing; // pode faltar (prefab antigo): sem bênção o dano não muda (D-085)

        // Host
        private readonly ModifierSet modifiers = new ModifierSet();
        private readonly PathTracker path = new PathTracker();
        private readonly Cooldown[] cooldowns =
            { new Cooldown(), new Cooldown(), new Cooldown(), new Cooldown() };
        private Loadout loadout;
        private EnergyModel energy;
        private CooldownState cooldownState;
        private float lastPublishedEnergy = -1f;
        private float lastHurtTime = -999f;
        private bool hurtArmed;

        // Todos
        private List<int> inventoryView = new List<int>();
        private bool changedPending;

        private NetworkHealth health;
        private PlayerLife life;
        private Vector3 aimPoint;
        private Vector3 aimDirection = Vector3.forward;

        /// <summary>Em todos: mudou o inventário, as cartas equipadas, o cinto ou uma recarga. A regeneração contínua da energia não dispara; leia EnergyFraction a cada quadro.</summary>
        public event Action Changed;

        /// <summary>Em todos: a qualidade de uma carta mudou (id, nova qualidade). Serve para a revelação da melhoria.</summary>
        public event Action<int, CardQuality> QualityChanged;

        public CardDatabase Database => database;
        public CardsSettings Settings => settings;

        public ModifierSet Modifiers => modifiers;
        public PathTracker Path => path;

        /// <summary>Cartas que o jogador tem e não equipou. Cada mudança troca a lista, então dá para percorrer sem medo.</summary>
        public IReadOnlyList<int> Inventory => inventoryView;

        public float Energy => IsServer && energy != null ? energy.Current : energyNet.Value;
        public float EnergyMax => IsServer && energy != null ? energy.Max : energyMaxNet.Value;
        public float EnergyFraction => EnergyMax > 0f ? Mathf.Clamp01(Energy / EnergyMax) : 0f;

        /// <summary>Escudo do Broquel ligado agora (todos leem; o host publica). Sinal da aura (D-063).</summary>
        public bool ShieldActive => (auraFlagsNet.Value & AuraFlagShield) != 0;

        /// <summary>Reforço da Mola de Recuo valendo agora (todos leem; o host publica). Sinal da aura (D-063).</summary>
        public bool HurtBonusActive => (auraFlagsNet.Value & AuraFlagHurtBonus) != 0;

        /// <summary>Alguma carta amaldiçoada nos espaços de skill (ela cobra vida a cada uso). Sinal da aura (D-063).</summary>
        public bool CursedEquipped
        {
            get
            {
                if (database == null)
                    return false;
                for (int i = 0; i < CardRules.SkillSlots; i++)
                {
                    CardData data = database.Get(GetSlot(SlotType.Skill, i));
                    if (data != null && data.cursed)
                        return true;
                }
                return false;
            }
        }

        // ---------- ICardUser ----------

        public Transform Transform => transform;
        public ulong ClientId => OwnerClientId;
        public Vector3 AimPoint => aimPoint;
        public Vector3 AimDirection => aimDirection;
        public NetworkHealth Health => health;

        private bool CanAct => life == null || life.CanAct;

        private void Awake()
        {
            health = GetComponent<NetworkHealth>();
            life = GetComponent<PlayerLife>();
            blessing = GetComponent<PlayerBlessing>();
        }

        public override void OnNetworkSpawn()
        {
            if (visualLibrary != null)
                CardVisuals.Library = visualLibrary;

            loadoutNet.OnValueChanged += OnLoadoutChanged;
            cooldownNet.OnValueChanged += OnCooldownChanged;
            inventoryNet.OnListChanged += OnInventoryChanged;
            qualityNet.OnListChanged += OnQualityChanged;

            if (IsServer)
            {
                qualityNet.Clear();
                int cardCount = database != null ? database.Count : 0;
                for (int i = 0; i < cardCount; i++)
                    qualityNet.Add((int)CardQuality.Good); // cartas dadas sem dado (debug) são boas

                loadout = new Loadout(id => database != null ? database.KindOf(id) : CardKind.Skill);
                energy = CreateStartEnergy();
                cooldownState = default;
                lastPublishedEnergy = -1f;
                hurtArmed = false;
                for (int i = 0; i < cooldowns.Length; i++)
                    cooldowns[i].Reset();

                energyMaxNet.Value = energy.Max;
                PublishEnergy(true);
                Sync();

                if (health != null)
                    health.Damaged += OnPlayerDamaged;
            }

            RefreshInventoryView();
            RebuildModifiers();
            changedPending = true;
        }

        public override void OnNetworkDespawn()
        {
            loadoutNet.OnValueChanged -= OnLoadoutChanged;
            cooldownNet.OnValueChanged -= OnCooldownChanged;
            inventoryNet.OnListChanged -= OnInventoryChanged;
            qualityNet.OnListChanged -= OnQualityChanged;
            if (health != null)
                health.Damaged -= OnPlayerDamaged;
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (IsServer && loadout != null)
            {
                float dt = Time.deltaTime;
                energy.Tick(dt);
                for (int i = 0; i < cooldowns.Length; i++)
                    cooldowns[i].Tick(dt);
                PublishEnergy(false);
                PublishAuraFlags();
            }

            if (changedPending)
            {
                changedPending = false;
                Changed?.Invoke();
            }
        }

        private void PublishAuraFlags()
        {
            if (shield == null)
                shield = GetComponent<PlayerShield>(); // o Broquel cria o componente no primeiro uso
            byte flags = 0;
            if (shield != null && shield.IsActive)
                flags |= AuraFlagShield;
            if (ServerHurtBonus > 0f)
                flags |= AuraFlagHurtBonus;
            if (auraFlagsNet.Value != flags)
                auraFlagsNet.Value = flags;
        }

        // ---------- Leitura (todos) ----------

        /// <summary>Id da carta no espaço, ou Loadout.Empty (-1).</summary>
        public int GetSlot(SlotType slot, int index)
        {
            if (IsServer && loadout != null)
                return loadout.Get(slot, index);
            return loadoutNet.Value.Get(slot, index);
        }

        /// <summary>Quantas cópias há no espaço do cinto.</summary>
        public int BeltCount(int index)
        {
            if (IsServer && loadout != null)
                return loadout.BeltCount(index);
            return loadoutNet.Value.BeltCount(index);
        }

        /// <summary>1 = acabou de usar, 0 = pronta. Calculado pelo fim da recarga em tempo de servidor.</summary>
        public float SkillCooldownFraction(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= CardRules.SkillSlots || NetworkManager == null || !IsSpawned)
                return 0f;

            CooldownState state = cooldownNet.Value;
            float duration = state.DurationOf(skillIndex);
            if (duration <= 0f)
                return 0f;

            double remaining = state.EndOf(skillIndex) - NetworkManager.ServerTime.Time;
            return remaining <= 0d ? 0f : Mathf.Clamp01((float)(remaining / duration));
        }

        /// <summary>Id de uma carta pelo identificador de texto (ex.: "pistao_runico"), ou -1.</summary>
        public int FindCardId(string cardId)
        {
            if (database == null)
                return -1;
            for (int i = 0; i < database.Count; i++)
                if (database.Get(i) != null && database.Get(i).id == cardId)
                    return i;
            return -1;
        }

        // ---------- Pedidos do dono ----------

        public void RequestEquip(int cardId, SlotType slot, int index)
        {
            if (!IsSpawned)
                return;
            if (IsServer)
                ServerEquip(cardId, slot, index);
            else
                EquipRpc(cardId, (int)slot, index);
        }

        public void RequestUnequip(SlotType slot, int index)
        {
            if (!IsSpawned)
                return;
            if (IsServer)
                ServerUnequip(slot, index);
            else
                UnequipRpc((int)slot, index);
        }

        public void RequestUseSkill(int skillIndex, Vector3 aimPoint)
        {
            if (!IsSpawned || !IsFinite(aimPoint))
                return;
            if (IsServer)
                ServerUseSkill(skillIndex, aimPoint);
            else
                UseSkillRpc(skillIndex, aimPoint);
        }

        public void RequestUseBelt(int beltIndex, Vector3 aimPoint)
        {
            if (!IsSpawned || !IsFinite(aimPoint))
                return;
            if (IsServer)
                ServerUseBelt(beltIndex, aimPoint);
            else
                UseBeltRpc(beltIndex, aimPoint);
        }

        [Rpc(SendTo.Server)]
        private void EquipRpc(int cardId, int slot, int index, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !Enum.IsDefined(typeof(SlotType), slot))
                return;
            ServerEquip(cardId, (SlotType)slot, index);
        }

        [Rpc(SendTo.Server)]
        private void UnequipRpc(int slot, int index, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !Enum.IsDefined(typeof(SlotType), slot))
                return;
            ServerUnequip((SlotType)slot, index);
        }

        [Rpc(SendTo.Server)]
        private void UseSkillRpc(int skillIndex, Vector3 aim, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;
            ServerUseSkill(skillIndex, aim);
        }

        [Rpc(SendTo.Server)]
        private void UseBeltRpc(int beltIndex, Vector3 aim, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;
            ServerUseBelt(beltIndex, aim);
        }

        // ---------- Host ----------

        /// <summary>Qualidade atual da carta (vale para todas as cópias).</summary>
        public CardQuality QualityOf(int cardId) =>
            cardId >= 0 && cardId < qualityNet.Count ? (CardQuality)qualityNet[cardId] : CardQuality.Good;

        /// <summary>Multiplicador da qualidade nos números da carta (CardsSettings).</summary>
        public float QualityMultiplier(CardQuality quality) => settings != null ? settings.QualityMultiplier(quality) : 1f;

        /// <summary>O jogador tem a carta (inventário, equipada ou no cinto).</summary>
        public bool Owns(int cardId)
        {
            if (cardId < 0)
                return false;
            if (IsServer && loadout != null)
                return loadout.Has(cardId);
            if (inventoryView.Contains(cardId))
                return true;
            for (int i = 0; i < CardRules.SkillSlots; i++)
                if (GetSlot(SlotType.Skill, i) == cardId) return true;
            for (int i = 0; i < CardRules.PassiveSlots; i++)
                if (GetSlot(SlotType.Passive, i) == cardId) return true;
            for (int i = 0; i < CardRules.EquipmentSlots; i++)
                if (GetSlot(SlotType.Equipment, i) == cardId) return true;
            for (int i = 0; i < CardRules.BeltSlots; i++)
                if (GetSlot(SlotType.Belt, i) == cardId) return true;
            return false;
        }

        /// <summary>Qualidade da carta se o jogador a tem; null se não tem. É o que o sorteio da carta do chão consulta.</summary>
        public CardQuality? OwnedQuality(int cardId) => Owns(cardId) ? QualityOf(cardId) : (CardQuality?)null;

        /// <summary>Host: entrega as cartas do resultado do dado (nova, melhoria ou cópia; D-051, D-052).</summary>
        public void ServerApplyGrants(IReadOnlyList<CardGrant> grants)
        {
            if (!IsServer || loadout == null || database == null || grants == null)
                return;
            foreach (CardGrant grant in grants)
            {
                if (database.Get(grant.CardId) == null)
                    continue;
                if (grant.Kind != GrantKind.Copy && grant.CardId < qualityNet.Count)
                    qualityNet[grant.CardId] = (int)grant.Quality;
                if (grant.Kind != GrantKind.Upgrade)
                    loadout.AddToInventory(grant.CardId);
            }
            Sync();
            RebuildModifiers();
        }

        /// <summary>Host: põe uma carta no inventário (chão, debug).</summary>
        public void ServerGiveCard(int cardId)
        {
            if (!IsServer || loadout == null || database == null || database.Get(cardId) == null)
                return;
            loadout.AddToInventory(cardId);
            Sync();
        }

        /// <summary>
        /// Host: queda total (D-084). Todas as cartas somem (inventário, espaços equipados e cinto), as qualidades voltam ao padrão,
        /// o caminho de tags é esquecido, a energia volta ao valor de início e escudo, recargas e efeitos em andamento acabam.
        /// </summary>
        public void ServerClearAll()
        {
            if (!IsServer || loadout == null)
                return;

            StopAllCoroutines(); // sopros e arremessos de carta ainda no ar (ICardUser.Run)
            if (shield == null)
                shield = GetComponent<PlayerShield>();
            if (shield != null)
                shield.Deactivate();

            loadout.Clear();
            path.Clear();
            for (int i = 0; i < qualityNet.Count; i++)
            {
                if (qualityNet[i] != (int)CardQuality.Good)
                    qualityNet[i] = (int)CardQuality.Good; // o padrão de quem nasce (cartas dadas sem dado são boas)
            }

            energy = CreateStartEnergy();
            for (int i = 0; i < cooldowns.Length; i++)
                cooldowns[i].Reset();
            cooldownState = default;
            cooldownNet.Value = cooldownState;
            hurtArmed = false;
            lastHurtTime = -999f;
            Potency = 1f;

            PublishEnergy(true);
            Sync(); // publica inventário e espaços vazios e refaz os modificadores
        }

        /// <summary>Energia de quem acabou de nascer: cheia (ou a fração de início do CardsSettings).</summary>
        private EnergyModel CreateStartEnergy()
        {
            float max = settings != null ? settings.maxEnergy : 100f;
            float regen = settings != null ? settings.regenPerSecond : 4f;
            float start = settings != null ? settings.startEnergyFraction : 1f;
            return new EnergyModel(max, regen, start);
        }

        /// <summary>Host: o golpe básico acertou. Gera energia (com o bônus da Caldeira Interna) e gasta o reforço da Mola de Recuo.</summary>
        public void ServerOnBasicHit()
        {
            if (!IsServer || energy == null)
                return;
            float perHit = settings != null ? settings.energyPerHit : 8f;
            energy.Add(perHit * (1f + modifiers.Get(ModifierKind.EnergyOnHitBonus)));
            PublishEnergy(true);
            if (ServerHurtBonus > 0f)
                hurtArmed = false; // este golpe gastou o reforço
        }

        /// <summary>Host: reforço da Mola de Recuo valendo agora (0 se não há passiva ou a janela passou).</summary>
        public float ServerHurtBonus
        {
            get
            {
                if (!IsServer || !hurtArmed)
                    return 0f;
                float window = settings != null ? settings.hurtBonusWindow : 3f;
                if (Time.time - lastHurtTime > window)
                    return 0f;
                return Mathf.Max(0f, modifiers.Get(ModifierKind.HurtNextHitBonus));
            }
        }

        public bool ServerUseSkill(int skillIndex, Vector3 aim)
        {
            if (!IsServer || loadout == null || database == null || !IsFinite(aim))
                return false;
            if (skillIndex < 0 || skillIndex >= CardRules.SkillSlots || !CanAct)
                return false;

            CardData card = database.Get(loadout.Get(SlotType.Skill, skillIndex));
            if (card == null || card.Kind != CardKind.Skill)
                return false;
            if (!cooldowns[skillIndex].Ready || !energy.CanSpend(card.energyCost))
                return false;

            energy.TrySpend(card.energyCost);
            float duration = card.cooldown * modifiers.CooldownMultiplier;
            cooldowns[skillIndex].TryUse(duration);
            cooldownState.Set(skillIndex, NetworkManager.ServerTime.Time + duration, duration);
            cooldownNet.Value = cooldownState;
            PublishEnergy(true);

            Perform(loadout.Get(SlotType.Skill, skillIndex), card, aim);
            return true;
        }

        public bool ServerUseBelt(int beltIndex, Vector3 aim)
        {
            if (!IsServer || loadout == null || database == null || !IsFinite(aim))
                return false;
            if (beltIndex < 0 || beltIndex >= CardRules.BeltSlots || !CanAct)
                return false;

            int beltCardId = loadout.Get(SlotType.Belt, beltIndex);
            CardData card = database.Get(beltCardId);
            if (card == null || card.Kind != CardKind.Item || !energy.CanSpend(card.energyCost))
                return false;

            energy.TrySpend(card.energyCost);
            loadout.ConsumeBelt(beltIndex);
            Sync();
            PublishEnergy(true);

            Perform(beltCardId, card, aim);
            return true;
        }

        /// <summary>Custo em vida, caminho e efeitos, depois que energia e recarga já foram cobradas.</summary>
        private void Perform(int cardId, CardData card, Vector3 aim)
        {
            Potency = QualityMultiplier(QualityOf(cardId));
            aimPoint = aim;
            Vector3 flat = new Vector3(aim.x - transform.position.x, 0f, aim.z - transform.position.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                flat = transform.forward;
                flat.y = 0f;
            }
            aimDirection = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;

            if (card.healthCostOnUse > 0f && health != null)
                health.ServerApplyDamage(new DamagePacket(card.healthCostOnUse, 0f), OwnerClientId);

            path.Record(card.tags);

            for (int i = 0; i < card.effects.Count; i++)
            {
                CardEffect effect = card.effects[i];
                if (effect == null)
                    continue;
                try
                {
                    effect.Execute(this, card);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this); // um efeito quebrado não impede os outros
                }
            }
            Potency = 1f;
        }

        private void ServerEquip(int cardId, SlotType slot, int index)
        {
            if (loadout == null || database == null || database.Get(cardId) == null)
                return;
            if (loadout.TryEquip(cardId, slot, index) == EquipResult.Ok)
                Sync();
        }

        private void ServerUnequip(SlotType slot, int index)
        {
            if (loadout != null && loadout.Unequip(slot, index))
                Sync();
        }

        private void OnPlayerDamaged(float applied)
        {
            lastHurtTime = Time.time;
            hurtArmed = true;
        }

        // ---------- Publicação ----------

        /// <summary>Host: publica inventário e cartas equipadas e refaz os modificadores.</summary>
        private void Sync()
        {
            loadoutNet.Value = LoadoutState.From(loadout);

            IReadOnlyList<int> inv = loadout.Inventory;
            bool same = inv.Count == inventoryNet.Count;
            for (int i = 0; same && i < inv.Count; i++)
                same = inventoryNet[i] == inv[i];
            if (!same)
            {
                inventoryNet.Clear();
                for (int i = 0; i < inv.Count; i++)
                    inventoryNet.Add(inv[i]);
            }

            RefreshInventoryView();
            RebuildModifiers();
            changedPending = true;
        }

        private void PublishEnergy(bool force)
        {
            if (energy == null)
                return;
            float current = energy.Current;
            bool edge = (current >= energy.Max && lastPublishedEnergy < energy.Max) || (current <= 0f && lastPublishedEnergy > 0f);
            if (!force && !edge && Mathf.Abs(current - lastPublishedEnergy) < EnergyPublishStep)
                return;
            lastPublishedEnergy = current;
            energyNet.Value = current;
        }

        private void OnLoadoutChanged(LoadoutState previous, LoadoutState current)
        {
            RebuildModifiers();
            changedPending = true;
        }

        private void OnCooldownChanged(CooldownState previous, CooldownState current) => changedPending = true;

        private void OnQualityChanged(NetworkListEvent<int> change)
        {
            RebuildModifiers();
            changedPending = true;
            if (change.Type == NetworkListEvent<int>.EventType.Value && change.Value != change.PreviousValue)
                QualityChanged?.Invoke(change.Index, (CardQuality)change.Value);
        }

        private void OnInventoryChanged(NetworkListEvent<int> change)
        {
            RefreshInventoryView();
            changedPending = true;
        }

        private void RefreshInventoryView()
        {
            var view = new List<int>();
            if (IsServer && loadout != null)
            {
                view.AddRange(loadout.Inventory);
            }
            else
            {
                for (int i = 0; i < inventoryNet.Count; i++)
                    view.Add(inventoryNet[i]);
            }
            inventoryView = view;
        }

        /// <summary>Soma os modificadores das passivas e equipamentos em uso (em todos; no host vem do Loadout do Core).</summary>
        private void RebuildModifiers()
        {
            modifiers.Clear();
            if (database == null)
                return;

            if (IsServer && loadout != null)
            {
                foreach (int id in loadout.EquippedModifierCards())
                    AddModifiersOf(id);
                return;
            }

            for (int i = 0; i < CardRules.PassiveSlots; i++)
                AddModifiersOf(GetSlot(SlotType.Passive, i));
            for (int i = 0; i < CardRules.EquipmentSlots; i++)
                AddModifiersOf(GetSlot(SlotType.Equipment, i));
        }

        private void AddModifiersOf(int cardId)
        {
            CardData card = database.Get(cardId);
            if (card == null)
                return;
            float mult = QualityMultiplier(QualityOf(cardId));
            foreach (ModifierEntry entry in card.modifiers)
                modifiers.Add(new Modifier(entry.kind, entry.value * mult));
        }

        // ---------- ICardUser ----------

        /// <summary>Multiplicador da qualidade da carta em uso (1 fora de um uso). Os efeitos multiplicam seus números por ele.</summary>
        public float Potency { get; private set; } = 1f;

        /// <summary>Host: multiplicador do dano da bênção do 20 no coop (D-085); 1 sem bênção. Os efeitos de dano multiplicam por ele.</summary>
        public float DamageMultiplier => blessing != null ? blessing.ServerMultiplier : 1f;

        public void AddEnergy(float amount)
        {
            if (!IsServer || energy == null)
                return;
            energy.Add(amount);
            PublishEnergy(true);
        }

        public IEnumerable<IDamageable> EnemiesInRadius(Vector3 center, float radius)
        {
            var found = new List<IDamageable>();
            CardTargets.Collect(center, radius, found);
            return found;
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        public void BroadcastVisual(string visualId, Vector3 position, Vector3 direction, float size)
        {
            if (!IsServer || !IsSpawned)
                return;
            int index = CardVisuals.IndexOf(visualId);
            if (index < 0)
            {
                Debug.LogWarning($"Visual de carta desconhecido: {visualId}");
                return;
            }
            VisualRpc(index, position, direction, size);
        }

        [Rpc(SendTo.Everyone)]
        private void VisualRpc(int visualIndex, Vector3 position, Vector3 direction, float size)
        {
            CardVisuals.PlayIndex(visualIndex, position, direction, size, transform);
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
