using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One AI Shrike seat in one of the Shrike's arcade games (Larder, Thornbush, Lancefall), and the plumbing the three
    /// mode AIs share. Server-only.
    ///
    /// <para><b>Steering only.</b> The Shrike already flies itself (<c>ShrikeExecutor.Autopilot</c>, SHRIKE.md §3): it
    /// picks Charge mode and paints while a rival hull is in its cone and a plant has a missile, Space mode and lances
    /// while rival mass lies on its line, and otherwise slings at the AI's target and presses Mass to leave a nest after
    /// a few seconds docked. A second presser would fight it, so a mode AI only answers
    /// <see cref="AIPilot.SetExternalTargetProvider"/> with where the nose should be (Flatline's and Corona's rule),
    /// and the mode's generator fails if it presses anything.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>): intensity is the
    /// map, difficulty the opponent.</para>
    /// </summary>
    public sealed class ShrikeSeat
    {
        public IPlayer Player;
        public int Level;
        public IVesselStatus Status;
        public AIPilot Autopilot;
        public ShrikeExecutor Shrike;
        public System.Random Rng;

        /// <summary>Where the nose is steered this frame.</summary>
        public Vector3 Steer;

        // Mode state, read and written by the mode's own AI.
        public IPlayer Rival;
        public float NextRetargetAt;
        public Vector3 Aim;
        public Vector3 Station;
        public Vector3 Goal;
        public int Phase;

        public Transform Self => Player?.Vessel?.Transform;
    }

    public static class ShrikeSeats
    {
        /// <summary>
        /// Every AI Shrike on the roster, with its autopilot steered at <see cref="ShrikeSeat.Steer"/>. <paramref name="salt"/>
        /// keeps two modes' random streams apart.
        /// </summary>
        public static void Arm(GameDataSO gameData, int salt, Vector3 home, List<ShrikeSeat> into)
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
                var shrike = c.GetComponentInChildren<ShrikeExecutor>(true);
                if (!shrike) continue;   // the card seats only Shrikes

                var seat = new ShrikeSeat
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Shrike = shrike,
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
        public static void Disarm(List<ShrikeSeat> seats)
        {
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].Autopilot) seats[i].Autopilot.ClearExternalTargetProvider();
            seats.Clear();
        }

        /// <summary>Is the seat live and flying itself this frame?</summary>
        public static bool Flying(ShrikeSeat s) =>
            s != null && s.Self && s.Autopilot && s.Autopilot.AutoPilotEnabled && s.Shrike;

        /// <summary>A pilot of another domain with a hull: someone a paint, a missile or a lance can score on.</summary>
        public static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;
            return candidate.Vessel?.Transform != null;
        }

        /// <summary>
        /// The cheapest rival to go after: distance, divided by <paramref name="humanFocus"/> for a human. Null when there
        /// is none.
        /// </summary>
        public static IPlayer PickRival(GameDataSO gameData, ShrikeSeat s, float humanFocus)
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
        /// The nearest live crystal of ANY kind, or null. To a Shrike every crystal is worth the same: it becomes a missile
        /// plant where it was taken and resets the sling (SHRIKE.md §3.5). Crystals embedded in a creature are skipped.
        /// </summary>
        public static Vector3? NearestCrystal(Vector3 from, float maxDistance)
        {
            Vector3? best = null;
            float bestSqr = maxDistance * maxDistance;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!c || c.IsEmbedded) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = c.transform.position;
            }
            return best;
        }

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
