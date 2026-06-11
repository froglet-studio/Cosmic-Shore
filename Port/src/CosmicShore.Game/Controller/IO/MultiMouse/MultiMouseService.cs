using CosmicShore.Engine;

namespace CosmicShore.Gameplay.MultiMouse
{
    /// <summary>
    /// Picks the best multi-mouse provider for the current platform and
    /// exposes a stable, ordered list of mouse devices. Tick once per frame
    /// before reading any device state.
    ///
    /// Order of preference:
    ///  1. Unity Input System's <c>Mouse.all</c> if it already separates
    ///     mice (macOS / Linux). Cheap, no native code.
    ///  2. Win32 Raw Input on Windows when only one Unity mouse is reported.
    ///  3. Falls back to whatever Unity reports (single device).
    /// </summary>
    public sealed class MultiMouseService
    {
        private readonly UnityMultiMouseProvider unityProvider;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private readonly Win32RawInputMultiMouseProvider win32Provider;
#endif
        private IMultiMouseProvider active;

        public IMultiMouseProvider Active => active;

        public int DeviceCount => active?.DeviceCount ?? 0;

        public bool HasTwoMice => DeviceCount >= 2;

        public MultiMouseService()
        {
            unityProvider = new UnityMultiMouseProvider();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            win32Provider = new Win32RawInputMultiMouseProvider();
#endif
            active = unityProvider;
        }

        public void Tick()
        {
            unityProvider.Tick();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            win32Provider.Tick();
#endif
            // If Unity sees 2+ mice, prefer it (cheaper, no native marshalling).
            // Otherwise on Windows fall back to Raw Input once it's enumerated 2+.
            if (unityProvider.DeviceCount >= 2)
                active = unityProvider;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            else if (win32Provider.DeviceCount >= 2)
                active = win32Provider;
#endif
            else
                active = unityProvider;
        }

        public IMultiMouseDevice GetDevice(int index)
        {
            return active?.GetDevice(index);
        }

        public void Refresh()
        {
            unityProvider.Refresh();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            win32Provider.Refresh();
#endif
        }

        public void Shutdown()
        {
            unityProvider.Shutdown();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            win32Provider.Shutdown();
#endif
        }
    }
}
