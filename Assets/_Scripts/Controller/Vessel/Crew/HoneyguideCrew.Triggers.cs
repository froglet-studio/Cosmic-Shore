using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide's two triggers (Honeyguide Lab round 6; Garrett 2026-10-21: "the right trigger should always be a weak
    /// attack power that gets upgraded sometimes. the left should be a movement power that works similar"). RT is <b>Peck</b>,
    /// a weak shot while held through the hull's own gun (<c>HoneyguidePeckAction.asset</c>); LT is <b>Flit</b>, a short throw
    /// ahead. Thirteen of the Ratel's cards are <b>upgrades</b> (the lab's <c>ACTIVES</c>): made, one takes its trigger over
    /// for <c>activeSeconds</c>, then the base power comes back. The same upgrade again refreshes it and raises its level (its
    /// reach grows by <c>activeLevelGain</c>); another replaces it. One button each, no combos: a press, a hold, or a hold to
    /// charge and a release.
    ///
    /// <para>The human's presses come from <see cref="HoneyguideTriggerActionSO"/> (the prefab maps RightStickAction and
    /// LeftStickAction to it). While an AI flies the hull - an AI crew, or the human playing the Ratel - this reads the moment
    /// itself, input-only: it answers with a trigger, never an effect (the lab's <c>activeWanted</c>).</para>
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        /// <summary>An upgrade on a trigger: its card, level, the seconds it has left, its cooldown and its charge.</summary>
        internal sealed class TriggerUpgrade
        {
            public HoneyguideUpgrade Def;
            public HoneyguideEffect Effect;
            public string Name;
            public int Level;
            public float Left, Cooldown, Charge;
        }

        struct Bomb
        {
            public Vector3 At;
            public float Left, Radius;
        }

        /// <summary>The upgrade on RT (0) and LT (1), or null while the base power is theirs.</summary>
        internal readonly TriggerUpgrade[] Upgrades = new TriggerUpgrade[2];
        readonly bool[] _btn = new bool[2], _btnWas = new bool[2], _aiWant = new bool[2];
        readonly List<Bomb> _bombs = new();
        readonly List<Prism> _hits = new();
        ActionExecutorRegistry _execs;
        bool _peckOn, _harmOn;
        float _flitCd, _aiTriggerAt, _droneLeft, _droneCd, _droneR, _harmAcc, _harmMul = 1f;

        bool AIFlies => _aiCrew || _humanEngineers;

        /// <summary>The human pilot's trigger k (0 RT, 1 LT) went down or up. <see cref="HoneyguideTriggerActionSO"/> calls it.</summary>
        public void SetTrigger(int k, bool down)
        {
            if (k < 0 || k >= _btn.Length || _core == null || AIFlies) return;
            _btn[k] = down;
        }

        void ResetTriggers()
        {
            SetPeck(false);
            for (int k = 0; k < 2; k++) { Upgrades[k] = null; _btn[k] = _btnWas[k] = _aiWant[k] = false; }
            _bombs.Clear();
            _flitCd = _aiTriggerAt = _droneLeft = _droneCd = _harmAcc = 0f;
            _harmOn = false;
        }

        void EndTriggers()
        {
            SetPeck(false);
            for (int k = 0; k < 2; k++) _btn[k] = _btnWas[k] = false;
        }

        float Mul(TriggerUpgrade u) => 1f + _cfg.ActiveLevelGain * (u.Level - 1);

        // A card's upgrade takes its own trigger: the same one again refreshes it and raises its level; another replaces it.
        // Returns its level.
        int GrantUpgrade(HoneyguideEffect effect, HoneyguideUpgrade def, string name)
        {
            int k = Mathf.Clamp(def.trigger, 0, 1);
            var cur = Upgrades[k];
            if (cur != null && cur.Effect == effect)
            {
                cur.Level = Mathf.Min(_cfg.PassiveMaxLevel, cur.Level + 1);
                cur.Left = _cfg.ActiveSeconds;
            }
            else Upgrades[k] = new TriggerUpgrade { Def = def, Effect = effect, Name = name, Level = 1, Left = _cfg.ActiveSeconds };
            Play(_cfg.UpgradeSfx);
            return Upgrades[k].Level;
        }

        // ------------------------------------------------------------------ the buttons

        void StepTriggers(float dt, bool riding)
        {
            _flitCd -= dt;
            _harmOn = false;
            if (AIFlies) StepTriggerAI();
            for (int k = 0; k < 2; k++)
            {
                bool was = _btnWas[k], down = AIFlies ? AIPress(k, was) : _btn[k];
                _btnWas[k] = down;
                var u = Upgrades[k];
                if (u == null)   // the base power: RT Peck while held, LT Flit on a press
                {
                    if (k == 0) SetPeck(down);
                    else if (down && !was && _flitCd <= 0f && !riding)
                    {
                        _flitCd = _cfg.FlitCooldown;
                        BeginJet(_cfg.FlitDistance, _cfg.FlitSeconds, false);
                        Play(_cfg.FlitSfx);
                    }
                    continue;
                }
                if (k == 0) SetPeck(false);
                u.Left -= dt;
                u.Cooldown -= dt;
                switch (u.Def.use)
                {
                    case HoneyguideTriggerUse.Press:
                        if (down && !was && u.Cooldown <= 0f && !riding) Use(u);
                        break;
                    case HoneyguideTriggerUse.Hold:
                        if (down) { _harmOn = true; _harmMul = Mul(u); }
                        break;
                    case HoneyguideTriggerUse.Charge:
                        if (down && u.Cooldown <= 0f) u.Charge = Mathf.Min(_cfg.FugacityFullCharge, u.Charge + dt);
                        else if (!down && was && u.Charge > 0f) Use(u);
                        break;
                }
                if (u.Left > 0f) continue;
                if (u.Def.use == HoneyguideTriggerUse.Charge && u.Charge > 0f) Use(u);   // a charge held to the end goes off
                Upgrades[k] = null;
            }
            StepHarmonizer(dt);
            StepDrone(dt);
            StepBombs(dt);
        }

        // RT's base power through the hull's own gun: the action asset is shared by every Honeyguide, so the vessel's own
        // executors carry the state (FullAutoActionSO's contract).
        void SetPeck(bool on)
        {
            if (on == _peckOn) return;
            _peckOn = on;
            var action = _cfg ? _cfg.PeckAction : null;
            if (!action || _status == null) return;
            if (!_execs && _vessel != null && UnityLiveness.Alive(_vessel.Transform))
                _execs = _vessel.Transform.GetComponentInChildren<ActionExecutorRegistry>(true);
            if (!_execs) return;
            if (on) action.StartAction(_execs, _status);
            else action.StopAction(_execs, _status);
        }

        // One use of an upgrade: a press, or the release of a charge (the lab's fireActive).
        void Use(TriggerUpgrade u)
        {
            u.Cooldown = u.Def.cooldown;
            float mul = Mul(u), reach = u.Def.reach * mul;
            var t = _vessel.Transform;
            Vector3 pos = t.position, fwd = t.forward;
            switch (u.Effect)
            {
                case HoneyguideEffect.Lasso:   // Hard Light Lasso: hook the nearest hostile prism ahead and reel in
                {
                    var target = HostileAhead(pos, fwd, reach, _cfg.LassoCone, 6f, -1);
                    if (!target) { u.Cooldown = 0.25f; return; }
                    BeginReel(target.transform.position, _cfg.LassoBurst * mul);
                    break;
                }
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
                case HoneyguideEffect.Fugacity:   // Fugacity Discharger: the burst grows with the charge; boost for what it breaks
                {
                    float full = Mathf.Clamp01(u.Charge / Mathf.Max(0.01f, _cfg.FugacityFullCharge));
                    float r = (_cfg.FugacityMinRadius + (u.Def.reach - _cfg.FugacityMinRadius) * full) * mul;
                    int n = Shatter(pos, r);
                    GiveBoost(Mathf.Min(_cfg.FugacityBoostCap, _cfg.FugacityBoostEach * n));
                    u.Charge = 0f;
                    break;
                }
                case HoneyguideEffect.Eyelet: BeginJet(reach, _cfg.EyeletSeconds, true); break;
                case HoneyguideEffect.Thruster: BeginJet(reach, _cfg.ThrusterSeconds, true); break;
                case HoneyguideEffect.Blink:
                case HoneyguideEffect.Portals:
                case HoneyguideEffect.Rosen:
                case HoneyguideEffect.Ftl:
                case HoneyguideEffect.Monte:
                    if (!DoJump(u.Effect, reach)) { u.Cooldown = 0.25f; Toast(_cfg.NoJumpTarget); return; }
                    break;
            }
            Play(u.Def.sfx);
        }

        // ------------------------------------------------------------------ what a running upgrade does

        // Particle Harmonizer: while held, hostile prisms in the cone turn to the newest charge's domain - new slip lanes.
        void StepHarmonizer(float dt)
        {
            int newest = NewestColour();
            if (!_harmOn || newest == 0) { _harmAcc = 0f; return; }
            var u = Upgrades[0];
            float reach = (u != null && u.Effect == HoneyguideEffect.Harmonizer ? u.Def.reach : 0f) * _harmMul;
            var dom = HoneyguideConfigSO.DomainOf(newest);
            var t = _vessel.Transform;
            _harmAcc += _cfg.HarmonizerPerSecond * dt;
            while (_harmAcc >= 1f)
            {
                _harmAcc -= 1f;
                var p = HostileAhead(t.position, t.forward, reach, _cfg.HarmonizerCone, 4f, newest);
                if (!p) { _harmAcc = 0f; break; }
                p.ChangeTeam(dom);
            }
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

        // ------------------------------------------------------------------ hostile prisms

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

        // ------------------------------------------------------------------ the AI's triggers (input-only)

        // A press is a press: the AI lets go for a frame between uses of a press upgrade or a Flit.
        bool AIPress(int k, bool was)
        {
            bool want = _aiWant[k];
            var u = Upgrades[k];
            bool press = u != null ? u.Def.use == HoneyguideTriggerUse.Press : k == 1;
            return press ? want && !was : want;
        }

        void StepTriggerAI()
        {
            if (Time.time < _aiTriggerAt) return;
            _aiTriggerAt = Time.time + _cfg.AITriggerThink;
            var t = _vessel.Transform;
            for (int k = 0; k < 2; k++)
            {
                var u = Upgrades[k];
                _aiWant[k] = u != null ? UpgradeWanted(u, t.position, t.forward)
                    : k == 0 ? PeckTargetAhead(t.position, t.forward)
                    : _flitCd <= 0f;   // freestyle has no goal, so ahead is closer: the lab flits when the goal is ahead
            }
        }

        // RT Peck at a hostile prism dead ahead - never one of a domain the battery holds (those are slip lanes).
        bool PeckTargetAhead(Vector3 pos, Vector3 fwd)
        {
            var p = HostileAhead(pos, fwd, _cfg.PeckRange, 8f, 0f, -1);
            return p && !_core.Battery.Contains(HoneyguideConfigSO.ColourOf(p.Domain));
        }

        // The lab's activeWanted: is now a good moment for this upgrade?
        bool UpgradeWanted(TriggerUpgrade u, Vector3 pos, Vector3 fwd)
        {
            float mul = Mul(u), reach = u.Def.reach * mul;
            if (u.Left < 2f && u.Def.use == HoneyguideTriggerUse.Press && u.Def.trigger == 0 && u.Effect != HoneyguideEffect.Lance)
                return true;   // about to go: use it rather than lose it
            switch (u.Effect)
            {
                case HoneyguideEffect.Lasso:
                {
                    var p = HostileAhead(pos, fwd, reach, _cfg.LassoCone, 6f, -1);
                    return p && (p.transform.position - pos).sqrMagnitude >= 25f * 25f;
                }
                case HoneyguideEffect.Lance: return NewestColour() != 0;
                case HoneyguideEffect.LittleBang: return CountHostile(pos + fwd * _cfg.GrenadeThrow, reach) >= 2;
                case HoneyguideEffect.Eyelet:
                case HoneyguideEffect.Thruster: return true;
                case HoneyguideEffect.Blink:
                case HoneyguideEffect.Portals:
                case HoneyguideEffect.Rosen:
                case HoneyguideEffect.Ftl:
                case HoneyguideEffect.Monte:
                    return JumpLanding(u.Effect, reach, out var at, out _) && Vector3.Dot(at - pos, fwd) > 20f;
                case HoneyguideEffect.Sepia: return _droneLeft <= 0f && CountHostile(pos, reach) >= 1;
                case HoneyguideEffect.Harmonizer:
                {
                    int newest = NewestColour();
                    if (newest == 0) return false;
                    var p = HostileAhead(pos, fwd, reach, _cfg.HarmonizerCone, 4f, newest);
                    return p && p.Trail != null;
                }
                case HoneyguideEffect.Fugacity:
                    return u.Charge > 0f ? u.Charge < _cfg.FugacityFullCharge - 0.2f : CountHostile(pos, 30f * mul) >= 3;
            }
            return false;
        }
    }
}
