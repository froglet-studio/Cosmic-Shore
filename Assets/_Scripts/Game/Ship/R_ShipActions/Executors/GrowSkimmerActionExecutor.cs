using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Game;
using UnityEngine;

public class GrowSkimmerActionExecutor : ShipActionExecutorBase, IScaleProvider
{
    [Header("Scene Refs")]
    [SerializeField] Transform skimmerRoot;

    [SerializeField] public Obvious.Soap.ScriptableEventNoParam OnMiniGameTurnEnd;

    IVesselStatus _status;
    float _minWorldZ;
    float _intensity;
    float _worldScaleZ;

    CancellationTokenSource _cts;
    bool _growing;

    public float WorldScaleZ => _worldScaleZ;
    public float CurrentScale => _worldScaleZ;
    public float MinScale => _minWorldZ;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame() => End();

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
        if (!skimmerRoot) return;
        _growing = true;

        End();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        GrowLoopAsync(so, _cts.Token).Forget();
    }

    public void End()
    {
        _growing = false;
        if (_cts == null) return;
        try
        {
            _cts.Cancel();
        }
        catch
        {
            //
        }
        _cts.Dispose();
        _cts = null;
    }

    async UniTaskVoid GrowLoopAsync(GrowSkimmerActionSO so, CancellationToken token)
    {
        float boostApplied = 1f;
        if (so.ApplyBoostWhileGrowing && _status != null)
        {
            boostApplied = so.BoostMultiplier;
            _status.BoostMultiplier *= boostApplied;
            _status.Boosting = true;
        }

        try
        {
            while (_growing)
            {
                StepSize(so.GrowRate, so.MaxSize, increase:true);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            while (skimmerRoot && skimmerRoot.lossyScale.z > MinScale + 0.0001f)
            {
                StepSize(so.ShrinkRate, MinScale, increase:false);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            if (skimmerRoot)
                SetWorldZ(MinScale);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (so.ApplyBoostWhileGrowing && _status != null)
            {
                _status.BoostMultiplier /= Mathf.Max(0.0001f, boostApplied);
                if (_status.BoostMultiplier <= 1f) { _status.BoostMultiplier = 1f; _status.Boosting = false; }
            }
        }
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
