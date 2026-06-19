using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One domain's standing row for the summary rank panel — place, domain name, points. The
    /// background image tints to the domain via the <see cref="DomainColorPaletteSO"/>.
    /// </summary>
    public class TournamentDomainScoreView : MonoBehaviour
    {
        [Tooltip("Optional place number (e.g. \"1\").")]
        [SerializeField] TMP_Text placeText;
        [SerializeField] TMP_Text domainNameText;
        [SerializeField] TMP_Text pointsText;

        [Header("Domain colour")]
        [Tooltip("Background image tinted to the domain colour.")]
        [SerializeField] Image domainBackground;
        [SerializeField] DomainColorPaletteSO palette;

        [Tooltip("Optional \"(You)\" marker shown when this is the local player's domain.")]
        [SerializeField] GameObject youBadge;

        public void Setup(Domains domain, int points, int place, bool isLocal)
        {
            if (placeText) placeText.text = place > 0 ? place.ToString() : string.Empty;
            if (domainNameText) domainNameText.text = domain.ToString().ToUpperInvariant();
            if (pointsText) pointsText.text = points.ToString();
            if (youBadge) youBadge.SetActive(isLocal);

            if (domainBackground && palette) domainBackground.color = palette.Get(domain);
        }
    }
}
