using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
namespace CosmicShore.UI
{
    public class ToggleSynchronizer : MonoBehaviour
    {
        [SerializeField] GameSetting.PlayerPrefKeys PlayerPrefKey;
        [SerializeField] GameObject On;
        [SerializeField] GameObject Off;

        void Start()
        {
            bool state = PlayerPrefs.HasKey(PlayerPrefKey.ToString()) && PlayerPrefs.GetInt(PlayerPrefKey.ToString()) == 1;
            On.SetVisible(state);
            Off.SetVisible(!state);
        }
    }
}