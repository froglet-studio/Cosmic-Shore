// PORT type-preserving SHELL (V11, extended V19) — the full Crystal
// (Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs, ~430L MonoBehaviour with
// materials/VFX/coroutines/UniTask explode pipeline) is not yet scheduled in the porting
// sequence, but CellRuntimeDataSO's crystal registry needs the TYPE: everything it touches
// (Id, ownDomain) lives on CellItem, plus MonoBehaviour members (transform, gameObject,
// lifetime bool). Precedent: AudioSystem shell (Deviation #11), BlockDensityGrid's
// MonoBehaviour stand-in for Prism. When the real Crystal ports, replace this file in place.
//
// V19 additions (impact-matrix slice): crystalProperties (read by
// CrystalImpactData.FromCrystal) and Vacuum (called by SkimmerImpactor.OnTriggerStay) —
// both verbatim from the original file.
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class Crystal : CellItem
    {
        public CrystalProperties crystalProperties;

        public void Vacuum(Vector3 newPosition, float vaccumAmount)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                newPosition,
                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
        }
    }
}
