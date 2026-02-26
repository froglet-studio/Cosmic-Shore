using TMPro;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Trigger sign that starts shape-drawing mode when the vessel flies through its collider.
    /// Same activation pattern as ModeSelectTrigger. Billboards toward the player camera.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ShapeSign : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] ShapeDefinition shapeDefinition;

        [Header("Display")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text descriptionLabel;

        Vector3 _lockedPosition;
        bool _triggered;
        Transform _cameraTransform;

        void Start()
        {
            _lockedPosition = transform.position;
            GetComponent<Collider>().isTrigger = true;
            ApplyDisplayData();
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

            // Lock position, billboard toward the player camera
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
            ShapeSignEvents.RaiseShapeSelected(shapeDefinition, _lockedPosition);
        }

        /// <summary>Called by SpawnableShapeSign immediately after instantiation.</summary>
        public void Initialize(ShapeDefinition definition)
        {
            shapeDefinition = definition;
            ApplyDisplayData();
        }

        public void ResetTrigger()
        {
            _triggered = false;
            gameObject.SetActive(true);
        }

        void ApplyDisplayData()
        {
            if (shapeDefinition == null) return;
            if (nameLabel)        nameLabel.text        = shapeDefinition.shapeName;
            if (descriptionLabel) descriptionLabel.text  = shapeDefinition.description;
        }
    }

    /// <summary>
    /// Static event bus — keeps ShapeSign and ShapeDrawingManager fully decoupled.
    /// </summary>
    public static class ShapeSignEvents
    {
        public static event System.Action<ShapeDefinition, Vector3> OnShapeSelected;

        public static void RaiseShapeSelected(ShapeDefinition def, Vector3 worldPos)
        {
            OnShapeSelected?.Invoke(def, worldPos);
        }
    }
}
