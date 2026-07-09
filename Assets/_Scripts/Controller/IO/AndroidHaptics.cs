using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Direct Android Vibrator access for the stripped mobile build.
    ///
    /// Why this exists: the project's NiceVibrations/Lofelt path routes through
    /// <c>liblofelt_sdk.so</c> and only produces a buzz when the device meets
    /// Lofelt's "advanced requirements" (amplitude-controlled haptics, API 26+
    /// with capable hardware) OR its version-supported fallback fires. On a
    /// mid/older Android phone that gate can silently no-op, so skim/crystal
    /// haptics never reach the motor even though every wiring layer above is
    /// correct. This helper talks to <c>android.os.Vibrator</c> straight through
    /// JNI, which works on any Android device that holds the VIBRATE permission
    /// (declared by the bundled LofeltHaptics.aar manifest and merged into the
    /// app). One-shot, short-duration pulses — suited to rapid skim feedback.
    ///
    /// All JNI is wrapped so a failure is a silent no-op, never a crash. Editor
    /// and non-Android platforms compile this out and keep the Lofelt path.
    /// </summary>
    public static class AndroidHaptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static bool _init;
        static bool _available;
        static int _sdkInt;
        static AndroidJavaObject _vibrator;
        static AndroidJavaClass _effectClass;

        static void EnsureInit()
        {
            if (_init) return;
            _init = true;

            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _sdkInt = version.GetStatic<int>("SDK_INT");

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                if (_sdkInt >= 31) // Android 12+: VibratorManager -> getDefaultVibrator()
                {
                    using var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                    _vibrator = manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                }

                if (_vibrator == null)
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (_vibrator != null && _sdkInt >= 26)
                    _effectClass = new AndroidJavaClass("android.os.VibrationEffect");

                _available = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            }
            catch (System.Exception e)
            {
                _available = false;
                Debug.LogWarning($"[AndroidHaptics] init failed, haptics disabled: {e.Message}");
            }
        }

        /// <summary>
        /// Fires a single vibration pulse. <paramref name="durationMs"/> is the
        /// pulse length; <paramref name="amplitude"/> is 1..255 (only honoured on
        /// API 26+; older devices vibrate at fixed strength for the duration).
        /// </summary>
        public static void Pulse(long durationMs, int amplitude)
        {
            EnsureInit();
            if (!_available || durationMs <= 0) return;

            try
            {
                if (_sdkInt >= 26 && _effectClass != null)
                {
                    int amp = Mathf.Clamp(amplitude, 1, 255);
                    using var effect = _effectClass.CallStatic<AndroidJavaObject>("createOneShot", durationMs, amp);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", durationMs);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AndroidHaptics] vibrate failed: {e.Message}");
            }
        }
#else
        public static void Pulse(long durationMs, int amplitude) { }
#endif
    }
}
