// PORT type-preserving SHELL (V11, extended V19 + CT1 + rung 3) — the full Crystal
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
// (+ cellData) — verbatim from the original file.
//
// Rung-3 additions (CrystalManager port): the staged CT1 manager calls are RESTORED
// verbatim (NotifyManagerToExplodeCrystal, Respawn → CrystalManager), plus the surface
// the manager family drives — CrystalManager property + InjectDependencies,
// CanBeCollected, SphereRadius, MoveToNewPos, DeactivateModels, ChangeDomain (+
// DecayingTheftCoroutine), Explode (+ WaitForImpact). Render-side bodies (crystalModels
// material lerps, FadeIn, spent-crystal VFX, explosion audio) are stripped with markers.
using System.Collections;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.Tasks;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class Crystal : CellItem
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        public CrystalProperties crystalProperties;

        [SerializeField] float sphereRadius = 100;
        public float SphereRadius => sphereRadius;

        [SerializeField] bool allowRespawnOnImpact;

        public CrystalManager CrystalManager { get; protected set; }

        // Set true for the 0.5s explode-impact window by Explode below; gated on by the
        // crystal impactors.
        public bool IsExploding { get; private set; }

        public void InjectDependencies(CrystalManager cm) => CrystalManager = cm;

        public bool CanBeCollected(Domains shipDomain) => ownDomain == Domains.Blue || ownDomain == shipDomain;

        public struct ExplodeParams
        {
            public Vector3 Course;
            public float Speed;
            public FixedString64Bytes PlayerName;
        }

        public void NotifyManagerToExplodeCrystal(ExplodeParams explodeParams) =>
            CrystalManager.ExplodeCrystal(Id, explodeParams);

        public void Respawn()
        {
            if (!allowRespawnOnImpact)
            {
                DestroyCrystal();
                return;
            }

            CrystalManager.RespawnCrystal(Id);
        }

        public void DestroyCrystal()
        {
            cellData.TryRemoveItem(this);
            Destroy(gameObject);
        }

        public void DeactivateModels()
        {
            // PORT Deviation (rung 3, restore with the full Crystal port — the body
            // re-activates each crystalModels entry and starts its FadeIn, both
            // render-side; the headless shell has no models):
            // foreach (var model in crystalModels)
            // {
            //     model.model.SetActive(true);
            //     model.model.GetComponent<FadeIn>().StartFadeIn();
            // }
        }

        public void ActivateCrystal()
        {
            transform.parent = cellData.Cell.transform;
            gameObject.GetComponent<SphereCollider>().enabled = true;
            enabled = true;

            // PORT Deviation (rung 3, restore with the full Crystal port — the body
            // swaps each crystalModels entry to its inactiveMaterial and lerps back to
            // the defaultMaterial; render-side, the headless shell has no models):
            // for (int i = 0; i < crystalModels.Count; i++)
            // {
            //     var modelData = crystalModels[i];
            //     var model = modelData.model;
            //
            //     model.GetComponent<Renderer>().material = modelData.inactiveMaterial;
            //     StartCoroutine(LerpCrystalMaterialCoroutine(model, modelData.defaultMaterial));
            // }
        }

        public void MoveToNewPos(Vector3 newPos)
        {
            transform.SetPositionAndRotation(newPos, Quaternion.identity);
        }

        public void Vacuum(Vector3 newPosition, float vaccumAmount)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                newPosition,
                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
        }

        public void Explode(ExplodeParams explodeParams)
        {
            if (IsExploding)
                return;

            IsExploding = true;
            WaitForImpact().Forget();

            // PORT Deviation (rung 3, restore with the full Crystal port — the body
            // instantiates a SpentCrystalPrefab per crystal model with the exploding
            // material + Impact handoff, then plays the CrystalCollect SFX; all
            // render/audio-side. The IsExploding gate + 0.5s reset above are verbatim.)
        }

        public void ChangeDomain(Domains newDomain, float duration = -1)
        {
            if (ownDomain == newDomain)
                return;
            if (newDomain == Domains.Blue)
            {
                // PORT Deviation (rung 3): crystalModels material lerp back to the
                // default material is render-side — stripped in the headless shell.
                ownDomain = newDomain;
                return;
            }
            ownDomain = newDomain;
            // PORT Deviation (rung 3): crystalModels material lerp to the team crystal
            // material (_themeManagerData.GetTeamCrystalMaterial) is render-side —
            // stripped in the headless shell.
            if (duration != -1) StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ChangeDomain(Domains.Blue);
        }

        /// <summary>
        /// This is to forbid multiple impacts due to multiple vessel colliders
        /// </summary>
        async Task WaitForImpact()
        {
            await GameTask.Delay(0.5f);
            IsExploding = false;
        }
    }
}
