using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Marionette: a PUPPETEER that, while its wasp is free, turns the nose at the creature nearest the
    /// rival it is after, so the wasp's own AI stings that one and rides it into the rival. Server-only.
    ///
    /// <para><b>Steering only</b> (<see cref="WaspSeat"/>): the wasp's AI stings the creature ahead of its nose, flies a
    /// host at the nearest rival, holds the attack and leaves when its time is up. So the puppeteer only chooses which
    /// creature is ahead: the one nearest the rival (a choosy AI) or any one (an easy AI), aimed off by the rung's
    /// error.</para>
    /// </summary>
    public sealed class MarionettePuppeteer
    {
        const int Salt = 126;

        readonly List<WaspSeat> _seats = new();
        MarionetteSettingsSO _settings;
        GameDataSO _gameData;
        Cell _cell;
        Vector3 _centre;

        public bool IsArmed => _seats.Count > 0;

        public void Arm(GameDataSO gameData, MarionetteSettingsSO settings, Cell cell)
        {
            _gameData = gameData;
            _settings = settings;
            _cell = cell;
            _centre = cell ? cell.transform.position : Vector3.zero;
            if (settings == null) { Disarm(); return; }
            WaspSeats.Arm(gameData, Salt, _centre, _seats);
        }

        public void Disarm() => WaspSeats.Disarm(_seats);

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (!WaspSeats.Flying(s) || !s.Free) continue;

                if (now >= s.NextRetargetAt || !WaspSeats.IsLiveRival(s.Rival, s.Player) || !WaspSeats.Takeable(s.Host))
                {
                    s.NextRetargetAt = now + WaspSeats.Rung(_settings.aiRetargetSeconds, s.Level);
                    s.Rival = WaspSeats.PickRival(_gameData, s, _settings.aiHumanFocus);
                    var rivalTf = s.Rival?.Vessel?.Transform;
                    s.Goal = rivalTf ? rivalTf.position : _centre;
                    s.Host = WaspSeats.PickHost(s, _cell, s.Goal, WaspSeats.Rung(_settings.aiHostChoosiness, s.Level));
                    s.Aim = WaspSeats.InBall(s.Rng, WaspSeats.Rung(_settings.aiAimError, s.Level));
                }

                s.Steer = (s.Host ? s.Host.PossessionNestPoint : s.Goal) + s.Aim;
            }
        }
    }
}
