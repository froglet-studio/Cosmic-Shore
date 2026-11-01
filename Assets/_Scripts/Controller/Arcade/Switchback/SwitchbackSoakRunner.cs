using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A test driver (SWITCHBACK.md "AI: the hit counter"): plays N Switchback races through the NORMAL game flow with
    /// EVERY seat flown by the AI Dolphin (the host seat is handed to the AI with <see cref="PilotControl.TrySetAIFlies"/>,
    /// the AI-flies switch a player uses), on one intensity's fixed Blast Run seed (45_501-45_504) at one difficulty,
    /// and writes each pilot's <see cref="SwitchbackHitStats"/> row per race to a JSONL file. Same shape as
    /// <see cref="SkimRaceBenchmarkRunner"/>: menu → configure → launch → Ready → wait → Play Again.
    /// It never writes a vessel, prism, score, course or timer. Started by the editor's SwitchbackSoakLauncher
    /// (<c>-executeMethod CosmicShore.Editor.SwitchbackSoakLauncher.Run</c>).
    /// </summary>
    public class SwitchbackSoakRunner : MonoBehaviour
    {
        bool _skippedName;
        [Serializable]
        public class Settings
        {
            public int Races = 2;
            public int Intensity = 1;
            public AIDifficulty Difficulty = AIDifficulty.Legendary;
            public int TotalPlayers = 3;
            public float TimeoutSeconds = 420f;
            public float TimeScale = 1f;
            public int TargetFrameRate = 60;
            public string OutputFile = "";
            public string Tag = "";
        }

        public static SwitchbackSoakRunner Active { get; private set; }
        public static event Action<bool> AllDone;

        Settings _s;
        GameDataSO _gameData;
        int _raceNo;
        bool _raceEnded;

        public static SwitchbackSoakRunner Launch(Settings s)
        {
            if (Active != null) Destroy(Active.gameObject);
            var go = new GameObject("[Switchback Soak]");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<SwitchbackSoakRunner>();
            r._s = s;
            Active = r;
            r.StartCoroutine(r.Run());
            return r;
        }

        void OnEnable() => SwitchbackHitStats.RaceEnded += OnRaceEnded;
        void OnDisable() => SwitchbackHitStats.RaceEnded -= OnRaceEnded;

        void OnRaceEnded(System.Collections.Generic.IReadOnlyList<SwitchbackHitStats.Row> rows, float time)
        {
            _raceEnded = true;
            Write(rows, time, "ended");
        }

        void Write(System.Collections.Generic.IEnumerable<SwitchbackHitStats.Row> rows, float time, string status)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                sb.Append('{')
                  .Append("\"tag\":\"").Append(_s.Tag).Append("\",")
                  .Append("\"intensity\":").Append(_s.Intensity).Append(',')
                  .Append("\"difficulty\":").Append((int)_s.Difficulty).Append(',')
                  .Append("\"race\":").Append(_raceNo).Append(',')
                  .Append("\"status\":\"").Append(status).Append("\",")
                  .Append("\"raceTime\":").Append(time.ToString("F2", inv)).Append(',')
                  .Append("\"pilot\":\"").Append(r.Name).Append("\",")
                  .Append("\"ai\":").Append(r.AI ? "true" : "false").Append(',')
                  .Append("\"pilotDifficulty\":").Append(r.Difficulty).Append(',')
                  .Append("\"course\":").Append(r.Hits[0]).Append(',')
                  .Append("\"mover\":").Append(r.Hits[1]).Append(',')
                  .Append("\"rival\":").Append(r.Hits[2]).Append(',')
                  .Append("\"own\":").Append(r.Hits[3]).Append(',')
                  .Append("\"total\":").Append(r.Total).Append(',')
                  .Append("\"emergencies\":").Append(r.Emergencies).Append(',')
                  .Append("\"avoids\":").Append(r.Avoids).Append(',')
                  .Append("\"blasts\":").Append(r.Blasts).Append(',')
                  .Append("\"gates\":").Append(r.Threaded).Append(',')
                  .Append("\"finish\":").Append(r.FinishTime.ToString("F2", inv))
                  .Append("}\n");
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_s.OutputFile)));
                File.AppendAllText(_s.OutputFile, sb.ToString());
            }
            catch (Exception ex) { CSDebug.LogError("[SwitchbackSoak] write failed: " + ex.Message); }
        }

        IEnumerator Run()
        {
            Application.targetFrameRate = _s.TargetFrameRate;
            _gameData = FindGameData();
            if (_gameData == null) { Finish(false, "GameDataSO not found"); yield break; }

            float deadline = Time.unscaledTime + 180f;
            while (Time.unscaledTime < deadline && !(IsMenu() && _gameData.LocalPlayer?.Vessel != null))
            {
                // Nobody is at the keyboard to answer the first-run age gate, and the boot holds for it with no
                // timeout. Close it unanswered: nothing is recorded, and collection stays off (its safe default).
                var privacy = CosmicShore.UI.PrivacyConsentOverlay.Current;
                if (privacy) { CSDebug.Log("[SwitchbackSoak] closing the unanswered privacy overlay (nothing recorded)"); Destroy(privacy.gameObject); }
                // A fresh anonymous account parks on the username step. Skip it the way the safety timeout does
                // (straight to the menu) instead of claiming a name for a throwaway soak account.
                if (!_skippedName)
                {
                    var auth = FindAnyObjectByType<CosmicShore.Core.AuthenticationSceneController>();
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
                    {
                        _skippedName = true;
                        CSDebug.Log("[SwitchbackSoak] skipping the username step (no name claimed)");
                        auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (Time.unscaledTime >= deadline) { Finish(false, "menu never gave a local pilot"); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);

            Configure();
            _gameData.InvokeGameLaunch();
            CSDebug.Log($"[SwitchbackSoak] launched I{_s.Intensity} D{(int)_s.Difficulty} x{_s.Races} -> {_s.OutputFile}");

            bool ok = true;
            for (_raceNo = 0; _raceNo < _s.Races; _raceNo++)
            {
                MiniGameControllerBase controller = null;
                deadline = Time.unscaledTime + 120f;
                while (Time.unscaledTime < deadline)
                {
                    if (SceneManager.GetActiveScene().name == _gameData.SceneName)
                    {
                        controller = FindAnyObjectByType<SwitchbackController>();
                        if (controller != null && SeatsReady()) break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (controller == null) { ok = false; CSDebug.LogError("[SwitchbackSoak] race scene never ready"); break; }
                yield return new WaitForSecondsRealtime(2f);

                deadline = Time.unscaledTime + 120f;
                while (!_gameData.IsTurnRunning && Time.unscaledTime < deadline)
                {
                    controller.OnReadyClicked();
                    float repress = Time.unscaledTime + 15f;
                    while (!_gameData.IsTurnRunning && Time.unscaledTime < repress) yield return new WaitForSecondsRealtime(0.25f);
                }
                if (!_gameData.IsTurnRunning) { ok = false; CSDebug.LogError("[SwitchbackSoak] race never started"); break; }

                // Every seat AI: hand the host's hull to the AI (the panel's AI-flies switch).
                yield return null;
                if (_gameData.LocalPlayer is Player host && !PilotControl.TrySetAIFlies(host, true, _gameData, out var refusal))
                    CSDebug.LogError("[SwitchbackSoak] host seat not handed to the AI: " + refusal);
                Time.timeScale = _s.TimeScale;

                _raceEnded = false;
                float start = Time.unscaledTime;
                while (!_raceEnded && Time.unscaledTime - start < _s.TimeoutSeconds / Mathf.Max(0.1f, _s.TimeScale))
                    yield return new WaitForSecondsRealtime(0.5f);
                Time.timeScale = 1f;
                if (!_raceEnded)
                {
                    var cur = SwitchbackHitStats.Current;
                    if (cur != null) Write(cur.Rows, cur.Elapsed, "timeout");
                    CSDebug.LogWarning($"[SwitchbackSoak] race {_raceNo} timed out");
                }
                yield return new WaitForSecondsRealtime(3f);
                if (_raceNo + 1 >= _s.Races) break;

                controller = FindAnyObjectByType<MiniGameControllerBase>();
                if (controller != null) controller.RequestReplay();
                deadline = Time.unscaledTime + 120f;
                while (controller != null && Time.unscaledTime < deadline) yield return new WaitForSecondsRealtime(0.25f);
            }
            Finish(ok, "done");
        }

        void Finish(bool ok, string why)
        {
            CSDebug.Log($"[SwitchbackSoak] finished ({why}, ok={ok}) -> {_s.OutputFile}");
            AllDone?.Invoke(ok);
        }

        void Configure()
        {
            SO_ArcadeGame card = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                if (g != null && g.Mode == GameModes.Switchback) { card = g; break; }
#if UNITY_EDITOR
            if (card == null)
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SO_ArcadeGame"))
                {
                    var g = UnityEditor.AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (g != null && g.Mode == GameModes.Switchback) { card = g; break; }
                }
#endif
            if (card != null) _gameData.SyncFromArcadeGame(card);
            else CSDebug.LogError("[SwitchbackSoak] no Switchback arcade card");
            _gameData.IsTraining = false;
            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = VesselClassType.Dolphin;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(_s.Intensity, 1, 4);
            _gameData.ConfigurePlayerCounts(Mathf.Max(1, _s.TotalPlayers), 1);
            _gameData.RequestedDomainCount = Mathf.Clamp(_s.TotalPlayers, 1, 3);
            _gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(_s.Difficulty);
            _gameData.RequestedAIDifficulties.Clear();
            _gameData.SwitchbackFixedSeed = true;
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

        void OnDestroy() { if (Active == this) Active = null; }
    }
}
