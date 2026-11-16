using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Larder: a HUNTER that keeps a rival under its nose so the Shrike's own autopilot paints them, and
    /// fetches crystals for more missile plants when its plants run dry. Server-only.
    ///
    /// <para><b>Steering only</b> (<see cref="ShrikeSeat"/>): the Shrike's autopilot already selects Charge mode and
    /// holds the trigger while a rival hull is inside its lock cone and a plant has a missile ready, and slings at the
    /// steering target when it is far. So the hunter only says where the nose goes.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Plants dry</b> (fewer missiles ready than the rung's floor) and a crystal in reach: to the crystal. A
    /// crystal is a new plant where it was taken.</item>
    /// <item><b>Otherwise</b>: the chosen rival, led by its velocity and off by the rung's aim error (re-rolled each
    /// retarget). The wreckage between them is cover: a missile that meets a block blows on it.</item>
    /// </list>
    /// </summary>
    public sealed class LarderHunter
    {
        const int Salt = 102;

        readonly List<ShrikeSeat> _seats = new();
        LarderSettingsSO _settings;
        GameDataSO _gameData;
        Vector3 _centre;

        public bool IsArmed => _seats.Count > 0;

        public void Arm(GameDataSO gameData, LarderSettingsSO settings, Vector3 centre)
        {
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
            if (settings == null) { Disarm(); return; }
            ShrikeSeats.Arm(gameData, Salt, centre, _seats);
        }

        public void Disarm() => ShrikeSeats.Disarm(_seats);

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (!ShrikeSeats.Flying(s)) continue;
                var self = s.Self;

                if (now >= s.NextRetargetAt || !ShrikeSeats.IsLiveRival(s.Rival, s.Player))
                {
                    s.NextRetargetAt = now + ShrikeSeats.Rung(_settings.aiRetargetSeconds, s.Level);
                    s.Rival = ShrikeSeats.PickRival(_gameData, s, _settings.aiHumanFocus);
                    s.Aim = ShrikeSeats.InBall(s.Rng, ShrikeSeats.Rung(_settings.aiAimError, s.Level));
                }

                bool dry = s.Shrike.MissilesReady < ShrikeSeats.Rung(_settings.aiReloadBelowMissiles, s.Level);
                if (dry && ShrikeSeats.NearestCrystal(self.position, _settings.aiCrystalRange) is Vector3 crystal)
                {
                    s.Steer = crystal;
                    continue;
                }

                var rivalTf = s.Rival?.Vessel?.Transform;
                if (!rivalTf)
                {
                    s.Steer = _centre;
                    continue;
                }
                var status = s.Rival.Vessel.VesselStatus;
                float lead = ShrikeSeats.Rung(_settings.aiLeadSeconds, s.Level);
                s.Steer = rivalTf.position + s.Aim + (status != null ? status.Course * status.Speed * lead : Vector3.zero);
            }
        }
    }
}
