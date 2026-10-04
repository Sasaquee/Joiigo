using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// <summary>Intenção de movimento e mira enviada pelo cliente. O host decide o resultado.</summary>
    public struct PlayerIntent : INetworkSerializable
    {
        public uint Seq;
        public Vector2 Move;
        public Vector3 Aim;
        public bool HasAim;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seq);
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Aim);
            serializer.SerializeValue(ref HasAim);
        }
    }

    /// <summary>
    /// Estado do jogador escrito pelo host. Position/Yaw servem a todos.
    /// AckSeq/AckPosition dizem ao dono onde o host estava ao receber a intenção AckSeq (D-009).
    /// </summary>
    public struct PlayerNetState : INetworkSerializable, System.IEquatable<PlayerNetState>
    {
        /// <summary>Tick do host. Muda sempre, então o estado é reenviado mesmo com o jogador parado.</summary>
        public int Tick;
        public Vector3 Position;
        public float Yaw;
        public uint AckSeq;
        public Vector3 AckPosition;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref AckSeq);
            serializer.SerializeValue(ref AckPosition);
        }

        public bool Equals(PlayerNetState other) =>
            Tick == other.Tick && Position == other.Position && Yaw == other.Yaw
            && AckSeq == other.AckSeq && AckPosition == other.AckPosition;

        public override bool Equals(object obj) => obj is PlayerNetState other && Equals(other);

        public override int GetHashCode() => System.HashCode.Combine(Tick, Position, Yaw, AckSeq, AckPosition);
    }
}
