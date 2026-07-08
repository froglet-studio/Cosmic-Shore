// PORT Deviation — type-preserving SHELL of the gameplay stats aggregator
// (original: Assets/_Scripts/Controller/Managers/StatsManager.cs, 333 lines: the
// per-round team/player stat roll-ups consumed by end-game scorecards). Only the
// type exists so AppManager's RegisterManagerSingleton<StatsManager> binding
// compiles; the full port lands with the scoring arc's stats pass. Precedent:
// AudioSystem shell (Deviation #11).
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class StatsManager : MonoBehaviour
    {
    }
}
