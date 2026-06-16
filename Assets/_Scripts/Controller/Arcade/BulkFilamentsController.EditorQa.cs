#if UNITY_EDITOR
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        bool _editorQaInputActive;
        bool _editorQaLatchPressed;
        float _editorQaThrottleInput;
        Vector2 _editorQaOrbitInput;

        public bool EditorQaIsRunning => _isRunning && !_turnFinished;
        public bool EditorQaTurnFinished => _turnFinished;
        public int EditorQaSuccessfulTransfers => _successfulTransfers;
        public int EditorQaCrystalsCollected => _crystalsCollected;
        public float EditorQaDistanceToTransfer => _filaments.Count == 0 ? 0f : CurrentFilament.TransferDistance - _distanceOnFilament;
        public float EditorQaLatchWindow => CurrentLatchWindow();

        public void SetEditorQaInput(float orbitX, float throttle, bool latchPressed)
        {
            _editorQaInputActive = true;
            _editorQaOrbitInput = new Vector2(Mathf.Clamp(orbitX, -1f, 1f), 0f);
            _editorQaThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
            _editorQaLatchPressed |= latchPressed;
        }

        public bool ConsumeEditorQaLatchPressed()
        {
            if (!_editorQaInputActive || !_editorQaLatchPressed)
                return false;

            _editorQaLatchPressed = false;
            return true;
        }

        public void ClearEditorQaInput()
        {
            _editorQaInputActive = false;
            _editorQaLatchPressed = false;
            _editorQaThrottleInput = 0f;
            _editorQaOrbitInput = Vector2.zero;
        }
    }
}
#endif
