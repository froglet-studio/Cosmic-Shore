using UnityEngine;
using System.Collections;

namespace CosmicShore
{

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

        public override bool CanPerform()
        {
            // Maybe we only do this if the fauna has some "Tail" component
            // or if it simply isn't on cooldown, etc.
            return tailReady;
        }

        public override IEnumerator Perform()
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
