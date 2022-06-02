using UnityEngine;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class ToggleSynchronizer : MonoBehaviour
    {
        [SerializeField]
        private RectTransform handleRectTransform;
        [SerializeField]
        private string PlayerPrefKey;
        [SerializeField]
        private Vector3 HandleOnPosition;
        [SerializeField]
        private Vector3 HandleOffPosition;

        private Toggle toggle;

        private Vector3 handleDisplacement = new Vector3(20, 0, 0);

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Toggled);
        }

        private void Start()
        {
            toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(PlayerPrefKey) == 1);
            Toggled(PlayerPrefs.GetInt(PlayerPrefKey) == 1);
        }

        public void Toggled(bool status)
        {
            Debug.Log($"ToggleSynchronizer.Toggled - {name} - status: {status}");
            int sign = status ? 1 : -1;
            handleRectTransform.localPosition = status ? HandleOnPosition : HandleOffPosition;
        }

        private void OnDestroy()
        {
            toggle.onValueChanged.RemoveListener(Toggled);
        }
    }
}

