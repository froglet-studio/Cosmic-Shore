using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Ark's FALL (Docs/Pilots/ARTIFACTS.md §5, §8.4): when its last hull prism is destroyed,
    /// everything in its hold goes, in one step, to the squad whose force destroyed that prism. If
    /// nobody earned it (fauna took the last prism, or the credited force has no Ark standing here
    /// to receive), the artifacts spill as <see cref="ArtifactPickup"/>s that anyone takes with RB.
    ///
    /// <para><b>Decided once, on the authority.</b> <see cref="ArkHold"/> calls <see cref="Settle"/>
    /// exactly once per fall on the machine that holds the Ark (today the local toy's; a networked
    /// Ark makes this the server). No PvP is involved: attacking an Ark is attacking mass, and petals
    /// move only through <c>CombatHitDrain</c>, untouched here.</para>
    /// </summary>
    public static class ArkFall
    {
        /// <summary>Where a fallen hold's artifacts go.</summary>
        public enum Verdict
        {
            /// <summary>The hold was empty: nothing moves.</summary>
            Nothing = 0,
            /// <summary>Everything aboard goes to the credited squad's Ark.</summary>
            Transfer = 1,
            /// <summary>Nobody earned it: the artifacts spill for anyone.</summary>
            Spill = 2,
        }

        /// <summary>What a fall settled, for a HUD and for tests.</summary>
        public readonly struct Outcome
        {
            public readonly Verdict Verdict;
            public readonly Domains RaiderDomain;
            public readonly int Count;

            public Outcome(Verdict verdict, Domains raiderDomain, int count)
            {
                Verdict = verdict;
                RaiderDomain = raiderDomain;
                Count = count;
            }
        }

        /// <summary>Raised on the authority once a fall is settled.</summary>
        public static event System.Action<ArkHold, Outcome> Settled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Settled = null;

        static readonly List<string> s_moving = new();

        /// <summary>
        /// The credit rule, pure: which squad earned a fall, or null for nobody. A creature kill,
        /// an unnamed force, a force whose side cannot be found and the Ark's own squad all earn
        /// nothing (an Ark's own crew finishing it is not a theft; its cargo has nowhere to go).
        /// </summary>
        public static Domains? Credit(string destroyerName, bool byCreature, Domains fallenDomain,
            System.Func<string, Domains?> domainOf)
        {
            if (byCreature || string.IsNullOrEmpty(destroyerName))
                return null;
            var domain = domainOf?.Invoke(destroyerName);
            if (domain == null || domain.Value == fallenDomain) return null;
            return domain;
        }

        /// <summary>The live vessel flown by <paramref name="playerName"/>'s side, or null.</summary>
        public static Domains? DomainOfPilot(string playerName)
        {
            var live = VesselStatus.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var hull = live[i];
                if (!hull) continue;
                IVesselStatus vessel = hull;
                if (vessel.PlayerName == playerName) return vessel.Domain;
            }
            return null;
        }

        /// <summary>Settle <paramref name="hold"/>'s fallen Ark. Called once by the hold.</summary>
        internal static Outcome Settle(ArkHold hold)
        {
            var ark = hold ? hold.Ark : null;
            if (!ark || hold.Artifacts.Count == 0) return Raise(hold, new Outcome(Verdict.Nothing, default, 0));

            var last = ark.LastFallenHullPrism();
            var credited = last
                ? Credit(last.DestroyerName, last.DestroyedByCreature, hold.Domain, DomainOfPilot)
                : null;
            var raiders = credited.HasValue ? ArkHold.FindHome(credited.Value, except: hold) : null;

            s_moving.Clear();
            hold.TakeAll(s_moving);

            if (raiders)
            {
                for (int i = 0; i < s_moving.Count; i++) raiders.Stow(s_moving[i]);
                var outcome = new Outcome(Verdict.Transfer, credited.Value, s_moving.Count);
                s_moving.Clear();
                return Raise(hold, outcome);
            }

            var cfg = ArkHold.Config;
            for (int i = 0; i < s_moving.Count; i++)
                ArtifactPickup.Spill(s_moving[i], ark.Position + SpillOffset(i, s_moving.Count, cfg.SpillRadius));
            var spilled = new Outcome(Verdict.Spill, default, s_moving.Count);
            s_moving.Clear();
            return Raise(hold, spilled);
        }

        static Outcome Raise(ArkHold hold, Outcome outcome)
        {
            Settled?.Invoke(hold, outcome);
            return outcome;
        }

        /// <summary>
        /// Where spilled artifact <paramref name="index"/> of <paramref name="count"/> lands around the
        /// fallen Ark: evenly over a sphere of <paramref name="radius"/> (the golden-angle spiral), the
        /// same places every time, so no two land on each other.
        /// </summary>
        public static Vector3 SpillOffset(int index, int count, float radius)
        {
            if (count <= 1) return Vector3.up * radius;
            float y = 1f - 2f * (index + 0.5f) / count;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = index * 2.39996323f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r) * radius;
        }
    }
}
