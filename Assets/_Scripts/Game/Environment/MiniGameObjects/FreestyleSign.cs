using TMPro;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Trigger sign that starts the normal segment-spawner gameplay
    /// when the vessel flies through its collider.
    /// Same activation pattern as ModeSelectTrigger. Billboards toward the player camera.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FreestyleSign : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] string displayText = "Freestyle";

        Vector3 _lockedPosition;
        bool _triggered;
        Transform _cameraTransform;

        void Start()
        {
            _lockedPosition = transform.position;
            if (nameLabel) nameLabel.text = displayText;
            GetComponent<Collider>().isTrigger = true;
        }

        void OnEnable()
        {
            _triggered = false;
        }

        void LateUpdate()
        {
            if (_cameraTransform == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                _cameraTransform = cam.transform;
            }

            var lookDir = transform.position - _cameraTransform.position;
            lookDir.y = 0f;
            var rotation = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir, Vector3.up)
                : transform.rotation;

            transform.SetPositionAndRotation(_lockedPosition, rotation);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            if (other.GetComponentInParent<VesselStatus>())
                Activate();
        }

        void Activate()
        {
            _triggered = true;
            gameObject.SetActive(false);
            FreestyleSignEvents.RaiseFreestyleSelected();
        }

        public void ResetTrigger()
        {
            _triggered = false;
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Static event bus for FreestyleSign — keeps sign and controller decoupled.
    /// </summary>
    public static class FreestyleSignEvents
    {
        public static event System.Action OnFreestyleSelected;

        public static void RaiseFreestyleSelected()
        {
            OnFreestyleSelected?.Invoke();
        }
    }
}
