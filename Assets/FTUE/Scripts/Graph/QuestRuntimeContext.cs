using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
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

        // ── Dialogue panel (self-contained; no dialogue system) ──
        /// <summary>Scene instance of the dialogue panel (preferred when set).</summary>
        public QuestDialoguePanelView DialoguePanel;
        /// <summary>Prefab fallback — instantiated lazily under <see cref="DialoguePanelParent"/> on first use.</summary>
        public QuestDialoguePanelView DialoguePanelPrefab;
        public Transform DialoguePanelParent;

        /// <summary>The panel Dialogue nodes drive: the scene instance, or a lazily-instantiated prefab copy.</summary>
        public QuestDialoguePanelView GetOrCreateDialoguePanel()
        {
            if (DialoguePanel != null)
                return DialoguePanel;

            if (DialoguePanelPrefab != null)
            {
                DialoguePanel = UnityEngine.Object.Instantiate(DialoguePanelPrefab, DialoguePanelParent);
                Debug.Log("[Quest] Dialogue panel instantiated from prefab.");
            }

            return DialoguePanel;
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
