using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.UI
{
    /// <summary>Shows a What's new row when the pad's selection lands on it (<see cref="WhatsNewModal"/>).</summary>
    public sealed class WhatsNewRowFocus : MonoBehaviour, ISelectHandler
    {
        public System.Action Picked;
        public void OnSelect(BaseEventData eventData) => Picked?.Invoke();
    }
}
