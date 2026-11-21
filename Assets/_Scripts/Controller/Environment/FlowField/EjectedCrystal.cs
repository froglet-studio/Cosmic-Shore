using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The motion of a crystal that was KNOCKED OUT of a vessel: it leaves at the velocity of
    /// whatever hit the hull, bleeds that off exponentially, and comes to rest in open space as
    /// an ordinary collectable pickup.
    ///
    /// <para>It is the crystal half of the prism debris model and deliberately reads the same
    /// way: a prism struck hard throws its pieces far and a graze drops them at your feet
    /// (<c>PrismEffectHelper.DamageProportional</c>), so a petal knocked out of a pilot at
    /// closing speed scatters across the arena while one prised loose by a point-blank blast
    /// lands next to them. The player never has to be told that the hit was hard.</para>
    ///
    /// <para><b>Why a script and not a Rigidbody.</b> A crystal's collider is a TRIGGER that the
    /// skimmer path reads; giving it a body would put it in the physics solver, let it collide
    /// with prisms and vessels, and cost one more simulated actor per petal in a game that can
    /// knock a dozen loose in a second. This integrates one position per frame and removes
    /// itself the moment it stops, so a settled crystal costs exactly what a spawner-placed one
    /// costs: nothing.</para>
    ///
    /// <para><b>The component retires itself, the crystal does not.</b> Continuity of existence
    /// is about the crystal, which stays in the world until somebody collects it - only the
    /// MOTION ends. Nothing here is on a clock that removes mass.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EjectedCrystal : MonoBehaviour
    {
        // Seconds^-1. At 1.8 a launch has shed ~85% of its speed in one second and is under the
        // rest threshold inside three - long enough to read as thrown, short enough that a petal
        // is never chased across the cell.
        const float DragPerSecond = 1.8f;

        // World units/second below which the crystal is simply parked. Sized against the slowest
        // thing that should still visibly drift rather than against float precision.
        const float RestSpeed = 0.35f;

        // A hard ceiling on how far one ejection can carry, independent of how fast the hit was.
        // Without it a Time-10 Sparrow round (375 u/s x its own multipliers) would post a petal
        // most of the way across an arena, and "knocked loose" would read as "deleted".
        const float MaxLaunchSpeed = 90f;

        Vector3 _velocity;

        /// <summary>
        /// Throws this crystal at <paramref name="velocity"/>, clamped to the ejection ceiling.
        /// Re-ejecting one that is still moving REPLACES its velocity rather than adding to it,
        /// so a crystal caught in a second blast cannot be accelerated without bound.
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            _velocity = Vector3.ClampMagnitude(velocity, MaxLaunchSpeed);
            enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            transform.position += _velocity * dt;

            // Exponential bleed - frame-rate independent, and it never overshoots into a reversal
            // the way a linear subtraction does at a low frame rate.
            _velocity *= Mathf.Exp(-DragPerSecond * dt);

            if (_velocity.sqrMagnitude <= RestSpeed * RestSpeed)
            {
                _velocity = Vector3.zero;
                enabled = false;   // settled: costs nothing from here on
            }
        }
    }
}
