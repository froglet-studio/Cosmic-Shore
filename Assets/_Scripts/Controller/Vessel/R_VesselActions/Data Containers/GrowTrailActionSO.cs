using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "GrowTrailAction", menuName = "ScriptableObjects/Vessel Actions/Grow Trail")]
    public class GrowTrailActionSO : ShipActionSO
    {
        [Header("General")]
        [SerializeField] ElementalFloat maxSize = new(3f);

        /// <summary>MASS -> maximum trail slab size: x1 at the resting level, x1.5 at level 10, floored at x0.25.
        /// Migrated verbatim from the retired ElementalAbilityMapSO generic
        /// multiplier (atFull 1.5, minMultiplier 0.25) — see
        /// Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md. Lives here, on the
        /// asset that owns the parameter, so it can only ever scale this one number.
        /// Never bound (this is a ScriptableObject, and BindElementalFloats reflects only
        /// over ElementalShipComponent MonoBehaviours), so it holds no per-vessel state.
        /// </summary>
        /// <para>NOTE: <c>maxSize</c> above is itself an ElementalFloat authored Enabled with
        /// Min 4 / Max 8 on Mass, but <see cref="MaxSize"/> reads its raw <c>.Value</c> and this
        /// is a ScriptableObject, so nothing ever evaluates it — that 4->8 ramp has never run.
        /// The shipped Mass scaling is this multiplier (4->6). Folding the two is a balance
        /// change, not a refactor; logged in Docs/ElementalAbilitySystem/BACKLOG.md.</para>
        [SerializeField] ElementalFloat massMaxSizeMultiplier =
            ElementalFloat.Multiplier(1f, 1.5f, Element.Mass, 0.25f);
        [SerializeField] float growRate = 1f;
        [SerializeField] ElementalFloat shrinkRate = new(1f);

        [Header("Weights")]
        [SerializeField] float XWeight = 0f;
        [SerializeField] float YWeight = 0f;
        [SerializeField] float ZWeight = 1f;
        [SerializeField] float GapWeight = 0f;

        public float MaxSize => maxSize.Value;

        /// <summary>The live MASS multiplier on the slab's size ceiling.</summary>
        public float MassMaxSizeMultiplier(IVesselStatus status)
            => massMaxSizeMultiplier.EvaluateLive(status);
        public float GrowRate => growRate;
        public float ShrinkRate => shrinkRate.Value;
        public float WX => XWeight; public float WY => YWeight; public float WZ => ZWeight; public float WGap => GapWeight;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowTrailActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowTrailActionExecutor>()?.End();
    }
}
