// PORT type-preserving SHELL (V11) — the full Crystal
// (Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs, ~430L MonoBehaviour with
// materials/VFX/coroutines/UniTask explode pipeline) is not yet scheduled in the porting
// sequence, but CellRuntimeDataSO's crystal registry needs the TYPE: everything it touches
// (Id, ownDomain) lives on CellItem, plus MonoBehaviour members (transform, gameObject,
// lifetime bool). Precedent: AudioSystem shell (Deviation #11), BlockDensityGrid's
// MonoBehaviour stand-in for Prism. When the real Crystal ports, replace this file in place.
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class Crystal : CellItem
    {
    }
}
