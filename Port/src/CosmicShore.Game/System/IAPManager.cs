// PORT Deviation — type-preserving SHELL of the in-app purchase manager
// (original: Assets/_Scripts/System/IAPManager.cs, 159 lines: UGS Purchasing
// integration — services-phase concerns). Only the type exists so AppManager's
// RegisterManagerSingleton<IAPManager> binding compiles; the real port arrives with
// the UGS services phase. Precedent: AudioSystem shell (Deviation #11).
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    public class IAPManager : MonoBehaviour
    {
    }
}
