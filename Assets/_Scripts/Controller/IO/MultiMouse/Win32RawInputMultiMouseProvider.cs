#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace CosmicShore.Gameplay.MultiMouse
{
    /// <summary>
    /// Windows Raw Input provider. Spins up a worker thread that owns a
    /// message-only window, registers for WM_INPUT mouse events, and
    /// accumulates per-device deltas + button state. The main thread
    /// snapshots that state once per frame via <see cref="Tick"/>.
    ///
    /// We do NOT pass <c>RIDEV_NOLEGACY</c>: legacy WM_MOUSEMOVE messages
    /// keep flowing so Unity's <c>Mouse.current</c> and the OS cursor still
    /// behave normally - we only need the per-device deltas as an extra
    /// channel.
    /// </summary>
    public sealed class Win32RawInputMultiMouseProvider : IMultiMouseProvider
    {
        // -------- Win32 interop --------

        private const int WM_INPUT = 0x00FF;
        private const int WM_QUIT = 0x0012;
        private const int WM_DESTROY = 0x0002;

        private const uint RID_INPUT = 0x10000003;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIDEV_REMOVE = 0x00000001;

        private const ushort RIM_TYPEMOUSE = 0;

        private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;

        private const int CW_USEDEFAULT = unchecked((int)0x80000000);
        private static readonly IntPtr HWND_MESSAGE = new(-3);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        // Mirrors the C RAWMOUSE struct exactly. The C struct uses a union
        // { ULONG ulButtons; struct { USHORT usButtonFlags; USHORT usButtonData } }
        // which is ULONG-aligned, so there's a 2-byte padding gap between
        // usFlags (USHORT at offset 0) and the union (DWORD-aligned offset 4).
        // C# Sequential layout with natural alignment of USHORT is 2, so we
        // must add the padding explicitly.
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public ushort _pad0;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT_MOUSE
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            [MarshalAs(UnmanagedType.FunctionPtr)] public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
            int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG msg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string name);

        // -------- Provider state --------

        private sealed class DeviceState
        {
            public IntPtr Handle;
            public int DxAccum;
            public int DyAccum;
            public bool LeftPressed;
            public bool RightPressed;
            public bool MiddlePressed;
            public bool LeftDownPending;
            public bool LeftUpPending;
            public bool RightDownPending;
            public bool RightUpPending;
            public bool MiddleDownPending;
            public bool MiddleUpPending;
        }

        private sealed class FrameView : IMultiMouseDevice
        {
            private readonly Win32RawInputMultiMouseProvider provider;
            private readonly IntPtr handle;

            public FrameView(Win32RawInputMultiMouseProvider provider, IntPtr handle)
            {
                this.provider = provider;
                this.handle = handle;
            }

            public string Id => handle.ToString();
            public string DisplayName => $"Raw Mouse {Id}";

            public bool LeftButton { get; internal set; }
            public bool RightButton { get; internal set; }
            public bool MiddleButton { get; internal set; }

            public bool LeftButtonDownThisFrame { get; internal set; }
            public bool LeftButtonUpThisFrame { get; internal set; }
            public bool RightButtonDownThisFrame { get; internal set; }
            public bool RightButtonUpThisFrame { get; internal set; }
            public bool MiddleButtonDownThisFrame { get; internal set; }
            public bool MiddleButtonUpThisFrame { get; internal set; }

            internal Vector2 FrameDelta;

            public Vector2 ConsumeDelta()
            {
                var d = FrameDelta;
                FrameDelta = Vector2.zero;
                return d;
            }

            public void Tick() { /* service ticks */ }
        }

        private readonly object stateLock = new();
        private readonly Dictionary<IntPtr, DeviceState> states = new();
        private readonly List<IntPtr> orderedHandles = new();

        private readonly List<FrameView> views = new();

        private Thread thread;
        private volatile bool running;
        private IntPtr hWnd;
        private WndProcDelegate wndProcDelegate; // GC root
        private string className;

        private static readonly uint HeaderSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();

        public bool IsAvailable
        {
            get { lock (stateLock) return orderedHandles.Count >= 2; }
        }

        public int DeviceCount
        {
            get { lock (stateLock) return orderedHandles.Count; }
        }

        public Win32RawInputMultiMouseProvider()
        {
            running = true;
            thread = new Thread(WorkerThreadMain) { IsBackground = true, Name = "CS RawInput" };
            thread.Start();
        }

        public IMultiMouseDevice GetDevice(int index)
        {
            return index >= 0 && index < views.Count ? views[index] : null;
        }

        public void Tick()
        {
            // Snapshot the worker-thread state into per-frame view objects.
            lock (stateLock)
            {
                while (views.Count < orderedHandles.Count)
                    views.Add(new FrameView(this, orderedHandles[views.Count]));

                for (int i = 0; i < orderedHandles.Count; i++)
                {
                    var handle = orderedHandles[i];
                    if (!states.TryGetValue(handle, out var s)) continue;

                    var v = views[i];
                    v.FrameDelta += new Vector2(s.DxAccum, s.DyAccum);
                    s.DxAccum = 0;
                    s.DyAccum = 0;

                    v.LeftButton = s.LeftPressed;
                    v.RightButton = s.RightPressed;
                    v.MiddleButton = s.MiddlePressed;

                    v.LeftButtonDownThisFrame = s.LeftDownPending;
                    v.LeftButtonUpThisFrame = s.LeftUpPending;
                    v.RightButtonDownThisFrame = s.RightDownPending;
                    v.RightButtonUpThisFrame = s.RightUpPending;
                    v.MiddleButtonDownThisFrame = s.MiddleDownPending;
                    v.MiddleButtonUpThisFrame = s.MiddleUpPending;

                    s.LeftDownPending = s.LeftUpPending = false;
                    s.RightDownPending = s.RightUpPending = false;
                    s.MiddleDownPending = s.MiddleUpPending = false;
                }
            }
        }

        public void Refresh() { /* hot-plug auto-detected as new HANDLEs arrive */ }

        public void Shutdown()
        {
            if (!running) return;
            running = false;
            try
            {
                if (hWnd != IntPtr.Zero)
                    PostMessage(hWnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
            catch { /* ignore */ }
            try { thread?.Join(500); } catch { /* ignore */ }
            thread = null;
        }

        // -------- Worker thread --------

        private void WorkerThreadMain()
        {
            try
            {
                if (!CreateMessageWindow()) return;
                if (!RegisterDevices()) return;

                while (running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CS RawInput] worker thread fault: {e.Message}");
            }
            finally
            {
                if (hWnd != IntPtr.Zero) DestroyWindow(hWnd);
                hWnd = IntPtr.Zero;
            }
        }

        private bool CreateMessageWindow()
        {
            wndProcDelegate = WndProc;
            className = "CSRawInputMsgWnd_" + Guid.NewGuid().ToString("N");

            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = wndProcDelegate,
                hInstance = GetModuleHandle(null),
                lpszClassName = className,
            };

            if (RegisterClassEx(ref wc) == 0)
            {
                Debug.LogWarning($"[CS RawInput] RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            hWnd = CreateWindowEx(0, className, className, 0,
                CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
                HWND_MESSAGE, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

            if (hWnd == IntPtr.Zero)
            {
                Debug.LogWarning($"[CS RawInput] CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
                return false;
            }
            return true;
        }

        private bool RegisterDevices()
        {
            var rid = new RAWINPUTDEVICE[]
            {
                new()
                {
                    usUsagePage = 0x01, // Generic desktop controls
                    usUsage = 0x02,     // Mouse
                    dwFlags = RIDEV_INPUTSINK,
                    hwndTarget = hWnd,
                },
            };

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                Debug.LogWarning($"[CS RawInput] RegisterRawInputDevices failed: {Marshal.GetLastWin32Error()}");
                return false;
            }
            return true;
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                HandleRawInput(lParam);
                return IntPtr.Zero;
            }
            if (msg == WM_DESTROY)
            {
                PostMessage(IntPtr.Zero, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                return IntPtr.Zero;
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void HandleRawInput(IntPtr hRawInput)
        {
            uint size = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, HeaderSize);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, buffer, ref size, HeaderSize) != size)
                    return;

                var ri = Marshal.PtrToStructure<RAWINPUT_MOUSE>(buffer);
                if (ri.header.dwType != RIM_TYPEMOUSE) return;

                var handle = ri.header.hDevice;
                int dx = ri.mouse.lLastX;
                int dy = ri.mouse.lLastY;
                ushort flags = ri.mouse.usButtonFlags;

                lock (stateLock)
                {
                    if (!states.TryGetValue(handle, out var s))
                    {
                        s = new DeviceState { Handle = handle };
                        states[handle] = s;
                        orderedHandles.Add(handle);
                    }

                    s.DxAccum += dx;
                    s.DyAccum += dy;

                    if ((flags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
                    {
                        s.LeftPressed = true;
                        s.LeftDownPending = true;
                    }
                    if ((flags & RI_MOUSE_LEFT_BUTTON_UP) != 0)
                    {
                        s.LeftPressed = false;
                        s.LeftUpPending = true;
                    }
                    if ((flags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
                    {
                        s.RightPressed = true;
                        s.RightDownPending = true;
                    }
                    if ((flags & RI_MOUSE_RIGHT_BUTTON_UP) != 0)
                    {
                        s.RightPressed = false;
                        s.RightUpPending = true;
                    }
                    if ((flags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0)
                    {
                        s.MiddlePressed = true;
                        s.MiddleDownPending = true;
                    }
                    if ((flags & RI_MOUSE_MIDDLE_BUTTON_UP) != 0)
                    {
                        s.MiddlePressed = false;
                        s.MiddleUpPending = true;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
#endif
