using System;
using System.Collections.Generic;
using CosmicShore.Engine.InputSystem.Controls;
using CosmicShore.Engine.InputSystem.Utilities;

namespace CosmicShore.Engine.InputSystem
{
    // ─────────────────────────────────────────────────────────────────────────
    // The device/control model (original contract: UnityEngine.InputSystem).
    //
    // A platform backend (the client's Silk.NET input) or a test writes RAW control
    // values at any time; InputSystem.Update — run by the GameLoop once per frame,
    // before any Update — commits them and derives the per-frame edges
    // (wasPressedThisFrame / wasReleasedThisFrame), then evaluates enabled actions.
    // Legacy ported code that pokes isPressed / wasPressedThisFrame directly keeps
    // working: the setters write the committed state the readers see.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Base of every control and device.</summary>
    public abstract class InputControl
    {
        readonly List<InputControl> _children = new();

        public string name { get; set; }
        public string displayName { get => _displayName ?? name; set => _displayName = value; }
        string _displayName;
        public string shortDisplayName => displayName;
        public InputControl parent { get; internal set; }
        public InputDevice device { get; internal set; }
        public ReadOnlyArray<InputControl> children => new(_children.ToArray());
        public bool synthetic { get; protected set; }
        public bool noisy { get; protected set; }

        /// <summary>The layout the control was built from ("Button", "Stick", "Gamepad" …).</summary>
        public virtual string layout
        {
            get
            {
                var n = GetType().Name;
                return n.EndsWith("Control", System.StringComparison.Ordinal) && n.Length > 7 ? n.Substring(0, n.Length - 7) : n;
            }
        }

        readonly List<InternedString> _usages = new();
        /// <summary>Usage tags (e.g. "PrimaryAction", "Submit"); on a device, its role ("LeftHand").</summary>
        public ReadOnlyArray<InternedString> usages => new(_usages.ToArray());
        public void AddUsage(string usage) { var u = new InternedString(usage); if (!_usages.Contains(u)) _usages.Add(u); }

        /// <summary>Control path, e.g. <c>/Keyboard/w</c>.</summary>
        public string path => parent == null ? "/" + name : parent.path + "/" + name;

        protected T AddChild<T>(T child) where T : InputControl
        {
            child.parent = this;
            _children.Add(child);
            child.AttachTo(device ?? this as InputDevice);
            return child;
        }

        internal void AttachTo(InputDevice d)
        {
            device = d;
            foreach (var c in _children) c.AttachTo(d);
        }

        internal IEnumerable<InputControl> Descendants()
        {
            foreach (var c in _children)
            {
                yield return c;
                foreach (var g in c.Descendants()) yield return g;
            }
        }

        internal IReadOnlyList<InputControl> ChildList => _children;

        public abstract object ReadValueAsObject();
        public virtual float EvaluateMagnitude() => 0f;
        public bool IsActuated(float threshold = 0f) => EvaluateMagnitude() > threshold;
        public Type valueType => ReadValueAsObject()?.GetType();

        /// <summary>Finds a descendant by relative path (<c>leftStick/x</c>, case-insensitive).</summary>
        public InputControl this[string relativePath] => TryGetChildControl(relativePath) ?? throw new KeyNotFoundException(relativePath);

        public InputControl TryGetChildControl(string relativePath)
        {
            InputControl cur = this;
            foreach (var part in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                InputControl next = null;
                foreach (var c in cur._children)
                    if (string.Equals(c.name, part, StringComparison.OrdinalIgnoreCase) || c.MatchesAlias(part)) { next = c; break; }
                if (next == null) return null;
                cur = next;
            }
            return cur;
        }

