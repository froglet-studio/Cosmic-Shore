using System.Linq;
using CosmicShore.Core;
using CosmicShore.SOAP;
using Unity.Services.Matchmaker.Models;
using UnityEngine;


namespace CosmicShore.Game.Arcade.Scoring
{
    public class TeamVolumeDifferenceScoring : BaseScoring
    {
        public TeamVolumeDifferenceScoring(MiniGameDataSO scoreData, float scoreMultiplier) : base(scoreData, scoreMultiplier) { }

        public override void CalculateScore()
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

            // DEBUG: show volumes, sorted order, and min/max
            /*Debug.Log($"[ScoreDebug] Volumes: " +
                      string.Join(", ", teamVolumes.Select(tv => $"{tv.Team}={tv.Volume:F2}")));
            Debug.Log($"[ScoreDebug] Sorted (desc): " +
                      string.Join(" > ", sorted.Select(tv => $"{tv.Team}({tv.Volume:F2})")));
            Debug.Log($"[ScoreDebug] min={minVol:F2}, max={maxVol:F2}, range={maxVol - minVol:F2}, multiplier={ScoreMultiplier}");*/

            // For quick lookup
            var volumeByTeam = teamVolumes.ToDictionary(t => t.Team, t => t.Volume);

            foreach (var ps in miniGameData.RoundStatsList)
            {
                if (!volumeByTeam.TryGetValue(ps.Team, out var v))
                {
                    ps.Score = 0f;
                    // Debug.Log($"[ScoreDebug] {ps.Name} ({ps.Team}) missing volume → Score=0");
                    continue;
                }

                var diff = v - minVol;          // relative to last place
                var rel  = Mathf.Max(0f, diff); // clamp negatives to 0
                ps.Score += rel * scoreMultiplier;

                // Debug.Log($"[ScoreDebug] {ps.Name} ({ps.Team}): v={v:F2}, diff={diff:F2}, rel={rel:F2} → Score={ps.Score:F2}");
            }
        }

        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
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
