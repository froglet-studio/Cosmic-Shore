using System;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One completed round in the Maelstrom history scroll: round number, the mode that was played
    /// (revealed after the fact), its intensity, and the per-domain placement that round with the
    /// winning domain's colour applied to the assigned targets.
    ///
    /// Pure view: <see cref="Setup"/> binds a <see cref="TournamentRoundRecord"/>; the caller passes a
    /// domain→colour resolver so the card stays decoupled from the theme system.
    /// </summary>
    public class TournamentRoundCard : MonoBehaviour
    {
        [SerializeField] TMP_Text roundLabelText;     // "ROUND 3"
        [SerializeField] TMP_Text modeNameText;       // "Joust"
        [SerializeField] TMP_Text intensityText;      // "Intensity 2"

        [Tooltip("Per-domain placement for the round, e.g. \"1st JADE   2nd RUBY   3rd GOLD\".")]
        [SerializeField] TMP_Text placementsText;

        [Tooltip("Graphics tinted to the winning domain's colour (accent bar, background, …).")]
        [SerializeField] Graphic[] winnerColorTargets;

        [Tooltip("Optional marker for the most-recently-played round (the auto-scroll target).")]
        [SerializeField] GameObject currentRoundHighlight;

        public void Setup(TournamentRoundRecord record, Func<Domains, Color> colorOf, bool isCurrent = false)
        {
            if (record == null) return;

            if (roundLabelText) roundLabelText.text = $"ROUND {record.RoundNumber}";
            if (modeNameText) modeNameText.text = record.ModeDisplayName ?? string.Empty;
            if (intensityText) intensityText.text = record.Intensity > 0 ? $"Intensity {record.Intensity}" : string.Empty;
            if (placementsText) placementsText.text = BuildPlacements(record);
            if (currentRoundHighlight) currentRoundHighlight.SetActive(isCurrent);

            if (winnerColorTargets != null && colorOf != null)
            {
                var c = colorOf(record.WinningDomain);
                foreach (var g in winnerColorTargets)
                    if (g) g.color = c;
            }
        }

        static string BuildPlacements(TournamentRoundRecord r)
        {
            if (r.DomainOrder == null) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < r.DomainOrder.Count; i++)
            {
                if (i > 0) sb.Append("   ");
                sb.Append(Ordinal(i + 1)).Append(' ').Append(r.DomainOrder[i].ToString().ToUpperInvariant());
            }
            return sb.ToString();
        }

        static string Ordinal(int n) => n switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{n}th",
        };
    }
}
