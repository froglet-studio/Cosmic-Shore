using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lightweight Maelstrom reveal for the in-scene connecting panel: the mode name + intensity of the
    /// round that's loading (the surprise is revealed HERE, not in the hub), with the current leading
    /// domain shown above. Reads the SYNCED <c>gameData.GameMode</c> / <c>SelectedIntensity</c> (so it
    /// resolves on every peer once the game scene's config has synced) and the persistent
    /// <c>TournamentDataSO</c> standings.
    ///
    /// Pure view: call <see cref="Refresh"/> (also runs on enable) when the connecting panel is shown.
    /// </summary>
    public class TournamentConnectingInfo : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;
        [Tooltip("Palette mapping Domain → colour (Assets/_SO_Assets/DomainColorPalette.asset).")]
        [SerializeField] DomainColorPaletteSO palette;

        [Header("Mode reveal")]
        [SerializeField] TMP_Text modeNameText;
        [SerializeField] TMP_Text intensityText;

        [Header("Lead domain (above the panel)")]
        [SerializeField] TMP_Text leadDomainText;
        [Tooltip("Optional background/swatch image tinted to the leading domain's colour.")]
        [SerializeField] Image leadDomainBackground;

        void OnEnable() => Refresh();

        public void Refresh()
        {
            bool tournament = gameData != null && gameData.IsTournamentMode && tournamentData != null;

            if (modeNameText) modeNameText.text = tournament ? ResolveModeName(gameData.GameMode) : string.Empty;

            if (intensityText)
            {
                int intensity = tournament && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 0;
                intensityText.text = intensity > 0 ? $"INTENSITY {intensity}" : string.Empty;
            }

            var lead = tournament ? LeadingDomain() : Domains.Blue;
            if (leadDomainText)
                leadDomainText.text = lead == Domains.Blue ? string.Empty : $"{lead.ToString().ToUpperInvariant()} LEADING";

            if (leadDomainBackground && palette) leadDomainBackground.color = palette.Get(lead);
        }

        Domains LeadingDomain()
        {
            var sorted = tournamentData.BuildSortedStandings();
            return sorted.Count > 0 ? sorted[0].Domain : Domains.Blue;
        }

        string ResolveModeName(GameModes mode)
        {
            if (tournamentData.GameQueue != null)
            {
                foreach (var card in tournamentData.GameQueue)
                    if (card != null && card.Mode == mode && !string.IsNullOrEmpty(card.DisplayName))
                        return card.DisplayName;
            }
            return mode.ToString();
        }
    }
}
