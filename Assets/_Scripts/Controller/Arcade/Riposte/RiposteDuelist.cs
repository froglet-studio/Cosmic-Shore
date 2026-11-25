using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Riposte: a Swordfish DUELIST. Server-only.
    ///
    /// <para><b>Steering and throttle only - the Swordfish's kit is untouched.</b> It hands
    /// <see cref="AIPilot.SetExternalTargetProvider"/> a point and a throttle floor. The ramp is pressed by the hull's
    /// own boost policy (<c>SwordfishAIBoostPolicy_Riposte</c>, the mode's tuning of it) and the sword is swung by the
    /// hull's own hand (<c>SwordfishSwordAutopilot</c>): both as on every other seat.</para>
    ///
    /// <para><b>Why not Jackstraw's hunter.</b> That one steers straight at a rival, which suits a flail that strikes
    /// at any speed. Flown by a Swordfish it made a slow knot: four hulls at 30 u/s, nobody winding up, the match
    /// decided by whoever was a hair faster at the first touch (measured 2026-11-25, RIPOSTE.md section 5). A sword
    /// lands only while its hull is the faster, so this pilot flies long straight legs (<see cref="RiposteDuel"/>):
    /// across the heap cutting sticks for fuel, through a rival and on past them, straight out, and back in.</para>
    ///
    /// <para><b>It reads the Lists</b> (<see cref="RiposteLanes"/>, by the rung's <c>duelLaneUse</c>): a cutting leg
    /// flies a fuel rail end to end, a run-out goes to the mouth of a lane ahead, and the charge that follows winds up
    /// down that lane before it turns onto the rival. In an arena with no lanes it flies as before.</para>
    ///
    /// <para>Silenced, it runs for a crystal by the rung's chance, as the hunter does. Difficulty is the seat's AI
    /// level, never the intensity.</para>
    /// </summary>
    public sealed class RiposteDuelist
    {
        sealed class Duelist
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public System.Random Rng;
            public int FuelIndex = -1;

            public RiposteDuel.Phase Phase;
            public float PhaseEndsAt;
            public Vector3 LegEnd;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 MissAxis;
            public Vector3 Steer;

            public bool WasSilenced;
            public bool Refuelling;

            /// <summary>The rail this cutting leg flies (-1 = across the heap), where it joins it, whether it has
            /// yet, and the tank when it set out.</summary>
            public int Rail = -1;
            public Vector3 RailEntry;
            public bool OnRail;
            public float RailFuelAtStart;
            /// <summary>The lane whose mouth this run-out flies to, and the lane the next charge winds up down (-1 = none).</summary>
            public int MouthLane = -1, RunInLane = -1;
        }

        readonly List<Duelist> _duelists = new();
        RiposteSettingsSO _settings;
        GameDataSO _gameData;
        Func<Vector3, Vector3?> _nearestCrystal;
        Vector3 _centre;
        float _heapRadius;
        IReadOnlyList<RiposteLanes.Line> _lanes = Array.Empty<RiposteLanes.Line>(), _rails = Array.Empty<RiposteLanes.Line>();
        float _centreClearing;
        /// <summary>Rails a pilot found already cut: nobody flies them again this match.</summary>
        readonly HashSet<int> _dryRails = new();

        public bool IsArmed => _duelists.Count > 0;

        /// <summary>Arms every AI pilot on the roster. <paramref name="heapRadius"/>: the world radius of the ball
        /// the sticks fill. <paramref name="nearestCrystal"/>: the nearest live crystal in a clearing, or null.</summary>
        /// <paramref name="lanes"/>, <paramref name="rails"/>: the arena's lines in world space (empty in an arena
        /// without them). <paramref name="centreClearing"/>: the centre clearing's world radius.
        public void Arm(GameDataSO gameData, RiposteSettingsSO settings, Vector3 centre, float heapRadius,
                        Func<Vector3, Vector3?> nearestCrystal, IReadOnlyList<RiposteLanes.Line> lanes = null,
                        IReadOnlyList<RiposteLanes.Line> rails = null, float centreClearing = 0f)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _lanes = lanes ?? Array.Empty<RiposteLanes.Line>();
            _rails = rails ?? Array.Empty<RiposteLanes.Line>();
            _centreClearing = centreClearing;
            _dryRails.Clear();
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
            _heapRadius = Mathf.Max(60f, heapRadius);
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

                int level = (int)gameData.AIDifficultyFor(p);
                var duelist = new Duelist
                {
                    Player = p,
                    Level = level,
                    Status = status,
                    Autopilot = autopilot,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 97)),
                    Steer = centre,
                    Phase = RiposteDuel.Phase.Charge,
                    PhaseEndsAt = Time.time + _settings.duelPhaseSeconds,
                };
                if (status.ActionHandler != null &&
                    status.ActionHandler.TryGetBoundAction<RampBoostActionSO>(out var ramp, out _) && ramp.UsesFuel)
                    duelist.FuelIndex = ramp.FuelResourceIndex;

                _duelists.Add(duelist);
                var captured = duelist;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
                autopilot.SetThrottleFloor(JackstrawSettingsSO.Rung(_settings.duelThrottleFloor, level));
            }
        }

        /// <summary>Hands every autopilot back (the throttle floor goes with the target). Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _duelists.Count; i++)
                if (_duelists[i].Autopilot) _duelists[i].Autopilot.ClearExternalTargetProvider();
            _duelists.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _duelists.Count; i++)
            {
                var d = _duelists[i];
                var self = d.Player?.Vessel?.Transform;
                if (!self || !d.Autopilot || !d.Autopilot.AutoPilotEnabled) continue;

                bool silenced = VesselSilence.IsSilenced(d.Status);
                if (silenced && !d.WasSilenced)
                    d.Refuelling = d.Rng.NextDouble() <
                                   JackstrawSettingsSO.Rung(_settings.aiRefuelWhenSilencedChance, d.Level);
                if (!silenced) d.Refuelling = false;
                d.WasSilenced = silenced;

                if (d.Refuelling && _nearestCrystal?.Invoke(self.position) is Vector3 crystal)
                {
                    d.Steer = crystal;
                    continue;
                }

                if (now >= d.NextRetargetAt || !JackstrawHunter.IsLiveRival(d.Rival, d.Player))
                    Retarget(d, self, now);

                Advance(d, self, now);
                d.Steer = Steer(d, self);
            }
        }

        void Retarget(Duelist d, Transform self, float now)
        {
            d.NextRetargetAt = now + Mathf.Max(0.1f, JackstrawSettingsSO.Rung(_settings.aiRetargetSeconds, d.Level));
            d.Rival = JackstrawHunter.ChooseRival(_gameData, _settings, d.Player, self, d.Level);
            // A fresh miss for the run: a random direction, at up to the rung's radius.
            var m = new Vector3((float)(d.Rng.NextDouble() * 2 - 1), (float)(d.Rng.NextDouble() * 2 - 1),
                                (float)(d.Rng.NextDouble() * 2 - 1));
            d.MissAxis = (m.sqrMagnitude > 1e-6f ? m.normalized : Vector3.right) *
                         (JackstrawSettingsSO.Rung(_settings.aiMissRadius, d.Level) * (float)d.Rng.NextDouble());
        }

        /// <summary>Where this leg steers: a rail's near end until the pilot is on it, the lane's inner end while it
        /// winds up down a lane, else the leg's own point.</summary>
        Vector3 Steer(Duelist d, Transform self)
        {
            switch (d.Phase)
            {
                case RiposteDuel.Phase.Reap when d.Rail >= 0:
                    if (!d.OnRail && RiposteLanes.LinedUp(self.position, _rails[d.Rail], _settings.duelLineUpRadius))
                        d.OnRail = true;
                    return d.OnRail ? d.LegEnd : d.RailEntry;
                case RiposteDuel.Phase.Charge:
                    if (d.RunInLane >= 0)
                    {
                        var rivalTf = d.Rival?.Vessel?.Transform;
                        float rivalDistance = rivalTf ? (rivalTf.position - self.position).magnitude : float.PositiveInfinity;
                        if (RiposteLanes.RunIn((self.position - _centre).magnitude, _centreClearing, rivalDistance,
                                               _settings.duelLaneBreakDistance))
                            return _lanes[d.RunInLane].A;
                        d.RunInLane = -1;   // the wind-up is spent: the rest of this charge is on the rival
                    }
                    return ChargePoint(d, self);
                default:
                    return d.LegEnd;
            }
        }

        bool ReadsArena(Duelist d) =>
            d.Rng.NextDouble() < JackstrawSettingsSO.Rung(_settings.duelLaneUse, d.Level);

        /// <summary>Moves the duelist to its next leg when this one is flown, and lays that leg's end.</summary>
        void Advance(Duelist d, Transform self, float now)
        {
            var rivalTf = d.Rival?.Vessel?.Transform;
            bool hasRival = rivalTf;
            bool passed = hasRival && RiposteDuel.Passed(self.position, self.forward, d.Status.Speed, rivalTf.position,
                                                         d.Rival.Vessel.VesselStatus?.Speed ?? 0f,
                                                         _settings.duelPassRadius);
            float arrive = _settings.duelArriveRadius;
            bool arrived = d.Phase != RiposteDuel.Phase.Charge &&
                           (d.LegEnd - self.position).sqrMagnitude <= arrive * arrive;
            bool timedOut = now >= d.PhaseEndsAt;

            float reapBelow = JackstrawSettingsSO.Rung(_settings.duelReapBelowFuel, d.Level);
            float reapUntil = JackstrawSettingsSO.Rung(_settings.duelReapUntilFuel, d.Level);
            var next = RiposteDuel.Next(d.Phase, Fuel01(d), reapBelow, reapUntil, hasRival, passed, arrived, timedOut);

            // The same leg goes on, unless it is a cutting line that ended (then it is followed by another).
            bool relay = next == RiposteDuel.Phase.Reap && d.Phase == RiposteDuel.Phase.Reap && (arrived || timedOut);
            if (next == d.Phase && !relay)
            {
                if (next == RiposteDuel.Phase.Charge && timedOut) d.PhaseEndsAt = now + _settings.duelPhaseSeconds;
                return;
            }

            // A rail run that earned nothing found the rail already cut.
            if (d.Phase == RiposteDuel.Phase.Reap && d.Rail >= 0 && d.OnRail &&
                Fuel01(d) - d.RailFuelAtStart < _settings.duelRailDryGain)
                _dryRails.Add(d.Rail);
            // A run-out that reached a lane's mouth is followed by a wind-up down that lane.
            d.RunInLane = d.Phase == RiposteDuel.Phase.Extend && next == RiposteDuel.Phase.Charge && arrived
                ? d.MouthLane : -1;
            d.Rail = -1;
            d.MouthLane = -1;

            d.Phase = next;
            d.PhaseEndsAt = now + _settings.duelPhaseSeconds;
            float arena = _heapRadius * 1.15f;
            switch (next)
            {
                case RiposteDuel.Phase.Extend:
                    d.MouthLane = _lanes.Count > 0 && ReadsArena(d)
                        ? RiposteLanes.MouthAhead(self.position, self.forward, _lanes, _settings.duelMouthConeDegrees,
                                                  _settings.duelArriveRadius * 2f)
                        : -1;
                    d.LegEnd = d.MouthLane >= 0
                        ? _lanes[d.MouthLane].B
                        : RiposteDuel.ExtendPoint(self.position, self.forward,
                            JackstrawSettingsSO.Rung(_settings.duelExtendDistance, d.Level), _centre, arena);
                    break;
                case RiposteDuel.Phase.Reap:
                    d.Rail = _rails.Count > 0 && ReadsArena(d) ? RiposteLanes.Nearest(self.position, _rails, _dryRails) : -1;
                    if (d.Rail >= 0)
                    {
                        d.RailEntry = RiposteLanes.Entry(self.position, _rails[d.Rail]);
                        d.LegEnd = RiposteLanes.Exit(self.position, _rails[d.Rail]);
                        d.OnRail = false;
                        d.RailFuelAtStart = Fuel01(d);
                    }
                    else
                        d.LegEnd = RiposteDuel.ReapPoint(self.position, _centre, _heapRadius, (float)d.Rng.NextDouble(),
                            (float)d.Rng.NextDouble(), _settings.duelReapSpreadDegrees);
                    break;
            }
        }

        /// <summary>The ramp's tank, 0..1; 1 for a hull whose ramp burns none.</summary>
        static float Fuel01(Duelist d)
        {
            var rs = d.Status.ResourceSystem;
            if (d.FuelIndex < 0 || rs == null || rs.Resources == null || d.FuelIndex >= rs.Resources.Count) return 1f;
            var r = rs.Resources[d.FuelIndex];
            return r.MaxAmount > 0f ? Mathf.Clamp01(r.CurrentAmount / r.MaxAmount) : 1f;
        }

        /// <summary>Through the led rival, off by the plan's miss, and on past them; the centre when nobody is in range.</summary>
        Vector3 ChargePoint(Duelist d, Transform self)
        {
            var rivalTf = d.Rival?.Vessel?.Transform;
            if (!rivalTf) return _centre;
            var status = d.Rival.Vessel.VesselStatus;
            float lead = JackstrawSettingsSO.Rung(_settings.aiLeadSeconds, d.Level);
            Vector3 aim = rivalTf.position + d.MissAxis +
                          (status != null ? status.Course * status.Speed * lead : Vector3.zero);
            return RiposteDuel.ChargePoint(self.position, aim,
                                           JackstrawSettingsSO.Rung(_settings.duelChargeOvershoot, d.Level));
        }
    }
}
