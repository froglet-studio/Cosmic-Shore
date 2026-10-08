using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Grizzly's TRIGGER BOMBS - LT and RT each own one bomb. See
    /// GRIZZLY_TRIGGER_BOMBS.md.
    ///
    /// Per trigger, the gesture is the charged cannon's (GRIZZLY_CHARGED_CANNON.md), with
    /// PRESSURE in place of hold time:
    ///   pull    -> arm (the peak analog pressure of this pull is the bomb's size)
    ///   release -> fire: spend ammo for that size, launch a visible bomb
    ///   pull    -> freeze the bomb where it is
    ///   release -> detonate it - and a Grizzly inside its own blast is LAUNCHED
    ///
    /// <b>Pressure is the commitment.</b> One number - the peak pressure - sets the ammo spent,
    /// the bomb's visible size and the blast's size together, so a feather tap is a cheap pop
    /// and a full squeeze is an expensive launch. A pull the ammo pool cannot pay for in full
    /// fires the biggest bomb it CAN pay for; a pool below the smallest bomb fizzles.
    ///
    /// Wired directly on <see cref="GrizzlyTriggerBombExecutor"/> (shared by both trigger
    /// actions) so a missing wire shows in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyTriggerBombConfig", menuName = "ScriptableObjects/Vessel Actions/Grizzly Trigger Bomb Config")]
    public class GrizzlyTriggerBombConfigSO : ScriptableObject
    {
        [Header("Pressure -> size")]
        [SerializeField, Tooltip("Trigger pressure (0-1) that yields the SMALLEST bomb. Anything at or below it is a minimum bomb.")]
        [Range(0f, 1f)] float pressureForMinBomb = 0.1f;
        [SerializeField, Tooltip("Trigger pressure (0-1) that yields the BIGGEST bomb. Set under 1 because many triggers never report a full 1.0.")]
        [Range(0f, 1f)] float pressureForMaxBomb = 0.95f;
        [SerializeField, Tooltip("Shape of the pressure -> size curve. 1 = linear, >1 makes the top of the pull matter more.")]
        [Range(0.25f, 4f)] float pressureExponent = 1f;

        [Header("Ammo")]
        [SerializeField, Tooltip("Index of the Ammo resource in the Grizzly's ResourceSystem (Energy is 0 and belongs to the charged cannon).")]
        int ammoIndex = 1;
        [SerializeField, Tooltip("Ammo a minimum bomb costs (fraction of a full pool).")]
        float minAmmoCost = 0.08f;
        [SerializeField, Tooltip("Ammo a maximum bomb costs (fraction of a full pool).")]
        float maxAmmoCost = 0.35f;

        [Header("Projectile")]
        [SerializeField, Tooltip("Muzzle speed ADDED to the hull's own velocity, u/s. The flight eases to rest over the fuse (Projectile's cos(pi t / 2T)), so it leaves faster than the Grizzly and the Grizzly catches it near the end.")]
        float projectileSpeed = 90f;
        [SerializeField, Tooltip("Fuse, seconds. A bomb nobody freezes detonates where it comes to rest; one that hits a prism detonates there.")]
        float projectileTime = 3f;
        [SerializeField, Tooltip("Visible bomb scale of a minimum bomb.")]
        float minProjectileScale = 8f;
        [SerializeField, Tooltip("Visible bomb scale of a maximum bomb.")]
        float maxProjectileScale = 20f;
        [SerializeField, Tooltip("Degrees each trigger's bomb is yawed off the muzzle (LT left, RT right) so the two bombs read as two.")]
        float sideYawDegrees = 3f;

        [Header("Blast")]
        [SerializeField, Tooltip("Explosion prefab(s) spawned at detonation. AffectSelfOverride is on: a Grizzly inside its own blast is launched along its nose (VesselImpulseByExplosionEffectSO) - and, as with the cannon, the blast also breaks its own trail.")]
        AOEExplosion[] aoePrefabs;
        [SerializeField, Tooltip("Blast scale of a minimum bomb. The self-launch impulse is scale / ExplosionDuration x selfLaunchMultiplier, capped by the 100 u/s velocity ceiling.")]
        float minBlastScale = 30f;
        [SerializeField, Tooltip("Blast scale of a maximum bomb (the cannon's full charge is 120).")]
        float maxBlastScale = 120f;

        [Header("Autopilot")]
        [SerializeField, Tooltip("An AUTOPILOT presses no triggers (AIPilot writes stick and throttle only), so without this an AI Grizzly never bombs. While AIPilot.AutoPilotEnabled the executor fires full-size bombs while the stick is inside this band, freezes each one Ai Freeze Distance ahead and detonates it as the hull closes on it - a launch. 0 disables the drive.")]
        [Range(0f, 1f)] float aiFireStickBand = 0.35f;
        [SerializeField, Tooltip("How far ahead of the hull an autopilot freezes its bomb, world units. Must sit well inside the blast radius (maxBlastScale / 2) or the launch misses the hull.")]
        float aiFreezeDistance = 18f;
        [SerializeField, Tooltip("An autopilot detonates a frozen bomb once the hull is this close to it, world units.")]
        float aiDetonateDistance = 10f;
        [SerializeField, Tooltip("Seconds between autopilot bombs, so the two triggers alternate rather than fire together.")]
        float aiFireIntervalSeconds = 0.6f;

        [Header("Audio")]
        [SerializeField, Tooltip("FMOD event played when a bomb is fired. Leave empty for silence.")]
        EventReference fireEvent;
        [FormerlySerializedAs("bombEvent")]
        [SerializeField, Tooltip("FMOD event played when a bomb detonates. Leave empty for silence.")]
        EventReference detonateEvent;

        public float PressureForMinBomb => pressureForMinBomb;
        public float PressureForMaxBomb => pressureForMaxBomb;
        public float PressureExponent => pressureExponent;
        public int AmmoIndex => ammoIndex;
        public float MinAmmoCost => minAmmoCost;
        public float MaxAmmoCost => maxAmmoCost;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileTime => projectileTime;
        public float SideYawDegrees => sideYawDegrees;
        public AOEExplosion[] AoePrefabs => aoePrefabs;
        public float MinBlastScale => minBlastScale;
        public float MaxBlastScale => maxBlastScale;
        public float AiFireStickBand => aiFireStickBand;
        public float AiFreezeDistance => aiFreezeDistance;
        public float AiDetonateDistance => aiDetonateDistance;
        public float AiFireIntervalSeconds => aiFireIntervalSeconds;
        public EventReference FireEvent => fireEvent;
        public EventReference DetonateEvent => detonateEvent;

        /// <summary>Peak trigger pressure (0-1) -> bomb size (0-1). Pure; edit-mode tested.</summary>
        public float SizeForPressure(float pressure)
        {
            float lo = Mathf.Min(pressureForMinBomb, pressureForMaxBomb);
            float hi = Mathf.Max(pressureForMinBomb, pressureForMaxBomb);
            float t = hi - lo <= 1e-5f ? (pressure >= hi ? 1f : 0f) : Mathf.Clamp01((pressure - lo) / (hi - lo));
            return Mathf.Pow(t, pressureExponent);
        }

        public float AmmoCostForSize(float size01) => Mathf.Lerp(minAmmoCost, maxAmmoCost, Mathf.Clamp01(size01));
        public float ProjectileScaleForSize(float size01) => Mathf.Lerp(minProjectileScale, maxProjectileScale, Mathf.Clamp01(size01));
        public float BlastScaleForSize(float size01) => Mathf.Lerp(minBlastScale, maxBlastScale, Mathf.Clamp01(size01));

        /// <summary>
        /// The size actually fired for a pull that asked for <paramref name="requestedSize01"/>
        /// with <paramref name="ammo"/> in the pool: the request when the pool can pay for it,
        /// else the biggest bomb the pool CAN pay for, else -1 (fizzle - the pool cannot buy
        /// even a minimum bomb). Pure; edit-mode tested.
        /// </summary>
        public float AffordableSize(float requestedSize01, float ammo)
        {
            float size = Mathf.Clamp01(requestedSize01);
            if (ammo + 1e-5f >= AmmoCostForSize(size)) return size;
            if (ammo + 1e-5f < minAmmoCost) return -1f;
            float span = maxAmmoCost - minAmmoCost;
            return span <= 1e-5f ? 0f : Mathf.Clamp01((ammo - minAmmoCost) / span);
        }
    }
}
