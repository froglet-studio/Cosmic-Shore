using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A second, read-only reader of the boot status channel (<see cref="ScriptableEventBootStatusRequest"/>)
    /// for a scene that draws its own loading readout - the sign-in scene (Docs/MENU_REDESIGN_B.md
    /// section 26). <see cref="BootStatusPanel"/> on the Bootstrap splash stays the one surface that owns
    /// the retry button; this only writes the line ("Signing in...", "Loading profile...") under the
    /// scene's own bar, because while the sign-in scene is up the splash is faded out and its copy of
    /// the line cannot be seen.
    /// </summary>
    public class BootStatusLine : MonoBehaviour
    {
        [Tooltip("Inbound: the boot status channel every boot phase raises on.")]
        [SerializeField] private ScriptableEventBootStatusRequest requestEvent;

        [Tooltip("The status line under the bar.")]
        [SerializeField] private TMP_Text statusText;

        [Tooltip("The whole readout (caption, bar, line). Hidden while the boot asks for nothing - the " +
                 "scene is waiting on the player, who should not see a loading bar under a question. Optional.")]
        [SerializeField] private CanvasGroup readoutGroup;

        private void OnEnable() => requestEvent.OnRaised += Apply;

        private void OnDisable() => requestEvent.OnRaised -= Apply;

        private void Apply(BootStatusRequest request)
        {
            bool hide = request.Mode == BootStatusMode.Hide;
            if (statusText) statusText.text = hide ? string.Empty : request.Text ?? string.Empty;
            KitVisibility.SetShown(readoutGroup, !hide);
        }
    }
}
