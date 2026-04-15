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
        /// Morph the 8 box corners inward to the 6 octahedron vertices. At t=0
        /// the mesh renders the box; at t=1 the mesh renders the octahedron.
        /// Implementation: we tessellate the box into 8 corner tetrahedra and
        /// collapse each tetra's outer corner toward the nearest face-center
        /// vertex. For prototype simplicity we instead lerp the 8 box-corner
        /// positions toward the octahedron's 6 face-centers along the dominant
        /// axis, then rebuild the octahedron faces. Practically this means we
        /// just scale the octahedron mesh from (1/shieldScale) of its final
        /// size up to full size while cross-fading from the authored box mesh.
        /// </summary>
        private void UpdateMorphMesh(float t)
        {
            // Simple, effective morph strategy for a prototype: rebuild the
            // octahedron at an interpolated scale factor. At t=0 the octahedron
            // is shrunk to exactly the inscribed dual (factor 1, vertices at
            // face centers), which visually coincides with the box silhouette
            // along each axis. At t=1 it's at full circumscribing size (factor 3).
            float scale = Mathf.Lerp(1f, shieldScale, t);
            OctahedronMeshGenerator.PopulateMesh(_morphMesh, _halfExtents, scale);

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
