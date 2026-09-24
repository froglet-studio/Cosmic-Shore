using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The <b>recording area</b> — the clean space a recording is watched in, with the live world
    /// taken off the screen and nothing in shot but the ghosts.
    ///
    /// <para><b>It is a CULLING MASK, not a scene load, and that is the whole design.</b> A
    /// dedicated scene is what a theater wants to be, and this project cannot have one cheaply: a
    /// local <c>SceneManager.LoadScene</c> while a NetworkManager is listening races the server's
    /// own scene management (CLAUDE.md's MPPM guard exists for exactly that), and every scene in
    /// the game — Menu_Main included — is running one. So the stage hides the world the one way
    /// that touches nothing: the replay camera is re-masked onto a layer only the puppets are on,
    /// and its background is painted. Nothing is destroyed, nothing is disabled, no gameplay
    /// object learns the theater exists, and leaving is four field restores.</para>
    ///
    /// <para><b>The match keeps simulating underneath.</b> Stated plainly because it is a real
    /// consequence rather than an oversight: prisms are still laid, fauna still feed, the clock
    /// still runs. That is what makes entering and leaving free — you come back to the game you
    /// left, mid-flight, rather than to a reloaded one. The local pilot's INPUT is paused for the
    /// duration (the theater and the vessel would otherwise both read the same sticks), so the
    /// ship coasts rather than flying off while its pilot is looking at a replay.</para>
    ///
    /// <para><b>A missing layer degrades, it does not fail.</b> With no <c>Theater</c> layer in
    /// <c>TagManager</c> the stage declines to mask and playback runs over the live world — which
    /// is worse-looking and completely functional. Masking onto a layer that does not exist would
    /// render a black screen, and a black screen is indistinguishable from a broken feature.</para>
    /// </summary>
    public class TheaterStage
    {
        /// <summary>The layer puppets are drawn on while the stage is up. Index 19 in TagManager.</summary>
        public const string LayerName = "Theater";

        static bool _warnedNoLayer;

        readonly List<IPlayer> _paused = new();

        Camera _camera;
        int _restoreMask;
        CameraClearFlags _restoreClearFlags;
        Color _restoreBackground;
        bool _masked;

        public bool IsUp { get; private set; }

        /// <summary>The layer puppets must be on, or -1 when the project has no Theater layer.</summary>
        public static int Layer => LayerMask.NameToLayer(LayerName);

        public void Enter(Camera camera, Transform puppetRoot, Color background)
        {
            if (IsUp) return;
            IsUp = true;

            PauseLocalPilots();

            int layer = Layer;
            if (layer < 0)
            {
                if (!_warnedNoLayer)
                {
                    _warnedNoLayer = true;
                    CSDebug.LogWarning(
                        $"[Theater] No '{LayerName}' layer in TagManager, so the recording is drawn " +
                        "over the live world instead of on a clean stage. Add a user layer named " +
                        $"'{LayerName}' (Project Settings > Tags and Layers).");
                }
                return;
            }

            if (puppetRoot != null) SetLayerRecursive(puppetRoot, layer);

            if (camera == null) return;

            _camera = camera;
            _restoreMask = camera.cullingMask;
            _restoreClearFlags = camera.clearFlags;
            _restoreBackground = camera.backgroundColor;
            _masked = true;

            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
        }

        public void Exit()
        {
            if (!IsUp) return;
            IsUp = false;

            // Restore the camera FIRST and unconditionally. A theater torn down by a scene change
            // can find its puppets already destroyed, and leaving the gameplay camera masked onto
            // an empty layer is a black screen for the rest of the session.
            if (_masked && _camera != null)
            {
                _camera.cullingMask = _restoreMask;
                _camera.clearFlags = _restoreClearFlags;
                _camera.backgroundColor = _restoreBackground;
            }
            _camera = null;
            _masked = false;

            for (int i = 0; i < _paused.Count; i++)
            {
                var player = _paused[i];
                if (player?.InputController != null) player.InputController.SetPause(false);
            }
            _paused.Clear();
        }

        /// <summary>
        /// Put a puppet built after <see cref="Enter"/> on the stage's layer. Playback builds every
        /// puppet up front today; this exists so a later phase's prisms and fauna have one door.
        /// </summary>
        public void Adopt(Transform root)
        {
            int layer = Layer;
            if (IsUp && layer >= 0 && root != null) SetLayerRecursive(root, layer);
        }

        void PauseLocalPilots()
        {
            var scratch = new List<Transform>();
            VesselVisionShading.CollectStampedVessels(scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                var vessel = scratch[i];
                if (vessel == null) continue;
                if (!vessel.TryGetComponent(out IVesselStatus status)) continue;

                // IsLocalPilot, never IsLocalUser: the legacy single-player spawn path never
                // network-spawns its Player, so IsLocalUser reports false for a human there and
                // that pilot's ship would fly on the theater's own stick input.
                var player = status.Player;
                if (player == null || !player.IsLocalPilot) continue;
                if (player.InputController == null) continue;

                player.InputController.SetPause(true);
                _paused.Add(player);
            }
        }

        static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursive(root.GetChild(i), layer);
        }
    }
}
