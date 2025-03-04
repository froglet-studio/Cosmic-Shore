using UnityEngine;
using System.Collections;

namespace CosmicShore
{
    /// <summary>
    /// A simple example behavior that makes the Fauna "rush forward" if it meets
    /// certain conditions. Demonstrates referencing extra components or
    /// species-specific data from the Fauna.
    /// </summary>
    public class BullRushBehavior : FaunaBehavior
    {
        [SerializeField] private float rushSpeed = 15f;
        [SerializeField] private float rushDuration = 2f;
        [SerializeField] private float cooldownTime = 5f;

        private bool isOnCooldown = false;


        public override bool CanPerform()
        {
            // Example condition: Must not be on cooldown and must have a certain aggression level
            if (isOnCooldown) return false;
            return (fauna.aggression >= 5);
        }

        public override IEnumerator Perform()
        {
            // Example logic: Move forward at high speed for a short time
            var startRotation = fauna.transform.rotation;
            var startVelocity = fauna.transform.forward * rushSpeed;

            float timer = 0f;
            while (timer < rushDuration)
            {
                timer += Time.deltaTime;
                fauna.transform.position += startVelocity * Time.deltaTime;
                // Optionally add more logic (damage, visual effects, etc.)
                yield return null;
            }

            // Once done, set a cooldown so we can't do it again immediately.
            isOnCooldown = true;
            yield return new WaitForSeconds(cooldownTime);
            isOnCooldown = false;
        }
    }
}
