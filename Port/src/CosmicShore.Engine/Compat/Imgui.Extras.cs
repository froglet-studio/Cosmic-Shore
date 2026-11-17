namespace CosmicShore.Engine
{
    // The rest of IMGUI the game's debug panels name (ThirdEyeHud, MultiplayerDebugOverlay), with Unity's
    // signatures. As in PlatformSurface.cs, OnGUI is not part of the port's frame: these calls are accepted and
    // draw nothing, controls return the value they were given, and Event.current is an idle Layout event.

    /// <summary>The kind of an IMGUI event (original: UnityEngine.EventType; values match).</summary>
    public enum EventType
    {
        MouseDown = 0, MouseUp = 1, MouseMove = 2, MouseDrag = 3, KeyDown = 4, KeyUp = 5, ScrollWheel = 6,
        Repaint = 7, Layout = 8, DragUpdated = 9, DragPerform = 10, Ignore = 11, Used = 12, ValidateCommand = 13,
        ExecuteCommand = 14, DragExited = 15, ContextClick = 16, MouseEnterWindow = 20, MouseLeaveWindow = 21,
        TouchDown = 30, TouchUp = 31, TouchMove = 32, TouchEnter = 33, TouchLeave = 34, TouchStationary = 35,
    }

    /// <summary>Modifier keys held during an IMGUI event (original: UnityEngine.EventModifiers; values match).</summary>
    [System.Flags]
    public enum EventModifiers { None = 0, Shift = 1, Control = 2, Alt = 4, Command = 8, Numeric = 16, CapsLock = 32, FunctionKey = 64 }

    /// <summary>An IMGUI event (original: UnityEngine.Event). The port raises none, so <see cref="current"/> is an idle Layout event.</summary>
    public sealed class Event
    {
        public static Event current { get; set; } = new() { type = EventType.Layout };

        public EventType type { get; set; }
        public EventType rawType => type;
        public Vector2 mousePosition { get; set; }
        public Vector2 delta { get; set; }
        public KeyCode keyCode { get; set; }
        public char character { get; set; }
        public int button { get; set; }
        public int clickCount { get; set; }
        public EventModifiers modifiers { get; set; }
        public string commandName { get; set; } = "";
        public bool shift => (modifiers & EventModifiers.Shift) != 0;
        public bool control => (modifiers & EventModifiers.Control) != 0;
        public bool alt => (modifiers & EventModifiers.Alt) != 0;
        public bool command => (modifiers & EventModifiers.Command) != 0;
        public bool isMouse => type is EventType.MouseDown or EventType.MouseUp or EventType.MouseMove or EventType.MouseDrag or EventType.ContextClick;
        public bool isKey => type is EventType.KeyDown or EventType.KeyUp;
        public bool isScrollWheel => type == EventType.ScrollWheel;
        public void Use() => type = EventType.Used;
    }

    public partial class GUIStyle
    {
        /// <summary>Original contract: a style named after a skin entry ("Button", "Label" ...). The port's styles are data.</summary>
        public static implicit operator GUIStyle(string str) => new();
        public static GUIStyle none => new();
    }

    public partial class GUIContent
    {
        public static GUIContent none => new("");
        public GUIContent(string text, string tooltip) { this.text = text; this.tooltip = tooltip; }
        public GUIContent(string text, Texture image) { this.text = text; this.image = image; }
        public GUIContent(Texture image) { this.image = image; }
        public GUIContent(GUIContent src) { if (src != null) { text = src.text; image = src.image; tooltip = src.tooltip; } }
    }

    public static partial class GUI
    {
        public static void Label(Rect position, GUIContent content) { }
        public static void Box(Rect position, GUIContent content) { }
        public static bool Button(Rect position, GUIContent content) => false;
        public static bool Button(Rect position, GUIContent content, GUIStyle style) => false;
        public static void SetNextControlName(string name) { }
        /// <summary>No control ever has focus in the port (OnGUI does not run).</summary>
        public static string GetNameOfFocusedControl() => "";
        public static void FocusControl(string name) { }
        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect) => scrollPosition;
        public static void EndScrollView() { }
    }

    public static partial class GUILayout
    {
        public static void Label(GUIContent content, params GUILayoutOption[] options) { }
        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { }
        public static void Box(GUIContent content, params GUILayoutOption[] options) { }
        public static void Box(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { }
        public static bool Button(GUIContent content, params GUILayoutOption[] options) => false;
        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => false;
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) => value;
        public static bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options) => value;
        public static bool Toggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options) => value;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static string TextField(string text, GUIStyle style, params GUILayoutOption[] options) => text;
        public static string TextField(string text, int maxLength, params GUILayoutOption[] options) => text;
        public static float HorizontalSlider(float value, float leftValue, float rightValue, params GUILayoutOption[] options) => value;
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static Vector2 BeginScrollView(Vector2 scrollPosition, GUIStyle style, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView() { }
        public static void BeginArea(Rect screenRect, GUIStyle style) { }
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static GUILayoutOption MinWidth(float minWidth) => new();
        public static GUILayoutOption MaxWidth(float maxWidth) => new();
        public static GUILayoutOption MinHeight(float minHeight) => new();
        public static GUILayoutOption MaxHeight(float maxHeight) => new();
        public static GUILayoutOption ExpandWidth(bool expand) => new();
        public static GUILayoutOption ExpandHeight(bool expand) => new();
    }
}
