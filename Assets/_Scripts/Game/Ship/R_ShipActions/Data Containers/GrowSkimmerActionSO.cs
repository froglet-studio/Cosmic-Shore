using System;
using CosmicShore.Game;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "GrowSkimmerAction", menuName = "ScriptableObjects/Vessel Actions/Grow Skimmer")]
public class GrowSkimmerActionSO : ShipActionSO
{
    [Header("Size")]
    [SerializeField] ElementalFloat maxSize = new(3f);
    [SerializeField] float growRate = 1.5f;
    [SerializeField] ElementalFloat shrinkRate = new(1f);

    [Header("Boost effect (future hook)")]
    [SerializeField] bool applyBoostWhileGrowing = false;
    [SerializeField] ElementalFloat boostMultiplier = new(1.25f);

    public float MaxSize => maxSize.Value;
    public float GrowRate => growRate;
    public float ShrinkRate => shrinkRate.Value;
    public bool ApplyBoostWhileGrowing => applyBoostWhileGrowing;
    public float BoostMultiplier => boostMultiplier.Value;

    
    bool _isMaxSizeDebuffed;
    float _originalMaxSize;
    
    /// <summary>
    /// Temporarily scales the max skimmer size by sizeMultiplier, then restores it after durationSeconds.
    /// </summary>
    public async UniTaskVoid ApplyMaxSizeDebuff(float sizeMultiplier, float durationSeconds)
    {
        if (_isMaxSizeDebuffed)
            return;

        _isMaxSizeDebuffed = true;
        _originalMaxSize = maxSize.Value;

        var safeMultiplier = Mathf.Max(0.01f, sizeMultiplier);
        maxSize.Value = _originalMaxSize * safeMultiplier;

        await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds));

        maxSize.Value = _originalMaxSize;
        _isMaxSizeDebuffed = false;
    }

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GrowSkimmerActionExecutor>()?.Begin(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<GrowSkimmerActionExecutor>()?.End();
}
