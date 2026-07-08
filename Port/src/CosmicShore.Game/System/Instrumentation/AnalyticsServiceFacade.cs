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

        /// <summary>Menu became fully interactive. Call from MainMenuController on ready.</summary>
        public void RecordMenuReady()
        {
            _menuReadyThisSession = true;
        }

        /// <summary>Shell-only observability: whether RecordMenuReady fired this session.</summary>
        public bool MenuReadyThisSession => _menuReadyThisSession;
    }
}
