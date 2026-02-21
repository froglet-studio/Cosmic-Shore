using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using Reflex.Attributes;

public class GunVesselTransformer : VesselTransformer
{
    [Inject] CameraManager injectedCameraManager;
    BlockscapeFollower trailFollower;
    [SerializeField] float rechargeRate = .1f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);
    [SerializeField] ElementalFloat growthAmount = new ElementalFloat(1);

    bool moveForward = true;
    bool attached = false;
    [SerializeField] int ammoIndex = 0;


    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        trailFollower = GetComponent<BlockscapeFollower>();
    }

    protected override void MoveShip()
    {
        switch (VesselStatus.IsAttached)
        {
            case true when !attached:
            {
                trailFollower.Attach(VesselStatus.AttachedPrism);
                if (Vessel.VesselStatus.AutoPilotEnabled && injectedCameraManager != null)
                {
                    injectedCameraManager.SetNormalizedCloseCameraDistance(1);
                    Debug.Log("camera distance now set to 1");
                }

                break;
            }
            case false when attached:
            {
                trailFollower.Detach();
                if (!Vessel.VesselStatus.AutoPilotEnabled && injectedCameraManager != null)
                {
                    injectedCameraManager.SetNormalizedCloseCameraDistance(0);
                    Debug.Log("camera distance now set to 0");
                }

                break;
            }
        }

        attached = VesselStatus.IsAttached;

        if (attached)
            Slide();
        else
            base.MoveShip();
    }

    void Slide()
    {
        // TODO: magic number
        float lookThreshold = -.6f;

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
        var rs = VesselStatus.ResourceSystem;
        // TODO: should this be pulled out as an action type?
        if (trailFollower.AttachedPrism.prismProperties.IsShielded) rs.ChangeResourceAmount(ammoIndex, rechargeRate * 2 * Time.deltaTime);
        else rs.ChangeResourceAmount(ammoIndex, rechargeRate * Time.deltaTime);
    }

    public void FinalBlockSlideEffects()
    {
        VesselStatus.AttachedPrism = trailFollower.AttachedPrism;

        if (VesselStatus.AttachedPrism.destroyed)
            VesselStatus.AttachedPrism.Restore();

        if (VesselStatus.AttachedPrism.Domain == Vessel.VesselStatus.Domain)
        {
            VesselStatus.AttachedPrism.Grow(growthAmount.Value);
        }
        else VesselStatus.AttachedPrism.Steal(Vessel.VesselStatus.Player.Name, Vessel.VesselStatus.Domain);
    }
}