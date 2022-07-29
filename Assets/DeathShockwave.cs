using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathExplosion : MonoBehaviour
{
    SphereCollider sphereCollider;
    private float initialColliderRadius = 0f;
    private float maxColliderRadius = 50f;
    private float radiusGrowthRate = .01f;

    void Start()
    {
        sphereCollider = GetComponent<Collider>() as SphereCollider;
        sphereCollider.radius = initialColliderRadius;
        sphereCollider.enabled = false;
    }


    private void OnEnable()
    {
        FuelSystem.zeroFuel += OnZeroFuel;
    }

    private void OnDisable()
    {
        FuelSystem.zeroFuel -= OnZeroFuel;
    }

    void OnZeroFuel()
    {
        StartCoroutine(OnZeroFuelCoroutine());

    }

    IEnumerator OnZeroFuelCoroutine()
    {
        sphereCollider.enabled = true;
        while (sphereCollider.radius < maxColliderRadius)
        {
            yield return null;
            sphereCollider.radius += radiusGrowthRate;
        }
        sphereCollider.enabled = false;
    }

}