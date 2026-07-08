// PORT Deviation — type-preserving SHELL of the UGS Leaderboards writer
// (original: Assets/_Scripts/UI/UGSStatsManager.cs, 254 lines: per-mode leaderboard
// submission via Unity.Services.Leaderboards, all services-phase concerns). Only the
// type exists so AppManager's RegisterManagerSingleton<UGSStatsManager> binding
// compiles; the real port arrives with the UGS services phase.
// Precedent: AudioSystem shell (Deviation #11) / CameraManager shell (#12).
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    public class UGSStatsManager : MonoBehaviour
    {
    }
}
