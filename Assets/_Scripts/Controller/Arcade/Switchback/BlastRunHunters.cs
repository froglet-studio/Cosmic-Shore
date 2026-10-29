using System;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One hunter pack's replicated state (Blast Run I4, <see cref="SwitchbackBlastRun"/>): where the pack is, and which
    /// of its members are down. Written by the server only (<see cref="SwitchbackController"/>), read on every peer by
    /// <see cref="SwitchbackFunTypeRuntime"/>, which moves the pack's danger prisms there.
    /// </summary>
    public struct BlastRunPackState : INetworkSerializable, IEquatable<BlastRunPackState>
    {
        public Vector3 Position;
        /// <summary>Bit m set: member m is down (at most <see cref="BlastRunHunters.AliveToStand"/> of its prisms left).</summary>
        public int DownMask;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref DownMask);
        }

        public bool Equals(BlastRunPackState other) => Position == other.Position && DownMask == other.DownMask;
        public override bool Equals(object obj) => obj is BlastRunPackState o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Position, DownMask);
    }

    /// <summary>
    /// The hunter packs' brain - pure, so EditMode can drive it. A pack sits at its den until the race's LEAD runner
    /// (most gates threaded) comes within <see cref="SwitchbackBlastRun.PackSense"/> of the den, then flies at it at
    /// <see cref="SwitchbackBlastRun.PackSpeed"/> (under the Dolphin's cruise: it intercepts, it cannot chase down),
    /// never further than <see cref="SwitchbackBlastRun.PackLeash"/> from the den, and goes home when the leader is gone.
    /// A member is DOWN once blasts leave <see cref="AliveToStand"/> or fewer of its prisms; a down member's remains stay
    /// where they died, and a pack with every member down stops.
    /// </summary>
    public static class BlastRunHunters
    {
        /// <summary>Server writes per second: the clients lerp between them.</summary>
        public const float TickHz = 10f;
        /// <summary>A member with this many prisms (or fewer) still standing is down.</summary>
        public const int AliveToStand = 2;
        /// <summary>1/s - how fast a peer's drawn pack closes on the replicated position.</summary>
        public const float FollowRate = 10f;

        public static bool IsDown(int alivePrisms) => alivePrisms <= AliveToStand;

        public static bool AllDown(int downMask, int members) => members > 0 && downMask == (1 << members) - 1;

        /// <summary>Where the pack goes next: toward the leader if it is within sense of the den (clamped to the leash),
        /// else home; at most <c>PackSpeed * dt</c> per step.</summary>
        public static Vector3 Step(Vector3 pos, Vector3 den, bool hasLeader, Vector3 leader, float dt)
        {
            Vector3 goal = den;
            if (hasLeader && (leader - den).sqrMagnitude <= SwitchbackBlastRun.PackSense * SwitchbackBlastRun.PackSense)
                goal = den + Vector3.ClampMagnitude(leader - den, SwitchbackBlastRun.PackLeash);
            return Vector3.MoveTowards(pos, goal, SwitchbackBlastRun.PackSpeed * Mathf.Max(0f, dt));
        }

        /// <summary>The drawn position closing on the replicated one, frame-rate independent.</summary>
        public static Vector3 Follow(Vector3 shown, Vector3 target, float dt) =>
            Vector3.Lerp(shown, target, 1f - Mathf.Exp(-FollowRate * Mathf.Max(0f, dt)));
    }
}
