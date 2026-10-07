using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CPU half of the black hole's WARP (Docs/BLACK_HOLE.md §5, Docs/PRISM_ANIMATION.md
    /// §4.7.4): mass near a horizon is drawn tidally stretched toward the singularity —
    /// spaghettification — by <c>PrismGravityWarp.hlsl</c> in the vertex stage of both live
    /// prism graphs. Photons only: nothing here changes where any prism IS; the gravity field
    /// does that, and the two are deliberately separate systems so the bend can be tuned,
    /// switched off, or judged on look without touching the physics.
    ///
    /// It does exactly two things per frame, the same two the cradle does and for the same
    /// reasons (<see cref="PrismCradle"/>):
    ///
    /// <para><b>1. The bank (the animation).</b> One slot per live hole — centre + horizon in one
    /// float4, strain + reach in another — published once per frame as file-scope shader
    /// globals, with a master sentinel carrying the live count so an unpublished bank reads as
    /// "the loop does not execute". "Where is the hole relative to this prism" is live data no
    /// stamp could carry, so it is a §4.7 global: O(1) writes that every prism reads.</para>
    ///
    /// <para><b>2. Residency (a state change).</b> The prisms nearest a horizon are swapped to the
    /// high-poly copy of the identical solid (<see cref="HighPolyPrismMesh"/>) so the stretch
    /// bends a surface instead of hinging 24 triangles — through the platform's shared-mesh
    /// handoff, strictly outside the volume the warp can move anything, budgeted, nearest first.
    /// The warp's field DECAYS with distance from the horizon, so nearest-to-horizon is the right
    /// residency key (the skill's §4.2 rule; a travelling front would rank differently).</para>
    ///
    /// A despawning hole keeps publishing with a falling weight until its ease completes, so the
    /// bend lets go instead of snapping; the field has already stopped pulling by then.
    /// </summary>
    public static class BlackHoleWarp
    {
        /// <summary>
        /// Mirrors <c>PRISM_GRAVITY_WARP_SLOTS</c> in <c>PrismGravityWarp.hlsl</c> and
        /// <see cref="BlackHolePhysics.NativeWells.Capacity"/> — change all three together.
        /// </summary>
        public const int Slots = 4;

        static readonly int CentreId = Shader.PropertyToID("_PrismGravityWarpCentre");
        static readonly int WeightId = Shader.PropertyToID("_PrismGravityWarpWeight");
        static readonly int ParamsId = Shader.PropertyToID("_PrismGravityWarpParams");

        // Always sent at full length: Unity binds an array global at the length of its first
        // write, so a short write later would silently leave the tail of the previous bank live.
        static readonly Vector4[] _centre = new Vector4[Slots];
        static readonly Vector4[] _weight = new Vector4[Slots];
        static int _publishedCount;

        static readonly HashSet<Prism> _resident = new();
        static readonly HashSet<Prism> _wanted = new();
        static readonly List<Prism> _query = new();
        static readonly List<Prism> _candidates = new();
        static readonly List<Prism> _evict = new();
        static Vector3 _sortOrigin;
        static readonly System.Comparison<Prism> _byDistance = CompareByDistance;

        /// <summary>True while any hole is publishing a live slot.</summary>
        public static bool IsActive => _publishedCount > 0;

        /// <summary>How many prisms currently hold the warp's high-poly mesh (diagnostics, tests).</summary>
        public static int ResidentPrismCount => _resident.Count;

        /// <summary>
        /// Pack this frame's holes into the bank and reconcile residency. <paramref name="holes"/>
        /// is every hole with a non-zero warp weight — live ones and those easing out.
        /// </summary>
        public static void Flush(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config)
        {
            bool enabled = config.WarpEnabled && config.IsSane;
            int count = 0;
            if (enabled)
            {
                for (int i = 0; i < holes.Count && count < Slots; i++)
                {
                    var h = holes[i];
                    if (h == null) continue;
                    float w = h.WarpWeight;
                    if (w <= 0.001f) continue;
                    var p = h.transform.position;
                    _centre[count] = new Vector4(p.x, p.y, p.z, h.HorizonRadius);
                    // Strain is scaled HERE, on the way into the bank: the shader's contract is
                    // "this is the strain at the horizon", and how strong a full-weight warp is
                    // is a tuning fact about the look (the cradle applies MaxStrength the same way).
                    _weight[count] = new Vector4(config.WarpStrength * w, h.WarpReach, 0f, 0f);
                    count++;
                }
            }
            for (int i = count; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _weight[i] = Vector4.zero;
            }

            ReconcileResidency(config, enabled, count);

            // Nothing to say and nothing said last frame: the write is skipped entirely, so a
            // match with no hole in it costs this system nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            // y is the shader's MASTER sentinel: 0 means "the loop does not execute".
            Shader.SetGlobalVector(ParamsId, new Vector4(config.WarpExponent, count, 0f, 0f));
            _publishedCount = count;
        }

        // ---------------- Residency ----------------

        static void ReconcileResidency(BlackHoleConfigSO config, bool enabled, int liveSlots)
        {
            _wanted.Clear();

            if (enabled && liveSlots > 0 && config.WarpMaxResidentPrisms > 0)
            {
                var index = PrismSpatialIndex.Instance;
                if (index != null && index.IsAvailable)
                {
                    var mesh = HighPolyPrismMesh.Get(config.WarpSubdivision);

                    for (int s = 0; s < liveSlots; s++)
                    {
                        Vector4 slot = _centre[s];
                        var centre = new Vector3(slot.x, slot.y, slot.z);
                        // Strictly outside the volume the warp can move anything: horizon + reach + margin.
                        float radius = slot.w + _weight[s].y + config.WarpResidencyMargin;
                        if (!(radius > 0f)) continue;

                        index.QuerySphere(centre, radius, _query);
                        if (_query.Count == 0) continue;

                        _candidates.Clear();
                        for (int i = 0; i < _query.Count; i++)
                        {
                            var p = _query[i];
                            if (p == null) continue;
                            if (_wanted.Contains(p)) continue;
                            // Someone else owns this prism's override — a settled shield, or the
                            // cradle's own high-poly mesh at its subdivision. Leave it.
                            if (p.RenderMeshOverride != null && !ReferenceEquals(p.RenderMeshOverride, mesh))
                                continue;
                            _candidates.Add(p);
                        }

                        _sortOrigin = centre;
                        _candidates.Sort(_byDistance);

                        int room = config.WarpMaxResidentPrisms - _wanted.Count;
                        int take = Mathf.Min(room, _candidates.Count);
                        for (int i = 0; i < take; i++)
                            _wanted.Add(_candidates[i]);

                        if (_wanted.Count >= config.WarpMaxResidentPrisms) break;
                    }

                    foreach (var p in _wanted)
                    {
                        if (_resident.Contains(p)) continue;
                        p.SetRenderMeshOverride(mesh);
                        _resident.Add(p);
                    }
                }
            }

            if (_resident.Count == 0) return;

            _evict.Clear();
            foreach (var p in _resident)
                if (p == null || !_wanted.Contains(p))
                    _evict.Add(p);

            for (int i = 0; i < _evict.Count; i++)
            {
                var p = _evict[i];
                _resident.Remove(p);
                if (p == null) continue;
                // Only clear an override that is still OURS (a shield may have taken the slot).
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

        /// <summary>Hand every resident prism its own mesh back (teardown — the prisms are alive).</summary>
        internal static void ReleaseAllResidents()
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

        internal static void PublishOff()
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
        /// Play-mode (re)entry: drop the bookkeeping WITHOUT touching the previous session's
        /// prisms (they are gone), and publish the off state before anything renders.
        /// </summary>
        internal static void ResetOnLoad()
        {
            _resident.Clear();
            _wanted.Clear();
            _query.Clear();
            _candidates.Clear();
            _evict.Clear();
            PublishOff();
        }
    }
}
