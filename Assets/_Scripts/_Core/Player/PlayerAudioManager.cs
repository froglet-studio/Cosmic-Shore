using StarWriter.Core.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAudioManager : MonoBehaviour
{
    [SerializeField] GameObject shipExplosionSFX;

    private void OnEnable()
    {
        //TODO: this should be hooked up to OnDeath instead in case we want to decouple fuel depletion from death in a different game mode (for example)
        DeathEvents.OnDeathBegin += OnShipExplosion;
    }

    private void OnDisable()
    {
        DeathEvents.OnDeathBegin -= OnShipExplosion;
    }

    private void OnShipExplosion()
    {
        AudioSystem.Instance.StopAllSongs();

        AudioSource audioSource = shipExplosionSFX.GetComponent<AudioSource>();
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
    }
}
