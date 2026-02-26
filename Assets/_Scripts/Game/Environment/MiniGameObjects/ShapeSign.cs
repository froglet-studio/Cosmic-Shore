using TMPro;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Floating sign in the Freestyle world advertising a drawable shape.
    ///
    /// Selection is driven by a Unity UI Button on a child World Space Canvas —
    /// no trigger collider needed.
    ///
    /// Positions are set manually in the scene editor. This script locks the
    /// transform to the scene-placed position so nothing can move it.
    ///
    /// Prefab structure:
    ///   ShapeSign (this script)
    ///   ├── SignMesh          (your 3D art / quad)
    ///   └── Canvas            (Render Mode: World Space, scale 0.1)
    ///         └── Button      (calls OnSignButtonPressed via OnClick())
    ///               ├── NameLabel    (TMP_Text)
    ///               └── Description (TMP_Text, optional)
    /// </summary>
    public class ShapeSign : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] ShapeDefinition shapeDefinition;

        [Header("Display")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text descriptionLabel;

        Vector3 _lockedPosition;
        Quaternion _lockedRotation;
        bool _selected;

        void Awake()
        {
            // Lock the scene-placed transform — nothing can override this
            _lockedPosition = transform.position;
            _lockedRotation = transform.rotation;
            ApplyDisplayData();
        }

        void OnEnable()
        {
            _selected = false;

            var btn = GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn) btn.interactable = true;
        }

        void LateUpdate()
        {
            // Enforce locked position every frame — prevents physics,
            // parent changes, or any other system from moving this sign
            transform.SetPositionAndRotation(_lockedPosition, _lockedRotation);
        }

        /// <summary>Called by SpawnableShapeSign immediately after instantiation.</summary>
        public void Initialize(ShapeDefinition definition)
        {
            shapeDefinition = definition;
            ApplyDisplayData();
        }

        /// <summary>
        /// Wire this to the Button's OnClick() event in the prefab.
        /// </summary>
        public void OnSignButtonPressed()
        {
            if (_selected) return;
            _selected = true;

            ShapeSignEvents.RaiseShapeSelected(shapeDefinition, _lockedPosition);

            var btn = GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn) btn.interactable = false;
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
        /// <summary>Fired when a player presses a shape sign button.</summary>
        public static event System.Action<ShapeDefinition, Vector3> OnShapeSelected;

        public static void RaiseShapeSelected(ShapeDefinition def, Vector3 worldPos)
        {
            OnShapeSelected?.Invoke(def, worldPos);
        }
    }
}
