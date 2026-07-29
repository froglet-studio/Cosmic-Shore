using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Visual/behavioral grouping of quest nodes. Drives node header colors and the
    /// legend in the Quest Graph editor — keep the meanings stable so designers can
    /// read a graph at a glance.
    /// </summary>
    public enum QuestNodeCategory
    {
        /// <summary>Pacing and cinematics: intro/outro, fixed waits.</summary>
        Flow = 0,
        /// <summary>Player-facing words: instruction text and dialogue sets.</summary>
        Presentation = 1,
        /// <summary>Acts on gameplay/menu state: freestyle control, navigation, mode locking.</summary>
        Gameplay = 2,
        /// <summary>Waits for the player or the game to do something.</summary>
        Gate = 3,
        /// <summary>CTA breadcrumbs — "go here, do this".</summary>
        Guidance = 4,
        /// <summary>Writes progression state (unlocks).</summary>
        Progression = 5,
        /// <summary>Ends a phase or the whole quest.</summary>
        Terminal = 6,
    }

    /// <summary>
    /// Where the player physically IS while a node runs — the app shell (menus, modals,
    /// profile, arcade) or gameplay (freestyle flight, a launched match). Only the nodes
    /// that MOVE the player between the two declare a venue; everything else inherits the
    /// venue of the beat before it. The graph editor's row layout starts a new row at each
    /// declared change, so a phase reads as one row per place the player is standing.
    /// </summary>
    public enum QuestVenue
    {
        /// <summary>Runs wherever the previous node left the player (the default).</summary>
        Inherit = 0,
        /// <summary>Menus, modals, the arcade — anything outside a flying/playing session.</summary>
        AppShell = 1,
        /// <summary>Freestyle flight or a launched game session.</summary>
        Gameplay = 2,
    }

    /// <summary>
    /// Base ScriptableObject for every node in a quest phase graph.
    ///
    /// Each concrete node is its own SO subclass carrying typed authoring fields and
    /// its own <see cref="Execute"/> behaviour — a polymorphic-SO design (mirrors the
    /// project's Effect / Scoring-rule SOs). Nodes are stored as sub-assets of the
    /// owning <see cref="QuestPhaseGraphSO"/>.
    ///
    /// A node is asset config only — it must hold NO per-run mutable state. All runtime
    /// state lives in the coroutine's locals and the passed-in <see cref="QuestRuntimeContext"/>,
    /// so the same asset can be re-run safely.
    /// </summary>
    public abstract class QuestNodeSO : ScriptableObject
    {
        [Tooltip("Stable identifier used to wire edges and record UGS progress. Assigned once on creation — never reuse or hand-edit.")]
        [HideInInspector] public string nodeId;

        [Tooltip("Human-readable label shown on the node in the graph editor (authoring aid only).")]
        public string displayName;

        [Tooltip("When off, the runner passes straight through this node without executing it (test harness).")]
        public bool nodeEnabled = true;

        [Tooltip("Node position on the editor canvas. Runtime-irrelevant.")]
        [HideInInspector] public Vector2 graphPosition;

        [Tooltip("Outgoing edges keyed by port name. Managed by the graph editor's port drag, not hand-edited.")]
        [HideInInspector] [SerializeField] private List<QuestEdge> outputs = new();

        /// <summary>Outgoing edges of this node.</summary>
        public List<QuestEdge> Outputs => outputs;

        /// <summary>Short type label for the editor header (e.g. "EnterFreestyle").</summary>
        public virtual string NodeTypeLabel => GetType().Name.Replace("Quest", string.Empty).Replace("Node", string.Empty);

        /// <summary>Visual/behavioral category — drives the node's header color + the editor legend.</summary>
        public virtual QuestNodeCategory Category => QuestNodeCategory.Flow;

        /// <summary>One-paragraph explanation of what this node does, shown as a hover tooltip in the editor.</summary>
        public virtual string TypeTooltip => string.Empty;

        /// <summary>Short live summary of the node's authored fields, shown on the card body in the editor.</summary>
        public virtual string EditorSummary => string.Empty;

        /// <summary>
        /// Where the player is WHILE this node runs. Override only on nodes that move the
        /// player between the app shell and gameplay (enter/exit freestyle, game launch/played,
        /// milestone gates the player has to go play for) — every other node inherits, so a
        /// row break in the editor always marks a real context switch.
        /// </summary>
        public virtual QuestVenue Venue => QuestVenue.Inherit;

        /// <summary>
        /// Where the player is AFTER this node completes. Defaults to <see cref="Venue"/>;
        /// override for "away trip" nodes that span a whole excursion and hand the player
        /// back somewhere else (e.g. WaitForGamePlayed runs during a match and returns to
        /// the app shell).
        /// </summary>
        public virtual QuestVenue VenueAfter => Venue;

        /// <summary>
        /// The output ports this node can advance through. Linear nodes emit only
        /// <see cref="QuestPorts.Next"/>; override to expose branch ports, or return an
        /// empty list for terminal nodes.
        /// </summary>
        public virtual IReadOnlyList<string> OutputPorts => QuestPorts.NextOnly;

        /// <summary>
        /// Run this node. Do the node's work (drive a runtime system, wait for a signal),
        /// then call <paramref name="advance"/> exactly once with the chosen output port
        /// (usually <see cref="QuestPorts.Next"/>). The coroutine ending does NOT advance —
        /// only calling <paramref name="advance"/> does — so event-driven nodes may subscribe,
        /// register cleanup via <see cref="QuestRuntimeContext.AddCleanup"/>, and yield break.
        /// Terminal nodes call <see cref="QuestRuntimeContext.CompletePhase"/> /
        /// <see cref="QuestRuntimeContext.CompleteQuest"/> instead of advancing.
        /// </summary>
        public abstract IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance);

        /// <summary>
        /// TESTING hook, called by the runner's force-advance right before skipping this node:
        /// apply the REAL state this node was waiting for (unlock the tier, unlock the mode)
        /// so downstream systems — the profile claim flow, card locks — behave as if the
        /// player earned it. Default: nothing (pure waits/presentation need no state).
        /// </summary>
        public virtual void DebugForceSatisfy(QuestRuntimeContext ctx) { }

        /// <summary>
        /// Author-time validation. Append human-readable problems to <paramref name="errors"/>
        /// (missing references, unreachable targets). Called by the graph editor, never at runtime.
        /// </summary>
        public virtual void Validate(QuestPhaseGraphSO graph, List<string> errors) { }

        /// <summary>Resolve the edge for a given output port, or null for a dead end.</summary>
        public QuestEdge EdgeForPort(string port)
        {
            foreach (var e in outputs)
                if (e != null && e.portName == port)
                    return e;
            return null;
        }
    }
}
