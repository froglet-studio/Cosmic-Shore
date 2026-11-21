using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Tandava: the swarm. Every pilot is on one side and there is one opponent, so the arrow
    /// answers the only question the long cell takes away - which way is it. The swarm's anchor follows its body's
    /// centre (SwarmFauna moves its transform there every tick), so the arrow points at the animal, not where it hatched.
    /// </summary>
    public class TandavaObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        public bool TryGetObjective(out Transform target)
        {
            var controller = TandavaController.Current;
            target = controller ? controller.SwarmTransform : null;
            return target;
        }
    }
}
