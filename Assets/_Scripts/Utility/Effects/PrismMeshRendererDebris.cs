using System.Collections.Generic;
using CosmicShore.ECS;
using CosmicShore.ScriptableObjects;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The death-visual carrier for a device where Entities Graphics cannot run (a WebGL2 browser —
    /// Docs/PRISM_ANIMATION.md §4.11): a small, capped pool of plain MeshRenderer GameObjects, each holding ONE
    /// pose and ONE clock stamp in its own MaterialPropertyBlock, exactly the initial conditions the batched
    /// entity debris (<see cref="PrismDebris"/>, <see cref="PrismShieldShatter"/>) would have stamped.
    ///
    /// <para>Why a new carrier and not the retired pooled <see cref="PrismExplosion"/>: that component pays
    /// OnEnable/OnDisable registry churn, a pressure model sized for 256 live effects and a per-effect timer
    /// entry, and its pool is deliberately never prewarmed. This carrier pays one renderer toggle and one block
    /// write per death, a flat end-time sweep (bookkeeping, never progress polling) and — for a suction only —
    /// the §1 moving-target exception, one <c>_Location</c> write per live suction per frame.</para>
    ///
    /// <para>Bounded by construction: at most <see cref="PrismRenderConfigSO.MeshRendererDebrisCap"/> carriers
    /// ever exist, created on first need and never destroyed. A death that arrives while all are live is
    /// skipped — the request still reports success, so the factory does not raise its "no visual" error for a
    /// budget that is working as designed. Under pressure the pieces shorten (smaller, quicker puffs) so the
    /// cap recycles, the same reading the retired pooled path's pressure model had.</para>
    ///
    /// <para>Never used where Entities Graphics runs: every entry point is gated on
    /// <see cref="PrismMeshRendererStamps.Active"/> by its caller.</para>
    /// </summary>
    public static class PrismMeshRendererDebris
    {
        /// <summary>Shortest a pressured piece is squeezed to — the same floor the retired pooled path used.</summary>
        internal const float MinPressuredDuration = 0.33f;

        /// <summary>Fallback cap when no PrismRenderConfig asset exists.</summary>
        internal const int DefaultCap = 96;

        /// <summary>Object-space slack around a suction's convergence point (mirrors PrismDebris).</summary>
        const float ConvergenceBoundsPadding = 2f;

        sealed class Carrier
        {
            public GameObject Go;
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
        }

        struct Live
        {
            public int Slot;
            public float EndTime;
            /// <summary>Non-null only for a suction still tracking a living target.</summary>
            public Transform Target;
            public bool Suction;
        }

        static readonly List<Carrier> s_carriers = new(DefaultCap);
        static readonly Stack<int> s_free = new(DefaultCap);
        static readonly List<Live> s_live = new(DefaultCap);
        static Transform s_root;
        static TickHost s_host;
        static int s_cap = -1;
        static bool s_quitting;

        /// <summary>Death visuals currently drawing on this path (diagnostics).</summary>
        public static int LiveCount => s_live.Count;

        /// <summary>Deaths skipped this session because every carrier was live (diagnostics).</summary>
        public static int SkippedOverCap { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_carriers.Clear();
            s_free.Clear();
            s_live.Clear();
            s_root = null;
            s_host = null;
            s_cap = -1;
            s_quitting = false;
            SkippedOverCap = 0;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
        }

        static void HandleQuitting() => s_quitting = true;

        static int Cap
        {
            get
            {
                if (s_cap >= 0) return s_cap;
                var config = Resources.Load<PrismRenderConfigSO>("PrismRenderConfig");
                s_cap = config != null ? config.MeshRendererDebrisCap : DefaultCap;
                return s_cap;
            }
        }

        /// <summary>
        /// A piece's animation length when <paramref name="live"/> carriers are already drawing out of
        /// <paramref name="cap"/>: full until half the cap, then eased down to <see cref="MinPressuredDuration"/>
        /// (never lengthened). Pure, for the tests.
        /// </summary>
        internal static float PressuredDuration(float full, int live, int cap)
        {
            float floor = Mathf.Min(full, MinPressuredDuration);
            if (cap <= 0) return floor;
            float half = cap * 0.5f;
            if (live <= half) return full;
            return Mathf.Lerp(full, floor, Mathf.InverseLerp(half, cap, live));
        }

        // ── Requests ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One explosion piece: every argument is what the entity spawn (<c>ExplosionDebrisSpawn</c>) would have
        /// carried, already computed by the caller with the entity path's own arithmetic. Returns false only when
        /// the carrier cannot exist (no mesh/material, quitting); a skipped-over-cap death returns true.
        /// </summary>
        internal static bool TrySpawnExplosion(Mesh mesh, Material material, int layer,
            Vector3 position, Quaternion rotation, Vector3 scale, Color bright, Color dark,
            Vector3 velocity, float speed, float duration, in float3 objectDisplacement, float boundsPadding,
            float facePivotFromCentroid)
        {
            if (!TryTake(mesh, material, out int slot, out var c, out bool overCap)) return overCap;

            float life = PressuredDuration(duration, s_live.Count, Cap);
            Pose(c, mesh, material, layer, position, rotation, scale);

            c.Block.Clear();
            PrismMeshRendererStamps.WriteTeamColors(c.Block, PrismRenderService.ToFloat4(bright),
                PrismRenderService.ToFloat4(dark));
            PrismMeshRendererStamps.WriteExplosionClock(c.Block, PrismClock.Now, speed, life,
                new float3(velocity.x, velocity.y, velocity.z), facePivotFromCentroid);
            c.Renderer.SetPropertyBlock(c.Block);
            c.Renderer.localBounds = PrismMeshRendererStamps.ExpandedBounds(mesh.bounds, in objectDisplacement,
                boundsPadding);
            c.Renderer.enabled = true;

            s_live.Add(new Live { Slot = slot, EndTime = PrismClock.Now + life });
            return true;
        }

        /// <summary>
        /// One suction (a consumed prism): progress 0→1 over <paramref name="duration"/>, converging on
        /// <paramref name="target"/>, which is RETAINED so the sink follows the creature (the §1 exception).
        /// Same return contract as <see cref="TrySpawnExplosion"/>.
        /// </summary>
        internal static bool TrySpawnImplosion(Mesh mesh, Material material, int layer,
            Vector3 position, Quaternion rotation, Vector3 scale, Color bright, Color dark,
            Transform target, float duration)
        {
            if (target == null) return false;
            if (!TryTake(mesh, material, out int slot, out var c, out bool overCap)) return overCap;

            float life = PressuredDuration(duration, s_live.Count, Cap);
            Pose(c, mesh, material, layer, position, rotation, scale);

            Vector3 location = target.position;
            c.Block.Clear();
            PrismMeshRendererStamps.WriteTeamColors(c.Block, PrismRenderService.ToFloat4(bright),
                PrismRenderService.ToFloat4(dark));
            PrismMeshRendererStamps.WriteSuctionClock(c.Block, PrismClock.Now, life, 1f, 0f,
                new float3(location.x, location.y, location.z));
            c.Renderer.SetPropertyBlock(c.Block);

            c.Renderer.ResetLocalBounds();
            Encapsulate(c, location);
            c.Renderer.enabled = true;

            s_live.Add(new Live { Slot = slot, EndTime = PrismClock.Now + life, Target = target, Suction = true });
            return true;
        }

        // ── Pool ─────────────────────────────────────────────────────────────────────────────────

        static bool TryTake(Mesh mesh, Material material, out int slot, out Carrier carrier, out bool overCap)
        {
            slot = -1;
            carrier = null;
            overCap = false;
            if (s_quitting || !Application.isPlaying || mesh == null || material == null) return false;
            EnsureHost();
            if (s_root == null) return false;

            if (s_free.Count == 0)
            {
                if (s_carriers.Count >= Cap)
                {
                    if (SkippedOverCap == 0)
                        CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                            $"[PrismDebris] MeshRenderer death carrier full ({Cap} live): deaths past the cap " +
                            "have no visual until one retires (Docs/PRISM_ANIMATION.md §4.11).");
                    SkippedOverCap++;
                    overCap = true;
                    return false;
                }
                s_free.Push(CreateCarrier());
            }

            slot = s_free.Pop();
            carrier = s_carriers[slot];
            return true;
        }

        static int CreateCarrier()
        {
            var go = new GameObject("PrismDebrisPiece");
            go.transform.SetParent(s_root, false);
            var c = new Carrier
            {
                Go = go,
                Transform = go.transform,
                Filter = go.AddComponent<MeshFilter>(),
                Renderer = go.AddComponent<MeshRenderer>(),
                Block = new MaterialPropertyBlock(),
            };
            // Same render flags the entity prototypes get (RenderMeshDescription(ShadowCastingMode.Off,
            // receiveShadows: false), probes off).
            c.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            c.Renderer.receiveShadows = false;
            c.Renderer.lightProbeUsage = LightProbeUsage.Off;
            c.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            c.Renderer.enabled = false;
            s_carriers.Add(c);
            return s_carriers.Count - 1;
        }

        static void Pose(Carrier c, Mesh mesh, Material material, int layer,
            Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!ReferenceEquals(c.Filter.sharedMesh, mesh)) c.Filter.sharedMesh = mesh;
            if (!ReferenceEquals(c.Renderer.sharedMaterial, material)) c.Renderer.sharedMaterial = material;
            if (c.Go.layer != layer) c.Go.layer = layer;
            c.Transform.SetPositionAndRotation(position, rotation);
            c.Transform.localScale = scale;
        }

        static void Retire(int slot)
        {
            var c = s_carriers[slot];
            if (c.Renderer != null)
            {
                c.Renderer.enabled = false;
                c.Block.Clear();
                c.Renderer.SetPropertyBlock(null);
                c.Renderer.ResetLocalBounds();
            }
            s_free.Push(slot);
        }

        static void Encapsulate(Carrier c, Vector3 worldPoint)
        {
            var p = c.Transform.InverseTransformPoint(worldPoint);
            PrismMeshRendererStamps.EncapsulateBoundsPoint(c.Renderer, new float3(p.x, p.y, p.z),
                ConvergenceBoundsPadding);
        }

        // ── Per-frame drive ──────────────────────────────────────────────────────────────────────

        static void EnsureHost()
        {
            if (s_host != null) return;
            if (s_quitting || !Application.isPlaying) return;
            // HideInHierarchy, not HideAndDontSave — same reasoning as PrismDebris' host.
            var go = new GameObject("[PrismMeshRendererDebris]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_root = go.transform;
            s_host = go.AddComponent<TickHost>();
        }

        // Same slot as PrismDebris' host: after gameplay has queued this frame's deaths, before rendering.
        [DefaultExecutionOrder(29000)]
        sealed class TickHost : MonoBehaviour
        {
            void LateUpdate() => Tick();

            void OnDestroy()
            {
                if (s_host == this) { s_host = null; s_root = null; s_carriers.Clear(); s_free.Clear(); s_live.Clear(); }
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_tickMarker = new("PrismMeshRendererDebris.Tick");

        /// <summary>
        /// Retires every piece whose clock ran out (a flat end-time compare over at most the cap) and, for each
        /// live suction whose target still exists, writes the target's current position — the §1 exception.
        /// </summary>
        static void Tick()
        {
            if (s_live.Count == 0) return;
            using (s_tickMarker.Auto())
            {
                float now = PrismClock.Now;
                for (int i = s_live.Count - 1; i >= 0; i--)
                {
                    var rec = s_live[i];
                    if (rec.EndTime <= now)
                    {
                        Retire(rec.Slot);
                        int last = s_live.Count - 1;
                        s_live[i] = s_live[last];
                        s_live.RemoveAt(last);
                        continue;
                    }

                    if (!rec.Suction || rec.Target is null) continue;
                    if (rec.Target == null)
                    {
                        // Gone mid-suction: freeze at the last known point (the entity path's degradation).
                        rec.Target = null;
                        s_live[i] = rec;
                        continue;
                    }

                    var c = s_carriers[rec.Slot];
                    Vector3 world = rec.Target.position;
                    PrismMeshRendererStamps.WriteSuctionLocation(c.Block, new float3(world.x, world.y, world.z));
                    c.Renderer.SetPropertyBlock(c.Block);
                    Encapsulate(c, world);
                }
            }
        }
    }
}
