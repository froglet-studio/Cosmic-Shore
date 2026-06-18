using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One domain's standing row, tinted to the domain colour. Reused for BOTH the Maelstrom
    /// lobby/hub team-score side panels and the end-of-tournament rank rows — same data
    /// (domain + points + place), different placement in the hierarchy.
    ///
    /// Pure view: <see cref="Setup"/> fills the text and tints every assigned colour target.
    /// </summary>
    public class TournamentDomainScoreView : MonoBehaviour
    {
        [Tooltip("Optional place number / ordinal (e.g. \"1\"). Leave unassigned to hide.")]
        [SerializeField] TMP_Text placeText;
        [SerializeField] TMP_Text domainNameText;
        [SerializeField] TMP_Text pointsText;

        [Tooltip("Graphics tinted to the domain colour (background image, swatch, name text, …).")]
        [SerializeField] Graphic[] colorTargets;

        [Tooltip("Optional \"(You)\" marker shown when this is the local player's domain.")]
        [SerializeField] GameObject youBadge;

        public void Setup(Domains domain, Color color, int points, int place, bool isLocal)
        {
            if (placeText) placeText.text = place > 0 ? place.ToString() : string.Empty;
            if (domainNameText) domainNameText.text = domain.ToString().ToUpperInvariant();
            if (pointsText) pointsText.text = points.ToString();
            if (youBadge) youBadge.SetActive(isLocal);

            if (colorTargets != null)
                foreach (var g in colorTargets)
                    if (g) g.color = color;
        }
    }
}
