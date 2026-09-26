#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The conditions a measurement was taken under, saved WITH the measurement. A frame time is
    /// only comparable with another taken under the same conditions, and every one of these has
    /// moved a number in this project by more than the change being measured: Burst was once OFF
    /// in the Editor for weeks (every job ~20× slower), an Editor frame is 2–3× a build frame,
    /// the Profiler recording inflates script time, and an unfocused Editor throttles itself.
    ///
    /// <para>Captured once, at the end of a run, so reading it costs the run nothing.</para>
    /// </summary>
    [Serializable]
    public class RunEnvironment
    {
        /// <summary>"Editor", "Development build" or "Release build".</summary>
        public string runtime;
        public string scriptingBackend;
        public string platform;
        public string unityVersion;
        public string appVersion;
        public string gpu;
        public string graphicsApi;
        public string cpu;
        public int cpuCores;
        public int systemMemoryMB;
        public string resolution;
        public string fullScreenMode;
        public string qualityLevel;
        /// <summary>Burst compilation switched on. Off makes every Burst job ~20× slower.</summary>
        public bool burstEnabled;
        /// <summary>The Profiler was recording during the run, so script time includes its overhead.</summary>
        public bool profilerRecording;
        /// <summary>The application had focus. An unfocused Editor throttles its own frame rate.</summary>
        public bool focused;

        public static RunEnvironment Capture()
        {
            var env = new RunEnvironment
            {
                runtime = Application.isEditor ? "Editor"
                        : Debug.isDebugBuild ? "Development build" : "Release build",
#if ENABLE_IL2CPP
                scriptingBackend = "IL2CPP",
#else
                scriptingBackend = "Mono",
#endif
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                appVersion = Application.version,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                cpu = SystemInfo.processorType,
                cpuCores = SystemInfo.processorCount,
                systemMemoryMB = SystemInfo.systemMemorySize,
                resolution = $"{Screen.width}x{Screen.height}",
                fullScreenMode = Screen.fullScreenMode.ToString(),
                burstEnabled = Unity.Burst.BurstCompiler.IsEnabled,
                profilerRecording = UnityEngine.Profiling.Profiler.enabled,
                focused = Application.isFocused,
            };

            int level = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            env.qualityLevel = level >= 0 && level < names.Length ? names[level] : level.ToString();
            return env;
        }

        /// <summary>One line for the report's .txt twin.</summary>
        public string Describe() =>
            $"{runtime} ({scriptingBackend}, {platform}, Unity {unityVersion}) · " +
            $"{resolution} {fullScreenMode} · quality {qualityLevel} · Burst {(burstEnabled ? "on" : "OFF")} · " +
            $"Profiler {(profilerRecording ? "RECORDING" : "off")} · {(focused ? "focused" : "NOT FOCUSED")} · " +
            $"{gpu} ({graphicsApi}) · {cpu} ×{cpuCores} · {systemMemoryMB} MB";
    }
}
#endif
