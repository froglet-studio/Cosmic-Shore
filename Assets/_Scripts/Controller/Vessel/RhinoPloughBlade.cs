using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's MASS ability, the Blade: a plough blade across the nose, one hull collider whose WIDTH
    /// follows the Rhino's Mass level (Garrett, 2026-11-07: "a wedge-shaped plough ahead of the hull that
    /// widens with Mass"). It is an ordinary hull collider (a trigger under the vessel's root Rigidbody with an
    /// <see cref="ImpactCollider"/>), so <see cref="VesselImpactor.HullColliders"/> already includes it: a wider
    /// blade catches more mass, and the plough's clearance (<see cref="RhinoPlough.HullReach"/>) grows with it,
    /// so what it catches is thrown clear of the whole blade.
    ///
    /// <para>The width is read at the REPLICATED Mass level (<see cref="ElementalFloat.EvaluateReplicated"/>), so
    /// every peer ploughs with the same blade. Only the box's local x changes; height, depth and centre stay as
    /// authored on the collider. Design: <c>R_VesselActions/RHINO_PLOUGH.md</c> § "The Blade (Mass)".</para>
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RhinoPloughBlade : MonoBehaviour
    {
        [Header("Width (Mass)")]
        [Tooltip("The blade's width in world units across the hull's right axis: at rest, at Mass 10, and its floor " +
                 "in the deficit band. The hull is ~12 u wide, so the rest width is a little wider than the hull and " +
                 "Mass 10 is about three hulls.")]
        [SerializeField] ElementalFloat width = ElementalFloat.Multiplier(14f, 40f, Element.Mass, 10f);

        BoxCollider _box;
        IVesselStatus _status;
        float _applied = -1f;

        /// <summary>The blade's live width, world units.</summary>
        public float Width => Mathf.Max(0.01f, width.EvaluateReplicated(_status));

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _status = GetComponentInParent<IVesselStatus>();
        }

        void OnEnable() => _applied = -1f;

        void Update()
        {
            if (!_box) return;
            float w = Width;
            if (Mathf.Approximately(w, _applied)) return;
            _applied = w;
            float sx = Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.x));
            var size = _box.size;
            size.x = w / sx;
            _box.size = size;
        }
    }
}
