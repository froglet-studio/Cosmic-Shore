using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CosmicShore.Engine.Rendering;

namespace CosmicShore.Engine
{
    // ─────────────────────────────────────────────────────────────────────────
    // Platform surface the live scripts read: object flags + lookups, application /
    // screen / quality / system info, the legacy Input class, gizmos and IMGUI
    // stand-ins. Values describe the machine the port actually runs on.
    // ─────────────────────────────────────────────────────────────────────────

    [Flags]
    public enum HideFlags
    {
        None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8,
        DontSaveInBuild = 16, DontUnloadUnusedAsset = 32,
        DontSave = DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset,
        HideAndDontSave = HideInHierarchy | DontSave | NotEditable,
    }

    public enum FindObjectsInactive { Exclude = 0, Include = 1 }

    public abstract partial class Object
    {
        public HideFlags hideFlags { get; set; }

        /// <summary>Destroy after <paramref name="t"/> seconds of scaled time (original contract).</summary>
        public static void Destroy(Object obj, float t)
        {
            if (obj is null) return;
            if (t <= 0f) { Destroy(obj); return; }
            _ = DelayedDestroy(obj, t);
        }

        static async Task DelayedDestroy(Object obj, float t)
        {
            await CosmicShore.Engine.Tasks.GameTask.Delay(t);
            if (obj) Destroy(obj);
        }

        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets) => DestroyImmediate(obj);

        public static T FindFirstObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : class
            => GameLoop.Current?.Scene.FindObjectOfType<T>(includeInactive: findObjectsInactive == FindObjectsInactive.Include);

        public static T FindAnyObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : class
            => FindFirstObjectByType<T>(findObjectsInactive);

        public static T[] FindObjectsByType<T>(FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) where T : class
            => GameLoop.Current?.Scene.FindObjectsOfType<T>(includeInactive: findObjectsInactive == FindObjectsInactive.Include).ToArray()
               ?? Array.Empty<T>();

        public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : class
            => GameLoop.Current?.Scene.FindObjectsOfType<T>(includeInactive).ToArray() ?? Array.Empty<T>();

        public static T FindObjectOfType<T>(bool includeInactive) where T : class
            => GameLoop.Current?.Scene.FindObjectOfType<T>(includeInactive);

        public static Object[] FindObjectsByType(Type type, FindObjectsSortMode sortMode)
            => FindObjectsByType(type, FindObjectsInactive.Exclude, sortMode);

        public static Object[] FindObjectsByType(Type type, FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode)
        {
            var result = new List<Object>();
            var scene = GameLoop.Current?.Scene;
            if (scene == null) return result.ToArray();
            foreach (var c in scene.FindObjectsOfType<Component>(findObjectsInactive == FindObjectsInactive.Include))
                if (type.IsInstanceOfType(c)) result.Add(c);
            return result.ToArray();
        }

