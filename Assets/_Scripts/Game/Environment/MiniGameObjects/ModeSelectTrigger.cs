using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Game.ShapeDrawing
{
    [RequireComponent(typeof(Collider))]
    public class ModeSelectTrigger : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Assign a ShapeDefinition for Shape Mode. Leave NULL for Standard Freestyle.")]
        public ShapeDefinition ShapeToLoad;
        
        [Tooltip("Text to display in world space (e.g. 'Standard', 'Star', 'Lightning')")]
        [SerializeField] string labelText;
        [SerializeField] TMPro.TMP_Text labelMesh;

        [Header("Events")]
        public UnityEvent<ShapeDefinition> OnModeSelected;

        bool _triggered;

        void Start()
        {
            if (labelMesh) labelMesh.text = labelText;
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            // Check for Vessel collision or Projectile collision
            // Assuming Projectiles have a specific tag or component
            if (other.GetComponentInParent<VesselStatus>())
            {
                ActivateTrigger();
            }
        }

        void ActivateTrigger()
        {
            _triggered = true;
            Debug.Log($"[ModeSelectTrigger] Selected: {(ShapeToLoad ? ShapeToLoad.shapeName : "Freestyle")}");
            
            // Visual feedback: Shrink or explode the text
            // [Visual Note] Play a "Selection" sound here
            gameObject.SetActive(false); 

            OnModeSelected?.Invoke(ShapeToLoad);
        }

        // Reset for when we return to lobby
        public void ResetTrigger()
        {
            _triggered = false;
            gameObject.SetActive(true);
        }
    }
}