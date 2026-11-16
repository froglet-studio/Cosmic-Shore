using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Thornbush: a BUILDER that picks perches through the heap so the Shrike's own autopilot docks,
    /// leaves a nest and moves on, and now and then RAIDS the nearest rival nest. Server-only.
    ///
    /// <para><b>Steering only</b> (<see cref="ShrikeSeat"/>): the Shrike's autopilot slings at a far steering target
    /// (docking on the first stick its tether meets), presses Mass to detach and leave a nest a few seconds after it
    /// docks, lances while rival mass lies on its line and paints a rival hull in its cone. So the builder only says
    /// where the nose goes.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Build</b>: a random perch inside the heap at least <see cref="ThornbushSettingsSO.aiPerchSpacing"/> from
    /// the last one, so the three nests the Shrike keeps spread out. Held while docked; re-picked after the rung's
    /// perch time or once the hull has detached from a dock.</item>
    /// <item><b>Raid</b> (the rung's chance at each new goal): the nearest rival nest block - super-shielded, a rival
    /// domain's - found by one shared scan a second. Below Space 5 the lance passes super-shields, so a raid is the
    /// missiles' work and the trail round the nest.</item>
    /// </list>
    /// </summary>
    public sealed class ThornbushBuilder
    {
        const int Salt = 103;
        const int PhaseBuild = 0, PhaseRaid = 1;

        readonly List<ShrikeSeat> _seats = new();
        readonly List<bool> _wasDocked = new();
        readonly List<Prism> _scan = new();
        readonly List<Prism> _rivalNestBlocks = new();
        ThornbushSettingsSO _settings;
        Vector3 _centre;
        float _heapRadius;
        float _nextScanAt;

        public bool IsArmed => _seats.Count > 0;

        public void Arm(GameDataSO gameData, ThornbushSettingsSO settings, Vector3 centre, float heapRadius)
        {
            _settings = settings;
            _centre = centre;
            _heapRadius = Mathf.Max(60f, heapRadius);
            _nextScanAt = 0f;
            if (settings == null) { Disarm(); return; }
            ShrikeSeats.Arm(gameData, Salt, centre, _seats);
            _wasDocked.Clear();
            for (int i = 0; i < _seats.Count; i++)
            {
                _wasDocked.Add(false);
                PickPerch(_seats[i], 0f);
            }
        }

        public void Disarm()
        {
            ShrikeSeats.Disarm(_seats);
            _wasDocked.Clear();
            _rivalNestBlocks.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            if (now >= _nextScanAt)
            {
                _nextScanAt = now + _settings.aiScanSeconds;
                ScanNests();
            }

            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (!ShrikeSeats.Flying(s)) continue;

                bool docked = s.Shrike.Docked;
                bool detached = _wasDocked[i] && !docked;
                _wasDocked[i] = docked;

                // Docked: hold the perch - the hull's own autopilot leaves the nest and detaches.
                if (docked && s.Phase == PhaseBuild) { s.Steer = s.Goal; continue; }

                if (detached || now >= s.NextRetargetAt)
                {
                    var nest = s.Rng.NextDouble() < ShrikeSeats.Rung(_settings.aiRaidChance, s.Level)
                        ? NearestRivalNest(s)
                        : null;
                    if (nest.HasValue)
                    {
                        s.Phase = PhaseRaid;
                        s.Goal = nest.Value + ShrikeSeats.InBall(s.Rng, ShrikeSeats.Rung(_settings.aiAimError, s.Level));
                        s.NextRetargetAt = now + ShrikeSeats.Rung(_settings.aiRaidSeconds, s.Level);
                    }
                    else PickPerch(s, now);
                }
                s.Steer = s.Goal;
            }
        }

        void PickPerch(ShrikeSeat s, float now)
        {
            s.Phase = PhaseBuild;
            var last = s.Station;
            Vector3 perch = _centre;
            for (int tries = 0; tries < 8; tries++)
            {
                perch = _centre + ShrikeSeats.InBall(s.Rng, _heapRadius * 0.85f);
                if ((perch - last).sqrMagnitude >= _settings.aiPerchSpacing * _settings.aiPerchSpacing) break;
            }
            s.Station = perch;
            s.Goal = perch;
            s.NextRetargetAt = now + ShrikeSeats.Rung(_settings.aiPerchSeconds, s.Level);
        }

        /// <summary>
        /// SERVER. One sphere query over the heap a second: every super-shielded block that belongs to a pilot (a nest
        /// block - the heap's own sticks are neutral and never super-shielded).
        /// </summary>
        void ScanNests()
        {
            _rivalNestBlocks.Clear();
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(_centre, _heapRadius + _settings.aiScanMargin, _scan);
            for (int i = 0; i < _scan.Count; i++)
            {
                var p = _scan[i];
                if (!p || p.destroyed || p.Domain == Domains.Blue) continue;
                if (p.prismProperties is not { IsSuperShielded: true }) continue;
                _rivalNestBlocks.Add(p);
            }
            _scan.Clear();
        }

        Vector3? NearestRivalNest(ShrikeSeat s)
        {
            var self = s.Self;
            if (!self) return null;
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var own = s.Player.Domain;
            for (int i = 0; i < _rivalNestBlocks.Count; i++)
            {
                var p = _rivalNestBlocks[i];
                if (!p || p.destroyed || p.Domain == own) continue;
                float d = (p.transform.position - self.position).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = p.transform.position;
            }
            return best;
        }
    }
}
