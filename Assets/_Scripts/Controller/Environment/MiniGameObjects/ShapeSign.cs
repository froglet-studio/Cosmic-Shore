using CosmicShore.Data;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Trigger sign that fires <c>ShapeSignEvents</c> when the vessel flies through its collider.
    /// The scored drawing-mode subscriber was deleted (C15); the bus still fires.
    /// Position, rotation, and scale are set in the editor - this script never touches them.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ShapeSign : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] ShapeDefinition shapeDefinition;

        [Header("Display")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text descriptionLabel;

        bool _triggered;

        void Start()
        {
            GetComponent<Collider>().isTrigger = true;
            ApplyDisplayData();
        }

        void OnEnable()
        {
            _triggered = false;
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
            ShapeSignEvents.RaiseShapeSelected(shapeDefinition, transform.position);
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
    /// Static event bus for shape-sign / spawnable-shape collisions.
    /// The scored drawing-mode subscriber was deleted (C15, 2026-08-25); the bus
    /// still fires so a future consumer can subscribe without re-coupling the signs.
    /// </summary>
    public static class ShapeSignEvents
    {
        public static event System.Action<ShapeDefinition, Vector3, Domains> OnShapeSelected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OnShapeSelected = null;

        public static void RaiseShapeSelected(ShapeDefinition def, Vector3 worldPos, Domains shapeDomain = Domains.Blue)
        {
            OnShapeSelected?.Invoke(def, worldPos, shapeDomain);
        }
    }
}
