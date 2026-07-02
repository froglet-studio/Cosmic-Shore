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

        [Header("Scene Systems")]
        [SerializeField] MenuCrystalClickHandler crystalHandler;
        [SerializeField] TutorialUIView tutorialUI;
        [SerializeField] FTUEIntroAnimator introAnimator;
        [SerializeField] DialogueManager dialogueManager;
        [SerializeField] ScreenSwitcher screenSwitcher;
        [Tooltip("Arcade game cards, used by LockModes nodes.")]
        [SerializeField] List<CallToActionTarget> gameCards = new();

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
                CrystalHandler = crystalHandler,
                TutorialUI = tutorialUI,
                IntroAnimator = introAnimator,
                DialogueManager = dialogueManager,
                ScreenSwitcher = screenSwitcher,
                GameCards = gameCards,
                CompletePhase = HandlePhaseComplete,
                CompleteQuest = HandleQuestComplete,
            };
        }

        // ── Phase sequencing ───────────────────────────────────────────

        void StartPhase(bool resume)
        {
            // Skip null phase slots defensively (mis-authored quest).
            while (_phaseIndex < quest.phases.Count && quest.phases[_phaseIndex] == null)
            {
                Debug.LogWarning($"[Quest] Phase {_phaseIndex} of '{QuestId}' is null — skipping.");
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
                RunNode(next);
            }
            else
            {
                Debug.LogWarning($"[Quest] Node '{from.displayName}' dead-ends without a Phase End — advancing phase anyway.");
                HandlePhaseComplete();
            }
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
            QuestProgressStore.ReportPhaseCompleted(QuestId, _phaseIndex);

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
            _current = null;

            QuestProgressStore.MarkQuestCompleted(QuestId);
            SetBreadcrumbSuppressed(false);
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
