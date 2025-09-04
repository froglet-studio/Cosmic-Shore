using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem.LowLevel;

public class GunShipTransformer : ShipTransformer
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


    protected override void Start()
    {
        base.Start();
        cameraManager = CameraManager.Instance;
        trailFollower = GetComponent<BlockscapeFollower>();
    }

    protected override void MoveShip()
    {
        if (shipStatus.Attached && !attached)
        {
            trailFollower.Attach(shipStatus.AttachedTrailBlock);
            if (Ship.ShipStatus.AutoPilotEnabled && cameraManager != null)
            {
                cameraManager.SetNormalizedCloseCameraDistance(1);
                Debug.Log("camera distance now set to 1");
            }
        }
        else if (!shipStatus.Attached && attached)
        {
            trailFollower.Detach();
            if (!Ship.ShipStatus.AutoPilotEnabled && cameraManager != null)
            {
                cameraManager.SetNormalizedCloseCameraDistance(0);
                Debug.Log("camera distance now set to 0");
            }
        }

        attached = shipStatus.Attached;

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

        // TODO - Ship components should not be accessing InputStatus directly.
        // var throttle = (InputStatus.XDiff - zeroPosition) / (1 - zeroPosition);
        var throttle = 0;

        if (Vector3.Dot(transform.forward, shipStatus.Course) < lookThreshold && throttle > 0)
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
        shipStatus.AttachedTrailBlock = trailFollower.AttachedTrailBlock;

        if (shipStatus.AttachedTrailBlock.destroyed)
            shipStatus.AttachedTrailBlock.Restore();

        if (shipStatus.AttachedTrailBlock.Team == Ship.ShipStatus.Team)
        {
            shipStatus.AttachedTrailBlock.Grow(growthAmount.Value);
        }
        else shipStatus.AttachedTrailBlock.Steal(Ship.ShipStatus.Player.Name, Ship.ShipStatus.Team);
    }
}