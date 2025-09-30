using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerAlignPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerAlignPrismEffect")]
    public class SkimmerAlignPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float velocityNudgeStrength = 4f;
        [SerializeField] private float alignSpeedFactor = 15f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmer = impactor.Skimmer;
            var ship    = skimmer.VesselStatus;

            var prism   = prismImpactee.Prism;
            if (prism == null || prism.Trail == null || prism.Trail.TrailList.Count < 5) return;

            // Use prism as "minMatureBlock"
            var minMatureBlock = prism;
            var nextBlocks = prism.Trail.LookAhead(
                minMatureBlock.prismProperties.Index,
                0,
                TrailFollowerDirection.Forward,
                5);

            if (nextBlocks == null || nextBlocks.Count < 5) return;

            // Distances
            float sqrDist = (skimmer.transform.position - minMatureBlock.transform.position).sqrMagnitude;
            float sqrSweetSpot = skimmer.transform.localScale.x * skimmer.transform.localScale.x / 16f;

            var normNextBlockDistance = (nextBlocks[0].transform.position - skimmer.transform.position).normalized;

            // Velocity nudging
            if (sqrDist < sqrSweetSpot - 3f)
                ship.VesselTransformer.ModifyVelocity(-normNextBlockDistance * velocityNudgeStrength, Time.deltaTime * 2f);
            else if (sqrDist > sqrSweetSpot + 3f)
                ship.VesselTransformer.ModifyVelocity(normNextBlockDistance * velocityNudgeStrength, Time.deltaTime * 2f);

            // Tube forward from further ahead
            var tubeForward = nextBlocks[4].transform.forward;

            // Radial
            var fromTube = skimmer.transform.position - nextBlocks[0].transform.position;
            var radial   = Vector3.ProjectOnPlane(fromTube, tubeForward).normalized;

            // Up / forward
            var directionWeight = Vector3.Dot(ship.Transform.transform.forward, minMatureBlock.transform.forward);
            var isInside = Vector3.Dot(normNextBlockDistance, skimmer.transform.up) > 0;
            var targetUp = isInside ? normNextBlockDistance : -normNextBlockDistance;

            Vector3 targetForward = Vector3.Lerp(
                skimmer.transform.forward,
                directionWeight * tubeForward,
                0.5f // blend factor could also be exposed
            );

            float alignSpeed = ship.Speed * Time.deltaTime / alignSpeedFactor;
            ship.VesselTransformer.GentleSpinShip(targetForward, targetUp, alignSpeed);
        }
    }
}