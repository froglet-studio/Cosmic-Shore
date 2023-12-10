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

        void Update()
        {
            if (paused) return;

            elapsedTime += Time.deltaTime;

            if (Display != null)
                Display.text = ((int)duration - (int)elapsedTime).ToString();
        }
    }
}