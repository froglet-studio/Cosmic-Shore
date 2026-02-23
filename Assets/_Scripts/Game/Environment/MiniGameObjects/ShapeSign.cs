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

        [Header("Bob & Rotate Animation")]
        [SerializeField] float bobAmplitude = 3f;
        [SerializeField] float bobFrequency = 0.5f;
        [SerializeField] float spinSpeedDeg = 15f;

        Vector3 _basePosition;
        bool    _selected;
        float   _time;

        void Awake()
        {
            _basePosition = transform.position;
            ApplyDisplayData();
        }

        void Update()
        {
            if (_selected) return;

            _time += Time.deltaTime;

            // Bob up and down
            transform.position = _basePosition +
                Vector3.up * (Mathf.Sin(_time * bobFrequency * Mathf.PI * 2f) * bobAmplitude);

            // Gentle spin
            transform.Rotate(Vector3.up, spinSpeedDeg * Time.deltaTime, Space.World);
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

            ShapeSignEvents.RaiseShapeSelected(shapeDefinition, transform.position);

            // Disable the button so it can't fire again
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