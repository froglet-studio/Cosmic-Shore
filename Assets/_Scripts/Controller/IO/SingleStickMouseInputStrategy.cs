using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Desktop mouse + keyboard flight for the fleet's ONE-THUMB vessels — the hulls whose
    /// transformer sets <c>IsSingleStickControls</c> and therefore steers off
    /// <c>EasedLeftJoystickPosition</c> alone: Sparrow, Serpent, Grizzly, Termite, Falcon,
    /// Shrike (<see cref="SingleStickVesselTransformer"/>) and Scarab
    /// (<see cref="ScarabVesselTransformer"/>).
    ///
    /// <para><b>Why these vessels need their own desktop scheme.</b> The desktop default,
    /// <see cref="KeyboardInputStrategy"/>, is a DUAL-stick layout: two digital sticks mixed
    /// through <see cref="DualStickMix"/> into yaw / pitch / speed / roll. A one-thumb vessel
    /// reads none of that mix — it reads the left stick's raw deflection — so on those hulls the
    /// entire right hand (P / ; / L / ') is dead keys, and the only steering left is four digital
    /// WASD directions with no magnitude between "centred" and "hard over". That is playable and
    /// it is not aiming, which is a problem on the vessel the shooter genre is built on.</para>
    ///
    /// <para><b>The mouse is the thumb.</b> The mouse hands us a DELTA and the vessel wants a
    /// POSITION, so the delta is integrated into a virtual stick clamped to the unit circle,
    /// with a spring back to centre standing in for the one a physical stick has and a mouse does
    /// not. Feel lives entirely in <see cref="MouseFlightConfigSO"/>
    /// (<c>Resources/MouseFlightConfig</c>) — never in code and never per-vessel.</para>
    ///
    /// <para><b>The buttons mirror the PAD, not the keyboard</b>, because a one-thumb vessel's
    /// abilities are authored against the pad and the pad's naming is what
    /// <c>InputHintBindingMap</c> and the ability lockup's control chips already speak:
    /// <list type="table">
    /// <item><term>Mouse move</term><description>the single stick — pitch, yaw, and the bank into
    /// the turn the transformer derives from it</description></item>
    /// <item><term>LMB <i>or</i> Right Shift</term><description>the RIGHT trigger side
    /// (<c>RightStickAction</c> / <c>OnlyRightStickAction</c> / <c>BothSticksAction</c> +
    /// <c>RightTriggerAnalog</c>) — the Sparrow's guns, the Scarab's throttle</description></item>
    /// <item><term>RMB <i>or</i> Left Shift</term><description>the LEFT trigger side — the
    /// Sparrow's skybursts, the Scarab's drift</description></item>
    /// <item><term>Space</term><description><c>Button1Action</c> (pad A)</description></item>
    /// <item><term>B</term><description><c>Button2Action</c> (pad B)</description></item>
    /// <item><term>N <i>or</i> MMB</term><description><c>Button3Action</c> (pad X)</description></item>
    /// <item><term>E</term><description><c>FlipAction</c> + the manual-throttle channel</description></item>
    /// </list>
    /// The shift keys are kept alongside the mouse buttons on purpose. They cost nothing (this
    /// scheme frees the whole right hand), they preserve the muscle memory of the keyboard
    /// scheme, and — the load-bearing reason — <c>InputHintBindingMap</c> maps the trigger sides
    /// to <c>KeyLeftShift</c>/<c>KeyRightShift</c> on keyboard, so every ability lockup chip on a
    /// one-thumb HUD keeps naming a control that genuinely fires it. A mouse-button glyph would
    /// need that map to answer "which of two controls do I label", which it has no way to decide
    /// today; a truthful label now beats an ambiguous one later.</para>
    ///
    /// <para>Both sources per side are OR'd into ONE boolean before edge detection. Two
    /// independent edge detectors on one logical trigger raise a release the moment either source
    /// lets go while the other is still held, which reads as the ability dropping out under your
    /// finger.</para>
    ///
    /// <para><b>There is no opt-out gesture</b>, and the first version's Escape-to-disengage was
    /// removed rather than kept: Escape is already the fullscreen toggle in
    /// <c>InputController.Update</c> and the reflexive "give me my cursor back" key in the
    /// Editor, so one press turned the scheme off for the whole session with nothing on screen to
    /// say so. It was redundant anyway — the cursor is released on pause and on every strategy
    /// hand-over. Every reason the scheme declines to engage is reported by
    /// <see cref="MouseFlightDiagnostics"/>, because its failure mode is silence: a one-thumb
    /// vessel still flies on WASD and still fires every ability off the same keys, so "not
    /// engaged" and "broken" look the same to a playtester.</para>
    /// </summary>
    public sealed class SingleStickMouseInputStrategy : BaseInputStrategy
    {
        /// <summary>
        /// What this scheme publishes for the two axes it does not have. A one-thumb vessel reads
        /// neither — <c>SingleStickVesselTransformer.ComputeThrottleTarget</c> ignores
        /// XDiff entirely (full throttle is implicit) and roll is the bank the transformer derives
        /// from the stick's own x — so the honest value is the NEUTRAL one every other strategy
        /// publishes with no throttle or roll axis deflected. Publishing full throttle (1) instead
        /// would make this scheme raise <c>FullSpeedStraightAction</c> on a hull where the pad
        /// never does, and a gesture that fires on one device and not another for the same vessel
        /// is worse than a gesture that fires on neither.
        ///
        /// <para>It also makes the InvertThrottle setting a genuine no-op here rather than a
        /// silently broken one: 0.5 is its own mirror image, so there is nothing to invert and
        /// nothing that can disagree with what the vessel actually does.</para>
        /// </summary>
        const float NeutralThrottle = 0.5f;

        Vector2 stick;

        bool prevLeftActive;
        bool prevRightActive;
        bool prevButton1;
        bool prevButton2;
        bool prevButton3;

        bool fullSpeedStraightEffectsStarted;
        bool minimumSpeedStraightEffectsStarted;

        // Engaged-but-deaf detection - see ReportIfMouseIsSilent.
        float activeSince;
        bool sawMouseMovement;

        static MouseFlightConfigSO Config => MouseFlightConfigSO.Instance;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
            ResetStrategyState();
        }

        public override void OnStrategyActivated()
        {
            base.OnStrategyActivated();
            inputStatus.ActiveInputDevice = InputDeviceType.MouseKeyboard;
            ResetInput();
            ResetStrategyState();
            LockCursor(true);
            activeSince = Time.unscaledTime;
            sawMouseMovement = false;

            // No delta to drain here, unlike DualMouseInputStrategy: Mouse.delta is reset by the
            // Input System every update, so it only ever holds THIS frame's movement. The stick
            // itself starts centred (ResetStrategyState above), so nothing the pointer did while
            // another strategy owned it can carry in.

            // Snapshot the live button state so a control already held at hand-over cannot raise a
            // press edge for something the player did before they were flying. (The engagement
            // gesture itself is a click RELEASE — see InputController — so this is belt and
            // braces rather than the primary guard: snapshotting a HELD button would otherwise
            // arm a release with no matching press, which is the same asymmetry it exists to
            // prevent.)
            SnapshotButtons();
        }

        public override void OnStrategyDeactivated()
        {
            ReleaseHeldControls();
            LockCursor(false);
            ResetInput();
            ResetStrategyState();
        }

        public override void OnPaused()
        {
            ReleaseHeldControls();
            LockCursor(false);
            stick = Vector2.zero;
            inputStatus.LeftTriggerAnalog = 0f;
            inputStatus.RightTriggerAnalog = 0f;
        }

        public override void OnResumed()
        {
            base.OnResumed();
            LockCursor(true);
            SnapshotButtons();
        }

        public override void ProcessInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;

            var pixelDelta = mouse.delta.ReadValue();
            ReportIfMouseIsSilent(pixelDelta);

            UpdateStick(pixelDelta);
            ProcessButtons(mouse, keyboard);
            Publish();
            PerformSpeedAndDirectionalEffects();
        }

        /// <summary>
        /// The scheme can fail in two ways that look identical on screen — never selected, or
        /// selected and reading nothing — and the first playtest could not tell them apart.
        /// <see cref="MouseFlightDiagnostics"/> reports the first; this reports the second, once,
        /// if the strategy has owned the input for a few seconds without a single non-zero mouse
        /// delta. A player who is flying necessarily moves the mouse, so silence that long means
        /// the device is not reaching us rather than that they sat still.
        /// </summary>
        void ReportIfMouseIsSilent(Vector2 pixelDelta)
        {
            if (sawMouseMovement) return;

            if (pixelDelta.sqrMagnitude > 0f)
            {
                sawMouseMovement = true;
                return;
            }

            if (Time.unscaledTime - activeSince < SilentMouseWarningSeconds) return;

            sawMouseMovement = true;   // report once, then stop asking
            CSDebug.LogWarning(
                "[MouseFlight] Engaged, but Mouse.current.delta has read exactly zero for " +
                $"{SilentMouseWarningSeconds:F0}s. The stick cannot deflect, so the vessel will " +
                "not turn. Check the Input System's Update Mode (a project set to " +
                "'Process Events In Fixed Update' delivers no delta to Update), and that the " +
                "Game view has focus with the cursor captured.");
        }

        const float SilentMouseWarningSeconds = 4f;

        // ------------------------------------------------------------------
        // The stick

        void UpdateStick(Vector2 pixelDelta)
        {
            var config = Config;
            // The STATE is kept un-snapped and the dead zone is applied at publish time - see
            // MouseVirtualStick's class doc on why zeroing the accumulator is a ratchet that
            // blocks slow mouse movement outright.
            stick = MouseVirtualStick.Step(stick, pixelDelta,
                                           config.StickUnitsPerPixel,
                                           config.SpringPerSecond,
                                           Time.deltaTime);
        }

        // ------------------------------------------------------------------
        // Buttons

        void ProcessButtons(Mouse mouse, Keyboard keyboard)
        {
            bool space = keyboard != null && keyboard.spaceKey.isPressed;
            bool bKey = keyboard != null && keyboard.bKey.isPressed;
            bool nKey = keyboard != null && keyboard.nKey.isPressed;
            bool eKey = keyboard != null && keyboard.eKey.isPressed;

            EdgeFire(space, ref prevButton1, InputEvents.Button1Action);
            EdgeFire(bKey, ref prevButton2, InputEvents.Button2Action);
            EdgeFire(nKey || mouse.middleButton.isPressed, ref prevButton3, InputEvents.Button3Action);

            // FlipAction has no held state to track beyond the key itself, and the manual-throttle
            // channel rides the same key, exactly as KeyboardInputStrategy wires it.
            if (keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                    inputStatus.OnButtonPressed.Raise(InputEvents.FlipAction);
                if (keyboard.eKey.wasReleasedThisFrame)
                    inputStatus.OnButtonReleased.Raise(InputEvents.FlipAction);
            }
            inputStatus.Throttle = eKey ? 1f : 0f;

            // ONE boolean per side from BOTH sources — see the class doc on why two edge
            // detectors would drop an ability the moment either source released.
            bool leftActive = mouse.rightButton.isPressed
                              || (keyboard != null && keyboard.leftShiftKey.isPressed);
            bool rightActive = mouse.leftButton.isPressed
                               || (keyboard != null && keyboard.rightShiftKey.isPressed);

            DispatchTriggers(leftActive, rightActive);
        }

        void EdgeFire(bool isPressed, ref bool prev, InputEvents evt)
        {
            if (isPressed && !prev) inputStatus.OnButtonPressed.Raise(evt);
            if (!isPressed && prev) inputStatus.OnButtonReleased.Raise(evt);
            prev = isPressed;
        }

        /// <summary>
        /// The per-side trigger events and their Only/Both composites. This is deliberately a
        /// transcription of <see cref="KeyboardInputStrategy"/>'s dispatch rather than a fresh
        /// derivation: every vessel's drift stack, the Squirrel tube, and the Scarab's
        /// double-tap detector are all written against these exact edges, and a scheme that
        /// raises a subtly different set is a scheme those abilities misbehave on.
        /// </summary>
        void DispatchTriggers(bool leftActive, bool rightActive)
        {
            inputStatus.LeftTriggerAnalog = leftActive ? 1f : 0f;
            inputStatus.RightTriggerAnalog = rightActive ? 1f : 0f;

            bool leftJustPressed = leftActive && !prevLeftActive;
            bool leftJustReleased = !leftActive && prevLeftActive;
            bool leftHeld = leftActive;

            bool rightJustPressed = rightActive && !prevRightActive;
            bool rightJustReleased = !rightActive && prevRightActive;
            bool rightHeld = rightActive;

            prevLeftActive = leftActive;
            prevRightActive = rightActive;

            if (leftJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            if (leftJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
            if (rightJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            if (rightJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);

            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);

            if ((leftJustReleased && !rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);

            if ((rightJustReleased && !leftHeld)
                || (leftJustPressed && rightHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);

            if ((leftJustPressed && !rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);

            if ((rightJustPressed && !leftHeld)
                || (leftJustReleased && rightHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);

            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);
        }

        // ------------------------------------------------------------------
        // Publish

        void Publish()
        {
            // InvertY is applied HERE, at the source, rather than to YSum the way DualStickMix
            // does it — because a one-thumb vessel never reads YSum. Its pitch, its hull puppetry
            // (VesselAnimation) and its strafing roll (BarrelRollController) all read this one
            // stick, so inverting the stick is what makes every consumer agree about which way the
            // player just pushed. YSum below is derived from the already-inverted value for the
            // same reason: one truth, published once.
            Vector2 reported = MouseVirtualStick.Deflection(stick, Config.DeadZone);
            Vector2 aimed = inputStatus.InvertYEnabled ? new Vector2(reported.x, -reported.y) : reported;

            inputStatus.EasedLeftJoystickPosition = new Vector2(Ease(2f * aimed.x), Ease(2f * aimed.y));
            inputStatus.LeftNormalizedJoystickPosition = aimed;

            // The right stick is genuinely absent, so it publishes as centred rather than being
            // left at whatever the previous strategy wrote. A stale right stick would keep the
            // Scarab's juke armed and keep any polled right-stick read alive with no control
            // behind it.
            inputStatus.EasedRightJoystickPosition = Vector2.zero;
            inputStatus.RightNormalizedJoystickPosition = Vector2.zero;

            inputStatus.XSum = Ease(aimed.x);   // yaw
            inputStatus.YSum = -Ease(aimed.y);  // pitch
            inputStatus.XDiff = NeutralThrottle;
            inputStatus.YDiff = 0f;             // roll: the transformer banks into the turn itself
        }

        /// <summary>
        /// The straight-line gestures, kept in the same shape as the other four strategies so
        /// this one is not the odd one out and so a throttle axis added here later gets them for
        /// free. With <see cref="NeutralThrottle"/> constant at 0.5 neither deviation can fall
        /// under the 0.3 threshold, so as shipped neither gesture fires — which is exactly what a
        /// one-thumb vessel sees on a pad.
        /// </summary>
        void PerformSpeedAndDirectionalEffects()
        {
            const float threshold = 0.3f;
            float sumOfRotations = Mathf.Abs(inputStatus.YDiff)
                                 + Mathf.Abs(inputStatus.YSum)
                                 + Mathf.Abs(inputStatus.XSum);
            float deviationFromFullSpeedStraight = (1f - inputStatus.XDiff) + sumOfRotations;
            float deviationFromMinimumSpeedStraight = inputStatus.XDiff + sumOfRotations;

            if (deviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.FullSpeedStraightAction);
            }
            else if (deviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && deviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && deviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        // ------------------------------------------------------------------
        // Housekeeping

        /// <summary>
        /// Hand every held control back before this strategy stops running. A trigger or button
        /// still latched when the player alt-tabs, pauses, or drops to the keyboard scheme would
        /// leave its ability running with nothing left to release it.
        /// </summary>
        void ReleaseHeldControls()
        {
            if (prevLeftActive || prevRightActive)
                DispatchTriggers(false, false);

            EdgeFire(false, ref prevButton1, InputEvents.Button1Action);
            EdgeFire(false, ref prevButton2, InputEvents.Button2Action);
            EdgeFire(false, ref prevButton3, InputEvents.Button3Action);

            if (fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
            }
            if (minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
            }
        }

        void SnapshotButtons()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            prevLeftActive = (mouse != null && mouse.rightButton.isPressed)
                             || (keyboard != null && keyboard.leftShiftKey.isPressed);
            prevRightActive = (mouse != null && mouse.leftButton.isPressed)
                              || (keyboard != null && keyboard.rightShiftKey.isPressed);
            prevButton1 = keyboard != null && keyboard.spaceKey.isPressed;
            prevButton2 = keyboard != null && keyboard.bKey.isPressed;
            prevButton3 = (keyboard != null && keyboard.nKey.isPressed)
                          || (mouse != null && mouse.middleButton.isPressed);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void ResetStrategyState()
        {
            stick = Vector2.zero;
            prevLeftActive = false;
            prevRightActive = false;
            prevButton1 = false;
            prevButton2 = false;
            prevButton3 = false;
            fullSpeedStraightEffectsStarted = false;
            minimumSpeedStraightEffectsStarted = false;
        }

        public override void SetPortrait(bool portrait) { }
    }
}
