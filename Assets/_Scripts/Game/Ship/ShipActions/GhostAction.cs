using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostAction : ShipAction
{
    [SerializeField] float maxDuration;
    List<GameObject> shipGeometries;

    Coroutine intangibilityCoroutine;

    protected override void Start()
    {
        shipGeometries = ship.GetShipGeometries();
    }

    public override void StartAction()
    {
        intangibilityCoroutine = StartCoroutine(TemporaryIntangibilityCoroutine());
    }

    public override void StopAction()
    {
        if (intangibilityCoroutine != null)
        {
            StopCoroutine(intangibilityCoroutine);
            intangibilityCoroutine = null;
        }

        foreach (var geometry in shipGeometries)
            geometry.GetComponent<Collider>().enabled = true;
    }

    IEnumerator TemporaryIntangibilityCoroutine()
    {
        foreach (var geometry in shipGeometries)
            geometry.GetComponent<Collider>().enabled = false;

        yield return new WaitForSeconds(maxDuration);

        foreach (var geometry in shipGeometries)
            geometry.GetComponent<Collider>().enabled = true;
    }
}