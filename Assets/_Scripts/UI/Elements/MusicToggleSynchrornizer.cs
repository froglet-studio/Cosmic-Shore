using UnityEngine;
using StarWriter.Core;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class MusicToggleSynchrornizer : MonoBehaviour
    {
        [SerializeField]
        private RectTransform handleRectTransform;
        private Toggle toggle;

        private Vector3 handleDisplacement = new Vector3(20, 0, 0);

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Toggled);
        }

        private void Start()
        {
            toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("isAudioEnabled") == 1);
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
