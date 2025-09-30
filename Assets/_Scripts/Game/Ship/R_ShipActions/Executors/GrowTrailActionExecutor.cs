using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public class GrowTrailActionExecutor : ShipActionExecutorBase, IScaleProvider
{
    [Header("Scene Refs")]
    [SerializeField] PrismSpawner spawner;

    IVesselStatus _status;
    float _min;      
    Coroutine _loop;
    bool _growing;

    public float MinScale => _min;
    public float CurrentScale => spawner ? spawner.ZScaler : 1f;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        if (spawner == null)
            spawner = shipStatus?.PrismSpawner;

        _min = spawner ? spawner.ZScaler : 1f;
    }

    public void Begin(GrowTrailActionSO so, IVesselStatus status)
    {
        if (spawner == null) return;
        _growing = true;
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(Loop(so));
    }

    public void End()
    {
        _growing = false;
    }

    IEnumerator Loop(GrowTrailActionSO so)
    {
        while (_growing)
        {
            Step(so.GrowRate, so.MaxSize, increase:true, so);
            yield return null;
        }

        while (spawner && AnyAboveMin(so))
        {
            Step(so.ShrinkRate, _min, increase:false, so);
            yield return null;
        }

        _loop = null;
    }

    void Step(float rate, float limit, bool increase, GrowTrailActionSO so)
    {
        if (spawner == null) return;
        float sign = increase ? +1f : -1f;
        float dt = Time.deltaTime * rate;

        spawner.XScaler = ClampAxis(spawner.XScaler + so.WX * sign * dt, _min, so.MaxSize, increase);
        spawner.YScaler = ClampAxis(spawner.YScaler + so.WY * sign * dt, _min, so.MaxSize, increase);
        spawner.ZScaler = ClampAxis(spawner.ZScaler + so.WZ * sign * dt, _min, so.MaxSize, increase);

        spawner.Gap += (-so.WGap * sign) * dt * 2f;
    }

    float ClampAxis(float val, float min, float max, bool increase)
        => increase ? Mathf.Min(val, max) : Mathf.Max(val, min);

    bool AnyAboveMin(GrowTrailActionSO so)
    {
        if (!spawner) return false;
        if (so.WX > 0f && spawner.XScaler > _min + 0.0001f) return true;
        if (so.WY > 0f && spawner.YScaler > _min + 0.0001f) return true;
        if (so.WZ > 0f && spawner.ZScaler > _min + 0.0001f) return true;
        if (so.WGap > 0f) return spawner.Gap < so.MaxSize - 0.0001f; // inverted logic for gap
        return false;
    }
}