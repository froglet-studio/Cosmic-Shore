using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI of the Wrasse's reef games: it STEERS an AI crew's Grouper and nothing else. Server-only.
    ///
    /// <para><b>An AI Wrasse is already two AIs</b> (KITE_NAVIGATOR.md, round 6b): the hull's <see cref="AIPilot"/>
    /// flies the Grouper, and the seat's own navigator AI flies the kite - it rams a rival on its board when it holds
    /// power, loops the fattest one-element cluster, and pops hostile prisms. Neither is touched here (the LOCKED vessel
    /// rule, and the input-only rule): the guide only answers <see cref="AIPilot.SetExternalTargetProvider"/> with where
    /// the Grouper should be, so the kite has the right things on its board.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Hunt</b> (Cleaner Ram, Charge): with no power on the kite, the nearest crystal (the reef is mostly
    /// Charge); with power, the nearest rival in range, led by its velocity, so it crosses the kite's board.</item>
    /// <item><b>Clean</b> (Blastline, Space): the nearest crystal in reach, else the densest mass not its own colour
    /// (<see cref="Cell.GetExplosionTarget"/>), where a blast takes the most.</item>
    /// <item><b>Build</b> (Reefbuild, Mass): the nearest crystal in reach, else its own cruise orbit through open water
    /// where its clusters go; when its domain trails (or the clock is nearly out) it raids the leader's reef so its kite
    /// pops and blasts it.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class WrasseGrouperGuide
    {
        public enum Plan { Hunt = 0, Clean = 1, Build = 2 }

        sealed class Seat
        {
            public IPlayer Player;
            public int Level;
            public AIPilot Autopilot;
            public KiteNavigator Kite;
            public Vector3 Axis, Start;
            public float NextThinkAt;
            public Vector3 Goal;
            public IPlayer Rival;
        }

        readonly List<Seat> _seats = new();
        GameDataSO _gameData;
        WrasseReefSettingsSO _settings;
        WrasseReefCrystals _crystals;
        Cell _cell;
        Plan _plan;
        Func<Domains, bool> _shouldRaid;

        public bool IsArmed => _seats.Count > 0;

        /// <summary>Arms every AI hull on the roster. <paramref name="shouldRaid"/> answers Build's "is my domain behind".</summary>
        public void Arm(GameDataSO gameData, WrasseReefSettingsSO settings, WrasseReefCrystals crystals, Cell cell, Plan plan,
                        Func<Domains, bool> shouldRaid = null)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _crystals = crystals;
            _cell = cell;
            _plan = plan;
            _shouldRaid = shouldRaid;

            int index = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                status.Transform.TryGetComponent(out KiteNavigator kite);

                // Each AI cruises its own great circle, so two builders do not lay one arc twice (Tapestry's).
                var axis = Quaternion.AngleAxis(index++ * 67f, Vector3.up) * Quaternion.AngleAxis(35f, Vector3.right) * Vector3.up;
                var start = Vector3.Cross(axis, Vector3.forward);
                start = start.sqrMagnitude > 1e-4f ? start.normalized : Vector3.right;

                var seat = new Seat
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Autopilot = autopilot,
                    Kite = kite,
                    Axis = axis,
                    Start = start,
                    Goal = Centre,
                };
                _seats.Add(seat);
                var captured = seat;
                autopilot.SetExternalTargetProvider(() => captured.Goal);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _seats.Count; i++)
                if (_seats[i].Autopilot) _seats[i].Autopilot.ClearExternalTargetProvider();
            _seats.Clear();
        }

        Vector3 Centre => _cell ? _cell.transform.position : Vector3.zero;

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                var self = s.Player?.Vessel?.Transform;
                if (!self || !s.Autopilot || !s.Autopilot.AutoPilotEnabled) continue;

                if (now >= s.NextThinkAt || !IsLiveRival(s.Rival, s.Player))
                {
                    s.NextThinkAt = now + WrasseReefSettingsSO.Rung(_settings.aiRetargetSeconds, s.Level);
                    s.Rival = NearestRival(s, self.position);
                }
                s.Goal = Goal(s, self.position, now);
            }
        }

        Vector3 Goal(Seat s, Vector3 at, float now)
        {
            float reach = WrasseReefSettingsSO.Rung(_settings.aiCrystalReach, s.Level);
            switch (_plan)
            {
                case Plan.Hunt:
                {
                    bool charged = s.Kite && s.Kite.Charges > 0;
                    if (charged && s.Rival?.Vessel?.VesselStatus is { } rival && rival.Transform)
                        return rival.Transform.position +
                               rival.Course * rival.Speed * WrasseReefSettingsSO.Rung(_settings.aiLeadSeconds, s.Level);
                    if (_crystals != null && _crystals.TryNearest(at, reach * (charged ? 0.5f : 1f), out var crystal))
                        return crystal;
                    if (s.Rival?.Vessel?.Transform is { } shadow) return shadow.position;
                    return Orbit(s, now);
                }
                case Plan.Clean:
                {
                    if (_crystals != null && _crystals.TryNearest(at, reach, out var crystal)) return crystal;
                    return _cell ? _cell.GetExplosionTarget(s.Player.Domain) : Orbit(s, now);
                }
                default:
                {
                    if (_shouldRaid != null && _shouldRaid(s.Player.Domain) && _cell)
                        return _cell.GetExplosionTarget(s.Player.Domain);
                    if (_crystals != null && _crystals.TryNearest(at, reach, out var crystal)) return crystal;
                    return Orbit(s, now);
                }
            }
        }

        Vector3 Orbit(Seat s, float now)
        {
            float radius = _settings.crystalScatterRadius * _settings.aiOrbitFraction;
            float angle = (now - _gameData.TurnStartTime) * _settings.aiOrbitDegreesPerSecond;
            return Centre + Quaternion.AngleAxis(angle, s.Axis) * s.Start * radius;
        }

        IPlayer NearestRival(Seat s, Vector3 at)
        {
            float range = WrasseReefSettingsSO.Rung(_settings.aiHuntRange, s.Level);
            float best = range * range;
            IPlayer pick = null;
            foreach (var p in _gameData.Players)
            {
                if (!IsLiveRival(p, s.Player)) continue;
                float d = (p.Vessel.Transform.position - at).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                pick = p;
            }
            return pick;
        }

        static bool IsLiveRival(IPlayer p, IPlayer self) =>
            p != null && self != null && p != self && p.Domain != self.Domain && p.Vessel?.Transform;
    }
}
