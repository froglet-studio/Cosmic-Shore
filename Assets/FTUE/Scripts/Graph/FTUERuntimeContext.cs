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
    /// The bundle of live scene systems and shared SOAP assets an <see cref="FTUENodeSO"/>
    /// needs to execute. Built once by the <c>FTUEGraphRunner</c> from its serialized
    /// references and handed to every node's <see cref="FTUENodeSO.Execute"/>.
    ///
    /// Keeps nodes decoupled from how the runner is wired: a node asks the context for
    /// "the freestyle events" or "the input-pressed channel", never for a specific scene
    /// object. Also owns per-node subscription cleanup so event-driven nodes can never
    /// leak a delegate onto a persistent SOAP asset (the class of bug called out in
    /// CLAUDE.md's anti-patterns).
    /// </summary>
    public class FTUERuntimeContext
    {
        /// <summary>The runner MonoBehaviour — use to start nested coroutines when needed.</summary>
        public MonoBehaviour Host;

        // ── Shared state / SOAP ──
        public GameDataSO GameData;
        public MenuFreestyleEventsContainerSO FreestyleEvents;
        public ScriptableEventInputEvents OnButtonPressed;

        // ── Scene systems ──
        public MenuCrystalClickHandler CrystalHandler;
        public TutorialUIView TutorialUI;
        public FTUEIntroAnimator IntroAnimator;
        public DialogueManager DialogueManager;
        public ScreenSwitcher ScreenSwitcher;
        public IReadOnlyList<CallToActionTarget> GameCards;

        // ── Persistence hooks (implemented by the runner) ──
        /// <summary>Persist "reached this phase / node" for resume + analytics.</summary>
        public Action<TutorialPhase, string> SaveProgress;

        /// <summary>Mark the whole FTUE complete (writes cloud + local, raises completion).</summary>
        public Action MarkCompleted;

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
                catch (Exception e) { Debug.LogWarning($"[FTUE] Node cleanup threw: {e.Message}"); }
            }
            _cleanups.Clear();
        }
    }
}
