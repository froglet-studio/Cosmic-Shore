using CosmicShore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class SliderInitializer : MonoBehaviour
    {
        [SerializeField] GameSetting.PlayerPrefKeys PlayerPrefKey;
        [SerializeField] Slider Slider;

        void Start()
        {
            float value = PlayerPrefs.HasKey(PlayerPrefKey.ToString()) ? PlayerPrefs.GetFloat(PlayerPrefKey.ToString()) : 1;
            Slider.SetValueWithoutNotify(value);
        }
    }
}