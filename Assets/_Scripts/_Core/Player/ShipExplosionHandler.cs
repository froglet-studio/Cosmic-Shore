using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class ShipExplosionHandler : MonoBehaviour
{
    [SerializeField] Material explosiveMaterial;
    [SerializeField] float maxExplosionRadius = 0f;
    [SerializeField] float explosionRate = 12f;

    public delegate void OnExplosionCompletionEvent();
    public static event OnExplosionCompletionEvent onExplosionCompletion;

    private void OnEnable()
    {
        FuelSystem.OnFuelEmpty += OnZeroFuel;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelEmpty -= OnZeroFuel;
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
        // PlayerAudioManager plays shipExplosion at this moment

        HapticController.PlayBlockCollisionHaptics();
        
        float explosionRadius = 0f;
        while (explosionRadius < maxExplosionRadius )
        {
            yield return null;  // Come back next frame
            explosionRadius += explosionRate * Time.deltaTime;
            explosiveMaterial.SetFloat("_explosion", explosionRadius);
        }
        onExplosionCompletion?.Invoke();
        explosiveMaterial.SetFloat("_explosion", 0);
    }

    //public IEnumerator OnRewindDeathCoroutine()
    //{
    //    // PlayerAudioManager plays shipExplosion at this moment

    //    HapticController.PlayBlockCollisionHaptics();

    //    float explosionRadius = 0f;
    //    while (explosionRadius < maxExplosionRadius)
    //    {
    //        yield return null;  // Come back next frame
    //        explosionRadius += explosionRate * Time.deltaTime;
    //        explosiveMaterial.SetFloat("_explosion", explosionRadius);
    //    }
    //    onExplosionCompletion?.Invoke();
    //    explosiveMaterial.SetFloat("_explosion", 0);
    //}

}
