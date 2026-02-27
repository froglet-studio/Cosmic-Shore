using CosmicShore.Core;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Toggles between menu mode (crystal camera + autopilot) and gameplay mode
    /// (vessel follow camera + player control) on Menu_Main.
    ///
    /// Menu mode: end camera looks at the crystal (already implemented in scene).
    ///   The crystal sits at screen center. Player taps the crystal → gameplay mode.
    /// Gameplay mode: player camera follows the vessel, player has control.
    ///   Player taps screen center → back to menu mode.
    /// </summary>
    public class MenuCrystalClickHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;

        [Header("Menu UI")]
        [Tooltip("CanvasGroups to fade when toggling between menu and gameplay.")]
        [SerializeField] CanvasGroup[] menuCanvasGroups;

        [Header("Settings")]
        [SerializeField] float fadeDuration = 0.5f;
        [SerializeField] float raycastDistance = 2000f;

        [Tooltip("Fraction of screen height defining the center-tap radius for returning to menu.")]
        [SerializeField, Range(0.05f, 0.3f)] float centerTapRadius = 0.12f;

        bool _isInGameplay;
        bool _isTransitioning;
        CancellationTokenSource _cts;

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

        void Update()
        {
            if (_isTransitioning) return;
            if (gameData.LocalPlayer?.Vessel == null) return;
            if (!DetectTap(out Vector2 screenPos)) return;

            if (_isInGameplay)
            {
                if (IsCenterTap(screenPos))
                    TransitionToMenu().Forget();
            }
            else
            {
                if (RaycastCrystal(screenPos))
                    TransitionToGameplay().Forget();
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

        async UniTaskVoid TransitionToGameplay()
        {
            _isTransitioning = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            // Fade out menu UI
            await FadeCanvasGroups(0f, ct);

            PauseSystem.TogglePauseGame(false);

            // Disable autopilot, give player control
            player.Vessel.ToggleAIPilot(false);
            player.InputController.SetPause(false);

            // Switch to gameplay follow camera
            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupGamePlayCameras(followTarget);
            }

            _isInGameplay = true;
            _isTransitioning = false;
        }

        async UniTaskVoid TransitionToMenu()
        {
            _isTransitioning = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            // Remove player control, re-enable autopilot
            player.InputController.SetPause(true);
            player.Vessel.ToggleAIPilot(true);

            // Switch back to crystal camera (end camera)
            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupEndCameraFollow(followTarget);
            }

            // Fade in menu UI
            await FadeCanvasGroups(1f, ct);

            _isInGameplay = false;
            _isTransitioning = false;
        }

        #endregion

        #region UI Fade

        async UniTask FadeCanvasGroups(float targetAlpha, CancellationToken ct)
        {
            if (menuCanvasGroups is not { Length: > 0 }) return;

            if (fadeDuration <= 0f)
            {
                ApplyCanvasGroupAlpha(targetAlpha);
                return;
            }

            float[] startAlphas = new float[menuCanvasGroups.Length];
            for (int i = 0; i < menuCanvasGroups.Length; i++)
                startAlphas[i] = menuCanvasGroups[i] ? menuCanvasGroups[i].alpha : 0f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                for (int i = 0; i < menuCanvasGroups.Length; i++)
                {
                    if (!menuCanvasGroups[i]) continue;
                    menuCanvasGroups[i].alpha = Mathf.Lerp(startAlphas[i], targetAlpha, t);
                }

                await UniTask.Yield(ct);
            }

            ApplyCanvasGroupAlpha(targetAlpha);
        }

        void ApplyCanvasGroupAlpha(float alpha)
        {
            foreach (var cg in menuCanvasGroups)
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
