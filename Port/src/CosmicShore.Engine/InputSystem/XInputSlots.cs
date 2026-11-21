using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Engine.InputSystem
{
    /// <summary>
    /// Which XInput user slot (0-3) each GLFW joystick is, so a pad's rumble reaches the right
    /// controller on Windows. GLFW reads Xbox-type pads through XInput but does not say which slot:
    /// it marks them with a GUID beginning <c>78696e707574</c> ("xinput" in hex) and gives them joystick
    /// ids in the order it found the slots. So the XInput joysticks, in id order, are the connected
    /// slots in slot order. Pure, so it is tested without a controller.
    /// </summary>
    public static class XInputSlots
    {
        public const string GuidPrefix = "78696e707574";

        public static bool IsXInput(string guid) => guid != null && guid.StartsWith(GuidPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>joystick id → XInput slot, for every XInput joystick that has a connected slot to match.</summary>
        public static Dictionary<int, int> Assign(IEnumerable<(int Joystick, string Guid)> joysticks, IEnumerable<int> connectedSlots)
        {
            var map = new Dictionary<int, int>();
            var slots = connectedSlots.Where(s => s is >= 0 and < 4).Distinct().OrderBy(s => s).ToList();
            int next = 0;
            foreach (var (joystick, _) in joysticks.Where(j => IsXInput(j.Guid)).OrderBy(j => j.Joystick))
            {
                if (next >= slots.Count) break;
                map[joystick] = slots[next++];
            }
            return map;
        }

        /// <summary>A motor speed in [0, 1] as XInput's 16-bit value.</summary>
        public static ushort ToWord(float speed) => (ushort)Math.Round(Math.Clamp(speed, 0f, 1f) * ushort.MaxValue);
    }
}
