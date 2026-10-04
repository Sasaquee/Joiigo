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
        private readonly NetworkVariable<CooldownState> cooldownNet = new NetworkVariable<CooldownState>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        public CardDatabase Database => database;
        public CardsSettings Settings => settings;

        public ModifierSet Modifiers => modifiers;
        public PathTracker Path => path;

        /// <summary>Cartas que o jogador tem e não equipou. Cada mudança troca a lista, então dá para percorrer sem medo.</summary>
        public IReadOnlyList<int> Inventory => inventoryView;

        public float Energy => IsServer && energy != null ? energy.Current : energyNet.Value;
        public float EnergyMax => IsServer && energy != null ? energy.Max : energyMaxNet.Value;
        public float EnergyFraction => EnergyMax > 0f ? Mathf.Clamp01(Energy / EnergyMax) : 0f;

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
        }

        public override void OnNetworkSpawn()
        {
            if (visualLibrary != null)
                CardVisuals.Library = visualLibrary;

            loadoutNet.OnValueChanged += OnLoadoutChanged;
            cooldownNet.OnValueChanged += OnCooldownChanged;
            inventoryNet.OnListChanged += OnInventoryChanged;

            if (IsServer)
            {
                loadout = new Loadout(id => database != null ? database.KindOf(id) : CardKind.Skill);
                float max = settings != null ? settings.maxEnergy : 100f;
                float regen = settings != null ? settings.regenPerSecond : 4f;
                float start = settings != null ? settings.startEnergyFraction : 1f;
                energy = new EnergyModel(max, regen, start);
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
            }

            if (changedPending)
            {
                changedPending = false;
                Changed?.Invoke();
            }
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

        /// <summary>Host: põe uma carta no inventário (chão, debug).</summary>
        public void ServerGiveCard(int cardId)
        {
            if (!IsServer || loadout == null || database == null || database.Get(cardId) == null)
                return;
            loadout.AddToInventory(cardId);
            Sync();
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

            Perform(card, aim);
            return true;
        }

        public bool ServerUseBelt(int beltIndex, Vector3 aim)
        {
            if (!IsServer || loadout == null || database == null || !IsFinite(aim))
                return false;
            if (beltIndex < 0 || beltIndex >= CardRules.BeltSlots || !CanAct)
                return false;

            CardData card = database.Get(loadout.Get(SlotType.Belt, beltIndex));
            if (card == null || card.Kind != CardKind.Item || !energy.CanSpend(card.energyCost))
                return false;

            energy.TrySpend(card.energyCost);
            loadout.ConsumeBelt(beltIndex);
            Sync();
            PublishEnergy(true);

            Perform(card, aim);
            return true;
        }

        /// <summary>Custo em vida, caminho e efeitos, depois que energia e recarga já foram cobradas.</summary>
        private void Perform(CardData card, Vector3 aim)
        {
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
            foreach (ModifierEntry entry in card.modifiers)
                modifiers.Add(entry.ToCore());
        }

        // ---------- ICardUser ----------

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
