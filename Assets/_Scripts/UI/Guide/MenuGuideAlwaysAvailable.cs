using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Marks a control a guided path must never dim or block (<see cref="MenuGuide"/>). The
    /// Settings button needs no marker - it is found from its own onClick - so this is for anything
    /// else that must stay reachable while a new player is being guided.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MenuGuideAlwaysAvailable : MonoBehaviour
    {
    }
}
