using UnityEngine;
using System.Linq;

namespace CosmicShore.Game.Arcade
{
    public class VolumeCreatedTurnMonitor : TurnMonitor
    {
        [SerializeField] float amount;
        
        // Use MiniGameData for player infos.
        // [SerializeField] MiniGame Game;
        
        // [SerializeField] bool hostileVolume;
        // Core.IRoundStats volumeStat;
        Domains domain;
        float volumeUnitConverstion = 145.65f;

        /*private void Start()
        {
            StartCoroutine(WaitForTeam());
        }*/

        /*private IEnumerator WaitForTeam()
        {
            team = hostileVolume ? Teams.Ruby : Teams.Jade;

            while (!StatsManager.Instance.TeamStats.ContainsKey(team))
            {
                yield return null; // Wait for the next frame
            }

            volumeStat = StatsManager.Instance.TeamStats[team];
            // Any additional logic once the red team is available
        }*/

        public override bool CheckForEndOfTurn()
        {
            return miniGameData.RoundStatsList.Any(roundStats => roundStats.VolumeCreated > amount);
            // return StatsManager.Instance.TeamStats[team].VolumeCreated > Amount;
        }
        
        /*protected override void StartTurn()
        {
            // StatsManager.Instance.ResetStats();
            StartCoroutine(WaitForTeam());
        }*/

        protected override void RestrictedUpdate()
        {
            /*if (StatsManager.Instance.TeamStats.ContainsKey(team))
            {
                string message = ((int)((Amount - StatsManager.Instance.TeamStats[team].VolumeCreated) / volumeUnitConverstion)).ToString();
                onUpdateTurnMonitorDisplay.Raise(message);
            }*/

            string message = ((int)((amount - miniGameData.RoundStatsList[0].VolumeCreated) / volumeUnitConverstion)).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
