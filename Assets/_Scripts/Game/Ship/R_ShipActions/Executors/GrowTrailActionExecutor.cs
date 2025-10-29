using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Game;
using UnityEngine;
using UnityEngine.Serialization;

public class GrowTrailActionExecutor : ShipActionExecutorBase, IScaleProvider
{
    [FormerlySerializedAs("spawner")]
    [Header("Scene Refs")]
    [SerializeField] VesselPrismController controller;

    [SerializeField] public Obvious.Soap.ScriptableEventNoParam OnMiniGameTurnEnd;

    IVesselStatus _status;
    float _min;
    CancellationTokenSource _cts;
    bool _growing;

    public float MinScale => _min;
    public float CurrentScale => controller ? controller.ZScaler : 1f;

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
        if (controller == null)
            controller = shipStatus?.VesselPrismController;

        _min = controller ? controller.ZScaler : 1f;
    }

    public void Begin(GrowTrailActionSO so, IVesselStatus status)
    {
        if (!controller) return;
        _growing = true;

        End();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LoopAsync(so, _cts.Token).Forget();
    }

    public void End()
    {
        _growing = false;
        if (_cts != null)
        {
            try { _cts.Cancel(); } catch { }
            _cts.Dispose();
            _cts = null;
        }
    }

    async UniTaskVoid LoopAsync(GrowTrailActionSO so, CancellationToken token)
    {
        try
        {
            while (_growing)
            {
                Step(so.GrowRate, so.MaxSize, increase:true, so);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            while (controller && AnyAboveMin(so))
            {
                Step(so.ShrinkRate, _min, increase:false, so);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    void Step(float rate, float limit, bool increase, GrowTrailActionSO so)
    {
        if (!controller) return;
        float sign = increase ? +1f : -1f;
        float dt = Time.deltaTime * rate;

        controller.XScaler = ClampAxis(controller.XScaler + so.WX * sign * dt, _min, so.MaxSize, increase);
        controller.YScaler = ClampAxis(controller.YScaler + so.WY * sign * dt, _min, so.MaxSize, increase);
        controller.ZScaler = ClampAxis(controller.ZScaler + so.WZ * sign * dt, _min, so.MaxSize, increase);

        controller.Gap += (-so.WGap * sign) * dt * 2f;
    }

    float ClampAxis(float val, float min, float max, bool increase)
        => increase ? Mathf.Min(val, max) : Mathf.Max(val, min);

    bool AnyAboveMin(GrowTrailActionSO so)
    {
        if (!controller) return false;
        if (so.WX > 0f && controller.XScaler > _min + 0.0001f) return true;
        if (so.WY > 0f && controller.YScaler > _min + 0.0001f) return true;
        if (so.WZ > 0f && controller.ZScaler > _min + 0.0001f) return true;
        if (so.WGap > 0f) return controller.Gap < so.MaxSize - 0.0001f; 
        return false;
    }
}
