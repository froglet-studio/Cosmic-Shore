using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One Rhino trigger steering the plough (Garrett, 2026-11-07: "pulling it should be a little shove in the
    /// direction of the trigger, holding it keeps the plough steered"). The PRESS gives the hull a small sideways
    /// shove toward the trigger's side; while the trigger is HELD every prism the hull ploughs is thrown to that
    /// side, whichever side of the centreline it sat on. Release hands the plough back to its own rule (each prism
    /// goes to the side it already sat on). LT is the left side, RT the right.
    ///
    /// <para>Stateless: the held state lives on <see cref="RhinoPloughSteerExecutor"/>, the numbers here.
    /// Presses replicate (R_VesselActionHandler), so every peer holds the same steered side and ploughs alike.
    /// Design: <c>R_VesselActions/RHINO_PLOUGH.md</c> § "Steering".</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoPloughSteerAction",
        menuName = "ScriptableObjects/Vessel Actions/RhinoPloughSteerActionSO")]
    public sealed class RhinoPloughSteerActionSO : ShipActionSO
    {
        public enum SteerSide
        {
            Right = 0,
            Left = 1,
        }

        [Header("Side")]
        [Tooltip("Right = the right trigger: shove right, throw ploughed prisms right. Left mirrors it.")]
        [SerializeField] SteerSide side = SteerSide.Right;

        [Header("Shove")]
        [Tooltip("Sideways speed the press adds to the hull, u/s, toward this side. A LITTLE shove: under the " +
                 "fleet's shared displacement ceiling (VesselTransformer.velocityModifierMax, 100 u/s), so it never " +
                 "saturates it. 0 = no shove, the trigger only steers.")]
        [SerializeField, Min(0f)] float shoveSpeed = 30f;

        [Tooltip("Seconds the shove lasts; it eases out over this (ShipVelocityModifier's cosine). With the speed " +
                 "this sets the sidestep: about speed x seconds x 2/pi, ~5 u at the shipped 30 u/s over 0.25 s.")]
        [SerializeField, Min(0.01f)] float shoveSeconds = 0.25f;

        /// <summary>+1 for the right trigger, -1 for the left, along the hull's right axis.</summary>
        public float Sign => side == SteerSide.Right ? 1f : -1f;

        public float ShoveSpeed => shoveSpeed;
        public float ShoveSeconds => shoveSeconds;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var steer = execs ? execs.Get<RhinoPloughSteerExecutor>() : null;
            if (steer) steer.Press(Sign);

            if (shoveSpeed <= 0f || vesselStatus == null) return;
            var transformer = vesselStatus.VesselTransformer;
            var hull = vesselStatus.ShipTransform;
            if (!transformer || !hull) return;
            transformer.ModifyVelocity(hull.right * (Sign * shoveSpeed), shoveSeconds);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var steer = execs ? execs.Get<RhinoPloughSteerExecutor>() : null;
            if (steer) steer.Release(Sign);
        }
    }
}
