using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Palisade scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.VolumeRemaining"/>: the prism volume a
    /// domain has STANDING when the clock runs out (the Swordfish's trail and its afterimages).
    ///
    /// <para><b>Tapestry's rule, reused</b>: <see cref="IsObjectiveReached"/> never fires (the time monitor is the
    /// scene's only end), every pilot carries the volume they hold, rows are team-major. Palisade's results lines are
    /// authored templates on its own asset (Tapestry's are the legacy literal shape).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Palisade", fileName = "PalisadeScoringRule")]
    public class PalisadeScoringRuleSO : TapestryScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("Every pilot's score column. {volume} = the volume they hold at the whistle.")]
        [SerializeField] string volumeTemplate = "{volume} Volume";

        [Tooltip("Every pilot's breakdown column. {prisms} = how many of their prisms are standing at the whistle.")]
        [SerializeField] string breakdownTemplate = "{prisms} prisms";

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var placement = ResolvePlacementOrder(gameData);
            var rows = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => { int i = placement.IndexOf(s.Domain); return i < 0 ? int.MaxValue : i; })
                .ThenByDescending(LiveMetric)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .Select(s => new ScoreResultBuilder.Row(
                    s.Name,
                    s.Domain,
                    s.Score,
                    TemplateText.Fill(volumeTemplate, "volume", LiveMetric(s).ToString()),
                    TemplateText.Fill(breakdownTemplate, "prisms", s.PrismsRemaining.ToString())))
                .ToList();
            return ScoreResultBuilder.BuildRanked(rows);
        }
    }
}
