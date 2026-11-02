using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A test driver (THRESHERTIME.md §4 "The aimed launch"): plays N Thresher Time races through the NORMAL game flow
    /// with EVERY seat an AI Thresher (the host seat handed to the AI with <see cref="PilotControl.TrySetAIFlies"/>, the
    /// AI-flies switch a player uses), each race on its own fixed course seed, and writes one JSON line per pilot per race:
    /// rings threaded, finish time, plants, armed launches, and how fast and how far off its next ring each launch left.
    /// <see cref="Settings.Aimed"/> off (an A/B's "before") switches the Thresher Time variant's <c>aimedLaunch</c> off for
    /// the run and puts it back. The Switchback soak's shape (<see cref="SwitchbackSoakRunner"/>): menu → configure →
    /// launch → Ready → wait → Play Again, then back to the menu for the next of <see cref="Settings.Intensities"/>.
    /// <see cref="Settings.Watch"/> (a Unity with a window) opens the AI Diagnose panel on every race with the AI's
    /// thinking, labels and trails on and the camera chasing an AI Thresher. It never writes a vessel, prism, score,
    /// course or timer. Started by the editor's ThresherTimeSoakLauncher
    /// (<c>-executeMethod CosmicShore.Editor.ThresherTimeSoakLauncher.Run</c>).
    /// </summary>
    public class ThresherTimeSoakRunner : MonoBehaviour
    {
        [Serializable]
        public class Settings
        {
            public int Races = 2;
            public int[] Intensities = { 1 };
            public int TotalPlayers = 3;
            public bool Aimed = true;
            public int FirstSeed = 7001;
            public float TimeoutSeconds = 420f;
            public float TimeScale = 1f;
            public int TargetFrameRate = 60;
            public string OutputFile = "";
            public string Tag = "";
            public bool Watch;
        }

        const string VariantPath = "AIBoostPolicies/ThresherAIBoostPolicy_ThresherTime";

        public static ThresherTimeSoakRunner Active { get; private set; }
        public static event Action<bool> AllDone;

        sealed class Pilot
        {
            public IPlayer Player;
            public ThresherExecutor Thresher;
            public bool WasPivoting, WasLaunched, Measure;
            public int Plants, Launches;
            public float FinishAt = -1f;
            public readonly List<float> Speeds = new();
            public readonly List<float> OffRing = new();
        }

        Settings _s;
        GameDataSO _gameData;
        GateRaceController _race;
        readonly List<Pilot> _pilots = new();
        bool _skippedName, _racing;
        float _raceStart;
        int _raceNo, _intensity;
        ThresherSlingAIPolicySO _variant;
        FieldInfo _aimedField;
        bool _aimedWas, _aimedSet;

        public static ThresherTimeSoakRunner Launch(Settings s)
        {
            if (Active != null) Destroy(Active.gameObject);
            var go = new GameObject("[Thresher Time Soak]");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<ThresherTimeSoakRunner>();
            r._s = s;
            Active = r;
            r.StartCoroutine(r.Run());
            return r;
        }

        // Read after the chain has stepped this frame: a launch shows as LaunchedThisStep on its own step; its exit is read
        // the frame after, once the transformer has faced the hull along it.
        void LateUpdate()
        {
            if (!_racing || _race == null) return;
            int target = _race.AuthoritativeGateCount;
            foreach (var p in _pilots)
            {
                var vessel = p.Player?.Vessel;
                if (vessel == null || !p.Thresher) continue;
                if (p.Measure)
                {
                    p.Measure = false;
                    MeasureExit(p, vessel);
                }
                bool pivot = p.Thresher.IsPivoting;
                if (pivot && !p.WasPivoting) p.Plants++;
                p.WasPivoting = pivot;
                bool launched = p.Thresher.LaunchedThisStep;
                if (launched && !p.WasLaunched)
                {
                    p.Launches++;
                    p.Measure = true;
                }
                p.WasLaunched = launched;
                if (p.FinishAt < 0f && target > 0 && p.Player.RoundStats != null && p.Player.RoundStats.SwitchesThreaded >= target)
                    p.FinishAt = Time.time - _raceStart;
            }
        }

        static void MeasureExit(Pilot p, IVessel vessel)
        {
            var status = vessel.VesselStatus;
            if (status == null) return;
            p.Speeds.Add(status.Speed);
            if (!GateRaceController.TryGetNextRing(vessel, out var ring) || !ring) return;
            Vector3 to = ring.transform.position - vessel.Transform.position;
            if (to.sqrMagnitude > 1e-4f && status.Course.sqrMagnitude > 1e-4f) p.OffRing.Add(Vector3.Angle(status.Course, to));
        }

        void Write(string status, float raceTime)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            int target = _race ? _race.AuthoritativeGateCount : 0;
            foreach (var p in _pilots)
            {
                int within15 = 0;
                foreach (var a in p.OffRing) if (a <= 15f) within15++;
                sb.Append('{')
                  .Append("\"tag\":\"").Append(_s.Tag).Append("\",")
                  .Append("\"aimed\":").Append(_s.Aimed ? "true" : "false").Append(',')
                  .Append("\"intensity\":").Append(_intensity).Append(',')
                  .Append("\"race\":").Append(_raceNo).Append(',')
                  .Append("\"seed\":").Append(_s.FirstSeed + _raceNo).Append(',')
                  .Append("\"status\":\"").Append(status).Append("\",")
                  .Append("\"raceTime\":").Append(raceTime.ToString("F2", inv)).Append(',')
                  .Append("\"pilot\":\"").Append(p.Player?.Name).Append("\",")
                  .Append("\"rings\":").Append(p.Player?.RoundStats?.SwitchesThreaded ?? -1).Append(',')
                  .Append("\"target\":").Append(target).Append(',')
                  .Append("\"finish\":").Append(p.FinishAt.ToString("F2", inv)).Append(',')
                  .Append("\"plants\":").Append(p.Plants).Append(',')
                  .Append("\"launches\":").Append(p.Launches).Append(',')
                  .Append("\"launchSpeedMedian\":").Append(Median(p.Speeds).ToString("F0", inv)).Append(',')
                  .Append("\"offRingMedian\":").Append(Median(p.OffRing).ToString("F1", inv)).Append(',')
                  .Append("\"offRingWithin15\":").Append(within15)
                  .Append("}\n");
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_s.OutputFile)));
                File.AppendAllText(_s.OutputFile, sb.ToString());
            }
            catch (Exception ex) { CSDebug.LogError("[ThresherTimeSoak] write failed: " + ex.Message); }
        }

        static float Median(List<float> v)
        {
            if (v.Count == 0) return -1f;
            var c = new List<float>(v);
            c.Sort();
            return c.Count % 2 == 1 ? c[c.Count / 2] : 0.5f * (c[c.Count / 2 - 1] + c[c.Count / 2]);
        }

        IEnumerator Run()
        {
            Application.targetFrameRate = _s.TargetFrameRate;
            _gameData = FindGameData();
            if (_gameData == null) { Finish(false, "GameDataSO not found"); yield break; }
            SetAimed();

            float deadline = Time.unscaledTime + 180f;
            while (Time.unscaledTime < deadline && !(IsMenu() && _gameData.LocalPlayer?.Vessel != null))
            {
                // Nobody is at the keyboard for the first-run age gate or the username step (SwitchbackSoakRunner).
                var privacy = CosmicShore.UI.PrivacyConsentOverlay.Current;
                if (privacy) { CSDebug.Log("[ThresherTimeSoak] closing the unanswered privacy overlay (nothing recorded)"); Destroy(privacy.gameObject); }
                if (!_skippedName)
                {
                    var auth = FindAnyObjectByType<CosmicShore.Core.AuthenticationSceneController>();
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
                    {
                        _skippedName = true;
                        CSDebug.Log("[ThresherTimeSoak] skipping the username step (no name claimed)");
                        auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (Time.unscaledTime >= deadline) { Finish(false, "menu never gave a local pilot"); yield break; }

            bool ok = true;
            var intensities = _s.Intensities is { Length: > 0 } ? _s.Intensities : new[] { 1 };
            for (int k = 0; k < intensities.Length && ok; k++)
            {
                if (k > 0)
                {
                    // Back to the menu for the next intensity, the way the pause menu's Main Menu button goes.
                    var loader = FindAnyObjectByType<CosmicShore.Core.SceneLoader>();
                    if (loader) loader.ReturnToMainMenu();
                    deadline = Time.unscaledTime + 120f;
                    while (Time.unscaledTime < deadline && !(IsMenu() && _gameData.LocalPlayer?.Vessel != null))
                        yield return new WaitForSecondsRealtime(0.25f);
                    if (Time.unscaledTime >= deadline) { ok = false; CSDebug.LogError("[ThresherTimeSoak] the menu never came back"); break; }
                }
                yield return new WaitForSecondsRealtime(1.5f);
                _intensity = Mathf.Clamp(intensities[k], 1, 4);
                yield return RunIntensity(result => ok = result);
            }
            Finish(ok, "done");
        }

        IEnumerator RunIntensity(Action<bool> done)
        {
            Configure();
            CourseSeedOverride.Forced = _s.FirstSeed;
            _gameData.InvokeGameLaunch();
            CSDebug.Log($"[ThresherTimeSoak] launched I{_intensity} aimed={_s.Aimed} x{_s.Races} -> {_s.OutputFile}");

            bool ok = true;
            float deadline;
            for (_raceNo = 0; _raceNo < _s.Races; _raceNo++)
            {
                _race = null;
                deadline = Time.unscaledTime + 120f;
                while (Time.unscaledTime < deadline)
                {
                    if (SceneManager.GetActiveScene().name == _gameData.SceneName)
                    {
                        _race = FindAnyObjectByType<ThresherTimeController>();
                        if (_race != null && SeatsReady()) break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (_race == null) { ok = false; CSDebug.LogError("[ThresherTimeSoak] race scene never ready"); break; }
                yield return new WaitForSecondsRealtime(2f);

                deadline = Time.unscaledTime + 120f;
                while (!_gameData.IsTurnRunning && Time.unscaledTime < deadline)
                {
                    _race.OnReadyClicked();
                    float repress = Time.unscaledTime + 15f;
                    while (!_gameData.IsTurnRunning && Time.unscaledTime < repress) yield return new WaitForSecondsRealtime(0.25f);
                }
                if (!_gameData.IsTurnRunning) { ok = false; CSDebug.LogError("[ThresherTimeSoak] race never started"); break; }

                // Every seat AI: hand the host's hull to the AI (the panel's AI-flies switch).
                yield return null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_gameData.LocalPlayer is Player host && !PilotControl.TrySetAIFlies(host, true, _gameData, out var refusal))
                    CSDebug.LogError("[ThresherTimeSoak] host seat not handed to the AI: " + refusal);
#else
                // PilotControl exists only in editor and development builds; a release player cannot run an all-AI soak.
                CSDebug.LogError("[ThresherTimeSoak] host seat not handed to the AI: PilotControl is not in a release build");
#endif
                BindPilots();
                Time.timeScale = _s.TimeScale;
                _raceStart = Time.time;
                _racing = true;
                if (_s.Watch) StartCoroutine(OpenAIDiagnose());

                float start = Time.unscaledTime;
                while (_gameData.IsTurnRunning && Time.unscaledTime - start < _s.TimeoutSeconds / Mathf.Max(0.1f, _s.TimeScale))
                    yield return new WaitForSecondsRealtime(0.25f);
                _racing = false;
                float raceTime = Time.time - _raceStart;
                Time.timeScale = 1f;
                bool ended = !_gameData.IsTurnRunning;
                Write(ended ? "ended" : "timeout", raceTime);
                if (!ended) CSDebug.LogWarning($"[ThresherTimeSoak] race {_raceNo} timed out");
                yield return new WaitForSecondsRealtime(3f);
                if (_raceNo + 1 >= _s.Races) break;

                CourseSeedOverride.Forced = _s.FirstSeed + _raceNo + 1;
                var controller = FindAnyObjectByType<MiniGameControllerBase>();
                if (controller != null) controller.RequestReplay();
                deadline = Time.unscaledTime + 120f;
                while (controller != null && Time.unscaledTime < deadline) yield return new WaitForSecondsRealtime(0.25f);
            }
            done(ok);
        }

        /// <summary>A watched run: the AI Diagnose panel (F2) open on the race, the AI's thinking, labels and trails on,
        /// the camera chasing an AI Thresher. The panel closes itself when the race scene unloads, so every race opens it.</summary>
        IEnumerator OpenAIDiagnose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var eye = ThirdEye.Instance;
            if (!eye) yield break;
            eye.SetPanel(true);
            var s = eye.Settings;
            s.ShowThinking = s.ShowLabels = s.ShowTrails = true;
            eye.SetMode(ThirdEyeCameraMode.Chase);
            eye.SetDriving(true);
            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until)
            {
                foreach (var p in eye.Pilots)
                    if (p.Alive && !p.IsLocalHuman && p.Player && p.Player.IsInitializedAsAI) { eye.Watch(p); yield break; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
#else
            yield break;
#endif
        }

        void BindPilots()
        {
            _pilots.Clear();
            foreach (var p in _gameData.Players)
            {
                var vessel = p?.Vessel;
                if (vessel == null || vessel.Transform == null) continue;
                _pilots.Add(new Pilot { Player = p, Thresher = vessel.Transform.GetComponentInChildren<ThresherExecutor>(true) });
            }
        }

        /// <summary>Switch the Thresher Time variant's aimed launch to <see cref="Settings.Aimed"/> for the run (it is read
        /// every tick by the driver), remembering what it was; <see cref="RestoreAimed"/> puts it back.</summary>
        void SetAimed()
        {
            _variant = Resources.Load<ThresherSlingAIPolicySO>(VariantPath);
            _aimedField = typeof(ThresherSlingAIPolicySO).GetField("aimedLaunch", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!_variant || _aimedField == null) { CSDebug.LogError("[ThresherTimeSoak] no Thresher Time variant to switch the aimed launch on"); return; }
            _aimedWas = (bool)_aimedField.GetValue(_variant);
            _aimedField.SetValue(_variant, _s.Aimed);
            _aimedSet = true;
        }

        void RestoreAimed()
        {
            if (!_aimedSet) return;
            _aimedSet = false;
            if (_variant && _aimedField != null) _aimedField.SetValue(_variant, _aimedWas);
        }

        void Finish(bool ok, string why)
        {
            RestoreAimed();
            CourseSeedOverride.Forced = 0;
            CSDebug.Log($"[ThresherTimeSoak] finished ({why}, ok={ok}) -> {_s.OutputFile}");
            AllDone?.Invoke(ok);
        }

        void Configure()
        {
            SO_ArcadeGame card = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                if (g != null && g.Mode == GameModes.ThresherTime) { card = g; break; }
#if UNITY_EDITOR
            if (card == null)
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SO_ArcadeGame"))
                {
                    var g = UnityEditor.AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (g != null && g.Mode == GameModes.ThresherTime) { card = g; break; }
                }
#endif
            if (card != null) _gameData.SyncFromArcadeGame(card);
            else CSDebug.LogError("[ThresherTimeSoak] no Thresher Time arcade card");
            _gameData.IsTraining = false;
            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = VesselClassType.Thresher;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = _intensity;
            _gameData.ConfigurePlayerCounts(Mathf.Max(1, _s.TotalPlayers), 1);
            _gameData.RequestedDomainCount = Mathf.Clamp(_s.TotalPlayers, 1, 3);
            _gameData.RequestedAIDifficulties.Clear();
        }

        bool SeatsReady()
        {
            int n = 0;
            foreach (var p in _gameData.Players) if (p != null && p.Vessel != null) n++;
            return n >= Mathf.Max(1, _s.TotalPlayers);
        }

        bool IsMenu() => SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static GameDataSO FindGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (all != null && all.Length > 0) return all[0];
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:GameDataSO");
            if (guids.Length > 0)
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
            return null;
        }

        void OnDestroy()
        {
            RestoreAimed();
            if (Active == this) Active = null;
        }
    }
}
