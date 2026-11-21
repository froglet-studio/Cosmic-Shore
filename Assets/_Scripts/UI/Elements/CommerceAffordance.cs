using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Marks the control it sits on as a commerce affordance, so it wears the app shell's shared
    /// locked look for as long as that surface is de-scoped (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4).
    ///
    /// <para><b>It carries WHICH surface, never what state that surface is in.</b> The state comes
    /// from <see cref="SO_CommerceAvailability"/> and the look comes from
    /// <see cref="MenuAvailabilityView"/>, so this component cannot become a second authority or a
    /// second locked look — and the paid-EA conversion un-dims every control carrying one without
    /// touching a scene. Authoring the state here instead is the drift this exists to avoid: the
    /// config would say Available and the button would stay dim.</para>
    ///
    /// <para><b>Why a component at all, when the screens gate themselves in code.</b> A screen can
    /// only mark a control it holds a reference to, and only once its own <c>Start</c> has run — and
    /// the affordance that most needs marking is the button that OPENS a de-scoped panel, which by
    /// construction lives outside it and runs first. <c>EpisodeScreen</c> is the worked example: it
    /// sits on the panel it toggles, that panel ships inactive, so its <c>Start</c> cannot run until
    /// the panel opens, which is the one thing the de-scope prevents. This component is reached by
    /// its own <c>OnEnable</c> instead, so the control is dim before anyone presses it.</para>
    ///
    /// <para>The gate in the screen's own code stays the authority on what HAPPENS — this is the
    /// presentation half only, and nothing relies on it being present. Same split
    /// <c>OfflineUIGate</c> records: gate the UI so the player is never offered something that
    /// cannot work, and never rely on the UI alone to enforce it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CommerceAffordance : MonoBehaviour
    {
        [SerializeField, Tooltip("Which commerce surface this control belongs to. Its STATE is read " +
                                 "from Resources/CommerceAvailability - there is deliberately no " +
                                 "state field here, so the conversion is one asset edit.")]
        CommerceSurface surface = CommerceSurface.CatalogPurchase;

        /// <summary>The surface this control belongs to.</summary>
        public CommerceSurface Surface => surface;

        // Re-applied on every enable rather than once, because these controls are SetActive-toggled
        // by the screens that own them and a press may have re-tinted them in between.
        void OnEnable() => SO_CommerceAvailability.Mark(gameObject, surface);

        /// <summary>
        /// Ask before acting, for a control whose handler wants the refusal presented for it.
        /// Returns true when the press should go ahead.
        /// </summary>
        public bool TryPress() => SO_CommerceAvailability.TryPress(gameObject, surface);

        /// <summary>
        /// Finds or adds the marker on <paramref name="host"/> for <paramref name="surface"/> and
        /// applies it immediately. For a control built at RUNTIME — an episode card — which has no
        /// authored component to carry the surface. Idempotent, and it re-points an existing marker
        /// rather than refusing, because a pooled or re-populated card may carry a stale one.
        /// </summary>
        public static CommerceAffordance Ensure(GameObject host, CommerceSurface surface)
        {
            if (!host) return null;

            if (!host.TryGetComponent(out CommerceAffordance affordance))
                affordance = host.AddComponent<CommerceAffordance>();

            affordance.surface = surface;

            // AddComponent on an already-enabled host has run OnEnable with the DEFAULT surface, and
            // on a disabled one has not run it at all - so apply here rather than trusting either.
            SO_CommerceAvailability.Mark(host, surface);
            return affordance;
        }
    }
}
