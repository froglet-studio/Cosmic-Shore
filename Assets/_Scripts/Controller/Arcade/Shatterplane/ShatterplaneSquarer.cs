using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Shatterplane: a SQUARER that steers. Server-only.
    ///
    /// <para><b>It steers; it never presses.</b> The Brittle Star already flies its own weapons with the replicated
    /// controls a human presses (<c>BrittleStarExecutor.Autopilot</c>): it holds the trigger while rival mass lies on
    /// the line ahead (neutral plates count: their domain is not its own), picks Space Mode's guns against a wall
    /// further out and Charge Mode's cords against a near one, and throws its ring when a cluster fills the sphere
    /// ahead of it. A second presser here would fight it, and the mode never changes the vessel, so this only answers
    /// WHERE to fly, through <see cref="AIPilot.SetExternalTargetProvider"/>:</para>
    ///   • with no spare ring and a crystal within <see cref="ShatterplaneSettingsSO.aiCrystalDetourRange"/>, to that
    ///     crystal - and every crystal floats on a plate's axis, so taking one leaves the hull lined up;
    ///   • otherwise at a standing plate, SQUARE: first to a point on its axis
    ///     <see cref="ShatterplaneSettingsSO.aiApproachDistance"/> out in front, then, once within
    ///     <see cref="ShatterplaneSettingsSO.aiSquareDegrees"/> of the axis, straight in along it, so the ring the
    ///     autopilot throws blows with its disk lying in the plate.
    ///
    /// <para><b>What it cannot do.</b> It cannot time the throw: the executor throws when its own count says so. From
    /// a long approach at cruise the ring can fuse before it reaches the plate (240 u/s plus the hull's speed for
    /// 1.1 s), and then the blast lies in front of the plate and takes little. The approach distance is the only
    /// lever the mode has, and SHATTERPLANE.md lists it for the editor run to measure.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (<see cref="GameDataSO.AIDifficultyFor"/>), never the intensity.</para>
    /// </summary>
    public sealed class ShatterplaneSquarer
    {
        sealed class Squarer
        {
            public IPlayer Player;
            public int Level;
            public AIPilot Autopilot;
            public BrittleStarExecutor Executor;
            public int Plate = -1;
            public float NextRetargetAt;
            public Vector3 Steer;
        }

        /// <summary>A plate's front face in world space and whether mass still stands on it.</summary>
        public readonly struct PlateFace
        {
            public readonly Vector3 Centre, Axis;
            public PlateFace(Vector3 centre, Vector3 axis) { Centre = centre; Axis = axis; }
        }

        readonly List<Squarer> _squarers = new();
        readonly List<Prism> _candidates = new();
        GameDataSO _gameData;
        ShatterplaneSettingsSO _settings;
        Func<Vector3, Domains, Vector3?> _nearestCrystal;
        IReadOnlyList<PlateFace> _plates;
        float _plateRadius;
        Vector3 _centre;

        public bool IsArmed => _squarers.Count > 0;

        /// <summary>Arms every AI pilot on the roster. <paramref name="plates"/> are the arena's front faces in world
        /// space; <paramref name="nearestCrystal"/> answers the nearest live crystal of the field a team may take (or null).</summary>
        public void Arm(GameDataSO gameData, ShatterplaneSettingsSO settings, Vector3 centre,
                        IReadOnlyList<PlateFace> plates, float plateRadius, Func<Vector3, Domains, Vector3?> nearestCrystal)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
            _plates = plates;
            _plateRadius = plateRadius;
            _nearestCrystal = nearestCrystal;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;

                var root = status.Transform;
                var squarer = new Squarer
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Autopilot = autopilot,
                    Executor = root ? root.GetComponentInChildren<BrittleStarExecutor>(true) : null,
                    Steer = centre,
                };
                _squarers.Add(squarer);
                var captured = squarer;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands the autopilots back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _squarers.Count; i++)
                if (_squarers[i].Autopilot) _squarers[i].Autopilot.ClearExternalTargetProvider();
            _squarers.Clear();
        }

        public void Tick()
        {
            if (_settings == null || _plates == null) return;
            float now = Time.time;
            for (int i = 0; i < _squarers.Count; i++)
            {
                var s = _squarers[i];
                var self = s.Player?.Vessel?.Transform;
                if (!self || !s.Autopilot || !s.Autopilot.AutoPilotEnabled) continue;

                if (now >= s.NextRetargetAt || s.Plate < 0 || !Standing(s.Plate))
                {
                    s.NextRetargetAt = now + Mathf.Max(0.1f, ShatterplaneSettingsSO.Rung(_settings.aiRetargetSeconds, s.Level));
                    s.Plate = ChoosePlate(s, self.position);
                }

                s.Steer = ChooseSteer(s, self.position);
            }
        }

        Vector3 ChooseSteer(Squarer s, Vector3 from)
        {
            // No spare ring: a near crystal first. It floats on a plate's axis, so it is also the line-up.
            if (s.Executor && s.Executor.SpareRings == 0 && _nearestCrystal != null)
            {
                var crystal = _nearestCrystal(from, s.Player.Domain);
                float detour = ShatterplaneSettingsSO.Rung(_settings.aiCrystalDetourRange, s.Level);
                if (crystal.HasValue && (crystal.Value - from).sqrMagnitude <= detour * detour)
                    return crystal.Value;
            }

            if (s.Plate < 0) return _centre;
            var face = _plates[s.Plate];
            return SquareSteer(from, face.Centre, face.Axis,
                ShatterplaneSettingsSO.Rung(_settings.aiApproachDistance, s.Level),
                ShatterplaneSettingsSO.Rung(_settings.aiSquareDegrees, s.Level));
        }

        /// <summary>
        /// Where to steer to come at a plate square: straight at its centre once the hull is in front of it and within
        /// <paramref name="squareDegrees"/> of its axis, otherwise to the point on the axis
        /// <paramref name="approach"/> out in front. Pure, so the tests read it.
        /// </summary>
        public static Vector3 SquareSteer(Vector3 from, Vector3 plateCentre, Vector3 axis, float approach, float squareDegrees)
        {
            Vector3 v = from - plateCentre;
            float along = Vector3.Dot(v, axis);
            if (along > 1e-3f)
            {
                float lateral = (v - axis * along).magnitude;
                float off = Mathf.Atan2(lateral, along) * Mathf.Rad2Deg;
                if (off <= squareDegrees) return plateCentre;
            }
            return plateCentre + axis * approach;
        }

        int ChoosePlate(Squarer s, Vector3 from)
        {
            int best = -1;
            float bestCost = float.MaxValue;
            float approach = ShatterplaneSettingsSO.Rung(_settings.aiApproachDistance, s.Level);
            for (int i = 0; i < _plates.Count; i++)
            {
                if (!Standing(i)) continue;
                var face = _plates[i];
                // The cost is the way to the line-up point, so a plate already in front of the hull wins.
                float cost = (face.Centre + face.Axis * approach - from).sqrMagnitude;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = i;
            }
            return best;
        }

        /// <summary>Whether a plate's front face still holds breakable mass (any prism there that is not super-shielded).</summary>
        bool Standing(int plate)
        {
            if (plate < 0 || plate >= _plates.Count) return false;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return true;
            index.QuerySphere(_plates[plate].Centre, _plateRadius, _candidates);
            bool any = false;
            for (int i = 0; i < _candidates.Count && !any; i++)
            {
                var prism = _candidates[i];
                any = prism && !prism.destroyed && prism.Domain == Domains.Blue && prism.prismProperties is not { IsSuperShielded: true };
            }
            _candidates.Clear();
            return any;
        }
    }
}
