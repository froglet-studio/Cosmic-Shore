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

        public override void CalculateScore()
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
        }

        public override void Subscribe()
        {
            throw new System.NotImplementedException();
        }

        public override void Unsubscribe()
        {
            throw new System.NotImplementedException();
        }

        /*public override void CalculateScore()
        {
            float Vol(Teams t) => miniGameData.TryGetRoundStats(t, out var s) ? s.VolumeRemaining : 0f;

            var teamVolumes = new[]
            {
                (Team: Teams.Jade, Volume: Vol(Teams.Jade)),
                (Team: Teams.Ruby, Volume: Vol(Teams.Ruby)),
                (Team: Teams.Gold, Volume: Vol(Teams.Gold)),
                (Team: Teams.Blue, Volume: Vol(Teams.Blue)),
            };

            var sorted = teamVolumes
                .OrderByDescending(tv => tv.Volume)
                .ToArray();

            var maxVol = sorted.First().Volume;
            var minVol = sorted.Last().Volume;
            
            var volumeByTeam = teamVolumes.ToDictionary(t => t.Team, t => t.Volume);

            foreach (var ps in miniGameData.RoundStatsList)
            {
                if (!volumeByTeam.TryGetValue(ps.Team, out var v))
                {
                    ps.Score = 0f;
                    continue;
                }

                var diff = v - minVol;          // relative to last place
                var rel  = Mathf.Max(0f, diff); // clamp negatives to 0
                ps.Score += rel * scoreMultiplier;
            }
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            var teamStats = StatsManager.Instance.TeamStats;
            var greenVolume = teamStats.TryGetValue(Teams.Jade, out var greenStats) ? greenStats.VolumeRemaining : 0f;
            var redVolume = teamStats.TryGetValue(Teams.Ruby, out var redStats) ? redStats.VolumeRemaining : 0f;
            var goldVolume = teamStats.TryGetValue(Teams.Gold, out var goldStats) ? goldStats.VolumeRemaining : 0f;
            var difference = redVolume > goldVolume ? greenVolume - redVolume : greenVolume - goldVolume;

            return (difference) * ScoreMultiplier;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            // var score = CalculateScore(playerName, currentScore, turnStartTime);
            var score = CalculateScore(playerName, currentScore);
            StatsManager.Instance.ResetStats();
            return score;
        }*/
        
    }
}
