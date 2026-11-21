using System;
using System.Collections.Generic;
using CosmicShore.Engine.InputSystem.Controls;

namespace CosmicShore.Engine.InputSystem
{
    /// <summary>Physical key identifiers (original contract: UnityEngine.InputSystem.Key — values match, serialized fields depend on them).</summary>
    public enum Key
    {
        None = 0,
        Space = 1,
        Enter = 2,
        Tab = 3,
        Backquote = 4,
        Quote = 5,
        Semicolon = 6,
        Comma = 7,
        Period = 8,
        Slash = 9,
        Backslash = 10,
        LeftBracket = 11,
        RightBracket = 12,
        Minus = 13,
        Equals = 14,
        A = 15,
        B = 16,
        C = 17,
        D = 18,
        E = 19,
        F = 20,
        G = 21,
        H = 22,
        I = 23,
        J = 24,
        K = 25,
        L = 26,
        M = 27,
        N = 28,
        O = 29,
        P = 30,
        Q = 31,
        R = 32,
        S = 33,
        T = 34,
        U = 35,
        V = 36,
        W = 37,
        X = 38,
        Y = 39,
        Z = 40,
        Digit1 = 41,
        Digit2 = 42,
        Digit3 = 43,
        Digit4 = 44,
        Digit5 = 45,
        Digit6 = 46,
        Digit7 = 47,
        Digit8 = 48,
        Digit9 = 49,
        Digit0 = 50,
        LeftShift = 51,
        RightShift = 52,
        LeftAlt = 53,
        RightAlt = 54,
        LeftCtrl = 55,
        RightCtrl = 56,
        LeftMeta = 57,
        RightMeta = 58,
        ContextMenu = 59,
        Escape = 60,
        LeftArrow = 61,
        RightArrow = 62,
        UpArrow = 63,
        DownArrow = 64,
        Backspace = 65,
        PageDown = 66,
        PageUp = 67,
        Home = 68,
        End = 69,
        Insert = 70,
        Delete = 71,
        CapsLock = 72,
        NumLock = 73,
        PrintScreen = 74,
        ScrollLock = 75,
        Pause = 76,
        NumpadEnter = 77,
        NumpadDivide = 78,
        NumpadMultiply = 79,
        NumpadPlus = 80,
        NumpadMinus = 81,
        NumpadPeriod = 82,
        NumpadEquals = 83,
        Numpad0 = 84,
        Numpad1 = 85,
        Numpad2 = 86,
        Numpad3 = 87,
        Numpad4 = 88,
        Numpad5 = 89,
        Numpad6 = 90,
        Numpad7 = 91,
        Numpad8 = 92,
        Numpad9 = 93,
        F1 = 94,
        F2 = 95,
        F3 = 96,
        F4 = 97,
        F5 = 98,
        F6 = 99,
        F7 = 100,
        F8 = 101,
        F9 = 102,
        F10 = 103,
        F11 = 104,
        F12 = 105,
        OEM1 = 106,
        OEM2 = 107,
        OEM3 = 108,
        OEM4 = 109,
        OEM5 = 110,
        IMESelected = 111,
        LeftWindows = LeftMeta, LeftApple = LeftMeta, LeftCommand = LeftMeta,
        RightWindows = RightMeta, RightApple = RightMeta, RightCommand = RightMeta,
        AltGr = RightAlt,
    }

    /// <summary>The keyboard device. Keys are addressable by property or by <see cref="Key"/> index.</summary>
    public class Keyboard : InputDevice
    {
        public const int KeyCount = 111;
        public static Keyboard current { get; set; }
        public static readonly List<Keyboard> all = new();

        readonly KeyControl[] _keys = new KeyControl[112];

        public Keyboard() : base("Keyboard")
        {
            for (int i = 1; i < _keys.Length; i++)
                _keys[i] = AddChild(new KeyControl((Key)i, ((Key)i).ToString()));
            anyKey = AddChild(new AnyKeyControl(this) { name = "anyKey" });
            shiftKey = AddChild(new PairButtonControl(leftShiftKey, rightShiftKey) { name = "shift" });
            ctrlKey = AddChild(new PairButtonControl(leftCtrlKey, rightCtrlKey) { name = "ctrl" });
            altKey = AddChild(new PairButtonControl(leftAltKey, rightAltKey) { name = "alt" });
        }

        public KeyControl this[Key key] => key <= Key.None || (int)key >= _keys.Length ? throw new ArgumentOutOfRangeException(nameof(key)) : _keys[(int)key];
        public IReadOnlyList<KeyControl> allKeys => Array.AsReadOnly(_keys[1..]);
        public ButtonControl anyKey { get; }
        public ButtonControl shiftKey { get; }
        public ButtonControl ctrlKey { get; }
        public ButtonControl altKey { get; }

        /// <summary>Text typed this frame (original: onTextInput). The backend raises it per character.</summary>
        public event Action<char> onTextInput;
        public void RaiseTextInput(char c) => onTextInput?.Invoke(c);

        public override void MakeCurrent() { current = this; }
        internal override void OnAdded() { if (!all.Contains(this)) all.Add(this); }
        internal override void OnRemoved() { all.Remove(this); if (current == this) current = all.Count > 0 ? all[^1] : null; }

