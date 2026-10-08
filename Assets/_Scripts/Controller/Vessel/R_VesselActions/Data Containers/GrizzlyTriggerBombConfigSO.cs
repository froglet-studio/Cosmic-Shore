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
    ///   release -> detonate it - and a Grizzly inside its own blast is LAUNCHED AWAY FROM IT
    ///
    /// <b>Only the trigger detonates a bomb.</b> It never goes off on a clock or on contact. It
    /// CRUISES - constant velocity, no drag, no range limit (<see cref="Projectile.Cruises"/>) -
    /// THROUGH prisms (lighting them as it passes - LIT, Docs/LIT.md) until the trigger freezes it
    /// or it touches another vessel, which freezes it too; the next release blows it (design
    /// asks, 2026-10-08).
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
        [SerializeField, Tooltip("Muzzle speed ADDED to the hull's own velocity, u/s. The bomb then CRUISES at that velocity - no drag, no range limit - until the trigger or another vessel freezes it, so it always leaves faster than the Grizzly that fired it.")]
        float projectileSpeed = 90f;
        [SerializeField, Tooltip("Visible bomb scale of a minimum bomb. Small on purpose: a bomb is a little hot thing that becomes a huge blast (design ask, 2026-10-08).")]
        float minProjectileScale = 2.5f;
        [SerializeField, Tooltip("Visible bomb scale of a maximum bomb - still small; the glow halo (GrizzlyBombVisual) carries the read.")]
        float maxProjectileScale = 5f;
        [SerializeField, Tooltip("Degrees each trigger's bomb is yawed off the muzzle (LT left, RT right) so the two bombs read as two.")]
        float sideYawDegrees = 3f;

        [Header("Blast")]
        [SerializeField, Tooltip("Explosion prefab(s) spawned at detonation. Spawned with AffectSelfOverride OFF: the pilot's own domain is spared (own trail shields, teammates untouched); enemy mass and pilots are hit and knocked back. The pilot's own launch is applied by the executor (Self Launch below).")]
        AOEExplosion[] aoePrefabs;
        [SerializeField, Tooltip("Blast scale of a minimum bomb. Reach is half the scale.")]
        float minBlastScale = 50f;
        [SerializeField, Tooltip("Blast scale of a maximum bomb (the cannon's full charge is 120). Reach is half the scale: 100 u.")]
        float maxBlastScale = 200f;

        [Header("Self launch (away from the bomb)")]
        [SerializeField, Tooltip("A Grizzly inside its own blast is thrown AWAY FROM THE BOMB at blast scale / ExplosionDuration x this, capped by Self Launch Ceiling. 4.5 = three times the 1.5 it first shipped at (design ask, 2026-10-08).")]
        float selfLaunchMultiplier = 4.5f;
        [SerializeField, Tooltip("The velocity ceiling a bomb launch may reach, u/s. Every other shove a vessel takes shares VesselTransformer's 100 u/s cap; a live bomb launch raises it to this for its own lifetime only (ShipVelocityModifier.ceiling). 300 = three times the shared cap, which a full squeeze used to sit on.")]
        float selfLaunchCeiling = 300f;
        [SerializeField, Tooltip("Seconds the launch lasts (cosine ease-out, VesselTransformer.ModifyVelocity).")]
        float selfLaunchSeconds = 1f;
        [SerializeField, Tooltip("Launch strength at the blast's EDGE as a fraction of the strength at the bomb. 1 = flat; lower rewards blowing it close.")]
        [Range(0f, 1f)] float selfLaunchEdgeStrength = 0.5f;

        [Header("Colour")]
        [SerializeField, Tooltip("Fallback DANGER colour, used only when the theme palette authors none (SO_ColorSet.GetDangerSignalColor answers alpha 0). The shipped palette's danger rim wins.")]
        Color fallbackDangerColor = new(1f, 0.072f, 0.105f, 1f);
        [SerializeField, Tooltip("How far each trigger's bomb is rotated off the danger hue, in HSV hue (0-1). LT turns one way (toward crimson-magenta), RT the other (toward red-orange), so the two bombs are both DANGER and still told apart.")]
        [Range(0f, 0.2f)] float sideHueShift = 0.045f;

        [Header("Autopilot")]
        [SerializeField, Tooltip("An AUTOPILOT presses no triggers (AIPilot writes stick and throttle only), so without this an AI Grizzly never bombs. While AIPilot.AutoPilotEnabled the executor fires full-size bombs while the stick is inside this band, freezes each one Ai Freeze Distance ahead, flies past it and detonates it behind - a launch. 0 disables the drive.")]
        [Range(0f, 1f)] float aiFireStickBand = 0.35f;
        [SerializeField, Tooltip("How far ahead of the hull an autopilot freezes its bomb, world units. Must sit well inside the blast radius (maxBlastScale / 2) or the launch misses the hull.")]
        float aiFreezeDistance = 18f;
        [FormerlySerializedAs("aiDetonateDistance")]
        [SerializeField, Tooltip("An autopilot flies PAST its frozen bomb and detonates it once it is this far behind the hull, world units - the launch is away from the bomb, so behind you is forward.")]
        float aiDetonateBehindDistance = 10f;
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
        public float SideYawDegrees => sideYawDegrees;
        public AOEExplosion[] AoePrefabs => aoePrefabs;
        public float MinBlastScale => minBlastScale;
        public float MaxBlastScale => maxBlastScale;
        public float AiFireStickBand => aiFireStickBand;
        public float AiFreezeDistance => aiFreezeDistance;
        public float AiDetonateBehindDistance => aiDetonateBehindDistance;
        public float SelfLaunchMultiplier => selfLaunchMultiplier;
        public float SelfLaunchSeconds => selfLaunchSeconds;
        public float SelfLaunchEdgeStrength => selfLaunchEdgeStrength;
        public float SelfLaunchCeiling => selfLaunchCeiling;
        public Color FallbackDangerColor => fallbackDangerColor;
        public float SideHueShift => sideHueShift;
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

        /// <summary>
        /// One trigger's bomb colour: <paramref name="danger"/> (alpha 0 = no palette colour, use
        /// <see cref="FallbackDangerColor"/>) rotated <see cref="SideHueShift"/> one way for LT and
        /// the other for RT, saturation and brightness kept. Pure; edit-mode tested.
        /// </summary>
        public Color BombColor(bool leftTrigger, Color danger)
        {
            var c = danger.a > 0f ? danger : fallbackDangerColor;
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            h = Mathf.Repeat(h + (leftTrigger ? -sideHueShift : sideHueShift), 1f);
            var shifted = Color.HSVToRGB(h, sat, v);
            shifted.a = 1f;
            return shifted;
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