        public static Object FindFirstObjectByType(Type type) => FindObjectsByType(type, FindObjectsInactive.Exclude, FindObjectsSortMode.None) is { Length: > 0 } a ? a[0] : null;

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays) where T : Object
            => Instantiate(original, position, rotation, parent);

        public static Object Instantiate(Object original) => Instantiate<Object>(original);
        public static Object Instantiate(Object original, Transform parent) => Instantiate<Object>(original, parent);
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation) => Instantiate<Object>(original, position, rotation);

        /// <summary>Asynchronous instantiation (original: InstantiateAsync). The port instantiates in-frame and completes immediately.</summary>
        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original) where T : Object
            => InstantiateAsync(original, 1);

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count) where T : Object
        {
            var op = new AsyncInstantiateOperation<T>();
            var results = new T[Math.Max(0, count)];
            for (int i = 0; i < results.Length; i++) results[i] = Instantiate(original);
            op.Result = results;
            op.Complete();
            return op;
        }

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, Transform parent) where T : Object
            => InstantiateAsync(original, 1, parent);

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, Transform parent) where T : Object
        {
            var op = new AsyncInstantiateOperation<T>();
            var results = new T[Math.Max(0, count)];
            for (int i = 0; i < results.Length; i++) results[i] = Instantiate(original, parent);
            op.Result = results;
            op.Complete();
            return op;
        }

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, Vector3 position, Quaternion rotation) where T : Object
            => InstantiateAsync(original, 1, position, rotation);

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, Vector3 position, Quaternion rotation) where T : Object
        {
            var op = new AsyncInstantiateOperation<T>();
            var results = new T[Math.Max(0, count)];
            for (int i = 0; i < results.Length; i++) results[i] = Instantiate(original, position, rotation);
            op.Result = results;
            op.Complete();
            return op;
        }

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, Transform parent, Vector3 position, Quaternion rotation) where T : Object
        {
            var op = new AsyncInstantiateOperation<T>();
            var results = new T[Math.Max(0, count)];
            for (int i = 0; i < results.Length; i++) results[i] = Instantiate(original, position, rotation, parent);
            op.Result = results;
            op.Complete();
            return op;
        }
    }

    public class AsyncInstantiateOperation : AsyncOperation
    {
        public void WaitForCompletion() { }
        public void Cancel() { }
        public static float GetIntegrationTimeMS() => 2f;
        public static void SetIntegrationTimeMS(float ms) { }
    }

    public class AsyncInstantiateOperation<T> : AsyncInstantiateOperation
    {
        public T[] Result { get; internal set; } = Array.Empty<T>();
        public new System.Runtime.CompilerServices.TaskAwaiter<bool> GetAwaiter() => base.GetAwaiter();
    }

    // ── Application / Screen / Quality / SystemInfo ─────────────────────────

    public enum SystemLanguage { English = 10, Unknown = 43 }
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
    public enum ApplicationInstallMode { Unknown = 0, Store = 1, DeveloperBuild = 2, Adhoc = 3, Enterprise = 4, Editor = 5 }
    public enum ThreadPriority { Low = 0, BelowNormal = 1, Normal = 2, High = 4 }

    public static partial class Application
    {
        public delegate void LogCallback(string condition, string stackTrace, LogType type);
        public delegate void LowMemoryCallback();

        public static bool isEditor => false;
        public static bool isBatchMode => false;
        public static bool isFocused { get; internal set; } = true;
        public static bool runInBackground { get; set; } = true;
        public static bool genuine => true;
        public static bool genuineCheckAvailable => false;
        public static string unityVersion => "6000.0.50f1";
        public static string productName { get; set; } = "Cosmic Shore";
        public static string companyName { get; set; } = "Froglet Inc.";
        public static string identifier { get; set; } = "com.froglet.cosmicshore";
        public static string buildGUID { get; } = Guid.NewGuid().ToString("N");
        public static SystemLanguage systemLanguage => SystemLanguage.English;
        public static ApplicationInstallMode installMode => ApplicationInstallMode.DeveloperBuild;
        public static ThreadPriority backgroundLoadingPriority { get; set; } = ThreadPriority.Normal;
        public static string consoleLogPath => Path.Combine(temporaryCachePath, "Player.log");

        /// <summary>The project's asset root the port loads content from (settable by the content bridge).</summary>
        public static string dataPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Data");
        public static string streamingAssetsPath => Path.Combine(dataPath, "StreamingAssets");

        public static string temporaryCachePath
        {
            get
            {
                string path = Path.Combine(Path.GetTempPath(), "CosmicShore");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static event LogCallback logMessageReceived;
        public static event LogCallback logMessageReceivedThreaded;
        public static event LowMemoryCallback lowMemory;
        public static event Action<bool> focusChanged;
        public static event Func<bool> wantsToQuit;
        public static event Action unloading;

        internal static void RaiseLog(string condition, string stackTrace, LogType type)
        {
            logMessageReceived?.Invoke(condition, stackTrace, type);
            logMessageReceivedThreaded?.Invoke(condition, stackTrace, type);
        }

        public static void SetFocus(bool focused) { isFocused = focused; focusChanged?.Invoke(focused); }
        public static bool CanQuit() { foreach (Func<bool> f in wantsToQuit?.GetInvocationList() ?? Array.Empty<Delegate>()) if (!f()) return false; return true; }
        public static void RaiseLowMemory() => lowMemory?.Invoke();
        internal static void RaiseUnloading() => unloading?.Invoke();
        public static bool HasProLicense() => true;
        public static void Quit(int exitCode) => Quit();
    }

    public struct RefreshRate : IEquatable<RefreshRate>, IComparable<RefreshRate>
    {
        public uint numerator;
        public uint denominator;
        public double value => denominator == 0 ? 0 : (double)numerator / denominator;
        public bool Equals(RefreshRate o) => value.Equals(o.value);
        public int CompareTo(RefreshRate o) => value.CompareTo(o.value);
        public override string ToString() => value.ToString("0.##");
    }

    public partial struct Resolution
    {
        public RefreshRate refreshRateRatio
        {
            get => new() { numerator = (uint)Math.Max(0, refreshRate), denominator = 1 };
            set => refreshRate = (int)Math.Round(value.value);
        }
        public override string ToString() => $"{width} x {height} @ {refreshRate}Hz";
    }

    public static partial class Screen
    {
        public static bool fullScreen { get; set; }
        public static FullScreenMode fullScreenMode { get; set; } = FullScreenMode.Windowed;
        public static Rect safeArea => new(0, 0, width, height);
        public static Rect[] cutouts => Array.Empty<Rect>();
        public static Resolution[] resolutions => new[]
        {
            new Resolution { width = 1280, height = 720, refreshRate = 60 },
            new Resolution { width = 1600, height = 900, refreshRate = 60 },
            new Resolution { width = 1920, height = 1080, refreshRate = 60 },
            new Resolution { width = 2560, height = 1440, refreshRate = 60 },
            new Resolution { width = 3840, height = 2160, refreshRate = 60 },
        };
        public static float brightness { get; set; } = 1f;

        /// <summary>Raised when a script asks for a new resolution; the window host applies it.</summary>
        public static event Action<int, int, FullScreenMode> ResolutionRequested;

        public static void SetResolution(int w, int h, FullScreenMode mode, RefreshRate preferredRefreshRate)
        { fullScreenMode = mode; fullScreen = mode != FullScreenMode.Windowed; ResolutionRequested?.Invoke(w, h, mode); }
        public static void SetResolution(int w, int h, FullScreenMode mode, int preferredRefreshRate = 0)
            => SetResolution(w, h, mode, default(RefreshRate));
        public static void SetResolution(int w, int h, bool full, int preferredRefreshRate = 0)
            => SetResolution(w, h, full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed, default(RefreshRate));
    }

    public static partial class QualitySettings
    {
        static int s_Level = 2;
        public static string[] names { get; } = { "Low", "Medium", "High" };
        public static int antiAliasing { get; set; } = 4;
        public static int globalTextureMipmapLimit { get; set; }
        public static int masterTextureLimit { get => globalTextureMipmapLimit; set => globalTextureMipmapLimit = value; }
        public static ColorSpace activeColorSpace => ColorSpace.Linear;
        public static ColorSpace desiredColorSpace => ColorSpace.Linear;
        public static float shadowDistance { get; set; } = 150f;
        public static float lodBias { get; set; } = 1f;
        public static int maximumLODLevel { get; set; }
        public static int pixelLightCount { get; set; } = 4;
        public static ShadowQuality shadows { get; set; } = ShadowQuality.All;
        public static AnisotropicFiltering anisotropicFiltering { get; set; } = AnisotropicFiltering.Enable;
        public static bool realtimeReflectionProbes { get; set; } = true;
        public static bool softParticles { get; set; }
        public static int maxQueuedFrames { get; set; } = 2;
        public static RenderPipelineAsset renderPipeline { get; set; }
        public static event Action<int, int> activeQualityLevelChanged;
        public static int GetQualityLevel() => s_Level;
        public static void SetQualityLevel(int index, bool applyExpensiveChanges = true)
        {
            int prev = s_Level;
            s_Level = Math.Clamp(index, 0, names.Length - 1);
            if (prev != s_Level) activeQualityLevelChanged?.Invoke(prev, s_Level);
        }
        public static RenderPipelineAsset GetRenderPipelineAssetAt(int index) => renderPipeline;
        public static void IncreaseLevel(bool applyExpensiveChanges = false) => SetQualityLevel(s_Level + 1);
        public static void DecreaseLevel(bool applyExpensiveChanges = false) => SetQualityLevel(s_Level - 1);
    }

    public enum ShadowQuality { Disable = 0, HardOnly = 1, All = 2 }
    public enum AnisotropicFiltering { Disable = 0, Enable = 1, ForceEnable = 2 }

    public enum OperatingSystemFamily { Other = 0, MacOSX = 1, Windows = 2, Linux = 3 }
    public enum BatteryStatus { Unknown = 0, Charging = 1, Discharging = 2, NotCharging = 3, Full = 4 }

    public static partial class SystemInfo
    {
        /// <summary>Filled in by the renderer when the GL context comes up.</summary>
        public static GraphicsDeviceType graphicsDeviceType { get; set; } = GraphicsDeviceType.OpenGLCore;
        public static string graphicsDeviceName { get; set; } = "OpenGL";
        public static string graphicsDeviceVendor { get; set; } = "Unknown";
        public static string graphicsDeviceVersion { get; set; } = "OpenGL 3.3";
        public static int graphicsMemorySize { get; set; } = 2048;
        public static int graphicsShaderLevel => 45;
        public static bool graphicsMultiThreaded => false;
        public static bool supportsComputeShaders { get; set; }
        /// <summary>
        /// Storage buffers readable in the vertex stage. Prisma renders with GL 3.3 / GL ES 3.0, which
        /// have none, so this is 0 - what Unity reports on such a device. Code that draws from a
        /// <see cref="GraphicsBuffer"/> in a vertex shader checks it and takes its own fallback.
        /// </summary>
        public static int maxComputeBufferInputsVertex => 0;
        public static bool supportsInstancing => true;
        public static bool supportsAsyncGPUReadback => false;
        public static bool supportsGyroscope => false;
        public static bool supportsAccelerometer => false;
        public static bool supportsVibration => false;
        public static bool supports2DArrayTextures => true;
        public static int maxTextureSize => 16384;
        public static int supportedRenderTargetCount => 8;
        public static int processorCount => Environment.ProcessorCount;
        public static string processorType => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + " CPU";
        public static int processorFrequency => 0;
        public static int systemMemorySize => (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024));
        public static string deviceModel => Environment.MachineName;
        public static string deviceName => Environment.MachineName;
        public static string operatingSystem => System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        public static OperatingSystemFamily operatingSystemFamily =>
            OperatingSystem.IsWindows() ? OperatingSystemFamily.Windows :
            OperatingSystem.IsMacOS() ? OperatingSystemFamily.MacOSX :
            OperatingSystem.IsLinux() ? OperatingSystemFamily.Linux : OperatingSystemFamily.Other;
        public static float batteryLevel => -1f;
        public static BatteryStatus batteryStatus => BatteryStatus.Unknown;
        public const string unsupportedIdentifier = "n/a";
        public static bool SupportsRenderTextureFormat(RenderTextureFormat format) => true;
        public static bool SupportsTextureFormat(TextureFormat format) => true;
    }

    public enum FormatUsage { Sample = 0, Linear = 1, Render = 3, Blend = 4 }

    /// <summary>A display (original: UnityEngine.Display) — one, the window the port renders to.</summary>
    public sealed class Display
    {
        public static Display main { get; } = new Display();
        public static Display[] displays { get; } = { main };
        public static event Action onDisplaysUpdated;

        public int systemWidth { get; set; } = 1920;
        public int systemHeight { get; set; } = 1080;
        public int renderingWidth => Screen.width;
        public int renderingHeight => Screen.height;
        public bool active { get; private set; } = true;
        public bool requiresBlitToBackbuffer => false;
        public bool requiresSrgbBlitToBackbuffer => false;
        public void Activate() => active = true;
        public void Activate(int width, int height, RefreshRate refreshRate) => active = true;
        public void SetRenderingResolution(int w, int h) { }
        public static void RaiseDisplaysUpdated() => onDisplaysUpdated?.Invoke();
    }

    // ── Legacy Input (original: UnityEngine.Input) over the Input System devices ──

    public enum KeyCode
    {
        None = 0, Backspace = 8, Tab = 9, Clear = 12, Return = 13, Pause = 19, Escape = 27, Space = 32,
        Exclaim = 33, DoubleQuote = 34, Hash = 35, Dollar = 36, Percent = 37, Ampersand = 38, Quote = 39,
        LeftParen = 40, RightParen = 41, Asterisk = 42, Plus = 43, Comma = 44, Minus = 45, Period = 46, Slash = 47,
        Alpha0 = 48, Alpha1 = 49, Alpha2 = 50, Alpha3 = 51, Alpha4 = 52, Alpha5 = 53, Alpha6 = 54, Alpha7 = 55, Alpha8 = 56, Alpha9 = 57,
        Colon = 58, Semicolon = 59, Less = 60, Equals = 61, Greater = 62, Question = 63, At = 64,
        LeftBracket = 91, Backslash = 92, RightBracket = 93, Caret = 94, Underscore = 95, BackQuote = 96,
        A = 97, B = 98, C = 99, D = 100, E = 101, F = 102, G = 103, H = 104, I = 105, J = 106, K = 107, L = 108, M = 109,
        N = 110, O = 111, P = 112, Q = 113, R = 114, S = 115, T = 116, U = 117, V = 118, W = 119, X = 120, Y = 121, Z = 122,
        Delete = 127,
        Keypad0 = 256, Keypad1 = 257, Keypad2 = 258, Keypad3 = 259, Keypad4 = 260, Keypad5 = 261, Keypad6 = 262, Keypad7 = 263, Keypad8 = 264, Keypad9 = 265,
        KeypadPeriod = 266, KeypadDivide = 267, KeypadMultiply = 268, KeypadMinus = 269, KeypadPlus = 270, KeypadEnter = 271, KeypadEquals = 272,
        UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276, Insert = 277, Home = 278, End = 279, PageUp = 280, PageDown = 281,
        F1 = 282, F2 = 283, F3 = 284, F4 = 285, F5 = 286, F6 = 287, F7 = 288, F8 = 289, F9 = 290, F10 = 291, F11 = 292, F12 = 293,
        Numlock = 300, CapsLock = 301, ScrollLock = 302, RightShift = 303, LeftShift = 304, RightControl = 305, LeftControl = 306,
        RightAlt = 307, LeftAlt = 308, RightCommand = 309, LeftCommand = 310, LeftWindows = 311, RightWindows = 312,
        Mouse0 = 323, Mouse1 = 324, Mouse2 = 325, Mouse3 = 326, Mouse4 = 327,
        JoystickButton0 = 330, JoystickButton1 = 331, JoystickButton2 = 332, JoystickButton3 = 333,
    }

    public enum TouchPhase { Began = 0, Moved = 1, Stationary = 2, Ended = 3, Canceled = 4 }
    public enum TouchType { Direct = 0, Indirect = 1, Stylus = 2 }
    public enum DeviceOrientation { Unknown = 0, Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, FaceUp = 5, FaceDown = 6 }

    public struct Touch
    {
        public int fingerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 rawPosition { get; set; }
        public Vector2 deltaPosition { get; set; }
        public float deltaTime { get; set; }
        public int tapCount { get; set; }
        public TouchPhase phase { get; set; }
        public float pressure { get; set; }
        public float maximumPossiblePressure { get; set; }
        public TouchType type { get; set; }
        public float radius { get; set; }
    }

    public struct Gyroscope
    {
        public Vector3 rotationRate => default;
        public Quaternion attitude => Quaternion.identity;
        public Vector3 gravity => new(0, -1, 0);
        public bool enabled { get; set; }
    }

    public static class Input
    {
        public static Vector3 acceleration => InputSystem.Accelerometer.current?.acceleration.value ?? Vector3.zero;
        public static Vector3 mousePosition
        {
            get { var m = InputSystem.Mouse.current; return m == null ? Vector3.zero : (Vector3)m.position.ReadValue(); }
        }
        public static Vector2 mouseScrollDelta => InputSystem.Mouse.current?.scroll.ReadValue() ?? Vector2.zero;
        public static bool mousePresent => InputSystem.Mouse.current != null;
        public static bool touchSupported => InputSystem.Touchscreen.current != null;
        public static bool multiTouchEnabled { get; set; } = true;
        public static bool simulateMouseWithTouches { get; set; } = true;
        public static DeviceOrientation deviceOrientation => DeviceOrientation.LandscapeLeft;
        public static Gyroscope gyro => default;
        public static string inputString { get; internal set; } = string.Empty;
        public static bool anyKey => InputSystem.Keyboard.current?.anyKey.isPressed == true || GetMouseButton(0) || GetMouseButton(1);
        public static bool anyKeyDown => InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame == true || GetMouseButtonDown(0) || GetMouseButtonDown(1);

        public static Touch[] touches
        {
            get
            {
                var list = new List<Touch>();
                foreach (var t in InputSystem.EnhancedTouch.Touch.activeTouches)
                    list.Add(new Touch
                    {
                        fingerId = t.touchId, position = t.screenPosition, rawPosition = t.screenPosition, deltaPosition = t.delta,
                        phase = t.phase switch
                        {
                            InputSystem.TouchPhase.Began => TouchPhase.Began,
                            InputSystem.TouchPhase.Moved => TouchPhase.Moved,
                            InputSystem.TouchPhase.Ended => TouchPhase.Ended,
                            InputSystem.TouchPhase.Canceled => TouchPhase.Canceled,
                            _ => TouchPhase.Stationary,
                        },
                        tapCount = 1,
                    });
                return list.ToArray();
            }
        }

        public static int touchCount => InputSystem.EnhancedTouch.Touch.activeTouches.Count;
        public static Touch GetTouch(int index) => touches[index];

        static InputSystem.Controls.ButtonControl Control(KeyCode key)
        {
            var mouse = InputSystem.Mouse.current;
            switch (key)
            {
                case KeyCode.Mouse0: return mouse?.leftButton;
                case KeyCode.Mouse1: return mouse?.rightButton;
                case KeyCode.Mouse2: return mouse?.middleButton;
                case KeyCode.Mouse3: return mouse?.backButton;
                case KeyCode.Mouse4: return mouse?.forwardButton;
            }
            var kb = InputSystem.Keyboard.current;
            if (kb == null) return null;
            var k = ToKey(key);
            return k == InputSystem.Key.None ? null : kb[k];
        }

        static InputSystem.Key ToKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return InputSystem.Key.A + (key - KeyCode.A);
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9) return InputSystem.Key.Digit1 + (key - KeyCode.Alpha1);
            if (key == KeyCode.Alpha0) return InputSystem.Key.Digit0;
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return InputSystem.Key.F1 + (key - KeyCode.F1);
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return InputSystem.Key.Numpad0 + (key - KeyCode.Keypad0);
            return key switch
            {
                KeyCode.Space => InputSystem.Key.Space,
                KeyCode.Return => InputSystem.Key.Enter,
                KeyCode.KeypadEnter => InputSystem.Key.NumpadEnter,
                KeyCode.Tab => InputSystem.Key.Tab,
                KeyCode.Escape => InputSystem.Key.Escape,
                KeyCode.Backspace => InputSystem.Key.Backspace,
                KeyCode.Delete => InputSystem.Key.Delete,
                KeyCode.UpArrow => InputSystem.Key.UpArrow,
                KeyCode.DownArrow => InputSystem.Key.DownArrow,
                KeyCode.LeftArrow => InputSystem.Key.LeftArrow,
                KeyCode.RightArrow => InputSystem.Key.RightArrow,
                KeyCode.LeftShift => InputSystem.Key.LeftShift,
                KeyCode.RightShift => InputSystem.Key.RightShift,
                KeyCode.LeftControl => InputSystem.Key.LeftCtrl,
                KeyCode.RightControl => InputSystem.Key.RightCtrl,
                KeyCode.LeftAlt => InputSystem.Key.LeftAlt,
                KeyCode.RightAlt => InputSystem.Key.RightAlt,
                KeyCode.Comma => InputSystem.Key.Comma,
                KeyCode.Period => InputSystem.Key.Period,
                KeyCode.Slash => InputSystem.Key.Slash,
                KeyCode.Semicolon => InputSystem.Key.Semicolon,
                KeyCode.Quote => InputSystem.Key.Quote,
                KeyCode.LeftBracket => InputSystem.Key.LeftBracket,
                KeyCode.RightBracket => InputSystem.Key.RightBracket,
                KeyCode.Minus => InputSystem.Key.Minus,
                KeyCode.Equals => InputSystem.Key.Equals,
                KeyCode.BackQuote => InputSystem.Key.Backquote,
                KeyCode.Backslash => InputSystem.Key.Backslash,
                KeyCode.Home => InputSystem.Key.Home,
                KeyCode.End => InputSystem.Key.End,
                KeyCode.PageUp => InputSystem.Key.PageUp,
                KeyCode.PageDown => InputSystem.Key.PageDown,
                KeyCode.Insert => InputSystem.Key.Insert,
                _ => InputSystem.Key.None,
            };
        }

        public static bool GetKey(KeyCode key) => Control(key)?.isPressed == true;
        public static bool GetKeyDown(KeyCode key) => Control(key)?.wasPressedThisFrame == true;
        public static bool GetKeyUp(KeyCode key) => Control(key)?.wasReleasedThisFrame == true;
        public static bool GetMouseButton(int button) => Control(KeyCode.Mouse0 + button)?.isPressed == true;
        public static bool GetMouseButtonDown(int button) => Control(KeyCode.Mouse0 + button)?.wasPressedThisFrame == true;
        public static bool GetMouseButtonUp(int button) => Control(KeyCode.Mouse0 + button)?.wasReleasedThisFrame == true;

        public static float GetAxis(string axisName) => GetAxisRaw(axisName);

        public static float GetAxisRaw(string axisName)
        {
            var kb = InputSystem.Keyboard.current;
            var pad = InputSystem.Gamepad.current;
            float Keys(InputSystem.Key neg, InputSystem.Key pos, InputSystem.Key neg2, InputSystem.Key pos2)
                => kb == null ? 0f : (kb[pos].isPressed || kb[pos2].isPressed ? 1f : 0f) - (kb[neg].isPressed || kb[neg2].isPressed ? 1f : 0f);
            switch (axisName)
            {
                case "Horizontal":
                    return Mathf.Clamp(Keys(InputSystem.Key.A, InputSystem.Key.D, InputSystem.Key.LeftArrow, InputSystem.Key.RightArrow)
                                       + (pad?.leftStick.x.ReadValue() ?? 0f), -1f, 1f);
                case "Vertical":
                    return Mathf.Clamp(Keys(InputSystem.Key.S, InputSystem.Key.W, InputSystem.Key.DownArrow, InputSystem.Key.UpArrow)
                                       + (pad?.leftStick.y.ReadValue() ?? 0f), -1f, 1f);
                case "Mouse X": return (InputSystem.Mouse.current?.delta.x.ReadValue() ?? 0f) * 0.1f;
                case "Mouse Y": return (InputSystem.Mouse.current?.delta.y.ReadValue() ?? 0f) * 0.1f;
                case "Mouse ScrollWheel": return (InputSystem.Mouse.current?.scroll.y.ReadValue() ?? 0f) / 120f;
                default: return 0f;
            }
        }

        public static bool GetButton(string buttonName) => buttonName switch
        {
            "Fire1" => GetMouseButton(0) || GetKey(KeyCode.LeftControl),
            "Fire2" => GetMouseButton(1) || GetKey(KeyCode.LeftAlt),
            "Jump" => GetKey(KeyCode.Space),
            "Submit" => GetKey(KeyCode.Return),
            "Cancel" => GetKey(KeyCode.Escape),
            _ => false,
        };

        public static bool GetButtonDown(string buttonName) => buttonName switch
        {
            "Fire1" => GetMouseButtonDown(0), "Fire2" => GetMouseButtonDown(1), "Jump" => GetKeyDown(KeyCode.Space),
            "Submit" => GetKeyDown(KeyCode.Return), "Cancel" => GetKeyDown(KeyCode.Escape), _ => false,
        };

        public static bool GetButtonUp(string buttonName) => buttonName switch
        {
            "Fire1" => GetMouseButtonUp(0), "Fire2" => GetMouseButtonUp(1), "Jump" => GetKeyUp(KeyCode.Space),
            "Submit" => GetKeyUp(KeyCode.Return), "Cancel" => GetKeyUp(KeyCode.Escape), _ => false,
        };

        public static void ResetInputAxes() { }
    }

    // ── Gizmos / IMGUI (editor + debug overlays: recorded, not drawn) ──────

    public static class Gizmos
    {
        public static Color color { get; set; } = Color.white;
        public static Matrix4x4 matrix { get; set; } = Matrix4x4.identity;
        public static bool enabled { get; set; }
        public static void DrawLine(Vector3 from, Vector3 to) { }
        public static void DrawRay(Vector3 from, Vector3 direction) { }
        public static void DrawRay(Ray r) { }
        public static void DrawSphere(Vector3 center, float radius) { }
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawCube(Vector3 center, Vector3 size) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawMesh(Mesh mesh, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default) { }
        public static void DrawWireMesh(Mesh mesh, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default) { }
        public static void DrawIcon(Vector3 center, string name, bool allowScaling = true) { }
        public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect) { }
    }

    public sealed class GUISkin : ScriptableObject
    {
        public GUIStyle label { get; set; } = new GUIStyle();
        public GUIStyle box { get; set; } = new GUIStyle();
        public GUIStyle button { get; set; } = new GUIStyle();
        public GUIStyle textField { get; set; } = new GUIStyle();
        public GUIStyle window { get; set; } = new GUIStyle();
        public GUIStyle toggle { get; set; } = new GUIStyle();
        public Font font { get; set; }
    }

    /// <summary>
    /// Immediate-mode GUI (original: UnityEngine.GUI). OnGUI is not part of the port's frame,
    /// so IMGUI overlays (debug HUDs) draw nothing; calls are accepted and ignored.
    /// </summary>
    public static class GUI
    {
        public static GUISkin skin { get; set; } = ScriptableObject.CreateInstance<GUISkin>();
        public static Color color { get; set; } = Color.white;
        public static Color backgroundColor { get; set; } = Color.white;
        public static Color contentColor { get; set; } = Color.white;
        public static bool enabled { get; set; } = true;
        public static int depth { get; set; }
        public static Matrix4x4 matrix { get; set; } = Matrix4x4.identity;
        public static bool changed { get; set; }
        public static void Label(Rect position, string text) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
        public static void Label(Rect position, GUIContent content, GUIStyle style) { }
        public static void Box(Rect position, string text) { }
        public static void Box(Rect position, string text, GUIStyle style) { }
        public static void Box(Rect position, GUIContent content, GUIStyle style) { }
        public static bool Button(Rect position, string text) => false;
        public static bool Button(Rect position, string text, GUIStyle style) => false;
        public static bool Toggle(Rect position, bool value, string text) => value;
        public static string TextField(Rect position, string text) => text;
        public static float HorizontalSlider(Rect position, float value, float left, float right) => value;
        public static void DrawTexture(Rect position, Texture image) { }
        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend = true, float imageAspect = 0f) { }
        public static void BeginGroup(Rect position) { }
        public static void EndGroup() { }
        public delegate void WindowFunction(int id);
        public static Rect Window(int id, Rect clientRect, WindowFunction func, string text) => clientRect;
        public static Rect Window(int id, Rect clientRect, WindowFunction func, string text, GUIStyle style) => clientRect;
        public static void DragWindow() { }
        public static void DragWindow(Rect position) { }
    }

    public enum ScaleMode { StretchToFill = 0, ScaleAndCrop = 1, ScaleToFit = 2 }

    public static class GUILayout
    {
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) { }
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => false;
        public static void Box(string text, params GUILayoutOption[] options) { }
        public static void Box(string text, GUIStyle style, params GUILayoutOption[] options) { }
        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, string text, params GUILayoutOption[] options) => screenRect;
        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, string text, GUIStyle style, params GUILayoutOption[] options) => screenRect;
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => value;
        public static void BeginArea(Rect screenRect) { }
        public static void EndArea() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static GUILayoutOption Width(float w) => new();
        public static GUILayoutOption Height(float h) => new();
    }

    public sealed class GUILayoutOption { }
}

