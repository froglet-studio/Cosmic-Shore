using StarWriter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class ToggleSynchronizer : MonoBehaviour
    {
        [SerializeField]
        private RectTransform handleRectTransform;
        [SerializeField]
        private GameSetting.PlayerPrefKeys PlayerPrefKey;
        [SerializeField]
        private Vector3 HandleOnPosition;
        [SerializeField]
        private Vector3 HandleOffPosition;

        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Toggled);
        }

        private void Start()
        {
            toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(PlayerPrefKey.ToString()) == 1);
            Toggled(PlayerPrefs.GetInt(PlayerPrefKey.ToString()) == 1);
        }

        public void Toggled(bool status)
        {
            Debug.Log($"ToggleSynchronizer.Toggled - {name} - status: {status}");
            handleRectTransform.localPosition = status ? HandleOnPosition : HandleOffPosition;
        }

        private void OnDestroy()
        {
            toggle.onValueChanged.RemoveListener(Toggled);
        }
    }
}

