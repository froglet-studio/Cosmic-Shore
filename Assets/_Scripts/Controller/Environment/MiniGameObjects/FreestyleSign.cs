using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Trigger sign that starts the normal segment-spawner gameplay
    /// when the vessel flies through its collider.
    /// Position, rotation, and scale are set in the editor — this script never touches them.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FreestyleSign : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] string displayText = "Freestyle";

        bool _triggered;

        void Start()
        {
            if (nameLabel) nameLabel.text = displayText;
            GetComponent<Collider>().isTrigger = true;
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
