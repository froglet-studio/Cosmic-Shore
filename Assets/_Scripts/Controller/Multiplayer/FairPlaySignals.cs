using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What kind of fair-play signal a check raised (Docs/AntiCheat/README.md §4.4).</summary>
    public enum FairPlaySignalKind
    {
        None = 0,
        SustainedSpeed = 1,
        JumpRate = 2,
        AiLikeSteering = 3,
    }

    /// <summary>
    /// One piece of fair-play evidence. A signal is never a verdict: it only adds weight to an
    /// account's trust record once that record exists (README §4.5), and a person reviews before
    /// anything restricts play. Every signal carries the numbers that raised it so a reviewer, and an
    /// appeal, can see exactly what was measured.
    /// </summary>
    public readonly struct FairPlaySignal
    {
        public readonly FairPlaySignalKind Kind;
        public readonly string PlayerName;
        public readonly ulong OwnerClientId;
        public readonly string Hull;
        public readonly float Measured;
        public readonly float Limit;
        public readonly float MatchTime;

        public FairPlaySignal(FairPlaySignalKind kind, string playerName, ulong ownerClientId, string hull,
            float measured, float limit, float matchTime)
        {
            Kind = kind;
            PlayerName = playerName;
            OwnerClientId = ownerClientId;
            Hull = hull;
            Measured = measured;
            Limit = limit;
            MatchTime = matchTime;
        }

        public override string ToString() =>
            $"{Kind} {PlayerName} (client {OwnerClientId}, {Hull}): {Measured:0.#} vs limit {Limit:0.#} at {MatchTime:0.0}s";
    }

    /// <summary>
    /// The host's in-memory record of this session's fair-play signals. Nothing leaves the machine
    /// yet: the server side (every player reporting the match, Cloud Code, the trust record) is
    /// Threat Board T4 and phase B. Until then a reviewer reads these in a development build.
    /// </summary>
    public static class FairPlaySignals
    {
        const int Capacity = 256;
        static readonly List<FairPlaySignal> s_signals = new(32);

        /// <summary>Raised on the host for every new signal.</summary>
        public static event Action<FairPlaySignal> OnSignal;

        public static IReadOnlyList<FairPlaySignal> All => s_signals;

        public static void Raise(in FairPlaySignal signal)
        {
            if (s_signals.Count >= Capacity) s_signals.RemoveAt(0);
            s_signals.Add(signal);
            OnSignal?.Invoke(signal);
        }

        public static void Clear() => s_signals.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_signals.Clear();
            OnSignal = null;
        }
    }
}
