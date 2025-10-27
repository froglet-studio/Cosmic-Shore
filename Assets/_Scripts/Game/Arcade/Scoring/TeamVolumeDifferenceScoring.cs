using System.Linq;
using CosmicShore.Core;
using CosmicShore.SOAP;
using Unity.Services.Matchmaker.Models;
using UnityEngine;


namespace CosmicShore.Game.Arcade.Scoring
{
    public class TeamVolumeDifferenceScoring : BaseScoring
    {
        public TeamVolumeDifferenceScoring(GameDataSO scoreData, float scoreMultiplier) : base(scoreData, scoreMultiplier) { }

        /*public override void CalculateScore()
        {
            var sorted = GameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();
            if (sorted == null || sorted.Count == 0) return;

            // last element (descending list) has the smallest volume
            float minVol = sorted[^1].VolumeRemaining;

            foreach (var ps in GameData.RoundStatsList)
            {
                float rel = Mathf.Max(0f, ps.VolumeRemaining - minVol); // relative to last place
                ps.Score += rel * scoreMultiplier;                      // accumulate like before
            }
        }*/

        public override void Subscribe()
        {
            throw new System.NotImplementedException();
        }

        public override void Unsubscribe()
        {
            throw new System.NotImplementedException();
        }
    }
}
