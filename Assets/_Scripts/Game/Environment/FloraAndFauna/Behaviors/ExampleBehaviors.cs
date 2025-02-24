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

        public override bool CanPerform(Fauna fauna)
        {
            // Example condition: Must not be on cooldown and must have a certain aggression level
            if (isOnCooldown) return false;
            return (fauna.aggression >= 5);
        }

        public override IEnumerator Perform(Fauna fauna)
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

    /// <summary>
    /// A behavior that "whips" a tail, possibly damaging nearby enemies.
    /// Illustrates how you might do a component-specific action.
    /// </summary>
    public class TailWhipBehavior : FaunaBehavior
    {
        [SerializeField] private float whipRadius = 5f;
        [SerializeField] private float whipDamage = 10f;
        [SerializeField] private float whipCooldown = 3f;

        private bool tailReady = true;

        public override bool CanPerform(Fauna fauna)
        {
            // Maybe we only do this if the fauna has some "Tail" component
            // or if it simply isn't on cooldown, etc.
            return tailReady;
        }

        public override IEnumerator Perform(Fauna fauna)
        {
            tailReady = false;

            // For demonstration, "Damage" anything in whip radius
            var nearby = Physics.OverlapSphere(fauna.transform.position, whipRadius);
            foreach (var obj in nearby)
            {
                // If it's a non-friendly or neutral, apply some damage
                var healthBlock = obj.GetComponent<HealthBlock>();
                if (healthBlock && healthBlock.LifeForm && healthBlock.LifeForm.Team != fauna.Team)
                {
                    // Attempt to damage them
                    Vector3 direction = obj.transform.position - fauna.transform.position;
                    healthBlock.Damage(direction.normalized * whipDamage, fauna.Team, "Tail Whip", false);
                }
            }

            // Optional short delay to simulate an animation time
            yield return new WaitForSeconds(0.5f);

            // Start cooldown
            yield return new WaitForSeconds(whipCooldown);
            tailReady = true;
        }
    }
}