        public KeyControl spaceKey => _keys[1];
        public KeyControl enterKey => _keys[2];
        public KeyControl tabKey => _keys[3];
        public KeyControl backquoteKey => _keys[4];
        public KeyControl quoteKey => _keys[5];
        public KeyControl semicolonKey => _keys[6];
        public KeyControl commaKey => _keys[7];
        public KeyControl periodKey => _keys[8];
        public KeyControl slashKey => _keys[9];
        public KeyControl backslashKey => _keys[10];
        public KeyControl leftBracketKey => _keys[11];
        public KeyControl rightBracketKey => _keys[12];
        public KeyControl minusKey => _keys[13];
        public KeyControl equalsKey => _keys[14];
        public KeyControl aKey => _keys[15];
        public KeyControl bKey => _keys[16];
        public KeyControl cKey => _keys[17];
        public KeyControl dKey => _keys[18];
        public KeyControl eKey => _keys[19];
        public KeyControl fKey => _keys[20];
        public KeyControl gKey => _keys[21];
        public KeyControl hKey => _keys[22];
        public KeyControl iKey => _keys[23];
        public KeyControl jKey => _keys[24];
        public KeyControl kKey => _keys[25];
        public KeyControl lKey => _keys[26];
        public KeyControl mKey => _keys[27];
        public KeyControl nKey => _keys[28];
        public KeyControl oKey => _keys[29];
        public KeyControl pKey => _keys[30];
        public KeyControl qKey => _keys[31];
        public KeyControl rKey => _keys[32];
        public KeyControl sKey => _keys[33];
        public KeyControl tKey => _keys[34];
        public KeyControl uKey => _keys[35];
        public KeyControl vKey => _keys[36];
        public KeyControl wKey => _keys[37];
        public KeyControl xKey => _keys[38];
        public KeyControl yKey => _keys[39];
        public KeyControl zKey => _keys[40];
        public KeyControl digit1Key => _keys[41];
        public KeyControl digit2Key => _keys[42];
        public KeyControl digit3Key => _keys[43];
        public KeyControl digit4Key => _keys[44];
        public KeyControl digit5Key => _keys[45];
        public KeyControl digit6Key => _keys[46];
        public KeyControl digit7Key => _keys[47];
        public KeyControl digit8Key => _keys[48];
        public KeyControl digit9Key => _keys[49];
        public KeyControl digit0Key => _keys[50];
        public KeyControl leftShiftKey => _keys[51];
        public KeyControl rightShiftKey => _keys[52];
        public KeyControl leftAltKey => _keys[53];
        public KeyControl rightAltKey => _keys[54];
        public KeyControl leftCtrlKey => _keys[55];
        public KeyControl rightCtrlKey => _keys[56];
        public KeyControl leftMetaKey => _keys[57];
        public KeyControl rightMetaKey => _keys[58];
        public KeyControl contextMenuKey => _keys[59];
        public KeyControl escapeKey => _keys[60];
        public KeyControl leftArrowKey => _keys[61];
        public KeyControl rightArrowKey => _keys[62];
        public KeyControl upArrowKey => _keys[63];
        public KeyControl downArrowKey => _keys[64];
        public KeyControl backspaceKey => _keys[65];
        public KeyControl pageDownKey => _keys[66];
        public KeyControl pageUpKey => _keys[67];
        public KeyControl homeKey => _keys[68];
        public KeyControl endKey => _keys[69];
        public KeyControl insertKey => _keys[70];
        public KeyControl deleteKey => _keys[71];
        public KeyControl capsLockKey => _keys[72];
        public KeyControl numLockKey => _keys[73];
        public KeyControl printScreenKey => _keys[74];
        public KeyControl scrollLockKey => _keys[75];
        public KeyControl pauseKey => _keys[76];
        public KeyControl numpadEnterKey => _keys[77];
        public KeyControl numpadDivideKey => _keys[78];
        public KeyControl numpadMultiplyKey => _keys[79];
        public KeyControl numpadPlusKey => _keys[80];
        public KeyControl numpadMinusKey => _keys[81];
        public KeyControl numpadPeriodKey => _keys[82];
        public KeyControl numpadEqualsKey => _keys[83];
        public KeyControl numpad0Key => _keys[84];
        public KeyControl numpad1Key => _keys[85];
        public KeyControl numpad2Key => _keys[86];
        public KeyControl numpad3Key => _keys[87];
        public KeyControl numpad4Key => _keys[88];
        public KeyControl numpad5Key => _keys[89];
        public KeyControl numpad6Key => _keys[90];
        public KeyControl numpad7Key => _keys[91];
        public KeyControl numpad8Key => _keys[92];
        public KeyControl numpad9Key => _keys[93];
        public KeyControl f1Key => _keys[94];
        public KeyControl f2Key => _keys[95];
        public KeyControl f3Key => _keys[96];
        public KeyControl f4Key => _keys[97];
        public KeyControl f5Key => _keys[98];
        public KeyControl f6Key => _keys[99];
        public KeyControl f7Key => _keys[100];
        public KeyControl f8Key => _keys[101];
        public KeyControl f9Key => _keys[102];
        public KeyControl f10Key => _keys[103];
        public KeyControl f11Key => _keys[104];
        public KeyControl f12Key => _keys[105];
        public KeyControl oem1Key => _keys[106];
        public KeyControl oem2Key => _keys[107];
        public KeyControl oem3Key => _keys[108];
        public KeyControl oem4Key => _keys[109];
        public KeyControl oem5Key => _keys[110];
    }
}
