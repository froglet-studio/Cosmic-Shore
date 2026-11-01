using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide's half of the crew: the Slip, the pattern effects (the lab's <c>applyEffect</c>, <c>contacts</c>,
    /// <c>slip</c>, <c>jumpLanding</c> and <c>stepPassives</c>) and boost; the triggers and their upgrades are
    /// <c>HoneyguideCrew.Triggers.cs</c>. Everything the lab does on a flat disc is done here in the hull's own 3D space,
    /// through <see cref="PrismSpatialIndex"/> (never physics) and <see cref="IVessel.SetPose"/> for the jumps; a slip is a
    /// ride along the trail (<c>HoneyguideCrew.Zip.cs</c>).
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        /// <summary>A running timed effect: its level and the seconds it has left.</summary>
        internal struct Passive
        {
            public HoneyguideEffect Effect;
            public int Level;
            public float Left;
        }

        // what each timed effect adds a level (the lab's EFFECTS[k].mech); lvl(k) is the sum over every running effect
        enum Mech { Plow = 0, Magnet = 1, Skip = 2, Agile = 3, Watt = 4, Gravity = 5, Void = 6, Wake = 7, Count = 8 }

        internal readonly List<Passive> Passives = new();
        readonly int[] _mech = new int[(int)Mech.Count];
        float _boostE;
        bool _boosting;
        int _sail, _recolour, _noodles;
        Domains? _lastSkip;
        Trail _ignoreTrail;
        float _ignoreT;
        int _lastMade = -1, _lastRetired = -1;
        float _baseYaw, _basePitch;
        bool _turnScaled;
        readonly List<Prism> _query = new();
        readonly List<SlipVec> _pts = new();

        /// <summary>Seconds of boost the pattern effects have given and the hull has not yet spent.</summary>
        public float BoostSeconds => _boostE;

        int Lvl(Mech m) => _mech[(int)m];

        void ResetPilotSide()
        {
            Passives.Clear();
            System.Array.Clear(_mech, 0, _mech.Length);
            _boostE = 0f;
            _boosting = false;
            _sail = _recolour = _noodles = 0;
            _lastSkip = null;
            _ignoreTrail = null;
            _ignoreT = 0f;
            _lastMade = _lastRetired = -1;
            _turnScaled = false;
            ResetTriggers();
            ResetMassHive();
        }

        void EndPilotSide()
        {
            if (_boosting && _status != null) _status.IsBoosting = false;
            _boosting = false;
            EndTriggers();
            DestroyPreviews();
            DestroyMassHive();
            EndZip(false);
            RestoreTurn();
        }

        void StepPilotSide(float dt)
        {
            StepPassives(dt);
            StepBoost(dt);
            bool riding = StepZip(dt);
            StepTriggers(dt, riding);   // the triggers are read on the rail too (a pull previews; a release ends the ride)
            StepMassHive(dt);
            if (riding) return;   // on the rail: no contacts until it lands
            StepWatt();
            Contacts(dt);
        }

        // ------------------------------------------------------------------ timed effects

        void StepPassives(float dt)
        {
            bool gone = false;
            for (int i = Passives.Count - 1; i >= 0; i--)
            {
                var p = Passives[i];
                p.Left -= dt;
                if (p.Left <= 0f) { Passives.RemoveAt(i); gone = true; }
                else Passives[i] = p;
            }
            if (gone) RecomputeMech();
        }

        void RecomputeMech()
        {
            System.Array.Clear(_mech, 0, _mech.Length);
            foreach (var p in Passives)
                switch (p.Effect)
                {
                    case HoneyguideEffect.Plow: _mech[(int)Mech.Plow] += p.Level; break;
                    case HoneyguideEffect.Dock: _mech[(int)Mech.Magnet] += 2 * p.Level; break;
                    case HoneyguideEffect.Skipper: _mech[(int)Mech.Skip] += p.Level; break;
                    case HoneyguideEffect.Eigen: _mech[(int)Mech.Agile] += 2 * p.Level; break;
                    case HoneyguideEffect.Watt: _mech[(int)Mech.Watt] += p.Level; break;
                    case HoneyguideEffect.Gravity: _mech[(int)Mech.Magnet] += p.Level; _mech[(int)Mech.Gravity] += p.Level; break;
                    case HoneyguideEffect.VoidJumper: _mech[(int)Mech.Void] += p.Level; break;
                    case HoneyguideEffect.Frame: _mech[(int)Mech.Wake] += p.Level; break;
                }
            ApplyTurn();
        }

        static bool IsTimed(HoneyguideEffect e) => e is HoneyguideEffect.Plow or HoneyguideEffect.Dock or HoneyguideEffect.Skipper
            or HoneyguideEffect.Eigen or HoneyguideEffect.Watt or HoneyguideEffect.Gravity or HoneyguideEffect.VoidJumper
            or HoneyguideEffect.Frame;

        // Fractal Frame: while it runs, the trail the hull lays is the newest charge's domain - a slip lane behind it
        // (the lab's 'wake'). The prism controller's own spawn event, so every lay rule (gaps, warp, mass) stays its own.
        void OnTrailPrismLaid(Prism prism)
        {
            if (Lvl(Mech.Wake) == 0 || !prism) return;
            int newest = NewestColour();
            if (newest != 0) prism.ChangeTeam(HoneyguideConfigSO.DomainOf(newest));
        }

        // Eigen Booster: the hull turns faster a level. The transformer's own turn scalers, scaled while it runs and put back
        // the moment it ends (or the seat unbinds).
        void ApplyTurn()
        {
            var t = _status?.VesselTransformer;
            if (!t) return;
            int agile = Lvl(Mech.Agile);
            if (agile == 0) { RestoreTurn(); return; }
            if (!_turnScaled) { _baseYaw = t.YawScaler; _basePitch = t.PitchScaler; _turnScaled = true; }
            float k = 1f + _cfg.EigenTurnPerUnit * agile;
            t.YawScaler = _baseYaw * k;
            t.PitchScaler = _basePitch * k;
        }

        void RestoreTurn()
        {
            if (!_turnScaled) return;
            _turnScaled = false;
            var t = _status?.VesselTransformer;
            if (!t) return;
            t.YawScaler = _baseYaw;
            t.PitchScaler = _basePitch;
        }

        // ------------------------------------------------------------------ boost

        void GiveBoost(float seconds) => _boostE = Mathf.Min(_cfg.BoostCap, _boostE + Mathf.Max(0f, seconds));

        // The effects' boost runs at once (the hull's own boostMultiplier applies), the way the Wrasse's AI spends a saved
        // boost. The lab spends it while the pilot holds boost; the Honeyguide has no boost button of its own.
        void StepBoost(float dt)
        {
            if (_boostE > 0f)
            {
                _boostE = Mathf.Max(0f, _boostE - dt);
                _status.IsBoosting = true;
                _boosting = true;
                return;
            }
            if (_boosting) { _status.IsBoosting = false; _boosting = false; }
        }

        // Wattslider: alongside a trail (a prism within wattReach, the heading within wattCone of its axis), faster a level.
        void StepWatt()
        {
            int lv = Lvl(Mech.Watt);
            if (lv == 0) return;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            var t = _vessel.Transform;
            index.QuerySphere(t.position, _cfg.WattReach, _query);
            float cos = Mathf.Cos(_cfg.WattCone * Mathf.Deg2Rad);
            foreach (var p in _query)
            {
                if (!p || p.destroyed || p.Trail == null || IsOwn(p)) continue;
                if (Mathf.Abs(Vector3.Dot(t.forward, p.transform.forward)) < cos) continue;
                _vessel.ModifyThrottle(1f + _cfg.WattSpeedPerLevel * lv, Time.deltaTime * 1.5f);
                return;
            }
        }

        bool IsOwn(Prism p) => _status != null && p.ownerID == _status.PlayerName;

        // ------------------------------------------------------------------ contacts: the Slip and hostile prisms

        // The colours a slip can ride now, as a bit mask over colours 1-3: those the battery holds, or any while Void Jumper
        // runs (with a charge to spend) or a Plankter Sail is set.
        int SlipColours()
        {
            if ((Lvl(Mech.Void) > 0 && _core.Battery.Count > 0) || _sail > 0) return 0b1110;
            int mask = 0;
            foreach (int c in _core.Battery) mask |= 1 << c;
            return mask;
        }

        int NewestColour() => _core.Battery.Count > 0 ? _core.Battery[_core.Battery.Count - 1] : 0;

        void Contacts(float dt)
        {
            if (_ignoreT > 0f) _ignoreT -= dt;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            var t = _vessel.Transform;
            Vector3 pos = t.position, fwd = t.forward;
            float rr = _cfg.ContactRadius;
            float catchR = Mathf.Max(rr + _cfg.MagnetPerLevel * Lvl(Mech.Magnet), _noodles > 0 ? rr + _cfg.NoodleReach : 0f);
            index.QuerySphere(pos, catchR, _query);

            int doms = SlipColours(), newest = NewestColour();
            var own = _status.Domain;
            float cos = Mathf.Cos(_cfg.AimCone * Mathf.Deg2Rad), now = Time.time;
            Prism slipPrism = null;
            int slipColour = 0;
            foreach (var p in _query)
            {
                if (!p || p.destroyed) continue;
                if (p.prismProperties != null && now - p.prismProperties.TimeCreated < _cfg.SelfGrace) continue;   // SELF_TRAIL_CONTACT
                var trail = p.Trail;
                if (trail != null && trail == _ignoreTrail && _ignoreT > 0f) continue;
                var dom = p.Domain;
                int c = HoneyguideConfigSO.ColourOf(dom);
                if (!slipPrism && trail != null && trail.TrailList.Count >= _cfg.MinSlipTrailLength && c != 0 && (doms & (1 << c)) != 0
                    && PrismscapeTopology.DimensionOf(p) == PrismscapeDimension.Trail
                    && Mathf.Abs(Vector3.Dot(fwd, p.transform.forward)) >= cos)
                {
                    slipPrism = p;
                    slipColour = c;
                }
                bool touch = (p.transform.position - pos).sqrMagnitude <= rr * rr, mine = IsOwn(p);
                if (touch && !mine && Lvl(Mech.Skip) > 0 && dom != _lastSkip)   // Anomaly Skipper
                {
                    if (_lastSkip.HasValue) GiveBoost(_cfg.SkipperBoostPerLevel * Lvl(Mech.Skip));
                    _lastSkip = dom;
                }
                if (dom == own || mine) continue;
                if (_noodles > 0 && !(slipPrism && slipPrism.Trail == trail))   // Noodley Appendages: push it off the nose
                {
                    var off = Vector3.ProjectOnPlane(p.transform.position - pos, fwd);
                    if (off.sqrMagnitude < 1e-4f) off = t.right;
                    p.transform.position += off.normalized * _cfg.NoodlePush;
                    p.NotifyPositionChanged();
                    _noodles--;
                    continue;
                }
                if (!touch) continue;
                if (_recolour > 0 && newest != 0 && c != newest)   // Reality Detangler
                {
                    p.ChangeTeam(HoneyguideConfigSO.DomainOf(newest));
                    _recolour--;
                    continue;
                }
                if (Lvl(Mech.Plow) > 0)   // Juggernaut Plow: it breaks before the hull meets it
                    p.Damage(fwd * Mathf.Max(1f, _status.Speed), own, _status.PlayerName);
            }
            if (slipPrism) Slip(slipPrism, slipColour);
        }

        void Slip(Prism contact, int colour)
        {
            var trail = contact.Trail;
            _pts.Clear();
            int contactIndex = -1;
            foreach (var b in trail.TrailList)
            {
                if (!b || b.destroyed) continue;
                if (b == contact) contactIndex = _pts.Count;
                var p = b.transform.position;
                _pts.Add(new SlipVec(p.x, p.y, p.z));
            }
            if (contactIndex < 0 || _pts.Count < 2) return;

            var t = _vessel.Transform;
            Vector3 pos = t.position, fwd = t.forward;
            float reach = _cfg.SlipReach * (_sail > 0 && _core.Battery.Count == 0 ? 0.5f : 1f);
            var land = DynamoCore.SlipDestination(_pts, contactIndex, new SlipVec(pos.x, pos.y, pos.z), new SlipVec(fwd.x, fwd.y, fwd.z),
                                                  reach, _cfg.ExitOffset);
            if (land == null) return;

            bool paid = _core.Spend(colour);
            if (!paid && _sail > 0) { _sail--; GiveBoost(_cfg.SailBoost); paid = true; }   // Plankter Sail
            if (!paid && Lvl(Mech.Void) > 0 && _core.Battery.Count > 0) { _core.Battery.RemoveAt(0); paid = true; }   // Void Jumper: the oldest charge
            if (!paid) return;
            if (Lvl(Mech.Gravity) > 0) GiveBoost(_cfg.GravityBoostPerLevel * Lvl(Mech.Gravity));

            var l = land.Value;
            var heading = _cfg.ExitAligned ? new Vector3((float)l.Heading.X, (float)l.Heading.Y, (float)l.Heading.Z) : fwd;
            if (heading.sqrMagnitude < 1e-6f) heading = fwd;
            var point = new Vector3((float)l.Point.X, (float)l.Point.Y, (float)l.Point.Z);
            // Not a teleport: the hull zips along the trail to the landing (Garrett 2026-10-21: "a satisfying zip over so they
            // keep some continuity and are not denied the joy of fast travel"). HoneyguideCrew.Zip.cs.
            BeginZip(contactIndex, l.Index, point, heading);
            _ignoreTrail = trail;
            _ignoreT = _cfg.SlipGrace;
            Play(_cfg.SlipSfx);
        }

        // ------------------------------------------------------------------ pattern effects

        // A made pattern's effect. `colour` is the battery prism it took; depth stops Zero Point and Meta Machine looping.
        void ApplyEffect(int pid, int colour, int depth)
        {
            var pats = _cfg.Patterns;
            if (pid < 0 || pid >= pats.Count || pats[pid] == null) return;
            var pat = pats[pid];
            var key = pat.effect;
            int lv = 0;
            var upgrade = _cfg.UpgradeFor(key);
            if (upgrade != null)   // a trigger upgrade: it banks usesPerCard uses on RT or LT (lab round 8)
            {
                int uses = Bank(key, upgrade, pat.name);
                if (depth == 0) SetMadeLine(pat, "");
                if (depth == 0)
                    Toast(TemplateText.Fill(_cfg.UpgradeToastTemplate, "name", pat.name, "trigger", _cfg.TriggerName(upgrade.trigger),
                                            "uses", uses.ToString(), "text", pat.effectText));
                return;
            }
            if (IsTimed(key))
            {
                int i = Passives.FindIndex(p => p.Effect == key);
                int level = Mathf.Min(_cfg.PassiveMaxLevel, (i >= 0 ? Passives[i].Level : 0) + 1);
                var next = new Passive { Effect = key, Level = level, Left = _cfg.PassiveSeconds };
                if (i >= 0) Passives[i] = next; else Passives.Add(next);
                lv = level;
                RecomputeMech();
            }
            switch (key)
            {
                case HoneyguideEffect.Plow: GiveBoost(_cfg.PlowBoost); break;
                case HoneyguideEffect.Amanita: _core.BoardEffect(_s, DynamoBoardEffect.Burn, 1); GiveBoost(_cfg.AmanitaBoost); break;
                case HoneyguideEffect.Plankter: _sail = Mathf.Min(_cfg.MaxSails, _sail + 1); break;
                case HoneyguideEffect.BigRed: GiveBoost(_cfg.BigRedBoostEach * _core.BoardEffect(_s, DynamoBoardEffect.Purge, colour)); break;
                case HoneyguideEffect.Saddle: GiveBoost(_cfg.SaddleBoostEach * _core.BoardEffect(_s, DynamoBoardEffect.Drain, 0)); break;
                case HoneyguideEffect.Detangler: _recolour += _cfg.DetangleCount; break;
                case HoneyguideEffect.Condenser: LayLane(NewestColour(), _cfg.CondenserAhead, _cfg.CondenserCount); break;
                case HoneyguideEffect.ZeroPoint:
                {
                    var others = new List<int>();
                    foreach (int p in _core.Slots)
                        if (p >= 0 && p < pats.Count && pats[p].effect != HoneyguideEffect.ZeroPoint && pats[p].effect != HoneyguideEffect.Meta) others.Add(p);
                    if (depth < 1 && others.Count > 0) ApplyEffect(others[(int)(_aiDice() * others.Count)], colour, depth + 1);
                    GiveBoost(_cfg.ZeroPointBoost * _core.BoardEffect(_s, DynamoBoardEffect.DrainOne, 0));
                    break;
                }
                case HoneyguideEffect.Noodley: _noodles += _cfg.NoodleCount; break;
                case HoneyguideEffect.Triplesynth: _core.BoardEffect(_s, DynamoBoardEffect.Trio, 0); break;
                case HoneyguideEffect.Translator: _core.BoardEffect(_s, DynamoBoardEffect.Free, 1); break;
                case HoneyguideEffect.Anode: for (int c = 1; c <= 3; c++) _core.BoardEffect(_s, DynamoBoardEffect.Charge, c); break;
                case HoneyguideEffect.Printer5D: _core.BoardEffect(_s, DynamoBoardEffect.Free, 2); break;
                case HoneyguideEffect.Hammer: _core.BoardEffect(_s, DynamoBoardEffect.Scatter, _cfg.HammerCount); break;
                case HoneyguideEffect.Cilia: _core.BoardEffect(_s, DynamoBoardEffect.Bonus, _cfg.CiliaBonus); break;
                case HoneyguideEffect.Chrono:
                    _core.BoardEffect(_s, DynamoBoardEffect.Bonus, 1);
                    _core.BoardEffect(_s, DynamoBoardEffect.Rewind, Mathf.RoundToInt(_cfg.ChronoRewind));
                    break;
                case HoneyguideEffect.Meta:
                {
                    int back = _lastRetired >= 0 && _lastRetired < pats.Count && pats[_lastRetired].effect != HoneyguideEffect.Meta ? _lastRetired
                        : _lastMade >= 0 && _lastMade != pid ? _lastMade : -1;
                    if (depth < 1 && back >= 0 && pats[back].effect != HoneyguideEffect.ZeroPoint) ApplyEffect(back, colour, depth + 1);
                    break;
                }
                case HoneyguideEffect.Eti: ChargeNearbyDomains(); break;
                case HoneyguideEffect.Vorticity: _core.BoardEffect(_s, DynamoBoardEffect.Unspin, 0); break;
            }
            if (depth == 0) SetMadeLine(pat, lv > 0 ? Roman(lv) : "");
            if (depth == 0)
                Toast(TemplateText.Fill(_cfg.MadeToastTemplate, "name", pat.name, "level", lv > 0 ? Roman(lv) : "", "text", pat.effectText));
        }

        // The game's toast (top right, under Settings: ToastNotificationAPI), for the seat on this screen. An AI crew is silent.
        void Toast(string line)
        {
            if (_aiCrew || string.IsNullOrEmpty(line)) return;
            ToastNotificationAPI.Show(line);
        }

        internal static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };

        // Mystic Eti: a charge for each domain with a trail within etiReach of the hull.
        void ChargeNearbyDomains()
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(_vessel.Transform.position, _cfg.EtiReach, _query);
            int seen = 0;
            foreach (var p in _query)
                if (p && !p.destroyed && p.Trail != null && !IsOwn(p)) seen |= 1 << HoneyguideConfigSO.ColourOf(p.Domain);
            for (int c = 1; c <= 3; c++) if ((seen & (1 << c)) != 0) _core.BoardEffect(_s, DynamoBoardEffect.Charge, c);
        }

        // Epitaxial Condenser (and the Genesis Lance's lane): `count` prisms of the newest charge's domain laid from `ahead` on,
        // condenserSpacing apart - a 1-D trail of its own, so it is a slip lane.
        void LayLane(int colour, float ahead, int count)
        {
            var spawner = _status.VesselPrismController;
            var channel = spawner ? spawner.PrismSpawnChannel : null;
            if (!channel) return;
            var domain = colour != 0 ? HoneyguideConfigSO.DomainOf(colour) : _status.Domain;
            var t = _vessel.Transform;
            var rot = Quaternion.LookRotation(t.forward, t.up);
            var scale = spawner.TrailBaseScale;
            var lane = new Trail();
            for (int k = 0; k < count; k++)
            {
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = domain,
                    Rotation = rot,
                    SpawnPosition = t.position + t.forward * (ahead + k * _cfg.CondenserSpacing),
                    Scale = scale,
                    PrismType = spawner.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;
                prism.TargetScale = scale;
                prism.ownerID = _status.PlayerName;
                prism.ChangeTeam(domain);
                prism.Initialize(_status.PlayerName);
                prism.AssignTrail(lane);
                lane.Add(prism);
            }
        }

        // ------------------------------------------------------------------ the jump upgrades (LT)

        // Blink Drive, All Terrain Portals, Rosen Bridge, FTL Chromatool and Monte Cannon: a jump to where JumpLanding puts
        // the hull. False (and nothing happens) when it finds no target.
        bool DoJump(HoneyguideEffect kind, float reach)
        {
            if (!JumpLanding(kind, reach, out var at, out var taken)) return false;
            if (taken)   // FTL Chromatool: the prism swapped with becomes a charge of its domain
            {
                int c = HoneyguideConfigSO.ColourOf(taken.Domain);
                if (c != 0) _core.BoardEffect(_s, DynamoBoardEffect.Charge, c);
                taken.Vanish(_status.PlayerName);
            }
            _vessel.SetPose(new Pose(at, _vessel.Transform.rotation));
            Play(_cfg.JumpSfx);
            return true;
        }

        // Where a jump upgrade lands (the lab's jumpLanding, in the hull's 3D space); `reach` is the upgrade's at its level.
        bool JumpLanding(HoneyguideEffect kind, float reach, out Vector3 at, out Prism taken)
        {
            taken = null;
            var t = _vessel.Transform;
            Vector3 pos = t.position, fwd = t.forward;
            at = pos;
            var index = PrismSpatialIndex.Instance;
            bool indexed = index && index.IsAvailable;
            float cone = Mathf.Cos(_cfg.JumpCone * Mathf.Deg2Rad);
            switch (kind)
            {
                case HoneyguideEffect.Blink:
                    at = pos + fwd * reach;
                    return true;
                case HoneyguideEffect.Portals:
                    at = pos + fwd * reach;
                    if (!indexed || index.QuerySphere(at, _cfg.PortalsEmptyRadius, _query) == 0) at += fwd * reach;
                    return true;
                case HoneyguideEffect.Rosen:
                {
                    int newest = NewestColour();
                    if (newest == 0 || !indexed) return false;
                    var dom = HoneyguideConfigSO.DomainOf(newest);
                    index.QuerySphere(pos, reach, _query);
                    float best = -1f;
                    foreach (var p in _query)
                    {
                        if (!p || p.destroyed || p.Domain != dom || IsOwn(p)) continue;
                        float d = Ahead(p.transform.position, pos, fwd, reach, cone);
                        if (d > best) { best = d; at = p.transform.position; }
                    }
                    return best > 0f;
                }
                case HoneyguideEffect.Ftl:
                {
                    if (!indexed) return false;
                    index.QuerySphere(pos, reach, _query);
                    float best = float.MaxValue;
                    foreach (var p in _query)
                    {
                        if (!p || p.destroyed || IsOwn(p)) continue;
                        float d = Ahead(p.transform.position, pos, fwd, reach, cone);
                        if (d > 0f && d < best) { best = d; taken = p; }
                    }
                    if (!taken) return false;
                    at = taken.transform.position;
                    return true;
                }
                case HoneyguideEffect.Monte:
                {
                    if (MonteHopList(reach, _hops) == 0) return false;
                    at = _hops[^1];
                    return true;
                }
            }
            return false;
        }

        /// <summary>Monte Cannon's hops into `into` (cleared first): each to the farthest prism ahead within `reach` of the
        /// last (the lab's monteHops). Returns how many.</summary>
        int MonteHopList(float reach, List<Vector3> into)
        {
            into.Clear();
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return 0;
            var t = _vessel.Transform;
            Vector3 from = t.position, fwd = t.forward;
            float cone = Mathf.Cos(_cfg.JumpCone * Mathf.Deg2Rad);
            for (int k = 0; k < _cfg.MonteHops; k++)
            {
                index.QuerySphere(from, reach, _query);
                float best = -1f;
                var next = from;
                foreach (var p in _query)
                {
                    if (!p || p.destroyed || IsOwn(p)) continue;
                    float d = Ahead(p.transform.position, from, fwd, reach, cone);
                    if (d > best) { best = d; next = p.transform.position; }
                }
                if (best <= 0f) break;
                from = next;
                into.Add(from);
            }
            return into.Count;
        }

        // the distance to `p` when it lies ahead of `from` within the cone and between jumpMinDistance and maxD, else -1
        float Ahead(Vector3 p, Vector3 from, Vector3 fwd, float maxD, float cosCone)
        {
            var d = p - from;
            float m = d.magnitude;
            return m > _cfg.JumpMinDistance && m <= maxD && Vector3.Dot(d / m, fwd) >= cosCone ? m : -1f;
        }
    }
}
