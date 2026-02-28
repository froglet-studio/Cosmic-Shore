using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// UI button that toggles between Menu and Freestyle mode on Menu_Main.
    /// Wire to a Button's OnClick or use Spacebar for testing.
    /// Listens to <see cref="MenuFreestyleEventsContainerSO"/> SOAP events
    /// to keep the label in sync with the current state.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ModeToggleButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] MenuCrystalClickHandler crystalClickHandler;

        [Header("SOAP Events")]
        [SerializeField] MenuFreestyleEventsContainerSO freestyleEvents;

        [Header("Label")]
        [SerializeField] TMP_Text buttonLabel;
        [SerializeField] string menuModeLabel = "Freestyle";
        [SerializeField] string freestyleModeLabel = "Menu";

        Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        void OnEnable()
        {
            freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
            freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
        }

        void OnDisable()
        {
            freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
            freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;
        }

        void Start()
        {
            UpdateLabel(crystalClickHandler.IsInFreestyle);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                OnClick();
        }

        void OnClick()
        {
            crystalClickHandler.ToggleTransition();
        }

        void HandleEnterFreestyle()
        {
            UpdateLabel(true);
        }

        void HandleExitFreestyle()
        {
            UpdateLabel(false);
        }

        void UpdateLabel(bool isFreestyle)
        {
            if (!buttonLabel) return;
            buttonLabel.text = isFreestyle ? freestyleModeLabel : menuModeLabel;
        }

        void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClick);
        }
    }
}
