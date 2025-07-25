using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class TimeBasedTurnMonitor : TurnMonitor
    {
        [SerializeField] float duration;
        float elapsedTime;

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;
            return elapsedTime > duration;
        }

        public override void NewTurn(string playerName)
        {
            elapsedTime = 0;
        }

        protected override void RestrictedUpdate()
        {
            if (paused) return;

            elapsedTime += Time.deltaTime;
            var message = ((int)duration - (int)elapsedTime).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}