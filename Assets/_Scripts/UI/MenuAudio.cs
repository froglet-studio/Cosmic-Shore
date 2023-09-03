using StarWriter.Core.Audio;
using UnityEngine;

public class MenuAudio : MonoBehaviour
{
    [SerializeField] AudioClip audioClip;

    public void PlayAudio()
    {
        AudioSystem.Instance.PlaySFXClip(audioClip);
    }
}