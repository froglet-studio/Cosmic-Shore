using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A <b>crystal wormhole</b> (Docs/CRYSTAL_WORMHOLE.md): space crystals fold the HyperSea into an
    /// <b>attractor</b> and a <b>repulsor</b> joined through their throats — fly into one and you come out of
    /// the other. Nothing about it is a surface. Each pole shrinks every length a pilot observes
    /// (<see cref="ThroatWarp"/>, through <see cref="WarpFieldRuntime"/>), which in the pilot's own lengths is a
    /// catenoid-like NECK; the throat spheres at the two necks are glued; light follows the same geometry
    /// (<see cref="CrystalWormholeView"/>, <c>CrystalWormholeLens.hlsl</c>) and so do vessels (the warp turn
    /// in <see cref="VesselTransformer"/> and the transit here). What a pilot sees is a crystal ball holding
    /// the far side's whole sky, opening into a tunnel as they shrink into it.
    ///
    /// <para><b>Through.</b> A point on one throat comes out at the ANTIPODAL point of the other — the point
    /// reflection through the pair's midpoint, <see cref="Glue"/> — and everything that goes through is turned
    /// 180° about the throat normal (<see cref="TurnThrough"/>): falling in becomes climbing out, the sideways
    /// part reverses. That is the isometry of the two necks, so the glued geometry is smooth: a pilot flying
    /// hands-off through feels nothing turn, and the camera that follows them crosses at its own point of the
    /// throat (<see cref="CameraThrough"/>) onto exactly the view it was shown of the far side.</para>
    ///
    /// <para><b>Life.</b> One clock, <see cref="Life"/>: the pair FORMS where it was opened, then the poles
    /// attract — slowly, then faster — orbiting as they close; when they touch they beat against each other
    /// (anti-phase amplitudes, quickening) and at the end of <see cref="Settings.LifeSeconds"/> they meet as
    /// equal and opposite wells and every amplitude is 0: space is flat. A world that loops re-opens the pair
    /// <see cref="Settings.ReformSeconds"/> later. Every effect rides the poles' <see cref="BlackHole.Amplitude"/>
    /// — gravity on prisms, the felt pull on vessels, the warp depth and so the lens.</para>
    ///
    /// <para>The poles are <see cref="BlackHole"/>s — the gravity-well engine — configured as smooth wells with
    /// no lens of their own; nothing here is a black hole.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalWormhole : MonoBehaviour
    {
        /// <summary>Every live crystal wormhole, oldest first (console, tests, transit resolution).</summary>
        public static readonly List<CrystalWormhole> Live = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Live.Clear();

        /// <summary>Everything a crystal wormhole is opened with, copied at <see cref="Open"/>.</summary>
        public struct Settings
        {
            /// <summary>Each pole's gravity on prisms (BlackHoleConfig: GM = strength × gmPerStrength).</summary>
            public float Strength;
            /// <summary>The glued spheres' radius, world units — the cell's <see cref="ThroatWarp"/> throat.</summary>
            public float ThroatRadius;
            /// <summary>The scale on the throat, used only when the live field is not a <see cref="ThroatWarp"/>.</summary>
            public float ThroatScale;
            /// <summary>The felt pull on vessels: k, its ceiling (× cruise), its reach (throat radii).</summary>
            public float FeltStrength, FeltCap, FeltReach;
            /// <summary>Spin axis of the wells (they drag no frame).</summary>
            public Vector3 SpinAxis;
            /// <summary>Whose vessels the throats carry (every pilot rides; a fold of the HyperSea is nobody's).</summary>
            public IReadOnlyList<IPlayer> Players;

            /// <summary>Seconds from opening until the poles meet and annihilate; 0 = they stand until retired.</summary>
            public float LifeSeconds;
            /// <summary>Seconds the pair takes to form where it was opened.</summary>
            public float FormSeconds;
            /// <summary>Separation (a fraction of the opening one) at which the poles touch and begin to annihilate.</summary>
            public float Touch;
            /// <summary>Turns the pair orbits through on its way together; amplitude beats over the annihilation.</summary>
            public float SpiralTurns, BeatCycles;
            /// <summary>How deep each beat swings the poles' amplitudes against each other (0..1).</summary>
            public float BeatDepth;
            /// <summary>Seconds after annihilating that the pair opens again; negative = never.</summary>
            public float ReformSeconds;

            /// <summary>The view: its far eye's render scale, panorama face size, proxy distance, ray-step budget.</summary>
            public float FarEyeRenderScale;
            public int PanoramaFaceSize;
            public float ProxyRadius;
            public int LensSteps;
        }

        /// <summary>Annihilation when the world it stands in retires — kept inside the cell's suction.</summary>
        const float RetireAnnihilateSeconds = 0.9f;

        /// <summary>Below this radius a throat is closed: nothing is glued, nothing goes through.</summary>
        const float MinOpenThroat = 0.5f;

        Settings _settings;
        BlackHole _attractor;
        BlackHole _repulsor;
        CrystalWormholeView _view;
        Vector3 _midpoint;          // local
        Vector3 _halfSeparation;    // local: attractor = midpoint + half, repulsor = midpoint − half (at opening)
        Vector3 _spiralAxis;        // local: the axis the pair orbits about
        float _progress;            // 0 opened .. 1 met
        float _rate;                // progress per second
        float _formed;              // seconds since (re)opening, for the formation ramp
        float _reformIn = -1f;      // seconds until it opens again, while gone
        bool _gone;
        bool _ending;               // Annihilate() was asked: run to the end regardless of LifeSeconds
        float _envelope;
        float _throat;
        Transform _adoptedBy;
        readonly Dictionary<IVessel, Vector3> _lastPos = new();

        public BlackHole Attractor => _attractor;
        public BlackHole Repulsor => _repulsor;
        public CrystalWormholeView View => _view;

        /// <summary>The glued spheres' radius right now, world units (0 = closed).</summary>
        public float Throat => _throat;

        /// <summary>The settings' throat radius: the throats' radius at full strength.</summary>
        public float FullThroat => Mathf.Max(1f, _settings.ThroatRadius);

        public float ThroatScaleFallback => _settings.ThroatScale;
        public int LensSteps => _settings.LensSteps;
        public float ProxyRadius => _settings.ProxyRadius;
        public float FarEyeRenderScale => _settings.FarEyeRenderScale;
        public int PanoramaFaceSize => _settings.PanoramaFaceSize;

        /// <summary>0 = opened (full separation), 1 = the poles have met.</summary>
        public float Progress => _progress;

        /// <summary>Formation × annihilation: 1 standing at full strength, 0 nothing.</summary>
        public float Envelope => _envelope;

        public bool IsAnnihilating => _ending || (_settings.LifeSeconds > 0f && Separation(_progress) < _settings.Touch);
        public bool IsGone => _gone;
        public bool IsOpen => !_gone && _throat >= MinOpenThroat && _attractor && _repulsor;

        /// <summary>
        /// Open a crystal wormhole on <paramref name="host"/> (its children move with it): the attractor at
        /// <paramref name="localAttractor"/>, the repulsor at <paramref name="localRepulsor"/>. Returns null (with
        /// the registry's warning) if the hole budget cannot take two more wells.
        /// </summary>
        public static CrystalWormhole Open(GameObject host, Vector3 localAttractor, Vector3 localRepulsor, Settings settings)
        {
            if (!host || !Application.isPlaying) return null;
            var wormhole = host.AddComponent<CrystalWormhole>();
            if (!wormhole.Build(localAttractor, localRepulsor, settings))
            {
                Destroy(wormhole);
                return null;
            }
            return wormhole;
        }

        bool Build(Vector3 localAttractor, Vector3 localRepulsor, Settings settings)
        {
            _settings = settings;
            _midpoint = (localAttractor + localRepulsor) * 0.5f;
            _halfSeparation = (localAttractor - localRepulsor) * 0.5f;
            var axisHint = Mathf.Abs(Vector3.Dot(_halfSeparation.normalized, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
            _spiralAxis = Vector3.Cross(_halfSeparation, axisHint).normalized;

            float throat = FullThroat;
            _attractor = SpawnWell(localAttractor, HolePolarity.Sink, "[Attractor]", throat);
            if (!_attractor) return false;
            _repulsor = SpawnWell(localRepulsor, HolePolarity.Source, "[Repulsor]", throat);
            if (!_repulsor)
            {
                _attractor.BeginDespawn();
                return false;
            }
            _attractor.Throat = _repulsor;
            _repulsor.Throat = _attractor;
            _attractor.ThroatRadius = throat;
            _repulsor.ThroatRadius = throat;
            _attractor.ConfigureFeltVesselLaw(settings.FeltStrength, settings.FeltCap, settings.FeltReach);
            _repulsor.ConfigureFeltVesselLaw(settings.FeltStrength, settings.FeltCap, settings.FeltReach);

            // Both poles shape the cell's warp field, with their live amplitudes.
            var attractor = _attractor;
            var repulsor = _repulsor;
            WarpFieldRuntime.AddPole(_attractor.transform, () => attractor ? attractor.Amplitude : 0f);
            WarpFieldRuntime.AddPole(_repulsor.transform, () => repulsor ? repulsor.Amplitude : 0f);

            Reopen();
            _view = gameObject.AddComponent<CrystalWormholeView>();
            _view.Bind(this);
            if (!Live.Contains(this)) Live.Add(this);
            return true;
        }

        BlackHole SpawnWell(Vector3 localPosition, HolePolarity polarity, string wellName, float throat)
        {
            var world = transform.TransformPoint(localPosition);
            // Size = the transit core for prisms: a prism inside half the throat is carried through.
            var well = BlackHoleRegistry.Spawn(world, _settings.Strength, Vector3.zero, _settings.SpinAxis,
                throat * 0.5f, polarity);
            if (!well) return null;
            well.name = wellName;
            well.transform.SetParent(transform, true);
            well.ConfigureSmoothWell(throat, 0f);   // the light is the field's own (CrystalWormholeView)
            well.Amplitude = 0f;
            return well;
        }

        void Reopen()
        {
            _gone = false;
            _ending = false;
            _progress = 0f;
            _formed = 0f;
            _reformIn = -1f;
            _rate = _settings.LifeSeconds > 0f ? 1f / _settings.LifeSeconds : 0f;
            ApplyLife();
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[CrystalWormhole] opened; meets in {(_settings.LifeSeconds > 0f ? _settings.LifeSeconds.ToString("F0") + "s" : "never")}.");
        }

        /// <summary>
        /// Bring the meeting forward: the poles close the rest of the way and annihilate over
        /// <paramref name="seconds"/> (negative = 12 s), from wherever they are.
        /// </summary>
        public void Annihilate(float seconds = -1f)
        {
            if (_gone) return;
            float remaining = Mathf.Max(0.05f, seconds >= 0f ? seconds : 12f);
            _rate = (1f - _progress) / remaining;
            _ending = true;
        }

        // ---- the life ------------------------------------------------------------------------

        /// <summary>The poles' separation (a fraction of the opening one) at progress p: they attract.</summary>
        public static float Separation(float p) => Mathf.Sqrt(1f - Mathf.Clamp01(p));

        /// <summary>
        /// The pair's life at progress <paramref name="p"/> (0 opened, 1 met) and formation weight
        /// <paramref name="form"/> (0..1) — pure, so it is tested: separation <c>√(1−p)</c> (slowly, then all at
        /// once), orbit angle <c>2π·turns·(1 − separation)</c>, annihilation <c>v = 1 − separation/touch</c>
        /// (0 until they touch, 1 as they meet), envelope <c>form·(1−v)^1.5</c>, and the attractor's amplitude
        /// <c>env·(1 + depth·v·sin(2π·beats·v²))</c>, the repulsor's with the beat reversed — anti-phase and
        /// quickening, so the warping and unwarping convolute, both 0 at the meeting.
        /// </summary>
        public static void Life(float p, float form, float touch, float spiralTurns, float beatCycles, float beatDepth,
            out float separation, out float spiralAngle, out float envelope, out float attractorAmplitude,
            out float repulsorAmplitude)
        {
            separation = Separation(p);
            spiralAngle = 2f * Mathf.PI * spiralTurns * (1f - separation);
            float v = touch > 0f ? Mathf.Clamp01(1f - separation / touch) : (p >= 1f ? 1f : 0f);
            float q = 1f - v;
            envelope = Mathf.Clamp01(form) * q * Mathf.Sqrt(q);
            float beat = Mathf.Clamp01(beatDepth) * v * Mathf.Sin(2f * Mathf.PI * beatCycles * v * v);
            attractorAmplitude = envelope * (1f + beat);
            repulsorAmplitude = envelope * (1f - beat);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_gone)
            {
                if (_reformIn >= 0f)
                {
                    _reformIn -= dt;
                    if (_reformIn <= 0f) Reopen();
                }
                return;
            }

            _formed += dt;
            _progress = Mathf.Min(1f, _progress + _rate * dt);
            ApplyLife();
            if (_progress >= 1f)
            {
                End();
                return;
            }
            Transits();
        }

        void ApplyLife()
        {
            float form = _settings.FormSeconds > 0f ? Mathf.Clamp01(_formed / _settings.FormSeconds) : 1f;
            form = form * form * (3f - 2f * form);
            Life(_progress, form, _settings.Touch, _settings.SpiralTurns, _settings.BeatCycles, _settings.BeatDepth,
                out float separation, out float angle, out _envelope, out float ampA, out float ampR);
            var half = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, _spiralAxis) * _halfSeparation * separation;
            if (_attractor)
            {
                _attractor.transform.localPosition = _midpoint + half;
                _attractor.Amplitude = ampA;
            }
            if (_repulsor)
            {
                _repulsor.transform.localPosition = _midpoint - half;
                _repulsor.Amplitude = ampR;
            }
            // The throats close with the envelope, and never overlap as the poles meet.
            float worldSeparation = 2f * transform.TransformVector(half).magnitude;
            _throat = Mathf.Min(FullThroat * _envelope, 0.4f * worldSeparation);
            if (_throat < MinOpenThroat) _throat = 0f;
            float throatNow = Mathf.Max(_throat, 1e-3f);
            if (_attractor) _attractor.ThroatRadius = throatNow;
            if (_repulsor) _repulsor.ThroatRadius = throatNow;
        }

        void End()
        {
            _gone = true;
            _envelope = 0f;
            _throat = 0f;
            if (_attractor) _attractor.Amplitude = 0f;
            if (_repulsor) _repulsor.Amplitude = 0f;
            _lastPos.Clear();
            CSDebug.LogVerbose(CSLogChannel.BlackHole, "[CrystalWormhole] annihilated.");
            if (_settings.ReformSeconds >= 0f) _reformIn = _settings.ReformSeconds;
            else Finish();
        }

        void Finish()
        {
            _reformIn = -1f;
            if (_attractor && !_attractor.IsDespawning) _attractor.BeginDespawn();
            if (_repulsor && !_repulsor.IsDespawning) _repulsor.BeginDespawn();
            if (_view) _view.Retire();
            Live.Remove(this);
        }

        // ---- through -------------------------------------------------------------------------

        /// <summary>Where a point on one throat comes out of the other: the point reflection through the midpoint.</summary>
        public Vector3 Glue(Vector3 point) => _attractor.transform.position + _repulsor.transform.position - point;

        /// <summary>What going through does to a direction or an orientation at the throat point whose outward
        /// normal is <paramref name="normal"/>: a half turn about it (CrystalWormholeTurnThrough).</summary>
        public static Quaternion TurnThrough(Vector3 normal) => Quaternion.AngleAxis(180f, normal);

        /// <summary>The pole nearer <paramref name="point"/>.</summary>
        public BlackHole NearerPole(Vector3 point) =>
            (point - _attractor.transform.position).sqrMagnitude <= (point - _repulsor.transform.position).sqrMagnitude
                ? _attractor : _repulsor;

        /// <summary>
        /// A pose taken through at the throat point FACING it — the near pole's throat point on the line to
        /// the pose — by the rigid map there: <c>p → Glue(x) + turn·(p − x)</c>, orientation turned. For a pose
        /// ON a throat this is exactly where it comes out; for one off it, it is where the far side is seen
        /// FROM (<see cref="CrystalWormholeView"/>'s far eye).
        /// </summary>
        public Pose Through(Pose pose)
        {
            var pole = NearerPole(pose.position).transform.position;
            Vector3 n = pose.position - pole;
            float m = n.magnitude;
            n = m > 1e-5f ? n / m : Vector3.up;
            Vector3 x = pole + n * Mathf.Max(_throat, 1e-3f);
            var turn = TurnThrough(n);
            return new Pose(Glue(x) + turn * (pose.position - x), turn * pose.rotation);
        }

        /// <summary>A camera that has reached a throat crosses at its OWN point (the follow-through's hand-over).</summary>
        public Pose CameraThrough(Pose camera)
        {
            if (!_attractor || !_repulsor) return camera;
            var pole = NearerPole(camera.position).transform.position;
            Vector3 n = camera.position - pole;
            n = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
            return new Pose(Glue(camera.position), TurnThrough(n) * camera.rotation);
        }

        /// <summary>
        /// Was the jump <paramref name="from"/> → <paramref name="to"/> a transit of some crystal wormhole? Then
        /// where it went in and came out on the throats, the turn it took, and the near throat's centre — for
        /// <see cref="TeleportContinuity"/> to cut ribbons there and carry the camera. Every machine can answer:
        /// it is read from the jump itself.
        /// </summary>
        public static bool TryResolveTransit(Vector3 from, Vector3 to, out CrystalWormhole wormhole, out Vector3 entry,
                                             out Vector3 exit, out Quaternion turn, out Vector3 nearCentre)
        {
            wormhole = null;
            entry = exit = nearCentre = default;
            turn = Quaternion.identity;
            float best = float.MaxValue;
            for (int i = 0; i < Live.Count; i++)
            {
                var w = Live[i];
                if (!w || !w._attractor || !w._repulsor) continue;
                float r = Mathf.Max(w._throat, w.FullThroat * 0.05f);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 pole = (k == 0 ? w._attractor : w._repulsor).transform.position;
                    Vector3 rel = from - pole;
                    if (rel.sqrMagnitude > 9f * r * r) continue;
                    Vector3 n = rel.sqrMagnitude > 1e-10f ? rel.normalized : Vector3.up;
                    Vector3 x = pole + n * r;
                    Vector3 y = w.Glue(x);
                    float err = (y - to).sqrMagnitude;
                    if (err > r * r || err >= best) continue;
                    best = err;
                    wormhole = w;
                    entry = x;
                    exit = y;
                    turn = TurnThrough(n);
                    nearCentre = pole;
                }
            }
            return wormhole != null;
        }

        /// <summary>
        /// Every pilot whose step this frame entered a throat comes out of the other: at the antipodal point,
        /// turned through. Only the machine that OWNS a vessel moves it (the pose write replicates).
        /// </summary>
        void Transits()
        {
            var players = _settings.Players;
            if (players == null || !IsOpen) { _lastPos.Clear(); return; }
            Vector3 a = _attractor.transform.position;
            Vector3 b = _repulsor.transform.position;
            for (int i = 0; i < players.Count; i++)
            {
                var vessel = players[i]?.Vessel;
                if (vessel == null || !vessel.IsNetworkOwner) continue;
                Vector3 cur = vessel.Transform.position;
                bool first = !_lastPos.TryGetValue(vessel, out var prev);
                _lastPos[vessel] = cur;
                if (first) continue;

                Vector3 pole;
                if (WormholeGeometry.SegmentEntersBall(prev, cur, a, _throat)) pole = a;
                else if (WormholeGeometry.SegmentEntersBall(prev, cur, b, _throat)) pole = b;
                else continue;

                Vector3 n = EntryNormal(prev, cur, pole, _throat);
                Vector3 exit = Glue(pole + n * _throat);
                vessel.SetPose(new Pose(exit, TurnThrough(n) * vessel.Transform.rotation));
                _lastPos[vessel] = vessel.Transform.position;
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[CrystalWormhole] transit through the {(pole == a ? "attractor" : "repulsor")}.");
            }
            if (_lastPos.Count > players.Count) _lastPos.Clear();
        }

        /// <summary>The outward normal where the step <paramref name="from"/> → <paramref name="to"/> first meets the ball.</summary>
        static Vector3 EntryNormal(Vector3 from, Vector3 to, Vector3 centre, float radius)
        {
            Vector3 o = from - centre;
            Vector3 s = to - from;
            float a = Mathf.Max(s.sqrMagnitude, 1e-12f);
            float b = Vector3.Dot(o, s);
            float c = o.sqrMagnitude - radius * radius;
            float disc = Mathf.Max(0f, b * b - a * c);
            float t = Mathf.Clamp01((-b - Mathf.Sqrt(disc)) / a);
            Vector3 hit = from + s * t - centre;
            return hit.sqrMagnitude > 1e-10f ? hit.normalized : Vector3.up;
        }

        /// <summary>
        /// The host's FIRST parent is the cell that adopted it; a later re-parent is that cell retiring its
        /// world — the pair annihilates inside the suction, and does not come back.
        /// </summary>
        void OnTransformParentChanged()
        {
            var parent = transform.parent;
            if (!parent) return;
            if (!_adoptedBy)
            {
                _adoptedBy = parent;
                return;
            }
            if (parent == _adoptedBy) return;
            _settings.ReformSeconds = -1f;
            if (_gone) Finish();
            else Annihilate(RetireAnnihilateSeconds);
        }

        void OnDestroy() => Live.Remove(this);
    }
}
