// Type-preserving SHELL of Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
// (the Deviation #11 AudioSystem precedent): MainMenuController takes the facade by
// [Inject] and calls RecordMenuReady() on menu-ready — the type must exist for that file
// to stay verbatim. The real facade is the single writer to UGS Analytics (consent/age
// gated, ~40 typed events); the full port lands with the services/instrumentation phase.
// PORT Deviation (services phase, UGS Analytics surface — restore when instrumentation ports).
namespace CosmicShore.Core
{
    public class AnalyticsServiceFacade
    {
        bool _menuReadyThisSession;

        public AnalyticsServiceFacade() { }

        /// <summary>
        /// The upstream bootstrap ctor (AppManager's lazy factory passes the full SOAP
        /// wiring surface). The shell accepts and discards the dependencies — the real
        /// facade subscribes its ~40 typed events here when instrumentation ports.
        /// </summary>
        public AnalyticsServiceFacade(
            CosmicShore.ScriptableObjects.AuthenticationDataVariable authenticationDataVariable,
            CosmicShore.ScriptableObjects.NetworkMonitorDataVariable networkMonitorDataVariable,
            CosmicShore.Utility.GameDataSO gameData,
            CosmicShore.ScriptableObjects.ApplicationLifecycleEventsContainerSO lifecycleEvents,
            CosmicShore.ScriptableObjects.ApplicationStateDataVariable applicationStateDataVariable,
            CosmicShore.ScriptableObjects.MenuFreestyleEventsContainerSO menuFreestyleEvents,
            CosmicShore.Utility.FriendsDataSO friendsData,
            CosmicShore.Utility.HostConnectionDataSO hostConnectionData,
            bool allowLog)
        {
        }

        /// <summary>Menu became fully interactive. Call from MainMenuController on ready.</summary>
        public void RecordMenuReady()
        {
            _menuReadyThisSession = true;
        }

        /// <summary>Shell-only observability: whether RecordMenuReady fired this session.</summary>
        public bool MenuReadyThisSession => _menuReadyThisSession;

        /// <summary>
        /// A vessel was purchased/unlocked (real: UGSKeys.EventVesselUnlocked with
        /// vessel/cost/balance). Shell records the last call for test observability.
        /// </summary>
        public void RecordVesselUnlocked(string vessel, int cost, int balance)
            => LastVesselUnlocked = (vessel, cost, balance);

        /// <summary>Shell-only observability: the last RecordVesselUnlocked payload.</summary>
        public (string Vessel, int Cost, int Balance)? LastVesselUnlocked { get; private set; }
    }
}
