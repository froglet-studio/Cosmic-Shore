using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which side the Rhino's plough is steered to while a trigger is held (<see cref="RhinoPloughSteerActionSO"/>).
    /// Both triggers held: the LAST one pulled wins, and letting it go hands the side back to the one still held.
    /// Neither held: <see cref="SteeredSide"/> is 0 and the plough throws each prism to the side it already sat on.
    ///
    /// <para>Per-vessel state, so it lives here and never on the shared action asset (vessel skill rule 1). Fed by
    /// the replicated press/release, so every peer steers alike (rule 52: no gesture, no analog depth).</para>
    /// </summary>
    public sealed class RhinoPloughSteerExecutor : ShipActionExecutorBase
    {
        bool _rightHeld;
        bool _leftHeld;
        float _lastPressed;

        /// <summary>+1 steered right, -1 steered left, 0 not steered.</summary>
        public float SteeredSide => RhinoPloughSteering.Side(_rightHeld, _leftHeld, _lastPressed);

        public override void Initialize(IVesselStatus shipStatus) => ReleaseAll();

        public void Press(float sign)
        {
            if (sign > 0f) _rightHeld = true;
            else if (sign < 0f) _leftHeld = true;
            if (sign != 0f) _lastPressed = Mathf.Sign(sign);
        }

        public void Release(float sign)
        {
            if (sign > 0f) _rightHeld = false;
            else if (sign < 0f) _leftHeld = false;
        }

        void OnDisable() => ReleaseAll();

        void ReleaseAll()
        {
            _rightHeld = false;
            _leftHeld = false;
            _lastPressed = 0f;
        }
    }

    /// <summary>The steer rule, pure so <c>RhinoPloughTests</c> can hold it.</summary>
    public static class RhinoPloughSteering
    {
        public static float Side(bool rightHeld, bool leftHeld, float lastPressed)
        {
            if (rightHeld && leftHeld) return lastPressed != 0f ? Mathf.Sign(lastPressed) : 1f;
            if (rightHeld) return 1f;
            if (leftHeld) return -1f;
            return 0f;
        }
    }
}
