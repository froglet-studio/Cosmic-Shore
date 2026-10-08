using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Both of the Tether's kinds of light tether, and everything they do besides move the hull:
    /// planting anchors, hooking, cutting, drawing, and the hook / swing / release feedback. The
    /// physics itself is <see cref="AutoTetherRig"/>, <see cref="LongTetherRope"/> and
    /// <see cref="TetherMath"/>; the flight model meets this class at exactly three methods
    /// (<see cref="SolveExternal"/>, <see cref="ConstrainToRope"/> and the state properties)
    /// through <see cref="TetherVesselTransformer"/>.
    ///
    /// <para><b>Where each part runs — and why no networking of its own is needed.</b></para>
    /// <list type="bullet">
    /// <item><b>Every peer</b>: the trigger edges (<see cref="Begin"/>/<see cref="Commit"/> arrive
    /// through <c>R_VesselActionHandler</c>'s ServerRpc → ClientRpc), planting, hooking, the
    /// abeam release, cutting and drawing — all from the hull's replicated pose and speed.
    /// Anchors are ordinary trail prisms laid through <see cref="VesselPrismController.LayAt"/>,
    /// and trail prisms are not network objects: every peer lays every vessel's trail locally
    /// from replicated motion. That is exactly the replication a wake prism has.</item>
    /// <item><b>Only where the flight model runs</b> (the machine flying the hull): the forces —
    /// the transformer is switched off on every other peer, which sees the result as replicated
    /// motion.</item>
    /// <item><b>Only for the local human pilot</b>: ghost markers, the swing arc, the release
    /// line, camera shake and haptics.</item>
    /// </list>
    /// Like the Rhino's sword, a cut resolves on each peer against its own copy of the prism, so
    /// two peers whose interpolated hull positions differ by a frame can disagree about a prism at
    /// the very edge of a beam. Known, shared with the sword, recorded in TETHER.md.
    /// </summary>
    public sealed class TetherExecutor : ShipActionExecutorBase
    {
        /// <summary>Which side of the search plane a long tether fires to.</summary>
        public enum Side
        {
            Left = -1,
            Right = 1,
        }

        [Header("Config")]
        [Tooltip("Every Tether dial. Shared and stateless.")]
        [SerializeField] TetherConfigSO config;

        [Header("Audio (FMOD) — each ships EMPTY until its event is authored")]
        [Tooltip("The taut snap when a long tether hooks.")]
        [SerializeField] EventReference hookSnapEvent;
        [Tooltip("Letting go of a long tether (the fling).")]
        [SerializeField] EventReference releaseEvent;
        [Tooltip("The swing passing a half turn — the release boost is now live.")]
        [SerializeField] EventReference halfTurnChimeEvent;
        [Tooltip("Each auto-tether hooking on — the beat of the straight-line run. Keep it subtle.")]
        [SerializeField] EventReference autoSurgeEvent;

        // Injected at vessel spawn (GameObjectInjector.InjectRecursive), for the domain palette.
        [Inject] GameDataSO _gameData;

        public TetherConfigSO Config => config;

        /// <summary>The long tether holds the hull right now.</summary>
        public bool LongHooked => _rope.Hooked;

        /// <summary>A long-tether trigger is held (hooked, or waiting for a target in reach).</summary>
        public bool LongArmed => _armed;

        /// <summary>An auto-tether is taut and the long tether is not in charge.</summary>
        public bool AnyAutoTaut => !_rope.Hooked && _auto.AnyTaut;

        /// <summary>Seconds since the last long-tether release (the glide window).</summary>
        public float SecondsSinceRelease => Time.time - _releaseTime;

        /// <summary>Swing progress toward the half turn, 0..1, for a HUD that wants it.</summary>
        public float SwingProgress01 => _rope.Hooked ? TetherMath.HalfTurnProgress01(_rope.Swept) : 0f;

        const float CutCandidateExtent = 8f;   // as Projectile.SweepPrismsAlong: generous, then refined

        IVesselStatus _status;
        ActionExecutorRegistry _registry;
        TetherVesselTransformer _transformer;
        TetherVisuals _visuals;

        readonly AutoTetherRig _auto = new();
        readonly LongTetherRope _rope = new();

        // The prism each auto-tether slot is anchored to (aligned with the rig's slots), so a beam
        // never cuts its own anchors and the long tether never hooks a live auto-anchor.
        readonly Prism[] _anchorPrisms = new Prism[AutoTetherRig.Capacity];
        readonly int[] _anchorIds = new int[AutoTetherRig.Capacity];

        bool _armed;
        Side _armedSide;
        Transform _hookTarget;
        Prism _hookPrism;
        Vector3 _anchorVelocity;
        Vector3 _hookStartRadial, _hookStartTangent;
        bool _halfTurnChimed;
        float _pendingFlash = -1f;
        float _releaseTime = -1e4f;

        Vector3 _lastHull;
        bool _hasLastHull;

        float _ghostTimer;
        readonly bool[] _ghostValid = new bool[2];
        readonly Vector3[] _ghostPositions = new Vector3[2];

        readonly List<Prism> _query = new(256);
        HashSet<Prism> _touchedNow = new();
        HashSet<Prism> _touchedLast = new();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            if (!_registry) _registry = GetComponentInParent<ActionExecutorRegistry>(true);
            _transformer = shipStatus != null ? shipStatus.VesselTransformer as TetherVesselTransformer : null;
            _visuals ??= new TetherVisuals(transform, config);
            ResetLines();
        }

        /// <summary>Drop every line, disarm, forget the hook. Respawns, swaps and disables call it,
        /// so an interrupted swing can never strand a rope (or its forces) on the next life.</summary>
        public void ResetLines()
        {
            _armed = false;
            _rope.Release();
            _auto.Reset();
            for (int i = 0; i < AutoTetherRig.Capacity; i++) { _anchorPrisms[i] = null; _anchorIds[i] = 0; }
            _hookTarget = null;
            _hookPrism = null;
            _pendingFlash = -1f;
            _hasLastHull = false;
            _releaseTime = -1e4f;
            _touchedNow.Clear();
            _touchedLast.Clear();
        }

        void OnDisable()
        {
            ResetLines();
            _visuals?.HideAll();
        }

        // ------------------------------------------------------------------ trigger edges (every peer)

        /// <summary>
        /// Trigger PRESS. Arms the long tether on that side; it hooks the best target in reach this
        /// frame, or the moment one appears. Pressing the other trigger mid-swing lets go of the
        /// current swing first — a release with its full fling — so alternating triggers chain.
        /// </summary>
        public void Begin(Side side)
        {
            if (!config || _status == null) return;
            if (_rope.Hooked && _rope.Side != (int)side) Release();
            _armed = true;
            _armedSide = side;
        }

        /// <summary>Trigger RELEASE. Lets go of the long tether (with the fling) if it is this
        /// trigger's. A release of a trigger whose swing was already handed over does nothing.</summary>
        public void Commit(Side side)
        {
            if (!_armed || _armedSide != side) return;
            _armed = false;
            if (_rope.Hooked) Release();
        }

        // ------------------------------------------------------------------ flight-model door (owner)

        /// <summary>
        /// The tethers' velocity change this frame (× dt), called by the transformer from inside
        /// the vector move step. While hooked: the long tether's arc (reel + rigid rope). Otherwise:
        /// the auto-tethers' springs, never pulling speed past <c>autoSpeedTarget</c> × the
        /// throttle's own cruise target.
        /// </summary>
        public Vector3 SolveExternal(Vector3 hull, Vector3 velocity, float throttleTarget, float dt)
        {
            if (!config || dt <= 0f) return Vector3.zero;

            if (_rope.Hooked)
            {
                var tuning = config.SwingTuningAt(velocity.magnitude);
                return _rope.Step(hull, velocity, ReelInput(), tuning, dt) - velocity;
            }

            if (!_auto.AnyLive) return Vector3.zero;
            Vector3 pull = _auto.Solve(hull, velocity, config.AutoStiffness, config.AutoDamping, dt);
            float cap = config.AutoSpeedTarget * Mathf.Max(0f, throttleTarget);
            return TetherMath.LimitSpeedGain(velocity, pull, cap);
        }

        /// <summary>The hull put back on the long tether (rigid both ways: the constraint outward,
        /// the reel inward). Identity when not hooked.</summary>
        public Vector3 ConstrainToRope(Vector3 hull)
            => _rope.Hooked ? TetherMath.ConstrainToRope(hull, _rope.Anchor, _rope.Length) : hull;

        /// <summary>Stick up (the nose-up command, so it honours InvertY) reels in; stick down lets
        /// out. Autopilot writes no reel.</summary>
        float ReelInput()
        {
            if (_status == null || _status.Player == null || _status.AutoPilotEnabled) return 0f;
            var input = _status.InputStatus;
            return input == null ? 0f : Mathf.Clamp(-input.YSum, -1f, 1f);
        }

        bool Simulating => _transformer && _transformer.LastSolveFrame >= Time.frameCount - 1;

        bool IsLocalPilot =>
            _status != null && _status.Player != null && _status.IsLocalUser && !_status.AutoPilotEnabled;

        // ------------------------------------------------------------------ per frame (every peer)

        void Update()
        {
            if (!config || _status == null || _visuals == null) return;
            Transform hullTransform = _status.Transform;
            if (!hullTransform) return;

            float dt = Time.deltaTime;
            Vector3 hull = hullTransform.position;
            Vector3 forward = hullTransform.forward, right = hullTransform.right, up = hullTransform.up;
            Vector3 hullVelocity = _status.Course * _status.Speed;
            Color team = TeamColour();

            _visuals.BeginFrame();
            (_touchedLast, _touchedNow) = (_touchedNow, _touchedLast);
            _touchedNow.Clear();

            // --- long tether ---
            if (_rope.Hooked)
            {
                FollowHookTarget(dt);
                _rope.Track(hull);
                if (!_halfTurnChimed && _rope.Swept >= TetherMath.HalfTurnRadians)
                {
                    _halfTurnChimed = true;
                    Play(halfTurnChimeEvent, hull);
                }
            }
            else if (_armed)
            {
                TryHook(hull, forward, right, up, hullVelocity, team);
            }

            // --- auto-tethers: paused while the long tether holds ---
            if (_rope.Hooked)
            {
                if (_auto.AnyLive) _auto.ReleaseAll();
            }
            else
            {
                _auto.ReleasePassed(hull, forward);
                if (CanPlant() && _auto.TickCadence(dt, config.AnchorPeriod))
                    Plant(hull, forward, right, up, team);
                if (!Simulating) _auto.UpdateTension(hull);
            }

            // --- cut and draw every live line ---
            for (int i = 0; i < AutoTetherRig.Capacity; i++)
            {
                var line = _auto[i];
                if (!line.Live) { _anchorPrisms[i] = null; continue; }
                CutAlong(line.Id, line.Anchor, Vector3.zero, hull, hullVelocity, up, right, null);
                _visuals.Beam(line.Id, hull, line.Anchor, line.Tension01, hero: false, team);
            }

            if (_rope.Hooked)
            {
                CutAlong(TetherVisuals.LongKey, _rope.Anchor, _anchorVelocity, hull, hullVelocity, up, right, _hookPrism);
                float spin01 = config.MaxSpinRevPerSec > 0f
                    ? Mathf.Clamp01(TetherMath.RevolutionsPerSecond(_rope.SpinRate(_status.Speed)) / config.MaxSpinRevPerSec)
                    : 1f;
                _visuals.Beam(TetherVisuals.LongKey, hull, _rope.Anchor, spin01, hero: true, team);
                if (_pendingFlash >= 0f)
                {
                    _visuals.Flash(TetherVisuals.LongKey, _pendingFlash);
                    _pendingFlash = -1f;
                }
            }

            // --- local pilot's instruments ---
            if (IsLocalPilot) DrawInstruments(hull, forward, right, up, team, dt);

            _lastHull = hull;
            _hasLastHull = true;
            _visuals.EndFrame(dt, hull);
        }

        // ------------------------------------------------------------------ auto-tethers

        bool CanPlant()
        {
            var controller = _status.VesselPrismController;
            return controller && controller.CanLay
                && _status.Speed >= config.MinPlantSpeed
                && !_status.IsStationary && !_status.IsTranslationRestricted;
        }

        void Plant(Vector3 hull, Vector3 forward, Vector3 right, Vector3 up, Color team)
        {
            int side = _auto.NextSide;
            Vector3 anchor = TetherMath.AnchorPoint(hull, forward, right, _status.Speed,
                                                    config.AnchorLead, config.AnchorAngle, side);

            // A real prism of this hull's own trail — same pool, owner, team and shield rules as
            // its wake — so the anchors stay in the world for everyone else to deal with.
            Prism prism = null;
            var controller = _status.VesselPrismController;
            if (controller && SafeLookRotation.TryGet(anchor - hull, up, out var rotation, this, logError: false))
                prism = controller.LayAt(anchor, rotation, config.AnchorScale);

            int id = _auto.Attach(hull, anchor, config.AutoRestFraction);
            for (int i = 0; i < AutoTetherRig.Capacity; i++)
            {
                if (_auto[i].Id != id) continue;
                _anchorPrisms[i] = prism;
                _anchorIds[i] = id;
                break;
            }

            _visuals.Bracket(anchor, hull - anchor, team, 0.6f);
            Play(autoSurgeEvent, hull);
        }

        bool IsLiveAutoAnchor(Prism prism)
        {
            for (int i = 0; i < AutoTetherRig.Capacity; i++)
                if (_anchorPrisms[i] == prism && _auto[i].Live && _auto[i].Id == _anchorIds[i]) return true;
            return false;
        }

        // ------------------------------------------------------------------ long tether

        void TryHook(Vector3 hull, Vector3 forward, Vector3 right, Vector3 up, Vector3 hullVelocity, Color team)
        {
            var window = config.HookWindowAt(_status.Speed);
            if (!FindBest((int)_armedSide, hull, forward, right, up, window, out Vector3 anchor, out Transform target, out Prism prism))
                return;

            Vector3 hooked = _rope.Hook(hull, hullVelocity, anchor, forward, (int)_armedSide);
            if (Simulating && hooked.sqrMagnitude > 1e-6f) _transformer.RedirectOnto(hooked);

            _hookTarget = target;
            _hookPrism = prism;
            _anchorVelocity = Vector3.zero;
            _halfTurnChimed = false;
            _hookStartRadial = (hull - anchor).normalized;
            _hookStartTangent = hooked.sqrMagnitude > 1e-6f ? hooked.normalized : forward;

            _auto.ReleaseAll();

            // The taut snap — as big as the redirect it caused.
            float yank = _rope.Yank01;
            _pendingFlash = Mathf.Max(0.35f, yank);
            _visuals.Bracket(anchor, hull - anchor, team, 1f);
            Play(hookSnapEvent, anchor);
            if (IsLocalPilot)
            {
                ShakeCamera(config.HookShakeIntensity * (0.3f + 0.7f * yank), config.HookShakeDuration);
                HapticController.PlayBind(config.HookHapticStrength * Mathf.Max(0.3f, yank));
            }
        }

        /// <summary>Let go of the long tether: the fling (nose + course snapped to the exit
        /// velocity, the half-turn boost paid) where the flight model runs, the retract everywhere.</summary>
        void Release()
        {
            if (!_rope.Hooked) return;
            float swept = _rope.Swept;
            _rope.Release();
            _releaseTime = Time.time;

            if (Simulating)
            {
                float exit = TetherMath.ReleaseSpeed(_transformer.FlightSpeed, swept, config.ReleaseBoost, config.MaxSpeed);
                _transformer.Fling(_status.Course, exit);
            }

            _hookTarget = null;
            _hookPrism = null;
            _auto.Reset();   // the run resumes with a fresh anchor straight away
            for (int i = 0; i < AutoTetherRig.Capacity; i++) _anchorPrisms[i] = null;

            Transform hullTransform = _status.Transform;
            if (hullTransform) Play(releaseEvent, hullTransform.position);
        }

        /// <summary>Follow a target that moves (a drifting crystal). A prism that has been
        /// destroyed is let go of as a TARGET — the rope stays hooked on the point where it was.</summary>
        void FollowHookTarget(float dt)
        {
            _anchorVelocity = Vector3.zero;
            if (!_hookTarget) return;
            if (_hookPrism && _hookPrism.destroyed) { _hookTarget = null; _hookPrism = null; return; }
            if (!_hookTarget.gameObject.activeInHierarchy) { _hookTarget = null; _hookPrism = null; return; }

            Vector3 now = _hookTarget.position;
            if (dt > 0f) _anchorVelocity = (now - _rope.Anchor) / dt;
            _rope.MoveAnchor(now);
        }

        bool FindBest(int side, Vector3 hull, Vector3 forward, Vector3 right, Vector3 up, in TetherMath.HookWindow window,
                      out Vector3 anchor, out Transform target, out Prism prism)
        {
            anchor = default;
            target = null;
            prism = null;
            float best = float.NegativeInfinity;

            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                index.QuerySphere(hull, window.MaxDistance, _query);
                for (int i = 0; i < _query.Count; i++)
                {
                    var p = _query[i];
                    if (!p || p.destroyed || IsLiveAutoAnchor(p)) continue;
                    Vector3 pos = p.transform.position;
                    if (!TetherMath.TryScoreHook(hull, forward, right, up, side, pos, window, out float score)) continue;
                    if (score <= best) continue;
                    best = score;
                    anchor = pos;
                    target = p.transform;
                    prism = p;
                }
            }

            if (config.HookCrystals)
            {
                var crystals = Crystal.Active;
                for (int i = 0; i < crystals.Count; i++)
                {
                    var c = crystals[i];
                    if (!c || !c.isActiveAndEnabled) continue;
                    Vector3 pos = c.transform.position;
                    if (!TetherMath.TryScoreHook(hull, forward, right, up, side, pos, window, out float score)) continue;
                    if (score <= best) continue;
                    best = score;
                    anchor = pos;
                    target = c.transform;
                    prism = null;
                }
            }

            return !float.IsNegativeInfinity(best);
        }

        // ------------------------------------------------------------------ cutting (every peer)

        /// <summary>
        /// The light sword. Everything hostile the beam sweeps through between last frame and this
        /// one is cut — the Rhino sword's rules: ordinary cutting is UNGATED (no cooldown, no
        /// stance); a shielded prism loses its shield and survives (<see cref="Prism.Damage"/>'s
        /// own rule); a super-shielded prism BINDS (<see cref="Prism.AbsorbSuperShieldHit"/>), as
        /// it does against a sword that is not energized — a tether has no energy meter to
        /// energize. Own-domain mass, this hull's live anchors and the hooked prism are never cut.
        ///
        /// Contact is ENTER-only, like the sword's trigger: a prism that was already touching the
        /// beam last frame is not hit again, so a shield the beam just popped is not cut through on
        /// the next frame by the same continuous contact.
        ///
        /// Contact velocity is the point's own velocity on a rigid segment pivoting on its anchor
        /// (<see cref="TetherMath.RopePointVelocity"/>) — fastest at the hull — and it feeds the
        /// slice plane and the debris exactly as the sword's does (× restitution, speed-limited).
        /// </summary>
        void CutAlong(int key, Vector3 anchor, Vector3 anchorVelocity, Vector3 hull, Vector3 hullVelocity,
                      Vector3 up, Vector3 right, Prism exclude)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || _status.Player == null) return;

            Domains own = _status.Domain;
            string pilot = _status.PlayerName;
            Vector3 previous = _hasLastHull ? _lastHull : hull;
            float swept = (hull - previous).magnitude;
            int samples = Mathf.Clamp(Mathf.CeilToInt(swept / Mathf.Max(0.1f, config.CutRadius)), 1, config.MaxSweepSamples);

            for (int s = 0; s < samples; s++)
            {
                Vector3 h = samples == 1 ? hull : Vector3.Lerp(previous, hull, (s + 1f) / samples);
                index.QuerySegment(anchor, h, config.CutRadius + CutCandidateExtent, _query);
                Vector3 ab = h - anchor;
                float abLenSq = ab.sqrMagnitude;

                for (int i = 0; i < _query.Count; i++)
                {
                    var prism = _query[i];
                    if (!prism || prism.destroyed || prism == exclude) continue;
                    if (prism.Domain == own || IsLiveAutoAnchor(prism)) continue;

                    Vector3 pos = prism.transform.position;
                    float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(pos - anchor, ab) / abLenSq) : 0f;
                    Vector3 cutPoint = anchor + ab * t;
                    float contact = config.CutRadius + 0.5f * prism.transform.lossyScale.magnitude;
                    if ((pos - cutPoint).sqrMagnitude > contact * contact) continue;

                    if (!_touchedNow.Add(prism) || _touchedLast.Contains(prism)) continue;
                    Cut(prism, key, t, cutPoint, ab, anchorVelocity, hullVelocity, up, right, own, pilot);
                }
            }
        }

        void Cut(Prism prism, int key, float t, Vector3 cutPoint, Vector3 axis, Vector3 anchorVelocity,
                 Vector3 hullVelocity, Vector3 up, Vector3 right, Domains own, string pilot)
        {
            Vector3 velocity = TetherMath.RopePointVelocity(hullVelocity, anchorVelocity, t);

            if (prism.prismProperties is { IsSuperShielded: true })
            {
                prism.AbsorbSuperShieldHit(velocity.magnitude);
                return;
            }

            // Slice plane: the plane the beam swept through the prism (the sword's
            // Cross(bladeAxis, contactVelocity)), falling back when the beam moves along itself.
            Vector3 normal = Vector3.Cross(axis, velocity);
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(axis, up);
            if (normal.sqrMagnitude < 1e-6f) normal = right;

            bool wasShielded = prism.prismProperties is { IsShielded: true };
            prism.Slice(velocity * config.Restitution, own, pilot, cutPoint, normal.normalized,
                        debrisSpeedLimit: config.DebrisSpeedLimit);
            if (prism.destroyed || wasShielded) _visuals.Spark(key, t);
        }

        // ------------------------------------------------------------------ local pilot's instruments

        void DrawInstruments(Vector3 hull, Vector3 forward, Vector3 right, Vector3 up, Color team, float dt)
        {
            if (_rope.Hooked)
            {
                _visuals.SwingArc(_rope.Anchor, _hookStartRadial, _hookStartTangent, _rope.Length, _rope.Swept, team);

                float speed = _transformer ? _transformer.FlightSpeed : _status.Speed;
                float exit = TetherMath.ReleaseSpeed(speed, _rope.Swept, config.ReleaseBoost, config.MaxSpeed);
                _visuals.ReleaseLine(hull, _status.Course, exit * config.ReleaseLineSeconds,
                                     _rope.Swept >= TetherMath.HalfTurnRadians);
                return;
            }

            // Ghost markers: the best target on each side, before (or while) a trigger is held.
            _ghostTimer -= dt;
            if (_ghostTimer <= 0f || _armed)
            {
                _ghostTimer = config.GhostSearchInterval;
                var window = config.HookWindowAt(_status.Speed);
                for (int s = 0; s < 2; s++)
                    _ghostValid[s] = FindBest(s == 0 ? -1 : 1, hull, forward, right, up, window,
                                              out _ghostPositions[s], out _, out _);
            }

            var cam = Camera.main;
            Vector3 viewer = cam ? cam.transform.position : hull - forward * 10f;
            for (int s = 0; s < 2; s++)
            {
                if (!_ghostValid[s]) continue;
                bool armedHere = _armed && (int)_armedSide == (s == 0 ? -1 : 1);
                Color c = Color.Lerp(team, Color.white, armedHere ? 0.6f : 0.25f);
                _visuals.Ghost(s, _ghostPositions[s], viewer, c);
            }
        }

        // ------------------------------------------------------------------ feedback plumbing

        Color TeamColour()
        {
            var themes = _gameData ? _gameData.ThemeManagerData : null;
            var colours = themes ? themes.ColorSet : null;
            if (!colours || _status.Player == null) return new Color(0.4f, 0.85f, 1f, 1f);
            return colours.GetDomainSignalColor(_status.Domain);
        }

        void Play(EventReference sound, Vector3 position)
        {
            if (sound.IsNull || !_registry) return;
            var audio = _registry.AudioSystem;
            if (audio) audio.PlaySFXEvent(sound, position);
        }

        static void ShakeCamera(float intensity, float duration)
        {
            var manager = CameraManager.Instance;
            if (!manager) return;
            if (manager.GetActiveController() is CustomCameraController controller && controller)
                controller.Shake(intensity, duration);
        }
    }
}
