using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Jacks' race coach in Regatta (<see cref="IJacksRaceCoach"/>, the seam Decoy Run built). Without a coach an AI
    /// Jacks keeps its herding rhythm and crawls at the rig's 50 u/s (4 of 24 gates in 600 s, 2026-11-19). With it, every
    /// ring is one Decoy Run door cycle: the autopilot rolls its board onto the ring (the board holds the ring's axis and
    /// the line to it), the jacks run out, lay the decoy past the ring and meet behind it on its line, the hull takes off
    /// there and travels through. With Time 5's second decoy one cycle takes the ring after it too.
    /// <para>Planning is <see cref="DecoyRunCourse.PlanCycle"/> on Regatta's rings, so the cycle is the one the Jacks fly
    /// in their own Time game. Inputs only: the executor runs the plan through the calls a person's controls make. Every
    /// other hull is untouched (only a hull with a <see cref="JacksExecutor"/> is coached).</para>
    /// </summary>
    [Serializable]
    public sealed class RegattaJacksCoach : IJacksRaceCoach
    {
        [Tooltip("Longest the autopilot waits, flying and rolling, for its next ring to lie on its board before it gives " +
                 "up waiting and plans with the board it has (a plan that threads nothing is never herded).")]
        [Min(0f)] public float BoardWaitSeconds = 2.5f;

        [Tooltip("The board must hold the ring's axis this closely (the sine of the angle between axis and board) before " +
                 "the coach herds, so the take-off line threads the mouth.")]
        [Range(0f, 1f)] public float AxisOnBoard = 0.2f;

        [Tooltip("Rings closer than this (u) to the hull are flown through, not herded: a cycle would land behind them.")]
        [Min(0f)] public float MinRingDistance = 120f;

        static readonly DecoyRunSettings Doors = DecoyRunCourse.ForIntensity(1);

        readonly Dictionary<JacksExecutor, IPlayer> _coached = new();
        readonly Dictionary<IPlayer, JacksExecutor> _jacksOf = new();
        readonly Dictionary<JacksExecutor, float> _waitingSince = new();
        readonly List<RaceGate> _rings = new(2);
        Func<IPlayer, int, RaceGate?> _ringAhead;

        /// <summary>Coach <paramref name="pilot"/> if it flies the Jacks. <paramref name="ringAhead"/>(pilot, k) is the
        /// k-th ring the pilot has still to thread (0 the next), or null past the end.</summary>
        public void Arm(IPlayer pilot, Func<IPlayer, int, RaceGate?> ringAhead)
        {
            _ringAhead = ringAhead;
            var tf = pilot?.Vessel?.Transform;
            var jacks = tf ? tf.GetComponentInChildren<JacksExecutor>(true) : null;
            if (!jacks) return;
            _coached[jacks] = pilot;
            _jacksOf[pilot] = jacks;
            jacks.RaceCoach = this;
        }

        /// <summary>Stand every coached Jacks down (the race ended or the controller is going).</summary>
        public void ReleaseAll()
        {
            foreach (var jacks in _coached.Keys)
                if (jacks && ReferenceEquals(jacks.RaceCoach, this)) jacks.RaceCoach = null;
            _coached.Clear();
            _jacksOf.Clear();
            _waitingSince.Clear();
        }

        JacksExecutor JacksOf(IPlayer pilot) =>
            pilot != null && _jacksOf.TryGetValue(pilot, out var jacks) && jacks ? jacks : null;

        public bool IsCoached(IPlayer pilot) => JacksOf(pilot);

        public bool TryNextCycle(JacksExecutor jacks, out JacksRacePlan plan)
        {
            plan = default;
            if (!jacks || _ringAhead == null || !_coached.TryGetValue(jacks, out var pilot)) return false;
            if (_ringAhead(pilot, 0) is not RaceGate next) return false;
            Vector3 hull = jacks.RaceHullPosition, up = jacks.RaceBoardUp;
            if ((next.Position - hull).magnitude < MinRingDistance) { _waitingSince.Remove(jacks); return false; }

            _rings.Clear();
            _rings.Add(next);
            int zips = jacks.HasTwoDecoys ? 2 : 1;
            if (zips >= 2 && _ringAhead(pilot, 1) is RaceGate after) _rings.Add(after);

            bool onBoard = OnBoard(next, hull, up);
            if (!onBoard)
            {
                float now = Time.time;
                if (!_waitingSince.TryGetValue(jacks, out float since)) _waitingSince[jacks] = since = now;
                if (now - since < BoardWaitSeconds) return false;
            }
            int maxRings = _rings.Count > 1 && OnBoard(_rings[1], hull, up) ? 2 : 1;
            if (DecoyRunCourse.PlanCycle(hull, up, _rings, 0, _rings.Count, zips, maxRings, Doors, out plan, out _) == 0)
                return false;
            _waitingSince.Remove(jacks);
            return true;
        }

        bool OnBoard(in RaceGate ring, Vector3 hull, Vector3 up) =>
            Mathf.Abs(Vector3.Dot(ring.Position - hull, up)) <= ring.Radius * DecoyRunCourse.OnBoardShare &&
            Mathf.Abs(Vector3.Dot(ring.Axis, up)) <= AxisOnBoard;

        /// <summary>Flying, a coached Jacks steers past its next ring on its line; mid-cycle it holds its heading (the hull
        /// is held on the board or travelling, and steering would swing the board off the ring).</summary>
        public bool TryAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var jacks = JacksOf(pilot);
            var tf = pilot?.Vessel?.Transform;
            if (!jacks || !tf || _ringAhead?.Invoke(pilot, 0) is not RaceGate next) return false;
            if (jacks.RaceCycleBusy) { target = tf.position + tf.forward * 200f; return true; }
            target = next.Position + next.Axis * DecoyRunCourse.Overshoot;
            return true;
        }

        /// <summary>Flying, the autopilot rolls its board onto the plane that holds the next ring's axis and the line to
        /// it (belly square to that plane, whichever face it already shows): the roll a person makes before swapping.</summary>
        public bool TryBelly(IPlayer pilot, out Vector3 worldDirection)
        {
            worldDirection = default;
            var jacks = JacksOf(pilot);
            var tf = pilot?.Vessel?.Transform;
            if (!jacks || !tf || jacks.RaceCycleBusy || _ringAhead?.Invoke(pilot, 0) is not RaceGate next) return false;
            Vector3 n = Vector3.Cross(next.Axis, next.Position - tf.position);
            if (n.sqrMagnitude < 1e-4f) n = Vector3.Cross(next.Axis, tf.right);
            if (n.sqrMagnitude < 1e-4f) return false;
            n.Normalize();
            worldDirection = Vector3.Dot(tf.up, n) >= 0f ? -n : n;
            return true;
        }
    }
}
