using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipVisualEffects : MonoBehaviour
{

    [SerializeField]
    private Material explosiveMaterial;
    [SerializeField]
    private float maxExplosionRadius = 0f;

    public delegate void OnExplosionCompletionEvent();
    public static event OnExplosionCompletionEvent onExplosionCompletion;

    private void OnEnable()
    {
        FuelSystem.zeroFuel += OnZeroFuel;
    }

    private void OnDisable()
    {
        FuelSystem.zeroFuel -= OnZeroFuel;
    }

    private void Start()
    {
        explosiveMaterial.SetFloat("_explosion", 0);
    }

    private void OnZeroFuel()
    {
        StartCoroutine(OnDeathShipExplosionCoroutine());
    }

    public IEnumerator OnDeathShipExplosionCoroutine()
    {
        HapticController.PlayBlockCollisionHaptics();

        // Play SFX sound //TODO for John to wire up end game sound/song and get from TIM. Consider sending an event to jukebox instead
        //AudioSource audioSource = GetComponent<AudioSource>();
        //audioSource.PlayOneShot(audioSource.clip);

        float explosionRadius = 0f;
        while (explosionRadius < maxExplosionRadius )
        {
            yield return new WaitForSeconds(.02f);
            explosionRadius += 20f;
            explosiveMaterial.SetFloat("_explosion", explosionRadius);
        }
        onExplosionCompletion?.Invoke();
        explosiveMaterial.SetFloat("_explosion", 0);
    }
    
}
