using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Runtime driver for an authored <see cref="FTUEGraphSO"/>. Replaces the flat-list walk
    /// of <c>TutorialFlowController</c> with graph traversal (named-port branching).
    ///
    /// Lifecycle:
    ///   • Waits for the menu autopilot vessel to exist (<c>GameData.OnClientReady</c>) so
    ///     force-enter-freestyle and input steps have a live vessel.
    ///   • Gates on <see cref="FTUECompletionStore"/> — runs only if the FTUE isn't already done.
    ///   • Executes the graph from <see cref="FTUEGraphSO.entryNode"/>, dispatching each node's
    ///     own <see cref="FTUENodeSO.Execute"/> and following the edge for the port it advances on.
    ///
    /// Wire the scene/asset references in the inspector (drop this on the Menu_Main "Game"
    /// object). The <c>Tools ▸ FrogletTools ▸ FTUE ▸ Setup Runner In Scene</c> helper can
    /// auto-resolve most of them.
    /// </summary>
    public class FTUEGraphRunner : MonoBehaviour
    {
        [Header("Graph")]
        [Tooltip("The authored FTUE flow to run.")]
        [SerializeField] FTUEGraphSO graph;

        [Header("Shared State / SOAP")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] MenuFreestyleEventsContainerSO freestyleEvents;
        [Tooltip("The SAME ScriptableEventInputEvents asset the vessel's InputStatus raises on button press.")]
        [SerializeField] ScriptableEventInputEvents onButtonPressed;

        [Header("Scene Systems")]
        [SerializeField] MenuCrystalClickHandler crystalHandler;
        [SerializeField] TutorialUIView tutorialUI;
        [SerializeField] FTUEIntroAnimator introAnimator;
        [SerializeField] DialogueManager dialogueManager;
        [SerializeField] ScreenSwitcher screenSwitcher;
        [Tooltip("Arcade game cards, used by LockModes nodes.")]
        [SerializeField] List<CallToActionTarget> gameCards = new();

        [Header("Gating (Debug)")]
        [Tooltip("Optional legacy progress asset — if assigned and its ftueDebugKey is on, the FTUE is skipped.")]
        [SerializeField] FTUEProgress ftueProgress;
        [Tooltip("Never run the FTUE (hard off switch for this scene).")]
        [SerializeField] bool debugDisable;
        [Tooltip("Run even if the player already completed the FTUE (testing).")]
        [SerializeField] bool debugForceRun;
        [Tooltip("Delay after the vessel is ready before the FTUE begins.")]
        [Min(0f)] [SerializeField] float startDelaySeconds = 0.5f;

        FTUERuntimeContext _ctx;
        FTUENodeSO _current;
        bool _started;
        bool _advanced;
        bool _stopped;

        // ── Unity lifecycle ────────────────────────────────────────────

        void OnEnable()
        {
            _stopped = false;

            if (gameData != null && gameData.OnClientReady != null)
                gameData.OnClientReady.OnRaised += HandleClientReady;

            // Optional manual/external trigger (e.g. a debug menu).
            FTUEEventManager.InitializeFTUE += TryStart;
        }

        void OnDisable()
        {
            _stopped = true;

            if (gameData != null && gameData.OnClientReady != null)
                gameData.OnClientReady.OnRaised -= HandleClientReady;

            FTUEEventManager.InitializeFTUE -= TryStart;

            StopAllCoroutines();
            _ctx?.RunCleanups();
        }

        // ── Start gating ───────────────────────────────────────────────

        void HandleClientReady() => TryStart();

        void TryStart()
        {
            if (_started || _stopped) return;

            if (debugDisable)
            {
                Debug.Log("[FTUE] Runner disabled (debugDisable).");
                return;
            }

            if (ftueProgress != null && ftueProgress.ftueDebugKey)
            {
                Debug.Log("[FTUE] Runner skipped (FTUEProgress.ftueDebugKey).");
                return;
            }

            if (!debugForceRun && FTUECompletionStore.IsCompleted())
            {
                Debug.Log("[FTUE] Already completed — not running.");
                return;
            }

            if (graph == null || graph.entryNode == null)
            {
                Debug.LogWarning("[FTUE] No graph / entry node assigned on runner — nothing to run.");
                return;
            }

            _started = true;
            StartCoroutine(StartAfterDelay());
        }

        IEnumerator StartAfterDelay()
        {
            if (startDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(startDelaySeconds);

            BuildContext();
            Debug.Log($"[FTUE] Starting flow '{(string.IsNullOrEmpty(graph.graphId) ? graph.name : graph.graphId)}'.");
            RunNode(graph.entryNode);
        }

        void BuildContext()
        {
            _ctx = new FTUERuntimeContext
            {
                Host = this,
                GameData = gameData,
                FreestyleEvents = freestyleEvents,
                OnButtonPressed = onButtonPressed,
                CrystalHandler = crystalHandler,
                TutorialUI = tutorialUI,
                IntroAnimator = introAnimator,
                DialogueManager = dialogueManager,
                ScreenSwitcher = screenSwitcher,
                GameCards = gameCards,
                SaveProgress = FTUECompletionStore.SaveProgress,
                MarkCompleted = MarkFtueCompleted,
            };
        }

        // ── Graph traversal ────────────────────────────────────────────

        void RunNode(FTUENodeSO node)
        {
            if (_stopped) return;

            if (node == null)
            {
                FinishGraph();
                return;
            }

            _current = node;
            _advanced = false;
            Debug.Log($"[FTUE] Node → {node.NodeTypeLabel} ({node.displayName})");
            StartCoroutine(RunNodeCoroutine(node));
        }

        IEnumerator RunNodeCoroutine(FTUENodeSO node)
        {
            // The coroutine ending does NOT advance; only the node calling `advance` does.
            yield return node.Execute(_ctx, port => Advance(node, port));
        }

        void Advance(FTUENodeSO from, string port)
        {
            if (_stopped) return;
            if (from != _current) return;   // stale advance from a superseded node
            if (_advanced) return;          // one advance per node
            _advanced = true;

            _ctx.RunCleanups();

            var edge = from.EdgeForPort(port);
            var next = edge != null ? graph.FindNode(edge.targetNodeId) : null;

            if (next != null)
                RunNode(next);
            else
                FinishGraph();
        }

        void FinishGraph()
        {
            _ctx?.RunCleanups();
            _current = null;
            Debug.Log("[FTUE] Flow complete.");
        }

        void MarkFtueCompleted()
        {
            FTUECompletionStore.MarkCompleted();
            Debug.Log("[FTUE] Marked completed (cloud + local).");
        }
    }
}
