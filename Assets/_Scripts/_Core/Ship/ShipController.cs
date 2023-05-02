using System.Collections;
using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.Input;
using System.Collections.Generic;

public class ShipController : MonoBehaviour
{
    Quaternion inverseInitialRotation = new(0, 0, 0, 0);

    #region Ship
    protected Ship ship;
    protected ShipData shipData;
    protected ResourceSystem resourceSystem;
    #endregion

    protected InputController inputController;
    protected float speed;
    protected readonly float lerpAmount = 2f;
    protected Quaternion accumulatedRotation;

    [HideInInspector] public float MinimumSpeed;
    [HideInInspector] public float ThrottleScaler;

    public float DefaultMinimumSpeed = 10f;
    public float DefaultThrottleScaler = 50;
    public float BoostDecay = 1;
    public float MaxBoostDecay = 10;
    public float BoostDecayGrowthRate = .03f;

    public float PitchScaler = 130f;
    public float YawScaler = 130f;
    public float RollScaler = 130f;
    public float RotationThrottleScaler = 0;

    List<ShipThrottleModifier> ThrottleModifiers = new();
    List<ShipVelocityModifier> VelocityModifiers = new();
    float speedModifierMax = 6f;
    float velocityModifierMax = 100;
    float throttleMultiplier = 1;
    public float SpeedMultiplier { get { return throttleMultiplier; } }
    Vector3 velocityShift = Vector3.zero;


    protected virtual void Start()
    {
        ship = GetComponent<Ship>();
        shipData = ship.GetComponent<ShipData>();
        resourceSystem = ship.GetComponent<ResourceSystem>();

        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        accumulatedRotation = transform.rotation;
        inputController = ship.InputController;
    }

    public void Reset()
    {
        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        accumulatedRotation = transform.rotation;
        resourceSystem.Reset();
        shipData.Reset();
    }

    protected virtual void Update()
    {
        if (inputController == null)
        {
            inputController = ship.InputController;
        }
        if (inputController != null)
        { 
            if (inputController.Paused)
                return;

            if (inputController.Idle)
                return;
        }
            

        if (shipData.BoostCharging)
            ChargeBoost();

        ApplyThrottleModifiers();
        ApplyVelocityModifiers();

        RotateShip();
        shipData.blockRotation = transform.rotation;

        MoveShip();
    }

    protected void RotateShip()
    {
        
        if (inputController != null)
        {
            
            Roll();
            Yaw();
            Pitch();

            if (inputController.isGyroEnabled && !Equals(inverseInitialRotation, new Quaternion(0, 0, 0, 0)))
            {
                // Updates GameObjects blockRotation from input device's gyroscope
                transform.rotation = Quaternion.Lerp(
                                            transform.rotation,
                                            accumulatedRotation * inputController.GetGyroRotation(),
                                            lerpAmount);
            }
            else
            {
                transform.rotation = Quaternion.Lerp(
                                            transform.rotation,
                                            accumulatedRotation,
                                            lerpAmount);
            }
        }
        else
        {
            transform.rotation = Quaternion.Lerp(
                                        transform.rotation,
                                        accumulatedRotation,
                                        lerpAmount);
        }
    }

    public void FlatSpinShip(float YAngle)
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            180,
                            transform.up) * accumulatedRotation;
    }

    public void SpinShip(Vector3 newDirection)
    {
        transform.localRotation = Quaternion.LookRotation(newDirection);
    }

    public void StartChargedBoost() 
    {
        StartCoroutine(DecayingBoostCoroutine());
    }

    void ChargeBoost()
    {
        BoostDecay += BoostDecayGrowthRate;
        resourceSystem.ChangeBoostAmount(BoostDecayGrowthRate);
    }

    IEnumerator DecayingBoostCoroutine()
    {
        shipData.BoostDecaying = true;
        while (BoostDecay > 1)
        {
            BoostDecay = Mathf.Clamp(BoostDecay - Time.deltaTime, 1, MaxBoostDecay);
            resourceSystem.ChangeBoostAmount(-Time.deltaTime);
            yield return null;
        }
        shipData.BoostDecaying = false;
        resourceSystem.ChangeBoostAmount(-resourceSystem.CurrentBoost);
    }

    protected virtual void Pitch() // These need to not use *= because quaternions are not commutative
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            inputController.YSum * (speed * RotationThrottleScaler + PitchScaler) * Time.deltaTime,
                            transform.right) * accumulatedRotation;
    }

    protected virtual void Yaw()  
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            inputController.XSum * (speed * RotationThrottleScaler + YawScaler) *
                                (Screen.currentResolution.width / Screen.currentResolution.height) * Time.deltaTime,
                            transform.up) * accumulatedRotation;
    }

    protected virtual void Roll()
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            inputController.YDiff * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime,
                            transform.forward) * accumulatedRotation;
    }

    protected virtual void MoveShip()
    {
        float boostAmount = 1f;
        if (shipData.Boosting && resourceSystem.CurrentBoost > 0) // TODO: if we run out of fuel while full speed and straight the ship data still thinks we are boosting
        {
            boostAmount = ship.boostMultiplier;
            resourceSystem.ChangeBoostAmount(ship.boostFuelAmount);
        }
        if (shipData.BoostDecaying) boostAmount *= BoostDecay;
        if (inputController != null)
        speed = Mathf.Lerp(speed, inputController.XDiff * ThrottleScaler * boostAmount + MinimumSpeed, lerpAmount * Time.deltaTime);

        speed *= throttleMultiplier;
        shipData.Speed = speed;

        if (shipData.Drifting)
        {
            ship.GetComponent<TrailSpawner>().SetDotProduct(Vector3.Dot(shipData.Course, transform.forward));
        }
        else
        {
            shipData.Course = transform.forward;
        }

        transform.position += (speed * shipData.Course + velocityShift) * Time.deltaTime;
    }

    public void ModifyThrottle(float amount, float duration)
    {
        ThrottleModifiers.Add(new ShipThrottleModifier(amount, duration, 0));
    }

    void ApplyThrottleModifiers()
    {
        float accumulatedThrottleModification = 1;
        for (int i = ThrottleModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = ThrottleModifiers[i];
            modifier.elapsedTime += Time.deltaTime;
            ThrottleModifiers[i] = modifier;

            if (modifier.elapsedTime >= modifier.duration)
                ThrottleModifiers.RemoveAt(i);
            else
                accumulatedThrottleModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
        }

        accumulatedThrottleModification = Mathf.Min(accumulatedThrottleModification, speedModifierMax);
        throttleMultiplier = Mathf.Max(accumulatedThrottleModification, 0) ;
    }

    public void ModifyVelocity(Vector3 amount, float duration)
    {
        VelocityModifiers.Add(new ShipVelocityModifier(amount, duration, 0));
    }

    void ApplyVelocityModifiers()
    {
        Vector3 accumulatedVelocityModification = Vector3.zero;
        for (int i = VelocityModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = VelocityModifiers[i];
            modifier.elapsedTime += Time.deltaTime;
            VelocityModifiers[i] = modifier;

            if (modifier.elapsedTime >= modifier.duration)
                VelocityModifiers.RemoveAt(i);
            else
                accumulatedVelocityModification += Vector3.Lerp(modifier.initialValue, Vector3.zero, modifier.elapsedTime / modifier.duration);
        }

        velocityShift = Mathf.Min(accumulatedVelocityModification.magnitude, velocityModifierMax) * accumulatedVelocityModification.normalized;
    }
}