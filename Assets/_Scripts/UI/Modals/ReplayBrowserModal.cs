using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The replay browser (<c>ModalWindows.REPLAYS</c>, <c>Docs/REPLAYS.md</c>): every replay on this
    /// device, newest first, with Watch and Delete, then the replays the player recorded on their
    /// other devices (listed from the Cloud Save index; their frames never left the device that
    /// recorded them, so they cannot be watched here). This is My Replays: opened from the Profile's
    /// MY REPLAYS and the Arcade window's REPLAYS, it lists the player's last games
    /// (<see cref="ReplayConfigSO.ListedReplays"/>, 10).
    ///
    /// <para><b>Watching</b> spawns a <see cref="ReplayPlaybackHud"/> (its own root canvas) and hands it
    /// the replay. While it plays this window stays open underneath, non-interactable and deaf to B,
    /// so leaving the replay lands back on the row it was started from.</para>
    ///
    /// <para><b>Every word is authored.</b> The row lines are templates on this component; the
    /// facts (mode name, date, duration, winner, counts) are filled in by code. The date is
    /// formatted with the invariant culture because the menu's font is ASCII-only.</para>
    /// </summary>
    public class ReplayBrowserModal : ModalWindowManager
    {
        [Header("Words (empty shows nothing)")]
        [SerializeField, Tooltip("A row's second line. Tokens: {date} {duration}.")]
        string whenTemplate;

        [SerializeField, Tooltip("How {date} is written (a .NET date format, invariant culture).")]
        string dateFormat;

        [SerializeField, Tooltip("A row's result when the match finished. Token: {winner}.")]
        string winnerTemplate;

        [SerializeField, Tooltip("A row's result when the replay was saved without final results " +
                                 "(the player left, the app closed, or the results never came).")]
        string noResultLine;

        [SerializeField, Tooltip("A row's last line. Tokens: {pilots} {intensity}.")]
        string detailTemplate;

        [SerializeField, Tooltip("Shown when a replay cannot be opened (missing or unreadable file, or the Theater is busy).")]
        string openFailedLine;

        [SerializeField, Tooltip("The line beside the title. Token: {count} - how many games the list keeps (ReplayConfigSO).")]
        string countTemplate;

        [Header("List")]
        [SerializeField, Tooltip("This device's rows are added here.")] RectTransform localList;
        [SerializeField, Tooltip("The 'on this device' heading and its rows.")] CanvasGroup localSection;
        [SerializeField, Tooltip("Other devices' rows are added here.")] RectTransform otherList;
        [SerializeField, Tooltip("The 'on your other devices' heading and its rows.")] CanvasGroup otherSection;
        [SerializeField, Tooltip("Shown while there is no replay to list.")] CanvasGroup emptyState;
        [SerializeField, Tooltip("One replay. Instantiated per row.")] ReplayRowView rowPrefab;
        [SerializeField, Tooltip("A line under the title for what just went wrong.")] TMP_Text statusText;
        [SerializeField, Tooltip("Beside the title: how many games the list keeps (countTemplate).")] TMP_Text countText;
        [SerializeField] CanvasGroup statusGroup;

        [Header("Watching")]
        [SerializeField, Tooltip("The controls over a replay being watched. Spawned per replay.")]
        ReplayPlaybackHud playbackHudPrefab;

        [SerializeField, Tooltip("The window's content. Non-interactable while a replay plays, so nothing behind the replay takes a press.")]
        CanvasGroup windowGroup;

        [Header("Gamepad scrolling")]
        [SerializeField, Tooltip("The list's scroll view. The right stick scrolls it, so rows with no button " +
                                 "(another device's replays) can still be reached with the pad.")]
        ScrollRect scroll;

        [SerializeField, Tooltip("Canvas units per second the right stick scrolls the list at full tilt.")]
        float stickScrollSpeed = 1400f;

        [SerializeField, Range(0f, 0.9f), Tooltip("Right-stick tilt below this is ignored.")]
        float stickDeadZone = 0.2f;

        [Inject] ReplayService replayService;

        // Handed to the replay's arena: a runtime-built cell gets no injection of its own.
        [Inject] Container container;
        [Inject] GameDataSO gameData;
        [Inject] SO_GameList gameList;

        readonly List<ReplayRowView> _rows = new();
        ReplayPlaybackHud _hud;
        ReplaySummary _watched;
        int _version;
        int _watchEndedFrame = -1;

        protected override bool AllowGamepadBClose =>
            base.AllowGamepadBClose && !_hud && Time.frameCount != _watchEndedFrame;

        protected override void Start()
        {
            base.Start();
            OnModalOpened += Refresh;
        }

        protected override void OnDisable()
        {
            if (_hud) _hud.Close();
            base.OnDisable();
        }

        void OnDestroy() => OnModalOpened -= Refresh;

        protected override void Update()
        {
            base.Update();
            if (!IsOpen || _hud || !scroll || !scroll.content || !scroll.viewport) return;

            var pad = Gamepad.current;
            if (pad == null) return;
            float tilt = pad.rightStick.y.ReadValue();
            if (Mathf.Abs(tilt) < stickDeadZone) return;

            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0f) return;
            scroll.verticalNormalizedPosition =
                Mathf.Clamp01(scroll.verticalNormalizedPosition + tilt * stickScrollSpeed * Time.unscaledDeltaTime / overflow);
        }

        /// <summary>Rebuild the list: this device's replays now, other devices' when the index answers.</summary>
        public void Refresh()
        {
            int version = ++_version;
            ClearRows();
            if (scroll) scroll.verticalNormalizedPosition = 1f;
            KitVisibility.SetShown(statusGroup, false);

            int limit = ReplayConfigSO.Resolve().ListedReplays;
            if (countText) countText.text = TemplateText.Fill(countTemplate, "count", limit.ToString(CultureInfo.InvariantCulture));

            // Newest first, the player's last N games (My Replays, opened from the Profile and the Arcade).
            var local = replayService != null ? replayService.ListOnDevice() : new List<ReplaySummary>();
            if (local.Count > limit) local.RemoveRange(limit, local.Count - limit);
            foreach (var summary in local) AddRow(localList, summary, here: true);
            KitVisibility.SetShown(localSection, local.Count > 0);
            KitVisibility.SetShown(otherSection, false);
            KitVisibility.SetShown(emptyState, local.Count == 0);
            FocusFirstRow(null);

            if (replayService != null) LoadOtherDevicesAsync(version).Forget();
        }

        async UniTaskVoid LoadOtherDevicesAsync(int version)
        {
            List<ReplaySummary> others;
            try { others = await replayService.ListFromOtherDevicesAsync(); }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[Replay] Could not list replays from other devices: {e.Message}");
                return;
            }
            if (!this || version != _version || others.Count == 0) return;
            int limit = ReplayConfigSO.Resolve().ListedReplays;
            if (others.Count > limit) others.RemoveRange(limit, others.Count - limit);

            foreach (var summary in others) AddRow(otherList, summary, here: false);
            KitVisibility.SetShown(otherSection, true);
            KitVisibility.SetShown(emptyState, false);
        }

        void AddRow(RectTransform parent, ReplaySummary summary, bool here)
        {
            if (!rowPrefab || !parent) return;
            var row = Instantiate(rowPrefab, parent);
            row.Bind(summary, ModeName(summary), WhenLine(summary), ResultLine(summary), DetailLine(summary),
                     here ? () => Watch(summary) : null,
                     here ? () => Delete(summary) : null);
            _rows.Add(row);
        }

        void ClearRows()
        {
            foreach (var row in _rows)
                if (row) Destroy(row.gameObject);
            _rows.Clear();
        }

        void FocusFirstRow(ReplaySummary prefer)
        {
            var events = EventSystem.current;
            if (!events || !IsOpen) return;
            ReplayRowView first = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!_rows[i] || _rows[i].WatchButton == null) continue;
                if (!first) first = _rows[i];
                if (prefer != null && ReferenceEquals(_rows[i].Summary, prefer)) { first = _rows[i]; break; }
            }
            if (first) events.SetSelectedGameObject(first.WatchButton.gameObject);
        }

        void Watch(ReplaySummary summary)
        {
            if (_hud || !playbackHudPrefab) return;

            // The match itself: its real scene, its real vessels on their recorded paths, their recorded
            // actions (Docs/REPLAYS.md §6.3). The in-menu picture below is only for a replay that cannot
            // be launched (a mode with no card).
            if (replayService.TryWatchMatch(summary, gameList ? gameList.Games : null, out string matchError))
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] watching {summary.RecordingFile} as the match");
                return;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] {summary.RecordingFile} plays in the menu: {matchError}");

            if (!replayService.TryLoad(summary, out var recording, out string error))
            {
                CSDebug.LogWarning($"[Replay] Cannot watch '{summary.RecordingFile}': {error}");
                ShowStatus(openFailedLine);
                return;
            }

            // The HUD builds the replay's arena first (its loading panel covers the wait), then plays;
            // it closes itself on any ending and says whether it failed.
            _hud = Instantiate(playbackHudPrefab);
            _watched = summary;
            _hud.Closed += HandleWatchClosed;
            if (windowGroup) windowGroup.interactable = false;
            bool fromMenu = !Switcher || !Switcher.IsInFreestyle;
            _hud.PlayAsync(recording, summary, ModeName(summary), WhenLine(summary), fromMenu, container,
                           gameData ? gameData.ThemeManagerData : null).Forget();
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] watching {summary.RecordingFile}");
        }

        void HandleWatchClosed()
        {
            bool failed = _hud && _hud.Failed;
            if (_hud) _hud.Closed -= HandleWatchClosed;
            _hud = null;
            if (failed) ShowStatus(openFailedLine);
            _watchEndedFrame = Time.frameCount;
            if (windowGroup) windowGroup.interactable = true;
            FocusFirstRow(_watched);
            _watched = null;
        }

        void Delete(ReplaySummary summary)
        {
            if (replayService == null) return;
            replayService.Delete(summary);
            Refresh();
        }

        void ShowStatus(string line)
        {
            if (statusText) statusText.text = line;
            KitVisibility.SetShown(statusGroup, !string.IsNullOrEmpty(line));
        }

        string ModeName(ReplaySummary summary)
        {
            if (gameList && gameList.Games != null)
                foreach (var game in gameList.Games)
                    if (game && (int)game.Mode == summary.GameMode && !string.IsNullOrEmpty(game.DisplayName))
                        return game.DisplayName;
            return summary.GameModeName;
        }

        string WhenLine(ReplaySummary summary)
        {
            string date;
            try { date = ReplaySummary.RecordedAt(summary).ToLocalTime().ToString(dateFormat, CultureInfo.InvariantCulture); }
            catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException) { date = string.Empty; }
            return TemplateText.Fill(whenTemplate, "date", date, "duration", ReplaySummary.Clock(summary.DurationSeconds));
        }

        string ResultLine(ReplaySummary summary) =>
            summary.HasResults && !string.IsNullOrEmpty(summary.WinnerName)
                ? TemplateText.Fill(winnerTemplate, "winner", summary.WinnerName)
                : noResultLine;

        string DetailLine(ReplaySummary summary) =>
            TemplateText.Fill(detailTemplate,
                              "pilots", (summary.Pilots?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                              "intensity", summary.Intensity.ToString(CultureInfo.InvariantCulture));
    }
}