        internal List<string> aliases;
        internal bool MatchesAlias(string s)
        {
            if (aliases == null) return false;
            foreach (var a in aliases) if (string.Equals(a, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal T WithAlias<T>(params string[] names) where T : InputControl
        {
            (aliases ??= new()).AddRange(names);
            return (T)this;
        }

        /// <summary>Frame commit: raw → current, previous remembered (called by InputSystem.Update).</summary>
        internal virtual void Commit() { foreach (var c in _children) c.Commit(); }

        public override string ToString() => $"{GetType().Name}:{path}";
    }

    public abstract class InputControl<TValue> : InputControl where TValue : struct
    {
        public abstract TValue ReadValue();
        public virtual TValue ReadValueFromPreviousFrame() => ReadValue();
        public TValue ReadUnprocessedValue() => ReadValue();
        public TValue ReadDefaultValue() => default;
        public override object ReadValueAsObject() => ReadValue();
    }

    /// <summary>A single float axis.</summary>
    public class AxisControl : InputControl<float>
    {
        protected float _raw, _current, _previous;
        public float clampMin = float.NegativeInfinity, clampMax = float.PositiveInfinity;

        /// <summary>Current value; setting it writes both the raw and committed value (backend / test poke).</summary>
        public virtual float value
        {
            get => _current;
            set { _raw = value; _current = value; }
        }

        /// <summary>Backend write: takes effect at the next frame commit.</summary>
        public void SetRaw(float v) => _raw = v;

        public override float ReadValue() => _current;
        public override float ReadValueFromPreviousFrame() => _previous;
        public override float EvaluateMagnitude() => MathF.Abs(_current);

        internal override void Commit()
        {
            _previous = _current;
            _current = Math.Clamp(_raw, clampMin, clampMax);
            base.Commit();
        }
    }

    /// <summary>A button: an axis with a press point and frame edges.</summary>
    public class ButtonControl : AxisControl
    {
        bool _pressed, _pressedThisFrame, _releasedThisFrame;

        public float pressPoint = -1f;
        public static float s_GlobalDefaultButtonPressPoint = 0.5f;
        public float pressPointOrDefault => pressPoint > 0f ? pressPoint : s_GlobalDefaultButtonPressPoint;

        public override float value
        {
            get => _current;
            set { _raw = value; _current = value; _pressed = value >= pressPointOrDefault; }
        }

        public bool isPressed
        {
            get => _pressed;
            set { _pressed = value; _raw = _current = value ? 1f : 0f; }
        }

        public bool wasPressedThisFrame { get => _pressedThisFrame; set => _pressedThisFrame = value; }
        public bool wasReleasedThisFrame { get => _releasedThisFrame; set => _releasedThisFrame = value; }
        public bool IsValueConsideredPressed(float v) => v >= pressPointOrDefault;
        public bool IsPressed() => _pressed;
        public bool WasPressedThisFrame() => _pressedThisFrame;
        public bool WasReleasedThisFrame() => _releasedThisFrame;

        /// <summary>Backend write of a digital state (takes effect at the next commit).</summary>
        public void SetRaw(bool down) => _raw = down ? 1f : 0f;

        internal override void Commit()
        {
            bool was = _pressed;
            base.Commit();
            _pressed = RecomputeCurrent() >= pressPointOrDefault;
            _pressedThisFrame = _pressed && !was;
            _releasedThisFrame = !_pressed && was;
        }

        /// <summary>Derived buttons (any-key, shift, stick directions) recompute from their sources.</summary>
        protected virtual float RecomputeCurrent() => _current;
    }

    public class KeyControl : ButtonControl
    {
        public Key keyCode { get; }
        public int scanCode { get; }
        public KeyControl(Key key, string keyName)
        {
            keyCode = key;
            name = char.ToLowerInvariant(keyName[0]) + keyName[1..];
            displayName = keyName;
        }
    }

    /// <summary>A button derived from other buttons (pressed if any source is).</summary>
    public class DerivedButtonControl : ButtonControl
    {
        readonly Func<float> _source;
        public DerivedButtonControl(Func<float> source) { _source = source; synthetic = true; }
        protected override float RecomputeCurrent() { _current = _source(); return _current; }
    }

    public sealed class AnyKeyControl : DerivedButtonControl
    {
        public AnyKeyControl(Keyboard kb) : base(() =>
        {
            foreach (var k in kb.allKeys) if (k.isPressed) return 1f;
            return 0f;
        }) { }
    }

    public sealed class PairButtonControl : DerivedButtonControl
    {
        public PairButtonControl(ButtonControl a, ButtonControl b) : base(() => MathF.Max(a.value, b.value)) { }
    }

    public class Vector2Control : InputControl<Vector2>
    {
        public AxisControl x { get; }
        public AxisControl y { get; }

        public Vector2Control()
        {
            x = AddChild(new AxisControl { name = "x" });
            y = AddChild(new AxisControl { name = "y" });
        }

        /// <summary>Current value; setting writes both components (backend / test poke).</summary>
        public Vector2 value
        {
            get => new(x.value, y.value);
            set { x.value = value.x; y.value = value.y; }
        }

        public void SetRaw(Vector2 v) { x.SetRaw(v.x); y.SetRaw(v.y); }

        public override Vector2 ReadValue() => new(x.ReadValue(), y.ReadValue());
        public override Vector2 ReadValueFromPreviousFrame() => new(x.ReadValueFromPreviousFrame(), y.ReadValueFromPreviousFrame());
        public override float EvaluateMagnitude() => ReadValue().magnitude;
    }

    /// <summary>Thumbstick: a Vector2 with derived directional buttons.</summary>
    public class StickControl : Vector2Control
    {
        public ButtonControl up { get; }
        public ButtonControl down { get; }
        public ButtonControl left { get; }
        public ButtonControl right { get; }

        public StickControl()
        {
            up = AddChild(new DerivedButtonControl(() => MathF.Max(0f, y.value)) { name = "up" });
            down = AddChild(new DerivedButtonControl(() => MathF.Max(0f, -y.value)) { name = "down" });
            left = AddChild(new DerivedButtonControl(() => MathF.Max(0f, -x.value)) { name = "left" });
            right = AddChild(new DerivedButtonControl(() => MathF.Max(0f, x.value)) { name = "right" });
        }
    }

    /// <summary>Four-way pad; its Vector2 value is composed from the buttons.</summary>
    public class DpadControl : InputControl<Vector2>
    {
        public ButtonControl up { get; }
        public ButtonControl down { get; }
        public ButtonControl left { get; }
        public ButtonControl right { get; }
        public AxisControl x { get; }
        public AxisControl y { get; }

        public DpadControl()
        {
            up = AddChild(new ButtonControl { name = "up" });
            down = AddChild(new ButtonControl { name = "down" });
            left = AddChild(new ButtonControl { name = "left" });
            right = AddChild(new ButtonControl { name = "right" });
            x = AddChild(new DerivedButtonControl(() => right.value - left.value) { name = "x" });
            y = AddChild(new DerivedButtonControl(() => up.value - down.value) { name = "y" });
        }

        public override Vector2 ReadValue() => new(right.value - left.value, up.value - down.value);
        public override float EvaluateMagnitude() => ReadValue().magnitude;
    }

    public class Vector3Control : InputControl<Vector3>
    {
        public AxisControl x { get; }
        public AxisControl y { get; }
        public AxisControl z { get; }
        public Vector3Control()
        {
            x = AddChild(new AxisControl { name = "x" });
            y = AddChild(new AxisControl { name = "y" });
            z = AddChild(new AxisControl { name = "z" });
        }
        public Vector3 value { get => new(x.value, y.value, z.value); set { x.value = value.x; y.value = value.y; z.value = value.z; } }
        public override Vector3 ReadValue() => value;
        public override float EvaluateMagnitude() => value.magnitude;
    }

    public class QuaternionControl : InputControl<Quaternion>
    {
        public Quaternion value = Quaternion.identity;
        public override Quaternion ReadValue() => value;
    }

    public class IntegerControl : InputControl<int>
    {
        public int value;
        public override int ReadValue() => value;
        public override float EvaluateMagnitude() => value;
    }

    public class DoubleControl : InputControl<double>
    {
        public double value;
        public override double ReadValue() => value;
    }

    // ── Devices ──────────────────────────────────────────────────────────────

    [Serializable]
    public struct InputDeviceDescription
    {
        public string interfaceName;
        public string deviceClass;
        public string manufacturer;
        public string product;
        public string serial;
        public string version;
        public string capabilities;
        public bool empty => string.IsNullOrEmpty(interfaceName) && string.IsNullOrEmpty(product) && string.IsNullOrEmpty(manufacturer);
        public override string ToString() => $"{manufacturer} {product} ({interfaceName})";
    }

    public enum InputDeviceChange
    {
        Added, Removed, Disconnected, Reconnected, Enabled, Disabled, UsageChanged, ConfigurationChanged,
        SoftReset, HardReset,
    }

    public abstract class InputDevice : InputControl
    {
        static int s_NextId = 1;

        public int deviceId { get; internal set; }
        public InputDeviceDescription description { get; set; }
        public bool added { get; internal set; }
        public bool enabled { get; internal set; } = true;
        public bool native { get; internal set; }
        public bool remote => false;
        public bool wasUpdatedThisFrame { get; internal set; }
        public double lastUpdateTime { get; internal set; }
        public bool canRunInBackground => false;

        public ReadOnlyArray<InputControl> allControls => new(new List<InputControl>(Descendants()).ToArray());

        protected InputDevice(string deviceName)
        {
            name = deviceName;
            deviceId = s_NextId++;
            device = this;
            description = new InputDeviceDescription { product = deviceName, interfaceName = "CosmicShore" };
        }

        public override object ReadValueAsObject() => null;
        public virtual void MakeCurrent() { }
        internal virtual void OnAdded() { }
        internal virtual void OnRemoved() { }
    }

    public class Pointer : InputDevice
    {
        public static Pointer current { get; set; }
        public Vector2Control position { get; }
        public Vector2Control delta { get; }
        public Vector2Control radius { get; }
        public AxisControl pressure { get; }
        public ButtonControl press { get; }

        protected Pointer(string deviceName) : base(deviceName)
        {
            position = AddChild(new Vector2Control { name = "position" });
            delta = AddChild(new Vector2Control { name = "delta" });
            radius = AddChild(new Vector2Control { name = "radius" });
            pressure = AddChild(new AxisControl { name = "pressure" });
            press = AddChild(new ButtonControl { name = "press" });
        }

        public Pointer() : this("Pointer") { }
        public override void MakeCurrent() => current = this;
    }

    public class Mouse : Pointer
    {
        public static new Mouse current { get; set; }
        public static readonly List<Mouse> all = new();

        public ButtonControl leftButton { get; }
        public ButtonControl rightButton { get; }
        public ButtonControl middleButton { get; }
        public ButtonControl forwardButton { get; }
        public ButtonControl backButton { get; }
        public Vector2Control scroll { get; }
        public IntegerControl clickCount { get; }

        public Mouse() : base("Mouse")
        {
            leftButton = AddChild(new ButtonControl { name = "leftButton" });
            rightButton = AddChild(new ButtonControl { name = "rightButton" });
            middleButton = AddChild(new ButtonControl { name = "middleButton" });
            forwardButton = AddChild(new ButtonControl { name = "forwardButton" });
            backButton = AddChild(new ButtonControl { name = "backButton" });
            scroll = AddChild(new Vector2Control { name = "scroll" });
            clickCount = AddChild(new IntegerControl { name = "clickCount" });
            displayName = "Mouse";
        }

        public void WarpCursorPosition(Vector2 p) { position.value = p; }
        public override void MakeCurrent() { base.MakeCurrent(); current = this; }
        internal override void OnAdded() { if (!all.Contains(this)) all.Add(this); }
        internal override void OnRemoved() { all.Remove(this); if (current == this) current = all.Count > 0 ? all[^1] : null; }

        /// <summary>Delta and scroll are per-frame accumulations: a frame with no motion reads zero.</summary>
        internal override void Commit()
        {
            base.Commit();
            delta.SetRaw(Vector2.zero);
            scroll.SetRaw(Vector2.zero);
        }
    }

    public enum GamepadButton
    {
        DpadUp = 0, DpadDown = 1, DpadLeft = 2, DpadRight = 3,
        North = 4, East = 5, South = 6, West = 7,
        LeftStick = 8, RightStick = 9, LeftShoulder = 10, RightShoulder = 11,
        Start = 12, Select = 13, LeftTrigger = 32, RightTrigger = 33,
        X = West, Y = North, A = South, B = East,
        Cross = South, Square = West, Triangle = North, Circle = East,
    }

    public class Gamepad : InputDevice
    {
        public static Gamepad current { get; set; }
        static readonly List<Gamepad> s_All = new();
        public static ReadOnlyArray<Gamepad> all => new(s_All.ToArray());

        public StickControl leftStick { get; }
        public StickControl rightStick { get; }
        public DpadControl dpad { get; }
        public ButtonControl leftTrigger { get; }
        public ButtonControl rightTrigger { get; }
        public ButtonControl buttonSouth { get; }
        public ButtonControl buttonNorth { get; }
        public ButtonControl buttonEast { get; }
        public ButtonControl buttonWest { get; }
        public ButtonControl leftShoulder { get; }
        public ButtonControl rightShoulder { get; }
        public ButtonControl leftStickButton { get; }
        public ButtonControl rightStickButton { get; }
        public ButtonControl startButton { get; }
        public ButtonControl selectButton { get; }

        public ButtonControl aButton => buttonSouth;
        public ButtonControl bButton => buttonEast;
        public ButtonControl xButton => buttonWest;
        public ButtonControl yButton => buttonNorth;
        public ButtonControl crossButton => buttonSouth;
        public ButtonControl circleButton => buttonEast;
        public ButtonControl squareButton => buttonWest;
        public ButtonControl triangleButton => buttonNorth;

        /// <summary>Last rumble speeds sent (port observability; no hardware rumble yet).</summary>
        public (float low, float high) MotorSpeeds { get; private set; }

        public Gamepad() : this("Gamepad") { }

        protected Gamepad(string deviceName) : base(deviceName)
        {
            leftStick = AddChild(new StickControl { name = "leftStick" });
            rightStick = AddChild(new StickControl { name = "rightStick" });
            dpad = AddChild(new DpadControl { name = "dpad" });
            leftTrigger = AddChild(new ButtonControl { name = "leftTrigger" });
            rightTrigger = AddChild(new ButtonControl { name = "rightTrigger" });
            buttonSouth = AddChild(new ButtonControl { name = "buttonSouth" }.WithAlias<ButtonControl>("a", "cross"));
            buttonNorth = AddChild(new ButtonControl { name = "buttonNorth" }.WithAlias<ButtonControl>("y", "triangle"));
            buttonEast = AddChild(new ButtonControl { name = "buttonEast" }.WithAlias<ButtonControl>("b", "circle"));
            buttonWest = AddChild(new ButtonControl { name = "buttonWest" }.WithAlias<ButtonControl>("x", "square"));
            leftShoulder = AddChild(new ButtonControl { name = "leftShoulder" });
            rightShoulder = AddChild(new ButtonControl { name = "rightShoulder" });
            leftStickButton = AddChild(new ButtonControl { name = "leftStickPress" });
            rightStickButton = AddChild(new ButtonControl { name = "rightStickPress" });
            startButton = AddChild(new ButtonControl { name = "start" });
            selectButton = AddChild(new ButtonControl { name = "select" });
        }

        public ButtonControl this[GamepadButton button] => button switch
        {
            GamepadButton.DpadUp => dpad.up,
            GamepadButton.DpadDown => dpad.down,
            GamepadButton.DpadLeft => dpad.left,
            GamepadButton.DpadRight => dpad.right,
            GamepadButton.North => buttonNorth,
            GamepadButton.East => buttonEast,
            GamepadButton.South => buttonSouth,
            GamepadButton.West => buttonWest,
            GamepadButton.LeftStick => leftStickButton,
            GamepadButton.RightStick => rightStickButton,
            GamepadButton.LeftShoulder => leftShoulder,
            GamepadButton.RightShoulder => rightShoulder,
            GamepadButton.Start => startButton,
            GamepadButton.Select => selectButton,
            GamepadButton.LeftTrigger => leftTrigger,
            GamepadButton.RightTrigger => rightTrigger,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };

        public void SetMotorSpeeds(float lowFrequency, float highFrequency) => MotorSpeeds = (lowFrequency, highFrequency);
        public void PauseHaptics() { }
        public void ResumeHaptics() { }
        public void ResetHaptics() => MotorSpeeds = (0f, 0f);

        public override void MakeCurrent() => current = this;
        internal override void OnAdded() { if (!s_All.Contains(this)) s_All.Add(this); }
        internal override void OnRemoved() { s_All.Remove(this); if (current == this) current = s_All.Count > 0 ? s_All[^1] : null; }
    }

    public class Touchscreen : Pointer
    {
        public static new Touchscreen current { get; set; }
        public TouchControl primaryTouch { get; }
        readonly TouchControl[] _touches;
        public ReadOnlyArray<TouchControl> touches => new(_touches);

        public Touchscreen() : base("Touchscreen")
        {
            primaryTouch = AddChild(new TouchControl { name = "primaryTouch" });
            _touches = new TouchControl[10];
            for (int i = 0; i < _touches.Length; i++) _touches[i] = AddChild(new TouchControl { name = "touch" + i });
        }

        public override void MakeCurrent() { base.MakeCurrent(); current = this; }
    }

    public abstract class Sensor : InputDevice
    {
        protected Sensor(string n) : base(n) { }
        public float samplingFrequency { get; set; }
    }

    public class Accelerometer : Sensor
    {
        public static Accelerometer current { get; set; }
        public Vector3Control acceleration { get; }
        public Accelerometer() : base("Accelerometer") { acceleration = AddChild(new Vector3Control { name = "acceleration" }); }
        public override void MakeCurrent() => current = this;
    }

    public class AttitudeSensor : Sensor
    {
        public static AttitudeSensor current { get; set; }
        public QuaternionControl attitude { get; }
        public AttitudeSensor() : base("AttitudeSensor") { attitude = AddChild(new QuaternionControl { name = "attitude" }); }
        public override void MakeCurrent() => current = this;
    }

    public class Gyroscope : Sensor
    {
        public static Gyroscope current { get; set; }
        public Vector3Control angularVelocity { get; }
        public Gyroscope() : base("Gyroscope") { angularVelocity = AddChild(new Vector3Control { name = "angularVelocity" }); }
        public override void MakeCurrent() => current = this;
    }

    public enum TouchPhase { None = 0, Began = 1, Moved = 2, Ended = 3, Canceled = 4, Stationary = 5 }

    /// <summary>The input-system root (original contract: UnityEngine.InputSystem.InputSystem).</summary>
    public static class InputSystem
    {
        static readonly List<InputDevice> s_Devices = new();

        public static ReadOnlyArray<InputDevice> devices => new(s_Devices.ToArray());
        public static ReadOnlyArray<InputDevice> disconnectedDevices => new(Array.Empty<InputDevice>());

        public static event Action<InputDevice, InputDeviceChange> onDeviceChange;
        public static event Action onBeforeUpdate;
        public static event Action onAfterUpdate;

        public static InputSettings settings { get; set; } = new InputSettings();
        public static string version => "1.14.2-cosmicshore";

        public static T AddDevice<T>(string name = null) where T : InputDevice, new()
        {
            var d = new T();
            if (name != null) d.name = name;
            AddDevice(d);
            return d;
        }

        public static void AddDevice(InputDevice device)
        {
            if (device == null || s_Devices.Contains(device)) return;
            s_Devices.Add(device);
            device.added = true;
            device.OnAdded();
            device.MakeCurrent();
            onDeviceChange?.Invoke(device, InputDeviceChange.Added);
        }

        public static void RemoveDevice(InputDevice device)
        {
            if (device == null || !s_Devices.Remove(device)) return;
            device.added = false;
            device.OnRemoved();
            onDeviceChange?.Invoke(device, InputDeviceChange.Removed);
        }

        public static void EnableDevice(InputDevice device)
        {
            if (device == null || device.enabled) return;
            device.enabled = true;
            onDeviceChange?.Invoke(device, InputDeviceChange.Enabled);
        }

        public static void DisableDevice(InputDevice device, bool keepSendingEvents = false)
        {
            if (device == null || !device.enabled) return;
            device.enabled = false;
            onDeviceChange?.Invoke(device, InputDeviceChange.Disabled);
        }

        public static InputDevice GetDeviceById(int id)
        {
            foreach (var d in s_Devices) if (d.deviceId == id) return d;
            return null;
        }

        public static TDevice GetDevice<TDevice>() where TDevice : InputDevice
        {
            TDevice best = null;
            foreach (var d in s_Devices) if (d is TDevice t) best = t;
            return best;
        }

        /// <summary>Resolves a control path like <c>&lt;Gamepad&gt;/leftStick/x</c> against the added devices.</summary>
        public static List<InputControl> FindControls(string path)
        {
            var result = new List<InputControl>();
            foreach (var d in AllKnownDevices()) InputControlPath.Match(path, d, result);
            return result;
        }

        public static void ResetDevice(InputDevice device, bool alsoResetDontResetControls = false) { }
        public static void ResetHaptics() { foreach (var d in s_Devices) if (d is Gamepad g) g.ResetHaptics(); }
        public static void PauseHaptics() { }
        public static void ResumeHaptics() { }

        static readonly List<InputDevice> s_Frame = new();

        /// <summary>Devices the frame commit touches: every added device plus any legacy-assigned <c>current</c>.</summary>
        internal static List<InputDevice> AllKnownDevices()
        {
            s_Frame.Clear();
            s_Frame.AddRange(s_Devices);
            void Add(InputDevice d) { if (d != null && !s_Frame.Contains(d)) s_Frame.Add(d); }
            Add(Keyboard.current); Add(Mouse.current); Add(Gamepad.current); Add(Touchscreen.current);
            Add(Pointer.current); Add(Accelerometer.current); Add(AttitudeSensor.current);
            return s_Frame;
        }

        /// <summary>One frame of input: commit raw device state, derive edges, evaluate actions.</summary>
        public static void Update()
        {
            onBeforeUpdate?.Invoke();
            foreach (var d in AllKnownDevices().ToArray())
                if (d.enabled) d.Commit();
            InputActionState.EvaluateAll();
            onAfterUpdate?.Invoke();
        }

        /// <summary>Test isolation: removes every device and clears the current pointers.</summary>
        public static void ResetForTests()
        {
            foreach (var d in s_Devices.ToArray()) RemoveDevice(d);
            Keyboard.current = null; Mouse.current = null; Gamepad.current = null;
            Touchscreen.current = null; Pointer.current = null;
            Accelerometer.current = null; AttitudeSensor.current = null;
            InputActionState.Reset();
        }
    }

    public class InputSettings : ScriptableObject
    {
        public float defaultDeadzoneMin = 0.125f;
        public float defaultDeadzoneMax = 0.925f;
        public float defaultButtonPressPoint = 0.5f;
        public bool backgroundBehavior;
    }
}

namespace CosmicShore.Engine.InputSystem.Controls
{

    /// <summary>One touch slot on a touchscreen.</summary>
    public class TouchControl : InputControl<TouchState>
    {
        public ButtonControl press { get; }
        public Vector2Control position { get; }
        public Vector2Control delta { get; }
        public Vector2Control startPosition { get; }
        public IntegerControl touchId { get; }
        public AxisControl pressure { get; }
        public TouchPhase phase { get; set; }
        public bool isInProgress => phase is TouchPhase.Began or TouchPhase.Moved or TouchPhase.Stationary;

        public TouchControl()
        {
            press = AddChild(new ButtonControl { name = "press" });
            position = AddChild(new Vector2Control { name = "position" });
            delta = AddChild(new Vector2Control { name = "delta" });
            startPosition = AddChild(new Vector2Control { name = "startPosition" });
            touchId = AddChild(new IntegerControl { name = "touchId" });
            pressure = AddChild(new AxisControl { name = "pressure" });
        }

        public override TouchState ReadValue() => new() { position = position.value, phase = phase, touchId = touchId.value };
    }

    public struct TouchState
    {
        public Vector2 position;
        public TouchPhase phase;
        public int touchId;
    }
}

namespace CosmicShore.Engine.InputSystem.EnhancedTouch
{
    /// <summary>Enhanced-touch facade: active touches are what the touch backend reports.</summary>
    public static class EnhancedTouchSupport
    {
        public static bool enabled { get; private set; }
        public static void Enable() => enabled = true;
        public static void Disable() => enabled = false;
    }

    public struct Touch
    {
        public static readonly System.Collections.Generic.List<Touch> activeTouches = new();
        public Vector2 screenPosition;
        public Vector2 startScreenPosition;
        public Vector2 delta;
        public CosmicShore.Engine.InputSystem.TouchPhase phase;
        public int touchId;
        public double startTime;
        public double time;
        public bool isInProgress => phase is TouchPhase.Began or TouchPhase.Moved or TouchPhase.Stationary;
        public bool began => phase == TouchPhase.Began;
        public bool ended => phase == TouchPhase.Ended;
    }
}

namespace CosmicShore.Engine.InputSystem.DualShock
{
    /// <summary>PlayStation pad family (original: DualShockGamepad) — lets `is DualShockGamepad` pick PS glyphs.</summary>
    public class DualShockGamepad : Gamepad
    {
        public DualShockGamepad() : base("DualShockGamepad") { }
        protected DualShockGamepad(string n) : base(n) { }
        public void SetLightBarColor(Color color) { }
    }

    public class DualShock4GamepadHID : DualShockGamepad { public DualShock4GamepadHID() : base("DualShock4GamepadHID") { } }
    public class DualSenseGamepadHID : DualShockGamepad { public DualSenseGamepadHID() : base("DualSenseGamepadHID") { } }
}

namespace CosmicShore.Engine.InputSystem.XInput
{
    /// <summary>Xbox pad family (original: XInputController).</summary>
    public class XInputController : Gamepad
    {
        public XInputController() : base("XInputController") { }
        protected XInputController(string n) : base(n) { }
    }

    public class XInputControllerWindows : XInputController { public XInputControllerWindows() : base("XInputControllerWindows") { } }
}

namespace CosmicShore.Engine.InputSystem.LowLevel
{
    /// <summary>Raw gamepad state layout (original: GamepadState) — button bits + stick/trigger values.</summary>
    public struct GamepadState
    {
        public uint buttons;
        public Vector2 leftStick, rightStick;
        public float leftTrigger, rightTrigger;
        public GamepadState WithButton(GamepadButton button, bool value = true)
        {
            uint bit = 1u << (int)button;
            buttons = value ? buttons | bit : buttons & ~bit;
            return this;
        }
    }

    public static class InputState
    {
        public static double currentTime => Time.realtimeSinceStartupAsDouble;
    }
}
