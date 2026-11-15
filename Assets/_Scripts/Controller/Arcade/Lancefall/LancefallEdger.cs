using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Lancefall: an EDGER that comes at a plate in its own plane, so the lance the Shrike's autopilot
    /// fires runs along the plate and breaks a whole strip of it. Server-only.
    ///
    /// <para><b>Steering only</b> (<see cref="ShrikeSeat"/>): the Shrike's autopilot already picks Space mode and holds
    /// the trigger while two or more prisms not its own lie on the lance's line, and Charge mode when a rival is in its
    /// cone with a missile ready. So the edger only says where the nose goes.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>A crystal in reach</b> (the rung's detour range): to the crystal. It is a missile plant where it is
    /// taken, and its Space widens the lance.</item>
    /// <item><b>Out of the plate's plane</b> (more than the rung's edge degrees) or inside its rim: to the STATION, the
    /// point in the plate's plane the rung's standoff out from its centre, on the hull's side.</item>
    /// <item><b>Edge-on</b>: nose at the plate's centre, off by the rung's aim error laid in the plane. The line the
    /// lance draws is a chord of the plate.</item>
    /// </list>
    /// <para><b>Difficulty is the seat's AI level</b> (<see cref="GameDataSO.AIDifficultyFor"/>), never the intensity.</para>
    /// </summary>
    public sealed class LancefallEdger
    {
        const int Salt = 104;

        /// <summary>A plate in world space: its centre and its axis (the normal of its plane).</summary>
        public readonly struct Plate
        {
            public readonly Vector3 Centre, Axis;
            public Plate(Vector3 centre, Vector3 axis) { Centre = centre; Axis = axis; }
        }

        readonly List<ShrikeSeat> _seats = new();
        readonly List<Prism> _candidates = new();
        LancefallSettingsSO _settings;
        IReadOnlyList<Plate> _plates;
        float _plateRadius;
        Vector3 _centre;

        public bool IsArmed => _seats.Count > 0;

        public void Arm(GameDataSO gameData, LancefallSettingsSO settings, Vector3 centre, IReadOnlyList<Plate> plates,
                        float plateRadius)
        {
            _settings = settings;
            _centre = centre;
            _plates = plates;
            _plateRadius = plateRadius;
            if (settings == null) { Disarm(); return; }
            ShrikeSeats.Arm(gameData, Salt, centre, _seats);
            for (int i = 0; i < _seats.Count; i++) _seats[i].Phase = -1;   // Phase holds the chosen plate
        }

        public void Disarm() => ShrikeSeats.Disarm(_seats);

        public void Tick()
        {
            if (_settings == null || _plates == null) return;
            float now = Time.time;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (!ShrikeSeats.Flying(s)) continue;
                Vector3 from = s.Self.position;

                if (now >= s.NextRetargetAt || !Standing(s.Phase))
                {
                    s.NextRetargetAt = now + Mathf.Max(0.1f, ShrikeSeats.Rung(_settings.aiRetargetSeconds, s.Level));
                    s.Phase = ChoosePlate(s, from);
                    s.Aim = ShrikeSeats.InBall(s.Rng, ShrikeSeats.Rung(_settings.aiAimError, s.Level));
                }

                float detour = ShrikeSeats.Rung(_settings.aiCrystalDetourRange, s.Level);
                if (ShrikeSeats.NearestCrystal(from, detour) is Vector3 crystal)
                {
                    s.Steer = crystal;
                    continue;
                }

                if (s.Phase < 0)
                {
                    s.Steer = _centre;
                    continue;
                }
                var plate = _plates[s.Phase];
                var steer = EdgeSteer(from, plate.Centre, plate.Axis, _plateRadius,
                    ShrikeSeats.Rung(_settings.aiStandoff, s.Level), ShrikeSeats.Rung(_settings.aiEdgeDegrees, s.Level));
                if (steer == plate.Centre)
                {
                    // Off the centre row, across the approach (the reliquary's ribs run through the centre row).
                    Vector3 across = Vector3.Cross(plate.Axis, plate.Centre - from);
                    if (across.sqrMagnitude > 1e-6f) steer += across.normalized * _settings.aiChordOffset;
                    steer += s.Aim - plate.Axis * Vector3.Dot(s.Aim, plate.Axis);
                }
                s.Steer = steer;
            }
        }

        /// <summary>
        /// Where to steer to come at a plate EDGE-ON: at its centre once the hull lies within
        /// <paramref name="edgeDegrees"/> of the plate's plane and outside its rim, otherwise to the station
        /// <paramref name="standoff"/> out from the centre in the plane, on the hull's side. Pure, so the tests read it.
        /// </summary>
        public static Vector3 EdgeSteer(Vector3 from, Vector3 plateCentre, Vector3 axis, float plateRadius, float standoff,
                                        float edgeDegrees)
        {
            Vector3 v = from - plateCentre;
            float along = Vector3.Dot(v, axis);
            Vector3 inPlane = v - axis * along;
            float lateral = inPlane.magnitude;
            if (lateral > plateRadius)
            {
                float off = Mathf.Atan2(Mathf.Abs(along), lateral) * Mathf.Rad2Deg;
                if (off <= edgeDegrees) return plateCentre;
            }
            Vector3 dir;
            if (lateral > 1e-3f) dir = inPlane / lateral;
            else ShatterplanePlates.Basis(axis, out dir, out _);
            return plateCentre + dir * Mathf.Max(standoff, plateRadius + 1f);
        }

        int ChoosePlate(ShrikeSeat s, Vector3 from)
        {
            int best = -1;
            float bestCost = float.MaxValue;
            float standoff = ShrikeSeats.Rung(_settings.aiStandoff, s.Level);
            for (int i = 0; i < _plates.Count; i++)
            {
                if (!Standing(i)) continue;
                var p = _plates[i];
                // The cost is the way to the station, so a plate whose plane the hull already lies in wins.
                float cost = (EdgeSteer(from, p.Centre, p.Axis, _plateRadius, standoff, 0f) - from).sqrMagnitude;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = i;
            }
            return best;
        }

        /// <summary>Whether a plate still holds breakable mass (any neutral prism there that is not super-shielded).</summary>
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
                any = prism && !prism.destroyed && prism.Domain == Domains.Blue
                      && prism.prismProperties is not { IsSuperShielded: true };
            }
            _candidates.Clear();
            return any;
        }
    }
}