namespace CosmicShore.Engine.Rendering
{
    /// <summary>Graphics API in use (original: UnityEngine.Rendering.GraphicsDeviceType; values match).</summary>
    public enum GraphicsDeviceType
    {
        OpenGLES2 = 8, Direct3D11 = 2, Null = 4, OpenGLES3 = 11, PlayStation4 = 13, XboxOne = 14,
        Metal = 16, OpenGLCore = 17, Direct3D12 = 18, Vulkan = 21, Switch = 22, XboxOneD3D12 = 23,
        GameCoreXboxOne = 24, GameCoreXboxSeries = 25, PlayStation5 = 26, PlayStation5NGGC = 27, WebGPU = 28,
    }
}

namespace CosmicShore.Engine.Profiling
{
    /// <summary>Memory + sampling profiler (original: UnityEngine.Profiling.Profiler) over the managed runtime.</summary>
    public static class Profiler
    {
        public static bool enabled { get; set; }
        public static bool supported => true;
        public static int maxUsedMemory { get; set; }
        public static long GetTotalAllocatedMemoryLong() => GC.GetTotalMemory(false);
        public static long GetTotalReservedMemoryLong() => GC.GetGCMemoryInfo().HeapSizeBytes;
        public static long GetTotalUnusedReservedMemoryLong() => Math.Max(0, GetTotalReservedMemoryLong() - GetTotalAllocatedMemoryLong());
        public static long GetMonoUsedSizeLong() => GC.GetTotalMemory(false);
        public static long GetMonoHeapSizeLong() => GC.GetGCMemoryInfo().HeapSizeBytes;
        public static long GetAllocatedMemoryForGraphicsDriver() => GraphicsMemoryBytes;
        public static long GetRuntimeMemorySizeLong(Object o) => 0;
        public static long GetTempAllocatorSize() => 0;
        /// <summary>Set by the renderer (texture + buffer uploads it tracks).</summary>
        public static long GraphicsMemoryBytes;
        public static void BeginSample(string name) { }
        public static void BeginSample(string name, Object targetObject) { }
        public static void EndSample() { }
        public static void BeginThreadProfiling(string threadGroupName, string threadName) { }
        public static void EndThreadProfiling() { }
    }
}
