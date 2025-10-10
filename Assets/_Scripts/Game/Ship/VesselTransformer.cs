using UnityEngine;
using CosmicShore.Core;
using System.Collections.Generic;
using CosmicShore.Game;
using CosmicShore.SOAP;

public class VesselTransformer : MonoBehaviour
{
    protected const float LERP_AMOUNT = 1.5f;

    [SerializeField] private MiniGameDataSO miniGameData;
    [SerializeField] protected bool toggleManualThrottle;

    #region Vessel
    protected IVessel Vessel;
    protected IVesselStatus VesselStatus => Vessel?.VesselStatus;
    protected ResourceSystem ResourceSystem => VesselStatus?.ResourceSystem;
    #endregion

    protected IInputStatus InputStatus => VesselStatus?.InputStatus;

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
    public float RotationThrottleScaler = 0f;

    private readonly List<ShipThrottleModifier> ThrottleModifiers = new();
    private readonly List<ShipVelocityModifier> VelocityModifiers = new();

    private float speedModifierMax = 6f;
    private float velocityModifierMax = 100f;
    protected float throttleMultiplier = 1f;
    public float SpeedMultiplier => throttleMultiplier;

    protected Vector3 velocityShift = Vector3.zero;
    private bool isInitialized;

    // ----------------------------- Initialization -----------------------------
    public virtual void Initialize(IVessel vessel)
    {
        this.Vessel = vessel;
        ResetTransformer();
        isInitialized = true;
    }

    // ----------------------------- Update Loop -----------------------------
    protected virtual void Update()
    {
        if (!isInitialized || VesselStatus == null || VesselStatus.IsStationary)
            return;

        VesselStatus.blockRotation = transform.rotation;

        RotateShip();
        ApplyThrottleModifiers();
        ApplyVelocityModifiers();
        MoveShip();
    }

    // ----------------------------- Reset State -----------------------------
    public void ResetTransformer()
    {
        // Core speed/rotation
        MinimumSpeed = DefaultMinimumSpeed;
        ThrottleScaler = DefaultThrottleScaler;
        speed = 0f;
        throttleMultiplier = 1f;

        // Rotation — reset to face forward
        accumulatedRotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;

        // Movement
        velocityShift = Vector3.zero;

        // Remove lingering modifiers and states
        ThrottleModifiers.Clear();
        VelocityModifiers.Clear();

        /*if (miniGameData != null)
            miniGameData.SlowedShipTransforms.Remove(transform);*/

        // Reset vessel animation/state safely
        if (VesselStatus != null)
        {
            VesselStatus.Speed = 0f;
            VesselStatus.Course = transform.forward;
            VesselStatus.Slowed = false;

            VesselStatus.ShipAnimation?.StopFlareEngine();
            VesselStatus.ShipAnimation?.StopFlareBody();
        }
    }

