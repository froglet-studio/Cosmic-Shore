using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// On-device boot tracer for the untethered mobile workflow (no PC / adb / logcat).
    ///
    /// The problem it solves: a build crashes on launch and we have no way to read the log. This
    /// records boot <b>checkpoints</b> + captured errors to a persistent file, and on the NEXT
    /// launch renders the PREVIOUS run on screen (IMGUI) — because a crash-looping app is reopened,
    /// the prior crash's trace shows even if that run died. Screenshot it to report where it died.
    ///
    /// Works in any build config (Release included — not gated on DEVELOPMENT_BUILD). Installed at
    /// the earliest runtime hook so it captures as much of the boot as possible. Gated on
    /// <see cref="PerfStrip.ShowBootTrace"/>.
    ///
    /// Fallback retrieval: the same trace is at
    /// <c>Android/data/&lt;package&gt;/files/cs_boottrace.txt</c> (Application.persistentDataPath),
    /// browsable with the phone's Files app if the on-screen overlay never renders (a pre-first-frame
    /// native crash) — that "no overlay" outcome is itself the diagnosis: the crash is before the
    /// first frame (native: graphics / a plugin's static init).
    /// </summary>
    public static class BootTrace
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "cs_boottrace.txt");
        static readonly StringBuilder _current = new();
        static string _previous = "";
        static string _lastMark = "(start)";
        static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            if (_installed || !PerfStrip.ShowBootTrace) return;
            _installed = true;

            // Read the previous run (if any) before we truncate the file for this run.
            try { if (File.Exists(FilePath)) _previous = File.ReadAllText(FilePath); } catch { }
            try { File.WriteAllText(FilePath, "RUN START\n"); } catch { }

            Application.logMessageReceivedThreaded += OnLog;
            Mark("SubsystemRegistration");
            // NOTE: the view GameObject is created at AfterSceneLoad (below), NOT here —
            // DontDestroyOnLoad is unreliable before the first scene is loaded, and OnGUI can't
            // render until the first frame anyway. The static capture above is already live.
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void MarkAssemblies() => Mark("AfterAssembliesLoaded");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void MarkSplash() => Mark("BeforeSplashScreen");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MarkBeforeScene() => Mark("BeforeSceneLoad");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MarkAfterScene()
        {
            Mark("AfterSceneLoad");
            EnsureView();
        }

        static bool _viewCreated;

        /// <summary>Create the on-screen view once the first scene has loaded (DontDestroyOnLoad is
        /// reliable here). Safe to call more than once.</summary>
        static void EnsureView()
        {
            if (_viewCreated || !PerfStrip.ShowBootTrace) return;
            _viewCreated = true;
            var go = new GameObject("[BootTrace]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BootTraceView>();
        }

        /// <summary>Record a boot checkpoint. Flushed to disk immediately so a crash right after
        /// still leaves it in the file for the next launch to show.</summary>
        public static void Mark(string label)
        {
            if (!PerfStrip.ShowBootTrace) return;
            float t;
            try { t = Time.realtimeSinceStartup; } catch { t = -1f; }
            string line = $"[{t:F2}] {label}\n";
            lock (_current) { _current.Append(line); _lastMark = label; }
            try { File.AppendAllText(FilePath, line); } catch { }
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string line = $"[{type}] {condition}\n{stack}\n----\n";
            lock (_current) _current.Append(line);
            try { File.AppendAllText(FilePath, line); } catch { }
        }

        public static string Previous => _previous;
        public static string LastMark { get { lock (_current) return _lastMark; } }
        public static string Current { get { lock (_current) return _current.ToString(); } }
    }

    /// <summary>IMGUI renderer for <see cref="BootTrace"/> — no Canvas/EventSystem needed, works
    /// the moment the first frame renders. Big text so it reads on a phone; tap to hide/show.</summary>
    public class BootTraceView : MonoBehaviour
    {
        Vector2 _scroll;
        bool _open = true;

        void OnGUI()
        {
            int fs = Mathf.Max(20, Screen.height / 48);

            var toggle = new Rect(12, 12, Mathf.Min(Screen.width - 24, 360), 72);
            var bstyle = new GUIStyle(GUI.skin.button) { fontSize = fs };
            if (GUI.Button(toggle, _open ? "HIDE BOOT LOG" : $"SHOW BOOT LOG", bstyle))
                _open = !_open;
            if (!_open) return;

            var area = new Rect(12, 96, Screen.width - 24, Screen.height - 108);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(area);

            var head = new GUIStyle(GUI.skin.label) { fontSize = fs + 4, fontStyle = FontStyle.Bold };
            var body = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true, richText = false };
            body.normal.textColor = Color.white;

            head.normal.textColor = Color.yellow;
            GUILayout.Label($"LAST RUN GOT TO: {BootTrace.LastMark}", head);

            _scroll = GUILayout.BeginScrollView(_scroll);

            head.normal.textColor = new Color(1f, 0.45f, 0.45f);
            GUILayout.Label("== PREVIOUS RUN (last launch — where it died) ==", head);
            GUILayout.Label(string.IsNullOrEmpty(BootTrace.Previous) ? "(no previous run)" : BootTrace.Previous, body);

            head.normal.textColor = new Color(0.5f, 0.9f, 1f);
            GUILayout.Label("== THIS RUN ==", head);
            GUILayout.Label(BootTrace.Current, body);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
