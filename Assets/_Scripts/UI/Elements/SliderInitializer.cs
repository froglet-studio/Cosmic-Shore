using CosmicShore.Game.Managers;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Game.Settings;
namespace CosmicShore.UI.Elements
{
    public class SliderInitializer : MonoBehaviour
    {
        [SerializeField] GameSetting.PlayerPrefKeys PlayerPrefKey;
        [SerializeField] Slider Slider;

        void Start()
        {
            float value = PlayerPrefs.HasKey(PlayerPrefKey.ToString()) ? PlayerPrefs.GetFloat(PlayerPrefKey.ToString()) : 1;
            //Slider.SetValueWithoutNotify(value);
            Slider.value = value;
        }

        public void UpdateSliderValue(float value)
        {
            PlayerPrefs.SetFloat(PlayerPrefKey.ToString(), value);
            PlayerPrefs.Save();
        }
    }
}