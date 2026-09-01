using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay.MultiMouse
{
    /// <summary>
    /// Wraps Unity Input System's <see cref="Mouse.all"/>. On macOS/Linux this
    /// reliably reports each physical mouse separately. On Windows the OS
    /// merges all mice into one device by default, so this provider reports
    /// only one - use <c>Win32RawInputMultiMouseProvider</c> for separation.
    /// </summary>
    public sealed class UnityMultiMouseProvider : IMultiMouseProvider
    {
        private sealed class Device : IMultiMouseDevice
        {
            private readonly Mouse mouse;
            private Vector2 accumulated;

            private bool prevLeft, prevRight, prevMiddle;
            private bool leftDown, leftUp, rightDown, rightUp, middleDown, middleUp;

            public Device(Mouse mouse) { this.mouse = mouse; }

            public string Id => mouse.deviceId.ToString();
            public string DisplayName => mouse.displayName ?? "Mouse";

            public bool LeftButton => mouse.leftButton.isPressed;
            public bool RightButton => mouse.rightButton.isPressed;
            public bool MiddleButton => mouse.middleButton.isPressed;

            public bool LeftButtonDownThisFrame => leftDown;
            public bool LeftButtonUpThisFrame => leftUp;
            public bool RightButtonDownThisFrame => rightDown;
            public bool RightButtonUpThisFrame => rightUp;
            public bool MiddleButtonDownThisFrame => middleDown;
            public bool MiddleButtonUpThisFrame => middleUp;

            public Vector2 ConsumeDelta()
            {
                var d = accumulated;
                accumulated = Vector2.zero;
                return d;
            }

            public void Tick()
            {
                accumulated += mouse.delta.ReadValue();

                bool l = mouse.leftButton.isPressed;
                bool r = mouse.rightButton.isPressed;
                bool m = mouse.middleButton.isPressed;

                leftDown = l && !prevLeft;
                leftUp = !l && prevLeft;
                rightDown = r && !prevRight;
                rightUp = !r && prevRight;
                middleDown = m && !prevMiddle;
                middleUp = !m && prevMiddle;

                prevLeft = l; prevRight = r; prevMiddle = m;
            }
        }

        private readonly List<Device> devices = new();

        public bool IsAvailable => devices.Count >= 2;
        public int DeviceCount => devices.Count;

        public UnityMultiMouseProvider() { Refresh(); }

        public void Refresh()
        {
            devices.Clear();
            foreach (var device in InputSystem.devices)
                if (device is Mouse m)
                    devices.Add(new Device(m));
        }

        public IMultiMouseDevice GetDevice(int index)
        {
            return index >= 0 && index < devices.Count ? devices[index] : null;
        }

        public void Tick()
        {
            // Cheap re-enumerate while we don't yet have a stable pair -
            // catches mice plugged in after the game starts.
            if (devices.Count < 2) Refresh();

            for (int i = 0; i < devices.Count; i++)
                devices[i].Tick();
        }

        public void Shutdown() { devices.Clear(); }
    }
}
