using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every dial of the Thresher: the chain physics (embedded <see cref="ThresherDials"/>, in
    /// sandbox units and scaled by its own <c>gameCruise / sandboxCruise</c>), the four element
    /// scalings, the smash explosion, the chain cut, the impact feel, and the look. One asset per
    /// hull; read by <see cref="ThresherExecutor"/>. See <c>THRESHER.md</c>.
    ///
    /// Element map (approved 2026-10-08): CHARGE = the ball's threat (explosion size; L5 "Lit"),
    /// MASS = the ball's weight and size (L5 "Wrecker"), SPACE = the chain's reach (L5 "Reaper
    /// Chain"), TIME = the plant's spin (L5 "Slingshot"). Every scaling is an
    /// <see cref="ElementalFloat"/> read through <c>EvaluateLive</c> at the point of use.
    /// </summary>
    [CreateAssetMenu(fileName = "ThresherConfig", menuName = "ScriptableObjects/Vessel Actions/Thresher Config")]
    public sealed class ThresherConfigSO : ScriptableObject
    {
        [Tooltip("The chain physics, authored in the 2D sandbox's units. See ThresherDials.")]
        [SerializeField] ThresherDials dials = new ThresherDials();

        [Header("Elements")]
        [Tooltip("CHARGE: multiplier on the smash explosion's diameter.")]
        [SerializeField] ElementalFloat explosionSizeMultiplier = ElementalFloat.Multiplier(1f, 2f, Element.Charge, 0.5f);
        [Tooltip("MASS: multiplier on the ball's mass (heavier tug, the same plough). The ball's radius " +
                 "grows with the CUBE ROOT of it, because Mass is volume.")]
        [SerializeField] ElementalFloat ballMassMultiplier = ElementalFloat.Multiplier(1f, 1.75f, Element.Mass, 0.5f);
        [Tooltip("SPACE: multiplier on the let-out chain length (maxLen). The reeled-in length is fixed.")]
        [SerializeField] ElementalFloat chainReachMultiplier = ElementalFloat.Multiplier(1f, 1.5f, Element.Space, 0.6f);
        [Tooltip("TIME: multiplier on the planted orbit's spin-up rate AND its cap (lockSpin, lockMax).")]
        [SerializeField] ElementalFloat spinMultiplier = ElementalFloat.Multiplier(1f, 1.75f, Element.Time, 0.5f);

        [Header("Smash explosion (ball at or above smash speed)")]
        [Tooltip("Explosion prefab(s) spawned where a smashing ball hits. Destroys every domain's prisms " +
                 "(AffectSelfOverride true) until the Charge L5 'Lit' is active, when it spares the pilot's own " +
                 "domain (AffectSelfOverride false) - the fleet's domain-sparing-in-the-explosion-layer rule.")]
        [SerializeField] AOEExplosion[] smashExplosions;
        [Tooltip("Explosion DIAMETER at rest Charge (world units); x explosionSizeMultiplier.")]
        [SerializeField, Min(0f)] float explosionDiameter = 45f;
        [Tooltip("At most one explosion per this many seconds, so a hot ball ploughing a row makes a " +
                 "string of blasts rather than one per prism.")]
        [SerializeField, Min(0f)] float explosionMinInterval = 0.08f;

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

        [Header("Camera - spectate a fast planted spin (local pilot only)")]
        [Tooltip("Detach the camera and WATCH the planted orbit from a still vantage once the ship " +
                 "circles the pivot at this rate or faster (radians/s; 1.8 is about 100 deg/s). Riding " +
                 "a faster spin in a chase camera reads as dizzying.")]
        [SerializeField, Min(0f)] float spectateSpinRate = 1.8f;
        [Tooltip("The spin must hold at or over spectateSpinRate this long (seconds) before the camera " +
                 "detaches, so a tap of LT does not bob the camera.")]
        [SerializeField, Min(0f)] float spectateEngageSeconds = 0.2f;
        [Tooltip("Re-attach once the spin falls below this fraction of spectateSpinRate (or the plant " +
                 "is released). Below 1 so a spin hovering at the threshold does not flap.")]
        [SerializeField, Range(0f, 1f)] float spectateReleaseFraction = 0.75f;
        [Tooltip("The vantage's tilt off straight-down-the-orbit-axis, leaning toward the side the ship " +
                 "was on when it detached (degrees). 0 = a top-down plan view.")]
        [SerializeField, Range(0f, 80f)] float spectateTiltDegrees = 25f;
        [Tooltip("Seconds to ease into the vantage and back out to the chase camera.")]
        [SerializeField, Min(0f)] float spectateBlendSeconds = 0.6f;
        [Tooltip("How fast the vantage leans after the orbit's plane when the pilot tilts a planted orbit " +
                 "(1/s, exponential). Slow, so the orbit turns in front of a still camera.")]
        [SerializeField, Min(0f)] float spectatePlaneFollowRate = 1.5f;

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
        [Tooltip("Chain links (gameplay: the chain cuts; and drawn - at the width that cuts, 2 x chainCutRadius).")]
        [SerializeField, Range(2, 32)] int chainLinks = 12;
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
        public ElementalFloat ExplosionSizeMultiplier => explosionSizeMultiplier;
        public ElementalFloat BallMassMultiplier => ballMassMultiplier;
        public ElementalFloat ChainReachMultiplier => chainReachMultiplier;
        public ElementalFloat SpinMultiplier => spinMultiplier;
        public AOEExplosion[] SmashExplosions => smashExplosions;
        public float ExplosionDiameter => explosionDiameter;
        public float ExplosionMinInterval => explosionMinInterval;
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
        public float SpectateTiltDegrees => spectateTiltDegrees;
        public float SpectateBlendSeconds => spectateBlendSeconds;
        public float SpectatePlaneFollowRate => spectatePlaneFollowRate;
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
        public Color ChainColor => chainColor;
        public Color FallbackDomainColor => fallbackDomainColor;
        public Color FallbackDangerColor => fallbackDangerColor;
        public Color FallbackReadyColor => fallbackReadyColor;
        public float SkidTrailSeconds => skidTrailSeconds;
    }
}
