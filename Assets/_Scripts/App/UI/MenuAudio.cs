using CosmicShore.App.Systems.Audio;
using UnityEngine;

namespace CosmicShore.App.UI
{
    public class MenuAudio : MonoBehaviour
    {
        [SerializeField] AudioClip audioClip;

        public void PlayAudio()
        {
            AudioSystem.Instance.PlaySFXClip(audioClip);
        }
    }
}