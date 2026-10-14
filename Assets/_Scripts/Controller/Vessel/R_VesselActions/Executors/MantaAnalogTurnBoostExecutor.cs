using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-frame executor for Manta analog trigger controls.
    /// Reads LeftTriggerAnalog and RightTriggerAnalog each frame and computes:
    ///   - Proportional yaw from the net trigger difference (RT - LT)
    ///   - Proportional boost from the trigger overlap min(LT, RT)
    ///
    /// Example: LT=1.0, RT=0.5 → turn left at 0.5 intensity, boost at 0.5 intensity.
    /// Both triggers fully pulled → full boost, no turn (current behavior preserved).
    ///
    /// Only active when InputDevice is Gamepad or Keyboard. On touch, the event-driven
    /// Yawstery + Boost actions handle control via _gamepadActionOverrides.
    ///
    /// <para><b>An autopilot has no triggers, so it needs a DRIVE.</b> AIPilot writes the stick
    /// and the throttle and nothing else; before this an AI Manta could never Soar, which in a
    /// race cut around the Manta's full-boost radius (Redline) made every bot a 180 u/s
    /// obstacle. The drive mirrors the human trade rather than inventing a policy: a pilot
    /// holds both triggers flat when the line is straight and eases off to turn, so the
    /// autopilot's boost is "how straight is the stick" - full inside
    /// <see cref="aiBoostStickBand"/>, fading to nothing at its edge. Gated on the pilot being
    /// an AUTOPILOT (<c>AIPilot.AutoPilotEnabled</c>), not on the player being an AI, so the
    /// lava-lamp Manta and an AI-piloted companion fly the same kit a human does.</para>
    ///
    /// <para><b>Past <see cref="aiYastriStickBand"/> the drive also PIVOTS.</b> A yaw stick hauled
    /// further than that band is a bot asking for more turn than the stick has, which is the
    /// moment a human releases one trigger: the trigger on the turn's side is raised above the
    /// boost hold by the excess deflection, so a full yaw deflection is one trigger flat and the
    /// other released - the 82 u Yastri pivot (REDLINE.md §1). The boost hold (the overlap) is
    /// exactly the band-only drive's at every stick, and below the Yastri band the two triggers
    /// stay equal, so straights and ordinary corners fly as they did before the pivot existed.
    /// The pure shape is <see cref="AutopilotTriggers"/>.</para>
    /// </summary>
    public sealed class MantaAnalogTurnBoostExecutor : ShipActionExecutorBase
    {
        [Header("Yaw Response")]
        [Tooltip("Max yaw speed (deg/sec) at full net trigger pull.")]
        [SerializeField] private float maxYawDegPerSec = 60f;

        [Header("Boost")]
        [Tooltip("If > 0, overrides VesselStatus.BoostMultiplier as the base value.")]
        [SerializeField] private float boostMultiplierOverride;

        [Header("AI")]
        [Tooltip("An autopilot has no triggers: it holds both flat (full Soar) while its stick " +
                 "deflection is under this band, and eases the boost off linearly as the stick " +
                 "goes over it - the same boost-for-turn trade a human makes on the triggers. " +
                 "0 disables the drive (an autopilot then never Soars).")]
        [SerializeField, Range(0f, 1f)] private float aiBoostStickBand = 0.35f;
        [Tooltip("Past this YAW stick deflection an autopilot also Yastri-pivots: the trigger on " +
                 "the turn's side rises above the boost hold by the excess deflection, reaching " +
                 "one trigger flat and the other released at a full yaw stick. The boost (the " +
                 "trigger overlap) is unchanged, and below this band both triggers stay equal. " +
                 "Keep it above Ai Boost Stick Band so a bot never pivots while it is still " +
                 "Soaring flat out. 1 disables the pivot.")]
        [SerializeField, Range(0f, 1f)] private float aiYastriStickBand = 0.75f;

        [Header("Refs")]
        [SerializeField] private VesselTransformer vesselTransformer;

        [Inject] private AudioSystem audioSystem;

        private const float TriggerDeadzone = 0.05f;

        private IVesselStatus _status;
        private float _baseBoostMultiplier;
        private bool _wasBoosting;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _baseBoostMultiplier = boostMultiplierOverride > 0f
                ? boostMultiplierOverride
                : shipStatus.BoostMultiplier;

            if (vesselTransformer == null)
                vesselTransformer = shipStatus.VesselTransformer;
        }

        private void Update()
        {
            if (_status == null || vesselTransformer == null) return;
            if (_status.InputStatus == null) return;

            float lt, rt;
            if (IsAutopilotDriven)
            {
                // The drive: both triggers at the autopilot's boost intent, which is a
                // function of how straight it is flying, plus a Yastri net trigger on the turn's
                // side once the yaw stick is past aiYastriStickBand (a hairpin pivot).
                var input = _status.InputStatus;
                AutopilotTriggers(input.XSum, Mathf.Max(Mathf.Abs(input.XSum), Mathf.Abs(input.YSum)),
                    aiBoostStickBand, aiYastriStickBand, out lt, out rt);
            }
            else
            {
                // Keyboard rides the same path: KeyboardInputStrategy writes the trigger analogs
                // from the two Shifts (digital 0/1), and keyboard resolves through the gamepad
                // override map — without this the desktop Manta had no turn or boost at all.
                var device = _status.InputStatus.ActiveInputDevice;
                if (device != InputDeviceType.Gamepad && device != InputDeviceType.Keyboard) return;

                lt = _status.InputStatus.LeftTriggerAnalog;
                rt = _status.InputStatus.RightTriggerAnalog;
            }
            if (_status.IsStationary) return;

            if (lt < TriggerDeadzone) lt = 0f;
            if (rt < TriggerDeadzone) rt = 0f;

            // Net turn: positive = right, negative = left
            float rawTurn = rt - lt;

            // Boost intensity: overlap of both triggers
            float boostIntensity = Mathf.Min(lt, rt);

            // Apply yaw
            if (Mathf.Abs(rawTurn) > 0.001f && !_status.IsTranslationRestricted)
            {
                float yawDeg = rawTurn * maxYawDegPerSec * Time.deltaTime;
                vesselTransformer.ApplyRotation(yawDeg, _status.Transform.up);
            }

            // Yastri: the held single-trigger turn shapes the trail — outer-lane flare plus
            // the Mass-5 Shielded Turn Trails window. Driven every frame (0 clears it), so a
            // released trigger immediately hands the trail back its straight-line shape.
            var prismController = _status.VesselPrismController;
            if (prismController)
                prismController.SetTurnTrail(Mathf.Abs(rawTurn), rawTurn >= 0f ? 1 : -1);

            // Apply analog boost
            if (boostIntensity > 0.01f)
            {
                if (!_wasBoosting && audioSystem != null)
                    audioSystem.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);

                _status.IsBoosting = true;
                _status.IsStationary = false;
                _status.BoostMultiplier = 1f + (_baseBoostMultiplier - 1f) * boostIntensity;
                _wasBoosting = true;
            }
            else if (_wasBoosting)
            {
                _status.IsBoosting = false;
                _status.BoostMultiplier = _baseBoostMultiplier;
                _wasBoosting = false;
            }
        }

        /// <summary>The vessel is flown by its AIPilot right now - a backfill bot, a released
        /// companion, or the menu's lava-lamp autopilot.</summary>
        private bool IsAutopilotDriven =>
            _status.AIPilot != null && _status.AIPilot.AutoPilotEnabled;

        /// <summary>
        /// How hard an autopilot holds the triggers: 1 with the stick inside
        /// <paramref name="boostBand"/>, 0 at a full deflection, linear between (0 band = never).
        /// Reads the stick AIPilot last wrote (its steering loop and this Update are not ordered
        /// against each other, so the read can be a frame stale - harmless against a 1.5/s speed
        /// lerp), so a bot lining up on a gate boosts and one hauling round a hairpin does not -
        /// which is the corner trade the Manta's course is cut around.
        /// </summary>
        public static float AutopilotBoostIntent(float stickDeflection, float boostBand)
        {
            if (boostBand <= 0f) return 0f;
            float stick = Mathf.Abs(stickDeflection);
            if (stick <= boostBand) return 1f;
            return Mathf.Clamp01(1f - (stick - boostBand) / Mathf.Max(1e-4f, 1f - boostBand));
        }

        /// <summary>
        /// The autopilot's two triggers. Both start at <see cref="AutopilotBoostIntent"/> of the
        /// larger stick axis; past <paramref name="yastriBand"/> of YAW deflection the trigger on
        /// the turn's side (RT for a positive, rightward <paramref name="yawStick"/>) is raised by
        /// the excess, rescaled to 0..1 over the band's remainder and capped at 1. The overlap
        /// <c>min(lt, rt)</c> - the boost - is therefore always the boost intent, and the net
        /// <c>rt - lt</c> is zero inside the band, so the pivot only ever ADDS yaw where a bot is
        /// already asking for more turn than the stick gives. Positive <c>rt - lt</c> and positive
        /// XSum both rotate about the vessel's up axis in the same sense (Update and
        /// <c>VesselTransformer.Yaw</c>), so the pivot always agrees with the stick.
        /// </summary>
        public static void AutopilotTriggers(float yawStick, float stickDeflection, float boostBand,
            float yastriBand, out float lt, out float rt)
        {
            float hold = AutopilotBoostIntent(stickDeflection, boostBand);
            lt = rt = hold;

            float yaw = Mathf.Abs(yawStick);
            if (yaw <= yastriBand) return;
            float excess = Mathf.Clamp01((yaw - yastriBand) / Mathf.Max(1e-4f, 1f - yastriBand));
            float outer = Mathf.Min(1f, hold + excess);
            if (yawStick > 0f) rt = outer;
            else lt = outer;
        }

        private void OnDisable()
        {
            if (_wasBoosting && _status != null)
            {
                _status.IsBoosting = false;
                _status.BoostMultiplier = _baseBoostMultiplier;
                _wasBoosting = false;
            }

            if (_status != null)
            {
                var prismController = _status.VesselPrismController;
                if (prismController) prismController.SetTurnTrail(0f, 0);
            }
        }
    }
}
