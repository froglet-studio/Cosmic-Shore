using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The CLIENT half of the Brood audit's host-vs-client wall check (2026-11-21): a second editor process boots to
    /// the menu, leaves its own offline host, and joins the audit host on 127.0.0.1 as a SPECTATOR (no Player object,
    /// <see cref="SpectatorSession"/>), before the host launches, so it sees every Serpent press from the first. Every
    /// half second it writes how many Serpent walls this peer holds and how many prisms they carry
    /// (<c>walls.csv</c>: scene time, walls, bricks, active), the same numbers the host's driver samples, so the two
    /// copies of each wall can be compared.
    /// <code>Unity -batchmode -projectPath &lt;second checkout&gt; -executeMethod CosmicShore.Editor.BroodNetClient.Run
    ///   -broodOut Logs/BroodNet/client [-broodPort 7777] [-broodLimit 600] -broodQuit</code>
    /// <para>A READER tool: it writes only its result file.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class BroodNetClient
    {
        const string StateKey = "CosmicShore.BroodNetClient.Out";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";
        enum Phase { Off, Boot, Leave, Connect, Watch }

        static Phase _phase;
        static string _out;
        static double _phaseAt, _startAt, _lastSample, _gameAt = -1;
        static float _limit = 900f;
        static ushort _port = 7777;
        static bool _quit, _skippedName, _sawGame;
        static readonly StringBuilder Rows = new();

        static BroodNetClient()
        {
            _out = SessionState.GetString(StateKey, "");
            if (!string.IsNullOrEmpty(_out)) Hook();
        }

        static void Hook()
        {
            _quit = Array.IndexOf(Environment.GetCommandLineArgs(), "-broodQuit") >= 0;
            _limit = float.Parse(Arg("-broodLimit", "900"), CultureInfo.InvariantCulture);
            _port = ushort.Parse(Arg("-broodPort", "7777"), CultureInfo.InvariantCulture);
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            _out = Path.GetFullPath(Arg("-broodOut", Path.Combine("Logs", "BroodNet", "client")));
            Directory.CreateDirectory(_out);
            SessionState.SetString(StateKey, _out);
            Hook();
            PlayerPrefs.SetInt("CosmicShore.OfflinePreferred", 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange c)
        {
            if (c == PlayModeStateChange.EnteredPlayMode) { _phase = Phase.Boot; _phaseAt = _startAt = EditorApplication.timeSinceStartup; Log("play"); }
            else if (c == PlayModeStateChange.EnteredEditMode && _phase != Phase.Off) Done("play ended");
        }

        static void Update()
        {
            if (_phase == Phase.Off || !EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _startAt > _limit) { Done("time limit"); return; }
            var nm = NetworkManager.Singleton;
            switch (_phase)
            {
                case Phase.Boot:
                    PokeBootGates();
                    var gd = Resources.FindObjectsOfTypeAll<GameDataSO>().FirstOrDefault();
                    if (IsMenu() && gd != null && gd.LocalPlayer?.Vessel != null && now - _phaseAt > 3)
                    {
                        Log("menu up; leaving the local host");
                        nm?.Shutdown();
                        Enter(Phase.Leave, now);
                    }
                    break;
                case Phase.Leave:
                    if (nm != null && !nm.ShutdownInProgress && !nm.IsListening && now - _phaseAt > 2)
                    {
                        SpectatorSession.ArmApprovalPayload();
                        if (nm.TryGetComponent(out UnityTransport t)) t.SetConnectionData("127.0.0.1", _port);
                        bool ok = nm.StartClient();
                        Log($"StartClient 127.0.0.1:{_port} -> {ok}");
                        Enter(Phase.Connect, now);
                    }
                    break;
                case Phase.Connect:
                    if (nm != null && nm.IsConnectedClient) { Log($"connected as client {nm.LocalClientId}"); Enter(Phase.Watch, now); }
                    else if (now - _phaseAt > 20) { Log("no connection; retrying"); nm?.Shutdown(); Enter(Phase.Leave, now); }
                    break;
                case Phase.Watch:
                    if (nm == null || !nm.IsConnectedClient)
                    {
                        if (_sawGame) { Done("host left"); return; }
                        if (now - _phaseAt > 5) { Log("dropped before the game; reconnecting"); Enter(Phase.Leave, now); }
                        return;
                    }
                    bool game = SceneManager.GetActiveScene().name.IndexOf("Brood", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (game && _gameAt < 0) { _gameAt = now; _sawGame = true; Log($"in {SceneManager.GetActiveScene().name}"); }
                    if (!game && _sawGame && IsMenu()) { Done("back in the menu"); return; }
                    if (game && now - _lastSample >= 0.5) { _lastSample = now; Sample(now - _gameAt); }
                    break;
            }
        }

        static void Sample(double t)
        {
            int walls = 0, bricks = 0, active = 0;
            var members = typeof(SerpentWallAssembler).GetField("_members", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var w in SerpentWallAssembler.Live)
            {
                if (!w) continue;
                walls++;
                if (w.IsActive) active++;
                if (members?.GetValue(w) is System.Collections.ICollection m) bricks += m.Count;
            }
            if (Rows.Length == 0) Rows.Append("t,walls,bricks,active\n");
            Rows.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1},{2},{3}\n", t, walls, bricks, active));
            File.WriteAllText(Path.Combine(_out, "walls.csv"), Rows.ToString());
        }

        static void PokeBootGates()
        {
            var privacy = CosmicShore.UI.PrivacyConsentOverlay.Current;
            if (privacy) UnityEngine.Object.Destroy(privacy.gameObject);
            if (_skippedName) return;
            var auth = UnityEngine.Object.FindAnyObjectByType<CosmicShore.Core.AuthenticationSceneController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
            {
                _skippedName = true;
                auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
            }
        }

        static void Enter(Phase p, double now) { _phase = p; _phaseAt = now; }

        static void Done(string why)
        {
            Log("done: " + why);
            _phase = Phase.Off;
            SessionState.SetString(StateKey, "");
            if (!_quit) return;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(_sawGame ? 0 : 1);
        }

        static void Log(string line)
        {
            Debug.Log("[BroodNetClient] " + line);
            if (!string.IsNullOrEmpty(_out)) File.AppendAllText(Path.Combine(_out, "client.log"), $"{DateTime.Now:HH:mm:ss} {line}\n");
        }

        static bool IsMenu() => SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }
    }
}
