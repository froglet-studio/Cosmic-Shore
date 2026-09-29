using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CosmicShore.Engine.InputSystem.Utilities;

namespace CosmicShore.Engine.InputSystem
{
    // ─────────────────────────────────────────────────────────────────────────
    // Actions (original contract: UnityEngine.InputSystem InputAction / Map / Asset).
    // Behavior-level re-implementation: bindings resolve by control path against the
    // live devices; composites (2DVector/Dpad, 1DAxis, OneModifier, TwoModifiers)
    // compose part values; a handful of the stock processors apply; phases follow the
    // documented default interactions — Button: started+performed on press, canceled
    // on release; Value: started+performed on actuation, performed on every change,
    // canceled on return to default; PassThrough: performed on every change.
    // Evaluation happens once per frame inside InputSystem.Update.
    // ─────────────────────────────────────────────────────────────────────────

    public enum InputActionType { Value = 0, Button = 1, PassThrough = 2 }

    public enum InputActionPhase { Disabled = 0, Waiting = 1, Started = 2, Performed = 3, Canceled = 4 }

    [Serializable]
    public struct InputBinding : IEquatable<InputBinding>
    {
        public const char Separator = ';';

        public string name;
        public string id;
        public string path;
        public string overridePath;
        public string interactions;
        public string overrideInteractions;
        public string processors;
        public string overrideProcessors;
        public string groups;
        public string action;
        public bool isComposite;
        public bool isPartOfComposite;

        public InputBinding(string path, string action = null, string groups = null, string processors = null,
            string interactions = null, string name = null)
        {
            this.path = path; this.action = action; this.groups = groups; this.processors = processors;
            this.interactions = interactions; this.name = name;
            id = Guid.NewGuid().ToString(); overridePath = null; overrideInteractions = null; overrideProcessors = null;
            isComposite = false; isPartOfComposite = false;
        }

        public string effectivePath => overridePath ?? path;
        public string effectiveInteractions => overrideInteractions ?? interactions;
        public string effectiveProcessors => overrideProcessors ?? processors;
        public bool hasOverrides => overridePath != null || overrideInteractions != null || overrideProcessors != null;

        public static InputBinding MaskByGroup(string group) => new() { groups = group };
        public static InputBinding MaskByGroups(params string[] groups) => new() { groups = string.Join(Separator, groups) };

        /// <summary>A mask matches when every field it sets matches this binding (groups: any overlap).</summary>
        public bool Matches(InputBinding binding)
        {
            if (path != null && !string.Equals(path, binding.effectivePath, StringComparison.OrdinalIgnoreCase)) return false;
            if (action != null && !string.Equals(action, binding.action, StringComparison.OrdinalIgnoreCase)) return false;
            if (id != null && !string.Equals(id, binding.id, StringComparison.OrdinalIgnoreCase)) return false;
            if (name != null && !string.Equals(name, binding.name, StringComparison.OrdinalIgnoreCase)) return false;
            if (groups != null)
            {
                if (binding.groups == null) return false;
                var mine = groups.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
                var theirs = binding.groups.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
                if (!mine.Any(g => theirs.Contains(g, StringComparer.OrdinalIgnoreCase))) return false;
            }
            return true;
        }

        public bool Equals(InputBinding o) => string.Equals(effectivePath, o.effectivePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, o.action) && string.Equals(groups, o.groups) && string.Equals(name, o.name);
        public override bool Equals(object obj) => obj is InputBinding b && Equals(b);
        public override int GetHashCode() => HashCode.Combine(effectivePath?.ToLowerInvariant(), action, groups, name);
        public override string ToString() => $"{action}:{effectivePath}";
        public string ToDisplayString() => InputControlPath.ToHumanReadableString(effectivePath);
    }

    [Serializable]
    public struct InputControlScheme : IEquatable<InputControlScheme>
    {
        public string name { get; set; }
        public string bindingGroup { get; set; }
        public ReadOnlyArray<DeviceRequirement> deviceRequirements { get; set; }

        public InputControlScheme(string name, IEnumerable<DeviceRequirement> devices = null, string bindingGroup = null)
        {
            this.name = name;
            this.bindingGroup = bindingGroup ?? name;
            deviceRequirements = new ReadOnlyArray<DeviceRequirement>((devices ?? Array.Empty<DeviceRequirement>()).ToArray());
        }

        public bool SupportsDevice(InputDevice device)
        {
            foreach (var r in deviceRequirements)
                if (InputControlPath.MatchesDevice(r.controlPath, device)) return true;
            return false;
        }

        public bool Equals(InputControlScheme o) => string.Equals(name, o.name, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is InputControlScheme s && Equals(s);
        public override int GetHashCode() => name?.ToLowerInvariant().GetHashCode() ?? 0;

        [Serializable]
        public struct DeviceRequirement
        {
            public string controlPath { get; set; }
            public bool isOptional { get; set; }
            public bool isOR { get; set; }
            public bool isAND => !isOR;
        }
    }

    /// <summary>An action: bindings in, phases + value out.</summary>
    public sealed class InputAction : ICloneable, IDisposable
    {
        internal readonly List<InputBinding> m_Bindings = new();
        internal InputActionMap m_ActionMap;

