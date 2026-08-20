using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the visual and physical transition between a prism's unshielded
    /// box state and its supershielded circumscribing octahedron state.
    ///
    /// States:
    ///   Unshielded:   authored BoxCollider (trigger) active, authored prism mesh visible,
    ///                 mass = rho · 8·a·b·c
    ///   Supershielded: authored BoxCollider (trigger) STAYS the collider (a convex-mesh
    ///                 trigger is invisible to trigger skimmers; the primitive box is what
    ///                 both trigger and solid impactors detect), octahedron mesh visible,
    ///                 mass = rho · 36·a·b·c (exactly 4.5× the box mass by default)
    ///
    /// Engage: per-face bloom morph - 8 faces grow outward from their centroids.
    /// Disengage: box mesh snaps back immediately, then a shatter overlay plays
    ///   where each octahedron face simultaneously shrinks and flies outward
    ///   along its face normal — and, when the caller hands over the velocity of the force
    ///   that BROKE the shield, drifting and tumbling along that blow. That is not
    ///   "mirroring" the prism destruction VFX any more; it is the SAME motion model,
    ///   applied per face (Docs/PRISM_ANIMATION.md §4.8.1). Zero velocity is the
    ///   identity, so a direction-less disengage stays the symmetric puff it always was.
    ///
    /// BOTH MORPHS RUN ON THE GPU (Docs/PRISM_ANIMATION.md §5 B4, the clock-material
    /// law). This class writes ONE stamp per transition and never touches the animation
    /// again — no ticker, no per-frame mesh rebuild, no end callback. Two consequences
    /// worth keeping in mind when editing:
    ///
    ///   • EVERYTHING IS FINAL AT t = 0. Engage() applies the whole shielded pose —
    ///     collider, mass, material, and the settled shared shield mesh — and only then
    ///     stamps the bloom. The vertex stage collapses the faces to their centroids at
    ///     t = 0 and expands them out; nothing waits for the animation to finish.
    ///   • THE SHIELD NEVER LEAVES THE INSTANCED PATH. The morph is evaluated on the
    ///     cache-SHARED settled mesh (per-face centroids baked into TEXCOORD1 by
    ///     OctahedronMeshGenerator), so there is no per-prism-unique geometry any more
    ///     and same-size shields stay in one batch through the whole animation. The
    ///     exotic-visual handoff is still honoured — SetRenderMeshOverride is what makes
    ///     the companion entity draw the octahedron at all, and a bare MeshFilter swap
    ///     would render nothing (CLAUDE.md ▸ Anti-Patterns) — but SetExoticVisualActive
    ///     is now only ever driven to FALSE, because nothing here needs the un-batched
    ///     GameObject renderer.
    ///
    /// Fast overlap test: <see cref="IsPointInsideShield"/> uses the
    /// precomputed L1 inverses for branchless gameplay queries that don't need a
    /// full physics collider.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismOctahedronShield : MonoBehaviour
    {
        [Header("Collider Sources")]
        [Tooltip("The authored BoxCollider that defines the unshielded shape. Its center/size drive the octahedron geometry.")]
        [SerializeField] private BoxCollider boxCollider;

        [Tooltip("MeshCollider used for the supershielded state. Auto-created if null.")]
        [SerializeField] private MeshCollider shieldMeshCollider;

        [Header("Rendering")]
        [Tooltip("MeshFilter whose mesh is swapped between the authored prism mesh and the generated octahedron mesh.")]
        [SerializeField] private MeshFilter meshFilter;

        [Tooltip("Optional override material for the shielded visual. If null, the existing MeshRenderer materials are reused.")]
        [SerializeField] private Material shieldMaterialOverride;

        [Header("Physics")]
        [Tooltip("Optional Rigidbody whose mass scales with shield state. If null, mass scaling is skipped.")]
        [SerializeField] private Rigidbody rb;

        [Tooltip("Uniform density (kg / unit^3) used for mass = density · volume. Set negative to disable density-based mass and use massRatioShielded instead.")]
        [SerializeField] private float density = 1f;

        [Tooltip("Multiplier applied to the unshielded (box) mass when entering the shielded state. Default 4.5 matches the geometric volume ratio V_oct_circum / V_box = 36·a·b·c / 8·a·b·c.")]
        [SerializeField] private float massRatioShielded = OctahedronMeshGenerator.SHIELD_TO_BOX_VOLUME_RATIO;

        [Header("Engage Transition")]
        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly. Easing is smoothstep on the GPU, which IS AnimationCurve.EaseInOut(0,0,1,1) — the curve every runtime-added shield used. The retired curve FIELD is gone: the GPU cannot evaluate an arbitrary AnimationCurve.")]
        [SerializeField] private float engageDuration = 0.35f;

        [Header("Shatter (Disengage)")]
        [Tooltip("Duration of the shatter VFX overlay after disengaging. 0 snaps instantly. Defaults to PrismExplosion.DefaultDuration - a shield coming apart and a prism coming apart are the same event class, and they read wrong at different lengths. Note the tumble is a RATE (rad/second), so lengthening this spins the faces further.")]
        [SerializeField] private float shatterDuration = PrismExplosion.DefaultDuration;

        [Tooltip("How far each face flies outward (in local-space units) at the end of the shatter.")]
        [SerializeField] private float shatterMaxOffset = 3f;

        [Tooltip("Ceiling (world units/second) on the drift the shards inherit from the force " +
                 "that BROKE the shield — the prism explosion's velocity, applied per face. " +
                 "This is the ONE dial for how violently the octahedron comes apart: the tumble " +
                 "angle rides the same clamped speed. 0 disables both terms, leaving the " +
                 "symmetric outward puff, which is also what every direction-less disengage " +
                 "(a shield timer expiring, an arena teardown, a herbivore stripping armour) " +
                 "already gets. Impact magnitudes are not comparable across the legacy and " +
                 "true-velocity damage paths, so only the DIRECTION survives unclamped.")]
        [SerializeField] private float shatterDriftSpeedCap = 20.0f;

        [Header("Shield Geometry")]
        [Tooltip("Circumscribing scale factor. 3 is the minimum that guarantees all box corners are inside the octahedron.")]
        [SerializeField] private float shieldScale = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        // --- Runtime state ---------------------------------------------------

        private Mesh _originalMesh;
        private Mesh _octahedronMesh;     // cache-shared settled octahedron; ALSO the morph mesh
        private Vector3 _halfExtents;      // from BoxCollider.size * 0.5
        private Vector3 _center;           // from BoxCollider.center

        /// <summary>Local-space shell center for the spatial index's shell view.</summary>
        internal Vector3 ShellCenterLocal => _center;

        /// <summary>
        /// Local-space shell semi-axes (shieldScale × Awake-cached half-extents)
        /// for the spatial index's shell view — deliberately the frozen authored
        /// geometry, never the live BoxCollider.size (HoldColliderAtFullSize
        /// mutates that during the bloom).
        /// </summary>
        internal Vector3 ShellSemiAxesLocal => _halfExtents * shieldScale;
        private float _boxMass;
        private float _shieldMass;
        private Material[] _originalMaterials;
        private MeshRenderer _meshRenderer;

        private bool _isShielded;

        // Owning prism — the settled octahedron is pushed to the companion entity as a
        // render-mesh override (Prism.SetRenderMeshOverride); without it the entity keeps
        // drawing the plain box and the shield is invisible. Null on a standalone rig.
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

            // NOT cached here: MeshRenderer.sharedMaterials allocates a fresh managed array on
            // every read, and only ApplyMaterialOverride (first shield engage) ever needs it —
            // paying it in Awake was 25k throwaway arrays on a mass environment lay. Captured
            // lazily on the first override instead, while the renderer still has the originals.

            // Mesh setup is deferred to the first Engage (EnsureShieldMeshBuilt): Load Time
            // Insights measured per-prism shield mesh work at Awake as a dominant share of
            // mass environment lays (25k prisms in one load) - prisms that are never shielded
            // must not pay for the shield's geometry.

            ComputeMassTargets();
        }

        /// <summary>
        /// Resolves the shield mesh on first use. It comes from the shared cache:
        /// half-extents are the authored LOCAL collider size, so every same-prefab shield
        /// resolves to ONE mesh — and since the GPU morph is evaluated on THAT mesh (its
        /// per-face centroids ride TEXCOORD1), the sharing holds through the engage bloom
        /// as well as the settled state. Cache-owned: never destroyed here.
        /// Deferred out of Awake so never-shielded prisms skip it entirely.
        /// </summary>
        private void EnsureShieldMeshBuilt()
        {
            if (_octahedronMesh != null) return;
            _octahedronMesh = OctahedronMeshGenerator.GetSharedShieldMesh(_halfExtents, shieldScale);
        }

        private void OnDisable()
        {
            // Snap to clean state when the GameObject is disabled (e.g. pooled back).
            // Prevents stale visuals on pool reuse. No ticker to unregister any more —
            // the morph is a GPU stamp with no CPU driver at all.
            if (!_isShielded) return;

            _isShielded = false;
            if (_octahedronMesh != null)
                ApplyUnshieldedPose();
            PrismShieldMorph.Clear(_prism, _meshRenderer);
            if (_prism != null)
            {
                _prism.ClearRenderMeshOverride();
                _prism.SetExoticVisualActive(false);
            }
        }

        // No OnDestroy: the settled octahedron is cache-shared (other shields reference
        // it), and the per-prism morph/shatter meshes it used to own are gone — the GPU
        // morph runs on the shared mesh and the shatter overlay is a batched entity
        // (PrismShieldShatter), so this component owns no disposable objects.

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
                _boxMass = density * 8f * _halfExtents.x * _halfExtents.y * _halfExtents.z;
                _shieldMass = density * 36f * _halfExtents.x * _halfExtents.y * _halfExtents.z;
            }
            else
            {
                _boxMass = rb != null ? rb.mass : 1f;
                _shieldMass = _boxMass * massRatioShielded;
            }
        }

        // --- Public API ------------------------------------------------------

        [ContextMenu("Engage Shield")]
        public void EngageContextMenu() => Engage();

        [ContextMenu("Disengage Shield")]
        public void DisengageContextMenu() => Disengage();

        [ContextMenu("Toggle Shield")]
        public void Toggle()
        {
            if (_isShielded) Disengage();
            else Engage();
        }

        /// <summary>Engage the supershield with per-face bloom.</summary>
        public void Engage(bool instant = false)
        {
            if (_isShielded)
            {
                // Already up. An INSTANT re-engage still means "be settled now", so it
                // settles a bloom that may still be running — matching the old driver,
                // which fell through to its instant branch and snapped _engageT to 1.
                // A non-instant re-engage is a no-op: the bloom is already in flight.
                if (instant) PrismShieldMorph.Clear(_prism, _meshRenderer);
                return;
            }

            EnsureShieldMeshBuilt();
            _isShielded = true;

            // Gameplay AND rendering go final first (the law: only photons animate).
            // The entity is put on the settled octahedron here, and the vertex stage
            // then collapses its faces to their centroids at t = 0 and blooms them out.
            ApplyShieldedPose();

            if (!instant && engageDuration > 0f)
                PrismShieldMorph.StampBloom(_prism, _meshRenderer, this, engageDuration,
                    $"shieldEngage:{name}");
            else
                PrismShieldMorph.Clear(_prism, _meshRenderer);
        }

        /// <summary>
        /// Disengage the supershield. Box mesh snaps back immediately; a
        /// shatter overlay plays where each octahedron face flies outward
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
            // right now, both of which ApplyUnshieldedPose is about to change. (On the
            // shipped prefabs "right now" is already the post-transition domain material,
            // because PrismStateManager repaints before it disengages — the same colour
            // the retired child-renderer overlay showed.)
            if (!instant && shatterDuration > 0f)
                PrismShieldMorph.RequestShatter(gameObject, _meshRenderer, _octahedronMesh,
                    shatterDuration, shatterMaxOffset,
                    PrismShieldMorph.ClampBreakVelocity(breakVelocity, shatterDriftSpeedCap));

            // Immediately restore box mesh + colliders so gameplay is unaffected.
            ApplyUnshieldedPose();

            // Settle the morph BEFORE the entity goes back to the box mesh: the box
            // carries no per-face centroids, so a live stamp would collapse it toward
            // the object origin.
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
        /// while <see cref="IsShielded"/> is true.
        /// </summary>
        public bool IsPointInsideShield(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - _center;
            return OctahedronMeshGenerator.ContainsPointLocal(local, _invA, _invB, _invC);
        }

        // --- Pose application ------------------------------------------------

        private void ApplyShieldedPose()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = _octahedronMesh;

            // COLLISION stays on the authored PRIMITIVE trigger box - the shield only changes
            // the LOOK (octahedron mesh, above) and mass, never the collider. A convex
            // MeshCollider can't serve both skimmer families at once: as a SOLID it is invisible
            // to solid impactors like the Rhino shield-swipe (solid-vs-solid fires nothing); as a
            // TRIGGER it is invisible to TRIGGER colliders (Unity/PhysX does not report a
            // convex-mesh trigger to another trigger), which is every vessel's kinematic-RB
            // trigger skimmer sphere - so the swap made skims "not register" at all. The authored
            // box is a PRIMITIVE trigger, which BOTH see (trigger-vs-trigger works for primitives;
            // solid-vs-trigger works) - exactly how unshielded prisms already skim for everyone.
            // Bonus: the box is LOD-cullable (PrismColliderLodManager) and needs no convex cook.
            // NOT while the prism is still being created: Prism.Initialize deliberately holds the
            // collider off until CreateBlockCoroutine reveals it, and a spawn-time INSTANT engage
            // (PrismStateManager.IsBirthTransition) reaches here inside that window.
            if (boxCollider != null && (_prism == null || _prism.IsCreationComplete))
                boxCollider.enabled = true;

            if (shieldMeshCollider != null)
                shieldMeshCollider.enabled = false;

            if (rb != null)
                rb.mass = _shieldMass;

            ApplyMaterialOverride(shielded: true);

            // The octahedron is SHARED geometry — hand rendering to the companion entity
            // so every same-size shielded prism batches into one draw, and so the GPU
            // morph has something to run on. Only the exotic-visual FALSE side is ever
            // used now; the morph needs no per-prism mesh. No-op on the legacy path.
            if (_prism != null)
            {
                _prism.SetRenderMeshOverride(_octahedronMesh);
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

            // Capture the authored materials on the first override, before we overwrite them
            // (deferred out of Awake — see the comment there).
            _originalMaterials ??= _meshRenderer.sharedMaterials;

            _meshRenderer.sharedMaterials = shielded
                ? new[] { shieldMaterialOverride }
                : _originalMaterials;
        }
    }
}
