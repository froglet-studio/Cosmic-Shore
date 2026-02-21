using CosmicShore.Systems.Audio;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.App.UI
{
    public class MenuAudio : MonoBehaviour
    {
        [SerializeField] MenuAudioCategory category;

        [Inject] AudioSystem audioSystem;

        public void PlayAudio()
        {
            audioSystem.PlayMenuAudio(category);
        }
    }
}