        public string name { get; private set; }
        public InputActionType type { get; private set; }
        public string expectedControlType { get; set; }
        public string processors { get; }
        public string interactions { get; }
        public Guid id { get; }
        public InputActionMap actionMap => m_ActionMap;
        public ReadOnlyArray<InputBinding> bindings => new(m_Bindings.ToArray());
        public bool wantsInitialStateCheck { get; set; }

        public bool enabled { get; private set; }
        public InputActionPhase phase { get; private set; } = InputActionPhase.Disabled;
        public bool inProgress => phase is InputActionPhase.Started or InputActionPhase.Performed;
        public bool triggered => _performedFrame == InputActionState.Frame;
        public InputControl activeControl { get; private set; }
        public ReadOnlyArray<InputControl> controls => new(ResolveControls().ToArray());
        public Type activeValueType => _value?.GetType();

        public event Action<CallbackContext> started;
        public event Action<CallbackContext> performed;
        public event Action<CallbackContext> canceled;

        object _value;
        float _magnitude;
        double _startTime, _time;
        int _performedFrame = -1, _pressedFrame = -1, _releasedFrame = -1;
        bool _pressed;

        public InputAction(string name = null, InputActionType type = default, string binding = null,
            string interactions = null, string processors = null, string expectedControlType = null)
        {
            this.name = name; this.type = type; this.interactions = interactions; this.processors = processors;
            this.expectedControlType = expectedControlType; id = Guid.NewGuid();
            if (!string.IsNullOrEmpty(binding)) AddBinding(binding);
        }

        internal InputAction(string name, InputActionType type, Guid id, string expectedControlType, string processors, string interactions)
        {
            this.name = name; this.type = type; this.id = id; this.expectedControlType = expectedControlType;
            this.processors = processors; this.interactions = interactions;
        }

        public BindingSyntax AddBinding(string path, string interactions = null, string processors = null, string groups = null)
        {
            var b = new InputBinding(path, name, groups, processors, interactions);
            m_Bindings.Add(b);
            return new BindingSyntax(this, m_Bindings.Count - 1);
        }

        public CompositeSyntax AddCompositeBinding(string composite, string interactions = null, string processors = null)
        {
            m_Bindings.Add(new InputBinding(composite, name, null, processors, interactions) { isComposite = true, name = composite });
            return new CompositeSyntax(this, m_Bindings.Count - 1);
        }

        public void Enable()
        {
            if (enabled) return;
            enabled = true;
            phase = InputActionPhase.Waiting;
            InputActionState.Register(this);
        }

        public void Disable()
        {
            if (!enabled) return;
            if (phase is InputActionPhase.Started or InputActionPhase.Performed)
                Fire(InputActionPhase.Canceled, canceled);
            enabled = false;
            phase = InputActionPhase.Disabled;
            _value = null; _magnitude = 0f; _pressed = false; activeControl = null;
            InputActionState.Unregister(this);
        }

        public void Reset()
        {
            if (inProgress) Fire(InputActionPhase.Canceled, canceled);
            if (enabled) phase = InputActionPhase.Waiting;
            _value = null; _magnitude = 0f; _pressed = false;
        }

        public TValue ReadValue<TValue>() where TValue : struct => Convert<TValue>(_value);
        public object ReadValueAsObject() => _value;
        public bool IsPressed() => _pressed;
        public bool IsInProgress() => inProgress;
        public bool WasPressedThisFrame() => _pressedFrame == InputActionState.Frame;
        public bool WasReleasedThisFrame() => _releasedFrame == InputActionState.Frame;
        public bool WasPerformedThisFrame() => _performedFrame == InputActionState.Frame;
        public float GetControlMagnitude() => _magnitude;

        public void Dispose() => Disable();
        public InputAction Clone()
        {
            var c = new InputAction(name, type, Guid.NewGuid(), expectedControlType, processors, interactions);
            c.m_Bindings.AddRange(m_Bindings);
            return c;
        }
        object ICloneable.Clone() => Clone();
        public override string ToString() => m_ActionMap != null ? $"{m_ActionMap.name}/{name}" : name;

        internal static TValue Convert<TValue>(object value) where TValue : struct
        {
            if (value is TValue t) return t;
            object r = value switch
            {
                null => default(TValue),
                float f when typeof(TValue) == typeof(bool) => f >= ButtonControl.s_GlobalDefaultButtonPressPoint,
                float f when typeof(TValue) == typeof(Vector2) => new Vector2(f, 0f),
                float f when typeof(TValue) == typeof(int) => (int)f,
                Vector2 v when typeof(TValue) == typeof(float) => v.magnitude,
                Vector2 v when typeof(TValue) == typeof(Vector3) => (Vector3)v,
                bool b when typeof(TValue) == typeof(float) => b ? 1f : 0f,
                int i when typeof(TValue) == typeof(float) => (float)i,
                _ => throw new InvalidOperationException(
                    $"Cannot read value of type '{typeof(TValue).Name}' from an action whose value is '{value.GetType().Name}'"),
            };
            return (TValue)r;
        }

