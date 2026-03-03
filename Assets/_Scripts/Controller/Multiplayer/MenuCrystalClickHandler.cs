using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Toggles between menu state (autopilot + menu UI) and freestyle state
    /// (player control + freestyle UI) on Menu_Main.
    ///
    /// Operates exclusively on <see cref="GameDataSO.LocalPlayer"/> — in multiplayer,
    /// each client has its own instance controlling only the locally-owned vessel.
    /// Other clients' vessels are unaffected by this toggle.
    ///
    /// Raises SOAP transition bracket events via <see cref="MenuFreestyleEventsContainerSO"/>
    /// so decoupled systems (<see cref="Core.MainMenuController"/>, ScreenSwitcher,
    /// MainMenuCameraController, MenuMiniGameHUD) can react without direct references.
    ///
    /// Transition is triggered externally via <see cref="ToggleTransition"/> (e.g. from a UI button).
    /// </summary>
    public class MenuCrystalClickHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;

        [Inject] MenuFreestyleEventsContainerSO freestyleEvents;

        [Header("Menu UI")]
        [Tooltip("CanvasGroups to fade OUT when entering freestyle (menu chrome, nav bar, etc).")]
        [SerializeField] CanvasGroup[] menuCanvasGroups;

        [Header("Freestyle UI")]
        [Tooltip("CanvasGroups to fade IN when entering freestyle (vessel HUD, freestyle controls, etc).")]
        [SerializeField] CanvasGroup[] freestyleCanvasGroups;

        [Header("Settings")]
        [SerializeField] float fadeDuration = 0.5f;

        [SerializeField, Tooltip("How long the camera blend takes (should match MainMenuCameraController). " +
                                 "The toggle is blocked for this duration to prevent click spam.")]
        float cameraTransitionDuration = 2f;

        bool _isInFreestyle;
        bool _isTransitioning;
        CancellationTokenSource _cts;

        // Saved menu alphas so we restore to the pre-freestyle state
        // (e.g. ArcadeScreen at 0 stays at 0, not forced to 1).
        float[] _savedMenuAlphas;

        /// <summary>Whether the menu is currently in freestyle state.</summary>
        public bool IsInFreestyle => _isInFreestyle;

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
            _isTransitioning = false; // Reset in case a previous transition was cancelled mid-flight
        }

        void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void Start()
        {
            // Freestyle UI starts hidden
            ApplyCanvasGroupState(freestyleCanvasGroups, 0f);
        }

        /// <summary>
        /// Toggles between menu and freestyle states.
        /// Safe to call from a UI Button — silently ignored while transitioning,
        /// before the local player vessel is ready, or if the local player does
        /// not own their vessel (multiplayer ownership guard).
        /// </summary>
        public void ToggleTransition()
        {
            if (_isTransitioning) return;

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel == null) return;

            // Only the owner of the vessel can toggle their own freestyle state.
            // In multiplayer, LocalPlayer is always the locally-owned player,
            // but guard explicitly in case of edge cases.
            if (!localPlayer.IsLocalUser) return;

            if (_isInFreestyle)
                TransitionToMenu().Forget();
            else
                TransitionToFreestyle().Forget();
        }

        #region Transitions

        async UniTaskVoid TransitionToFreestyle()
        {
            _isTransitioning = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            // In multiplayer, avoid touching Time.timeScale — it would freeze all
            // local rendering including other players' vessels. Only unpause for
            // single-player (local host with no remote clients).
            if (!IsMultiplayerSession())
                PauseSystem.TogglePauseGame(false);

            player.Vessel.ToggleAIPilot(false);
            player.InputController.SetPause(false);
            _isInFreestyle = true;

            // Save current menu alphas so we can restore them exactly when exiting freestyle.
            // This preserves hidden panels (e.g. ArcadeScreen at alpha 0).
            _savedMenuAlphas = CaptureAlphas(menuCanvasGroups);

            // Raise SOAP event early so the camera blend and HUD setup start immediately.
            // The UI fade and camera blend then run in parallel.
            freestyleEvents.OnGameStateTransitionStart.Raise();

            // Run UI fade and camera transition duration in parallel.
            // _isTransitioning stays true until both complete — prevents click spam.
            await UniTask.WhenAll(
                FadeBetweenStates(menuAlpha: 0f, freestyleAlpha: 1f, ct),
                UniTask.Delay((int)(cameraTransitionDuration * 1000),
                              ignoreTimeScale: true, cancellationToken: ct));

            freestyleEvents.OnGameStateTransitionEnd.Raise();
            _isTransitioning = false;
        }

        async UniTaskVoid TransitionToMenu()
        {
            _isTransitioning = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            player.InputController.SetPause(true);
            player.Vessel.ToggleAIPilot(true);

            // Raise SOAP event early so the camera blend starts immediately.
            // Camera controller captures CM PlayerCam position as a static snapshot,
            // then blends back to orbit — runs in parallel with the UI fade.
            freestyleEvents.OnMenuStateTransitionStart.Raise();

            // Run UI fade and camera transition duration in parallel.
            // FadeToSavedMenuAlphas restores each menu canvas group to its pre-freestyle
            // alpha (not blindly to 1, which would force-show hidden panels like ArcadeScreen).
            // _isTransitioning stays true until both complete — prevents click spam.
            await UniTask.WhenAll(
                FadeToSavedMenuAlphas(ct),
                UniTask.Delay((int)(cameraTransitionDuration * 1000),
                              ignoreTimeScale: true, cancellationToken: ct));

            freestyleEvents.OnMenuStateTransitionEnd.Raise();
            _isInFreestyle = false;
            _isTransitioning = false;
        }

        #endregion

        #region Multiplayer Helpers

        /// <summary>
        /// Returns true when a NetworkManager is active and has remote clients connected,
        /// indicating a multiplayer party session (not just a local host).
        /// </summary>
        static bool IsMultiplayerSession()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return false;
            return nm.ConnectedClientsIds.Count > 1;
        }

        #endregion

        #region UI Fade

        /// <summary>
        /// Fades menu canvas groups back to their saved pre-freestyle alphas,
        /// and freestyle groups to 0.
        /// </summary>
        async UniTask FadeToSavedMenuAlphas(CancellationToken ct)
        {
            if (fadeDuration <= 0f)
            {
                ApplyCanvasGroupStates(menuCanvasGroups, _savedMenuAlphas);
                ApplyCanvasGroupState(freestyleCanvasGroups, 0f);
                return;
            }

            float[] menuStartAlphas = CaptureAlphas(menuCanvasGroups);
            float[] freestyleStartAlphas = CaptureAlphas(freestyleCanvasGroups);
            float[] menuTargets = _savedMenuAlphas ?? CaptureAlphas(menuCanvasGroups);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                LerpCanvasGroupAlphasToTargets(menuCanvasGroups, menuStartAlphas, menuTargets, t);
                LerpCanvasGroupAlphas(freestyleCanvasGroups, freestyleStartAlphas, 0f, t);

                await UniTask.Yield(ct);
            }

            ApplyCanvasGroupStates(menuCanvasGroups, menuTargets);
            ApplyCanvasGroupState(freestyleCanvasGroups, 0f);
        }

        async UniTask FadeBetweenStates(float menuAlpha, float freestyleAlpha, CancellationToken ct)
        {
            if (fadeDuration <= 0f)
            {
                ApplyCanvasGroupState(menuCanvasGroups, menuAlpha);
                ApplyCanvasGroupState(freestyleCanvasGroups, freestyleAlpha);
                return;
            }

            float[] menuStartAlphas = CaptureAlphas(menuCanvasGroups);
            float[] freestyleStartAlphas = CaptureAlphas(freestyleCanvasGroups);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                LerpCanvasGroupAlphas(menuCanvasGroups, menuStartAlphas, menuAlpha, t);
                LerpCanvasGroupAlphas(freestyleCanvasGroups, freestyleStartAlphas, freestyleAlpha, t);

                await UniTask.Yield(ct);
            }

            ApplyCanvasGroupState(menuCanvasGroups, menuAlpha);
            ApplyCanvasGroupState(freestyleCanvasGroups, freestyleAlpha);
        }

        static float[] CaptureAlphas(CanvasGroup[] groups)
        {
            if (groups is not { Length: > 0 }) return System.Array.Empty<float>();

            var alphas = new float[groups.Length];
            for (int i = 0; i < groups.Length; i++)
                alphas[i] = groups[i] ? groups[i].alpha : 0f;
            return alphas;
        }

        static void LerpCanvasGroupAlphas(CanvasGroup[] groups, float[] startAlphas, float targetAlpha, float t)
        {
            if (groups is not { Length: > 0 }) return;

            for (int i = 0; i < groups.Length; i++)
            {
                if (!groups[i]) continue;
                groups[i].alpha = Mathf.Lerp(startAlphas[i], targetAlpha, t);
            }
        }

        static void LerpCanvasGroupAlphasToTargets(CanvasGroup[] groups, float[] startAlphas, float[] targetAlphas, float t)
        {
            if (groups is not { Length: > 0 }) return;

            for (int i = 0; i < groups.Length; i++)
            {
                if (!groups[i]) continue;
                float target = (targetAlphas != null && i < targetAlphas.Length) ? targetAlphas[i] : 1f;
                groups[i].alpha = Mathf.Lerp(startAlphas[i], target, t);
            }
        }

        static void ApplyCanvasGroupState(CanvasGroup[] groups, float alpha)
        {
            if (groups is not { Length: > 0 }) return;

            foreach (var cg in groups)
            {
                if (!cg) continue;
                cg.alpha = alpha;
                cg.blocksRaycasts = alpha > 0.01f;
                cg.interactable = alpha > 0.01f;
            }
        }

        static void ApplyCanvasGroupStates(CanvasGroup[] groups, float[] alphas)
        {
            if (groups is not { Length: > 0 }) return;

            for (int i = 0; i < groups.Length; i++)
            {
                if (!groups[i]) continue;
                float alpha = (alphas != null && i < alphas.Length) ? alphas[i] : 1f;
                groups[i].alpha = alpha;
                groups[i].blocksRaycasts = alpha > 0.01f;
                groups[i].interactable = alpha > 0.01f;
            }
        }

        #endregion
    }
}
