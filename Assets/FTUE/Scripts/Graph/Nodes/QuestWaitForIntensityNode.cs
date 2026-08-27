using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until a game mode's unlocked intensity reaches a tier — the quest gate the
    /// progression system already evaluates ("reach Intensity 4 to unlock the next mode").
    /// Listens to <see cref="GameModeProgressionService.OnIntensityUnlocked"/> and also
    /// checks the current state up-front, so an already-met gate passes straight through.
    /// </summary>
    public class QuestWaitForIntensityNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the mode's unlocked intensity reaches the target tier (the progression service's own gate — tier 4 completes a mode's quest). Passes immediately if already met.";
        public override string EditorSummary => $"{mode} ≥ tier {intensityTier}";

        /// <summary>An away trip: the player earns the tier by playing, then lands back in the shell.</summary>
        public override QuestVenue Venue => QuestVenue.Gameplay;
        public override QuestVenue VenueAfter => QuestVenue.AppShell;

        [Tooltip("The game mode whose intensity gate this node waits on.")]
        public GameModes mode = GameModes.MultiplayerCrystalCapture;

        [Tooltip("The intensity tier that satisfies this gate (4 = the tier that completes the mode's quest).")]
        [Range(2, 4)] public int intensityTier = 4;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc == null)
            {
                Debug.LogError("[Quest] WaitForIntensityNode: GameModeProgressionService not alive — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            if (svc.GetMaxUnlockedIntensity(mode) >= intensityTier)
            {
                advance(QuestPorts.Next);
                yield break;
            }

            void OnUnlocked(GameModes unlockedMode, int tier)
            {
                if (unlockedMode == mode && tier >= intensityTier)
                    advance(QuestPorts.Next);
            }

            svc.OnIntensityUnlocked += OnUnlocked;
            ctx.AddCleanup(() => svc.OnIntensityUnlocked -= OnUnlocked);
        }

        /// <summary>
        /// Force-advance applies the REAL milestone: the tier is actually unlocked and the
        /// mode's quest is completed, so the profile quest track offers the CLAIM exactly as
        /// if the player had earned intensity 4 — no Froglet Toolbox needed while testing.
        /// </summary>
        public override void DebugForceSatisfy(QuestRuntimeContext ctx)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc == null)
            {
                Debug.LogWarning("[Quest] Force-satisfy: GameModeProgressionService not alive — intensity not written.");
                return;
            }

            svc.DebugSetMaxIntensity(mode, intensityTier);
            // Tier 4 = the mode's quest milestone: evaluate it so the quest is MARKED completed
            // and the claim button lights up on the quest track (IntensityUnlocked quests
            // evaluate against the max unlocked tier we just wrote).
            svc.ReportQuestStat(mode, 0f);
            Debug.Log($"[Quest] Force-satisfy: {mode} max intensity → {intensityTier}, quest evaluated for completion.");
        }
    }
}
