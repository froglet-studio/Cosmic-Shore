using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Which of the Gibbon's two arms an input drives. Left = LT / LShift, Right = RT / RShift.</summary>
    public enum GibbonArm
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>
    /// One arm of the Gibbon, as a vessel action so it rides the platform's input plumbing:
    /// bound on the prefab to <c>LeftStickAction</c> (LT) / <c>RightStickAction</c> (RT),
    /// which is what lets the ability lockup draw the correct trigger chip, lets the press
    /// replicate through <c>R_VesselActionHandler</c> like every other ability, and retires
    /// the old binding to the DERIVED <c>FullSpeedStraightAction</c> (whose "release" fired the
    /// moment the pilot moved a stick more than 0.3 — both lines dropped exactly when they
    /// started steering).
    ///
    /// Press = CAST the arm at its reticle; release = LET GO (fling). The reel is not an action:
    /// it is the trigger's analog depth, read every frame by the transformer, because a winch
    /// rate is a continuous quantity and an action is an edge.
    /// </summary>
    [CreateAssetMenu(fileName = "GibbonArmAction", menuName = "ScriptableObjects/Vessel Actions/Gibbon Arm")]
    public class GibbonArmActionSO : ShipActionSO
    {
        [Header("Gibbon Arm")]
        [Tooltip("Which arm this asset drives. The prefab binds the Left asset to LeftStickAction (LT) and the Right asset to RightStickAction (RT).")]
        [SerializeField] GibbonArm arm = GibbonArm.Left;

        public GibbonArm Arm => arm;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus?.VesselTransformer is SwingingVesselTransformer gibbon)
                gibbon.CastArm(arm);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus?.VesselTransformer is SwingingVesselTransformer gibbon)
                gibbon.ReleaseArm(arm);
        }
    }
}
