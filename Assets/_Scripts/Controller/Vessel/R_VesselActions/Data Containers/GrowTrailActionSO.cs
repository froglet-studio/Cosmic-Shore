using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "GrowTrailAction", menuName = "ScriptableObjects/Vessel Actions/Grow Trail")]
    public class GrowTrailActionSO : ShipActionSO
    {
        /// <summary>The trail slab's authored size ceiling. A plain float, and the TYPE is the
        /// statement: MASS reaches this ceiling through <see cref="massMaxSizeMultiplier"/>, and
        /// nothing scales this number.
        /// <para>It was an <c>ElementalFloat</c> authored Enabled with a 4 -> 8 Mass ramp that had
        /// never run once — an ElementalFloat on a ScriptableObject can only scale through
        /// <c>EvaluateLive</c>, and this was read as <c>.Value</c>. That was answered first by
        /// authoring <c>Enabled: 0</c> (honest data, misleading type) and then by taking the type
        /// away, which is the half no gate can express. Turning a real ramp on here is still a
        /// BALANCE change — it would stack with the multiplier — and would mean declaring a new
        /// ElementalFloat deliberately, not flipping a bool; logged in
        /// Docs/ElementalAbilitySystem/BACKLOG.md.</para></summary>
        [Header("General")]
        [SerializeField] float maxSize = 4f;

        /// <summary>MASS -> maximum trail slab size: x1 at the resting level, x1.5 at level 10, floored at x0.25.
        /// Migrated verbatim from the retired ElementalAbilityMapSO generic
        /// multiplier (atFull 1.5, minMultiplier 0.25) — see
        /// Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md. Lives here, on the
        /// asset that owns the parameter, so it can only ever scale this one number.
        /// Never bound (this is a ScriptableObject, and BindElementalFloats reflects only
        /// over ElementalShipComponent MonoBehaviours), so it holds no per-vessel state.
        /// </summary>
        /// <para>This is the ONE live Mass channel on the slab ceiling; see the note on
        /// <c>maxSize</c> for the dead ramp it replaced.</para>
        [SerializeField] ElementalFloat massMaxSizeMultiplier =
            ElementalFloat.Multiplier(1f, 1.5f, Element.Mass, 0.25f);
        [SerializeField] float growRate = 1f;
        [SerializeField] float shrinkRate = 1f;

        [Header("Weights")]
        [SerializeField] float XWeight = 0f;
        [SerializeField] float YWeight = 0f;
        [SerializeField] float ZWeight = 1f;
        [SerializeField] float GapWeight = 0f;

        public float MaxSize => maxSize;

        /// <summary>The live MASS multiplier on the slab's size ceiling.</summary>
        public float MassMaxSizeMultiplier(IVesselStatus status)
            => massMaxSizeMultiplier.EvaluateLive(status);
        public float GrowRate => growRate;
        public float ShrinkRate => shrinkRate;
        public float WX => XWeight; public float WY => YWeight; public float WZ => ZWeight; public float WGap => GapWeight;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowTrailActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrowTrailActionExecutor>()?.End();
    }
}
