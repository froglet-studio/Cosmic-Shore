using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the Urchin's CRADLE: while an Urchin rides a prismscape, the mass around it
    /// DRAPES over the hull — every vertex within the drape reach of the hull's surface slides
    /// along its own radius toward that surface, closing over the parts of the ship it swallows
    /// and rising to meet the parts it has not reached.
    ///
    /// It does exactly two things per frame, and they are different KINDS of thing:
    ///
    /// <para><b>1. The bank (the animation).</b> A small set of global shader uniforms, published
    /// once per frame and nothing else. There is no per-prism animation work of any kind — no
    /// trigger volume, no material writes, no per-instance overrides. The deformation runs on the
    /// GPU in <c>PrismCradle.hlsl</c>, wired LAST on the vertex chain of both live-prism graphs.
    /// "Where is the hull relative to this prism" is live gameplay data that changes every frame
    /// for every prism as the Urchin slides, so it can never be a per-prism stamp, and a CPU pass
    /// that updates each prism's material is exactly what the clock-material law forbids. The
    /// law's sanctioned shape for this case (Docs/PRISM_ANIMATION.md §1, §4.7) is a global
    /// uniform: O(1) writes per frame that every prism reads.</para>
    ///
    /// <para><b>2. Residency (a state change).</b> A prism is 24 triangles, and a deformation is
    /// only as smooth as the surface it moves — the cradle's first two rounds bent those triangles
    /// and read as facets hinging. So the handful of prisms close enough to be draped are swapped
    /// to <see cref="HighPolyPrismMesh"/>, the identical solid at ~3,000 triangles, through the
    /// platform's own shared-mesh handoff (<c>Prism.SetRenderMeshOverride</c>). That is NOT an
    /// exception to the clock-material law: a mesh override is FINAL at the instant it is applied,
    /// exactly like a shield engaging — it is a state change, not an animation, and it is
    /// invisible because the swap happens strictly outside the volume the drape can move anything
    /// (<c>PrismCradleConfigSO.ResidencyMargin</c>). The mesh is SHARED, so the resident prisms
    /// stay in one instanced batch rather than minting a mesh and a draw call each.</para>
    ///
    /// <para><b>The bank's shape.</b> One slot per riding Urchin, <see cref="Slots"/> at most: the
    /// hull's centre and radius in one float4, its eased strength in another. The centre is
    /// sampled HERE, in LateUpdate, off the registered Transform — after every transformer's
    /// Update has moved its vessel — so a hull grinding at 300 u/s is never published a frame
    /// stale. The radius is constant per vessel and travels beside the centre only because a slot
    /// is one float4; nothing computes it per frame.</para>
    ///
    /// Not a platform law: it is one vessel's ride feel, live only while that vessel is attached.
    /// </summary>
    public static class PrismCradle
    {
        /// <summary>
        /// How many riding Urchins can cradle at once. Mirrors <c>PRISM_CRADLE_SLOTS</c> in
        /// <c>PrismCradle.hlsl</c> — change both together, since the shader's arrays are declared
        /// at this length. Four is the largest roster any Urchin mode seats (Hijack and Skein,
        /// <c>MaxPlayersAllowed 4</c>). <see cref="Flush"/> keeps the strongest if it ever overflows.
        /// </summary>
        public const int Slots = 4;

        public const string ConfigResourcePath = "PrismCradleConfig";

        static readonly int CentreId = Shader.PropertyToID("_PrismCradleCentre");
        static readonly int WeightId = Shader.PropertyToID("_PrismCradleWeight");
        static readonly int ParamsId = Shader.PropertyToID("_PrismCradleParams");

        /// <summary>
        /// One riding hull, as reported this frame. <see cref="Frame"/> is what makes the bank
        /// self-cleaning: a source that stops reporting — its vessel destroyed, swapped, or its
        /// scene unloaded — has its slot dropped on the next flush with nothing needing to have
        /// called <see cref="Clear"/>. A cradle that outlives the hull it wraps is the one failure
        /// mode a registry like this actually has.
        /// </summary>
        struct Source
        {
            public Transform Hull;
            public float Radius;
            public float Strength;
            public int Frame;
        }

        static readonly Dictionary<int, Source> _sources = new();
        static readonly List<int> _stale = new();

        // Always sent at full length: Unity binds an array global at the length of its first
        // write, so a short write later would silently leave the tail of the previous frame's
        // bank live. Unused slots are zeroed and _PrismCradleParams.z is the real bound.
        static readonly Vector4[] _centre = new Vector4[Slots];
        static readonly Vector4[] _weight = new Vector4[Slots];
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

        static PrismCradleConfigSO _config;
        static bool _configResolved;

        /// <summary>True while any Urchin is publishing a live cradle.</summary>
        public static bool IsActive => _publishedCount > 0;

        /// <summary>How many prisms currently hold the high-poly mesh (diagnostics, tests).</summary>
        public static int ResidentPrismCount => _resident.Count;

        /// <summary>
        /// Tuning (the drape, the residency budget and the engage/release eases). Falls back to
        /// the SO's own defaults when no <c>Resources/PrismCradleConfig</c> asset exists, so the
        /// feature works with no authoring.
        /// </summary>
        public static PrismCradleConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<PrismCradleConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<PrismCradleConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>Forget the cached config so the next read reloads it (editor tooling).</summary>
        public static void InvalidateConfig() => _configResolved = false;

        /// <summary>
        /// Report a riding hull. <paramref name="sourceId"/> identifies the reporting vessel (its
        /// source component's instance id) so one vessel can only ever occupy one slot across a
        /// swap or a re-initialise. <paramref name="hull"/> is sampled at flush time, not now.
        /// <paramref name="strength01"/> is the eased engage/release, so the cradle never pops.
        ///
        /// Must be called every frame (from Update — the flush runs in LateUpdate) while the
        /// cradle is up; a slot that stops being reported is dropped by the next <see cref="Flush"/>.
        /// </summary>
        public static void Publish(int sourceId, Transform hull, float radius, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (hull == null || radius <= 0f || strength01 <= 0.001f)
            {
                Clear(sourceId);
                return;
            }

            _sources[sourceId] = new Source
            {
                Hull = hull,
                Radius = radius,
                Strength = strength01,
                Frame = Time.frameCount,
            };
        }

        /// <summary>
        /// Drop a hull. Idempotent, and not strictly required — the frame stamp collects an
        /// abandoned slot anyway — but calling it on release retires the cradle on the same frame
        /// instead of the next one.
        /// </summary>
        public static void Clear(int sourceId) => _sources.Remove(sourceId);

        /// <summary>
        /// Pack this frame's reported hulls into the shader's bank and reconcile which prisms hold
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
                        Write(count++, src, config.MaxStrength);
                        continue;
                    }

                    // Unreachable with any roster the game ships (see Slots), but a bank that
                    // silently drops whoever it happened to enumerate last would be a
                    // machine-dependent difference in what each player sees. Evict the weakest
                    // instead: the faintest cradle is the one whose absence is least noticeable.
                    int weakest = 0;
                    for (int i = 1; i < Slots; i++)
                        if (_weight[i].x < _weight[weakest].x)
                            weakest = i;
                    // Both sides of the compare are SCALED: the bank holds published strengths,
                    // so weighing a raw one against them would evict by the wrong ordering the
                    // moment MaxStrength is anything but 1.
                    if (src.Strength * config.MaxStrength > _weight[weakest].x)
                        Write(weakest, src, config.MaxStrength);
                }
            }

            for (int i = count; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _weight[i] = Vector4.zero;
            }

            ReconcileResidency(config, enabled, count);

            // Nothing to say and nothing said last frame: skip the writes entirely, so a match
            // with no Urchin in it costs this system literally nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            // z is the shader's MASTER sentinel: 0 means "the loop does not execute".
            Shader.SetGlobalVector(ParamsId,
                new Vector4(config.DrapeReach, config.DrapeExponent, count, 0f));
            _publishedCount = count;
        }

        /// <summary>
        /// Pack one hull. <paramref name="maxStrength"/> is the config's ceiling, applied HERE
        /// rather than in the shader: the shader's contract is "this is how far through the motion
        /// you are", and how far through it a full-strength cradle goes is a tuning fact about the
        /// feel. Applying it on the way into the bank also means the tuning costs no uniform lane
        /// and no shader edit, so the harness that proves the deformation still proves it.
        /// </summary>
        static void Write(int slot, in Source src, float maxStrength)
        {
            Vector3 p = src.Hull.position;
            _centre[slot] = new Vector4(p.x, p.y, p.z, src.Radius);
            _weight[slot] = new Vector4(src.Strength * maxStrength, 0f, 0f, 0f);
        }

        // ---------------- Residency ----------------

        /// <summary>
        /// Decide which prisms hold the high-poly mesh this frame and apply the difference.
        ///
        /// The radius is <c>hullRadius + drapeReach + residencyMargin</c>, so a prism only changes
        /// geometry while every one of its vertices is provably unmoved by the drape — the margin
        /// is what makes the swap invisible rather than merely quick. Prisms are taken NEAREST
        /// first up to the budget, and any prism already holding a different override (a settled
        /// shield's octahedron) is declined outright: that slot has one owner at a time, and
        /// stomping a shield would replace the geometry the player is looking at with a box.
        ///
        /// <para><b>The one limitation, stated.</b> The spatial index keys prisms by their CENTRE,
        /// so a very long prism whose centre is outside the radius but whose end pokes inside is
        /// not made resident. It still drapes — the shader reads world position and knows nothing
        /// about residency — just at the authored mesh's resolution. Coarse, never wrong.</para>
        /// </summary>
        static void ReconcileResidency(PrismCradleConfigSO config, bool enabled, int liveSlots)
        {
            _wanted.Clear();

            if (enabled && liveSlots > 0 && config.MaxResidentPrisms > 0)
            {
                var index = PrismSpatialIndex.Instance;
                if (index != null && index.IsAvailable)
                {
                    var mesh = HighPolyPrismMesh.Get(config.Subdivision);
                    float pad = config.DrapeReach + config.ResidencyMargin;

                    for (int s = 0; s < liveSlots; s++)
                    {
                        Vector4 slot = _centre[s];
                        var centre = new Vector3(slot.x, slot.y, slot.z);
                        float radius = slot.w + pad;
                        if (!(radius > 0f)) continue;

                        index.QuerySphere(centre, radius, _query);
                        if (_query.Count == 0) continue;

                        // Nearest first, then take what the budget allows. QuerySphere is
                        // unordered, so without this the prisms that get the dense mesh in a
                        // crowded trail would be whichever the bucket walk happened to reach —
                        // which is not the ones wrapped around the ship.
                        _candidates.Clear();
                        for (int i = 0; i < _query.Count; i++)
                        {
                            var p = _query[i];
                            if (p == null) continue;
                            if (_wanted.Contains(p)) continue;
                            // Someone else owns this prism's override (a settled shield). Leave it.
                            if (p.RenderMeshOverride != null && !ReferenceEquals(p.RenderMeshOverride, mesh))
                                continue;
                            _candidates.Add(p);
                        }

                        _sortOrigin = centre;
                        _candidates.Sort(_byDistance);

                        int room = config.MaxResidentPrisms - _wanted.Count;
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

            // Release: everything resident that is no longer wanted, plus anything that died or
            // was taken over while it was. Deferred into a list because the set cannot be mutated
            // while it is walked.
            _evict.Clear();
            foreach (var p in _resident)
                if (p == null || !_wanted.Contains(p))
                    _evict.Add(p);

            for (int i = 0; i < _evict.Count; i++)
            {
                var p = _evict[i];
                _resident.Remove(p);
                if (p == null) continue;
                // Only clear an override that is still OURS. A prism shielded mid-cradle has
                // legitimately had the slot taken over, and clearing there would drop the
                // shield's octahedron and render it as a box.
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
                _weight[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            _publishedCount = 0;
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a cradle left live when play
        /// stopped would otherwise keep deforming mass around a hull that no longer exists. Publish
        /// the off state before anything renders — the same guard the occlusion corridor and the
        /// Echo Sight install — and install the driver that flushes the bank.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            _sources.Clear();
            _stale.Clear();
            // Not ReleaseAllResidents: the prisms of the previous session are gone, and touching
            // a destroyed one is the failure this guard exists to avoid. Drop the bookkeeping.
            _resident.Clear();
            _wanted.Clear();
            _query.Clear();
            _candidates.Clear();
            _evict.Clear();
            _configResolved = false;
            PublishOff();

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern the corridor's and the sight's publishers use.
            var go = new GameObject("[PrismCradle]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate so the bank is packed after every source's Update has reported this frame,
        /// and after the vessels those sources ride have moved — the same reasoning as the
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
