using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public class GrowSkimmerActionExecutor : ShipActionExecutorBase, IScaleProvider
{
    [Header("Scene Refs")]
    [SerializeField] Transform skimmerRoot;

    IVesselStatus _status;
    float _minWorldZ;
    float _intensity;          
    Coroutine _loop;
    bool _growing;
    float _worldScaleZ;
    
    public float WorldScaleZ => _worldScaleZ;
    public float CurrentScale => _worldScaleZ;
    public float MinScale => _minWorldZ;
    
    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        if (skimmerRoot == null)
            skimmerRoot = shipStatus?.ShipTransform;

        _minWorldZ = skimmerRoot ? skimmerRoot.lossyScale.z : 1f;
        _worldScaleZ = _minWorldZ;
    }

    public void Begin(GrowSkimmerActionSO so, IVesselStatus status)
    {
        if (skimmerRoot == null) return;
        _growing = true;
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(GrowLoop(so));
    }

    public void End()
    {
        _growing = false;
    }

    IEnumerator GrowLoop(GrowSkimmerActionSO so)
    {
        float boostApplied = 1f;
        if (so.ApplyBoostWhileGrowing && _status != null)
        {
            boostApplied = so.BoostMultiplier;
            _status.BoostMultiplier *= boostApplied;
            _status.Boosting = true;
        }

        while (_growing)
        {
            StepSize(so.GrowRate, so.MaxSize, increase:true);
            yield return null;
        }

        // ramp back
        while (skimmerRoot && skimmerRoot.lossyScale.z > MinScale + 0.0001f)
        {
            StepSize(so.ShrinkRate, MinScale, increase:false);
            yield return null;
        }
        if (skimmerRoot)
            SetWorldZ(MinScale);

        // remove boost hook
        if (so.ApplyBoostWhileGrowing && _status != null)
        {
            _status.BoostMultiplier /= Mathf.Max(0.0001f, boostApplied);
            if (_status.BoostMultiplier <= 1f) { _status.BoostMultiplier = 1f; _status.Boosting = false; }
        }

        _loop = null;
    }

void StepSize(float rate, float limit, bool increase)
{
    if (!skimmerRoot) return;
    float parentZ = (skimmerRoot.parent ? skimmerRoot.parent.lossyScale.z : 1f);
    float worldZ  = _worldScaleZ <= 0f ? skimmerRoot.lossyScale.z : _worldScaleZ;

    worldZ += (increase ? +1f : -1f) * rate * Time.deltaTime;
    worldZ = increase ? Mathf.Min(worldZ, limit) : Mathf.Max(worldZ, limit);
    _worldScaleZ = worldZ;

    float localZ = worldZ / parentZ;
    skimmerRoot.localScale = Vector3.one * localZ;
}


    void SetWorldZ(float worldZ)
    {
        float parentZ = (skimmerRoot && skimmerRoot.parent) ? skimmerRoot.parent.lossyScale.z : 1f;
        float localZ = worldZ / parentZ;
        skimmerRoot.localScale = Vector3.one * localZ;
    }
    
    public void ResetToMinScale()
    {
        if (!skimmerRoot) return;
        SetWorldZ(MinScale);
    }

}