using TMPro;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class TimeBasedTurnMonitor : TurnMonitor
    {
        [SerializeField] float duration;
        [HideInInspector] public TMP_Text display;
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

            if (display != null)
                display.text = ((int)duration - (int)elapsedTime).ToString();
        }
    }
}