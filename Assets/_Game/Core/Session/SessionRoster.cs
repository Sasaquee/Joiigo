using System.Collections.Generic;

namespace Game.Core.Session
{
    public enum AdmitResult
    {
        Admitted,
        Full,
        AlreadyStarted,
        AlreadyConnected
    }

    /// <summary>
    /// Quem pode entrar na sessão (host). Antes da largada, entra qualquer um até o limite.
    /// Depois da largada, só volta quem já estava (D-011, D-014).
    /// Cada membro é identificado por um token que o cliente envia ao conectar.
    /// </summary>
    public class SessionRoster
    {
        private readonly int maxPlayers;
        private readonly List<Member> members = new List<Member>();

        public SessionRoster(int maxPlayers)
        {
            this.maxPlayers = maxPlayers;
        }

        public bool Started { get; private set; }

        public int ConnectedCount
        {
            get
            {
                int count = 0;
                foreach (var m in members)
                    if (m.Connected)
                        count++;
                return count;
            }
        }

        /// <summary>Tenta admitir um token. Em caso de sucesso, devolve a vaga de spawn (0 a maxPlayers-1).</summary>
        public AdmitResult TryAdmit(string token, out int slot)
        {
            slot = -1;
            Member existing = Find(token);

            if (existing != null && existing.Connected)
                return AdmitResult.AlreadyConnected;
            if (ConnectedCount >= maxPlayers)
                return AdmitResult.Full;

            if (Started)
            {
                if (existing == null)
                    return AdmitResult.AlreadyStarted;
                existing.Connected = true;
                slot = existing.Slot;
                return AdmitResult.Admitted;
            }

            slot = LowestFreeSlot();
            members.Add(new Member(token, slot));
            return AdmitResult.Admitted;
        }

        /// <summary>Antes da largada, quem sai libera a vaga. Depois, a vaga fica guardada para a volta.</summary>
        public void Disconnect(string token)
        {
            Member m = Find(token);
            if (m == null)
                return;
            if (Started)
                m.Connected = false;
            else
                members.Remove(m);
        }

        public void Start() => Started = true;

        public bool IsMember(string token) => Find(token) != null;

        private Member Find(string token)
        {
            foreach (var m in members)
                if (m.Token == token)
                    return m;
            return null;
        }

        private int LowestFreeSlot()
        {
            for (int s = 0; s < maxPlayers; s++)
            {
                bool used = false;
                foreach (var m in members)
                    used |= m.Slot == s;
                if (!used)
                    return s;
            }
            return -1;
        }

        private class Member
        {
            public readonly string Token;
            public readonly int Slot;
            public bool Connected = true;

            public Member(string token, int slot)
            {
                Token = token;
                Slot = slot;
            }
        }
    }
}
