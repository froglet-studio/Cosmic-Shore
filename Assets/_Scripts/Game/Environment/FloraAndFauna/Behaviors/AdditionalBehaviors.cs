using UnityEngine;
using System.Collections;

namespace CosmicShore
{
    /// <summary>
    /// Demonstrates an "ambush predator" style behavior,
    /// waiting near a point for unsuspecting targets, then 
    /// quickly rushing them for a short burst.
    /// </summary>
    public class AmbushPredatorBehavior : FaunaBehavior
    {
        [SerializeField] float ambushWaitTime = 5f;
        [SerializeField] float rushSpeed = 20f;
        [SerializeField] float rushDuration = 2f;

        private bool isAmbushing = false;

        public override bool CanPerform(Fauna fauna)
        {
            return !isAmbushing;
        }

        public override IEnumerator Perform(Fauna fauna)
        {
            isAmbushing = true;

            // Step 1: Hide or remain still for a few seconds
            yield return new WaitForSeconds(ambushWaitTime);

            // Step 2: Rush forward
            float timer = 0f;
            Vector3 direction = fauna.transform.forward;
            while (timer < rushDuration)
            {
                fauna.transform.position += direction * rushSpeed * Time.deltaTime;
                timer += Time.deltaTime;
                yield return null;
            }
            yield return null;
        }

        public override void OnBehaviorEnd(Fauna fauna)
        {
            isAmbushing = false;
        }
    }

    /// <summary>
    /// A short-distance teleport or 'blink' style behavior.
    /// Useful for surprising movement or passing obstacles.
    /// </summary>
    public class BlinkTeleportBehavior : FaunaBehavior
    {
        [SerializeField] float blinkDistance = 10f;
        [SerializeField] float blinkCooldown = 3f;

        private bool onCooldown = false;

        public override bool CanPerform(Fauna fauna)
        {
            return !onCooldown;
        }

        public override IEnumerator Perform(Fauna fauna)
        {
            onCooldown = true;

            // Teleport in the facing direction
            Vector3 forward = fauna.transform.forward;
            fauna.transform.position += forward * blinkDistance;

            // Maybe spawn a VFX or sound effect
            // e.g.:
            // Instantiate(teleportEffect, fauna.transform.position, fauna.transform.rotation);

            yield return new WaitForSeconds(blinkCooldown);
            onCooldown = false;
        }
    }

    /// <summary>
    /// A simple 'Gather Resource' style behavior
    /// that looks for the nearest block of a certain type
    /// and tries to pull it in or attach it.
    /// </summary>
    public class GatherResourceBehavior : FaunaBehavior
    {
        [SerializeField] float scanRadius = 20f;
        [SerializeField] float pullSpeed = 3f;
        [SerializeField] LayerMask resourceLayers;

        private bool isGathering = false;

        public override bool CanPerform(Fauna fauna)
        {
            return !isGathering;
        }

        public override IEnumerator Perform(Fauna fauna)
        {
            isGathering = true;

            Collider[] resources = Physics.OverlapSphere(fauna.transform.position, scanRadius, resourceLayers);
            if (resources.Length > 0)
            {
                // Just pick the first or find the closest
                Collider closest = resources[0];
                float minDist = (closest.transform.position - fauna.transform.position).sqrMagnitude;
                foreach (var r in resources)
                {
                    float dist = (r.transform.position - fauna.transform.position).sqrMagnitude;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = r;
                    }
                }

                // Pull it in or move to it
                float time = 0f;
                float maxTime = 5f; // fail-safe if it can't be gathered quickly
                Vector3 pullDir = (fauna.transform.position - closest.transform.position).normalized;
                while (time < maxTime && closest != null)
                {
                    // Move resource or the fauna
                    closest.transform.position = Vector3.MoveTowards(
                        closest.transform.position, 
                        fauna.transform.position,
                        pullSpeed * Time.deltaTime
                    );
                    time += Time.deltaTime;
                    yield return null;
                }

                // Optionally do something once it's gathered 
                // (e.g. attach the block, add to inventory, etc.)
            }

            isGathering = false;
        }

        public override void OnBehaviorEnd(Fauna fauna)
        {
            isGathering = false;
        }
    }

    /// <summary>
    /// A "Morphing" or "Evolve" style behavior that can transform 
    /// the fauna's stats or visuals for a period of time.
    /// </summary>
    public class MorphEvolveBehavior : FaunaBehavior
    {
        [SerializeField] float morphDuration = 5f;
        [SerializeField] Material morphMaterial;
        [SerializeField] int requiredAggression = 3;
        
        private bool isMorphing = false;

        public override bool CanPerform(Fauna fauna)
        {
            // Only do it if aggression is at least X
            return !isMorphing && fauna.aggression >= requiredAggression;
        }

        public override IEnumerator Perform(Fauna fauna)
        {
            isMorphing = true;

            Renderer rend = fauna.GetComponentInChildren<Renderer>();
            Material originalMat = null;
            if (rend != null)
            {
                originalMat = rend.sharedMaterial;
                if (morphMaterial != null)
                {
                    rend.sharedMaterial = morphMaterial;
                }
            }

            // Maybe temporarily boost stats
            int oldAggression = fauna.aggression;
            fauna.aggression += 2;

            yield return new WaitForSeconds(morphDuration);

            // Revert
            if (rend != null && originalMat != null)
            {
                rend.sharedMaterial = originalMat;
            }
            fauna.aggression = oldAggression;

            isMorphing = false;
        }

        public override void OnBehaviorEnd(Fauna fauna)
        {
            // If forcibly ended, revert if needed
            isMorphing = false;
        }
    }
}
