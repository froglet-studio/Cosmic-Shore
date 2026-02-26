using TMPro;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Floating sign in the Freestyle world that starts the normal segment-spawner gameplay.
    ///
    /// Uses a world-space UI Button for selection, same pattern as ShapeSign.
    ///
    /// Prefab structure:
    ///   FreestyleSign (this script)
    ///   ├── SignMesh          (3D art / quad)
    ///   └── Canvas            (Render Mode: World Space, scale 0.1)
    ///         └── Button      (calls OnSignButtonPressed via OnClick())
    ///               └── Label (TMP_Text)
    /// </summary>
    public class FreestyleSign : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] string displayText = "Freestyle";

        bool _selected;

        void Awake()
        {
            if (nameLabel) nameLabel.text = displayText;
        }

        void OnEnable()
        {
            _selected = false;

            var btn = GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn) btn.interactable = true;
        }

        /// <summary>
        /// Wire this to the Button's OnClick() event in the prefab.
        /// </summary>
        public void OnSignButtonPressed()
        {
            if (_selected) return;
            _selected = true;

            FreestyleSignEvents.RaiseFreestyleSelected();

            var btn = GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn) btn.interactable = false;
        }
    }

    /// <summary>
    /// Static event bus for FreestyleSign — keeps sign and controller decoupled.
    /// </summary>
    public static class FreestyleSignEvents
    {
        /// <summary>Fired when a player presses the freestyle sign button.</summary>
        public static event System.Action OnFreestyleSelected;

        public static void RaiseFreestyleSelected()
        {
            OnFreestyleSelected?.Invoke();
        }
    }
}
