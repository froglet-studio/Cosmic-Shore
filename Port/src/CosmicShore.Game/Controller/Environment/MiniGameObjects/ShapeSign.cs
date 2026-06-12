using CosmicShore.Data;
// PORT Deviation (UI shell, restore when TMPro shim lands): using TMPro;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Trigger sign that starts shape-drawing mode when the vessel flies through its collider.
    /// Position, rotation, and scale are set in the editor — this script never touches them.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ShapeSign : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] ShapeDefinition shapeDefinition;

        [Header("Display")]
        // PORT Deviation (UI shell, restore when TMPro shim lands): [SerializeField] TMP_Text nameLabel;
        // PORT Deviation (UI shell, restore when TMPro shim lands): [SerializeField] TMP_Text descriptionLabel;

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
            // PORT Deviation (UI shell, restore when TMPro shim lands):
            // if (nameLabel)        nameLabel.text        = shapeDefinition.shapeName;
            // if (descriptionLabel) descriptionLabel.text  = shapeDefinition.description;
        }
    }

    /// <summary>
    /// Static event bus — keeps ShapeSign and ShapeDrawingManager fully decoupled.
    /// </summary>
    public static class ShapeSignEvents
    {
        public static event System.Action<ShapeDefinition, Vector3, Domains> OnShapeSelected;

        public static void RaiseShapeSelected(ShapeDefinition def, Vector3 worldPos, Domains shapeDomain = Domains.Blue)
        {
            OnShapeSelected?.Invoke(def, worldPos, shapeDomain);
        }
    }
}
