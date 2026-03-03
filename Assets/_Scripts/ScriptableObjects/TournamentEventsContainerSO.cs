using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// SOAP event container for tournament lifecycle events.
    /// Raised by <see cref="Core.TournamentManager"/> and consumable by any system via
    /// inspector-wired EventListeners or code subscription.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TournamentEvents",
        menuName = "ScriptableObjects/Data Containers/TournamentEvents")]
    public class TournamentEventsContainerSO : ScriptableObject
    {
        [Header("Tournament Lifecycle")]
        [Tooltip("Raised when a tournament session begins. Subscribers should disable menu navigation and prepare tournament UI.")]
        public ScriptableEventNoParam OnTournamentStarted;

        [Tooltip("Raised when a tournament round's scores have been captured and standings updated. Subscribers should display the TournamentStandingsPanel.")]
        public ScriptableEventNoParam OnTournamentRoundCaptured;

        [Tooltip("Raised when advancing to the next round (scene is about to load). Subscribers should show a loading/transition state.")]
        public ScriptableEventNoParam OnTournamentAdvancing;

        [Header("Tournament Completion")]
        [Tooltip("Raised when all rounds are complete and final standings are calculated. Subscribers should show the final results variant of TournamentStandingsPanel.")]
        public ScriptableEventNoParam OnTournamentComplete;

        [Tooltip("Raised when the tournament session ends (cleanup complete, returning to menu). Subscribers should tear down tournament UI and re-enable menu navigation.")]
        public ScriptableEventNoParam OnTournamentEnded;
    }
}
