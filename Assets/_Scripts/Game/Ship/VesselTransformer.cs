using UnityEngine;
using CosmicShore.Core;
using System.Collections.Generic;
using CosmicShore.Game;
using CosmicShore.SOAP;

public class VesselTransformer : MonoBehaviour
{
    protected const float LERP_AMOUNT = 1.5f;

    [SerializeField]
    MiniGameDataSO miniGameData;

    [SerializeField]
    protected bool toggleManualThrottle;

    #region Vessel
    protected IVessel Vessel;
    protected IVesselStatus VesselStatus => Vessel.VesselStatus;
    protected ResourceSystem resourceSystem => Vessel.VesselStatus.ResourceSystem;
    #endregion

    protected IInputStatus InputStatus => VesselStatus.InputStatus;

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

    private bool isInitialized;

    public virtual void Initialize(IVessel vessel)
    {
        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        accumulatedRotation = transform.rotation;
        
        Vessel = vessel;
        isInitialized = true;
    }
    
    protected virtual void Update()
    {
        if (!isInitialized)
            return;
        
        VesselStatus.blockRotation = transform.rotation;

        RotateShip();

        if (VesselStatus.IsStationary)
            return;

        ApplyThrottleModifiers();
        ApplyVelocityModifiers();

        MoveShip();
    }

    public void ResetShipTransformer()
    {
        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        accumulatedRotation = transform.rotation;
        resourceSystem.Reset();
        VesselStatus.ResetValues();
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
        if (VesselStatus.Boosting) // TODO: if we run out of fuel while full speed and straight the vessel data still thinks we are boosting
        {
            boostAmount = Vessel.VesselStatus.BoostMultiplier;
        }
        if (VesselStatus.ChargedBoostDischarging) boostAmount *= VesselStatus.ChargedBoostCharge;
        speed = Mathf.Lerp(speed, InputStatus.XDiff * ThrottleScaler * ThrottleScalerMultiplier.Value * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);
        speed *= throttleMultiplier;

        if (toggleManualThrottle)
            speed = Mathf.Lerp(0, speed, InputStatus.Throttle);

        VesselStatus.Speed = speed;

        VesselStatus.Course = VesselStatus.Drifting ? (speed * VesselStatus.Course + velocityShift).normalized : transform.forward;

        transform.position += (speed * VesselStatus.Course + velocityShift) * Time.deltaTime;
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
                    VesselStatus.Slowed = false;
                    miniGameData.SlowedShipTransforms.Remove(transform);
                }
            }
            else if (modifier.initialValue < 1) // multiplicative for debuff and additive for buff 
            {
                accumulatedThrottleModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
                VesselStatus.Slowed = true;
                miniGameData.SlowedShipTransforms.Add(transform);
            }
            else
                accumulatedThrottleModification += Mathf.Lerp(modifier.initialValue - 1, 0f, modifier.elapsedTime / modifier.duration);
        }

        accumulatedThrottleModification = Mathf.Min(accumulatedThrottleModification, speedModifierMax);
        if (accumulatedThrottleModification < 0f)
        {
            VesselStatus.Slowed = false;
            miniGameData.SlowedShipTransforms.Remove(transform);
        }
        throttleMultiplier = Mathf.Max(accumulatedThrottleModification, 0);
        if (throttleMultiplier > 1)
            Vessel.VesselStatus.ShipAnimation.FlareEngine();
        else
            Vessel.VesselStatus.ShipAnimation.StopFlareEngine();
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
            Vessel.VesselStatus.ShipAnimation.FlareBody(sqrMagnitude/4000);
        else
            Vessel.VesselStatus.ShipAnimation.StopFlareBody();
    }

    // TODO - Should not access hangar like this.
    // Use different way!
    /*private void OnDisable()
    {
        
        // Hangar.Instance.SlowedShipTransforms.Remove(transform);
    }*/
}