using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Toggles between menu state (autopilot + menu UI) and freestyle state
    /// (player control + freestyle UI) on Menu_Main.
    ///
    /// Raises SOAP events via <see cref="MenuFreestyleEventsContainerSO"/> so decoupled
    /// systems (<see cref="Core.MainMenuController"/> for camera switching,
    /// ScreenSwitcher, NavBar, HUD) can react without direct references.
    ///
    /// Camera switching is handled by <see cref="Core.MainMenuController"/> in response
    /// to the freestyle SOAP events.
    /// </summary>
    public class MenuCrystalClickHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;

        [Header("SOAP Events")]
        [Tooltip("Container holding OnEnterFreestyle / OnExitFreestyle SOAP events.")]
        [SerializeField] MenuFreestyleEventsContainerSO freestyleEvents;

        [Header("Menu UI")]
        [Tooltip("CanvasGroups to fade OUT when entering freestyle (menu chrome, nav bar, etc).")]
        [SerializeField] CanvasGroup[] menuCanvasGroups;

        [Header("Freestyle UI")]
        [Tooltip("CanvasGroups to fade IN when entering freestyle (vessel HUD, freestyle controls, etc).")]
        [SerializeField] CanvasGroup[] freestyleCanvasGroups;

        [Header("Settings")]
        [SerializeField] float fadeDuration = 0.5f;
        [SerializeField] float raycastDistance = 2000f;

        [Tooltip("Fraction of screen height defining the center-tap radius for returning to menu.")]
        [SerializeField, Range(0.05f, 0.3f)] float centerTapRadius = 0.12f;

        bool _isInFreestyle;
        bool _isTransitioning;
        CancellationTokenSource _cts;

        /// <summary>Whether the menu is currently in freestyle state.</summary>
        public bool IsInFreestyle => _isInFreestyle;

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
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

        void Update()
        {
            if (_isTransitioning) return;
            if (gameData.LocalPlayer?.Vessel == null) return;
            if (!DetectTap(out Vector2 screenPos)) return;

            if (_isInFreestyle)
            {
                if (IsCenterTap(screenPos))
                    TransitionToMenu().Forget();
            }
            else
            {
                if (RaycastCrystal(screenPos))
                    TransitionToFreestyle().Forget();
            }
        }

        #region Input Detection

        bool DetectTap(out Vector2 screenPos)
        {
            screenPos = default;

            if (Touchscreen.current != null)
            {
                var primaryTouch = Touchscreen.current.primaryTouch;
                if (primaryTouch.press.wasPressedThisFrame)
                {
                    screenPos = primaryTouch.position.ReadValue();
                    return !IsPointerOverUI();
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                return !IsPointerOverUI();
            }

            return false;
        }

        static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        bool IsCenterTap(Vector2 screenPos)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float radius = Screen.height * centerTapRadius;
            return Vector2.Distance(screenPos, center) <= radius;
        }

        bool RaycastCrystal(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (!cam) return false;

            var ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                return hit.collider.GetComponentInParent<Crystal>() != null;
            }

            return false;
        }

        #endregion

        #region Transitions

        async UniTaskVoid TransitionToFreestyle()
        {
            _isTransitioning = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            // Fade out menu UI, fade in freestyle UI
            await FadeBetweenStates(menuAlpha: 0f, freestyleAlpha: 1f, ct);

            PauseSystem.TogglePauseGame(false);

            player.Vessel.ToggleAIPilot(false);
            player.InputController.SetPause(false);

            _isInFreestyle = true;
            _isTransitioning = false;

            // Camera switching handled by MainMenuController via this event
            freestyleEvents.OnEnterFreestyle.Raise();
        }

        async UniTaskVoid TransitionToMenu()
        {
            _isTransitioning = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            player.InputController.SetPause(true);
            player.Vessel.ToggleAIPilot(true);

            // Camera switching handled by MainMenuController via this event
            freestyleEvents.OnExitFreestyle.Raise();

            // Fade out freestyle UI, fade in menu UI
            await FadeBetweenStates(menuAlpha: 1f, freestyleAlpha: 0f, ct);

            _isInFreestyle = false;
            _isTransitioning = false;
        }

        #endregion

        #region UI Fade

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

        #endregion
    }
}
