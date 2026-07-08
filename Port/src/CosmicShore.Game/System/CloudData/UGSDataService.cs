// PORT Deviation #14 — type-preserving SHELL of the UGS CloudSave data service
// (original: Assets/_Scripts/System/CloudData/UGSDataService.cs, 245 lines:
// MonoBehaviour implementing IUGSDataService over Unity.Services.CloudSave repos —
// services-phase concerns; the IUGSDataService interface stays unported with it).
// Only the type exists so AppManager's RegisterManagerSingleton<UGSDataService>
// binding compiles; restoring it un-carries the PlayerDataService / GameSetting
// deviation-#14 inject sites. Precedent: AudioSystem shell (Deviation #11).
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    public class UGSDataService : MonoBehaviour
    {
    }
}
