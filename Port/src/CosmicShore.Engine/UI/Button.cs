using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Headless UGUI Button shim (engine addition, Tournament arc): ported code wires
    /// <see cref="onClick"/> listeners and toggles <see cref="interactable"/>; a render/input
    /// backend dispatches presses in the presentation phase (harnesses/tests call
    /// <c>onClick.Invoke()</c>). Substitution: the `using UnityEngine.UI;` directive folds
    /// into `using CosmicShore.Engine.UI;` (shared with the TMPro shim).
    /// </summary>
    public class Button : Behaviour
    {
        public UnityEvent onClick = new();
        public bool interactable = true;
    }
}
