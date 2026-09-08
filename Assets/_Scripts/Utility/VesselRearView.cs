using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The look-back view: while the player HOLDS the gesture (<see cref="RearViewGesture"/> —
    /// C, or LB+RB together), the gameplay camera moves to the mirror of its own follow offset —
    /// the same distance AHEAD of the vessel that it normally sits behind it — and keeps
    /// looking at the ship, so the pilot sees their own nose against everything that is chasing
    /// them. Let go and it is forward again.
    ///
    /// <para><b>Opt-in per vessel, not a platform law.</b> A hull only has a rear view if its
    /// <c>CameraSettingsSO.enableRearView</c> says so — <b>Manta and Scarab</b> today — because
    /// the look-back is an affordance of a particular ship rather than something every ship owes
    /// the player: it reads completely differently at the Urchin's 6.67-unit follow distance and
    /// at the Serpent's 250, and a hull whose silhouette fills the frame from in front has
    /// nothing to show. This is the opposite call from the occlusion corridor, the speed tunnel
    /// and the vision band, which are laws precisely because they must not be authorable — and
    /// the difference is that those answer questions every vessel raises (can I see my ship, how
    /// fast am I going, where is that other pilot) while this one answers a question only some
    /// hulls are shaped to ask.
    ///
    /// <para>The gate is read off the CAMERA CONTROLLER
    /// (<see cref="CustomCameraController.RearViewSupported"/>), which is answered by whichever
    /// vessel last configured the rig, so a swap onto a hull that never asked for it drops the
    /// view with nothing to keep in step.</para></para>
    ///
    /// <para>It replaces the picture-in-picture rear view (<c>Pip</c> / <c>PipUI</c> /
    /// <c>PipCamera.prefab</c>), which showed the same information in a 300x150 corner panel at
    /// a hardcoded 25 units and 120° FOV — a second camera pass, on a shared render texture
    /// only one vessel in a match was allowed to write, framed by a panel whose art no longer
    /// existed. See <c>Pip.cs</c> for how that is retired.</para>
    ///
    /// <para><b>There is deliberately NO second camera.</b> The rear view is the SAME rig, read
    /// from the other side. That is not only tidier — a second <c>Camera</c> would sit outside
    /// every camera-shaped platform system at once: the speed tunnel resolves
    /// <c>CameraManager</c>'s active controller and would keep narrowing the camera the player
    /// is no longer looking through (Docs/SPEED_TUNNEL.md), <c>ApplyCameraGraphicsSettings</c>
    /// pushes the player's FOV and anti-aliasing onto its three managed cameras and no others,
    /// <c>ThemeManagerData.SetBackgroundColor</c> is applied per camera, and
    /// <c>Camera.main</c> returns the first ENABLED camera tagged MainCamera — so two live
    /// gameplay cameras is a coin toss for everything that asks. Reusing the rig inherits all
    /// four for free. It is the same reasoning
    /// <c>CameraManager.BeginWindowedPlayerCamera</c> already records for the mode preview:
    /// use the real gameplay rig, because the platform laws are already bound to it.</para>
    ///
    /// <para><b>The mirror is applied at the POINT OF USE, never by writing the offset.</b>
    /// <c>CustomCameraController.EffectiveOffset</c> flips z when
    /// <see cref="CustomCameraController.RearView"/> is set, leaving <c>_followOffset</c> itself
    /// untouched. Everything that legitimately moves the camera keeps writing that field and
    /// keeps working while the rear view is up — the Manta/Rhino zoom-out abilities
    /// (<c>ZoomOutActionExecutor</c>, <c>CameraZoomFollowScaleProvider</c>), adaptive zoom, the
    /// skimmer's camera-scaling prism effect, and a mid-flight vessel swap re-applying its own
    /// <c>CameraSettingsSO</c>. Had the toggle written a mirrored offset instead, the first of
    /// those to fire would have written a negative z back and silently dropped the pilot out of
    /// the rear view with nothing to explain it.</para>
    ///
    /// <para>Local pilot only, bound in <c>VesselController.Initialize</c> and
    /// <c>ChangePlayer</c> under <c>IPlayer.IsLocalPilot</c> — the two sites the occlusion
    /// corridor, the speed tunnel and the vessel vision band bind at, and for the same reason:
    /// they are the only places every spawn path passes through, and <c>ChangePlayer</c> hands
    /// a LIVE vessel to another player without ever reaching <c>Initialize</c>. The clear is
    /// identity-guarded so an outgoing vessel's teardown, which runs AFTER the incoming
    /// vessel's bind during a swap, cannot cancel it.</para>
    ///
    /// <para>The occlusion corridor needs no special handling and is the reason the view is
    /// readable at all: it is a camera-to-vessel corridor that reads <c>_WorldSpaceCameraPos</c>
    /// on the GPU, so it follows the camera to the front of the ship and dissolves whatever the
    /// pilot is flying INTO while they are looking backwards.</para>
    /// </summary>
    public static class VesselRearView
    {
        static Transform _targetKey;
        static bool _held;
        static int _heldFrame = -1;
        static CustomCameraController _appliedController;
        static bool _warnedNoCameraManager;

        /// <summary>True while the pilot is holding the gesture down.</summary>
        public static bool IsHeld => _held && IsHoldFresh;

        /// <summary>True while the rear vantage is actually being applied to a camera.</summary>
        public static bool IsApplied => _appliedController != null && _appliedController.RearView;

        /// <summary>
        /// Bind the local pilot's vessel. The ONLY callers are <c>VesselController.Initialize</c>
        /// and <c>ChangePlayer</c> under <c>IPlayer.IsLocalPilot</c>. Re-binding always lands
        /// FORWARD-facing: a fresh vessel, a new round or a mid-match hull swap must not inherit
        /// a rear view the pilot asked for on a ship they are no longer flying.
        /// </summary>
        public static void SetTarget(Transform key)
        {
            _targetKey = key;
            SetHeld(false);
        }

        /// <summary>
        /// Stop tracking, but only if <paramref name="key"/> is still the vessel in force — a
        /// late teardown from an outgoing vessel must not unbind the incoming one during a swap.
        /// </summary>
        public static void ClearTarget(Transform key)
        {
            if (_targetKey == key)
                ClearTarget();
        }

        /// <summary>Unconditional off (scene teardown, returning to a vessel-less camera).</summary>
        public static void ClearTarget()
        {
            _targetKey = null;
            SetHeld(false);
        }

        /// <summary>
        /// Report what the player is holding THIS FRAME. Called every frame by
        /// <c>InputController</c> while it is running; the hold expires on its own if that stops
        /// happening (see <see cref="IsHoldFresh"/>).
        /// </summary>
        public static void SetHeld(bool held)
        {
            _held = held;
            _heldFrame = Time.frameCount;
            Apply();
        }

        /// <summary>
        /// Was the hold reported on the CURRENT frame? This is what makes releasing the view
        /// unmissable rather than a list of places to remember.
        ///
        /// <para><c>InputController.Update</c> has five early returns above the poll — not
        /// initialized, window not focused, not the local pilot, <c>InputStatus.Paused</c>,
        /// <c>PauseSystem.Paused</c> — and the component can also be disabled or destroyed
        /// outright. Every one of those means "nobody is holding anything any more", and a
        /// held-state driver that waited to be TOLD would sit mirrored through a pause, a
        /// tab-out, or the frame a vessel is despawned. Expiry inverts the burden: the hold has
        /// to be renewed to survive, so every present and future early return releases it for
        /// free.</para>
        /// </summary>
        static bool IsHoldFresh => _heldFrame == Time.frameCount;

        /// <summary>
        /// Push the current vantage onto the live gameplay camera. Called on every change AND
        /// once per frame — both because the hold has to be re-evaluated every frame (it expires
        /// rather than waiting to be cancelled) and because the camera under us can change
        /// without anything telling us:
        /// the death camera, the end camera and the manual replay camera all take over through
        /// <c>CameraManager.SetActiveCamera</c>, and the menu hands the view to a rig this does
        /// not own at all. A per-frame push is what makes "which camera is the rear view on"
        /// answerable at any instant rather than a function of call order.
        /// </summary>
        static void Apply()
        {
            var controller = ResolveGameplayController();

            // Four independent conditions, all of which must hold: the player is holding the
            // gesture, that hold is from THIS frame, the vessel is still live, and this hull
            // actually has a rear view. Any one of them lapsing puts the camera back forward.
            bool want = _held && IsHoldFresh && IsTargetLive() &&
                        controller != null && controller.RearViewSupported;

            // A camera we are no longer driving must be handed back FORWARD-facing. Without
            // this, cutting to the end camera mid-look-back would leave the player camera
            // holding a mirrored offset it would still be holding on the next round.
            if (_appliedController != null && _appliedController != controller)
            {
                if (_appliedController.RearView)
                {
                    _appliedController.RearView = false;
                    _appliedController.SnapToTarget();
                }
                _appliedController = null;
            }

            if (controller == null) return;

            if (controller.RearView != want)
            {
                controller.RearView = want;
                // CUT, never a sweep. The two vantages are a full 2x the follow distance apart
                // (60 units on a Manta, 100 on a Scarab), and a dynamic-mode rig would
                // SmoothDamp that gap straight THROUGH the ship. A look-back is an instant
                // glance in every game that has one.
                controller.SnapToTarget();
            }

            _appliedController = controller;
        }

        /// <summary>
        /// The bound vessel is still a live, active object. A vessel despawned mid-swap must
        /// drop the rear view immediately rather than leave the camera parked ahead of a ghost.
        /// </summary>
        static bool IsTargetLive() => _targetKey && _targetKey.gameObject.activeInHierarchy;

        /// <summary>
        /// The PLAYER's follow camera, and only that camera — null whenever anything else owns
        /// the view. Null is a designed state, not a fault (the pilot simply has no camera to
        /// flip), so only a missing <c>CameraManager</c> warns.
        ///
        /// <para><b>Being the active controller is not enough.</b> The death camera and the
        /// end/replay camera are <c>CustomCameraController</c>s too — <c>CameraManager</c>
        /// creates them as such if a scene has not — so an "active controller" test would flip
        /// a framing the pilot never asked for and cannot see the reason for: a death cam
        /// mirrored on the frame you die, or a broadcast replay posed by hand and then
        /// re-snapped underneath its own framing math. The identity test is against
        /// <c>GetCloseCamera</c>, which is the manager's own name for the player rig.</para>
        /// </summary>
        static CustomCameraController ResolveGameplayController()
        {
            var manager = CameraManager.Instance;
            if (manager == null)
            {
                if (!_warnedNoCameraManager)
                {
                    _warnedNoCameraManager = true;
                    CSDebug.LogWarning(
                        "[VesselRearView] No CameraManager — the look-back view is inert. It " +
                        "lives on the camera rig in the Bootstrap scene; a scene entered " +
                        "directly (tool scenes, play-from-scene) has no instance.");
                }
                return null;
            }

            var active = manager.GetActiveController() as CustomCameraController;
            if (active == null) return null;   // Cinemachine / the menu rig — expected.

            var playerRig = manager.GetCloseCamera();
            return playerRig != null && playerRig == active.transform ? active : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallDriver()
        {
            // Statics survive play-mode exit in the editor, so a stale binding from the previous
            // session would otherwise park the camera ahead of a vessel that no longer exists.
            _targetKey = null;
            _held = false;
            _heldFrame = -1;
            _appliedController = null;
            _warnedNoCameraManager = false;

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismOcclusionCorridor and VesselSpeedTunnel use.
            var go = new GameObject("[VesselRearView]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate, after the vessel has moved and the camera controllers have posed
        /// themselves — the same reasoning as the occlusion corridor's and the speed tunnel's
        /// publishers. Snapping here is order-independent either way: a fixed rig writes its
        /// pose outright, and a dynamic one SmoothDamps from a position that is already the
        /// destination.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Apply();

            void OnDisable()
            {
                if (_appliedController != null && _appliedController.RearView)
                {
                    _appliedController.RearView = false;
                    _appliedController.SnapToTarget();
                }
                _appliedController = null;
                _held = false;
                _heldFrame = -1;
            }
        }
    }
}
