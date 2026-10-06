using System;
using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.AI
{
    /// <summary>
    /// Escolha da boca de rua por onde sai o próximo inimigo (D-077, passe do mapa). Rodízio a partir de uma boca inicial,
    /// pulando as que têm jogador vivo a menos de <c>clearance</c>; se todas estiverem ocupadas, a mais longe dos jogadores.
    /// Puro: quem chama (WaveSpawner) fornece as posições no plano (X = X, Y = Z).
    /// </summary>
    public static class SpawnMouthPicker
    {
        /// <summary>Distância da boca ao jogador mais perto (float.MaxValue se não há jogador).</summary>
        public static float NearestPlayerDistance(Float2 mouth, IReadOnlyList<Float2> players)
        {
            float best = float.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                float d = (players[i] - mouth).Length;
                if (d < best)
                    best = d;
            }
            return best;
        }

        /// <summary>
        /// Índice da boca a usar, ou -1 se não há bocas. <paramref name="start"/> é a boca da vez no rodízio (qualquer inteiro;
        /// dá a volta). Boca ocupada = jogador vivo a menos de <paramref name="clearance"/>.
        /// </summary>
        public static int Pick(IReadOnlyList<Float2> mouths, IReadOnlyList<Float2> players, int start, float clearance)
        {
            int count = mouths.Count;
            if (count == 0)
                return -1;

            int first = ((start % count) + count) % count;
            for (int k = 0; k < count; k++)
            {
                int index = (first + k) % count;
                if (NearestPlayerDistance(mouths[index], players) >= clearance)
                    return index;
            }

            // Todas ocupadas: a mais longe do jogador mais perto (empate: a mais próxima da vez no rodízio).
            int best = first;
            float bestDistance = -1f;
            for (int k = 0; k < count; k++)
            {
                int index = (first + k) % count;
                float d = NearestPlayerDistance(mouths[index], players);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = index;
                }
            }
            return best;
        }
    }

    /// <summary>Um nascimento que já tem hora marcada: tipo do inimigo e boca.</summary>
    public readonly struct QueuedSpawn
    {
        public readonly int Type;
        public readonly int Mouth;
        public readonly float Due;

        public QueuedSpawn(int type, int mouth, float due)
        {
            Type = type;
            Mouth = mouth;
            Due = due;
        }
    }

    /// <summary>
    /// Fila de nascimentos com espaçamento por boca (D-077): dois inimigos da mesma boca nascem com pelo menos
    /// <c>stagger</c> segundos de diferença, para os CharacterController não nascerem um dentro do outro. Bocas diferentes
    /// não se esperam. Relógio próprio, avançado por <see cref="Advance"/>. Puro.
    /// </summary>
    public class MouthSpawnQueue
    {
        private readonly List<QueuedSpawn> pending = new List<QueuedSpawn>();
        private readonly float[] mouthFree;
        private readonly float stagger;
        private float clock;

        public MouthSpawnQueue(int mouthCount, float stagger)
        {
            mouthFree = new float[System.Math.Max(1, mouthCount)];
            this.stagger = MathF.Max(0f, stagger);
        }

        /// <summary>Quantos ainda não nasceram.</summary>
        public int Pending => pending.Count;

        public int MouthCount => mouthFree.Length;

        /// <summary>Marca um nascimento: já, se a boca está livre, ou depois do último dela mais o espaçamento.</summary>
        public void Enqueue(int type, int mouth)
        {
            if (mouth < 0 || mouth >= mouthFree.Length)
                throw new ArgumentOutOfRangeException(nameof(mouth));
            float due = MathF.Max(clock, mouthFree[mouth]);
            mouthFree[mouth] = due + stagger;
            pending.Add(new QueuedSpawn(type, mouth, due));
        }

        /// <summary>Avança o relógio e devolve em <paramref name="ready"/> (na ordem de chegada) os que já podem nascer.</summary>
        public void Advance(float deltaTime, List<QueuedSpawn> ready)
        {
            ready.Clear();
            clock += deltaTime;
            for (int i = 0; i < pending.Count;)
            {
                if (pending[i].Due <= clock)
                {
                    ready.Add(pending[i]);
                    pending.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
