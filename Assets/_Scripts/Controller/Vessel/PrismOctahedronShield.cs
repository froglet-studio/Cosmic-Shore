using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the visual and physical transition between a prism's unshielded
    /// box state and its shielded circumscribing octahedron state.
    ///
    /// States:
    ///   Unshielded: authored BoxCollider is the trigger, authored prism mesh
    ///               visible, mass = rho · 8·a·b·c
    ///   Shielded:   the prism's shield AABB proxy BoxCollider is the trigger
    ///               (no MeshCollider — the octahedron's exact surface is enforced
    ///               by the analytic narrowphase gate, see
    ///               <see cref="IShieldContainmentGate"/> /
    ///               <see cref="ImpactorBase.OnTriggerEnter"/>), octahedron mesh
    ///               visible, mass = rho · 36·a·b·c (exactly 4.5× the box mass
    ///               by default)
    ///
    /// Collider cost: the engaged shield is a plain BoxCollider + a 3-linear-form
    /// L1 point test — no convex MeshCollider cook, no convex narrowphase, and the
    /// proxy participates in the proximity collider-LOD like any other prism
    /// collider (the old MeshCollider was LOD-exempt, an always-on budget line).
    ///
    /// Engage: per-face bloom morph - 8 faces grow outward from their centroids.
    /// Disengage: box mesh snaps back immediately, then a shatter overlay plays
    ///   where each octahedron face simultaneously shrinks and flies outward
    ///   along its face normal, mirroring the prism destruction VFX.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismOctahedronShield : MonoBehaviour, IShieldContainmentGate, IPrismShieldTicker
    {
        [Header("Collider Sources")]
        [Tooltip("The authored BoxCollider that defines the unshielded shape. Its center/size drive the octahedron geometry.")]
        [SerializeField] private BoxCollider boxCollider;

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
        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly.")]
        [SerializeField] private float engageDuration = 0.35f;

        [Tooltip("Easing curve applied to the engage morph progress (0→1).")]
        [SerializeField] private AnimationCurve engageCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Shatter (Disengage)")]
        [Tooltip("Duration of the shatter VFX overlay after disengaging. 0 snaps instantly.")]
        [SerializeField] private float shatterDuration = 0.6f;

        [Tooltip("How far each face flies outward (in local-space units) at the end of the shatter.")]
        [SerializeField] private float shatterMaxOffset = 3f;

        [Tooltip("Easing curve applied to the shatter progress (0→1). Output drives both face-offset and face-shrink.")]
        [SerializeField] private AnimationCurve shatterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Shield Geometry")]
        [Tooltip("Circumscribing scale factor. 3 is the minimum that guarantees all box corners are inside the octahedron.")]
        [SerializeField] private float shieldScale = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        // --- Runtime state ---------------------------------------------------

        private Mesh _originalMesh;
        private Mesh _octahedronMesh;     // static full-size octahedron, owned
        private Mesh _morphMesh;           // reused every frame during engage morph, owned
        private Vector3 _halfExtents;      // from BoxCollider.size * 0.5
        private Vector3 _center;           // from BoxCollider.center
        private float _boxMass;
        private float _shieldMass;
        private Material[] _originalMaterials;
        private MeshRenderer _meshRenderer;

        private bool _isShielded;

        // -- Engage morph state --
        private float _engageT;            // 0 = collapsed, 1 = full octahedron
        private bool _isEngaging;

        // -- Shatter overlay state --
        private float _shatterT;           // 0 = start, 1 = fully shattered
        private bool _isShattering;

        // Lazily-created child that renders the shatter overlay so the parent
        // MeshFilter can show the box mesh while the faces fly away.
        private GameObject _shatterChild;
        private MeshFilter _shatterMeshFilter;
        private MeshRenderer _shatterRenderer;
        private Mesh _shatterMesh;

        // Owning prism — the shield's morphing per-prism mesh can't be instanced,
        // so engage/disengage hand rendering between the companion entity and
        // this GameObject's MeshRenderer (Prism.SetExoticVisualActive).
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
            // Prefer the prism's authored collider — a bare GetComponent could grab
            // the shield AABB proxy (a second BoxCollider on the same GameObject)
            // when this component is added after a proxy already exists.
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

            // Settled octahedron comes from the shared cache: half-extents are the authored
            // LOCAL collider size, so every same-prefab shield resolves to ONE mesh and
            // settled shields batch on the instanced render path instead of each owning a
            // unique octahedron. Render-only — the physics side is the box AABB proxy, no
            // mesh is ever cooked. Cache-owned: never destroy it here.
            _octahedronMesh = OctahedronMeshGenerator.GetSharedShieldMesh(_halfExtents, shieldScale);
            _morphMesh = new Mesh { name = "Octahedron_Shield_Morph" };
            _morphMesh.MarkDynamic();

            ComputeMassTargets();
        }

        private void OnDisable()
        {
            // Snap to clean state when the GameObject is disabled (e.g.
            // pooled back). Prevents stale visuals on pool reuse.
            // Stop being ticked the moment we're pooled/disabled (cheap if not registered).
            PrismOctahedronShieldManager.Instance?.Unregister(this);

            if (_isShielded || _isEngaging || _isShattering)
            {
                _engageT = 0f;
                _shatterT = 0f;
                _isEngaging = false;
                _isShattering = false;
                _isShielded = false;
                if (_octahedronMesh != null)
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
            // _octahedronMesh is cache-shared (other shields reference it) — not destroyed here.
            if (_morphMesh != null)      Destroy(_morphMesh);
            if (_shatterMesh != null)    Destroy(_shatterMesh);
            if (_shatterChild != null)   Destroy(_shatterChild);
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
            if (_isShielded && !_isEngaging) return;

            // If a shatter overlay is still playing, kill it immediately.
            StopShatter();

            _isShielded = true;

            // The morph mesh is per-prism-unique geometry — render through the
            // GameObject while the shield is up (no-op on the legacy path).
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
        /// Disengage the supershield. Box mesh snaps back immediately; a
        /// shatter overlay plays where each octahedron face flies outward
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
            // instanced path. The shatter overlay plays on its own child renderer,
            // independent of this.
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
                // Start the shatter overlay.
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
        /// while <see cref="IsShielded"/> is true.
        /// </summary>
        public bool IsPointInsideShield(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - _center;
            return OctahedronMeshGenerator.ContainsPointLocal(local, _invA, _invB, _invC);
        }

        /// <summary>
        /// Narrowphase gate over the engaged shield's AABB proxy trigger — rejects
        /// contacts in the AABB corners outside the octahedron. Installed on the
        /// prism while Engaged (<see cref="Prism.ActiveShieldGate"/>).
        /// </summary>
        bool IShieldContainmentGate.ContainsWorldPoint(Vector3 worldPoint) => IsPointInsideShield(worldPoint);

        bool IPrismShieldTicker.Tick(float dt) => Tick(dt);

        // --- Transition driver -----------------------------------------------

        /// <summary>
        /// Advances any in-progress engage/shatter morph by <paramref name="dt"/>.
        /// Called by <see cref="PrismOctahedronShieldManager"/> ONLY while this shield
        /// is registered (i.e. actively transitioning) — idle shields are not ticked,
        /// so there is no per-prism Update() at scale. Returns true while still
        /// transitioning; the manager drops it when this returns false.
        /// </summary>
        internal bool Tick(float dt)
        {
            if (_isEngaging)
                DriveEngage(dt);

            if (_isShattering)
                DriveShatter(dt);

            return _isEngaging || _isShattering;
        }

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
        /// Per-face bloom for engage: faces grow from centroid points to full size.
        /// </summary>
        private void UpdateEngageMesh(float faceScale)
        {
            OctahedronMeshGenerator.PopulateMeshFaceScale(
                _morphMesh, _halfExtents, faceScale, shieldScale);

            if (meshFilter != null)
                meshFilter.sharedMesh = _morphMesh;
        }

        /// <summary>
        /// Shatter overlay: each face shrinks toward its centroid AND flies
        /// outward along its face normal. Rendered on the child overlay object
        /// while the parent shows the box mesh.
        ///   t=0: faces at full size, in place (just-disengaged octahedron)
        ///   t=1: faces collapsed to centroid points, displaced far along normals
        /// </summary>
        private void UpdateShatterMesh(float t)
        {
            float faceScale = 1f - t;            // 1→0 (shrink)
            float faceOffset = t * shatterMaxOffset; // 0→max (fly outward)

            OctahedronMeshGenerator.PopulateMeshFaceShatter(
                _shatterMesh, _halfExtents, faceScale, faceOffset, shieldScale);

            _shatterMeshFilter.sharedMesh = _shatterMesh;
        }

        // --- Pose application ------------------------------------------------

        private void ApplyShieldedPose()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = _octahedronMesh;

            ApplyColliderState(ShieldColliderState.Engaged);

            if (rb != null)
                rb.mass = _shieldMass;

            ApplyMaterialOverride(shielded: true);

            // The settled octahedron is SHARED geometry — hand rendering back to the
            // companion entity so every same-size shielded prism batches into one draw.
            // Only the engage morph (above) and the shatter overlay are per-prism-unique
            // and need the GameObject renderer. No-op on the legacy path.
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
        /// first disengage actually happens - most prisms are never shielded,
        /// so most never pay this cost.
        /// </summary>
        private void EnsureShatterChild()
        {
            if (_shatterChild != null) return;

            _shatterChild = new GameObject("ShieldShatter");
            _shatterChild.transform.SetParent(transform, worldPositionStays: false);
            // Reset local transform so the overlay inherits parent's position/rotation/scale.
            _shatterChild.transform.localPosition = Vector3.zero;
            _shatterChild.transform.localRotation = Quaternion.identity;
            _shatterChild.transform.localScale = Vector3.one;
            // Stay on the same layer so rendering/culling matches.
            _shatterChild.layer = gameObject.layer;

            _shatterMeshFilter = _shatterChild.AddComponent<MeshFilter>();
            _shatterRenderer = _shatterChild.AddComponent<MeshRenderer>();
            _shatterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shatterRenderer.receiveShadows = false;

            _shatterMesh = new Mesh { name = "Octahedron_Shield_Shatter" };
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
