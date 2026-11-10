using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Flatline: a HUNTER that steers. Server-only.
    ///
    /// <para><b>It steers; it never presses.</b> The Brittle Star already flies its own weapons with the replicated
    /// controls a human presses (<c>BrittleStarExecutor.Autopilot</c>): it holds the trigger when a rival hull is on
    /// the nose, takes Charge Mode's cords against a dense wall inside cord reach (a pane is one), Space Mode's guns
    /// against one further out, and throws its ring into a big cluster. A second presser here would fight it every
    /// three seconds, and the mode never changes the vessel, so this hunter only answers WHERE to fly, through
    /// <see cref="AIPilot.SetExternalTargetProvider"/>:</para>
    ///   • with no spare ring and a crystal within <see cref="FlatlineSettingsSO.aiCrystalDetourRange"/>, to that
    ///     crystal (a crystal is a spare ring);
    ///   • otherwise at the chosen rival, led by <see cref="FlatlineSettingsSO.aiLeadSeconds"/> of its flight, so the
    ///     rival ends up on the nose (where the rounds go) and inside cord reach (where a Strike pays eight rounds);
    ///   • with no rival in the cell, at the centre of the field.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (<see cref="GameDataSO.AIDifficultyFor"/>), never the intensity.</para>
    /// </summary>
    public sealed class FlatlineHunter
    {
        sealed class Hunter
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public BrittleStarExecutor Executor;
            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 Steer;
        }

        readonly List<Hunter> _hunters = new();
        GameDataSO _gameData;
        FlatlineSettingsSO _settings;
        Func<Vector3, Vector3?> _nearestCrystal;
        Vector3 _centre;

        public bool IsArmed => _hunters.Count > 0;

        /// <summary>Arms every AI pilot on the roster. <paramref name="nearestCrystal"/> answers the nearest live
        /// crystal of the field (or null), from this machine's copy of it.</summary>
        public void Arm(GameDataSO gameData, FlatlineSettingsSO settings, Vector3 centre,
                        Func<Vector3, Vector3?> nearestCrystal)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
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
                var hunter = new Hunter
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Executor = root ? root.GetComponentInChildren<BrittleStarExecutor>(true) : null,
                    Steer = centre,
                };
                _hunters.Add(hunter);
                var captured = hunter;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands the autopilots back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _hunters.Count; i++)
                if (_hunters[i].Autopilot) _hunters[i].Autopilot.ClearExternalTargetProvider();
            _hunters.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _hunters.Count; i++)
            {
                var h = _hunters[i];
                var self = h.Player?.Vessel?.Transform;
                if (!self || !h.Autopilot || !h.Autopilot.AutoPilotEnabled) continue;

                if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player))
                {
                    h.NextRetargetAt = now + Mathf.Max(0.1f, FlatlineSettingsSO.Rung(_settings.aiRetargetSeconds, h.Level));
                    h.Rival = ChooseRival(h, self);
                }

                h.Steer = ChooseSteer(h, self.position);
            }
        }

        Vector3 ChooseSteer(Hunter h, Vector3 from)
        {
            // No spare ring: a near crystal first (each is a spare ring).
            if (h.Executor && h.Executor.SpareRings == 0 && _nearestCrystal != null)
            {
                var crystal = _nearestCrystal(from);
                float detour = FlatlineSettingsSO.Rung(_settings.aiCrystalDetourRange, h.Level);
                if (crystal.HasValue && (crystal.Value - from).sqrMagnitude <= detour * detour)
                    return crystal.Value;
            }

            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) return _centre;
            var status = h.Rival.Vessel.VesselStatus;
            float lead = FlatlineSettingsSO.Rung(_settings.aiLeadSeconds, h.Level);
            return status != null ? rivalTf.position + status.Course * (status.Speed * lead) : rivalTf.position;
        }

        IPlayer ChooseRival(Hunter h, Transform self)
        {
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveRival(candidate, h.Player)) continue;
                Vector3 to = candidate.Vessel.Transform.position - self.position;
                float distance = to.magnitude;
                // A rival dead ahead costs its distance; one straight behind costs ~3x it.
                float facing = distance > 1e-3f ? Vector3.Dot(self.forward, to / distance) : 1f;
                float cost = distance / Mathf.Lerp(0.35f, 1f, 0.5f * (facing + 1f));
                if (!candidate.IsInitializedAsAI) cost /= _settings.aiHumanFocus;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;   // rounds and cords never score on a team-mate
            return candidate.Vessel?.Transform != null;
        }
    }
}
