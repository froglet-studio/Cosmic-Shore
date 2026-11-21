using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Orca's race coach in Regatta (<see cref="IOrcaRaceCoach"/>, the seam Breach built). Without one an AI Orca helm
    /// keeps its fight-mode burn rule (8 deg, 250 u), which rarely fires on a course, so it cruised at ~30 u/s (2026-11-19,
    /// 5.3x the field). With it the helm charges every leg its nose is on, and coasts the last stretch before a sharp ring
    /// so the charge's wide turn is spent on the straight. Inputs only: the coach says when to hold the charge, the tank
    /// says whether it burns. Every other hull is untouched (only a hull with an <see cref="OrcaCrew"/> is coached).
    /// </summary>
    [Serializable]
    public sealed class RegattaOrcaCoach : IOrcaRaceCoach
    {
        [Tooltip("Degrees the nose may be off the line to the next ring and still charge.")]
        [Range(1f, 90f)] public float AlignDegrees = 45f;

        [Tooltip("Inside this distance (u) of the next ring the helm coasts when the turn there is sharp.")]
        [Min(0f)] public float CoastDistance = 200f;

        [Tooltip("A ring whose turn (from the leg in to the leg out) is wider than this (deg) is coasted into.")]
        [Range(0f, 180f)] public float SharpTurnDegrees = 40f;

        [Tooltip("Tank seconds the helm waits for before it starts a charge (it keeps charging below this once started).")]
        [Min(0f)] public float MinTank = 0f;

        readonly Dictionary<OrcaCrew, IPlayer> _coached = new();
        Func<IPlayer, int, RaceGate?> _ringAhead;

        /// <summary>Coach <paramref name="pilot"/> if it flies the Orca. <paramref name="ringAhead"/>(pilot, k) is the k-th
        /// ring the pilot has still to thread (0 the next), or null past the end.</summary>
        public void Arm(IPlayer pilot, Func<IPlayer, int, RaceGate?> ringAhead)
        {
            _ringAhead = ringAhead;
            var tf = pilot?.Vessel?.Transform;
            var crew = tf ? tf.GetComponentInChildren<OrcaCrew>(true) : null;
            if (!crew) return;
            _coached[crew] = pilot;
            crew.RaceCoach = this;
        }

        /// <summary>Stand every coached Orca down (the race ended or the controller is going).</summary>
        public void ReleaseAll()
        {
            foreach (var crew in _coached.Keys)
                if (crew && ReferenceEquals(crew.RaceCoach, this)) crew.RaceCoach = null;
            _coached.Clear();
        }

        public bool HelmBurns(OrcaCrew crew)
        {
            if (!crew || _ringAhead == null || !_coached.TryGetValue(crew, out var pilot)) return false;
            if (_ringAhead(pilot, 0) is not RaceGate next) return false;
            if (!crew.Boosting && crew.BoostTankSeconds < MinTank) return false;
            var hull = crew.transform;
            var pilotStatus = pilot.Vessel?.VesselStatus;
            var autopilot = pilotStatus != null && pilotStatus.Transform && pilotStatus.Transform.TryGetComponent(out AIPilot ai) ? ai : null;
            // the autopilot's aim leads the ring, so the nose is judged against it; the ring only for coasting
            Vector3 aim = autopilot ? autopilot.TargetPosition : next.Position;
            if (Vector3.Angle(hull.forward, aim - hull.position) > AlignDegrees) return false;
            Vector3 to = next.Position - hull.position;
            if (to.magnitude < CoastDistance && _ringAhead(pilot, 1) is RaceGate after
                && Vector3.Angle(to, after.Position - next.Position) > SharpTurnDegrees)
                return false;
            return true;
        }
    }
}
