using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class TimeBasedTurnMonitor : TurnMonitor
    {
        [SerializeField] float duration;
        float elapsedTime;

        public override bool CheckForEndOfTurn() => elapsedTime >= duration;
        
        protected override void StartTurn() => elapsedTime = 0;

        protected override void RestrictedUpdate()
        {
            base.RestrictedUpdate();
            elapsedTime += Time.deltaTime;
            var message = ((int)duration - (int)elapsedTime).ToString();
            onUpdateTurnMonitorDisplay?.Raise(message);
        }
    }
}