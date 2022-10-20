using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipExplosionHandler : MonoBehaviour
{
    [SerializeField] List<GameObject> explodableParts;
    [SerializeField] Material explosiveMaterial;
    [SerializeField] float maxExplosionRadius = 0f;
    [SerializeField] float explosionRate = 12f;
    private float explosionRadius;
    private List<Material> explosiveMaterials;

    public delegate void OnShipExplosionAnimationCompletionEvent();
    public static event OnShipExplosionAnimationCompletionEvent onShipExplosionAnimationCompletion;

    public delegate void OnShipFormationAnimationCompleteEvent();
    public static event OnShipFormationAnimationCompleteEvent OnShipFormationAnimationCompletion;

    private void OnEnable()
    {
        DeathEvents.OnDeathBegin += DoShipExplosionEffect;
        GameManager.onExtendGamePlay += DoShipReformingEffect;
    }

    private void OnDisable()
    {
        DeathEvents.OnDeathBegin -= DoShipExplosionEffect;
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
        HapticController.PlayBlockCollisionHaptics();
        
        while (explosionRadius < maxExplosionRadius )
        {
            yield return null;  // Come back next frame
            explosionRadius += explosionRate * Time.deltaTime;

            foreach (var explMaterial in explosiveMaterials)
            {
                explMaterial.SetFloat("_explosion", explosionRadius);
            }
        }

        onShipExplosionAnimationCompletion?.Invoke();
    }

    public IEnumerator OnFormShipCoroutine()
    {
        while (explosionRadius > 0)
        {
            yield return null;  // Come back next frame
            explosionRadius -= explosionRate * Time.deltaTime;

            foreach (var explMaterial in explosiveMaterials)
            {
                explMaterial.SetFloat("_explosion", explosionRadius);
            }
        }

        foreach (var explMaterial in explosiveMaterials)
        {
            explMaterial.SetFloat("_explosion", 0);
        }

        StartCoroutine(ToggleCollisionCoroutine());
    }

    IEnumerator ToggleCollisionCoroutine()
    {
        yield return new WaitForSeconds(.2f);
        GameManager.Instance.player.ToggleCollision(true);
    }
}