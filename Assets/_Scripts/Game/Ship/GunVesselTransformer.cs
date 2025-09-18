using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine.InputSystem.LowLevel;

public class GunVesselTransformer : VesselTransformer
{
    BlockscapeFollower trailFollower;
    [SerializeField] float rechargeRate = .1f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);
    [SerializeField] ElementalFloat growthAmount = new ElementalFloat(1);

    bool moveForward = true;
    bool attached = false;
    CameraManager cameraManager;

    [SerializeField] int ammoIndex = 0;


    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        cameraManager = CameraManager.Instance;
        trailFollower = GetComponent<BlockscapeFollower>();
    }

    protected override void MoveShip()
    {
        switch (VesselStatus.Attached)
        {
            case true when !attached:
            {
                trailFollower.Attach(VesselStatus.AttachedTrailBlock);
                if (Vessel.VesselStatus.AutoPilotEnabled && cameraManager != null)
                {
                    cameraManager.SetNormalizedCloseCameraDistance(1);
                    Debug.Log("camera distance now set to 1");
                }

                break;
            }
            case false when attached:
            {
                trailFollower.Detach();
                if (!Vessel.VesselStatus.AutoPilotEnabled && cameraManager != null)
                {
                    cameraManager.SetNormalizedCloseCameraDistance(0);
                    Debug.Log("camera distance now set to 0");
                }

                break;
            }
        }

        attached = VesselStatus.Attached;

        if (attached)
            Slide();
        else
            base.MoveShip();
    }

    void Slide()
    {
        // TODO: magic numbers
        float lookThreshold = -.6f;
        float zeroPosition = .2f;

        // TODO - Vessel components should not be accessing InputStatus directly.
        // var throttle = (InputStatus.XDiff - zeroPosition) / (1 - zeroPosition);
        var throttle = 0;

        if (Vector3.Dot(transform.forward, VesselStatus.Course) < lookThreshold && throttle > 0)
            moveForward = !moveForward;

        trailFollower.Throttle = Mathf.Abs(throttle);
        trailFollower.RideTheTrail();

        SlideActions();
    }
    void SlideActions()
    {
        // TODO: should this be pulled out as an action type?
        if (trailFollower.AttachedTrailBlock.TrailBlockProperties.IsShielded) resourceSystem.ChangeResourceAmount(ammoIndex, rechargeRate * 2 * Time.deltaTime);
        else resourceSystem.ChangeResourceAmount(ammoIndex, rechargeRate * Time.deltaTime);
    }

    public void FinalBlockSlideEffects()
    {
        VesselStatus.AttachedTrailBlock = trailFollower.AttachedTrailBlock;

        if (VesselStatus.AttachedTrailBlock.destroyed)
            VesselStatus.AttachedTrailBlock.Restore();

        if (VesselStatus.AttachedTrailBlock.Team == Vessel.VesselStatus.Team)
        {
            VesselStatus.AttachedTrailBlock.Grow(growthAmount.Value);
        }
        else VesselStatus.AttachedTrailBlock.Steal(Vessel.VesselStatus.Player.Name, Vessel.VesselStatus.Team);
    }
}