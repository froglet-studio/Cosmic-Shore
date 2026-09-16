using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the Urchin's CRADLE: while an Urchin rides a prismscape, the prism triangle
    /// nearest the hull swings to face it and settles onto its surface, its three neighbours come
    /// partway, and every other triangle stays put.
    ///
    /// It publishes a small bank of global shader uniforms once per frame and does nothing else.
    /// There is no per-prism work of any kind — no spatial query, no trigger volume, no material
    /// swaps, no per-instance overrides, no tracking list of prisms. The deformation runs on the
    /// GPU in <c>PrismCradle.hlsl</c>, wired LAST on the vertex chain of both live-prism graphs.
    ///
    /// <para><b>Why a global uniform and not a per-prism write.</b> "Where is the hull relative to
    /// this prism" is live gameplay data that changes every frame for every prism as the Urchin
    /// slides — so it can never be a per-prism stamp, and a CPU pass that finds the prisms in range
    /// and updates each one's material is exactly what the clock-material law forbids. The law's
    /// sanctioned shape for this case (Docs/PRISM_ANIMATION.md §1, "animation vs. live gameplay
    /// data"; §4.7, the ONE shape for a live-data prism visual) is a global uniform: O(1) writes per
    /// frame that every prism reads. "Every prism in range has its material updated with the vessel
    /// position" is therefore satisfied literally, for all of them at once, by publishing the
    /// position ONCE. Sibling of <see cref="PrismOcclusionCorridor"/> and
    /// <see cref="PrismDestructionSight"/>, and shaped exactly like the latter's peer bank.</para>
    ///
    /// <para><b>The bank.</b> One slot per riding Urchin, <see cref="Slots"/> at most: the hull's
    /// centre and radius in one float4, its eased strength in another. The centre is sampled HERE,
    /// in LateUpdate, off the registered Transform — after every transformer's Update has moved
    /// its vessel — so a hull grinding at 300 u/s is never published a frame stale. The radius is
    /// constant per vessel and travels beside the centre only because a slot is one float4; nothing
    /// computes it per frame.</para>
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

        static PrismCradleConfigSO _config;
        static bool _configResolved;

        /// <summary>True while any Urchin is publishing a live cradle.</summary>
        public static bool IsActive => _publishedCount > 0;

        /// <summary>
        /// Tuning (the band and the engage/release eases). Falls back to the SO's own defaults
        /// when no <c>Resources/PrismCradleConfig</c> asset exists, so the feature works with no
        /// authoring.
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
        /// Pack this frame's reported hulls into the shader's bank. Called once per frame from
        /// <see cref="Driver"/> in LateUpdate — after every source's Update has reported and after
        /// the vessels those sources ride have moved, and before anything renders.
        ///
        /// The whole per-frame cost of the feature is this method: two array writes and one
        /// vector, independent of how many Urchins are riding and completely independent of how
        /// many prisms are on screen.
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

            // Nothing to say and nothing said last frame: skip the writes entirely, so a match
            // with no Urchin in it costs this system literally nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            // z is the shader's MASTER sentinel: 0 means "the loop does not execute". w is how much
            // farther than the nearest triangle a neighbour may be and still come partway.
            Shader.SetGlobalVector(ParamsId,
                new Vector4(config.OuterRange, config.InnerRange, count, config.NeighbourSpread));
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
                PublishOff();
            }
        }
    }
}
