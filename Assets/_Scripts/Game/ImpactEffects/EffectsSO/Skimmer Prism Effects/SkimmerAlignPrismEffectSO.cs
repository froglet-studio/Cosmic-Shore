using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerAlignPrismEffect_Minimal",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerAlignPrismEffect")]
    public class SkimmerAlignPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Simple Controls")]
        [SerializeField, Range(0.5f, 2.0f)] private float lockGain   = 2f;  // stronger = locks easier
        [SerializeField, Range(6f, 18f)]    private float turnSpeed  = 12f;    // lower = turns faster
        [SerializeField, Range(2f, 10f)]    private float stickiness = 10f;     // higher = holds ring tighter

        const int   KLookAhead     = 6;
        const int   KBackStep      = 2;
        const int   KAnchorIndex   = 3;
        const float KFacingBiasMin = 0.3f;          
        const float KMinAngular    = 0.004f;       
        const float KMargin        = 2f;            

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmer = impactor.Skimmer;
            var ship    = skimmer.VesselStatus;
            var prism   = prismImpactee.Prism;
            if (prism == null || prism.Trail == null) return;

            var idx        = prism.prismProperties.Index;
            var nextBlocks = prism.Trail.LookAhead(idx, -KBackStep, TrailFollowerDirection.Forward, KLookAhead);
            if (nextBlocks == null || nextBlocks.Count == 0) return;

            var anchor       = Mathf.Min(KAnchorIndex, nextBlocks.Count - 1);
            var anchorBlock  = nextBlocks[anchor];
            var toFirst      = nextBlocks[0].transform.position - skimmer.transform.position;
            var toFirstN     = toFirst.normalized;

            var scale     = skimmer.transform.localScale.x;
            var sweet     = Mathf.Max(0.01f, scale * 0.25f);
            var sqrSweet  = sweet * sweet;
            var sqrDist   = (skimmer.transform.position - anchorBlock.transform.position).sqrMagnitude;

            var nudge = stickiness; 
            if (sqrDist < sqrSweet - KMargin)
                ship.VesselTransformer.ModifyVelocity(-toFirstN * nudge, Time.deltaTime * 2f);
            else if (sqrDist > sqrSweet + KMargin)
                ship.VesselTransformer.ModifyVelocity( toFirstN * nudge, Time.deltaTime * 2f);

            var tubeForward = anchorBlock.transform.forward;
            var facing    = Mathf.Clamp01(Vector3.Dot(ship.Transform.forward, tubeForward));
            facing          = Mathf.Max(facing, KFacingBiasMin);

            var w = Mathf.Clamp01(impactor.CombinedWeight * lockGain) * facing;

            var targetForward = Vector3.Slerp(
                skimmer.transform.forward,
                tubeForward,
                w
            );

            var isInside = Vector3.Dot(toFirstN, skimmer.transform.up) > 0f;
            var  targetUp = isInside ? toFirstN : -toFirstN;

            var ang = Mathf.Max(KMinAngular, (ship.Speed / Mathf.Max(0.001f, turnSpeed)) * Time.deltaTime);
            ship.VesselTransformer.GentleSpinShip(targetForward, targetUp, ang);
        }
    }
}
