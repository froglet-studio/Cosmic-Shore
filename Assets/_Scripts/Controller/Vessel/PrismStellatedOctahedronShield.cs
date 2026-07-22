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
    ///   Unshielded:        authored BoxCollider is the trigger, authored prism
    ///                      mesh visible, mass = ρ · 8·a·b·c
    ///   Super-shielded:    the prism's shield AABB proxy BoxCollider is the
    ///                      trigger, stellation mesh visible,
    ///                      mass = ρ · 108·a·b·c (exactly 13.5× box mass by default,
    ///                      3× the inscribed octahedron shield's mass)
    ///
    /// Collider cost: the AABB proxy (±s·a, ±s·b, ±s·c) is EXACTLY the convex hull
    /// PhysX used to compute for the non-convex stellation (the 6 octahedron
    /// vertices sit on the face centers of the spike-tip cube), so the broadphase
    /// shape is identical to the old convex MeshCollider at BoxCollider cost — and
    /// the notch over-cover both shapes share is now rejected by the analytic
    /// 4-linear-form narrowphase gate (<see cref="IShieldContainmentGate"/> /
    /// <see cref="ImpactorBase.OnTriggerEnter"/>). The proxy participates in the
    /// proximity collider-LOD like any other prism collider; the old MeshCollider
    /// was LOD-exempt (the documented always-on budget line).
    ///
    /// Engage: per-face bloom morph - 24 outer faces grow outward from their
    /// centroids.
    /// Disengage: box mesh snaps back immediately, then a shatter overlay plays
    ///   where each of the 24 faces simultaneously shrinks and flies outward
    ///   along its face normal, mirroring the prism destruction VFX.
    ///
    /// Note on terminology: the existing <see cref="PrismOctahedronShield"/>'s
    /// docstring calls the octahedron state "supershielded"; in the broader
    /// design language used here, "super-shielded" refers specifically to this
    /// stellated state, with the octahedron being merely "shielded".
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismStellatedOctahedronShield : MonoBehaviour, IShieldContainmentGate, IPrismShieldTicker
    {
        [Header("Collider Sources")]
        [Tooltip("The authored BoxCollider that defines the unshielded shape. Its center/size drive the stellation geometry.")]
        [SerializeField] private BoxCollider boxCollider;

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
        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly.")]
        [SerializeField] private float engageDuration = 0.45f;

        [Tooltip("Easing curve applied to the engage morph progress (0→1).")]
        [SerializeField] private AnimationCurve engageCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Shatter (Disengage)")]
        [Tooltip("Duration of the shatter VFX overlay after disengaging. 0 snaps instantly.")]
        [SerializeField] private float shatterDuration = 0.7f;

        [Tooltip("How far each face flies outward (in local-space units) at the end of the shatter.")]
        [SerializeField] private float shatterMaxOffset = 4f;

        [Tooltip("Easing curve applied to the shatter progress (0→1). Output drives both face-offset and face-shrink.")]
        [SerializeField] private AnimationCurve shatterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Shield Geometry")]
        [Tooltip("Circumscribing scale factor for the inscribed octahedron / cube of spike tips. 3 is the minimum that guarantees all box corners are inside the stellation and matches the octahedron shield.")]
        [SerializeField] private float shieldScale = StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        // --- Runtime state ---------------------------------------------------

        private Mesh _originalMesh;
        private Mesh _stellatedMesh;       // static full-size stellation, owned
        private Mesh _morphMesh;            // reused every frame during engage morph, owned
        private Vector3 _halfExtents;       // from BoxCollider.size * 0.5
        private Vector3 _center;            // from BoxCollider.center
        private float _boxMass;
        private float _shieldMass;
        private Material[] _originalMaterials;
        private MeshRenderer _meshRenderer;

        private bool _isShielded;

        // -- Engage morph state --
        private float _engageT;             // 0 = collapsed, 1 = full stellation
        private bool _isEngaging;

        // -- Shatter overlay state --
        private float _shatterT;            // 0 = start, 1 = fully shattered
        private bool _isShattering;

        // Lazily-created child that renders the shatter overlay so the parent
        // MeshFilter can show the box mesh while the faces fly away.
        private GameObject _shatterChild;
        private MeshFilter _shatterMeshFilter;
        private MeshRenderer _shatterRenderer;
        private Mesh _shatterMesh;

        // Owning prism — prisms render through an instanced companion entity, so engage/disengage
        // must hand rendering between the entity and this GameObject's MeshRenderer
        // (Prism.SetExoticVisualActive), and the settled stellation is pushed back to the entity
        // as a render-mesh override. Mirrors PrismOctahedronShield; without this the companion
        // keeps drawing the plain box and the stellation is invisible.
        private Prism _prism;

        // Precomputed fast-path containment inverses.
        private float _invA, _invB, _invC;

        public bool IsShielded => _isShielded;
        public float TransitionProgress => _engageT;
        public bool IsTransitioning => _isEngaging || _isShattering;

        // ---------------------------------------------------------------------

        private void Awake()
        {
            _prism = GetComponent<Prism>();
            // Prefer the prism's authored collider — this component is added lazily
            // on the first super-shield engage, by which point the shield AABB proxy
            // (a second BoxCollider on the same GameObject) may already exist and a
            // bare GetComponent could grab it.
            if (boxCollider == null)
                boxCollider = _prism != null && _prism.AuthoredCollider != null
                    ? _prism.AuthoredCollider
                    : GetComponent<BoxCollider>();
            if (meshFilter == null)  meshFilter  = GetComponent<MeshFilter>();
            if (rb == null)          rb          = GetComponent<Rigidbody>();
            _meshRenderer = GetComponent<MeshRenderer>();

            CacheGeometry();

            if (meshFilter != null)
                _originalMesh = meshFilter.sharedMesh;

            if (_meshRenderer != null)
                _originalMaterials = _meshRenderer.sharedMaterials;

            // Settled stellation comes from the shared cache (half-extents are the authored
            // LOCAL collider size), so every same-size super-shielded prism resolves to ONE
            // mesh and settled stellations batch on the instanced render path. Render-only —
            // the physics side is the box AABB proxy, no mesh is ever cooked. Cache-owned:
            // never destroy it here.
            _stellatedMesh = StellatedOctahedronMeshGenerator.GetSharedShieldMesh(_halfExtents, shieldScale);
            _morphMesh = new Mesh { name = "StellatedOctahedron_SuperShield_Morph" };
            _morphMesh.MarkDynamic();

            ComputeMassTargets();
        }

        private void OnDisable()
        {
            // Snap to clean state when the GameObject is disabled (e.g. pooled
            // back). Prevents stale visuals on pool reuse.
            // Stop being ticked the moment we're pooled/disabled (cheap if not registered).
            PrismOctahedronShieldManager.Instance?.Unregister(this);

            if (_isShielded || _isEngaging || _isShattering)
            {
                _engageT = 0f;
                _shatterT = 0f;
                _isEngaging = false;
                _isShattering = false;
                _isShielded = false;
                if (_stellatedMesh != null)
                    ApplyUnshieldedPose();
                StopShatter();
                if (_prism != null)
                {
                    _prism.ClearRenderMeshOverride();
                    _prism.SetExoticVisualActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            // _stellatedMesh is cache-shared (other shields reference it) - not destroyed here.
            if (_morphMesh != null)     Destroy(_morphMesh);
            if (_shatterMesh != null)   Destroy(_shatterMesh);
            if (_shatterChild != null)  Destroy(_shatterChild);
        }

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
            if (_isShielded && !_isEngaging) return;

            // If a shatter overlay is still playing, kill it immediately.
            StopShatter();

            _isShielded = true;

            // The morph mesh is per-prism-unique geometry — render through the
            // GameObject while the shield blooms (no-op on the legacy path).
            if (_prism != null) _prism.SetExoticVisualActive(true);

            if (instant || engageDuration <= 0f)
            {
                _engageT = 1f;
                _isEngaging = false;
                ApplyShieldedPose();
            }
            else
            {
                _isEngaging = true;
                DisableCollidersDuringMorph();
                UpdateEngageMesh(engageCurve.Evaluate(_engageT));
                PrismOctahedronShieldManager.EnsureInstance()?.Register(this);
            }
        }

        /// <summary>
        /// Disengage the super-shield. Box mesh snaps back immediately; a
        /// shatter overlay plays where each of the 24 faces flies outward
        /// along its normal while shrinking to a point.
        /// </summary>
        public void Disengage(bool instant = false)
        {
            if (!_isShielded && !_isEngaging) return;

            _isShielded = false;
            _isEngaging = false;
            _engageT = 0f;

            // Immediately restore box mesh + colliders so gameplay is unaffected.
            ApplyUnshieldedPose();

            // Box mesh is back — return the entity to the prism mesh and rendering to the
            // instanced path. The shatter overlay plays on its own child renderer.
            if (_prism != null)
            {
                _prism.ClearRenderMeshOverride();
                _prism.SetExoticVisualActive(false);
            }

            if (instant || shatterDuration <= 0f)
            {
                // No overlay needed.
            }
            else
            {
                _shatterT = 0f;
                _isShattering = true;
                PrismOctahedronShieldManager.EnsureInstance()?.Register(this);
                EnsureShatterChild();
                _shatterRenderer.sharedMaterial =
                    _meshRenderer != null ? _meshRenderer.sharedMaterial : null;
                _shatterChild.SetActive(true);
                UpdateShatterMesh(0f);
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

        /// <summary>
        /// Narrowphase gate over the engaged shield's AABB proxy trigger — rejects
        /// contacts in the notches between spikes (outside both tetrahedra).
        /// Installed on the prism while Engaged (<see cref="Prism.ActiveShieldGate"/>).
        /// </summary>
        bool IShieldContainmentGate.ContainsWorldPoint(Vector3 worldPoint) => IsPointInsideShield(worldPoint);

        // --- Transition driver -----------------------------------------------

        /// <summary>
        /// Advances any in-progress engage/shatter morph by <paramref name="dt"/>.
        /// Called by <see cref="PrismOctahedronShieldManager"/> ONLY while this shield
        /// is registered (i.e. actively transitioning) — idle shields are not ticked,
        /// completing the centralized-ticking migration for the stellated variant
        /// (it previously ran its own per-instance Update()).
        /// </summary>
        internal bool Tick(float dt)
        {
            if (_isEngaging)
                DriveEngage(dt);

            if (_isShattering)
                DriveShatter(dt);

            return _isEngaging || _isShattering;
        }

        bool IPrismShieldTicker.Tick(float dt) => Tick(dt);

        private void DriveEngage(float dt)
        {
            float step = engageDuration > 0f ? dt / engageDuration : 1f;
            _engageT = Mathf.Clamp01(_engageT + step);

            UpdateEngageMesh(engageCurve.Evaluate(_engageT));

            if (_engageT >= 1f)
            {
                _isEngaging = false;
                ApplyShieldedPose();
            }
        }

        private void DriveShatter(float dt)
        {
            float step = shatterDuration > 0f ? dt / shatterDuration : 1f;
            _shatterT = Mathf.Clamp01(_shatterT + step);

            float t = shatterCurve.Evaluate(_shatterT);
            UpdateShatterMesh(t);

            if (_shatterT >= 1f)
                StopShatter();
        }

        // --- Mesh updates ----------------------------------------------------

        /// <summary>
        /// Per-face bloom for engage: all 24 faces grow from centroid points to full size.
        /// </summary>
        private void UpdateEngageMesh(float faceScale)
        {
            StellatedOctahedronMeshGenerator.PopulateMeshFaceScale(
                _morphMesh, _halfExtents, faceScale, shieldScale);

            if (meshFilter != null)
                meshFilter.sharedMesh = _morphMesh;
        }

        /// <summary>
        /// Shatter overlay: each face shrinks toward its centroid AND flies
        /// outward along its face normal. Rendered on the child overlay object
        /// while the parent shows the box mesh.
        ///   t=0: faces at full size, in place (just-disengaged stellation)
        ///   t=1: faces collapsed to centroid points, displaced far along normals
        /// </summary>
        private void UpdateShatterMesh(float t)
        {
            float faceScale  = 1f - t;                // 1→0 (shrink)
            float faceOffset = t * shatterMaxOffset;  // 0→max (fly outward)

            StellatedOctahedronMeshGenerator.PopulateMeshFaceShatter(
                _shatterMesh, _halfExtents, faceScale, faceOffset, shieldScale);

            _shatterMeshFilter.sharedMesh = _shatterMesh;
        }

        // --- Pose application ------------------------------------------------

        private void ApplyShieldedPose()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = _stellatedMesh;

            // The AABB proxy IS the convex hull PhysX computed for the non-convex
            // stellation (≈ the spike-tip bounding cube) at BoxCollider cost; the
            // notch regions are rejected by the analytic narrowphase gate.
            ApplyColliderState(ShieldColliderState.Engaged);

            if (rb != null)
                rb.mass = _shieldMass;

            ApplyMaterialOverride(shielded: true);

            // The settled stellation is static geometry — hand rendering back to the companion
            // entity with the stellated mesh as its render override (same-size super-shielded
            // prisms share the look through the instanced path; only the engage morph and the
            // shatter overlay are per-prism-unique). No-op on the legacy path.
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

            ApplyColliderState(ShieldColliderState.None);

            if (rb != null)
                rb.mass = _boxMass;

            ApplyMaterialOverride(shielded: false);
        }

        private void DisableCollidersDuringMorph()
        {
            ApplyColliderState(ShieldColliderState.Morphing);
        }

        /// <summary>
        /// Routes collider selection through the prism (box / none / AABB proxy) so
        /// it composes with the LOD cull, destruction, and the spawn window. The
        /// legacy fallback (tester harness on a bare GameObject with no Prism)
        /// drives the authored box and a locally-created proxy directly.
        /// </summary>
        private void ApplyColliderState(ShieldColliderState state)
        {
            if (_prism != null)
            {
                _prism.SetShieldColliderState(state, this, shieldScale);
                return;
            }

            if (boxCollider != null)
                boxCollider.enabled = state == ShieldColliderState.None;
            if (state == ShieldColliderState.Engaged)
            {
                EnsureLegacyProxyCollider();
                if (_legacyProxyCollider != null) _legacyProxyCollider.enabled = true;
            }
            else if (_legacyProxyCollider != null)
            {
                _legacyProxyCollider.enabled = false;
            }
        }

        // Legacy-path (no Prism) shield AABB proxy for the tester harness.
        private BoxCollider _legacyProxyCollider;

        private void EnsureLegacyProxyCollider()
        {
            if (_legacyProxyCollider != null) return;
            _legacyProxyCollider = gameObject.AddComponent<BoxCollider>();
            _legacyProxyCollider.center = _center;
            _legacyProxyCollider.size = _halfExtents * 2f * shieldScale;
            _legacyProxyCollider.isTrigger = boxCollider == null || boxCollider.isTrigger;
            _legacyProxyCollider.enabled = false;
        }

        private void ApplyMaterialOverride(bool shielded)
        {
            if (_meshRenderer == null || shieldMaterialOverride == null) return;
            _meshRenderer.sharedMaterials = shielded
                ? new[] { shieldMaterialOverride }
                : _originalMaterials;
        }

        // --- Shatter child management ----------------------------------------

        /// <summary>
        /// Lazily create the shatter overlay child. Only allocated when the
        /// first disengage actually happens - most prisms are never
        /// super-shielded, so most never pay this cost.
        /// </summary>
        private void EnsureShatterChild()
        {
            if (_shatterChild != null) return;

            _shatterChild = new GameObject("SuperShieldShatter");
            _shatterChild.transform.SetParent(transform, worldPositionStays: false);
            _shatterChild.transform.localPosition = Vector3.zero;
            _shatterChild.transform.localRotation = Quaternion.identity;
            _shatterChild.transform.localScale = Vector3.one;
            _shatterChild.layer = gameObject.layer;

            _shatterMeshFilter = _shatterChild.AddComponent<MeshFilter>();
            _shatterRenderer = _shatterChild.AddComponent<MeshRenderer>();
            _shatterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shatterRenderer.receiveShadows = false;

            _shatterMesh = new Mesh { name = "StellatedOctahedron_SuperShield_Shatter" };
            _shatterMesh.MarkDynamic();

            _shatterChild.SetActive(false);
        }

        private void StopShatter()
        {
            _isShattering = false;
            _shatterT = 0f;
            if (_shatterChild != null)
                _shatterChild.SetActive(false);
        }
    }
}
