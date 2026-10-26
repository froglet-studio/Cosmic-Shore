using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Rhino sword (RHINO_ENERGY_SWORD.md): the energize ritual (the supershield key,
    /// and its Live Wire level-5), how the Space length eases and how far the camera follows it, the
    /// omni-crystal surge's sword ring and camera, and every blade-look/juice knob (energized danger
    /// red, impact flashes, crackle, camera shake). There is no energy (2026-10-26). The blade's
    /// LENGTH range is Space's: Skimmer.Scale on Rhino.prefab (24 at rest, 40 at Space 10).
    /// One shared asset (`_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset`)
    /// drives every Rhino, read by <see cref="ShieldSkimmerScaleDriver"/> and
    /// <see cref="RhinoSwordFXController"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShieldSkimmerScaleConfig",
        menuName = "ScriptableObjects/Skimmer/ShieldSkimmerScaleConfigSO")]
    public sealed class ShieldSkimmerScaleConfigSO : ScriptableObject
    {
        [Header("Length (Space alone) and the camera that follows it")]
        [Tooltip("World units/sec the blade eases toward its Space length when the Space level changes.")]
        [SerializeField, Min(0.01f)] private float spaceLengthEaseSpeed = 30f;
        [Tooltip("How much of the blade's growth the local pilot's camera follows: 1 = it pulls back in " +
                 "proportion (a blade 1.67x its resting length puts the camera 1.67x as far back, so the " +
                 "blade reads the same size on screen); 0 = the camera never moves for it.")]
        [SerializeField, Range(0f, 1f)] private float cameraFollowsBladeLength = 1f;

        [Header("Energize (hold the both-triggers chop stance — the supershield key)")]
        [Tooltip("Seconds the lower/chop stance must be held to energize.")]
        [SerializeField] private float energizeHoldSeconds = 1f;
        [Tooltip("PHONE ONLY: how many times FASTER the energize charge fills for a human flying on touch. " +
                 "A phone pilot energizes by lifting BOTH thumbs, which also lets go of the steering, so the " +
                 "hold is divided by this (2 = half the hold). 1 = the pad's timing. AI and autopilot keep the " +
                 "pad's timing. Decided on the vessel's owner and replicated with the energize phase " +
                 "(RhinoSwordNetworkRelay), so every peer ignites on the same edge.")]
        [SerializeField, Min(0.01f)] private float touchEnergizeRateMultiplier = 2f;
        [Tooltip("Seconds the blade stays energized AFTER leaving the stance. Holding the stance " +
                 "from ignition keeps it lit; once let go the tail runs out whatever the fingers do - " +
                 "unless Live Wire (below) is active.")]
        [SerializeField] private float energizedTailSeconds = 5f;
        [Tooltip("Seconds of cooldown after the energized tail before the blade can charge again.")]
        [SerializeField] private float energizeCooldownSeconds = 5f;

        [Tooltip("Element whose level-5 upgrade, LIVE WIRE, lets a return to the stance re-light the " +
                 "energized tail - a blade kept lit for as long as the pilot keeps coming back to the " +
                 "chop. Gated on the replicated unlock bit. None disables it.")]
        [SerializeField] private Element liveWireUpgradeElement = Element.Charge;

        [Header("Omni-crystal Surge - the sword ring and camera")]
        [Tooltip("Swords in the ring a surge raises round the hull, the pilot's own blade included " +
                 "(8 = the blade plus 7 copies, 45 degrees apart round the nose axis). Every copy is posed " +
                 "from the blade's live pose, so the triggers swing the whole ring.")]
        [SerializeField, Range(2, 16)] private int surgeRingSwordCount = 8;
        [Tooltip("Seconds the ring takes to fan out of the blade when the surge starts, and to fold back " +
                 "into it when the surge ends.")]
        [SerializeField, Min(0.01f)] private float surgeRingUnfurlSeconds = 0.35f;
        [Tooltip("Camera distance multiplier while the surge holds (the local pilot only): the camera " +
                 "pulls straight back by this and re-centres on the hull, so the whole ring is on screen.")]
        [SerializeField, Min(1f)] private float surgeCameraDistanceScale = 2f;
        [Tooltip("Seconds the camera takes to move into the surge framing, and back out of it.")]
        [SerializeField, Min(0.01f)] private float surgeCameraBlendSeconds = 0.6f;

        [Header("Blade Look — resting colour")]
        [Tooltip("Brightness multiplier always applied to the blade's colour. Bloom (threshold 0.2, clamp 0.5) " +
                 "does the rest - past ~3 the blade stops reading as a blade and becomes a white blob.")]
        [SerializeField] private float visibilityMultiplier = 1.2f;
        [Tooltip("The blade's RESTING colour — deliberately NOT the pilot's domain colour: the sword " +
                 "friendly-fires, so a team-coloured blade would read as safe to allies. White-hot says " +
                 "'this cuts anything'. (Authored here, not read off FresnelMaterial, so the blade's " +
                 "meaning can never drift with a shared material's tint.)")]
        [ColorUsage(true, true)] [SerializeField] private Color restingBladeColor = Color.white;

        [Header("Blade Look — energized (the danger colour)")]
        [Tooltip("Blade colour while ENERGIZED — the shared DANGER colour (SO_ColorSet.Danger, the same " +
                 "domain-independent red a danger prism's rim wears). An energized blade destroys hardened " +
                 "mass and still friendly-fires, so it speaks the platform's existing 'this hurts' language " +
                 "rather than inventing one. Multiplied by visibilityMultiplier at runtime.")]
        [ColorUsage(true, true)] [SerializeField] private Color energizedColor = new Color(1.4979111f, 0.0058463f, 0.0068495f, 1f);
        [Tooltip("Seconds to blend the blade + its tracer between the heat ramp and the danger colour " +
                 "on energize/de-energize. The tracer is tinted from the same live colour, so the streak " +
                 "changes with the sword through every state.")]
        [SerializeField] private float colorTransitionSeconds = 0.25f;

        [Header("Blade Look — crackle (ForcefieldCrackleController on the blade)")]
        [Tooltip("Arc intensity of the IGNITION burst the instant the blade energizes.")]
        [SerializeField] private float igniteCrackleIntensity = 2.5f;
        [Tooltip("Seconds the ignition arcs persist.")]
        [SerializeField] private float igniteCrackleSeconds = 0.9f;
        [Tooltip("How many arc sites the ignition seeds along the blade (spread hilt→tip).")]
        [SerializeField, Range(1, 8)] private int igniteCrackleSites = 5;
        [Tooltip("Seconds between the small anticipation arcs while CHARGING (holding the stance).")]
        [SerializeField] private float chargeCrackleInterval = 0.18f;
        [Tooltip("Arc intensity of the charging arcs at full charge (ramps up from a third of this).")]
        [SerializeField] private float chargeCrackleIntensity = 1.1f;
        [Tooltip("Arc intensity of the contact spark when the sword destroys a prism.")]
        [SerializeField] private float sparkIntensity = 1.6f;
        [Tooltip("Seconds a contact spark persists.")]
        [SerializeField] private float sparkSeconds = 0.45f;
        [Tooltip("World-unit ripple reach of a contact spark on the blade surface.")]
        [SerializeField] private float sparkWorldRadius = 14f;
        [Tooltip("Arc intensity of the DENIED spark — a non-energized blade touching a super-shielded " +
                 "prism (the bounce). Dim on purpose: it teaches, it doesn't reward.")]
        [SerializeField] private float deniedSparkIntensity = 0.7f;

        [Header("Blade Look — impact flashes")]
        [Tooltip("Flash strength (0..1) when the sword destroys a normal prism.")]
        [SerializeField, Range(0f, 1f)] private float hitFlashAmount = 0.35f;
        [Tooltip("Flash strength (0..1) when the sword pops a super-shielded prism or a surge starts.")]
        [SerializeField, Range(0f, 1f)] private float popFlashAmount = 1f;
        [Tooltip("Seconds a full-strength flash takes to decay back to the heat-ramp colour.")]
        [SerializeField] private float flashDecaySeconds = 0.35f;
        [Tooltip("Colour the blade flashes toward on impacts (bright so bloom sells the hit).")]
        [ColorUsage(true, true)] [SerializeField] private Color flashColor = new Color(2f, 2f, 2f, 1f);

        [Header("Camera Shake (local pilot only)")]
        [Tooltip("Shake intensity when the sword pops a super-shielded prism.")]
        [SerializeField] private float popShakeIntensity = 1.2f;
        [SerializeField] private float popShakeDuration = 0.25f;
        [Tooltip("Shake intensity when a surge raises the sword ring.")]
        [SerializeField] private float burstShakeMaxIntensity = 2.5f;
        [SerializeField] private float burstShakeDuration = 0.4f;

        [Header("Binding (a NON-energized blade inside super-shielded mass)")]
        [Tooltip("Rotation (pitch/yaw/roll) rate multiplier while the blade is inside a super-shielded prism it " +
                 "cannot cut — the armour DRAGS on the sword instead of throwing the ship back. " +
                 "1 = no drag, 0 = the vessel cannot rotate at all while bound. " +
                 "Replaces the old recoil (RhinoSkimmerDamagePrismEffectSO.recoilWhenDenied).")]
        [SerializeField, Range(0f, 1f)] private float bindTurnRateMultiplier = 0.3f;
        [Tooltip("How fast (multiplier units/sec) the turn rate eases DOWN into the bind. High, so " +
                 "the drag reads as the blade biting on contact.")]
        [SerializeField] private float bindEngageRate = 8f;
        [Tooltip("How fast (multiplier units/sec) the turn rate eases back UP once the blade is free. " +
                 "Lower than the engage rate so breaking free reads as the armour letting go, not as a snap.")]
        [SerializeField] private float bindReleaseRate = 4f;
        [Tooltip("Seconds between re-stamping the deflection jiggle on every bound prism, so it keeps " +
                 "shuddering for as long as the blade is in it (one jiggle lasts " +
                 "PrismSuperShieldJiggleConfig.duration and starts at its peak). Keep above that " +
                 "config's minSecondsBetweenStamps or the stamps are gated away.")]
        [SerializeField] private float bindJiggleIntervalSeconds = 0.2f;
        [Tooltip("Impact speed handed to each re-stamped jiggle (world units/sec) — sets how hard the " +
                 "bound prism shudders against PrismSuperShieldJiggleConfig.referenceImpactSpeed.")]
        [SerializeField] private float bindJiggleImpactSpeed = 90f;
        [Tooltip("Seconds between grind pulses of the BIND haptic while bound (local human pilot only). " +
                 "The entry into the armour is the punish thud; this is the texture after it. Keep at " +
                 "or above the grind clip's 80 ms length (Docs/HAPTICS.md, the cadence floor).")]
        [SerializeField] private float bindHapticIntervalSeconds = 0.1f;
        [Tooltip("Strength (0..1) of each grind pulse, before the player's haptics level.")]
        [SerializeField, Range(0f, 1f)] private float bindHapticStrength = 0.7f;

        // --- Public API ---

        public int   SurgeRingSwordCount      => Mathf.Clamp(surgeRingSwordCount, 2, 16);
        public float SurgeRingUnfurlSeconds   => Mathf.Max(0.01f, surgeRingUnfurlSeconds);
        public float SurgeCameraDistanceScale => Mathf.Max(1f, surgeCameraDistanceScale);
        public float SurgeCameraBlendSeconds  => Mathf.Max(0.01f, surgeCameraBlendSeconds);
        public float SpaceLengthEaseSpeed     => Mathf.Max(0.01f, spaceLengthEaseSpeed);
        public float CameraFollowsBladeLength => Mathf.Clamp01(cameraFollowsBladeLength);
        public float EnergizeHoldSeconds     => Mathf.Max(0.01f, energizeHoldSeconds);
        public float TouchEnergizeRateMultiplier => Mathf.Max(0.01f, touchEnergizeRateMultiplier);

        /// <summary>The stance hold that energizes this pilot's blade: <see cref="EnergizeHoldSeconds"/>,
        /// divided by <see cref="TouchEnergizeRateMultiplier"/> for a human flying on a phone.</summary>
        public float EnergizeHoldSecondsFor(bool touchPilot) =>
            touchPilot ? Mathf.Max(0.01f, EnergizeHoldSeconds / TouchEnergizeRateMultiplier) : EnergizeHoldSeconds;
        public float EnergizedTailSeconds    => Mathf.Max(0f, energizedTailSeconds);
        public float EnergizeCooldownSeconds => Mathf.Max(0f, energizeCooldownSeconds);
        public Element LiveWireUpgradeElement => liveWireUpgradeElement;

        /// <summary>CHARGE level-5, Live Wire: is it active on this vessel? Through the replicated unlock
        /// bit (IsUpgradeActive), never a raw level read.</summary>
        public bool LiveWireActive(IVesselStatus status)
        {
            if (liveWireUpgradeElement == Element.None) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(liveWireUpgradeElement);
        }

        public float VisibilityMultiplier => Mathf.Max(0f, visibilityMultiplier);
        public Color RestingBladeColor    => restingBladeColor;

        public Color EnergizedColor         => energizedColor;
        public float ColorTransitionSeconds => Mathf.Max(0.0001f, colorTransitionSeconds);

        public float IgniteCrackleIntensity => Mathf.Max(0f, igniteCrackleIntensity);
        public float IgniteCrackleSeconds   => Mathf.Max(0.05f, igniteCrackleSeconds);
        public int   IgniteCrackleSites     => Mathf.Clamp(igniteCrackleSites, 1, 8);
        public float ChargeCrackleInterval  => Mathf.Max(0.05f, chargeCrackleInterval);
        public float ChargeCrackleIntensity => Mathf.Max(0f, chargeCrackleIntensity);
        public float SparkIntensity         => Mathf.Max(0f, sparkIntensity);
        public float SparkSeconds           => Mathf.Max(0.05f, sparkSeconds);
        public float SparkWorldRadius       => Mathf.Max(0.5f, sparkWorldRadius);
        public float DeniedSparkIntensity   => Mathf.Max(0f, deniedSparkIntensity);

        public float HitFlashAmount    => hitFlashAmount;
        public float PopFlashAmount    => popFlashAmount;
        public float FlashDecaySeconds => Mathf.Max(0.0001f, flashDecaySeconds);
        public Color FlashColor        => flashColor;

        public float PopShakeIntensity      => Mathf.Max(0f, popShakeIntensity);
        public float PopShakeDuration       => Mathf.Max(0f, popShakeDuration);
        public float BurstShakeMaxIntensity => Mathf.Max(0f, burstShakeMaxIntensity);
        public float BurstShakeDuration     => Mathf.Max(0f, burstShakeDuration);

        public float BindTurnRateMultiplier    => Mathf.Clamp01(bindTurnRateMultiplier);
        public float BindEngageRate            => Mathf.Max(0.01f, bindEngageRate);
        public float BindReleaseRate           => Mathf.Max(0.01f, bindReleaseRate);
        public float BindJiggleIntervalSeconds => Mathf.Max(0.02f, bindJiggleIntervalSeconds);
        public float BindJiggleImpactSpeed     => Mathf.Max(0f, bindJiggleImpactSpeed);
        public float BindHapticIntervalSeconds => Mathf.Max(0.06f, bindHapticIntervalSeconds);
        public float BindHapticStrength        => Mathf.Clamp01(bindHapticStrength);

        // ApplyMaxSizeDebuff is RETIRED (Sep 2026, Docs/ELEMENTAL_ECONOMY.md §9). The Sparrow's
        // guns called it to shrink a Rhino's blade for 3s, which is taking a pilot's CONTROLS -
        // the one class of hit they cannot answer with flying. It also mutated THIS asset, and
        // ONE asset drives every Rhino, so shooting one pilot shrank every Rhino's sword in the
        // match. What a weapon may take from another pilot is their elemental PETALS.
        // The `_maxScaleMultiplier` runtime state went with it, and on 2026-10-26 the blade's length
        // became Space's alone (no energy length, no surge burst). Do not reintroduce a debuff, or a
        // second source of size, here.
    }
}
