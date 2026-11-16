using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One AI Jewel Wasp seat in one of the wasp's arcade games (Marionette, Gorge), and the plumbing the mode AIs share.
    /// Server-only.
    ///
    /// <para><b>Steering only, and only while the wasp is free.</b> The wasp flies itself (<c>JewelWaspExecutor</c>'s AI,
    /// JEWEL_WASP.md §6): free, it stings the creature ahead of its nose (any creature in the cell if none is ahead);
    /// in a host it flies the host at the nearest rival, keeps the right trigger held (suction or the attack) and leaves
    /// after a while. A second presser would fight it, so a mode AI only answers
    /// <see cref="AIPilot.SetExternalTargetProvider"/> with where the free hull's nose should be - which decides the
    /// creature "ahead" it stings next. The mode's generator fails if it presses anything.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class WaspSeat
    {
        public IPlayer Player;
        public int Level;
        public IVesselStatus Status;
        public AIPilot Autopilot;
        public JewelWaspExecutor Wasp;
        public System.Random Rng;

        /// <summary>Where the free hull's nose is steered this frame.</summary>
        public Vector3 Steer;

        // Mode state, read and written by the mode's own AI.
        public IPlayer Rival;
        public Fauna Host;
        public float NextRetargetAt;
        public Vector3 Aim;
        public Vector3 Goal;

        public Transform Self => Player?.Vessel?.Transform;
        public bool Free => Wasp && Wasp.Mode == JewelWaspExecutor.WaspMode.Free;
    }

    public static class WaspSeats
    {
        /// <summary>Every AI Jewel Wasp on the roster, its autopilot steered at <see cref="WaspSeat.Steer"/>.
        /// <paramref name="salt"/> keeps two modes' random streams apart.</summary>
        public static void Arm(GameDataSO gameData, int salt, Vector3 home, List<WaspSeat> into)
        {
            Disarm(into);
            if (gameData == null) return;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                if (!(status is Component c) || !c) continue;
                if (!c.TryGetComponent(out AIPilot autopilot)) continue;
                var wasp = c.GetComponentInChildren<JewelWaspExecutor>(true);
                if (!wasp) continue;   // the card seats only Jewel Wasps

                var seat = new WaspSeat
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Wasp = wasp,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + salt)),
                    Steer = home,
                    Goal = home,
                };
                into.Add(seat);
                var captured = seat;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
        public static void Disarm(List<WaspSeat> seats)
        {
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].Autopilot) seats[i].Autopilot.ClearExternalTargetProvider();
            seats.Clear();
        }

        /// <summary>Is the seat live and flying itself this frame?</summary>
        public static bool Flying(WaspSeat s) =>
            s != null && s.Self && s.Autopilot && s.Autopilot.AutoPilotEnabled && s.Wasp;

        /// <summary>A pilot of another domain with a hull.</summary>
        public static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;
            return candidate.Vessel?.Transform != null;
        }

        /// <summary>The cheapest rival: distance, divided by <paramref name="humanFocus"/> for a human. Null when none.</summary>
        public static IPlayer PickRival(GameDataSO gameData, WaspSeat s, float humanFocus)
        {
            var self = s.Self;
            if (gameData == null || !self) return null;
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var c = players[i];
                if (!IsLiveRival(c, s.Player)) continue;
                float cost = Vector3.Distance(self.position, c.Vessel.Transform.position);
                if (!c.IsInitializedAsAI) cost /= Mathf.Max(1f, humanFocus);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = c;
            }
            return best;
        }

        /// <summary>
        /// The creature to take next: of the cell's possessable creatures, the one nearest <paramref name="goal"/> - or,
        /// with probability 1 - <paramref name="choosiness"/>, any one, from the seat's own stream (an easy AI takes what
        /// is to hand). Null when the cell has none.
        /// </summary>
        public static Fauna PickHost(WaspSeat s, Cell cell, Vector3 goal, float choosiness)
        {
            if (!cell) return null;
            var live = cell.LiveFauna;
            if (live == null || live.Count == 0) return null;
            bool choosy = s.Rng.NextDouble() < choosiness;
            Fauna best = null;
            float bestSqr = float.PositiveInfinity;
            int seen = 0;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i];
                if (!f) continue;
                var host = f.PossessionHost;
                if (!host || !host.Possessable) continue;
                if (choosy)
                {
                    float d = (host.PossessionNestPoint - goal).sqrMagnitude;
                    if (d >= bestSqr) continue;
                    bestSqr = d;
                    best = host;
                }
                else if (s.Rng.Next(++seen) == 0)
                {
                    best = host;   // a reservoir sample: every candidate equally likely
                }
            }
            return best;
        }

        /// <summary>Is <paramref name="host"/> still a creature the wasp can take?</summary>
        public static bool Takeable(Fauna host) => host && host.Possessable;

        /// <summary>A point uniformly inside a ball of <paramref name="radius"/>, from the seat's own stream.</summary>
        public static Vector3 InBall(System.Random rng, float radius)
        {
            for (int tries = 0; tries < 16; tries++)
            {
                var v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f);
                if (v.sqrMagnitude <= 1f) return v * radius;
            }
            return Vector3.zero;
        }

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
