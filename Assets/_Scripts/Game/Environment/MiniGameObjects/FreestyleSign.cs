using TMPro;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Trigger sign that starts the normal segment-spawner gameplay
    /// when the vessel or a projectile flies through its collider.
    /// Same activation pattern as ModeSelectTrigger.
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
