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
        FuelSystem.zeroFuel += OnShipExplosion;
    }

    private void OnDisable()
    {
        FuelSystem.zeroFuel -= OnShipExplosion;
    }

    private void OnShipExplosion()
    {
        AudioSource audioSource = shipExplosionSFX.GetComponent<AudioSource>();
        AudioClip clip = audioSource.clip;
        audioSource.PlayOneShot(clip);
    }

   
}
