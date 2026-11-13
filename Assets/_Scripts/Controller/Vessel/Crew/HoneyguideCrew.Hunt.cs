using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI Honeyguide's hunt (Garrett, 2026-11-02: "lets have the AI go for more than just omnicrystals in freestyle. lets
    /// have the honeyguide also uses it abilities to hunt fauna and opposing prisms. omnicrystals should still be acquired, but
    /// not at such a high frequency"), and its slip guard ("lets have the honeyguide AI not get stuck when slipping").
    ///
    /// <para><b>The hunt.</b> While an AI flies the hull (an AI crew, or the human playing the Ratel) outside a match or in
    /// freestyle, every <c>aiHuntThink</c> seconds it weighs what is near: a creature, a rival vessel, the densest opposing
    /// prism mass (the cell's own density grid, the query aggression-1 fauna hunt with), the Mass hive and the nearest
    /// omnicrystal. Each pulls by its want, halved at <c>aiHuntFalloff</c>; the goal it holds is kept <c>aiHuntCommit</c>
    /// seconds and then gives way only to a pull <c>aiHuntSwitch</c> times its own. After an omnicrystal (or a hive) it leaves
    /// omnicrystals alone <c>aiCrystalRest</c> seconds unless one is within <c>aiCrystalSnatch</c>. It steers through the
    /// hull's own <see cref="AIPilot"/> (its external target hook, input-only); an omnicrystal goal hands the steering back to
    /// the pilot's own crystal chase. A match's hook is never touched. The triggers it pulls on the goal are
    /// <c>HoneyguideCrew.Triggers.cs</c>'s <c>Wanted</c>: LT strikes a creature or vessel on its lane, RT pecks prisms.</para>
    ///
    /// <para><b>The slip guard.</b> The lab's slip takes any trail of a battery colour the nose lies along, either way. An AI
    /// that cannot choose rode trails that carried it away from its goal, turned back, met the same trail and rode it again:
    /// stuck. The AI now slips only when the landing is at least <c>aiSlipMinGain</c> nearer its goal (the hunt's goal, or the
    /// pilot's own objective), turns the trail down for <c>aiSlipSkipSeconds</c> otherwise, rests its slips <c>aiSlipRest</c>
    /// seconds after <c>aiSlipLoopCount</c> of them inside <c>aiSlipLoopWindow</c>, and every ride has a watchdog
    /// (<c>rideWatchdog</c>, HoneyguideCrew.Zip.cs). A person's slips are the lab's, untouched.</para>
    ///
    /// <para><b>The strip.</b> What the hull is after and doing (the goal, the ride, the strikes and breaks it lands) is kept
    /// here for the symbolic strip over the Ratel's board (HoneyguideDynamoView.Strip).</para>
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        /// <summary>What the hull is after, for the strip.</summary>
        internal enum HuntGoal { None = 0, Crystal = 1, Hive = 2, Fauna = 3, Vessel = 4, Prisms = 5 }

        /// <summary>The ride the hull is on, for the strip.</summary>
        internal enum RideKind { None = 0, Slip = 1, Jet = 2, Reel = 3 }

        HuntGoal _goal;
        Vector3 _goalAt;
        Fauna _goalFauna;
        Transform _goalVessel;
        int _goalColour;
        float _goalSince, _huntThinkAt, _crystalRestUntil, _massScanAt;
        Vector3 _massAt;
        bool _massOk, _steering;
        int _massColour;
        System.Func<Vector3> _huntSteer;
        RideKind _rideKind;
        int _rideColour;
        float _aiSlipRestUntil, _slipWindowAt, _aiSkipT;
        Trail _aiSkipTrail;   // the trail the AI turned down: no slip on it for aiSlipSkipSeconds (its prisms still count)
        int _slipWindowN;
        readonly int[] _colourTally = new int[4];

        /// <summary>What the hull is after: the AI's hunt, or the nearest omnicrystal / Mass hive for a person.</summary>
        internal HuntGoal Goal => _goal;
        /// <summary>The goal's colour (1 Ruby, 2 Jade, 3 Gold; 0 none or another) - a rival's or a prism mass's.</summary>
        internal int GoalColour => _goalColour;
        /// <summary>The goal's distance from the hull (u), or -1 with no goal.</summary>
        internal float GoalDistance { get; private set; } = -1f;
        /// <summary>The ride the hull is on now.</summary>
        internal RideKind Ride => _zipping ? _rideKind : RideKind.None;
        /// <summary>The colour of the trail a slip rides (1-3), or 0.</summary>
        internal int RideColour => _rideColour;
        /// <summary>When the hull last struck a creature or vessel, broke a hostile prism, started a Peck burst, took an
        /// omnicrystal (match clock).</summary>
        internal float StruckAt { get; private set; } = -99f;
        internal float BrokeAt { get; private set; } = -99f;
        internal float PeckAt { get; private set; } = -99f;
        internal float CrystalAt { get; private set; } = -99f;
        /// <summary>This life's tallies: creatures and vessels struck, hostile prisms broken.</summary>
        internal int StruckCount { get; private set; }
        internal int BrokeCount { get; private set; }
        /// <summary>The hull's speed (u/s), for the strip's scroll.</summary>
        internal float HullSpeed => _status != null ? _status.Speed : 0f;
        /// <summary>True while the AI's hunt holds the steering.</summary>
        internal bool Hunting => _steering;

        void ResetHunt()
        {
            _goal = HuntGoal.None;
            _goalFauna = null;
            _goalVessel = null;
            _goalColour = 0;
            _goalSince = _huntThinkAt = _crystalRestUntil = _massScanAt = 0f;
            _massOk = false;
            _rideKind = RideKind.None;
            _rideColour = 0;
            _aiSlipRestUntil = _slipWindowAt = _aiSkipT = 0f;
            _aiSkipTrail = null;
            _slipWindowN = 0;
            GoalDistance = -1f;
            StruckAt = BrokeAt = PeckAt = CrystalAt = -99f;
            StruckCount = BrokeCount = 0;
        }

        // ------------------------------------------------------------------ the hunt

        bool HuntRuns(AIPilot pilot)
        {
            if (!_cfg.AIHunts || !AIFlies || _hive != null || !pilot || !pilot.AutoPilotEnabled) return false;
            if (_cfg.AIHuntFreestyleOnly && CosmicShore.Core.FlightClock.IsGameActive && !CosmicShore.Core.FlightClock.IsFreestyleActive) return false;
            var hook = pilot.ExternalTargetProvider;
            return hook == null || ReferenceEquals(hook, _huntSteer);   // a match's hook is the match's
        }

        void StepHunt()
        {
            var pilot = _status?.AIPilot;
            if (!HuntRuns(pilot))
            {
                ReleaseHunt(pilot);
                return;
            }
            float now = Time.time;
            var pos = _vessel.Transform.position;

            // a live goal moves; a dead one is thought about again at once
            switch (_goal)
            {
                case HuntGoal.Fauna:
                    if (!_goalFauna || _goalFauna.IsDying) _huntThinkAt = 0f;
                    else _goalAt = _goalFauna.transform.position;
                    break;
                case HuntGoal.Vessel:
                    if (!_goalVessel) _huntThinkAt = 0f;
                    else _goalAt = _goalVessel.position;
                    break;
                case HuntGoal.Hive:
                    if (!_massHiveUp) _huntThinkAt = 0f;
                    break;
            }

            if (now >= _huntThinkAt)
            {
                _huntThinkAt = now + _cfg.AIHuntThink;
                ThinkHunt(pos, now);
            }
            GoalDistance = _goal == HuntGoal.None ? -1f : Vector3.Distance(pos, _goalAt);

            // the omnicrystal is the pilot's own chase (its drift commit, its ram); everything else is steered here
            bool steer = _goal is HuntGoal.Fauna or HuntGoal.Vessel or HuntGoal.Prisms or HuntGoal.Hive;
            if (steer && !_steering)
            {
                _huntSteer ??= () => _goalAt;
                pilot.SetExternalTargetProvider(_huntSteer);
                _steering = true;
            }
            else if (!steer && _steering) ReleaseSteer(pilot);
        }

        void ThinkHunt(Vector3 pos, float now)
        {
            HuntGoal best = HuntGoal.None;
            float bestScore = 0f;
            Vector3 bestAt = pos;
            Fauna bestFauna = null;
            Transform bestVessel = null;
            int bestColour = 0;
            bool committed = now - _goalSince < _cfg.AIHuntCommit;

            void Offer(HuntGoal g, Vector3 at, float want, Fauna f, Transform v, int colour)
            {
                if (want <= 0f) return;
                float score = want / (1f + Vector3.Distance(pos, at) / _cfg.AIHuntFalloff);
                bool same = g == _goal && (g == HuntGoal.Fauna ? f == _goalFauna : g != HuntGoal.Vessel || v == _goalVessel);
                if (same) score *= committed ? 1000f : _cfg.AIHuntSwitch;
                if (score <= bestScore) return;
                bestScore = score;
                best = g;
                bestAt = at;
                bestFauna = f;
                bestVessel = v;
                bestColour = colour;
            }

            float reach2 = _cfg.AIHuntReach * _cfg.AIHuntReach;

            // the Mass hive (a human Ratel's hull plants one): the AI flies there to open hive mode
            if (_massHiveUp) Offer(HuntGoal.Hive, _massHiveAt, _cfg.AIWantHive, null, null, 0);

            // the nearest omnicrystal, unless the AI rests from them (one within snatch reach is taken anyway)
            var active = Crystal.Active;
            float cd = float.MaxValue;
            Vector3 cAt = pos;
            for (int k = 0; k < active.Count; k++)
            {
                var c = active[k];
                if (!c || c.crystalProperties.Element != Element.Omni) continue;
                float d = (c.transform.position - pos).sqrMagnitude;
                if (d < cd) { cd = d; cAt = c.transform.position; }
            }
            if (cd < float.MaxValue)
            {
                bool snatch = cd <= _cfg.AICrystalSnatch * _cfg.AICrystalSnatch;
                if (snatch || now >= _crystalRestUntil) Offer(HuntGoal.Crystal, cAt, _cfg.AIWantCrystal * (snatch ? 3f : 1f), null, null, 0);
            }

            // the nearest creature that is not ours (our own bees guard our comb)
            var cell = CellAt(pos);
            if (cell)
            {
                var fauna = cell.LiveFauna;
                Fauna nf = null;
                float fd = reach2;
                for (int i = 0; i < fauna.Count; i++)
                {
                    var f = fauna[i];
                    if (!f || f.IsDying || f.Domain == _status.Domain) continue;
                    float d = (f.transform.position - pos).sqrMagnitude;
                    if (d < fd) { fd = d; nf = f; }
                }
                if (nf) Offer(HuntGoal.Fauna, nf.transform.position, _cfg.AIWantFauna, nf, null, HoneyguideConfigSO.ColourOf(nf.Domain));
            }

            // the nearest rival vessel
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
            {
                IVesselStatus nv = null;
                float vd = reach2;
                for (int i = 0; i < players.Count; i++)
                {
                    var v = RivalStatus(players[i]);
                    if (v == null) continue;
                    float d = (v.Transform.position - pos).sqrMagnitude;
                    if (d < vd) { vd = d; nv = v; }
                }
                if (nv != null) Offer(HuntGoal.Vessel, nv.Transform.position, _cfg.AIWantVessel, null, nv.Transform, HoneyguideConfigSO.ColourOf(nv.Domain));
            }

            // the densest opposing prism mass, looked for every aiPrismMassScan seconds
            if (cell && now >= _massScanAt)
            {
                _massScanAt = now + _cfg.AIPrismMassScan;
                _massAt = cell.GetExplosionTarget(_status.Domain);
                _massOk = CountMass(_massAt, out _massColour) >= _cfg.AIPrismMassMin;
            }
            if (_massOk && (_massAt - pos).sqrMagnitude <= reach2)
                Offer(HuntGoal.Prisms, _massAt, _cfg.AIWantPrisms, null, null, _massColour);

            if (best != _goal || bestFauna != _goalFauna || bestVessel != _goalVessel) _goalSince = now;
            _goal = best;
            _goalAt = bestAt;
            _goalFauna = bestFauna;
            _goalVessel = bestVessel;
            _goalColour = bestColour;
        }

        // The hostile prisms within aiPrismMassRadius of `at`, and the colour most of them are.
        int CountMass(Vector3 at, out int colour)
        {
            colour = 0;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return 0;
            index.QuerySphere(at, _cfg.AIPrismMassRadius, _hits);
            System.Array.Clear(_colourTally, 0, _colourTally.Length);
            int n = 0;
            foreach (var p in _hits)
            {
                if (!IsHostile(p)) continue;
                n++;
                _colourTally[HoneyguideConfigSO.ColourOf(p.Domain)]++;
            }
            for (int c = 1; c < _colourTally.Length; c++) if (_colourTally[c] > _colourTally[colour]) colour = c;
            return n;
        }

        void ReleaseSteer(AIPilot pilot)
        {
            if (_steering && pilot && ReferenceEquals(pilot.ExternalTargetProvider, _huntSteer)) pilot.ClearExternalTargetProvider();
            _steering = false;
        }

        void ReleaseHunt(AIPilot pilot)
        {
            ReleaseSteer(pilot);
            if (_goal is not (HuntGoal.Fauna or HuntGoal.Vessel or HuntGoal.Prisms)) return;   // the omnicrystal and hive are ScanStrip's too
            _goal = HuntGoal.None;
            _goalFauna = null;
            _goalVessel = null;
            GoalDistance = -1f;
        }

        // An omnicrystal or a hive opened: the AI rests from omnicrystals, and the strip flashes.
        void NoteCrystalTaken()
        {
            CrystalAt = Time.time;
            if (_cfg) _crystalRestUntil = Time.time + _cfg.AICrystalRest;
            if (_goal == HuntGoal.Crystal || _goal == HuntGoal.Hive) _huntThinkAt = 0f;
        }

        /// <summary>Where the AI is going: the hunt's goal, a race coach's goal, or the pilot's own objective. False when no AI flies.</summary>
        bool AIGoal(out Vector3 goal)
        {
            goal = default;
            if (!AIFlies) return false;
            if (_steering) { goal = _goalAt; return true; }
            if (RaceCoach != null && RaceCoach.TryRaceGoal(this, out goal)) return true;   // Slip Line: the race says where
            var pilot = _status?.AIPilot;
            if (!pilot || !pilot.AutoPilotEnabled) return false;
            goal = pilot.ObjectivePosition;
            return true;
        }

        // ------------------------------------------------------------------ the slip guard

        /// <summary>The AI's slip from `from` to `landing`: taken only when it brings the AI nearer its goal, and not in a
        /// loop. Counts the slip when it says yes.</summary>
        bool AISlipHelps(Vector3 from, Vector3 landing)
        {
            float now = Time.time;
            if (now < _aiSlipRestUntil) return false;
            if (AIGoal(out var goal))
            {
                float before = Vector3.Distance(from, goal), after = Vector3.Distance(landing, goal);
                if (after > before - Mathf.Min(_cfg.AISlipMinGain, 0.5f * before)) return false;
            }
            if (now - _slipWindowAt > _cfg.AISlipLoopWindow) { _slipWindowAt = now; _slipWindowN = 0; }
            if (++_slipWindowN >= _cfg.AISlipLoopCount)
            {
                _aiSlipRestUntil = now + _cfg.AISlipRest;
                _slipWindowN = 0;
            }
            return true;
        }

        // ------------------------------------------------------------------ the AI's LT on its own

        /// <summary>Base Flit for the AI: a creature or rival vessel on the dart's lane is struck; otherwise it darts only to
        /// travel, with its goal far and dead ahead, and never cuts a slip short.</summary>
        bool FlitWanted(Vector3 pos, Vector3 fwd)
        {
            if (StrikeOnLane(pos, fwd, _cfg.FlitDistance, _cfg.FlitStrikeRadiusFor(_status) * 1.5f)) return true;
            if (_zipping) return false;
            if (!AIGoal(out var goal)) return true;   // the lab's freestyle rule: ahead is closer
            var to = goal - pos;
            float d = to.magnitude;
            return d > _cfg.FlitDistance * 1.5f && Vector3.Dot(to / d, fwd) > 0.95f;
        }

        /// <summary>A rival vessel or creature (not ours) within `r` of the lane `length` ahead of the nose.</summary>
        bool StrikeOnLane(Vector3 pos, Vector3 fwd, float length, float r)
        {
            float r2 = r * r;
            bool OnLane(Vector3 p)
            {
                var d = p - pos;
                float along = Vector3.Dot(d, fwd);
                return along > 0f && along <= length && (d - fwd * along).sqrMagnitude <= r2;
            }
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var v = RivalStatus(players[i]);
                    if (v != null && OnLane(v.Transform.position)) return true;
                }
            var cell = CellAt(pos);
            if (!cell) return false;
            var fauna = cell.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
            {
                var f = fauna[i];
                if (f && !f.IsDying && f.Domain != _status.Domain && OnLane(f.transform.position)) return true;
            }
            return false;
        }
    }
}
