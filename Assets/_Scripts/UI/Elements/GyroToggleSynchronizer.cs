using UnityEngine;
using StarWriter.Core;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class GyroToggleSynchronizer : MonoBehaviour
    {
        private bool isGyroEnabled;

        [SerializeField]
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();

            if (PlayerPrefs.HasKey("isMuted"))
            {
                if (PlayerPrefs.GetInt("isMuted") == 0)
                {
                    isGyroEnabled = false;
                }
                else
                {
                    isGyroEnabled = true;
                }
                toggle.isOn = isGyroEnabled;
            }
            else
            {
                PlayerPrefs.SetInt("isMuted", 0);
                toggle.isOn = isGyroEnabled = false;
                PlayerPrefs.Save();
            }
        }
    }
}

