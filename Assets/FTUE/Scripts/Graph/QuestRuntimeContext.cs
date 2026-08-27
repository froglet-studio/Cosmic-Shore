using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// A scene Button quest nodes can enable/disable by key (e.g. the Episodes button starts
    /// non-interactable and phase 5 unlocks it). Wired on the runner; keys are free-form.
    /// </summary>
    [Serializable]
    public class QuestButtonEntry
    {
        [Tooltip("Key referenced by SetButtonInteractable nodes (e.g. 'episodes').")]
        public string key;
        public UnityEngine.UI.Button button;
    }

    /// <summary>
    /// The bundle of live scene systems and shared SOAP assets a <see cref="QuestNodeSO"/>
    /// needs to execute. Built once by the <c>QuestGraphRunner</c> from its serialized
    /// references and handed to every node's <see cref="QuestNodeSO.Execute"/>.
    ///
    /// Keeps nodes decoupled from how the runner is wired: a node asks the context for
    /// "the freestyle events" or "the input-pressed channel", never for a specific scene
    /// object. Also owns per-node subscription cleanup so event-driven nodes can never
    /// leak a delegate onto a persistent SOAP asset (the class of bug called out in
    /// CLAUDE.md's anti-patterns).
    /// </summary>
    public class QuestRuntimeContext
    {
        /// <summary>The runner MonoBehaviour — use to start nested coroutines when needed.</summary>
        public MonoBehaviour Host;

        // ── Shared state / SOAP ──
        public GameDataSO GameData;
        public MenuFreestyleEventsContainerSO FreestyleEvents;
        public ScriptableEventInputEvents OnButtonPressed;
        public ScriptableEventBoostChanged OnSkimBoost;

        // ── Scene systems ──
        public MenuCrystalClickHandler CrystalHandler;
        public QuestInstructionView InstructionView;
        public ScreenSwitcher ScreenSwitcher;
        public IReadOnlyList<CallToActionTarget> GameCards;

        /// <summary>Extra UI groups hidden while flight training runs (designer-authored; the
        /// vessel HUD itself is hidden through its own controller, not this list).</summary>
        public IReadOnlyList<CanvasGroup> TrainingHiddenGroups;

        /// <summary>The FTUE-funnel nav buttons the quest may lock (Hangar + Profile links —
        /// Ark/Port are permanently scene-locked and must never be listed here).</summary>
        public IReadOnlyList<UnityEngine.UI.Button> NavButtons;

        /// <summary>Never locked: the button that opens the Arcade.</summary>
        public UnityEngine.UI.Button AllowedNavButton;

        /// <summary>Keyed scene buttons quest nodes can enable/disable (see <see cref="QuestButtonEntry"/>).</summary>
        public IReadOnlyList<QuestButtonEntry> QuestButtons;

        /// <summary>Flip a registered quest button's interactable state (key match is case-insensitive).</summary>
        public void SetQuestButtonInteractable(string key, bool interactable)
        {
            if (QuestButtons != null && !string.IsNullOrEmpty(key))
            {
                foreach (var entry in QuestButtons)
                {
                    if (entry?.button == null || !string.Equals(entry.key, key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    entry.button.interactable = interactable;
                    Debug.Log($"[Quest] Button '{key}' → interactable={interactable}.");
                    return;
                }
            }

            Debug.LogWarning($"[Quest] SetButtonInteractable: no button registered under key '{key}' on the runner (Quest Buttons list).");
        }

        /// <summary>The vessel action buttons disabled during flight training (sticks + triggers stay live).</summary>
        static readonly InputEvents[] TrainingSuppressedButtons =
            { InputEvents.Button1Action, InputEvents.Button2Action, InputEvents.Button3Action, InputEvents.FlipAction };

        /// <summary>True between <see cref="BeginFlightTraining"/> and <see cref="EndFlightTraining"/> —
        /// the runner keeps the training-hidden groups forced to alpha 0 while this holds (so a late
        /// freestyle fade can't re-show them).</summary>
        public bool FlightTrainingActive { get; private set; }

        /// <summary>
        /// Flight school starts: hide the local vessel HUD (through its own controller — the same
        /// channel MenuMiniGameHUD uses), suppress the action buttons (A/X/B/flip) so only the
        /// taught controls do anything, and zero any extra authored groups. The volume/pause
        /// button stays visible — it IS the taught exit.
        /// </summary>
        public void BeginFlightTraining()
        {
            FlightTrainingActive = true;

            GameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.HideHUD();
            SetTrainingButtonsSuppressed(true);
            HideTrainingGroups();
        }

        /// <summary>
        /// Flight school ends (player exited via the volume button, or the quest tears down):
        /// release the button suppression. No HUD restore needed — the freestyle exit hides the
        /// HUD anyway, and the next manual freestyle entry re-shows it (MenuMiniGameHUD).
        /// </summary>
        public void EndFlightTraining()
        {
            if (!FlightTrainingActive) return;
            FlightTrainingActive = false;
            SetTrainingButtonsSuppressed(false);
        }

        void SetTrainingButtonsSuppressed(bool on)
        {
            var vesselTransform = GameData?.LocalPlayer?.Vessel?.Transform;
            var handler = vesselTransform != null ? vesselTransform.GetComponent<R_VesselActionHandler>() : null;
            if (handler == null)
            {
                Debug.LogWarning("[Quest] Flight training: no R_VesselActionHandler on the local vessel — action buttons not gated.");
                return;
            }

            foreach (var ie in TrainingSuppressedButtons)
                handler.SetInputSuppressed(ie, on);
        }

        /// <summary>Zero the extra training-hidden groups (also enforced per-frame by the runner while training is active).</summary>
        public void HideTrainingGroups()
        {
            if (TrainingHiddenGroups == null) return;
            foreach (var group in TrainingHiddenGroups)
            {
                if (group == null) continue;
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        /// <summary>
        /// Lock the footer nav down to <see cref="AllowedNavButton"/> (or restore everything).
        /// Scene-state only — the runner always unlocks on teardown.
        /// </summary>
        public void SetNavLocked(bool locked)
        {
            if (NavButtons == null || NavButtons.Count == 0)
            {
                if (locked)
                    Debug.LogWarning("[Quest] LockNavigation: no nav buttons wired on the runner — nothing to lock.");
                return;
            }

            foreach (var btn in NavButtons)
            {
                if (btn == null) continue;
                btn.interactable = !locked || btn == AllowedNavButton;
            }
        }

        // ── Dialogue panel (self-contained; no dialogue system) ──
        /// <summary>ONE slot: a scene instance of the dialogue panel, or a PREFAB asset that is
        /// instantiated under <see cref="DialoguePanelParent"/> on first use.</summary>
        public QuestDialoguePanelView DialoguePanel;
        public Transform DialoguePanelParent;

        QuestDialoguePanelView _dialogueInstance;

        /// <summary>The live panel Dialogue nodes drive (instantiating the prefab if needed).</summary>
        public QuestDialoguePanelView GetOrCreateDialoguePanel()
        {
            if (_dialogueInstance != null)
                return _dialogueInstance;

            if (DialoguePanel == null)
                return null;

            // A scene object has a valid scene handle; a prefab asset does not.
            if (DialoguePanel.gameObject.scene.IsValid())
            {
                _dialogueInstance = DialoguePanel;
            }
            else
            {
                _dialogueInstance = UnityEngine.Object.Instantiate(DialoguePanel, DialoguePanelParent);
                Debug.Log("[Quest] Dialogue panel instantiated from prefab.");
            }

            return _dialogueInstance;
        }

        // ── Flow-control hooks (implemented by the runner) ──
        /// <summary>End the CURRENT PHASE and advance the quest to the next phase graph.</summary>
        public Action CompletePhase;

        /// <summary>Complete the WHOLE QUEST (persists completion to UGS + local, restores breadcrumb authority).</summary>
        public Action CompleteQuest;

        // ── Per-node subscription cleanup ──
        readonly List<Action> _cleanups = new();

        /// <summary>Register an undo action (e.g. an event unsubscribe) for the current node.</summary>
        public void AddCleanup(Action cleanup)
        {
            if (cleanup != null)
                _cleanups.Add(cleanup);
        }

        /// <summary>Run and clear all registered cleanups. Called by the runner on every advance and on stop.</summary>
        public void RunCleanups()
        {
            for (int i = _cleanups.Count - 1; i >= 0; i--)
            {
                try { _cleanups[i]?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[Quest] Node cleanup threw: {e.Message}"); }
            }
            _cleanups.Clear();
        }
    }
}
