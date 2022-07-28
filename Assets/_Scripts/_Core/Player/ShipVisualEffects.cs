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

    private void OnZeroFuel()
    {
        StartCoroutine(OnDeathShipExplosionCoroutine());
    }

    public IEnumerator OnDeathShipExplosionCoroutine()
    {
        float explosionRadius = 0f;
        while (explosionRadius < maxExplosionRadius )
        {
            yield return new WaitForSeconds(.01f);
            explosionRadius += 0.1f;
            explosiveMaterial.SetFloat("_explosion", explosionRadius);
        }
        onExplosionCompletion?.Invoke();
    }
    
}
