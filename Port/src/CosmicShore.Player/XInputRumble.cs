using System;
using System.Runtime.InteropServices;
using CosmicShore.Engine.InputSystem;

namespace CosmicShore.Player
{
    /// <summary>
    /// Controller rumble on Windows: GLFW (the player's window and input) has no rumble at all, so a
    /// pad's motor speeds go to XInput directly, as Unity's own XInputController does. Covers Xbox
    /// pads and anything Windows or Steam Input presents as one; a PlayStation pad read through
    /// DirectInput has no motors here unless Steam Input is translating it.
    /// </summary>
    static class XInputRumble
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Vibration { public ushort Left, Right; }

        [StructLayout(LayoutKind.Sequential)]
        struct State
        {
            public uint PacketNumber;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        }

        delegate uint SetStateFn(uint userIndex, ref Vibration vibration);
        delegate uint GetStateFn(uint userIndex, out State state);

        static SetStateFn s_set;
        static GetStateFn s_get;
        static bool s_tried;

        /// <summary>XInput is loaded (Windows, any of its three DLL versions).</summary>
        public static bool Available
        {
            get
            {
                if (s_tried) return s_set != null;
                s_tried = true;
                if (!OperatingSystem.IsWindows()) return false;
                foreach (var dll in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
                {
                    if (!NativeLibrary.TryLoad(dll, out var lib)) continue;
                    if (NativeLibrary.TryGetExport(lib, "XInputSetState", out var set) && NativeLibrary.TryGetExport(lib, "XInputGetState", out var get))
                    {
                        s_set = Marshal.GetDelegateForFunctionPointer<SetStateFn>(set);
                        s_get = Marshal.GetDelegateForFunctionPointer<GetStateFn>(get);
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The XInput slots (0-3) with a controller in them.</summary>
        public static int[] ConnectedSlots()
        {
            if (!Available) return Array.Empty<int>();
            var slots = new System.Collections.Generic.List<int>(4);
            for (uint i = 0; i < 4; i++)
                if (s_get(i, out _) == 0) slots.Add((int)i);
            return slots.ToArray();
        }

        /// <summary>Low frequency is the left (heavy) motor, high the right, as in Unity's XInputController.</summary>
        public static void Set(int slot, float low, float high)
        {
            if (!Available || slot is < 0 or > 3) return;
            var v = new Vibration { Left = XInputSlots.ToWord(low), Right = XInputSlots.ToWord(high) };
            s_set((uint)slot, ref v);
        }
    }
}
