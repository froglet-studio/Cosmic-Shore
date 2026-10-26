using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Player-facing match replays, built on Will Bogen's Theater (<c>Docs/THEATER.md</c>,
    /// <c>Docs/REPLAYS.md</c>).
    ///
    /// <para><b>It records by itself.</b> A match of any mode listed in <see cref="ReplayConfigSO"/>
    /// (Regatta first) is recorded from Go to the end of the turn by a <see cref="TheaterRecorder"/>
    /// - the Theater's own recorder, unchanged - and saved, with a <see cref="ReplaySummary"/> beside
    /// it, when the end screen appears, which is the moment the final results exist. No key, no
    /// button, no scene wiring.</para>
    ///
    /// <para><b>Every peer records its own replay.</b> Vessel poses already replicate, so each
    /// machine holds the same match and nothing extra crosses the network - the argument
    /// <see cref="AstroLeagueGoalReplay"/> already makes for its ghost ball. A guest's remote hulls
    /// are the interpolated poses it saw, which is what that player watched.</para>
    ///
    /// <para><b>A replay is never lost to a missing end screen.</b> If the results do not come
    /// within <see cref="ReplayConfigSO.ResultsWaitSeconds"/>, or the player leaves, or the app
    /// closes, the replay is saved without a scoreboard and says why in
    /// <see cref="ReplaySummary.EndedBecause"/>.</para>
    ///
    /// <para><b>Where the files go.</b> Into the Theater's own output folder
    /// (<see cref="TheaterConfigSO.ResolveOutputFolder"/>), so the Theater's play key (8) plays the
    /// newest replay with no browser. The newest <see cref="ReplayConfigSO.KeepNewest"/> are kept;
    /// a recording made by hand with the Theater's key has no summary and is never pruned. The
    /// summaries are mirrored to Cloud Save (<see cref="ReplayIndexRepository"/>).</para>
    ///
    /// <para>A lazy DI singleton like the other services, resolved at boot by
    /// <see cref="AppManager"/> because it works from SOAP events and nothing else asks for it
    /// until the browser does.</para>
    /// </summary>
    public sealed class ReplayService : IDisposable
    {
        readonly GameDataSO _gameData;
        readonly ReplayIndexRepository _index;

        ReplayStore _store;
        TheaterRecorder _recorder;
        ReplaySummary _pending;
        CancellationTokenSource _recordCts;
        CancellationTokenSource _resultsCts;
        Task _indexLoad;
        bool _disposed;

        public ReplayService(GameDataSO gameData, ReplayIndexRepository index)
        {
            _gameData = gameData;
            _index = index;

            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
            _gameData.OnShowGameEndScreen.OnRaised += HandleEndScreen;
            _gameData.OnSessionEnded.OnRaised += HandleSessionEnded;
            Application.quitting += HandleQuitting;
        }

        /// <summary>True while a match is being recorded.</summary>
        public bool IsRecording => _recorder != null && _recorder.IsRecording;

        /// <summary>
        /// The replay folder's store, created on first use - not in the constructor, which runs at
        /// DI resolution, before anything should be asking <c>Application</c> for paths.
        /// </summary>
        public ReplayStore Store => _store ??= new ReplayStore(TheaterConfigSO.Resolve().ResolveOutputFolder());

        /// <summary>Replays on this device, newest first.</summary>
        public List<ReplaySummary> ListOnDevice() => Store.List();

        /// <summary>
        /// Replays the Cloud Save index knows about that are NOT on this device - recorded on another
        /// one. They can be listed but not watched here: the frames never leave the device that
        /// recorded them.
        /// </summary>
        public async UniTask<List<ReplaySummary>> ListFromOtherDevicesAsync()
        {
            var result = new List<ReplaySummary>();
            if (!await TryEnsureIndexLoadedAsync()) return result;

            var local = Store.List();
            foreach (var entry in _index.Data.Entries)
            {
                if (entry == null) continue;
                bool here = false;
                for (int i = 0; i < local.Count && !here; i++) here = ReplaySummary.SameReplay(local[i], entry);
                if (!here) result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// Load a replay's frames to watch, through a <see cref="ReplayPlayer"/> (the browser's
        /// playback HUD). False, with the reason for the log, when the replay is not on this device,
        /// cannot be read, or the recorder or the Theater's dev keys are using the stage right now.
        /// </summary>
        public bool TryLoad(ReplaySummary summary, out TheaterRecording recording, out string error)
        {
            recording = null;
            if (summary == null || !ReplaySummary.IsBareFileName(summary.RecordingFile))
            {
                error = "not a replay on this device";
                return false;
            }
            if (IsRecording)
            {
                error = "a match is being recorded";
                return false;
            }
            var director = TheaterDirector.Instance;
            if (director != null && (director.IsRecording || director.IsPlaying))
            {
                error = "the Theater (keys 9 / 8) is recording or playing";
                return false;
            }
            return Store.TryLoadRecording(summary, out recording, out error);
        }

        /// <summary>Delete a replay from this device and from the Cloud Save index.</summary>
        public bool Delete(ReplaySummary summary)
        {
            bool deleted = Store.Delete(summary);
            if (_index.IsLoaded) _index.Remove(summary);
            return deleted;
        }

        // ── Recording ───────────────────────────────────────────────────────

        void HandleTurnStarted()
        {
            if (_disposed) return;
            if (!ReplayConfigSO.Resolve().ShouldAutoRecord(_gameData.GameMode)) return;

            var theater = TheaterConfigSO.Resolve();
            if (!theater.recordVessels) return;

            // Two turns of one match have no end screen between them, so the earlier turn's
            // results are not coming: keep what there is rather than drop it.
            if (_pending != null) SavePending(withResults: false);

            // The arena the match is flown in: its config names the exact cell to stand when the
            // replay is watched, and its position is the origin the poses are recorded around.
            var cell = Cell.FindNearestActiveCell(Vector3.zero, sceneCellsOnly: true);
            string configName = cell && cell.Config ? cell.Config.name : string.Empty;

            _recorder = new TheaterRecorder();
            _recorder.Begin(theater, ResolveSceneName(), (int)_gameData.GameMode, configName);
            _pending = NewSummary();
            _pending.ArenaOrigin = cell ? cell.transform.position : Vector3.zero;

            _recordCts = new CancellationTokenSource();
            RecordLoopAsync(_recorder, _recordCts.Token).Forget();
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] recording {_gameData.GameMode}");
        }

        /// <summary>
        /// Drive the recorder once a frame while it records. Scaled delta, as the Theater's own key
        /// does, so a slow-motion moment is recorded as the slow motion the players saw.
        /// </summary>
        static async UniTaskVoid RecordLoopAsync(TheaterRecorder recorder, CancellationToken ct)
        {
            try
            {
                while (recorder.IsRecording)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    recorder.Tick(Time.deltaTime);
                }
            }
            catch (OperationCanceledException) { /* stopped: turn end, session end, dispose */ }
        }

        void HandleTurnEnded()
        {
            if (_pending == null) return;

            StopRecorder();
            _pending.EndedBecause = "finished";
            WaitForResultsAsync(ReplayConfigSO.Resolve().ResultsWaitSeconds).Forget();
        }

        /// <summary>
        /// The end screen is raised once the results are final - it is the only path to the
        /// scoreboard (<c>EndGameSequencer</c>) - so this is where a replay is normally saved.
        /// </summary>
        void HandleEndScreen()
        {
            if (_pending == null) return;

            StopRecorder();
            if (string.IsNullOrEmpty(_pending.EndedBecause)) _pending.EndedBecause = "finished";
            SavePending(withResults: true);
        }

        void HandleSessionEnded()
        {
            if (_pending == null) return;

            if (IsRecording) _pending.EndedBecause = "left the match";
            StopRecorder();
            SavePending(withResults: false);
        }

        void HandleQuitting()
        {
            if (_pending == null) return;

            if (IsRecording) _pending.EndedBecause = "app closed";
            StopRecorder();
            SavePending(withResults: false);
        }

        /// <summary>Real time, so a paused game cannot hold a finished replay unsaved.</summary>
        async UniTaskVoid WaitForResultsAsync(float seconds)
        {
            _resultsCts?.Cancel();
            _resultsCts?.Dispose();
            _resultsCts = new CancellationTokenSource();
            var token = _resultsCts.Token;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, token);
            }
            catch (OperationCanceledException) { return; }

            if (_pending != null) SavePending(withResults: false);
        }

        void StopRecorder()
        {
            _recorder?.Stop();
            _recordCts?.Cancel();
            _recordCts?.Dispose();
            _recordCts = null;
        }

        void SavePending(bool withResults)
        {
            var recorder = _recorder;
            var summary = _pending;
            _recorder = null;
            _pending = null;
            _resultsCts?.Cancel();
            if (recorder == null || summary == null) return;

            var recording = recorder.Recording;
            summary.RecordedAtUtcTicks = recording.UtcTicks;
            summary.DurationSeconds = recording.Duration;
            if (withResults) CopyResults(summary);

            var store = Store;
            if (!store.TrySave(recording, summary, out string path, out string error))
            {
                CSDebug.LogWarning($"[Replay] {summary.GameModeName} replay not saved: {error}");
                return;
            }

            int pruned = store.Prune(ReplayConfigSO.Resolve().KeepNewest);
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Replay] saved {path} ({recording.Tracks.Count} vessels, {recording.Duration:0.0}s, " +
                $"{(summary.HasResults ? "with results" : "no results")}, {summary.EndedBecause}" +
                $"{(pruned > 0 ? $", pruned {pruned} old" : string.Empty)})");

            MirrorToCloudAsync(summary).Forget();
        }

        ReplaySummary NewSummary()
        {
            var summary = new ReplaySummary
            {
                GameMode = (int)_gameData.GameMode,
                GameModeName = _gameData.GameMode.ToString(),
                SceneName = ResolveSceneName(),
                Intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0
            };

            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.IsAlive()) continue;

                var vessel = player.Vessel;
                var status = vessel.IsAlive() ? vessel.VesselStatus : null;
                summary.Pilots.Add(new ReplayPilot
                {
                    Name = player.Name ?? string.Empty,
                    Domain = (int)player.Domain,
                    VesselType = status.IsAlive() ? (int)status.VesselType : -1,
                    IsAI = player.IsInitializedAsAI,
                    IsLocal = player.IsLocalUser
                });
            }
            return summary;
        }

        void CopyResults(ReplaySummary summary)
        {
            var results = _gameData.Results;
            summary.Results.Clear();
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                summary.Results.Add(new ReplayResult
                {
                    Rank = r.Rank,
                    Name = r.Name ?? string.Empty,
                    Domain = (int)r.Domain,
                    Score = r.Score,
                    ScoreText = r.ScoreText ?? string.Empty
                });
            }

            summary.HasResults = summary.Results.Count > 0;
            summary.WinnerName = _gameData.WinnerName ?? string.Empty;
            summary.WinnerDomain = (int)_gameData.WinnerDomain;
        }

        string ResolveSceneName() =>
            string.IsNullOrEmpty(_gameData.SceneName)
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                : _gameData.SceneName;

        // ── Cloud Save index ────────────────────────────────────────────────

        async UniTaskVoid MirrorToCloudAsync(ReplaySummary summary)
        {
            int cap = ReplayConfigSO.Resolve().CloudIndexSize;
            if (cap <= 0) return;

            if (!await TryEnsureIndexLoadedAsync()) return;
            _index.Upsert(summary, cap);
        }

        /// <summary>
        /// Load the index once, on first need. A failed load does not stop the replay feature: the
        /// repository already refuses to upload over a record it could not read, and the files on
        /// this device are the replays either way.
        /// </summary>
        async UniTask<bool> TryEnsureIndexLoadedAsync()
        {
            if (_index.IsLoaded) return true;
            try
            {
                _indexLoad ??= _index.LoadAsync();
                await _indexLoad.AsMainThread();
                return _index.IsLoaded;
            }
            catch (Exception e)
            {
                _indexLoad = null;
                CSDebug.LogWarning($"[Replay] Cloud Save replay index not loaded: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            _gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            _gameData.OnShowGameEndScreen.OnRaised -= HandleEndScreen;
            _gameData.OnSessionEnded.OnRaised -= HandleSessionEnded;
            Application.quitting -= HandleQuitting;

            StopRecorder();
            _resultsCts?.Cancel();
            _resultsCts?.Dispose();
            _resultsCts = null;
        }
    }
}
