#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The Goby &amp; Pistol duo, playable in the REAL game before its vessel class exists (dev-only). Rides on the local
    /// vessel, which stands in for the Goby (fly the Squirrel: the Goby is a two-thumb flier on the Squirrel's numbers):
    /// its path is unrolled into the Pistol's 2D world (<see cref="PistolRibbon"/>) and the Pistol rides the ground its
    /// trail's hinges make, seen side on through a second camera. The Pistol's body, its launch, ghost and Snap (the
    /// vortex ring) act on the scene's REAL prisms; the Goby's primer and flares do too. One human flies one half and the
    /// AI flies the other; the swap is the arena's (D-pad left / right, keyboard 1 / 2, <see cref="PilotSwapGesture"/>).
    ///
    /// <para>Everything it computes is the lab's arithmetic, ported line for line and held to the lab's numbers by
    /// <c>GobyPistolMathTests</c>; every number is <see cref="GobyPistolConfigSO"/>, cloned from the Vessel Studio artifact
    /// (<c>Docs/Studios/VesselStudio/goby.html</c>). This file is only the glue to the scene: which prisms, which camera,
    /// which buttons. Open it from <c>FrogletTools ▸ Vessels ▸ Goby and Pistol (Play mode)</c>.</para>
    ///
    /// <para>Prisms are moved the sanctioned way (transform, then <see cref="Prism.NotifyPositionChanged"/>; never a
    /// super-shielded or growing one) and broken only by an active force (<see cref="Prism.Damage"/>, the game's own
    /// explosion), so mass is conserved and nothing pops. The side view draws each prism's own mesh and material at its
    /// unrolled place, turned by the ribbon there: the Pistol's world, not a copy of it.</para>
    /// </summary>
    public sealed class GobyPistolHarness : MonoBehaviour
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
        const float Pfov = 32f;                 // the lab's side camera (PFOV)
        const int GroundSamples = 96;           // the lab's GN
        static readonly Vector3 Stage = new(0f, -40000f, 0f);   // where the flat world is drawn, far from the real one

        sealed class RingRun { public VortexRing Ring; public readonly List<Prism> Prisms = new(); }
        struct Flare { public Vector3 P, V; public Prism Target; public float Life; }

        IVessel _goby; Transform _gt;
        PistolRibbon _rb; PistolBodyMath.State _p; PistolAI _ai;
        PistolBodyMath.Settings _body; VortexRing.Settings _vs; PistolAI.Settings _ais; PistolRibbon.LaySettings _lay;
        float _t, _lastSwapAt = -9f; Vector3 _lastPos; Half _human = Half.Goby; bool _weTookGoby;
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

        // the side view
        Camera _cam; float _scX, _scY; bool _scLive;
        Mesh _ground, _cube, _cylinder, _torus;
        Material _groundMat, _bodyMat, _ghostMat, _gobyMat, _primedMat, _hotMat, _ringMat, _aimMat, _flareMat;
        readonly Vector3[] _gv = new Vector3[GroundSamples * 4];

        GobyPistolConfigSO Config => config;

        /// <summary>Attach to the local vessel (the Goby's stand-in). Null when no local vessel is flying.</summary>
        public static GobyPistolHarness Attach(GobyPistolConfigSO cfg)
        {
            var vt = VesselVisionShading.LocalVessel;
            if (!vt || !vt.TryGetComponent(out IVessel vessel)) return null;
            foreach (var old in FindObjectsByType<GobyPistolHarness>(FindObjectsSortMode.None)) Destroy(old.gameObject);
            var h = new GameObject("GobyPistolHarness").AddComponent<GobyPistolHarness>();
            h.config = cfg; h.Bind(vessel, vt);
            return h;
        }

        void Bind(IVessel vessel, Transform t)
        {
            _goby = vessel; _gt = t;
            if (!config) config = ScriptableObject.CreateInstance<GobyPistolConfigSO>();
            _body = Config.Body; _vs = Config.Vortex; _ais = Config.AI; _lay = Config.Lay;
            _primer = Mathf.RoundToInt(Config.PrimerCharges); _flare = Mathf.RoundToInt(Config.FlareCharges);
            BuildView();
            ResetWorld();
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
            _scLive = false;
        }

        void OnDisable()
        {
            if (_human == Half.Pistol) SetHuman(Half.Goby);
            ReleaseAllRings();
            if (_cam) Destroy(_cam.gameObject);
            foreach (var o in new Object[] { _ground, _torus, _groundMat, _bodyMat, _ghostMat, _gobyMat, _primedMat, _hotMat, _ringMat, _aimMat, _flareMat })
                if (o) Destroy(o);
        }

        // ================================ the step (the lab's stepWorld) ================================

        void Update()
        {
            if (!_gt || _goby == null) { enabled = false; return; }
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
            BodyHits();
            StepRings(dt);
            GobyAbilities(dt, pos, f, u, r);
            StepFlares(dt);
            Expire(_primed); Expire(_hot);
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
                if (Live(prism) && PistolAI.TrySee(_rb, _p, prism.GetInstanceID(), prism.transform.position, IsPrimed(prism), Friendly(prism), reach, _ais, out var seen))
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
                if (!Live(prism)) continue;
                var m = _rb.Map(prism.transform.position, _p.x - reach - 30f, _p.x + reach + 30f);
                if (!PistolBodyMath.Touches(_p, m, Config.Vision, Config.BodyR)) continue;
                Break(prism, By.Body, PistolVelocity());
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
                    if (!Live(prism)) continue;
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
                        if (!Live(prism) || _held.Contains(prism) || prism.IsGrowing || prism.prismProperties is { IsSuperShielded: true }) continue;
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
                if (!Live(prism)) continue;
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
                    foreach (var prism in _qGoby) if (Live(prism)) _primed[prism] = _t + Config.PrimeLife;
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
            if (_goby == null || !_gt) { _human = half; return; }   // the hull is gone: nothing to hand back
            var input = _goby.VesselStatus.InputStatus;
            if (half == Half.Pistol)
            {
                // the AI takes the Goby; the human's sticks stop reaching it (they fly the Pistol now)
                _weTookGoby = !_goby.VesselStatus.AutoPilotEnabled;
                if (_weTookGoby) _goby.ToggleAIPilot(true);
                if (input != null) input.Paused = true;
            }
            else
            {
                if (_weTookGoby) _goby.ToggleAIPilot(false);
                _weTookGoby = false;
                if (input != null) input.Paused = false;
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
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return false;
            float s = (Stage.z - ray.origin.z) / ray.direction.z; if (s <= 0f) return false;
            w = ray.origin + ray.direction * s - Stage; return true;
        }

        // ================================ the side view (the lab's renderSide3D) ================================

        void BuildView()
        {
            var go = new GameObject("PistolSideCamera"); go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.Skybox; _cam.fieldOfView = Pfov; _cam.nearClipPlane = 1f; _cam.cullingMask = 1;
            _ground = new Mesh { name = "PistolGround" }; _ground.MarkDynamic();
            _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            _torus = BuildTorus(1f, 0.08f, 40, 8);
            _groundMat = Unlit(new Color(0.12f, 0.55f, 0.5f));
            _bodyMat = Unlit(new Color(1f, 0.55f, 0.24f)); _ghostMat = Unlit(new Color(0.45f, 0.3f, 0.2f));
            _gobyMat = Unlit(new Color(0.3f, 1f, 0.85f)); _primedMat = Unlit(new Color(1f, 0.78f, 0.2f)); _hotMat = Unlit(new Color(1f, 0.25f, 0.15f));
            _ringMat = Unlit(new Color(0.62f, 0.91f, 1f)); _aimMat = Unlit(Color.white); _flareMat = Unlit(new Color(1f, 0.9f, 0.5f));
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

        // the frame at the ribbon there, as the rotation that unrolls a world rotation into the flat world
        Quaternion Unroll(int k, float t)
        {
            _rb.Frame(k, t, out _, out var U, out var R);
            return Quaternion.Inverse(Quaternion.LookRotation(R, U));
        }

        void LateUpdate()
        {
            if (!_cam || _rb == null) return;
            _cam.enabled = showSideView;
            if (!showSideView) return;
            var main = Camera.main;
            _cam.depth = (main ? main.depth : 0f) + 5f;
            _cam.rect = _human == Half.Pistol ? new Rect(0f, 0f, 1f, 1f) : new Rect(0.62f, 0.03f, 0.35f, 0.35f);
            float W = Config.ViewWidth, aspect = Mathf.Max(0.2f, _cam.aspect);
            // the lab's sideCam: a fifth of a screen ahead of the Pistol, easing in height
            float tx = _p.x + 0.2f * W, gy = _rb.Ground(_p.x), ty = Mathf.Max(_p.y, gy) + 0.08f * (W / aspect);
            _scX = tx; _scY = _scLive ? Mathf.LerpUnclamped(_scY, ty, Mathf.Clamp01(Time.deltaTime * 4f)) : ty; _scLive = true;
            float dist = W * 0.5f / (Mathf.Tan(Pfov * 0.5f * Mathf.Deg2Rad) * aspect);
            _cam.fieldOfView = Pfov; _cam.farClipPlane = dist + 4000f;
            _cam.transform.SetPositionAndRotation(Stage + new Vector3(0f, _scY, -dist), Quaternion.identity);

            DrawPrisms(W);
            DrawGround(W);
            DrawActors(W);
        }

        Vector3 Flat(float x, float h, float d) => Stage + new Vector3(x - _scX, h, d);

        // prisms: their own mesh and material, at their unrolled place, turned by the ribbon there (the lab's sideList)
        void DrawPrisms(float W)
        {
            var idx = Index(); if (idx == null) return;
            float half = W * 0.75f;
            idx.QuerySphere(_p.wpos, half + 260f, _qView);
            foreach (var prism in _qView)
            {
                if (!Live(prism) || !prism.TryGetComponent(out MeshRenderer mr) || !mr.sharedMaterial) continue;
                var tr = prism.transform;
                var m = _rb.Map(tr.position, _p.x - half, _p.x + half); if (!m.Ok) continue;
                bool primed = IsPrimed(prism), faint = Mathf.Abs(m.D) > Config.Vision; if (faint && !primed) continue;
                var mesh = prism.EffectiveRenderMesh(); if (!mesh) continue;
                Vector3 at = Flat(m.X, m.H, faint ? Mathf.Clamp(m.D, -Config.Vision, Config.Vision) : m.D);
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(at, Unroll(m.K, m.T) * tr.rotation, tr.lossyScale), mr.sharedMaterial, 0, _cam);
                if (primed || IsHot(prism))
                    Graphics.DrawMesh(_cube, Matrix4x4.TRS(at + Vector3.up * 4f, Quaternion.identity, Vector3.one * 1.6f), primed ? _primedMat : _hotMat, 0, _cam);
            }
        }

        // the ground: the floor across the band and a curtain down from its far edge (the lab's gFloor / gCurtain)
        void DrawGround(float W)
        {
            if (_rb.ControlPointCount == 0) return;
            float xa = Mathf.Max(_rb.CX.Count > 0 ? _rb.CX[0] : _rb.EndX, _scX - W * 0.75f), xb = Mathf.Min(_rb.GobyX, _scX + W * 0.75f);
            if (xb <= xa) return;
            float v = Config.Vision;
            for (int k = 0; k < GroundSamples; k++)
            {
                float x = Mathf.LerpUnclamped(xa, xb, k / (float)(GroundSamples - 1)), y = _rb.Ground(x), fx = x - _scX;
                _gv[k * 4] = new Vector3(fx, y, -v); _gv[k * 4 + 1] = new Vector3(fx, y, v);
                _gv[k * 4 + 2] = new Vector3(fx, y, v); _gv[k * 4 + 3] = new Vector3(fx, y - 600f, v);
            }
            if (_ground.vertexCount != _gv.Length)
            {
                var tri = new List<int>((GroundSamples - 1) * 12);
                for (int k = 0; k < GroundSamples - 1; k++)
                    for (int s = 0; s < 4; s += 2)
                    {
                        int a = k * 4 + s, b = a + 1, c = a + 4, d = a + 5;
                        tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
                        tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);   // both faces
                    }
                _ground.Clear(); _ground.vertices = _gv; _ground.SetTriangles(tri, 0);
            }
            else _ground.vertices = _gv;
            _ground.RecalculateBounds();
            Graphics.DrawMesh(_ground, Matrix4x4.TRS(Stage, Quaternion.identity, Vector3.one), _groundMat, 0, _cam);
        }

        void DrawActors(float W)
        {
            // the Pistol: the cylinder END ON (its length runs into the screen, across the band)
            float half = Config.Vision + 4f;
            var spin = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, _t * 2f * Mathf.Rad2Deg, 0f);
            Graphics.DrawMesh(_cylinder, Matrix4x4.TRS(Flat(_p.x, _p.y, 0f), spin, new Vector3(2f * Config.BodyR, half, 2f * Config.BodyR)),
                _t < _p.ghostUntil ? _ghostMat : _bodyMat, 0, _cam);
            // the Goby, at the end of the ground
            Graphics.DrawMesh(_cube, Matrix4x4.TRS(Flat(_rb.GobyX, _rb.EndY, 0f), Quaternion.identity, Vector3.one * 6f), _gobyMat, 0, _cam);
            // vortex rings: in the real world, and mapped into the Pistol's
            foreach (var run in _rings)
            {
                var ring = run.Ring; var look = Quaternion.LookRotation(ring.N);
                Graphics.DrawMesh(_torus, Matrix4x4.TRS(ring.C, look, Vector3.one * _vs.pVortexR), _ringMat, 0);
                var m = _rb.Map(ring.C, _p.x - W, _p.x + W);
                if (m.Ok && Mathf.Abs(m.D) < Config.Vision * 3f)
                    Graphics.DrawMesh(_torus, Matrix4x4.TRS(Flat(m.X, m.H, m.D), Unroll(m.K, m.T) * look, Vector3.one * _vs.pVortexR), _ringMat, 0, _cam);
            }
            foreach (var F in _flares)
            {
                Graphics.DrawMesh(_cube, Matrix4x4.TRS(F.P, Quaternion.identity, Vector3.one * 1.5f), _flareMat, 0);
                var m = _rb.Map(F.P, _p.x - W, _p.x + W);
                if (m.Ok && Mathf.Abs(m.D) < Config.Vision * 3f)
                    Graphics.DrawMesh(_cube, Matrix4x4.TRS(Flat(m.X, m.H, m.D), Quaternion.identity, Vector3.one * 1.5f), _flareMat, 0, _cam);
            }
            // the launch aim: yours while you drag (or hold RT), the AI's when it has one
            Vector2? aim = _human == Half.Pistol ? _mouseAim ?? _padAim : _ai.LaunchAim.HasValue ? _ai.LaunchAim.Value - new Vector2(_p.x, _p.y) : (Vector2?)null;
            if (!aim.HasValue) return;
            float dx = aim.Value.x, dy = aim.Value.y, L = Mathf.Sqrt(dx * dx + dy * dy);
            if (L > _body.pLaunchRange) { dx *= _body.pLaunchRange / L; dy *= _body.pLaunchRange / L; }
            float T = Mathf.Max(0.35f, Mathf.Sqrt(dx * dx + dy * dy) / _body.pLaunchSpeed), vx = dx / T, vy = dy / T + 0.5f * _body.pGravity * T;
            for (float s = 0f; s <= 1.0001f; s += 0.08f)
            {
                float tt = s * T;
                Graphics.DrawMesh(_cube, Matrix4x4.TRS(Flat(_p.x + vx * tt, _p.y + vy * tt - 0.5f * _body.pGravity * tt * tt, 0f), Quaternion.identity, Vector3.one * 1.2f), _aimMat, 0, _cam);
            }
        }

        void OnGUI()
        {
            if (_rb == null) return;
            var box = new Rect(10f, 10f, 520f, 132f);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f));
            GUILayout.Label($"GOBY & PISTOL (dev harness)   you fly: {(_human == Half.Goby ? "GOBY" : "PISTOL")}   AI: {aiLevel}   swap: D-pad left/right or 1/2");
            GUILayout.Label($"Pistol  launch {_p.launch}/{Mathf.RoundToInt(_body.pLaunchCharges)}   ghost {Ready(_p.ghostAt)}   snap {Ready(_p.vortexAt)}   " +
                            $"{(_p.grounded ? "on the ground" : "in the air")}   {_rb.GobyX - _p.x:0} u behind the Goby");
            GUILayout.Label($"Goby    primer {_primer}/{Mathf.RoundToInt(Config.PrimerCharges)}   flares {_flare}/{Mathf.RoundToInt(Config.FlareCharges)}   " +
                            $"primed {_primed.Count}   hot {_hot.Count}   rings {_rings.Count}");
            GUILayout.Label(_human == Half.Pistol
                ? "Pistol: hold Space / mouse / A to dive; drag (or RT + right stick) and let go to launch; F launch; G / B ghost; Q E / X Y Snap"
                : "Goby: fly the vessel as usual; 3 / D-pad up primer; 4 / D-pad down flares");
            GUILayout.EndArea();
        }

        string Ready(float at) => _t >= at ? "ready" : $"{at - _t:0.0}s";
    }
}
#endif
