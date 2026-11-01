using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The <b>recording area</b>: what the theater takes over while a recording plays, and gives
    /// back the instant it stops.
    ///
    /// <para><b>The world STAYS. Only the interface goes.</b> The first cut masked the live world
    /// off the camera onto a private layer, on the reasoning that a theater wants a clean void —
    /// and a void is exactly what it produced: no prisms, no environment, no crystals, nothing but
    /// ghosts in the dark. A Halo theater shows you the MAP. So the mask is now opt-in
    /// (<see cref="TheaterConfigSO.hideWorld"/>, default off) and the stage's real job is the three
    /// things that genuinely have to stop while somebody watches a replay.</para>
    ///
    /// <list type="number">
    /// <item><b>The gameplay UI is hidden, and that is a BUG FIX before it is a look.</b> IMGUI and
    /// uGUI process the same mouse event independently and neither can consume it for the other, so
    /// every theater button sitting over a live uGUI control pressed BOTH — which is why the two
    /// rightmost buttons, parked over the HUD's own top-right Volume/Pause button, kicked the
    /// player back to the menu. Hiding the canvases and standing the EventSystem down removes the
    /// competing half rather than moving the panel and hoping.</item>
    /// <item><b>The local pilot's input is paused</b> (<c>IsLocalPilot</c>, never
    /// <c>IsLocalUser</c> — the legacy single-player spawn path never network-spawns its Player),
    /// so the theater and the vessel are not both reading the same sticks.</item>
    /// <item><b>The live vessels stop DRAWING</b>, through <c>forceRenderingOff</c> rather than by
    /// disabling anything: a frozen real ship parked beside the ghost replaying its own flight is
    /// the one thing in shot that can only ever confuse. It is a render suppression, so every
    /// component keeps running and restoring is one bool.</item>
    /// </list>
    ///
    /// <para><b>The match keeps simulating underneath.</b> Prisms are still laid, fauna still feed,
    /// the clock still runs — which is what makes entering and leaving free: you come back to the
    /// game you left, mid-flight. It also means the world you fly the theater through is the world
    /// as it is NOW, not as it was during the recording. Prisms are P1; until then the trails in
    /// shot are live ones.</para>
    /// </summary>
    public class TheaterStage
    {
        /// <summary>The layer puppets are drawn on while <c>hideWorld</c> is up. Index 19.</summary>
        public const string LayerName = "Theater";

        static bool _warnedNoLayer;

        readonly List<IPlayer> _pausedPilots = new();
        readonly List<Canvas> _hiddenCanvases = new();
        readonly List<Renderer> _hiddenVessels = new();
        readonly List<Transform> _scratch = new();

        EventSystem _stoodDownEventSystem;
        Camera _camera;
        int _restoreMask;
        CameraClearFlags _restoreClearFlags;
        Color _restoreBackground;
        bool _masked;

        public bool IsUp { get; private set; }

        /// <summary>The layer puppets go on when the world is hidden, or -1 when the project has none.</summary>
        public static int Layer => LayerMask.NameToLayer(LayerName);

        public void Enter(Camera camera, Transform puppetRoot, TheaterConfigSO config)
        {
            if (IsUp) return;
            IsUp = true;

            PauseLocalPilots();
            HideLiveVessels();

            if (config == null || config.hideGameplayUI) HideGameplayUI();
            if (config != null && config.hideWorld) HideWorld(camera, puppetRoot, config.stageBackground);
        }

        public void Exit()
        {
            if (!IsUp) return;
            IsUp = false;

            // The camera comes back FIRST and unconditionally. A theater torn down by a scene
            // change can find its puppets already destroyed, and a gameplay camera left masked
            // onto an empty layer is a black screen for the rest of the session.
            if (_masked && _camera != null)
            {
                _camera.cullingMask = _restoreMask;
                _camera.clearFlags = _restoreClearFlags;
                _camera.backgroundColor = _restoreBackground;
            }
            _camera = null;
            _masked = false;

            for (int i = 0; i < _hiddenCanvases.Count; i++)
            {
                var canvas = _hiddenCanvases[i];
                if (canvas == null) continue;
                canvas.enabled = true;

                // A Graphic's rebuilds are inert while its Canvas is disabled (CLAUDE.md's
                // Canvas.enabled trap), so anything the HUD tried to redraw while the theater was
                // up was dropped. Re-dirty on the way out rather than leaving a stale readout.
                var graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                    if (graphics[g] != null) graphics[g].SetAllDirty();
            }
            _hiddenCanvases.Clear();

            if (_stoodDownEventSystem != null) _stoodDownEventSystem.enabled = true;
            _stoodDownEventSystem = null;

            for (int i = 0; i < _hiddenVessels.Count; i++)
                if (_hiddenVessels[i] != null) _hiddenVessels[i].forceRenderingOff = false;
            _hiddenVessels.Clear();

            for (int i = 0; i < _pausedPilots.Count; i++)
            {
                var player = _pausedPilots[i];
                if (player?.InputController != null) player.InputController.SetPause(false);
            }
            _pausedPilots.Clear();
        }

        /// <summary>
        /// Put a puppet built after <see cref="Enter"/> onto the stage's layer, when there is one.
        /// Playback builds every puppet up front today; this exists so a later phase's prisms and
        /// fauna have one door.
        /// </summary>
        public void Adopt(Transform root)
        {
            int layer = Layer;
            if (_masked && layer >= 0 && root != null) SetLayerRecursive(root, layer);
        }

        /// <summary>
        /// Mask the live world off the camera and paint a backdrop. <b>Opt-in</b>: it answers
        /// "show me this flight and nothing else", which is a real thing to want and the opposite
        /// of the default.
        ///
        /// <para>A missing layer DEGRADES rather than failing — masking onto a layer that does not
        /// exist renders a black screen, and a black screen is indistinguishable from a broken
        /// feature.</para>
        /// </summary>
        void HideWorld(Camera camera, Transform puppetRoot, Color background)
        {
            int layer = Layer;
            if (layer < 0)
            {
                if (!_warnedNoLayer)
                {
                    _warnedNoLayer = true;
                    CSDebug.LogWarning(
                        $"[Theater] hideWorld is on but there is no '{LayerName}' layer in " +
                        "TagManager, so the world is left visible. Add a user layer named " +
                        $"'{LayerName}' (Project Settings > Tags and Layers).");
                }
                return;
            }

            if (camera == null) return;

            if (puppetRoot != null) SetLayerRecursive(puppetRoot, layer);

            _camera = camera;
            _restoreMask = camera.cullingMask;
            _restoreClearFlags = camera.clearFlags;
            _restoreBackground = camera.backgroundColor;
            _masked = true;

            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
        }

        /// <summary>
        /// Switch off every root canvas that is currently drawing, and stand the EventSystem down
        /// so no uGUI control can be pressed through the theater's own overlay.
        ///
        /// <para>World-space canvases are left alone: they are part of the SCENE rather than part
        /// of the interface, so a world-space label belongs in shot exactly as the prisms do.</para>
        /// </summary>
        void HideGameplayUI()
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || !canvas.enabled) continue;
                if (!canvas.isRootCanvas) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.enabled)
            {
                eventSystem.enabled = false;
                _stoodDownEventSystem = eventSystem;
            }
        }

        /// <summary>
        /// Stop the live vessels drawing for the duration. <c>forceRenderingOff</c> rather than
        /// disabling the renderer or the object: every component keeps running (nothing learns the
        /// theater exists), and restoring is one bool per renderer.
        /// </summary>
        void HideLiveVessels()
        {
            _scratch.Clear();
            VesselVisionShading.CollectStampedVessels(_scratch);

            for (int i = 0; i < _scratch.Count; i++)
            {
                var vessel = _scratch[i];
                if (vessel == null) continue;

                var renderers = vessel.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || renderer.forceRenderingOff) continue;
                    renderer.forceRenderingOff = true;
                    _hiddenVessels.Add(renderer);
                }
            }
        }

        void PauseLocalPilots()
        {
            _scratch.Clear();
            VesselVisionShading.CollectStampedVessels(_scratch);

            for (int i = 0; i < _scratch.Count; i++)
            {
                var vessel = _scratch[i];
                if (vessel == null) continue;
                if (!vessel.TryGetComponent(out IVesselStatus status)) continue;

                // IsLocalPilot, never IsLocalUser: the legacy single-player spawn path never
                // network-spawns its Player, so IsLocalUser reports false for a human there and
                // that pilot's ship would fly on the theater's own stick input.
                var player = status.Player;
                if (player == null || !player.IsLocalPilot) continue;
                if (player.InputController == null) continue;

                player.InputController.SetPause(true);
                _pausedPilots.Add(player);
            }
        }

        static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursive(root.GetChild(i), layer);
        }
    }
}
