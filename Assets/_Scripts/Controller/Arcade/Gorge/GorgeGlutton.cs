using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Gorge: a GLUTTON that, while its wasp is free, turns the nose at a creature by the shoal it means
    /// to strip, so the wasp's own AI stings that one and feeds it there. Server-only.
    ///
    /// <para><b>Steering only</b> (<see cref="WaspSeat"/>): the wasp's AI stings the creature ahead of its nose and, in a
    /// host, keeps the right trigger held (suction at prisms) and leaves when its time is up. So the glutton only
    /// chooses which creature is ahead: one by the chosen shoal (a choosy AI) or any one (an easy AI).</para>
    /// </summary>
    public sealed class GorgeGlutton
    {
        public interface IReefView
        {
            int ShoalCount { get; }
            Vector3 ShoalCentre(int shoal);
            int Standing(int shoal);
        }

        const int Salt = 127;

        readonly List<WaspSeat> _seats = new();
        GorgeSettingsSO _settings;
        IReefView _reef;
        Cell _cell;
        Vector3 _centre;

        public bool IsArmed => _seats.Count > 0;

        public void Arm(GameDataSO gameData, GorgeSettingsSO settings, IReefView reef, Cell cell)
        {
            _settings = settings;
            _reef = reef;
            _cell = cell;
            _centre = cell ? cell.transform.position : Vector3.zero;
            if (settings == null || reef == null) { Disarm(); return; }
            WaspSeats.Arm(gameData, Salt, _centre, _seats);
        }

        public void Disarm() => WaspSeats.Disarm(_seats);

        public void Tick()
        {
            if (_settings == null || _reef == null) return;
            float now = Time.time;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (!WaspSeats.Flying(s) || !s.Free) continue;

                if (now >= s.NextRetargetAt || !WaspSeats.Takeable(s.Host))
                {
                    s.NextRetargetAt = now + WaspSeats.Rung(_settings.aiRetargetSeconds, s.Level);
                    s.Goal = PickShoal(s.Self.position, WaspSeats.Rung(_settings.aiFullShoalWeight, s.Level));
                    s.Host = WaspSeats.PickHost(s, _cell, s.Goal, WaspSeats.Rung(_settings.aiHostChoosiness, s.Level));
                    s.Aim = WaspSeats.InBall(s.Rng, WaspSeats.Rung(_settings.aiAimError, s.Level));
                }

                s.Steer = (s.Host ? s.Host.PossessionNestPoint : s.Goal) + s.Aim;
            }
        }

        /// <summary>The shoal to feed on: nearest, weighed against fullest by <paramref name="fullWeight"/> (0 nearest
        /// only, 1 fullest only). The centre when the reef is stripped.</summary>
        Vector3 PickShoal(Vector3 from, float fullWeight)
        {
            int n = _reef.ShoalCount, most = 0;
            float far = 1f;
            for (int k = 0; k < n; k++)
            {
                most = Mathf.Max(most, _reef.Standing(k));
                far = Mathf.Max(far, Vector3.Distance(from, _reef.ShoalCentre(k)));
            }
            if (most <= 0) return _centre;

            int best = -1;
            float bestCost = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                int standing = _reef.Standing(k);
                if (standing <= 0) continue;
                float near = Vector3.Distance(from, _reef.ShoalCentre(k)) / far;   // 0 here .. 1 farthest
                float empty = 1f - standing / (float)most;                          // 0 fullest .. 1 nearly bare
                float cost = (1f - fullWeight) * near + fullWeight * empty;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = k;
            }
            return best >= 0 ? _reef.ShoalCentre(best) : _centre;
        }
    }
}
