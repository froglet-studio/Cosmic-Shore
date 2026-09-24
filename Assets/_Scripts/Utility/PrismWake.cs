using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the WAKE — a warhead's SHOCKWAVE FRONT. A round carrying a live warhead
    /// throws a thin spherical SHELL of rippled prisms outward, over and over, all the way in: born
    /// at the shell's own half-thickness and dying at exactly the radius that warhead's blast will
    /// reach. So the front is not decoration — it is the blast's own reach, drawn on the mass rather
    /// than on the HUD.
    ///
    /// <para><b>It is half of a pair.</b> The same round publishes its armed fuze volume as a LIT
    /// SPHERE (Docs/LIT.md, <c>Projectile.PublishFuzeLit</c>): mass standing where this warhead WILL
    /// go off, in the shooter's domain colour. That says WHERE, statically, in colour; this says HOW
    /// FAR, kinetically, in motion. Two channels of the surface description for one weapon, and
    /// neither duplicating the other.</para>
    ///
    /// <para><b>The reach is the WARHEAD'S, never a tuned number.</b> The carrier reports its own
    /// blast radius live through <c>IPrismWakeCarrier</c>, so the front grows with MASS exactly as
    /// the warhead does and cannot drift from the thing it describes. Every length in the config is
    /// a fraction of it.</para>
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
    /// swapped to <see cref="HighPolyPrismMesh"/>, the identical solid at ~1,200 triangles,
    /// through the platform's own shared-mesh handoff (<c>Prism.SetRenderMeshOverride</c>). That is
    /// NOT an exception to the clock-material law: a mesh override is FINAL at the instant it is
    /// applied, exactly like a shield engaging — a state change, not an animation — and it is
    /// invisible because the swap happens strictly outside the volume the ripple can move anything
    /// (<c>PrismWakeConfigSO.ResidencyRadiusFor</c>, which is <c>reach + sigma + margin</c> — the
    /// sigma is what makes that structural rather than a coincidence between an absolute margin and
    /// a shell thickness authored as a fraction of the reach). The mesh is SHARED, so the resident prisms stay
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
        // There is no _PrismWakeAxis any more: a sphere has no axis. The name is left unwritten
        // rather than repurposed, so nothing can read a stale direction out of it.
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
            public Transform Carrier;
            public float Reach;         // the warhead's own blast radius, world units
            public float Front;         // where this pulse's shell is right now, world units
            public float Strength;
            public int Frame;
        }

        static readonly Dictionary<int, Source> _sources = new();
        static readonly List<int> _stale = new();

        // Always sent at full length: Unity binds an array global at the length of its first write,
        // so a short write later would silently leave the tail of the previous frame's bank live.
        // Unused slots are zeroed and _PrismWakeParams.z is the real bound.
        static readonly Vector4[] _centre = new Vector4[Slots];
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
        // The source integrates its pulse phase and the bank packs the shader's slot, and both need
        // the same arithmetic. It lives on the CONFIG (HalfThicknessFor, FrontRadiusAt,
        // FrontEnvelope) so there is one copy the publisher, the residency pass and every test read
        // — the retune that moved one of these and not the other is the failure this shape removes.

        /// <summary>
        /// How fast a pulse's phase advances, in pulses per second. It is simply the authored rate:
        /// a front's travel is expressed as a FRACTION of its life rather than as a speed, so one
        /// pulse takes the same time to cross a small warhead's reach as a large one's — which is
        /// what keeps a MASS-swollen round's front readable instead of leisurely.
        /// </summary>
        public static float PulseRate(PrismWakeConfigSO config) => config.PulsesPerSecond;

        /// <summary>
        /// Report a live shockwave. <paramref name="sourceId"/> identifies the reporting round (its
        /// source component's instance id) so one round can only ever occupy one slot across a pool
        /// reuse. <paramref name="carrier"/> is sampled at flush time, not now — a round at 240 u/s
        /// published from Update would be a frame stale by the time anything renders.
        ///
        /// Must be called every frame (from Update — the flush runs in LateUpdate) while the front
        /// is up; a slot that stops being reported is dropped by the next <see cref="Flush"/>.
        /// </summary>
        public static void Publish(int sourceId, Transform carrier, float reach, float frontRadius,
            float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (carrier == null || reach <= 0f || frontRadius <= 0f)
            {
                Clear(sourceId);
                return;
            }

            // A strength of zero is NOT a reason to drop the slot, and that is deliberate. A front's
            // own envelope passes through zero at the seam between one pulse and the next (birth and
            // death are both silent, which is what makes the recycle invisible), and dropping there
            // would release every resident prism and re-acquire it 1.6 times a second for no visual
            // difference at all. The shader's own `w > 0` test skips a zero slot for free. The SOURCE
            // decides when a round stops having a shockwave, and it calls Clear.

            _sources[sourceId] = new Source
            {
                Carrier = carrier,
                Reach = reach,
                Front = frontRadius,
                Strength = strength01,
                Frame = Time.frameCount,
            };
        }

        /// <summary>
        /// Drop a shockwave. Idempotent, and not strictly required — the frame stamp collects an
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
                if (kv.Value.Frame != frame || kv.Value.Carrier == null)
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

                    // Reachable the moment a fifth rocket is in the air, which one Sparrow can do.
                    // Evict the WEAKEST rather than whoever the dictionary happened to enumerate
                    // last: a bank that dropped by enumeration order would show a different set of
                    // fronts on each machine for the same match.
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
                _shape[i] = Vector4.zero;
            }

            ReconcileResidency(config, enabled, count);

            ReportState(config, enabled, count);

            // Nothing to say and nothing said last frame: skip the writes entirely, so a match in
            // which nobody has a rocket in the air costs this system literally nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(ShapeId, _shape);
            // z is the shader's MASTER sentinel: 0 means "the loop does not execute".
            Shader.SetGlobalVector(ParamsId,
                new Vector4(config.Amplitude, config.WavesInFront, count, 0f));
            _publishedCount = count;
        }

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
                    $"[PrismWake] idle: {_sources.Count} source(s) reporting, none with a live " +
                    "warhead (a base rocket, or a round whose prefab authors no warhead radius).");
                return;
            }

            _report.Clear();
            for (int i = 0; i < count; i++)
                _report.Append($" [{i}] w={_shape[i].x:0.00} front={_centre[i].w:0.0} " +
                               $"reach={_shape[i].z:0.0} sigma={_shape[i].y:0.0}");

            CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                $"[PrismWake] {count} shockwave(s), amp {config.Amplitude:0.000} x sigma, " +
                $"Q={config.WavesInFront}, " +
                $"{_resident.Count}/{config.MaxResidentPrisms} prisms dense at s={config.Subdivision}:" +
                _report);
        }

        static readonly System.Text.StringBuilder _report = new();

        /// <summary>
        /// Pack one shockwave. The half-thickness is derived HERE rather than in the shader: it is a
        /// pure function of the reach and the config, the CPU already has both, and deriving it once
        /// means the shell the residency pass makes room for and the shell the shader draws cannot
        /// fall out of step. The CENTRE is sampled now, at flush time in LateUpdate, after the
        /// round's own mover has run.
        /// </summary>
        static void Write(int slot, in Source src, PrismWakeConfigSO config)
        {
            Vector3 p = src.Carrier.position;
            _centre[slot] = new Vector4(p.x, p.y, p.z, src.Front);
            _shape[slot] = new Vector4(
                src.Strength,
                config.HalfThicknessFor(src.Reach),
                src.Reach,
                0f);
        }

        // ---------------- Residency ----------------

        /// <summary>
        /// Decide which prisms hold the high-poly mesh this frame and apply the difference.
        ///
        /// A shockwave's support is a SPHERE of the warhead's own reach, so the query is exactly
        /// that sphere grown by the margin and there is no second filtering step — the cylinder's
        /// version needed one because a sphere bounding a cylinder holds a lot of prisms the ripple
        /// could never move, and a sphere bounding a sphere holds none.
        ///
        /// <para><b>Residency is the REACH, not the shell.</b> A prism is made resident for the whole
        /// volume the front will cross, not for the thin shell the front occupies right now — which
        /// is the point: a prism must already be carrying the dense mesh by the time the shell
        /// arrives at it, and the swap must happen where the map provably cannot have moved a vertex.
        /// Both are satisfied at once, because the shell is thin: a prism swapped in at reach-range
        /// while the front is anywhere else is swapped where the displacement is exactly zero. The
        /// cost is that residents outside the current shell carry the dense mesh for nothing, which
        /// is the price of a travelling front and is bounded by the budget rather than by the reach.</para>
        ///
        /// The swept radius is <c>reach + sigma + margin</c>, because the front dies AT the reach and
        /// the shell reaches <c>sigma</c> past its own centre: the outermost displaced vertex of a
        /// pulse's last frame is at <c>reach + sigma</c>, and a prism swapped in THERE would pop. The
        /// margin then covers the remaining case — a prism arriving at that outer edge on the very
        /// frame the front reaches it — and it is why this effect is better protected than the
        /// cradle: the shell's two faces are C1-zero (the wavelet's value AND slope vanish at both),
        /// so even a prism long enough to straddle a face has a displacement there of second order in
        /// how far it straddles.
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

                    // An even split, at least one each: a wake with no prisms at all would read as
                    // the effect having failed on that ship rather than as the budget being thin.
                    int share = Mathf.Max(1, config.MaxResidentPrisms / liveSlots);

                    for (int s = 0; s < liveSlots; s++)
                    {
                        Vector4 centreSlot = _centre[s];
                        Vector4 shapeSlot = _shape[s];

                        var origin = new Vector3(centreSlot.x, centreSlot.y, centreSlot.z);

                        // reach + sigma + margin, never reach + margin: the front dies AT the reach
                        // and the shell reaches sigma past its own centre, so the last frame of a
                        // pulse displaces vertices out to reach + sigma. See
                        // PrismWakeConfigSO.ResidencyRadiusFor - the margin is absolute and the shell
                        // is a fraction of the reach, so the two only ever agree by coincidence.
                        float queryRadius = config.ResidencyRadiusFor(shapeSlot.z);
                        if (!(queryRadius > 0f)) continue;

                        index.QuerySphere(origin, queryRadius, _query);
                        if (_query.Count == 0) continue;

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

                            _candidates.Add(p);
                        }
                        if (_candidates.Count == 0) continue;

                        // QuerySphere is unordered, so without this the dense mesh would go to
                        // whichever prisms the bucket walk happened to reach rather than to the ones
                        // nearest the round — which is where every front spends its early life.
                        _sortOrigin = origin;
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
                _shape[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(ShapeId, _shape);
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            _publishedCount = 0;
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a front left live when play
        /// stopped would otherwise keep rippling mass around a rocket that no longer exists. Publish
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
