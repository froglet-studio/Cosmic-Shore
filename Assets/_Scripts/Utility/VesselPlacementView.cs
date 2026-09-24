using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The placement view: while a pilot is choosing WHERE to put their vessel, the gameplay
    /// camera frames the place rather than the ship. The Butterfly's Fold is its one caller —
    /// hold LT, the vessel stops, and the sticks sweep a destination anywhere in the cell
    /// (<c>R_VesselActions/BUTTERFLY_FOLD.md</c>).
    ///
    /// <para><b>A pilot cannot choose a place they cannot see.</b> The Fold reaches
    /// <c>MaxRadiusFraction</c> of the membrane — hundreds of units — so a camera left behind the
    /// stationary vessel shows the destination as a few pixels of ghost against the cell, if it
    /// is on screen at all. Framing the ghost turns the hold into what it is: you fly the
    /// DESTINATION with the sticks and let go when you like where you are.</para>
    ///
    /// <para><b>The sibling of <see cref="VesselRearView"/>, deliberately built to the same
    /// shape</b> — one static, one binding, one per-frame push onto the resolved gameplay
    /// controller, and the vantage applied at the POINT OF USE rather than written into the
    /// camera's follow target. Everything that argument buys there it buys here: there is no
    /// second camera to fall outside the speed tunnel, the graphics settings, the background
    /// colour and <c>Camera.main</c>; and nothing that legitimately re-writes the follow target
    /// (the spawn chain, a vessel swap, a re-applied <c>CameraSettingsSO</c>) can silently drop
    /// the placement.</para>
    ///
    /// <para><b>Only the POINT moves.</b> The follow distance, the offset and the ROTATION FRAME
    /// still come from the vessel, so the camera sits behind the destination at the vessel's own
    /// distance, oriented the way the pilot is oriented. That is not a detail: the Fold addresses
    /// its target in the vessel's ROLLED frame, so "roll the world until the place you want is
    /// where your thumbs already are" is only legible if the camera rolls with it.</para>
    ///
    /// <para><b>It is the pilot's, not the owner's.</b> The binding comes from
    /// <c>VesselController.Initialize</c> and <c>ChangePlayer</c> under
    /// <c>IPlayer.IsLocalPilot</c> — the two sites every camera-shaped platform law binds at — so
    /// an ability may call <see cref="Place"/> on every peer without knowing anything about
    /// screens. A call keyed on a vessel that is not the local pilot's is simply ignored, which
    /// is what makes the ability's own code free of a camera gate it could get wrong (an AI
    /// Butterfly folds on the server, where there is no camera and no ghost).</para>
    /// </summary>
    public static class VesselPlacementView
    {
        static Transform _targetKey;
        static Transform _placingKey;
        static Vector3 _point;
        static bool _placing;
        static CustomCameraController _appliedController;
        static bool _warnedNoCameraManager;

        /// <summary>True while a placement is actually being framed by a camera.</summary>
        public static bool IsApplied => _appliedController != null &&
                                        _appliedController.PlacementAnchor.HasValue;

        /// <summary>
        /// Bind the local pilot's vessel. The ONLY callers are <c>VesselController.Initialize</c>
        /// and <c>ChangePlayer</c> under <c>IPlayer.IsLocalPilot</c>. Re-binding always lands on
        /// the vessel: a fresh hull must not inherit a placement the pilot was choosing on a ship
        /// they are no longer flying.
        /// </summary>
        public static void SetTarget(Transform key)
        {
            _targetKey = key;
            StopPlacing();
        }

        /// <summary>
        /// Stop tracking, but only if <paramref name="key"/> is still the vessel in force — a
        /// late teardown from an outgoing vessel must not unbind the incoming one during a swap.
        /// </summary>
        public static void ClearTarget(Transform key)
        {
            if (_targetKey == key) ClearTarget();
        }

        /// <summary>Unconditional off (scene teardown, returning to a vessel-less camera).</summary>
        public static void ClearTarget()
        {
            _targetKey = null;
            StopPlacing();
        }

        /// <summary>
        /// Frame <paramref name="point"/> while <paramref name="key"/>'s pilot chooses. Safe to
        /// call every frame and safe to call from a peer that is not the local pilot — a key that
        /// is not the bound vessel does nothing at all.
        /// </summary>
        public static void Place(Transform key, Vector3 point)
        {
            if (!key || key != _targetKey) return;
            _placingKey = key;
            _point = point;
            _placing = true;
            Apply();
        }

        /// <summary>
        /// Hand the camera back to the vessel. Keyed so a stale teardown cannot cancel a
        /// placement another vessel has since started.
        /// </summary>
        public static void Clear(Transform key)
        {
            if (_placingKey != key) return;
            StopPlacing();
        }

        static void StopPlacing()
        {
            _placingKey = null;
            _placing = false;
            Apply();
        }

        /// <summary>
        /// Push the current vantage onto the live gameplay camera. Called on every change AND once
        /// per frame, because the camera under us can change without anything telling us — the
        /// death camera, the end camera and the manual replay camera all take over through
        /// <c>CameraManager.SetActiveCamera</c>.
        /// </summary>
        static void Apply()
        {
            var controller = ResolveGameplayController();
            bool want = _placing && IsTargetLive();

            // A camera we are no longer driving must be handed back framing its own vessel.
            if (_appliedController != null && _appliedController != controller)
            {
                if (_appliedController.PlacementAnchor.HasValue)
                {
                    _appliedController.PlacementAnchor = null;
                    _appliedController.SnapToTarget();
                }
                _appliedController = null;
            }

            if (controller == null) return;

            bool had = controller.PlacementAnchor.HasValue;
            controller.PlacementAnchor = want ? _point : (Vector3?)null;

            // SNAP only on the TRANSITIONS, never while placing. Entering, the anchor starts AT
            // the vessel (the Fold seeds its ghost there), so the snap is a no-op that only
            // clears the smoothing state; leaving, the vessel has just been posed ONTO the point
            // the camera is already framing, so the snap is also a no-op. Between those two the
            // point sweeps the cell and the ordinary SmoothDamp is exactly what should carry it —
            // a per-frame snap would make the sweep read as a cut per frame.
            if (had != want) controller.SnapToTarget();

            _appliedController = controller;
        }

        static bool IsTargetLive() => _targetKey && _targetKey.gameObject.activeInHierarchy;

        /// <summary>
        /// The PLAYER's follow camera, and only that camera — null whenever anything else owns the
        /// view. Null is a designed state, not a fault; only a missing <c>CameraManager</c> warns.
        /// The identity test is against <c>GetCloseCamera</c> rather than "is it the active
        /// controller", because the death camera and the replay camera are
        /// <c>CustomCameraController</c>s too (<see cref="VesselRearView"/> records why).
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
                        "[VesselPlacementView] No CameraManager - the placement view is inert. It " +
                        "lives on the camera rig in the Bootstrap scene; a scene entered directly " +
                        "(tool scenes, play-from-scene) has no instance.");
                }
                return null;
            }

            var active = manager.GetActiveController() as CustomCameraController;
            if (active == null) return null;   // Cinemachine / the menu rig - expected.

            var playerRig = manager.GetCloseCamera();
            return playerRig != null && playerRig == active.transform ? active : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallDriver()
        {
            // Statics survive play-mode exit in the editor, so a stale placement from the previous
            // session would otherwise park the camera on a point in space.
            _targetKey = null;
            _placingKey = null;
            _placing = false;
            _appliedController = null;
            _warnedNoCameraManager = false;

            // HideInHierarchy (NOT HideAndDontSave - that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismOcclusionCorridor and VesselSpeedTunnel use.
            var go = new GameObject("[VesselPlacementView]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// LateUpdate, after the vessel has moved and the ability has placed its ghost, and before
        /// the camera controllers pose themselves for the frame - the same reasoning as the
        /// occlusion corridor's and the speed tunnel's publishers.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Apply();

            void OnDisable()
            {
                if (_appliedController != null && _appliedController.PlacementAnchor.HasValue)
                {
                    _appliedController.PlacementAnchor = null;
                    _appliedController.SnapToTarget();
                }
                _appliedController = null;
                _placingKey = null;
                _placing = false;
            }
        }
    }
}
