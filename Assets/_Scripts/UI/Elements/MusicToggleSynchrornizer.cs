using UnityEngine;
using StarWriter.Core;
using UnityEngine.UI;

namespace StarWriter.UI
{
    public class MusicToggleSynchrornizer : MonoBehaviour
    {
        private bool isMuted;

        [SerializeField]
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();

            if (PlayerPrefs.HasKey("isMuted"))
            {
                if (PlayerPrefs.GetInt("isMuted") == 0)
                {
                    isMuted = false;
                }
                else
                {
                    isMuted = true;
                }
                toggle.isOn = isMuted;
            }
            else //if PlayerPrefs is null for some reason
            {
                PlayerPrefs.SetInt("isMuted", 0);
                toggle.isOn = isMuted = false;
                PlayerPrefs.Save();
            }
        }
    }
}