    // ----------------------------- Rotation Logic -----------------------------
    protected virtual void RotateShip()
    {
        // Apply rotational inputs
        Roll();
        Yaw();
        Pitch();

        if (InputStatus != null && InputStatus.IsGyroEnabled)
        {
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

    // ----------------------------- Public Controls -----------------------------
    public void SetPose(Pose pose)
    {
        transform.SetPositionAndRotation(pose.position, pose.rotation);
        accumulatedRotation = pose.rotation;
    }

    public void FlatSpinShip(float YAngle)
    {
        accumulatedRotation = Quaternion.AngleAxis(180, transform.up) * accumulatedRotation;
    }

    public void SpinShip(Vector3 newDirection)
    {
        accumulatedRotation = Quaternion.LookRotation(newDirection);
    }

    public void GentleSpinShip(Vector3 newDirection, Vector3 newUp, float amount)
    {
        accumulatedRotation = Quaternion.Slerp(accumulatedRotation, Quaternion.LookRotation(newDirection, newUp), amount);
    }

    public void ApplyRotation(float angle, Vector3 axis)
    {
        accumulatedRotation = Quaternion.AngleAxis(angle, axis) * accumulatedRotation;
    }

    // ----------------------------- Movement Logic -----------------------------
    protected virtual void Pitch()
    {
        if (InputStatus == null) return;
        accumulatedRotation = Quaternion.AngleAxis(
            InputStatus.YSum * (speed * RotationThrottleScaler + PitchScaler) * Time.deltaTime,
            transform.right) * accumulatedRotation;
    }

    protected virtual void Yaw()
    {
        if (InputStatus == null) return;
        accumulatedRotation = Quaternion.AngleAxis(
            InputStatus.XSum * (speed * RotationThrottleScaler + YawScaler) * Time.deltaTime,
            transform.up) * accumulatedRotation;
    }

    protected virtual void Roll()
    {
        if (InputStatus == null) return;
        accumulatedRotation = Quaternion.AngleAxis(
            InputStatus.YDiff * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime,
            transform.forward) * accumulatedRotation;
    }

    protected virtual void MoveShip()
    {
        if (VesselStatus == null || InputStatus == null) return;

        float boostAmount = 1f;
        if (VesselStatus.Boosting)
            boostAmount = VesselStatus.BoostMultiplier;

        if (VesselStatus.ChargedBoostDischarging)
            boostAmount *= VesselStatus.ChargedBoostCharge;

        // Smooth throttle speed calculation
        speed = Mathf.Lerp(
            speed,
            InputStatus.XDiff * ThrottleScaler * ThrottleScalerMultiplier.Value * boostAmount + MinimumSpeed,
            LERP_AMOUNT * Time.deltaTime);

        speed *= throttleMultiplier;

        if (toggleManualThrottle)
            speed = Mathf.Lerp(0, speed, InputStatus.Throttle);

        VesselStatus.Speed = speed;

        // If drifting, keep direction; otherwise, go straight
        VesselStatus.Course = VesselStatus.Drifting
            ? (speed * VesselStatus.Course + velocityShift).normalized
            : transform.forward;

        transform.position += (speed * VesselStatus.Course + velocityShift) * Time.deltaTime;
    }

    // ----------------------------- Modifiers -----------------------------
    public void ModifyThrottle(float amount, float duration)
    {
        ThrottleModifiers.Add(new ShipThrottleModifier(amount, duration, 0));
    }

    private void ApplyThrottleModifiers()
    {
        float accumulatedThrottleModification = 1f;

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
                    miniGameData?.SlowedShipTransforms.Remove(transform);
                }
            }
            else if (modifier.initialValue < 1f)
            {
                accumulatedThrottleModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
                VesselStatus.Slowed = true;
                miniGameData?.SlowedShipTransforms.Add(transform);
            }
            else
            {
                accumulatedThrottleModification += Mathf.Lerp(modifier.initialValue - 1f, 0f, modifier.elapsedTime / modifier.duration);
            }
        }

        accumulatedThrottleModification = Mathf.Clamp(accumulatedThrottleModification, 0f, speedModifierMax);

        if (accumulatedThrottleModification < 0.001f)
        {
            VesselStatus.Slowed = false;
            miniGameData?.SlowedShipTransforms.Remove(transform);
        }

        throttleMultiplier = Mathf.Max(accumulatedThrottleModification, 0f);

        if (throttleMultiplier > 1f)
            VesselStatus.ShipAnimation?.FlareEngine();
        else
            VesselStatus.ShipAnimation?.StopFlareEngine();
    }

    private void ApplyVelocityModifiers()
    {
        Vector3 accumulatedVelocity = Vector3.zero;

        for (int i = VelocityModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = VelocityModifiers[i];
            modifier.elapsedTime += Time.deltaTime;
            VelocityModifiers[i] = modifier;

            if (modifier.elapsedTime >= modifier.duration)
                VelocityModifiers.RemoveAt(i);
            else
                accumulatedVelocity += ((Mathf.Cos(modifier.elapsedTime * Mathf.PI / modifier.duration) / 2) + 1) * modifier.initialValue;
        }

        velocityShift = Mathf.Min(accumulatedVelocity.magnitude, velocityModifierMax) * accumulatedVelocity.normalized;

        var sqrMag = velocityShift.sqrMagnitude;

        if (sqrMag > 0.01f)
            VesselStatus.ShipAnimation?.FlareBody(sqrMag / 4000);
        else
            VesselStatus.ShipAnimation?.StopFlareBody();
    }

    public void TranslateShip(Vector3 nudgeVector)
    {
        transform.position += nudgeVector;
    }

    public void ModifyVelocity(Vector3 amount, float duration)
    {
        VelocityModifiers.Add(new ShipVelocityModifier(amount, duration, 0));
    }
}
