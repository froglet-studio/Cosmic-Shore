using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class SwitchToggle : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;

        [SerializeField] RectTransform handleRectTransform;

        Toggle toggle;
        Vector3 handleDisplacement = new Vector3(20, 0, 0);

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Toggled);
        }

        public void Toggled(bool status)
        {
            int sign = status ? 1 : -1;
            handleRectTransform.localPosition += sign * handleDisplacement;
            audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);
        }

        private void OnDestroy()
        {
            toggle.onValueChanged.RemoveListener(Toggled);
        }
    }
}