namespace Game.Core.Combat
{
    /// <summary>Tempo de recarga simples, avançado pelo host.</summary>
    public class Cooldown
    {
        private float remaining;

        public bool Ready => remaining <= 0f;
        public float Remaining => remaining;

        public void Tick(float deltaTime)
        {
            if (remaining > 0f)
                remaining -= deltaTime;
        }

        /// <summary>Tenta usar. Se estiver pronto, dispara a recarga e devolve true.</summary>
        public bool TryUse(float duration)
        {
            if (!Ready)
                return false;
            remaining = duration;
            return true;
        }

        public void Reset() => remaining = 0f;
    }
}
