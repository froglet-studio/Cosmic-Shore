using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Base of every raycaster the event system queries (original contract). Enabled
    /// instances self-register (the original's RaycasterManager);
    /// <see cref="EventSystem.RaycastAll"/> walks the registry. Registration order is
    /// creation order — deterministic, same convention as the trigger pass.
    /// </summary>
    public abstract class BaseRaycaster : MonoBehaviour
    {
        static readonly List<BaseRaycaster> s_Raycasters = new();

        internal static IReadOnlyList<BaseRaycaster> ActiveRaycasters => s_Raycasters;

        /// <summary>Fresh-world reset — a new GameLoop clears registrations the old
        /// world never got to unregister (loop disposal skips OnDisable).</summary>
        internal static void ResetRegistry() => s_Raycasters.Clear();

        /// <summary>Appends every hit under <paramref name="eventData"/>.position.</summary>
        public abstract void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList);

        /// <summary>Canvas sortingOrder tier for cross-raycaster result ordering.</summary>
        public virtual int sortOrderPriority => 0;

        protected virtual void OnEnable()
        {
            if (!s_Raycasters.Contains(this)) s_Raycasters.Add(this);
        }

        protected virtual void OnDisable() => s_Raycasters.Remove(this);
    }
}
