using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class SwitchToggle : MonoBehaviour
    {
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
        }

        private void OnDestroy()
        {
            toggle.onValueChanged.RemoveListener(Toggled);
        }
    }
}