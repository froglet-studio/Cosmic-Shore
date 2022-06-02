using UnityEngine;
using StarWriter.Core;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class GyroToggleSynchronizer : MonoBehaviour
    {
        [SerializeField]
        private RectTransform handleRectTransform;
        private Toggle toggle;

        private Vector3 handleDisplacement = new Vector3(20, 0, 0);

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            
        }
        private void Start()
        {
            toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("isGyroEnabled") == 1);
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

