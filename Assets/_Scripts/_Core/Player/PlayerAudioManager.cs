using StarWriter.Core.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAudioManager : MonoBehaviour
{
    [SerializeField]
    private GameObject shipExplosionSFX;

    private void OnEnable()
    {
        FuelSystem.OnFuelEmpty += OnShipExplosion;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelEmpty -= OnShipExplosion;
    }

    private void OnShipExplosion()
    {
        AudioSource audioSource = shipExplosionSFX.GetComponent<AudioSource>();
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
        //AudioClip clip = audioSource.clip;
        //audioSource.PlayOneShot(clip);
    }
}
