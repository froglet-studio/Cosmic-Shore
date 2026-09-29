using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Bridges quest/progression achievements into the existing toast system:
    /// mode unlocked, intensity tier unlocked, quest-track quest completed, quest-graph
    /// phase completed, and quest-graph (FTUE) completed. Drop one on a Menu_Main object;
    /// no wiring needed beyond the message templates.
    /// </summary>
    public class QuestToastNotifier : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("TEMP until the toast UI exists: route messages to CSDebug.Log instead of the toast system. Untick once the toast UI is in the scene.")]
        [SerializeField] bool logInsteadOfToast = true;

        [Header("Toggles")]
        [SerializeField] bool showModeUnlocked = true;
        [SerializeField] bool showIntensityUnlocked = true;
        [SerializeField] bool showQuestTrackCompleted = true;
        [SerializeField] bool showPhaseCompleted = true;
        [SerializeField] bool showQuestCompleted = true;

        [Header("Message Templates")]
        [Tooltip("{0} = mode display name")]
        [SerializeField] string modeUnlockedTemplate = "{0} unlocked!";
        [Tooltip("{0} = mode, {1} = intensity tier")]
        [SerializeField] string intensityUnlockedTemplate = "{0} — Intensity {1} unlocked!";
        [Tooltip("{0} = quest display name (quest track objective met)")]
        [SerializeField] string questTrackCompletedTemplate = "Objective complete: {0} — claim it on your profile!";
        [Tooltip("{0} = phase number (1-based)")]
        [SerializeField] string phaseCompletedTemplate = "Chapter {0} complete!";
        [SerializeField] string questCompletedTemplate = "Welcome aboard, pilot — you know the HyperSea now!";

        GameModeProgressionService _service;

        void Start()
        {
            _service = GameModeProgressionService.Instance;
            if (_service != null)
            {
                _service.OnQuestCompleted += HandleQuestTrackCompleted;
                _service.OnIntensityUnlocked += HandleIntensityUnlocked;
                _service.OnProgressionChanged += HandleProgressionChanged;
            }
            else
            {
                Debug.LogWarning("[Quest] QuestToastNotifier: GameModeProgressionService not alive — progression toasts disabled.");
            }

            FTUEEventManager.OnQuestPhaseCompleted += HandlePhaseCompleted;
            FTUEEventManager.OnQuestCompleted += HandleQuestCompleted;
        }

        void OnDestroy()
        {
            if (_service != null)
            {
                _service.OnQuestCompleted -= HandleQuestTrackCompleted;
                _service.OnIntensityUnlocked -= HandleIntensityUnlocked;
                _service.OnProgressionChanged -= HandleProgressionChanged;
            }

            FTUEEventManager.OnQuestPhaseCompleted -= HandlePhaseCompleted;
            FTUEEventManager.OnQuestCompleted -= HandleQuestCompleted;
        }

        // ── Progression service ────────────────────────────────────────

        int _knownUnlockedCount = -1;

        void HandleProgressionChanged(GameModeProgressionData data)
        {
            // Toast newly-unlocked modes by watching the unlocked count grow (covers both
            // quest-track claims and quest-graph UnlockMode writes with one signal).
            int count = data.UnlockedModes?.Count ?? 0;
            if (_knownUnlockedCount >= 0 && count > _knownUnlockedCount && showModeUnlocked
                && data.UnlockedModes.Count > 0)
            {
                Notify(string.Format(modeUnlockedTemplate,
                    data.UnlockedModes[data.UnlockedModes.Count - 1]));
            }
            _knownUnlockedCount = count;
        }

        void HandleQuestTrackCompleted(SO_UnlockData quest)
        {
            if (!showQuestTrackCompleted || quest == null) return;
            Notify(string.Format(questTrackCompletedTemplate, quest.DisplayName));
        }

        void HandleIntensityUnlocked(GameModes mode, int tier)
        {
            if (!showIntensityUnlocked) return;
            Notify(string.Format(intensityUnlockedTemplate, mode, tier));
        }

        void Notify(string message)
        {
            if (logInsteadOfToast)
                CSDebug.Log($"[Quest][TOAST] {message}");
            else
                ToastNotificationAPI.Show(message);
        }

        // ── Quest graph runner ─────────────────────────────────────────

        void HandlePhaseCompleted(string questId, int phaseIndex)
        {
            if (!showPhaseCompleted) return;
            Notify(string.Format(phaseCompletedTemplate, phaseIndex + 1));
        }

        void HandleQuestCompleted(string questId)
        {
            if (!showQuestCompleted) return;
            Notify(questCompletedTemplate);
        }
    }
}
