using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A team's header on the Rev 8 scoreboard (Docs/MENU_REDESIGN_B.md §49): the domain's name and a rule in its colour,
    /// then the team's line ("Won - 00:44.93"). Spawned by <see cref="Scoreboard"/> into the list above the team's rows.
    /// </summary>
    public class ScoreboardTeamHeader : MonoBehaviour
    {
        [SerializeField] TMP_Text domainText;
        [SerializeField] Image rule;
        [SerializeField] TMP_Text summaryText;

        public void Bind(string domainLabel, Color domainColor, string summary)
        {
            if (domainText)
            {
                domainText.text = domainLabel;
                domainText.color = domainColor;
            }
            if (rule) rule.color = domainColor;
            if (summaryText) summaryText.text = summary;
        }
    }
}
