using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the visual and physical transition between a prism's unshielded
    /// box state and its super-shielded stellated octahedron (Stella Octangula)
    /// state. Mirrors <see cref="PrismOctahedronShield"/> but uses the
    /// stellation - the compound of two interpenetrating tetrahedra whose
    /// intersection is the octahedron shield and whose union has spike tips
    /// at the 8 cube corners.
    ///
    /// States:
    ///   Unshielded:        authored BoxCollider (trigger) active, authored prism mesh visible,
    ///                      mass = ρ · 8·a·b·c
    ///   Super-shielded:    authored BoxCollider (trigger) STAYS the collider (a convex-mesh
    ///                      trigger is invisible to trigger skimmers; the primitive box is what
    ///                      both trigger and solid impactors detect), stellation mesh visible,
    ///                      mass = ρ · 108·a·b·c (exactly 13.5× box mass by default,
    ///                      3× the inscribed octahedron shield's mass)
    ///
    /// Engage: per-face bloom morph - 24 outer faces grow outward from their
    /// centroids.
    /// Disengage: box mesh snaps back immediately, then a shatter overlay plays
    ///   where each of the 24 faces simultaneously shrinks and flies outward
    ///   along its face normal — and, when the caller hands over the velocity of the force
    ///   that BROKE the shield, drifting and tumbling along that blow. That is not
    ///   "mirroring" the prism destruction VFX any more; it is the SAME motion model,
    ///   applied per face (Docs/PRISM_ANIMATION.md §4.8.1). Zero velocity is the
    ///   identity, so a direction-less disengage stays the symmetric puff it always was.
    ///
    /// BOTH MORPHS RUN ON THE GPU — one stamp per transition, no ticker, no per-frame
    /// mesh rebuild, everything final at t = 0, and the shield never leaves the
    /// instanced path because the morph is evaluated on the cache-SHARED settled
    /// stellation (per-face centroids in TEXCOORD1). The full rationale, and the two
    /// consequences that matter when editing either tier, are on
    /// <see cref="PrismOctahedronShield"/>; this class is deliberately its mirror so the
    /// two cannot drift. Docs/PRISM_ANIMATION.md §5 B4.
    ///
    /// Fast overlap test: <see cref="IsPointInsideShield"/> uses the
    /// 4-linear-form tetrahedral check from
    /// <see cref="StellatedOctahedronMeshGenerator.ContainsPointLocal"/> for
    /// gameplay queries that don't need a full physics collider.
    ///
    /// Note on terminology: the existing <see cref="PrismOctahedronShield"/>'s
    /// docstring calls the octahedron state "supershielded"; in the broader
    /// design language used here, "super-shielded" refers specifically to this
    /// stellated state, with the octahedron being merely "shielded".
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismStellatedOctahedronShield : MonoBehaviour
    {
        [Header("Collider Sources")]
        [Tooltip("The authored BoxCollider that defines the unshielded shape. Its center/size drive the stellation geometry.")]
        [SerializeField] private BoxCollider boxCollider;

        [Tooltip("MeshCollider used for the super-shielded state. Auto-created if null. Convex is required for Rigidbody interaction.")]
        [SerializeField] private MeshCollider shieldMeshCollider;

        [Header("Rendering")]
        [Tooltip("MeshFilter whose mesh is swapped between the authored prism mesh and the generated stellation mesh.")]
        [SerializeField] private MeshFilter meshFilter;

        [Tooltip("Optional override material for the super-shielded visual. If null, the existing MeshRenderer materials are reused.")]
        [SerializeField] private Material shieldMaterialOverride;

        [Header("Physics")]
        [Tooltip("Optional Rigidbody whose mass scales with shield state. If null, mass scaling is skipped.")]
        [SerializeField] private Rigidbody rb;

        [Tooltip("Uniform density (kg / unit^3) used for mass = density · volume. Set negative to disable density-based mass and use massRatioSuperShielded instead.")]
        [SerializeField] private float density = 1f;

        [Tooltip("Multiplier applied to the unshielded (box) mass when entering the super-shielded state. Default 13.5 matches V_stellated / V_box = 108·a·b·c / 8·a·b·c.")]
        [SerializeField] private float massRatioSuperShielded = StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_BOX_VOLUME_RATIO;

        [Header("Engage Transition")]
        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly. Easing is smoothstep on the GPU, which IS AnimationCurve.EaseInOut(0,0,1,1) — the curve every runtime-added shield used. The retired curve FIELD is gone: the GPU cannot evaluate an arbitrary AnimationCurve.")]
        [SerializeField] private float engageDuration = 0.45f;

        [Header("Shatter (Disengage)")]
        [Tooltip("Duration of the shatter VFX overlay after disengaging. 0 snaps instantly. Defaults to PrismExplosion.DefaultDuration - a shield coming apart and a prism coming apart are the same event class, and they read wrong at different lengths. Note the tumble is a RATE (rad/second), so lengthening this spins the faces further.")]
        [SerializeField] private float shatterDuration = PrismExplosion.DefaultDuration;

        [Tooltip("How far each face flies outward (in local-space units) at the end of the shatter.")]
        [SerializeField] private float shatterMaxOffset = 4f;

        [Tooltip("Ceiling (world units/second) on the drift the shards inherit from the force " +
                 "that BROKE the shield — the prism explosion's velocity, applied per face. " +
                 "This is the ONE dial for how violently the stellation comes apart: the tumble " +
                 "angle rides the same clamped speed. 0 disables both terms, leaving the " +
                 "symmetric outward puff, which is also what every direction-less disengage " +
                 "(a shield timer expiring, an arena teardown, a herbivore stripping armour) " +
                 "already gets. Impact magnitudes are not comparable across the legacy and " +
                 "true-velocity damage paths, so only the DIRECTION survives unclamped.")]
        [SerializeField] private float shatterDriftSpeedCap = 25.0f;

        [Header("Shield Geometry")]
        [Tooltip("Circumscribing scale factor for the inscribed octahedron / cube of spike tips. 3 is the minimum that guarantees all box corners are inside the stellation and matches the octahedron shield.")]
        [SerializeField] private float shieldScale = StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        // --- Runtime state ---------------------------------------------------

        private Mesh _originalMesh;
        private Mesh _stellatedMesh;       // cache-shared settled stellation; ALSO the morph mesh
        private Vector3 _halfExtents;       // from BoxCollider.size * 0.5
        private Vector3 _center;            // from BoxCollider.center

        /// <summary>Local-space shell center for the spatial index's shell view.</summary>
        internal Vector3 ShellCenterLocal => _center;

        /// <summary>
        /// Local-space shell semi-axes (shieldScale × Awake-cached half-extents):
        /// the spike-tip cube's half-extents — the two tetrahedra sit at its
        /// alternating corners. Frozen authored geometry, never live collider size.
        /// </summary>
        internal Vector3 ShellSemiAxesLocal => _halfExtents * shieldScale;
        private float _boxMass;
        private float _shieldMass;
        private Material[] _originalMaterials;
        private MeshRenderer _meshRenderer;

        private bool _isShielded;

        // Owning prism — prisms render through an instanced companion entity, so the
        // settled stellation must be pushed to it as a render-mesh override
        // (Prism.SetRenderMeshOverride); without this the companion keeps drawing the
        // plain box and the stellation is invisible (exactly how this class first
        // shipped). Null on a standalone rig.
        private Prism _prism;

        // Precomputed fast-path containment inverses.
        private float _invA, _invB, _invC;

        public bool IsShielded => _isShielded;

        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            if (meshFilter == null)  meshFilter  = GetComponent<MeshFilter>();
            if (rb == null)          rb          = GetComponent<Rigidbody>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _prism = GetComponent<Prism>();

            CacheGeometry();

            if (meshFilter != null)
                _originalMesh = meshFilter.sharedMesh;

            if (_meshRenderer != null)
                _originalMaterials = _meshRenderer.sharedMaterials;

            // Settled stellation comes from the shared cache (half-extents are the authored
            // LOCAL collider size), so every same-size super-shielded prism resolves to ONE
            // mesh — and because the GPU morph is evaluated on THAT mesh, the sharing holds
            // through the engage bloom too. Cache-owned: never destroy it here.
            _stellatedMesh = StellatedOctahedronMeshGenerator.GetSharedShieldMesh(_halfExtents, shieldScale);

            ComputeMassTargets();
        }

        private void OnDisable()
        {
            // Snap to clean state when the GameObject is disabled (e.g. pooled back).
            // Prevents stale visuals on pool reuse. No ticker to unregister any more —
            // the morph is a GPU stamp with no CPU driver at all.
            if (!_isShielded) return;

            _isShielded = false;
            if (_stellatedMesh != null)
                ApplyUnshieldedPose();
            PrismShieldMorph.Clear(_prism, _meshRenderer);
            if (_prism != null)
            {
                _prism.ClearRenderMeshOverride();
                _prism.SetExoticVisualActive(false);
            }
        }

        // No OnDestroy: the settled stellation is cache-shared, and the per-prism
        // morph/shatter meshes are gone (GPU morph on the shared mesh + batched-entity
        // shatter), so this component owns no disposable objects.

        /// <summary>
        /// Re-reads the BoxCollider's size/center. Call this if the box
        /// dimensions change at runtime (e.g., scaling prisms).
        /// </summary>
        public void CacheGeometry()
        {
            if (boxCollider != null)
            {
                _halfExtents = boxCollider.size * 0.5f;
                _center = boxCollider.center;
            }
            else
            {
                _halfExtents = Vector3.one * 0.5f;
                _center = Vector3.zero;
            }

            float a = Mathf.Max(_halfExtents.x * shieldScale, 1e-5f);
            float b = Mathf.Max(_halfExtents.y * shieldScale, 1e-5f);
            float c = Mathf.Max(_halfExtents.z * shieldScale, 1e-5f);
            _invA = 1f / a;
            _invB = 1f / b;
            _invC = 1f / c;
        }

        private void ComputeMassTargets()
        {
            if (density > 0f)
            {
                // V_box = 8·a·b·c, V_stellated = 108·a·b·c.
                _boxMass = density * 8f * _halfExtents.x * _halfExtents.y * _halfExtents.z;
                _shieldMass = density * 108f * _halfExtents.x * _halfExtents.y * _halfExtents.z;
            }
            else
            {
                _boxMass = rb != null ? rb.mass : 1f;
                _shieldMass = _boxMass * massRatioSuperShielded;
            }
        }

        // --- Public API ------------------------------------------------------

        [ContextMenu("Engage Super-Shield")]
        public void EngageContextMenu() => Engage();

        [ContextMenu("Disengage Super-Shield")]
        public void DisengageContextMenu() => Disengage();

        [ContextMenu("Toggle Super-Shield")]
        public void Toggle()
        {
            if (_isShielded) Disengage();
            else Engage();
        }

        /// <summary>Engage the super-shield with per-face bloom across all 24 faces.</summary>
        public void Engage(bool instant = false)
        {
            if (_isShielded)
            {
                // Already up. See the octahedron tier: an INSTANT re-engage settles a
                // bloom still in flight; a non-instant one is a no-op.
                if (instant) PrismShieldMorph.Clear(_prism, _meshRenderer);
                return;
            }

            _isShielded = true;

            // Gameplay AND rendering go final first (the law: only photons animate).
            ApplyShieldedPose();

            if (!instant && engageDuration > 0f)
                PrismShieldMorph.StampBloom(_prism, _meshRenderer, this, engageDuration,
                    $"superShieldEngage:{name}");
            else
                PrismShieldMorph.Clear(_prism, _meshRenderer);
        }

        /// <summary>
        /// Disengage the super-shield. Box mesh snaps back immediately; a
        /// shatter overlay plays where each of the 24 faces flies outward
        /// along its normal while shrinking to a point.
        /// </summary>
        /// <param name="breakVelocity">
        /// WORLD-space velocity of the force that broke the shield, if the caller has one.
        /// The shards drift and tumble along it (Docs/PRISM_ANIMATION.md §4.8.1), clamped to
        /// <c>shatterDriftSpeedCap</c>. Default zero = the symmetric puff.
        /// </param>
        public void Disengage(bool instant = false, Vector3 breakVelocity = default)
        {
            if (!_isShielded) return;

            _isShielded = false;

            // Queued BEFORE the pose flips: the shards are the shield that was standing
            // here, so they take this prism's transform and the material it is wearing
            // right now, both of which ApplyUnshieldedPose is about to change. (See the
            // octahedron tier for why "right now" is already the post-transition domain
            // material on the shipped prefabs.)
            if (!instant && shatterDuration > 0f)
                PrismShieldMorph.RequestShatter(gameObject, _meshRenderer, _stellatedMesh,
                    shatterDuration, shatterMaxOffset,
                    PrismShieldMorph.ClampBreakVelocity(breakVelocity, shatterDriftSpeedCap));

            // Immediately restore box mesh + colliders so gameplay is unaffected.
            ApplyUnshieldedPose();

            // Settle the morph BEFORE the entity goes back to the box mesh, which carries
            // no per-face centroids.
            PrismShieldMorph.Clear(_prism, _meshRenderer);

            // Box mesh is back — return the entity to the prism mesh and rendering to the
            // instanced path. The shatter overlay is independent batched debris.
            if (_prism != null)
            {
                _prism.ClearRenderMeshOverride();
                _prism.SetExoticVisualActive(false);
            }
        }

        /// <summary>
        /// Branchless point-in-shield test (world-space input). Only valid
        /// while <see cref="IsShielded"/> is true. Uses the 4-linear-form
        /// tetrahedral check - a point is inside the stellation iff it lies
        /// in either constituent tetrahedron.
        /// </summary>
        public bool IsPointInsideShield(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - _center;
            return StellatedOctahedronMeshGenerator.ContainsPointLocal(local, _invA, _invB, _invC);
        }

        // --- Pose application ------------------------------------------------

        private void ApplyShieldedPose()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = _stellatedMesh;

            // COLLISION stays on the authored PRIMITIVE trigger box - super-shield only
            // changes the LOOK (stellated mesh, above) and mass, never the collider. A convex
            // MeshCollider can't serve both skimmer families at once: as a SOLID it is invisible
            // to solid impactors like the Rhino shield-swipe (solid-vs-solid fires nothing); as a
            // TRIGGER it is invisible to TRIGGER colliders (Unity/PhysX does not report a
            // convex-mesh trigger to another trigger), which is every vessel's kinematic-RB
            // trigger skimmer sphere - so the swap made skims "not register" on the Skim Race /
            // Astro League lining. The authored box is a PRIMITIVE trigger, which BOTH see
            // (trigger-vs-trigger works for primitives; solid-vs-trigger works) - exactly how
            // unshielded prisms already skim for everyone. It's also LOD-cullable and needs no
            // convex cook. (True stellated containment is still available via IsPointInsideShield.)
            // NOT while the prism is still being created: Prism.Initialize holds the collider off
            // until CreateBlockCoroutine reveals it, and a spawn-time INSTANT engage
            // (PrismStateManager.IsBirthTransition) reaches here inside that window.
            if (boxCollider != null && (_prism == null || _prism.IsCreationComplete))
                boxCollider.enabled = true;

            if (shieldMeshCollider != null)
                shieldMeshCollider.enabled = false;

            if (rb != null)
                rb.mass = _shieldMass;

            ApplyMaterialOverride(shielded: true);

            // The stellation is SHARED geometry — hand rendering to the companion entity so
            // same-size super-shielded prisms batch into one draw, and so the GPU morph has
            // something to run on. Only the exotic-visual FALSE side is ever used now; the
            // morph needs no per-prism mesh. No-op on the legacy path.
            if (_prism != null)
            {
                _prism.SetRenderMeshOverride(_stellatedMesh);
                _prism.SetExoticVisualActive(false);
            }
        }

        private void ApplyUnshieldedPose()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = _originalMesh;

            // See ApplyShieldedPose: the spawn window owns the collider until reveal.
            if (boxCollider != null && (_prism == null || _prism.IsCreationComplete))
                boxCollider.enabled = true;

            if (shieldMeshCollider != null)
                shieldMeshCollider.enabled = false;

            if (rb != null)
                rb.mass = _boxMass;

            ApplyMaterialOverride(shielded: false);
        }

        private void ApplyMaterialOverride(bool shielded)
        {
            if (_meshRenderer == null || shieldMaterialOverride == null) return;
            _meshRenderer.sharedMaterials = shielded
                ? new[] { shieldMaterialOverride }
                : _originalMaterials;
        }
    }
}
