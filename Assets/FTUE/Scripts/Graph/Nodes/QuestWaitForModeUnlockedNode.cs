using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until a game mode becomes unlocked — i.e. the player pressed CLAIM on the
    /// quest track (or the mode was unlocked any other way). This is the "ask them to
    /// unlock the next game mode" beat: guide the player to the profile screen, then
    /// this node holds until the claim lands.
    /// </summary>
    public class QuestWaitForModeUnlockedNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the mode is unlocked — normally by the player pressing CLAIM on the quest track. Passes immediately if already unlocked.";
        public override string EditorSummary => $"Until {mode} unlocked (claim)";

        [Tooltip("The game mode whose unlock (claim) this node waits on.")]
        public GameModes mode = GameModes.HexRace;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc == null)
            {
                Debug.LogError("[Quest] WaitForModeUnlockedNode: GameModeProgressionService not alive — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            if (svc.IsGameModeUnlocked(mode))
            {
                advance(QuestPorts.Next);
                yield break;
            }

            void OnChanged(GameModeProgressionData data)
            {
                if (svc.IsGameModeUnlocked(mode))
                    advance(QuestPorts.Next);
            }

            svc.OnProgressionChanged += OnChanged;
            ctx.AddCleanup(() => svc.OnProgressionChanged -= OnChanged);
        }

        /// <summary>
        /// Force-advance performs the REAL unlock (same write the quest track's CLAIM makes),
        /// so the arcade card unlocks and downstream play gates are actually reachable.
        /// </summary>
        public override void DebugForceSatisfy(QuestRuntimeContext ctx)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc == null)
            {
                Debug.LogWarning("[Quest] Force-satisfy: GameModeProgressionService not alive — mode not unlocked.");
                return;
            }

            svc.UnlockMode(mode);
            Debug.Log($"[Quest] Force-satisfy: {mode} unlocked (as if claimed).");
        }
    }
}