        // ── evaluation ──────────────────────────────────────────────────────

        internal IEnumerable<InputControl> ResolveControls()
        {
            foreach (var b in EffectiveBindings())
                if (!b.isComposite)
                    foreach (var c in InputSystem.FindControls(b.effectivePath)) yield return c;
        }

        IEnumerable<InputBinding> EffectiveBindings()
        {
            var mask = m_ActionMap?.EffectiveMask;
            foreach (var b in m_Bindings)
                if (mask == null || b.isComposite || mask.Value.Matches(b)) yield return b;
        }

        internal void Evaluate()
        {
            object best = null; float bestMag = -1f; InputControl bestControl = null;
            var list = EffectiveBindings().ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.isPartOfComposite) continue;
                object v; float mag; InputControl ctrl;
                if (b.isComposite)
                {
                    var parts = new List<InputBinding>();
                    for (int j = i + 1; j < list.Count && list[j].isPartOfComposite; j++) parts.Add(list[j]);
                    (v, mag, ctrl) = Composites.Evaluate(b.effectivePath, parts);
                }
                else
                {
                    (v, mag, ctrl) = (null, -1f, null);
                    foreach (var c in InputSystem.FindControls(b.effectivePath))
                    {
                        if (!c.device.enabled) continue;
                        float m = c.EvaluateMagnitude();
                        if (m > mag) { mag = m; v = c.ReadValueAsObject(); ctrl = c; }
                    }
                }
                if (ctrl == null && v == null) continue;
                v = Processors.Apply(b.effectiveProcessors, Processors.Apply(processors, v));
                mag = Processors.Magnitude(v);
                if (mag > bestMag) { bestMag = mag; best = v; bestControl = ctrl; }
            }
            if (bestMag < 0f) bestMag = 0f;
            Step(best, bestMag, bestControl);
        }

        void Step(object value, float mag, InputControl control)
        {
            var old = _value;
            _value = value; _magnitude = mag;
            if (control != null) activeControl = control;
            double now = Time.realtimeSinceStartupAsDouble;
            _time = now;

            bool nowPressed = mag >= ButtonControl.s_GlobalDefaultButtonPressPoint;
            if (nowPressed && !_pressed) _pressedFrame = InputActionState.Frame;
            if (!nowPressed && _pressed) _releasedFrame = InputActionState.Frame;
            _pressed = nowPressed;

            switch (type)
            {
                case InputActionType.Button:
                    if (phase == InputActionPhase.Waiting && nowPressed)
                    {
                        _startTime = now;
                        Fire(InputActionPhase.Started, started);
                        Fire(InputActionPhase.Performed, performed);
                    }
                    else if (phase is InputActionPhase.Performed or InputActionPhase.Started && !nowPressed)
                        Fire(InputActionPhase.Canceled, canceled);
                    break;

                case InputActionType.PassThrough:
                    if (!Equals(old, value)) Fire(InputActionPhase.Performed, performed, keepPhase: true);
                    break;

                default: // Value
                    bool actuated = mag > 0f;
                    if (phase == InputActionPhase.Waiting && actuated)
                    {
                        _startTime = now;
                        Fire(InputActionPhase.Started, started);
                        Fire(InputActionPhase.Performed, performed);
                    }
                    else if (phase is InputActionPhase.Performed or InputActionPhase.Started)
                    {
                        if (!actuated) Fire(InputActionPhase.Canceled, canceled);
                        else if (!Equals(old, value)) Fire(InputActionPhase.Performed, performed);
                    }
                    break;
            }
        }

        void Fire(InputActionPhase p, Action<CallbackContext> handlers, bool keepPhase = false)
        {
            phase = p;
            if (p == InputActionPhase.Performed) _performedFrame = InputActionState.Frame;
            var ctx = new CallbackContext(this, p);
            try { handlers?.Invoke(ctx); m_ActionMap?.RaiseTriggered(ctx); }
            catch (Exception e) { Debug.LogException(e); }
            if (p == InputActionPhase.Canceled) { phase = InputActionPhase.Waiting; if (type != InputActionType.PassThrough) { } }
            if (keepPhase && type == InputActionType.PassThrough) phase = InputActionPhase.Performed;
        }

        public readonly struct CallbackContext
        {
            readonly InputAction _action;
            readonly InputActionPhase _phase;
            readonly object _value;
            readonly InputControl _control;
            readonly double _time, _start;

            internal CallbackContext(InputAction action, InputActionPhase phase)
            {
                _action = action; _phase = phase; _value = action._value; _control = action.activeControl;
                _time = action._time; _start = action._startTime;
            }

            public InputAction action => _action;
            public InputActionPhase phase => _phase;
            public bool started => _phase == InputActionPhase.Started;
            public bool performed => _phase == InputActionPhase.Performed;
            public bool canceled => _phase == InputActionPhase.Canceled;
            public InputControl control => _control;
            public object interaction => null;
            public double time => _time;
            public double startTime => _start;
            public double duration => _time - _start;
            public Type valueType => _value?.GetType();
            public int valueSizeInBytes => _value switch { Vector2 => 8, Vector3 => 12, float => 4, _ => 0 };
            public TValue ReadValue<TValue>() where TValue : struct => Convert<TValue>(_value);
            public object ReadValueAsObject() => _value;
            public bool ReadValueAsButton() => Processors.Magnitude(_value) >= ButtonControl.s_GlobalDefaultButtonPressPoint;
            public override string ToString() => $"{{ action={_action} phase={_phase} value={_value} }}";
        }

