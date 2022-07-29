using System.Collections;
using UnityEngine;

public class ShipVisualEffects : MonoBehaviour
{
    [SerializeField] Material explosiveMaterial;
    [SerializeField] float maxExplosionRadius = 0f;
    [SerializeField] float explosionRate = 1200f;

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
            yield return null;  // Come back next frame
            explosionRadius += explosionRate * Time.deltaTime;
            explosiveMaterial.SetFloat("_explosion", explosionRadius);
        }
        onExplosionCompletion?.Invoke();
        explosiveMaterial.SetFloat("_explosion", 0);
    }
}
