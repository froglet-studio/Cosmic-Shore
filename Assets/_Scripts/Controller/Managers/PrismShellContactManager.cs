using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shielded-prism analytic-collision tier ("collision LOD" tier 3).
    ///
    /// A shielded prism's visible shell — the octahedron (SHIELDED) or the
    /// non-convex stellated octahedron (SUPER-SHIELDED) — is 3× its authored box,
    /// but its PhysX collider stays the authored box trigger (see
    /// PrismOctahedronShield: a convex-mesh trigger is invisible to trigger
    /// skimmers, a convex-mesh solid is invisible to solid swipes). This manager
    /// makes the SHELL the interaction surface without touching PhysX: each frame
    /// it rebuilds a small probe set from the live vessel-hull / skimmer colliders,
    /// runs one Burst query against <see cref="PrismSpatialIndex"/>'s shell view
    /// (exact sphere/capsule/OBB vs octahedron and vs the two-tet stella UNION —
    /// a probe touching a spike tip hits, a probe threaded between spikes inside
    /// the bounding box does not), and dispatches enter transitions through the
    /// same AcceptImpactee effect chain the trigger path uses.
    ///
    /// The trigger path stays authoritative for everything else: unshielded
    /// prisms, projectiles, crystals, prism-side OnTriggerEnter/Exit. While a
    /// prism's shell owns contact (<see cref="ShellOwnsContact"/>), Skimmer- and
    /// VesselImpactor suppress their box-trigger prism dispatch so the pair can
    /// never double-fire; when a hit pops the shield the flags clear the same
    /// frame, this tier stops owning the prism, and a genuine later box contact
    /// re-enters through PhysX — the one-swing pop-then-destroy stays emergent.
    ///
    /// Runs in Update (per render frame, ~60 Hz — finer temporal sampling than
    /// the 25 Hz physics tick the trigger path samples at). Cost is one
    /// O(highWaterMark) Burst scan (flag byte early-out) plus exact narrowphase
    /// only for shield-flagged slots near a probe — bounded O(near/active),
    /// never per-pair managed callbacks. Markers: ShellContact.Build,
    /// ShellContact.Query (in the index), ShellContact.Dispatch.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismShellContactManager : Singleton<PrismShellContactManager>
    {
        /// <summary>
        /// A/B switch mirroring ExplosionImpactor.ForceLegacyPhysics: true reverts
        /// to the pre-shell behavior (shielded prisms interact at the authored box
        /// via triggers; this tier goes fully inert). For in-editor verification.
        /// </summary>
        public static bool ForceLegacyBoxInteraction;

        /// <summary>
        /// EXPERIMENTAL, default OFF. Extends this tier from shielded prisms to EVERY
        /// registered prism, so vessel/skimmer contact resolves against the spatial
        /// index at render rate instead of against PhysX box triggers at the 25 Hz
        /// physics tick.
        ///
        /// It buys nothing in SHAPE for an unshielded prism — its exact surface already
        /// IS its box, which is what PhysX tests. What it buys is sampling rate, contact
        /// determinism across peers, and the ability to own a prism whose COLLIDER
        /// cannot be trusted (one posed per-frame from the GPU). What it costs is the
        /// flag-byte early-out in ShellContactQueryJob: the scan goes from one byte read
        /// per slot to a bound test per slot per probe, with no broadphase under it.
        ///
        /// Toggle only through <see cref="SetExtendToUnshieldedPrisms"/> — the shell view
        /// is maintained incrementally and a policy change has no incremental event to
        /// ride.
        /// </summary>
        public static bool ExtendToUnshieldedPrisms { get; private set; }

        /// <summary>Per-frame diagnostics for the contact probe. Off by default; reading
        /// these while false gives the last values, not live ones.</summary>
        public static bool CollectDiagnostics;
        public static int LastProbeCount, LastHitCount, LastActivePairs, LastEnterDispatches;
        public static int PeakHitCount, PeakActivePairs;
        public static double LastBuildMs, LastQueryMs, LastDispatchMs;
        public static double PeakQueryMs;

        // A/B verification switches; must not survive into a normal session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForceLegacy()
        {
            ForceLegacyBoxInteraction = false;
            ExtendToUnshieldedPrisms = false;
            CollectDiagnostics = false;
            ResetPeaks();
        }

        public static void ResetPeaks()
        {
            PeakHitCount = PeakActivePairs = 0;
            PeakQueryMs = 0d;
        }

        /// <summary>
        /// Flips extended coverage and rebuilds the shell view, because which slots HAVE
        /// a shell is exactly what changed. Live pairs are dropped with exit bookkeeping
        /// so no contact survives the policy change in a half-owned state.
        /// </summary>
        public static void SetExtendToUnshieldedPrisms(bool on)
        {
            if (ExtendToUnshieldedPrisms == on) return;
            ExtendToUnshieldedPrisms = on;
            Instance?.DropAllPairs();
            PrismSpatialIndex.Instance?.RebuildAllShells();
            ResetPeaks();
        }

        struct OwnerEntry
        {
            public ImpactorBase Owner;
            public Collider[] Colliders;
        }

        class ActivePair
        {
            public ImpactorBase Owner;
            public PrismImpactor PrismImpactor;
            public Prism Prism;
            public int LastSeenFrame;
        }

        static readonly List<OwnerEntry> s_owners = new(16);

        static readonly ProfilerMarker s_buildMarker = new("ShellContact.Build");
        static readonly ProfilerMarker s_dispatchMarker = new("ShellContact.Dispatch");

        NativeArray<ShellProbe> _probes;
        NativeList<ShellContactHit> _hits;
        int _probeCount;

        readonly List<ImpactorBase> _frameOwners = new(16);
        readonly Dictionary<long, ActivePair> _activePairs = new(64);
        readonly List<long> _staleKeys = new(32);
        readonly List<ActivePair> _redispatchBuffer = new(8);
        int _enterDispatches;

        public static PrismShellContactManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            // Ride the spatial index's host GameObject - one bootstrap path,
            // present exactly in prism-bearing scenes (PrismColliderLodManager
            // precedent).
            var host = PrismSpatialIndex.EnsureInstance().gameObject;
            if (!host.TryGetComponent(out PrismShellContactManager _))
                host.AddComponent<PrismShellContactManager>();
            return Instance;
        }

        /// <summary>
        /// True while the prism's engaged shell is the interaction surface for
        /// vessels and skimmers. Read by SkimmerImpactor/VesselImpactor to suppress
        /// their box-trigger prism dispatch (the shell tier owns the pair), and by
        /// this manager to validate hits whose shield popped earlier in the same
        /// resolve pass (a pop cannot destroy the freshly unshielded prism through
        /// a stale same-frame hit).
        /// </summary>
        public static bool ShellOwnsContact(Prism prism)
        {
            if (ForceLegacyBoxInteraction) return false;
            if (prism == null) return false;
            var props = prism.prismProperties;
            if (props != null && (props.IsShielded || props.IsSuperShielded)) return true;
            if (!ExtendToUnshieldedPrisms) return false;
            // Claim an unshielded prism ONLY if the index really carries a shell for it.
            // Claiming one without a shell suppresses the trigger path for a prism the
            // query can never hit, which is an uninteractable prism.
            if (prism.destroyed || prism.SpatialIndexId < 0) return false;
            var index = PrismSpatialIndex.Instance;
            return index != null && index.HasShell(prism.SpatialIndexId);
        }

        /// <summary>
        /// Registers an impactor's collider set as shell probes. The set is cached
        /// by the caller (hull colliders never change at runtime); world poses are
        /// re-read every frame, so runtime scale drivers (elemental skimmer growth,
        /// the Rhino shield-scale driver) are picked up with zero events.
        /// </summary>
        public static void RegisterProbeOwner(ImpactorBase owner, Collider[] colliders)
        {
            if (owner == null || colliders == null || colliders.Length == 0) return;
            for (int i = 0; i < s_owners.Count; i++)
                if (ReferenceEquals(s_owners[i].Owner, owner))
                    return;
            s_owners.Add(new OwnerEntry { Owner = owner, Colliders = colliders });
            EnsureInstance();
        }

        /// <summary>
        /// Re-dispatches this owner's LIVE shell-owned pairs through the same
        /// AcceptImpactee chain their entry used. The shell tier dispatches each pair
        /// exactly once on ENTRY, so an impactor whose OWN state changes mid-contact —
        /// the Rhino blade ENERGIZING against a resting super-shielded prism — calls
        /// this to have the standing contact re-evaluated without waiting for an
        /// exit/re-enter. Pairs whose prism died or whose shield dropped since entry
        /// are skipped (the trigger path owns them again).
        /// </summary>
        public static void RedispatchPairsForOwner(ImpactorBase owner)
        {
            var inst = Instance;
            if (inst == null || owner == null || ForceLegacyBoxInteraction) return;
            if (inst._activePairs.Count == 0) return;

            // Snapshot first: a dispatch can pop/destroy prisms, and nothing may mutate
            // _activePairs while we enumerate it.
            inst._redispatchBuffer.Clear();
            foreach (var kvp in inst._activePairs)
            {
                if (ReferenceEquals(kvp.Value.Owner, owner))
                    inst._redispatchBuffer.Add(kvp.Value);
            }

            for (int i = 0; i < inst._redispatchBuffer.Count; i++)
            {
                var pair = inst._redispatchBuffer[i];
                var prism = pair.Prism;
                if (prism == null || prism.destroyed || !ShellOwnsContact(prism))
                    continue;
                owner.AcceptImpacteeFromShellContact(pair.PrismImpactor);
            }
            inst._redispatchBuffer.Clear();
        }

        /// <summary>
        /// Appends every LIVE super-shielded prism this owner's probes are currently inside to
        /// <paramref name="into"/> (not cleared first) and returns how many were added.
        ///
        /// The shell tier dispatches a pair ONCE, on entry, so an impactor that needs a
        /// continuous "am I still inside it?" answer — the Rhino's blade binding in armour it
        /// cannot cut (RHINO_ENERGY_SWORD.md § "Binding") — reads the live pair set here rather
        /// than keeping its own copy, which would need its own exit test and could disagree with
        /// this one. The set is exactly what <see cref="SweepStalePairs"/> keeps: a pair leaves it
        /// the first frame its probes stop overlapping the shell.
        ///
        /// Always zero under <see cref="ForceLegacyBoxInteraction"/>: the A/B box path has no
        /// persistent pair set, so a binding reads as "never inside" there.
        /// </summary>
        public static int CollectSuperShieldedContactsForOwner(ImpactorBase owner, List<Prism> into)
        {
            var inst = Instance;
            if (inst == null || owner == null || into == null || ForceLegacyBoxInteraction) return 0;
            if (inst._activePairs.Count == 0) return 0;

            int added = 0;
            foreach (var kvp in inst._activePairs)
            {
                var pair = kvp.Value;
                if (!ReferenceEquals(pair.Owner, owner)) continue;
                var prism = pair.Prism;
                if (prism == null || prism.destroyed) continue;
                if (prism.prismProperties is not { IsSuperShielded: true }) continue;
                into.Add(prism);
                added++;
            }
            return added;
        }

        public static void UnregisterProbeOwner(ImpactorBase owner)
        {
            for (int i = 0; i < s_owners.Count; i++)
            {
                if (ReferenceEquals(s_owners[i].Owner, owner))
                {
                    s_owners.RemoveAt(i);
                    break;
                }
            }
            // Drop this owner's live pairs immediately (with exit bookkeeping) so a
            // vessel swap/despawn can't leave phantom contacts behind.
            Instance?.DropPairsForOwner(owner);
        }

        public override void Awake()
        {
            base.Awake();
            _probes = new NativeArray<ShellProbe>(32, Allocator.Persistent);
            _hits = new NativeList<ShellContactHit>(256, Allocator.Persistent);
        }

        void OnDestroy()
        {
            if (_probes.IsCreated) _probes.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
            _activePairs.Clear();
        }

        void Update()
        {
            var index = PrismSpatialIndex.Instance;
            if (ForceLegacyBoxInteraction || index == null || !index.IsAvailable || s_owners.Count == 0)
            {
                DropAllPairs();
                return;
            }

            var sw = CollectDiagnostics ? System.Diagnostics.Stopwatch.StartNew() : null;

            using (s_buildMarker.Auto())
                BuildProbes();

            if (sw != null) { LastBuildMs = sw.Elapsed.TotalMilliseconds; sw.Restart(); }

            if (_probeCount == 0)
            {
                DropAllPairs();
                return;
            }

            index.CollectShellContacts(_probes, _probeCount, _hits);

            if (sw != null)
            {
                LastQueryMs = sw.Elapsed.TotalMilliseconds;
                if (LastQueryMs > PeakQueryMs) PeakQueryMs = LastQueryMs;
                sw.Restart();
            }

            _enterDispatches = 0;
            using (s_dispatchMarker.Auto())
            {
                ResolveContacts(index);
                SweepStalePairs();
            }

            if (sw != null)
            {
                LastDispatchMs = sw.Elapsed.TotalMilliseconds;
                LastProbeCount = _probeCount;
                LastHitCount = _hits.Length;
                LastActivePairs = _activePairs.Count;
                LastEnterDispatches = _enterDispatches;
                if (LastHitCount > PeakHitCount) PeakHitCount = LastHitCount;
                if (LastActivePairs > PeakActivePairs) PeakActivePairs = LastActivePairs;
            }
        }

        // ------------------------------------------------------------------
        // Probe building (world poses re-read every frame)
        // ------------------------------------------------------------------

        void BuildProbes()
        {
            _probeCount = 0;
            _frameOwners.Clear();

            for (int i = 0; i < s_owners.Count; i++)
            {
                var entry = s_owners[i];
                var owner = entry.Owner;
                // Same gate as OnTriggerEnter: an uninitialized impactor (e.g. a
                // skimmer whose Player deactivated mid-scene) contributes no probes;
                // its pairs age out through the sweep like a trigger going silent.
                if (owner == null || !owner.isActiveAndEnabled || !owner.IsInitializedForImpact)
                    continue;

                int ownerSlot = _frameOwners.Count;
                _frameOwners.Add(owner);

                var colliders = entry.Colliders;
                for (int c = 0; c < colliders.Length; c++)
                {
                    var col = colliders[c];
                    if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                        continue;
                    AppendProbe(col, ownerSlot);
                }
            }
        }

        void AppendProbe(Collider col, int ownerSlot)
        {
            if (_probeCount >= _probes.Length)
            {
                var grown = new NativeArray<ShellProbe>(Mathf.NextPowerOfTwo(_probeCount + 1), Allocator.Persistent);
                NativeArray<ShellProbe>.Copy(_probes, grown, _probes.Length);
                _probes.Dispose();
                _probes = grown;
            }

            Transform t = col.transform;
            Vector3 lossy = t.lossyScale;
            var probe = new ShellProbe { OwnerSlot = ownerSlot };

            switch (col)
            {
                case SphereCollider sc:
                {
                    // PhysX sphere rule: radius scales by the max absolute axis.
                    float3 center = t.TransformPoint(sc.center);
                    float r = sc.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
                    probe.Kind = ShellProbeKind.Sphere;
                    probe.A = center;
                    probe.Radius = r;
                    probe.BoundCenter = center;
                    probe.BoundRadius = r;
                    break;
                }
                case CapsuleCollider cc:
                {
                    // PhysX capsule rule: radius scales by the max of the two
                    // perpendicular axes, height by its own axis (the Rhino sword's
                    // non-uniform {1.5, 30, 4.8} needs this exactly).
                    int dir = cc.direction; // 0 X, 1 Y, 2 Z
                    float sAxis = Mathf.Abs(dir == 0 ? lossy.x : dir == 1 ? lossy.y : lossy.z);
                    float sRad = dir == 0 ? Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z))
                               : dir == 1 ? Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z))
                                          : Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
                    float r = cc.radius * sRad;
                    float halfSeg = Mathf.Max(0f, cc.height * sAxis * 0.5f - r);
                    Vector3 localAxis = dir == 0 ? Vector3.right : dir == 1 ? Vector3.up : Vector3.forward;
                    float3 center = t.TransformPoint(cc.center);
                    float3 axis = ((float3)t.TransformDirection(localAxis)) * halfSeg;
                    probe.Kind = ShellProbeKind.Capsule;
                    probe.A = center - axis;
                    probe.B = center + axis;
                    probe.Radius = r;
                    probe.BoundCenter = center;
                    probe.BoundRadius = halfSeg + r;
                    break;
                }
                case BoxCollider bc:
                {
                    float3 center = t.TransformPoint(bc.center);
                    float3 e1 = t.TransformVector(new Vector3(bc.size.x * 0.5f, 0f, 0f));
                    float3 e2 = t.TransformVector(new Vector3(0f, bc.size.y * 0.5f, 0f));
                    float3 e3 = t.TransformVector(new Vector3(0f, 0f, bc.size.z * 0.5f));
                    probe.Kind = ShellProbeKind.Box;
                    probe.A = center;
                    probe.E1 = e1;
                    probe.E2 = e2;
                    probe.E3 = e3;
                    probe.BoundCenter = center;
                    probe.BoundRadius = math.sqrt(math.lengthsq(e1) + math.lengthsq(e2) + math.lengthsq(e3));
                    break;
                }
                default:
                {
                    // Exotic collider type: approximate by its world AABB as an
                    // axis-aligned box probe (conservative over-cover on the PROBE
                    // side only; the shell side stays exact).
                    Bounds b = col.bounds;
                    float3 ext = b.extents;
                    probe.Kind = ShellProbeKind.Box;
                    probe.A = b.center;
                    probe.E1 = new float3(ext.x, 0f, 0f);
                    probe.E2 = new float3(0f, ext.y, 0f);
                    probe.E3 = new float3(0f, 0f, ext.z);
                    probe.BoundCenter = b.center;
                    probe.BoundRadius = math.length(ext);
                    break;
                }
            }

            _probes[_probeCount++] = probe;
        }

        // ------------------------------------------------------------------
        // Contact resolution (enter dispatch + mark) and exit sweep
        // ------------------------------------------------------------------

        static long PairKey(ImpactorBase owner, Prism prism)
            => ((long)owner.GetInstanceID() << 32) ^ (uint)prism.GetInstanceID();

        void ResolveContacts(PrismSpatialIndex index)
        {
            int frame = Time.frameCount;

            for (int i = 0; i < _hits.Length; i++)
            {
                var hit = _hits[i];
                var owner = _frameOwners[_probes[hit.ProbeIndex].OwnerSlot];
                var prism = index.GetRegisteredPrism(hit.PrismIndex);

                // A dispatch earlier in this loop may have popped/destroyed the
                // prism - a stale same-frame hit must not dispatch against the
                // freshly unshielded prism (the trigger path takes over via a
                // genuine later box enter).
                if (prism == null || prism.destroyed || !ShellOwnsContact(prism))
                    continue;

                long key = PairKey(owner, prism);
                if (_activePairs.TryGetValue(key, out var pair))
                {
                    pair.LastSeenFrame = frame;
                    continue;
                }

                // Exact parity with the trigger path's impactee precondition:
                // OnTriggerEnter resolves the impactee through ImpactCollider
                // (ImpactorBase.cs), so a prism without a wired ImpactCollider must
                // not gain brand-new interactions from the shell tier either.
                if (!prism.TryGetComponent(out ImpactCollider impactCollider)
                    || impactCollider.Impactor is not PrismImpactor prismImpactor)
                    continue;

                _activePairs.Add(key, new ActivePair
                {
                    Owner = owner,
                    PrismImpactor = prismImpactor,
                    Prism = prism,
                    LastSeenFrame = frame
                });

                _enterDispatches++;
                owner.AcceptImpacteeFromShellContact(prismImpactor);
            }
        }

        void SweepStalePairs()
        {
            int frame = Time.frameCount;
            _staleKeys.Clear();

            foreach (var kvp in _activePairs)
            {
                var pair = kvp.Value;
                if (pair.LastSeenFrame == frame && pair.Owner != null && pair.Prism != null)
                    continue;
                _staleKeys.Add(kvp.Key);
                if (pair.Owner != null && pair.PrismImpactor != null)
                    pair.Owner.NotifyShellContactExit(pair.PrismImpactor);
            }

            for (int i = 0; i < _staleKeys.Count; i++)
                _activePairs.Remove(_staleKeys[i]);
        }

        void DropAllPairs()
        {
            if (_activePairs.Count == 0) return;
            _staleKeys.Clear();
            foreach (var kvp in _activePairs)
            {
                var pair = kvp.Value;
                _staleKeys.Add(kvp.Key);
                if (pair.Owner != null && pair.PrismImpactor != null)
                    pair.Owner.NotifyShellContactExit(pair.PrismImpactor);
            }
            for (int i = 0; i < _staleKeys.Count; i++)
                _activePairs.Remove(_staleKeys[i]);
        }

        void DropPairsForOwner(ImpactorBase owner)
        {
            if (_activePairs.Count == 0) return;
            _staleKeys.Clear();
            foreach (var kvp in _activePairs)
            {
                var pair = kvp.Value;
                if (!ReferenceEquals(pair.Owner, owner))
                    continue;
                _staleKeys.Add(kvp.Key);
                if (pair.Owner != null && pair.PrismImpactor != null)
                    pair.Owner.NotifyShellContactExit(pair.PrismImpactor);
            }
            for (int i = 0; i < _staleKeys.Count; i++)
                _activePairs.Remove(_staleKeys[i]);
        }
    }
}
