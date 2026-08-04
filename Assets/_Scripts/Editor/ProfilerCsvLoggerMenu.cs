using System.IO;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor menu wrapper around <see cref="ProfilerCsvLogger"/> so a per-frame CSV capture
    /// can be started/stopped during Play Mode without wiring a component into the scene.
    /// Menu: FrogletTools > Performance > Profiler CSV Logger.
    /// </summary>
    static class ProfilerCsvLoggerMenu
    {
        const string Root = "FrogletTools/Performance/Profiler CSV Logger/";
        const string DefaultFolder = "ProfilerCaptures";

        [MenuItem(Root + "Start Capture", priority = 100)]
        static void Start()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Profiler CSV Logger",
                    "Enter Play Mode first, then Start Capture.\n\n" +
                    "The logger samples live frames, so it only runs during play. " +
                    "Tip: enter your gameplay scene, Start when the round begins, play 2-3 min, then Stop & Save.",
                    "OK");
                return;
            }

            if (ProfilerCsvLogger.IsCapturing)
            {
                Debug.LogWarning("[ProfilerCsvLogger] A capture is already running. Stop it first.");
                return;
            }

            ProfilerCsvLogger.StartCapture();
        }

        // "&&" renders as a single literal "&" in the menu label.
        [MenuItem(Root + "Stop Capture && Save", priority = 101)]
        static void Stop()
        {
            string path = ProfilerCsvLogger.StopCapture();
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[ProfilerCsvLogger] Saved capture: {path}");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogWarning("[ProfilerCsvLogger] No active capture to stop.");
            }
        }

        [MenuItem(Root + "Open Capture Folder", priority = 102)]
        static void OpenFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, DefaultFolder);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }
    }
}
