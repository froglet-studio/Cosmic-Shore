using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The spindle LOD (Docs/SPINDLE_LOD.md): three tiers of one limb, chosen by how THICK it is on
    /// screen, with every visible change happening continuously on the GPU so the CPU's mesh swaps
    /// are never seen.
    ///
    /// <list type="bullet">
    /// <item><b>Near</b> - the authored braid on SpindleGraph, untouched (the look up close is the one
    /// the game was built around, so this tier is the spindle exactly as it was).</item>
    /// <item><b>Mid</b> - the braid at every third ring (432 -> 144 triangles) on SpindleLodMidGraph:
    /// the same lace, then the three strands drawing together into one thread as the camera pulls
    /// away (SpindleLod.hlsl).</item>
    /// <item><b>Far / Low / Min</b> - on SpindleLodFarGraph, and while still, not a renderer at all:
    /// its renderer is switched off and it is drawn in ONE <c>Graphics.RenderMeshInstanced</c> call
    /// per material and mesh, which is what removes the per-limb culling and submission cost that grew
    /// with the forest. OVERT steps the mesh down once the effect has settled back - the 144-triangle
    /// braid through the peak and the recede, the 72-triangle braid once settled, then the 12-triangle
    /// thread (the cheapest form that still draws a spindle); COVERT goes straight to the thread.</item>
    /// <item><b>Culled</b> - past the fade's end, where the thread's alpha has reached zero: nothing.</item>
    /// </list>
    ///
    /// Classification is ONE Burst pass per tick over a read-only <see cref="TransformAccessArray"/>
    /// (the <see cref="PrismColliderLodManager"/> shape): it reads every limb's pose, measures camera
    /// distance in GIRTHS, applies hysteresis against the tier each limb is in, and notes whether the
    /// limb MOVED since the last pass. Main-thread work is O(changed limbs), budgeted per frame. A
    /// limb that moved never joins the instanced batch (a creature's body would lag its creature),
    /// and one that moves while batched leaves it the same pass, unbudgeted.
    ///
    /// TWO STYLES (<see cref="SpindleLodProfileSO.Style"/>, switchable in Play). COVERT measures in
    /// girths and hides itself. OVERT runs in LOCKSTEP with the prisms' own depth effect: distance is
    /// the prisms' normalised squared distance (d^2 / _SqrDistance, read off the danger prism through
    /// <see cref="IncorporealDepth"/>), the near -> mid swap sits where that ramp starts opening the
    /// braid and the mid -> far swap where it is fully open, so both swaps land on a flat end of the
    /// ramp and the only thing anyone sees is the effect itself: the braid opening like danger mass,
    /// a wave running through it.
    ///
    /// Distance is measured from <see cref="CameraManager.ViewCamera"/>, the camera the player looks
    /// through - never <c>Camera.main</c>, which in flight is Bootstrap's menu camera and never moves
    /// (measuring from it drew near and far tiers side by side around the vessel, 2026-10-20).
    ///
    /// Spindle keeps owning life and death: a fading limb (condense at birth, evaporate at death) is
    /// never batched, because its fade is a per-renderer MaterialPropertyBlock stamp
    /// (<see cref="SetFading"/>), and the ordered wither and tree links never see this system.
    ///
    /// BODIES (Docs/SPINDLE_LOD.md §8). A spindle renderer that is NOT the braid - a creature's own
    /// model, the shark's, the brittlestar's, the worm's - has no LOD meshes, so it never enters the
    /// pass. It is handed a <b>depth material</b> instead: its own look on SpindleDepthGraph, which
    /// blooms it outward from its pivot on the same ramp, recede and wave, at its element's strength.
    /// It keeps that material at every distance (the effect is zero up close) until it unregisters.
    ///
    /// SYNTHETIC WIRES (Docs/SPINDLE_LOD.md section 9). The switch rings are a machined cable with the
    /// braid's channels (a mesh set flagged synthetic): tiered like a limb, but every tier draws on
    /// SpindleWireGraph - solid up close, a spindle's lace while the effect is open - and a ring is
    /// never culled, because a switch is a signal you fly toward.
    /// </summary>
    public sealed class SpindleLodSystem : MonoBehaviour
    {
        public enum Tier : byte { Near = 0, Mid = 1, Far = 2, Low = 3, Min = 4, Culled = 5 }

        /// <summary>The tiers drawn on the far graph, instanced while still.</summary>
        static bool IsFarFamily(Tier t) => t is Tier.Far or Tier.Low or Tier.Min;

        /// <summary>One registered renderer. Held by its <see cref="Spindle"/>; the index is
        /// rewritten when a swap-back removal moves the entry, and is -1 once unregistered.</summary>
        public sealed class Handle
        {
            internal int index = -1;
            internal SpindleLodSystem owner;
            internal bool body;          // indexes _bodies, not _entries
            public bool IsRegistered => owner != null && index >= 0;
        }

        /// <summary>A registered body: a spindle renderer that wears a depth material and no tiers.</summary>
        struct Body
        {
            public Handle handle;
            public Renderer renderer;
            public Material near;
            public Material depth;
        }

        sealed class TierMaterials
        {
            public Material near;
            public Material mid;
            public Material far;
            public float strength;
            // One instanced batch per far-family tier: they share the far material, not the mesh.
            public FarBatch farBatch;
            public FarBatch lowBatch;
            public FarBatch minBatch;

            public FarBatch BatchFor(Tier t) => t switch { Tier.Far => farBatch, Tier.Low => lowBatch, _ => minBatch };
        }

        sealed class FarBatch
        {
            public int set;
            public Tier tier;
            public float strength;
            public Material material;
            public Mesh mesh;
            public readonly List<int> entries = new();
            public Matrix4x4[] matrices = new Matrix4x4[64];
            public Bounds bounds;
            public bool dirty;               // a full Rebuild is due (new batch, or the box is stale)
            public int leftSinceRebuild;     // leaves since the box was last fitted
            public RenderParams renderParams;
        }

        struct Entry
        {
            public Handle handle;
            public Renderer renderer;
            public MeshFilter filter;
            public int set;
            public TierMaterials materials;
            public Tier tier;
            public bool fading;
            public bool batched;
            public bool stillOnce;        // has been seen still for a whole pass
            public int batchSlot;
            public FarBatch batch;        // the batch it is in while batched
        }

        // ── Shader globals (declared in SpindleLod.hlsl) ────────────────────────────────────────
        static readonly int BandsId = Shader.PropertyToID("_SpindleLodBands");
        static readonly int LookId = Shader.PropertyToID("_SpindleLodLook");
        static readonly int Look2Id = Shader.PropertyToID("_SpindleLodLook2");
        static readonly int GirthId = Shader.PropertyToID("_SpindleLodGirth");
        static readonly int StrandId = Shader.PropertyToID("_SpindleLodStrand");
        static readonly int StrengthId = Shader.PropertyToID("_SpindleLodStrength");
        static readonly int ReachId = Shader.PropertyToID("_SpindleDepthReach");
        static readonly int FormId = Shader.PropertyToID("_SpindleDepthForm");

        // What makes a material SpindleGraph's (or a graph generated from it): a body on any other
        // shader keeps it, because SpindleDepthGraph would draw it as a spindle.
        static readonly int[] SpindleSignature =
        {
            Shader.PropertyToID("_CellDensity"), Shader.PropertyToID("_SwayAmplitude"),
            Shader.PropertyToID("_DullColor"), Shader.PropertyToID("_PrismClock"),
        };

        // SpindleGraph's own exposed blackboard, which both LOD graphs carry verbatim (they are
        // generated from it by Tools/Shaders/wire_spindle_lod.py): copied from each limb's phase
        // variant onto its LOD clones so a gyroid branch stays a gyroid branch at every tier.
        static readonly int[] CopiedColors = { Shader.PropertyToID("_DullColor"), Shader.PropertyToID("_BrightColor") };
        static readonly int[] CopiedFloats =
        {
            Shader.PropertyToID("_Phase"), Shader.PropertyToID("_SwayAmplitude"), Shader.PropertyToID("_SwayFrequency"),
            Shader.PropertyToID("_CellDensity"), Shader.PropertyToID("_Distance"), Shader.PropertyToID("_DeathAnimation"),
        };

        // Instanced calls are capped at 1023 per call (the bound SnowChanger and NcaCreatureFauna keep to).
        const int MaxInstancesPerCall = 1023;
        // A pose that differs by less than this between passes is still (a float jitter is not a move).
        const float MoveEpsilon = 1e-4f;

        static readonly ProfilerMarker s_tickMarker = new("SpindleLod.Tick");
        static readonly ProfilerMarker s_drainMarker = new("SpindleLod.Drain");
        static readonly ProfilerMarker s_drawMarker = new("SpindleLod.Draw");

        static SpindleLodSystem s_instance;
        static bool s_unavailable;     // the profile is missing, off or broken: stop asking

        // ── Telemetry (EcosystemPerfProbe, the `renderers` census) ──────────────────────────────
        public static int RegisteredCount => s_instance ? s_instance._entries.Count : 0;
        public static int NearCount { get; private set; }
        public static int MidCount { get; private set; }
        public static int FarRendererCount { get; private set; }
        public static int FarBatchedCount { get; private set; }
        public static int CulledCount { get; private set; }
        public static int BodyCount => s_instance ? s_instance._bodies.Count : 0;

        /// <summary>The same line as a property, for a probe that reads static members (Amoebius's eval).</summary>
        public static string Summary => Describe();

        /// <summary>One line for the perf instruments: where every registered limb is drawn.</summary>
        public static string Describe() => s_instance
            ? $"spindle LOD ({(s_instance.Overt ? "overt" : "covert")}): {RegisteredCount:N0} limbs - near {NearCount:N0} · mid {MidCount:N0} · " +
              $"braid {s_tierCount[(int)Tier.Far]:N0} · low {s_tierCount[(int)Tier.Low]:N0} · thread {s_tierCount[(int)Tier.Min]:N0} " +
              $"({FarBatchedCount:N0} of the far ones instanced) · culled {CulledCount:N0} · bodies {BodyCount:N0}"
            : "spindle LOD: off";

        // Limbs per tier, batched or not (the far family's split for Describe).
        static readonly int[] s_tierCount = new int[6];

        SpindleLodProfileSO _profile;
        SpindleLodProfileSO.Style _style;
        TierBounds _bounds;
        readonly Dictionary<Mesh, int> _setOfMesh = new();
        readonly Dictionary<(int, Material, float), TierMaterials> _materials = new();
        readonly List<Material> _ownedMaterials = new();
        readonly List<Entry> _entries = new(1024);
        readonly List<Handle> _pending = new(1024);
        readonly List<Handle> _dead = new();
        readonly List<FarBatch> _batches = new();
        readonly List<Body> _bodies = new();
        readonly Dictionary<(Material, float, float), Material> _bodyMaterials = new();

        // The pass's per-limb columns, parallel to _entries and to _transforms' order. Plain
        // persistent arrays grown by hand rather than NativeLists: the pass WRITES _lastPose in
        // place, and a list's AsArray() is only an alias in Unity's collections (Amoebius copies,
        // which silently loses the write and makes every limb look like it moved).
        TransformAccessArray _transforms;
        NativeArray<float4x4> _lastPose;
        NativeArray<float> _girth;
        NativeArray<byte> _tier;
        float _nextTickAt;
        int _pendingCursor;

        // ── Registration (Spindle) ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers <paramref name="renderer"/>: as a tiered LOD limb if it draws a mesh the profile
        /// has LOD meshes for, otherwise as a BODY (a creature's own spindle-material model) that
        /// wears a depth material. <paramref name="nearMaterial"/> is what it draws up close (its
        /// phase variant); every clone is derived from it. Returns null when neither applies (no
        /// profile, a body on another shader, bodies switched off) - a probe, so it never logs.
        /// </summary>
        public static Handle Register(Renderer renderer, Material nearMaterial, bool fading, bool charge)
        {
            if (!renderer || !nearMaterial || s_unavailable) return null;
            var system = EnsureInstance();
            if (!system) return null;
            float strength = system._profile.StrengthFor(charge);
            if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh &&
                system._setOfMesh.TryGetValue(filter.sharedMesh, out int set))
                return system.Add(renderer, filter, set, nearMaterial, fading, strength);
            return system.AddBody(renderer, nearMaterial, strength);
        }

        /// <summary>
        /// Registers a SWITCH RING drawing the synthetic wire (<c>SpindleLodProfileSO.SyntheticSet</c>)
        /// at <c>switchRingStrength</c>. <paramref name="nearMaterial"/> is the ring's own material on
        /// SpindleWireGraph. Null when the ring's mesh is not the wire or the system cannot run.
        /// </summary>
        public static Handle RegisterSwitchRing(Renderer renderer, Material nearMaterial)
        {
            if (!renderer || !nearMaterial || s_unavailable || !Application.isPlaying) return null;
            var system = EnsureInstance();
            if (!system) return null;
            if (!renderer.TryGetComponent(out MeshFilter filter) || !filter.sharedMesh ||
                !system._setOfMesh.TryGetValue(filter.sharedMesh, out int set) || !system._profile.MeshSets[set].synthetic)
                return null;
            return system.Add(renderer, filter, set, nearMaterial, false, system._profile.SwitchRingStrength);
        }

        /// <summary>
        /// Starts the system if the profile allows it, so the shared depth globals are published for
        /// a surface that is not a spindle at all (the switch rings). False when it cannot run - and
        /// outside Play, where the system (a DontDestroyOnLoad object) cannot exist: an edit-mode
        /// test that builds a ring keeps the prism ring.
        /// </summary>
        public static bool EnsureRunning() => !s_unavailable && Application.isPlaying && EnsureInstance();

        /// <summary>
        /// Returns the limb to its authored mesh and near material and forgets it. With
        /// <paramref name="restore"/> false (the renderer is being destroyed) only the books are
        /// cleared.
        /// </summary>
        public static void Unregister(Handle handle, bool restore)
        {
            if (handle == null || !handle.IsRegistered) return;
            if (handle.body) handle.owner.RemoveBody(handle.index, restore);
            else handle.owner.Remove(handle.index, restore);
        }

        /// <summary>
        /// A limb's per-renderer fade (condense / evaporate) started or settled. A fading limb is
        /// drawn by its own renderer, so it leaves the instanced batch at once.
        /// </summary>
        public static void SetFading(Handle handle, bool fading)
        {
            // A body draws through its own renderer always, so its fade stamp needs nothing here.
            if (handle == null || !handle.IsRegistered || handle.body) return;
            var system = handle.owner;
            int i = handle.index;
            var e = system._entries[i];
            if (e.fading == fading) return;
            e.fading = fading;
            system._entries[i] = e;
            if (fading && e.batched) system.Apply(i, e.tier, batch: false);
        }

        /// <summary>The material this limb should draw with right now (its tier's), or null when
        /// it is not registered - for Spindle's own "restore the material" path.</summary>
        public static Material CurrentMaterial(Handle handle)
        {
            if (handle == null || !handle.IsRegistered) return null;
            if (handle.body) return handle.owner._bodies[handle.index].depth;
            var e = handle.owner._entries[handle.index];
            return MaterialFor(e.materials, e.tier);
        }

        static SpindleLodSystem EnsureInstance()
        {
            if (s_instance) return s_instance;
            if (s_unavailable) return null;

            var profile = SpindleLodProfileSO.Instance;
            if (!profile || !profile.LodEnabled)
            {
                s_unavailable = true;
                return null;
            }
            if (!profile.IsSane(out string why))
            {
                CSDebug.LogWarning($"[SpindleLod] {SpindleLodProfileSO.ResourcePath} is not usable ({why}); every spindle keeps its authored braid.");
                s_unavailable = true;
                return null;
            }

            // Outlives scene loads: it holds limbs from every loaded scene, and each limb leaves it
            // through its own Spindle's OnDisable, so no scene's unload may take the books with it.
            var go = new GameObject("[SpindleLodSystem]");
            DontDestroyOnLoad(go);
            var system = go.AddComponent<SpindleLodSystem>();
            if (!system.Initialize(profile))
            {
                Destroy(go);
                s_unavailable = true;
                return null;
            }
            s_instance = system;
            return system;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_unavailable = false;
            NearCount = MidCount = FarRendererCount = FarBatchedCount = CulledCount = 0;
            System.Array.Clear(s_tierCount, 0, s_tierCount.Length);
        }

        bool Initialize(SpindleLodProfileSO profile)
        {
            _profile = profile;
            if (!profile.MidTemplate.shader || !profile.MidTemplate.shader.isSupported ||
                !profile.FarTemplate.shader || !profile.FarTemplate.shader.isSupported)
            {
                CSDebug.LogWarning("[SpindleLod] a spindle LOD shader is missing or unsupported on this device; every spindle keeps its authored braid.");
                return false;
            }

            for (int s = 0; s < profile.MeshSets.Count; s++)
            {
                var set = profile.MeshSets[s];
                if (set == null) continue;
                // The synthetic wire's tiers all draw on SpindleWireGraph; without it the rings stay
                // whatever ToyFactory gave them (it checks the same template before building a wire).
                if (set.synthetic && (!profile.WireTemplate || !profile.WireTemplate.shader || !profile.WireTemplate.shader.isSupported))
                    continue;
                if (!set.MatchesImport(out string why))
                {
                    // A real fault, so it is loud: the LOD meshes no longer line up with the
                    // braid they replace. Re-run Tools/Build/author_spindle_lod_meshes.py.
                    CSDebug.LogWarning($"[SpindleLod] mesh set {s} ({(set.source ? set.source.name : "none")}) disabled: {why}. Re-run Tools/Build/author_spindle_lod_meshes.py.");
                    continue;
                }
                _setOfMesh[set.source] = s;
            }
            if (_setOfMesh.Count == 0) return false;

            _transforms = new TransformAccessArray(1024);
            Grow(1024);
            _style = profile.LookStyle;
            PublishGlobals();
            return true;
        }

        void PublishGlobals()
        {
            var p = _profile;
            IncorporealDepth.Publish();
            bool overt = Overt;
            Vector4 recede = IncorporealDepth.Recede;
            Shader.SetGlobalVector(BandsId, overt
                ? new Vector4(recede.x, recede.y, p.OvertFadeStartNorm, p.OvertFadeEndNorm)
                : new Vector4(p.ConvergeStartGirths, p.ConvergeEndGirths, p.FarFadeStartGirths, p.FarFadeEndGirths));
            Shader.SetGlobalVector(LookId, new Vector4(p.MinThreadPixels, p.ThreadBase, p.ThreadFacet, p.ThreadGlow));
            Shader.SetGlobalVector(Look2Id, new Vector4(p.ThreadAlpha, overt ? 1f : 0f, p.SwitchRingShadeBase, p.SwitchRingShadeFacet));
            _bounds = BoundsFor(p, overt, IncorporealDepth.Spread, recede);
        }

        bool _warnedOvertBands;

        /// <summary>OVERT needs the prisms' ramp to follow and a recede that fits the ladder; with
        /// nothing published (no Resources/IncorporealDepth, no danger tier on it) or bands out of order,
        /// the style falls back to covert rather than draw a tier outside the range where it matches its
        /// neighbours. The bad-band case is a real fault, so it says so once.</summary>
        bool Overt
        {
            get
            {
                if (_style != SpindleLodProfileSO.Style.Overt || !(IncorporealDepth.Spread.sqrMagnitude > 0f)) return false;
                if (_profile.OvertBandsAreSane(IncorporealDepth.Recede, out string why)) return true;
                if (!_warnedOvertBands)
                {
                    _warnedOvertBands = true;
                    CSDebug.LogWarning($"[SpindleLod] overt style off ({why}); spindles use the covert LOD.");
                }
                return false;
            }
        }

        /// <summary>A style's three boundaries in its own units, and the keep fraction.</summary>
        internal struct TierBounds
        {
            public bool overt;
            public float sqrDistance;
            public float nearEnd;    // Near -> Mid
            public float midEnd;     // Mid  -> Far
            public float lowStart;   // Far  -> Low
            public float minStart;   // Low  -> Min
            public float cull;       // Min  -> Culled
            public float keep;
        }

        /// <summary>
        /// COVERT: girths; near/mid below convergeStart, mid/far at convergeEnd = midEnd x keep.
        /// OVERT, in the prisms' norm: the near -> mid boundary is where the ramp first opens an axis
        /// (both tiers closed inside its band below it), the mid -> far boundary is 1 / keep so its
        /// inward edge is the prisms' over-distance (both tiers fully open inside the band above 1).
        /// Either way the cull's inward edge is the fade's end, where the alpha is zero.
        /// </summary>
        internal static TierBounds BoundsFor(SpindleLodProfileSO p, bool overt, Vector4 spread, Vector4 recede)
        {
            float keep = 1f - p.Hysteresis;
            // OVERT: the 144-triangle braid carries the whole recede, and the 144 -> 72 step's inward
            // edge is the recede's END, where both braids have converged into the thread's own
            // construction (they differ by their decimation alone, under a pixel there); the
            // 72 -> thread step comes at overtThreadStart, and the cull's inward edge is the fade's
            // end. COVERT goes straight from the converging braid to the thread.
            return overt
                ? new TierBounds
                {
                    overt = true, sqrDistance = Mathf.Max(spread.w, 1f), keep = keep,
                    nearEnd = IncorporealDepth.OpeningStartNorm(spread), midEnd = 1f / keep,
                    lowStart = recede.y / keep, minStart = p.OvertThreadStartNorm, cull = p.OvertFadeEndNorm / keep,
                }
                : new TierBounds
                {
                    overt = false, sqrDistance = 1f, keep = keep,
                    nearEnd = p.NearEndGirths, midEnd = p.MidEndGirths, lowStart = p.MidEndGirths, minStart = p.MidEndGirths,
                    cull = p.FarFadeEndGirths / keep,
                };
        }

        /// <summary>The style changed in Play: every limb back to its braid, the far batches onto the
        /// style's far mesh, and the next pass re-tiers everything under the new style's bands.</summary>
        void Restyle()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].tier != Tier.Near || _entries[i].batched) Apply(i, Tier.Near, batch: false);
            _style = _profile.LookStyle;
            PublishGlobals();
            for (int b = 0; b < _batches.Count; b++)
                _batches[b].mesh = TierMesh(_profile.MeshSets[_batches[b].set], _batches[b].tier);
            _nextTickAt = 0f;
        }

        /// <summary>
        /// The mesh a tier draws. OVERT: Far is the SAME braid the mid tier draws (the peak, where the
        /// fan is widest and the two must open identically, and the recede that closes it), Low the
        /// 72-triangle braid once it has settled, Min the 12-triangle thread. COVERT never uses Far or
        /// Low (zero-width bands).
        /// </summary>
        Mesh TierMesh(SpindleLodProfileSO.MeshSet set, Tier tier) => tier switch
        {
            Tier.Near => set.source,
            Tier.Mid => set.mid,
            Tier.Far => Overt ? set.mid : set.far,
            Tier.Low => Overt ? set.low : set.far,
            _ => set.far,
        };

        void Grow(int capacity)
        {
            var pose = new NativeArray<float4x4>(capacity, Allocator.Persistent);
            var girth = new NativeArray<float>(capacity, Allocator.Persistent);
            var tier = new NativeArray<byte>(capacity, Allocator.Persistent);
            int n = _entries.Count;
            if (_lastPose.IsCreated)
            {
                NativeArray<float4x4>.Copy(_lastPose, pose, n);
                NativeArray<float>.Copy(_girth, girth, n);
                NativeArray<byte>.Copy(_tier, tier, n);
                _lastPose.Dispose();
                _girth.Dispose();
                _tier.Dispose();
            }
            _lastPose = pose;
            _girth = girth;
            _tier = tier;
        }

        Handle Add(Renderer renderer, MeshFilter filter, int set, Material nearMaterial, bool fading, float strength)
        {
            int i = _entries.Count;
            if (i >= _lastPose.Length) Grow(_lastPose.Length * 2);
            var handle = new Handle { owner = this, index = i };
            _entries.Add(new Entry
            {
                handle = handle,
                renderer = renderer,
                filter = filter,
                set = set,
                materials = MaterialsFor(set, nearMaterial, strength),
                tier = Tier.Near,
                fading = fading,
                batchSlot = -1,
            });
            _transforms.Add(renderer.transform);
            _lastPose[i] = float4x4.identity;
            _girth[i] = _profile.MeshSets[set].girth;
            _tier[i] = (byte)Tier.Near;
            NearCount++;
            s_tierCount[(int)Tier.Near]++;
            return handle;
        }

        void Remove(int i, bool restore)
        {
            var e = _entries[i];
            if (e.batched) LeaveBatch(i);
            if (restore && e.renderer && e.filter && e.materials.near)
            {
                e.filter.sharedMesh = _profile.MeshSets[e.set].source;
                e.renderer.sharedMaterial = e.materials.near;
                e.renderer.ResetLocalBounds();
                e.renderer.enabled = true;
            }
            CountOut(e.tier, e.batched);

            int last = _entries.Count - 1;
            if (i != last)
            {
                var moved = _entries[last];
                _entries[i] = moved;
                moved.handle.index = i;
                if (moved.batched) moved.batch.entries[moved.batchSlot] = i;
            }
            _entries.RemoveAt(last);
            _transforms.RemoveAtSwapBack(i);
            _lastPose[i] = _lastPose[last];
            _girth[i] = _girth[last];
            _tier[i] = _tier[last];
            e.handle.index = -1;
            e.handle.owner = null;
        }

        // ── Bodies (Docs/SPINDLE_LOD.md §8) ─────────────────────────────────────────────────────

        Handle AddBody(Renderer renderer, Material near, float strength)
        {
            if (!_profile.DepthOnBodies || !_profile.DepthTemplate || !IsSpindleShaded(near)) return null;
            if (!_profile.DepthTemplate.shader || !_profile.DepthTemplate.shader.isSupported) return null;

            float reach = ReachBucket(ReachOf(renderer));
            var depth = BodyMaterialFor(near, strength, reach);
            var handle = new Handle { owner = this, index = _bodies.Count, body = true };
            _bodies.Add(new Body { handle = handle, renderer = renderer, near = near, depth = depth });
            renderer.sharedMaterial = depth;
            // The bloom moves the surface up to its travel off the mesh, in the vertex shader where
            // culling cannot see it: without room in the bounds a creature just off screen would
            // lose the parts that bloomed into view.
            ExpandLocalBounds(renderer, IncorporealDepth.BloomTravel(strength, reach));
            return handle;
        }

        void RemoveBody(int i, bool restore)
        {
            var b = _bodies[i];
            if (restore && b.renderer && b.near)
            {
                b.renderer.sharedMaterial = b.near;
                b.renderer.ResetLocalBounds();
            }
            int last = _bodies.Count - 1;
            if (i != last)
            {
                var moved = _bodies[last];
                _bodies[i] = moved;
                moved.handle.index = i;
            }
            _bodies.RemoveAt(last);
            b.handle.index = -1;
            b.handle.owner = null;
        }

        static bool IsSpindleShaded(Material m)
        {
            for (int i = 0; i < SpindleSignature.Length; i++)
                if (!m.HasProperty(SpindleSignature[i])) return false;
            return true;
        }

        /// <summary>
        /// How far the body reaches from the pivot it is drawn about, in world units: the bounds'
        /// farthest corner. A skinned body is drawn about its ROOT BONE, so that is the pivot.
        /// </summary>
        static float ReachOf(Renderer renderer)
        {
            Transform pivot = renderer is SkinnedMeshRenderer skinned && skinned.rootBone ? skinned.rootBone : renderer.transform;
            var b = renderer.bounds;
            Vector3 p = pivot.position;
            float reach = 0f;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y,
                                         (c & 4) == 0 ? b.min.z : b.max.z);
                reach = Mathf.Max(reach, (corner - p).magnitude);
            }
            return reach;
        }

        /// <summary>Reach rounded up to a half-octave (x1.41), so bodies of a size share a material.</summary>
        internal static float ReachBucket(float reach) =>
            Mathf.Pow(2f, Mathf.Ceil(Mathf.Log(Mathf.Max(reach, 1f), 2f) * 2f) / 2f);

        Material BodyMaterialFor(Material near, float strength, float reach)
        {
            var key = (near, strength, reach);
            if (_bodyMaterials.TryGetValue(key, out var m) && m) return m;
            // The body's own look - every property, keyword and the render queue - on the depth graph,
            // which carries SpindleGraph's blackboard verbatim.
            m = new Material(_profile.DepthTemplate) { name = near.name + "_Depth" };
            m.CopyPropertiesFromMaterial(near);
            m.SetFloat(StrengthId, strength);
            m.SetFloat(ReachId, reach);
            m.SetFloat(FormId, 0f);
            m.renderQueue = near.renderQueue;
            _ownedMaterials.Add(m);
            _bodyMaterials[key] = m;
            return m;
        }

        /// <summary>Grow a renderer's local bounds by <paramref name="travel"/> world units each way.</summary>
        static void ExpandLocalBounds(Renderer renderer, float travel)
        {
            if (!(travel > 0f)) return;
            Transform space = renderer is SkinnedMeshRenderer skinned && skinned.rootBone ? skinned.rootBone : renderer.transform;
            Vector3 s = space.lossyScale;
            var b = renderer.localBounds;
            b.Expand(new Vector3(2f * travel / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
                                 2f * travel / Mathf.Max(Mathf.Abs(s.y), 1e-4f),
                                 2f * travel / Mathf.Max(Mathf.Abs(s.z), 1e-4f)));
            renderer.localBounds = b;
        }

        // ── Materials ───────────────────────────────────────────────────────────────────────────

        TierMaterials MaterialsFor(int set, Material near, float strength)
        {
            if (_materials.TryGetValue((set, near, strength), out var m) && m.mid && m.far) return m;
            var ms = _profile.MeshSets[set];
            // A synthetic wire draws every tier on SpindleWireGraph: it never trades its solid look for
            // the plain thread's fade.
            var midTemplate = ms.synthetic ? _profile.WireTemplate : _profile.MidTemplate;
            var farTemplate = ms.synthetic ? _profile.WireTemplate : _profile.FarTemplate;
            m = new TierMaterials
            {
                near = near,
                strength = strength,
                mid = Derive(midTemplate, near, ms, "_LodMid", instanced: false, strength),
                far = Derive(farTemplate, near, ms, "_LodFar", instanced: true, strength),
            };
            m.farBatch = NewBatch(set, Tier.Far, m);
            m.lowBatch = NewBatch(set, Tier.Low, m);
            m.minBatch = NewBatch(set, Tier.Min, m);
            _materials[(set, near, strength)] = m;
            return m;
        }

        FarBatch NewBatch(int set, Tier tier, TierMaterials m)
        {
            var b = new FarBatch
            {
                set = set, tier = tier, strength = m.strength, material = m.far,
                mesh = TierMesh(_profile.MeshSets[set], tier),
                renderParams = new RenderParams(m.far) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
            };
            _batches.Add(b);
            return b;
        }

        Material Derive(Material template, Material near, SpindleLodProfileSO.MeshSet set, string suffix, bool instanced, float strength)
        {
            var mat = new Material(template) { name = near.name + suffix, enableInstancing = instanced };
            for (int i = 0; i < CopiedColors.Length; i++)
                if (near.HasProperty(CopiedColors[i])) mat.SetColor(CopiedColors[i], near.GetColor(CopiedColors[i]));
            for (int i = 0; i < CopiedFloats.Length; i++)
                if (near.HasProperty(CopiedFloats[i])) mat.SetFloat(CopiedFloats[i], near.GetFloat(CopiedFloats[i]));
            mat.SetFloat(GirthId, set.girth);
            mat.SetFloat(StrandId, set.strandRadius);
            mat.SetFloat(StrengthId, strength);
            mat.renderQueue = near.renderQueue;
            _ownedMaterials.Add(mat);
            return mat;
        }

        static Material MaterialFor(TierMaterials m, Tier tier) => tier switch
        {
            Tier.Near => m.near,
            Tier.Mid => m.mid,
            _ => m.far,
        };

        // ── The pass ────────────────────────────────────────────────────────────────────────────

        void Update()
        {
            if (!_profile || !_profile.LodEnabled)
            {
                RestoreAllAndRetire();
                return;
            }

            if (_profile.LookStyle != _style) Restyle();

            if (Time.unscaledTime >= _nextTickAt)
            {
                _nextTickAt = Time.unscaledTime + _profile.TickIntervalSeconds;
                // Bodies and switch rings read only the globals, so they are kept current even
                // when no limb is registered.
                if (_entries.Count > 0) Tick();
                else PublishGlobals();
            }
            Drain();
        }

        void LateUpdate() => Draw();

        void Tick()
        {
            var cam = CameraManager.ViewCamera;
            if (!cam) return;

            using (s_tickMarker.Auto())
            {
                PublishGlobals();
                int n = _entries.Count;
                var desired = new NativeArray<byte>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var moved = new NativeArray<byte>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var valid = new NativeArray<byte>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new ClassifyJob
                {
                    Camera = cam.transform.position,
                    Overt = _bounds.overt,
                    SqrDistance = _bounds.sqrDistance,
                    Bounds = _bounds,
                    Girth = _girth.GetSubArray(0, n),
                    Current = _tier.GetSubArray(0, n),
                    LastPose = _lastPose.GetSubArray(0, n),
                    Desired = desired,
                    Moved = moved,
                    Valid = valid,
                }.ScheduleReadOnly(_transforms, 64).Complete();

                _pending.Clear();
                _pendingCursor = 0;
                _dead.Clear();
                bool batchFar = _profile.BatchFarThreads;
                for (int i = 0; i < n; i++)
                {
                    var e = _entries[i];
                    if (valid[i] == 0 || !e.renderer)
                    {
                        // Its Spindle unregisters it on the way out; this only catches a renderer
                        // destroyed without its Spindle (an editor tool, a hand-deleted child).
                        _dead.Add(e.handle);
                        continue;
                    }
                    bool still = moved[i] == 0;
                    var want = Kept((Tier)desired[i], _profile.MeshSets[e.set].synthetic);
                    bool wantBatch = batchFar && IsFarFamily(want) && still && e.stillOnce && !e.fading;

                    if (e.stillOnce != still)
                    {
                        e.stillOnce = still;
                        _entries[i] = e;
                    }
                    // Safety direction first, unbudgeted: a batched limb that moved is drawn by its
                    // own renderer from this frame on, so the batch never draws it where it was.
                    if (e.batched && !still)
                    {
                        Apply(i, want, batch: false);
                        continue;
                    }
                    if (want != e.tier || wantBatch != e.batched)
                        _pending.Add(e.handle);
                }
                desired.Dispose();
                moved.Dispose();
                valid.Dispose();
                for (int d = 0; d < _dead.Count; d++)
                    if (_dead[d].IsRegistered) Remove(_dead[d].index, restore: false);
            }
        }

        void Drain()
        {
            if (_pendingCursor >= _pending.Count) return;
            using (s_drainMarker.Auto())
            {
                int budget = _profile.MaxTransitionsPerFrame;
                // Count AND time: a swap costs several renderer writes, so the count alone let one
                // frame spend 14.7 ms here. What is left waits for the next frame, invisibly.
                long deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
                                (long)(_profile.MaxTransitionMsPerFrame * 0.001 * System.Diagnostics.Stopwatch.Frequency);
                var cam = CameraManager.ViewCamera;
                Vector3 camPos = cam ? cam.transform.position : Vector3.zero;
                while (_pendingCursor < _pending.Count && budget > 0)
                {
                    // Read the clock every 16 swaps: a timestamp is cheap, not free.
                    if ((budget & 15) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() >= deadline) break;
                    var h = _pending[_pendingCursor++];
                    if (!h.IsRegistered || h.owner != this) continue;
                    int i = h.index;
                    var e = _entries[i];
                    if (!e.renderer) continue;
                    // Re-derive from the pass's verdict, which _tier does not yet hold for a limb
                    // still waiting: recompute it here from the pose the pass read.
                    var want = Classify(i, camPos);
                    bool wantBatch = _profile.BatchFarThreads && IsFarFamily(want) && e.stillOnce && !e.fading;
                    if (want == e.tier && wantBatch == e.batched) continue;
                    Apply(i, want, wantBatch);
                    budget--;
                }
            }
        }

        Tier Classify(int i, Vector3 camPos)
        {
            float metric = ClassifyJob.Metric(_lastPose[i], camPos, _girth[i], _bounds.overt, _bounds.sqrDistance);
            return Kept((Tier)ClassifyJob.TierFor(metric, _tier[i], _bounds), _profile.MeshSets[_entries[i].set].synthetic);
        }

        /// <summary>A synthetic wire (a switch ring) is never culled: past the cull it stays the thread,
        /// which on SpindleWireGraph never fades.</summary>
        internal static Tier Kept(Tier tier, bool synthetic) => synthetic && tier == Tier.Culled ? Tier.Min : tier;

        /// <summary>The far -> culled boundary, set so its INWARD edge (bound x keep) is exactly
        /// farFadeEnd: a thread is only ever switched back on where its alpha is still zero, and
        /// fades in from there. (Every boundary is placed by the same rule: near/mid swaps happen
        /// below convergeStart, mid/far ones above convergeEnd = midEnd x keep.)</summary>
        internal static float CullBoundFor(SpindleLodProfileSO p) => p.FarFadeEndGirths / (1f - p.Hysteresis);

        /// <summary>The tier a limb at <paramref name="metric"/> (its style's units) moves to from
        /// <paramref name="current"/> under <paramref name="b"/> - the Burst pass's own rule.</summary>
        internal static Tier TierFor(float metric, Tier current, TierBounds b) =>
            (Tier)ClassifyJob.TierFor(metric, (byte)current, b);

        void Apply(int i, Tier tier, bool batch)
        {
            var e = _entries[i];
            CountOut(e.tier, e.batched);
            // Leave the batch on any change: a far-family step moves the limb to another tier's batch.
            if (e.batched) LeaveBatch(i);
            e = _entries[i];

            var set = _profile.MeshSets[e.set];
            switch (tier)
            {
                case Tier.Near:
                    e.filter.sharedMesh = set.source;
                    e.renderer.sharedMaterial = e.materials.near;
                    e.renderer.ResetLocalBounds();
                    e.renderer.enabled = true;
                    break;
                case Tier.Mid:
                    e.filter.sharedMesh = set.mid;
                    e.renderer.sharedMaterial = e.materials.mid;
                    FitBounds(e.renderer, set.mid, e.materials.strength);
                    e.renderer.enabled = true;
                    break;
                case Tier.Far:
                case Tier.Low:
                case Tier.Min:
                    var mesh = TierMesh(set, tier);
                    e.filter.sharedMesh = mesh;
                    e.renderer.sharedMaterial = e.materials.far;
                    FitBounds(e.renderer, mesh, e.materials.strength);
                    e.renderer.enabled = !batch;
                    break;
                default:
                    e.renderer.enabled = false;
                    break;
            }
            e.tier = tier;
            _tier[i] = (byte)tier;
            _entries[i] = e;
            if (batch) JoinBatch(i, e.materials.BatchFor(tier));
            e = _entries[i];
            CountIn(e.tier, e.batched);
        }

        /// <summary>
        /// An overt limb's faces travel up to <see cref="IncorporealDepth.MaxOpening"/> world units off
        /// its mesh, in the vertex shader, where culling cannot see them: without room in the bounds the
        /// opened slats of a limb just off screen would blink out at the frame edge. Covert needs none.
        /// </summary>
        void FitBounds(Renderer renderer, Mesh mesh, float strength)
        {
            if (!Overt) { renderer.ResetLocalBounds(); return; }
            var b = mesh.bounds;
            Vector3 s = renderer.transform.lossyScale;
            float open = IncorporealDepth.MaxOpening * strength;
            b.Expand(new Vector3(2f * open / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
                                 2f * open / Mathf.Max(Mathf.Abs(s.y), 1e-4f),
                                 2f * open / Mathf.Max(Mathf.Abs(s.z), 1e-4f)));
            renderer.localBounds = b;
        }

        void JoinBatch(int i, FarBatch b)
        {
            var e = _entries[i];
            e.batch = b;
            e.batched = true;
            e.batchSlot = b.entries.Count;
            _entries[i] = e;
            b.entries.Add(i);
            // Incremental, never a rebuild: a joining limb writes its own slot and grows the box. A
            // full Rebuild per changed frame walked every limb of the batch (18,893 instanced limbs
            // in Wrecking Ball: SpindleLod.Draw 9.3 ms/frame while the drain was busy).
            if (b.dirty) return;
            if (b.entries.Count == 1) { b.dirty = true; return; }
            if (b.matrices.Length < b.entries.Count)
                System.Array.Resize(ref b.matrices, Mathf.NextPowerOfTwo(b.entries.Count));
            var m = PoseMatrix(_lastPose[i]);
            b.matrices[e.batchSlot] = m;
            var box = InstanceBox(b.mesh.bounds, m);
            box.Expand(InstancePad(b));
            b.bounds.Encapsulate(box);
            b.renderParams.worldBounds = b.bounds;
        }

        void LeaveBatch(int i)
        {
            var e = _entries[i];
            var b = e.batch;
            int slot = e.batchSlot;
            int last = b.entries.Count - 1;
            if (slot != last)
            {
                int movedEntry = b.entries[last];
                b.entries[slot] = movedEntry;
                var me = _entries[movedEntry];
                me.batchSlot = slot;
                _entries[movedEntry] = me;
                // A batch waiting for its Rebuild owns no live matrices: JoinBatch returns before
                // growing the array while it is dirty, so `last` can lie past its end (Bug Ledger
                // E-7cbbaadd58, Garrett: IndexOutOfRange here in a busy drain). Rebuild rewrites every
                // slot from the poses, so nothing needs carrying across.
                if (!b.dirty) b.matrices[slot] = b.matrices[last];
            }
            b.entries.RemoveAt(last);
            // The box is kept: it only over-covers (a little culling, never a missing limb), and is
            // fitted again once half the batch has left.
            if (++b.leftSinceRebuild * 2 > b.entries.Count) b.dirty = true;
            e.batched = false;
            e.batchSlot = -1;
            e.batch = null;
            _entries[i] = e;
        }

        void CountIn(Tier tier, bool batched)
        {
            s_tierCount[(int)tier]++;
            switch (tier)
            {
                case Tier.Near: NearCount++; break;
                case Tier.Mid: MidCount++; break;
                case Tier.Culled: CulledCount++; break;
                default: if (batched) FarBatchedCount++; else FarRendererCount++; break;
            }
        }

        void CountOut(Tier tier, bool batched)
        {
            s_tierCount[(int)tier]--;
            switch (tier)
            {
                case Tier.Near: NearCount--; break;
                case Tier.Mid: MidCount--; break;
                case Tier.Culled: CulledCount--; break;
                default: if (batched) FarBatchedCount--; else FarRendererCount--; break;
            }
        }

        // ── The instanced far threads ───────────────────────────────────────────────────────────

        void Draw()
        {
            using (s_drawMarker.Auto())
            {
                for (int b = 0; b < _batches.Count; b++)
                {
                    var batch = _batches[b];
                    int count = batch.entries.Count;
                    if (count == 0 || !batch.mesh || !batch.material) continue;
                    if (batch.dirty) Rebuild(batch);
                    for (int start = 0; start < count; start += MaxInstancesPerCall)
                        Graphics.RenderMeshInstanced(batch.renderParams, batch.mesh, 0, batch.matrices,
                            Mathf.Min(MaxInstancesPerCall, count - start), start);
                }
            }
        }

        void Rebuild(FarBatch batch)
        {
            int count = batch.entries.Count;
            if (batch.matrices.Length < count)
                batch.matrices = new Matrix4x4[Mathf.NextPowerOfTwo(count)];
            var meshBounds = batch.mesh.bounds;
            Bounds world = default;
            for (int j = 0; j < count; j++)
            {
                var m = PoseMatrix(_lastPose[batch.entries[j]]);
                batch.matrices[j] = m;
                var box = InstanceBox(meshBounds, m);
                if (j == 0) world = box; else world.Encapsulate(box);
            }
            world.Expand(world.size.magnitude * 0.05f + InstancePad(batch));
            batch.bounds = world;
            batch.renderParams.worldBounds = world;
            batch.dirty = false;
            batch.leftSinceRebuild = 0;
        }

        static Matrix4x4 PoseMatrix(float4x4 p) => new(
            new Vector4(p.c0.x, p.c0.y, p.c0.z, p.c0.w), new Vector4(p.c1.x, p.c1.y, p.c1.z, p.c1.w),
            new Vector4(p.c2.x, p.c2.y, p.c2.z, p.c2.w), new Vector4(p.c3.x, p.c3.y, p.c3.z, p.c3.w));

        // The instance's world box, conservatively: its mesh box's eight corners through its pose
        // would be exact; centre +- the transformed half-diagonal is enough for a culling box and
        // cheaper. The sway and the width hold add a little - InstancePad covers them.
        static Bounds InstanceBox(Bounds meshBounds, Matrix4x4 m)
        {
            Vector3 c = m.MultiplyPoint3x4(meshBounds.center);
            Vector3 r = Vector3.one * m.MultiplyVector(meshBounds.extents).magnitude;
            return new Bounds(c, r * 2f);
        }

        float InstancePad(FarBatch batch) => 1f + (Overt ? 2f * IncorporealDepth.MaxOpening * batch.strength : 0f);

        // ── Teardown ────────────────────────────────────────────────────────────────────────────

        void RestoreAllAndRetire()
        {
            for (int i = _entries.Count - 1; i >= 0; i--) Remove(i, restore: true);
            for (int i = _bodies.Count - 1; i >= 0; i--) RemoveBody(i, restore: true);
            s_unavailable = true;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            // Scene teardown: the renderers are going down with it, so only the books are cleared.
            for (int i = 0; i < _entries.Count; i++)
            {
                var h = _entries[i].handle;
                h.index = -1;
                h.owner = null;
            }
            _entries.Clear();
            for (int i = 0; i < _bodies.Count; i++)
            {
                var h = _bodies[i].handle;
                h.index = -1;
                h.owner = null;
            }
            _bodies.Clear();
            _bodyMaterials.Clear();
            _batches.Clear();
            if (_transforms.isCreated) _transforms.Dispose();
            if (_lastPose.IsCreated) _lastPose.Dispose();
            if (_girth.IsCreated) _girth.Dispose();
            if (_tier.IsCreated) _tier.Dispose();
            for (int i = 0; i < _ownedMaterials.Count; i++)
                if (_ownedMaterials[i]) Destroy(_ownedMaterials[i]);
            _ownedMaterials.Clear();
            _materials.Clear();
            NearCount = MidCount = FarRendererCount = FarBatchedCount = CulledCount = 0;
            System.Array.Clear(s_tierCount, 0, s_tierCount.Length);
        }

        // ── The Burst pass ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every limb's pose, distance in girths, tier under hysteresis, and whether it moved.
        /// Read-only over the transforms; each index writes only its own slots.
        /// </summary>
        [BurstCompile]
        internal struct ClassifyJob : IJobParallelForTransform
        {
            public float3 Camera;
            public bool Overt;
            public float SqrDistance;
            public TierBounds Bounds;
            [ReadOnly] public NativeArray<float> Girth;
            [ReadOnly] public NativeArray<byte> Current;
            public NativeArray<float4x4> LastPose;
            [WriteOnly] public NativeArray<byte> Desired;
            [WriteOnly] public NativeArray<byte> Moved;
            [WriteOnly] public NativeArray<byte> Valid;

            public void Execute(int i, TransformAccess transform)
            {
                if (!transform.isValid)
                {
                    Valid[i] = 0;
                    Desired[i] = Current[i];
                    Moved[i] = 1;
                    return;
                }
                float4x4 pose = transform.localToWorldMatrix;
                float4x4 last = LastPose[i];
                float4 d = math.abs(pose.c0 - last.c0) + math.abs(pose.c1 - last.c1) +
                           math.abs(pose.c2 - last.c2) + math.abs(pose.c3 - last.c3);
                Moved[i] = (byte)(math.cmax(d) > MoveEpsilon ? 1 : 0);
                LastPose[i] = pose;
                Valid[i] = 1;

                Desired[i] = TierFor(Metric(pose, Camera, Girth[i], Overt, SqrDistance), Current[i], Bounds);
            }

            /// <summary>The style's distance, measured exactly as the shader measures it (the object
            /// origin): girths, or the prisms' normalised squared distance.</summary>
            public static float Metric(float4x4 pose, float3 camera, float girth, bool overt, float sqrDistance)
            {
                float3 d = pose.c3.xyz - camera;
                if (overt) return math.dot(d, d) / math.max(sqrDistance, 1f);
                float scale = math.max(math.min(math.length(pose.c0.xyz), math.length(pose.c1.xyz)), 1e-6f);
                return math.length(d) / math.max(girth * scale, 1e-6f);
            }

            /// <summary>Outward a limb moves past a boundary; inward it must come back inside
            /// boundary x keep. Between the two it stays where it is - and both tiers draw the
            /// same picture there, which is the whole reason the swap is invisible.</summary>
            public static byte TierFor(float metric, byte current, TierBounds b)
            {
                int t = current;
                if (t > (int)Tier.Culled) t = (int)Tier.Near;
                while (t < (int)Tier.Culled && metric > Bound(t, b)) t++;
                while (t > (int)Tier.Near && metric < Bound(t - 1, b) * b.keep) t--;
                return (byte)t;
            }

            /// <summary>The boundary leaving tier <paramref name="t"/> outward.</summary>
            static float Bound(int t, TierBounds b) => t switch
            {
                0 => b.nearEnd,
                1 => b.midEnd,
                2 => b.lowStart,
                3 => b.minStart,
                _ => b.cull,
            };
        }
    }
}