        public readonly struct BindingSyntax
        {
            readonly InputAction _a; readonly int _i;
            internal BindingSyntax(InputAction a, int i) { _a = a; _i = i; }
            public bool valid => _a != null && _i >= 0 && _i < _a.m_Bindings.Count;
            public InputBinding binding => _a.m_Bindings[_i];
            public BindingSyntax WithPath(string path) => Set(x => { ref var b = ref x.B; b.path = path; });
            public BindingSyntax WithGroup(string g) => Set(x => { ref var b = ref x.B; b.groups = string.IsNullOrEmpty(b.groups) ? g : b.groups + InputBinding.Separator + g; });
            public BindingSyntax WithGroups(string g) => WithGroup(g);
            public BindingSyntax WithProcessor(string p) => Set(x => { ref var b = ref x.B; b.processors = string.IsNullOrEmpty(b.processors) ? p : b.processors + "," + p; });
            public BindingSyntax WithProcessors(string p) => WithProcessor(p);
            public BindingSyntax WithInteraction(string p) => Set(x => { ref var b = ref x.B; b.interactions = string.IsNullOrEmpty(b.interactions) ? p : b.interactions + "," + p; });
            public BindingSyntax WithInteractions(string p) => WithInteraction(p);
            public BindingSyntax WithName(string n) => Set(x => { ref var b = ref x.B; b.name = n; });
            BindingSyntax Set(Action<Box> f) { var box = new Box { B = _a.m_Bindings[_i] }; f(box); _a.m_Bindings[_i] = box.B; return this; }
            sealed class Box { public InputBinding B; }
        }

