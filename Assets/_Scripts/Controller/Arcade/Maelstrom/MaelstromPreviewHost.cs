using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Maelstrom hub's <c>ConfigurationContent</c> panel: a live window onto the round the
    /// party is about to play, and - while the countdown runs - a place to fly it.
    ///
    /// <para><b>The hub IS the arena, rather than standing one beside it.</b> The arcade modal's
    /// preview parks a SATELLITE cell 120,000 units away, because Menu_Main has a live world the
    /// preview must not disturb. The Maelstrom scene has no world at all - it is a UI scene with a
    /// camera in it - so there is nothing to protect and no reason to pay for a second cell. The
    /// hub instead swaps its own <see cref="Cell"/> onto the drawn mode's arena through
    /// <see cref="Cell.RequestCellSwap"/>, which is the platform's one sanctioned runtime
    /// world-change: the old world suctions away and the new one blooms in behind the standard
    /// veil, so continuity of existence holds at both ends and the ecology, phase ladder and
    /// spawners are the Cell's own rather than a parallel set this mode invented.</para>
    ///
    /// <para><b>Two cameras, one surface, handed over in order.</b> Unfocused, an orbit camera
    /// frames the whole arena - "show me where we are going". Tapping in hands the surface to the
    /// real gameplay camera behind the local pilot's vessel and gives them the stick. The incoming
    /// camera always takes the <see cref="RenderTexture"/> BEFORE the outgoing one lets go,
    /// because a frame with nobody drawing into the surface is the white rectangle this whole
    /// window exists to make impossible.</para>
    ///
    /// <para><b>It degrades rather than failing.</b> No preview definition for the drawn mode (six
    /// of the sixteen pool modes have none) shows the honest "not available" state with the mode's
    /// name and description still on screen. No vessel in the scene - the hub's spawn pair is not
    /// placed - still gives the orbiting look at the arena; only the tap-in is lost, and it says
    /// so once with the fix attached.</para>
    ///
    /// <para><b>No flight on the stats screen.</b> The end-of-tournament screen shares this panel
    /// and shows the environment with <see cref="ModePreviewWindow.SetFocusEnabled"/> off, so the
    /// "tap to play" affordance is not offered for a round that will never be played.</para>
    /// </summary>
    public class MaelstromPreviewHost : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField, Tooltip("Optional. Falls back to Resources/ModePreviewLibrary, which is " +
                                 "where the arcade modal's previews already resolve from.")]
        ModePreviewLibrarySO previewLibrary;

        [Header("Surface")]
        [SerializeField, Tooltip("The window inside ConfigurationContent. Owns the RawImage, the " +
                                 "status label, the focus button and the 'tap to play' hint.")]
        ModePreviewWindow window;

        [Header("Briefing")]
        [SerializeField, Tooltip("The arcade launch panel, when the scene carries one (the " +
                                 "Maelstrom hub's ConfigurationContent does). Preferred over the " +
                                 "two fields below: binding it is literally what the main menu " +
                                 "does, so the name and description read identically in both. " +
                                 "Found on this object or below it when empty.")]
        MinigameLaunchPanel launchPanel;

        [SerializeField, Tooltip("Mode name, for a panel-less host. Ignored when a launch panel " +
                                 "is present - that panel owns its own name line.")]
        TMP_Text modeNameText;

        [SerializeField, Tooltip("Description / tips block, for a panel-less host. Ignored when a " +
                                 "launch panel is present.")]
        GameBriefingView briefing;

        [Header("Arena")]
        [SerializeField, Tooltip("The hub's Cell. Left empty it finds the scene's own - there is " +
                                 "normally exactly one.")]
        Cell cell;

        [SerializeField, Tooltip("Degrees per second the unfocused camera orbits the arena.")]
        float orbitDegreesPerSecond = 6f;

        [SerializeField, Tooltip("Seconds to wait for a cell swap to finish before giving up and " +
                                 "showing the arena anyway.")]
        [Min(1f)] float buildTimeoutSeconds = 45f;

        Camera _orbitCamera;
        float _orbitAngle;
        bool _flying;
        bool _navigationWasEnabled = true;
        bool _warnedNoVessel;
        CancellationTokenSource _cts;

        SO_ArcadeGame _game;
        int _intensity = 1;
        bool _allowFlight;

        /// <summary>True while the local pilot has the stick inside the preview.</summary>
        public bool IsFlying => _flying;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        void Awake()
        {
            if (!launchPanel) launchPanel = GetComponent<MinigameLaunchPanel>();
            if (!launchPanel) launchPanel = GetComponentInChildren<MinigameLaunchPanel>(true);

            // The panel's OWN window, when there is a panel - never a second one beside it.
            if (!window && launchPanel) window = launchPanel.PreviewWindow;
            if (!window) window = GetComponentInChildren<ModePreviewWindow>(true);
            if (!briefing) briefing = GetComponentInChildren<GameBriefingView>(true);

            // Only build one if the scene authored none. A hub whose ConfigurationContent carries
            // a real launch panel already has its surface, status label, focus button and hint
            // wired, and EnsureSurface is a no-op there - it exists for a panel-less scene, where
            // a window that silently renders nothing because one of three references was not
            // dragged in is the failure this screen has already shipped once.
            if (!window) window = gameObject.AddComponent<ModePreviewWindow>();
            window.EnsureSurface();

            window.OnFocusRequested += HandleFocusRequested;
            window.OnFocusReleased += HandleFocusReleased;
        }

        void OnDestroy()
        {
            if (window)
            {
                window.OnFocusRequested -= HandleFocusRequested;
                window.OnFocusReleased -= HandleFocusReleased;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            ReleaseStick();
            TearDownOrbitCamera();
        }

        void LateUpdate()
        {
            if (_flying || !_orbitCamera || !_orbitCamera.enabled) return;
            FrameArena(Time.unscaledDeltaTime * orbitDegreesPerSecond);
        }

        // ── Entry points ─────────────────────────────────────────────────────────

        /// <summary>
        /// Show <paramref name="game"/>'s arena. <paramref name="allowFlight"/> false makes it a
        /// look-only view (the stats screen), which is enforced on the window itself rather than
        /// by hiding a button - an affordance the surface will not honour is worse than none.
        /// </summary>
        public void ShowRound(SO_ArcadeGame game, int intensity, bool allowFlight)
        {
            intensity = Mathf.Clamp(intensity <= 0 ? 1 : intensity, 1, 4);

            bool sameArena = _game == game && _intensity == intensity;
            _game = game;
            _intensity = intensity;
            _allowFlight = allowFlight;

            RenderBriefing(game);
            if (launchPanel) launchPanel.SetPreviewFocusEnabled(allowFlight);
            else if (window) window.SetFocusEnabled(allowFlight);

            // A re-show of the SAME round is the common case - the view re-renders whenever the
            // countdown ticks - and it must not pay for a rebuild. Standing a cell is a
            // multi-second veiled build; doing it per tick would make the hub unusable.
            if (sameArena && window && window.IsLive) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ShowRoundAsync(game, intensity, _cts.Token).Forget();
        }

        /// <summary>Stop drawing. Safe when nothing is showing.</summary>
        public void Hide()
        {
            _cts?.Cancel();
            ReleaseStick();
            TearDownOrbitCamera();
            CameraManager.Instance?.EndWindowedPlayerCamera();
            if (window) window.Hide();
        }

        // ── Standing the arena ───────────────────────────────────────────────────

        async UniTaskVoid ShowRoundAsync(SO_ArcadeGame game, int intensity, CancellationToken ct)
        {
            try
            {
                if (game == null)
                {
                    window?.ShowUnavailable();
                    return;
                }

                var definition = ResolveDefinition(game);
                var config = definition ? definition.ResolveCell(intensity) : null;

                if (!config)
                {
                    // Six of the sixteen pool modes author no preview definition (their arenas are
                    // built by their own controllers, not by a cell config). That is an honest
                    // "no preview", not a fault - the name and description are still on screen.
                    window?.ShowUnavailable();
                    return;
                }

                window?.ShowLoading(game.DisplayName);

                var host = ResolveCell();
                if (!host)
                {
                    CSDebug.LogError(
                        "[MaelstromPreview] The Maelstrom scene carries no Cell, so the hub has no " +
                        "arena to build the next round in. Add one (Assets/_Prefabs/Environment/" +
                        "Cell.prefab) and the preview comes up on its own.");
                    window?.ShowUnavailable();
                    return;
                }

                // Same world already standing? Nothing to build - go straight to the view. This is
                // what makes a hub re-entry on an unchanged draw free.
                if (host.Config != config)
                {
                    if (!host.RequestCellSwap(config))
                    {
                        window?.ShowUnavailable();
                        return;
                    }

                    // The swap retires the old world and blooms the new one in behind the standard
                    // veil. Waiting on the cell's own flag rather than on a fixed delay, because
                    // arena build times across this pool span an order of magnitude.
                    bool built = await WaitWhile(() => host && host.IsSwappingConfig, buildTimeoutSeconds, ct);
                    if (!built)
                        CSDebug.LogWarning(
                            $"[MaelstromPreview] {game.DisplayName}'s arena did not finish building " +
                            $"within {buildTimeoutSeconds:0}s - showing it as it stands.");
                }

                if (ct.IsCancellationRequested) return;

                BeginOrbitView();
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer round, a hide, or teardown - whoever cancelled owns the rest.
            }
        }

        ModePreviewDefinitionSO ResolveDefinition(SO_ArcadeGame game)
        {
            if (!previewLibrary)
                previewLibrary = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);

            return previewLibrary ? previewLibrary.Resolve(game.Mode) : null;
        }

        Cell ResolveCell()
        {
            if (cell) return cell;
            cell = FindFirstObjectByType<Cell>(FindObjectsInactive.Exclude);
            return cell;
        }

        // ── The orbit view (nobody flying) ───────────────────────────────────────

        void BeginOrbitView()
        {
            var texture = window ? window.LiveTexture : null;
            if (!texture) return;

            EnsureOrbitCamera();
            if (!_orbitCamera) return;

            _orbitCamera.targetTexture = texture;
            _orbitCamera.enabled = true;
            FrameArena(0f);

            // Only now does the gameplay camera stop drawing here: the surface must never have a
            // frame with nobody rendering into it.
            CameraManager.Instance?.EndWindowedPlayerCamera();

            window?.GoLive();
        }

        void EnsureOrbitCamera()
        {
            if (_orbitCamera) return;

            var go = new GameObject("MaelstromPreviewCamera");
            go.transform.SetParent(transform, false);
            _orbitCamera = go.AddComponent<Camera>();

            AdoptGameCameraSettings(_orbitCamera);
            _orbitCamera.nearClipPlane = 1f;

            // Sized for the arena, not for a backdrop a few units away: the template camera's far
            // plane is authored for the scene it lives in and clips a whole cell out of frame.
            _orbitCamera.farClipPlane = 20000f;
        }

        void TearDownOrbitCamera()
        {
            if (!_orbitCamera) return;
            _orbitCamera.targetTexture = null;
            _orbitCamera.enabled = false;
        }

        /// <summary>
        /// Draw the way the game draws. A bare <c>AddComponent&lt;Camera&gt;</c> comes up with
        /// URP's defaults rather than the project's - no post-processing, no anti-aliasing, SDR -
        /// so the arena would look visibly worse in the window than it does the moment the round
        /// loads. Copying the <see cref="UniversalAdditionalCameraData"/> is the load-bearing
        /// half: image quality lives there, not on <see cref="Camera"/>.
        /// </summary>
        static void AdoptGameCameraSettings(Camera target)
        {
            var source = Camera.main;
            if (!source) return;

            target.clearFlags = source.clearFlags;
            target.backgroundColor = source.backgroundColor;
            target.fieldOfView = source.fieldOfView;
            target.cullingMask = source.cullingMask;
            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;

            if (!source.TryGetComponent(out UniversalAdditionalCameraData from)) return;

            var to = target.GetUniversalAdditionalCameraData();
            if (!to) return;

            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.antialiasingQuality = from.antialiasingQuality;
            to.renderShadows = from.renderShadows;
            to.volumeLayerMask = from.volumeLayerMask;
        }

        void FrameArena(float advanceDegrees)
        {
            if (!_orbitCamera) return;

            _orbitAngle = Mathf.Repeat(_orbitAngle + advanceDegrees, 360f);

            var host = ResolveCell();
            var centre = host ? host.transform.position : Vector3.zero;
            float radius = FramingRadius(host);

            var offset = Quaternion.Euler(0f, _orbitAngle, 0f) *
                         new Vector3(0f, radius * 0.35f, -radius * ArenaFramingFactor);

            var t = _orbitCamera.transform;
            t.position = centre + offset;
            t.rotation = Quaternion.LookRotation((centre - t.position).normalized, Vector3.up);
        }

        /// <summary>
        /// How big the arena is, for framing - re-read every tick rather than sampled once.
        ///
        /// <para><c>Cell.MembraneRadius</c> returns 0 until the membrane has actually SPAWNED, so a
        /// camera placed the instant a build reports finished lands at a fallback distance with the
        /// arena's size unknown and shows the skybox. Re-reading corrects the framing the moment
        /// the membrane appears, which is the same fix <c>ModePreviewArena</c> already carries and
        /// the same one the connecting panel's preview needed.</para>
        /// </summary>
        static float FramingRadius(Cell host)
        {
            if (host)
            {
                float membrane = host.MembraneRadius;
                if (membrane > 1f) return membrane;

                float nucleus = host.ExpectedNucleusWorldRadius;
                if (nucleus > 1f) return nucleus * NucleusFramingMultiple;
            }
            return DefaultFramingRadius;
        }

        const float DefaultFramingRadius = 1200f;
        const float NucleusFramingMultiple = 3f;

        /// <summary>Well outside the membrane, so the whole place is in frame with air around it.</summary>
        const float ArenaFramingFactor = 1.95f;

        // ── Flying (the player tapped in) ────────────────────────────────────────

        void HandleFocusRequested()
        {
            if (!_allowFlight || _flying) return;

            var vessel = LocalVessel();
            if (vessel == null || vessel.VesselStatus == null)
            {
                if (!_warnedNoVessel)
                {
                    _warnedNoVessel = true;
                    CSDebug.LogError(
                        "[MaelstromPreview] Nobody's vessel is in the hub, so there is nothing to " +
                        "fly. The Maelstrom scene needs the standard spawn pair - a " +
                        "MaelstromHubVesselInitializer beside a ClientPlayerVesselInitializer, " +
                        "exactly as every other multiplayer scene carries one. The arena view " +
                        "works without it; the tap-to-play does not.");
                }
                window?.ReleaseFocus();
                return;
            }

            var manager = CameraManager.Instance;
            var texture = window ? window.LiveTexture : null;
            if (!manager || !texture) return;

            // Gameplay camera takes the surface FIRST, then the orbit camera lets go.
            if (manager.BeginWindowedPlayerCamera(vessel.VesselStatus.CameraFollowTarget, texture) == null)
            {
                window?.ReleaseFocus();
                return;
            }
            TearDownOrbitCamera();

            _flying = true;
            vessel.ToggleAIPilot(false);
            LocalPlayer()?.InputController?.SetPause(false);

            // The pad flies the ship, so it stops driving the UI at the same moment. This is the
            // EventSystem half; anything that polls a device directly checks
            // ModePreviewWindow.AnyHasFocus instead.
            if (EventSystem.current)
            {
                _navigationWasEnabled = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = false;
            }

            window?.GrantFocus();
        }

        void HandleFocusReleased()
        {
            if (EventSystem.current)
                EventSystem.current.sendNavigationEvents = _navigationWasEnabled;

            if (!_flying) return;
            ReleaseStick();

            // Back to looking at it. Same ordering rule as the way in.
            BeginOrbitView();
        }

        void ReleaseStick()
        {
            if (!_flying) return;
            _flying = false;

            var player = LocalPlayer();
            player?.InputController?.SetPause(true);

            var vessel = player?.Vessel;
            if (vessel != null && vessel is UnityEngine.Object o && o)
                vessel.ToggleAIPilot(true);
        }

        IPlayer LocalPlayer()
        {
            if (gameData != null && gameData.LocalPlayer != null) return gameData.LocalPlayer;

            var nm = NetworkManager.Singleton;
            var obj = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (obj != null && obj.TryGetComponent<Player>(out var p)) return p;
            return null;
        }

        IVessel LocalVessel()
        {
            var vessel = LocalPlayer()?.Vessel;
            return vessel is UnityEngine.Object o && o ? vessel : null;
        }

        // ── Briefing ─────────────────────────────────────────────────────────────

        void RenderBriefing(SO_ArcadeGame game)
        {
            // The panel is the main menu's own name + description block, so binding it IS "the
            // name and description read like the main menu". Note Bind is all we call: Show() and
            // Hide() toggle the panel's own GameObject, which is the object THIS component lives
            // on - hiding it would stop the host that is meant to be driving the preview.
            if (launchPanel)
            {
                launchPanel.Bind(game, _intensity);
                launchPanel.HideObjective();
                return;
            }

            if (modeNameText)
            {
                string name = game != null ? game.DisplayName.ToUpperInvariant() : "DRAWING…";
                bool changed = modeNameText.text != name;
                modeNameText.text = name;

                // The draw landing is the one moment in the hub where something genuinely new
                // appears, so it is the one that gets the flourish - and only when the name
                // actually changed, or every re-show would re-type it.
                if (changed && game != null) MaelstromTransitions.Reveal(modeNameText);
            }

            if (briefing) briefing.Show(game);
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Wait while <paramref name="condition"/> holds, up to <paramref name="timeoutSeconds"/>.
        /// Returns false on timeout so the caller can say so rather than hanging on a build that
        /// never reports finished.
        /// </summary>
        static async UniTask<bool> WaitWhile(Func<bool> condition, float timeoutSeconds, CancellationToken ct)
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (condition())
            {
                if (Time.unscaledTime > deadline) return false;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            return true;
        }
    }
}
