using UnityEngine;
using System.Collections;

namespace CosmicShore
{
    /// <summary>
    /// Base class for all fauna behaviors.
    /// Extend this with your own logic.
    /// </summary>
    public abstract class FaunaBehavior : MonoBehaviour
    {
        /// <summary>
        /// Override to define whether this behavior
        /// is currently valid for the given Fauna (e.g., has relevant components).
        /// </summary>
        public virtual bool CanPerform(Fauna fauna)
        {
            return true;
        }

        /// <summary>
        /// Override to define the actual behavior logic.
        /// Ideally a coroutine that can run or do a single step.
        /// </summary>
        public abstract IEnumerator Perform(Fauna fauna);

        /// <summary>
        /// Called after Perform completes or is canceled, allowing for cleanup.
        /// Optional override if you need special teardown logic.
        /// </summary>
        public virtual void OnBehaviorEnd(Fauna fauna) { }
    }
}