        public readonly struct CompositeSyntax
        {
            readonly InputAction _a; readonly int _i;
            internal CompositeSyntax(InputAction a, int i) { _a = a; _i = i; }
            public int bindingIndex => _i;
            public CompositeSyntax With(string name, string binding, string groups = null, string processors = null)
            {
                int insert = _i + 1;
                while (insert < _a.m_Bindings.Count && _a.m_Bindings[insert].isPartOfComposite) insert++;
                _a.m_Bindings.Insert(insert, new InputBinding(binding, _a.name, groups, processors) { name = name, isPartOfComposite = true });
                return this;
            }
        }
    }

    /// <summary>A named set of actions (original: InputActionMap).</summary>
    public sealed class InputActionMap : IInputActionCollection2, IDisposable
    {
        internal readonly List<InputAction> m_Actions = new();
        internal InputActionAsset m_Asset;

        public string name { get; }
        public Guid id { get; }
        public InputActionAsset asset => m_Asset;
        public ReadOnlyArray<InputAction> actions => new(m_Actions.ToArray());
        public IEnumerable<InputBinding> bindings => m_Actions.SelectMany(a => a.m_Bindings);
        public ReadOnlyArray<InputControlScheme> controlSchemes => m_Asset?.controlSchemes ?? new ReadOnlyArray<InputControlScheme>(Array.Empty<InputControlScheme>());
        public bool enabled => m_Actions.Count > 0 && m_Actions.Any(a => a.enabled);
        public InputBinding? bindingMask { get; set; }
        public ReadOnlyArray<InputDevice>? devices { get; set; }

        internal InputBinding? EffectiveMask => bindingMask ?? m_Asset?.bindingMask;

        public event Action<InputAction.CallbackContext> actionTriggered;
        internal void RaiseTriggered(InputAction.CallbackContext ctx) => actionTriggered?.Invoke(ctx);

        public InputActionMap(string name = null) : this(name, Guid.NewGuid()) { }
        internal InputActionMap(string name, Guid id) { this.name = name; this.id = id; }

        public InputAction AddAction(string name, InputActionType type = default, string binding = null,
            string interactions = null, string processors = null, string groups = null, string expectedControlType = null)
        {
            var a = new InputAction(name, type, Guid.NewGuid(), expectedControlType, processors, interactions) { m_ActionMap = this };
            if (!string.IsNullOrEmpty(binding)) a.AddBinding(binding, groups: groups);
            m_Actions.Add(a);
            return a;
        }

        public InputAction FindAction(string nameOrId, bool throwIfNotFound = false)
        {
            foreach (var a in m_Actions)
                if (string.Equals(a.name, nameOrId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.id.ToString(), nameOrId.Trim('{', '}'), StringComparison.OrdinalIgnoreCase))
                    return a;
            if (throwIfNotFound) throw new ArgumentException($"No action '{nameOrId}' in map '{name}'");
            return null;
        }

        public InputAction FindAction(Guid id) => m_Actions.FirstOrDefault(a => a.id == id);

        public int FindBinding(InputBinding mask, out InputAction action)
        {
            int index = 0;
            foreach (var a in m_Actions)
                foreach (var b in a.m_Bindings)
                {
                    if (mask.Matches(b)) { action = a; return index; }
                    index++;
                }
            action = null;
            return -1;
        }

        public bool Contains(InputAction action) => action != null && action.m_ActionMap == this;
        public void Enable() { foreach (var a in m_Actions) a.Enable(); }
        public void Disable() { foreach (var a in m_Actions) a.Disable(); }
        public void Dispose() => Disable();
        public IEnumerator<InputAction> GetEnumerator() => m_Actions.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public InputAction this[string actionNameOrId] => FindAction(actionNameOrId, throwIfNotFound: true);
        public override string ToString() => m_Asset != null ? $"{m_Asset.name}:{name}" : name;
    }

    public interface IInputActionCollection : IEnumerable<InputAction>
    {
        InputBinding? bindingMask { get; set; }
        ReadOnlyArray<InputDevice>? devices { get; set; }
        ReadOnlyArray<InputControlScheme> controlSchemes { get; }
        bool Contains(InputAction action);
        void Enable();
        void Disable();
    }

    public interface IInputActionCollection2 : IInputActionCollection
    {
        IEnumerable<InputBinding> bindings { get; }
        InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false);
        int FindBinding(InputBinding bindingMask, out InputAction action);
    }

    /// <summary>A set of action maps + control schemes, usually loaded from <c>.inputactions</c> JSON.</summary>
    public class InputActionAsset : ScriptableObject, IInputActionCollection2
    {
        public const string Extension = "inputactions";

        internal readonly List<InputActionMap> m_Maps = new();
        internal readonly List<InputControlScheme> m_Schemes = new();

        public ReadOnlyArray<InputActionMap> actionMaps => new(m_Maps.ToArray());
        public ReadOnlyArray<InputControlScheme> controlSchemes => new(m_Schemes.ToArray());

        /// <summary>Index of the named control scheme (case-insensitive), or -1 (original contract).</summary>
        public int FindControlSchemeIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < m_Schemes.Count; i++)
                if (string.Equals(m_Schemes[i].name, name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public IEnumerable<InputBinding> bindings => m_Maps.SelectMany(m => m.bindings);
        public InputBinding? bindingMask { get; set; }
        public ReadOnlyArray<InputDevice>? devices { get; set; }
        public bool enabled => m_Maps.Any(m => m.enabled);

        public static InputActionAsset FromJson(string json)
        {
            var asset = CreateInstance<InputActionAsset>();
            asset.LoadFromJson(json);
            return asset;
        }

        public void LoadFromJson(string json)
        {
            m_Maps.Clear(); m_Schemes.Clear();
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var n)) name = n.GetString();
            if (root.TryGetProperty("maps", out var maps))
                foreach (var m in maps.EnumerateArray())
                {
                    var map = new InputActionMap(Str(m, "name"), ParseGuid(Str(m, "id"))) { m_Asset = this };
                    if (m.TryGetProperty("actions", out var actions))
                        foreach (var a in actions.EnumerateArray())
                        {
                            var type = Enum.TryParse<InputActionType>(Str(a, "type"), true, out var t) ? t : InputActionType.Value;
                            map.m_Actions.Add(new InputAction(Str(a, "name"), type, ParseGuid(Str(a, "id")),
                                Str(a, "expectedControlType"), Str(a, "processors"), Str(a, "interactions"))
                            {
                                m_ActionMap = map,
                                wantsInitialStateCheck = a.TryGetProperty("initialStateCheck", out var isc) && isc.ValueKind == JsonValueKind.True,
                            });
                        }
                    if (m.TryGetProperty("bindings", out var binds))
                        foreach (var b in binds.EnumerateArray())
                        {
                            var binding = new InputBinding
                            {
                                name = Str(b, "name"), id = Str(b, "id"), path = Str(b, "path"),
                                interactions = Str(b, "interactions"), processors = Str(b, "processors"),
                                groups = Str(b, "groups"), action = Str(b, "action"),
                                isComposite = Bool(b, "isComposite"), isPartOfComposite = Bool(b, "isPartOfComposite"),
                            };
                            var target = map.FindAction(binding.action ?? string.Empty);
                            target?.m_Bindings.Add(binding);
                        }
                    m_Maps.Add(map);
                }
            if (root.TryGetProperty("controlSchemes", out var schemes))
                foreach (var s in schemes.EnumerateArray())
                {
                    var reqs = new List<InputControlScheme.DeviceRequirement>();
                    if (s.TryGetProperty("devices", out var devs))
                        foreach (var d in devs.EnumerateArray())
                            reqs.Add(new InputControlScheme.DeviceRequirement
                            {
                                controlPath = Str(d, "devicePath"), isOptional = Bool(d, "isOptional"), isOR = Bool(d, "isOR"),
                            });
                    m_Schemes.Add(new InputControlScheme(Str(s, "name"), reqs, Str(s, "bindingGroup")));
                }

            static string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            static bool Bool(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
            static Guid ParseGuid(string s) => Guid.TryParse(s, out var g) ? g : Guid.NewGuid();
        }

        public InputActionMap FindActionMap(string nameOrId, bool throwIfNotFound = false)
        {
            foreach (var m in m_Maps)
                if (string.Equals(m.name, nameOrId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(m.id.ToString(), nameOrId.Trim('{', '}'), StringComparison.OrdinalIgnoreCase))
                    return m;
            if (throwIfNotFound) throw new ArgumentException($"No action map '{nameOrId}' in asset '{name}'");
            return null;
        }

        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        {
            int slash = actionNameOrId.IndexOf('/');
            if (slash > 0)
            {
                var map = FindActionMap(actionNameOrId[..slash]);
                var a = map?.FindAction(actionNameOrId[(slash + 1)..]);
                if (a != null) return a;
            }
            else
                foreach (var m in m_Maps)
                {
                    var a = m.FindAction(actionNameOrId);
                    if (a != null) return a;
                }
            if (throwIfNotFound) throw new ArgumentException($"No action '{actionNameOrId}' in asset '{name}'");
            return null;
        }

        public int FindBinding(InputBinding mask, out InputAction action)
        {
            foreach (var m in m_Maps)
            {
                int i = m.FindBinding(mask, out action);
                if (i >= 0) return i;
            }
            action = null;
            return -1;
        }

        public InputActionMap AddActionMap(string name)
        {
            var m = new InputActionMap(name) { m_Asset = this };
            m_Maps.Add(m);
            return m;
        }

        public void AddControlScheme(InputControlScheme scheme) => m_Schemes.Add(scheme);

        public InputControlScheme? FindControlScheme(string name)
        {
            foreach (var s in m_Schemes) if (string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public bool Contains(InputAction action) => action?.m_ActionMap?.m_Asset == this;
        public void Enable() { foreach (var m in m_Maps) m.Enable(); }
        public void Disable() { foreach (var m in m_Maps) m.Disable(); }
        public IEnumerator<InputAction> GetEnumerator() => m_Maps.SelectMany(m => m.m_Actions).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public InputAction this[string actionNameOrId] => FindAction(actionNameOrId, throwIfNotFound: true);
    }

    /// <summary>A serialized reference to one action inside an asset (original: InputActionReference).</summary>
    public class InputActionReference : ScriptableObject
    {
        [SerializeField] InputActionAsset m_Asset;
        [SerializeField] string m_ActionId;
        InputAction _action;

        public InputActionAsset asset => m_Asset;
        public InputAction action => _action ??= m_Asset != null && m_ActionId != null ? m_Asset.FindAction(m_ActionId) : null;

        public void Set(InputAction action)
        {
            _action = action;
            m_Asset = action?.actionMap?.asset;
            m_ActionId = action?.id.ToString();
        }

        public void Set(InputActionAsset asset, string mapName, string actionName)
        {
            m_Asset = asset;
            _action = asset?.FindActionMap(mapName)?.FindAction(actionName);
            m_ActionId = _action?.id.ToString();
        }

        public static InputActionReference Create(InputAction action)
        {
            var r = CreateInstance<InputActionReference>();
            r.Set(action);
            return r;
        }

        public static implicit operator InputAction(InputActionReference r) => r?.action;
        public InputAction ToInputAction() => action;
    }

    /// <summary>Per-frame action driver (the original's InputActionState, radically simplified).</summary>
    internal static class InputActionState
    {
        static readonly List<InputAction> s_Enabled = new();
        internal static int Frame;

        internal static void Register(InputAction a) { if (!s_Enabled.Contains(a)) s_Enabled.Add(a); }
        internal static void Unregister(InputAction a) => s_Enabled.Remove(a);

        internal static void EvaluateAll()
        {
            Frame++;
            foreach (var a in s_Enabled.ToArray())
                if (a.enabled) a.Evaluate();
        }

        internal static void Reset() { s_Enabled.Clear(); Frame = 0; }
    }

    /// <summary>Composite binding evaluation (2DVector/Dpad, 1DAxis, modifiers).</summary>
    internal static class Composites
    {
        internal static (object value, float magnitude, InputControl control) Evaluate(string composite, List<InputBinding> parts)
        {
            string kind = composite ?? string.Empty;
            int paren = kind.IndexOf('(');
            string args = paren > 0 ? kind[(paren + 1)..].TrimEnd(')') : string.Empty;
            if (paren > 0) kind = kind[..paren];

            (float v, InputControl c) Part(string name)
            {
                float best = 0f; InputControl bc = null;
                foreach (var p in parts)
                    if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                        foreach (var c in InputSystem.FindControls(p.effectivePath))
                        {
                            float m = c.EvaluateMagnitude();
                            if (m > best || bc == null) { best = m; bc = c; }
                        }
                return (best, bc);
            }

            switch (kind.ToLowerInvariant())
            {
                case "2dvector":
                case "dpad":
                {
                    var (u, cu) = Part("up"); var (d, cd) = Part("down"); var (l, cl) = Part("left"); var (r, cr) = Part("right");
                    var v = new Vector2(r - l, u - d);
                    bool analog = args.Contains("mode=2") || args.Contains("mode=Analog", StringComparison.OrdinalIgnoreCase);
                    bool digital = args.Contains("mode=1") || args.Contains("mode=Digital", StringComparison.OrdinalIgnoreCase);
                    if (!analog && !digital && v.sqrMagnitude > 0f) v = v.normalized;
                    var ctrl = cu ?? cd ?? cl ?? cr;
                    return (v, v.magnitude, ctrl);
                }
                case "1daxis":
                case "axis":
                {
                    var (neg, cn) = Part("negative"); var (pos, cp) = Part("positive");
                    float v = pos - neg;
                    return (v, MathF.Abs(v), cp ?? cn);
                }
                case "buttonwithonemodifier":
                case "onemodifier":
                {
                    var (mod, _) = Part("modifier"); var (btn, cb) = Part("button");
                    if (cb == null) (btn, cb) = Part("binding");
                    float v = mod >= ButtonControl.s_GlobalDefaultButtonPressPoint ? btn : 0f;
                    return (v, v, cb);
                }
                case "buttonwithtwomodifiers":
                case "twomodifiers":
                {
                    var (m1, _) = Part("modifier1"); var (m2, _) = Part("modifier2"); var (btn, cb) = Part("button");
                    if (cb == null) (btn, cb) = Part("binding");
                    float p = ButtonControl.s_GlobalDefaultButtonPressPoint;
                    float v = m1 >= p && m2 >= p ? btn : 0f;
                    return (v, v, cb);
                }
                default:
                    return (null, 0f, null);
            }
        }
    }

    /// <summary>The stock processors the project's assets use.</summary>
    internal static class Processors
    {
        internal static object Apply(string list, object value)
        {
            if (string.IsNullOrEmpty(list) || value == null) return value;
            foreach (var raw in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var (name, args) = Parse(raw);
                float A(string k, float def) => args.TryGetValue(k, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;
                value = (name.ToLowerInvariant(), value) switch
                {
                    ("invert", float f) => -f,
                    ("invertvector2", Vector2 v) => new Vector2(A("invertX", 1) != 0 || !args.ContainsKey("invertX") ? -v.x : v.x, A("invertY", 1) != 0 || !args.ContainsKey("invertY") ? -v.y : v.y),
                    ("scale", float f) => f * A("factor", 1f),
                    ("scalevector2", Vector2 v) => new Vector2(v.x * A("x", 1f), v.y * A("y", 1f)),
                    ("clamp", float f) => Math.Clamp(f, A("min", 0f), A("max", 1f)),
                    ("normalize", float f) => A("max", 1f) - A("min", 0f) == 0 ? f : (f - A("min", 0f)) / (A("max", 1f) - A("min", 0f)),
                    ("normalizevector2", Vector2 v) => v.sqrMagnitude > 0 ? v.normalized : v,
                    ("axisdeadzone", float f) => AxisDeadzone(f, A("min", 0.125f), A("max", 0.925f)),
                    ("stickdeadzone", Vector2 v) => StickDeadzone(v, A("min", 0.125f), A("max", 0.925f)),
                    _ => value,
                };
            }
            return value;
        }

        static float AxisDeadzone(float v, float min, float max)
        {
            float a = MathF.Abs(v);
            if (a < min) return 0f;
            if (a > max) return MathF.Sign(v);
            return MathF.Sign(v) * (a - min) / (max - min);
        }

        static Vector2 StickDeadzone(Vector2 v, float min, float max)
        {
            float m = v.magnitude;
            if (m < min) return Vector2.zero;
            float n = m > max ? 1f : (m - min) / (max - min);
            return v / m * n;
        }

        static (string, Dictionary<string, string>) Parse(string s)
        {
            var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int p = s.IndexOf('(');
            if (p < 0) return (s.Trim(), args);
            foreach (var kv in s[(p + 1)..].TrimEnd(')').Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = kv.Split('=');
                if (parts.Length == 2) args[parts[0].Trim()] = parts[1].Trim();
            }
            return (s[..p].Trim(), args);
        }

        internal static float Magnitude(object v) => v switch
        {
            float f => MathF.Abs(f),
            Vector2 v2 => v2.magnitude,
            Vector3 v3 => v3.magnitude,
            bool b => b ? 1f : 0f,
            int i => Math.Abs(i),
            _ => 0f,
        };
    }

    /// <summary>Control-path parsing (original: InputControlPath).</summary>
    public static class InputControlPath
    {
        public const string DoubleWildcard = "**";
        public const char Separator = '/';

        static readonly Dictionary<string, (string gamepad, string keyboard)> Usages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Submit"] = ("buttonSouth", "enter"),
            ["Cancel"] = ("buttonEast", "escape"),
            ["Back"] = ("buttonEast", "escape"),
            ["Menu"] = ("start", "escape"),
            ["PrimaryAction"] = ("buttonSouth", "space"),
            ["SecondaryAction"] = ("buttonEast", null),
            ["Primary2DMotion"] = ("leftStick", null),
            ["Secondary2DMotion"] = ("rightStick", null),
        };

        public static bool MatchesDevice(string path, InputDevice device)
        {
            if (string.IsNullOrEmpty(path) || device == null) return false;
            var (layout, _) = SplitDevice(path);
            return LayoutMatches(layout, device);
        }

        internal static void Match(string path, InputDevice device, List<InputControl> results)
        {
            if (string.IsNullOrEmpty(path) || device == null) return;
            var (layout, rest) = SplitDevice(path);
            if (!LayoutMatches(layout, device)) return;
            if (string.IsNullOrEmpty(rest)) { results.Add(device); return; }

            // "{Usage}" addressing.
            if (rest.StartsWith("{") && rest.EndsWith("}"))
            {
                var usage = rest.Trim('{', '}');
                if (Usages.TryGetValue(usage, out var m))
                {
                    string child = device is Gamepad ? m.gamepad : device is Keyboard ? m.keyboard : null;
                    if (child != null && device.TryGetChildControl(child) is { } c) results.Add(c);
                }
                return;
            }

            if (rest.Contains('*'))
            {
                // "*/leftStick" style or "<Keyboard>/*" — any direct child.
                if (rest == "*" ) { foreach (var c in device.ChildList) results.Add(c); return; }
            }

            var control = device.TryGetChildControl(rest);
            if (control != null) results.Add(control);
        }

        static (string layout, string rest) SplitDevice(string path)
        {
            path = path.Trim();
            if (path.StartsWith("/")) path = path[1..];
            int slash = path.IndexOf('/');
            string head = slash < 0 ? path : path[..slash];
            string rest = slash < 0 ? string.Empty : path[(slash + 1)..];
            int usage = head.IndexOf('{');
            if (usage > 0) head = head[..usage];
            return (head.Trim('<', '>'), rest);
        }

        static bool LayoutMatches(string layout, InputDevice device)
        {
            if (layout == "*" || string.IsNullOrEmpty(layout)) return true;
            for (var t = device.GetType(); t != null && t != typeof(object); t = t.BaseType)
                if (string.Equals(t.Name, layout, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(device.name, layout, StringComparison.OrdinalIgnoreCase)) return true;
            // Layout aliases the project's assets use.
            return layout.ToLowerInvariant() switch
            {
                "xinputcontroller" or "dualshockgamepad" or "switchprocontrollerhid" => device is Gamepad,
                "pen" => false,
                _ => false,
            };
        }

        public static string ToHumanReadableString(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var (layout, rest) = SplitDevice(path);
            return string.IsNullOrEmpty(rest) ? layout : $"{rest.Replace('/', ' ')} [{layout}]";
        }

        public static string TryGetDeviceLayout(string path) => SplitDevice(path).layout;
        public static string TryGetControlLayout(string path) => null;
        public static bool Matches(string expected, InputControl control)
        {
            var list = new List<InputControl>();
            Match(expected, control.device, list);
            return list.Contains(control);
        }
    }
}

namespace CosmicShore.Engine.InputSystem.Utilities
{
    /// <summary>Read-only view over an array (original: ReadOnlyArray&lt;T&gt;).</summary>
    public readonly struct ReadOnlyArray<TValue> : IReadOnlyList<TValue>
    {
        readonly TValue[] _array;
        readonly int _start, _length;

        public ReadOnlyArray(TValue[] array) { _array = array; _start = 0; _length = array?.Length ?? 0; }
        public ReadOnlyArray(TValue[] array, int index, int length) { _array = array; _start = index; _length = length; }

        public int Count => _length;
        public TValue this[int index] => index < 0 || index >= _length ? throw new IndexOutOfRangeException() : _array[_start + index];

        public int IndexOf(Predicate<TValue> predicate)
        {
            for (int i = 0; i < _length; i++) if (predicate(this[i])) return i;
            return -1;
        }

        public TValue[] ToArray()
        {
            var r = new TValue[_length];
            if (_length > 0) Array.Copy(_array, _start, r, 0, _length);
            return r;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            for (int i = 0; i < _length; i++) yield return _array[_start + i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static implicit operator ReadOnlyArray<TValue>(TValue[] array) => new(array);
    }

    public static class ReadOnlyArrayExtensions
    {
        public static bool Contains<TValue>(this ReadOnlyArray<TValue> array, TValue value)
        {
            foreach (var v in array) if (EqualityComparer<TValue>.Default.Equals(v, value)) return true;
            return false;
        }

        public static int IndexOf<TValue>(this ReadOnlyArray<TValue> array, TValue value)
        {
            for (int i = 0; i < array.Count; i++) if (EqualityComparer<TValue>.Default.Equals(array[i], value)) return i;
            return -1;
        }
    }

    /// <summary>Case-insensitive interned string (original: InternedString).</summary>
    public readonly struct InternedString : IEquatable<InternedString>
    {
        readonly string _s;
        public InternedString(string s) { _s = s; }
        public int length => _s?.Length ?? 0;
        public bool IsEmpty() => string.IsNullOrEmpty(_s);
        public bool Equals(InternedString o) => string.Equals(_s, o._s, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is InternedString i && Equals(i);
        public override int GetHashCode() => _s?.ToLowerInvariant().GetHashCode() ?? 0;
        public override string ToString() => _s ?? string.Empty;
        public static implicit operator string(InternedString s) => s._s;
    }
}
