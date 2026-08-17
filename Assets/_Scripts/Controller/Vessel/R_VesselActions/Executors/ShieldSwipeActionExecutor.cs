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

        [Header("Blade Geometry")]
        [Tooltip("Half the blade mesh's extent along its length axis, in the blade's OWN local units. " +
                 "Unity's built-in Capsule spans local y in [-1, 1], so 1. This is what anchors the HILT: " +
                 "the blade grows from its mount outward like a sword instead of extending equally both " +
                 "ways like a staff, at every size the energy meter produces.")]
        [SerializeField] float bladeHalfExtentLocal = 1f;

        // Same deadzone the gamepad strategy uses for its press/release edges.
        const float TriggerDeadzone = 0.05f;

        IVesselStatus _status;
        Skimmer _swordSkimmer; // resolved from shieldRoot; carries SwordState when this is a Rhino

        Vector3 _baseLocalPos;
        Quaternion _baseLocalRot;
        bool _baseCaptured;

        float _diff;       // smoothed swipe control: -1 (left stance) .. +1 (right stance)
        float _sum;        // smoothed chop control: 0 (raised rest) .. 2 (full chop)
        float _appliedAnchor = float.NaN; // blade half-extent the last applied pose was anchored for

        // Per-direction swipe recovery. Engaged tracks the RAW pull so a suppressed input can't
        // re-arm itself, and the timer starts on RELEASE — a swing plays out in full, then owes
        // its recovery.
        bool _rightSwung, _leftSwung;
        float _rightReadyAt, _leftReadyAt;
        float _activeSign; // event-driven stance (+1/-1/0) for non-analog inputs
        bool _rightHeld;   // event-side per-direction held state (cross-swipe handoff)
        bool _leftHeld;

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
                // full difference, half chop; BOTH holds down mirror the both-triggers
                // centered chop (the energize stance), so binary devices can energize and
                // remote peers replay the owner's stance pose instead of a one-sided swipe.
                // If autopilot took over mid-hold (menu freestyle exit) the release edge
                // never arrives, so drop the stance.
                if (_activeSign != 0f && _status.IsLocalUser && _status.AutoPilotEnabled)
                    ClearStance();
                if (_rightHeld && _leftHeld)
                {
                    diffTarget = 0f;
                    sumTarget = 2f;
                }
                else
                {
                    diffTarget = _activeSign;
                    sumTarget = _activeSign != 0f ? 1f : 0f;
                }
            }

            // The stance is fed from the RAW trigger mirrors, before any swipe recovery is
            // applied — a sword recovering from a swing must still be able to chop and energize.
            FeedSwordStance();

            // Lateral recovery gates the SWIPE axis only; the chop (sum) passes through untouched.
            diffTarget = ApplySwipeRecovery(diffTarget);

            // A resting pose still has to be re-applied when the blade's LENGTH moved: the hilt
            // anchor is a function of that length, so skipping the write would leave the sword
            // growing out of both ends of its mount again (the staff read this fix removes).
            bool poseAtRest = _diff == 0f && _sum == 0f && diffTarget == 0f && sumTarget == 0f;
            if (poseAtRest && Mathf.Approximately(AnchorOffsetLocal(), _appliedAnchor)) return;

            _diff = Drive(_diff, diffTarget, analog);
            _sum = Drive(_sum, sumTarget, analog);
            ApplyShieldPose();
        }

        /// <summary>
        /// The Rhino energy sword's per-vessel state (null on any vessel whose shield
        /// root carries no sword brain). The swipe executor is the gesture SOURCE: it
        /// feeds the both-triggers stance each frame so the driver's energize state
        /// machine (the supershield key) runs off the same reparameterized trigger
        /// signals that pose the blade. See RHINO_ENERGY_SWORD.md.
        /// </summary>
        IRhinoSwordState Sword
        {
            get
            {
                if (!shieldRoot) return null;
                if (_swordSkimmer == null) shieldRoot.TryGetComponent(out _swordSkimmer);
                return _swordSkimmer ? _swordSkimmer.SwordState : null;
            }
        }

        /// <summary>
        /// The energize gesture: both triggers pulled (sum high) and even (difference
        /// near zero) — the lower/chop stance. Evaluated from the replicated trigger
        /// MIRRORS (`InputStatus` n_lTrig/n_rTrig — Owner-write, Everyone-read), NOT the
        /// local pose signals: the stance gates the supershield pop, which every client
        /// executes in its own local prism sim, so the verdict must be computable
        /// identically on every machine or one peer's energized blade pops a prism the
        /// owner's world keeps (a divergent conserved prismscape). The owner writes the
        /// mirrors from the same fingers that drive the pose; every peer runs the same
        /// thresholds on the same values. Autopilot drops the stance on the owner's
        /// machine (a paused InputController freezes the mirrors rather than zeroing
        /// them; the remote-side residual of that freeze is the replication follow-up
        /// in RHINO_ENERGY_SWORD.md).
        /// </summary>
        void FeedSwordStance()
        {
            var sword = Sword;
            if (sword == null) return;

            var input = _status?.InputStatus;
            if (input == null || (_status.IsLocalUser && _status.AutoPilotEnabled))
            {
                sword.SetInStance(false);
                return;
            }

            float lt = ApplyDeadzone(input.LeftTriggerAnalog);
            float rt = ApplyDeadzone(input.RightTriggerAnalog);
            bool inStance = lt + rt >= config.StanceSumThreshold
                            && Mathf.Abs(rt - lt) <= config.StanceCenterEpsilon;
            sword.SetInStance(inStance);
        }

        /// <summary>
        /// Give each swipe direction a short recovery after it releases, so the sword swings with
        /// a rhythm instead of flapping side to side as fast as the triggers can be worked. While
        /// the blade is ENERGIZED the recovery is ZERO — the frenzy is part of what energizing buys.
        ///
        /// This gates the lateral POSE and nothing else. The blade keeps cutting everything it
        /// touches throughout (ordinary damage is ungated — a locked rule), the chop/energize
        /// stance rides the sum axis and is never blocked, and a direction still recovering simply
        /// holds centre. Returns the difference target with any recovering direction suppressed.
        /// </summary>
        float ApplySwipeRecovery(float diffTarget)
        {
            float now = Time.time;
            float threshold = config.SwipeEngageThreshold;
            bool energized = Sword is { IsEnergized: true };

            // Track the swing/release edges off the RAW target, so suppressing a direction can
            // never make it look released and re-arm itself mid-hold.
            UpdateSwipeEdge(ref _rightSwung, ref _rightReadyAt, diffTarget >= threshold, now);
            UpdateSwipeEdge(ref _leftSwung, ref _leftReadyAt, -diffTarget >= threshold, now);

            if (energized)
            {
                // Zero cooldown: clear the timers too, so dropping out of energized never inherits
                // a stale recovery the player never felt themselves earn.
                _rightReadyAt = _leftReadyAt = now;
                return diffTarget;
            }

            if (diffTarget > 0f && now < _rightReadyAt) return 0f;
            if (diffTarget < 0f && now < _leftReadyAt) return 0f;
            return diffTarget;
        }

        void UpdateSwipeEdge(ref bool swung, ref float readyAt, bool engaged, float now)
        {
            if (engaged) swung = true;
            else if (swung)
            {
                swung = false;
                readyAt = now + config.SwipeCooldownSeconds;   // recovery starts on release
            }
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
            var pose = sweep * _baseLocalRot;
            shieldRoot.localRotation = pose;

            // HILT ANCHOR — what makes it a sword instead of a staff. The blade mesh is
            // centred on its transform, so growing it extends the capsule equally in BOTH
            // directions from the mount: at 30 the sword ran 30 units past the grip in each
            // direction, at 120 it ran 120 — a quarterstaff the vessel wears through its
            // middle. Offsetting the centre by the blade's own half-extent pins the HILT to
            // the authored mount and sends every unit of growth out the tip, so the sword
            // reads as a sword at every length the energy meter produces.
            float anchor = AnchorOffsetLocal();
            shieldRoot.localPosition = sweep * _baseLocalPos + pose * Vector3.up * anchor;
            _appliedAnchor = anchor;
        }

        /// <summary>
        /// Distance from the blade's mount to the centre of its mesh, in PARENT units: the
        /// capsule's local half-extent scaled by the length the shield driver is currently
        /// running. Local +Y is the tip direction (the blade elongates on Y —
        /// <c>Skimmer.elongateYOnly</c>).
        /// </summary>
        float AnchorOffsetLocal() =>
            shieldRoot ? shieldRoot.localScale.y * bladeHalfExtentLocal : 0f;

        void ClearStance()
        {
            _activeSign = 0f;
            _rightHeld = false;
            _leftHeld = false;
            // Never hand a re-spawned / re-taken vessel a recovery it did not swing for.
            _rightSwung = _leftSwung = false;
            _rightReadyAt = _leftReadyAt = 0f;
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

            // Drop the energize gesture too, so a turn-end/despawn mid-hold can't leave
            // the sword charging forever off a stance edge that will never release.
            Sword?.SetInStance(false);

            if (_baseCaptured && shieldRoot)
            {
                // Rest pose, hilt still anchored — the blade keeps whatever length the energy
                // meter is holding, so snapping back to the raw mount would re-centre it.
                float anchor = AnchorOffsetLocal();
                shieldRoot.localRotation = _baseLocalRot;
                shieldRoot.localPosition = _baseLocalPos + _baseLocalRot * Vector3.up * anchor;
                _appliedAnchor = anchor;
            }
        }
    }
}
