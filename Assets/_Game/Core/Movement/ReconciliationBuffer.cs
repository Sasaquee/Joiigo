using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Movement
{
    /// <summary>
    /// Previsão local do movimento (D-009). O cliente anota onde estava quando enviou cada intenção.
    /// O host responde onde ele estava ao receber essa mesma intenção. A diferença é o erro da previsão.
    /// </summary>
    public class ReconciliationBuffer
    {
        private readonly int capacity;
        private readonly LinkedList<(uint seq, Float2 pos)> pending = new LinkedList<(uint, Float2)>();

        public ReconciliationBuffer(int capacity = 128)
        {
            this.capacity = capacity;
        }

        public int PendingCount => pending.Count;

        public void Record(uint seq, Float2 predictedPosition)
        {
            pending.AddLast((seq, predictedPosition));
            while (pending.Count > capacity)
                pending.RemoveFirst();
        }

        /// <summary>
        /// Confirma a intenção seq com a posição do host. Devolve a correção a aplicar no cliente,
        /// ou zero se o erro está dentro da zona morta. As anotações ainda pendentes são deslocadas
        /// pela correção, para não corrigir o mesmo erro duas vezes.
        /// </summary>
        public Float2 Acknowledge(uint seq, Float2 authoritativePosition, float deadZone)
        {
            Float2? recorded = null;
            while (pending.First != null && pending.First.Value.seq <= seq)
            {
                if (pending.First.Value.seq == seq)
                    recorded = pending.First.Value.pos;
                pending.RemoveFirst();
            }

            if (!recorded.HasValue)
                return Float2.Zero;

            Float2 error = authoritativePosition - recorded.Value;
            if (error.Length <= deadZone)
                return Float2.Zero;

            for (var node = pending.First; node != null; node = node.Next)
                node.Value = (node.Value.seq, node.Value.pos + error);
            return error;
        }
    }
}
