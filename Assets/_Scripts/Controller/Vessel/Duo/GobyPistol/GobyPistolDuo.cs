using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Goby &amp; Pistol duo (VesselClassType.Goby): the hull you fly is the GOBY (a two-thumb flier on the Squirrel's
    /// numbers, the Squirrel's own abilities); its path is unrolled into the PISTOL's 2D world (<see cref="PistolRibbon"/>)
    /// and the Pistol rides the ground its trail's hinges make, seen side on through a second camera. The Pistol's body,
    /// its launch, ghost and Snap (the vortex ring) act on the scene's REAL prisms; the Goby's primer and flares do too.
    /// One human flies one half and the AI flies the other; the swap is the arena's gesture (D-pad left / right, keyboard
    /// 1 / 2, <see cref="PilotSwapGesture"/>). The design: <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c>.
    ///
    /// <para>It lives on <c>Goby.prefab</c> and wakes only on the LOCAL pilot's hull while a human flies it (not on an AI
    /// or remote copy, not while the menu's autopilot flies it): the Pistol is simulated on that one machine, so its
    /// breaks are local until the duo is networked (§8 of the brief). <c>FrogletTools ▸ Vessels ▸ Goby and Pistol (Play
    /// mode)</c> adds the same component to any vessel you are flying.</para>
    ///
    /// <para>Everything it computes is the lab's arithmetic, ported line for line and held to the lab's numbers by
    /// <c>GobyPistolMathTests</c>; every number is <see cref="GobyPistolConfigSO"/>, cloned from the Vessel Studio artifact
    /// (<c>Docs/Studios/VesselStudio/goby.html</c>). This file is only the glue to the scene: which prisms, which camera,
    /// which buttons.</para>
    ///
    /// <para>Prisms are moved the sanctioned way (transform, then <see cref="Prism.NotifyPositionChanged"/>; never a
    /// super-shielded or growing one) and broken only by an active force (<see cref="Prism.Damage"/>, the game's own
    /// explosion), so mass is conserved and nothing pops. The side view draws each prism's own mesh and material at its
    /// unrolled place, turned by the ribbon there: the Pistol's world, not a copy of it.</para>
    /// </summary>
    public sealed class GobyPistolDuo : MonoBehaviour
    {
        enum Half { Goby = 0, Pistol = 1 }
        enum By { Body = 0, Vortex = 1, Chain = 2, Flare = 3 }

        [Tooltip("The duo's numbers, cloned from the artifact. Empty: the artifact's SHIPPED values.")]
        [SerializeField] GobyPistolConfigSO config;

        [Tooltip("The AI's level on whichever half the human is not flying (the lab's Easy / Medium / Hard).")]
        [SerializeField] AIDifficulty aiLevel = AIDifficulty.Hard;

        [Tooltip("Draw the Pistol's side view (full screen while you fly the Pistol, an inset while you fly the Goby).")]
        [SerializeField] bool showSideView = true;

        const float TeleportU = 400f;           // the Goby moved further than this in a frame: a respawn, start a new ribbon
        static readonly Vector3 Stage = new(0f, -40000f, 0f);   // where the flat world is drawn, far from the real one

        sealed class RingRun { public VortexRing Ring; public readonly List<Prism> Prisms = new(); }
        struct Flare { public Vector3 P, V; public Prism Target; public float Life; }

        IVessel _goby; Transform _gt;   // bound while this is the local pilot's hull, flown by a human
        PistolRibbon _rb; PistolBodyMath.State _p; PistolAI _ai;
        PistolBodyMath.Settings _body; VortexRing.Settings _vs; PistolAI.Settings _ais; PistolRibbon.LaySettings _lay;
        float _t, _lastSwapAt = -9f; Vector3 _lastPos; Half _human = Half.Goby; bool _weTookGoby; IInputStatus _pausedInput;
        readonly List<RingRun> _rings = new();
        readonly HashSet<Prism> _held = new();
        readonly Dictionary<Prism, float> _primed = new(), _hot = new();
        readonly List<Prism> _qBody = new(), _qRing = new(), _qChain = new(), _qScan = new(), _qView = new(), _qGoby = new();
        readonly List<Prism> _expired = new();
        readonly Stack<Vector3> _chain = new();

        // the Goby's primer and flares (the lab's g.primer / g.flare)
        int _primer, _flare; float _primerT, _flareT, _lastPrimerAt = -9f, _lastFlareAt = -9f; bool _prevPrimer, _prevFlare;
        readonly List<Flare> _flares = new();

        // the human Pistol's pointer and pad (the lab's humanPistol)
        bool _mouseDown, _aiming; Vector2 _mouseFrom; Vector2? _mouseAim, _padAim;

        // the side view (what the Pistol sees: the section below)
        const int ViewSides = 24;                 // the Pistol's tube (the lab's cylinder)
        const int MaxWarped = 400, MaxBoxes = 600, MaxBars = 16, MaxTiles = 400, MaxSegTests = 120000, LineSamples = 220;
        struct Seen { public Prism Prism; public PistolRibbon.MapResult M; public PistolWarp.Zone Z; public bool Ground; public int Class; public float Key; public int Id; }
        struct Fx { public Vector3 At; public float T0, Life, R0, R1; public Color32 Color; public bool Disc; }
        readonly List<Seen> _seen = new(2048), _foreground = new(256);
        static readonly System.Comparison<Seen> s_order = (a, b) =>
        {
            int c = a.Class.CompareTo(b.Class); if (c != 0) return c;
            c = a.Key.CompareTo(b.Key); if (c != 0) return c;
            return a.Id.CompareTo(b.Id);
        };
        static readonly System.Comparison<Seen> s_farFirst = (a, b) => { int c = b.M.D.CompareTo(a.M.D); return c != 0 ? c : a.Id.CompareTo(b.Id); };
        static readonly ProfilerMarker s_viewMarker = new("GobyPistol.SideView");
        static readonly Color32 C_Primed = new(185, 139, 255, 255), C_Hot = new(255, 106, 42, 255), C_Held = new(159, 232, 255, 255);   // the lab's own marks
        static readonly Color32 C_Body = new(255, 138, 61, 255), C_AimGreen = new(75, 227, 143, 255), C_AimAmber = new(255, 180, 84, 255), C_AimRed = new(255, 92, 108, 255);
        readonly List<Fx> _fx = new();
        readonly List<Vector3> _tail = new(32), _linePts = new(LineSamples);
        readonly List<Color32> _lineCols = new(LineSamples);
        readonly Vector3[] _corners = new Vector3[8], _xhd = new Vector3[8];
        readonly Dictionary<Domains, Color32> _domainColor = new();
        readonly Dictionary<Prism, int> _lastK = new(2048);        // where each prism mapped last frame (the coherence cache)
        readonly Dictionary<Prism, float> _barSince = new();         // when a prism became a bar (hysteresis, grow-in)
        readonly List<Prism> _expiredK = new();
        readonly PistolWorldMesh _world = new();
        GameObject _view; Camera _cam; float _scX, _scY; bool _scLive;
        Quaternion _frame = Quaternion.identity; bool _frameLive;
        float _squash, _shake, _nextSweep;
        int _cntMapped, _cntWarped, _cntBars, _cntBoxes, _cntTiles;
        // the 3D world's share of the Pistol's effects: the rings and flares the Goby pilot sees, instanced
        Mesh _cube, _torus; Material _ringMat, _flareMat;

        GobyPistolConfigSO Config => config;

        /// <summary>Add the duo to the vessel you are flying (it then stands in for the Goby). False when no local
        /// vessel is flying. A hull that already carries it (the Goby) is left as it is.</summary>
        public static bool AttachToLocalVessel(GobyPistolConfigSO cfg)
        {
            var vt = VesselVisionShading.LocalVessel;
            if (!vt || !vt.TryGetComponent(out IVessel _)) return false;
            if (vt.TryGetComponent(out GobyPistolDuo _)) return true;
            var duo = vt.gameObject.AddComponent<GobyPistolDuo>();
            if (cfg) duo.config = cfg;
            return true;
        }

        // the local pilot's own hull, flown by a human (or handed to the AI by this component's own swap)
        bool ShouldRun(out IVessel vessel)
        {
            vessel = null;
            if (VesselVisionShading.LocalVessel != transform || !TryGetComponent(out vessel)) return false;
            return _weTookGoby || vessel.VesselStatus == null || !vessel.VesselStatus.AutoPilotEnabled;
        }

        void Bind(IVessel vessel)
        {
            _goby = vessel; _gt = transform;
            if (!config) config = ScriptableObject.CreateInstance<GobyPistolConfigSO>();
            _body = Config.Body; _vs = Config.Vortex; _ais = Config.AI; _lay = Config.Lay;
            _primer = Mathf.RoundToInt(Config.PrimerCharges); _flare = Mathf.RoundToInt(Config.FlareCharges);
            _t = 0f; _lastSwapAt = -9f; _human = Half.Goby;
            BuildView();
            ResetWorld();
        }

        void Unbind()
        {
            if (_goby == null) return;
            if (_human == Half.Pistol) SetHuman(Half.Goby);
            ReleaseAllRings(); _flares.Clear(); _primed.Clear(); _hot.Clear();
            if (_view) Destroy(_view);
            _world.Dispose(); _fx.Clear(); _tail.Clear(); _seen.Clear(); _foreground.Clear(); _lastK.Clear(); _barSince.Clear();
            foreach (var o in new UnityEngine.Object[] { _torus, _ringMat, _flareMat })
                if (o) Destroy(o);
            _goby = null; _gt = null; _rb = null; _cam = null; _view = null;
        }

        void ResetWorld()
        {
            ReleaseAllRings();
            _rb = new PistolRibbon(Config.Unroll);
            Vector3 pos = _gt.position, f = _gt.forward, u = _gt.up;
            _rb.Begin(pos, f, u, Vector3.Cross(f, u).normalized, _lay);
            _lastPos = pos;
            _p = PistolBodyMath.Spawn(_rb, 0f, _body.pMinSpeed + 25f, _body);   // the lab's makeWorld: the Pistol at x = 0
            _ai = new PistolAI(31337u + 17u);
            _scLive = false; _frameLive = false; _lastK.Clear(); _barSince.Clear();
            _tail.Clear(); _fx.Clear();   // the old ribbon's marks: a tail kept here streaks across the new screen
        }

        void OnDisable() => Unbind();

        // ================================ the step (the lab's stepWorld) ================================

        void Update()
        {
            bool run = ShouldRun(out var vessel);
            if (!run) { Unbind(); return; }
            if (_goby == null) Bind(vessel);
            float dt = Time.deltaTime; if (dt <= 0f) return;
            _t += dt;

            Vector3 pos = _gt.position, f = _gt.forward, u = _gt.up, r = Vector3.Cross(f, u).normalized;
            float moved = (pos - _lastPos).magnitude;
            if (moved > TeleportU) { ResetWorld(); return; }
            _rb.Lay(_lastPos, pos, f, u, r, _lay);
            _lastPos = pos;
            float gobySpeed = moved / dt;

            PollSwap();
            var inp = _human == Half.Pistol ? HumanPistol() : _ai.Think(_p, _rb, _t, Level(), _ais, Scan);
            var ev = PistolBodyMath.Step(_p, _rb, _rb.GobyX, gobySpeed, _t, dt, inp, _body);
            if (ev.FireSnap) _rings.Add(new RingRun { Ring = VortexRing.Fire(_rb, _p, ev.SnapAim, _vs.pVortexLife) });
            Feel(ev, dt);
            BodyHits();
            StepRings(dt);
            GobyAbilities(dt, pos, f, u, r);
            StepFlares(dt);
            Expire(_primed); Expire(_hot);
            if (_t >= _nextSweep) { _nextSweep = _t + 2f; SweepDead(_lastK); Expire(_barSince); }
        }

        void SweepDead(Dictionary<Prism, int> marks)
        {
            _expiredK.Clear();
            foreach (var kv in marks) if (!Live(kv.Key)) _expiredK.Add(kv.Key);
            foreach (var k in _expiredK) marks.Remove(k);
        }

        PistolAI.Difficulty Level() => aiLevel switch
        {
            AIDifficulty.Easy => PistolAI.Difficulty.Easy,
            AIDifficulty.Medium => PistolAI.Difficulty.Medium,
            _ => PistolAI.Difficulty.Hard,
        };

        static PrismSpatialIndex Index()
        {
            var idx = PrismSpatialIndex.Instance;
            return idx != null && idx.IsAvailable ? idx : null;
        }

        bool Live(Prism prism) => prism && !prism.destroyed;
        bool IsPrimed(Prism prism) => _primed.TryGetValue(prism, out float until) && until > _t;
        bool IsHot(Prism prism) => _hot.TryGetValue(prism, out float until) && until > _t;
        bool Friendly(Prism prism) => prism.Domain == _goby.VesselStatus.Domain;

        /// <summary>The Goby's OWN trail is the ground the Pistol rides, never a target: not seen, not hit, not primed, not
        /// carried (the lab keeps its trail blocks out of the prism arrays for the same reason). Identity of the Trail, not
        /// the player name: the hull's other lay sites (the Squirrel's tube) are friendly prisms like any other.</summary>
        bool IsGround(Prism prism)
        {
            var pc = _goby?.VesselStatus?.VesselPrismController;
            return pc != null && PistolTargets.IsOwnTrail(prism.Trail, pc.Trail, pc.SecondaryTrail);
        }

        void Expire(Dictionary<Prism, float> marks)
        {
            _expired.Clear();
            foreach (var kv in marks) if (!Live(kv.Key) || kv.Value <= _t) _expired.Add(kv.Key);
            foreach (var k in _expired) marks.Remove(k);
        }

        // the lab's pistolScan: what the Pistol AI can see
        void Scan(List<PistolAI.Seen> into)
        {
            var idx = Index(); if (idx == null) return;
            float reach = PistolAI.ScanReach(_ais);
            idx.QuerySphere(_p.wpos, reach, _qScan);
            foreach (var prism in _qScan)
                if (Live(prism) && !IsGround(prism) && PistolAI.TrySee(_rb, _p, prism.GetInstanceID(), prism.transform.position, IsPrimed(prism), Friendly(prism), reach, _ais, out var seen))
                    into.Add(seen);
        }

        // the lab's body test: a cylinder across the band, breaking everything it touches (friendly fire ON) unless ghosting
        void BodyHits()
        {
            if (_t < _p.ghostUntil) return;
            var idx = Index(); if (idx == null) return;
            float half = Config.Vision + 4f, reach = half + Config.BodyR + 16f;
            idx.QuerySphere(_p.wpos, reach, _qBody);
            foreach (var prism in _qBody)
            {
                if (!Live(prism) || IsGround(prism)) continue;
                var m = _rb.Map(prism.transform.position, _p.x - reach - 30f, _p.x + reach + 30f);
                if (!PistolBodyMath.Touches(_p, m, Config.Vision, Config.BodyR)) continue;
                Break(prism, By.Body, PistolVelocity());
                Pop(new Vector3(m.X, m.H, m.D), PrismColor(prism), 3f, 14f, 0.3f); _shake = Mathf.Max(_shake, 0.12f);
                if (_p.grounded) _p.v *= 0.97f; else _p.vx *= 0.97f;
            }
        }

        Vector3 PistolVelocity()
        {
            _rb.FrameAt(_p.x, out var T, out var U, out _);
            return _p.grounded ? T * _p.v : T * _p.vx + U * _p.vy;
        }

        // the lab's kill: the game's own damage, then a primed block goes up (unless a flare took it)
        void Break(Prism prism, By by, Vector3 impact)
        {
            if (!Live(prism) || prism.prismProperties is { IsSuperShielded: true }) return;
            bool primed = IsPrimed(prism); Vector3 at = prism.transform.position;
            prism.Damage(impact, _goby.VesselStatus.Domain, _goby.VesselStatus.PlayerName);
            if (primed && by != By.Flare) Detonate(prism, at);
        }

        // the lab's detonate: the primed blocks it touches go with it; the ring round it turns hot
        void Detonate(Prism first, Vector3 at)
        {
            var idx = Index(); if (idx == null) return;
            float c2 = Config.Chain * Config.Chain, h2 = Config.HotRing * Config.HotRing;
            _primed.Remove(first); _chain.Clear(); _chain.Push(at);
            while (_chain.Count > 0)
            {
                Vector3 c = _chain.Pop();
                idx.QuerySphere(c, Mathf.Max(Config.Chain, Config.HotRing), _qChain);
                foreach (var prism in _qChain)
                {
                    if (!Live(prism) || IsGround(prism)) continue;
                    Vector3 w = prism.transform.position; float d2 = (w - c).sqrMagnitude;
                    if (IsPrimed(prism) && d2 <= c2)
                    {
                        _primed.Remove(prism);
                        if (prism.prismProperties is { IsSuperShielded: true }) continue;
                        prism.Damage((w - c).normalized * 40f, _goby.VesselStatus.Domain, _goby.VesselStatus.PlayerName);
                        _chain.Push(w);
                    }
                    else if (d2 <= h2) _hot[prism] = _t + Config.HotLife;
                }
            }
        }

        // the lab's vortexStep: the ring travels, pulls prisms near its core into its toroidal flow, and collapses
        void StepRings(float dt)
        {
            var idx = Index();
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var run = _rings[i]; var ring = run.Ring;
                bool alive = ring.Advance(dt, _vs);
                if (idx != null && !ring.Full(_vs))
                {
                    idx.QuerySphere(ring.C, _vs.pVortexR + _vs.pVortexCapture, _qRing);
                    foreach (var prism in _qRing)
                    {
                        if (ring.Full(_vs)) break;
                        if (!Live(prism) || IsGround(prism) || _held.Contains(prism) || prism.IsGrowing || prism.prismProperties is { IsSuperShielded: true }) continue;
                        if (ring.TryCapture(run.Prisms.Count, prism.transform.position, _vs)) { run.Prisms.Add(prism); _held.Add(prism); }
                    }
                }
                for (int j = 0; j < ring.Caps.Count; j++)
                {
                    var prism = run.Prisms[ring.Caps[j].Id]; if (!Live(prism)) continue;
                    var tr = prism.transform;
                    tr.position = ring.Flow(j, dt, _vs);
                    tr.Rotate(ring.N, 4f * dt * Mathf.Rad2Deg, Space.World);
                    prism.NotifyPositionChanged();
                }
                if (alive) continue;
                if (_vs.pVortexCollapse >= 0.5f)
                {   // the lab's collapse boom: a disc that swells and fades, and a shake
                    var m = _rb.MapFast(ring.C, _scX - 2f * Config.ViewWidth, _scX + 2f * Config.ViewWidth, true);
                    if (m.Ok) Boom(new Vector3(m.X, m.H, m.D), Alpha(C_Held, 0.5f), Mathf.Max(1f, _vs.pVortexR * 0.3f), _vs.pVortexR * 1.3f, 0.5f);
                    _shake = Mathf.Max(_shake, 0.35f);
                }
                foreach (var prism in run.Prisms)
                {
                    _held.Remove(prism);
                    if (_vs.pVortexCollapse >= 0.5f) Break(prism, By.Vortex, ring.N * _vs.pVortexSpeed);
                }
                _rings.RemoveAt(i);
            }
        }

        void ReleaseAllRings()
        {
            _rings.Clear(); _held.Clear();   // carried prisms stay where the ring left them
        }

        // ---- the Goby's primer and flares (the lab's gobyStep abilities; its AI's choice when the AI flies the Goby) ----
        Prism PrimerTarget(Vector3 pos, Vector3 f)
        {
            var idx = Index(); if (idx == null) return null;
            float cosMax = Mathf.Cos(10f * Mathf.Deg2Rad), bd = float.PositiveInfinity; Prism best = null;
            idx.QuerySphere(pos, Config.PrimerRange, _qGoby);
            foreach (var prism in _qGoby)
            {
                if (!Live(prism) || IsGround(prism)) continue;
                Vector3 d = prism.transform.position - pos; float dist = d.magnitude;
                if (dist > Config.PrimerRange || dist < 1e-6f || Vector3.Dot(d, f) / dist < cosMax) continue;
                if (dist < bd) { bd = dist; best = prism; }
            }
            return best;
        }

        int HotInRange(Vector3 pos, List<Prism> into)
        {
            into.Clear();
            foreach (var kv in _hot)
                if (Live(kv.Key) && kv.Value > _t && (kv.Key.transform.position - pos).sqrMagnitude <= Config.FlareRange * Config.FlareRange)
                    into.Add(kv.Key);
            return into.Count;
        }

        void GobyAbilities(float dt, Vector3 pos, Vector3 f, Vector3 u, Vector3 r)
        {
            bool primer, flares;
            if (_human == Half.Goby)
            {
                var kb = Keyboard.current; var pad = Gamepad.current;
                primer = (kb != null && kb.digit3Key.isPressed) || (pad != null && pad.dpad.up.isPressed);
                flares = (kb != null && kb.digit4Key.isPressed) || (pad != null && pad.dpad.down.isPressed);
            }
            else
            {
                // the lab's gobyAI: prime what is ahead, fire the flares at three hot blocks (or one, with a full rack)
                var pt = _primer > 0 && _t - _lastPrimerAt > 0.8f ? PrimerTarget(pos, f) : null;
                bool wantPrimer = pt && !Friendly(pt) && !IsPrimed(pt);
                int hot = _flare > 0 ? HotInRange(pos, _qGoby) : 0;
                bool wantFlares = hot >= 3 || (hot >= 1 && _flare >= Config.FlareCharges && _t - _lastFlareAt > 3f);
                primer = wantPrimer && !_prevPrimer; flares = wantFlares && !_prevFlare;
            }
            if (primer && !_prevPrimer && _primer > 0)
            {
                var target = PrimerTarget(pos, f); _primer--; _lastPrimerAt = _t;
                var idx = Index();
                if (target && idx != null)
                {
                    idx.QuerySphere(target.transform.position, Config.PrimeSplash, _qGoby);
                    foreach (var prism in _qGoby) if (Live(prism) && !IsGround(prism)) _primed[prism] = _t + Config.PrimeLife;
                }
            }
            _prevPrimer = primer;
            if (flares && !_prevFlare && _flare > 0 && HotInRange(pos, _qGoby) > 0)
            {
                _qGoby.Sort((a, b) => (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
                int n = Mathf.Min(_qGoby.Count, Mathf.RoundToInt(Config.FlareMax));
                for (int j = 0; j < n; j++)
                {
                    float a2 = j / (float)n * Mathf.PI * 2f;
                    var dir = (f + u * (Mathf.Cos(a2) * 0.7f) + r * (Mathf.Sin(a2) * 0.7f)).normalized;
                    _flares.Add(new Flare { P = pos, V = dir * Config.FlareSpeed, Target = _qGoby[j], Life = 4f });
                }
                _flare--; _lastFlareAt = _t;
            }
            _prevFlare = flares;
            if (_primer < Config.PrimerCharges) { _primerT += dt; if (_primerT >= Config.PrimerCd) { _primer++; _primerT = 0f; } } else _primerT = 0f;
            if (_flare < Config.FlareCharges) { _flareT += dt; if (_flareT >= Config.FlareCd) { _flare++; _flareT = 0f; } } else _flareT = 0f;
        }

        // the lab's flareStep: heat-seeking, one per hot block; a flare whose block went finds the nearest unchased one
        void StepFlares(float dt)
        {
            for (int i = _flares.Count - 1; i >= 0; i--)
            {
                var F = _flares[i];
                if (!Live(F.Target))
                {
                    Prism best = null; float bd = 160f * 160f;
                    foreach (var kv in _hot)
                    {
                        if (!Live(kv.Key) || kv.Value <= _t || Chased(kv.Key, i)) continue;
                        float d2 = (kv.Key.transform.position - F.P).sqrMagnitude; if (d2 < bd) { bd = d2; best = kv.Key; }
                    }
                    if (!best) { _flares.RemoveAt(i); continue; }
                    F.Target = best;
                }
                Vector3 T = F.Target.transform.position; float sp = Config.FlareSpeed, k = Mathf.Clamp01(5f * dt);
                F.V = Vector3.LerpUnclamped(F.V, (T - F.P).normalized * sp, k).normalized * sp;
                F.P += F.V * dt; F.Life -= dt;
                if ((T - F.P).sqrMagnitude < 49f) { Break(F.Target, By.Flare, F.V); F.Life = 0f; }
                if (F.Life <= 0f) _flares.RemoveAt(i); else _flares[i] = F;
            }
        }

        bool Chased(Prism prism, int except)
        {
            for (int i = 0; i < _flares.Count; i++) if (i != except && _flares[i].Target == prism) return true;
            return false;
        }

        // ================================ who flies what (the arena's swap) ================================

        void PollSwap()
        {
            // the arena's gesture, read here because the Goby's InputController is paused while the human is on the
            // Pistol (and RequestPilotSwap is a no-op outside an arena match, so the two never both act)
            if (PilotSwapGesture.DirectionThisFrame() == 0 || _t - _lastSwapAt < PilotSwap.CooldownSeconds) return;
            _lastSwapAt = _t;
            SetHuman(_human == Half.Goby ? Half.Pistol : Half.Goby);
        }

        void SetHuman(Half half)
        {
            if (half == Half.Goby && _pausedInput != null)
            {
                // the input paused is the PLAYER's, which outlives this hull (a swap re-homes it): restore that one
                if (_pausedInput is not UnityEngine.Object o || o) _pausedInput.Paused = false;   // skip a destroyed component
                _pausedInput = null;
            }
            if (_goby == null || !_gt) { _human = half; _weTookGoby = false; return; }   // the hull is gone: nothing to hand back
            if (half == Half.Pistol)
            {
                // the AI takes the Goby; the human's sticks stop reaching it (they fly the Pistol now)
                _weTookGoby = !_goby.VesselStatus.AutoPilotEnabled;
                if (_weTookGoby) _goby.ToggleAIPilot(true);
                var player = _goby.VesselStatus.Player;
                _pausedInput = player != null ? player.InputStatus : null;
                if (_pausedInput != null) _pausedInput.Paused = true;
            }
            else
            {
                if (_weTookGoby) _goby.ToggleAIPilot(false);
                _weTookGoby = false;
            }
            _human = half;
            // a press carried across the swap is not a press (the lab's swap)
            _ai.ReleaseAll(); _p.prevGhost = _p.prevVortex = false; _prevPrimer = _prevFlare = false;
            _mouseDown = _aiming = false; _mouseAim = _padAim = null;
        }

        // the lab's humanPistol: hold to dive; drag (or RT + right stick) to aim a launch; G / B ghost; Q / E / X / Y Snap
        PistolBodyMath.Input HumanPistol()
        {
            var inp = new PistolBodyMath.Input();
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            float dive = 0f;
            if (mouse != null && _cam)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && _cam.pixelRect.Contains(mp)) { _mouseDown = true; _aiming = false; _mouseFrom = mp; }
                if (_mouseDown)
                {
                    if ((mp - _mouseFrom).magnitude > 12f) _aiming = true;
                    if (_aiming && PlanePoint(mp, out var w)) _mouseAim = new Vector2(w.x - (_p.x - _scX), w.y - _p.y);
                    if (!_aiming) dive = 1f;
                    if (!mouse.leftButton.isPressed)
                    {
                        if (_aiming && _mouseAim.HasValue) { inp.HasLaunch = true; inp.Launch = _mouseAim.Value; }
                        _mouseDown = _aiming = false; _mouseAim = null;
                    }
                }
            }
            if (pad != null)
            {
                if (pad.rightTrigger.ReadValue() > 0.5f) _padAim = pad.rightStick.ReadValue() * _body.pLaunchRange;
                else if (_padAim.HasValue) { if (_padAim.Value.magnitude > 10f) { inp.HasLaunch = true; inp.Launch = _padAim.Value; } _padAim = null; }
                if (pad.buttonSouth.isPressed) dive = 1f;
                dive = Mathf.Max(dive, pad.leftTrigger.ReadValue());
                inp.Ghost |= pad.buttonEast.isPressed;
                inp.Snap |= pad.buttonWest.isPressed || pad.buttonNorth.isPressed;
            }
            if (kb != null)
            {
                if (kb.spaceKey.isPressed || kb.sKey.isPressed || kb.downArrowKey.isPressed) dive = 1f;
                if (kb.fKey.wasPressedThisFrame && !inp.HasLaunch) { inp.HasLaunch = true; inp.Launch = new Vector2(0.7f * _body.pLaunchRange, 0.45f * _body.pLaunchRange); }
                inp.Ghost |= kb.gKey.isPressed;
                inp.Snap |= kb.qKey.isPressed || kb.eKey.isPressed;
            }
            inp.Dive = dive;
            if (inp.Snap) inp.SnapAim = _mouseAim ?? _padAim ?? new Vector2(1f, 0f);
            return inp;
        }

        // a screen point on the side view's plane (d = 0), in the flat world relative to the camera's x
        bool PlanePoint(Vector2 screen, out Vector3 w)
        {
            w = default;
            var ray = _cam.ScreenPointToRay(screen);
            var inv = Quaternion.Inverse(_frame);   // the plane is the flat frame's z = 0, placed in the world by _frame
            Vector3 o = inv * (ray.origin - Stage), dir = inv * ray.direction;
            if (Mathf.Abs(dir.z) < 1e-5f) return false;
            float s = -o.z / dir.z; if (s <= 0f) return false;
            w = o + dir * s; return true;
        }

        // ================================ the side view (the lab's renderSide3D, grown up) ================================
        // What the Pistol SEES. Every prism in a wide depth window, warped through the unroll (PistolWarp: a prism the
        // Goby circles stretches out, as the unroll says it must; one holding a lap's centre is the bar of the whole lap)
        // and laid on the screen plane with 2.5D parallax about the Pistol (the lab's pParallax: a layer that deep draws
        // at half size), vivid and rimmed inside the band its body spans, fogged in the near field, a rigid box sinking
        // into the fog in the deep background, see-through in the foreground, a fogged forecast past the Goby. The
        // Goby's own trail is the GROUND it rides: a domain-coloured line on the hinge hills, warm on the downslopes,
        // with the trail's blocks sitting on it. The Pistol end on (a cone whose axis is one screen point), its tiny
        // trail, the rings, the flares, the aim, and the pops, puffs and shakes that say what just happened. One mesh a
        // frame in two passes (solid, then glass far to near); the frame at the Pistol turns the sky with the Goby's heading.

        void BuildView()
        {
            _view = new GameObject("PistolSideView");   // at the scene root: the hull's scale and pose are not the view's
            _cam = _view.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.Skybox; _cam.orthographic = true; _cam.nearClipPlane = 0.1f; _cam.cullingMask = 1;
            _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            _torus = BuildTorus(1f, 0.08f, 40, 8);
            _ringMat = Unlit(new Color(0.62f, 0.91f, 1f)); _flareMat = Unlit(new Color(1f, 0.9f, 0.5f));
            _scLive = false; _frameLive = false; _squash = _shake = 0f; _tail.Clear(); _fx.Clear();
        }

        static Material Unlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Color");
            if (!shader) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { color = c, enableInstancing = true };
        }

        static Mesh BuildTorus(float R, float r, int seg, int side)
        {
            var v = new Vector3[seg * side]; var tri = new int[seg * side * 6]; int t = 0;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f, b = j / (float)side * Mathf.PI * 2f, rr = R + r * Mathf.Cos(b);
                    v[i * side + j] = new Vector3(rr * Mathf.Cos(a), rr * Mathf.Sin(a), r * Mathf.Sin(b));
                    int i1 = (i + 1) % seg, j1 = (j + 1) % side;
                    tri[t++] = i * side + j; tri[t++] = i1 * side + j; tri[t++] = i1 * side + j1;
                    tri[t++] = i * side + j; tri[t++] = i1 * side + j1; tri[t++] = i * side + j1;
                }
            var m = new Mesh { name = "VortexRing", vertices = v, triangles = tri }; m.RecalculateNormals(); return m;
        }

        Color32 DomainColor(Domains domain)
        {
            if (!_domainColor.TryGetValue(domain, out var c)) { c = PrismLit.DomainTint(domain); _domainColor[domain] = c; }
            return c;
        }

        /// <summary>The colour the prism is DRAWN in (parity with the 3D view): its material animator's bright colour,
        /// brought down from HDR to a peak of 1; the domain's signal colour when it has none.</summary>
        Color32 PrismColor(Prism prism)
        {
            if (prism.TryGetComponent(out MaterialPropertyAnimator mpa))
            {
                var b = mpa.TargetBrightColor; float peak = Mathf.Max(b.r, Mathf.Max(b.g, b.b));
                if (peak > 0.001f) return new Color(b.r / peak, b.g / peak, b.b / peak, 1f);
            }
            return DomainColor(prism.Domain);
        }

        static Color32 Tint(Color32 c, Color32 toward, float k) => Color.Lerp(c, toward, k);
        static Color32 Alpha(Color32 c, float a) { c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255); return c; }

        // ---- feel: what the step did, drawn and felt (a squash on landing, a puff, a shake on a hard one)
        void Feel(in PistolBodyMath.Events ev, float dt)
        {
            if (ev.Landed)
            {
                _squash = 1f;
                var ground = DomainColor(_goby.VesselStatus.Domain);
                Pop(new Vector3(_p.x, _p.y - Config.BodyR, 0f), Alpha(ground, 0.8f), 2f, ev.SmoothLanding ? 10f : 18f, ev.SmoothLanding ? 0.3f : 0.45f);
                if (!ev.SmoothLanding) _shake = Mathf.Max(_shake, 0.5f);
            }
            if (ev.Launched) Pop(new Vector3(_p.x, _p.y, 0f), new Color32(255, 255, 255, 200), 2f, 9f, 0.25f);
            if (ev.GhostStarted) Pop(new Vector3(_p.x, _p.y, 0f), Alpha(C_Body, 0.63f), Config.BodyR, Config.BodyR * 3f, 0.4f);
            _squash = Mathf.Max(0f, _squash - dt * 4f);
            _shake = Mathf.Max(0f, _shake - dt * 2.5f);
            // the tiny trail: where the Pistol has just been
            if (_tail.Count == 0 || (new Vector3(_p.x, _p.y, 0f) - _tail[_tail.Count - 1]).sqrMagnitude > 1f) _tail.Add(new Vector3(_p.x, _p.y, 0f));
            if (_tail.Count > 26) _tail.RemoveAt(0);
        }

        /// <summary>A ring that grows from r0 to r1 over <paramref name="life"/> s and fades, at (x, h, d).</summary>
        void Pop(Vector3 at, Color32 color, float r0, float r1, float life)
        {
            if (_fx.Count < 64) _fx.Add(new Fx { At = at, T0 = _t, Life = life, R0 = r0, R1 = r1, Color = color });
        }

        /// <summary>A disc that swells from r0 to r1 over <paramref name="life"/> s and fades (the lab's collapse boom).</summary>
        void Boom(Vector3 at, Color32 color, float r0, float r1, float life)
        {
            if (_fx.Count < 64) _fx.Add(new Fx { At = at, T0 = _t, Life = life, R0 = r0, R1 = r1, Color = color, Disc = true });
        }

        void LateUpdate()
        {
            if (!_cam || _rb == null) return;
            _cam.enabled = showSideView;
            if (!showSideView) return;
            using (s_viewMarker.Auto())
            {
                var main = Camera.main;
                _cam.depth = (main ? main.depth : 0f) + 5f;
                _cam.rect = _human == Half.Pistol ? new Rect(0f, 0f, 1f, 1f) : new Rect(0.62f, 0.03f, 0.35f, 0.35f);
                float W = Config.ViewWidth, aspect = Mathf.Max(0.2f, _cam.aspect), D = Mathf.Max(20f, Config.Parallax), lift = 0.08f * (W / aspect);
                // the lab's sideCam: a fifth of a screen ahead of the Pistol, easing in height
                PistolWarp.SideCamTarget(_p.x, _p.y, _rb.Ground(_p.x), W, aspect, out float tx, out float ty);
                _scX = tx; _scY = _scLive ? Mathf.LerpUnclamped(_scY, ty, Mathf.Clamp01(Time.deltaTime * 4f)) : ty; _scLive = true;
                float cx = _p.x, cy = _scY - lift;   // the parallax centre: the Pistol's x, the camera's height less its lift
                // the flat frame at the Pistol places the picture in the world: the sky turns with the Goby's heading (eased:
                // the frame steps every sample)
                _rb.FrameAt(_p.x, out var fT, out var fU, out var fR);
                var target = PistolWarp.FlatFrame(fT, fU, fR);
                _frame = _frameLive ? Quaternion.Slerp(_frame, target, Mathf.Clamp01(Time.deltaTime * 6f)) : target; _frameLive = true;
                _cam.orthographicSize = 0.5f * W / aspect;
                _cam.farClipPlane = 4f * D + 50f;
                var shake = _shake > 0f ? new Vector3(Mathf.Sin(_t * 47f), Mathf.Cos(_t * 31f), 0f) * (_shake * _shake * 3f) : Vector3.zero;
                _cam.transform.SetPositionAndRotation(Stage + _frame * (new Vector3(0f, _scY, -D) + shake), _frame);

                _world.Begin(_scX, _scY, cx, cy, D);
                _rb.SegTests = 0; _cntMapped = _cntWarped = _cntBars = _cntBoxes = _cntTiles = 0;
                float xLo = _scX - 2f * W, xHi = _scX + 2f * W;   // the ribbon the far background can still show
                DrawGroundLine(W);
                DrawPrisms(W, aspect, D, cx, cy, xLo, xHi);
                DrawActors(W, aspect, D, xLo, xHi);
                DrawForeground(W, D, xLo, xHi);
                DrawFx();
                _world.End(_cam, Matrix4x4.TRS(Stage, _frame, Vector3.one));
            }
            Draw3D();
        }

        // ---- the ground line: the hinge hills the Pistol rides, in the domain colour, warm where they fall away
        void DrawGroundLine(float W)
        {
            if (_rb.ControlPointCount == 0 || _rb.CX.Count == 0) return;
            float xa = Mathf.Max(_rb.CX[0], _scX - 0.6f * W), xb = Mathf.Min(_rb.GobyX, _scX + 0.6f * W);
            if (xb <= xa) return;
            var domain = DomainColor(_goby.VesselStatus.Domain);
            _linePts.Clear(); _lineCols.Clear();
            for (int i = 0; i < LineSamples; i++)
            {
                float x = Mathf.LerpUnclamped(xa, xb, i / (float)(LineSamples - 1)), y = _rb.Ground(x, out float slope);
                _linePts.Add(new Vector3(x, y, 0f));
                _lineCols.Add(Tint(domain, C_Body, PistolWarp.DiveTint(slope)));
            }
            _world.AddLine(_linePts, _lineCols, 0.45f, 1f, true);
        }

        // ---- the prisms: gathered, mapped (through last frame's segment when it still fits), zoned, chosen, then
        // drawn far to near — the deep background as boxes, the near field warped, the foreground after the actors
        void DrawPrisms(float W, float aspect, float D, float cx, float cy, float xLo, float xHi)
        {
            var idx = Index(); if (idx == null) return;
            float band = Config.Vision + 4f, vision = Config.Vision, near = 3f * vision;
            Vector3 centre = _rb.Unmap(_scX, _scY, 0f);
            idx.QuerySphere(centre, 0.75f * W + 260f, _qView);
            _seen.Clear(); _foreground.Clear();
            foreach (var prism in _qView)
            {
                if (!Live(prism)) continue;
                Vector3 q = prism.transform.position;
                PistolRibbon.MapResult m; bool hit = false;
                if (_lastK.TryGetValue(prism, out int lk))
                {
                    m = _rb.MapNear(q, lk, 6, xLo, xHi, true);
                    hit = m.Ok && m.K > lk - 6 && m.K < lk + 6;
                }
                else m = default;
                if (!hit) m = _rb.MapFast(q, xLo, xHi, true);
                if (!m.Ok) continue;
                _lastK[prism] = m.K; _cntMapped++;
                bool ground = IsGround(prism);
                var z = ground ? new PistolWarp.Zone { Drawn = true, Warped = false, Solid = true, Reach = 1f, Alpha = 1f, D = m.D } : PistolWarp.ZoneOf(m.D, band, vision, D, IsPrimed(prism));
                if (!z.Drawn) continue;
                if (!PistolWarp.OnScreen(m.X, m.H, z.D, _scX, _scY, cx, cy, D, W, aspect, 60f)) continue;
                int cls = ground ? 0 : Mathf.Abs(m.D) <= near ? 1 : 2;
                _seen.Add(new Seen { Prism = prism, M = m, Z = z, Ground = ground, Class = cls, Key = cls == 2 ? m.D : Mathf.Abs(m.X - _p.x), Id = prism.GetInstanceID() });
            }
            _seen.Sort(s_order);
            int kSpan = Mathf.Max(2, _rb.Index(xHi) - _rb.Index(xLo));
            // the far set: the nearest 600 by depth, drawn deepest first
            int farStart = _seen.Count; while (farStart > 0 && _seen[farStart - 1].Class == 2) farStart--;
            int farEnd = Mathf.Min(_seen.Count, farStart + MaxBoxes);
            for (int i = farEnd - 1; i >= farStart; i--) DrawPrism(_seen[i], D, xLo, xHi, kSpan);
            // the ground tiles, then the near set by distance along the path (the foreground waits for the actors)
            int tiles = 0, nears = 0;
            for (int i = 0; i < farStart; i++)
            {
                var e = _seen[i];
                if (e.Class == 0) { if (tiles++ < MaxTiles) DrawPrism(e, D, xLo, xHi, kSpan); continue; }
                if (nears++ >= MaxWarped) continue;
                if (e.M.D < -band) _foreground.Add(e); else DrawPrism(e, D, xLo, xHi, kSpan);
            }
        }

        void DrawForeground(float W, float D, float xLo, float xHi)
        {
            if (_foreground.Count == 0) return;
            _foreground.Sort(s_farFirst);
            int kSpan = Mathf.Max(2, _rb.Index(xHi) - _rb.Index(xLo));
            foreach (var e in _foreground) DrawPrism(e, D, xLo, xHi, kSpan);
        }

        void DrawPrism(in Seen e, float D, float xLo, float xHi, int kSpan)
        {
            var prism = e.Prism; var tr = prism.transform; var m = e.M; var z = e.Z;
            var mesh = prism.EffectiveRenderMesh();
            var data = PistolWorldMesh.DataOf(mesh);
            Bounds b = mesh ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            Vector3 c = tr.TransformPoint(b.center);
            Vector3 ax = tr.TransformVector(new Vector3(b.extents.x, 0f, 0f)), ay = tr.TransformVector(new Vector3(0f, b.extents.y, 0f)), az = tr.TransformVector(new Vector3(0f, 0f, b.extents.z));
            _rb.Frame(m.K, m.T, out var T, out var U, out var R);

            if (e.Ground)
            {
                // the trail is the ground: each block sits rigid on the line at its x, tilted to the slope, at its own depth
                float gy = _rb.Ground(m.X, out float slope);
                PistolWarp.TileCorners(m.X, gy, slope, m.D, ax, ay, az, T, U, R, _xhd);
                _world.AddWarped(PistolWorldMesh.Box, _xhd, Tint(PrismColor(prism), new Color32(255, 255, 255, 255), 0.35f), 0f, 1f, 0f, true);
                _cntTiles++;
                return;
            }

            Color32 color = PrismColor(prism); float glow = 0f;
            if (m.X > _rb.GobyX + 2f) PistolWarp.Forecast(ref z);   // the forecast: not laid yet
            if (IsPrimed(prism)) { color = Tint(color, C_Primed, 0.75f); glow = 0.35f + 0.25f * Mathf.Sin(_t * 6f); z.Fog *= 0.3f; z.Alpha = Mathf.Max(z.Alpha, 0.7f); }
            else if (IsHot(prism)) { color = C_Hot; glow = 0.3f + 0.2f * Mathf.Sin(_t * 9f); }
            if (_held.Contains(prism)) { color = Tint(color, C_Held, 0.5f); glow = Mathf.Max(glow, 0.5f); }
            // the ghost cue: a friendly block the AI would ghost through, while ghost is ready (PistolAI's own rule)
            if (_t >= _p.ghostAt && Friendly(prism) && Mathf.Abs(m.D) <= Config.Vision && m.X > _p.x && m.X - _p.x < 60f && Mathf.Abs(m.H - _p.y) < 25f)
                glow = Mathf.Max(glow, 0.25f * (0.5f + 0.5f * Mathf.Sin(_t * 8f)));
            var col = Alpha(color, z.Alpha);

            if (!z.Warped)
            {
                PistolWarp.RigidCorners(new Vector3(m.X, m.H, z.D), ax, ay, az, T, U, R, _xhd);
                _world.AddWarped(PistolWorldMesh.Box, _xhd, col, glow, z.Reach, z.Fog, z.Solid);
                _cntBoxes++;
                return;
            }

            float hd = Mathf.Sqrt(ax.sqrMagnitude + ay.sqrMagnitude + az.sqrMagnitude), kappa = _rb.Curvature(m.K), gap = _lay.SampleGap;
            var kind = PistolWarp.Classify(hd, kappa, m.D, gap, kSpan, out int window);
            // the bar: a prism holding the bend's centre is beside the whole lap (hysteresis so a wobbling lap cannot flicker it)
            float r = PistolWarp.CentreDistance(kappa, m.D);
            bool wasBar = _barSince.TryGetValue(prism, out float since), isBar = wasBar ? r <= hd + 6f : r <= hd + 2f;
            if (isBar && !wasBar) { since = _t; _barSince[prism] = since; }
            if (!isBar && wasBar) _barSince.Remove(prism);
            if (isBar && _cntBars < MaxBars)
            {
                PistolWarp.BoxCorners(c, ax, ay, az, _corners);
                PistolWarp.BarExtent(_rb, c, m.K, 1f / Mathf.Max(1e-6f, Mathf.Abs(kappa)), hd, _corners, xLo, xHi, out float xA, out float xB);
                float grow = 0.5f + 0.5f * Mathf.Clamp01((_t - since) / 0.25f);
                PistolWarp.BarCorners(xA, xB, new Vector3(m.X, m.H, m.D), ax, ay, az, U, R, grow, _xhd);
                _world.AddWarped(PistolWorldMesh.Box, _xhd, col, glow, z.Reach, z.Fog, z.Solid);
                _cntBars++;
                return;
            }
            if (kind == PistolWarp.Kind.Bar) window = 24;                 // over the bar budget: a wide bend instead
            if (_rb.SegTests > MaxSegTests) window = 2;                   // over the frame's budget: drawn straight, never dropped
            PistolWarp.BoxCorners(c, ax, ay, az, _corners);
            bool warped = PistolWarp.WarpBox(_rb, m.K, _corners, xLo, xHi, window, _xhd, true)
                          && PistolWarp.XRange(_xhd) <= PistolWarp.TearBound(hd, kappa, m.D, gap);
            if (!warped) PistolWarp.RigidCorners(new Vector3(m.X, m.H, m.D), ax, ay, az, T, U, R, _xhd);   // a fold: a corner landed on another pass
            _world.AddWarped(data, _xhd, col, glow, z.Reach, z.Fog, z.Solid);
            _cntWarped++;
        }

        // ---- the Pistol, its trail, the Goby, the rings, the flares, the aim
        void DrawActors(float W, float aspect, float D, float xLo, float xHi)
        {
            float half = Config.Vision + 4f, r = Config.BodyR * (1f + 0.3f * _squash);
            bool ghost = _t < _p.ghostUntil;
            var body = Alpha(C_Body, ghost ? 0.35f : 1f);
            var vel = PlaneVelocity(); var lean = vel.sqrMagnitude > 1f ? vel.normalized * 0.12f : Vector2.zero;
            var domain = DomainColor(_goby.VesselStatus.Domain);
            // the contact marker while airborne: where the ground is under you
            if (!_p.grounded)
            {
                float gy = _rb.Ground(_p.x);
                _world.AddDash(new Vector3(_p.x, gy, 0f), 1.3f, 0.25f, Alpha(domain, 0.6f * (1f - Mathf.Clamp01(Mathf.Abs(_p.y - gy) / 300f))));
            }
            // the tiny trail, then the tube END ON (its length runs into the screen, across the band), its facets turning, capped, with a glowing eye
            _world.AddStrip(_tail, 1.1f, Alpha(C_Body, ghost ? 0.3f : 0.75f), Alpha(C_Body, 0f), 0.6f);
            _world.AddTube(_p.x, _p.y, -half, half, r, r * 0.92f, ViewSides, body, 0.1f, 1f, lean, _t * 2f, !ghost);
            _world.AddDisc(new Vector3(_p.x - lean.x * half, _p.y - lean.y * half, -half + 0.05f), r, ViewSides, body, Tint(body, new Color32(0, 0, 0, body.a), 0.25f), 0.1f, !ghost, _t * 2f);
            _world.AddDisc(new Vector3(_p.x - lean.x * half, _p.y - lean.y * half, -half - 0.3f), r * 0.42f, 12, new Color32(255, 250, 230, 255), Alpha(C_Body, 1f), 1f, false);
            // the Goby, at the end of the ground, nosing along it; an arrow at the edge when it is off screen
            {
                float gx = _rb.GobyX, gyEnd = _rb.EndY; _rb.Ground(Mathf.Max(0f, gx - 3f), out float slope); float th = Mathf.Atan(slope);
                Vector3 axv = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f) * 7f, ayv = new Vector3(-Mathf.Sin(th), Mathf.Cos(th), 0f) * 2.4f, azv = new Vector3(0f, 0f, 2.4f);
                _world.AddBox(new Vector3(gx, gyEnd, 0f), axv, ayv, azv, domain, 0.6f, 1f, 0f, true);
                _world.AddDisc(new Vector3(gx + axv.x * 0.6f, gyEnd + axv.y * 0.6f, -2.6f), 1.2f, 8, new Color32(255, 255, 255, 255), domain, 1f, false);
                if (PistolWarp.ArrowPlace(gx, gyEnd, _scX, _scY, W, aspect, out float arrowX, out float arrowY))
                    _world.AddDisc(new Vector3(arrowX, arrowY, 0f), 4f, 3, domain, domain, 1f, false);
            }
            // vortex rings, mapped into the Pistol's world with their axis unrolled
            foreach (var run in _rings)
            {
                var ring = run.Ring; var m = _rb.MapFast(ring.C, xLo, xHi, true);
                if (!m.Ok) continue;
                _rb.FrameAt(m.X, out var T, out var U, out var R);
                var axis = new Vector3(Vector3.Dot(ring.N, T), Vector3.Dot(ring.N, U), Vector3.Dot(ring.N, R));
                float fade = Mathf.Clamp01(ring.Life / 0.35f);
                _world.AddRing(new Vector3(m.X, m.H, m.D), axis, _vs.pVortexR, 0.16f * _vs.pVortexR, 32, 8, Alpha(C_Held, fade), 1f);
            }
            foreach (var F in _flares)
            {
                var m = _rb.MapFast(F.P, xLo, xHi, true); if (!m.Ok) continue;
                _world.AddDot(new Vector3(m.X, m.H, m.D), 2.5f, new Color32(255, 230, 128, 255));
                var back = _rb.MapFast(F.P - F.V * 0.05f, xLo, xHi, true); if (back.Ok) _world.AddDot(new Vector3(back.X, back.H, back.D), 0.8f, new Color32(255, 200, 90, 140));
            }
            // the launch aim: yours while you drag (or hold RT) as the arc; the AI's as the lab's line, coloured by its state
            if (_human == Half.Pistol)
            {
                Vector2? aim = _mouseAim ?? _padAim;
                if (!aim.HasValue) return;
                float dx = aim.Value.x, dy = aim.Value.y, L = Mathf.Sqrt(dx * dx + dy * dy);
                if (L > _body.pLaunchRange) { dx *= _body.pLaunchRange / L; dy *= _body.pLaunchRange / L; }
                float Tt = Mathf.Max(0.35f, Mathf.Sqrt(dx * dx + dy * dy) / _body.pLaunchSpeed), vx = dx / Tt, vy = dy / Tt + 0.5f * _body.pGravity * Tt;
                for (float s = 0f; s <= 1.0001f; s += 0.08f)
                {
                    float tt = s * Tt;
                    _world.AddDot(new Vector3(_p.x + vx * tt, _p.y + vy * tt - 0.5f * _body.pGravity * tt * tt, 0f), 1.1f, new Color32(255, 255, 255, 230));
                }
                _world.AddRing(new Vector3(_p.x + dx, _p.y + dy, 0f), Vector3.forward, 5f, 0.5f, 16, 4, new Color32(255, 255, 255, 230), 1f);
            }
            else if (_ai.LaunchAim.HasValue)
            {
                var to = _ai.LaunchAim.Value; var colour = _ai.Misjudged ? C_AimRed : _ai.Unnoticed ? C_AimAmber : C_AimGreen;
                Vector3 a = new(_p.x, _p.y, 0f), bb = new(to.x, to.y, 0f), dir = bb - a;
                if (dir.sqrMagnitude > 1f)
                {
                    Vector3 n = new Vector3(-dir.y, dir.x, 0f).normalized * 0.3f;
                    _world.AddQuad(a + n, bb + n, bb - n, a - n, Alpha(colour, 0.85f), 1f, 0f, false);
                }
            }
        }

        Vector2 PlaneVelocity() => _p.grounded ? new Vector2(_p.v, 0f) : new Vector2(_p.vx, _p.vy);

        void DrawFx()
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i]; float age = _t - f.T0, k = age / f.Life;
                if (k >= 1f) { _fx.RemoveAt(i); continue; }
                float e = 1f - (1f - k) * (1f - k);   // ease out
                var col = Alpha(f.Color, (1f - k) * f.Color.a / 255f);
                float rad = Mathf.LerpUnclamped(f.R0, f.R1, e);
                if (f.Disc) _world.AddDisc(f.At, rad, 20, col, col, 1f, false);
                else _world.AddRing(f.At, Vector3.forward, rad, 0.5f + 0.5f * (1f - k), 20, 4, col, 1f);
            }
        }

        // ---- the 3D world's share: one instanced submission per (mesh, material), the one draw API Unity and Amoebius share.
        // (The flat world is drawn at Stage, 40 000 u below the real one: past every vessel camera's far plane,
        // check_vessel_camera_farclip.py; only the side camera looks there.)
        sealed class Batch { public Mesh Mesh; public Material Material; public Matrix4x4[] M = new Matrix4x4[32]; public int N; public Bounds Bounds; }
        readonly List<Batch> _batches = new();
        int _batchesUsed;

        void Draw3D()
        {
            foreach (var run in _rings) Draw(_torus, Matrix4x4.TRS(run.Ring.C, Quaternion.LookRotation(run.Ring.N, Mathf.Abs(run.Ring.N.y) < 0.9f ? Vector3.up : Vector3.right), Vector3.one * _vs.pVortexR), _ringMat);
            foreach (var F in _flares) Draw(_cube, Matrix4x4.TRS(F.P, Quaternion.identity, Vector3.one * 1.5f), _flareMat);
            FlushDraws();
        }

        void Draw(Mesh mesh, Matrix4x4 m, Material material)
        {
            if (!mesh || !material) return;
            Batch b = null;
            for (int i = 0; i < _batchesUsed; i++) if (_batches[i].Mesh == mesh && _batches[i].Material == material) { b = _batches[i]; break; }
            if (b == null)
            {
                if (_batchesUsed == _batches.Count) _batches.Add(new Batch());
                b = _batches[_batchesUsed++]; b.Mesh = mesh; b.Material = material; b.N = 0;
            }
            if (b.N == b.M.Length)
            {
                if (b.N >= 1023) return;   // one call's instance cap; the view never needs more of one kind
                System.Array.Resize(ref b.M, Mathf.Min(1023, b.N * 2));
            }
            // what the instance covers (its mesh's bounds at its largest scale), for culling
            Vector3 c = m.GetColumn(3);
            float scale = Mathf.Max(((Vector3)m.GetColumn(0)).magnitude, Mathf.Max(((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude));
            var box = new Bounds(c, Vector3.one * (2f * scale * (mesh.bounds.extents.magnitude + mesh.bounds.center.magnitude)));
            if (b.N == 0) b.Bounds = box; else b.Bounds.Encapsulate(box);
            b.M[b.N++] = m;
        }

        void FlushDraws()
        {
            for (int i = 0; i < _batchesUsed; i++)
            {
                var b = _batches[i]; if (b.N == 0) continue;
                var rp = new RenderParams(b.Material) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, worldBounds = b.Bounds };
                Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.M, b.N);
                b.N = 0;
            }
            _batchesUsed = 0;
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_rb == null) return;
            var box = new Rect(10f, 10f, 560f, 150f);
            GUI.Box(box, string.Empty);
            GUILayout.BeginArea(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f));
            GUILayout.Label($"GOBY & PISTOL   you fly: {(_human == Half.Goby ? "GOBY" : "PISTOL")}   AI: {aiLevel}   swap: D-pad left/right or 1/2");
            GUILayout.Label($"Pistol  launch {_p.launch}/{Mathf.RoundToInt(_body.pLaunchCharges)}   ghost {Ready(_p.ghostAt)}   snap {Ready(_p.vortexAt)}   " +
                            $"{(_p.grounded ? "on the ground" : "in the air")}   {_rb.GobyX - _p.x:0} u behind the Goby");
            GUILayout.Label($"Goby    primer {_primer}/{Mathf.RoundToInt(Config.PrimerCharges)}   flares {_flare}/{Mathf.RoundToInt(Config.FlareCharges)}   " +
                            $"primed {_primed.Count}   hot {_hot.Count}   rings {_rings.Count}");
            GUILayout.Label(_human == Half.Pistol
                ? "Pistol: hold Space / mouse / A to dive; drag (or RT + right stick) and let go to launch; F launch; G / B ghost; Q E / X Y Snap"
                : "Goby: fly the vessel as usual; 3 / D-pad up primer; 4 / D-pad down flares");
            GUILayout.Label($"view    mapped {_cntMapped}   warped {_cntWarped}   bars {_cntBars}   boxes {_cntBoxes}   tiles {_cntTiles}   segTests {_rb.SegTests}   verts {_world.VertexCount}");
            GUILayout.EndArea();
#endif
        }

        string Ready(float at) => _t >= at ? "ready" : $"{at - _t:0.0}s";
    }
}
