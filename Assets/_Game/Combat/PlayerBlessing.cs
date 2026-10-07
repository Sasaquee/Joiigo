using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Bênção de dano do 20 do D20 no coop (D-085). O host decide e anda o relógio (BlessingState, Core); todos leem a flag
    /// publicada para a aura dourada (PlayerAura). O dano multiplicado é calculado só no host: o golpe básico (PlayerCombat) e os
    /// efeitos das cartas (ICardUser.DamageMultiplier) leem o ServerMultiplier na hora de montar o DamagePacket, e ele multiplica
    /// junto com os outros bônus (Mola de Recuo, qualidade da carta). Quem concede é o CardDropService.
    /// </summary>
    public class PlayerBlessing : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> activeNet = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly BlessingState state = new BlessingState();

        /// <summary>Em todos: a bênção vale agora (no host vem do relógio; nos clientes, da flag publicada).</summary>
        public bool Active => IsServer && IsSpawned ? state.Active : activeNet.Value;

        /// <summary>Host: segundos que faltam (0 = inativa). Para testes e debug.</summary>
        public float ServerRemaining => IsServer ? state.Remaining : 0f;

        /// <summary>Host: multiplicador do dano causado por este jogador agora (1 sem bênção).</summary>
        public float ServerMultiplier => IsServer ? state.Multiplier : 1f;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                state.Clear();
                activeNet.Value = false;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
                return;
            state.Tick(Time.deltaTime);
            if (activeNet.Value != state.Active)
                activeNet.Value = state.Active;
        }

        /// <summary>Host: dá a bênção, ou renova se já está ativa (reinicia a duração, não empilha o multiplicador).</summary>
        public void ServerGrant(float duration, float damageMultiplier)
        {
            if (!IsServer || !IsSpawned)
                return;
            state.Begin(duration, damageMultiplier);
            if (activeNet.Value != state.Active)
                activeNet.Value = state.Active;
        }

        /// <summary>Host: encerra na hora (recomeço da partida).</summary>
        public void ServerClear()
        {
            if (!IsServer)
                return;
            state.Clear();
            if (activeNet.Value)
                activeNet.Value = false;
        }
    }
}
