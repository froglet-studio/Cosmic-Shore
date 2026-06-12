// PORT type-preserving SHELL (V11, extended V19 + CT1) — the full Crystal
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
//
// CT1 additions (contact arc — crystal impactor slice): the lifecycle surface the
// CrystalImpactor family reads/drives — IsExploding, ExplodeParams,
// NotifyManagerToExplodeCrystal, Respawn (+ allowRespawnOnImpact), DestroyCrystal
// (+ cellData) — verbatim from the original file except where a CrystalManager call is
// staged (markers below).
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class Crystal : CellItem
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        public CrystalProperties crystalProperties;

        [SerializeField] bool allowRespawnOnImpact;

        // Set true for the 0.5s explode-impact window by the (still unported) Explode
        // pipeline; the property itself is verbatim and is gated on by the crystal
        // impactors, so it lives on the shell.
        public bool IsExploding { get; private set; }

        public struct ExplodeParams
        {
            public Vector3 Course;
            public float Speed;
            public FixedString64Bytes PlayerName;
        }

        // PORT Deviation (CT1, restore when CrystalManager ports — the networked
        // explode/respawn manager that owns spent-crystal VFX and pooling is unported):
        // public void NotifyManagerToExplodeCrystal(ExplodeParams explodeParams) =>
        //     CrystalManager.ExplodeCrystal(Id, explodeParams);
        public void NotifyManagerToExplodeCrystal(ExplodeParams explodeParams) { }

        public void Respawn()
        {
            if (!allowRespawnOnImpact)
            {
                DestroyCrystal();
                return;
            }

            // PORT Deviation (CT1, restore when CrystalManager ports — respawn placement
            // is manager-side): CrystalManager.RespawnCrystal(Id);
        }

        public void DestroyCrystal()
        {
            cellData.TryRemoveItem(this);
            Destroy(gameObject);
        }

        public void Vacuum(Vector3 newPosition, float vaccumAmount)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                newPosition,
                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
        }
    }
}
