using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Krill's <see cref="VesselAnimation"/> (design: <c>R_VesselActions/KRILL.md</c>). The carrier hull is the VIEW and
    /// is hidden while the swarm flies, so there is no hull to puppeteer: this exists so the vessel has the concrete
    /// animation <see cref="VesselStatus"/> requires, and so the base class keeps driving the krill model's four element
    /// morphs (Charge / Mass / Space / Time, <c>Tools/Build/vessel_model/specs/krill.py</c>) on the hidden model, which
    /// <see cref="KrillSwarmExecutor"/> copies onto every member.
    /// </summary>
    public class KrillAnimation : VesselAnimation
    {
        [Header("Hull")]
        [Tooltip("The transform that holds the krill model (KrillHull). Leave empty and the model is found under the vessel.")]
        [SerializeField] Transform hullVisual;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            // The model (Krill.fbx, nested under the hull) is found, not wired: its sub-objects' fileIDs exist only after
            // Unity imports it. The base drives its element shapes by name.
            if (!SkinnedMeshRenderer)
                SkinnedMeshRenderer = hullVisual
                    ? hullVisual.GetComponentInChildren<SkinnedMeshRenderer>(true)
                    : GetComponentInChildren<SkinnedMeshRenderer>(true);
            base.Initialize(vesselStatus);
        }

        // Nothing to assign or puppeteer: the members are posed by the executor from their own headings.
        protected override void AssignTransforms() { }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) { }

        protected override void Idle() { }
    }
}
