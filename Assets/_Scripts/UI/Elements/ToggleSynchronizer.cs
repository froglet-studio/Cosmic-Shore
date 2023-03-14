using StarWriter.Core;
using UnityEngine;

namespace StarWriter.UI
{
    public class ToggleSynchronizer : MonoBehaviour
    {
        [SerializeField] GameSetting.PlayerPrefKeys PlayerPrefKey;
        [SerializeField] GameObject On;
        [SerializeField] GameObject Off;

        void Start()
        {
            bool state = PlayerPrefs.HasKey(PlayerPrefKey.ToString()) && PlayerPrefs.GetInt(PlayerPrefKey.ToString()) == 1;
            On.SetActive(state);
            Off.SetActive(!state);
        }
    }
}