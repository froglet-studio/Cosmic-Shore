using System.Globalization;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Wildlife Blitz specific HUD
    /// Shows remaining score during gameplay
    /// Clears display when round ends
    /// </summary>
    public class WildlifeBlitzHUD : MiniGameHUD
    {
        [Header("Blitz Settings")]
        [SerializeField] private int targetScoreToWin = 500;

        [Header("Blitz Events")]
        [SerializeField] private ScriptableEventInt onSetScoreTargetEvent;
        [SerializeField] private ScriptableEventNoParam onScoreChanged;
        [SerializeField] private ScriptableEventString onLifeFormCounterUpdatedEvent;

        protected override void SubscribeToEvents()
        {
            base.SubscribeToEvents();
            
            if (onSetScoreTargetEvent)
                onSetScoreTargetEvent.OnRaised += SetTargetScore;
            
            if (onScoreChanged)
                onScoreChanged.OnRaised += UpdateScoreUI;
            
            if (onLifeFormCounterUpdatedEvent)
                onLifeFormCounterUpdatedEvent.OnRaised += UpdateLifeFormCounter;
        }

        protected override void UnsubscribeFromEvents()
        {
            base.UnsubscribeFromEvents();
            
            if (onSetScoreTargetEvent)
                onSetScoreTargetEvent.OnRaised -= SetTargetScore;
            
            if (onScoreChanged)
                onScoreChanged.OnRaised -= UpdateScoreUI;
            
            if (onLifeFormCounterUpdatedEvent)
                onLifeFormCounterUpdatedEvent.OnRaised -= UpdateLifeFormCounter;
        }

        private void SetTargetScore(int newTarget)
        {
            targetScoreToWin = newTarget;
            
            // ISSUE 1 FIX: Show target value at start
            Debug.Log($"<color=cyan>[WildlifeBlitzHUD] Target set to {targetScoreToWin}</color>");
            UpdateScoreUI();
        }

        protected override void UpdateScoreUI()
        {
            // During gameplay: show remaining score
            if (localRoundStats == null)
            {
                // Before game starts: show target
                view.UpdateScoreUI(targetScoreToWin.ToString());
                return;
            }

            int currentScore = (int)localRoundStats.Score;
            int remaining = Mathf.Max(0, targetScoreToWin - currentScore);
            
            view.UpdateScoreUI(remaining.ToString());
        }
        
        /// <summary>
        /// ISSUE 4 FIX: Handle lifeform counter updates
        /// </summary>
        private void UpdateLifeFormCounter(string count)
        {
            UpdateLifeformCounterDisplay(count);
        }
        
        /// <summary>
        /// Override turn end to clear displays
        /// </summary>
        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            
            // ISSUE 1 FIX: Clear score display at end
            Debug.Log("<color=yellow>[WildlifeBlitzHUD] Round ended - clearing displays</color>");
            view.UpdateScoreUI("");
            UpdateLifeformCounterDisplay("");
        }
    }
}