using StarWriter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class ToggleSynchronizer : MonoBehaviour
    {
        [SerializeField] RectTransform handleRectTransform;
        [SerializeField] GameSetting.PlayerPrefKeys PlayerPrefKey;
        [SerializeField] Vector3 HandleOnPosition;
        [SerializeField] Vector3 HandleOffPosition;

        Toggle toggle;

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Toggled);
        }

        void OnDestroy()
        {
            toggle.onValueChanged.RemoveListener(Toggled);
        }

        void Start()
        {
            bool state = PlayerPrefs.HasKey(PlayerPrefKey.ToString()) && PlayerPrefs.GetInt(PlayerPrefKey.ToString()) == 1;
            toggle.SetIsOnWithoutNotify(state);
            Toggled(state);
        }

        public void Toggled(bool status)
        {
            Debug.Log($"ToggleSynchronizer.Toggled - {name} - status: {status}");
            handleRectTransform.localPosition = status ? HandleOnPosition : HandleOffPosition;
        }
    }
}