using System.Collections.Generic;
using Game.Combat;
using Game.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Levantar um aliado caído segurando E por perto (D-083). O dono só avisa o host quando começa e quando para de
    /// segurar E (RPC, só quando muda); o host decide tudo: acha o aliado caído ao alcance, avança o ReviveProgress
    /// (Core) dele com UM aliado de cada vez (o mais perto), publica o progresso em PlayerLife (a aura mostra, sem número)
    /// e, ao completar, levanta quem caiu no lugar da queda (PlayerLife.ServerRevive). Quem levanta fica parado e sem
    /// atacar enquanto segura (PlayerLife.CanAct), exposto. Sem aliado, a queda continua acabando no spawn (D-003).
    /// </summary>
    public class PlayerRevive : NetworkBehaviour
    {
        /// <summary>
        /// Gancho de teste: aliados extras que o host também enumera, além dos clientes conectados (os testes só têm
        /// um cliente real). Vazio no jogo. Quem põe aqui tira ao terminar.
        /// </summary>
        public static readonly List<PlayerRevive> TestAllies = new List<PlayerRevive>();

        // Listas reaproveitadas a cada quadro (o host é uma thread só), para não gerar lixo.
        private static readonly List<PlayerRevive> Players = new List<PlayerRevive>();
        private static readonly List<PlayerRevive> CandidateOwners = new List<PlayerRevive>();
        private static readonly List<ReviveCandidate> Candidates = new List<ReviveCandidate>();

        [SerializeField] private ReviveSettings settings;

        private PlayerLife life;
        private bool localHolding;      // dono: o que já avisou ao host
        private bool serverHolding;     // host: este jogador segura E agora
        private ReviveProgress progress; // host: só existe enquanto este jogador está caído
        private PlayerRevive rescuer;   // host: aliado que levanta este jogador (se estiver caído)

        public ReviveSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Host: este jogador segura E agora (para testes e depuração).</summary>
        public bool ServerHolding => serverHolding;

        /// <summary>Host: aliado que levanta este jogador neste momento. Null se ninguém.</summary>
        public PlayerRevive ServerRescuer => rescuer;

        private void Awake()
        {
            life = GetComponent<PlayerLife>();
        }

        public override void OnNetworkDespawn()
        {
            TestAllies.Remove(this);
            serverHolding = false;
            localHolding = false;
            rescuer = null;
            progress = null;
        }

        // ---------- Dono ----------

        /// <summary>Dono: E está segurado agora. Só vai ao host quando muda; no host vale na hora.</summary>
        public void SetHolding(bool holding)
        {
            if (!IsSpawned || !IsOwner || holding == localHolding)
                return;
            localHolding = holding;
            if (IsServer)
                ServerSetHolding(holding);
            else
                HoldRpc(holding);
        }

        [Rpc(SendTo.Server)]
        private void HoldRpc(bool holding, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                ServerSetHolding(holding);
        }

        // ---------- Host ----------

        /// <summary>Host: registra se este jogador segura E (chamado pelo RPC do dono; testes chamam direto).</summary>
        public void ServerSetHolding(bool holding)
        {
            if (!IsServer)
                return;
            serverHolding = holding;
        }

        private void Update()
        {
            if (IsSpawned && IsServer)
                ServerTick(Time.deltaTime);
        }

        /// <summary>Host: um quadro da lógica de levantar. Público para os testes poderem avançar o tempo à mão.</summary>
        public void ServerTick(float deltaTime)
        {
            if (!IsServer || life == null || settings == null)
                return;

            CollectPlayers();
            TickAsDowned(deltaTime);
            life.ServerSetReviving(settings.rescuerFrozen && IsRescuingSomeone());
        }

        /// <summary>Se este jogador está caído: acha quem levanta, avança o progresso e levanta ao completar.</summary>
        private void TickAsDowned(float deltaTime)
        {
            if (!life.IsDowned)
            {
                // De pé (ou voltou no spawn pelo fim do tempo): sem progresso, sem aliado.
                if (progress != null)
                {
                    progress = null;
                    life.ServerSetReviveProgress(0f);
                }
                rescuer = null;
                return;
            }

            // O tempo de segurar e de decair é lido na queda, como o downedDuration: trocar o asset vale na próxima.
            progress ??= new ReviveProgress(settings.holdSeconds, settings.graceAfterRelease);

            Candidates.Clear();
            CandidateOwners.Clear();
            int keep = -1;
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerRevive other = Players[i];
                if (other == this || other.life == null)
                    continue;
                if (other == rescuer)
                    keep = Candidates.Count;

                // Serve quem está vivo, segura E e não levanta outro caído ao mesmo tempo.
                bool free = !other.life.IsDowned && other.serverHolding && !IsRescuingAnother(other);
                Candidates.Add(new ReviveCandidate(PlanarDistance(other.transform.position), free));
                CandidateOwners.Add(other);
            }

            int chosen = ReviveRescuer.Choose(Candidates, settings.reviveRadius, keep);
            rescuer = chosen >= 0 ? CandidateOwners[chosen] : null;

            progress.Update(deltaTime, targetDowned: true, rescuerHolding: rescuer != null);
            life.ServerSetReviveProgress(progress.Progress);

            if (!progress.Completed)
                return;

            life.ServerRevive(settings.reviveHealthFraction);
            progress = null;
            rescuer = null;
        }

        /// <summary>Algum caído tem este jogador como o aliado que o levanta (ele fica parado).</summary>
        private bool IsRescuingSomeone() => IsRescuingAnother(this, ignore: null);

        /// <summary>O aliado já levanta outro caído que não seja este jogador.</summary>
        private bool IsRescuingAnother(PlayerRevive ally) => IsRescuingAnother(ally, ignore: this);

        private static bool IsRescuingAnother(PlayerRevive ally, PlayerRevive ignore)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerRevive downed = Players[i];
                if (downed == ignore || downed == ally || downed.rescuer != ally)
                    continue;
                if (downed.life != null && downed.life.IsDowned)
                    return true;
            }
            return false;
        }

        private float PlanarDistance(Vector3 other)
        {
            Vector3 p = transform.position;
            return new Vector2(other.x - p.x, other.z - p.z).magnitude;
        }

        /// <summary>Os jogadores que o host enumera: os clientes conectados mais os aliados de teste.</summary>
        private static void CollectPlayers()
        {
            Players.Clear();
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer)
            {
                var clients = manager.ConnectedClientsList;
                for (int i = 0; i < clients.Count; i++)
                {
                    NetworkObject obj = clients[i]?.PlayerObject;
                    if (obj != null && obj.TryGetComponent(out PlayerRevive revive) && revive.IsSpawned)
                        Players.Add(revive);
                }
            }

            for (int i = 0; i < TestAllies.Count; i++)
            {
                PlayerRevive ally = TestAllies[i];
                if (ally != null && ally.IsSpawned && !Players.Contains(ally))
                    Players.Add(ally);
            }
        }
    }
}
