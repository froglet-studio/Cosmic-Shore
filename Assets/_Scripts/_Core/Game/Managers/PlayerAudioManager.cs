using StarWriter.Core.Audio;
using UnityEngine;

public class PlayerAudioManager : MonoBehaviour
{
    [SerializeField] GameObject shipExplosionSFX;

    void OnEnable()
    {
        DeathEvents.OnDeathBegin += OnShipExplosion;
    }

    void OnDisable()
    {
        DeathEvents.OnDeathBegin -= OnShipExplosion;
    }

    void OnShipExplosion()
    {
        AudioSystem.Instance.StopAllSongs();

        AudioSource audioSource = shipExplosionSFX.GetComponent<AudioSource>();
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
    }
}