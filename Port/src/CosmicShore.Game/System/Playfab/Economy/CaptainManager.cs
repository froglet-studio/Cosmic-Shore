// PORT Deviation — type-preserving SHELL of the captain economy manager
// (original: Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs, 202 lines:
// captain unlock/upgrade state over the legacy PlayFab catalog + CaptainManager
// UI feeds — meta-economy phase concerns). Only the type exists so AppManager's
// RegisterManagerSingleton<CaptainManager> binding compiles; the real port arrives
// with the meta-economy phase. Precedent: AudioSystem shell (Deviation #11).
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CaptainManager : SingletonPersistent<CaptainManager>
    {
    }
}
