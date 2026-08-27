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
    /// Runtime driver for a <see cref="QuestSO"/> — an ordered sequence of phase graphs.
    ///
    /// Lifecycle:
    ///   • Waits for the menu autopilot vessel to exist (<c>GameData.OnClientReady</c>) so
    ///     force-enter-freestyle and input steps have a live vessel.
    ///   • Gates on <see cref="QuestProgressStore"/> — runs only if the quest isn't already done.
    ///   • Resumes at the saved phase + node (every completed node is persisted to UGS).
    ///   • Executes each phase graph node-by-node; a <c>QuestPhaseEndNode</c> advances to the
    ///     next phase, a <c>QuestEndNode</c> completes the quest.
    ///   • While running, the runner owns guidance: the progression service's automatic
    ///     frontier breadcrumb is suppressed and restored on completion/teardown.
    ///
    /// Wire the scene/asset references in the inspector (drop this on the Menu_Main "Game"
    /// object). FrogletTools ▸ Quest Graph ▸ Setup Runner In Scene auto-resolves most of them.
    /// </summary>
    public class QuestGraphRunner : MonoBehaviour
    {
        [Header("Quest")]
        [Tooltip("The quest to run — an ordered sequence of phase graphs.")]
        [SerializeField] QuestSO quest;

        [Tooltip("TESTING: run this single phase graph instead of the quest (no persistence writes for phase/quest completion).")]
        [SerializeField] QuestPhaseGraphSO debugPhaseOverride;

        [Header("Shared State / SOAP")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] MenuFreestyleEventsContainerSO freestyleEvents;
        [Tooltip("The SAME ScriptableEventInputEvents asset the vessel's InputStatus raises on button press.")]
        [SerializeField] ScriptableEventInputEvents onButtonPressed;

        [Tooltip("The shared skim-boost channel (ScriptableEventBoostChanged) raised per skimmed prism — used by WaitForSkim gates.")]
        [SerializeField] ScriptableEventBoostChanged skimBoostEvent;

        [Header("Scene Systems")]
        [SerializeField] MenuCrystalClickHandler crystalHandler;
        [Tooltip("Lightweight text+haptics overlay used by ShowInstruction nodes (control teaching).")]
        [SerializeField] QuestInstructionView instructionView;
        [Tooltip("The dialogue panel (DialogueSetUI): a SCENE instance or a PREFAB asset — a prefab is instantiated under Dialogue Panel Parent on first use. Dialogue nodes drive it directly, no dialogue system.")]
        [SerializeField] QuestDialoguePanelView dialoguePanel;
        [Tooltip("Parent for the panel when Dialogue Panel is a prefab asset (e.g. the UI root).")]
        [SerializeField] Transform dialoguePanelParent;
        [SerializeField] ScreenSwitcher screenSwitcher;
        [Tooltip("Arcade game cards, used by LockModes nodes.")]
        [SerializeField] List<CallToActionTarget> gameCards = new();

        [Tooltip("EXTRA UI groups hidden during flight training. The vessel HUD is hidden automatically through its own controller — do NOT add 'Game UI' here (that would also hide the volume button, which is the taught exit).")]
        [SerializeField] List<CanvasGroup> hideDuringFlightTraining = new();

        [Tooltip("Nav buttons LockNavigation nodes disable during the FTUE funnel: the HANGAR and PROFILE links only. Ark/Port are permanently locked scene-side (never list them here — the Unlock step would re-enable them) and Home stays available. Auto-wired by the Phase 0 wirer.")]
        [SerializeField] List<UnityEngine.UI.Button> lockableNavButtons = new();

        [Tooltip("The button that opens the Arcade (wired to OnClickArcadeNav) — always stays clickable while navigation is locked.")]
        [SerializeField] UnityEngine.UI.Button arcadeNavButton;

        [Tooltip("Keyed scene buttons SetButtonInteractable nodes can enable/disable — e.g. the Episodes button (key 'episodes') starts non-interactable and phase 5 unlocks it before its CTA.")]
        [SerializeField] List<QuestButtonEntry> questButtons = new();

        [Header("Gating (Debug)")]
        [Tooltip("Never run the quest (hard off switch for this scene).")]
        [SerializeField] bool debugDisable;
        [Tooltip("Run even if the player already completed the quest, ignoring saved progress (testing).")]
        [SerializeField] bool debugForceRun;
        [Tooltip("Delay after the vessel is ready before the quest begins.")]
        [Min(0f)] [SerializeField] float startDelaySeconds = 0.5f;

        QuestRuntimeContext _ctx;
        QuestPhaseGraphSO _activeGraph;
        QuestNodeSO _current;
        int _phaseIndex;
        bool _started;
        bool _advanced;
        bool _stopped;

        string QuestId => quest != null ? quest.QuestId
            : debugPhaseOverride != null ? debugPhaseOverride.name
            : string.Empty;


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
            _ctx?.EndFlightTraining();
            _ctx?.SetNavLocked(false);
            // NOTE: QuestArcadeConstraints are NOT cleared here — they must survive the
            // Menu → game → Menu scene round-trip. They clear via an authored Clear node,
            // quest completion, or the editor's progress reset.
            SetBreadcrumbSuppressed(false);
        }

        // ── Start gating ───────────────────────────────────────────────

        void HandleClientReady() => TryStart();

        void TryStart()
        {
            if (_started || _stopped) return;

            if (debugDisable)
            {
                Debug.Log("[Quest] Runner disabled (debugDisable).");
                return;
            }

            if (quest != null && !quest.questEnabled && debugPhaseOverride == null)
            {
                Debug.Log($"[Quest] '{QuestId}' is disabled (questEnabled=false) — not running.");
                return;
            }

            bool hasQuest = quest != null && quest.phases.Count > 0;
            if (!hasQuest && debugPhaseOverride == null)
            {
                Debug.LogWarning("[Quest] No quest (or debug phase) assigned on runner — nothing to run.");
                return;
            }

            if (!debugForceRun && QuestProgressStore.IsCompleted(QuestId))
            {
                Debug.Log($"[Quest] '{QuestId}' already completed — not running.");
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
            SetBreadcrumbSuppressed(true);

            if (debugPhaseOverride != null)
            {
                _phaseIndex = -1;
                _activeGraph = debugPhaseOverride;
                Debug.Log($"[Quest] TEST-running single phase '{debugPhaseOverride.PhaseName}'.");
                RunNode(_activeGraph.entryNode);
                yield break;
            }

            _phaseIndex = debugForceRun ? 0
                : Mathf.Clamp(QuestProgressStore.GetPhaseIndex(QuestId), 0, quest.phases.Count - 1);

            Debug.Log($"[Quest] Starting '{QuestId}' at phase {_phaseIndex}.");
            StartPhase(resume: !debugForceRun);
        }

        void BuildContext()
        {
            _ctx = new QuestRuntimeContext
            {
                Host = this,
                GameData = gameData,
                FreestyleEvents = freestyleEvents,
                OnButtonPressed = onButtonPressed,
                OnSkimBoost = skimBoostEvent,
                CrystalHandler = crystalHandler,
                InstructionView = instructionView,
                DialoguePanel = dialoguePanel,
                DialoguePanelParent = dialoguePanelParent,
                ScreenSwitcher = screenSwitcher,
                GameCards = gameCards,
                TrainingHiddenGroups = hideDuringFlightTraining,
                NavButtons = lockableNavButtons,
                AllowedNavButton = arcadeNavButton,
                QuestButtons = questButtons,
                CompletePhase = HandlePhaseComplete,
                CompleteQuest = HandleQuestComplete,
            };
        }

        /// <summary>
        /// While flight training is active, keep the extra hidden groups at alpha 0 — the
        /// freestyle toggle's own fade can finish AFTER the transition-end event and would
        /// otherwise re-show them (the bug that left the HUD visible during training).
        /// </summary>
        void LateUpdate()
        {
            if (_ctx == null || !_ctx.FlightTrainingActive) return;
            _ctx.HideTrainingGroups();
        }

        // ── Phase sequencing ───────────────────────────────────────────

        void StartPhase(bool resume)
        {
            // Skip null or disabled phase slots (test harness / mis-authored quest).
            while (_phaseIndex < quest.phases.Count
                   && (quest.phases[_phaseIndex] == null || !quest.phases[_phaseIndex].phaseEnabled))
            {
                Debug.Log($"[Quest] Phase {_phaseIndex} of '{QuestId}' is " +
                          $"{(quest.phases[_phaseIndex] == null ? "null" : "disabled")} — skipping.");
                _phaseIndex++;
            }

            if (_phaseIndex >= quest.phases.Count)
            {
                HandleQuestComplete();
                return;
            }

            _activeGraph = quest.phases[_phaseIndex];

            var startNode = _activeGraph.entryNode;
            if (resume)
            {
                var savedNode = _activeGraph.FindNode(QuestProgressStore.GetCurrentNodeId(QuestId));
                if (savedNode != null)
                {
                    startNode = savedNode;
                    Debug.Log($"[Quest] Resuming phase {_phaseIndex} ('{_activeGraph.PhaseName}') at node '{savedNode.displayName}'.");
                }
            }

            Debug.Log($"[Quest] Phase {_phaseIndex}: '{_activeGraph.PhaseName}'.");
            RunNode(startNode);
        }

        // ── Graph traversal ────────────────────────────────────────────

        void RunNode(QuestNodeSO node)
        {
            if (_stopped) return;

            if (node == null)
            {
                HandlePhaseComplete();
                return;
            }

            if (!node.nodeEnabled)
            {
                Debug.Log($"[Quest] Node '{node.displayName}' disabled — passing through.");
                _current = node;
                _advanced = false;
                Advance(node, QuestPorts.Next);
                return;
            }

            _current = node;
            _advanced = false;
            Debug.Log($"[Quest] Node → {node.NodeTypeLabel} ({node.displayName})");
            StartCoroutine(RunNodeCoroutine(node));
        }

        IEnumerator RunNodeCoroutine(QuestNodeSO node)
        {
            // The coroutine ending does NOT advance; only the node calling `advance` does.
            yield return node.Execute(_ctx, port => Advance(node, port));
        }

        void Advance(QuestNodeSO from, string port)
        {
            if (_stopped) return;
            if (from != _current) return;   // stale advance from a superseded node
            if (_advanced) return;          // one advance per node
            _advanced = true;

            _ctx.RunCleanups();

            var edge = from.EdgeForPort(port);
            var next = edge != null ? _activeGraph.FindNode(edge.targetNodeId) : null;

            // Record the completed node + resume cursor (every node lands in UGS).
            if (_phaseIndex >= 0)
                QuestProgressStore.ReportNodeCompleted(QuestId, _phaseIndex, from.nodeId, next != null ? next.nodeId : string.Empty);

            if (next != null)
            {
                float delay = edge != null ? edge.delaySeconds : 0f;
                if (delay > 0f)
                    StartCoroutine(RunNodeAfterDelay(next, delay));
                else
                    RunNode(next);
            }
            else
            {
                Debug.LogWarning($"[Quest] Node '{from.displayName}' dead-ends without a Phase End — advancing phase anyway.");
                HandlePhaseComplete();
            }
        }

        IEnumerator RunNodeAfterDelay(QuestNodeSO next, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (!_stopped)
                RunNode(next);
        }

        /// <summary>
        /// TESTING: force-complete the CURRENT node as if it advanced through its first
        /// output port — skips the gate/action it was waiting for. Persists progress and
        /// runs cleanups exactly like a real advance (driven by the Quest Graph editor's
        /// play-mode "Force-Advance" button so testers don't have to replay every beat).
        /// </summary>
        public void DebugForceAdvance()
        {
            if (_current == null)
            {
                Debug.LogWarning("[Quest] Force-advance: no node is currently active.");
                return;
            }

            var node = _current;
            string port = node.OutputPorts.Count > 0 ? node.OutputPorts[0] : QuestPorts.Next;
            Debug.Log($"[Quest] TEST force-advance past '{node.displayName}' ({node.NodeTypeLabel}).");

            // Apply the REAL state the node was waiting for (unlock the tier / the mode) so
            // downstream systems behave as if the player earned it. May advance the node by
            // itself via the event it was listening to — the Advance below then no-ops.
            try
            {
                node.DebugForceSatisfy(_ctx);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            Advance(node, port);
        }

        // ── Completion ─────────────────────────────────────────────────

        void HandlePhaseComplete()
        {
            _ctx.RunCleanups();
            _current = null;

            if (debugPhaseOverride != null)
            {
                Debug.Log("[Quest] TEST phase finished.");
                SetBreadcrumbSuppressed(false);
                return;
            }

            Debug.Log($"[Quest] Phase {_phaseIndex} ('{_activeGraph.PhaseName}') complete.");
            instructionView?.Hide();
            QuestProgressStore.ReportPhaseCompleted(QuestId, _phaseIndex);
            FTUEEventManager.RaiseQuestPhaseCompleted(QuestId, _phaseIndex);

            _phaseIndex++;
            if (_phaseIndex >= quest.phases.Count)
            {
                HandleQuestComplete();
                return;
            }

            StartPhase(resume: false);
        }

        void HandleQuestComplete()
        {
            _ctx?.RunCleanups();
            _ctx?.EndFlightTraining();
            _ctx?.SetNavLocked(false);
            QuestArcadeConstraints.Clear();
            _current = null;

            instructionView?.Hide();
            QuestProgressStore.MarkQuestCompleted(QuestId);
            SetBreadcrumbSuppressed(false);
            FTUEEventManager.RaiseQuestCompleted(QuestId);
            Debug.Log($"[Quest] '{QuestId}' COMPLETE (persisted to UGS + local).");
        }

        static void SetBreadcrumbSuppressed(bool suppressed)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc != null)
                svc.BreadcrumbSuppressed = suppressed;
        }
    }
}
