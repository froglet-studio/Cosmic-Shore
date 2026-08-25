using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel's <b>JET</b> — an engine plume that tells a pilot their own ship is moving.
    ///
    /// A jet is mounted on the MODEL, at whatever that hull calls an engine, and it points where
    /// that engine points. That is the whole placement rule: a jet must come out of somewhere the
    /// model says thrust comes out of, so it reads as the ship working rather than as a decal
    /// stuck behind it. Contrast <see cref="VesselTail"/>, which hangs off the vessel ROOT because
    /// its job is a silhouette at range, not a mechanism up close.
    ///
    /// <b>A jet is for its own pilot.</b> Every pilot sees their own jets and nobody else's, so a
    /// crowded cell does not fill with other people's exhaust and the pilot's own thrust stays
    /// readable. <see cref="visibleToOtherPilots"/> is the opt-out for a vessel whose jets ARE a
    /// signal to the rest of the field (the Serpent). It is per-jet rather than per-vessel on
    /// purpose: a hull may want one telegraphing plume and three private ones.
    ///
    /// Hiding is a <c>SetActive(false)</c> on this GameObject, which is safe precisely because a
    /// jet is pure photons — no collider, no gameplay state, nothing another system reads. The
    /// tint pass still finds a hidden jet (<see cref="VesselTailAndJets"/> discovers with
    /// <c>includeInactive: true</c>), so a jet that is revealed later is already wearing the
    /// right domain instead of flashing its prefab colour for one frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselJet : MonoBehaviour
    {
        [Tooltip("Leave OFF for the standard jet: visible only to the pilot flying this vessel. " +
                 "Turn ON only for a jet that is deliberately a signal to the rest of the field " +
                 "(the Serpent) — every jet you reveal is exhaust in somebody else's view.")]
        [SerializeField] bool visibleToOtherPilots;

        /// <summary>
        /// Show or hide this jet for the machine that is watching. <paramref name="viewerIsOwnPilot"/>
        /// is true only on the client whose player flies this vessel; an AI hull and every remote
        /// replica pass false, so a standard jet is drawn on exactly one screen.
        /// </summary>
        public void SetViewerIsOwnPilot(bool viewerIsOwnPilot)
        {
            var visible = viewerIsOwnPilot || visibleToOtherPilots;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
