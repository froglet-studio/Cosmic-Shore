using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide's two triggers (Honeyguide Lab round 8; Garrett 2026-11-01: "right trigger should be a space active
    /// ability ... left trigger should be charge active ability ... charge should always be able to attack vessels and fauna.
    /// space should always be able to reduce opposing prism volume ... the ability has a preview when pulled and then
    /// completes when released").
    ///
    /// <para>RT is the <b>Space</b> active: <b>Peck</b> (a burst of <c>peckBurst</c> shots through the hull's own gun,
    /// <c>HoneyguidePeckAction.asset</c>) unless an upgrade is banked. LT is the <b>Charge</b> active: <b>Flit</b> (a dart
    /// that strikes every rival vessel and creature near its lane) unless an upgrade is banked. A pull shows the preview
    /// (HoneyguideCrew.Preview.cs); the release fires it. Both triggers down at once is the cancel (on a phone: both thumbs
    /// lifted), and the previews drop unfired.</para>
    ///
    /// <para>Every make of one of the Ratel's thirteen upgrade cards (the lab's <c>ACTIVES8</c>) BANKS <c>usesPerCard</c>
    /// uses on its trigger (Garrett 2026-11-01: "each completion of a pattern should be a stored use of the ability"). The
    /// trigger's bank is a stack: the newest banked ability is current and fires first; the same ability banked again moves
    /// to the top with its uses added. A release with nothing to hit fizzles and keeps its use.</para>
    ///
    /// <para>The human's pulls come from <see cref="HoneyguideTriggerActionSO"/> (the prefab maps RightStickAction and
    /// LeftStickAction to it). While an AI flies the hull - an AI crew, or the human playing the Ratel - this reads the moment
    /// itself, input-only: it pulls a trigger, holds it <c>aiPreviewSeconds</c> and lets go (the lab's <c>wanted8</c>).</para>
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        /// <summary>One banked ability on a trigger: its card, and the uses it has left.</summary>
        internal sealed class Banked
        {
            public HoneyguideUpgrade Def;
            public HoneyguideEffect Effect;
            public string Name;
            public int Uses;
        }

        struct Bomb
        {
            public Vector3 At;
            public float Left, Radius;
        }

        /// <summary>RT's (0) and LT's (1) banks, oldest first: the last is current.</summary>
        internal readonly List<Banked>[] Banks = { new(), new() };
        readonly bool[] _btn = new bool[2], _btnWas = new bool[2], _aiWant = new bool[2], _pv = new bool[2];
        readonly float[] _trigCd = new float[2], _aiLetGoAt = new float[2];
        readonly List<Bomb> _bombs = new();
        readonly List<Prism> _hits = new();
        readonly List<Vector3> _hops = new();
        readonly HashSet<object> _struck = new();
        readonly List<(Prism p, float d)> _turn = new();
        ActionExecutorRegistry _execs;
        bool _peckOn, _cancel;
        float _peckLeft, _aiTriggerAt, _droneLeft, _droneCd, _droneR, _thrustCd;
        int _thrusts;

        bool AIFlies => _aiCrew || _humanEngineers;

        /// <summary>The current (newest) banked ability on trigger k, or null while its base power is current.</summary>
        internal Banked Current(int k) => k >= 0 && k < 2 && Banks[k].Count > 0 ? Banks[k][^1] : null;

        /// <summary>Every use banked on trigger k (the chip's ticks).</summary>
        internal int BankedUses(int k)
        {
            int n = 0;
            if (k >= 0 && k < 2) foreach (var b in Banks[k]) n += b.Uses;
            return n;
        }

        /// <summary>True while trigger k is pulled and shows its preview.</summary>
        internal bool Previewing(int k) => k >= 0 && k < 2 && _pv[k];

        /// <summary>The human pilot's trigger k (0 RT, 1 LT) went down or up. <see cref="HoneyguideTriggerActionSO"/> calls it.</summary>
        public void SetTrigger(int k, bool down)
        {
            if (k < 0 || k >= _btn.Length || _core == null || AIFlies) return;
            _btn[k] = down;
        }

        void ResetTriggers()
        {
            SetPeck(false);
            for (int k = 0; k < 2; k++)
            {
                Banks[k].Clear();
                _btn[k] = _btnWas[k] = _aiWant[k] = _pv[k] = false;
                _trigCd[k] = _aiLetGoAt[k] = 0f;
            }
            _bombs.Clear();
            _cancel = false;
            _peckLeft = _aiTriggerAt = _droneLeft = _droneCd = _thrustCd = 0f;
            _thrusts = 0;
            HidePreviews();
        }

        // A pull held across a seat swap belongs to neither seat: it drops unfired.
        void EndTriggers()
        {
            SetPeck(false);
            _peckLeft = 0f;
            for (int k = 0; k < 2; k++) _btn[k] = _btnWas[k] = _aiWant[k] = _pv[k] = false;
            _cancel = false;
            HidePreviews();
        }

        // A made upgrade card banks usesPerCard uses on its trigger; the same ability again moves to the top with its uses
        // added (the lab's bank8). Returns the uses it now has.
        int Bank(HoneyguideEffect effect, HoneyguideUpgrade def, string name)
        {
            int k = Mathf.Clamp(def.trigger, 0, 1);
            var q = Banks[k];
            int n = Mathf.Max(1, _cfg.UsesPerCard);
            int at = q.FindIndex(b => b.Effect == effect);
            if (at >= 0) { n += q[at].Uses; q.RemoveAt(at); }
            q.Add(new Banked { Def = def, Effect = effect, Name = name, Uses = n });
            Play(_cfg.UpgradeSfx);
            return n;
        }

        // ------------------------------------------------------------------ pull, preview, release

        void StepTriggers(float dt, bool riding)
        {
            if (AIFlies) StepTriggerAI();
            bool d0 = AIFlies ? _aiWant[0] : _btn[0], d1 = AIFlies ? _aiWant[1] : _btn[1];
            if (!AIFlies && d0 && d1 && (_pv[0] || _pv[1])) _cancel = true;   // both thumbs lifted: the previews drop
            for (int k = 0; k < 2; k++)
            {
                bool down = k == 0 ? d0 : d1, was = _btnWas[k];
                _btnWas[k] = down;
                _trigCd[k] -= dt;
                if (down && !was && !_pv[k] && _trigCd[k] <= 0f && !_cancel) _pv[k] = true;
                if (!_pv[k] || down) continue;
                _pv[k] = false;
                if (_cancel) continue;
                Fire(k, riding);
            }
            if (!d0 && !d1) _cancel = false;
            StepPeck(dt);
            StepThrusters(dt, riding);
            StepDrone(dt);
            StepBombs(dt);
            StepPreviews();
        }

        // One release (the lab's fire8). A banked ability spends a use only when it does something: with no target it
        // fizzles and keeps it.
        void Fire(int k, bool riding)
        {
            var b = Current(k);
            var t = _vessel.Transform;
            Vector3 pos = t.position, fwd = t.forward;
            if (b == null)
            {
                if (k == 0)   // Peck: a burst through the hull's own gun
                {
                    StartPeckBurst();
                    _trigCd[0] = _cfg.PeckCooldown;
                }
                else          // Flit: strike along the dart's lane, then dart
                {
                    if (riding) EndZip(true);
                    _struck.Clear();
                    for (float d = 0f; d <= _cfg.FlitDistance; d += 3f) Strike(pos + fwd * d, _cfg.FlitStrikeRadius, _struck);
                    BeginJet(_cfg.FlitDistance, _cfg.FlitSeconds, false);
                    _trigCd[1] = _cfg.FlitCooldown;
                    Play(_cfg.FlitSfx);
                }
                return;
            }

            float reach = b.Def.reach;
            bool did = true;
            switch (b.Effect)
            {
                case HoneyguideEffect.Lance:   // Genesis Lance: shatter the line, then lay a lane along it
                {
                    ShatterLine(pos, fwd, reach);
                    int count = Mathf.FloorToInt((reach - _cfg.LanceLaneStart) / Mathf.Max(1f, _cfg.CondenserSpacing)) + 1;
                    if (count > 0) LayLane(NewestColour(), _cfg.LanceLaneStart, count);
                    break;
                }
                case HoneyguideEffect.LittleBang:   // Little Bang Grenade: lobbed ahead, it bursts after its fuse
                    _bombs.Add(new Bomb { At = pos + fwd * _cfg.GrenadeThrow, Left = _cfg.GrenadeFuse, Radius = reach });
                    break;
                case HoneyguideEffect.Sepia:   // Sepia Drone: circles and zaps
                    _droneLeft = _cfg.DroneSeconds;
                    _droneCd = 0f;
                    _droneR = reach;
                    break;
                case HoneyguideEffect.Fugacity:   // Fugacity Discharger: the full ring; boost for what it breaks
                    GiveBoost(Mathf.Min(_cfg.FugacityBoostCap, _cfg.FugacityBoostEach * Shatter(pos, reach)));
                    break;
                case HoneyguideEffect.Harmonizer:   // Particle Harmonizer: the nearest hostile prisms in the cone turn ours
                    Harmonize(pos, fwd, reach);
                    break;
                case HoneyguideEffect.Eyelet:
                    if (riding) EndZip(true);
                    BeginJet(reach, _cfg.EyeletSeconds, true);
                    break;
                case HoneyguideEffect.Thruster:   // Thruster Swarm: thrusts, thrusterGap apart
                    _thrusts = _cfg.ThrusterThrusts;
                    _thrustCd = 0f;
                    break;
                case HoneyguideEffect.Lasso:   // Hard Light Lasso: hook a rival vessel or creature ahead, reel in, strike
                {
                    if (!StrikeTargetAhead(pos, fwd, reach, _cfg.LassoCone, out var at)) { did = false; break; }
                    if (riding) EndZip(true);
                    BeginReel(at, _cfg.StrikeRadius);
                    break;
                }
                case HoneyguideEffect.Monte:   // Monte Cannon: hop, striking at every landing
                {
                    if (MonteHopList(reach, _hops) == 0) { did = false; break; }
                    if (riding) EndZip(true);
                    _struck.Clear();
                    var rot = t.rotation;
                    foreach (var h in _hops)
                    {
                        _vessel.SetPose(new Pose(h, rot));
                        Strike(h, _cfg.StrikeRadius, _struck);
                    }
                    Play(_cfg.JumpSfx);
                    break;
                }
                case HoneyguideEffect.Blink:
                case HoneyguideEffect.Portals:
                case HoneyguideEffect.Rosen:
                case HoneyguideEffect.Ftl:
                {
                    if (riding) EndZip(true);
                    if (!DoJump(b.Effect, reach)) { did = false; break; }
                    Strike(_vessel.Transform.position, _cfg.StrikeRadius, null);
                    break;
                }
            }
            if (!did) { Toast(_cfg.NoJumpTarget); return; }
            if (--b.Uses <= 0) Banks[k].Remove(b);
            _trigCd[k] = Mathf.Max(_trigCd[k], _cfg.PostFireCooldown);
            Play(b.Def.sfx);
        }

        // ------------------------------------------------------------------ Peck: a burst through the gun

        // The gun fires a volley the instant it starts and one every 1 / firingRate after, so it runs for just under
        // peckBurst of its shots. The action asset is shared by every Honeyguide: the vessel's own executors carry the state
        // (FullAutoActionSO's contract).
        void StartPeckBurst()
        {
            float rate = _cfg.PeckAction is FullAutoActionSO gun ? Mathf.Max(0.1f, gun.FiringRate) : 10f;
            _peckLeft = (Mathf.Max(1, _cfg.PeckBurst) - 0.5f) / rate;
            SetPeck(true);
        }

        void StepPeck(float dt)
        {
            if (_peckLeft <= 0f) return;
            _peckLeft -= dt;
            if (_peckLeft <= 0f) SetPeck(false);
        }

        void SetPeck(bool on)
        {
            if (on == _peckOn) return;
            _peckOn = on;
            var action = _cfg ? _cfg.PeckAction : null;
            if (!action || _status == null) return;
            if (!_execs && _vessel != null && UnityLiveness.Alive(_vessel.Transform))
                _execs = _vessel.Transform.GetComponentInChildren<ActionExecutorRegistry>(true);
            if (!_execs) return;
            if (on) VesselActionTape.Start(action, _execs, _status);
            else VesselActionTape.Stop(action, _execs, _status);
        }

        // ------------------------------------------------------------------ what a fired upgrade goes on doing

        // Thruster Swarm: each thrust a short ploughing jet ahead, thrusterGap apart (it waits while the hull is on a rail).
        void StepThrusters(float dt, bool riding)
        {
            if (_thrusts <= 0) return;
            _thrustCd -= dt;
            if (_thrustCd > 0f || riding || _zipping) return;
            _thrustCd = _cfg.ThrusterGap;
            _thrusts--;
            var u = _cfg.UpgradeFor(HoneyguideEffect.Thruster);
            BeginJet(u != null ? u.reach : 20f, _cfg.ThrusterSeconds, true);
        }

        // Sepia Drone: zaps the nearest hostile prism within its reach, droneZapsPerSecond times a second.
        void StepDrone(float dt)
        {
            if (_droneLeft <= 0f) return;
            _droneLeft -= dt;
            _droneCd -= dt;
            if (_droneCd > 0f) return;
            _droneCd = 1f / Mathf.Max(0.1f, _cfg.DroneZapsPerSecond);
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            var pos = _vessel.Transform.position;
            index.QuerySphere(pos, _droneR, _hits);
            Prism best = null;
            float bd = float.MaxValue;
            foreach (var p in _hits)
            {
                if (!IsHostile(p)) continue;
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best) Break(best, pos);
        }

        // Little Bang Grenade: in flight, then the burst.
        void StepBombs(float dt)
        {
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                var b = _bombs[i];
                b.Left -= dt;
                if (b.Left > 0f) { _bombs[i] = b; continue; }
                _bombs.RemoveAt(i);
                Shatter(b.At, b.Radius);
            }
        }

        // Particle Harmonizer: up to harmonizerMaxTurned hostile prisms in the cone, nearest first, turn to our domain
        // ("opposing volume becomes yours").
        void Harmonize(Vector3 pos, Vector3 fwd, float reach)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || reach <= 0f) return;
            index.QuerySphere(pos, reach, _hits);
            float c = Mathf.Cos(_cfg.HarmonizerCone * Mathf.Deg2Rad);
            _turn.Clear();
            foreach (var p in _hits)
            {
                if (!IsHostile(p)) continue;
                var d = p.transform.position - pos;
                float m = d.magnitude;
                if (m > 4f && m <= reach && Vector3.Dot(d / m, fwd) >= c) _turn.Add((p, m));
            }
            _turn.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < _turn.Count && i < _cfg.HarmonizerMaxTurned; i++) _turn[i].p.ChangeTeam(_status.Domain);
        }

        // ------------------------------------------------------------------ strikes (LT, Charge): vessels and creatures

        /// <summary>Strikes every rival vessel and every creature within r of `at` (the lab's strike). A vessel takes a
        /// scored Strike (petals only, decided on the shooter's owner); a creature is jousted. `seen` keeps one strike per
        /// target per use (null: no memory). Returns how many it struck.</summary>
        int Strike(Vector3 at, float r, HashSet<object> seen)
        {
            if (r <= 0f || _status == null) return 0;
            int n = 0;
            float r2 = r * r;
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null && ElementalTransfer.IsDecidedHere(_status))
                for (int i = 0; i < players.Count; i++)
                {
                    var victim = RivalStatus(players[i]);
                    if (victim == null || (victim.Transform.position - at).sqrMagnitude > r2) continue;
                    if (seen != null && !seen.Add(victim)) continue;
                    if (StrikeVessel(victim, victim.Transform.position - _vessel.Transform.position)) n++;
                }
            var cell = CellAt(at);
            if (cell)
            {
                var fauna = cell.LiveFauna;
                for (int i = fauna.Count - 1; i >= 0; i--)
                {
                    if (i >= fauna.Count) continue;   // a joust can take more than one creature off the list
                    var f = fauna[i];
                    if (!f || f.IsDying || (f.transform.position - at).sqrMagnitude > r2) continue;
                    if (seen != null && !seen.Add(f)) continue;
                    if (f.Jousted(_status.PlayerName)) n++;
                }
            }
            return n;
        }

        bool StrikeVessel(IVesselStatus victim, Vector3 velocity)
        {
            const CombatHitClass hitClass = CombatHitClass.Strike;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, _cfg.StrikeCooldown, ElementalDebuffSources.VesselContact,
                                         out int superseded))
                return false;
            CombatHitDrain.ApplyPriced(victim, _status, hitClass, superseded, velocity, ElementalDebuffSources.VesselContact);
            var hitEvent = _cfg.GameData ? _cfg.GameData.OnCombatHitLanded : null;
            if (hitEvent)
                hitEvent.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName,
                    VictimName = victim.PlayerName,
                    HitClass = hitClass,
                    SupersededRank = superseded,
                });
            return true;
        }

        IVesselStatus RivalStatus(IPlayer player)
        {
            var victim = player?.Vessel?.VesselStatus;
            if (victim == null || ReferenceEquals(victim, _status)) return null;
            if (victim.Domain == _status.Domain || !victim.Transform) return null;
            return victim;
        }

        static Cell CellAt(Vector3 at)
        {
            var cell = Cell.FindCellContaining(at);
            return cell ? cell : Cell.FindNearestActiveCell(at);
        }

        /// <summary>The nearest rival vessel or creature within `reach` and `cone` degrees of the nose, farther than 6 u
        /// (the lasso's hook).</summary>
        bool StrikeTargetAhead(Vector3 pos, Vector3 fwd, float reach, float cone, out Vector3 at)
        {
            at = pos;
            float c = Mathf.Cos(cone * Mathf.Deg2Rad), bd = float.MaxValue;
            bool found = false;
            Vector3 best = pos;
            void Look(Vector3 p)
            {
                var d = p - pos;
                float m = d.magnitude;
                if (m > 6f && m <= reach && Vector3.Dot(d / m, fwd) >= c && m < bd) { bd = m; best = p; found = true; }
            }
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var victim = RivalStatus(players[i]);
                    if (victim != null) Look(victim.Transform.position);
                }
            var cell = CellAt(pos);
            if (cell)
            {
                var fauna = cell.LiveFauna;
                for (int i = 0; i < fauna.Count; i++)
                    if (fauna[i] && !fauna[i].IsDying) Look(fauna[i].transform.position);
            }
            at = best;
            return found;
        }

        bool StrikeNear(Vector3 at, float r)
        {
            float r2 = r * r;
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var victim = RivalStatus(players[i]);
                    if (victim != null && (victim.Transform.position - at).sqrMagnitude <= r2) return true;
                }
            var cell = CellAt(at);
            if (!cell) return false;
            var fauna = cell.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
                if (fauna[i] && !fauna[i].IsDying && (fauna[i].transform.position - at).sqrMagnitude <= r2) return true;
            return false;
        }

        // ------------------------------------------------------------------ hostile prisms (RT, Space)

        bool IsHostile(Prism p) => p && !p.destroyed && p.Domain != _status.Domain && !IsOwn(p);

        void Break(Prism p, Vector3 from)
        {
            var dir = p.transform.position - from;
            if (dir.sqrMagnitude < 1e-4f) dir = _vessel.Transform.forward;
            p.Damage(dir.normalized * Mathf.Max(1f, _status.Speed), _status.Domain, _status.PlayerName);
        }

        /// <summary>Shatters every hostile prism within r of `at`; returns how many.</summary>
        int Shatter(Vector3 at, float r)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || r <= 0f) return 0;
            index.QuerySphere(at, r, _hits);
            int n = 0;
            foreach (var p in _hits)
            {
                if (!IsHostile(p)) continue;
                Break(p, at);
                n++;
            }
            return n;
        }

        // Genesis Lance: every hostile prism within lanceWidth of the line `length` ahead.
        void ShatterLine(Vector3 from, Vector3 fwd, float length)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(from + fwd * (length * 0.5f), length * 0.5f + _cfg.LanceWidth, _hits);
            foreach (var p in _hits)
            {
                if (!IsHostile(p)) continue;
                var d = p.transform.position - from;
                float along = Vector3.Dot(d, fwd);
                if (along < 0f || along > length || (d - fwd * along).sqrMagnitude > _cfg.LanceWidth * _cfg.LanceWidth) continue;
                Break(p, from);
            }
        }

        /// <summary>The nearest hostile prism within `reach` and `cone` degrees of the nose, farther than `minD`; a prism of
        /// colour `notColour` is skipped (-1: none).</summary>
        Prism HostileAhead(Vector3 pos, Vector3 fwd, float reach, float cone, float minD, int notColour)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || reach <= 0f) return null;
            index.QuerySphere(pos, reach, _hits);
            float c = Mathf.Cos(cone * Mathf.Deg2Rad), bd = float.MaxValue;
            Prism best = null;
            foreach (var p in _hits)
            {
                if (!IsHostile(p) || HoneyguideConfigSO.ColourOf(p.Domain) == notColour) continue;
                var d = p.transform.position - pos;
                float m = d.magnitude;
                if (m > minD && m <= reach && Vector3.Dot(d / m, fwd) >= c && m < bd) { bd = m; best = p; }
            }
            return best;
        }

        int CountHostile(Vector3 at, float r)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return 0;
            index.QuerySphere(at, r, _hits);
            int n = 0;
            foreach (var p in _hits) if (IsHostile(p)) n++;
            return n;
        }

        int CountHostileInCone(Vector3 pos, Vector3 fwd, float reach, float cone)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return 0;
            index.QuerySphere(pos, reach, _hits);
            float c = Mathf.Cos(cone * Mathf.Deg2Rad);
            int n = 0;
            foreach (var p in _hits)
            {
                if (!IsHostile(p)) continue;
                var d = p.transform.position - pos;
                float m = d.magnitude;
                if (m > 4f && m <= reach && Vector3.Dot(d / m, fwd) >= c) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ the AI's triggers (input-only)

        // The AI pulls a trigger when its current ability is wanted, holds it aiPreviewSeconds (the preview a person sees)
        // and lets go.
        void StepTriggerAI()
        {
            float now = Time.time;
            for (int k = 0; k < 2; k++)
                if (_aiWant[k] && now >= _aiLetGoAt[k]) _aiWant[k] = false;
            if (now < _aiTriggerAt) return;
            _aiTriggerAt = now + _cfg.AITriggerThink;
            var t = _vessel.Transform;
            for (int k = 0; k < 2; k++)
            {
                if (_aiWant[k] || _pv[k] || _trigCd[k] > 0f) continue;
                if (!Wanted(k, t.position, t.forward)) continue;
                _aiWant[k] = true;
                _aiLetGoAt[k] = now + _cfg.AIPreviewSeconds;
            }
        }

        // The lab's wanted8: is now a good moment for trigger k's current ability? Freestyle has no destination, so where
        // the lab weighs the goal this weighs ahead.
        bool Wanted(int k, Vector3 pos, Vector3 fwd)
        {
            var b = Current(k);
            if (b == null)
                return k == 0 ? PeckTargetAhead(pos, fwd)
                    : true;   // Flit: freestyle has no goal, so ahead is closer (the lab flits when the goal is ahead)
            float reach = b.Def.reach;
            switch (b.Effect)
            {
                case HoneyguideEffect.Lasso: return StrikeTargetAhead(pos, fwd, reach, _cfg.LassoCone, out _);
                case HoneyguideEffect.Harmonizer: return CountHostileInCone(pos, fwd, reach, _cfg.HarmonizerCone) >= 3;
                case HoneyguideEffect.Fugacity: return CountHostile(pos, reach) >= 3;
                case HoneyguideEffect.Lance: return NewestColour() != 0 || HostileAhead(pos, fwd, reach, 6f, 0f, -1);
                case HoneyguideEffect.LittleBang: return CountHostile(pos + fwd * _cfg.GrenadeThrow, reach) >= 2;
                case HoneyguideEffect.Eyelet:
                case HoneyguideEffect.Thruster: return true;
                case HoneyguideEffect.Sepia: return _droneLeft <= 0f && CountHostile(pos, reach) >= 1;
                case HoneyguideEffect.Monte:
                {
                    if (MonteHopList(reach, _hops) == 0) return false;
                    if (Vector3.Dot(_hops[^1] - pos, fwd) > 20f) return true;
                    foreach (var h in _hops) if (StrikeNear(h, _cfg.StrikeRadius)) return true;
                    return false;
                }
                case HoneyguideEffect.Blink:
                case HoneyguideEffect.Portals:
                case HoneyguideEffect.Rosen:
                case HoneyguideEffect.Ftl:
                    return JumpLanding(b.Effect, reach, out var at, out _)
                           && (Vector3.Dot(at - pos, fwd) > 20f || StrikeNear(at, _cfg.StrikeRadius));
            }
            return false;
        }

        // RT Peck at a hostile prism dead ahead - never one of a domain the battery holds (those are slip lanes).
        bool PeckTargetAhead(Vector3 pos, Vector3 fwd)
        {
            var p = HostileAhead(pos, fwd, _cfg.PeckRange, 8f, 0f, -1);
            return p && !_core.Battery.Contains(HoneyguideConfigSO.ColourOf(p.Domain));
        }
    }
}
