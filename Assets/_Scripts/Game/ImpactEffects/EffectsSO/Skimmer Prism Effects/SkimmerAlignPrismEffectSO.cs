using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerAlignPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerAlignPrismEffect")]
    public class SkimmerAlignPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Alignment")]
        private float direction;

        [SerializeField, Range(5f, 30f), Tooltip("Higher = slower rotation (old default was 15)")]
        private float alignDivisor = 15f;

        [Header("Position Nudging")]
        [SerializeField, Range(1f, 8f), Tooltip("Nudge force magnitude (old default was 4)")]
        private float nudgeForce = 4f;

        [SerializeField, Range(0.5f, 5f), Tooltip("Margin around sweet spot (old default was 3)")]
        private float sweetSpotMargin = 3f;

        [Header("Look-Ahead")]
        [SerializeField, Range(1, 4)]
        private int backStep = 0;

        //[SerializeField, Range(5, 10)]
        //private int lookAhead = 6;

        [SerializeField, Range(0, 3)]
        private float LookAheadTime = .5f;

        [SerializeField, Range(3, 6), Tooltip("Which block to use for tube forward direction")]
        private int forwardReferenceIndex = 4;

        private const int MinBlocksRequired = 3;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmer = impactor.Skimmer;
            var ship = skimmer.VesselStatus;
            var prism = prismImpactee.Prism;

            if (prism == null || prism.Trail == null) return;

            var nextPrisms = FindNextBlocks(prism, ship.Speed * LookAheadTime);
            //var idx = prism.prismProperties.Index;
            //var nextBlocks = prism.Trail.LookAhead(idx, -backStep, TrailFollowerDirection.Forward, lookAhead);

            if (nextPrisms == null || nextPrisms.Count < MinBlocksRequired) return;

            var xform = skimmer.transform;

            // === Position Nudging (preserve sweet spot behavior) ===
            var toNextBlock = nextPrisms[0].transform.position - xform.position;
            var toNextBlockNorm = toNextBlock.normalized;
            var sqrDist = toNextBlock.sqrMagnitude;

            // Use the skimmer's sweet spot if available, otherwise estimate
            var sqrSweetSpot = impactor.SqrSweetSpot;
            var angularAxis = Vector3.Cross(xform.forward, toNextBlockNorm).normalized;
            var radialAlignment = Vector3.Dot(toNextBlockNorm, xform.up);
            var angularWeight = radialAlignment > 0 ? (1 - Mathf.Abs(radialAlignment)) : -(1 - Mathf.Abs(radialAlignment));
            direction = Vector3.Dot(xform.forward, nextPrisms[0].transform.forward) >= 0 ? 1f : -1f;

            if (sqrDist < sqrSweetSpot - sweetSpotMargin)
            {
                //ship.VesselTransformer.ModifyVelocity(-toNextBlockNorm * nudgeForce, Time.deltaTime * 2f);
                //ship.VesselTransformer.ModifyVelocity(angularAxis * angularWeight * nudgeForce, Time.deltaTime * 2f);
            }
            else if (sqrDist > sqrSweetSpot + sweetSpotMargin)
            {
                //ship.VesselTransformer.ModifyVelocity(toNextBlockNorm * nudgeForce, Time.deltaTime * 2f);
                //ship.VesselTransformer.ModifyVelocity(angularAxis * angularWeight * nudgeForce, Time.deltaTime * 2f);
            }

            // === Alignment (preserve original Lerp behavior) ===
            //var refIndex = Mathf.Min(forwardReferenceIndex, nextPrisms.Count - 1);
            var tubeForward = nextPrisms[nextPrisms.Count - 1].transform.forward;

            // Original used Lerp with directionWeight applied to tubeForward
            var targetForward = Vector3.Lerp(
                xform.forward,
                direction * tubeForward,
                impactor.CombinedWeight
            );

            // Determine inside/outside for up vector
            var targetUp = Vector3.Lerp(
                xform.up,
                radialAlignment > 0 ? toNextBlockNorm : -toNextBlockNorm,
                Mathf.Abs(radialAlignment) * impactor.CombinedWeight
            );

            // Original align speed formula
            var alignSpeed = ship.Speed * Time.deltaTime / alignDivisor;

            ship.VesselTransformer.GentleSpinShip(targetForward, targetUp, alignSpeed);
        }

        List<Prism> FindNextBlocks(Prism minMatureBlock, float distance = 100f)
        {
            if (minMatureBlock.Trail == null) return new List<Prism> { minMatureBlock };
            var minIndex = minMatureBlock.prismProperties.Index;
            List<Prism> nextBlocks;
            if (direction < 0 && minIndex > 0)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Backward, distance);
            else if (direction > 0 && minIndex < minMatureBlock.Trail.TrailList.Count - 1)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, distance);
            else
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, distance);
            return nextBlocks;
        }
    }
}