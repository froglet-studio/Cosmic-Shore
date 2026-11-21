using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Jackstraw: a HUNTER that runs its flail through rival pilots. Server-only.
    ///
    /// <para><b>Steering only - the Thresher's kit is untouched.</b> The ball and every chain link strike any enemy
    /// hull they reach, at any speed and in Mass Mode too (<c>ThresherExecutor.HitVesselsAlong</c>), so an AI Thresher
    /// scores by being in the right place; it needs no press of its own. The hunter only hands
    /// <see cref="AIPilot.SetExternalTargetProvider"/> a point to fly at. The hull's own autopilot sling
    /// (<c>ThresherSlingAIPolicySO</c>, on the prefab) still plants on any corner past ~70 degrees and swings round the
    /// stopped ball - which, with a rival beside it, is the plant-scythe: the chain sweeps a disc a chain wide.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Pick a rival</b>: the cheapest by distance and bearing, cheaper still if human
    /// (<see cref="JackstrawSettingsSO.aiHumanFocus"/>) or silenced and unable to hit back
    /// (<see cref="JackstrawSettingsSO.aiSilencedPreyFocus"/>).</item>
    /// <item><b>Run through them</b>: aim at their led position, off by the rung's miss radius, and
    /// <see cref="JackstrawSettingsSO.aiOvershoot"/> past them, so the hull passes and the trailing ball crosses
    /// them instead of the hull braking short.</item>
    /// <item><b>Refuel when silenced</b>: a silenced Thresher harms nothing (Docs/ElementalAbilitySystem/SILENCE.md),
    /// so on each silence it rolls the rung's chance to break off to the nearest live crystal in a clearing.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class JackstrawHunter
    {
        sealed class Hunter
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 MissAxis;
            public Vector3 Steer;

            public bool WasSilenced;
            public bool Refuelling;
        }

        readonly List<Hunter> _hunters = new();
        JackstrawSettingsSO _settings;
        GameDataSO _gameData;
        Func<Vector3, Vector3?> _nearestCrystal;
        Vector3 _centre;

        public bool IsArmed => _hunters.Count > 0;

        /// <summary>Arms every AI pilot on the roster. <paramref name="nearestCrystal"/> answers the nearest live
        /// crystal in a clearing (or null), from this machine's copy of them.</summary>
        public void Arm(GameDataSO gameData, JackstrawSettingsSO settings, Vector3 centre,
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

                var hunter = new Hunter
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 84)),
                    Steer = centre,
                };
                _hunters.Add(hunter);
                var captured = hunter;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
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

                bool silenced = VesselSilence.IsSilenced(h.Status);
                if (silenced && !h.WasSilenced)
                    h.Refuelling = h.Rng.NextDouble() <
                                   JackstrawSettingsSO.Rung(_settings.aiRefuelWhenSilencedChance, h.Level);
                if (!silenced) h.Refuelling = false;
                h.WasSilenced = silenced;

                if (h.Refuelling && _nearestCrystal?.Invoke(self.position) is Vector3 crystal)
                {
                    h.Steer = crystal;
                    continue;
                }

                if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player))
                    Retarget(h, self, now);
                h.Steer = RunPoint(h, self);
            }
        }

        void Retarget(Hunter h, Transform self, float now)
        {
            h.NextRetargetAt = now + Mathf.Max(0.1f, JackstrawSettingsSO.Rung(_settings.aiRetargetSeconds, h.Level));
            h.Rival = ChooseRival(h, self);
            // A fresh miss for the run: a random direction, at up to the rung's radius.
            var d = new Vector3((float)(h.Rng.NextDouble() * 2 - 1), (float)(h.Rng.NextDouble() * 2 - 1),
                                (float)(h.Rng.NextDouble() * 2 - 1));
            h.MissAxis = (d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right) *
                         (JackstrawSettingsSO.Rung(_settings.aiMissRadius, h.Level) * (float)h.Rng.NextDouble());
        }

        IPlayer ChooseRival(Hunter h, Transform self)
        {
            float huntRange = JackstrawSettingsSO.Rung(_settings.aiHuntRange, h.Level);
            float silencedFocus = Mathf.Max(1f, JackstrawSettingsSO.Rung(_settings.aiSilencedPreyFocus, h.Level));
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveRival(candidate, h.Player)) continue;
                Vector3 to = candidate.Vessel.Transform.position - self.position;
                float distance = to.magnitude;
                if (distance > huntRange) continue;
                // A rival dead ahead costs its distance; one straight behind costs ~3x it.
                float facing = distance > 1e-3f ? Vector3.Dot(self.forward, to / distance) : 1f;
                float cost = distance / Mathf.Lerp(0.35f, 1f, 0.5f * (facing + 1f));
                if (!candidate.IsInitializedAsAI) cost /= _settings.aiHumanFocus;
                if (VesselSilence.IsSilenced(candidate.Vessel.VesselStatus)) cost /= silencedFocus;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;   // the flail never strikes a team-mate
            return candidate.Vessel?.Transform != null;
        }

        /// <summary>Where to fly: through the led rival, off by the plan's miss, and on past them.</summary>
        Vector3 RunPoint(Hunter h, Transform self)
        {
            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) return _centre;
            var status = h.Rival.Vessel.VesselStatus;
            float lead = JackstrawSettingsSO.Rung(_settings.aiLeadSeconds, h.Level);
            Vector3 aim = rivalTf.position + h.MissAxis +
                          (status != null ? status.Course * status.Speed * lead : Vector3.zero);
            Vector3 run = aim - self.position;
            return run.sqrMagnitude > 1e-4f ? aim + run.normalized * _settings.aiOvershoot : aim;
        }
    }
}
