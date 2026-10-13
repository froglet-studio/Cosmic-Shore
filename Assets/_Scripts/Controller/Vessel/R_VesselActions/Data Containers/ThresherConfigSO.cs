using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every dial of the Thresher: the chain physics (embedded <see cref="ThresherDials"/>, in
    /// sandbox units and scaled by its own <c>gameCruise / sandboxCruise</c>), the four element
    /// scalings, the smash explosion, the chain cut, the impact feel, and the look. One asset per
    /// hull; read by <see cref="ThresherExecutor"/>. See <c>THRESHER.md</c>.
    ///
    /// Element map (re-cut 2026-10-13, round 3): CHARGE = petals the ball and chain take from an
    /// enemy vessel per hit (L5 "Magnet": the free ball is drawn to enemy vessels), MASS = the size of
    /// the prisms the ball lays in Mass Mode (L5 "Armoured Trail": they are laid shielded), SPACE =
    /// the largest explosion the chain's banked energy can buy (L5 "Breaker": a red ball's direct hit
    /// destroys super-shielded prisms), TIME = how fast the planted spin winds up to launch speed (L5
    /// "Perfect Release": a release timed inside the window leaves with a boost). Every scaling is an
    /// <see cref="ElementalFloat"/> read through <c>EvaluateReplicated</c> at the point of use.
    /// </summary>
    [CreateAssetMenu(fileName = "ThresherConfig", menuName = "ScriptableObjects/Vessel Actions/Thresher Config")]
    public sealed class ThresherConfigSO : ScriptableObject
    {
        [Tooltip("The chain physics, authored in the 2D sandbox's units. See ThresherDials.")]
        [SerializeField] ThresherDials dials = new ThresherDials();

        [Header("Elements")]
        [Tooltip("CHARGE: petals the ball or the chain takes from an ENEMY vessel per hit, on each element " +
                 "(a scored Strike that STEALS them - the PvP-is-petals-only rule). 1 at rest -> 3 at full Charge.")]
        [SerializeField] ElementalFloat vesselHitPetals = ElementalFloat.Multiplier(1f, 3f, Element.Charge, 0.5f);
        [Tooltip("MASS: size multiplier (every axis) on the prisms the ball lays in Mass Mode (massTrailPrismScale).")]
        [SerializeField] ElementalFloat massTrailScale = ElementalFloat.Multiplier(1f, 2f, Element.Mass, 0.5f);
        [Tooltip("SPACE: the largest explosion-diameter multiplier the chain's banked energy can buy - the cap " +
                 "of 1 + energyDiameterPerUnit x energy. 4 at rest -> 8 at full Space. The Space card's gauge " +
                 "shows how much of it is banked.")]
        [SerializeField] ElementalFloat energyMaxDiameterScale = ElementalFloat.Multiplier(4f, 8f, Element.Space, 2f);
        [Tooltip("TIME: multiplier on the planted orbit's spin-up rate AND its cap (lockSpin, lockMax) - how fast " +
                 "the spin winds up to launch speed.")]
        [SerializeField] ElementalFloat spinMultiplier = ElementalFloat.Multiplier(1f, 1.75f, Element.Time, 0.5f);

        [Header("Smash explosion (ball at or above smash speed)")]
        [Tooltip("Explosion prefab(s) spawned where a smashing ball (or an energised hit) lands. Destroys every " +
                 "domain's prisms (AffectSelfOverride true).")]
        [SerializeField] AOEExplosion[] smashExplosions;
        [Tooltip("Explosion DIAMETER with nothing banked (world units); x the energy multiplier.")]
        [SerializeField, Min(0f)] float explosionDiameter = 45f;
        [Tooltip("At most one explosion per this many seconds, so a hot ball ploughing a row makes a " +
                 "string of blasts rather than one per prism.")]
        [SerializeField, Min(0f)] float explosionMinInterval = 0.08f;

        [Header("Chain energy (the chain's kills charge the ball's next explosion)")]
        [Tooltip("Energy each prism the CHAIN slices stores in the ball. Stored energy is released by the ball's " +
                 "next hit on a RIVAL prism as an explosion, even below smash speed (outside Mass Mode).")]
        [SerializeField, Min(0f)] float energyPerSlice = 1f;
        [Tooltip("Energy each prism the ball STEALS in Mass Mode adds - Mass Mode never releases energy, it banks it.")]
        [SerializeField, Min(0f)] float energyPerSteal = 0.5f;
        [Tooltip("The most energy the ball can bank. Enough to reach the full-Space explosion cap (70 x 0.1 + 1 = 8).")]
        [SerializeField, Min(0f)] float energyMax = 70f;
        [Tooltip("Each unit of banked energy adds this much to the explosion's diameter multiplier (1 + this x energy), " +
                 "up to the Space-scaled energyMaxDiameterScale.")]
        [SerializeField, Min(0f)] float energyDiameterPerUnit = 0.1f;
        [Tooltip("How far the ball's rim leans toward the CTA lime at a FULL bank (0 = no cue, 1 = pure lime).")]
        [SerializeField, Range(0f, 1f)] float energyRimTint = 0.6f;

        [Header("Mass Mode (A button: toggle)")]
        [Tooltip("Size of each prism the ball lays behind it in Mass Mode (x across, y thick, z along its path).")]
        [SerializeField] Vector3 massTrailPrismScale = new Vector3(8f, 0.75f, 5.5f);
        [Tooltip("The ball lays one prism per this much of its own travel in Mass Mode (world units).")]
        [SerializeField, Min(0.5f)] float massTrailSpacing = 6f;
        [Tooltip("Most prisms the ball lays in one frame, so a hitch or a white-hot ball never dumps a burst; " +
                 "the excess distance is dropped, not owed.")]
        [SerializeField, Min(1)] int massTrailMaxPerFrame = 3;
        [Tooltip("Seconds a freshly laid ball-trail prism keeps its collider off so the ball and chain can clear it.")]
        [SerializeField, Min(0f)] float massTrailWaitTime = 0.3f;

        [Header("Windmill (omni crystal)")]
        [Tooltip("Seconds the ball windmills round the ship after an omni crystal (speed: ThresherDials.windmillSpeed). " +
                 "A second crystal mid-windmill tops the clock back up.")]
        [SerializeField, Min(0f)] float windmillSeconds = 12f;

        [Header("Enemy vessels (Charge)")]
        [Tooltip("The ball's swept sphere and the chain's cut width reach a vessel this much further (world units, " +
                 "roughly a hull's radius).")]
        [SerializeField, Min(0f)] float vesselHitRadius = 4f;
        [Tooltip("Seconds before the ball or chain can score on the same enemy vessel again.")]
        [SerializeField, Min(0f)] float vesselHitCooldownSeconds = 1f;
        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager scores combat hits on. Fail-loud.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;
        [Tooltip("Charge L5 'Magnet': a free ball is drawn toward the nearest enemy vessel within this range (world units).")]
        [SerializeField, Min(0f)] float magnetRange = 150f;
        [Tooltip("Charge L5 'Magnet': how hard the ball is drawn (u/s^2).")]
        [SerializeField, Min(0f)] float magnetAcceleration = 220f;

        [Header("Perfect release (Time)")]
        [Tooltip("A release that leaves within this many seconds of LT coming up - i.e. timed just before the launch " +
                 "point - is PERFECT. The launch line turns lime while a release now would be perfect.")]
        [SerializeField, Min(0f)] float perfectWindowSeconds = 0.25f;
        [Tooltip("Time L5 'Perfect Release': a perfect launch leaves this many times faster than the orbit.")]
        [SerializeField, Min(1f)] float perfectBoost = 1.6f;

        [Header("Chain cut")]
        [Tooltip("The chain's cutting half-width (world units). It cuts RIVAL prisms it touches while taut; " +
                 "never its own domain's.")]
        [SerializeField, Min(0f)] float chainCutRadius = 0.6f;
        [Tooltip("Debris velocity = the chain point's own velocity x this.")]
        [SerializeField, Min(0f)] float chainDebrisRestitution = 0.5f;

        [Header("Impact - hit-stop (one per whip, on its first smash)")]
        [Tooltip("Hit-stop for a ball that only just reached smash speed (seconds).")]
        [SerializeField, Min(0f)] float hitStopMinSeconds = 0.03f;
        [Tooltip("Hit-stop for a white-hot ball (seconds).")]
        [SerializeField, Min(0f)] float hitStopMaxSeconds = 0.09f;
        [Tooltip("Time scale during a SOLO hit-stop. Multiplayer never touches Time.timeScale; it freezes this hull and its ball instead.")]
        [SerializeField, Range(0f, 1f)] float hitStopTimeScale = 0.05f;

        [Header("Impact - shake, haptics, debris")]
        [Tooltip("Camera shake for a smash at smash speed.")]
        [SerializeField, Min(0f)] float shakeMin = 0.35f;
        [Tooltip("Camera shake for a white-hot smash.")]
        [SerializeField, Min(0f)] float shakeMax = 1.8f;
        [Tooltip("Camera shake duration (seconds).")]
        [SerializeField, Min(0f)] float shakeSeconds = 0.22f;
        [Tooltip("Shake for a crush (a below-smash kill), as a fraction of shakeMin.")]
        [SerializeField, Range(0f, 1f)] float crushShakeFraction = 0.35f;
        [Tooltip("Haptic strength floor (0-1) for a smash; rises to 1 when white-hot.")]
        [SerializeField, Range(0f, 1f)] float hapticFloor01 = 0.45f;
        [Tooltip("Debris velocity = ball velocity x this (the fleet's 'proportional' restitution).")]
        [SerializeField, Min(0f)] float debrisRestitution = 1f / 3f;
        [Tooltip("Debris speed ceiling (u/s). Must be > 0 or the prefab's ~33 u/s cap flattens a fast ball's debris.")]
        [SerializeField, Min(0f)] float debrisSpeedLimit = 200f;

        [Header("Combo")]
        [Tooltip("A whip ends (and the combo counter resets) once the ball has stayed below smash speed this long (seconds).")]
        [SerializeField, Min(0f)] float comboGraceSeconds = 0.4f;

        [Header("READY cue")]
        [Tooltip("READY shows while the predicted reel-in crack is at least this multiple of smash speed.")]
        [SerializeField, Min(0f)] float readyMargin = 1.05f;
        [Tooltip("How fast the ball and chain ease to a new state's colour (1/s, exponential): domain -> red at " +
                 "smash speed, slack -> taut, -> READY lime. 15 = 95% in 0.2 s - a quick, visible change, never a " +
                 "cut or a flicker. The HUD gauges ease at their own matching rate.")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        [Header("Camera - keep the ball in frame (local pilot only)")]
        [Tooltip("Pull the chase camera back whenever the ball would leave the frame, and ease it home after.")]
        [SerializeField] bool cameraFraming = true;
        [Tooltip("How fast the camera pulls BACK to keep the ball in frame (1/s, exponential). Fast: the " +
                 "pilot should never lose sight of the ball.")]
        [SerializeField, Min(0f)] float cameraZoomOutRate = 12f;
        [Tooltip("How fast the camera eases back IN once the ball no longer needs the room (1/s, " +
                 "exponential). Slow, so a reel-in does not yank the view.")]
        [SerializeField, Min(0f)] float cameraZoomInRate = 0.7f;
        [Tooltip("Framing margin: 1 = the rendered ball just touches the frame edge; 1.15 keeps it " +
                 "about 13% inside.")]
        [SerializeField, Min(1f)] float cameraFramingMargin = 1.15f;
        [Tooltip("The ball is kept at least this far in FRONT of the camera (world units), so a ball " +
                 "trailing behind the hull pulls the camera back past it rather than through it.")]
        [SerializeField, Min(0f)] float cameraMinAhead = 6f;
        [Tooltip("The farthest the chase camera is ever pulled back (world units). The prefab's own " +
                 "distance is always the near limit.")]
        [SerializeField, Min(0f)] float cameraMaxDistance = 450f;
        [Tooltip("The fastest the chase camera's frame may turn after the hull (degrees/s). The fleet's camera " +
                 "is hard-attached, so a hull snap (hooking onto a planted orbit swings it up to 91 deg in one " +
                 "frame) whipped the camera round with it. Above every stick turn rate, so ordinary flying is " +
                 "untouched; only snaps are spread out. 0 = hard-attached as the rest of the fleet.")]
        [SerializeField, Min(0f)] float cameraMaxFollowTurnRate = 240f;

        [Header("Camera - spectate + AIM a planted spin (local pilot only)")]
        [Tooltip("Detach the camera from the spinning hull once the ship circles the pivot at this rate or " +
                 "faster (radians/s; 0.9 is about 52 deg/s). Riding a spin in a chase camera made spectators " +
                 "sick at the old 1.8 (about 100 deg/s), so it now detaches at half that.")]
        [SerializeField, Min(0f)] float spectateSpinRate = 0.9f;
        [Tooltip("The spin must hold at or over spectateSpinRate this long (seconds) before the camera " +
                 "detaches, so a tap of LT does not bob the camera.")]
        [SerializeField, Min(0f)] float spectateEngageSeconds = 0.1f;
        [Tooltip("Re-attach once the spin falls below this fraction of spectateSpinRate (or the plant " +
                 "is released). Below 1 so a spin hovering at the threshold does not flap.")]
        [SerializeField, Range(0f, 1f)] float spectateReleaseFraction = 0.75f;
        [Tooltip("The launch camera follows behind the launch point ON the trajectory, raised this far above it " +
                 "(degrees). 0 (shipped) = no height offset: the trajectory is the screen centre, drawn as a crosshair.")]
        [SerializeField, Range(0f, 85f)] float aimElevationDegrees = 0f;
        [Tooltip("How far behind the launch point the launch camera follows (world units). 0 = the hull's own " +
                 "chase distance (the camera settings' follow offset).")]
        [SerializeField, Min(0f)] float aimCameraDistance = 0f;
        [Tooltip("Seconds the camera EASES from wherever it is into the launch view once a spin is detected " +
                 "(SmoothStep, on the executor's own clock, from a still start pose - so the spinning chase " +
                 "pose never leaks into the move).")]
        [SerializeField, Min(0.01f)] float spectateEnterSeconds = 0.9f;
        [Tooltip("Seconds the camera EASES from the launch view back to the chase pose behind the hull after " +
                 "the launch (or when the spin falls away), tracking the hull's live pose.")]
        [SerializeField, Min(0.01f)] float spectateExitSeconds = 0.8f;
        [Tooltip("The camera's own hand-over blend at either end of those eases (seconds). Short, because the " +
                 "two poses it blends between already coincide.")]
        [SerializeField, Min(0f)] float spectateBlendSeconds = 0.05f;
        [Tooltip("While spectating, full stick X swings the AIM (the path a release flies off along) at this " +
                 "rate (degrees/s). The launch point moves round the circle with it and the camera follows, so " +
                 "aiming IS turning the camera; stick Y tilts the whole orbit with the aim.")]
        [SerializeField, Min(0f)] float aimYawRate = 180f;
        [Tooltip("While spectating, full stick Y pitches the aim at this rate (degrees/s), the hull's own pitch sign.")]
        [SerializeField, Min(0f)] float aimPitchRate = 120f;

        [Header("Launch sight - the Time-petal zoom window (local pilot only)")]
        [Tooltip("The zoom window's vertical field of view with LT held lightly (degrees) - the wide end. A " +
                 "constant: the window is never narrowed by speed or by the gameplay camera.")]
        [SerializeField, Range(5f, 120f)] float sightWideFieldOfView = 50f;
        [Tooltip("The window's field of view with LT pulled all the way (degrees) - the zoomed end.")]
        [SerializeField, Range(1f, 90f)] float sightZoomFieldOfView = 6f;
        [Tooltip("LT depth below which the window does not zoom. The Plant engages at 5% depth, so this is a " +
                 "light hold that plants without zooming.")]
        [SerializeField, Range(0f, 0.9f)] float sightZoomDeadzone = 0.25f;
        [Tooltip("How fast the zoom chases the trigger (1/s). Its only job is ramping a BINARY trigger " +
                 "(keyboard/mouse) - the Serpent scope's zoomResponse.")]
        [SerializeField, Min(0.1f)] float sightZoomResponse = 12f;

        [Header("Ball shading (ThresherBallFresnelShader: dull body, bright fresnel rim)")]
        [Tooltip("Body brightness (max channel) of a slow ball. Under the bloom threshold (0.2).")]
        [SerializeField, Range(0f, 1f)] float ballBodyCool = 0.16f;
        [Tooltip("Body brightness (max channel) just under smash speed.")]
        [SerializeField, Range(0f, 1f)] float ballBodyNearSmash = 0.4f;
        [Tooltip("Body brightness (max channel) at smash speed: ON the 0.5 bloom clamp, so the whole ball blooms.")]
        [SerializeField, Range(0f, 1f)] float ballBodyHot = 0.5f;
        [Tooltip("Rim brightness (max channel) of a slow ball.")]
        [SerializeField, Range(0f, 1f)] float ballRimCool = 0.6f;
        [Tooltip("Rim brightness (max channel) at and above smash speed. At most 1: tonemapping is None, so " +
                 "a channel above 1 clips and shifts the hue.")]
        [SerializeField, Range(0f, 1f)] float ballRimHot = 1f;
        [Tooltip("How far a white-hot ball's rim whitens (0 = pure hue, 1 = white).")]
        [SerializeField, Range(0f, 1f)] float ballHotRimWhiten = 0.35f;
        [Tooltip("The fastest the drawn ball ROLLS (degrees/s). A white-hot ball truly rolls ~7000 deg/s, " +
                 "which strobes the studs backwards at 60 fps.")]
        [SerializeField, Min(0f)] float ballMaxVisualSpin = 540f;

        [Header("Chain shading (ThresherChainFresnelShader: a fresnel tube on the chain's ribbon)")]
        [Tooltip("Rim brightness (max channel, the ball's hue) while the chain is SLACK.")]
        [SerializeField, Range(0f, 1f)] float chainRimSlack = 0.35f;
        [Tooltip("Rim brightness while the chain is TAUT - the state in which it cuts.")]
        [SerializeField, Range(0f, 1f)] float chainRimTaut = 0.75f;
        [Tooltip("Rim brightness of the READY lime.")]
        [SerializeField, Range(0f, 1f)] float chainRimReady = 0.95f;

        [Header("Look")]
        [Tooltip("Rendered ball radius as a multiple of the physics radius.")]
        [SerializeField, Min(0.1f)] float ballVisualScale = 1.25f;
        [Tooltip("Chain links (gameplay: the chain cuts along these segments). The drawn chain is a smooth " +
                 "curve through them (chainRenderSubdivisions), at the width that cuts, 2 x chainCutRadius.")]
        [SerializeField, Range(2, 64)] int chainLinks = 24;
        [Tooltip("Drawn segments per chain link: the chain is drawn as a Catmull-Rom curve through the links, " +
                 "so a slack chain bends smoothly instead of showing a kink at every link.")]
        [SerializeField, Range(1, 16)] int chainRenderSubdivisions = 6;
        [Tooltip("The chain tube's BODY colour (iron). Keep it under the 0.2 bloom threshold: the rim carries the light.")]
        [SerializeField] Color chainColor = new Color(0.07f, 0.065f, 0.065f);
        [Tooltip("Fallback ball colour when the palette cannot be read (e.g. no theme loaded).")]
        [SerializeField] Color fallbackDomainColor = new Color(0.3f, 0.9f, 0.85f);
        [Tooltip("Fallback hot colour when the palette has no danger colour.")]
        [SerializeField] Color fallbackDangerColor = new Color(1f, 0.05f, 0.05f);
        [Tooltip("Fallback READY colour when the palette has no CTA colour.")]
        [SerializeField] Color fallbackReadyColor = new Color(0.55f, 0.95f, 0.15f);
        [Tooltip("Skid-trail lifetime while planting (seconds).")]
        [SerializeField, Min(0f)] float skidTrailSeconds = 0.6f;

        public ThresherDials Dials => dials;
        public ElementalFloat VesselHitPetals => vesselHitPetals;
        public ElementalFloat MassTrailScale => massTrailScale;
        public ElementalFloat EnergyMaxDiameterScale => energyMaxDiameterScale;
        public ElementalFloat SpinMultiplier => spinMultiplier;
        public AOEExplosion[] SmashExplosions => smashExplosions;
        public float ExplosionDiameter => explosionDiameter;
        public float ExplosionMinInterval => explosionMinInterval;
        public float EnergyPerSlice => energyPerSlice;
        public float EnergyPerSteal => energyPerSteal;
        public float EnergyMax => energyMax;
        public float EnergyDiameterPerUnit => energyDiameterPerUnit;
        public float EnergyRimTint => energyRimTint;
        public Vector3 MassTrailPrismScale => massTrailPrismScale;
        public float MassTrailSpacing => Mathf.Max(0.5f, massTrailSpacing);
        public int MassTrailMaxPerFrame => Mathf.Max(1, massTrailMaxPerFrame);
        public float MassTrailWaitTime => massTrailWaitTime;
        public float WindmillSeconds => windmillSeconds;
        public float VesselHitRadius => vesselHitRadius;
        public float VesselHitCooldownSeconds => vesselHitCooldownSeconds;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public float MagnetRange => magnetRange;
        public float MagnetAcceleration => magnetAcceleration;
        public float PerfectWindowSeconds => perfectWindowSeconds;
        public float PerfectBoost => perfectBoost;
        public float ChainCutRadius => chainCutRadius;
        public float ChainDebrisRestitution => chainDebrisRestitution;
        public float HitStopMinSeconds => hitStopMinSeconds;
        public float HitStopMaxSeconds => Mathf.Max(hitStopMinSeconds, hitStopMaxSeconds);
        public float HitStopTimeScale => hitStopTimeScale;
        public float ShakeMin => shakeMin;
        public float ShakeMax => Mathf.Max(shakeMin, shakeMax);
        public float ShakeSeconds => shakeSeconds;
        public float CrushShakeFraction => crushShakeFraction;
        public float HapticFloor01 => hapticFloor01;
        public float DebrisRestitution => debrisRestitution;
        public float DebrisSpeedLimit => debrisSpeedLimit;
        public float ComboGraceSeconds => comboGraceSeconds;
        public float ReadyMargin => readyMargin;
        public float ColorBlendRate => colorBlendRate;
        public bool CameraFraming => cameraFraming;
        public float CameraZoomOutRate => cameraZoomOutRate;
        public float CameraZoomInRate => cameraZoomInRate;
        public float CameraFramingMargin => cameraFramingMargin;
        public float CameraMinAhead => cameraMinAhead;
        public float CameraMaxDistance => cameraMaxDistance;
        public float CameraMaxFollowTurnRate => cameraMaxFollowTurnRate;
        public float SpectateSpinRate => spectateSpinRate;
        public float SpectateEngageSeconds => spectateEngageSeconds;
        public float SpectateReleaseFraction => spectateReleaseFraction;
        public float AimElevationDegrees => aimElevationDegrees;
        public float AimCameraDistance => aimCameraDistance;
        public float SightWideFieldOfView => sightWideFieldOfView;
        public float SightZoomFieldOfView => sightZoomFieldOfView;
        public float SightZoomDeadzone => sightZoomDeadzone;
        public float SightZoomResponse => sightZoomResponse;
        public float SpectateBlendSeconds => spectateBlendSeconds;
        public float SpectateEnterSeconds => spectateEnterSeconds;
        public float SpectateExitSeconds => spectateExitSeconds;
        public float AimYawRate => aimYawRate;
        public float AimPitchRate => aimPitchRate;
        public float BallBodyCool => ballBodyCool;
        public float BallBodyNearSmash => ballBodyNearSmash;
        public float BallBodyHot => ballBodyHot;
        public float BallRimCool => ballRimCool;
        public float BallRimHot => ballRimHot;
        public float BallHotRimWhiten => ballHotRimWhiten;
        public float BallMaxVisualSpin => ballMaxVisualSpin;
        public float ChainRimSlack => chainRimSlack;
        public float ChainRimTaut => chainRimTaut;
        public float ChainRimReady => chainRimReady;
        public float BallVisualScale => ballVisualScale;
        public int ChainLinks => chainLinks;
        public int ChainRenderSubdivisions => chainRenderSubdivisions;
        public Color ChainColor => chainColor;
        public Color FallbackDomainColor => fallbackDomainColor;
        public Color FallbackDangerColor => fallbackDangerColor;
        public Color FallbackReadyColor => fallbackReadyColor;
        public float SkidTrailSeconds => skidTrailSeconds;
    }
}
