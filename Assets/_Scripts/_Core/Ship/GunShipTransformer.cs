using UnityEngine;
using StarWriter.Core;

public class GunShipTransformer : ShipTransformer
{
    [SerializeField] TrailFollower trailFollower;
    [SerializeField] float rechargeRate = .1f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);
    [SerializeField] ElementalFloat growthAmount = new ElementalFloat(1);

    bool moveForward = true;
    bool attached = false;
    CameraManager cameraManager;


    protected override void Start()
    {
        base.Start();
        cameraManager = CameraManager.Instance;
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void MoveShip()
    {
        if (shipStatus.Attached && !attached)
        {
            trailFollower.Attach(shipStatus.AttachedTrailBlock);
            if (!ship.InputController.AutoPilotEnabled && cameraManager != null)
            {
                cameraManager.SetNormalizedCloseCameraDistance(1);
                Debug.Log("camera distance now set to 1");
            }
        }
        else if (!shipStatus.Attached && attached)
        {
            trailFollower.Detach();
            if (!ship.InputController.AutoPilotEnabled && cameraManager != null)
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

        var throttle = (inputController.XDiff - zeroPosition)/(1 - zeroPosition);

        if (Vector3.Dot(transform.forward, shipStatus.Course) < lookThreshold && throttle > 0)
             moveForward = !moveForward;

        if ((moveForward && throttle > 0) || (!moveForward && throttle < 0))
            trailFollower.SetDirection(TrailFollowerDirection.Forward);
        else
            trailFollower.SetDirection(TrailFollowerDirection.Backward);

        trailFollower.Throttle = Mathf.Abs(throttle);
        trailFollower.Move();

        SlideActions();
    }
    void SlideActions()
    {
        // TODO: should this be pulled out as an action type?
        if (trailFollower.AttachedTrailBlock.Shielded) resourceSystem.ChangeAmmoAmount(rechargeRate * 2 * Time.deltaTime);
        else resourceSystem.ChangeAmmoAmount(rechargeRate * Time.deltaTime);
    }

    public void FinalBlockSlideEffects()
    {
        shipStatus.AttachedTrailBlock = trailFollower.AttachedTrailBlock;

        if (shipStatus.AttachedTrailBlock.destroyed)
            shipStatus.AttachedTrailBlock.Restore();

        if (shipStatus.AttachedTrailBlock.Team == ship.Team)
        {
            shipStatus.AttachedTrailBlock.Grow(growthAmount.Value);
        }
        else shipStatus.AttachedTrailBlock.Steal(ship.Player.PlayerName, ship.Team);
    }
}