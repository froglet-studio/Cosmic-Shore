// STUB (engine module): UnityEngine.UnityConsentModule is a Unity 6.x engine module that the 6000.0.75
// reference set only type-forwards to (the module DLL itself is not in it). Members below are exactly
// the ones com.unity.services.analytics 6.2.1 uses; nothing in Assets touches this namespace.
// refs: (engine)
namespace UnityEngine.UnityConsent
{
    public enum ConsentStatus { Unspecified = 0, Granted = 1, Denied = 2 }

    public struct ConsentState
    {
        public ConsentStatus AdsIntent;
        public ConsentStatus AnalyticsIntent;
    }

    public static class EndUserConsent
    {
        public static event System.Action<ConsentState> consentStateChanged;
        public static ConsentState GetConsentState() => default;
        public static void SetConsentState(ConsentState state) => consentStateChanged?.Invoke(state);
    }
}
