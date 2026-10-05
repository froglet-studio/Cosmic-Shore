using CosmicShore.Cli;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using Silk.NET.Input;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-H: the HUMAN PILOT in a windowed round. Takes over
    /// <c>round.Players[0]</c>'s vessel — the MenuCrystalClickHandler.ToggleTransition
    /// shape (autopilot OFF, the human drives the SAME <see cref="IInputStatus"/> sink
    /// the AIPilot wrote, so the verbatim InputStatus → VesselTransformer flight path
    /// is untouched) — and feeds it the RaceWindow keyboard scheme each frame:
    /// WASD = left stick, arrows = right, Space = boost (Button1Action), Shift =
    /// drift (OnlyLeftStickAction). Button actions go STRAIGHT to the one vessel via
    /// <see cref="VesselController.PerformShipControllerActions"/> — the call the real
    /// input pipeline ultimately makes — because the round harness wires per-world
    /// SOAP channels, not the shared scene assets.
    ///
    /// The domain-game controllers re-arm every AIPilot at turn start
    /// (SetPlayersActive → StartPlayer → autopilot ON), so <see cref="Drive"/>
    /// re-asserts StopAIPilot whenever the autopilot has crept back on — the human
    /// keeps the stick across countdowns, kickoff re-parks, and turn resets.
    /// With no keys held (xvfb gate runs) the sticks read zero and the vessel flies
    /// straight — deterministic, but the GATES keep autopilot default anyway.
    /// </summary>
    public sealed class HumanPilotBridge
    {
        IPlayer _player;
        VesselController _vessel;
        IInputStatus _input;
        bool _prevSpace, _prevShift;

        public bool Active { get; private set; }

        /// <summary>Take the stick: autopilot off on Players[0], input sink captured.</summary>
        public void Attach(IRoundDriver round)
        {
            _player = round.Players[0];
            _vessel = (VesselController)_player.Vessel;
            _vessel.ToggleAIPilot(false);
            _input = _vessel.VesselStatus.InputStatus;
            Active = true;
        }

        /// <summary>Hand the stick back to the autopilot (menushell Tab toggle).</summary>
        public void Detach()
        {
            if (Active && _vessel != null)
                _vessel.ToggleAIPilot(true);
            Active = false;
            _player = null;
            _vessel = null;
            _input = null;
        }

        /// <summary>
        /// Per-frame keyboard poll → the vessel's InputStatus (call BEFORE the round
        /// steps so the tick consumes this frame's input, like any hardware pilot).
        /// </summary>
        public void Drive(IInputContext inputContext)
        {
            if (!Active || _input == null) return;

            // The controllers re-arm autopilot at turn start — take it back.
            if (_vessel.VesselStatus.AIPilot.AutoPilotEnabled)
                _vessel.ToggleAIPilot(false);

            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;
            bool space = false, shift = false;
            foreach (var keyboard in inputContext.Keyboards)
            {
                if (keyboard.IsKeyPressed(Key.W)) ly += 1f;
                if (keyboard.IsKeyPressed(Key.S)) ly -= 1f;
                if (keyboard.IsKeyPressed(Key.A)) lx -= 1f;
                if (keyboard.IsKeyPressed(Key.D)) lx += 1f;
                if (keyboard.IsKeyPressed(Key.Up)) ry += 1f;
                if (keyboard.IsKeyPressed(Key.Down)) ry -= 1f;
                if (keyboard.IsKeyPressed(Key.Left)) rx -= 1f;
                if (keyboard.IsKeyPressed(Key.Right)) rx += 1f;
                if (keyboard.IsKeyPressed(Key.Space)) space = true;
                if (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight)) shift = true;
            }

            // No second stick touched → mirror the left so single-hand play still flies.
            if (rx == 0f && ry == 0f && (lx != 0f || ly != 0f)) { rx = lx; ry = ly; }

            // The authentic dual-stick scheme (RaceWindow keyboard mapping):
            // XSum = yaw, YSum = pitch, YDiff = roll, XDiff = throttle.
            _input.XSum = Mathf.Clamp(rx + lx, -1f, 1f);
            _input.YSum = Mathf.Clamp(-(ry + ly), -1f, 1f);
            _input.YDiff = Mathf.Clamp(ry - ly, -1f, 1f);
            _input.XDiff = Mathf.Clamp01((Mathf.Clamp(rx - lx, -2f, 2f) + 2f) / 4f + (space ? 0.5f : 0.12f));
            _input.EasedLeftJoystickPosition = new Vector2(lx, ly);

            // Keyboard has no analog triggers — Shift is full single-tier drift.
            _input.LeftTriggerAnalog = shift ? 1f : 0f;
            _input.RightTriggerAnalog = 0f;

            if (shift != _prevShift)
            {
                if (shift) _vessel.PerformShipControllerActions(InputEvents.OnlyLeftStickAction);
                else _vessel.StopShipControllerActions(InputEvents.OnlyLeftStickAction);
                _prevShift = shift;
            }
            if (space != _prevSpace)
            {
                if (space) _vessel.PerformShipControllerActions(InputEvents.Button1Action);
                else _vessel.StopShipControllerActions(InputEvents.Button1Action);
                _prevSpace = space;
            }
        }
    }
}
