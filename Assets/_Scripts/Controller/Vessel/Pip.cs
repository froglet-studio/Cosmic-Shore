using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>RETIRED — switched off, not deleted (2026-09).</b> The picture-in-picture rear view
    /// is replaced by the full-screen look-back camera, <c>VesselRearView</c>
    /// (Docs/REAR_VIEW.md): same information, at the vessel's own follow distance, drawn by the
    /// one gameplay rig every camera platform law is already bound to, toggled by <c>C</c> or
    /// LB+RB. <c>VesselController</c> no longer calls <see cref="SetLocalPilot"/>, so no vessel
    /// ever claims the panel and the panel never lights.
    ///
    /// <para><b>The file is kept, and keeping it is load-bearing rather than sentimental.</b>
    /// Eight hulls still instance <c>PipCamera.prefab</c>, which ships ACTIVE and ENABLED, and
    /// this component's <c>Awake</c> default-off is now the only thing standing that camera
    /// down. Deleting the component would hand every one of those vessels a permanent extra
    /// camera pass into a render texture nothing is showing — exactly the fault the default-off
    /// was written to prevent, arriving by the back door. Retire it properly by removing the
    /// <c>PipCamera</c> child from those prefabs first; until then, off is off.</para>
    ///
    /// <para>What it was: a second camera on the hull rendering into the shared
    /// <c>PipRenderTexture</c>, shown in the HUD's Pip panel. Eight vessels carry one
    /// (Falcon, Grizzly, Manta, Serpent, Shrike, Squirrel, Termite, Urchin); Dolphin, Sparrow,
    /// Rhino and Scarab do not, which is why no arcade mode locked to those four has ever had
    /// to think about it.</para>
    ///
    /// <para><b>Exactly one vessel in a match may drive it, and that vessel is the LOCAL PILOT'S.
    /// </b> There is one <c>PipRenderTexture</c> asset and one Pip panel on the HUD, so a second
    /// camera writing that texture is not a second view - it is two cameras overwriting each
    /// other's frames, and the panel shows whichever wrote last. Ownership therefore comes from
    /// <see cref="VesselController"/>, the one method every vessel routes through on every spawn
    /// path, under the same <c>IPlayer.IsLocalPilot</c> test the prism occlusion corridor, the
    /// speed tunnel and the vessel vision band use.</para>
    ///
    /// <para><b>What it must NOT ask is <c>AutoPilotEnabled</c>, and the reason is an ordering
    /// one.</b> That flag is set by <c>AIPilot.ActivateAutopilot</c>, which the spawn chain runs
    /// AFTER the vessel is instantiated - so at <c>Start</c> it reads false on every vessel in
    /// the match, AI and human alike. Gating on it therefore switched the camera on for every
    /// AI hull too and raised <c>IsActive = true</c> on a global SOAP channel that has no owner
    /// test, which is how a Hijack match ended up running two Urchin far-view cameras into one
    /// render texture. The flag was never a stand-in for "is this my ship"; it only ever looked
    /// like one because a human's autopilot is off by the time anyone looks.</para>
    ///
    /// <para>The default is OFF, applied in <c>Awake</c> so it lands before any
    /// <c>Initialize</c>: <c>PipCamera.prefab</c> ships active and enabled, and four vessels
    /// (Sparrow and Scarab among them) instance it while carrying no <see cref="Pip"/> at all -
    /// so without a default-off every one of those hulls renders a whole extra camera pass,
    /// forever, into a texture nothing is showing.</para>
    /// </summary>
    [RequireComponent(typeof(IVesselStatus))]
    public class Pip : MonoBehaviour
    {
        [SerializeField, Tooltip("The second camera on this hull. Rendered only while this " +
                                 "vessel is the local pilot's.")]
        Camera pipCamera;

        [SerializeField, Tooltip("Mirror the panel horizontally - a rear-view hull wants it, a " +
                                 "forward-looking one does not.")]
        bool mirrored;

        [SerializeField] ScriptableEventPipData _EventPipEventData;

        /// <summary>
        /// The one vessel currently speaking for the HUD panel, or null. IDENTITY-GUARDED for
        /// the same reason the corridor and the speed tunnel guard their targets: several
        /// vessels are bound in an order nobody controls - AI hulls are pre-spawned ahead of the
        /// human's, a client receives every pair in one RPC, and Cellular Duel hands a live
        /// vessel from a human to an AI at a round boundary. If a NON-local vessel were allowed
        /// to announce "panel off", whichever of those happened to be bound last would blank the
        /// local pilot's view, and the panel would be dark or lit depending on spawn order.
        /// </summary>
        static Pip _panelOwner;

        /// <summary>The last answer <see cref="SetLocalPilot"/> was given; false until it is
        /// called, which IS the default-off.</summary>
        bool _isLocalPilot;

        void Awake()
        {
            // Default OFF: PipCamera.prefab ships active and enabled, so a hull that is never
            // bound would otherwise render a whole extra camera pass forever into a texture
            // nothing is showing - four vessels instance that prefab without carrying a Pip.
            //
            // Applies the LAST KNOWN answer rather than a literal false, because Awake does not
            // reliably precede the bind: a vessel spawned inactive runs Awake when something
            // activates it, which can be after VesselController.Initialize. Writing false here
            // would then switch the local pilot's own view off a moment after granting it.
            SetCameraActive(_isLocalPilot);
        }

        void OnDestroy()
        {
            if (_panelOwner == this) _panelOwner = null;
        }

        /// <summary>
        /// Called by <see cref="VesselController"/> on initialization and on every ownership
        /// change, with whether this vessel is now the one the local pilot is flying. Idempotent,
        /// so a vessel handed away and handed back re-binds cleanly; a vessel that is never
        /// bound keeps the <c>Awake</c> default and costs nothing.
        /// </summary>
        public void SetLocalPilot(bool isLocalPilot)
        {
            _isLocalPilot = isLocalPilot;
            SetCameraActive(isLocalPilot);

            if (isLocalPilot)
            {
                _panelOwner = this;
                _EventPipEventData.Raise(new PipData { IsActive = true, IsMirrored = mirrored });
            }
            else if (_panelOwner == this)
            {
                // Only the vessel that turned the panel ON may turn it off - it is being handed
                // away, and no replacement has claimed the panel yet.
                _panelOwner = null;
                _EventPipEventData.Raise(new PipData { IsActive = false, IsMirrored = mirrored });
            }
        }

        void SetCameraActive(bool active)
        {
            if (pipCamera != null) pipCamera.gameObject.SetActive(active);
        }
    }
}
