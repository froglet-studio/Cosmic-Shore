using CosmicShore.App.Systems.Audio;
using UnityEngine;

namespace CosmicShore.App.UI
{
    public class MenuAudio : MonoBehaviour
    {
        [SerializeField] MenuAudioCategory category;

        public void PlayAudio()
        {
            AudioSystem.Instance.PlayMenuAudio(category);
        }
    }
}