using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the WAKE: a vessel travelling fast enough drags a travelling ripple through
    /// the mass around its recent path — most visibly the RAILS of the ribbon it is laying, which
    /// run either side of the disturbance. Mass near the path swells away from it and shrinks back
    /// toward it in a wave that streams backward, so the crests hold still in the world and the
    /// ship flies out from under them.
    ///
    /// <para><b>"Near the path" means near, not ON it.</b> The map is a STRAIN, so displacement is
    /// <c>r·E</c> and the axis itself is a fixed point: mass lying exactly along the path barely
    /// moves, and what ripples is the mass standing off it. That is a good fit for a trail — a
    /// vessel that lays two rails (the Squirrel's <c>Gap 18.5</c> puts them ±9.6 u out) has its
    /// ribbon exactly where the strain is strongest — and it is why the REACH has to cover that
    /// separation: authored too small, the rails sit past the radial falloff, where the wake is
    /// exactly zero and the ship leaves no visible trace at all.</para>
    ///
    /// It does exactly two things per frame, and they are different KINDS of thing:
    ///
    /// <para><b>1. The bank (the animation).</b> A small set of global shader uniforms, published
    /// once per frame and nothing else. There is no per-prism animation work of any kind — no
    /// trigger volume, no material writes, no per-instance overrides. The deformation runs on the
    /// GPU in <c>PrismWake.hlsl</c>, wired LAST on the vertex chain of both live-prism graphs.
    /// "Where is that hull, which way is it pointing, and how far through the wave is it" is live
    /// gameplay data that changes every frame for every prism, so it can never be a per-prism
    /// stamp, and a CPU pass that updates each prism's material is exactly what the clock-material
    /// law forbids. The law's sanctioned shape for this case (Docs/PRISM_ANIMATION.md §1, §4.7) is
    /// a global uniform: O(1) writes per frame that every prism reads.</para>
    ///
    /// <para><b>2. Residency (a state change).</b> A prism is 24 triangles, and a deformation is
    /// only as smooth as the surface it moves. So the prisms inside a live wake's own volume are
    /// swapped to <see cref="HighPolyPrismMesh"/>, the identical solid at ~1,700 triangles,
    /// through the platform's own shared-mesh handoff (<c>Prism.SetRenderMeshOverride</c>). That is
    /// NOT an exception to the clock-material law: a mesh override is FINAL at the instant it is
    /// applied, exactly like a shield engaging — a state change, not an animation — and it is
    /// invisible because the swap happens strictly outside the volume the ripple can move anything
    /// (<c>PrismWakeConfigSO.ResidencyMargin</c>). The mesh is SHARED, so the resident prisms stay
    /// in one instanced batch rather than minting a mesh and a draw call each.</para>
    ///
    /// <para><b>The bank's shape.</b> One slot per wake: the hull's centre and radius, the wake
    /// axis and the wave's phase, and the four derived scalars the shader would otherwise have to
    /// re-derive. Everything a vessel contributes that varies per vessel lives in the slot;
    /// everything that is a property of the FEEL lives in the params. The centre is sampled HERE,
    /// in LateUpdate, off the registered Transform — after every transformer's Update has moved
    /// its vessel — so a hull at 400 u/s is never published a frame stale.</para>
    ///
    /// <para><b>Not a platform law, and deliberately not local-pilot-gated.</b> A wake is a thing
    /// OTHER pilots see you leaving behind you — the same argument the vessel tail is built on — so
    /// every vessel publishes one, on every machine. <c>VesselStatus.Speed</c> and
    /// <c>VesselStatus.Course</c> both replicate (<c>VesselController.n_Speed</c> / <c>n_Course</c>),
    /// so a remote replica's wake is driven by the same numbers its owner is driving.</para>
    /// </summary>
    public static class PrismWake
    {
        /// <summary>
        /// How many vessels can leave a wake at once. Mirrors <c>PRISM_WAKE_SLOTS</c> in
        /// <c>PrismWake.hlsl</c> — change both together, since the shader's arrays are declared at
        /// this length. Four is the largest roster any arcade mode seats; <see cref="Flush"/> keeps
        /// the strongest if it ever overflows.
        /// </summary>
        public const int Slots = 4;

        public const string ConfigResourcePath = "PrismWakeConfig";

        static readonly int CentreId = Shader.PropertyToID("_PrismWakeCentre");
        static readonly int AxisId = Shader.PropertyToID("_PrismWakeAxis");
        static readonly int ShapeId = Shader.PropertyToID("_PrismWakeShape");
        static readonly int ParamsId = Shader.PropertyToID("_PrismWakeParams");

        /// <summary>
        /// One wake, as reported this frame. <see cref="Frame"/> is what makes the bank
        /// self-cleaning: a source that stops reporting — its vessel destroyed, swapped, slowed
        /// down, or its scene unloaded — has its slot dropped on the next flush with nothing
        /// needing to have called <see cref="Clear"/>. A wake that outlives the ship that made it
        /// is the one failure mode a registry like this actually has.
        /// </summary>
        struct Source
        {
            public Transform Hull;
            public float Radius;
            public Vector3 Axis;        // unit, pointing BEHIND the ship
            public float Phase;         // radians, integrated by the source
            public float Strength;
            public int Frame;
        }

        static readonly Dictionary<int, Source> _sources = new();
        static readonly List<int> _stale = new();

        // Always sent at full length: Unity binds an array global at the length of its first write,
        // so a short write later would silently leave the tail of the previous frame's bank live.
        // Unused slots are zeroed and _PrismWakeParams.z is the real bound.
        static readonly Vector4[] _centre = new Vector4[Slots];
        static readonly Vector4[] _axis = new Vector4[Slots];
        static readonly Vector4[] _shape = new Vector4[Slots];
        static int _publishedCount;

        // Residency: the prisms currently holding the high-poly mesh, and this frame's candidate
        // set. Both are reused every frame so the pass allocates nothing after the first.
        static readonly HashSet<Prism> _resident = new();
        static readonly HashSet<Prism> _wanted = new();
        static readonly List<Prism> _query = new();
        static readonly List<Prism> _candidates = new();
        static readonly List<Prism> _evict = new();
        static Vector3 _sortOrigin;
        static readonly System.Comparison<Prism> _byDistance = CompareByDistance;

        static PrismWakeConfigSO _config;
        static bool _configResolved;

        /// <summary>True while any vessel is publishing a live wake.</summary>
        public static bool IsActive => _publishedCount > 0;

        /// <summary>How many prisms currently hold the high-poly mesh (diagnostics, tests).</summary>
        public static int ResidentPrismCount => _resident.Count;

        /// <summary>
        /// Tuning (the ripple, the speed window, the residency budget and the eases). Falls back to
        /// the SO's own defaults when no <c>Resources/PrismWakeConfig</c> asset exists, so the
        /// feature works with no authoring.
        /// </summary>
        public static PrismWakeConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<PrismWakeConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<PrismWakeConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>Forget the cached config so the next read reloads it (editor tooling).</summary>
        public static void InvalidateConfig() => _configResolved = false;

        // ---------------- Derived geometry (ONE copy of each formula) ----------------
        //
        // The source integrates its own phase and the bank packs the shader's slot, and both need
        // the same three numbers. Deriving them here rather than at each call site is what keeps a
        // retune of ReachHullRadii or WavesPerTrain from moving one of them and not the other.

        /// <summary>How far OUT from the path the ripple reaches, world units.</summary>
        public static float ReachFor(float hullRadius, PrismWakeConfigSO config) =>
            hullRadius * config.ReachHullRadii;

        /// <summary>How far BEHIND the ship the wave train runs, world units.</summary>
        public static float TrainLengthFor(float hullRadius, PrismWakeConfigSO config) =>
            hullRadius * config.TrainHullRadii;

        /// <summary>The wave's spatial frequency along the axis, radians per world unit.</summary>
        public static float WavenumberFor(float hullRadius, PrismWakeConfigSO config)
        {
            float length = TrainLengthFor(hullRadius, config);
            return length > 0f ? 2f * Mathf.PI * config.WavesPerTrain / length : 0f;
        }

        /// <summary>
        /// How fast this ship's phase advances, radians per second. At <c>PhaseTravel == 1</c> this
        /// is exactly the rate that holds a crest STILL in the world while the ship flies out from
        /// under it — the ship covers <c>speed</c> units of axis per second and the wave's argument
        /// is <c>ψ − k·x</c>, so the two cancel.
        /// </summary>
        public static float PhaseRateFor(float hullRadius, float speed, PrismWakeConfigSO config) =>
            WavenumberFor(hullRadius, config) * Mathf.Max(0f, speed) * config.PhaseTravel;

        /// <summary>
        /// Report a wake. <paramref name="sourceId"/> identifies the reporting vessel (its source
        /// component's instance id) so one vessel can only ever occupy one slot across a swap or a
        /// re-initialise. <paramref name="hull"/> is sampled at flush time, not now.
        ///
        /// Must be called every frame (from Update — the flush runs in LateUpdate) while the wake
        /// is up; a slot that stops being reported is dropped by the next <see cref="Flush"/>.
        /// </summary>
        public static void Publish(int sourceId, Transform hull, float radius, Vector3 axis,
            float phase, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            float axisLenSq = axis.sqrMagnitude;
            if (hull == null || radius <= 0f || strength01 <= 0.001f || axisLenSq <= 1e-6f)
            {
                Clear(sourceId);
                return;
            }

            _sources[sourceId] = new Source
            {
                Hull = hull,
                Radius = radius,
                // Normalised HERE so the shader's slot is always unit and its own axis test can be
                // a cheap rejection of an unset slot rather than a renormalise of a direction that
                // may carry no direction at all.
                Axis = axis / Mathf.Sqrt(axisLenSq),
                Phase = phase,
                Strength = strength01,
                Frame = Time.frameCount,
            };
        }

        /// <summary>
        /// Drop a wake. Idempotent, and not strictly required — the frame stamp collects an
        /// abandoned slot anyway — but calling it on release retires the wake on the same frame
        /// instead of the next one.
        /// </summary>
        public static void Clear(int sourceId) => _sources.Remove(sourceId);

        /// <summary>
        /// Pack this frame's reported wakes into the shader's bank and reconcile which prisms hold
        /// the high-poly mesh. Called once per frame from <see cref="Driver"/> in LateUpdate —
        /// after every source's Update has reported and after the vessels those sources ride have
        /// moved, and before anything renders.
        /// </summary>
        public static void Flush()
        {
            int frame = Time.frameCount;

            // Collect slots nobody reported this frame — and any whose hull was destroyed since.
            // Deferred into a list because the dictionary cannot be mutated while it is walked.
            _stale.Clear();
            foreach (var kv in _sources)
                if (kv.Value.Frame != frame || kv.Value.Hull == null)
                    _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
                _sources.Remove(_stale[i]);

            var config = Config;
            bool enabled = config.Enabled && config.IsSane;

            int count = 0;
            if (enabled)
            {
                foreach (var kv in _sources)
                {
                    var src = kv.Value;
                    if (count < Slots)
                    {
                        Write(count++, src, config);
                        continue;
                    }

                    // Reachable the moment five vessels boost at once, which several modes seat.
                    // Evict the WEAKEST rather than whoever the dictionary happened to enumerate
                    // last: a bank that dropped by enumeration order would show a different set of
                    // wakes on each machine for the same match.
                    int weakest = 0;
                    for (int i = 1; i < Slots; i++)
                        if (_shape[i].x < _shape[weakest].x)
                            weakest = i;
                    if (src.Strength > _shape[weakest].x)
                        Write(weakest, src, config);
                }
            }

            for (int i = count; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _axis[i] = Vector4.zero;
                _shape[i] = Vector4.zero;
            }

            ReconcileResidency(config, enabled, count);

            ReportState(config, enabled, count);

            // Nothing to say and nothing said last frame: skip the writes entirely, so a match in
            // which nobody is moving fast costs this system literally nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(AxisId, _axis);
            Shader.SetGlobalVectorArray(ShapeId, _shape);
            // z is the shader's MASTER sentinel: 0 means "the loop does not execute".
            Shader.SetGlobalVector(ParamsId,
                new Vector4(config.Amplitude, config.RadialExponent, count, 0f));
            _publishedCount = count;
        }

        /// <summary>
        /// Pack one wake. The three derived scalars (reach, train length, wavenumber) are computed
        /// HERE rather than in the shader: they are pure functions of the hull's radius and the
        /// config, the CPU already has both, and deriving them once means the phase the source
        /// integrates and the wave the shader draws cannot fall out of step.
        /// </summary>
        static float _nextReportTime;

        /// <summary>
        /// Say, once a second on <see cref="CSLogChannel.PrismRuntime"/>, what the wake is actually
        /// doing — because every way this effect fails looks identical on screen to every other way.
        /// A speed under the engage threshold, a hull whose measured radius makes the volume tiny,
        /// a config switched off, an amplitude that is running but too small to see and a spatial
        /// index that handed back no candidates all present as "nothing is happening", and no
        /// amount of staring at a ship separates them. The line names the live slot count, each
        /// slot's strength / radius / reach / train, and how many prisms are carrying the dense
        /// mesh, so "is it even working" is answered by reading rather than by guessing.
        ///
        /// It reports the IDLE state too, and with the reason: a system that goes quiet when it has
        /// nothing to say cannot be told apart from one that is not running at all.
        /// Toggle the channel in FrogletTools &gt; Toolbox &gt; Logging; it is off by default and
        /// costs one float compare per frame when it is.
        /// </summary>
        static void ReportState(PrismWakeConfigSO config, bool enabled, int count)
        {
            if (!CSDebug.IsVerbose(CSLogChannel.PrismRuntime)) return;
            if (Time.unscaledTime < _nextReportTime) return;
            _nextReportTime = Time.unscaledTime + 1f;

            if (!enabled)
            {
                CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                    "[PrismWake] idle: " + (config.Enabled ? "config is not sane" : "config disabled"));
                return;
            }

            if (count == 0)
            {
                CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                    $"[PrismWake] idle: {_sources.Count} source(s) reporting, none above the " +
                    $"engage speed ({config.EngageSpeed:0} u/s -> full at {config.FullSpeed:0}).");
                return;
            }

            _report.Clear();
            for (int i = 0; i < count; i++)
                _report.Append($" [{i}] w={_shape[i].x:0.00} r={_centre[i].w:0.0} " +
                               $"reach={_shape[i].y:0.0} train={_shape[i].z:0.0}");

            CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                $"[PrismWake] {count} wake(s), amp {config.Amplitude:0.00}, " +
                $"{_resident.Count}/{config.MaxResidentPrisms} prisms dense at s={config.Subdivision}:" +
                _report);
        }

        static readonly System.Text.StringBuilder _report = new();

        static void Write(int slot, in Source src, PrismWakeConfigSO config)
        {
            Vector3 p = src.Hull.position;
            _centre[slot] = new Vector4(p.x, p.y, p.z, src.Radius);
            _axis[slot] = new Vector4(src.Axis.x, src.Axis.y, src.Axis.z, src.Phase);
            _shape[slot] = new Vector4(
                src.Strength,
                ReachFor(src.Radius, config),
                TrainLengthFor(src.Radius, config),
                WavenumberFor(src.Radius, config));
        }

        // ---------------- Residency ----------------

        /// <summary>
        /// Decide which prisms hold the high-poly mesh this frame and apply the difference.
        ///
        /// A wake's support is a CYLINDER — <c>x ∈ [0, trainLength]</c> behind the ship,
        /// <c>r ≤ reach</c> out from its path — so the query is the sphere that bounds that
        /// cylinder grown by the margin, and candidates are then filtered to the grown cylinder
        /// itself. The two steps matter: the sphere's corners hold a lot of prisms the ripple
        /// cannot move, and a budget spent on those is a budget not spent on the ones it can.
        ///
        /// The margin is what makes the swap invisible rather than merely quick — a prism changes
        /// geometry only while every one of its vertices is provably unmoved. It is ALSO the one
        /// place the wake is better protected than the cradle: the support's boundary planes are
        /// C1-zero (the train envelope's value and slope both vanish at the ship's plane and at the
        /// train's end, and the radial falloff's at the reach), so even a prism long enough to
        /// straddle the boundary has a displacement there of second order in how far it straddles.
        ///
        /// <para><b>The budget is SHARED and split evenly.</b> Each live wake may claim at most its
        /// own share of <see cref="PrismWakeConfigSO.MaxResidentPrisms"/>, so four ships boosting at
        /// once get a coarser wake each rather than the first one enumerated taking the lot.</para>
        ///
        /// <para><b>The one limitation, stated.</b> The spatial index keys prisms by their CENTRE,
        /// so a prism longer than twice the margin whose centre is outside the volume but whose end
        /// pokes inside is not made resident. It still ripples — the shader reads world position and
        /// knows nothing about residency — just at the authored mesh's resolution. Coarse, never
        /// wrong.</para>
        /// </summary>
        static void ReconcileResidency(PrismWakeConfigSO config, bool enabled, int liveSlots)
        {
            _wanted.Clear();

            if (enabled && liveSlots > 0 && config.MaxResidentPrisms > 0)
            {
                var index = PrismSpatialIndex.Instance;
                if (index != null && index.IsAvailable)
                {
                    var mesh = HighPolyPrismMesh.Get(config.Subdivision);
                    float margin = config.ResidencyMargin;

                    // An even split, at least one each: a wake with no prisms at all would read as
                    // the effect having failed on that ship rather than as the budget being thin.
                    int share = Mathf.Max(1, config.MaxResidentPrisms / liveSlots);

                    for (int s = 0; s < liveSlots; s++)
                    {
                        Vector4 centreSlot = _centre[s];
                        Vector4 axisSlot = _axis[s];
                        Vector4 shapeSlot = _shape[s];

                        var origin = new Vector3(centreSlot.x, centreSlot.y, centreSlot.z);
                        var axis = new Vector3(axisSlot.x, axisSlot.y, axisSlot.z);
                        float reach = shapeSlot.y + margin;
                        float half = 0.5f * shapeSlot.z;
                        if (!(reach > 0f) || !(half > 0f)) continue;

                        // The envelope's PEAK plane: where the ripple is strongest, so where the
                        // surface is most worth having. Also the tightest centre for the query.
                        Vector3 peak = origin + axis * half;
                        float queryRadius = Mathf.Sqrt((half + margin) * (half + margin) + reach * reach);

                        index.QuerySphere(peak, queryRadius, _query);
                        if (_query.Count == 0) continue;

                        float along = shapeSlot.z + margin;
                        _candidates.Clear();
                        for (int i = 0; i < _query.Count; i++)
                        {
                            var p = _query[i];
                            if (p == null) continue;
                            if (_wanted.Contains(p)) continue;
                            // Someone else owns this prism's override (a settled shield, or the
                            // cradle). Leave it: the slot has one owner at a time, and stomping a
                            // shield would replace the geometry the player is looking at with a box.
                            if (p.RenderMeshOverride != null && !ReferenceEquals(p.RenderMeshOverride, mesh))
                                continue;

                            // Filter the sphere down to the grown cylinder the ripple can reach.
                            Vector3 q = p.transform.position - origin;
                            float x = Vector3.Dot(q, axis);
                            if (x < -margin || x > along) continue;
                            if ((q - axis * x).sqrMagnitude > reach * reach) continue;

                            _candidates.Add(p);
                        }
                        if (_candidates.Count == 0) continue;

                        // QuerySphere is unordered, so without this the dense mesh would go to
                        // whichever prisms the bucket walk happened to reach — which in a crowded
                        // trail is not the ones in the thick of the wake.
                        _sortOrigin = peak;
                        _candidates.Sort(_byDistance);

                        int room = Mathf.Min(share, config.MaxResidentPrisms - _wanted.Count);
                        int take = Mathf.Min(room, _candidates.Count);
                        for (int i = 0; i < take; i++)
                            _wanted.Add(_candidates[i]);

                        if (_wanted.Count >= config.MaxResidentPrisms) break;
                    }

                    // Apply: everything wanted that is not already resident.
                    foreach (var p in _wanted)
                    {
                        if (_resident.Contains(p)) continue;
                        p.SetRenderMeshOverride(mesh);
                        _resident.Add(p);
                    }
                }
            }

            if (_resident.Count == 0) return;

            // Release: everything resident that is no longer wanted, plus anything that died or was
            // taken over while it was. Deferred into a list because the set cannot be mutated while
            // it is walked.
            _evict.Clear();
            foreach (var p in _resident)
                if (p == null || !_wanted.Contains(p))
                    _evict.Add(p);

            for (int i = 0; i < _evict.Count; i++)
            {
                var p = _evict[i];
                _resident.Remove(p);
                if (p == null) continue;
                // Only clear an override that is still OURS. A prism shielded mid-wake has
                // legitimately had the slot taken over, and clearing there would drop the shield's
                // octahedron and render it as a box.
                if (HighPolyPrismMesh.IsHighPoly(p.RenderMeshOverride))
                    p.ClearRenderMeshOverride();
            }
        }

        static int CompareByDistance(Prism a, Prism b)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;
            float da = (a.transform.position - _sortOrigin).sqrMagnitude;
            float db = (b.transform.position - _sortOrigin).sqrMagnitude;
            return da.CompareTo(db);
        }

        /// <summary>Hand every resident prism its own mesh back. Called on teardown and reset.</summary>
        static void ReleaseAllResidents()
        {
            foreach (var p in _resident)
            {
                if (p == null) continue;
                if (HighPolyPrismMesh.IsHighPoly(p.RenderMeshOverride))
                    p.ClearRenderMeshOverride();
            }
            _resident.Clear();
            _wanted.Clear();
            _query.Clear();
            _candidates.Clear();
            _evict.Clear();
        }

        // ---------------- Lifecycle ----------------

        static void PublishOff()
        {
            for (int i = 0; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _axis[i] = Vector4.zero;
                _shape[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(AxisId, _axis);
            Shader.SetGlobalVectorArray(ShapeId, _shape);
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            _publishedCount = 0;
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a wake left live when play
        /// stopped would otherwise keep rippling mass around a ship that no longer exists. Publish
        /// the off state before anything renders — the same guard the occlusion corridor, the Echo
        /// Sight and the cradle install — and install the driver that flushes the bank.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            _sources.Clear();
            _stale.Clear();
            // Not ReleaseAllResidents: the prisms of the previous session are gone, and touching a
            // destroyed one is the failure this guard exists to avoid. Drop the bookkeeping.
            _resident.Clear();
            _wanted.Clear();
            _query.Clear();
            _candidates.Clear();
            _evict.Clear();
            _configResolved = false;
            PublishOff();

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern the corridor's, the sight's and the cradle's publishers use.
            var go = new GameObject("[PrismWake]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate so the bank is packed after every source's Update has reported this frame,
        /// and after the vessels those sources describe have moved — the same reasoning as the
        /// occlusion corridor's publisher.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Flush();

            void OnDisable()
            {
                _sources.Clear();
                // The prisms are still alive here (this is a teardown, not a domain reload), so
                // hand every one of them its own mesh back rather than orphaning the override.
                ReleaseAllResidents();
                PublishOff();
            }
        }
    }
}
