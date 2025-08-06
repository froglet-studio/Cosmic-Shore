using CosmicShore.Game;
using System.Collections;
using UnityEngine;

public class GrowActionBase : ShipAction, IScaleProvider
{
    protected float MinSize;
    [SerializeField] protected ElementalFloat maxSize;
    [SerializeField] protected float growRate;
    [SerializeField] protected ElementalFloat shrinkRate = new ElementalFloat(1);
    [SerializeField] protected GameObject target;

    public bool growing;

    public float MinScale => MinSize;
    private float MaxSize      => maxSize.Value;
    public float CurrentScale => target.transform.lossyScale.z;

    private enum ScaleDir { None, Grow, Shrink }

    private ScaleDir _dir = ScaleDir.None;

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        MinSize = target.transform.lossyScale.z;
    }

    public override void StartAction()
    {
        _dir = ScaleDir.Grow;
    }

    public override void StopAction()
    {
        _dir = ScaleDir.Shrink;
    }
    
    private void LateUpdate()
    {
        if (_dir == ScaleDir.None) return;

        float dt = Time.deltaTime;

        float worldR = target.transform.lossyScale.z; 
        if (_dir == ScaleDir.Grow)
        {
            worldR += growRate * dt;
            if (worldR >= MaxSize) { worldR = MaxSize; _dir = ScaleDir.None; }
        }
        else 
        {
            worldR -= shrinkRate.Value * dt;
            if (worldR <= MinScale) { worldR = MinScale; _dir = ScaleDir.None; }
        }

        float parentScaleZ = 1f;                             
        if (target.transform.parent != null)
            parentScaleZ = target.transform.parent.lossyScale.z;

        float localR = worldR / parentScaleZ;

        target.transform.localScale = Vector3.one * localR;
    }
    
    protected virtual IEnumerator GrowCoroutine(bool growing)
    {
        while (growing && target.transform.localScale.z < maxSize.Value)
        {
            target.transform.localScale += Time.deltaTime * growRate * Vector3.one;
            yield return null;
        }
    }

    protected virtual IEnumerator ReturnToNeutralCoroutine()
    {
        while (target.transform.localScale.z > MinSize)
        {
            target.transform.localScale -= Time.deltaTime * shrinkRate.Value * Vector3.one;
            yield return null;
        }
        target.transform.localScale = MinSize * Vector3.one;
    }
}