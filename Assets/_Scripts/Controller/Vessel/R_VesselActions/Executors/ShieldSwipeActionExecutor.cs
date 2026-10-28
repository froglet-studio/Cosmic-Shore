using CosmicShore.Core;
using CosmicShore.UI;
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
    /// skimmer transform is authored to in the prefab. Only scale is driven on this transform
    /// elsewhere (ShieldSkimmerScaleDriver), so rotation/position are ours to animate.
    ///
    /// EVERY PEER POSES THE BLADE FROM THE SAME TRIGGERS. The owner's analog trigger depth travels
    /// in InputStatus's owner-write trigger mirrors, and every peer reads them; the local gamepad
    /// pilot reads the hardware directly (the same value it writes, minus the netvar's latency).
    /// Until 2026-10-16 a remote peer posed the blade from the replayed press/release EVENTS
    /// instead - a full ±1 single-trigger swing the moment a trigger crossed its deadzone - and
    /// each peer cuts prisms against its OWN blade, so a half-pull sweep that missed on the
    /// pilot's screen cut on everyone else's and the prism fields diverged. The press/release
    /// path survives only for touch, whose SWINGS are thumb lifts (events). A phone writes the
    /// mirrors only while both thumbs are off the glass (TouchControlProfile.idlePullsBothTriggers,
    /// 2026-10-21): both read fully pulled, which is the energize stance - so a phone energizes
    /// through the mirrors like every other device and the event-driven pose drops into the
    /// centred chop with it (<see cref="MirrorsInStance"/>). An AI Rhino's
    /// <see cref="RhinoSwordAutopilot"/> writes the mirrors on its owning machine, so its sword
    /// replicates the same way.
    ///
    /// COMBOS ride on top: a rapid string of trigger TAPS (RR, RL, RLR, …) calls an authored
    /// flourish from <see cref="RhinoSwordComboLibrarySO"/> — a separate, upgraded set while
    /// the blade is ENERGIZED — which blends in from the current pose, plays its path, and
    /// blends back to wherever the fingers are. The local pilot sees its name in the game's toast
    /// container (top right, under Settings). A held trigger is never a tap, so positioning
    /// the sword is untouched. Detection reads the replicated trigger MIRRORS (the same values
    /// the energize stance reads), so every peer calls the same combo. See RHINO_SWORD_COMBOS.md.
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

        [Header("Autopilot")]
        [Tooltip("The AI's sword hand (RhinoSwordAutopilot on the Rhino root). While it drives the " +
                 "trigger mirrors, an autopiloted vessel's triggers are followed instead of dropped. " +
                 "Found on the vessel when empty.")]
        [SerializeField] RhinoSwordAutopilot autopilot;

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

        // --- Combos -------------------------------------------------------------------------
        RhinoSwordComboDetector _comboDetector;
        RhinoSwordComboLibrarySO _comboLibraryConfigured; // library the detector was configured from
        RhinoSwordComboPath _flourish;         // playing flourish, null when the fingers own the pose
        float _flourishElapsed;
        SwordPoseChannels _flourishEntry;      // pose the flourish blends in FROM (wrapped)
        SwordPoseChannels _lastApplied;        // last pose written to the sword, in channel space
        RhinoSwordFXController _fx;            // the blade's own FX (same GameObject as shieldRoot)

        /// <summary>The flourish currently playing, or null. For FX/HUD/diagnostics.</summary>
        public RhinoSwordComboPath ActiveFlourish => _flourish;

        /// <summary>The pose last written to the blade (the swipe, the chop and any flourish), in
        /// channel space; zero at rest. <see cref="RhinoAnimation"/> leans the wings with it.</summary>
        public SwordPoseChannels CurrentPose => _lastApplied;

        /// <summary>The swipe/chop tuning, so a reader can normalise <see cref="CurrentPose"/> (the HUD's katana).</summary>
        public RhinoShieldSwipeConfigSO Config => config;

        /// <summary>The frame <see cref="CurrentPose"/>'s sweep is expressed in: the blade's parent,
        /// whose origin it pivots about. Null until the blade resolves.</summary>
        public Transform PoseFrame => shieldRoot ? shieldRoot.parent : null;

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
            bool analog;
            if (TryReadTriggers(out float lt, out float rt, out analog))
            {
                // The same reparameterization for every source. A binary device's mirror is 0 or 1,
                // so a single press is full difference / half chop and both held is the centred chop
                // (the energize stance) - exactly what the event path below draws, on every peer.
                diffTarget = rt - lt;
                sumTarget = rt + lt;
            }
            else
            {
                // A device that writes no trigger mirrors (touch): the press/release events drive
                // the stance at full deflection. If autopilot took over mid-hold the release edge
                // never arrives, so drop the stance.
                if (_activeSign != 0f && IgnoresTriggers) ClearStance();
                // Both swing controls held, or (a phone with both thumbs off) both mirrors pulled:
                // the centred chop. The second reads the same replicated values the energize stance
                // reads, so the blade is down on every peer exactly while the stance counts.
                if ((_rightHeld && _leftHeld) || (_status.Player != null && MirrorsInStance(_status.InputStatus)))
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

            // Combos read the same replicated mirrors, so a flourish starts on every peer.
            TickCombos();

            // Lateral recovery gates the SWIPE axis only; the chop (sum) passes through untouched.
            diffTarget = ApplySwipeRecovery(diffTarget);

            // A resting pose still has to be re-applied when the blade's LENGTH moved: the hilt
            // anchor is a function of that length, so skipping the write would leave the sword
            // growing out of both ends of its mount again (the staff read this fix removes).
            bool poseAtRest = _flourish == null
                              && _diff == 0f && _sum == 0f && diffTarget == 0f && sumTarget == 0f;
            if (poseAtRest && Mathf.Approximately(AnchorOffsetLocal(), _appliedAnchor)) return;

            // The fingers keep driving underneath a flourish, so the blend back lands exactly
            // where the triggers are at that moment rather than where they were when it began.
            _diff = Drive(_diff, diffTarget, analog);
            _sum = Drive(_sum, sumTarget, analog);

            ApplyShieldPose(_flourish != null ? AdvanceFlourish(Time.deltaTime) : AnalogChannels());
        }

        /// <summary>The trigger reparameterization, expressed in pose channels.</summary>
        SwordPoseChannels AnalogChannels() => new(
            _diff * config.SwipeYawDegrees,
            _diff * config.SwipeRollDegrees,
            0.5f * _sum * config.ChopPitchDegrees,
            0f);

        /// <summary>
        /// Feed this frame's trigger MIRRORS to the combo detector and start a flourish when a
        /// string of taps resolves. The mirrors are Owner-write/Everyone-read, so the owner and
        /// every replica see the same edges and call the same combo (to within a network tick
        /// of timing). A paused owner's mirrors freeze rather than zero, so the owner's
        /// autopilot drops the chain the same way it drops the stance.
        /// </summary>
        void TickCombos()
        {
            var library = config.ComboLibrary;
            if (!library)
            {
                _comboDetector?.Reset();
                return;
            }

            if (_comboDetector == null || _comboLibraryConfigured != library)
            {
                _comboDetector ??= new RhinoSwordComboDetector(library.DetectorSettings);
                _comboDetector.Configure(library.DetectorSettings);
                _comboDetector.Reset();
                _comboLibraryConfigured = library;
            }

            var input = _status.InputStatus;
            if (input == null || IgnoresTriggers)
            {
                _comboDetector.Reset();
                return;
            }

            float lt = ApplyDeadzone(input.LeftTriggerAnalog);
            float rt = ApplyDeadzone(input.RightTriggerAnalog);
            if (!_comboDetector.Step(Time.time, lt, rt, out var combo)) return;

            bool energized = Sword is { IsEnergized: true };
            if (!library.TryGet(combo.Sequence, energized, out var path)) return;
            StartFlourish(library, path, combo.IsFinisher, energized && path.Energized);
        }

        void StartFlourish(RhinoSwordComboLibrarySO library, RhinoSwordComboPath path, bool finisher, bool energized)
        {
            _flourishEntry = _lastApplied.Wrapped();
            _flourish = path;
            _flourishElapsed = 0f;

            // A finisher plays out: presses during most of it neither chain nor cancel it.
            if (finisher)
                _comboDetector.SuppressPressesUntil(Time.time + path.DurationSeconds * library.FinisherLockoutFraction);

            // The flourish is the swing — it never owes the recovery a lone swipe would.
            ClearSwipeRecovery();

            if (!path.Sound.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEventAttached(path.Sound, shieldRoot.gameObject);

            if (!_fx) shieldRoot.TryGetComponent(out _fx);
            if (_fx) _fx.NotifyCombo(finisher, energized);

            // The combo's name, for the pilot who landed it: through the game's toast container (top
            // right, under Settings) - the only place incoming text goes - from the library's authored
            // template. An AI, a remote copy and the lava lamp's autopilot are silent.
            if (_status.Player != null && _status.IsLocalUser && !_status.IsInitializedAsAI && !_status.AutoPilotEnabled)
            {
                string line = TemplateText.Fill(library.ComboToastTemplate, "name", path.DisplayName);
                if (!string.IsNullOrEmpty(line)) ToastNotificationAPI.Show(line);
            }
        }

        /// <summary>
        /// Advance the playing flourish and return this frame's pose: blend in from the entry
        /// pose, follow the authored path, then blend out to the LIVE trigger pose. The end pose
        /// is wrapped before the blend out (visually identical — see
        /// <see cref="SwordPoseChannels.Wrapped"/>) so a flourish that spun the blade a full turn
        /// does not unwind it on the way back to the fingers.
        /// </summary>
        SwordPoseChannels AdvanceFlourish(float dt)
        {
            var library = config.ComboLibrary;
            float blendIn = library ? library.BlendInSeconds : 0.06f;
            float blendOut = library ? library.BlendOutSeconds : 0.18f;

            _flourishElapsed += dt;
            float duration = _flourish.DurationSeconds;

            if (_flourishElapsed <= duration)
            {
                var onPath = _flourish.Sample(_flourishElapsed / duration);
                float w = Smooth01(_flourishElapsed / blendIn);
                return SwordPoseChannels.Lerp(_flourishEntry, onPath, w);
            }

            float outT = (_flourishElapsed - duration) / blendOut;
            var live = AnalogChannels();
            if (outT >= 1f)
            {
                EndFlourish();
                return live;
            }
            return SwordPoseChannels.Lerp(_flourish.EndPose.Wrapped(), live, Smooth01(outT));
        }

        void EndFlourish()
        {
            _flourish = null;
            _flourishElapsed = 0f;
            ClearSwipeRecovery();
        }

        void ClearSwipeRecovery()
        {
            _rightReadyAt = _leftReadyAt = 0f;
            _rightSwung = _leftSwung = false;
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
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
            if (input == null || IgnoresTriggers)
            {
                sword.SetInStance(false);
                return;
            }

            sword.SetInStance(MirrorsInStance(input));
        }

        /// <summary>The energize stance read off the trigger MIRRORS: both pulled (sum at or over
        /// <c>StanceSumThreshold</c>) and even (difference within <c>StanceCenterEpsilon</c>),
        /// deadzone-renormalized. On every device, touch included - a phone's mirrors read 1 while
        /// both thumbs are off the glass.</summary>
        bool MirrorsInStance(IInputStatus input)
        {
            if (input == null) return false;
            float lt = ApplyDeadzone(input.LeftTriggerAnalog);
            float rt = ApplyDeadzone(input.RightTriggerAnalog);
            return lt + rt >= config.StanceSumThreshold
                   && Mathf.Abs(rt - lt) <= config.StanceCenterEpsilon;
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
        /// True while this machine's own human pilot is under autopilot and nothing is driving the
        /// triggers for them (the menu's lava-lamp autopilot with no sword hand): a paused input
        /// controller FREEZES the mirrors rather than zeroing them, so the last pull would hold the
        /// pose and the stance forever. An autopilot that writes the triggers itself is followed.
        /// </summary>
        bool IgnoresTriggers =>
            _status != null && _status.IsLocalUser && _status.AutoPilotEnabled && !AutopilotDrivesTriggers;

        bool AutopilotDrivesTriggers
        {
            get
            {
                if (!autopilot && _status?.ShipTransform) autopilot = _status.ShipTransform.GetComponentInChildren<RhinoSwordAutopilot>(true);
                return autopilot && autopilot.DrivesTriggers;
            }
        }

        /// <summary>
        /// This frame's trigger depths (deadzoned), from the source every peer agrees on. False for
        /// touch, whose thumb-lift press/release events drive the swing instead (its mirrors move only
        /// for the both-thumbs-off energize stance, read by <see cref="MirrorsInStance"/>).
        /// <paramref name="analog"/> says whether the values are continuous (track them) or binary
        /// (rate-limit them into a swing) - decided from the REPLICATED device, so it is the same
        /// answer on every peer.
        /// </summary>
        bool TryReadTriggers(out float lt, out float rt, out bool analog)
        {
            lt = rt = 0f;
            analog = false;
            var input = _status?.InputStatus;
            if (input == null || _status.Player == null) return false;

            if (IgnoresTriggers)
            {
                analog = true;   // release smoothly back to rest
                return true;
            }

            var device = input.ActiveInputDevice;
            bool driven = _status.IsInitializedAsAI || _status.AutoPilotEnabled;
            if (device == InputDeviceType.Touch && !driven) return false;

            // DualMouse's mirror is binary by design but was always TRACKED (not rate-limited), and
            // that feel is kept; keyboard was always the rate-limited swing.
            analog = device is InputDeviceType.Gamepad or InputDeviceType.DualMouse || driven;

            // The local gamepad pilot reads the hardware: the mirror carries the same value, but
            // through the whole netvar pipeline between the finger and the sword.
            var pad = Gamepad.current;
            if (_status.IsLocalUser && !_status.AutoPilotEnabled && device == InputDeviceType.Gamepad && pad != null)
            {
                lt = ApplyDeadzone(pad.leftTrigger.ReadValue());
                rt = ApplyDeadzone(pad.rightTrigger.ReadValue());
                return true;
            }

            lt = ApplyDeadzone(input.LeftTriggerAnalog);
            rt = ApplyDeadzone(input.RightTriggerAnalog);
            return true;
        }

        /// <summary>True while the events should drive the stance (see <see cref="TryReadTriggers"/>).</summary>
        bool EventsDriveStance => !TryReadTriggers(out _, out _, out _);

        public void BeginSwipe(RhinoShieldSwipeActionSO so, IVesselStatus status)
        {
            _status ??= status;
            if (!so) return;

            // The trigger mirrors own the pose wherever they exist; the event only drives a device
            // that writes none.
            if (!EventsDriveStance) return;

            if (so.DirectionSign > 0f) _rightHeld = true;
            else _leftHeld = true;
            _activeSign = so.DirectionSign;
        }

        public void EndSwipe(RhinoShieldSwipeActionSO so, IVesselStatus status)
        {
            _status ??= status;
            if (!so) return;
            if (!EventsDriveStance) return;

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

        void ApplyShieldPose(SwordPoseChannels channels)
        {
            float yaw = channels.yaw;
            float roll = channels.roll;
            float pitch = channels.pitch;

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
            // THRUST (combos only) slides the whole blade out along itself — a lunge — as a
            // fraction of that same anchor length, so it reads the same at every blade size.
            float anchor = AnchorOffsetLocal();
            shieldRoot.localPosition = sweep * _baseLocalPos + pose * Vector3.up * (anchor * (1f + channels.thrust));
            _appliedAnchor = anchor;
            _lastApplied = channels;
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
            _flourish = null;
            _flourishElapsed = 0f;
            _lastApplied = default;
            _comboDetector?.Reset();

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
