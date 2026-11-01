using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Slip's ride: the hull ZIPS along the trail it touched to the landing, instead of appearing there (Garrett,
    /// 2026-10-21: "lets have the slips not teleport the honeyguide but give a satisfying zip over so they keep some
    /// continuity and are not denied the joy of fast travel").
    ///
    /// <para>For the ride the hull's own transformer stands down and this flies the hull along the trail's prisms, lifted
    /// <c>zipLift</c> off them, ramping up to <c>zipSpeed</c> and turning smoothly onto each leg. The hull keeps laying
    /// its trail, so the path it took stays on the map. On landing the transformer takes the hull back at the exit heading
    /// (a zero-length <see cref="IVessel.SetPose"/>, which seeds its rotation and momentum), and <c>zipExitBoost</c> carries the
    /// speed out. The crew runs only on the machine that flies the hull, which writes the transform as the transformer does,
    /// so every other machine sees the ride through the hull's own replication.</para>
    ///
    /// <para>The triggers ride the same rail (lab round 6): Flit, Hexapod Eyelet Jet and Thruster Swarm are a straight ride
    /// ahead at a fixed speed, and the Hard Light Lasso reels the hull to the vessel or creature it hooked (round 8). Those that
    /// plough shatter the hostile prisms the hull crosses; the lasso strikes what is near where it arrives.</para>
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        readonly List<Vector3> _zipPath = new();
        int _zipNext;
        float _zipSpeed, _rideTopSpeed, _rideRamp, _rideExitBoost, _rideLeft, _rideArriveStrike;
        bool _zipping, _zipTookTransformer, _ridePlow;
        Vector3 _zipUp, _zipExitHeading;

        /// <summary>True while the hull rides a slip.</summary>
        public bool Zipping => _zipping;

        void BeginZip(int contactIndex, int landIndex, Vector3 landing, Vector3 exitHeading)
        {
            var t = _vessel.Transform;
            _zipUp = t.up;
            _zipPath.Clear();
            _zipPath.Add(t.position);
            int step = landIndex >= contactIndex ? 1 : -1;
            for (int i = contactIndex; ; i += step)
            {
                var p = _pts[i];
                AddZipPoint(new Vector3((float)p.X, (float)p.Y, (float)p.Z) + _zipUp * _cfg.ZipLift);
                if (i == landIndex) break;
            }
            AddZipPoint(landing);
            StartRide(exitHeading, Mathf.Max(_status != null ? _status.Speed : 0f, 1f), _cfg.ZipSpeed,
                      _cfg.ZipSpeed / Mathf.Max(0.01f, _cfg.ZipRamp), _cfg.ZipExitBoost, false, 0f, float.MaxValue);
        }

        /// <summary>A straight ride `distance` ahead in `seconds` (Flit, Eyelet Jet, Thruster Swarm). `plow` shatters the
        /// hostile prisms it crosses.</summary>
        void BeginJet(float distance, float seconds, bool plow)
        {
            var t = _vessel.Transform;
            _zipUp = t.up;
            _zipPath.Clear();
            _zipPath.Add(t.position);
            AddZipPoint(t.position + t.forward * distance);
            float speed = distance / Mathf.Max(0.02f, seconds);
            StartRide(t.forward, speed, speed, 0f, 0f, plow, 0f, seconds * 2f);
        }

        /// <summary>The Hard Light Lasso's reel: to `at` (the rival vessel or creature it hooked), ploughing, then a strike
        /// of every rival vessel and creature within `strike` u where it arrives (lab round 8).</summary>
        void BeginReel(Vector3 at, float strike)
        {
            var t = _vessel.Transform;
            var heading = at - t.position;
            if (heading.sqrMagnitude < 1e-4f) return;
            _zipUp = t.up;
            _zipPath.Clear();
            _zipPath.Add(t.position);
            AddZipPoint(at);
            float speed = Mathf.Max(_status != null ? _status.Speed : 0f, _cfg.LassoReelSpeed);
            StartRide(heading.normalized, speed, speed, 0f, 0f, true, strike, _cfg.LassoReelSeconds);
        }

        void StartRide(Vector3 exitHeading, float speed, float topSpeed, float ramp, float exitBoost, bool plow, float arriveStrike,
                       float maxSeconds)
        {
            _zipExitHeading = exitHeading;
            _zipNext = 1;
            _zipSpeed = speed;
            _rideTopSpeed = topSpeed;
            _rideRamp = ramp;
            _rideExitBoost = exitBoost;
            _ridePlow = plow;
            _rideArriveStrike = arriveStrike;
            _rideLeft = maxSeconds;

            var tr = _status?.VesselTransformer;
            _zipTookTransformer = tr && tr.IsActive;
            if (_zipTookTransformer) tr.ToggleActive(false);
            _zipping = true;
        }

        void AddZipPoint(Vector3 p)
        {
            if ((p - _zipPath[^1]).sqrMagnitude > 0.25f) _zipPath.Add(p);
        }

        /// <summary>One frame of the ride. True while the hull is still on it.</summary>
        bool StepZip(float dt)
        {
            if (!_zipping) return false;
            var t = _vessel.Transform;
            _zipSpeed = Mathf.MoveTowards(_zipSpeed, _rideTopSpeed, _rideRamp * dt);
            _rideLeft -= dt;

            Vector3 pos = t.position, tangent = _zipExitHeading;
            float move = _zipSpeed * dt;
            while (_zipNext < _zipPath.Count)
            {
                var to = _zipPath[_zipNext];
                var d = to - pos;
                float len = d.magnitude;
                if (len > 1e-4f) tangent = d / len;
                if (len > move) { pos += tangent * move; break; }
                pos = to;
                move -= len;
                _zipNext++;
            }

            var want = Quaternion.LookRotation(tangent, Vector3.ProjectOnPlane(_zipUp, tangent).sqrMagnitude > 1e-6f ? _zipUp : Vector3.up);
            var rot = Quaternion.Slerp(t.rotation, want, 1f - Mathf.Exp(-_cfg.ZipTurnRate * dt));
            t.SetPositionAndRotation(pos, rot);
            if (_status != null) _status.blockRotation = rot;   // the trail it lays follows the ride, as the transformer's does
            if (_ridePlow) Shatter(pos, _cfg.ContactRadius);

            if (_zipNext < _zipPath.Count && _rideLeft > 0f) return true;
            if (_rideArriveStrike > 0f) Strike(pos, _rideArriveStrike, null);
            EndZip(true);
            return false;
        }

        void EndZip(bool landed)
        {
            if (!_zipping) return;
            _zipping = false;
            var tr = _status?.VesselTransformer;
            if (_zipTookTransformer && tr) tr.ToggleActive(true);
            _zipTookTransformer = false;
            if (!landed || _vessel == null || !UnityLiveness.Alive(_vessel.Transform)) return;
            var t = _vessel.Transform;
            var up = Vector3.ProjectOnPlane(t.up, _zipExitHeading).sqrMagnitude > 1e-6f ? t.up : Vector3.up;
            _vessel.SetPose(new Pose(t.position, Quaternion.LookRotation(_zipExitHeading, up)));
            GiveBoost(_rideExitBoost);
        }
    }
}
