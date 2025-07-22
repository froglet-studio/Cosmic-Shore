using UnityEngine;
using CosmicShore.Core;
using System.Collections.Generic;
using CosmicShore.Game;

public class ShipTransformer : MonoBehaviour
{
    protected const float LERP_AMOUNT = 1.5f;


    [SerializeField]
    protected bool toggleManualThrottle;

    #region Ship
    protected IShip Ship;
    protected IShipStatus shipStatus => Ship.ShipStatus;
    protected ResourceSystem resourceSystem;
    #endregion

    protected IInputStatus InputStatus => shipStatus.InputStatus;

    protected float speed;
    protected Quaternion accumulatedRotation;

    [HideInInspector] public float MinimumSpeed;
    [HideInInspector] public float ThrottleScaler;

    public float DefaultMinimumSpeed = 10f;
    public float DefaultThrottleScaler = 50f;
    public ElementalFloat ThrottleScalerMultiplier = new(1f);

    public float PitchScaler = 130f;
    public float YawScaler = 130f;
    public float RollScaler = 130f;
    public float RotationThrottleScaler = 0;

    List<ShipThrottleModifier> ThrottleModifiers = new();
    List<ShipVelocityModifier> VelocityModifiers = new();
    float speedModifierMax = 6f;
    float velocityModifierMax = 100;
    protected float throttleMultiplier = 1;
    public float SpeedMultiplier => throttleMultiplier;
    protected Vector3 velocityShift = Vector3.zero;

    public virtual void Initialize(IShip ship)
    {
        this.Ship = ship;
        resourceSystem = ship.ShipStatus.ResourceSystem;
    }

    protected virtual void Start()
    {
        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        accumulatedRotation = transform.rotation;
    }

    public void ResetShipTransformer()
    {
        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        accumulatedRotation = transform.rotation;
        resourceSystem.Reset();
        shipStatus.ResetValues();
    }

    protected virtual void Update()
    {
        // TODO - Ship should never be null if ShipTransformer is enabled.
        // Find better way to do this.
        if (Ship == null)
            return;

        // TODO - InputStatus will be paused in auto-pilot mode,
        // hence this check is invalid.
        /*if (InputStatus == null)
            return;

        if (InputStatus.Paused)
            return;*/

        shipStatus.blockRotation = transform.rotation;

        RotateShip();

        if (shipStatus.Stationary)
            return;

        ApplyThrottleModifiers();
        ApplyVelocityModifiers();

        MoveShip();
    }

    protected virtual void RotateShip()
    {

        Roll();
        Yaw();
        Pitch();

        if (InputStatus.IsGyroEnabled) //&& !Equals(inverseInitialRotation, new Quaternion(0, 0, 0, 0)))
        {
            // Updates GameObjects blockRotation from input device's gyroscope
            transform.rotation = Quaternion.Slerp(
                                        transform.rotation,
                                        accumulatedRotation * InputStatus.GetGyroRotation(),
                                        LERP_AMOUNT * Time.deltaTime);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(
                                        transform.rotation,
                                        accumulatedRotation,
                                        LERP_AMOUNT * Time.deltaTime);
        }
    }

    #region Public Rotation Methods
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

    public void GentleSpinShip(Vector3 newDirection, Vector3 newUp, float amount)
    {
        accumulatedRotation = Quaternion.Slerp(accumulatedRotation, Quaternion.LookRotation(newDirection, newUp), amount);
    }

    public void ApplyRotation(float angle, Vector3 axis)
    {
        accumulatedRotation = Quaternion.AngleAxis(angle, axis) * accumulatedRotation;
    }
    #endregion

    #region Public translation Methods
    public void TranslateShip(Vector3 nudgeVector)
    {
        transform.position += nudgeVector;
    }

    public void ModifyVelocity(Vector3 amount, float duration)
    {
        VelocityModifiers.Add(new ShipVelocityModifier(amount, duration, 0));
    }
    #endregion

    protected virtual void Pitch() // These need to not use *= because quaternions are not commutative
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            InputStatus.YSum * (speed * RotationThrottleScaler + PitchScaler) * Time.deltaTime,
                            transform.right) * accumulatedRotation;
    }

    protected virtual void Yaw()  // TODO: test replacing these AngleAxis calls with eulerangles
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            InputStatus.XSum * (speed * RotationThrottleScaler + YawScaler)  * Time.deltaTime,
                            transform.up) * accumulatedRotation;
    }

    protected virtual void Roll()
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            InputStatus.YDiff * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime,
                            transform.forward) * accumulatedRotation;
    }

    protected virtual void MoveShip()
    {
        float boostAmount = 1f;
        if (shipStatus.Boosting) // TODO: if we run out of fuel while full speed and straight the ship data still thinks we are boosting
        {
            boostAmount = Ship.ShipStatus.BoostMultiplier;
        }
        if (shipStatus.ChargedBoostDischarging) boostAmount *= shipStatus.ChargedBoostCharge;
        speed = Mathf.Lerp(speed, InputStatus.XDiff * ThrottleScaler * ThrottleScalerMultiplier.Value * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);
        speed *= throttleMultiplier;

        if (toggleManualThrottle)
            speed = Mathf.Lerp(0, speed, InputStatus.Throttle);

        shipStatus.Speed = speed;

        shipStatus.Course = shipStatus.Drifting ? (speed * shipStatus.Course + velocityShift).normalized : transform.forward;

        transform.position += (speed * shipStatus.Course + velocityShift) * Time.deltaTime;
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
            {
                ThrottleModifiers.RemoveAt(i);
                if (ThrottleModifiers.Count == 0)
                {
                    shipStatus.Slowed = false;
                    Hangar.Instance.SlowedShipTransforms.Remove(transform);
                }
            }
            else if (modifier.initialValue < 1) // multiplicative for debuff and additive for buff 
            {
                accumulatedThrottleModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
                shipStatus.Slowed = true;
                Hangar.Instance.SlowedShipTransforms.Add(transform);
            }
            else
                accumulatedThrottleModification += Mathf.Lerp(modifier.initialValue - 1, 0f, modifier.elapsedTime / modifier.duration);
        }

        accumulatedThrottleModification = Mathf.Min(accumulatedThrottleModification, speedModifierMax);
        if (accumulatedThrottleModification < 0f)
        {
            shipStatus.Slowed = false;
            Hangar.Instance.SlowedShipTransforms.Remove(transform);
        }
        throttleMultiplier = Mathf.Max(accumulatedThrottleModification, 0);
        if (throttleMultiplier > 1)
            Ship.ShipStatus.ShipAnimation.FlareEngine();
        else
            Ship.ShipStatus.ShipAnimation.StopFlareEngine();
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
                accumulatedVelocityModification += ((Mathf.Cos(modifier.elapsedTime * Mathf.PI / modifier.duration)/2) + 1) * modifier.initialValue; // cosine interpolation
        }

        velocityShift = Mathf.Min(accumulatedVelocityModification.magnitude, velocityModifierMax) * accumulatedVelocityModification.normalized;

        var sqrMagnitude = velocityShift.sqrMagnitude;

        if (sqrMagnitude > .01f)
            Ship.ShipStatus.ShipAnimation.FlareBody(sqrMagnitude/4000);
        else
            Ship.ShipStatus.ShipAnimation.StopFlareBody();
    }

    private void OnDisable()
    {
        Hangar.Instance.SlowedShipTransforms.Remove(transform);
    }
}