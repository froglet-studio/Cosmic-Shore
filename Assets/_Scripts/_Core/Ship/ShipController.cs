using System.Collections;
using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.Input;


public class ShipController : MonoBehaviour
{
    #region Ship
    protected Ship ship;
    ShipAnimation shipAnimation;
    protected ShipData shipData;
    protected ResourceSystem resourceSystem;
    #endregion

    protected string uuid;
    protected InputController inputController;

    public delegate void Boost(string uuid, float amount);
    public static event Boost OnBoost;

    protected float speed;
    public float boostDecay = 1;

    public float defaultMinimumSpeed = 10f;
    public float DefaultThrottleScaler = 50;
    public float MaxBoostDecay = 10;
    public float BoostDecayGrowthRate = .03f;

    [HideInInspector] public float minimumSpeed;
    [HideInInspector] public float ThrottleScaler;

    public float rotationThrottleScaler = 0;
    public float PitchScaler = 130f;
    public float YawScaler = 130f;
    public float RollScaler = 130f;

    protected readonly float lerpAmount = 2f;

    protected Quaternion displacementQuaternion;
    Quaternion inverseInitialRotation = new(0, 0, 0, 0);

    // Start is called before the first frame update
    protected virtual void Start()
    {
        ship = GetComponent<Ship>();
        uuid = ship.Player.PlayerUUID;
        shipData = ship.GetComponent<ShipData>();
        resourceSystem = ship.GetComponent<ResourceSystem>();

        minimumSpeed = defaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        displacementQuaternion = transform.rotation;
        inputController = ship.inputController;
    }

    public void Reset()
    {
        minimumSpeed = defaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        displacementQuaternion = transform.rotation;
        shipData.Boosting = false;
        shipData.BoostCharging = false;
        shipData.BoostDecaying = false;
        shipData.Drifting = false;
        shipData.Attached = false;
        shipData.GunsActive = false;
        shipData.InputSpeed = 1;
        shipData.SpeedMultiplier = 1;
        shipData.Course = transform.forward;
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (inputController == null) 
            inputController = ship.inputController;
        if (inputController.Paused) 
            return;
        if (inputController.Idle) 
            Idle();
        else
        {
            RotateShip();
            
            shipData.blockRotation = transform.rotation; // TODO: move this
        }
        if (shipData.BoostCharging)
            ChargeBoost();

        MoveShip();
    }

    protected void RotateShip()
    {
        Pitch();
        Yaw();
        Roll();

        if (inputController.isGyroEnabled && !Equals(inverseInitialRotation, new Quaternion(0, 0, 0, 0)))
        {
            // Updates GameObjects blockRotation from input device's gyroscope
            transform.rotation = Quaternion.Lerp(
                                        transform.rotation,
                                        displacementQuaternion * inputController.GetGyroRotation(),
                                        lerpAmount);
        }
        else
        {
            transform.rotation = Quaternion.Lerp(
                                        transform.rotation,
                                        displacementQuaternion,
                                        lerpAmount);
        }
    }

    void ChargeBoost()
    {
        boostDecay += BoostDecayGrowthRate;
        resourceSystem.ChangeBoostAmount(ship.Player.PlayerUUID, BoostDecayGrowthRate);
    }

    public void StartChargedBoost() 
    {
        StartCoroutine(DecayingBoostCoroutine());
    }

    IEnumerator DecayingBoostCoroutine()
    {
        shipData.BoostDecaying = true;
        while (boostDecay > 1)
        {
            boostDecay = Mathf.Clamp(boostDecay - Time.deltaTime, 1, MaxBoostDecay);
            resourceSystem.ChangeBoostAmount(ship.Player.PlayerUUID, -Time.deltaTime);
            yield return null;
        }
        shipData.BoostDecaying = false;
        resourceSystem.ChangeBoostAmount(ship.Player.PlayerUUID, -resourceSystem.CurrentBoost);

    }

    protected virtual void Pitch() // These need to not use *= because quaternions are not commutative
    {
        displacementQuaternion = Quaternion.AngleAxis(
                            inputController.YSum * -(speed * rotationThrottleScaler + PitchScaler) * Time.deltaTime,
                            transform.right) * displacementQuaternion;
    }

    protected virtual void Yaw()  
    {
        displacementQuaternion = Quaternion.AngleAxis(
                            inputController.XSum * (speed * rotationThrottleScaler + YawScaler) *
                                (Screen.currentResolution.width / Screen.currentResolution.height) * Time.deltaTime,
                            transform.up) * displacementQuaternion;
    }

    protected virtual void Roll()
    {
        displacementQuaternion = Quaternion.AngleAxis(
                            inputController.YDiff * (speed * rotationThrottleScaler + RollScaler) * Time.deltaTime,
                            transform.forward) * displacementQuaternion;
    }

    public void Rotate(Vector3 euler)
    {
        displacementQuaternion = Quaternion.Euler(euler) * displacementQuaternion;
    }

    public void Rotate(Quaternion rotation, bool replace = false)
    {
        if (replace) displacementQuaternion = rotation;
        else displacementQuaternion = rotation * displacementQuaternion;
    }


    protected virtual void MoveShip()
    {
        float boostAmount = 1f;
        if (shipData.Boosting && resourceSystem.CurrentBoost > 0) // TODO: if we run out of fuel while full speed and straight the ship data still thinks we are boosting
        {
            boostAmount = ship.boostMultiplier;
            OnBoost?.Invoke(uuid, ship.boostFuelAmount);
        }
        if (shipData.BoostDecaying) boostAmount *= boostDecay;
        speed = Mathf.Lerp(speed, inputController.XDiff * ThrottleScaler * boostAmount + minimumSpeed, lerpAmount * Time.deltaTime);

        // Move ship velocityDirection
        shipData.InputSpeed = speed;
        

        if (shipData.Drifting)
        {
            ship.GetComponent<TrailSpawner>().SetDotProduct(Vector3.Dot(shipData.Course, transform.forward));
        }
        else
        {
            shipData.Course = transform.forward;
        }

        transform.position += shipData.Speed * Time.deltaTime * shipData.Course;

    }

    protected void InvokeBoost(float amount)
    {
        OnBoost?.Invoke(uuid, amount);
    }

    private void Idle()
    {
        
    }

}
