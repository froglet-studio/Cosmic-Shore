using StarWriter.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class ShipExplosionHandler : MonoBehaviour
{
    [SerializeField] Material explosiveMaterial;
    [SerializeField] float maxExplosionRadius = 0f;
    [SerializeField] float explosionRate = 12f;
    private float explosionRadius;

    public delegate void OnExplosionCompletionEvent();
    public static event OnExplosionCompletionEvent onExplosionCompletion;

    private void OnEnable()
    {
        FuelSystem.OnFuelEmpty += OnZeroFuel;
        GameManager.onExtendGamePlay += OnExtendGamePlay;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelEmpty -= OnZeroFuel;
        GameManager.onExtendGamePlay -= OnExtendGamePlay;
    }

    private void Start()
    {
        explosionRadius = maxExplosionRadius;
        explosiveMaterial.SetFloat("_explosion", explosionRadius);
        StartCoroutine(OnFormShipCoroutine());
    }

    private void OnZeroFuel()
    {
        StartCoroutine(OnDeathShipExplosionCoroutine());
    }
    
    private void OnExtendGamePlay()
    {
        StartCoroutine(OnFormShipCoroutine());
    }

    public IEnumerator OnDeathShipExplosionCoroutine()
    {
        // PlayerAudioManager plays shipExplosion at this moment

        HapticController.PlayBlockCollisionHaptics();
        
        
        while (explosionRadius < maxExplosionRadius )
        {
            yield return null;  // Come back next frame
            explosionRadius += explosionRate * Time.deltaTime;
            explosiveMaterial.SetFloat("_explosion", explosionRadius);
        }
        onExplosionCompletion?.Invoke();
        //explosiveMaterial.SetFloat("_explosion", 0);
    }

    public IEnumerator OnFormShipCoroutine()
    {
        // PlayerAudioManager plays shipExplosion at this moment

        //HapticController.PlayBlockCollisionHaptics();

        //float explosionRadius = 0f;
        while (explosionRadius > 0)
        {
            yield return null;  // Come back next frame
            explosionRadius -= explosionRate * Time.deltaTime;
            explosiveMaterial.SetFloat("_explosion", explosionRadius);
        }
        //onExplosionCompletion?.Invoke();
        explosiveMaterial.SetFloat("_explosion", 0);
    }

}
