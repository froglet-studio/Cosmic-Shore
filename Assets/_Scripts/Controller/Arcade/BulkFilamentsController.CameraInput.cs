using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void UpdateCamera()
        {
            if (!_mainCamera)
                EnsureMainCamera();
            if (!_mainCamera || _filaments.Count == 0 || _vessel == null)
                return;

            var filament = _filaments[Mathf.Clamp(_currentFilamentIndex, 0, _filaments.Count - 1)];
            Vector3 vesselPosition = _vessel.Transform.position;
            Vector3 chasePosition = vesselPosition - filament.Direction * 120f + filament.Up * 48f;
            float gap = PlayerRouteDistance - _naniteRouteDistance;
            float naniteLook = Mathf.Clamp01((naniteCatchBuffer * 1.35f - gap) / naniteCatchBuffer);
            float pitchOffset = Mathf.Sin(_cameraLookPitch * Mathf.Deg2Rad) * 220f;
            Vector3 lookTarget = vesselPosition + filament.Direction * 170f + Vector3.up * pitchOffset;
            lookTarget -= Vector3.up * (naniteLook * 55f);

            float positionLerp = 4.5f;
            if (_cameraIntroTimer > 0f)
            {
                float intro01 = 1f - Mathf.Clamp01(_cameraIntroTimer / Mathf.Max(0.01f, introCameraDuration));
                float eased = intro01 * intro01 * (3f - 2f * intro01);
                Vector3 introStart = vesselPosition - filament.Direction * 260f - Vector3.up * 210f + filament.Side * 120f;
                Vector3 introLook = Vector3.Lerp(vesselPosition - Vector3.up * 140f, lookTarget + Vector3.up * 120f, eased);
                chasePosition = Vector3.Lerp(introStart, chasePosition, eased);
                lookTarget = introLook;
                _cameraIntroTimer = Mathf.Max(0f, _cameraIntroTimer - Time.deltaTime);
                positionLerp = 3.5f;
            }

            _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, chasePosition, Time.deltaTime * positionLerp);
            Quaternion rotation = Quaternion.LookRotation(lookTarget - _mainCamera.transform.position, Vector3.up);
            _mainCamera.transform.rotation = Quaternion.Slerp(_mainCamera.transform.rotation, rotation, Time.deltaTime * 6f);
        }

        void EnsureMainCamera()
        {
            if (_mainCamera && _mainCamera.enabled)
            {
                EnsureSingleAudioListener();
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera)
            {
                _mainCamera.enabled = true;
                EnsureSingleAudioListener();
                return;
            }

            if (!_runtimeRoot)
                return;

            var cameraObject = new GameObject("Bulk Filaments Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(_runtimeRoot.transform, false);
            cameraObject.transform.position = new Vector3(0f, -80f, -360f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.up * 60f - cameraObject.transform.position, Vector3.up);

            _mainCamera = cameraObject.AddComponent<Camera>();
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.005f, 0f, 0.02f, 1f);
            _mainCamera.fieldOfView = 76f;
            _mainCamera.nearClipPlane = 0.05f;
            _mainCamera.farClipPlane = Mathf.Max(1000f, _targetTransfers * filamentRisePerTransfer + tubeRadius * 4f);
            _mainCamera.depth = 5f;
            _mainCamera.targetDisplay = 0;
            _mainCamera.enabled = true;
            EnsureSingleAudioListener();
        }

        Vector2 ReadOrbitInput()
        {
#if UNITY_EDITOR
            if (_editorQaInputActive)
                return _editorQaOrbitInput;
#endif
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
                return new Vector2(Gamepad.current.rightStick.ReadValue().x, 0f);

            if (Keyboard.current != null)
            {
                float x = 0f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                return new Vector2(x, 0f);
            }
#endif
            return Vector2.zero;
        }

        float ReadThrottleInput()
        {
#if UNITY_EDITOR
            if (_editorQaInputActive)
                return _editorQaThrottleInput;
#endif
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
                return Gamepad.current.rightStick.ReadValue().y;

            if (Keyboard.current != null)
            {
                float y = 0f;
                if (Keyboard.current.wKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed) y -= 1f;
                return y;
            }
#endif
            return 0f;
        }

        Vector2 ReadCameraLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
                return Gamepad.current.leftStick.ReadValue();

            if (Keyboard.current != null)
            {
                float y = 0f;
                if (Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.downArrowKey.isPressed) y -= 1f;
                return new Vector2(0f, y);
            }
#endif
            return Vector2.zero;
        }

        bool ReadLatchPressed()
        {
#if UNITY_EDITOR
            if (ConsumeEditorQaLatchPressed())
                return true;
#endif
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null &&
                (Gamepad.current.rightTrigger.wasPressedThisFrame || Gamepad.current.leftTrigger.wasPressedThisFrame))
                return true;

            if (Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                return true;
#endif
            return false;
        }
    }
}
