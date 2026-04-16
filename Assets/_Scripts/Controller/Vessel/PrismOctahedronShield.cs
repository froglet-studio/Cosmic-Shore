using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the visual and physical transition between a prism's unshielded
    /// box state and its supershielded circumscribing octahedron state.
    ///
    /// States:
    ///   Unshielded:   BoxCollider active, authored prism mesh visible,
    ///                 mass = rho · 8·a·b·c
    ///   Supershielded: MeshCollider (convex) active with generated octahedron,
    ///                 octahedron mesh visible, mass = rho · 36·a·b·c
    ///                 (exactly 4.5× the box mass by default)
    ///
    /// The transition can snap or lerp. During a lerp the collider is disabled
    /// to avoid generating intermediate convex hulls every frame; it is
    /// re-enabled in the final pose.
    ///
    /// Fast overlap test: <see cref="IsPointInsideShield"/> uses the
    /// precomputed L1 inverses (<see cref="_invA"/>, <see cref="_invB"/>,
    /// <see cref="_invC"/>) for branchless gameplay queries that don't need a
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

        [Header("Transition")]
        [Tooltip("Duration of the box→octahedron morph. 0 snaps instantly.")]
        [SerializeField] private float transitionDuration = 0.35f;

        [Tooltip("Easing curve applied to the morph progress (0→1).")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Shield Geometry")]
        [Tooltip("Circumscribing scale factor. 3 is the minimum that guarantees all box corners are inside the octahedron.")]
        [SerializeField] private float shieldScale = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        // --- Runtime state ---------------------------------------------------

        private Mesh _originalMesh;
        private Mesh _octahedronMesh;     // instance owned by this component
        private Mesh _morphMesh;           // reused every frame during lerp
        private Vector3 _halfExtents;      // from BoxCollider.size * 0.5
        private Vector3 _center;           // from BoxCollider.center
        private float _boxMass;
        private float _shieldMass;
        private Material[] _originalMaterials;
        private MeshRenderer _meshRenderer;

        private bool _isShielded;
        private float _transitionT;        // 0 = box, 1 = octahedron
        private bool _isTransitioning;
        private float _transitionSign;     // +1 engaging, -1 disengaging

        // Precomputed fast-path containment inverses.
        private float _invA, _invB, _invC;

        public bool IsShielded => _isShielded;
        public float TransitionProgress => _transitionT;

        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            if (meshFilter == null)  meshFilter  = GetComponent<MeshFilter>();
            if (rb == null)          rb          = GetComponent<Rigidbody>();
            _meshRenderer = GetComponent<MeshRenderer>();

            CacheGeometry();

            if (meshFilter != null)
                _originalMesh = meshFilter.sharedMesh;

            if (_meshRenderer != null)
                _originalMaterials = _meshRenderer.sharedMaterials;

            // Build the octahedron mesh once from the cached half-extents.
            _octahedronMesh = OctahedronMeshGenerator.Generate(_halfExtents, shieldScale);
            _morphMesh = new Mesh { name = "Octahedron_Shield_Morph" };
            _morphMesh.MarkDynamic();

            ComputeMassTargets();
        }

        private void OnDisable()
        {
            // Snap to unshielded state when the GameObject is disabled (e.g.
            // pooled back to the prism pool). On next activation, the prism's
            // Initialize / ActivateShield path will re-engage if needed. This
            // prevents a pooled prism from coming back out still rendering
            // as an octahedron.
            if (_isShielded || _isTransitioning)
            {
                _transitionT = 0f;
                _transitionSign = 0f;
                _isTransitioning = false;
                _isShielded = false;
                // Only touch components if we actually initialized (Awake ran).
                if (_octahedronMesh != null)
                    ApplyFinalPose(shielded: false);
            }
        }

        private void OnDestroy()
        {
            if (_octahedronMesh != null) Destroy(_octahedronMesh);
            if (_morphMesh != null)      Destroy(_morphMesh);
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

            // Guard against degenerate axes before computing inverses.
            float a = Mathf.Max(_halfExtents.x * shieldScale, 1e-5f);
            float b = Mathf.Max(_halfExtents.y * shieldScale, 1e-5f);
            float c = Mathf.Max(_halfExtents.z * shieldScale, 1e-5f);
            _invA = 1f / a;
            _invB = 1f / b;
            _invC = 1f / c;
        }

        private void ComputeMassTargets()
        {
            float vBox = 8f * _halfExtents.x * _halfExtents.y * _halfExtents.z;
            if (density > 0f)
            {
                _boxMass = density * vBox;
                _shieldMass = density * 36f * _halfExtents.x * _halfExtents.y * _halfExtents.z;
            }
            else
            {
                _boxMass = rb != null ? rb.mass : 1f;
                _shieldMass = _boxMass * massRatioShielded;
            }
        }

        // --- Public API ------------------------------------------------------

        /// <summary>Engage the supershield. Lerps if transitionDuration &gt; 0.</summary>
        [ContextMenu("Engage Shield")]
        public void EngageContextMenu() => Engage();

        /// <summary>Disengage the supershield. Lerps if transitionDuration &gt; 0.</summary>
        [ContextMenu("Disengage Shield")]
        public void DisengageContextMenu() => Disengage();

        /// <summary>Toggle current state. Handy for runtime testing.</summary>
        [ContextMenu("Toggle Shield")]
        public void Toggle()
        {
            if (_isShielded) Disengage();
            else Engage();
        }

        public void Engage(bool instant = false)
        {
            if (_isShielded && !_isTransitioning) return;
            _isShielded = true;
            if (instant || transitionDuration <= 0f)
            {
                _transitionT = 1f;
                ApplyFinalPose(shielded: true);
                _isTransitioning = false;
            }
            else
            {
                _transitionSign = +1f;
                _isTransitioning = true;
                DisableCollidersDuringMorph();
                // Immediately swap to the morph mesh at the current t so we
                // don't render one frame of the box mesh before Update kicks in.
                // At t=0 the face-scale is 0, so faces are collapsed to their
                // centroids (invisible) — the box vanishes and the shield
                // panels bloom outward starting next frame.
                UpdateMorphMesh(transitionCurve.Evaluate(_transitionT));
            }
        }

        /// <summary>Disengage the supershield and return to box state.</summary>
        public void Disengage(bool instant = false)
        {
            if (!_isShielded && !_isTransitioning) return;
            _isShielded = false;
            if (instant || transitionDuration <= 0f)
            {
                _transitionT = 0f;
                ApplyFinalPose(shielded: false);
                _isTransitioning = false;
            }
            else
            {
                _transitionSign = -1f;
                _isTransitioning = true;
                DisableCollidersDuringMorph();
                // Immediately update morph mesh at current t (should be 1.0)
                // so the face-shrink starts rendering this frame.
                UpdateMorphMesh(transitionCurve.Evaluate(_transitionT));
            }
        }

        /// <summary>
        /// Branchless point-in-shield test (world-space input). Only valid
        /// while <see cref="IsShielded"/> is true — callers should gate on that.
        /// </summary>
        public bool IsPointInsideShield(Vector3 worldPoint)
        {
            // Transform world → local then subtract the box center.
            Vector3 local = transform.InverseTransformPoint(worldPoint) - _center;
            return OctahedronMeshGenerator.ContainsPointLocal(local, _invA, _invB, _invC);
        }

        // --- Transition driver ----------------------------------------------

        private void Update()
        {
            if (!_isTransitioning) return;

            float step = (transitionDuration > 0f ? (Time.deltaTime / transitionDuration) : 1f) * _transitionSign;
            _transitionT = Mathf.Clamp01(_transitionT + step);

            UpdateMorphMesh(transitionCurve.Evaluate(_transitionT));

            bool done = (_transitionSign > 0f && _transitionT >= 1f)
                     || (_transitionSign < 0f && _transitionT <= 0f);
            if (done)
            {
                _isTransitioning = false;
                ApplyFinalPose(_isShielded);
            }
        }

        /// <summary>
        /// Per-face bloom morph. Each of the 8 triangular faces grows outward
        /// from its own centroid:
        ///   t=0 → every face collapsed to a single point (invisible)
        ///   t=1 → faces at full size, forming the complete octahedron
        ///
        /// This replaces the old uniform-scale morph (whole shape growing from
        /// inscribed → circumscribing) with a more visually distinctive
        /// animation where individual shield panels "open" independently.
        /// </summary>
        private void UpdateMorphMesh(float t)
        {
            OctahedronMeshGenerator.PopulateMeshFaceScale(
                _morphMesh, _halfExtents, t, shieldScale);

            if (meshFilter != null)
                meshFilter.sharedMesh = _morphMesh;
        }

        private void ApplyFinalPose(bool shielded)
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = shielded ? _octahedronMesh : _originalMesh;

            if (boxCollider != null)
                boxCollider.enabled = !shielded;

            if (shielded)
            {
                EnsureShieldMeshCollider();
                if (shieldMeshCollider != null)
                {
                    shieldMeshCollider.sharedMesh = _octahedronMesh;
                    shieldMeshCollider.convex = true;
                    shieldMeshCollider.enabled = true;
                }
            }
            else if (shieldMeshCollider != null)
            {
                shieldMeshCollider.enabled = false;
            }

            if (rb != null)
                rb.mass = shielded ? _shieldMass : _boxMass;

            ApplyMaterialOverride(shielded);
        }

        private void DisableCollidersDuringMorph()
        {
            if (boxCollider != null) boxCollider.enabled = false;
            if (shieldMeshCollider != null) shieldMeshCollider.enabled = false;
        }

        private void EnsureShieldMeshCollider()
        {
            if (shieldMeshCollider != null) return;
            shieldMeshCollider = gameObject.AddComponent<MeshCollider>();
            shieldMeshCollider.convex = true;
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
