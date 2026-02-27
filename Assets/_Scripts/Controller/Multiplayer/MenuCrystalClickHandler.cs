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
    /// Handles the crystal click interaction on Menu_Main. When the player taps a crystal,
    /// the menu UI fades out, the camera switches to the gameplay follow camera,
    /// autopilot disables, and the player gains vessel control.
    /// </summary>
    public class MenuCrystalClickHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;

        [Header("Menu UI")]
        [Tooltip("CanvasGroups to fade out when transitioning to gameplay.")]
        [SerializeField] CanvasGroup[] menuCanvasGroups;

        [Header("Settings")]
        [SerializeField] float fadeDuration = 0.5f;
        [SerializeField] float raycastDistance = 2000f;

        bool _hasTransitioned;
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
            if (_hasTransitioned) return;
            if (gameData.LocalPlayer?.Vessel == null) return;
            if (!DetectTap(out Vector2 screenPos)) return;
            if (!RaycastCrystal(screenPos)) return;

            TransitionToGameplay().Forget();
        }

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

        async UniTaskVoid TransitionToGameplay()
        {
            _hasTransitioned = true;
            var ct = _cts.Token;
            var player = gameData.LocalPlayer;

            // Fade out menu UI
            await FadeCanvasGroups(0f, ct);

            // Ensure game is unpaused (may be paused if on a non-HOME screen)
            PauseSystem.TogglePauseGame(false);

            // Disable autopilot and enable player input
            player.Vessel.ToggleAIPilot(false);
            player.InputController.SetPause(false);

            // Switch to gameplay camera
            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupGamePlayCameras(followTarget);
            }
        }

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
    }
}
