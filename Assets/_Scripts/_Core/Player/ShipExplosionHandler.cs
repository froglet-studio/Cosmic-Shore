using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class ShipExplosionHandler : MonoBehaviour
{
    [SerializeField] List<GameObject> explodableParts;
    [SerializeField] Material explosiveMaterial;
    [SerializeField] float maxExplosionRadius = 0f;
    [SerializeField] float explosionRate = 12f;
    private float explosionRadius;
    private List<Material> explosiveMaterials;

    public delegate void OnExplosionCompletionEvent();
    public static event OnExplosionCompletionEvent onExplosionCompletion;

    private void OnEnable()
    {
        FuelSystem.OnFuelEmpty += DoShipExplosionEffect;
        GameManager.onExtendGamePlay += DoShipReformingEffect;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelEmpty -= DoShipExplosionEffect;
        GameManager.onExtendGamePlay -= DoShipReformingEffect;
    }

    private void Start()
    {
        explosiveMaterials = new List<Material>();

        foreach (var explodable in explodableParts)
        {
            var explMaterial = explodable.GetComponent<MeshRenderer>().material;
            explosiveMaterials.Add(explMaterial);
            explMaterial.SetFloat("_explosion", explosionRadius);
        }
        explosionRadius = maxExplosionRadius;
        //explosiveMaterial.SetFloat("_explosion", explosionRadius);
        StartCoroutine(OnFormShipCoroutine());
    }

    private void DoShipExplosionEffect()
    {
        StartCoroutine(OnDeathShipExplosionCoroutine());
    }
    
    private void DoShipReformingEffect()
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
            //explosiveMaterial.SetFloat("_explosion", explosionRadius);

            foreach (var explMaterial in explosiveMaterials)
            {
                explMaterial.SetFloat("_explosion", explosionRadius);
            }
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
            //explosiveMaterial.SetFloat("_explosion", explosionRadius);

            foreach (var explMaterial in explosiveMaterials)
            {
                explMaterial.SetFloat("_explosion", explosionRadius);
            }
        }
        //onExplosionCompletion?.Invoke();
        //explosiveMaterial.SetFloat("_explosion", 0);

        foreach (var explMaterial in explosiveMaterials)
        {
            explMaterial.SetFloat("_explosion", 0);
        }
    }
}
