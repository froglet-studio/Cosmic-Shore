using CosmicShore.Utility;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the Rhino shield swipe from the analog triggers, reparameterized like the
    /// Manta's turn boost: the DIFFERENCE (right - left) sweeps the sword laterally
    /// through yaw+roll (full pulls are the +/-1 stances) and the SUM pitches it down
    /// (both triggers = centered but fully down; a single full pull chops halfway). The
    /// sword pivots about its parent's origin - rotation and mount position together -
    /// so it carves a real arc through space. The raised rest pose is whatever the
    /// skimmer transform is authored to in the prefab. For inputs without analog
    /// triggers (remote peers replaying the owner's events, touch bindings), the
    /// press/release events drive the same rig at full deflection. Only scale is driven
    /// on this transform elsewhere (ShieldSkimmerScaleDriver), so rotation/position are
    /// ours to animate.
    /// </summary>
    public sealed class ShieldSwipeActionExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [SerializeField] RhinoShieldSwipeConfigSO config;

        [Header("References")]
        [Tooltip("The shield/sword transform to sweep (the ForceFieldSkimmer root). Falls back to the near-field skimmer.")]
        [SerializeField] Transform shieldRoot;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        // Same deadzone the gamepad strategy uses for its press/release edges.
        const float TriggerDeadzone = 0.05f;

        IVesselStatus _status;

        Vector3 _baseLocalPos;
        Quaternion _baseLocalRot;
        bool _baseCaptured;

        float _diff;       // smoothed swipe control: -1 (left stance) .. +1 (right stance)
        float _sum;        // smoothed chop control: 0 (raised rest) .. 2 (full chop)
        float _activeSign; // event-driven stance (+1/-1/0) for non-analog inputs
        bool _rightHeld;   // event-side per-direction held state (cross-swipe handoff)
        bool _leftHeld;

        // The Rhino energy-sword state lives on the sword skimmer (ShieldSkimmerScaleDriver).
        // Difference (single trigger) drives SLASHING; sum (both triggers, centered) drives the
        // energize STANCE. Null-safe so a Rhino without the driver simply runs the pose only.
        IRhinoSwordState Sword
        {
            get
            {
                var sk = _status?.NearFieldSkimmer;
                return sk ? sk.SwordState : null;
            }
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            ResetImmediate();
        }

        void OnTurnEndOfMiniGame() => ResetImmediate();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            if (!config)
                CSDebug.LogError($"[{nameof(ShieldSwipeActionExecutor)}] No {nameof(RhinoShieldSwipeConfigSO)} wired on '{name}' - the shield swipe will not run.");
            EnsureShieldRoot();
        }

        void Update()
        {
            if (_status == null || !config) return;
            if (!EnsureShieldRoot()) return;

            float diffTarget, sumTarget;
            bool analog = IsLocalAnalogPilot(out var input);
            if (analog)
            {
                // Read the triggers straight off the hardware for the local pose. The
                // InputStatus trigger properties are NetworkVariable mirrors meant for
                // replication - going through them puts the whole netvar pipeline
                // between the finger and the sword. DualMouse has no hardware trigger
                // axis, so it keeps the mirror (binary 0/1 by design).
                float lt, rt;
                var pad = Gamepad.current;
                if (input.ActiveInputDevice == InputDeviceType.Gamepad && pad != null)
                {
                    lt = ApplyDeadzone(pad.leftTrigger.ReadValue());
                    rt = ApplyDeadzone(pad.rightTrigger.ReadValue());
                }
                else
                {
                    lt = ApplyDeadzone(input.LeftTriggerAnalog);
                    rt = ApplyDeadzone(input.RightTriggerAnalog);
                }
                diffTarget = rt - lt;
                sumTarget = rt + lt;
            }
            else
            {
                // Event-driven stance: remote peers replaying the owner's press/release,
                // and binary local devices. A press mirrors a full single-trigger pull -
                // full difference, half chop. If autopilot took over mid-hold (menu
                // freestyle exit) the release edge never arrives, so drop the stance.
                if (_activeSign != 0f && _status.IsLocalUser && _status.AutoPilotEnabled)
                    ClearStance();
                diffTarget = _activeSign;
                sumTarget = _activeSign != 0f ? 1f : 0f;
            }

            FeedSwordSignals(diffTarget, sumTarget);

            if (_diff == 0f && _sum == 0f && diffTarget == 0f && sumTarget == 0f) return;

            _diff = Drive(_diff, diffTarget, analog);
            _sum = Drive(_sum, sumTarget, analog);
            ApplyShieldPose();
        }

        /// <summary>
        /// Analog control only exists for the local pilot - triggers don't replicate.
        /// Remote peers and AI fall through to the event-driven stance.
        /// </summary>
        bool IsLocalAnalogPilot(out IInputStatus input)
        {
            input = null;
            if (_status == null || !_status.IsLocalUser || _status.AutoPilotEnabled) return false;
            input = _status.InputStatus;
            if (input == null) return false;
            return input.ActiveInputDevice is InputDeviceType.Gamepad or InputDeviceType.DualMouse;
        }

        public void BeginSwipe(RhinoShieldSwipeActionSO so, IVesselStatus status)
        {
            _status ??= status;
            if (!so) return;

            // The analog drive owns the pose on this machine; the event still replicated
            // to peers before reaching this executor, which is all it needs to do here.
            if (IsLocalAnalogPilot(out _)) return;

            if (so.DirectionSign > 0f) _rightHeld = true;
            else _leftHeld = true;
            _activeSign = so.DirectionSign;
        }

        public void EndSwipe(RhinoShieldSwipeActionSO so, IVesselStatus status)
        {
            _status ??= status;
            if (!so) return;
            if (IsLocalAnalogPilot(out _)) return;

            bool isRight = so.DirectionSign > 0f;
            if (isRight) _rightHeld = false;
            else _leftHeld = false;

            // Only the stance-owning trigger moves the sword; if the opposite trigger is
            // still held it takes the stance back instead of the sword recentering.
            if (_activeSign == 0f || !Mathf.Approximately(_activeSign, so.DirectionSign)) return;
            _activeSign = (isRight ? _leftHeld : _rightHeld) ? -so.DirectionSign : 0f;
        }

        float Drive(float current, float target, bool analog)
        {
            if (analog)
            {
                // Position tracking, not an animation - the finger is the tween. The
                // tiny time constant only filters sensor jitter, so partial pulls hold
                // partial orientations and the pose follows the trigger continuously.
                float tau = Mathf.Max(0.001f, config.AnalogSmoothingSeconds);
                current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.deltaTime / tau));
                return Mathf.Abs(current - target) < 0.001f ? target : current;
            }

            // Event-driven targets snap (press/release); rate-limit the travel so a
            // binary input still reads as a swing: full single-trigger travel (1 unit)
            // takes swipeOutSeconds out, returnSeconds back.
            bool attacking = Mathf.Abs(target) > Mathf.Abs(current);
            float seconds = Mathf.Max(0.01f, attacking ? config.SwipeOutSeconds : config.ReturnSeconds);
            return Mathf.MoveTowards(current, target, Time.deltaTime / seconds);
        }

        // Re-normalized so travel is continuous from 0 at the deadzone edge to 1 at
        // full pull - a plain cutoff would step the pose at the deadzone boundary.
        static float ApplyDeadzone(float value) =>
            value < TriggerDeadzone ? 0f : (value - TriggerDeadzone) / (1f - TriggerDeadzone);

        void ApplyShieldPose()
        {
            float yaw = _diff * config.SwipeYawDegrees;
            float roll = _diff * config.SwipeRollDegrees;
            float pitch = 0.5f * _sum * config.ChopPitchDegrees;

            // Pitch innermost so the chop lowers the blade in front first and the
            // yaw/roll swipe then carries the lowered blade around the vessel. With
            // both triggers pulled (difference 0) this is a pure straight-down chop.
            var sweep = Quaternion.AngleAxis(yaw, Vector3.up)
                      * Quaternion.AngleAxis(roll, Vector3.forward)
                      * Quaternion.AngleAxis(pitch, Vector3.right);
            shieldRoot.localRotation = sweep * _baseLocalRot;
            shieldRoot.localPosition = sweep * _baseLocalPos;
        }

        // Translate the analog difference/sum into the sword's two gestures each frame:
        // a single-trigger pull (|difference| high) is a SLASH; both triggers held centered
        // (sum high, |difference| low) is the energize STANCE (the "lower position").
        void FeedSwordSignals(float diffTarget, float sumTarget)
        {
            var sword = Sword;
            if (sword == null) return;

            float absDiff = Mathf.Abs(diffTarget);
            bool inStance = sumTarget >= config.StanceSumThreshold && absDiff <= config.StanceCenterEpsilon;
            bool slashing = absDiff >= config.SlashTriggerThreshold;

            sword.SetInStance(inStance);
            sword.SetSlashing(slashing);
        }

        void ClearStance()
        {
            _activeSign = 0f;
            _rightHeld = false;
            _leftHeld = false;
        }

        bool EnsureShieldRoot()
        {
            if (!shieldRoot && _status?.NearFieldSkimmer)
                shieldRoot = _status.NearFieldSkimmer.transform;
            if (!shieldRoot) return false;

            if (!_baseCaptured)
            {
                _baseLocalPos = shieldRoot.localPosition;
                _baseLocalRot = shieldRoot.localRotation;
                _baseCaptured = true;
            }
            return true;
        }

        // Never leave a half-applied swipe behind (pooling / vessel swap / turn end).
        void ResetImmediate()
        {
            _diff = 0f;
            _sum = 0f;
            ClearStance();

            // Drop the sword gestures so a turn-end/despawn can't leave it stuck slashing
            // or mid-stance (which would keep it energizing or gating damage).
            var sword = Sword;
            if (sword != null)
            {
                sword.SetInStance(false);
                sword.SetSlashing(false);
            }

            if (_baseCaptured && shieldRoot)
            {
                shieldRoot.localRotation = _baseLocalRot;
                shieldRoot.localPosition = _baseLocalPos;
            }
        }
    }
}
