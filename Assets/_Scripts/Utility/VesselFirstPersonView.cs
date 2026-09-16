using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The cockpit view: the gameplay camera moves ONTO the vessel, just past its nose, and aims
    /// where the ship is pointing instead of back at the hull — and the field of view narrows,
    /// so the pilot is looking down a scope rather than merely sitting further forward. Held by
    /// the Serpent's <c>SniperScopeActionExecutor</c> while its pilot holds the left trigger
    /// (<c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c>).
    ///
    /// <para><b>There is deliberately NO second camera</b>, for the four reasons
    /// <see cref="VesselRearView"/> records in full: a second live <c>Camera</c> falls outside
    /// the speed tunnel, <c>ApplyCameraGraphicsSettings</c>, per-camera background colour, and
    /// <c>Camera.main</c> all at once. This is the same rig, read from a different seat, so it
    /// inherits every camera-shaped platform law for free.</para>
    ///
    /// <para><b>The pose is applied at the POINT OF USE, never by writing the offset.</b>
    /// <see cref="CustomCameraController.EffectiveOffset"/> substitutes the cockpit while
    /// <see cref="CustomCameraController.FirstPerson"/> is set, leaving <c>_followOffset</c>
    /// untouched — so the four systems that legitimately move this camera mid-flight (the
    /// zoom-out abilities, adaptive zoom, the skimmer's camera-scaling prism effect, and a
    /// vessel swap re-applying its own <c>CameraSettingsSO</c>) keep working, and the first of
    /// them to fire cannot silently eject the pilot from the cockpit.</para>
    ///
    /// <para><b>The ZOOM goes through the speed tunnel's HOME, never through the camera.</b>
    /// <see cref="VesselSpeedTunnel"/> owns the gameplay camera's field of view fleet-wide and is
    /// its only writer; a direct write here would be overwritten every frame while the tunnel is
    /// engaged, and — worse — would be CAPTURED as the home to restore the next time the tunnel
    /// engaged, baking the zoom in permanently. Publishing the scoped value as the home instead
    /// keeps one writer and composes correctly: the tunnel still narrows for speed, it just
    /// narrows from the scoped base, so a scoped pilot who accelerates still reads their speed in
    /// the optics. See <c>VesselSpeedTunnel.SetHomeFieldOfViewOverride</c>.</para>
    ///
    /// <para><b>The eye is MEASURED, not authored.</b> Its offset is a multiple of the vessel's
    /// own circumscribing hull radius (<see cref="PrismOcclusionCorridor.MeasureCircumscribedRadius"/>,
    /// the same rotation-invariant hull-only measurement the occlusion corridor sizes itself
    /// from), so a hull of any size seats the camera just clear of its own geometry rather than
    /// at a constant that is inside one ship and far ahead of another. The Serpent is 250 units
    /// behind its camera in third person and a few units across; a hard-coded eye offset could
    /// not serve both it and a Squirrel.</para>
    ///
    /// <para>Local pilot only, bound in <c>VesselController.Initialize</c> and
    /// <c>ChangePlayer</c> under <c>IPlayer.IsLocalPilot</c> — the two sites the occlusion
    /// corridor, the speed tunnel, the vision band and the rear view all bind at, and for the
    /// same reason: they are the only places every spawn path passes through, and
    /// <c>ChangePlayer</c> hands a LIVE vessel to another player without ever reaching
    /// <c>Initialize</c>. Both clears are identity-guarded so an outgoing vessel's teardown,
    /// which runs AFTER the incoming vessel's bind during a swap, cannot cancel it.</para>
    ///
    /// <para><b>The occlusion corridor needs no special handling and is why the view is usable at
    /// all.</b> It is a camera-to-vessel corridor read from <c>_WorldSpaceCameraPos</c> on the
    /// GPU, so in the cockpit it collapses to almost nothing — which is correct: there is no
    /// longer any mass BETWEEN the camera and the ship to dissolve, and a scoped pilot sees the
    /// arena undissolved, which is exactly what aiming at it requires.</para>
    /// </summary>
    public static class VesselFirstPersonView
    {
        static Transform _targetKey;
        static float _hullRadius;
        static bool _engaged;
        static float _zoom01;
        static float _fovAtFullZoom = 20f;
        static CustomCameraController _appliedController;
        static bool _warnedNoCameraManager;

        /// <summary>
        /// How far past the vessel's own circumscribing hull radius the eye sits, so the pilot is
        /// never looking at the inside of their own ship. A multiple rather than a distance: see
        /// the class summary on why the eye is measured.
        /// </summary>
        const float EyeForwardHullRadii = 1.05f;

        /// <summary>True while the pilot has asked for the cockpit.</summary>
        public static bool IsEngaged => _engaged;

        /// <summary>True while the cockpit is actually being applied to a camera.</summary>
        public static bool IsApplied => _appliedController != null && _appliedController.FirstPerson;

        /// <summary>
        /// Bind the local pilot's vessel and MEASURE its hull once. Re-binding always lands in
        /// third person: a fresh vessel, a new round or a mid-match hull swap must not inherit a
        /// cockpit the pilot asked for on a ship they are no longer flying.
        /// </summary>
        public static void SetTarget(Transform key)
        {
            // Release the OUTGOING vessel's state BEFORE the key moves. The FOV override is keyed
            // on _targetKey, so rebinding first would leave it keyed to a transform nobody ever
            // passes again — a zoom stranded on the camera for the rest of the session. The swap
            // guard that matters lives on ClearTarget(Transform), which refuses a late teardown
            // from a vessel that is no longer the one in force; once we have decided to release,
            // the override is ours to drop outright.
            _engaged = false;
            _zoom01 = 0f;
            if (_appliedController != null) ReleaseController(_appliedController);
            VesselSpeedTunnel.ClearHomeFieldOfViewOverride();

            _targetKey = key;
            // Measured at bind, not per frame: it is a property of the hull, and the measurement
            // walks every renderer on the vessel.
            _hullRadius = key != null ? PrismOcclusionCorridor.MeasureCircumscribedRadius(key) : 0f;
            SetEngaged(false, 0f, _fovAtFullZoom);
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
            _hullRadius = 0f;
            SetEngaged(false, 0f, _fovAtFullZoom);
        }

        /// <summary>
        /// Set the vantage and the magnification together, because they are one ability: an
        /// ability holding the cockpit re-states both every frame it holds them.
        /// Idempotent, so "tell it again" is always safe.
        /// </summary>
        /// <param name="engaged">Whether the pilot is in the cockpit at all.</param>
        /// <param name="zoom01">0 = no magnification, 1 = <paramref name="fovAtFullZoom"/>.</param>
        /// <param name="fovAtFullZoom">The field of view at full magnification, in degrees.
        /// Element-scaled by the ability, so it arrives per vessel rather than as a constant.</param>
        public static void SetEngaged(bool engaged, float zoom01, float fovAtFullZoom)
        {
            _engaged = engaged;
            _zoom01 = Mathf.Clamp01(zoom01);
            if (fovAtFullZoom > 0f) _fovAtFullZoom = fovAtFullZoom;
            Apply();
        }

        /// <summary>
        /// Push the current vantage onto the live gameplay camera. Called on every change AND
        /// once per frame, because the camera under us can change without anything telling us:
        /// the death camera, the end camera and the manual replay camera all take over through
        /// <c>CameraManager.SetActiveCamera</c>, and the menu hands the view to a rig this does
        /// not own at all. A per-frame push is what makes "which camera is the cockpit on"
        /// answerable at any instant rather than a function of call order.
        /// </summary>
        static void Apply()
        {
            var controller = ResolveGameplayController();
            bool want = _engaged && IsTargetLive();

            // A camera we are no longer driving must be handed back in THIRD person. Without
            // this, cutting to the end camera while scoped would leave the player camera holding
            // a cockpit offset it would still be holding on the next round.
            if (_appliedController != null && _appliedController != controller)
            {
                ReleaseController(_appliedController);
                _appliedController = null;
            }

            if (controller == null)
            {
                // The camera went away mid-scope (menu, replay). The FOV override lives on a
                // static that outlives this frame, so it has to be dropped here too or the next
                // camera to engage the tunnel would inherit a zoom nobody is holding.
                if (!want) VesselSpeedTunnel.ClearHomeFieldOfViewOverride();
                return;
            }

            if (want)
            {
                controller.FirstPersonOffset = EyeOffset();

                if (!controller.FirstPerson)
                {
                    controller.FirstPerson = true;
                    // CUT, never a sweep. The two vantages are a whole follow distance apart
                    // (250 units on a Serpent), and a dynamic rig would SmoothDamp that gap
                    // straight THROUGH the ship. Raising a scope is instant in every game that
                    // has one.
                    controller.SnapToTarget();
                }

                VesselSpeedTunnel.SetHomeFieldOfViewOverride(ScopedFieldOfView(controller), _targetKey);
            }
            else if (controller.FirstPerson)
            {
                ReleaseController(controller);
            }
            else
            {
                // Not scoped and not applied — make sure no override is stranded on a frame where
                // the controller never carried the flag (the ability released before Apply ran).
                VesselSpeedTunnel.ClearHomeFieldOfViewOverride();
            }

            _appliedController = controller;
        }

        static void ReleaseController(CustomCameraController controller)
        {
            // Unconditional: by here this view has DECIDED to release, and the only thing that
            // could have set an override is this view. The swap guard is upstream, in
            // ClearTarget(Transform).
            VesselSpeedTunnel.ClearHomeFieldOfViewOverride();

            if (!controller.FirstPerson) return;
            controller.FirstPerson = false;
            controller.SnapToTarget();
        }

        /// <summary>
        /// The eye, in the vessel's own local space: straight ahead along its nose, just past the
        /// hull. Zero on a vessel whose hull could not be measured, which is still a usable
        /// cockpit (the camera sits on the origin) rather than a guess at a distance.
        /// </summary>
        static Vector3 EyeOffset() => new(0f, 0f, _hullRadius * EyeForwardHullRadii);

        /// <summary>
        /// The field of view the scope is asking for: the camera's own unscoped value at
        /// <c>zoom01 == 0</c>, narrowing to the ability's authored (element-scaled) value at
        /// full. Reading the LIVE camera for the unscoped end rather than a constant is what
        /// makes the scope respect the player's own FOV setting — a scope that starts by snapping
        /// to 90° would be a zoom OUT for anyone who plays at 70°.
        /// </summary>
        static float ScopedFieldOfView(CustomCameraController controller)
        {
            // WHICH value is "unscoped" depends on whether anything is currently writing the
            // camera, and getting this wrong is not subtle:
            //
            //  - While the speed tunnel is APPLIED, the camera's live field of view is already
            //    the tunnel's output (narrowed for speed, and once we engage, narrowed from our
            //    own scoped base). Reading it back would make scoping at speed lerp from the
            //    narrowed value — and then lock that in as the base and ratchet a little tighter
            //    every frame. The tunnel's own _homeFov is the honest player value there.
            //  - While it is NOT applied, nothing is writing the camera, so its live value IS the
            //    player's setting — and the tunnel's _homeFov is stale from whenever it last
            //    engaged, which could be any camera in any scene.
            //
            // HasHomeFieldOfViewOverride implies IsActive (an override engages the law on its
            // own), so the override case falls out of the first branch for free.
            float unscoped;
            if (VesselSpeedTunnel.IsActive)
                unscoped = VesselSpeedTunnel.HomeFieldOfView;
            else if (controller.Camera != null && !controller.Camera.orthographic)
                unscoped = controller.Camera.fieldOfView;
            else
                unscoped = _fovAtFullZoom;

            return Mathf.Lerp(unscoped, _fovAtFullZoom, _zoom01);
        }

        /// <summary>
        /// The bound vessel is still a live, active object. A vessel despawned mid-swap must drop
        /// the cockpit immediately rather than leave the camera parked inside a ghost.
        /// </summary>
        static bool IsTargetLive() => _targetKey && _targetKey.gameObject.activeInHierarchy;

        /// <summary>
        /// The PLAYER's follow camera, and only that camera — null whenever anything else owns
        /// the view. Null is a designed state, not a fault (the pilot simply has no camera to
        /// seat), so only a missing <c>CameraManager</c> warns.
        ///
        /// <para><b>Being the active controller is not enough</b>, for the reason
        /// <see cref="VesselRearView"/> records: the death camera and the end/replay camera are
        /// <c>CustomCameraController</c>s too, so an "active controller" test would seat a
        /// framing the pilot never asked for. The identity test is against <c>GetCloseCamera</c>,
        /// which is the manager's own name for the player rig.</para>
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
                        "[VesselFirstPersonView] No CameraManager — the cockpit view is inert. It " +
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
            // session would otherwise park the camera inside a vessel that no longer exists.
            _targetKey = null;
            _hullRadius = 0f;
            _engaged = false;
            _zoom01 = 0f;
            _appliedController = null;
            _warnedNoCameraManager = false;

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismOcclusionCorridor, VesselSpeedTunnel and
            // VesselRearView all use.
            var go = new GameObject("[VesselFirstPersonView]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate, after the vessel has moved and the camera controllers have posed
        /// themselves — the same reasoning as the occlusion corridor's, the speed tunnel's and
        /// the rear view's publishers.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Apply();

            void OnDisable()
            {
                if (_appliedController != null)
                    ReleaseController(_appliedController);
                _appliedController = null;
                _engaged = false;
                VesselSpeedTunnel.ClearHomeFieldOfViewOverride();
            }
        }
    }
}
